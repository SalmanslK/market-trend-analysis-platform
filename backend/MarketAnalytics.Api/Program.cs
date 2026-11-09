using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Application.Interfaces.IRepositories;
using MarketAnalytics.Application.Interfaces.Services;
using MarketAnalytics.Application.Services;
using MarketAnalytics.Infrastructure.Data;
using MarketAnalytics.Infrastructure.DataFetchers;
using MarketAnalytics.Infrastructure.News;
using MarketAnalytics.Infrastructure.Repositories;
using MarketAnalytics.Infrastructure.Updates;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Unhandled errors return a short JSON problem description instead of a stack trace
builder.Services.AddProblemDetails();

// Optional parts of the app (appsettings.json "Features"); news is switched on there, and off if the setting is missing
builder.Services.Configure<MarketAnalytics.Api.FeatureSettings>(builder.Configuration.GetSection("Features"));

// Backtest and price responses are large JSON; compression cuts them to a fraction
builder.Services.AddResponseCompression();

// Database configuration (the password lives in user secrets, shared by Api and Worker)
builder.Configuration.AddUserSecrets<Program>(optional: true);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=MarketAnalytics;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register repositories
builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
builder.Services.AddScoped<IPricePointRepository, PricePointRepository>();
builder.Services.AddScoped<INewsRepository, NewsRepository>();
builder.Services.AddScoped<IFundPayoutRepository, FundPayoutRepository>();

// Register data fetchers as typed HTTP clients
builder.Services.AddHttpClient<JsonApiDataFetcher>();
builder.Services.AddHttpClient<HtmlScraperDataFetcher>();
builder.Services.AddHttpClient<CurrencyApiDataFetcher>();
builder.Services.AddHttpClient<FundPayoutFetcher>();
builder.Services.AddHttpClient<RssNewsFetcher>();
builder.Services.AddScoped<InstrumentDataFetcherFactory>();

// The same updates the Worker runs on its schedule, for the "Check for new data" button
builder.Services.AddScoped<MarketDataUpdater>();
builder.Services.AddScoped<NewsUpdater>();
builder.Services.AddScoped<SignalRecorder>();

// Register application services
builder.Services.AddScoped<IResamplingService, ResamplingService>();
builder.Services.AddScoped<ITimeframeFilterService, TimeframeFilterService>();
builder.Services.AddScoped<IIndicatorCalculationService, IndicatorCalculationService>();
builder.Services.AddScoped<IPriceDataProcessingService, PriceDataProcessingService>();
builder.Services.AddScoped<IMarketAnalyticsService, MarketAnalyticsService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IBacktestService, BacktestService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
builder.Services.AddScoped<IAlertService, AlertService>();

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

// Bring the database up to date, so pulling new code doesn't need a manual "dotnet ef database update".
// Only the API migrates; start-app starts the Worker after the API is ready.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline
app.UseExceptionHandler();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactFrontend");
app.UseAuthorization();
app.MapControllers();

// Health check: whether the database answers and how fresh the newest price is
app.MapGet("/api/health", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    try
    {
        var latestPrice = await db.PricePoints.MaxAsync(p => (DateTime?)p.DateTime, cancellationToken);
        return Results.Ok(new { status = "Healthy", database = "Connected", latestPrice });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "Unhealthy", database = ex.GetType().Name }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();