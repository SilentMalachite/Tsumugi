using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// spec §6.1 の第2の副条件を機械判定へ格上げする常設アサーション。
/// </summary>
/// <remarks>
/// ADR 0049 の存在検査（<c>OfficeCapabilityCoveragePolicy.FindUncoveredKeys</c>）は
/// 「条件定義が当月に有効か」だけを見ており、「有効な条件定義を参照する service-code 行が
/// 当月に1件も無い」場合（＝宣言しても算定に効かない穴）を拾わない。本ブランチでは
/// 現行seedに該当が **0件**（全32条件定義）であることを人手で確認して逸脱を受け入れたが、
/// **それが崩れたときに何もfail-closeしない**という状態が残っていた。ここで seed 側の
/// 不変条件として固定し、将来のseed追加が黙って前提を壊せないようにする。
/// </remarks>
public sealed class ClaimMasterCapabilityCoverageTests
{
    private static readonly JsonClaimMasterProvider Provider = JsonClaimMasterProvider.LoadEmbedded();

    // R6-06世代の開始前から、R8-06世代の十分先まで。版が無い月は自然にスキップされる。
    private const int FirstYear = 2024;
    private const int LastYear = 2030;

    [Fact]
    public void Every_effective_office_capability_condition_is_referenced_by_an_effective_service_code_row()
    {
        var violations = new List<string>();
        var scannedMonths = 0;
        var inspectedConditions = 0;

        foreach (var month in MonthsToScan())
        {
            ClaimCalculationMasterBundle masters;
            try
            {
                masters = Provider.ResolveCalculationMasters(month);
            }
            catch (ClaimMasterPolicyUnavailableException)
            {
                continue; // 版が存在しない月は検査対象外
            }

            scannedMonths++;
            inspectedConditions += masters.ConditionDefinitions
                .Count(condition => condition.Kind == ClaimConditionKind.OfficeCapability);
            violations.AddRange(UnreferencedCapabilityConditionKeys(masters)
                .Select(key => $"{month.Year:D4}-{month.Month:D2} {key}"));
        }

        // 走査そのものが空振りしていないこと（版解決の変更でこのテストが無言で
        // 真空になるのを防ぐ）。
        scannedMonths.Should().BeGreaterThan(24, "R6-06とR8-06の両世代を跨いで走査する");
        inspectedConditions.Should().BeGreaterThan(0, "検査対象の体制届条件が1件も無い状態は異常");

        violations.Should().BeEmpty(
            "office-capability 条件定義が有効な月には、それを参照する service-code 行も"
            + "有効でなければならない（そうでないと体制届で宣言しても算定に効かず、"
            + "ADR 0049 の存在検査も警告しない無音の穴になる。spec §6.1 条件2）");
    }

    /// <summary>
    /// 歯の確認: 同じ判定関数が、条件定義だけがあって参照行が無い状態を実際に検出する。
    /// production seed 側は現時点で違反ゼロのため、上のテストだけでは判定関数が
    /// 常に空を返す実装であっても緑になってしまう。
    /// </summary>
    [Fact]
    public void The_check_detects_a_capability_condition_that_no_service_code_row_references()
    {
        var masters = new ClaimCalculationMasterBundle(
            BasicRewards: [],
            UnitAdjustments: [],
            RegionUnitPrices: [],
            BurdenCaps: [],
            TransitionRules: [],
            ServiceCodes: [],
            ConditionDefinitions:
            [
                new ClaimConditionDefinition(
                    "cond-orphan",
                    new ServiceMonth(2024, 6),
                    null,
                    ClaimConditionKind.OfficeCapability,
                    ClaimConditionOperator.Equals,
                    new ClaimConditionTokenOperand("mhlw.b46.capability.orphan.1"),
                    [
                        new ClaimSourceRef(
                            "doc-1",
                            new string('0', 64),
                            "loc",
                            ClaimSourceEvidenceRole.Authoritative,
                            [ClaimSourceSupport.Conditions]),
                    ]),
            ]);

        UnreferencedCapabilityConditionKeys(masters).Should().Equal("cond-orphan");
    }

    private static string[] UnreferencedCapabilityConditionKeys(ClaimCalculationMasterBundle masters)
    {
        var referenced = new HashSet<string>(
            masters.ServiceCodes.SelectMany(row => row.ConditionSelectors), StringComparer.Ordinal);

        return masters.ConditionDefinitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .Select(condition => condition.Key)
            .Where(key => !referenced.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<ServiceMonth> MonthsToScan()
    {
        for (var year = FirstYear; year <= LastYear; year++)
        {
            for (var month = 1; month <= 12; month++)
                yield return new ServiceMonth(year, month);
        }
    }
}
