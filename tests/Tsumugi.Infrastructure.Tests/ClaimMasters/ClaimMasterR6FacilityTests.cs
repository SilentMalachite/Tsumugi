using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// R6-06世代（2024-06〜2026-05）の処遇改善加算における指定障害者支援施設variantを
/// production seedで固定する（ADR 0048）。Phase 3-5がR8-06について塞いだ欠陥と
/// 同一クラスの欠陥がR6世代に残っていたものを解消する。
/// </summary>
public sealed class ClaimMasterR6FacilityTests
{
    private static readonly ServiceMonth June2024 = new(2024, 6);

    private static readonly JsonClaimMasterProvider Provider =
        JsonClaimMasterProvider.LoadEmbedded();

    /// <summary>
    /// R6世代の処遇改善加算family（通常・施設の両方）だけへ絞り込む。
    /// <c>ResolveAdditions</c>は条件に一致する加算行を<b>すべて</b>返すため、
    /// 欠席時対応加算のようなreward-system条件しか持たない行が常に混入する。
    /// </summary>
    private static ResolvedUnitAddition[] TreatmentImprovementRows(
        IReadOnlyList<ResolvedUnitAddition> rows) => rows
        .Where(row => row.AdjustmentComponentKey.StartsWith(
            "addition.treatment-improvement.unified.", StringComparison.Ordinal))
        .ToArray();

    private static ClaimBillingConditionContext Context(
        int officialOptionCode, string? facilityClassification) => new(
        RewardSystem: "employment-continuation-support-b",
        PaymentBand: "",
        CapacityHeadcount: 15,
        StaffingKey: "staff-6-1",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
        OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal)
        {
            $"mhlw.b46.capability.treatment-improvement.{officialOptionCode}",
        },
        FacilityClassification: facilityClassification);

    /// <summary>
    /// ADR 0048: 施設variantを持つ3区分（体制届option 2・4・5）は施設区分ごとに
    /// ちょうど1行へ解決する。通常行と施設行の両方が一致する状態は条件の付け方を
    /// 誤った証拠（＝二重計上）であり、ここで検出する。
    /// </summary>
    [Theory]
    [InlineData(2, "465120", "465138")]
    [InlineData(4, "465122", "465140")]
    [InlineData(5, "465123", "465141")]
    public void Facility_variants_resolve_to_exactly_one_row_per_classification(
        int officialOptionCode, string generalCode, string facilityCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var general = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            masters, June2024, Context(officialOptionCode, "general")));
        var facility = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            masters, June2024, Context(officialOptionCode, "designated-support-facility")));

        general.Should().ContainSingle(
            $"非施設 × option {officialOptionCode} は通常行だけに一致する")
            .Which.ServiceCode.Should().Be(generalCode);
        facility.Should().ContainSingle(
            $"施設 × option {officialOptionCode} は施設行だけに一致する")
            .Which.ServiceCode.Should().Be(facilityCode);
    }

    /// <summary>
    /// ADR 0048: (Ⅱ)（option 3）は告示に括弧書きが無くサービスコードも存在しないため
    /// 施設区分条件を付けない。施設事業所でも通常行へ解決しなければならない
    /// （条件を付けると施設事業所が算定できなくなる＝無音の未算定）。
    /// </summary>
    [Theory]
    [InlineData("general")]
    [InlineData("designated-support-facility")]
    public void Tier_two_has_no_facility_variant_and_resolves_for_both_classifications(
        string classification)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var rows = TreatmentImprovementRows(
            ServiceCodeResolver.ResolveAdditions(masters, June2024, Context(3, classification)));

        rows.Should().ContainSingle("(Ⅱ)は施設別立てが無く施設区分に依らず1行へ解決する")
            .Which.ServiceCode.Should().Be("465121");
    }

    /// <summary>
    /// 施設区分が未入力（null）のとき、施設variantを持つ区分は推測で通常行へ倒さず
    /// 専用コードでフェイルクローズする（ADR 0047の方針をR6世代へ適用）。
    /// </summary>
    [Fact]
    public void An_unresolved_facility_classification_fails_closed()
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var act = () => ServiceCodeResolver.ResolveAdditions(
            masters, June2024, Context(2, facilityClassification: null));

        act.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(
                ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved);
    }

    /// <summary>
    /// 施設variantはR6世代（2024-06〜2026-05）に閉じる。2026-06以降はADR 0047が
    /// 投入したR8世代の行が担うため、R6キーが漏れ出さないことを固定する。
    /// </summary>
    [Fact]
    public void R6_facility_rows_do_not_reach_june_2026()
    {
        var june2026 = Provider.ResolveCalculationMasters(new ServiceMonth(2026, 6));

        june2026.ServiceCodes
            .Select(row => row.Key)
            .Where(key => key.StartsWith("b-addition.r6-06.treatment-improvement.", StringComparison.Ordinal))
            .Should().BeEmpty("R6世代の処遇改善行は2026-05で終了する");
    }
}
