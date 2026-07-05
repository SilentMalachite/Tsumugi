using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientHourlyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipientHourlyRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodJson = table.Column<string>(type: "TEXT", nullable: false),
                    HourlyYen = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipientHourlyRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_RecipientHourlyRates_OfficeRecipientPeriodStart_NewOnly",
                table: "RecipientHourlyRates",
                columns: new[] { "OfficeId", "RecipientId", "PeriodStart" },
                unique: true,
                filter: "\"Kind\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipientHourlyRates");
        }
    }
}
