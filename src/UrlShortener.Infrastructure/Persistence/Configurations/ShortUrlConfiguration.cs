using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Domain.ShortUrls;

namespace UrlShortener.Infrastructure.Persistence.Configurations;

public sealed class ShortUrlConfiguration : IEntityTypeConfiguration<ShortUrl>
{
    public void Configure(EntityTypeBuilder<ShortUrl> builder)
    {
        builder.ToTable("ShortUrls");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ShortCode)
            .HasConversion(
                code => code.ToString(),
                value => ShortCode.Create(value))
            .HasMaxLength(7)
            .IsRequired();

        builder.HasIndex(s => s.ShortCode).IsUnique();

        builder.Property(s => s.OriginalUrl)
            .HasConversion(
                url => url.ToString(),
                value => OriginalUrl.Create(value))
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ExpiresAt);
        builder.Property(s => s.IsEnabled).IsRequired();
        builder.Property(s => s.ClickCount).IsRequired();

        builder.Ignore(s => s.DomainEvents);
    }
}
