using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase33ClaimCsvExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimCsvExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessingMonthKey = table.Column<int>(type: "INTEGER", nullable: false),
                    CsvSpecificationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClaimMasterVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ByteLength = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimCsvExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimCsvExports_ClaimBatches_ClaimBatchId",
                        column: x => x.ClaimBatchId,
                        principalTable: "ClaimBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimCsvExports_ClaimBatchId_CreatedAt",
                table: "ClaimCsvExports",
                columns: new[] { "ClaimBatchId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimCsvExports");
        }
    }
}
