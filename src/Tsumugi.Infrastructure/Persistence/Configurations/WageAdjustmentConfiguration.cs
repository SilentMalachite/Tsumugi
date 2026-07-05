using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageAdjustmentConfiguration : IEntityTypeConfiguration<WageAdjustment>
{
    public void Configure(EntityTypeBuilder<WageAdjustment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageAdjustments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.RecipientId).IsRequired();
        builder.Property(x => x.YearMonth)
            .HasConversion(v => v.ToInt(), v => YearMonth.FromInt(v))
            .IsRequired();
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.AmountYen).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.OriginId);
        builder.Property(x => x.Note).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ConcurrencyToken);
        // partial unique index: 同一 (OfficeId, RecipientId, YearMonth, Type) の Kind=New（=1）を DB レベルで一意化する（ADR 0018）
        builder.HasIndex(x => new { x.OfficeId, x.RecipientId, x.YearMonth, x.Type })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_WageAdjustments_OfficeRecipientYmType_NewOnly");
    }
}
