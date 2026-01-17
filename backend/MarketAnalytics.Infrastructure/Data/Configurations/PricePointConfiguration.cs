using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for PricePoint entity.
/// Implements data integrity rules: unique (InstrumentId, DateTime), positive prices, date range validation.
/// </summary>
public class PricePointConfiguration : IEntityTypeConfiguration<PricePoint>
{
    public void Configure(EntityTypeBuilder<PricePoint> builder)
    {
        builder.ToTable("PricePoints");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.InstrumentId)
            .IsRequired();

        builder.Property(p => p.DateTime)
            .IsRequired();

        // Price with high precision (18,6) to match financial data requirements
        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 6);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Composite unique index to prevent duplicate timestamps for same instrument
        // This matches Python logic: drop_duplicates("datetime", keep="last")
        builder.HasIndex(p => new { p.InstrumentId, p.DateTime })
            .IsUnique();

        // Index on DateTime for efficient filtering by date range
        builder.HasIndex(p => p.DateTime);

        // Index on InstrumentId for efficient queries by instrument
        builder.HasIndex(p => p.InstrumentId);

        // Composite index for efficient queries: get latest data for an instrument
        builder.HasIndex(p => new { p.InstrumentId, p.DateTime })
            .HasDatabaseName("IX_PricePoints_InstrumentId_DateTime");

        // Relationship
        builder.HasOne(p => p.Instrument)
            .WithMany(i => i.PricePoints)
            .HasForeignKey(p => p.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}