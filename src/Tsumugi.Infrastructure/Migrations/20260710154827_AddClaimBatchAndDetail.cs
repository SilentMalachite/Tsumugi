using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimBatchAndDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceMonthKey = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExpectedHeadBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExpectedHeadRevision = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalUnits = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCostYen = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBenefitYen = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBurdenYen = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimMasterVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CsvSpecificationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReportSpecificationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SnapshotApplicationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperationApplicationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FinalizationOperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationPayloadSchemaVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperationPayloadSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimBatches_ClaimBatches_ExpectedHeadBatchId",
                        column: x => x.ExpectedHeadBatchId,
                        principalTable: "ClaimBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimBatches_ClaimBatches_OriginId",
                        column: x => x.OriginId,
                        principalTable: "ClaimBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimBatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SnapshotSchemaVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClaimMasterVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CsvSpecificationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReportSpecificationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SnapshotApplicationVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    InputSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    CalculationSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    TotalUnits = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCostYen = table.Column<int>(type: "INTEGER", nullable: false),
                    BenefitYen = table.Column<int>(type: "INTEGER", nullable: false),
                    BurdenYen = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimDetails_ClaimBatches_ClaimBatchId",
                        column: x => x.ClaimBatchId,
                        principalTable: "ClaimBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatches_ExpectedHeadBatchId",
                table: "ClaimBatches",
                column: "ExpectedHeadBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimBatches_OriginId",
                table: "ClaimBatches",
                column: "OriginId");

            migrationBuilder.CreateIndex(
                name: "UX_ClaimBatches_FinalizationOperationId",
                table: "ClaimBatches",
                column: "FinalizationOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly",
                table: "ClaimBatches",
                columns: new[] { "OfficeId", "ServiceMonthKey" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ClaimBatches_OfficeId_ServiceMonthKey_Revision",
                table: "ClaimBatches",
                columns: new[] { "OfficeId", "ServiceMonthKey", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDetails_ClaimBatchId",
                table: "ClaimDetails",
                column: "ClaimBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_ClaimDetails_ClaimBatchId_RecipientId",
                table: "ClaimDetails",
                columns: new[] { "ClaimBatchId", "RecipientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimDetails");

            migrationBuilder.DropTable(
                name: "ClaimBatches");
        }
    }
}
