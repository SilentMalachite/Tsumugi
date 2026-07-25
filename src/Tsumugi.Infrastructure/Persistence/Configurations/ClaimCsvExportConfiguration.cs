using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ClaimCsvExportConfiguration : IEntityTypeConfiguration<ClaimCsvExport>
{
    public void Configure(EntityTypeBuilder<ClaimCsvExport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ClaimCsvExports");
        builder.HasKey(export => export.Id);
        builder.Property(export => export.ClaimBatchId).IsRequired();
        builder.Property(export => export.ProcessingMonth)
            .HasConversion(month => month.ToInt(), value => ProcessingMonth.FromInt(value))
            .HasColumnName("ProcessingMonthKey")
            .IsRequired();
        builder.Property(export => export.CsvSpecificationVersion).IsRequired().HasMaxLength(64);
        builder.Property(export => export.FinalizedCsvSpecificationVersion)
            .IsRequired().HasMaxLength(64);
        builder.Property(export => export.ClaimMasterVersion).IsRequired().HasMaxLength(64);
        builder.Property(export => export.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(export => export.ByteLength).IsRequired();
        builder.Property(export => export.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(export => export.CreatedAt).IsRequired();
        builder.Property(export => export.ConcurrencyToken).IsConcurrencyToken();

        builder.HasIndex(export => new { export.ClaimBatchId, export.CreatedAt })
            .HasDatabaseName("IX_ClaimCsvExports_ClaimBatchId_CreatedAt");

        // 履歴を守るため、参照先の請求バッチは削除できない。
        builder.HasOne<ClaimBatch>()
            .WithMany()
            .HasForeignKey(export => export.ClaimBatchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClaimCsvExports_ClaimBatches_ClaimBatchId");
    }
}
