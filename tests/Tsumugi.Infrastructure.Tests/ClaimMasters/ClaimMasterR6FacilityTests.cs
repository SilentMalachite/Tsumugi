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

    private static ClaimBillingConditionContext VContext(
        int subdivision, string? facilityClassification) => new(
        RewardSystem: "employment-continuation-support-b",
        PaymentBand: "",
        CapacityHeadcount: 15,
        StaffingKey: "staff-6-1",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
        OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal)
        {
            "mhlw.b46.capability.treatment-improvement.6",
            $"mhlw.b46.capability.treatment-improvement-v-band.{subdivision}",
        },
        FacilityClassification: facilityClassification);

    /// <summary>
    /// ADR 0048: (Ⅴ)の14サブ区分は通常事業所で全件解決する。
    /// </summary>
    [Theory]
    [InlineData(1, "465124")]
    [InlineData(2, "465125")]
    [InlineData(3, "465126")]
    [InlineData(4, "465127")]
    [InlineData(5, "465128")]
    [InlineData(6, "465129")]
    [InlineData(7, "465130")]
    [InlineData(8, "465131")]
    [InlineData(9, "465132")]
    [InlineData(10, "465133")]
    [InlineData(11, "465134")]
    [InlineData(12, "465135")]
    [InlineData(13, "465136")]
    [InlineData(14, "465137")]
    public void Category_v_subdivisions_resolve_for_a_general_office(
        int subdivision, string expectedCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var rows = TreatmentImprovementRows(
            ServiceCodeResolver.ResolveAdditions(masters, June2024, VContext(subdivision, "general")));

        rows.Should().ContainSingle($"(Ⅴ)⑵{subdivision}は通常行だけに一致する")
            .Which.ServiceCode.Should().Be(expectedCode);
    }

    /// <summary>
    /// ADR 0048: (Ⅴ)のうち施設variantを持つ9サブ区分は施設事業所で施設行へ解決する。
    /// 告示の括弧書きの有無とサービスコード表の欠番が一致することを根拠とする。
    /// </summary>
    [Theory]
    [InlineData(1, "465142")]
    [InlineData(2, "465143")]
    [InlineData(5, "465146")]
    [InlineData(7, "465148")]
    [InlineData(8, "465149")]
    [InlineData(10, "465151")]
    [InlineData(11, "465152")]
    [InlineData(13, "465154")]
    [InlineData(14, "465155")]
    public void Category_v_facility_variants_resolve_for_a_facility_office(
        int subdivision, string expectedFacilityCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var rows = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            masters, June2024, VContext(subdivision, "designated-support-facility")));

        rows.Should().ContainSingle($"(Ⅴ)⑵{subdivision}施設は施設行だけに一致する")
            .Which.ServiceCode.Should().Be(expectedFacilityCode);
    }

    /// <summary>
    /// ADR 0048: 施設variantを持たない5サブ区分（⑶⑷⑹⑼⑿）は施設事業所でも
    /// 通常行へ解決する。条件を付けると施設事業所が算定できなくなる。
    /// </summary>
    [Theory]
    [InlineData(3, "465126")]
    [InlineData(4, "465127")]
    [InlineData(6, "465129")]
    [InlineData(9, "465132")]
    [InlineData(12, "465135")]
    public void Category_v_subdivisions_without_a_facility_variant_resolve_for_both(
        int subdivision, string expectedCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        foreach (var classification in new[] { "general", "designated-support-facility" })
        {
            var rows = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
                masters, June2024, VContext(subdivision, classification)));

            rows.Should().ContainSingle(
                $"(Ⅴ)⑵{subdivision}は施設別立てが無く{classification}でも通常行へ解決する")
                .Which.ServiceCode.Should().Be(expectedCode);
        }
    }

    /// <summary>
    /// ADR 0048: (Ⅴ)は令和7年3月31日限りで失効する。r6-fee-noticeの規定本文が
    /// 期限を明記し、r8-fee-noticeが当該規定を「（削る）」として削除し、
    /// R8サービスコード表にも465124〜465137が存在しないことによる。
    /// </summary>
    [Fact]
    public void Category_v_expires_at_the_end_of_march_2025()
    {
        var march = Provider.ResolveCalculationMasters(new ServiceMonth(2025, 3));
        var april = Provider.ResolveCalculationMasters(new ServiceMonth(2025, 4));

        TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            march, new ServiceMonth(2025, 3), VContext(1, "general")))
            .Should().ContainSingle("2025-03は(Ⅴ)が有効な最終月");

        TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            april, new ServiceMonth(2025, 4), VContext(1, "general")))
            .Should().BeEmpty("2025-04以降に(Ⅴ)は存在しない");
    }

    /// <summary>
    /// ADR 0048 完全性: R6-06世代の処遇改善サービスコードは、r6-service-codes-2-pdf
    /// 物理259頁に現れる type-46 処遇改善コード30件と過不足なく一致する。
    /// 集合の上限（余分な行の混入）と下限（投入漏れ）の両方をここで固定する。
    /// </summary>
    [Fact]
    public void The_r6_treatment_improvement_codes_match_the_official_table_exactly()
    {
        string[] expected =
        [
            "465120", "465121", "465122", "465123", "465124", "465125", "465126",
            "465127", "465128", "465129", "465130", "465131", "465132", "465133",
            "465134", "465135", "465136", "465137", "465138", "465140", "465141",
            "465142", "465143", "465146", "465148", "465149", "465151", "465152",
            "465154", "465155",
        ];

        var actual = Provider.ResolveCalculationMasters(June2024).ServiceCodes
            .Where(row => row.Key.StartsWith(
                "b-addition.r6-06.treatment-improvement.", StringComparison.Ordinal))
            .Select(row => row.ServiceCode)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        actual.Should().BeEquivalentTo(expected,
            "r6-service-codes-2-pdf 物理259頁の type-46 処遇改善コードは30件ちょうど");
    }

    /// <summary>
    /// ADR 0048: 率は告示（r6-fee-notice 物理235〜238頁）の値そのものでなければならない。
    /// </summary>
    [Theory]
    [InlineData("addition.treatment-improvement.unified.i.facility", "0.104")]
    [InlineData("addition.treatment-improvement.unified.iii.facility", "0.086")]
    [InlineData("addition.treatment-improvement.unified.iv.facility", "0.069")]
    [InlineData("addition.treatment-improvement.unified.v-1", "0.080")]
    [InlineData("addition.treatment-improvement.unified.v-1.facility", "0.091")]
    [InlineData("addition.treatment-improvement.unified.v-14", "0.031")]
    [InlineData("addition.treatment-improvement.unified.v-14.facility", "0.035")]
    public void R6_treatment_improvement_percentages_match_the_notice(
        string additionKey, string expectedPercentage)
    {
        var row = Provider.ResolveCalculationMasters(June2024).UnitAdjustments
            .Should().ContainSingle(r => r.Key == additionKey).Subject;

        row.Amount.Should().BeOfType<PercentageOfTargetAmount>()
            .Which.Percentage.Should().Be(decimal.Parse(
                expectedPercentage, System.Globalization.CultureInfo.InvariantCulture));
    }
}
