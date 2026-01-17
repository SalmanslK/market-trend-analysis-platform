using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for Instrument entity.
/// </summary>
public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("Instruments");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Symbol)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(i => i.Symbol)
            .IsUnique();

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.DataSourceUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .IsRequired();

        // Relationships
        builder.HasMany(i => i.PricePoints)
            .WithOne(p => p.Instrument)
            .HasForeignKey(p => p.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.UpdateLogs)
            .WithOne(u => u.Instrument)
            .HasForeignKey(u => u.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}