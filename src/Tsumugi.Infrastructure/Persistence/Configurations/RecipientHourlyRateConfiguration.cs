using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class RecipientHourlyRateConfiguration : IEntityTypeConfiguration<RecipientHourlyRate>
{
    public void Configure(EntityTypeBuilder<RecipientHourlyRate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("RecipientHourlyRates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.RecipientId).IsRequired();

        // DateRange は Certificate.Validity / WageSettings.Period と同じく DateRangeJson で単一 TEXT 列に格納
        builder.Property(x => x.Period)
            .HasConversion(
                v => DateRangeJson.Serialize(v),
                s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("PeriodJson");

        // partial unique index のために Period.Start を専用 shadow 列として保持する
        // RecipientHourlyRateRepository.AddAsync でセットされる
        builder.Property<DateOnly>("PeriodStart").HasColumnName("PeriodStart").IsRequired();

        builder.Property(x => x.HourlyYen).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.OriginId);
        builder.Property(x => x.Note).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ConcurrencyToken);

        // partial unique index: 同一 (OfficeId, RecipientId, PeriodStart) の Kind=New を DB レベルで一意化する
        builder.HasIndex("OfficeId", "RecipientId", "PeriodStart")
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_RecipientHourlyRates_OfficeRecipientPeriodStart_NewOnly");
    }
}
