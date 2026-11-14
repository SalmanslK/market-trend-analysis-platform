using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

public class DailyBriefConfiguration : IEntityTypeConfiguration<DailyBrief>
{
    public void Configure(EntityTypeBuilder<DailyBrief> builder)
    {
        builder.ToTable("DailyBriefs");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Model).HasMaxLength(64);
        builder.Property(b => b.CostUsd).HasPrecision(10, 4);
        builder.HasIndex(b => b.CreatedAt);
    }
}
