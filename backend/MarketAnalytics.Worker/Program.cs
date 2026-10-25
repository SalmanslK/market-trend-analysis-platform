using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;
using MarketAnalytics.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Database configuration (the password lives in user secrets, shared by Api and Worker)
builder.Configuration.AddUserSecrets<Program>(optional: true);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=MarketAnalytics;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register HTTP clients for data fetchers
builder.Services.AddHttpClient<JsonApiDataFetcher>();
builder.Services.AddHttpClient<HtmlScraperDataFetcher>();

// Register data fetchers
builder.Services.AddScoped<JsonApiDataFetcher>();
builder.Services.AddScoped<HtmlScraperDataFetcher>();
builder.Services.AddScoped<InstrumentDataFetcherFactory>();

// Register background service
builder.Services.AddHostedService<DataUpdateBackgroundService>();

var host = builder.Build();
host.Run();