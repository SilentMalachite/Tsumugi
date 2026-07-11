using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class UpperLimitManagementStatementLineConfiguration
    : IEntityTypeConfiguration<UpperLimitManagementStatementLine>
{
    public void Configure(EntityTypeBuilder<UpperLimitManagementStatementLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("UpperLimitManagementStatementLines", table =>
        {
            table.HasCheckConstraint("CK_UpperLimitManagementStatementLines_LineNumber", "\"LineNumber\" > 0");
            ClaimInputConfigurationShared.AddEnteredYenCheck(
                table, "UpperLimitManagementStatementLines", "TotalCostYen");
            ClaimInputConfigurationShared.AddEnteredYenCheck(
                table, "UpperLimitManagementStatementLines", "PreManagementBurdenYen");
            ClaimInputConfigurationShared.AddEnteredYenCheck(
                table, "UpperLimitManagementStatementLines", "ManagedBurdenYen");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StatementId).IsRequired();
        builder.Property(x => x.LineNumber).IsRequired();
        builder.Property(x => x.OfficeNumber).IsRequired();
        builder.Property(x => x.OfficeName).IsRequired();
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatementLine.TotalCostYen), "TotalCostYen");
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatementLine.PreManagementBurdenYen), "PreManagementBurdenYen");
        ClaimInputConfigurationShared.ConfigureEnteredYen(
            builder, nameof(UpperLimitManagementStatementLine.ManagedBurdenYen), "ManagedBurdenYen");
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ConcurrencyToken).IsRequired();
        builder.HasIndex(x => new { x.StatementId, x.LineNumber }).IsUnique()
            .HasDatabaseName("UX_UpperLimitManagementStatementLines_StatementId_LineNumber");
        builder.HasIndex(x => new { x.StatementId, x.OfficeNumber }).IsUnique()
            .HasDatabaseName("UX_UpperLimitManagementStatementLines_StatementId_OfficeNumber");
        builder.HasOne<UpperLimitManagementStatement>().WithMany().HasForeignKey(x => x.StatementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_UpperLimitManagementStatementLines_UpperLimitManagementStatements_StatementId");
    }
}
