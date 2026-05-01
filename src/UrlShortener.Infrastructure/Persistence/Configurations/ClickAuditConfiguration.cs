using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.Domain.ClickAudits;

namespace UrlShortener.Infrastructure.Persistence.Configurations;

public sealed class ClickAuditConfiguration : IEntityTypeConfiguration<ClickAudit>
{
    public void Configure(EntityTypeBuilder<ClickAudit> builder)
    {
        builder.ToTable("ClickAudits");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ShortUrlId).IsRequired();
        builder.HasIndex(c => c.ShortUrlId);

        builder.Property(c => c.ShortCodeValue)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(c => c.ClickedAt).IsRequired();
        builder.Property(c => c.UserAgent).HasMaxLength(512);
        builder.Property(c => c.IpAddress).HasMaxLength(45);
    }
}
