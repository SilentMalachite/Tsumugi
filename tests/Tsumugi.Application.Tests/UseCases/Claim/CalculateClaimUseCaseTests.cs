using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class CalculateClaimUseCaseTests
{
    [Fact]
    public async Task Execute_returns_issues_and_skips_calculation_when_not_ready()
    {
        // (a) profileなし → readiness不成立。算定マスタの解決自体が呼ばれないことを確認する。
        var masterProvider = new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters());
        var useCase = CreateUseCase(
            Kit.Snapshot(includeProfile: false), masterProvider: masterProvider);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.PreviewHash.Should().BeEmpty();
        dto.Details.Should().BeEmpty();
        dto.TotalUnits.Should().Be(0);
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MissingRequiredEvidence
            && issue.FieldCode == "OfficeClaimProfile.Effective");
        masterProvider.CalculationMasterResolutions.Should().Be(0);
    }

    [Fact]
    public async Task Execute_maps_calculator_result_when_ready()
    {
        // (b) 700単位/日 × 2日 = 1,400単位 → ×10.00円 = 14,000円 → 1割相当1,400円(上限9,300円未満)
        var useCase = CreateUseCase(Kit.Snapshot());

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        dto.ServiceMonth.Should().Be(Kit.Month);
        dto.ClaimMasterVersion.Should().Be("master-v1");
        dto.PreviewHash.Should().MatchRegex("^[0-9a-f]{64}$");
        var detail = dto.Details.Should().ContainSingle().Subject;
        detail.RecipientId.Should().Be(Kit.RecipientId);
        detail.ServiceCode.Should().Be("610000");
        detail.BilledDays.Should().Be(2);
        detail.TotalUnits.Should().Be(1400);
        detail.TotalCostYen.Should().Be(14000);
        detail.BurdenYen.Should().Be(1400);
        detail.BenefitYen.Should().Be(12600);
        dto.TotalUnits.Should().Be(1400);
        dto.TotalCostYen.Should().Be(14000);
        dto.TotalBenefitYen.Should().Be(12600);
        dto.TotalBurdenYen.Should().Be(1400);
    }

    [Fact]
    public async Task Execute_fails_closed_when_the_month_has_no_transition_rule_for_the_profile_version()
    {
        // Task 13 (ADR 0023): 対象月のマスタ束にprofileのmaster版へ対応する経過措置rule
        // （transition-rules行）が無ければ算定しない（例: R8施行後にR6版profileが残留し
        // snapshot readerが期限切れprofileを返した場合の無検証単価請求を遮断する）。
        var useCase = CreateUseCase(
            Kit.Snapshot(),
            masterProvider: new Kit.FakeMasterProvider(
                Kit.Release(), Kit.SyntheticMasters(includeTransitionRule: false)));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.PreviewHash.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.MasterVersion");
    }

    [Fact]
    public async Task Execute_fails_closed_when_reform_status_and_band_option_disagree_with_the_month_rule()
    {
        // Task 13 (ADR 0023): R8ReformStatusと版付き許可option集合の不一致はフェイルクローズ
        // （合成ruleはNotApplicableBeforeR8だけへoptionを対応付けており、ReformExemptの
        // 宣言は経過措置検証で算定不能になる）。
        var useCase = CreateUseCase(
            Kit.Snapshot(profile: Kit.Profile(reformStatus: R8ReformStatus.ReformExempt)));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Details.Should().BeEmpty();
        dto.Issues.Should().ContainSingle(issue =>
            issue.Code == ClaimPreparationIssueCode.ReformTransitionMismatch
            && issue.FieldCode == "OfficeClaimProfile.AverageWageBandOption");
    }

    [Fact]
    public async Task Execute_produces_stable_preview_hash_for_identical_input()
    {
        // (c) 同一入力 → 同一hash（インスタンスをまたいでも決定論的）。
        var first = await CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        var second = await CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        second.PreviewHash.Should().Be(first.PreviewHash);
    }

    [Fact]
    public async Task Execute_changes_preview_hash_when_input_changes()
    {
        var baseline = await CreateUseCase(Kit.Snapshot()).ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);
        var changed = await CreateUseCase(Kit.Snapshot(
                billedDays: new Dictionary<Guid, int> { [Kit.RecipientId] = 3 }))
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        changed.PreviewHash.Should().NotBe(baseline.PreviewHash);
    }

    [Fact]
    public async Task Execute_does_not_block_on_zero_activity_recipient_with_nothing_else()
    {
        // Task 9b: 契約により対象者集合には残るが、実績0日・ClaimInput未入力・証未登録の
        // 利用者は、readinessのブロック評価から除外されるため月全体は依然としてready。
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input()],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(),
            },
            billedDays: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 2,
                [Kit.SecondRecipientId] = 0,
            },
            certificateCounts: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 1,
                [Kit.SecondRecipientId] = 0,
            });
        var useCase = CreateUseCase(snapshot);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue();
        dto.Issues.Should().BeEmpty();
        dto.Details.Should().ContainSingle(detail => detail.RecipientId == Kit.RecipientId);
    }

    [Fact]
    public async Task Execute_still_blocks_when_the_same_recipient_has_billed_days_and_missing_evidence()
    {
        // 同じ利用者でも実績日数が1日以上ある月は除外対象外に戻り、証や入力の欠落が
        // そのままブロック要因として可視化される。
        var snapshot = Kit.Snapshot(
            recipientIds: [Kit.RecipientId, Kit.SecondRecipientId],
            inputs: [Kit.Input()],
            evidenceByRecipient: new Dictionary<Guid, CertificateClaimEvidence>
            {
                [Kit.RecipientId] = Kit.Evidence(),
            },
            billedDays: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 2,
                [Kit.SecondRecipientId] = 1,
            },
            certificateCounts: new Dictionary<Guid, int>
            {
                [Kit.RecipientId] = 1,
                [Kit.SecondRecipientId] = 0,
            });
        var useCase = CreateUseCase(snapshot);

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Issues.Should().Contain(issue => issue.RecipientId == Kit.SecondRecipientId);
    }

    [Fact]
    public async Task Execute_is_not_ready_when_region_key_sources_conflict()
    {
        // controller decision 2026-07-19 (Task 9b fix round): 地域区分の二重ソース不一致は
        // プレビュー全体をNotReadyにする（Finding 1: OfficeClaimBillingTokenProvider fail-closed）。
        var useCase = CreateUseCase(
            Kit.Snapshot(),
            tokens: Kit.Tokens(regionKey: null, regionKeyConflict: true));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.RegionKeySourceConflict
            && issue.FieldCode == "OfficeClaimProfile.RegionKey");
    }

    [Fact]
    public async Task Execute_reports_master_version_unavailable_month()
    {
        var useCase = CreateUseCase(
            Kit.Snapshot(),
            masterProvider: new Kit.FakeMasterProvider(release: null, masters: null));

        var dto = await useCase.ExecuteAsync(
            new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeFalse();
        dto.ClaimMasterVersion.Should().BeEmpty();
        dto.Issues.Should().Contain(issue =>
            issue.Code == ClaimPreparationIssueCode.MasterVersionUnavailable);
    }

    [Fact]
    public async Task Execute_rejects_invalid_request()
    {
        var useCase = CreateUseCase(Kit.Snapshot());

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                new CalculateClaimRequest(Guid.Empty, Kit.Month), CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    internal static CalculateClaimUseCase CreateUseCase(
        ClaimCalculationSnapshot snapshot,
        Kit.FakeMasterProvider? masterProvider = null,
        ClaimBillingConditionTokens? tokens = null,
        Domain.Entities.Office? office = null,
        bool officeMissing = false)
        => new(
            new Kit.FakeSnapshotReader(snapshot),
            masterProvider ?? new Kit.FakeMasterProvider(Kit.Release(), Kit.SyntheticMasters()),
            new Kit.FakeOfficeRepository(officeMissing ? null : office ?? Kit.Office()),
            new Kit.FakeTokenProvider(tokens ?? Kit.Tokens()),
            Kit.Readiness());
}
