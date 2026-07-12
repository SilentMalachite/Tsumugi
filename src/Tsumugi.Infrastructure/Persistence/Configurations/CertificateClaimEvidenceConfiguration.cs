using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class CertificateClaimEvidenceConfiguration : IEntityTypeConfiguration<CertificateClaimEvidence>
{
    public void Configure(EntityTypeBuilder<CertificateClaimEvidence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ClaimInputConfigurationShared.ConfigureHistory(
            builder,
            "CertificateClaimEvidences",
            "\"Kind\" <> 3 OR (\"MonthlyCostCap_IsEntered\" = 0 AND \"MonthlyCostCap_ValueYen\" IS NULL " +
            "AND \"UpperLimitManagementApplicability\" = 0 AND \"UpperLimitManagementOfficeNumber\" IS NULL " +
            "AND \"Article31Status\" = 0 AND \"Article31AmountYen_IsEntered\" = 0 " +
            "AND \"Article31AmountYen_ValueYen\" IS NULL AND \"Article31EffectivePeriod\" IS NULL " +
            "AND \"OriginalDocumentReference\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL " +
            "AND \"ConfirmationReason\" IS NULL)",
            table =>
            {
                ClaimInputConfigurationShared.AddEnteredYenCheck(table, "CertificateClaimEvidences", "MonthlyCostCap");
                ClaimInputConfigurationShared.AddEnteredYenCheck(table, "CertificateClaimEvidences", "Article31AmountYen");
                table.HasCheckConstraint(
                    "CK_CertificateClaimEvidences_UpperLimitManagementApplicability_ClosedSet",
                    "\"UpperLimitManagementApplicability\" IN (0, 1, 2)");
                table.HasCheckConstraint(
                    "CK_CertificateClaimEvidences_Article31Status_ClosedSet",
                    "\"Article31Status\" IN (0, 1, 2)");
            });
        builder.Property(x => x.CertificateId).IsRequired();
        builder.Property(x => x.Validity)
            .HasConversion(value => DateRangeJson.Serialize(value), value => DateRangeJson.Deserialize(value))
            .HasColumnName("Validity").IsRequired();
        builder.Property(x => x.UpperLimitManagementApplicability).HasConversion<int>().IsRequired();
        builder.Property(x => x.Article31Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Article31EffectivePeriod)
            .HasConversion(
                value => value.HasValue ? DateRangeJson.Serialize(value.Value) : null,
                value => value == null ? (DateRange?)null : DateRangeJson.Deserialize(value))
            .HasColumnName("Article31EffectivePeriod");
        ClaimInputConfigurationShared.ConfigureEnteredYen(builder, nameof(CertificateClaimEvidence.MonthlyCostCap), "MonthlyCostCap");
        ClaimInputConfigurationShared.ConfigureEnteredYen(builder, nameof(CertificateClaimEvidence.Article31AmountYen), "Article31AmountYen");
        builder.HasIndex(x => new { x.CertificateId, x.Validity }).HasFilter("\"Kind\" = 1").IsUnique()
            .HasDatabaseName("UX_CertificateClaimEvidences_CertificateId_Validity_NewOnly");
        builder.HasOne<Certificate>().WithMany().HasForeignKey(x => x.CertificateId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CertificateClaimEvidences_Certificates_CertificateId");
    }
}
