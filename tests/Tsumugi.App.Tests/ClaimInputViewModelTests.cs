using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests;

public sealed class ClaimInputViewModelTests
{
    private static readonly ServiceMonth Month = new(2026, 6);

    [Fact]
    public void Constructor_uses_workspace_query_and_exactly_the_five_owned_setters()
    {
        var dependencies = typeof(ClaimInputViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        dependencies.Should().Contain(typeof(QueryClaimInputWorkspaceUseCase));
        dependencies.Should().Contain(typeof(SetClaimInputUseCase));
        dependencies.Should().Contain(typeof(SetAverageWageAnnualEvidenceUseCase));
        dependencies.Should().Contain(typeof(SetOfficeClaimProfileUseCase));
        dependencies.Should().Contain(typeof(SetCertificateClaimEvidenceUseCase));
        dependencies.Should().Contain(typeof(SetUpperLimitManagementStatementUseCase));
        dependencies.Should().NotContain(typeof(SetIntensiveSupportEpisodeUseCase));
    }

    [Fact]
    public async Task Load_reads_history_heads_and_active_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);

        await fixture.Sut.LoadAsync();

        fixture.Sut.WorkspaceLoaded.Should().BeTrue();
        fixture.Sut.ClaimInputCurrentHeadId.Should().Be(fixture.ClaimInput.Items[0].Id);
        fixture.Sut.ClaimInputEffectiveHeadId.Should().Be(fixture.ClaimInput.Items[0].Id);
        fixture.Sut.ClaimInputRevisions.Should().ContainSingle();
        fixture.Sut.UpperLimitManagementResult.Should().Be(UpperLimitManagementResult.Result2);
        fixture.Sut.UpperLimitManagedAmountYen.Should().Be(1_000);
    }

    [Fact]
    public async Task Correcting_claim_input_preserves_the_five_non_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result3;
        fixture.Sut.UpperLimitManagedAmountYen = 2_000;

        await fixture.Sut.SaveClaimInputAsync();

        fixture.ClaimInput.Items.Should().HaveCount(2);
        var saved = fixture.ClaimInput.Items[^1];
        saved.Kind.Should().Be(RecordKind.Correct);
        saved.ExpectedHeadId.Should().Be(fixture.ClaimInput.Items[0].Id);
        saved.MunicipalSubsidyAmountYen.Should().Be(500);
        saved.ExceptionalUsageStartMonth.Should().Be(Month);
        saved.ExceptionalUsageEndMonth.Should().Be(Month);
        saved.ExceptionalUsageDays.Should().Be(10);
        saved.StandardUsageDayTotal.Should().Be(22);
    }

    [Fact]
    public async Task Context_change_clears_workspace_synchronously_and_blocks_save()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        await fixture.Sut.LoadAsync();

        fixture.Sut.SourceFiscalYear++;

        fixture.Sut.WorkspaceLoaded.Should().BeFalse();
        fixture.Sut.ClaimInputRevisions.Should().BeEmpty();
        fixture.Sut.UpperLimitManagementResult.Should().BeNull();
        fixture.Sut.UpperLimitManagedAmountYen.Should().BeNull();
        await fixture.Sut.SaveClaimInputAsync();
        fixture.ClaimInput.Items.Should().ContainSingle();
        fixture.Sut.ErrorMessage.Should().Be("請求入力履歴を再読込してください。");
    }

    [Fact]
    public async Task Cancelled_claim_input_requires_explicit_reentry_and_clears_non_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        var root = fixture.ClaimInput.Items[0];
        var cancelId = Guid.NewGuid();
        fixture.ClaimInput.Items.Add(new ClaimInput
        {
            Id = cancelId,
            OfficeId = root.OfficeId,
            RecipientId = root.RecipientId,
            ServiceMonth = root.ServiceMonth,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            CreatedAt = DateTimeOffset.UnixEpoch.AddHours(1),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        });
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result1;
        fixture.Sut.UpperLimitManagedAmountYen = 300;

        await fixture.Sut.SaveClaimInputAsync();
        fixture.ClaimInput.Items.Should().HaveCount(2);

        fixture.Sut.ReenterClaimInput();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result1;
        fixture.Sut.UpperLimitManagedAmountYen = 300;
        await fixture.Sut.SaveClaimInputAsync();

        var reentered = fixture.ClaimInput.Items.Should().HaveCount(3).And.Subject.Last();
        reentered.Kind.Should().Be(RecordKind.Correct);
        reentered.ExpectedHeadId.Should().Be(cancelId);
        reentered.MunicipalSubsidyAmountYen.Should().BeNull();
        reentered.ExceptionalUsageStartMonth.Should().BeNull();
        reentered.ExceptionalUsageEndMonth.Should().BeNull();
        reentered.ExceptionalUsageDays.Should().BeNull();
        reentered.StandardUsageDayTotal.Should().BeNull();
    }

    [Fact]
    public async Task New_claim_input_starts_with_null_expected_head_and_null_non_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result2;
        fixture.Sut.UpperLimitManagedAmountYen = 900;

        await fixture.Sut.SaveClaimInputAsync();

        var created = fixture.ClaimInput.Items.Should().ContainSingle().Subject;
        created.Kind.Should().Be(RecordKind.New);
        created.ExpectedHeadId.Should().BeNull();
        created.MunicipalSubsidyAmountYen.Should().BeNull();
        created.ExceptionalUsageStartMonth.Should().BeNull();
        created.ExceptionalUsageEndMonth.Should().BeNull();
        created.ExceptionalUsageDays.Should().BeNull();
        created.StandardUsageDayTotal.Should().BeNull();
    }

    [Fact]
    public async Task Load_discards_response_when_context_changes_while_query_is_in_flight()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        fixture.ClaimInput.BlockNextLoad();

        var load = fixture.Sut.LoadAsync();
        await fixture.ClaimInput.LoadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.SourceFiscalYear++;
        fixture.ClaimInput.ReleaseLoad();
        await load;

        fixture.Sut.WorkspaceLoaded.Should().BeFalse();
        fixture.Sut.ClaimInputRevisions.Should().BeEmpty();
        fixture.Sut.ClaimInputCurrentHeadId.Should().BeNull();
    }

    [Fact]
    public async Task Query_failure_uses_fixed_sanitized_message()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        var existing = fixture.ClaimInput.Items[0];
        var secondRoot = Guid.NewGuid();
        fixture.ClaimInput.Items.Add(existing with
        {
            Id = secondRoot,
            RootId = secondRoot,
            UpperLimitManagedAmountYen = 999_999,
        });

        await fixture.Sut.LoadAsync();

        fixture.Sut.WorkspaceLoaded.Should().BeFalse();
        fixture.Sut.ErrorMessage.Should().Be("請求入力履歴を再読込してください。");
        fixture.Sut.ErrorMessage.Should().NotContain(secondRoot.ToString())
            .And.NotContain("Claim input query failed");
    }

    [Fact]
    public async Task Average_wage_reentry_preserves_chain_identity_period()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        var rootId = Guid.NewGuid();
        var cancelId = Guid.NewGuid();
        var periodStart = new DateOnly(2025, 4, 1);
        var periodEnd = new DateOnly(2026, 3, 31);
        fixture.AverageWage.Items.AddRange([
            new AverageWageAnnualEvidence
            {
                Id = rootId,
                OfficeId = fixture.Sut.OfficeId,
                SourceFiscalYear = fixture.Sut.SourceFiscalYear,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                RootId = rootId,
                Revision = 1,
                Kind = RecordKind.New,
                AnnualWagePaidYen = 1_000,
                AnnualExtendedUsers = 10,
                AnnualOpeningDays = 20,
                Completeness = FiscalYearCompleteness.Complete,
                EvidenceDocumentId = "evidence",
                DailyEvidenceReference = "daily",
                MonthlyEvidenceReference = "monthly",
                ConfirmedAt = DateTimeOffset.UnixEpoch,
                ConfirmedBy = "reviewer",
                ConfirmationReason = "checked",
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "operator",
                ConcurrencyToken = Guid.NewGuid(),
            },
            new AverageWageAnnualEvidence
            {
                Id = cancelId,
                OfficeId = fixture.Sut.OfficeId,
                SourceFiscalYear = fixture.Sut.SourceFiscalYear,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                RootId = rootId,
                Revision = 2,
                Kind = RecordKind.Cancel,
                ExpectedHeadId = rootId,
                CreatedAt = DateTimeOffset.UnixEpoch.AddHours(1),
                CreatedBy = "operator",
                ConcurrencyToken = Guid.NewGuid(),
            },
        ]);
        await fixture.Sut.LoadAsync();

        fixture.Sut.ReenterAverageWage();

        fixture.Sut.AverageWagePeriodStart.Should().Be(periodStart);
        fixture.Sut.AverageWagePeriodEnd.Should().Be(periodEnd);
        fixture.Sut.AverageWageCurrentHeadId.Should().Be(cancelId);
    }

    private static Fixture CreateFixture(bool withActiveClaimInput)
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();
        var claimInput = new FakeClaimInputRepository();
        if (withActiveClaimInput)
        {
            var id = Guid.NewGuid();
            claimInput.Items.Add(new ClaimInput
            {
                Id = id,
                OfficeId = officeId,
                RecipientId = recipientId,
                ServiceMonth = Month,
                RootId = id,
                Revision = 1,
                Kind = RecordKind.New,
                UpperLimitManagementResult = UpperLimitManagementResult.Result2,
                UpperLimitManagedAmountYen = 1_000,
                MunicipalSubsidyAmountYen = 500,
                ExceptionalUsageStartMonth = Month,
                ExceptionalUsageEndMonth = Month,
                ExceptionalUsageDays = 10,
                StandardUsageDayTotal = 22,
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "operator",
                ConcurrencyToken = Guid.NewGuid(),
            });
        }

        var averageWage = new FakeAverageWageRepository();
        var officeProfile = new FakeOfficeProfileRepository();
        var certificateEvidence = new FakeCertificateEvidenceRepository();
        var statement = new FakeStatementRepository();
        var policy = CreateOfficePolicy();
        var query = new QueryClaimInputWorkspaceUseCase(
            claimInput, averageWage, officeProfile, certificateEvidence, statement, policy);
        var uow = new FakeUnitOfWork();
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddDays(1));
        var sut = new ClaimInputViewModel(
            null!, null!, null!, query,
            new SetClaimInputUseCase(claimInput, uow, clock),
            new SetAverageWageAnnualEvidenceUseCase(averageWage, uow, clock),
            new SetOfficeClaimProfileUseCase(officeProfile, uow, clock, policy),
            new SetCertificateClaimEvidenceUseCase(certificateEvidence, uow, clock),
            new SetUpperLimitManagementStatementUseCase(statement, uow, clock))
        {
            OfficeId = officeId,
            RecipientId = recipientId,
            CertificateId = certificateId,
            Year = Month.Year,
            Month = Month.Month,
            SourceFiscalYear = 2025,
        };
        return new Fixture(sut, claimInput, averageWage);
    }

    private static OfficeClaimProfilePolicy CreateOfficePolicy()
    {
        var version = new ClaimMasterVersion("test-master");
        var option = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1);
        var rule = new AverageWageBandOptionVersionRule(
            version, Month, null, [option],
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.ReformTarget] = [option],
            });
        return new OfficeClaimProfilePolicy(
            version, [rule], new DateOnly(2026, 6, 1), date => date.AddYears(3));
    }

    private sealed record Fixture(
        ClaimInputViewModel Sut,
        FakeClaimInputRepository ClaimInput,
        FakeAverageWageRepository AverageWage);

    private sealed class FakeClaimInputRepository : IClaimInputRepository
    {
        private TaskCompletionSource? _loadStarted;
        private TaskCompletionSource? _releaseLoad;
        public List<ClaimInput> Items { get; } = [];
        public Task LoadStarted => _loadStarted?.Task ?? Task.CompletedTask;
        public void BlockNextLoad()
        {
            _loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public void ReleaseLoad() => _releaseLoad?.TrySetResult();
        public Task AddAsync(ClaimInput input, CancellationToken ct)
        {
            Items.Add(input);
            return Task.CompletedTask;
        }
        public async Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
            Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct)
        {
            if (_loadStarted is { } started && _releaseLoad is { } release)
            {
                _loadStarted = null;
                started.TrySetResult();
                await release.Task.WaitAsync(ct);
                _releaseLoad = null;
            }
            return Items.Where(item => item.OfficeId == officeId
                && item.RecipientId == recipientId && item.ServiceMonth == serviceMonth).ToArray();
        }
    }

    private sealed class FakeAverageWageRepository : IAverageWageAnnualEvidenceRepository
    {
        public List<AverageWageAnnualEvidence> Items { get; } = [];
        public Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
            Guid officeId, int sourceFiscalYear, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AverageWageAnnualEvidence>>(Items.Where(item =>
                item.OfficeId == officeId && item.SourceFiscalYear == sourceFiscalYear).ToArray());
    }

    private sealed class FakeOfficeProfileRepository : IOfficeClaimProfileRepository
    {
        public List<OfficeClaimProfile> Items { get; } = [];
        public Task AddAsync(OfficeClaimProfile profile, CancellationToken ct)
        {
            Items.Add(profile);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
            Guid officeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OfficeClaimProfile>>(
                Items.Where(item => item.OfficeId == officeId).ToArray());
    }

    private sealed class FakeCertificateEvidenceRepository : ICertificateClaimEvidenceRepository
    {
        public List<CertificateClaimEvidence> Items { get; } = [];
        public Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
            Guid certificateId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CertificateClaimEvidence>>(
                Items.Where(item => item.CertificateId == certificateId).ToArray());
    }

    private sealed class FakeStatementRepository : IUpperLimitManagementStatementRepository
    {
        public List<UpperLimitManagementStatementAggregate> Items { get; } = [];
        public Task AddAsync(UpperLimitManagementStatement statement,
            IReadOnlyCollection<UpperLimitManagementStatementLine> lines, CancellationToken ct)
        {
            Items.Add(new UpperLimitManagementStatementAggregate(statement, lines));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<UpperLimitManagementStatementAggregate>>
            ListHistoryAggregatesAsync(Guid managingOfficeId, Guid recipientId,
                ServiceMonth serviceMonth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UpperLimitManagementStatementAggregate>>(Items.Where(item =>
                item.Header.ManagingOfficeId == managingOfficeId
                && item.Header.RecipientId == recipientId
                && item.Header.ServiceMonth == serviceMonth).ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
