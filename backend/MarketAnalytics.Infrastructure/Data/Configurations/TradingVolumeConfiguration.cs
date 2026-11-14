using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

public class TradingVolumeConfiguration : IEntityTypeConfiguration<TradingVolume>
{
    public void Configure(EntityTypeBuilder<TradingVolume> builder)
    {
        builder.ToTable("TradingVolumes");
        builder.HasKey(v => v.Id);

        builder.HasOne(v => v.Instrument)
            .WithMany()
            .HasForeignKey(v => v.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // One volume per instrument per trading day
        builder.HasIndex(v => new { v.InstrumentId, v.Date })
            .IsUnique();
    }
}
