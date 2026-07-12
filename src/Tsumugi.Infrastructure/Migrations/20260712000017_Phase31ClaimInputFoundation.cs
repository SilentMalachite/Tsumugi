using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsumugi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase31ClaimInputFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Offices",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Offices",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Offices",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepresentativeTitleAndName",
                table: "Offices",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmergencyAdmissionApplied",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IntensiveSupportApplied",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MedicalCoordinationType",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "OffsiteSupportApplied",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientConfirmation",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RegionalCollaborationApplied",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ServiceEndTime",
                table: "DailyRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ServiceStartTime",
                table: "DailyRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SpecialVisitSupportMinutes",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TrialUseSupportType",
                table: "DailyRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CertificateEntryNumber",
                table: "ContractedProviders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpectedHeadCertificateId",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipalityNumber",
                table: "Certificates",
                type: "TEXT",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Revision",
                table: "Certificates",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootCertificateId",
                table: "Certificates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubsidyMunicipalityNumber",
                table: "Certificates",
                type: "TEXT",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpperLimitManagementProviderNumber",
                table: "Certificates",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AverageWageAnnualEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceFiscalYear = table.Column<int>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RootId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedHeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnnualWagePaidYen = table.Column<int>(type: "INTEGER", nullable: true),
                    AnnualExtendedUsers = table.Column<int>(type: "INTEGER", nullable: true),
                    AnnualOpeningDays = table.Column<int>(type: "INTEGER", nullable: true),
                    Completeness = table.Column<int>(type: "INTEGER", nullable: true),
                    EvidenceDocumentId = table.Column<string>(type: "TEXT", nullable: true),
                    DailyEvidenceReference = table.Column<string>(type: "TEXT", nullable: true),
                    MonthlyEvidenceReference = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmationReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AverageWageAnnualEvidences", x => x.Id);
                    table.CheckConstraint("CK_AverageWageAnnualEvidences_CancelPayload", "\"Kind\" <> 3 OR (\"AnnualWagePaidYen\" IS NULL AND \"AnnualExtendedUsers\" IS NULL AND \"AnnualOpeningDays\" IS NULL AND \"Completeness\" IS NULL AND \"EvidenceDocumentId\" IS NULL AND \"DailyEvidenceReference\" IS NULL AND \"MonthlyEvidenceReference\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL)");
                    table.CheckConstraint("CK_AverageWageAnnualEvidences_Completeness_ClosedSet", "\"Completeness\" IS NULL OR \"Completeness\" IN (1, 2)");
                    table.CheckConstraint("CK_AverageWageAnnualEvidences_RevisionLineage", "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND ((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_AverageWageAnnualEvidences_AverageWageAnnualEvidences_ExpectedHeadId",
                        column: x => x.ExpectedHeadId,
                        principalTable: "AverageWageAnnualEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AverageWageAnnualEvidences_AverageWageAnnualEvidences_RootId",
                        column: x => x.RootId,
                        principalTable: "AverageWageAnnualEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AverageWageAnnualEvidences_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CertificateClaimEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Validity = table.Column<string>(type: "TEXT", nullable: false),
                    RootId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedHeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpperLimitManagementApplicability = table.Column<int>(type: "INTEGER", nullable: false),
                    UpperLimitManagementOfficeNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Article31Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Article31EffectivePeriod = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalDocumentReference = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmationReason = table.Column<string>(type: "TEXT", nullable: true),
                    Article31AmountYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    Article31AmountYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    MonthlyCostCap_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    MonthlyCostCap_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateClaimEvidences", x => x.Id);
                    table.CheckConstraint("CK_CertificateClaimEvidences_Article31AmountYen_EnteredYen", "((\"Article31AmountYen_IsEntered\" = 0 AND \"Article31AmountYen_ValueYen\" IS NULL) OR (\"Article31AmountYen_IsEntered\" = 1 AND \"Article31AmountYen_ValueYen\" IS NOT NULL AND \"Article31AmountYen_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_CertificateClaimEvidences_Article31Status_ClosedSet", "\"Article31Status\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_CertificateClaimEvidences_CancelPayload", "\"Kind\" <> 3 OR (\"MonthlyCostCap_IsEntered\" = 0 AND \"MonthlyCostCap_ValueYen\" IS NULL AND \"UpperLimitManagementApplicability\" = 0 AND \"UpperLimitManagementOfficeNumber\" IS NULL AND \"Article31Status\" = 0 AND \"Article31AmountYen_IsEntered\" = 0 AND \"Article31AmountYen_ValueYen\" IS NULL AND \"Article31EffectivePeriod\" IS NULL AND \"OriginalDocumentReference\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL)");
                    table.CheckConstraint("CK_CertificateClaimEvidences_MonthlyCostCap_EnteredYen", "((\"MonthlyCostCap_IsEntered\" = 0 AND \"MonthlyCostCap_ValueYen\" IS NULL) OR (\"MonthlyCostCap_IsEntered\" = 1 AND \"MonthlyCostCap_ValueYen\" IS NOT NULL AND \"MonthlyCostCap_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_CertificateClaimEvidences_RevisionLineage", "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND ((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
                    table.CheckConstraint("CK_CertificateClaimEvidences_UpperLimitManagementApplicability_ClosedSet", "\"UpperLimitManagementApplicability\" IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_CertificateClaimEvidences_CertificateClaimEvidences_ExpectedHeadId",
                        column: x => x.ExpectedHeadId,
                        principalTable: "CertificateClaimEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateClaimEvidences_CertificateClaimEvidences_RootId",
                        column: x => x.RootId,
                        principalTable: "CertificateClaimEvidences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CertificateClaimEvidences_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimInputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceMonthKey = table.Column<int>(type: "INTEGER", nullable: false),
                    RootId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedHeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpperLimitManagementResult = table.Column<int>(type: "INTEGER", nullable: true),
                    UpperLimitManagedAmountYen = table.Column<int>(type: "INTEGER", nullable: true),
                    MunicipalSubsidyAmountYen = table.Column<int>(type: "INTEGER", nullable: true),
                    ExceptionalUsageStartMonthKey = table.Column<int>(type: "INTEGER", nullable: true),
                    ExceptionalUsageEndMonthKey = table.Column<int>(type: "INTEGER", nullable: true),
                    ExceptionalUsageDays = table.Column<int>(type: "INTEGER", nullable: true),
                    StandardUsageDayTotal = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimInputs", x => x.Id);
                    table.CheckConstraint("CK_ClaimInputs_CancelPayload", "\"Kind\" <> 3 OR (\"UpperLimitManagementResult\" IS NULL AND \"UpperLimitManagedAmountYen\" IS NULL AND \"MunicipalSubsidyAmountYen\" IS NULL AND \"ExceptionalUsageStartMonthKey\" IS NULL AND \"ExceptionalUsageEndMonthKey\" IS NULL AND \"ExceptionalUsageDays\" IS NULL AND \"StandardUsageDayTotal\" IS NULL)");
                    table.CheckConstraint("CK_ClaimInputs_RevisionLineage", "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND ((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
                    table.CheckConstraint("CK_ClaimInputs_UpperLimitManagementResult_ClosedSet", "\"UpperLimitManagementResult\" IS NULL OR \"UpperLimitManagementResult\" IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ClaimInputs_ClaimInputs_ExpectedHeadId",
                        column: x => x.ExpectedHeadId,
                        principalTable: "ClaimInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimInputs_ClaimInputs_RootId",
                        column: x => x.RootId,
                        principalTable: "ClaimInputs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimInputs_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimInputs_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntensiveSupportEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedHeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntensiveSupportEpisodes", x => x.Id);
                    table.CheckConstraint("CK_IntensiveSupportEpisodes_CancelPayload", "(\"Kind\" = 3 AND \"StartDate\" IS NULL) OR (\"Kind\" IN (1, 2) AND \"StartDate\" IS NOT NULL)");
                    table.CheckConstraint("CK_IntensiveSupportEpisodes_RevisionLineage", "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND ((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_IntensiveSupportEpisodes_IntensiveSupportEpisodes_ExpectedHeadId",
                        column: x => x.ExpectedHeadId,
                        principalTable: "IntensiveSupportEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntensiveSupportEpisodes_IntensiveSupportEpisodes_RootId",
                        column: x => x.RootId,
                        principalTable: "IntensiveSupportEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntensiveSupportEpisodes_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntensiveSupportEpisodes_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficeClaimProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RootId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedHeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MasterVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ReformStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    DesignationDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SupportStartDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EarlierRegistrationMonthKey = table.Column<int>(type: "INTEGER", nullable: true),
                    LaterRegistrationMonthKey = table.Column<int>(type: "INTEGER", nullable: true),
                    ReformComparisonEvidenceDocumentId = table.Column<string>(type: "TEXT", nullable: true),
                    FiledTransitionPeriod = table.Column<string>(type: "TEXT", nullable: true),
                    FiledTransitionEvidenceDocumentId = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceDocumentId = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmationReason = table.Column<string>(type: "TEXT", nullable: true),
                    AverageWageBandOption_Kind = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageWageBandOption_OfficialOptionCode = table.Column<int>(type: "INTEGER", nullable: true),
                    EarlierRegisteredBandOption_Option_Kind = table.Column<int>(type: "INTEGER", nullable: true),
                    EarlierRegisteredBandOption_MasterVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    EarlierRegisteredBandOption_Option_OfficialOptionCode = table.Column<int>(type: "INTEGER", nullable: true),
                    LaterRegisteredBandOption_Option_Kind = table.Column<int>(type: "INTEGER", nullable: true),
                    LaterRegisteredBandOption_MasterVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    LaterRegisteredBandOption_Option_OfficialOptionCode = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeClaimProfiles", x => x.Id);
                    table.CheckConstraint("CK_OfficeClaimProfiles_AverageWageBandOption", "((\"AverageWageBandOption_Kind\" IS NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NULL) OR (\"AverageWageBandOption_Kind\" IS NOT NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NOT NULL AND \"AverageWageBandOption_Kind\" IN (1, 2, 3) AND \"AverageWageBandOption_OfficialOptionCode\" > 0))");
                    table.CheckConstraint("CK_OfficeClaimProfiles_CancelPayload", "\"Kind\" <> 3 OR (\"MasterVersion\" IS NULL AND \"ReformStatus\" IS NULL AND \"AverageWageBandOption_Kind\" IS NULL AND \"AverageWageBandOption_OfficialOptionCode\" IS NULL AND \"DesignationDate\" IS NULL AND \"SupportStartDate\" IS NULL AND \"EarlierRegisteredBandOption_MasterVersion\" IS NULL AND \"EarlierRegisteredBandOption_Option_Kind\" IS NULL AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" IS NULL AND \"EarlierRegistrationMonthKey\" IS NULL AND \"LaterRegisteredBandOption_MasterVersion\" IS NULL AND \"LaterRegisteredBandOption_Option_Kind\" IS NULL AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" IS NULL AND \"LaterRegistrationMonthKey\" IS NULL AND \"ReformComparisonEvidenceDocumentId\" IS NULL AND \"FiledTransitionPeriod\" IS NULL AND \"FiledTransitionEvidenceDocumentId\" IS NULL AND \"EvidenceDocumentId\" IS NULL AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL)");
                    table.CheckConstraint("CK_OfficeClaimProfiles_EarlierRegisteredBandOption", "((\"EarlierRegisteredBandOption_MasterVersion\" IS NULL AND \"EarlierRegisteredBandOption_Option_Kind\" IS NULL AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" IS NULL) OR (\"EarlierRegisteredBandOption_MasterVersion\" IS NOT NULL AND \"EarlierRegisteredBandOption_Option_Kind\" IS NOT NULL AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" IS NOT NULL AND length(trim(\"EarlierRegisteredBandOption_MasterVersion\")) BETWEEN 1 AND 64 AND \"EarlierRegisteredBandOption_Option_Kind\" IN (1, 2, 3) AND \"EarlierRegisteredBandOption_Option_OfficialOptionCode\" > 0))");
                    table.CheckConstraint("CK_OfficeClaimProfiles_LaterRegisteredBandOption", "((\"LaterRegisteredBandOption_MasterVersion\" IS NULL AND \"LaterRegisteredBandOption_Option_Kind\" IS NULL AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" IS NULL) OR (\"LaterRegisteredBandOption_MasterVersion\" IS NOT NULL AND \"LaterRegisteredBandOption_Option_Kind\" IS NOT NULL AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" IS NOT NULL AND length(trim(\"LaterRegisteredBandOption_MasterVersion\")) BETWEEN 1 AND 64 AND \"LaterRegisteredBandOption_Option_Kind\" IN (1, 2, 3) AND \"LaterRegisteredBandOption_Option_OfficialOptionCode\" > 0))");
                    table.CheckConstraint("CK_OfficeClaimProfiles_ReformStatus_ClosedSet", "\"ReformStatus\" IS NULL OR \"ReformStatus\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_OfficeClaimProfiles_RevisionLineage", "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND ((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_OfficeClaimProfiles_OfficeClaimProfiles_ExpectedHeadId",
                        column: x => x.ExpectedHeadId,
                        principalTable: "OfficeClaimProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeClaimProfiles_OfficeClaimProfiles_RootId",
                        column: x => x.RootId,
                        principalTable: "OfficeClaimProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficeClaimProfiles_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UpperLimitManagementStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedHeadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ServiceMonthKey = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CertificateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ManagingOfficeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MunicipalityNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CertificateNumber = table.Column<string>(type: "TEXT", nullable: false),
                    UpperLimitManagementApplicability = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificateManagingOfficeNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ManagingOfficeNumber = table.Column<string>(type: "TEXT", nullable: false),
                    ManagingOfficeName = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalCreationKind = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OriginalDocumentReference = table.Column<string>(type: "TEXT", nullable: true),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "TEXT", nullable: true),
                    ConfirmationReason = table.Column<string>(type: "TEXT", nullable: true),
                    Result = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificateMonthlyCostCap_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    CertificateMonthlyCostCap_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalCostYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalCostYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalManagedBurdenYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalManagedBurdenYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalPreManagementBurdenYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalPreManagementBurdenYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpperLimitManagementStatements", x => x.Id);
                    table.CheckConstraint("CK_UpperLimitManagementStatements_CancelPayload", "\"Kind\" <> 3 OR (\"MunicipalityNumber\" = '' AND \"CertificateNumber\" = '' AND \"CertificateMonthlyCostCap_IsEntered\" = 0 AND \"CertificateMonthlyCostCap_ValueYen\" IS NULL AND \"UpperLimitManagementApplicability\" = 0 AND \"CertificateManagingOfficeNumber\" = '' AND \"ManagingOfficeNumber\" = '' AND \"ManagingOfficeName\" = '' AND \"OriginalCreationKind\" = '' AND \"ReceivedAt\" IS NULL AND \"OriginalDocumentReference\" IS NULL AND \"IsConfirmed\" = 0 AND \"ConfirmedAt\" IS NULL AND \"ConfirmedBy\" IS NULL AND \"ConfirmationReason\" IS NULL AND \"Result\" = 0 AND \"TotalCostYen_IsEntered\" = 0 AND \"TotalCostYen_ValueYen\" IS NULL AND \"TotalPreManagementBurdenYen_IsEntered\" = 0 AND \"TotalPreManagementBurdenYen_ValueYen\" IS NULL AND \"TotalManagedBurdenYen_IsEntered\" = 0 AND \"TotalManagedBurdenYen_ValueYen\" IS NULL)");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_CertificateMonthlyCostCap_EnteredYen", "((\"CertificateMonthlyCostCap_IsEntered\" = 0 AND \"CertificateMonthlyCostCap_ValueYen\" IS NULL) OR (\"CertificateMonthlyCostCap_IsEntered\" = 1 AND \"CertificateMonthlyCostCap_ValueYen\" IS NOT NULL AND \"CertificateMonthlyCostCap_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_Result_ClosedSet", "\"Result\" IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_RevisionLineage", "\"Revision\" >= 1 AND \"Kind\" IN (1, 2, 3) AND ((\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL))");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_TotalCostYen_EnteredYen", "((\"TotalCostYen_IsEntered\" = 0 AND \"TotalCostYen_ValueYen\" IS NULL) OR (\"TotalCostYen_IsEntered\" = 1 AND \"TotalCostYen_ValueYen\" IS NOT NULL AND \"TotalCostYen_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_TotalManagedBurdenYen_EnteredYen", "((\"TotalManagedBurdenYen_IsEntered\" = 0 AND \"TotalManagedBurdenYen_ValueYen\" IS NULL) OR (\"TotalManagedBurdenYen_IsEntered\" = 1 AND \"TotalManagedBurdenYen_ValueYen\" IS NOT NULL AND \"TotalManagedBurdenYen_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_TotalPreManagementBurdenYen_EnteredYen", "((\"TotalPreManagementBurdenYen_IsEntered\" = 0 AND \"TotalPreManagementBurdenYen_ValueYen\" IS NULL) OR (\"TotalPreManagementBurdenYen_IsEntered\" = 1 AND \"TotalPreManagementBurdenYen_ValueYen\" IS NOT NULL AND \"TotalPreManagementBurdenYen_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_UpperLimitManagementStatements_UpperLimitManagementApplicability_ClosedSet", "\"UpperLimitManagementApplicability\" IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_UpperLimitManagementStatements_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpperLimitManagementStatements_Offices_ManagingOfficeId",
                        column: x => x.ManagingOfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpperLimitManagementStatements_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpperLimitManagementStatements_UpperLimitManagementStatements_ExpectedHeadId",
                        column: x => x.ExpectedHeadId,
                        principalTable: "UpperLimitManagementStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UpperLimitManagementStatements_UpperLimitManagementStatements_RootId",
                        column: x => x.RootId,
                        principalTable: "UpperLimitManagementStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UpperLimitManagementStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    OfficeNumber = table.Column<string>(type: "TEXT", nullable: false),
                    OfficeName = table.Column<string>(type: "TEXT", nullable: false),
                    ManagedBurdenYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    ManagedBurdenYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    PreManagementBurdenYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreManagementBurdenYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalCostYen_IsEntered = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalCostYen_ValueYen = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpperLimitManagementStatementLines", x => x.Id);
                    table.CheckConstraint("CK_UpperLimitManagementStatementLines_LineNumber", "\"LineNumber\" > 0");
                    table.CheckConstraint("CK_UpperLimitManagementStatementLines_ManagedBurdenYen_EnteredYen", "((\"ManagedBurdenYen_IsEntered\" = 0 AND \"ManagedBurdenYen_ValueYen\" IS NULL) OR (\"ManagedBurdenYen_IsEntered\" = 1 AND \"ManagedBurdenYen_ValueYen\" IS NOT NULL AND \"ManagedBurdenYen_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_UpperLimitManagementStatementLines_PreManagementBurdenYen_EnteredYen", "((\"PreManagementBurdenYen_IsEntered\" = 0 AND \"PreManagementBurdenYen_ValueYen\" IS NULL) OR (\"PreManagementBurdenYen_IsEntered\" = 1 AND \"PreManagementBurdenYen_ValueYen\" IS NOT NULL AND \"PreManagementBurdenYen_ValueYen\" >= 0))");
                    table.CheckConstraint("CK_UpperLimitManagementStatementLines_TotalCostYen_EnteredYen", "((\"TotalCostYen_IsEntered\" = 0 AND \"TotalCostYen_ValueYen\" IS NULL) OR (\"TotalCostYen_IsEntered\" = 1 AND \"TotalCostYen_ValueYen\" IS NOT NULL AND \"TotalCostYen_ValueYen\" >= 0))");
                    table.ForeignKey(
                        name: "FK_UpperLimitManagementStatementLines_UpperLimitManagementStatements_StatementId",
                        column: x => x.StatementId,
                        principalTable: "UpperLimitManagementStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                "UPDATE \"Certificates\" SET \"RootCertificateId\" = \"Id\", \"Revision\" = 1, " +
                "\"ExpectedHeadCertificateId\" = NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "Revision",
                table: "Certificates",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "RootCertificateId",
                table: "Certificates",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Certificates_ExpectedHeadCertificateId",
                table: "Certificates",
                column: "ExpectedHeadCertificateId",
                unique: true,
                filter: "\"ExpectedHeadCertificateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Certificates_RootCertificateId_Revision",
                table: "Certificates",
                columns: new[] { "RootCertificateId", "Revision" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Certificates_RevisionLineage",
                table: "Certificates",
                sql: "\"Revision\" >= 1 AND ((\"Revision\" = 1 AND \"RootCertificateId\" = \"Id\" AND \"ExpectedHeadCertificateId\" IS NULL) OR (\"Revision\" >= 2 AND \"RootCertificateId\" <> \"Id\" AND \"ExpectedHeadCertificateId\" IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "UX_AverageWageAnnualEvidences_ExpectedHeadId",
                table: "AverageWageAnnualEvidences",
                column: "ExpectedHeadId",
                unique: true,
                filter: "\"ExpectedHeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_AverageWageAnnualEvidences_OfficeId_SourceFiscalYear_NewOnly",
                table: "AverageWageAnnualEvidences",
                columns: new[] { "OfficeId", "SourceFiscalYear" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_AverageWageAnnualEvidences_RootId_Revision",
                table: "AverageWageAnnualEvidences",
                columns: new[] { "RootId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CertificateClaimEvidences_CertificateId_Validity_NewOnly",
                table: "CertificateClaimEvidences",
                columns: new[] { "CertificateId", "Validity" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_CertificateClaimEvidences_ExpectedHeadId",
                table: "CertificateClaimEvidences",
                column: "ExpectedHeadId",
                unique: true,
                filter: "\"ExpectedHeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CertificateClaimEvidences_RootId_Revision",
                table: "CertificateClaimEvidences",
                columns: new[] { "RootId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimInputs_RecipientId",
                table: "ClaimInputs",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "UX_ClaimInputs_ExpectedHeadId",
                table: "ClaimInputs",
                column: "ExpectedHeadId",
                unique: true,
                filter: "\"ExpectedHeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ClaimInputs_OfficeId_RecipientId_ServiceMonthKey_NewOnly",
                table: "ClaimInputs",
                columns: new[] { "OfficeId", "RecipientId", "ServiceMonthKey" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ClaimInputs_RootId_Revision",
                table: "ClaimInputs",
                columns: new[] { "RootId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntensiveSupportEpisodes_RecipientId",
                table: "IntensiveSupportEpisodes",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "UX_IntensiveSupportEpisodes_ExpectedHeadId",
                table: "IntensiveSupportEpisodes",
                column: "ExpectedHeadId",
                unique: true,
                filter: "\"ExpectedHeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_IntensiveSupportEpisodes_OfficeId_RecipientId_NewOnly",
                table: "IntensiveSupportEpisodes",
                columns: new[] { "OfficeId", "RecipientId" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_IntensiveSupportEpisodes_RootId_Revision",
                table: "IntensiveSupportEpisodes",
                columns: new[] { "RootId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_OfficeClaimProfiles_ExpectedHeadId",
                table: "OfficeClaimProfiles",
                column: "ExpectedHeadId",
                unique: true,
                filter: "\"ExpectedHeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OfficeClaimProfiles_OfficeId_EffectiveFrom_EffectiveTo_ClosedNewOnly",
                table: "OfficeClaimProfiles",
                columns: new[] { "OfficeId", "EffectiveFrom", "EffectiveTo" },
                unique: true,
                filter: "\"Kind\" = 1 AND \"EffectiveTo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OfficeClaimProfiles_OfficeId_EffectiveFrom_OpenNewOnly",
                table: "OfficeClaimProfiles",
                columns: new[] { "OfficeId", "EffectiveFrom" },
                unique: true,
                filter: "\"Kind\" = 1 AND \"EffectiveTo\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_OfficeClaimProfiles_RootId_Revision",
                table: "OfficeClaimProfiles",
                columns: new[] { "RootId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UpperLimitManagementStatementLines_StatementId_LineNumber",
                table: "UpperLimitManagementStatementLines",
                columns: new[] { "StatementId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UpperLimitManagementStatementLines_StatementId_OfficeNumber",
                table: "UpperLimitManagementStatementLines",
                columns: new[] { "StatementId", "OfficeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UpperLimitManagementStatements_CertificateId",
                table: "UpperLimitManagementStatements",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_UpperLimitManagementStatements_ManagingOfficeId",
                table: "UpperLimitManagementStatements",
                column: "ManagingOfficeId");

            migrationBuilder.CreateIndex(
                name: "UX_UpperLimitManagementStatements_ExpectedHeadId",
                table: "UpperLimitManagementStatements",
                column: "ExpectedHeadId",
                unique: true,
                filter: "\"ExpectedHeadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UpperLimitManagementStatements_RecipientId_CertificateId_ManagingOfficeId_ServiceMonthKey_NewOnly",
                table: "UpperLimitManagementStatements",
                columns: new[] { "RecipientId", "CertificateId", "ManagingOfficeId", "ServiceMonthKey" },
                unique: true,
                filter: "\"Kind\" = 1");

            migrationBuilder.CreateIndex(
                name: "UX_UpperLimitManagementStatements_RootId_Revision",
                table: "UpperLimitManagementStatements",
                columns: new[] { "RootId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AverageWageAnnualEvidences");

            migrationBuilder.DropTable(
                name: "CertificateClaimEvidences");

            migrationBuilder.DropTable(
                name: "ClaimInputs");

            migrationBuilder.DropTable(
                name: "IntensiveSupportEpisodes");

            migrationBuilder.DropTable(
                name: "OfficeClaimProfiles");

            migrationBuilder.DropTable(
                name: "UpperLimitManagementStatementLines");

            migrationBuilder.DropTable(
                name: "UpperLimitManagementStatements");

            migrationBuilder.DropIndex(
                name: "UX_Certificates_ExpectedHeadCertificateId",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "UX_Certificates_RootCertificateId_Revision",
                table: "Certificates");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Certificates_RevisionLineage",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "RepresentativeTitleAndName",
                table: "Offices");

            migrationBuilder.DropColumn(
                name: "EmergencyAdmissionApplied",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "IntensiveSupportApplied",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "MedicalCoordinationType",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "OffsiteSupportApplied",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "RecipientConfirmation",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "RegionalCollaborationApplied",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "ServiceEndTime",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "ServiceStartTime",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "SpecialVisitSupportMinutes",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "TrialUseSupportType",
                table: "DailyRecords");

            migrationBuilder.DropColumn(
                name: "CertificateEntryNumber",
                table: "ContractedProviders");

            migrationBuilder.DropColumn(
                name: "ExpectedHeadCertificateId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "MunicipalityNumber",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "RootCertificateId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "SubsidyMunicipalityNumber",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "UpperLimitManagementProviderNumber",
                table: "Certificates");
        }
    }
}
