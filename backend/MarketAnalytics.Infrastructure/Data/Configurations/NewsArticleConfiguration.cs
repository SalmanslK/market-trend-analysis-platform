using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MarketAnalytics.Domain.Entities;

namespace MarketAnalytics.Infrastructure.Data.Configurations;

/// <summary>
/// Entity Framework configuration for NewsArticle entity.
/// </summary>
public class NewsArticleConfiguration : IEntityTypeConfiguration<NewsArticle>
{
    // Google News redirect links can be very long
    public const int MaxUrlLength = 2000;
    public const int MaxTitleLength = 500;
    public const int MaxSourceLength = 100;

    public void Configure(EntityTypeBuilder<NewsArticle> builder)
    {
        builder.ToTable("NewsArticles");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Source)
            .IsRequired()
            .HasMaxLength(MaxSourceLength);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(MaxTitleLength);

        builder.Property(n => n.Summary)
            .HasMaxLength(1000);

        builder.Property(n => n.Url)
            .IsRequired()
            .HasMaxLength(MaxUrlLength);

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
