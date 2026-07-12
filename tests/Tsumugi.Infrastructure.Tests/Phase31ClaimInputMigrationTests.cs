using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

public sealed class Phase31ClaimInputMigrationTests
{
    private const string MigrationSuffix = "_Phase31ClaimInputFoundation";

    private static readonly string[] NewTables =
    [
        "ClaimInputs",
        "IntensiveSupportEpisodes",
        "AverageWageAnnualEvidences",
        "OfficeClaimProfiles",
        "CertificateClaimEvidences",
        "UpperLimitManagementStatements",
        "UpperLimitManagementStatementLines",
    ];

    private static readonly HeaderSpec[] HeaderSpecs =
    [
        new(
            "ClaimInputs",
            ["OfficeId", "RecipientId", "ServiceMonthKey"],
            "UX_ClaimInputs_OfficeId_RecipientId_ServiceMonthKey_NewOnly",
            [
                SelfForeignKey("ClaimInputs", "RootId"),
                SelfForeignKey("ClaimInputs", "ExpectedHeadId"),
                BusinessForeignKey("OfficeId", "Offices"),
                BusinessForeignKey("RecipientId", "Recipients"),
            ],
            [
                "UpperLimitManagementResult", "UpperLimitManagedAmountYen", "MunicipalSubsidyAmountYen",
                "ExceptionalUsageStartMonthKey", "ExceptionalUsageEndMonthKey", "ExceptionalUsageDays",
                "StandardUsageDayTotal",
            ]),
        new(
            "IntensiveSupportEpisodes",
            ["OfficeId", "RecipientId"],
            "UX_IntensiveSupportEpisodes_OfficeId_RecipientId_NewOnly",
            [
                SelfForeignKey("IntensiveSupportEpisodes", "RootId"),
                SelfForeignKey("IntensiveSupportEpisodes", "ExpectedHeadId"),
                BusinessForeignKey("OfficeId", "Offices"),
                BusinessForeignKey("RecipientId", "Recipients"),
            ],
            ["StartDate"]),
        new(
            "AverageWageAnnualEvidences",
            ["OfficeId", "SourceFiscalYear"],
            "UX_AverageWageAnnualEvidences_OfficeId_SourceFiscalYear_NewOnly",
            [
                SelfForeignKey("AverageWageAnnualEvidences", "RootId"),
                SelfForeignKey("AverageWageAnnualEvidences", "ExpectedHeadId"),
                BusinessForeignKey("OfficeId", "Offices"),
            ],
            [
                "AnnualWagePaidYen", "AnnualExtendedUsers", "AnnualOpeningDays", "Completeness",
                "EvidenceDocumentId", "DailyEvidenceReference", "MonthlyEvidenceReference", "ConfirmedAt",
                "ConfirmedBy", "ConfirmationReason",
            ]),
        new(
            "OfficeClaimProfiles",
            ["OfficeId", "EffectiveFrom"],
            "UX_OfficeClaimProfiles_OfficeId_EffectiveFrom_OpenNewOnly",
            [
                SelfForeignKey("OfficeClaimProfiles", "RootId"),
                SelfForeignKey("OfficeClaimProfiles", "ExpectedHeadId"),
                BusinessForeignKey("OfficeId", "Offices"),
            ],
            [
                "MasterVersion", "ReformStatus", "AverageWageBandOption_Kind",
                "AverageWageBandOption_OfficialOptionCode", "DesignationDate", "SupportStartDate",
                "EarlierRegisteredBandOption_MasterVersion", "EarlierRegisteredBandOption_Option_Kind",
                "EarlierRegisteredBandOption_Option_OfficialOptionCode", "EarlierRegistrationMonthKey",
                "LaterRegisteredBandOption_MasterVersion", "LaterRegisteredBandOption_Option_Kind",
                "LaterRegisteredBandOption_Option_OfficialOptionCode", "LaterRegistrationMonthKey",
                "ReformComparisonEvidenceDocumentId", "FiledTransitionPeriod",
                "FiledTransitionEvidenceDocumentId", "EvidenceDocumentId", "ConfirmedAt", "ConfirmedBy",
                "ConfirmationReason",
            ]),
        new(
            "CertificateClaimEvidences",
            ["CertificateId", "Validity"],
            "UX_CertificateClaimEvidences_CertificateId_Validity_NewOnly",
            [
                SelfForeignKey("CertificateClaimEvidences", "RootId"),
                SelfForeignKey("CertificateClaimEvidences", "ExpectedHeadId"),
                BusinessForeignKey("CertificateId", "Certificates"),
            ],
            [
                "MonthlyCostCap_IsEntered", "MonthlyCostCap_ValueYen", "UpperLimitManagementApplicability",
                "UpperLimitManagementOfficeNumber", "Article31Status", "Article31AmountYen_IsEntered",
                "Article31AmountYen_ValueYen", "Article31EffectivePeriod", "OriginalDocumentReference",
                "ConfirmedAt", "ConfirmedBy", "ConfirmationReason",
            ]),
        new(
            "UpperLimitManagementStatements",
            ["RecipientId", "CertificateId", "ManagingOfficeId", "ServiceMonthKey"],
            "UX_UpperLimitManagementStatements_RecipientId_CertificateId_ManagingOfficeId_ServiceMonthKey_NewOnly",
            [
                SelfForeignKey("UpperLimitManagementStatements", "RootId"),
                SelfForeignKey("UpperLimitManagementStatements", "ExpectedHeadId"),
                BusinessForeignKey("RecipientId", "Recipients"),
                BusinessForeignKey("CertificateId", "Certificates"),
                BusinessForeignKey("ManagingOfficeId", "Offices"),
            ],
            [
                "MunicipalityNumber", "CertificateNumber", "CertificateMonthlyCostCap_IsEntered",
                "CertificateMonthlyCostCap_ValueYen", "UpperLimitManagementApplicability",
                "CertificateManagingOfficeNumber", "ManagingOfficeNumber", "ManagingOfficeName",
                "OriginalCreationKind", "ReceivedAt", "OriginalDocumentReference", "IsConfirmed", "ConfirmedAt",
                "ConfirmedBy", "ConfirmationReason", "Result", "TotalCostYen_IsEntered", "TotalCostYen_ValueYen",
                "TotalPreManagementBurdenYen_IsEntered", "TotalPreManagementBurdenYen_ValueYen",
                "TotalManagedBurdenYen_IsEntered", "TotalManagedBurdenYen_ValueYen",
            ]),
    ];

    private static readonly (string Table, string Prefix)[] EnteredYenSpecs =
    [
        ("CertificateClaimEvidences", "MonthlyCostCap"),
        ("CertificateClaimEvidences", "Article31AmountYen"),
        ("UpperLimitManagementStatements", "CertificateMonthlyCostCap"),
        ("UpperLimitManagementStatements", "TotalCostYen"),
        ("UpperLimitManagementStatements", "TotalPreManagementBurdenYen"),
        ("UpperLimitManagementStatements", "TotalManagedBurdenYen"),
        ("UpperLimitManagementStatementLines", "TotalCostYen"),
        ("UpperLimitManagementStatementLines", "PreManagementBurdenYen"),
        ("UpperLimitManagementStatementLines", "ManagedBurdenYen"),
    ];

    private static readonly (string Table, string Column)[] ClosedEnumSpecs =
    [
        ("ClaimInputs", "UpperLimitManagementResult"),
        ("AverageWageAnnualEvidences", "Completeness"),
        ("OfficeClaimProfiles", "ReformStatus"),
        ("CertificateClaimEvidences", "UpperLimitManagementApplicability"),
        ("CertificateClaimEvidences", "Article31Status"),
        ("UpperLimitManagementStatements", "UpperLimitManagementApplicability"),
        ("UpperLimitManagementStatements", "Result"),
    ];

    [Fact]
    public async Task Target_up_preserves_legacy_rows_backfills_certificate_lineage_and_leaves_claim_inputs_unset()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();

        await migrator.MigrateAsync(previous);
        var seed = await SeedLegacyRowsAsync(database.Connection);

