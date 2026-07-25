using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase33GroupBExplicitAdditionInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClaimInputs_CancelPayload",
                table: "ClaimInputs");

            migrationBuilder.AddColumn<int>(
                name: "SpecialVisitSupportBilledHours",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OffsiteSupportCumulativeDays",
                table: "ClaimInputs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialVisitSupportBilledCount",
                table: "ClaimInputs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClaimInputs_CancelPayload",
                table: "ClaimInputs",
                sql: "\"Kind\" <> 3 OR (\"UpperLimitManagementResult\" IS NULL AND \"UpperLimitManagedAmountYen\" IS NULL AND \"MunicipalSubsidyAmountYen\" IS NULL AND \"ExceptionalUsageStartMonthKey\" IS NULL AND \"ExceptionalUsageEndMonthKey\" IS NULL AND \"ExceptionalUsageDays\" IS NULL AND \"StandardUsageDayTotal\" IS NULL AND \"SpecialVisitSupportBilledCount\" IS NULL AND \"OffsiteSupportCumulativeDays\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClaimInputs_CancelPayload",
                table: "ClaimInputs");

            migrationBuilder.DropColumn(
                name: "SpecialVisitSupportBilledHours",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "OffsiteSupportCumulativeDays",
                table: "ClaimInputs");

            migrationBuilder.DropColumn(
                name: "SpecialVisitSupportBilledCount",
                table: "ClaimInputs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClaimInputs_CancelPayload",
                table: "ClaimInputs",
                sql: "\"Kind\" <> 3 OR (\"UpperLimitManagementResult\" IS NULL AND \"UpperLimitManagedAmountYen\" IS NULL AND \"MunicipalSubsidyAmountYen\" IS NULL AND \"ExceptionalUsageStartMonthKey\" IS NULL AND \"ExceptionalUsageEndMonthKey\" IS NULL AND \"ExceptionalUsageDays\" IS NULL AND \"StandardUsageDayTotal\" IS NULL)");
        }
    }
}
