using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarketAnalytics.Application.DTOs;
using MarketAnalytics.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketAnalytics.Infrastructure.Brief;

/// <summary>
/// Writes the market brief with Claude Code on this computer (print mode), so it runs on the user's Claude plan with
/// no API key or separate charge. Claude Code gets only web search and web fetch, no files, no project settings and
/// no saved session. Brief:ClaudePath overrides where the claude executable is (default: "claude" on the PATH).
/// </summary>
public partial class ClaudeCodeBriefWriter : IBriefWriter
{
    public const string WrittenBy = "Claude Code (your Claude plan)";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    private readonly string _claude;
    private readonly ILogger<ClaudeCodeBriefWriter> _logger;
    private readonly Lazy<bool> _available;

    public ClaudeCodeBriefWriter(IConfiguration configuration, ILogger<ClaudeCodeBriefWriter> logger)
    {
        _claude = configuration["Brief:ClaudePath"] is { Length: > 0 } path ? path : "claude";
        _logger = logger;
        _available = new Lazy<bool>(() => Run(["--version"], null, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult().ExitCode == 0);
    }

    public bool IsConfigured => _available.Value;

    public async Task<WrittenBrief> WriteAsync(string factsJson, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new BriefUnavailableException("Claude Code was not found on this computer. Install it and sign in with your Claude account, then try again.");

        // An empty folder, so Claude Code reads none of this project's files or settings
        var workDir = Directory.CreateTempSubdirectory("market-brief-").FullName;
        var systemFile = Path.Combine(workDir, "system.txt");
        await File.WriteAllTextAsync(systemFile, SystemPrompt, cancellationToken);
        try
        {
            var (exitCode, output, error) = await Run(
                [
                    "-p", "--output-format", "json",
                    "--system-prompt-file", systemFile,
                    "--tools", "WebSearch,WebFetch",
                    "--allowedTools", "WebSearch,WebFetch",
                    "--setting-sources", "local",
                    "--strict-mcp-config",
                    "--no-session-persistence",
                ],
                $"Write today's brief from these facts.\n\n<facts>\n{factsJson}\n</facts>",
                Timeout, workDir, cancellationToken);

            var result = ParseResult(output);
            if (exitCode != 0 || result.IsError || string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogError("Claude Code failed ({ExitCode}): {Error} {Output}", exitCode, error, output.Length > 2000 ? output[..2000] : output);
                throw new BriefUnavailableException(FriendlyError(result.Text ?? error));
            }

            var (text, sources) = SplitSources(result.Text!);
            return new WrittenBrief(text, sources, WrittenBy, 0m);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Reads Claude Code's JSON result: { "result": "...", "is_error": false, ... }.</summary>
    public static (string? Text, bool IsError) ParseResult(string output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;
            var text = root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            var isError = root.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True;
            return (text, isError);
        }
        catch (JsonException)
        {
            return (null, true);
        }
    }

    [GeneratedRegex(@"https?://[^\s)\]>]+")]
    private static partial Regex UrlPattern();

    /// <summary>
    /// Takes the closing "## Sources" section off the brief and turns its "- Title - URL" lines into a list.
    /// </summary>
    public static (string Text, List<BriefSourceDto> Sources) SplitSources(string brief)
    {
        var marker = Regex.Match(brief, @"^##\s*Sources\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!marker.Success)
            return (brief.Trim(), new());

        var sources = new List<BriefSourceDto>();
        foreach (var line in brief[(marker.Index + marker.Length)..].Split('\n'))
        {
            var url = UrlPattern().Match(line);
            if (!url.Success || sources.Any(s => s.Url == url.Value))
                continue;
            var title = line[..url.Index].Trim().TrimStart('-', '*', ' ').TrimEnd('-', '–', '—', ':', '(', ' ');
            sources.Add(new BriefSourceDto { Title = title.Length > 0 ? title : url.Value, Url = url.Value });
        }
        return (brief[..marker.Index].Trim(), sources);
    }

    private static string FriendlyError(string? detail)
    {
        var d = detail ?? string.Empty;
        if (d.Contains("limit", StringComparison.OrdinalIgnoreCase))
            return "Your Claude plan's usage limit has been reached for now. Try again later.";
        if (d.Contains("login", StringComparison.OrdinalIgnoreCase) || d.Contains("auth", StringComparison.OrdinalIgnoreCase))
            return "Claude Code is not signed in. Open Claude Code and sign in with your Claude account, then try again.";
        return "Claude Code could not write the brief this time. Try again in a few minutes.";
    }

    private async Task<(int ExitCode, string Output, string Error)> Run(
        string[] args, string? input, TimeSpan timeout, string? workDir = null, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo(_claude)
        {
            RedirectStandardInput = input != null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir ?? Path.GetTempPath(),
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return (-1, string.Empty, ex.Message);
        }

        if (input != null)
        {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
        }
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timer.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timer.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (cancellationToken.IsCancellationRequested)
                throw;
            throw new BriefUnavailableException("Writing the brief took too long and was stopped. Try again.");
        }
        return (process.ExitCode, await output, await error);
    }

    private const string SystemPrompt = """
        You are a senior financial analyst writing a market brief for one reader in Pakistan who has no finance
        background. They hold or are considering Meezan (Islamic) mutual funds, gold and cash, and want to understand
        what is going on and what might come next, so they can make their own informed decisions.

        Your material:
        - The facts block: figures the reader's app computed from daily prices up to the date shown, its trading
          volume and fund flows, its news headlines, and "whatFollowedSimilarTrends": for each main instrument, what
          happened over the next 1 month, 6 months and 1 year after earlier days in the same trend state as today.
        - Web search, which you may use up to five times, for the world economy and markets and for Pakistan's
          economy (inflation, the State Bank's policy rate, reserves, the IMF programme, oil). The facts block has no
          data on these. Prefer recent, reputable sources.

        Rules that matter more than style:
        - Use only numbers from the facts block or from pages you found. Never invent a figure, date or event. If
          something is unknown, say it is unknown.
        - Talk about the future only through history and scenarios. For the short and long term, use the
          whatFollowedSimilarTrends numbers: the share of past cases that ended higher, the middle outcome and the
          range (one in ten did worse than the low figure, one in ten better than the high one), and how many
          separate spells they come from; compare with all days. Then describe what could make things go better
          or worse, and say plainly that nobody can reliably predict markets over weeks or months, including you.
        - The reader's own app tested its signals; the researchFindings in the facts say what held up. Do not
          present technical or news signals as forecasts. You may mention the "stronger than cash" check as the one
          tested rule, with its caveats.
        - Do not tell the reader to buy or sell anything. You may explain what the figures mean for different
          kinds of investor (for example someone who needs the money within a year compared with someone investing
          for ten years) and general principles such as time horizon, spreading money across stocks, gold and cash,
          and keeping emergency savings in cash.
        - Explain every term the first time you use it, in plain words. Short sentences. Rupees as "Rs".

        Format, using only this small Markdown subset: "## " section headings, plain paragraphs, "- " bullets and
        **bold**. No tables, links or other Markdown. Sections, in this order:
        ## Today in one paragraph
        ## Pakistan stocks
        ## Gold
        ## The rupee and the dollar
        ## Cash and interest rates
        ## The wider world
        ## What history says about the coming weeks and months
        ## What history says about the next year and beyond
        ## What to watch
        ## What this means for different investors
        ## Sources
        Under Sources, list each web page you used as "- Title - URL", one per line; write "- None" if you used none.
        Aim for 900 to 1,300 words before Sources. Write the brief directly, with no preamble.
        """;
}
