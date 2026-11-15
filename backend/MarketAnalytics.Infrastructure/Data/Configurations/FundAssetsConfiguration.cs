using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

public class FundAssetsConfiguration : IEntityTypeConfiguration<FundAssets>
{
    public void Configure(EntityTypeBuilder<FundAssets> builder)
    {
        builder.ToTable("FundAssets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Rupees)
            .HasPrecision(20, 2);

        builder.HasOne(a => a.Instrument)
            .WithMany()
            .HasForeignKey(a => a.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // One size per fund per month
        builder.HasIndex(a => new { a.InstrumentId, a.Month })
            .IsUnique();
    }
}
