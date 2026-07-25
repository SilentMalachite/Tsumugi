using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase33ClaimInputGenericValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClaimInputGenericValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimInputId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimInputGenericValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimInputGenericValues_ClaimInputs_ClaimInputId",
                        column: x => x.ClaimInputId,
                        principalTable: "ClaimInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimInputGenericValues_ClaimInputId_Name",
                table: "ClaimInputGenericValues",
                columns: new[] { "ClaimInputId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimInputGenericValues");
        }
    }
}
