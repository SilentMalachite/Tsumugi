using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ClaimBatchConfiguration : IEntityTypeConfiguration<ClaimBatch>
{
    public void Configure(EntityTypeBuilder<ClaimBatch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ClaimBatches");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.OfficeId).IsRequired();
        builder.Property(batch => batch.ServiceMonth)
            .HasConversion(month => month.ToInt(), value => ServiceMonth.FromInt(value))
            .HasColumnName("ServiceMonthKey")
            .IsRequired();
        builder.Property(batch => batch.Revision).IsRequired();
        builder.Property(batch => batch.Kind).HasConversion<int>().IsRequired();
        builder.Property(batch => batch.OriginId);
        builder.Property(batch => batch.ExpectedHeadBatchId);
        builder.Property(batch => batch.ExpectedHeadRevision);
        builder.Property(batch => batch.TotalUnits).IsRequired();
        builder.Property(batch => batch.TotalCostYen).IsRequired();
        builder.Property(batch => batch.TotalBenefitYen).IsRequired();
        builder.Property(batch => batch.TotalBurdenYen).IsRequired();
        builder.Property(batch => batch.ClaimMasterVersion).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.CsvSpecificationVersion).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.ReportSpecificationVersion).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.SnapshotApplicationVersion).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.OperationApplicationVersion).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.FinalizationOperationId).IsRequired();
        builder.Property(batch => batch.OperationPayloadSchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.OperationPayloadSha256).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(batch => batch.CreatedAt).IsRequired();
        builder.Property(batch => batch.ConcurrencyToken).IsConcurrencyToken();

        builder.HasIndex(batch => new { batch.OfficeId, batch.ServiceMonth })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly");
        builder.HasIndex(batch => batch.FinalizationOperationId)
            .IsUnique()
            .HasDatabaseName("UX_ClaimBatches_FinalizationOperationId");
        builder.HasIndex(batch => new { batch.OfficeId, batch.ServiceMonth, batch.Revision })
            .IsUnique()
            .HasDatabaseName("UX_ClaimBatches_OfficeId_ServiceMonthKey_Revision");
        builder.HasIndex(batch => batch.OriginId);
        builder.HasIndex(batch => batch.ExpectedHeadBatchId);

        builder.HasOne<ClaimBatch>()
            .WithMany()
            .HasForeignKey(batch => batch.OriginId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClaimBatches_ClaimBatches_OriginId");
        builder.HasOne<ClaimBatch>()
            .WithMany()
            .HasForeignKey(batch => batch.ExpectedHeadBatchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ClaimBatches_ClaimBatches_ExpectedHeadBatchId");
    }
}