        await migrator.MigrateAsync(target);
        await AssertLegacySnapshotAsync(database.Connection, seed.Snapshot);

        (await CountRowsAsync(database.Connection, "Offices")).Should().Be(1);
        (await CountRowsAsync(database.Connection, "Recipients")).Should().Be(1);
        (await CountRowsAsync(database.Connection, "Certificates")).Should().Be(2);
        (await CountRowsAsync(database.Connection, "ContractedProviders")).Should().Be(1);
        (await CountRowsAsync(database.Connection, "DailyRecords")).Should().Be(1);

        foreach (var certificateId in seed.CertificateIds)
        {
            var row = await ReadSingleRowAsync(
                database.Connection,
                """
                SELECT "RootCertificateId", "Revision", "ExpectedHeadCertificateId",
                       "MunicipalityNumber", "SubsidyMunicipalityNumber", "UpperLimitManagementProviderNumber"
                FROM "Certificates" WHERE "Id" = $id;
                """,
                ("$id", certificateId));
            row["RootCertificateId"].Should().Be(certificateId.ToString());
            row["Revision"].Should().Be(1L);
            row["ExpectedHeadCertificateId"].Should().BeNull();
            row["MunicipalityNumber"].Should().BeNull();
            row["SubsidyMunicipalityNumber"].Should().BeNull();
            row["UpperLimitManagementProviderNumber"].Should().BeNull();
        }

        var office = await ReadSingleRowAsync(
            database.Connection,
            """
            SELECT "PostalCode", "Address", "PhoneNumber", "RepresentativeTitleAndName"
            FROM "Offices" WHERE "Id" = $id;
            """,
            ("$id", seed.OfficeId));
        office.Values.Should().AllSatisfy(value => value.Should().BeNull());

        (await ExecuteScalarAsync(
            database.Connection,
            "SELECT \"CertificateEntryNumber\" FROM \"ContractedProviders\" WHERE \"Id\" = $id;",
            ("$id", seed.ContractedProviderId))).Should().BeNull();

        var dailyRecord = await ReadSingleRowAsync(
            database.Connection,
            """
            SELECT "ServiceStartTime", "ServiceEndTime", "SpecialVisitSupportMinutes",
                   "OffsiteSupportApplied", "RegionalCollaborationApplied", "IntensiveSupportApplied",
                   "EmergencyAdmissionApplied", "MedicalCoordinationType", "TrialUseSupportType",
                   "RecipientConfirmation"
            FROM "DailyRecords" WHERE "Id" = $id;
            """,
            ("$id", seed.DailyRecordId));
        foreach (var column in new[]
                 {
                     "ServiceStartTime", "ServiceEndTime", "SpecialVisitSupportMinutes",
                     "OffsiteSupportApplied", "RegionalCollaborationApplied", "IntensiveSupportApplied",
                     "EmergencyAdmissionApplied",
                 })
            dailyRecord[column].Should().BeNull();
        foreach (var column in new[] { "MedicalCoordinationType", "TrialUseSupportType", "RecipientConfirmation" })
            dailyRecord[column].Should().Be(0L);

