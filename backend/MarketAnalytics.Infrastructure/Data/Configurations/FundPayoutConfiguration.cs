using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for FundPayout entity.
/// </summary>
public class FundPayoutConfiguration : IEntityTypeConfiguration<FundPayout>
{
    public void Configure(EntityTypeBuilder<FundPayout> builder)
    {
        builder.ToTable("FundPayouts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AmountPerUnit)
            .HasPrecision(18, 4);

        builder.HasOne(p => p.Instrument)
            .WithMany()
            .HasForeignKey(p => p.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // A fund pays at most once per listed date
        builder.HasIndex(p => new { p.InstrumentId, p.PayoutDate })
            .IsUnique();
    }
}
