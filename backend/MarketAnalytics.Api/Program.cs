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

// Unhandled errors return a short JSON problem description instead of a stack trace
builder.Services.AddProblemDetails();

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
builder.Services.AddScoped<InstrumentDataFetcherFactory>();

// Register application services
builder.Services.AddScoped<IResamplingService, ResamplingService>();
builder.Services.AddScoped<ITimeframeFilterService, TimeframeFilterService>();
builder.Services.AddScoped<IIndicatorCalculationService, IndicatorCalculationService>();
builder.Services.AddScoped<IPriceDataProcessingService, PriceDataProcessingService>();
builder.Services.AddScoped<IMarketAnalyticsService, MarketAnalyticsService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IBacktestService, BacktestService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();

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