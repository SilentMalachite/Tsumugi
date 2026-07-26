using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

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

    private static ClaimSourceRef SourceRef() => new(
        "doc-1",
        "0000000000000000000000000000000000000000000000000000000000000000",
        "loc",
        ClaimSourceEvidenceRole.Authoritative,
        [ClaimSourceSupport.Conditions]);

    private static ClaimConditionDefinition Condition(
        string key, ClaimConditionKind kind, ClaimConditionOperand operand) => new(
        key, new ServiceMonth(2024, 4), null, kind, ClaimConditionOperator.Equals, operand, [SourceRef()]);

    /// <summary>
    /// <c>kind: office-capability</c>以外の条件定義（例: staffing）は対象外。
    /// 月側・全期間側どちらの抽出にもkind比較を効かせる必要がある（レビューImportant 1）。
    /// </summary>
    [Fact]
    public void ExtractCapabilityValues_ignores_conditions_of_other_kinds()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValues(
        [
            Condition("cond-staff", ClaimConditionKind.Staffing, new ClaimConditionTokenOperand("staff-a")),
        ]);

        result.Should().BeEmpty();
    }

    /// <summary>token operand（単一値）はその1値だけを返す。</summary>
    [Fact]
    public void ExtractCapabilityValues_extracts_a_single_value_from_a_token_operand()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValues(
        [
            Condition(
                "cond-cap", ClaimConditionKind.OfficeCapability,
                new ClaimConditionTokenOperand("mhlw.b46.capability.treatment-improvement.2")),
        ]);

        result.Should().Equal("mhlw.b46.capability.treatment-improvement.2");
    }

    /// <summary>token set operand（複数値）は全値を返す（In条件が束ねる複数option）。</summary>
    [Fact]
    public void ExtractCapabilityValues_extracts_every_value_from_a_token_set_operand()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValues(
        [
            Condition(
                "cond-cap-set", ClaimConditionKind.OfficeCapability,
                new ClaimConditionTokenSetOperand(
                [
                    "mhlw.b46.capability.treatment-improvement.2",
                    "mhlw.b46.capability.treatment-improvement.4",
                ])),
        ]);

        result.Should().BeEquivalentTo(
        [
            "mhlw.b46.capability.treatment-improvement.2",
            "mhlw.b46.capability.treatment-improvement.4",
        ]);
    }

    /// <summary>
    /// token・token set以外のoperand（例: 整数条件）はkindがoffice-capabilityでも値を持たない
    /// ため対象外。将来operand型が増えても、ここに来ない限り無音で取りこぼす契約を明示する。
    /// </summary>
    [Fact]
    public void ExtractCapabilityValues_ignores_operands_that_are_not_token_shaped()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValues(
        [
            Condition(
                "cond-cap-int", ClaimConditionKind.OfficeCapability, new ClaimConditionIntegerOperand(1)),
        ]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractCapabilityValues_rejects_null_input()
    {
        var act = () => OfficeCapabilityCoveragePolicy.ExtractCapabilityValues(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static HashSet<string> Set(params string[] values) =>
        new(values, StringComparer.Ordinal);

    // --- FindUnsatisfiableDeclaredKeys（本タスクの一般化。ADR 0049の隣にある別の穴） ---

    /// <summary>
    /// 宣言キーを要求する行が1つでもあり、その行の他の条件（capability種別に限る）すべてが
    /// 宣言集合と重なるなら充足可能であり、報告しない。単一条件行（他のキーを要求しない）は
    /// 常にこれに当たる。
    /// </summary>
    [Fact]
    public void A_declared_key_with_a_satisfiable_row_is_not_reported()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> rows =
        [
            [Set("mhlw.b46.capability.treatment-improvement.2")],
        ];

        var result = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.2"],
            monthCapabilityRows: rows);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// 処遇改善(Ⅴ)の実例（2024-06〜2025-03）: option 6 は当月に有効だが、それを要求する行は
    /// すべてband（第2のcapabilityキー）も要求する。band を宣言していなければ、その行は
    /// 1つも充足できず、加算は無音で0円になる。これが本タスクの検出対象。
    /// </summary>
    [Fact]
    public void A_declared_key_whose_only_referencing_row_also_requires_an_undeclared_companion_key_is_reported()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> rows =
        [
            [
                Set("mhlw.b46.capability.treatment-improvement.6"),
                Set("mhlw.b46.capability.treatment-improvement-v-band.3"),
            ],
        ];

        var result = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.6"],
            monthCapabilityRows: rows);

        result.Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.6");
    }

    /// <summary>companionキーも宣言すれば、同じ行が充足可能になり報告されなくなる。</summary>
    [Fact]
    public void Declaring_the_companion_key_makes_the_row_satisfiable()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> rows =
        [
            [
                Set("mhlw.b46.capability.treatment-improvement.6"),
                Set("mhlw.b46.capability.treatment-improvement-v-band.3"),
            ],
        ];

        var result = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
            declaredKeys:
            [
                "mhlw.b46.capability.treatment-improvement.6",
                "mhlw.b46.capability.treatment-improvement-v-band.3",
            ],
            monthCapabilityRows: rows);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// 当月のどの行のcapability条件にも現れないキーは対象外（FindUncoveredKeysの領分、
    /// または請求に効かないキー）。ここを報告すると、算定に関与しないキーで毎月ノイズが出る。
    /// </summary>
    [Fact]
    public void A_key_not_referenced_by_any_row_this_month_is_out_of_scope()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> rows =
        [
            [Set("mhlw.b46.capability.treatment-improvement.2")],
        ];

        var result = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
            declaredKeys: ["mealProvision"],
            monthCapabilityRows: rows);

        result.Should().BeEmpty();
    }

    /// <summary>in演算子の代替値のどれか1つを宣言していれば、その条件は充足する。</summary>
    [Fact]
    public void Declaring_any_alternative_value_of_an_in_condition_satisfies_it()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> rows =
        [
            [
                Set("mhlw.b46.capability.treatment-improvement.6"),
                Set(
                    "mhlw.b46.capability.treatment-improvement-v-band.1",
                    "mhlw.b46.capability.treatment-improvement-v-band.2",
                    "mhlw.b46.capability.treatment-improvement-v-band.3"),
            ],
        ];

        var result = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
            declaredKeys:
            [
                "mhlw.b46.capability.treatment-improvement.6",
                "mhlw.b46.capability.treatment-improvement-v-band.2",
            ],
            monthCapabilityRows: rows);

        result.Should().BeEmpty();
    }

    /// <summary>結果は決定論（順序が安定）でなければならない。</summary>
    [Fact]
    public void FindUnsatisfiableDeclaredKeys_orders_results_deterministically()
    {
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> rows =
        [
            [Set("b.key"), Set("undeclared-companion")],
            [Set("a.key"), Set("undeclared-companion")],
        ];

        var result = OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(
            declaredKeys: ["b.key", "a.key"],
            monthCapabilityRows: rows);

        result.Should().Equal("a.key", "b.key");
    }

    [Fact]
    public void FindUnsatisfiableDeclaredKeys_rejects_null_declared_keys()
    {
        var act = () => OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FindUnsatisfiableDeclaredKeys_rejects_null_rows()
    {
        var act = () => OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys([], null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // --- ExtractCapabilityValueSets（平坦化しない経路。既存のExtractCapabilityValuesの隣） ---

    /// <summary>
    /// office-capability以外の条件（例: facility-classification）は除外する。実seedの
    /// 処遇改善(Ⅰ)行はcapability条件とfacility-classification条件を同じ行に持つため、
    /// ここを誤るとfacility条件が偽陽性の原因になる（brief記載の465120/465138相当）。
    /// </summary>
    [Fact]
    public void ExtractCapabilityValueSets_ignores_conditions_of_other_kinds()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets(
        [
            Condition(
                "cond-facility", ClaimConditionKind.FacilityClassification,
                new ClaimConditionTokenOperand("general")),
            Condition(
                "cond-cap", ClaimConditionKind.OfficeCapability,
                new ClaimConditionTokenOperand("mhlw.b46.capability.treatment-improvement.2")),
        ]);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(Set("mhlw.b46.capability.treatment-improvement.2"));
    }

    /// <summary>token operand（単一値）は1要素集合になる。</summary>
    [Fact]
    public void ExtractCapabilityValueSets_extracts_a_single_value_set_from_a_token_operand()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets(
        [
            Condition(
                "cond-cap", ClaimConditionKind.OfficeCapability,
                new ClaimConditionTokenOperand("mhlw.b46.capability.treatment-improvement.6")),
        ]);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(Set("mhlw.b46.capability.treatment-improvement.6"));
    }

    /// <summary>token set operand（in条件）は複数値集合になる。</summary>
    [Fact]
    public void ExtractCapabilityValueSets_extracts_a_multi_value_set_from_a_token_set_operand()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets(
        [
            Condition(
                "cond-cap-set", ClaimConditionKind.OfficeCapability,
                new ClaimConditionTokenSetOperand(
                [
                    "mhlw.b46.capability.treatment-improvement-v-band.1",
                    "mhlw.b46.capability.treatment-improvement-v-band.2",
                ])),
        ]);

        result.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(Set(
                "mhlw.b46.capability.treatment-improvement-v-band.1",
                "mhlw.b46.capability.treatment-improvement-v-band.2"));
    }

    /// <summary>token・token set以外のoperandは対象外（ExtractCapabilityValuesと同じ契約）。</summary>
    [Fact]
    public void ExtractCapabilityValueSets_ignores_operands_that_are_not_token_shaped()
    {
        var result = OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets(
        [
            Condition(
                "cond-cap-int", ClaimConditionKind.OfficeCapability, new ClaimConditionIntegerOperand(1)),
        ]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractCapabilityValueSets_rejects_null_input()
    {
        var act = () => OfficeCapabilityCoveragePolicy.ExtractCapabilityValueSets(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
