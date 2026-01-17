using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for DataUpdateLog entity.
/// </summary>
public class DataUpdateLogConfiguration : IEntityTypeConfiguration<DataUpdateLog>
{
    public void Configure(EntityTypeBuilder<DataUpdateLog> builder)
    {
        builder.ToTable("DataUpdateLogs");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.InstrumentId)
            .IsRequired();

        builder.Property(u => u.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // Index on InstrumentId for efficient querying of update logs
        builder.HasIndex(u => u.InstrumentId);

        // Index on CreatedAt for chronological queries
        builder.HasIndex(u => u.CreatedAt);

        // Relationship
        builder.HasOne(u => u.Instrument)
            .WithMany(i => i.UpdateLogs)
            .HasForeignKey(u => u.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}