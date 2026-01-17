using Microsoft.EntityFrameworkCore;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the Market Analytics application.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Instrument> Instruments { get; set; }
    public DbSet<PricePoint> PricePoints { get; set; }
    public DbSet<DataUpdateLog> DataUpdateLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Seed data for instruments
        var seedInstruments = MarketAnalytics.Infrastructure.Data.SeedData.InstrumentSeedData.GetSeedInstruments();
        modelBuilder.Entity<Instrument>().HasData(seedInstruments);
    }
}