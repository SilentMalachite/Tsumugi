using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Offices");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OfficeNumber).IsRequired().HasMaxLength(32);
        builder.HasIndex(o => o.OfficeNumber).IsUnique();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(128);
        builder.Property(o => o.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.ConcurrencyToken).IsConcurrencyToken();
    }
}
