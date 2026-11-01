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

// Register data fetchers as typed HTTP clients
builder.Services.AddHttpClient<JsonApiDataFetcher>();
builder.Services.AddHttpClient<HtmlScraperDataFetcher>();
builder.Services.AddHttpClient<GoldApiDataFetcher>();
builder.Services.AddScoped<InstrumentDataFetcherFactory>();

// Register background service
builder.Services.AddHostedService<DataUpdateBackgroundService>();

var host = builder.Build();
host.Run();