        foreach (var table in NewTables)
            (await CountRowsAsync(database.Connection, table)).Should().Be(0, $"{table} must not infer rows from legacy data");
    }

    [Fact]
    public async Task Target_schema_has_named_lineage_business_foreign_key_cancel_and_entered_yen_constraints()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, _) = ResolveMigration(database.Context);
        await database.Context.GetService<IMigrator>().MigrateAsync(target);

        foreach (var table in NewTables)
            (await TableExistsAsync(database.Connection, table)).Should().BeTrue();

        await AssertColumnAsync(database.Connection, "Certificates", "RootCertificateId", "TEXT", true, null);
        await AssertColumnAsync(database.Connection, "Certificates", "Revision", "INTEGER", true, null);
        await AssertColumnAsync(database.Connection, "Certificates", "ExpectedHeadCertificateId", "TEXT", false, null);
        await AssertNamedCheckAsync(
            database.Connection,
            "Certificates",
            "CK_Certificates_RevisionLineage",
            "\"Revision\" >= 1",
            "\"Revision\" = 1 AND \"RootCertificateId\" = \"Id\" AND \"ExpectedHeadCertificateId\" IS NULL",
            "\"Revision\" >= 2 AND \"RootCertificateId\" <> \"Id\" AND \"ExpectedHeadCertificateId\" IS NOT NULL");
        await AssertUniqueIndexAsync(
            database.Connection,
            "Certificates",
            "UX_Certificates_RootCertificateId_Revision",
            ["RootCertificateId", "Revision"],
            expectedWhereClause: null);
        await AssertUniqueIndexAsync(
            database.Connection,
            "Certificates",
            "UX_Certificates_ExpectedHeadCertificateId",
            ["ExpectedHeadCertificateId"],
            expectedWhereClause: "WHERE \"ExpectedHeadCertificateId\" IS NOT NULL");
        (await ReadForeignKeysAsync(database.Connection, "Certificates")).Should().BeEmpty();

        foreach (var column in new[]
                 {
                     "PostalCode", "Address", "PhoneNumber", "RepresentativeTitleAndName",
                 })
            await AssertColumnAsync(database.Connection, "Offices", column, "TEXT", false, null);
        foreach (var column in new[]
                 {
                     "MunicipalityNumber", "SubsidyMunicipalityNumber", "UpperLimitManagementProviderNumber",
                 })
            await AssertColumnAsync(database.Connection, "Certificates", column, "TEXT", false, null);
        await AssertColumnAsync(
            database.Connection,
            "ContractedProviders",
            "CertificateEntryNumber",
            "INTEGER",
            false,
            null);
        foreach (var column in new[]
                 {
                     "ServiceStartTime", "ServiceEndTime",
                 })
            await AssertColumnAsync(database.Connection, "DailyRecords", column, "TEXT", false, null);
        foreach (var column in new[]
                 {
                     "SpecialVisitSupportMinutes", "OffsiteSupportApplied", "RegionalCollaborationApplied",
                     "IntensiveSupportApplied", "EmergencyAdmissionApplied",
                 })
            await AssertColumnAsync(database.Connection, "DailyRecords", column, "INTEGER", false, null);
        foreach (var column in new[]
                 {
                     "MedicalCoordinationType", "TrialUseSupportType", "RecipientConfirmation",
                 })
            await AssertColumnAsync(database.Connection, "DailyRecords", column, "INTEGER", true, "0");

        foreach (var spec in HeaderSpecs)
        {
            await AssertColumnAsync(database.Connection, spec.Table, "RootId", "TEXT", true, null);
            await AssertColumnAsync(database.Connection, spec.Table, "Revision", "INTEGER", true, null);
            await AssertColumnAsync(database.Connection, spec.Table, "Kind", "INTEGER", true, null);
            await AssertColumnAsync(database.Connection, spec.Table, "ExpectedHeadId", "TEXT", false, null);
            await AssertNamedCheckAsync(
                database.Connection,
                spec.Table,
                $"CK_{spec.Table}_RevisionLineage",
                "\"Revision\" >= 1",
                "\"Revision\" = 1 AND \"RootId\" = \"Id\" AND \"Kind\" = 1 AND \"ExpectedHeadId\" IS NULL",
                "\"Revision\" >= 2 AND \"RootId\" <> \"Id\" AND \"Kind\" IN (2, 3) AND \"ExpectedHeadId\" IS NOT NULL");
            await AssertCancelPayloadCheckAsync(database.Connection, spec);
            await AssertUniqueIndexAsync(
                database.Connection,
                spec.Table,
                $"UX_{spec.Table}_RootId_Revision",
                ["RootId", "Revision"],
                expectedWhereClause: null);
            await AssertUniqueIndexAsync(
                database.Connection,
                spec.Table,
                $"UX_{spec.Table}_ExpectedHeadId",
                ["ExpectedHeadId"],
                expectedWhereClause: "WHERE \"ExpectedHeadId\" IS NOT NULL");
            if (spec.Table == "OfficeClaimProfiles")
            {
                await AssertUniqueIndexAsync(
                    database.Connection,
                    spec.Table,
                    spec.BusinessIndexName,
                    spec.BusinessColumns,
                    expectedWhereClause: "WHERE \"Kind\" = 1 AND \"EffectiveTo\" IS NULL");
                await AssertUniqueIndexAsync(
                    database.Connection,
                    spec.Table,
                    "UX_OfficeClaimProfiles_OfficeId_EffectiveFrom_EffectiveTo_ClosedNewOnly",
                    ["OfficeId", "EffectiveFrom", "EffectiveTo"],
                    expectedWhereClause: "WHERE \"Kind\" = 1 AND \"EffectiveTo\" IS NOT NULL");
            }
            else
            {
                await AssertUniqueIndexAsync(
                    database.Connection,
                    spec.Table,
                    spec.BusinessIndexName,
                    spec.BusinessColumns,
                    expectedWhereClause: "WHERE \"Kind\" = 1");
            }

            var foreignKeys = await ReadForeignKeysAsync(database.Connection, spec.Table);
            foreignKeys.Should().BeEquivalentTo(spec.ForeignKeys);

            var createSql = await ReadCreateTableSqlAsync(database.Connection, spec.Table);
            foreach (var foreignKey in spec.ForeignKeys)
            {
                createSql.Should().Contain(
                    $"FK_{spec.Table}_{foreignKey.PrincipalTable}_{foreignKey.FromColumn}");
            }
        }

        await AssertEnteredYenChecksAsync(
            database.Connection,
            "CertificateClaimEvidences",
            "MonthlyCostCap",
            "Article31AmountYen");
        await AssertEnteredYenChecksAsync(
            database.Connection,
            "UpperLimitManagementStatements",
            "CertificateMonthlyCostCap",
            "TotalCostYen",
            "TotalPreManagementBurdenYen",
            "TotalManagedBurdenYen");
        await AssertEnteredYenChecksAsync(
            database.Connection,
            "UpperLimitManagementStatementLines",
            "TotalCostYen",
            "PreManagementBurdenYen",
            "ManagedBurdenYen");

        await AssertUniqueIndexAsync(
            database.Connection,
            "UpperLimitManagementStatementLines",
            "UX_UpperLimitManagementStatementLines_StatementId_LineNumber",
            ["StatementId", "LineNumber"],
            expectedWhereClause: null);
        await AssertUniqueIndexAsync(
            database.Connection,
            "UpperLimitManagementStatementLines",
            "UX_UpperLimitManagementStatementLines_StatementId_OfficeNumber",
            ["StatementId", "OfficeNumber"],
            expectedWhereClause: null);
        await AssertNamedCheckAsync(
            database.Connection,
            "UpperLimitManagementStatementLines",
            "CK_UpperLimitManagementStatementLines_LineNumber",
            "\"LineNumber\" > 0");
        var lineForeignKeys = await ReadForeignKeysAsync(database.Connection, "UpperLimitManagementStatementLines");
        lineForeignKeys.Should().ContainSingle().Which.Should()
            .Be(new SqliteForeignKey("StatementId", "UpperLimitManagementStatements", "Id", "RESTRICT"));
        (await ReadCreateTableSqlAsync(database.Connection, "UpperLimitManagementStatementLines"))
            .Should().Contain("FK_UpperLimitManagementStatementLines_UpperLimitManagementStatements_StatementId");
    }

    [Fact]
    public async Task Target_raw_sql_rejects_named_checks_uniques_and_foreign_keys_with_sqlite_constraint_code()
    {
        await AssertViolationAsync(
            async (connection, seed) =>
            {
                await CloneInvalidCertificateLineageAsync(connection, seed.CertificateIds[0]);
            },
            expectedExtendedErrorCode: 275,
            expectedCheckName: "CK_Certificates_RevisionLineage");

        foreach (var spec in HeaderSpecs)
        {
            await AssertViolationAsync(
                async (connection, seed) =>
                {
                    var ids = await SeedValidClaimHeadersAsync(connection, seed);
                    await ExecuteNonQueryAsync(
                        connection,
                        $"UPDATE \"{spec.Table}\" SET \"Revision\" = 2 WHERE \"Id\" = $id;",
                        ("$id", ids[spec.Table]));
                },
                expectedExtendedErrorCode: 275,
                expectedCheckName: $"CK_{spec.Table}_RevisionLineage");

            await AssertIsolatedCancelPayloadViolationsAsync(spec);
        }

        foreach (var (table, prefix) in EnteredYenSpecs)
        {
            await AssertViolationAsync(
                async (connection, seed) =>
                {
                    var ids = await SeedValidClaimHeadersAsync(connection, seed);
                    await ExecuteNonQueryAsync(
                        connection,
                        $"""
                         UPDATE "{table}"
                         SET "{prefix}_IsEntered" = 0, "{prefix}_ValueYen" = 1
                         WHERE "Id" = $id;
                         """,
                        ("$id", ids[table]));
                },
                expectedExtendedErrorCode: 275,
                expectedCheckName: $"CK_{table}_{prefix}_EnteredYen");
        }

        foreach (var spec in HeaderSpecs)
        {
            await AssertViolationAsync(
                async (connection, seed) =>
                {
                    var roots = await SeedValidClaimHeadersAsync(connection, seed);
                    await CloneHeaderRevisionAsync(
                        connection,
                        spec,
                        roots[spec.Table],
                        Guid.NewGuid(),
                        1,
                        1,
                        null,
                        clearPayload: false,
                        resetRoot: true);
                },
                expectedExtendedErrorCode: 2067);
        }

        await AssertViolationAsync(
            async (connection, seed) =>
            {
                var rootId = Guid.NewGuid();
                await InsertClaimInputAsync(
                    connection,
                    rootId,
                    rootId,
                    1,
                    1,
                    null,
                    seed with { OfficeId = Guid.NewGuid() },
                    202608,
                    null);
            },
            expectedExtendedErrorCode: 787);

        await AssertSucceedsAsync(
            async (connection, seed) =>
            {
                var roots = await SeedValidClaimHeadersAsync(connection, seed);
                foreach (var spec in HeaderSpecs)
                {
                    var correctionId = Guid.NewGuid();
                    await CloneHeaderRevisionAsync(
                        connection, spec, roots[spec.Table], correctionId, 2, 2, roots[spec.Table], clearPayload: false);
                    await CloneHeaderRevisionAsync(
                        connection, spec, correctionId, Guid.NewGuid(), 3, 3, correctionId, clearPayload: true);
                    (await CountRowsAsync(connection, spec.Table)).Should().Be(3);
                }
            });
    }

    [Fact]
    public async Task Entered_yen_checks_reject_every_inconsistent_raw_state()
    {
        var invalidStates = new (bool IsEntered, int? ValueYen)[]
        {
            (false, 1),
            (true, null),
            (true, -1),
        };

        foreach (var (table, prefix) in EnteredYenSpecs)
        {
            foreach (var (isEntered, valueYen) in invalidStates)
            {
                await AssertViolationAsync(
                    async (connection, seed) =>
                    {
                        var ids = await SeedValidClaimHeadersAsync(connection, seed);
                        await ExecuteNonQueryAsync(
                            connection,
                            $"""
                             UPDATE "{table}"
                             SET "{prefix}_IsEntered" = $isEntered, "{prefix}_ValueYen" = $valueYen
                             WHERE "Id" = $id;
                             """,
                            ("$isEntered", isEntered),
                            ("$valueYen", valueYen),
                            ("$id", ids[table]));
                    },
                    expectedExtendedErrorCode: 275,
                    expectedCheckName: $"CK_{table}_{prefix}_EnteredYen");
            }
        }
    }

    [Fact]
    public async Task Office_profile_option_checks_reject_every_partial_null_raw_state()
    {
        var cases = new[]
        {
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_AverageWageBandOption", 1, null, null, null, null, null, null, null),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_AverageWageBandOption", null, 1, null, null, null, null, null, null),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_EarlierRegisteredBandOption", null, null, null, 1, 1, null, null, null),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_EarlierRegisteredBandOption", null, null, "r8-06", null, 1, null, null, null),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_EarlierRegisteredBandOption", null, null, "r8-06", 1, null, null, null, null),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_LaterRegisteredBandOption", null, null, null, null, null, null, 1, 1),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_LaterRegisteredBandOption", null, null, null, null, null, "r8-06", null, 1),
            new OfficeProfileOptionState("CK_OfficeClaimProfiles_LaterRegisteredBandOption", null, null, null, null, null, "r8-06", 1, null),
        };

        foreach (var state in cases)
        {
            await AssertViolationAsync(
                (connection, seed) => InsertOfficeClaimProfileWithOptionStateAsync(connection, seed, state),
                expectedExtendedErrorCode: 275,
                expectedCheckName: state.ConstraintName);
        }
    }

    [Fact]
    public async Task Header_lineage_unique_indexes_reject_each_duplicate_branch()
    {
        foreach (var spec in HeaderSpecs)
        {
            await AssertViolationAsync(
                async (connection, seed) =>
                {
                    var roots = await SeedValidClaimHeadersAsync(connection, seed);
                    var firstCorrectionId = Guid.NewGuid();
                    await CloneHeaderRevisionAsync(
                        connection, spec, roots[spec.Table], firstCorrectionId, 2, 2, roots[spec.Table], false);
                    await CloneHeaderRevisionAsync(
                        connection, spec, firstCorrectionId, Guid.NewGuid(), 2, 2, firstCorrectionId, false);
                },
                expectedExtendedErrorCode: 2067);

            await AssertViolationAsync(
                async (connection, seed) =>
                {
                    var roots = await SeedValidClaimHeadersAsync(connection, seed);
                    var firstCorrectionId = Guid.NewGuid();
                    await CloneHeaderRevisionAsync(
                        connection, spec, roots[spec.Table], firstCorrectionId, 2, 2, roots[spec.Table], false);
                    await CloneHeaderRevisionAsync(
                        connection, spec, firstCorrectionId, Guid.NewGuid(), 3, 2, roots[spec.Table], false);
                },
                expectedExtendedErrorCode: 2067);
        }
    }

    [Fact]
    public async Task Statement_line_constraints_reject_orphan_and_each_duplicate_branch()
    {
        await AssertViolationAsync(
            async (connection, seed) =>
            {
                var ids = await SeedValidClaimHeadersAsync(connection, seed);
                await CloneStatementLineAsync(
                    connection, ids["UpperLimitManagementStatementLines"], Guid.NewGuid(), Guid.NewGuid(), 2,
                    "orphan-office");
            },
            expectedExtendedErrorCode: 787);

        await AssertViolationAsync(
            async (connection, seed) =>
            {
                var ids = await SeedValidClaimHeadersAsync(connection, seed);
                await CloneStatementLineAsync(
                    connection, ids["UpperLimitManagementStatementLines"], Guid.NewGuid(),
                    ids["UpperLimitManagementStatements"], 1, "duplicate-line-office");
            },
            expectedExtendedErrorCode: 2067);

        await AssertViolationAsync(
            async (connection, seed) =>
            {
                var ids = await SeedValidClaimHeadersAsync(connection, seed);
                await CloneStatementLineAsync(
                    connection, ids["UpperLimitManagementStatementLines"], Guid.NewGuid(),
                    ids["UpperLimitManagementStatements"], 2, "1310000001");
            },
            expectedExtendedErrorCode: 2067);
    }

    [Fact]
    public async Task Persisted_closed_enums_reject_unknown_raw_values()
    {
        foreach (var (table, column) in ClosedEnumSpecs)
        {
            await AssertViolationAsync(
                async (connection, seed) =>
                {
                    var ids = await SeedValidClaimHeadersAsync(connection, seed);
                    await ExecuteNonQueryAsync(
                        connection,
                        $"UPDATE \"{table}\" SET \"{column}\" = 999 WHERE \"Id\" = $id;",
                        ("$id", ids[table]));
                },
                expectedExtendedErrorCode: 275,
                expectedCheckName: $"CK_{table}_{column}_ClosedSet");
        }
    }

    [Fact]
    public async Task Down_to_previous_removes_phase31_schema_preserves_legacy_rows_and_reup_is_deterministic()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();

        await migrator.MigrateAsync(previous);
        var seed = await SeedLegacyRowsAsync(database.Connection);
        await migrator.MigrateAsync(target);
        await AssertLegacySnapshotAsync(database.Connection, seed.Snapshot);
        await AssertCertificateBackfillAsync(database.Connection, seed.CertificateIds);

        await migrator.MigrateAsync(previous);
        await AssertLegacySnapshotAsync(database.Connection, seed.Snapshot);

        foreach (var table in NewTables)
            (await TableExistsAsync(database.Connection, table)).Should().BeFalse();
        var certificateColumns = await ReadColumnsAsync(database.Connection, "Certificates");
        certificateColumns.Should().NotContain(
            "RootCertificateId",
            "Revision",
            "ExpectedHeadCertificateId",
            "MunicipalityNumber",
            "SubsidyMunicipalityNumber",
            "UpperLimitManagementProviderNumber");
        (await CountRowsAsync(database.Connection, "Offices")).Should().Be(1);
        (await CountRowsAsync(database.Connection, "Recipients")).Should().Be(1);
        (await CountRowsAsync(database.Connection, "Certificates")).Should().Be(2);
        (await CountRowsAsync(database.Connection, "ContractedProviders")).Should().Be(1);
        (await CountRowsAsync(database.Connection, "DailyRecords")).Should().Be(1);

        await migrator.MigrateAsync(target);

        await AssertLegacySnapshotAsync(database.Connection, seed.Snapshot);
        await AssertCertificateBackfillAsync(database.Connection, seed.CertificateIds);
        foreach (var table in NewTables)
            (await CountRowsAsync(database.Connection, table)).Should().Be(0);
    }

    private static (string Target, string Previous) ResolveMigration(TsumugiDbContext context)
    {
        var migrations = context.Database.GetMigrations().ToArray();
        var targetIndex = Array.FindIndex(migrations, migration =>
            migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        targetIndex.Should().BeGreaterThan(0);
        return (migrations[targetIndex], migrations[targetIndex - 1]);
    }

    private static async Task<LegacySeed> SeedLegacyRowsAsync(SqliteConnection connection)
    {
        var officeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var recipientId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var certificateOneId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var certificateTwoId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var providerId = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var dailyRecordId = Guid.Parse("10000000-0000-0000-0000-000000000006");
        var createdAt = DateTimeOffset.Parse("2026-07-01T00:00:00+00:00", CultureInfo.InvariantCulture);

        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "Offices"
                ("Id", "OfficeNumber", "Name", "CreatedAt", "CreatedBy", "ConcurrencyToken", "RegionGrade", "ServiceCategory")
            VALUES ($id, '1310000001', 'migration office', $createdAt, 'migration-test', $token, 1, 1);
            """,
            ("$id", officeId), ("$createdAt", createdAt), ("$token", Guid.NewGuid()));
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "Recipients"
                ("Id", "KanjiName", "KanaName", "DateOfBirth", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, '移行利用者', 'イコウリヨウシャ', '1990-01-01', $createdAt, 'migration-test', $token);
            """,
            ("$id", recipientId), ("$createdAt", createdAt), ("$token", Guid.NewGuid()));

        foreach (var (id, number, start, end) in new[]
                 {
                     (certificateOneId, "CERT-001", "2026-04-01", "2027-03-31"),
                     (certificateTwoId, "CERT-002", "2026-07-01", "2027-06-30"),
                 })
        {
            await ExecuteNonQueryAsync(
                connection,
                """
                INSERT INTO "Certificates"
                    ("Id", "RecipientId", "CertificateNumber", "Validity", "SupplyDays", "MonthlyCostCap",
                     "Municipality", "RecipientAddress", "RecipientGender", "GuardianName", "GuardianRelationship",
                     "Disability_Physical", "Disability_Intellectual", "Disability_Mental",
                     "Disability_Intractable", "SupportCategory", "BenefitType", "ServiceCategory", "SupplyNotes",
                     "ConsultationProviderName", "ConsultationProviderNumber", "ConsultationStart", "ConsultationEnd",
                     "PaymentBurden", "UpperLimitManagementProvider", "MealProvisionApplicable",
                     "HighCostBenefitApplicable", "CreatedAt", "CreatedBy", "ConcurrencyToken")
                VALUES ($id, $recipientId, $number, $validity, 23, 9300,
                        'migration municipality', 'migration recipient address', 2, 'migration guardian', 'parent',
                        1, 1, 0, 1, 2, 1, '就労継続支援B型', 'migration supply notes',
                        'migration consultation provider', '1399999999', '2026-04-01', '2027-03-31',
                        2, 'migration upper-limit provider', 1, 1, $createdAt, 'migration-test', $token);
                """,
                ("$id", id),
                ("$recipientId", recipientId),
                ("$number", number),
                ("$validity", $"{{\"Start\":\"{start}\",\"End\":\"{end}\"}}"),
                ("$createdAt", createdAt),
                ("$token", Guid.NewGuid()));
        }

        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "ContractedProviders"
                ("Id", "CertificateId", "ProviderNumber", "ProviderName", "ServiceCategory",
                 "ContractedSupplyDays", "ContractDate", "TerminationDate", "Notes",
                 "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $certificateId, '1310000001', 'migration provider', '就労継続支援B型',
                    23, '2026-04-01', '2027-03-31', 'migration provider notes',
                    $createdAt, 'migration-test', $token);
            """,
            ("$id", providerId),
            ("$certificateId", certificateOneId),
            ("$createdAt", createdAt),
            ("$token", Guid.NewGuid()));
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "DailyRecords"
                ("Id", "RecipientId", "ServiceDate", "Kind", "OriginId", "Attendance", "Transport",
                 "MealProvided", "Note", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $recipientId, '2026-07-01', 1, NULL, 1, 3, 1, 'migration daily note',
                    $createdAt, 'migration-test', $token);
            """,
            ("$id", dailyRecordId),
            ("$recipientId", recipientId),
            ("$createdAt", createdAt),
            ("$token", Guid.NewGuid()));

        var snapshot = await CaptureLegacySnapshotAsync(connection);
        return new LegacySeed(
            officeId,
            recipientId,
            [certificateOneId, certificateTwoId],
            providerId,
            dailyRecordId,
            snapshot);
    }

    private static async Task InsertClaimInputAsync(
        SqliteConnection connection,
        Guid id,
        Guid rootId,
        int revision,
        int kind,
        Guid? expectedHeadId,
        LegacySeed seed,
        int serviceMonthKey,
        int? upperLimitManagedAmountYen)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "ClaimInputs"
                ("Id", "OfficeId", "RecipientId", "ServiceMonthKey", "RootId", "Revision", "Kind",
                 "ExpectedHeadId", "UpperLimitManagedAmountYen", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $officeId, $recipientId, $serviceMonthKey, $rootId, $revision, $kind,
                    $expectedHeadId, $amount, $createdAt, 'migration-test', $token);
            """,
            ("$id", id),
            ("$officeId", seed.OfficeId),
            ("$recipientId", seed.RecipientId),
            ("$serviceMonthKey", serviceMonthKey),
            ("$rootId", rootId),
            ("$revision", revision),
            ("$kind", kind),
            ("$expectedHeadId", expectedHeadId),
            ("$amount", upperLimitManagedAmountYen),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));
    }

    private static async Task<IReadOnlyDictionary<string, Guid>> SeedValidClaimHeadersAsync(
        SqliteConnection connection,
        LegacySeed seed)
    {
        var ids = HeaderSpecs.ToDictionary(spec => spec.Table, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var lineId = Guid.NewGuid();

        var claimInputId = ids["ClaimInputs"];
        await InsertClaimInputAsync(
            connection, claimInputId, claimInputId, 1, 1, null, seed, 202607, 1_000);

        var episodeId = ids["IntensiveSupportEpisodes"];
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "IntensiveSupportEpisodes"
                ("Id", "OfficeId", "RecipientId", "StartDate", "RootId", "Revision", "Kind",
                 "ExpectedHeadId", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $officeId, $recipientId, '2026-07-01', $id, 1, 1, NULL,
                    $createdAt, 'migration-test', $token);
            """,
            ("$id", episodeId),
            ("$officeId", seed.OfficeId),
            ("$recipientId", seed.RecipientId),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));

        var averageWageId = ids["AverageWageAnnualEvidences"];
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "AverageWageAnnualEvidences"
                ("Id", "OfficeId", "SourceFiscalYear", "PeriodStart", "PeriodEnd", "RootId", "Revision",
                 "Kind", "ExpectedHeadId", "AnnualWagePaidYen", "AnnualExtendedUsers", "AnnualOpeningDays",
                 "Completeness", "EvidenceDocumentId", "DailyEvidenceReference", "MonthlyEvidenceReference",
                 "ConfirmedAt", "ConfirmedBy", "ConfirmationReason", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $officeId, 2025, '2025-04-01', '2026-03-31', $id, 1, 1, NULL,
                    100000, 100, 240, 2, 'annual-evidence', 'daily-evidence', 'monthly-evidence',
                    $createdAt, 'reviewer', 'confirmed', $createdAt, 'migration-test', $token);
            """,
            ("$id", averageWageId),
            ("$officeId", seed.OfficeId),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));

        var profileId = ids["OfficeClaimProfiles"];
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "OfficeClaimProfiles"
                ("Id", "OfficeId", "EffectiveFrom", "RootId", "Revision", "Kind", "ExpectedHeadId",
                 "MasterVersion", "ReformStatus", "EvidenceDocumentId", "ConfirmedAt", "ConfirmedBy",
                 "ConfirmationReason", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $officeId, '2026-07-01', $id, 1, 1, NULL,
                    'r8-06', 2, 'profile-evidence', $createdAt, 'reviewer', 'confirmed',
                    $createdAt, 'migration-test', $token);
            """,
            ("$id", profileId),
            ("$officeId", seed.OfficeId),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));

        var certificateEvidenceId = ids["CertificateClaimEvidences"];
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "CertificateClaimEvidences"
                ("Id", "CertificateId", "Validity", "RootId", "Revision", "Kind", "ExpectedHeadId",
                 "MonthlyCostCap_IsEntered", "MonthlyCostCap_ValueYen", "UpperLimitManagementApplicability",
                 "UpperLimitManagementOfficeNumber", "Article31Status", "Article31AmountYen_IsEntered",
                 "Article31AmountYen_ValueYen", "OriginalDocumentReference", "ConfirmedAt", "ConfirmedBy",
                 "ConfirmationReason", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $certificateId, '{"Start":"2026-04-01","End":"2027-03-31"}', $id, 1, 1, NULL,
                    1, 9300, 2, '1310000001', 1, 1, 0, 'certificate-original', $createdAt, 'reviewer',
                    'confirmed', $createdAt, 'migration-test', $token);
            """,
            ("$id", certificateEvidenceId),
            ("$certificateId", seed.CertificateIds[0]),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));

        var statementId = ids["UpperLimitManagementStatements"];
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "UpperLimitManagementStatements"
                ("Id", "RootId", "Revision", "Kind", "ExpectedHeadId", "ServiceMonthKey", "RecipientId",
                 "CertificateId", "ManagingOfficeId", "MunicipalityNumber", "CertificateNumber",
                 "CertificateMonthlyCostCap_IsEntered", "CertificateMonthlyCostCap_ValueYen",
                 "UpperLimitManagementApplicability", "CertificateManagingOfficeNumber", "ManagingOfficeNumber",
                 "ManagingOfficeName", "OriginalCreationKind", "ReceivedAt", "OriginalDocumentReference",
                 "IsConfirmed", "ConfirmedAt", "ConfirmedBy", "ConfirmationReason", "Result",
                 "TotalCostYen_IsEntered", "TotalCostYen_ValueYen", "TotalPreManagementBurdenYen_IsEntered",
                 "TotalPreManagementBurdenYen_ValueYen", "TotalManagedBurdenYen_IsEntered",
                 "TotalManagedBurdenYen_ValueYen", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $id, 1, 1, NULL, 202607, $recipientId, $certificateId, $officeId,
                    '131156', 'CERT-001', 1, 9300, 2, '1310000001', '1310000001', 'migration office',
                    'original', $createdAt, 'statement-original', 1, $createdAt, 'reviewer', 'confirmed', 1,
                    1, 10000, 1, 1000, 1, 900, $createdAt, 'migration-test', $token);
            """,
            ("$id", statementId),
            ("$recipientId", seed.RecipientId),
            ("$certificateId", seed.CertificateIds[0]),
            ("$officeId", seed.OfficeId),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));

        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "UpperLimitManagementStatementLines"
                ("Id", "StatementId", "LineNumber", "OfficeNumber", "OfficeName",
                 "TotalCostYen_IsEntered", "TotalCostYen_ValueYen", "PreManagementBurdenYen_IsEntered",
                 "PreManagementBurdenYen_ValueYen", "ManagedBurdenYen_IsEntered", "ManagedBurdenYen_ValueYen",
                 "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $statementId, 1, '1310000001', 'migration office', 1, 10000, 1, 1000, 1, 900,
                    $createdAt, 'migration-test', $token);
            """,
            ("$id", lineId),
            ("$statementId", statementId),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));

        ids.Add("UpperLimitManagementStatementLines", lineId);
        return ids;
    }

    private static async Task CloneHeaderRevisionAsync(
        SqliteConnection connection,
        HeaderSpec spec,
        Guid sourceId,
        Guid id,
        int revision,
        int kind,
        Guid? expectedHeadId,
        bool clearPayload,
        bool resetRoot = false)
    {
        var columns = await ReadColumnNamesInOrderAsync(connection, spec.Table);
        var details = await ReadColumnDetailsAsync(connection, spec.Table);
        var projection = columns.Select(column => column switch
        {
            "Id" => "$id",
            "Revision" => "$revision",
            "Kind" => "$kind",
            "RootId" when resetRoot => "$id",
            "ExpectedHeadId" => "$expectedHeadId",
            "ConcurrencyToken" => "$token",
            _ when clearPayload && spec.CancelPayloadColumns.Contains(column, StringComparer.Ordinal) =>
                details[column].NotNull
                    ? details[column].Type == "TEXT" ? "''" : "0"
                    : "NULL",
            _ => $"\"{column}\"",
        });
        var insertColumns = string.Join(", ", columns.Select(column => $"\"{column}\""));
        await ExecuteNonQueryAsync(
            connection,
            $"""
             INSERT INTO "{spec.Table}" ({insertColumns})
             SELECT {string.Join(", ", projection)} FROM "{spec.Table}" WHERE "Id" = $sourceId;
             """,
            ("$id", id),
            ("$revision", revision),
            ("$kind", kind),
            ("$expectedHeadId", expectedHeadId),
            ("$token", Guid.NewGuid()),
            ("$sourceId", sourceId));
    }

    private static async Task InsertOfficeClaimProfileWithOptionStateAsync(
        SqliteConnection connection,
        LegacySeed seed,
        OfficeProfileOptionState state)
    {
        var id = Guid.NewGuid();
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "OfficeClaimProfiles"
                ("Id", "OfficeId", "EffectiveFrom", "RootId", "Revision", "Kind", "ExpectedHeadId",
                 "AverageWageBandOption_Kind", "AverageWageBandOption_OfficialOptionCode",
                 "EarlierRegisteredBandOption_MasterVersion", "EarlierRegisteredBandOption_Option_Kind",
                 "EarlierRegisteredBandOption_Option_OfficialOptionCode",
                 "LaterRegisteredBandOption_MasterVersion", "LaterRegisteredBandOption_Option_Kind",
                 "LaterRegisteredBandOption_Option_OfficialOptionCode", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $officeId, '2026-07-01', $id, 1, 1, NULL,
                    $averageKind, $averageCode, $earlierVersion, $earlierKind, $earlierCode,
                    $laterVersion, $laterKind, $laterCode, $createdAt, 'migration-test', $token);
            """,
            ("$id", id),
            ("$officeId", seed.OfficeId),
            ("$averageKind", state.AverageKind),
            ("$averageCode", state.AverageCode),
            ("$earlierVersion", state.EarlierVersion),
            ("$earlierKind", state.EarlierKind),
            ("$earlierCode", state.EarlierCode),
            ("$laterVersion", state.LaterVersion),
            ("$laterKind", state.LaterKind),
            ("$laterCode", state.LaterCode),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));
    }

    private static async Task CloneStatementLineAsync(
        SqliteConnection connection,
        Guid sourceId,
        Guid id,
        Guid statementId,
        int lineNumber,
        string officeNumber)
    {
        var columns = await ReadColumnNamesInOrderAsync(connection, "UpperLimitManagementStatementLines");
        var projection = columns.Select(column => column switch
        {
            "Id" => "$id",
            "StatementId" => "$statementId",
            "LineNumber" => "$lineNumber",
            "OfficeNumber" => "$officeNumber",
            "ConcurrencyToken" => "$token",
            _ => $"\"{column}\"",
        });
        var insertColumns = string.Join(", ", columns.Select(column => $"\"{column}\""));
        await ExecuteNonQueryAsync(
            connection,
            $"""
             INSERT INTO "UpperLimitManagementStatementLines" ({insertColumns})
             SELECT {string.Join(", ", projection)}
             FROM "UpperLimitManagementStatementLines" WHERE "Id" = $sourceId;
             """,
            ("$id", id),
            ("$statementId", statementId),
            ("$lineNumber", lineNumber),
            ("$officeNumber", officeNumber),
            ("$token", Guid.NewGuid()),
            ("$sourceId", sourceId));
    }

    private static async Task CloneInvalidCertificateLineageAsync(
        SqliteConnection connection,
        Guid sourceId)
    {
        var columns = await ReadColumnNamesInOrderAsync(connection, "Certificates");
        var projection = columns.Select(column => column switch
        {
            "Id" => "$id",
            "CertificateNumber" => "$certificateNumber",
            "ConcurrencyToken" => "$token",
            "RootCertificateId" => "$id",
            "Revision" => "2",
            "ExpectedHeadCertificateId" => "NULL",
            _ => $"\"{column}\"",
        });
        var insertColumns = string.Join(", ", columns.Select(column => $"\"{column}\""));
        await ExecuteNonQueryAsync(
            connection,
            $"""
             INSERT INTO "Certificates" ({insertColumns})
             SELECT {string.Join(", ", projection)} FROM "Certificates" WHERE "Id" = $sourceId;
             """,
            ("$id", Guid.NewGuid()),
            ("$certificateNumber", $"invalid-lineage-{Guid.NewGuid():N}"),
            ("$token", Guid.NewGuid()),
            ("$sourceId", sourceId));
    }

    private static async Task AssertViolationAsync(
        Func<SqliteConnection, LegacySeed, Task> action,
        int expectedExtendedErrorCode,
        string? expectedCheckName = null)
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);
        var seed = await SeedLegacyRowsAsync(database.Connection);
        await migrator.MigrateAsync(target);

        var exception = await FluentActions.Awaiting(() => action(database.Connection, seed))
            .Should().ThrowAsync<SqliteException>();
        exception.Which.SqliteErrorCode.Should().Be(19);
        exception.Which.SqliteExtendedErrorCode.Should().Be(expectedExtendedErrorCode);
        if (expectedCheckName is not null)
            exception.Which.Message.Should().Contain(expectedCheckName);
    }

    private static async Task AssertIsolatedCancelPayloadViolationsAsync(HeaderSpec spec)
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);
        var seed = await SeedLegacyRowsAsync(database.Connection);
        await migrator.MigrateAsync(target);
        var roots = await SeedValidClaimHeadersAsync(database.Connection, seed);
        var correctionId = Guid.NewGuid();
        await CloneHeaderRevisionAsync(
            database.Connection,
            spec,
            roots[spec.Table],
            correctionId,
            2,
            2,
            roots[spec.Table],
            clearPayload: false);
        var cancelId = Guid.NewGuid();
        await CloneHeaderRevisionAsync(
            database.Connection,
            spec,
            correctionId,
            cancelId,
            3,
            3,
            correctionId,
            clearPayload: true);

        var columns = await ReadColumnDetailsAsync(database.Connection, spec.Table);
        foreach (var column in spec.CancelPayloadColumns)
        {
            if (column.EndsWith("_ValueYen", StringComparison.Ordinal)
                && spec.CancelPayloadColumns.Contains(
                    column.Replace("_ValueYen", "_IsEntered", StringComparison.Ordinal),
                    StringComparer.Ordinal))
            {
                continue;
            }

            string assignment;
            if (column.EndsWith("_IsEntered", StringComparison.Ordinal))
            {
                var valueColumn = column.Replace("_IsEntered", "_ValueYen", StringComparison.Ordinal);
                assignment = $"\"{column}\" = 1, \"{valueColumn}\" = 0";
            }
            else if (column.StartsWith("AverageWageBandOption_", StringComparison.Ordinal))
            {
                assignment = "\"AverageWageBandOption_Kind\" = 1, " +
                             "\"AverageWageBandOption_OfficialOptionCode\" = 1";
            }
            else if (column.StartsWith("EarlierRegisteredBandOption_", StringComparison.Ordinal))
            {
                assignment = "\"EarlierRegisteredBandOption_MasterVersion\" = 'r8-06', " +
                             "\"EarlierRegisteredBandOption_Option_Kind\" = 1, " +
                             "\"EarlierRegisteredBandOption_Option_OfficialOptionCode\" = 1";
            }
            else if (column.StartsWith("LaterRegisteredBandOption_", StringComparison.Ordinal))
            {
                assignment = "\"LaterRegisteredBandOption_MasterVersion\" = 'r8-06', " +
                             "\"LaterRegisteredBandOption_Option_Kind\" = 1, " +
                             "\"LaterRegisteredBandOption_Option_OfficialOptionCode\" = 1";
            }
            else
            {
                assignment = $"\"{column}\" = $invalidValue";
            }

            var invalidValue = columns[column].Type == "TEXT" ? (object)"invalid" : 1;
            var exception = await FluentActions.Awaiting(() => ExecuteNonQueryAsync(
                    database.Connection,
                    $"UPDATE \"{spec.Table}\" SET {assignment} WHERE \"Id\" = $id;",
                    ("$invalidValue", invalidValue),
                    ("$id", cancelId)))
                .Should().ThrowAsync<SqliteException>($"only {column} payload state remains on Cancel");
            exception.Which.SqliteErrorCode.Should().Be(19);
            exception.Which.SqliteExtendedErrorCode.Should().Be(275);
            exception.Which.Message.Should().Contain($"CK_{spec.Table}_CancelPayload");
        }
    }

    private static async Task AssertSucceedsAsync(Func<SqliteConnection, LegacySeed, Task> action)
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);
        var seed = await SeedLegacyRowsAsync(database.Connection);
        await migrator.MigrateAsync(target);
        await action(database.Connection, seed);
    }

    private static async Task AssertCertificateBackfillAsync(
        SqliteConnection connection,
        IReadOnlyCollection<Guid> certificateIds)
    {
        foreach (var id in certificateIds)
        {
            var row = await ReadSingleRowAsync(
                connection,
                """
                SELECT "RootCertificateId", "Revision", "ExpectedHeadCertificateId"
                FROM "Certificates" WHERE "Id" = $id;
                """,
                ("$id", id));
            row["RootCertificateId"].Should().Be(id.ToString());
            row["Revision"].Should().Be(1L);
            row["ExpectedHeadCertificateId"].Should().BeNull();
        }
    }

    private static async Task<IReadOnlyList<LegacyTableSnapshot>> CaptureLegacySnapshotAsync(
        SqliteConnection connection)
    {
        var snapshots = new List<LegacyTableSnapshot>();
        foreach (var table in new[]
                 {
                     "Offices", "Recipients", "Certificates", "ContractedProviders", "DailyRecords",
                 })
        {
            var columns = await ReadColumnNamesInOrderAsync(connection, table);
            snapshots.Add(new LegacyTableSnapshot(
                table,
                columns,
                await ReadRowsAsync(connection, table, columns)));
        }

        return snapshots;
    }

    private static async Task AssertLegacySnapshotAsync(
        SqliteConnection connection,
        IReadOnlyCollection<LegacyTableSnapshot> expectedSnapshots)
    {
        foreach (var expected in expectedSnapshots)
        {
            var actualRows = await ReadRowsAsync(connection, expected.Table, expected.Columns);
            actualRows.Should().BeEquivalentTo(
                expected.Rows,
                options => options.WithStrictOrdering(),
                $"{expected.Table} legacy business, identity, audit, and concurrency values must survive migration");
        }
    }

    private static async Task AssertColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string type,
        bool notNull,
        string? defaultValue)
    {
        var columns = await ReadColumnDetailsAsync(connection, table);
        columns.Should().ContainKey(column);
        columns[column].Should().Be(new SqliteColumn(type, notNull, defaultValue));
    }

    private static async Task AssertUniqueIndexAsync(
        SqliteConnection connection,
        string table,
        string indexName,
        IReadOnlyList<string> expectedColumns,
        string? expectedWhereClause)
    {
        var indexes = await ReadIndexesAsync(connection, table);
        indexes.Should().ContainKey(indexName);
        indexes[indexName].Should().Be(new SqliteIndex(Unique: true, Partial: expectedWhereClause is not null));
        (await ReadIndexColumnsAsync(connection, indexName)).Should().Equal(expectedColumns);
        var indexSql = await ReadCreateIndexSqlAsync(connection, indexName);
        var actualWhereClause = ExtractWhereClause(indexSql);
        if (expectedWhereClause is null)
            actualWhereClause.Should().BeNull();
        else
            CanonicalizeSqlExpression(actualWhereClause!).Should().Be(
                CanonicalizeSqlExpression(expectedWhereClause.Replace("WHERE", "", StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task AssertNamedCheckAsync(
        SqliteConnection connection,
        string table,
        string checkName,
        params string[] expectedExpressionFragments)
    {
        var createSql = NormalizeSql(await ReadCreateTableSqlAsync(connection, table));
        createSql.Should().Contain(checkName);
        foreach (var fragment in expectedExpressionFragments)
            createSql.Should().Contain(NormalizeSql(fragment));
    }

    private static async Task AssertCancelPayloadCheckAsync(
        SqliteConnection connection,
        HeaderSpec spec)
    {
        var columns = await ReadColumnDetailsAsync(connection, spec.Table);
        var payloadPredicates = spec.CancelPayloadColumns.Select(column =>
        {
            var detail = columns[column];
            if (!detail.NotNull)
                return $"\"{column}\" IS NULL";
            return detail.Type == "TEXT"
                ? $"\"{column}\" = ''"
                : $"\"{column}\" = 0";
        });
        var expected = spec.Table == "IntensiveSupportEpisodes"
            ? "(\"Kind\" = 3 AND \"StartDate\" IS NULL) OR " +
              "(\"Kind\" IN (1, 2) AND \"StartDate\" IS NOT NULL)"
            : $"\"Kind\" <> 3 OR ({string.Join(" AND ", payloadPredicates)})";
        var actual = ExtractNamedCheckExpression(
            await ReadCreateTableSqlAsync(connection, spec.Table),
            $"CK_{spec.Table}_CancelPayload");
        CanonicalizeSqlExpression(actual).Should().Be(CanonicalizeSqlExpression(expected));
    }

    private static string ExtractNamedCheckExpression(string createTableSql, string checkName)
    {
        var constraintIndex = createTableSql.IndexOf(checkName, StringComparison.Ordinal);
        constraintIndex.Should().BeGreaterThanOrEqualTo(0);
        var checkIndex = createTableSql.IndexOf("CHECK", constraintIndex, StringComparison.OrdinalIgnoreCase);
        checkIndex.Should().BeGreaterThanOrEqualTo(0);
        var openingParenthesis = createTableSql.IndexOf('(', checkIndex);
        openingParenthesis.Should().BeGreaterThanOrEqualTo(0);

        var depth = 0;
        for (var index = openingParenthesis; index < createTableSql.Length; index++)
        {
            depth += createTableSql[index] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (depth == 0)
                return createTableSql[(openingParenthesis + 1)..index];
        }

        throw new InvalidOperationException($"{checkName} CHECK expression is not balanced.");
    }

    private static async Task AssertEnteredYenChecksAsync(
        SqliteConnection connection,
        string table,
        params string[] propertyPrefixes)
    {
        foreach (var prefix in propertyPrefixes)
        {
            await AssertColumnAsync(connection, table, $"{prefix}_IsEntered", "INTEGER", true, null);
            await AssertColumnAsync(connection, table, $"{prefix}_ValueYen", "INTEGER", false, null);
            await AssertNamedCheckAsync(
                connection,
                table,
                $"CK_{table}_{prefix}_EnteredYen",
                $"\"{prefix}_IsEntered\" = 0 AND \"{prefix}_ValueYen\" IS NULL",
                $"\"{prefix}_IsEntered\" = 1 AND \"{prefix}_ValueYen\" IS NOT NULL",
                $"\"{prefix}_ValueYen\" >= 0");
        }
    }

    private static SqliteForeignKey SelfForeignKey(string table, string fromColumn) =>
        new(fromColumn, table, "Id", "RESTRICT");

    private static SqliteForeignKey BusinessForeignKey(string fromColumn, string principalTable) =>
        new(fromColumn, principalTable, "Id", "RESTRICT");

    private static string NormalizeSql(string sql) =>
        string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? ExtractWhereClause(string indexSql)
    {
        var whereIndex = indexSql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        return whereIndex < 0 ? null : indexSql[(whereIndex + "WHERE".Length)..].Trim().TrimEnd(';');
    }

    private static string CanonicalizeSqlExpression(string sql) =>
        new(sql.Where(character => !char.IsWhiteSpace(character)
                                   && character is not '"' and not '`' and not '[' and not ']' and not '(' and not ')')
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName) =>
        Convert.ToInt64(
            await ExecuteScalarAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;",
                ("$name", tableName)),
            CultureInfo.InvariantCulture) == 1;

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName) =>
        Convert.ToInt64(
            await ExecuteScalarAsync(connection, $"SELECT COUNT(*) FROM \"{tableName}\";"),
            CultureInfo.InvariantCulture);

    private static async Task<Dictionary<string, SqliteColumn>> ReadColumnDetailsAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new Dictionary<string, SqliteColumn>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            columns.Add(
                reader.GetString(1),
                new SqliteColumn(
                    reader.GetString(2),
                    reader.GetInt64(3) == 1,
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return columns;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(SqliteConnection connection, string tableName) =>
        (await ReadColumnDetailsAsync(connection, tableName)).Keys.ToHashSet(StringComparer.Ordinal);

    private static async Task<IReadOnlyList<string>> ReadColumnNamesInOrderAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<(long Ordinal, string Name)>();
        while (await reader.ReadAsync())
            columns.Add((reader.GetInt64(0), reader.GetString(1)));
        return columns.OrderBy(column => column.Ordinal).Select(column => column.Name).ToArray();
    }

    private static async Task<IReadOnlyList<IReadOnlyList<object?>>> ReadRowsAsync(
        SqliteConnection connection,
        string table,
        IReadOnlyCollection<string> columns)
    {
        var projection = string.Join(", ", columns.Select(column => $"\"{column}\""));
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {projection} FROM \"{table}\" ORDER BY \"Id\";";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<IReadOnlyList<object?>>();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(row);
        }

        return rows;
    }

    private static async Task<Dictionary<string, SqliteIndex>> ReadIndexesAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var indexes = new Dictionary<string, SqliteIndex>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(1), new SqliteIndex(reader.GetInt64(2) == 1, reader.GetInt64(4) == 1));
        return indexes;
    }

    private static async Task<IReadOnlyList<string>> ReadIndexColumnsAsync(
        SqliteConnection connection,
        string indexName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info(\"{indexName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<(long Sequence, string Name)>();
        while (await reader.ReadAsync())
            columns.Add((reader.GetInt64(0), reader.GetString(2)));
        return columns.OrderBy(column => column.Sequence).Select(column => column.Name).ToArray();
    }

    private static async Task<IReadOnlyList<SqliteForeignKey>> ReadForeignKeysAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var foreignKeys = new List<SqliteForeignKey>();
        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new SqliteForeignKey(
                reader.GetString(3),
                reader.GetString(2),
                reader.GetString(4),
                reader.GetString(6)));
        }

        return foreignKeys;
    }

    private static async Task<string> ReadCreateTableSqlAsync(SqliteConnection connection, string tableName) =>
        (string)(await ExecuteScalarAsync(
            connection,
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;",
            ("$name", tableName)))!;

    private static async Task<string> ReadCreateIndexSqlAsync(SqliteConnection connection, string indexName) =>
        (string)(await ExecuteScalarAsync(
            connection,
            "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = $name;",
            ("$name", indexName)))!;

    private static async Task<Dictionary<string, object?>> ReadSingleRowAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 0; index < reader.FieldCount; index++)
            row.Add(reader.GetName(index), reader.IsDBNull(index) ? null : reader.GetValue(index));
        (await reader.ReadAsync()).Should().BeFalse();
        return row;
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        string sql,
        IReadOnlyCollection<(string Name, object? Value)> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    private sealed record HeaderSpec(
        string Table,
        IReadOnlyList<string> BusinessColumns,
        string BusinessIndexName,
        IReadOnlyList<SqliteForeignKey> ForeignKeys,
        IReadOnlyList<string> CancelPayloadColumns);

    private sealed record OfficeProfileOptionState(
        string ConstraintName,
        int? AverageKind,
        int? AverageCode,
        string? EarlierVersion,
        int? EarlierKind,
        int? EarlierCode,
        string? LaterVersion,
        int? LaterKind,
        int? LaterCode);

    private sealed record LegacySeed(
        Guid OfficeId,
        Guid RecipientId,
        IReadOnlyList<Guid> CertificateIds,
        Guid ContractedProviderId,
        Guid DailyRecordId,
        IReadOnlyList<LegacyTableSnapshot> Snapshot);

    private sealed record LegacyTableSnapshot(
        string Table,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<object?>> Rows);

    private sealed record SqliteColumn(string Type, bool NotNull, string? DefaultValue);
    private sealed record SqliteIndex(bool Unique, bool Partial);
    private sealed record SqliteForeignKey(
        string FromColumn,
        string PrincipalTable,
        string PrincipalColumn,
        string OnDelete);

    private sealed class TemporarySqliteDatabase : IAsyncDisposable
    {
        private readonly string _path;

        private TemporarySqliteDatabase(
            string path,
            SqliteConnection connection,
            TsumugiDbContext context)
        {
            _path = path;
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public TsumugiDbContext Context { get; }

        public static async Task<TemporarySqliteDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"tsumugi-phase31-migration-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                ForeignKeys = true,
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys;";
                Convert.ToInt64(
                    await command.ExecuteScalarAsync(),
                    CultureInfo.InvariantCulture).Should().Be(1);
            }

            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite(connection)
                .Options;
            return new TemporarySqliteDatabase(path, connection, new TsumugiDbContext(options));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { _path, _path + "-shm", _path + "-wal" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
