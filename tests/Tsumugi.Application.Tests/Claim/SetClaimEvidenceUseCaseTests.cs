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

public sealed class SetClaimEvidenceUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 2, 3, 4, TimeSpan.Zero);
    private static readonly ServiceMonth Month = new(2026, 6);
    private static readonly ClaimMasterVersion MasterVersion = new("R8-2026-06");
    private static readonly AverageWageBandOption WageOption =
        new(AverageWageBandOptionKind.Numeric, 1);

    [Fact]
    public async Task Average_wage_evidence_appends_a_validated_revision()
    {
        var repo = new FakeAverageWageAnnualEvidenceRepository();
        var uow = new FakeUnitOfWork();
        var sut = new SetAverageWageAnnualEvidenceUseCase(
            repo, uow, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            ValidAverageWageRequest(officeId, RecordKind.New, null), "operator", default);
        var corrected = await sut.ExecuteAsync(
            ValidAverageWageRequest(officeId, RecordKind.Correct, created.Id) with
            {
                AnnualWagePaidYen = 1_200_000,
            },
            "operator", default);

        corrected.Revision.Should().Be(2);
        repo.Items[^1].AnnualWagePaidYen.Should().Be(1_200_000);
        repo.Items.Should().OnlyContain(item => item.CreatedAt == Now);
        uow.SaveCalls.Should().Be(2);
    }

    [Fact]
    public async Task Average_wage_evidence_returns_sanitized_error_for_invalid_values()
    {
        var repo = new FakeAverageWageAnnualEvidenceRepository();
        var sut = new SetAverageWageAnnualEvidenceUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var request = ValidAverageWageRequest(Guid.NewGuid(), RecordKind.New, null) with
        {
            AnnualExtendedUsers = 0,
            EvidenceDocumentId = "secret-document-id",
        };

        var act = () => sut.ExecuteAsync(request, "operator-secret", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        error.Message.Should().NotContain("secret-document-id").And.NotContain("operator-secret");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Office_claim_profile_uses_the_injected_version_policy()
    {
        var repo = new FakeOfficeClaimProfileRepository();
        var sut = new SetOfficeClaimProfileUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(Now),
            new FakePolicyProvider(CreateOfficePolicy()));

        var saved = await sut.ExecuteAsync(
            ValidOfficeProfileRequest(Guid.NewGuid(), RecordKind.New, null),
            "operator", default);

        saved.Revision.Should().Be(1);
        repo.Items.Should().ContainSingle();
        repo.Items[0].MasterVersion.Should().Be(MasterVersion);
    }

    [Fact]
    public async Task Office_claim_profile_rejects_values_outside_the_injected_version_policy()
    {
        var repo = new FakeOfficeClaimProfileRepository();
        var sut = new SetOfficeClaimProfileUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(Now),
            new FakePolicyProvider(CreateOfficePolicy()));
        var request = ValidOfficeProfileRequest(Guid.NewGuid(), RecordKind.New, null) with
        {
            MasterVersion = new ClaimMasterVersion("another-version"),
        };

        var act = () => sut.ExecuteAsync(request, "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Values);
        error.Message.Should().NotContain("another-version");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Office_claim_profile_requires_a_master_version_before_policy_resolution()
    {
        var repo = new FakeOfficeClaimProfileRepository();
        var provider = new FakePolicyProvider(CreateOfficePolicy());
        var sut = new SetOfficeClaimProfileUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(Now), provider);
        var request = ValidOfficeProfileRequest(Guid.NewGuid(), RecordKind.New, null) with
        {
            MasterVersion = null,
        };

        var act = () => sut.ExecuteAsync(request, "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        provider.ResolveCalls.Should().Be(0);
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Office_claim_profile_sanitizes_unavailable_master_policy()
    {
        var repo = new FakeOfficeClaimProfileRepository();
        var provider = new FakePolicyProvider(
            new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.Unavailable));
        var sut = new SetOfficeClaimProfileUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(Now), provider);

        var act = () => sut.ExecuteAsync(
            ValidOfficeProfileRequest(Guid.NewGuid(), RecordKind.New, null),
            "operator-secret",
            default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.MasterUnavailable);
        error.Message.Should().NotContain(MasterVersion.Value).And.NotContain("operator-secret");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Certificate_claim_evidence_rejects_inconsistent_management_fields()
    {
        var repo = new FakeCertificateClaimEvidenceRepository();
        var sut = new SetCertificateClaimEvidenceUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var request = ValidCertificateEvidenceRequest(Guid.NewGuid(), RecordKind.New, null) with
        {
            UpperLimitManagementOfficeNumber = null,
            OriginalDocumentReference = "secret-original-reference",
        };

        var act = () => sut.ExecuteAsync(request, "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Values);
        error.Message.Should().NotContain("secret-original-reference");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Upper_limit_statement_allows_cancelled_history_to_be_reentered()
    {
        var repo = new FakeUpperLimitManagementStatementRepository();
        var uow = new FakeUnitOfWork();
        var sut = new SetUpperLimitManagementStatementUseCase(
            repo, uow, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            ValidStatementRequest(officeId, recipientId, certificateId, RecordKind.New, null),
            "operator", default);
        var cancelled = await sut.ExecuteAsync(
            CancelStatementRequest(officeId, recipientId, certificateId, created.Id),
            "operator", default);
        var reentered = await sut.ExecuteAsync(
            ValidStatementRequest(
                officeId, recipientId, certificateId, RecordKind.Correct, cancelled.Id),
            "operator", default);

        repo.Aggregates.Select(item => item.Header.Kind).Should()
            .Equal(RecordKind.New, RecordKind.Cancel, RecordKind.Correct);
        repo.Aggregates[1].Lines.Should().BeEmpty();
        repo.Aggregates[2].Lines.Should().ContainSingle();
        repo.Aggregates[2].Lines[0].StatementId.Should().Be(reentered.Id);
        repo.Aggregates.SelectMany(item => item.Lines).Select(line => line.Id)
            .Should().OnlyHaveUniqueItems();
        reentered.Revision.Should().Be(3);
        uow.SaveCalls.Should().Be(3);
    }

    private static SetAverageWageAnnualEvidenceRequest ValidAverageWageRequest(
        Guid officeId,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new(
            officeId,
            2025,
            new DateOnly(2025, 4, 1),
            new DateOnly(2026, 3, 31),
            kind,
            expectedHeadId)
        {
            AnnualWagePaidYen = 1_000_000,
            AnnualExtendedUsers = 100,
            AnnualOpeningDays = 240,
            Completeness = FiscalYearCompleteness.Complete,
            EvidenceDocumentId = "wage-evidence",
            DailyEvidenceReference = "daily-evidence",
            MonthlyEvidenceReference = "monthly-evidence",
            ConfirmedAt = Now,
            ConfirmedBy = "reviewer",
            ConfirmationReason = "original checked",
        };

    private static SetOfficeClaimProfileRequest ValidOfficeProfileRequest(
        Guid officeId,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new(officeId, new DateOnly(2026, 6, 1), null, kind, expectedHeadId)
        {
            MasterVersion = MasterVersion,
            ReformStatus = R8ReformStatus.ReformTarget,
            AverageWageBandOption = WageOption,
            EvidenceDocumentId = "designation-ledger",
            ConfirmedAt = Now,
            ConfirmedBy = "reviewer",
            ConfirmationReason = "ledger checked",
        };

    private static SetCertificateClaimEvidenceRequest ValidCertificateEvidenceRequest(
        Guid certificateId,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new(
            certificateId,
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            kind,
            expectedHeadId)
        {
            MonthlyCostCap = new EnteredYen(true, 9_300),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            UpperLimitManagementOfficeNumber = "1234567890",
            Article31Status = Article31SpecialBurdenStatus.NotApplicable,
            Article31AmountYen = new EnteredYen(false, null),
            Article31EffectivePeriod =
                new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            OriginalDocumentReference = "certificate-original",
            ConfirmedAt = Now,
            ConfirmedBy = "reviewer",
            ConfirmationReason = "original checked",
        };

    private static SetUpperLimitManagementStatementRequest ValidStatementRequest(
        Guid officeId,
        Guid recipientId,
        Guid certificateId,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new(Month, recipientId, certificateId, officeId, kind, expectedHeadId)
        {
            MunicipalityNumber = "131156",
            CertificateNumber = "1234567890",
            CertificateMonthlyCostCap = new EnteredYen(true, 9_300),
            UpperLimitManagementApplicability = UpperLimitManagementApplicability.Applicable,
            CertificateManagingOfficeNumber = "9876543210",
            ManagingOfficeNumber = "9876543210",
            ManagingOfficeName = "管理事業所",
            OriginalCreationKind = "original",
            ReceivedAt = Now,
            OriginalDocumentReference = "statement-original",
            IsConfirmed = true,
            ConfirmedAt = Now,
            ConfirmedBy = "reviewer",
            ConfirmationReason = "original checked",
            Result = UpperLimitManagementResult.Result2,
            TotalCostYen = new EnteredYen(true, 1_000),
            TotalPreManagementBurdenYen = new EnteredYen(true, 500),
            TotalManagedBurdenYen = new EnteredYen(true, 500),
            Lines =
            [
                new UpperLimitManagementStatementLineRequest(
                    1,
                    "9876543210",
                    "管理事業所",
                    new EnteredYen(true, 1_000),
                    new EnteredYen(true, 500),
                    new EnteredYen(true, 500)),
            ],
        };

    private static SetUpperLimitManagementStatementRequest CancelStatementRequest(
        Guid officeId,
        Guid recipientId,
        Guid certificateId,
        Guid expectedHeadId) =>
        new(Month, recipientId, certificateId, officeId, RecordKind.Cancel, expectedHeadId);

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

    private sealed class FakePolicyProvider : IOfficeClaimProfilePolicyProvider
    {
        private readonly OfficeClaimProfilePolicy? _policy;
        private readonly ClaimMasterPolicyUnavailableException? _error;

        public FakePolicyProvider(OfficeClaimProfilePolicy policy) => _policy = policy;

        public FakePolicyProvider(ClaimMasterPolicyUnavailableException error) => _error = error;

        public int ResolveCalls { get; private set; }

        public OfficeClaimProfilePolicy Resolve(ClaimMasterVersion masterVersion)
        {
            ResolveCalls++;
            if (_error is not null) throw _error;
            return _policy!;
        }
    }

    private sealed class FakeAverageWageAnnualEvidenceRepository
        : IAverageWageAnnualEvidenceRepository
    {
        public List<AverageWageAnnualEvidence> Items { get; } = [];
        public Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
            Guid officeId, int sourceFiscalYear, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AverageWageAnnualEvidence>>(Items
                .Where(item => item.OfficeId == officeId
                               && item.SourceFiscalYear == sourceFiscalYear)
                .ToArray());
    }

    private sealed class FakeOfficeClaimProfileRepository : IOfficeClaimProfileRepository
    {
        public List<OfficeClaimProfile> Items { get; } = [];
        public Task AddAsync(OfficeClaimProfile profile, CancellationToken ct)
        {
            Items.Add(profile);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
            Guid officeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OfficeClaimProfile>>(Items
                .Where(item => item.OfficeId == officeId)
                .ToArray());
    }

    private sealed class FakeCertificateClaimEvidenceRepository
        : ICertificateClaimEvidenceRepository
    {
        public List<CertificateClaimEvidence> Items { get; } = [];
        public Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
            Guid certificateId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CertificateClaimEvidence>>(Items
                .Where(item => item.CertificateId == certificateId)
                .ToArray());
    }

    private sealed class FakeUpperLimitManagementStatementRepository
        : IUpperLimitManagementStatementRepository
    {
        public List<UpperLimitManagementStatementAggregate> Aggregates { get; } = [];

        public Task AddAsync(
            UpperLimitManagementStatement statement,
            IReadOnlyCollection<UpperLimitManagementStatementLine> lines,
            CancellationToken ct)
        {
            Aggregates.Add(new UpperLimitManagementStatementAggregate(statement, lines));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UpperLimitManagementStatementAggregate>>
            ListHistoryAggregatesAsync(
                Guid managingOfficeId,
                Guid recipientId,
                ServiceMonth serviceMonth,
                CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UpperLimitManagementStatementAggregate>>(Aggregates
                .Where(item => item.Header.ManagingOfficeId == managingOfficeId
                               && item.Header.RecipientId == recipientId
                               && item.Header.ServiceMonth == serviceMonth)
                .ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
