using FluentAssertions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// Task 11（ADR 0028）の加算seedスコープを固定するpinned test。
/// <para>
/// ADR 0028決定5が特定したruntime実績入力のストレージgapを持つ行は、このラウンドでは
/// **意図的にseedしない**（推測入力・黙示マッピングでの穴埋めを禁止。readiness gate／入力UIで
/// 顕在化させる）。将来ストレージを実装して行を追加する際は、本テストの期待値を意識的に
/// 更新すること（暗黙にseedへ滑り込ませない）。
/// </para>
/// <list type="bullet">
/// <item>465050 初期加算 — 利用開始日の専用ストレージなし（Contract.Periodの契約開始日への
/// 黙示マッピング禁止。ADR 0028決定5）。</item>
/// <item>466592 / 466593 送迎加算(Ⅰ)(Ⅱ)同一敷地内 — 同一敷地内送迎かの判別フィールドが
/// DailyRecord・Certificate・OfficeCapabilityのいずれにもない（ADR 0028決定5）。</item>
/// <item>旧処遇改善3制度（466715/466716/466710/466711/466665/466666/466772/466773/466774/466766）—
/// 2024-04〜05のみ算定可能で現行請求月では算定不能（ADR 0028決定4.2「seedするかは実装判断」）。
/// 加えて体制区分の対応（legacy-career-path選択番号⇔処遇改善Ⅰ/Ⅱ/Ⅲ）が登録済み一次資料から
/// 一意に確定できないため、確定なしのseedは行わない。</item>
/// <item>処遇改善(Ⅴ)・障害者支援施設variant等はADR 0028決定8のスコープ外。</item>
/// </list>
/// </summary>
public sealed class ClaimAdditionSeedScopeTests
{
    /// <summary>ADR 0028決定5のgapによりseedしない行（実装されるまで絶対に現れないこと）。</summary>
    private static readonly string[] ExcludedByStorageGap = ["465050", "466592", "466593"];

    /// <summary>旧処遇改善3制度（決定4.2）。現行請求月で算定不能のためseedしない。</summary>
    private static readonly string[] ExcludedLegacyTreatmentImprovement =
    [
        "466715", "466716", "466710", "466711", "466665", "466666",
        "466772", "466773", "466774", "466766",
    ];

    private static ServiceCodeMasterRow[] AdditionRows(ServiceMonth month) =>
        JsonClaimMasterProvider.LoadEmbedded()
            .ResolveCalculationMasters(month)
            .ServiceCodes
            .Where(row => row.UnitRule is UnitAdditionRule)
            .ToArray();

    private static readonly string[] FixedAdditionCodes =
    [
        "465070",  // 食事提供体制加算
        "465255",  // 目標工賃達成指導員配置加算（定員20人以下）
        "465256",  // 同（定員21〜40人）
        "465257",  // 同（定員41〜60人）
        "465258",  // 同（定員61〜80人）
        "465259",  // 同（定員81人以上）
        "466035",  // 福祉専門職員配置等加算(Ⅱ)
        "466036",  // 福祉専門職員配置等加算(Ⅲ)
        "466037",  // 福祉専門職員配置等加算(Ⅰ)
        "466040",  // 欠席時対応加算（月4回上限はマスタ行のmonthlyCountCap）
        "466590",  // 送迎加算(Ⅰ)
        "466591",  // 送迎加算(Ⅱ)（同一敷地variantは決定5 gapのため未seed）
    ];

    /// <summary>統一 福祉・介護職員等処遇改善加算(Ⅰ)〜(Ⅳ)の事業所コード（2024-06〜2026-05）。</summary>
    private static readonly string[] UnifiedTreatmentImprovementCodes =
    [
        "465120", "465121", "465122", "465123",
    ];

    [Fact]
    public void R6_fixed_addition_rows_cover_exactly_the_implemented_scope()
    {
        var codes = AdditionRows(new ServiceMonth(2025, 4))
            .Select(row => row.ServiceCode)
            .Order(StringComparer.Ordinal);

        codes.Should().Equal(
            FixedAdditionCodes.Concat(UnifiedTreatmentImprovementCodes).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Unified_treatment_improvement_rows_apply_only_between_2024_06_and_2026_05()
    {
        // ADR 0028決定1・4.1: 統一(Ⅰ)〜(Ⅳ)は2024-06-01〜2026-05-31。R6-04月（旧3制度の期間）と
        // R8-06月（新率・新コード、決定7でスコープ外）には現れない。固定単位行は全期間で継続。
        foreach (var month in new ServiceMonth[] { new(2024, 4), new(2026, 6) })
        {
            var codes = AdditionRows(month).Select(row => row.ServiceCode).ToHashSet(StringComparer.Ordinal);
            codes.Should().NotIntersectWith(UnifiedTreatmentImprovementCodes);
            codes.Should().Contain(FixedAdditionCodes);
        }

        AdditionRows(new ServiceMonth(2024, 6)).Select(row => row.ServiceCode)
            .Should().Contain(UnifiedTreatmentImprovementCodes);
        AdditionRows(new ServiceMonth(2026, 5)).Select(row => row.ServiceCode)
            .Should().Contain(UnifiedTreatmentImprovementCodes);
    }

    [Fact]
    public void Storage_gap_rows_and_legacy_treatment_improvement_rows_stay_unseeded()
    {
        foreach (var month in new ServiceMonth[] { new(2024, 4), new(2025, 4), new(2026, 6) })
        {
            var codes = AdditionRows(month).Select(row => row.ServiceCode).ToHashSet(StringComparer.Ordinal);
            codes.Should().NotIntersectWith(ExcludedByStorageGap,
                because: "ADR 0028決定5のストレージgap行はreadiness/入力UI実装まで意図的にseedしない");
            codes.Should().NotIntersectWith(ExcludedLegacyTreatmentImprovement,
                because: "旧処遇改善3制度は現行請求月で算定不能かつ体制区分対応が未確定のためseedしない");
        }
    }
}
