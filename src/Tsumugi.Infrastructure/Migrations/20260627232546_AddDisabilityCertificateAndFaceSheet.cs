using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisabilityCertificateAndFaceSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisabilityCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Grade = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Subtype = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    IssuedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    NextRenewalDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    IssuingAuthority = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CertificateNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisabilityCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaceSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    EmailAddress = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EmergencyContactRelationship = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FamilyComposition = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Cohabitants = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PrimaryDoctorName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PrimaryDoctorHospital = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PrimaryDoctorPhone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    MedicalHistory = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CurrentConditions = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Medications = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Allergies = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ReceivesNursingInsurance = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReceivesDisabilityPension = table.Column<bool>(type: "INTEGER", nullable: false),
                    PensionDetails = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LifeHistory = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    PersonalWishes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SupportNeeds = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    AssessmentSummary = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceSheets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisabilityCertificates_RecipientId",
                table: "DisabilityCertificates",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_FaceSheets_RecipientId",
                table: "FaceSheets",
                column: "RecipientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisabilityCertificates");

            migrationBuilder.DropTable(
                name: "FaceSheets");
        }
    }
}
