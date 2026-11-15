using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for SignalSnapshot entity.
/// </summary>
public class SignalSnapshotConfiguration : IEntityTypeConfiguration<SignalSnapshot>
{
    public void Configure(EntityTypeBuilder<SignalSnapshot> builder)
    {
        builder.ToTable("SignalSnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Price)
            .HasPrecision(18, 6);

        builder.Property(s => s.DailyAction).HasMaxLength(16);
        builder.Property(s => s.SteadyAction).HasMaxLength(16);
        builder.Property(s => s.NewsAction).HasMaxLength(16);
        builder.Property(s => s.CombinedAction).HasMaxLength(16);
        builder.Property(s => s.Trend).HasMaxLength(16);

        builder.HasOne(s => s.Instrument)
            .WithMany()
            .HasForeignKey(s => s.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // One reading per instrument per price date
        builder.HasIndex(s => new { s.InstrumentId, s.Date })
            .IsUnique();
    }
}
