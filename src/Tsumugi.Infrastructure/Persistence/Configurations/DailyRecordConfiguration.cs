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
        builder.HasIndex(r => new { r.RecipientId, r.ServiceDate });
        builder.HasIndex(r => r.OriginId);
        builder.Property(r => r.ServiceDate).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        // OriginId は nullable — IsRequired() は不要
        builder.Property(r => r.OriginId);
        builder.Property(r => r.Attendance).HasConversion<int>().IsRequired();
        builder.Property(r => r.Transport).HasConversion<int>().IsRequired();
        builder.Property(r => r.MealProvided).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        // 取引記録は更新しないため ConcurrencyToken は IsConcurrencyToken() しない。列としては存在する。
        builder.Property(r => r.ConcurrencyToken);
    }
}
