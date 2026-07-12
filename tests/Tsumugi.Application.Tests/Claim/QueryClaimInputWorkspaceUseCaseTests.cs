using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class QueryClaimInputWorkspaceUseCaseTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000102");
    private static readonly Guid CertificateId = Guid.Parse("00000000-0000-0000-0000-000000000103");
    private static readonly ServiceMonth Month = new(2026, 6);
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 3, 4, 5, TimeSpan.Zero);
    private static readonly ClaimMasterVersion MasterVersion = new("R8-2026-06");
    private static readonly AverageWageBandOption WageOption =
        new(AverageWageBandOptionKind.Numeric, 1);

    [Fact]
    public async Task Execute_returns_empty_workspace_after_successful_empty_loads()
    {
        var repositories = new WorkspaceRepositories();
        var sut = CreateSut(repositories);

        var result = await sut.ExecuteAsync(ValidRequest(), default);

        result.ClaimInputChain.Should().BeNull();
        result.AverageWageAnnualEvidenceChain.Should().BeNull();
        result.OfficeClaimProfileChains.Should().BeEmpty();
        result.CertificateClaimEvidenceChains.Should().BeEmpty();
        result.UpperLimitManagementStatementChain.Should().BeNull();
        repositories.ClaimInput.LastQuery.Should().Be((OfficeId, RecipientId, Month));
        repositories.AverageWage.LastQuery.Should().Be((OfficeId, 2025));
        repositories.OfficeProfile.LastOfficeId.Should().Be(OfficeId);
        repositories.CertificateEvidence.LastCertificateId.Should().Be(CertificateId);
        repositories.Statement.LastQuery.Should().Be((OfficeId, RecipientId, Month));
    }

    [Fact]
    public async Task Execute_maps_all_business_values_and_revision_metadata_in_stable_order()
    {
        var repositories = new WorkspaceRepositories();
        var claimRoot = Guid.Parse("00000000-0000-0000-0000-000000000201");
        var claimCancelId = Guid.Parse("00000000-0000-0000-0000-000000000202");
        var claimHeadId = Guid.Parse("00000000-0000-0000-0000-000000000203");
        var claimNew = ClaimInputRevision(claimRoot, claimRoot, 1, RecordKind.New, null, 1_000);
        var claimCancel = ClaimInputRevision(
            claimCancelId, claimRoot, 2, RecordKind.Cancel, claimRoot, null);
        var claimReentry = ClaimInputRevision(
            claimHeadId, claimRoot, 3, RecordKind.Correct, claimCancelId, 2_000);
        repositories.ClaimInput.Items.AddRange([claimReentry, claimNew, claimCancel]);

        var wageRoot = Guid.Parse("00000000-0000-0000-0000-000000000301");
        var wage = AverageWageRevision(wageRoot, wageRoot, 1, RecordKind.New, null);
        repositories.AverageWage.Items.Add(wage);

        var profileRoot = Guid.Parse("00000000-0000-0000-0000-000000000401");
        var profile = OfficeProfileRevision(profileRoot, profileRoot, 1, RecordKind.New, null);
        repositories.OfficeProfile.Items.Add(profile);

        var certificateRoot = Guid.Parse("00000000-0000-0000-0000-000000000501");
        var certificateEvidence = CertificateEvidenceRevision(
            certificateRoot, certificateRoot, 1, RecordKind.New, null);
        repositories.CertificateEvidence.Items.Add(certificateEvidence);

        var statementRoot = Guid.Parse("00000000-0000-0000-0000-000000000601");
        var statementCancelId = Guid.Parse("00000000-0000-0000-0000-000000000602");
        var statementHeadId = Guid.Parse("00000000-0000-0000-0000-000000000603");
        var statementNew = StatementRevision(
            statementRoot, statementRoot, 1, RecordKind.New, null);
        var statementCancel = StatementRevision(
            statementCancelId, statementRoot, 2, RecordKind.Cancel, statementRoot);
        var statementReentry = StatementRevision(
            statementHeadId, statementRoot, 3, RecordKind.Correct, statementCancelId);
        repositories.Statement.Aggregates.AddRange([
            StatementAggregate(statementReentry),
            StatementAggregate(statementNew),
            StatementAggregate(statementCancel),
        ]);

        var result = await CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        result.ClaimInputChain.Should().NotBeNull();
        result.ClaimInputChain!.RootId.Should().Be(claimRoot);
        result.ClaimInputChain.CurrentHeadId.Should().Be(claimHeadId);
        result.ClaimInputChain.EffectiveHeadId.Should().Be(claimHeadId);
        result.ClaimInputChain.Revisions.Select(item => item.Revision).Should().Equal(1, 2, 3);
        result.ClaimInputChain.Revisions[0].Should().BeEquivalentTo(
            claimNew, options => options.ExcludingMissingMembers());
        result.ClaimInputChain.Revisions[2].UpperLimitManagedAmountYen.Should().Be(2_000);

        result.AverageWageAnnualEvidenceChain.Should().NotBeNull();
        result.AverageWageAnnualEvidenceChain!.Revisions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(wage, options => options.ExcludingMissingMembers());
        result.OfficeClaimProfileChains.Should().ContainSingle();
        result.OfficeClaimProfileChains[0].Revisions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(profile, options => options.ExcludingMissingMembers());
        result.CertificateClaimEvidenceChains.Should().ContainSingle();
        result.CertificateClaimEvidenceChains[0].Revisions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                certificateEvidence, options => options.ExcludingMissingMembers());

        var statementChain = result.UpperLimitManagementStatementChain;
        statementChain.Should().NotBeNull();
        statementChain!.CurrentHeadId.Should().Be(statementHeadId);
        statementChain.EffectiveHeadId.Should().Be(statementHeadId);
        statementChain.Revisions.Select(item => item.Revision).Should().Equal(1, 2, 3);
        var mappedStatement = statementChain.Revisions[2];
        mappedStatement.Should().BeEquivalentTo(
            statementReentry, options => options.ExcludingMissingMembers());
        mappedStatement.Lines.Select(line => line.LineNumber).Should().Equal(1, 2);
        mappedStatement.Lines[0].OfficeName.Should().Be("管理事業所");
        mappedStatement.Lines[1].ManagedBurdenYen.Should().Be(new EnteredYen(true, 200));
        mappedStatement.Lines.Should().OnlyContain(line =>
            line.Id != Guid.Empty && line.CreatedAt == Now && line.CreatedBy == "operator");
    }

    [Fact]
    public async Task Execute_rejects_multiple_average_wage_roots_with_sanitized_error()
    {
        var repositories = new WorkspaceRepositories();
        var laterRoot = Guid.Parse("00000000-0000-0000-0000-000000000702");
        var earlierRoot = Guid.Parse("00000000-0000-0000-0000-000000000701");
        var earlierCancelId = Guid.Parse("00000000-0000-0000-0000-000000000703");
        repositories.AverageWage.Items.AddRange([
            AverageWageRevision(laterRoot, laterRoot, 1, RecordKind.New, null),
            AverageWageRevision(earlierCancelId, earlierRoot, 2, RecordKind.Cancel, earlierRoot),
            AverageWageRevision(earlierRoot, earlierRoot, 1, RecordKind.New, null),
        ]);

        var act = () => CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain(earlierRoot.ToString())
            .And.NotContain(laterRoot.ToString());
    }

    [Fact]
    public async Task Execute_rejects_invalid_request_without_loading_repositories()
    {
        var invalidRequests = new[]
        {
            ValidRequest() with { OfficeId = Guid.Empty },
            ValidRequest() with { RecipientId = Guid.Empty },
            ValidRequest() with { CertificateId = Guid.Empty },
            ValidRequest() with { ServiceMonth = default },
            ValidRequest() with { SourceFiscalYear = 1899 },
            ValidRequest() with { SourceFiscalYear = 2200 },
        };

        foreach (var request in invalidRequests)
        {
            var repositories = new WorkspaceRepositories();
            var act = () => CreateSut(repositories).ExecuteAsync(request, default);

            var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
            error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidRequest);
            error.Message.Should().NotContain(
                request.SourceFiscalYear.ToString(System.Globalization.CultureInfo.InvariantCulture));
            repositories.TotalLoadCalls.Should().Be(0);
        }
    }

    [Fact]
    public async Task Execute_rejects_multiple_roots_for_single_chain_with_sanitized_error()
    {
        var repositories = new WorkspaceRepositories();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000801");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000802");
        repositories.ClaimInput.Items.AddRange([
            ClaimInputRevision(first, first, 1, RecordKind.New, null, 1_000),
            ClaimInputRevision(second, second, 1, RecordKind.New, null, 2_000),
        ]);

        var act = () => CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain(first.ToString()).And.NotContain(second.ToString());
    }

    [Fact]
    public async Task Execute_uses_injected_office_policy_and_sanitizes_policy_details()
    {
        var repositories = new WorkspaceRepositories();
        var root = Guid.Parse("00000000-0000-0000-0000-000000000901");
        repositories.OfficeProfile.Items.Add(
            OfficeProfileRevision(root, root, 1, RecordKind.New, null) with
            {
                MasterVersion = new ClaimMasterVersion("secret-invalid-version"),
            });

        var act = () => CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain("secret-invalid-version")
            .And.NotContain("master");
    }

    [Fact]
    public async Task Execute_rejects_statement_with_lines_from_another_revision()
    {
        var repositories = new WorkspaceRepositories();
        var root = Guid.Parse("00000000-0000-0000-0000-000000001001");
        var statement = StatementRevision(root, root, 1, RecordKind.New, null);
        var badLine = StatementLine(Guid.NewGuid(), 1, 1_000, 500, 500);
        repositories.Statement.Aggregates.Add(
            new UpperLimitManagementStatementAggregate(statement, [badLine]));

        var act = () => CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain("行");
    }

    [Fact]
    public async Task Execute_rejects_full_statement_chain_when_later_revision_changes_certificate()
    {
        var repositories = new WorkspaceRepositories();
        var root = Guid.Parse("00000000-0000-0000-0000-000000001011");
        var correctionId = Guid.Parse("00000000-0000-0000-0000-000000001012");
        var changedCertificateId = Guid.Parse("00000000-0000-0000-0000-000000001013");
        var created = StatementRevision(root, root, 1, RecordKind.New, null);
        var corruptCorrection = StatementRevision(
            correctionId, root, 2, RecordKind.Correct, root) with
        {
            CertificateId = changedCertificateId,
        };
        repositories.Statement.Aggregates.AddRange([
            StatementAggregate(created),
            StatementAggregate(corruptCorrection),
        ]);

        try
        {
            var result = await CreateSut(repositories).ExecuteAsync(ValidRequest(), default);
            throw new Xunit.Sdk.XunitException(
                $"Expected invalid history, but stale head {result.UpperLimitManagementStatementChain?.CurrentHeadId} was returned.");
        }
        catch (ClaimInputQueryException error)
        {
            error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
            error.Message.Should().NotContain(changedCertificateId.ToString());
        }
    }

    [Fact]
    public async Task Execute_validates_all_statement_roots_then_selects_requested_certificate_chain()
    {
        var repositories = new WorkspaceRepositories();
        var selectedRoot = Guid.Parse("00000000-0000-0000-0000-000000001021");
        var otherRoot = Guid.Parse("00000000-0000-0000-0000-000000001022");
        var otherCertificateId = Guid.Parse("00000000-0000-0000-0000-000000001023");
        var selected = StatementRevision(
            selectedRoot, selectedRoot, 1, RecordKind.New, null);
        var other = StatementRevision(
            otherRoot, otherRoot, 1, RecordKind.New, null) with
        {
            CertificateId = otherCertificateId,
        };
        repositories.Statement.Aggregates.AddRange([
            StatementAggregate(other),
            StatementAggregate(selected),
        ]);

        var result = await CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        result.UpperLimitManagementStatementChain.Should().NotBeNull();
        result.UpperLimitManagementStatementChain!.RootId.Should().Be(selectedRoot);
        result.UpperLimitManagementStatementChain.CurrentHeadId.Should().Be(selectedRoot);
        result.UpperLimitManagementStatementChain.Revisions.Should().ContainSingle()
            .Which.CertificateId.Should().Be(CertificateId);
    }

    [Fact]
    public async Task Execute_rejects_corrupt_nonselected_statement_root_before_selecting_requested_certificate_chain()
    {
        var repositories = new WorkspaceRepositories();
        var selectedRoot = Guid.Parse("00000000-0000-0000-0000-000000001031");
        var otherRoot = Guid.Parse("00000000-0000-0000-0000-000000001032");
        var otherCorrectionId = Guid.Parse("00000000-0000-0000-0000-000000001033");
        var otherCertificateId = Guid.Parse("00000000-0000-0000-0000-000000001034");
        var changedCertificateId = Guid.Parse("00000000-0000-0000-0000-000000001035");
        var selected = StatementRevision(
            selectedRoot, selectedRoot, 1, RecordKind.New, null);
        var other = StatementRevision(
            otherRoot, otherRoot, 1, RecordKind.New, null) with
        {
            CertificateId = otherCertificateId,
        };
        var corruptOtherCorrection = StatementRevision(
            otherCorrectionId, otherRoot, 2, RecordKind.Correct, otherRoot) with
        {
            CertificateId = changedCertificateId,
        };
        repositories.Statement.Aggregates.AddRange([
            StatementAggregate(selected),
            StatementAggregate(other),
            StatementAggregate(corruptOtherCorrection),
        ]);

        var act = () => CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain(otherCertificateId.ToString())
            .And.NotContain(changedCertificateId.ToString());
    }

    [Fact]
    public async Task Execute_rejects_multiple_statement_roots_for_requested_certificate()
    {
        var repositories = new WorkspaceRepositories();
        var firstRoot = Guid.Parse("00000000-0000-0000-0000-000000001041");
        var secondRoot = Guid.Parse("00000000-0000-0000-0000-000000001042");
        repositories.Statement.Aggregates.AddRange([
            StatementAggregate(StatementRevision(
                firstRoot, firstRoot, 1, RecordKind.New, null)),
            StatementAggregate(StatementRevision(
                secondRoot, secondRoot, 1, RecordKind.New, null)),
        ]);

        var act = () => CreateSut(repositories).ExecuteAsync(ValidRequest(), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain(firstRoot.ToString())
            .And.NotContain(secondRoot.ToString());
    }

    private static QueryClaimInputWorkspaceUseCase CreateSut(WorkspaceRepositories repositories) =>
        new(
            repositories.ClaimInput,
            repositories.AverageWage,
            repositories.OfficeProfile,
            repositories.CertificateEvidence,
            repositories.Statement,
            CreateOfficePolicy());

    private static QueryClaimInputWorkspaceRequest ValidRequest() =>
        new(OfficeId, RecipientId, CertificateId, Month, 2025);

    private static ClaimInput ClaimInputRevision(
        Guid id,
        Guid rootId,
        int revision,
        RecordKind kind,
        Guid? expectedHeadId,
        int? managedAmount) =>
        new()
        {
            Id = id,
            OfficeId = OfficeId,
            RecipientId = RecipientId,
            ServiceMonth = Month,
            RootId = rootId,
            Revision = revision,
            Kind = kind,
            ExpectedHeadId = expectedHeadId,
            UpperLimitManagementResult = kind == RecordKind.Cancel
                ? null
                : UpperLimitManagementResult.Result2,
            UpperLimitManagedAmountYen = managedAmount,
            MunicipalSubsidyAmountYen = kind == RecordKind.Cancel ? null : 300,
            ExceptionalUsageStartMonth = kind == RecordKind.Cancel ? null : Month,
            ExceptionalUsageEndMonth = kind == RecordKind.Cancel ? null : Month,
            ExceptionalUsageDays = kind == RecordKind.Cancel ? null : 10,
            StandardUsageDayTotal = kind == RecordKind.Cancel ? null : 22,
            CreatedAt = Now.AddMinutes(revision),
            CreatedBy = $"operator-{revision}",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static AverageWageAnnualEvidence AverageWageRevision(
        Guid id,
        Guid rootId,
        int revision,
        RecordKind kind,
        Guid? expectedHeadId,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null) =>
        new()
        {
            Id = id,
            OfficeId = OfficeId,
            SourceFiscalYear = 2025,
            PeriodStart = periodStart ?? new DateOnly(2025, 4, 1),
            PeriodEnd = periodEnd ?? new DateOnly(2026, 3, 31),
            RootId = rootId,
            Revision = revision,
            Kind = kind,
            ExpectedHeadId = expectedHeadId,
            AnnualWagePaidYen = kind == RecordKind.Cancel ? null : 1_200_000,
            AnnualExtendedUsers = kind == RecordKind.Cancel ? null : 120,
            AnnualOpeningDays = kind == RecordKind.Cancel ? null : 240,
            Completeness = kind == RecordKind.Cancel ? null : FiscalYearCompleteness.Complete,
            EvidenceDocumentId = kind == RecordKind.Cancel ? null : "wage-document",
            DailyEvidenceReference = kind == RecordKind.Cancel ? null : "daily-reference",
            MonthlyEvidenceReference = kind == RecordKind.Cancel ? null : "monthly-reference",
            ConfirmedAt = kind == RecordKind.Cancel ? null : Now,
            ConfirmedBy = kind == RecordKind.Cancel ? null : "reviewer",
            ConfirmationReason = kind == RecordKind.Cancel ? null : "checked",
            CreatedAt = Now.AddMinutes(revision),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static OfficeClaimProfile OfficeProfileRevision(
        Guid id,
        Guid rootId,
        int revision,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new()
        {
            Id = id,
            OfficeId = OfficeId,
            EffectiveFrom = new DateOnly(2026, 6, 1),
            EffectiveTo = null,
            RootId = rootId,
            Revision = revision,
            Kind = kind,
            ExpectedHeadId = expectedHeadId,
            MasterVersion = kind == RecordKind.Cancel ? null : MasterVersion,
            ReformStatus = kind == RecordKind.Cancel ? null : R8ReformStatus.ReformTarget,
            AverageWageBandOption = kind == RecordKind.Cancel ? null : WageOption,
            DesignationDate = null,
            SupportStartDate = null,
            EarlierRegisteredBandOption = null,
            EarlierRegistrationMonth = null,
            LaterRegisteredBandOption = null,
            LaterRegistrationMonth = null,
            ReformComparisonEvidenceDocumentId = null,
            FiledTransitionPeriod = null,
            FiledTransitionEvidenceDocumentId = null,
            EvidenceDocumentId = kind == RecordKind.Cancel ? null : "designation-ledger",
            ConfirmedAt = kind == RecordKind.Cancel ? null : Now,
            ConfirmedBy = kind == RecordKind.Cancel ? null : "reviewer",
            ConfirmationReason = kind == RecordKind.Cancel ? null : "checked",
            CreatedAt = Now.AddMinutes(revision),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static CertificateClaimEvidence CertificateEvidenceRevision(
        Guid id,
        Guid rootId,
        int revision,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new()
        {
            Id = id,
            CertificateId = CertificateId,
            Validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            RootId = rootId,
            Revision = revision,
            Kind = kind,
            ExpectedHeadId = expectedHeadId,
            MonthlyCostCap = kind == RecordKind.Cancel
                ? new EnteredYen(false, null)
                : new EnteredYen(true, 9_300),
            UpperLimitManagementApplicability = kind == RecordKind.Cancel
                ? UpperLimitManagementApplicability.Unknown
                : UpperLimitManagementApplicability.Applicable,
            UpperLimitManagementOfficeNumber = kind == RecordKind.Cancel ? null : "9876543210",
            Article31Status = kind == RecordKind.Cancel
                ? Article31SpecialBurdenStatus.Unknown
                : Article31SpecialBurdenStatus.NotApplicable,
            Article31AmountYen = new EnteredYen(false, null),
            Article31EffectivePeriod = kind == RecordKind.Cancel
                ? null
                : new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            OriginalDocumentReference = kind == RecordKind.Cancel ? null : "certificate-original",
            ConfirmedAt = kind == RecordKind.Cancel ? null : Now,
            ConfirmedBy = kind == RecordKind.Cancel ? null : "reviewer",
            ConfirmationReason = kind == RecordKind.Cancel ? null : "checked",
            CreatedAt = Now.AddMinutes(revision),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static UpperLimitManagementStatement StatementRevision(
        Guid id,
        Guid rootId,
        int revision,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new()
        {
            Id = id,
            RootId = rootId,
            Revision = revision,
            Kind = kind,
            ExpectedHeadId = expectedHeadId,
            ServiceMonth = Month,
            RecipientId = RecipientId,
            CertificateId = CertificateId,
            ManagingOfficeId = OfficeId,
            MunicipalityNumber = kind == RecordKind.Cancel ? string.Empty : "131156",
            CertificateNumber = kind == RecordKind.Cancel ? string.Empty : "1234567890",
            CertificateMonthlyCostCap = kind == RecordKind.Cancel
                ? new EnteredYen(false, null)
                : new EnteredYen(true, 9_300),
            UpperLimitManagementApplicability = kind == RecordKind.Cancel
                ? UpperLimitManagementApplicability.Unknown
                : UpperLimitManagementApplicability.Applicable,
            CertificateManagingOfficeNumber = kind == RecordKind.Cancel ? string.Empty : "9876543210",
            ManagingOfficeNumber = kind == RecordKind.Cancel ? string.Empty : "9876543210",
            ManagingOfficeName = kind == RecordKind.Cancel ? string.Empty : "管理事業所",
            OriginalCreationKind = kind == RecordKind.Cancel ? string.Empty : "original",
            ReceivedAt = kind == RecordKind.Cancel ? null : Now,
            OriginalDocumentReference = kind == RecordKind.Cancel ? null : "statement-original",
            IsConfirmed = kind != RecordKind.Cancel,
            ConfirmedAt = kind == RecordKind.Cancel ? null : Now,
            ConfirmedBy = kind == RecordKind.Cancel ? null : "reviewer",
            ConfirmationReason = kind == RecordKind.Cancel ? null : "checked",
            Result = kind == RecordKind.Cancel ? default : UpperLimitManagementResult.Result2,
            TotalCostYen = kind == RecordKind.Cancel
                ? new EnteredYen(false, null)
                : new EnteredYen(true, 1_500),
            TotalPreManagementBurdenYen = kind == RecordKind.Cancel
                ? new EnteredYen(false, null)
                : new EnteredYen(true, 700),
            TotalManagedBurdenYen = kind == RecordKind.Cancel
                ? new EnteredYen(false, null)
                : new EnteredYen(true, 700),
            CreatedAt = Now.AddMinutes(revision),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static UpperLimitManagementStatementAggregate StatementAggregate(
        UpperLimitManagementStatement statement) =>
        new(
            statement,
            statement.Kind == RecordKind.Cancel
                ? []
                :
                [
                    StatementLine(statement.Id, 2, 500, 200, 200),
                    StatementLine(statement.Id, 1, 1_000, 500, 500),
                ]);

    private static UpperLimitManagementStatementLine StatementLine(
        Guid statementId,
        int lineNumber,
        int total,
        int pre,
        int managed) =>
        new()
        {
            Id = Guid.NewGuid(),
            StatementId = statementId,
            LineNumber = lineNumber,
            OfficeNumber = lineNumber == 1 ? "9876543210" : "1111111111",
            OfficeName = lineNumber == 1 ? "管理事業所" : "他事業所",
            TotalCostYen = new EnteredYen(true, total),
            PreManagementBurdenYen = new EnteredYen(true, pre),
            ManagedBurdenYen = new EnteredYen(true, managed),
            CreatedAt = Now,
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static OfficeClaimProfilePolicy CreateOfficePolicy()
    {
        var rule = new AverageWageBandOptionVersionRule(
            MasterVersion,
            Month,
            null,
            [WageOption],
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.ReformTarget] = [WageOption],
            });
        return new OfficeClaimProfilePolicy(
            MasterVersion,
            [rule],
            new DateOnly(2026, 6, 1),
            designation => designation.AddYears(3));
    }

    private sealed class WorkspaceRepositories
    {
        public FakeClaimInputRepository ClaimInput { get; } = new();
        public FakeAverageWageRepository AverageWage { get; } = new();
        public FakeOfficeProfileRepository OfficeProfile { get; } = new();
        public FakeCertificateEvidenceRepository CertificateEvidence { get; } = new();
        public FakeStatementRepository Statement { get; } = new();

        public int TotalLoadCalls =>
            ClaimInput.LoadCalls + AverageWage.LoadCalls + OfficeProfile.LoadCalls
            + CertificateEvidence.LoadCalls + Statement.LoadCalls;
    }

    private sealed class FakeClaimInputRepository : IClaimInputRepository
    {
        public List<ClaimInput> Items { get; } = [];
        public int LoadCalls { get; private set; }
        public (Guid, Guid, ServiceMonth)? LastQuery { get; private set; }
        public Task AddAsync(ClaimInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
            Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct)
        {
            LoadCalls++;
            LastQuery = (officeId, recipientId, serviceMonth);
            return Task.FromResult<IReadOnlyList<ClaimInput>>(Items);
        }
    }

    private sealed class FakeAverageWageRepository : IAverageWageAnnualEvidenceRepository
    {
        public List<AverageWageAnnualEvidence> Items { get; } = [];
        public int LoadCalls { get; private set; }
        public (Guid, int)? LastQuery { get; private set; }
        public Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
            Guid officeId, int sourceFiscalYear, CancellationToken ct)
        {
            LoadCalls++;
            LastQuery = (officeId, sourceFiscalYear);
            return Task.FromResult<IReadOnlyList<AverageWageAnnualEvidence>>(Items);
        }
    }

    private sealed class FakeOfficeProfileRepository : IOfficeClaimProfileRepository
    {
        public List<OfficeClaimProfile> Items { get; } = [];
        public int LoadCalls { get; private set; }
        public Guid? LastOfficeId { get; private set; }
        public Task AddAsync(OfficeClaimProfile profile, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
            Guid officeId, CancellationToken ct)
        {
            LoadCalls++;
            LastOfficeId = officeId;
            return Task.FromResult<IReadOnlyList<OfficeClaimProfile>>(Items);
        }
    }

    private sealed class FakeCertificateEvidenceRepository : ICertificateClaimEvidenceRepository
    {
        public List<CertificateClaimEvidence> Items { get; } = [];
        public int LoadCalls { get; private set; }
        public Guid? LastCertificateId { get; private set; }
        public Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
            Guid certificateId, CancellationToken ct)
        {
            LoadCalls++;
            LastCertificateId = certificateId;
            return Task.FromResult<IReadOnlyList<CertificateClaimEvidence>>(Items);
        }
    }

    private sealed class FakeStatementRepository : IUpperLimitManagementStatementRepository
    {
        public List<UpperLimitManagementStatementAggregate> Aggregates { get; } = [];
        public int LoadCalls { get; private set; }
        public (Guid, Guid, ServiceMonth)? LastQuery { get; private set; }
        public Task AddAsync(
            UpperLimitManagementStatement statement,
            IReadOnlyCollection<UpperLimitManagementStatementLine> lines,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<UpperLimitManagementStatementAggregate>>
            ListHistoryAggregatesAsync(
                Guid managingOfficeId,
                Guid recipientId,
                ServiceMonth serviceMonth,
                CancellationToken ct)
        {
            LoadCalls++;
            LastQuery = (managingOfficeId, recipientId, serviceMonth);
            return Task.FromResult<IReadOnlyList<UpperLimitManagementStatementAggregate>>(Aggregates);
        }
    }
}
