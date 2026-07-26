using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// ADR 0049: 宣言された体制届optionに対応する有効なマスタ行が当月に存在しない場合を
/// 検出する。無音で加算0円になる経路を可視化するための警告であり、確定は止めない。
/// </summary>
public sealed class OfficeCapabilityCoveragePolicyTests
{
    /// <summary>
    /// 当月に有効な条件定義が宣言キーを覆っていれば警告しない。
    /// </summary>
    [Fact]
    public void A_declared_key_covered_by_the_month_is_not_reported()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.2"],
            monthConditionValues: ["mhlw.b46.capability.treatment-improvement.2"],
            allConditionValues: ["mhlw.b46.capability.treatment-improvement.2"]);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// 他の期間では使われているのに当月に無いキーは「失効した／まだ施行されていない」
    /// 本当の穴であり、警告する。処遇改善(Ⅴ)を2025-04以降も届け出たままの事業所がこれ。
    /// </summary>
    [Fact]
    public void A_key_used_in_other_periods_but_not_this_month_is_reported()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.6"],
            monthConditionValues: ["mhlw.b46.capability.treatment-improvement.2"],
            allConditionValues:
            [
                "mhlw.b46.capability.treatment-improvement.2",
                "mhlw.b46.capability.treatment-improvement.6",
            ]);

        result.Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.6");
    }

    /// <summary>
    /// どの期間の条件定義からも参照されていないキーは請求に効かない体制届項目であり、
    /// 警告しない。ここを警告にすると、算定に関与しない項目で毎月ノイズが出る。
    /// </summary>
    [Fact]
    public void A_key_never_used_by_any_condition_is_ignored()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mealProvision"],
            monthConditionValues: ["mhlw.b46.capability.treatment-improvement.2"],
            allConditionValues: ["mhlw.b46.capability.treatment-improvement.2"]);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// 結果は決定論（順序が安定）でなければならない。警告の並びが呼び出しごとに
    /// 変わると、確定snapshotやUI表示の差分が無意味に揺れる。
    /// </summary>
    [Fact]
    public void The_result_is_ordered_deterministically()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["b.key", "a.key"],
            monthConditionValues: [],
            allConditionValues: ["a.key", "b.key"]);

        result.Should().Equal("a.key", "b.key");
    }
}
