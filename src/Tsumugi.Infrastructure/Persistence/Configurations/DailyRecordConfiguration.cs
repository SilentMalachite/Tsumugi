using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class DailyRecordConfiguration : IEntityTypeConfiguration<DailyRecord>
{
    public void Configure(EntityTypeBuilder<DailyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DailyRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RecipientId).IsRequired();
        // partial unique index: 同一 (RecipientId, ServiceDate) の Kind=New（=1）を DB レベルで一意化する（ADR 0015）
        builder.HasIndex(r => new { r.RecipientId, r.ServiceDate })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_DailyRecords_RecipientId_ServiceDate_NewOnly");
        builder.HasIndex(r => r.OriginId);
        builder.Property(r => r.ServiceDate).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        // OriginId は nullable — IsRequired() は不要
        builder.Property(r => r.OriginId);
        builder.Property(r => r.Attendance).HasConversion<int>().IsRequired();
        builder.Property(r => r.Transport).HasConversion<int>().IsRequired();
        builder.Property(r => r.MealProvided).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.ServiceStartTime);
        builder.Property(r => r.ServiceEndTime);
        builder.Property(r => r.SpecialVisitSupportMinutes);
        builder.Property(r => r.OffsiteSupportApplied);
        builder.Property(r => r.MedicalCoordinationType).HasConversion<int>().IsRequired();
        builder.Property(r => r.TrialUseSupportType).HasConversion<int>().IsRequired();
        builder.Property(r => r.RegionalCollaborationApplied);
        builder.Property(r => r.IntensiveSupportApplied);
        builder.Property(r => r.EmergencyAdmissionApplied);
        builder.Property(r => r.RecipientConfirmation).HasConversion<int>().IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        // 取引記録は更新しないため ConcurrencyToken は IsConcurrencyToken() しない。列としては存在する。
        builder.Property(r => r.ConcurrencyToken);
    }
}
