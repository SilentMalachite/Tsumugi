using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// R8-06境界（2026-05→2026-06）の切替をproduction seedで固定する（Task 13・ADR 0023）。
/// 経過措置ruleはtransition-rules.jsonのseedから供給され、体制届の公式option番号を
/// C#へ直書きしない（値の出典はADR 0023の版付き対応表とsource inventory manifest）。
/// </summary>
public sealed class ClaimMasterR8BoundaryTests
{
    private static readonly ServiceMonth May2026 = new(2026, 5);
    private static readonly ServiceMonth June2026 = new(2026, 6);

    private static readonly JsonClaimMasterProvider Provider =
        JsonClaimMasterProvider.LoadEmbedded();

    private static AverageWageBandOption Numeric(int code)
        => new(AverageWageBandOptionKind.Numeric, code);

    private static AverageWageBandOption FiledTransition()
        => new(AverageWageBandOptionKind.FiledTransition, 8);

    private static AverageWageBandOption ProductionActivitySupport()
        => new(AverageWageBandOptionKind.ProductionActivitySupport, 10);

    private static OfficeClaimProfileTransitionRuleMasterRow SingleTransitionRule(
        ServiceMonth month)
    {
        var rules = Provider.ResolveCalculationMasters(month).TransitionRules;
        rules.Should().ContainSingle(
            $"月{month.Year}-{month.Month:00}の経過措置ruleは一意でなければならない");
        return rules[0];
    }

    [Theory]
    [InlineData(2024, 4, "claim-master-r6-04")]
    [InlineData(2024, 5, "claim-master-r6-04")]
    [InlineData(2024, 6, "claim-master-r6-06")]
    [InlineData(2025, 9, "claim-master-r6-06")]
    [InlineData(2026, 5, "claim-master-r6-06")]
    [InlineData(2026, 6, "claim-master-r8-06")]
    public void Transition_rules_resolve_a_single_band_edition_per_month(
        int year, int month, string expectedMasterVersion)
    {
        var rule = SingleTransitionRule(new ServiceMonth(year, month));

        rule.MasterVersion.Value.Should().Be(expectedMasterVersion);
        rule.R8EffectiveDate.Should().Be(new DateOnly(2026, 6, 1));
        rule.FiledTransitionEndRule.Should().Be(
            FiledTransitionExclusiveEndRule.AddYearsExclusive);
        rule.FiledTransitionDurationYears.Should().Be(1);
    }

