using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MarketAnalytics.Infrastructure.Data;

namespace MarketAnalytics.Api;

/// <summary>
/// Keeps computed answers (analytics, alerts, backtests) until the data behind them changes. Every price update writes
/// update-log rows and every news run adds articles, from the Worker or the API, so the newest of each is the data's
/// version: a new version means a fresh computation. A time limit also refreshes answers that depend on "now"
/// (the news mood covers the last 7 days).
/// </summary>
public class DataCache
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;
    private readonly ApplicationDbContext _db;

    public DataCache(IMemoryCache cache, ApplicationDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public async Task<T> GetOrComputeAsync<T>(string key, Func<Task<T>> compute, CancellationToken cancellationToken)
    {
        var fullKey = $"{key}|{await VersionAsync(cancellationToken)}";
        if (_cache.TryGetValue(fullKey, out T? cached) && cached is not null)
            return cached;

        var value = await compute();
        if (value is not null)
            _cache.Set(fullKey, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = MaxAge, Size = 1 });
        return value;
    }

    private async Task<string> VersionAsync(CancellationToken cancellationToken)
    {
        var lastUpdate = await _db.DataUpdateLogs.MaxAsync(l => (int?)l.Id, cancellationToken);
        var lastNews = await _db.NewsArticles.MaxAsync(a => (int?)a.Id, cancellationToken);
        return $"{lastUpdate}-{lastNews}";
    }
}
