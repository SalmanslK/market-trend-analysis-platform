using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for NewsArticle entity.
/// </summary>
public class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.ToTable("NewsArticles");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Source)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Summary)
            .HasMaxLength(1000);

        builder.Property(n => n.Url)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.Topics)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.Symbols)
            .IsRequired()
            .HasMaxLength(200);

        // The same article must only be stored once, however often the feeds are polled
        builder.HasIndex(n => n.Url)
            .IsUnique();

        // News is always queried newest first, usually for a recent window
        builder.HasIndex(n => n.PublishedAt);
    }
}
