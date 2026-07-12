using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class ClaimInputRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public ClaimInputRepositoryTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task AddAsync_stages_only_until_real_unit_of_work_saves_and_clears_tracking()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 7);
        await using var context = _fixture.NewContext();
        context.AddRange(ClaimRows.Office(officeId), ClaimRows.Recipient(recipientId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var input = ClaimRows.Input(ClaimRows.EarlyRootId, officeId, recipientId, month);

        await new ClaimInputRepository(context).AddAsync(input, default);

        context.Entry(input).State.Should().Be(EntityState.Added);
        (await context.ClaimInputs.AsNoTracking().CountAsync()).Should().Be(0);

        (await new EfUnitOfWork(context).SaveChangesAsync(default)).Should().Be(1);
        context.ChangeTracker.Entries().Should().BeEmpty();

        await using var verification = _fixture.NewContext();
        var persisted = await new ClaimInputRepository(verification)
            .ListHistoryAsync(officeId, recipientId, month, default);
        persisted.Should().ContainSingle().Which.Should().Be(input);
        verification.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_rejects_null_for_each_claim_input_repository()
    {
        await using var context = _fixture.NewContext();
        Func<Task> addInput = () => new ClaimInputRepository(context).AddAsync(null!, default);
        Func<Task> addEpisode = () => new IntensiveSupportEpisodeRepository(context).AddAsync(null!, default);
        Func<Task> addAnnual = () => new AverageWageAnnualEvidenceRepository(context).AddAsync(null!, default);
        Func<Task> addProfile = () => new OfficeClaimProfileRepository(context).AddAsync(null!, default);
        Func<Task> addCertificate = () => new CertificateClaimEvidenceRepository(context).AddAsync(null!, default);

        await addInput.Should().ThrowAsync<ArgumentNullException>();
        await addEpisode.Should().ThrowAsync<ArgumentNullException>();
        await addAnnual.Should().ThrowAsync<ArgumentNullException>();
        await addProfile.Should().ThrowAsync<ArgumentNullException>();
        await addCertificate.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ClaimInput_list_returns_all_exact_raw_roots_in_root_and_revision_order_without_tracking()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var context = NewContext(connection);
        await context.Database.ExecuteSqlRawAsync(
            "DROP INDEX UX_ClaimInputs_OfficeId_RecipientId_ServiceMonthKey_NewOnly;");
        var officeId = Guid.NewGuid();
        var otherOfficeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 7);
        context.AddRange(
            ClaimRows.Office(officeId),
            ClaimRows.Office(otherOfficeId),
            ClaimRows.Recipient(recipientId),
            ClaimRows.Recipient(otherRecipientId));
        await context.SaveChangesAsync();
        var lateRoot = ClaimRows.Input(ClaimRows.LateRootId, officeId, recipientId, month);
        var lateCancel = ClaimRows.CancelInput(lateRoot);
        var earlyRoot = ClaimRows.Input(ClaimRows.EarlyRootId, officeId, recipientId, month) with
        {
            UpperLimitManagementResult = null,
            UpperLimitManagedAmountYen = null,
            MunicipalSubsidyAmountYen = null,
            ExceptionalUsageStartMonth = null,
            ExceptionalUsageEndMonth = null,
            ExceptionalUsageDays = null,
            StandardUsageDayTotal = null,
        };
        context.AddRange(
            lateCancel,
            lateRoot,
            earlyRoot,
            ClaimRows.Input(Guid.NewGuid(), otherOfficeId, recipientId, month),
            ClaimRows.Input(Guid.NewGuid(), officeId, otherRecipientId, month),
            ClaimRows.Input(Guid.NewGuid(), officeId, recipientId, new ServiceMonth(2026, 8)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new ClaimInputRepository(context)
            .ListHistoryAsync(officeId, recipientId, month, default);

        result.Select(row => (row.RootId, row.Revision)).Should().Equal(
            (ClaimRows.EarlyRootId, 1),
            (ClaimRows.LateRootId, 1),
            (ClaimRows.LateRootId, 2));
        result[0].ExceptionalUsageStartMonth.Should().BeNull();
        result[1].ExceptionalUsageStartMonth.Should().Be(new ServiceMonth(2026, 6));
        result[1].ExceptionalUsageEndMonth.Should().Be(new ServiceMonth(2026, 7));
        result[2].Kind.Should().Be(RecordKind.Cancel);
        result[2].UpperLimitManagementResult.Should().BeNull();
        result[2].ExceptionalUsageStartMonth.Should().BeNull();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Episode_list_filters_by_office_and_recipient_and_orders_raw_history_without_tracking()
    {
        var officeId = Guid.NewGuid();
        var otherOfficeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        await using var context = _fixture.NewContext();
        context.AddRange(
            ClaimRows.Office(officeId),
            ClaimRows.Office(otherOfficeId),
            ClaimRows.Recipient(recipientId),
            ClaimRows.Recipient(otherRecipientId));
        await context.SaveChangesAsync();
        var root = ClaimRows.Episode(ClaimRows.LateRootId, officeId, recipientId);
        var cancel = ClaimRows.CancelEpisode(root);
        context.AddRange(
            cancel,
            root,
            ClaimRows.Episode(Guid.NewGuid(), otherOfficeId, recipientId),
            ClaimRows.Episode(Guid.NewGuid(), officeId, otherRecipientId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new IntensiveSupportEpisodeRepository(context)
            .ListHistoryAsync(officeId, recipientId, default);

        result.Select(row => (row.RootId, row.Revision)).Should().Equal(
            (ClaimRows.LateRootId, 1),
            (ClaimRows.LateRootId, 2));
        result.Select(row => row.StartDate).Should().Equal(new DateOnly(2026, 7, 1), null);
        result[1].Kind.Should().Be(RecordKind.Cancel);
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Annual_list_filters_by_office_and_fiscal_year_and_orders_raw_history_without_tracking()
    {
        var officeId = Guid.NewGuid();
        var otherOfficeId = Guid.NewGuid();
        await using var context = _fixture.NewContext();
        context.AddRange(ClaimRows.Office(officeId), ClaimRows.Office(otherOfficeId));
        await context.SaveChangesAsync();
        var root = ClaimRows.Annual(ClaimRows.LateRootId, officeId, 2025);
        var cancel = ClaimRows.CancelAnnual(root);
        context.AddRange(
            cancel,
            root,
            ClaimRows.Annual(Guid.NewGuid(), otherOfficeId, 2025),
            ClaimRows.Annual(Guid.NewGuid(), officeId, 2024));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new AverageWageAnnualEvidenceRepository(context)
            .ListHistoryAsync(officeId, 2025, default);

        result.Select(row => (row.RootId, row.Revision)).Should().Equal(
            (ClaimRows.LateRootId, 1),
            (ClaimRows.LateRootId, 2));
        result[0].Completeness.Should().Be(FiscalYearCompleteness.Complete);
        result[0].AnnualWagePaidYen.Should().Be(1_200_000);
        result[1].Kind.Should().Be(RecordKind.Cancel);
        result[1].AnnualWagePaidYen.Should().BeNull();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Profile_list_returns_multiple_periods_with_open_end_last_then_root_and_revision_without_tracking()
    {
        var officeId = Guid.NewGuid();
        var otherOfficeId = Guid.NewGuid();
        await using var context = _fixture.NewContext();
        context.AddRange(ClaimRows.Office(officeId), ClaimRows.Office(otherOfficeId));
        await context.SaveChangesAsync();
        var closed = ClaimRows.Profile(
            ClaimRows.LateRootId,
            officeId,
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 6, 30));
        var closedCorrection = ClaimRows.CorrectProfile(closed);
        var open = ClaimRows.Profile(
            ClaimRows.EarlyRootId,
            officeId,
            new DateOnly(2026, 4, 1),
            null);
        context.AddRange(
            open,
            closedCorrection,
            closed,
            ClaimRows.Profile(Guid.NewGuid(), otherOfficeId, new DateOnly(2026, 4, 1), null));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new OfficeClaimProfileRepository(context)
            .ListByOfficeAsync(officeId, default);

        result.Select(row => (row.EffectiveTo, row.RootId, row.Revision)).Should().Equal(
            (new DateOnly(2026, 6, 30), ClaimRows.LateRootId, 1),
            (new DateOnly(2026, 6, 30), ClaimRows.LateRootId, 2),
            ((DateOnly?)null, ClaimRows.EarlyRootId, 1));
        result[0].MasterVersion.Should().Be(new ClaimMasterVersion("claim-master-r8"));
        result[0].AverageWageBandOption.Should().Be(
            new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3));
        result[0].EarlierRegisteredBandOption.Should().Be(
            new VersionedAverageWageBandOption(
                new ClaimMasterVersion("claim-master-r6"),
                new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 2)));
        result[0].LaterRegisteredBandOption.Should().Be(
            new VersionedAverageWageBandOption(
                new ClaimMasterVersion("claim-master-r8"),
                new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3)));
        result[0].FiledTransitionPeriod.Should().Be(
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)));
        result[1].Kind.Should().Be(RecordKind.Correct);
        result[1].MasterVersion.Should().Be(new ClaimMasterVersion("claim-master-r8"));
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Profile_repository_round_trips_cancel_sentinel_through_real_unit_of_work()
    {
        var officeId = Guid.NewGuid();
        await using var context = _fixture.NewContext();
        context.Add(ClaimRows.Office(officeId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var root = ClaimRows.Profile(
            Guid.NewGuid(),
            officeId,
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31));
        var cancel = ClaimRows.CancelProfile(root);
        var repository = new OfficeClaimProfileRepository(context);

        await repository.AddAsync(root, default);
        await repository.AddAsync(cancel, default);

        context.ChangeTracker.Entries<OfficeClaimProfile>()
            .Should().OnlyContain(entry => entry.State == EntityState.Added);
        (await new EfUnitOfWork(context).SaveChangesAsync(default)).Should().Be(2);
        context.ChangeTracker.Entries().Should().BeEmpty();

        await using var verification = _fixture.NewContext();
        var history = await new OfficeClaimProfileRepository(verification)
            .ListByOfficeAsync(officeId, default);
        history.Should().HaveCount(2);
        var persisted = history[1];
        persisted.Kind.Should().Be(RecordKind.Cancel);
        persisted.MasterVersion.Should().BeNull();
        persisted.ReformStatus.Should().BeNull();
        persisted.AverageWageBandOption.Should().BeNull();
        persisted.DesignationDate.Should().BeNull();
        persisted.SupportStartDate.Should().BeNull();
        persisted.EarlierRegisteredBandOption.Should().BeNull();
        persisted.EarlierRegistrationMonth.Should().BeNull();
        persisted.LaterRegisteredBandOption.Should().BeNull();
        persisted.LaterRegistrationMonth.Should().BeNull();
        persisted.ReformComparisonEvidenceDocumentId.Should().BeNull();
        persisted.FiledTransitionPeriod.Should().BeNull();
        persisted.FiledTransitionEvidenceDocumentId.Should().BeNull();
        persisted.EvidenceDocumentId.Should().BeNull();
        persisted.ConfirmedAt.Should().BeNull();
        persisted.ConfirmedBy.Should().BeNull();
        persisted.ConfirmationReason.Should().BeNull();
        verification.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Certificate_evidence_list_returns_multiple_validities_with_open_end_last_then_root_and_revision()
    {
        var recipientId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();
        var otherCertificateId = Guid.NewGuid();
        await using var context = _fixture.NewContext();
        context.Add(ClaimRows.Recipient(recipientId));
        await context.SaveChangesAsync();
        context.AddRange(
            ClaimRows.Certificate(certificateId, recipientId),
            ClaimRows.Certificate(otherCertificateId, recipientId));
        await context.SaveChangesAsync();
        var closed = ClaimRows.CertificateEvidence(
            ClaimRows.LateRootId,
            certificateId,
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)));
        var closedCancel = ClaimRows.CancelCertificateEvidence(closed);
        var open = ClaimRows.CertificateEvidence(
            ClaimRows.EarlyRootId,
            certificateId,
            new DateRange(new DateOnly(2026, 4, 1), null));
        context.AddRange(
            open,
            closedCancel,
            closed,
            ClaimRows.CertificateEvidence(
                Guid.NewGuid(),
                otherCertificateId,
                new DateRange(new DateOnly(2026, 4, 1), null)));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new CertificateClaimEvidenceRepository(context)
            .ListByCertificateAsync(certificateId, default);

        result.Select(row => (row.Validity.End, row.RootId, row.Revision)).Should().Equal(
            (new DateOnly(2027, 3, 31), ClaimRows.LateRootId, 1),
            (new DateOnly(2027, 3, 31), ClaimRows.LateRootId, 2),
            ((DateOnly?)null, ClaimRows.EarlyRootId, 1));
        result[0].MonthlyCostCap.Should().Be(new EnteredYen(true, 0));
        result[0].Article31AmountYen.Should().Be(new EnteredYen(true, 0));
        result[0].Article31EffectivePeriod.Should().Be(
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)));
        result[1].Kind.Should().Be(RecordKind.Cancel);
        result[1].MonthlyCostCap.Should().Be(new EnteredYen(false, null));
        result[1].Article31AmountYen.Should().Be(new EnteredYen(false, null));
        result[1].Article31EffectivePeriod.Should().BeNull();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public void Statement_aggregate_orders_lines_and_defensively_copies_source()
    {
        var statement = ClaimRows.Statement();
        var lines = new List<UpperLimitManagementStatementLine>
        {
            ClaimRows.Line(statement.Id, 2),
            ClaimRows.Line(statement.Id, 1),
        };

        var aggregate = new UpperLimitManagementStatementAggregate(statement, lines);

        lines.Clear();

        aggregate.Header.Should().BeSameAs(statement);
        aggregate.Lines.Select(line => line.LineNumber).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Statement_AddAsync_stages_header_and_all_lines_until_real_unit_of_work_saves()
    {
        var statement = ClaimRows.Statement();
        var lines = new[]
        {
            ClaimRows.Line(statement.Id, 2),
            ClaimRows.Line(statement.Id, 1),
        };
        await using var context = _fixture.NewContext();
        context.AddRange(
            ClaimRows.Office(statement.ManagingOfficeId),
            ClaimRows.Recipient(statement.RecipientId),
            ClaimRows.Certificate(statement.CertificateId, statement.RecipientId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await new UpperLimitManagementStatementRepository(context)
            .AddAsync(statement, lines, default);

        context.Entry(statement).State.Should().Be(EntityState.Added);
        lines.Should().OnlyContain(line => context.Entry(line).State == EntityState.Added);
        (await context.UpperLimitManagementStatements.AsNoTracking()
                .CountAsync(row => row.Id == statement.Id))
            .Should().Be(0);
        (await context.UpperLimitManagementStatementLines.AsNoTracking()
                .CountAsync(row => row.StatementId == statement.Id))
            .Should().Be(0);

        (await new EfUnitOfWork(context).SaveChangesAsync(default)).Should().Be(3);
        context.ChangeTracker.Entries().Should().BeEmpty();

        await using var verification = _fixture.NewContext();
        var persisted = await new UpperLimitManagementStatementRepository(verification)
            .ListHistoryAggregatesAsync(
                statement.ManagingOfficeId,
                statement.RecipientId,
                statement.ServiceMonth,
                default);
        persisted.Should().ContainSingle();
        persisted[0].Header.Should().Be(statement);
        persisted[0].Lines.Should().Equal(lines.OrderBy(line => line.LineNumber));
        verification.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Statement_list_filters_only_three_scope_keys_and_orders_certificates_roots_revisions_and_lines()
    {
        var officeId = Guid.NewGuid();
        var otherOfficeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var otherRecipientId = Guid.NewGuid();
        var month = new ServiceMonth(2026, 7);
        var otherMonth = new ServiceMonth(2026, 8);
        var earlyCertificateId = Guid.Parse("20000000-0000-0000-0000-000000000000");
        var lateCertificateId = Guid.Parse("e0000000-0000-0000-0000-000000000000");
        var otherCertificateId = Guid.NewGuid();
        var early = ClaimRows.Statement(
            ClaimRows.LateRootId, earlyCertificateId, officeId, recipientId, month);
        var earlyCorrection = ClaimRows.CorrectStatement(early);
        var late = ClaimRows.Statement(
            ClaimRows.EarlyRootId, lateCertificateId, officeId, recipientId, month);
        var lateCancel = ClaimRows.CancelStatement(late);
        var included = new[] { lateCancel, earlyCorrection, late, early };
        await using var context = _fixture.NewContext();
        context.AddRange(
            ClaimRows.Office(officeId),
            ClaimRows.Office(otherOfficeId),
            ClaimRows.Recipient(recipientId),
            ClaimRows.Recipient(otherRecipientId),
            ClaimRows.Certificate(earlyCertificateId, recipientId),
            ClaimRows.Certificate(lateCertificateId, recipientId),
            ClaimRows.Certificate(otherCertificateId, recipientId));
        context.AddRange(included);
        context.AddRange(included.SelectMany(statement => statement.Kind == RecordKind.Cancel
            ? []
            : new[]
            {
                ClaimRows.Line(statement.Id, 2),
                ClaimRows.Line(statement.Id, 1),
            }));
        context.AddRange(
            ClaimRows.Statement(Guid.NewGuid(), otherCertificateId, otherOfficeId, recipientId, month),
            ClaimRows.Statement(Guid.NewGuid(), otherCertificateId, officeId, otherRecipientId, month),
            ClaimRows.Statement(Guid.NewGuid(), otherCertificateId, officeId, recipientId, otherMonth));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new UpperLimitManagementStatementRepository(context)
            .ListHistoryAggregatesAsync(officeId, recipientId, month, default);

        result.Select(item =>
                (item.Header.CertificateId, item.Header.RootId, item.Header.Revision))
            .Should().Equal(
                (earlyCertificateId, ClaimRows.LateRootId, 1),
                (earlyCertificateId, ClaimRows.LateRootId, 2),
                (lateCertificateId, ClaimRows.EarlyRootId, 1),
                (lateCertificateId, ClaimRows.EarlyRootId, 2));
        result.Take(3).Select(item => item.Lines.Select(line => line.LineNumber))
            .Should().AllSatisfy(lineNumbers => lineNumbers.Should().Equal(1, 2));
        result[3].Lines.Should().BeEmpty();
        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Statement_repository_round_trips_entered_zero_unentered_null_and_cancel_sentinels()
    {
        var root = ClaimRows.Statement() with
        {
            MunicipalityNumber = "municipality-roundtrip",
            CertificateNumber = "certificate-roundtrip",
            CertificateMonthlyCostCap = new EnteredYen(true, 0),
            CertificateManagingOfficeNumber = "certificate-office",
            ManagingOfficeNumber = "managing-office-roundtrip",
            ManagingOfficeName = "管理事業所",
            OriginalCreationKind = "imported",
            ReceivedAt = DateTimeOffset.UnixEpoch.AddHours(1),
            OriginalDocumentReference = "statement-original",
            ConfirmedAt = DateTimeOffset.UnixEpoch.AddHours(2),
            ConfirmedBy = "confirmer",
            ConfirmationReason = "verified",
            TotalCostYen = new EnteredYen(true, 0),
            TotalPreManagementBurdenYen = new EnteredYen(false, null),
            TotalManagedBurdenYen = new EnteredYen(true, 0),
        };
        var line = ClaimRows.Line(root.Id, 1) with
        {
            OfficeNumber = "line-office",
            OfficeName = "明細事業所",
            TotalCostYen = new EnteredYen(true, 0),
            PreManagementBurdenYen = new EnteredYen(false, null),
            ManagedBurdenYen = new EnteredYen(true, 0),
        };
        var cancel = ClaimRows.CancelStatement(root);
        await using var context = _fixture.NewContext();
        context.AddRange(
            ClaimRows.Office(root.ManagingOfficeId),
            ClaimRows.Recipient(root.RecipientId),
            ClaimRows.Certificate(root.CertificateId, root.RecipientId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new UpperLimitManagementStatementRepository(context);

        await repository.AddAsync(root, [line], default);
        await repository.AddAsync(cancel, [], default);
        (await new EfUnitOfWork(context).SaveChangesAsync(default)).Should().Be(3);

        await using var verification = _fixture.NewContext();
        var roundTripped = await new UpperLimitManagementStatementRepository(verification)
            .ListHistoryAggregatesAsync(
                root.ManagingOfficeId,
                root.RecipientId,
                root.ServiceMonth,
                default);

        roundTripped.Should().HaveCount(2);
        roundTripped[0].Header.Should().Be(root);
        roundTripped[0].Lines.Should().Equal(line);
        roundTripped[1].Header.Should().Be(cancel);
        roundTripped[1].Lines.Should().BeEmpty();
        verification.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Statement_invalid_line_constraint_rolls_back_header_and_all_lines_atomically()
    {
        var statement = ClaimRows.Statement();
        var duplicateLineNumber = new[]
        {
            ClaimRows.Line(statement.Id, 1),
            ClaimRows.Line(statement.Id, 1),
        };
        await using var context = _fixture.NewContext();
        context.AddRange(
            ClaimRows.Office(statement.ManagingOfficeId),
            ClaimRows.Recipient(statement.RecipientId),
            ClaimRows.Certificate(statement.CertificateId, statement.RecipientId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await new UpperLimitManagementStatementRepository(context)
            .AddAsync(statement, duplicateLineNumber, default);

        Func<Task> save = () => new EfUnitOfWork(context).SaveChangesAsync(default);

        await save.Should().ThrowAsync<DbUpdateException>();
        await using var verification = _fixture.NewContext();
        (await verification.UpperLimitManagementStatements.AsNoTracking()
                .CountAsync(row => row.Id == statement.Id))
            .Should().Be(0);
        (await verification.UpperLimitManagementStatementLines.AsNoTracking()
                .CountAsync(row => row.StatementId == statement.Id))
            .Should().Be(0);
    }

    [Fact]
    public async Task Statement_AddAsync_rejects_null_header_and_lines()
    {
        await using var context = _fixture.NewContext();
        var repository = new UpperLimitManagementStatementRepository(context);
        var statement = ClaimRows.Statement();
        Func<Task> addNullHeader = () => repository.AddAsync(null!, [], default);
        Func<Task> addNullLines = () => repository.AddAsync(statement, null!, default);

        await addNullHeader.Should().ThrowAsync<ArgumentNullException>();
        await addNullLines.Should().ThrowAsync<ArgumentNullException>();
    }

    private static class ClaimRows
    {
        internal static readonly Guid EarlyRootId =
            Guid.Parse("10000000-0000-0000-0000-000000000000");
        internal static readonly Guid LateRootId =
            Guid.Parse("f0000000-0000-0000-0000-000000000000");

        public static Office Office(Guid id) => Tsumugi.Domain.Entities.Office.Create(
            id,
            $"office-{id:N}"[..20],
            "請求テスト事業所",
            ServiceCategory.TypeB,
            RegionGrade.None,
            "tester",
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid());

        public static Recipient Recipient(Guid id) => Tsumugi.Domain.Entities.Recipient.Create(
            id,
            "請求利用者",
            "セイキュウリヨウシャ",
            new DateOnly(1990, 1, 1),
            "tester",
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid());

        public static Certificate Certificate(Guid id, Guid recipientId) => Tsumugi.Domain.Entities.Certificate.Create(
            id,
            recipientId,
            $"certificate-{id:N}"[..20],
            new DateRange(new DateOnly(2026, 4, 1), null),
            23,
            37_200,
            "請求自治体",
            "tester",
            DateTimeOffset.UnixEpoch,
            Guid.NewGuid());

        public static ClaimInput Input(
            Guid rootId,
            Guid officeId,
            Guid recipientId,
            ServiceMonth month) => new()
            {
                Id = rootId,
                OfficeId = officeId,
                RecipientId = recipientId,
                ServiceMonth = month,
                RootId = rootId,
                Revision = 1,
                Kind = RecordKind.New,
                ExpectedHeadId = null,
                UpperLimitManagementResult = UpperLimitManagementResult.Result1,
                UpperLimitManagedAmountYen = 1_000,
                MunicipalSubsidyAmountYen = 500,
                ExceptionalUsageStartMonth = new ServiceMonth(2026, 6),
                ExceptionalUsageEndMonth = new ServiceMonth(2026, 7),
                ExceptionalUsageDays = 10,
                StandardUsageDayTotal = 22,
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };

        public static ClaimInput CancelInput(ClaimInput root) => new()
        {
            Id = Guid.NewGuid(),
            OfficeId = root.OfficeId,
            RecipientId = root.RecipientId,
            ServiceMonth = root.ServiceMonth,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            UpperLimitManagementResult = null,
            UpperLimitManagedAmountYen = null,
            MunicipalSubsidyAmountYen = null,
            ExceptionalUsageStartMonth = null,
            ExceptionalUsageEndMonth = null,
            ExceptionalUsageDays = null,
            StandardUsageDayTotal = null,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static IntensiveSupportEpisode Episode(Guid rootId, Guid officeId, Guid recipientId) => new()
        {
            Id = rootId,
            OfficeId = officeId,
            RecipientId = recipientId,
            RootId = rootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            StartDate = new DateOnly(2026, 7, 1),
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static IntensiveSupportEpisode CancelEpisode(IntensiveSupportEpisode root) => new()
        {
            Id = Guid.NewGuid(),
            OfficeId = root.OfficeId,
            RecipientId = root.RecipientId,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            StartDate = null,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static AverageWageAnnualEvidence Annual(Guid rootId, Guid officeId, int fiscalYear) => new()
        {
            Id = rootId,
            OfficeId = officeId,
            SourceFiscalYear = fiscalYear,
            PeriodStart = new DateOnly(fiscalYear, 4, 1),
            PeriodEnd = new DateOnly(fiscalYear + 1, 3, 31),
            RootId = rootId,
            Revision = 1,
            Kind = RecordKind.New,
            ExpectedHeadId = null,
            AnnualWagePaidYen = 1_200_000,
            AnnualExtendedUsers = 240,
            AnnualOpeningDays = 250,
            Completeness = FiscalYearCompleteness.Complete,
            EvidenceDocumentId = "annual-evidence",
            DailyEvidenceReference = "daily-evidence",
            MonthlyEvidenceReference = "monthly-evidence",
            ConfirmedAt = DateTimeOffset.UnixEpoch,
            ConfirmedBy = "tester",
            ConfirmationReason = "checked",
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static AverageWageAnnualEvidence CancelAnnual(AverageWageAnnualEvidence root) => new()
        {
            Id = Guid.NewGuid(),
            OfficeId = root.OfficeId,
            SourceFiscalYear = root.SourceFiscalYear,
            PeriodStart = root.PeriodStart,
            PeriodEnd = root.PeriodEnd,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            AnnualWagePaidYen = null,
            AnnualExtendedUsers = null,
            AnnualOpeningDays = null,
            Completeness = null,
            EvidenceDocumentId = null,
            DailyEvidenceReference = null,
            MonthlyEvidenceReference = null,
            ConfirmedAt = null,
            ConfirmedBy = null,
            ConfirmationReason = null,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static OfficeClaimProfile Profile(
            Guid rootId,
            Guid officeId,
            DateOnly effectiveFrom,
            DateOnly? effectiveTo) => new()
            {
                Id = rootId,
                OfficeId = officeId,
                EffectiveFrom = effectiveFrom,
                EffectiveTo = effectiveTo,
                RootId = rootId,
                Revision = 1,
                Kind = RecordKind.New,
                ExpectedHeadId = null,
                MasterVersion = new ClaimMasterVersion("claim-master-r8"),
                ReformStatus = R8ReformStatus.ReformExempt,
                AverageWageBandOption = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
                DesignationDate = new DateOnly(2024, 4, 1),
                SupportStartDate = new DateOnly(2024, 4, 1),
                EarlierRegisteredBandOption = new VersionedAverageWageBandOption(
                    new ClaimMasterVersion("claim-master-r6"),
                    new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 2)),
                EarlierRegistrationMonth = new ServiceMonth(2024, 3),
                LaterRegisteredBandOption = new VersionedAverageWageBandOption(
                    new ClaimMasterVersion("claim-master-r8"),
                    new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3)),
                LaterRegistrationMonth = new ServiceMonth(2024, 4),
                ReformComparisonEvidenceDocumentId = "comparison-evidence",
                FiledTransitionPeriod = new DateRange(effectiveFrom, effectiveTo),
                FiledTransitionEvidenceDocumentId = "transition-evidence",
                EvidenceDocumentId = "profile-evidence",
                ConfirmedAt = DateTimeOffset.UnixEpoch,
                ConfirmedBy = "tester",
                ConfirmationReason = "checked",
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };

        public static OfficeClaimProfile CorrectProfile(OfficeClaimProfile root) => root with
        {
            Id = Guid.NewGuid(),
            Revision = 2,
            Kind = RecordKind.Correct,
            ExpectedHeadId = root.Id,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static OfficeClaimProfile CancelProfile(OfficeClaimProfile root) => new()
        {
            Id = Guid.NewGuid(),
            OfficeId = root.OfficeId,
            EffectiveFrom = root.EffectiveFrom,
            EffectiveTo = root.EffectiveTo,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            MasterVersion = null,
            ReformStatus = null,
            AverageWageBandOption = null,
            DesignationDate = null,
            SupportStartDate = null,
            EarlierRegisteredBandOption = null,
            EarlierRegistrationMonth = null,
            LaterRegisteredBandOption = null,
            LaterRegistrationMonth = null,
            ReformComparisonEvidenceDocumentId = null,
            FiledTransitionPeriod = null,
            FiledTransitionEvidenceDocumentId = null,
            EvidenceDocumentId = null,
            ConfirmedAt = null,
            ConfirmedBy = null,
            ConfirmationReason = null,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static CertificateClaimEvidence CertificateEvidence(
            Guid rootId,
            Guid certificateId,
            DateRange validity) => new()
            {
                Id = rootId,
                CertificateId = certificateId,
                Validity = validity,
                RootId = rootId,
                Revision = 1,
                Kind = RecordKind.New,
                ExpectedHeadId = null,
                MonthlyCostCap = new EnteredYen(true, 0),
                UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
                UpperLimitManagementOfficeNumber = "1234567890",
                Article31Status = Article31SpecialBurdenStatus.Applicable,
                Article31AmountYen = new EnteredYen(true, 0),
                Article31EffectivePeriod = new DateRange(
                    new DateOnly(2026, 4, 1),
                    new DateOnly(2027, 3, 31)),
                OriginalDocumentReference = "certificate-original",
                ConfirmedAt = DateTimeOffset.UnixEpoch,
                ConfirmedBy = "tester",
                ConfirmationReason = "checked",
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };

        public static CertificateClaimEvidence CancelCertificateEvidence(CertificateClaimEvidence root) => new()
        {
            Id = Guid.NewGuid(),
            CertificateId = root.CertificateId,
            Validity = root.Validity,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            MonthlyCostCap = new EnteredYen(false, null),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Unknown,
            UpperLimitManagementOfficeNumber = null,
            Article31Status = Article31SpecialBurdenStatus.Unknown,
            Article31AmountYen = new EnteredYen(false, null),
            Article31EffectivePeriod = null,
            OriginalDocumentReference = null,
            ConfirmedAt = null,
            ConfirmedBy = null,
            ConfirmationReason = null,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

        public static UpperLimitManagementStatement Statement()
        {
            var id = Guid.NewGuid();
            return Statement(
                id,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new ServiceMonth(2026, 7));
        }

        public static UpperLimitManagementStatement Statement(
            Guid rootId,
            Guid certificateId,
            Guid managingOfficeId,
            Guid recipientId,
            ServiceMonth serviceMonth)
            => new()
            {
                Id = rootId,
                RootId = rootId,
                Revision = 1,
                Kind = RecordKind.New,
                ServiceMonth = serviceMonth,
                RecipientId = recipientId,
                CertificateId = certificateId,
                ManagingOfficeId = managingOfficeId,
                MunicipalityNumber = "municipality",
                CertificateNumber = "certificate",
                CertificateMonthlyCostCap = new EnteredYen(true, 1_000),
                UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
                CertificateManagingOfficeNumber = "managing-office",
                ManagingOfficeNumber = "managing-office",
                ManagingOfficeName = "managing-name",
                OriginalCreationKind = "original-kind",
                IsConfirmed = true,
                Result = UpperLimitManagementResult.Result1,
                TotalCostYen = new EnteredYen(true, 10_000),
                TotalPreManagementBurdenYen = new EnteredYen(true, 1_000),
                TotalManagedBurdenYen = new EnteredYen(true, 1_000),
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };

        public static UpperLimitManagementStatement CorrectStatement(UpperLimitManagementStatement root)
            => root with
            {
                Id = Guid.NewGuid(),
                RootId = root.RootId,
                Revision = 2,
                Kind = RecordKind.Correct,
                ExpectedHeadId = root.Id,
                CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
                ConcurrencyToken = Guid.NewGuid(),
            };

        public static UpperLimitManagementStatement CancelStatement(UpperLimitManagementStatement root)
            => root with
            {
                Id = Guid.NewGuid(),
                RootId = root.RootId,
                Revision = 2,
                Kind = RecordKind.Cancel,
                ExpectedHeadId = root.Id,
                MunicipalityNumber = string.Empty,
                CertificateNumber = string.Empty,
                CertificateMonthlyCostCap = new EnteredYen(false, null),
                UpperLimitManagementApplicability = UpperLimitManagementApplicability.Unknown,
                CertificateManagingOfficeNumber = string.Empty,
                ManagingOfficeNumber = string.Empty,
                ManagingOfficeName = string.Empty,
                OriginalCreationKind = string.Empty,
                ReceivedAt = null,
                OriginalDocumentReference = null,
                IsConfirmed = false,
                ConfirmedAt = null,
                ConfirmedBy = null,
                ConfirmationReason = null,
                Result = (UpperLimitManagementResult)0,
                TotalCostYen = new EnteredYen(false, null),
                TotalPreManagementBurdenYen = new EnteredYen(false, null),
                TotalManagedBurdenYen = new EnteredYen(false, null),
                CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
                ConcurrencyToken = Guid.NewGuid(),
            };

        public static UpperLimitManagementStatementLine Line(Guid statementId, int lineNumber)
            => new()
            {
                Id = Guid.NewGuid(),
                StatementId = statementId,
                LineNumber = lineNumber,
                OfficeNumber = $"office-{lineNumber}",
                OfficeName = $"office-{lineNumber}",
                TotalCostYen = new EnteredYen(true, 1_000),
                PreManagementBurdenYen = new EnteredYen(true, 100),
                ManagedBurdenYen = new EnteredYen(true, 100),
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "tester",
                ConcurrencyToken = Guid.NewGuid(),
            };
    }

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = NewContext(connection);
        await context.Database.MigrateAsync();
        return connection;
    }

    private static TsumugiDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(connection)
            .Options;
        return new TsumugiDbContext(options);
    }
}
