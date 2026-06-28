using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class RecipientConfiguration : IEntityTypeConfiguration<Recipient>
{
    public void Configure(EntityTypeBuilder<Recipient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Recipients");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.KanjiName).IsRequired().HasMaxLength(128);
        builder.Property(r => r.KanaName).IsRequired().HasMaxLength(128);
        builder.Property(r => r.DateOfBirth).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ConcurrencyToken).IsConcurrencyToken();
        builder.Property(r => r.ArchivedAt);
        builder.Property(r => r.ArchivedBy).HasMaxLength(64);
        builder.Ignore(r => r.IsArchived);
        builder.HasIndex(r => r.ArchivedAt);

        // 障害種別: ComplexProperty で 4 列にフラット展開
        builder.ComplexProperty(r => r.Disabilities, d =>
        {
            d.Property(x => x.Physical).HasColumnName("Disability_Physical").IsRequired();
            d.Property(x => x.Intellectual).HasColumnName("Disability_Intellectual").IsRequired();
            d.Property(x => x.Mental).HasColumnName("Disability_Mental").IsRequired();
            d.Property(x => x.Intractable).HasColumnName("Disability_Intractable").IsRequired();
        });

        // 連絡先
        builder.Property(r => r.PostalCode).HasMaxLength(16);
        builder.Property(r => r.Address).HasMaxLength(256);
        builder.Property(r => r.PhoneNumber).HasMaxLength(32);
        builder.Property(r => r.EmailAddress).HasMaxLength(128);
        builder.Property(r => r.EmergencyContactName).HasMaxLength(128);
        builder.Property(r => r.EmergencyContactRelationship).HasMaxLength(32);
        builder.Property(r => r.EmergencyContactPhone).HasMaxLength(32);
    }
}
