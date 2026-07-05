using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWageAdjustment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HourUnitMinutes",
                table: "WageSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SkillAllowanceTiersJson",
                table: "WageSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WorkAllowancePerDayYen",
                table: "WageSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WageAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    YearMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    AmountYen = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WageAdjustments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_WageAdjustments_OfficeRecipientYmType_NewOnly",
                table: "WageAdjustments",
                columns: new[] { "OfficeId", "RecipientId", "YearMonth", "Type" },
                unique: true,
                filter: "\"Kind\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WageAdjustments");

            migrationBuilder.DropColumn(
                name: "HourUnitMinutes",
                table: "WageSettings");

            migrationBuilder.DropColumn(
                name: "SkillAllowanceTiersJson",
                table: "WageSettings");

            migrationBuilder.DropColumn(
                name: "WorkAllowancePerDayYen",
                table: "WageSettings");
        }
    }
}
