using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WageFundDuplicateNewIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_WageFunds_OfficeId_MonthKey_NewOnly",
                table: "WageFunds",
                columns: new[] { "OfficeId", "MonthKey" },
                unique: true,
                filter: "\"Kind\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WageFunds_OfficeId_MonthKey_NewOnly",
                table: "WageFunds");
        }
    }
}
