using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase31OfficeClaimBillingTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OfficeClaimProfiles_CancelPayload",
                table: "OfficeClaimProfiles");

            migrationBuilder.AddColumn<int>(
                name: "CapacityHeadcount",
                table: "OfficeClaimProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegionKey",
                table: "OfficeClaimProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffingKey",
                table: "OfficeClaimProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_OfficeClaimProfiles_CancelPayload",
                table: "OfficeClaimProfiles",
                sql: "\"Kind\" <> 3 OR (\"MasterVersion\" IS NULL AND \"ReformStatus\" IS NULL AND \"AverageWageBandOption_Kind\" IS NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NULL AND \"DesignationDate\" IS NULL AND \"SupportStartDate\" IS NULL AND \"EarlierRegisteredBandOption_MasterVersion\" IS NULL AND \"EarlierRegisteredBandOption_Option_Kind\" IS NULL AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" IS NULL AND \"EarlierRegistrationMonthKey\" IS NULL AND \"LaterRegisteredBandOption_MasterVersion\" IS NULL AND \"LaterRegisteredBandOption_Option_Kind\" IS NULL AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" IS NULL AND \"LaterRegistrationMonthKey\" IS NULL AND \"ReformComparisonEvidenceDocumentId\" IS NULL AND \"FiledTransitionPeriod\" IS NULL AND \"FiledTransitionEvidenceDocumentId\" IS NULL AND \"EvidenceDocumentId\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL AND \"CapacityHeadcount\" IS NULL AND \"StaffingKey\" IS NULL AND \"RegionKey\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OfficeClaimProfiles_CancelPayload",
                table: "OfficeClaimProfiles");

            migrationBuilder.DropColumn(
                name: "CapacityHeadcount",
                table: "OfficeClaimProfiles");

            migrationBuilder.DropColumn(
                name: "RegionKey",
                table: "OfficeClaimProfiles");

            migrationBuilder.DropColumn(
                name: "StaffingKey",
                table: "OfficeClaimProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OfficeClaimProfiles_CancelPayload",
                table: "OfficeClaimProfiles",
                sql: "\"Kind\" <> 3 OR (\"MasterVersion\" IS NULL AND \"ReformStatus\" IS NULL AND \"AverageWageBandOption_Kind\" IS NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NULL AND \"DesignationDate\" IS NULL AND \"SupportStartDate\" IS NULL AND \"EarlierRegisteredBandOption_MasterVersion\" IS NULL AND \"EarlierRegisteredBandOption_Option_Kind\" IS NULL AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" IS NULL AND \"EarlierRegistrationMonthKey\" IS NULL AND \"LaterRegisteredBandOption_MasterVersion\" IS NULL AND \"LaterRegisteredBandOption_Option_Kind\" IS NULL AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" IS NULL AND \"LaterRegistrationMonthKey\" IS NULL AND \"ReformComparisonEvidenceDocumentId\" IS NULL AND \"FiledTransitionPeriod\" IS NULL AND \"FiledTransitionEvidenceDocumentId\" IS NULL AND \"EvidenceDocumentId\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL)");
        }
    }
}
