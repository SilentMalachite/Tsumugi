using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageFundConfiguration : IEntityTypeConfiguration<WageFund>
{
    public void Configure(EntityTypeBuilder<WageFund> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageFunds");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.OfficeId).IsRequired();
        // YearMonth は YYYYMM 形式の int 単一列に変換（OwnsOne/ComplexProperty は HasIndex 制約があるため）
        builder.Property(r => r.Month)
            .HasConversion(v => v.ToInt(), v => Tsumugi.Domain.ValueObjects.YearMonth.FromInt(v))
            .HasColumnName("MonthKey")
            .IsRequired();
        builder.Property(r => r.TotalYen).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        builder.Property(r => r.OriginId);
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ConcurrencyToken);
        builder.HasIndex(r => r.OfficeId);
        // partial unique index: 同一 (OfficeId, Month) の Kind=New（=1）を DB レベルで一意化する（ADR 0017）
        builder.HasIndex(r => new { r.OfficeId, r.Month })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_WageFunds_OfficeId_MonthKey_NewOnly");
        builder.HasIndex(r => r.OriginId);
    }
}
