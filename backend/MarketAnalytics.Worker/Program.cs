using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;
using MarketAnalytics.Infrastructure.News;
using MarketAnalytics.Infrastructure.Updates;
using MarketAnalytics.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Database configuration (the password lives in user secrets, shared by Api and Worker)
builder.Configuration.AddUserSecrets<Program>(optional: true);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=MarketAnalytics;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register data fetchers as typed HTTP clients
builder.Services.AddHttpClient<JsonApiDataFetcher>();
builder.Services.AddHttpClient<HtmlScraperDataFetcher>();
builder.Services.AddHttpClient<CurrencyApiDataFetcher>();
builder.Services.AddHttpClient<RssNewsFetcher>();
builder.Services.AddHttpClient<FundPayoutFetcher>();
builder.Services.AddScoped<InstrumentDataFetcherFactory>();
builder.Services.AddScoped<MarketDataUpdater>();
builder.Services.AddScoped<NewsUpdater>();

// "--import-csv <directory>" runs a one-off historical CSV import instead of the scheduled updates
var importDirectory = builder.Configuration["import-csv"];
if (!string.IsNullOrWhiteSpace(importDirectory))
{
    builder.Services.AddScoped<CsvPriceImporter>();
    using var importHost = builder.Build();
    using var scope = importHost.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CsvPriceImporter>().ImportDirectoryAsync(importDirectory);
    return;
}

// "--backfill-news <date>" downloads older news from that date instead of running the scheduled updates
var backfillFrom = builder.Configuration["backfill-news"];
if (!string.IsNullOrWhiteSpace(backfillFrom))
{
    builder.Services.AddScoped<NewsBackfill>();
    using var backfillHost = builder.Build();
    using var scope = backfillHost.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NewsBackfill>().RunAsync(DateTime.SpecifyKind(DateTime.Parse(backfillFrom), DateTimeKind.Utc));
    return;
}

// Register background services (prices every 6 hours, news hourly)
builder.Services.AddHostedService<DataUpdateBackgroundService>();
builder.Services.AddHostedService<NewsUpdateBackgroundService>();

var host = builder.Build();
host.Run();