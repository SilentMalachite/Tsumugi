using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ClaimDetailConfiguration : IEntityTypeConfiguration<ClaimDetail>
{
    public void Configure(EntityTypeBuilder<ClaimDetail> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ClaimDetails");
        builder.HasKey(detail => detail.Id);
        builder.Property(detail => detail.ClaimBatchId).IsRequired();
        builder.Property(detail => detail.RecipientId).IsRequired();
        builder.Property(detail => detail.SnapshotSchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(detail => detail.ClaimMasterVersion).IsRequired().HasMaxLength(64);
        builder.Property(detail => detail.CsvSpecificationVersion).IsRequired().HasMaxLength(64);
        builder.Property(detail => detail.ReportSpecificationVersion).IsRequired().HasMaxLength(64);
        builder.Property(detail => detail.SnapshotApplicationVersion).IsRequired().HasMaxLength(64);
        builder.Property(detail => detail.InputSnapshotJson).IsRequired().HasColumnType("TEXT");
        builder.Property(detail => detail.CalculationSnapshotJson).IsRequired().HasColumnType("TEXT");
        builder.Property(detail => detail.TotalUnits).IsRequired();
        builder.Property(detail => detail.TotalCostYen).IsRequired();
        builder.Property(detail => detail.BenefitYen).IsRequired();
        builder.Property(detail => detail.BurdenYen).IsRequired();
        builder.Property(detail => detail.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(detail => detail.CreatedAt).IsRequired();
        builder.Property(detail => detail.ConcurrencyToken).IsConcurrencyToken();

        builder.HasIndex(detail => new { detail.ClaimBatchId, detail.RecipientId })
            .IsUnique()
            .HasDatabaseName("UX_ClaimDetails_ClaimBatchId_RecipientId");
        builder.HasIndex(detail => detail.ClaimBatchId);

        builder.HasOne<ClaimBatch>()
            .WithMany()
            .HasForeignKey(detail => detail.ClaimBatchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClaimDetails_ClaimBatches_ClaimBatchId");
    }
}
