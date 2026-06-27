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
    }
}
