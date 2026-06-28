using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageSettingsConfiguration : IEntityTypeConfiguration<WageSettings>
{
    public void Configure(EntityTypeBuilder<WageSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.OfficeId).IsRequired();
        // DateRange は Certificate.Validity と同じく DateRangeJson で単一文字列列に展開
        builder.Property(s => s.Period)
            .HasConversion(
                v => DateRangeJson.Serialize(v),
                s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("Period");
        builder.Property(s => s.Method).HasConversion<int>().IsRequired();
        builder.Property(s => s.Rounding).HasConversion<int>().IsRequired();
        builder.Property(s => s.Remainder).HasConversion<int>().IsRequired();
        builder.Property(s => s.FiscalYearStartMonth).IsRequired();
        builder.Property(s => s.FixedDailyYen);
        builder.Property(s => s.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(s => s.OfficeId);
    }
}
