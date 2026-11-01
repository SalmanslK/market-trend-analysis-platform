using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Domain.Entities;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Worker;

/// <summary>
/// One-off import of historical prices from CSV files, e.g. the psx-trading-analysis data/prices folder.
/// Each file is named &lt;SYMBOL&gt;.csv and starts with a header whose first two columns are date and price
/// (extra columns such as Open/High/Low are ignored). Only dates not already stored are inserted,
/// so prices fetched from the live data sources always take precedence.
/// Run with: dotnet run -- --import-csv &lt;directory&gt;
/// </summary>
public class CsvPriceImporter
{
    private static readonly DateTime MinValidDate = new(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy", "MMM dd, yyyy" };

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CsvPriceImporter> _logger;

    public CsvPriceImporter(ApplicationDbContext dbContext, ILogger<CsvPriceImporter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ImportDirectoryAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"CSV import directory not found: {directory}");

        var instruments = await _dbContext.Instruments.ToListAsync(cancellationToken);

        foreach (var instrument in instruments)
        {
            var path = Path.Combine(directory, $"{instrument.Symbol}.csv");
            if (!File.Exists(path))
            {
                _logger.LogInformation("{Symbol}: no CSV file, skipped", instrument.Symbol);
                continue;
            }

            var rows = ReadPrices(path);

            var existingDates = (await _dbContext.PricePoints
                    .Where(p => p.InstrumentId == instrument.Id)
                    .Select(p => p.DateTime)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var now = DateTime.UtcNow;
            var newPoints = rows
                .Where(r => !existingDates.Contains(r.Date))
                .Select(r => new PricePoint { InstrumentId = instrument.Id, DateTime = r.Date, Price = r.Price, CreatedAt = now })
                .ToList();

            await _dbContext.PricePoints.AddRangeAsync(newPoints, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            _logger.LogInformation("{Symbol}: {Read} valid rows in CSV, {Inserted} inserted, {Skipped} already stored",
                instrument.Symbol, rows.Count, newPoints.Count, rows.Count - newPoints.Count);
        }
    }

    private List<(DateTime Date, decimal Price)> ReadPrices(string path)
    {
        var today = DateTime.UtcNow.Date;
        var prices = new Dictionary<DateTime, decimal>();

        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var columns = line.Split(',');
            if (columns.Length < 2)
                continue;

            if (!DateTime.TryParseExact(columns[0].Trim().Trim('"'), DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                continue;

            if (!decimal.TryParse(columns[1].Trim().Trim('"'), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                continue;

            // Same cleaning rules as the scheduled update: positive prices, dates between 1990 and today
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            if (price <= 0 || date < MinValidDate || date > today)
                continue;

            // Keep the last row for a duplicated date (matches drop_duplicates(keep="last"))
            prices[date] = price;
        }

        return prices.Select(p => (p.Key, p.Value)).OrderBy(p => p.Key).ToList();
    }
}
