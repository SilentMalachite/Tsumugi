using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
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
