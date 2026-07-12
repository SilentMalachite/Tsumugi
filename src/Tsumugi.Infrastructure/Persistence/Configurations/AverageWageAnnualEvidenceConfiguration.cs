using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class AverageWageAnnualEvidenceConfiguration : IEntityTypeConfiguration<AverageWageAnnualEvidence>
{
    public void Configure(EntityTypeBuilder<AverageWageAnnualEvidence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ClaimInputConfigurationShared.ConfigureHistory(
            builder,
            "AverageWageAnnualEvidences",
            "\"Kind\" <> 3 OR (\"AnnualWagePaidYen\" IS NULL AND \"AnnualExtendedUsers\" IS NULL " +
            "AND \"AnnualOpeningDays\" IS NULL AND \"Completeness\" IS NULL AND \"EvidenceDocumentId\" IS NULL " +
            "AND \"DailyEvidenceReference\" IS NULL AND \"MonthlyEvidenceReference\" IS NULL " +
            "AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL)",
            table => table.HasCheckConstraint(
                "CK_AverageWageAnnualEvidences_Completeness_ClosedSet",
                "\"Completeness\" IS NULL OR \"Completeness\" IN (1, 2)"));
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.SourceFiscalYear).IsRequired();
        builder.Property(x => x.PeriodStart).IsRequired();
        builder.Property(x => x.PeriodEnd).IsRequired();
        builder.Property(x => x.Completeness).HasConversion<int?>();
        builder.HasIndex(
                x => new { x.OfficeId, x.SourceFiscalYear },
                "IX_AverageWageAnnualEvidences_OfficeId_SourceFiscalYear")
            .HasDatabaseName("IX_AverageWageAnnualEvidences_OfficeId_SourceFiscalYear");
        builder.HasIndex(x => new { x.OfficeId, x.SourceFiscalYear }).HasFilter("\"Kind\" = 1").IsUnique()
            .HasDatabaseName("UX_AverageWageAnnualEvidences_OfficeId_SourceFiscalYear_NewOnly");
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AverageWageAnnualEvidences_Offices_OfficeId");
    }
}
