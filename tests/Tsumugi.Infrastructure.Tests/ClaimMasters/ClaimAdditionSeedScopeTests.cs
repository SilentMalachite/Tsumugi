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
/// <para>
/// ADR 0045（Task 2）: R8処遇改善(Ⅰ)イ・(Ⅱ)イ・(Ⅲ)・(Ⅳ)は、公式資料（`r8-service-codes-2-xlsx`・
/// `r8-service-codes-2-pdf` の2方式抽出が一致、ADR 0021の請求サービスコード対応表とも一致）が
/// R6統一処遇改善と**同一のサービスコード**（465120/465121/465122/465123）を2026-06以降も継続
/// 使用することを示している。新設区分(Ⅰ)ロ・(Ⅱ)ロだけが新コード（465174/465175）を持つ。
/// 処遇改善(Ⅴ)・障害者支援施設variant（465138/465140/465141/465176等）は本タスクでも未確定
/// につきseedしない。
/// </para>
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

    /// <summary>
    /// ADR 0045: R8処遇改善(Ⅰ)イ・(Ⅰ)ロ・(Ⅱ)イ・(Ⅱ)ロ・(Ⅲ)・(Ⅳ)の事業所コード（2026-06〜）。
    /// (Ⅰ)イ・(Ⅱ)イ・(Ⅲ)・(Ⅳ)はUnifiedTreatmentImprovementCodesと同一コードを継続使用し、
    /// (Ⅰ)ロ・(Ⅱ)ロだけが新設コード（465174・465175）を持つ。
    /// </summary>
    private static readonly string[] R8TreatmentImprovementCodes =
    [
        "465120", "465174", "465121", "465175", "465122", "465123",
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
        // ADR 0028決定1・4.1: 統一(Ⅰ)〜(Ⅳ)は2024-06-01〜2026-05-31。R6-04月（旧3制度の期間）には
        // 現れない。固定単位行は全期間で継続。
        var r6PreCodes = AdditionRows(new ServiceMonth(2024, 4))
            .Select(row => row.ServiceCode).ToHashSet(StringComparer.Ordinal);
        r6PreCodes.Should().NotIntersectWith(UnifiedTreatmentImprovementCodes);
        r6PreCodes.Should().Contain(FixedAdditionCodes);

        // ADR 0045: 2026-06はR8処遇改善へ世代交代する。465120/465121/465122/465123は公式資料上
        // (Ⅰ)イ/(Ⅱ)イ/(Ⅲ)/(Ⅳ)として2026-06以降も継続使用されるため、「含まれない」ことは
        // ここでは主張しない（正しい継続/新設の組合せは
        // R8_treatment_improvement_rows_apply_only_from_2026_06 が固定する）。
        var juneCodes = AdditionRows(new ServiceMonth(2026, 6))
            .Select(row => row.ServiceCode).ToHashSet(StringComparer.Ordinal);
        juneCodes.Should().Contain(FixedAdditionCodes);

        AdditionRows(new ServiceMonth(2024, 6)).Select(row => row.ServiceCode)
            .Should().Contain(UnifiedTreatmentImprovementCodes);
        AdditionRows(new ServiceMonth(2026, 5)).Select(row => row.ServiceCode)
            .Should().Contain(UnifiedTreatmentImprovementCodes);
    }

    /// <summary>
    /// ADR 0045: R8処遇改善行は2026-06以降のみ有効。公式資料は(Ⅰ)イ・(Ⅱ)イ・(Ⅲ)・(Ⅳ)について
    /// R6統一処遇改善と同一のサービスコード（465120/465121/465122/465123）を2026-06以降も継続
    /// 使用し、(Ⅰ)ロ・(Ⅱ)ロの新設区分だけが新コード（465174/465175）を持つ
    /// （`r8-service-codes-2-xlsx`・`r8-service-codes-2-pdf`で2方式確認、ADR 0021の請求サービス
    /// コード対応表とも一致）。したがって「R8の新コードはR6コードと重複しない」という想定は誤りで
    /// あり、コード集合の**完全一致**（上限も含む）で正しい継続/新設の組合せを固定する。
    /// </summary>
    /// <remarks>
    /// Fix Round 1 I-1: 当初は<c>R8TreatmentImprovementCodes.Except(UnifiedTreatmentImprovementCodes)</c>
    /// という同一ファイル内のリテラル配列同士を比較していたため、seedを一切読まない恒真式になって
    /// いた（additions.json / service-codes.jsonが何であっても失敗しない。障害者支援施設variant
    /// の465138等を誤ってseedしても全緑だった）。本バージョンは<see cref="AdditionRows"/>で
    /// production seedから解決した実データどうしを比較する。
    /// </remarks>
    [Fact]
    public void R8_treatment_improvement_rows_apply_only_from_2026_06()
    {
        // 2026-06のR8処遇改善コード集合を、期待6コードちょうどと完全一致で固定する
        // （上限側も固定するため、465138等のvariantを誤ってseedしてもここでRED化する）。
        var juneCodes = AdditionRows(new ServiceMonth(2026, 6))
            .Select(row => row.ServiceCode)
            .Order(StringComparer.Ordinal);
        juneCodes.Should().Equal(
            FixedAdditionCodes.Concat(R8TreatmentImprovementCodes).Order(StringComparer.Ordinal),
            "2026-06の加算コード集合は固定単位行＋R8処遇改善6区分ちょうどでなければならない");

        // 465174/465175だけが2026-06で新たに現れることを、リテラル同士ではなく2026-05・2026-06
        // それぞれのseedから解決した実データの差分で確認する。
        var mayCodeSet = AdditionRows(new ServiceMonth(2026, 5))
            .Select(row => row.ServiceCode).ToHashSet(StringComparer.Ordinal);
        var juneCodeSet = AdditionRows(new ServiceMonth(2026, 6))
            .Select(row => row.ServiceCode).ToHashSet(StringComparer.Ordinal);
        juneCodeSet.Except(mayCodeSet).Order(StringComparer.Ordinal).Should().Equal(
            ["465174", "465175"],
            "新設区分(Ⅰ)ロ・(Ⅱ)ロだけが2026-06で新たに現れる（ADR 0045）");
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
