using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class DisabilityCertificateConfiguration : IEntityTypeConfiguration<DisabilityCertificate>
{
    public void Configure(EntityTypeBuilder<DisabilityCertificate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DisabilityCertificates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RecipientId).IsRequired();
        builder.HasIndex(c => c.RecipientId);
        builder.Property(c => c.Type).IsRequired().HasConversion<int>();
        builder.Property(c => c.Grade).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Subtype).HasMaxLength(32);
        builder.Property(c => c.IssuedDate).IsRequired();
        builder.Property(c => c.IssuingAuthority).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CertificateNumber).HasMaxLength(64);
        builder.Property(c => c.Notes).HasMaxLength(512);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ConcurrencyToken).IsConcurrencyToken();
    }
}
