using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageStatementConfiguration : IEntityTypeConfiguration<WageStatement>
{
    public void Configure(EntityTypeBuilder<WageStatement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageStatements");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.OfficeId).IsRequired();
        // YearMonth は YYYYMM 形式の int 単一列に変換（WageFund と同パターン）
        builder.Property(s => s.Month)
            .HasConversion(v => v.ToInt(), v => Tsumugi.Domain.ValueObjects.YearMonth.FromInt(v))
            .HasColumnName("MonthKey")
            .IsRequired();
        builder.Property(s => s.RecipientId).IsRequired();
        builder.Property(s => s.AmountYen).IsRequired();
        builder.Property(s => s.BasisSummary).IsRequired().HasMaxLength(512);
        builder.Property(s => s.Kind).HasConversion<int>().IsRequired();
        builder.Property(s => s.OriginId);
        builder.Property(s => s.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ConcurrencyToken);
        builder.HasIndex(s => s.OriginId);
        builder.HasIndex(s => new { s.OfficeId, s.Month });
        // 確定済 (Kind=New) の (Office, Month, Recipient) 一意化
        // NOTE(A3): RecordKind.New = 1 のため "Kind" = 1
        builder.HasIndex(s => new { s.OfficeId, s.Month, s.RecipientId })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_WageStatements_Office_YM_Recipient_NewOnly");
    }
}
