using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DailyRecordDuplicateNewIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyRecords_RecipientId_ServiceDate",
                table: "DailyRecords");

            migrationBuilder.CreateIndex(
                name: "UX_DailyRecords_RecipientId_ServiceDate_NewOnly",
                table: "DailyRecords",
                columns: new[] { "RecipientId", "ServiceDate" },
                unique: true,
                filter: "\"Kind\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_DailyRecords_RecipientId_ServiceDate_NewOnly",
                table: "DailyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_DailyRecords_RecipientId_ServiceDate",
                table: "DailyRecords",
                columns: new[] { "RecipientId", "ServiceDate" });
        }
    }
}
