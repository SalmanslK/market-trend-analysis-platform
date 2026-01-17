using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;
using MarketAnalytics.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=MarketAnalytics;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repositories
builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
builder.Services.AddScoped<IPricePointRepository, PricePointRepository>();

// Register HTTP clients for data fetchers
builder.Services.AddHttpClient<JsonApiDataFetcher>();
builder.Services.AddHttpClient<HtmlScraperDataFetcher>();

// Register data fetchers
builder.Services.AddScoped<JsonApiDataFetcher>();
builder.Services.AddScoped<HtmlScraperDataFetcher>();
builder.Services.AddScoped<InstrumentDataFetcherFactory>();

// Register application services
builder.Services.AddScoped<IResamplingService, ResamplingService>();
builder.Services.AddScoped<ITimeframeFilterService, TimeframeFilterService>();
builder.Services.AddScoped<IIndicatorCalculationService, IndicatorCalculationService>();
builder.Services.AddScoped<IPriceDataProcessingService, PriceDataProcessingService>();

// CORS configuration for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();