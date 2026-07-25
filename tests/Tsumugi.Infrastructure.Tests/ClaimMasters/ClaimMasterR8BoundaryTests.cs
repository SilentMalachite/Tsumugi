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
        var may = Provider.ResolveCalculationMasters(May2026).BasicRewards;
        var june = Provider.ResolveCalculationMasters(June2026).BasicRewards;

        may.Should().HaveCount(135);
        june.Should().BeEquivalentTo(may, "R6基本報酬行は135/135が検証済みの継続対象");
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

    [Fact]
    public void Reform_target_r8_numeric_options_fail_explicitly_until_their_rows_land()
    {
        // ADR 0027はR8改定対象の新12区分（option 11〜22）のservice-code実値を確定していない
        // （seed投入なし。docs/open-questions.md）。改定対象が新区分で2026-06を請求しようと
        // すると、暗黙のR6単価にフォールバックせず明示的に失敗する。
        var juneMasters = Provider.ResolveCalculationMasters(June2026);

        var action = () => ServiceCodeResolver.ResolveBasicReward(
            juneMasters, June2026, Context(Numeric(12), R8ReformStatus.ReformTarget));

        action.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.MasterUnavailable);
    }

    [Fact]
    public void Profile_policy_rejects_a_reform_target_profile_with_an_r6_numeric_option_at_r8()
    {
        var policy = Provider.Resolve(new ClaimMasterVersion("claim-master-r8-06"));

        // 改定対象がR6数値区分（option 3）を宣言したprofileは登録できない（フェイルクローズ）。
        var invalid = () => policy.ValidateHistory([ReformTargetProfile(Numeric(3))]);
        invalid.Should().Throw<InvalidOperationException>();

        // 一方で新12区分（option 12）の宣言自体は登録可能であり、請求は上の
        // service-code未実装の明示的失敗で停止する（暗黙請求は成立しない）。
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
