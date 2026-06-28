using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WorkRecordConfiguration : IEntityTypeConfiguration<WorkRecord>
{
    public void Configure(EntityTypeBuilder<WorkRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WorkRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RecipientId).IsRequired();
        builder.Property(r => r.WorkDate).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        builder.Property(r => r.OriginId);
        builder.Property(r => r.WorkedMinutes);
        builder.Property(r => r.PieceCount);
        builder.Property(r => r.PieceUnitYen);
        builder.Property(r => r.Points);
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ConcurrencyToken);
        builder.HasIndex(r => r.OriginId);
        // 多重 New 防止（DailyRecord と同じ partial unique index 戦略）
        // NOTE(A3): RecordKind.New = 1 のため "Kind" = 1
        builder.HasIndex(r => new { r.RecipientId, r.WorkDate })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_WorkRecords_RecipientId_WorkDate_NewOnly");
    }
}
