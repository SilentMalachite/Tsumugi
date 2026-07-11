using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class UpperLimitManagementStatementConfiguration
    : IEntityTypeConfiguration<UpperLimitManagementStatement>
{
    public void Configure(EntityTypeBuilder<UpperLimitManagementStatement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ClaimInputConfigurationShared.ConfigureHistory(
            builder,
            "UpperLimitManagementStatements",
            "\"Kind\" <> 3 OR (\"MunicipalityNumber\" = '' AND \"CertificateNumber\" = '' " +
            "AND \"CertificateMonthlyCostCap_IsEntered\" = 0 AND \"CertificateMonthlyCostCap_ValueYen\" IS NULL " +
            "AND \"UpperLimitManagementApplicability\" = 0 AND \"CertificateManagingOfficeNumber\" = '' " +
            "AND \"ManagingOfficeNumber\" = '' AND \"ManagingOfficeName\" = '' AND \"OriginalCreationKind\" = '' " +
            "AND \"ReceivedAt\" IS NULL AND \"OriginalDocumentReference\" IS NULL AND \"IsConfirmed\" = 0 " +
            "AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL " +
            "AND \"Result\" = 0 AND \"TotalCostYen_IsEntered\" = 0 AND \"TotalCostYen_ValueYen\" IS NULL " +
            "AND \"TotalPreManagementBurdenYen_IsEntered\" = 0 AND \"TotalPreManagementBurdenYen_ValueYen\" IS NULL " +
            "AND \"TotalManagedBurdenYen_IsEntered\" = 0 AND \"TotalManagedBurdenYen_ValueYen\" IS NULL)",
            table =>
            {
                ClaimInputConfigurationShared.AddEnteredYenCheck(table, "UpperLimitManagementStatements", "CertificateMonthlyCostCap");
                ClaimInputConfigurationShared.AddEnteredYenCheck(table, "UpperLimitManagementStatements", "TotalCostYen");
                ClaimInputConfigurationShared.AddEnteredYenCheck(table, "UpperLimitManagementStatements", "TotalPreManagementBurdenYen");
                ClaimInputConfigurationShared.AddEnteredYenCheck(table, "UpperLimitManagementStatements", "TotalManagedBurdenYen");
            });
        builder.Property(x => x.ServiceMonth)
            .HasConversion(value => value.ToInt(), value => ServiceMonth.FromInt(value))
            .HasColumnName("ServiceMonthKey").IsRequired();
        builder.Property(x => x.RecipientId).IsRequired();
        builder.Property(x => x.CertificateId).IsRequired();
        builder.Property(x => x.ManagingOfficeId).IsRequired();
        builder.Property(x => x.MunicipalityNumber).IsRequired();
        builder.Property(x => x.CertificateNumber).IsRequired();
        builder.Property(x => x.UpperLimitManagementApplicability).HasConversion<int>().IsRequired();
        builder.Property(x => x.CertificateManagingOfficeNumber).IsRequired();
        builder.Property(x => x.ManagingOfficeNumber).IsRequired();
        builder.Property(x => x.ManagingOfficeName).IsRequired();
        builder.Property(x => x.OriginalCreationKind).IsRequired();
        builder.Property(x => x.IsConfirmed).IsRequired();
        builder.Property(x => x.Result).HasConversion<int>().IsRequired();
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatement.CertificateMonthlyCostCap), "CertificateMonthlyCostCap");
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatement.TotalCostYen), "TotalCostYen");
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatement.TotalPreManagementBurdenYen), "TotalPreManagementBurdenYen");
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatement.TotalManagedBurdenYen), "TotalManagedBurdenYen");
        builder.HasIndex(x => new { x.RecipientId, x.CertificateId, x.ManagingOfficeId, x.ServiceMonth })
            .HasFilter("\"Kind\" = 1").IsUnique()
            .HasDatabaseName(
                "UX_UpperLimitManagementStatements_RecipientId_CertificateId_ManagingOfficeId_ServiceMonthKey_NewOnly");
        builder.HasOne<Recipient>().WithMany().HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UpperLimitManagementStatements_Recipients_RecipientId");
        builder.HasOne<Certificate>().WithMany().HasForeignKey(x => x.CertificateId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UpperLimitManagementStatements_Certificates_CertificateId");
        builder.HasOne<Office>().WithMany().HasForeignKey(x => x.ManagingOfficeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UpperLimitManagementStatements_Offices_ManagingOfficeId");
    }
}
