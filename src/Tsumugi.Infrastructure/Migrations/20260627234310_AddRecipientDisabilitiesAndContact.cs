using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientDisabilitiesAndContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Recipients",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Intellectual",
                table: "Recipients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Intractable",
                table: "Recipients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Mental",
                table: "Recipients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Disability_Physical",
                table: "Recipients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "Recipients",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "Recipients",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "Recipients",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactRelationship",
                table: "Recipients",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Recipients",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Recipients",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "Disability_Intellectual",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "Disability_Intractable",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "Disability_Mental",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "Disability_Physical",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "EmergencyContactRelationship",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Recipients");
        }
    }
}
