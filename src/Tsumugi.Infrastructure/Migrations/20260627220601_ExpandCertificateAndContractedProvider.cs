using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCertificateAndContractedProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BenefitType",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ConsultationEnd",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsultationProviderName",
                table: "Certificates",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsultationProviderNumber",
                table: "Certificates",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ConsultationStart",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Intellectual",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Intractable",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Mental",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Physical",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "Certificates",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianRelationship",
                table: "Certificates",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HighCostBenefitApplicable",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MealProvisionApplicable",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaymentBurden",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecipientAddress",
                table: "Certificates",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientGender",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ServiceCategory",
                table: "Certificates",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SupplyNotes",
                table: "Certificates",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupportCategory",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UpperLimitManagementProvider",
                table: "Certificates",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractedProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ServiceCategory = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ContractedSupplyDays = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TerminationDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractedProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractedProviders_CertificateId",
                table: "ContractedProviders",
                column: "CertificateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractedProviders");

            migrationBuilder.DropColumn(
                name: "BenefitType",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ConsultationEnd",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ConsultationProviderName",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ConsultationProviderNumber",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ConsultationStart",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Disability_Intellectual",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Disability_Intractable",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Disability_Mental",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Disability_Physical",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "GuardianRelationship",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "HighCostBenefitApplicable",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "MealProvisionApplicable",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "PaymentBurden",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "RecipientAddress",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "RecipientGender",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ServiceCategory",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "SupplyNotes",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "SupportCategory",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "UpperLimitManagementProvider",
                table: "Certificates");
        }
    }
}