    [Fact]
    public void R6_band_edition_serves_official_options_1_to_10_until_may_2026()
    {
        var rule = SingleTransitionRule(May2026);

        rule.AllowedAverageWageBandOptions.Should().BeEquivalentTo(
        [
            Numeric(1), Numeric(2), Numeric(3), Numeric(4), Numeric(5),
            Numeric(6), Numeric(7), FiledTransition(), Numeric(9),
            ProductionActivitySupport(),
        ]);
        rule.AllowedOptionsByR8ReformStatus.Keys.Should().BeEquivalentTo(
            [R8ReformStatus.NotApplicableBeforeR8],
            "R8施行前の版に改定対象・対象外の区分群は存在しない（ADR 0023）");
        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.NotApplicableBeforeR8]
            .Should().BeEquivalentTo(rule.AllowedAverageWageBandOptions);
    }

    [Fact]
    public void R8_band_edition_partitions_official_options_by_reform_status_from_june_2026()
    {
        var rule = SingleTransitionRule(June2026);

        rule.AllowedAverageWageBandOptions.Should().HaveCount(22);
        rule.AllowedOptionsByR8ReformStatus.Keys.Should().BeEquivalentTo(
        [
            R8ReformStatus.ReformTarget,
            R8ReformStatus.ReformExempt,
            R8ReformStatus.UnchangedBelow15000,
        ], "施行前状態はR8-06版で使用できず、option 10のR8状態対応は一次資料未確定（open-questions）");

        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformTarget]
            .Should().BeEquivalentTo(
            [
                FiledTransition(),
                Numeric(11), Numeric(12), Numeric(13), Numeric(14), Numeric(15),
                Numeric(16), Numeric(17), Numeric(18), Numeric(19), Numeric(20),
                Numeric(21), Numeric(22),
            ], "改定対象は新12区分option（11〜22）と新規指定FiledTransitionだけを使える");
        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformExempt]
            .Should().BeEquivalentTo(
                [Numeric(1), Numeric(2), Numeric(3), Numeric(4), Numeric(5), Numeric(6)],
                "改定対象外は従前6区分（option 1〜6）を継続する");
        rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.UnchangedBelow15000]
            .Should().BeEquivalentTo(
                [Numeric(7), Numeric(9)],
                "1万5千円未満（option 7・9）は区分境界が変わらない");
    }

    [Fact]
    public void Reform_target_offices_cannot_keep_r6_numeric_band_options_from_june_2026()
    {
        var rule = SingleTransitionRule(June2026);
        var reformTarget = rule.AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformTarget];

        foreach (var r6NumericCode in new[] { 1, 2, 3, 4, 5, 6, 7, 9 })
            reformTarget.Should().NotContain(
                Numeric(r6NumericCode),
                "改定対象がR6数値区分のまま2026-06以降を請求することはフェイルクローズする");
    }

    [Theory]
    [InlineData("claim-master-r6-04")]
    [InlineData("claim-master-r6-06")]
    [InlineData("claim-master-r8-06")]
    public void Profile_policies_resolve_for_each_seeded_band_edition(string masterVersion)
    {
        var action = () => Provider.Resolve(new ClaimMasterVersion(masterVersion));

        action.Should().NotThrow();
    }

    // ---- 境界月の挙動（2026-05 vs 2026-06） -------------------------------------------

    private static ClaimBillingConditionContext Context(
        AverageWageBandOption option, R8ReformStatus status) => new(
        RewardSystem: "employment-continuation-support-b",
        PaymentBand: "",
        CapacityHeadcount: 15,
        StaffingKey: "staff-6-1",
        AverageWageBandOption: option,
        R8ReformStatus: status,
        OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal));

    [Fact]
    public void Basic_reward_rows_continue_unchanged_across_the_r8_boundary()
    {
        // ADR 0027 決定6: 改定対象外向けR6基本報酬行（135行）はR8-06でも無変更で継続する。
        // ADR 0046: R8改定対象向けの新12区分180行が2026-06から加わる（R6行は変えない）。
        var may = Provider.ResolveCalculationMasters(May2026).BasicRewards;
        var june = Provider.ResolveCalculationMasters(June2026).BasicRewards;

        may.Should().HaveCount(135);
        june.Should().HaveCount(135 + 180, "R6の135行を保ったままR8改定対象の180行が加わる");

        // R6の135行は1行も変わらず、1行も消えていない。
        june.Should().Contain(may, "R6基本報酬行は135/135が検証済みの継続対象");
    }

    [Theory]
    // 改定対象外: R6数値option（例: 3 = 3万円以上3万5千円未満）を従前どおり使える
    [InlineData(3, R8ReformStatus.ReformExempt)]
    // 1万5千円未満: option 7・9は区分境界が変わらない
    [InlineData(7, R8ReformStatus.UnchangedBelow15000)]
    [InlineData(9, R8ReformStatus.UnchangedBelow15000)]
    public void Exempt_offices_resolve_the_same_code_and_units_across_the_boundary(
        int officialOptionCode, R8ReformStatus juneStatus)
    {
        var mayMasters = Provider.ResolveCalculationMasters(May2026);
        var juneMasters = Provider.ResolveCalculationMasters(June2026);
        var option = Numeric(officialOptionCode);

        var may = ServiceCodeResolver.ResolveBasicReward(
            mayMasters, May2026, Context(option, R8ReformStatus.NotApplicableBeforeR8));
        var june = ServiceCodeResolver.ResolveBasicReward(
            juneMasters, June2026, Context(option, juneStatus));

        june.ServiceCode.Should().Be(may.ServiceCode);
        june.UnitsPerDay.Should().Be(may.UnitsPerDay);

        // 同じ組合せは経過措置ruleでも許可されている（runtime guardと整合）。
        SingleTransitionRule(June2026)
            .AllowedOptionsByR8ReformStatus[juneStatus].Should().Contain(option);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    public void Reform_target_offices_resolve_every_r8_numeric_band(int officialOptionCode)
    {
        // ADR 0046: 改定対象の新12区分（option 11〜22）はseed投入済みで、2026-06に解決できる。
        var juneMasters = Provider.ResolveCalculationMasters(June2026);

        var resolved = ServiceCodeResolver.ResolveBasicReward(
            juneMasters, June2026,
            Context(Numeric(officialOptionCode), R8ReformStatus.ReformTarget));

        resolved.ServiceCode.Should().NotBeNullOrWhiteSpace();
        resolved.UnitsPerDay.Should().BePositive();

        // 経過措置ruleでも許可されている（runtime guardと整合）。
        SingleTransitionRule(June2026)
            .AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformTarget]
            .Should().Contain(Numeric(officialOptionCode));
    }

    /// <summary>
    /// Fix Round 1 I-1: <c>ClaimCalculatorGoldenCaseTests.Matches_adr_0046_worked_example_reform_target_office_in_june_2026</c>
    /// （golden case 2）がテストファイル内に再掲する基本報酬行（cap-20-or-less × band-48000-plus
    /// (option 11) × staff-6-1 = 837単位/日、サービスコード463340）を、production seedから解決した
    /// 実データで名指しでpinする。<see cref="Reform_target_offices_resolve_every_r8_numeric_band"/>は
    /// <c>UnitsPerDay.Should().BePositive()</c>までしか確認しないため、golden case 2の金額を最も
    /// 支配するこの単位数837自体には同等の防護が無かった（production seed側が837→738等に変わっても、
    /// あるいはgolden case側の再掲が転記ミスをしても、どちらも機械的に検出されない）。
    /// 期待値の唯一の出典はADR 0046決定表（Task 4、サービス費（Ⅰ（６：１））先頭行）である。
    /// </summary>
    [Fact]
    public void Reform_target_option_11_resolves_to_the_service_code_and_units_from_adr_0046()
    {
        var juneMasters = Provider.ResolveCalculationMasters(June2026);

        var resolved = ServiceCodeResolver.ResolveBasicReward(
            juneMasters, June2026,
            Context(Numeric(11), R8ReformStatus.ReformTarget));

        // ADR 0046決定表（Task 4、サービス費（Ⅰ（６：１））先頭行）が唯一の出典。
        resolved.ServiceCode.Should().Be(
            "463340", "golden case 2が再掲する基本報酬行のサービスコード（ADR 0046決定表）");
        resolved.UnitsPerDay.Should().Be(
            837, "golden case 2が再掲する基本報酬行の単位数（ADR 0046決定表）");
    }

    // Task 5 ブリーフは本テストを ServiceCodeResolver.ResolveBasicReward への直接呼び出しで
    // 書いていたが、実装を調査したところ ServiceCodeResolver 自体は
    // AverageWageBandOption と R8ReformStatus の整合性を検査しない（R6行はr8-reform-status
    // 条件を持たないため「制約なし」と評価され、改定対象コンテキストでも普通に解決してしまう
    // ことを実測で確認した）。この整合性は1つ上の層、OfficeClaimProfilePolicy.ValidateHistory
    // （プロファイル登録時点でreform-target×R6数値区分の組合せそのものを拒否する。既存の
    // Profile_policy_rejects_a_reform_target_profile_with_an_r6_numeric_option_at_r8が
    // option 3の1点で実証済み）が担っている。fail-closeを「消す」のではなく、ブリーフが
    // 意図した「消えていないことの確認」を実際に効いている層へ「移す」ため、本テストは
    // 同じ機構をoption 1〜7・9の全8点へ拡張したTheoryとして書き直す。
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    public void Reform_target_offices_still_fail_closed_on_r6_numeric_bands(int r6NumericCode)
    {
        // 新12区分を投入しても、改定対象がR6数値区分を宣言する経路は開かない
        // （プロファイル登録時点でフェイルクローズする）。
        var policy = Provider.Resolve(new ClaimMasterVersion("claim-master-r8-06"));

        var action = () => policy.ValidateHistory([ReformTargetProfile(Numeric(r6NumericCode))]);

        action.Should().Throw<InvalidOperationException>(
            $"改定対象がR6区分option {r6NumericCode}を宣言することはフェイルクローズする");
    }

    [Fact]
    public void Profile_policy_rejects_a_reform_target_profile_with_an_r6_numeric_option_at_r8()
    {
        var policy = Provider.Resolve(new ClaimMasterVersion("claim-master-r8-06"));

        // 改定対象がR6数値区分（option 3）を宣言したprofileは登録できない（フェイルクローズ）。
        var invalid = () => policy.ValidateHistory([ReformTargetProfile(Numeric(3))]);
        invalid.Should().Throw<InvalidOperationException>();

        // 一方で新12区分（option 12）の宣言自体は登録可能である。option 12は
        // service-code側も解決可能（ADR 0046。Reform_target_offices_resolve_every_r8_numeric_band
        // が12ケースを固定）であり、ここが固定するのはprofile登録の可否だけである。
        var valid = () => policy.ValidateHistory([ReformTargetProfile(Numeric(12))]);
        valid.Should().NotThrow();
    }

    [Fact]
    public void Treatment_improvement_additions_switch_generations_at_june_2026()
    {
        // ADR 0045: R6統一処遇改善(Ⅰ)〜(Ⅳ)は2026-05で失効し、2026-06からR8の区分へ入れ替わる。
        // 「R6行が消える」ことと「R8行が現れる」ことの両方を固定する（片方だけでは
        // 沈黙して加算が消える退行を検出できない）。
        var may = Provider.ResolveCalculationMasters(May2026);
        var june = Provider.ResolveCalculationMasters(June2026);

        var mayKeys = may.UnitAdjustments.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        var juneKeys = june.UnitAdjustments.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);

        // R6世代は2026-06に存在しない。
        var r6TreatmentImprovement = mayKeys
            .Where(key => key.Contains("treatment-improvement.unified", StringComparison.Ordinal))
            .ToArray();
        r6TreatmentImprovement.Should().NotBeEmpty("2026-05にはR6統一処遇改善行が存在する");
        r6TreatmentImprovement.Should().OnlyContain(
            key => !juneKeys.Contains(key), "R6統一処遇改善は2026-05で失効する");

        // R8世代が2026-06に存在する。
        var juneTreatmentImprovement = juneKeys
            .Where(key => key.Contains("treatment-improvement.r8", StringComparison.Ordinal))
            .ToArray();
        juneTreatmentImprovement.Should().NotBeEmpty(
            "R8の処遇改善行が入っていなければ、2026-06以降は全事業所で当該加算を算定できない");

        // 対応するサービスコード行も同じ世代交代をする。
        // サービスコード行のkeyは「b-addition.r8-06.treatment-improvement.<区分>」（R6の
        // 「b-addition.r6-06.treatment-improvement.unified.<区分>」と同形）で、世代タグ
        // 「r8-06」はtreatment-improvementの前に置かれる（addition行の
        // 「addition.treatment-improvement.r8.<区分>」とは並び順が異なる）。
        // Fix Round 1 M-5: 述語が additionKey を参照しない「r8-06のservice-code行が1本でも
        // あれば通る」チェックだった（6回同じ主張を繰り返すだけで1対1対応を検証していなかった）。
        // suffixを切り出して個別のキーの存在を主張する。
        const string additionPrefix = "addition.treatment-improvement.r8.";
        var juneServiceCodeKeys = june.ServiceCodes
            .Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var additionKey in juneTreatmentImprovement)
        {
            additionKey.Should().StartWith(additionPrefix,
                $"加算行のkey命名規約（ADR 0045）から外れている: {additionKey}");
            var suffix = additionKey[additionPrefix.Length..];
            var expectedServiceCodeKey = $"b-addition.r8-06.treatment-improvement.{suffix}";
            juneServiceCodeKeys.Should().Contain(
                expectedServiceCodeKey,
                $"加算行 {additionKey} に対応するサービスコード行 {expectedServiceCodeKey} が必要");
        }
    }

    /// <summary>
    /// Fix Round 1 I-2: R8処遇改善6行の率(percentage)をproduction seedから解決した実データで
    /// pinする。ADR 0045の決定表がこの期待値の唯一の出典。既存のClaimMaster検証群は
    /// additions.jsonとservice-codes.json間の構造一致（<c>ValidateAdjustmentComponent</c>）は
    /// 検証するが、値そのものが105/1000か999/1000かは問わない。<c>ClaimCalculatorGoldenCaseTests</c>
    /// はDomain層のテストで、依存方向規律によりInfrastructureのseedを読めないため、production seed
    /// 上の率を直接pinできるのはこのテスト（Infrastructure.Tests）だけである。
    /// </summary>
    [Fact]
    public void R8_treatment_improvement_percentages_match_adr_0045()
    {
        var june = Provider.ResolveCalculationMasters(June2026);

        // ADR 0045決定表（唯一の出典）: (Ⅰ)イ0.105/(Ⅰ)ロ0.109/(Ⅱ)イ0.103/(Ⅱ)ロ0.107/(Ⅲ)0.088/(Ⅳ)0.074。
        var expectedPercentages = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["addition.treatment-improvement.r8.i-i"] = 0.105m,
            ["addition.treatment-improvement.r8.i-ro"] = 0.109m,
            ["addition.treatment-improvement.r8.ii-i"] = 0.103m,
            ["addition.treatment-improvement.r8.ii-ro"] = 0.107m,
            ["addition.treatment-improvement.r8.iii"] = 0.088m,
            ["addition.treatment-improvement.r8.iv"] = 0.074m,
        };

        var actualPercentages = june.UnitAdjustments
            .Where(row => expectedPercentages.ContainsKey(row.Key))
            .ToDictionary(
                row => row.Key,
                row => row.Amount switch
                {
                    PercentageOfTargetAmount percentage => percentage.Percentage,
                    _ => throw new InvalidOperationException(
                        $"{row.Key} はpercentage-of-target形式ではない"),
                },
                StringComparer.Ordinal);

        actualPercentages.Keys.Should().BeEquivalentTo(
            expectedPercentages.Keys,
            "ADR 0045の6区分すべてがseedされていなければならない");
        foreach (var (key, expected) in expectedPercentages)
            actualPercentages[key].Should().Be(
                expected, $"{key} の率はADR 0045決定表の値と一致しなければならない");
    }

    /// <summary>
    /// Task 6（ADR 0044・AC3-4-4）: 地域単価・負担上限はR8出典に裏付けられて2026-06でも
    /// **R6行のまま無変更で継続する**（Task 1の分岐(a)。分岐(c)＝確定できず閉じる、を
    /// 採らなかったことを固定する）。これが空/未解決のまま2026-06の請求を通すと、給付単位数は
    /// 算定できても総費用額・利用者負担額が確定できず、静かな誤請求（0円扱い等）に繋がる。
    /// Fix Round 1 I-2: 当初は<c>NotBeEmpty</c>だけだったが、これでは7級地のうち6級地を閉じても
    /// 単価を書き換えても通ってしまい、ADR 0044の決定内容（「非空」ではなく「継続」）を弱くしか
    /// 固定できていなかった。<see cref="Basic_reward_rows_continue_unchanged_across_the_r8_boundary"/>
    /// と同じ形（may/juneの行集合が完全一致）へ強化する。
    /// Fix Wave I-1（最終レビュー）: may/juneの等価性だけでは、両方が同じ誤った値へ書き換わっても
    /// 検出できない（値そのものを問わない）。ADR 0044「決定表（seed実値。これが値の唯一の出典）」
    /// （地域単価8件・負担上限4件）から**ADR本文を読んで書き写した**数値で、2026-06に解決される
    /// 実値そのものをpinする（seedを読んでseedと突き合わせる空転を避けるため、配列はADRの表から
    /// 独立に作成した）。
    /// </summary>
    [Fact]
    public void Region_unit_prices_and_burden_caps_resolve_in_june_2026()
    {
        // ADR 0044: 地域単価・負担上限は2026-06もR6行のまま無変更で継続する。
        var may = Provider.ResolveCalculationMasters(May2026);
        var june = Provider.ResolveCalculationMasters(June2026);

        june.RegionUnitPrices.Should().BeEquivalentTo(
            may.RegionUnitPrices,
            "地域単価はADR 0044によりR8-06でも無変更で継続する（新規行の追加・既存行の変更いずれも無い）");
        june.BurdenCaps.Should().BeEquivalentTo(
            may.BurdenCaps,
            "負担上限はADR 0044によりR8-06でも無変更で継続する（新規行の追加・既存行の変更いずれも無い）");

        // ADR 0044「決定表」（region-unit-prices.json）: 級地ごとの単価（円）。
        var expectedRegionUnitPricesYen = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["region-grade-1"] = 11.14m,
            ["region-grade-2"] = 10.91m,
            ["region-grade-3"] = 10.86m,
            ["region-grade-4"] = 10.68m,
            ["region-grade-5"] = 10.57m,
            ["region-grade-6"] = 10.34m,
            ["region-grade-7"] = 10.17m,
            ["region-other"] = 10.00m,
        };
        var actualRegionUnitPricesYen = june.RegionUnitPrices.ToDictionary(
            row => row.RegionKey, row => row.UnitPriceYen, StringComparer.Ordinal);
        actualRegionUnitPricesYen.Keys.Should().BeEquivalentTo(
            expectedRegionUnitPricesYen.Keys,
            "ADR 0044決定表の8級地すべてがseedされていなければならない");
        foreach (var (regionKey, expected) in expectedRegionUnitPricesYen)
            actualRegionUnitPricesYen[regionKey].Should().Be(
                expected, $"{regionKey} の単価はADR 0044決定表の値と一致しなければならない");

        // ADR 0044「決定表」（burden-caps.json）: 負担区分ごとの上限額（円）。
        var expectedBurdenCapsYen = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["welfare"] = 0,
            ["low-income"] = 0,
            ["general-1"] = 9300,
            ["general-2"] = 37200,
        };
        var actualBurdenCapsYen = june.BurdenCaps.ToDictionary(
            row => row.BurdenCategory, row => row.CapYen, StringComparer.Ordinal);
        actualBurdenCapsYen.Keys.Should().BeEquivalentTo(
            expectedBurdenCapsYen.Keys,
            "ADR 0044決定表の4区分すべてがseedされていなければならない");
        foreach (var (burdenCategory, expected) in expectedBurdenCapsYen)
            actualBurdenCapsYen[burdenCategory].Should().Be(
                expected, $"{burdenCategory} の上限額はADR 0044決定表の値と一致しなければならない");
    }

    /// <summary>
    /// Fix Wave I-1（最終レビュー）: R8改定対象の新12区分180行のうち、production seed経由で
    /// 名指しで値をpinしていたのは
    /// <see cref="Reform_target_option_11_resolves_to_the_service_code_and_units_from_adr_0046"/>
    /// が固定する463340/837の1行だけだった。<see cref="Reform_target_offices_resolve_every_r8_numeric_band"/>
    /// は<c>UnitsPerDay.Should().BePositive()</c>までしか見ないため、残り179行の単位数は
    /// production seedを書き換えても全テストが緑のままだった。
    ///
    /// 15系列（定員5×人員配置3）それぞれについて、12区分（option 11〜22。band-48000-plus→
    /// band-15000-18000の順＝ADR 0046決定表の列順）の単位数を配列でpinする。期待値の唯一の出典は
    /// ADR 0046「決定表（180行。これが値の唯一の出典）」（Task 4節、サービス費（Ⅰ（６：１））・
    /// （Ⅱ（７．５：１））・（Ⅲ（１０：１）の3表）であり、**ADR本文を読んで書き写した**
    /// （production seedから生成した配列ではない。空転防止）。
    ///
    /// 180個をハッシュ1本へ畳み込む方式（案B）も検討したが、失敗時にどの系列のどの区分が
    /// 壊れたか分からず診断性が低いため不採用。15配列に分ける方式（案A）は、テーマ引数
    /// （capacityKey/staffingKey）と配列indexだけで壊れた箇所が一意に特定できる。
    /// </summary>
    [Theory]
    [MemberData(nameof(ExpectedR8BasicRewardUnitsBySeries))]
    public void R8_basic_reward_units_match_adr_0046_decision_table(
        string capacityKey, string staffingKey, int[] expectedUnitsInOptionOrder)
    {
        var june = Provider.ResolveCalculationMasters(June2026);

        var actualUnitsInOptionOrder = BandsInOfficialOptionOrder
            .Select(band => june.BasicRewards.Single(row =>
                    row.EffectiveFrom == June2026
                    && row.CapacityKey == capacityKey
                    && row.StaffingKey == staffingKey
                    && row.PaymentBand == band)
                .BaseUnits)
            .ToArray();

        actualUnitsInOptionOrder.Should().Equal(
            expectedUnitsInOptionOrder,
            $"{capacityKey}/{staffingKey} の12区分（option 11〜22）の単位数はADR 0046決定表と一致しなければならない");
    }

    // option 11(band-48000-plus)→22(band-15000-18000)の順。ADR 0046決定表の列順と同じ
    // （このADR自身がキー命名をADR 0027決定1の`band-<下限>-<上限>`規約に従うと定めている）。
    private static readonly string[] BandsInOfficialOptionOrder =
    [
        "band-48000-plus", "band-45000-48000", "band-38000-45000", "band-35000-38000",
        "band-33000-35000", "band-30000-33000", "band-28000-30000", "band-25000-28000",
        "band-23000-25000", "band-20000-23000", "band-18000-20000", "band-15000-18000",
    ];

    // ADR 0046「決定表（180行。これが値の唯一の出典）」Task 4節の3表（サービス費 Ⅰ/Ⅱ/Ⅲ）を
    // 目視で書き写した15系列（capacityKey × staffingKey）× 12区分（option 11〜22の単位数）。
    public static IEnumerable<object[]> ExpectedR8BasicRewardUnitsBySeries()
    {
        // サービス費（Ⅰ（６：１））
        yield return
        [
            "cap-20-or-less", "staff-6-1",
            new[] { 837, 812, 805, 781, 758, 738, 738, 726, 726, 705, 703, 682 },
        ];
        yield return
        [
            "cap-21-40", "staff-6-1",
            new[] { 746, 724, 717, 696, 676, 660, 660, 641, 637, 624, 624, 606 },
        ];
        yield return
        [
            "cap-41-60", "staff-6-1",
            new[] { 700, 679, 674, 654, 636, 620, 620, 602, 600, 586, 586, 569 },
        ];
        yield return
        [
            "cap-61-80", "staff-6-1",
            new[] { 688, 668, 662, 643, 625, 609, 609, 591, 589, 575, 575, 558 },
        ];
        yield return
        [
            "cap-81-plus", "staff-6-1",
            new[] { 666, 647, 640, 621, 605, 590, 590, 573, 570, 557, 557, 541 },
        ];

        // サービス費（Ⅱ（７．５：１））
        yield return
        [
            "cap-20-or-less", "staff-7.5-1",
            new[] { 748, 726, 716, 695, 669, 649, 649, 637, 637, 618, 614, 596 },
        ];
        yield return
        [
            "cap-21-40", "staff-7.5-1",
            new[] { 666, 647, 637, 618, 596, 580, 580, 563, 557, 544, 544, 528 },
        ];
        yield return
        [
            "cap-41-60", "staff-7.5-1",
            new[] { 625, 607, 599, 582, 561, 545, 545, 529, 525, 511, 511, 496 },
        ];
        yield return
        [
            "cap-61-80", "staff-7.5-1",
            new[] { 614, 596, 588, 571, 551, 535, 535, 519, 515, 501, 501, 486 },
        ];
        yield return
        [
            "cap-81-plus", "staff-7.5-1",
            new[] { 594, 577, 568, 551, 533, 518, 518, 503, 498, 485, 485, 471 },
        ];

        // サービス費（Ⅲ（１０：１）
        yield return
        [
            "cap-20-or-less", "staff-10-1",
            new[] { 682, 662, 653, 634, 611, 594, 594, 577, 572, 557, 557, 541 },
        ];
        yield return
        [
            "cap-21-40", "staff-10-1",
            new[] { 609, 591, 584, 567, 547, 532, 532, 517, 511, 497, 497, 483 },
        ];
        yield return
        [
            "cap-41-60", "staff-10-1",
            new[] { 564, 548, 541, 525, 508, 493, 493, 479, 474, 461, 461, 448 },
        ];
        yield return
        [
            "cap-61-80", "staff-10-1",
            new[] { 554, 538, 530, 515, 498, 484, 483, 469, 465, 452, 452, 439 },
        ];
        yield return
        [
            "cap-81-plus", "staff-10-1",
            new[] { 535, 519, 512, 497, 480, 467, 467, 453, 449, 437, 437, 424 },
        ];
    }

    private static OfficeClaimProfile ReformTargetProfile(AverageWageBandOption option)
    {
        var id = Guid.NewGuid();
        return new OfficeClaimProfile
        {
            Id = id,
            OfficeId = Guid.NewGuid(),
            EffectiveFrom = new DateOnly(2026, 6, 1),
            EffectiveTo = null,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            MasterVersion = new ClaimMasterVersion("claim-master-r8-06"),
            ReformStatus = R8ReformStatus.ReformTarget,
            AverageWageBandOption = option,
            EvidenceDocumentId = "profile-doc",
            ConfirmedAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            ConfirmedBy = "admin",
            ConfirmationReason = "台帳確認",
            CreatedAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
