using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

public enum ServiceCodeResolutionErrorCode
{
    MasterUnavailable = 1,
    AmbiguousMatch = 2,
    ConditionUnresolved = 3,
    ComponentMissing = 4,
    UnsupportedUnitRule = 5,

    /// <summary>
    /// service-codes側の<see cref="UnitAdditionRule"/>とadditions側の
    /// <see cref="UnitAdjustmentMasterRow"/>で amount / roundingRuleId / billingUnit が
    /// 一致しない（マスタ間の食い違い。フェイルクローズ）。
    /// </summary>
    ComponentMismatch = 6,
}

public sealed class ServiceCodeResolutionException(ServiceCodeResolutionErrorCode code)
    : Exception($"Service code resolution failed: {code}.")
{
    public ServiceCodeResolutionErrorCode Code { get; } = code;
}

/// <param name="Selectors">
/// 解決された基本報酬service-code行の<see cref="ServiceCodeMasterRow.Selectors"/>。割合加算の
/// targetSelector（ADR 0025のsource row契約）は「このトークンをSelectorsに含む行」の単位合計を
/// 基底とするため、行帰属の判定材料として保持する。<c>null</c>は空集合と同義（旧呼び出し互換）。
/// </param>
public sealed record ResolvedBasicReward(
    string ServiceCode,
    string OfficialLabel,
    int UnitsPerDay,
    BillingUnit BillingUnit,
    IReadOnlyList<string>? Selectors = null);

/// <summary>
/// 体制・算定条件を満たした加算service-code行（1行=1加算コード）。<see cref="Amount"/>は
/// additions側マスタ行（<see cref="UnitAdjustmentMasterRow"/>）の値（service-codes側ruleとの
/// 一致は<see cref="ServiceCodeResolver.ResolveAdditions"/>が検証済み）。
/// </summary>
public sealed record ResolvedUnitAddition(
    string ServiceCode,
    string OfficialLabel,
    IReadOnlyList<string> Selectors,
    string AdjustmentComponentKey,
    UnitAdjustmentAmount Amount,
    string? RoundingRuleId,
    BillingUnit BillingUnit);

/// <summary>
/// 事業所の算定条件から、適用される基本報酬のサービスコードを純粋関数で解決する。
/// 定員は<see cref="ClaimBillingConditionContext.CapacityHeadcount"/>の頭数で受け取り、
/// 定員区分（cap-20-or-less等）への変換はマスタの閾値条件（<see cref="ClaimConditionOperator"/>の
/// 比較演算子）に委ねる。呼び出し側・本関数のいずれにも定員区分の閾値をハードコードしない。
/// </summary>
public static class ServiceCodeResolver
{
    public static ResolvedBasicReward ResolveBasicReward(
        ClaimCalculationMasterBundle masters, ServiceMonth month, ClaimBillingConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(masters);
        ArgumentNullException.ThrowIfNull(context);

        var candidates = masters.ServiceCodes
            .Where(row => row.ComponentRefs.Any(c =>
                c.MasterKind == ClaimComponentMasterKind.BasicRewards && c.Role == ClaimComponentRole.Base))
            .Where(row => MatchesAll(row, masters, context))
            .ToArray();

        if (candidates.Length == 0)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.MasterUnavailable);
        if (candidates.Length > 1)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.AmbiguousMatch);

        var row = candidates[0];
        if (row.UnitRule is not BaseComponentPassThroughRule passThrough)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.UnsupportedUnitRule);

        var baseMatches = masters.BasicRewards
            .Where(b => b.Key == passThrough.BaseComponentKey)
            .Take(2)
            .ToArray();

        var baseRow = baseMatches.Length switch
        {
            0 => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ComponentMissing),
            1 => baseMatches[0],
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.AmbiguousMatch),
        };

        return new ResolvedBasicReward(
            row.ServiceCode, row.OfficialLabel, baseRow.BaseUnits, row.UnitRule.BillingUnit, row.Selectors);
    }

    /// <summary>
    /// 体制・算定条件（<see cref="ServiceCodeMasterRow.ConditionSelectors"/>）を満たす加算行
    /// （<see cref="UnitAdditionRule"/>）をすべて解決する。条件を満たさない行は「算定しない」
    /// （正しい請求挙動）として単に除外する。additions側マスタ行との参照・値の食い違いは
    /// フェイルクローズ（<see cref="ServiceCodeResolutionErrorCode.ComponentMissing"/> /
    /// <see cref="ServiceCodeResolutionErrorCode.ComponentMismatch"/>）。
    /// 結果はサービスコードの序数昇順で決定的に返す。
    /// </summary>
    public static IReadOnlyList<ResolvedUnitAddition> ResolveAdditions(
        ClaimCalculationMasterBundle masters, ServiceMonth month, ClaimBillingConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(masters);
        ArgumentNullException.ThrowIfNull(context);
        _ = month.ToInt();

        return masters.ServiceCodes
            .Where(row => row.UnitRule is UnitAdditionRule)
            .Where(row => MatchesAll(row, masters, context))
            .Select(row => ResolveAddition(masters, row, (UnitAdditionRule)row.UnitRule))
            .OrderBy(addition => addition.ServiceCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static ResolvedUnitAddition ResolveAddition(
        ClaimCalculationMasterBundle masters, ServiceCodeMasterRow row, UnitAdditionRule rule)
    {
        var matchingRefs = row.ComponentRefs.Where(component =>
                component.MasterKind == ClaimComponentMasterKind.Additions
                && component.Role == ClaimComponentRole.Adjustment
                && component.Key == rule.AdjustmentComponentKey)
            .ToArray();
        if (matchingRefs.Length != 1)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ComponentMissing);

        var adjustmentRows = masters.UnitAdjustments
            .Where(adjustment => adjustment.Key == rule.AdjustmentComponentKey)
            .Take(2)
            .ToArray();
        var adjustmentRow = adjustmentRows.Length switch
        {
            0 => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ComponentMissing),
            1 => adjustmentRows[0],
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.AmbiguousMatch),
        };

        // additions側マスタ行が値の正本（基本報酬のBaseComponentPassThroughと同型）。service-codes側
        // ruleの複製値と食い違う場合はどちらも採用せずフェイルクローズする。
        if (adjustmentRow.Amount != rule.Amount
            || adjustmentRow.RoundingRuleId != rule.RoundingRuleId
            || adjustmentRow.BillingUnit != rule.BillingUnit)
        {
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ComponentMismatch);
        }

        return new ResolvedUnitAddition(
            row.ServiceCode,
            row.OfficialLabel,
            row.Selectors,
            rule.AdjustmentComponentKey,
            adjustmentRow.Amount,
            adjustmentRow.RoundingRuleId,
            adjustmentRow.BillingUnit);
    }

    private static bool MatchesAll(
        ServiceCodeMasterRow row, ClaimCalculationMasterBundle masters, ClaimBillingConditionContext context)
        => row.ConditionSelectors.All(selector =>
        {
            var matches = masters.ConditionDefinitions
                .Where(d => d.Key == selector)
                .Take(2)
                .ToArray();

            var definition = matches.Length switch
            {
                0 => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
                1 => matches[0],
                _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
            };

            return Evaluate(definition, context);
        });

    private static bool Evaluate(ClaimConditionDefinition definition, ClaimBillingConditionContext context)
        => definition.Kind switch
        {
            ClaimConditionKind.RewardSystem => EvaluateToken(definition, context.RewardSystem),
            ClaimConditionKind.PaymentBand => EvaluateToken(definition, context.PaymentBand),
            ClaimConditionKind.Capacity => EvaluateInteger(definition, context.CapacityHeadcount),
            ClaimConditionKind.Staffing => EvaluateToken(definition, context.StaffingKey),
            ClaimConditionKind.AverageWageBand =>
                EvaluateInteger(definition, context.AverageWageBandOption.OfficialOptionCode),
            ClaimConditionKind.R8ReformStatus => EvaluateToken(definition, TokenFor(context.R8ReformStatus)),
            ClaimConditionKind.OfficeCapability => EvaluateCapability(definition, context),
            // 凍結スコープ（保護施設・基準該当等）のkindはフェイルクローズ
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

    /// <summary>
    /// 体制届条件: operandのキーが実効体制届の有効キー集合に含まれるか。集合が未取得（null）の
    /// 場合は判定不能としてフェイルクローズする（推測しない）。集合が空なら単に不成立
    /// （=当該加算を算定しない）であり、これは正しい請求挙動でありfail-openではない。
    /// </summary>
    private static bool EvaluateCapability(
        ClaimConditionDefinition definition, ClaimBillingConditionContext context)
    {
        if (context.OfficeCapabilityKeys is not { } keys)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved);

        return (definition.Operator, definition.Operand) switch
        {
            (ClaimConditionOperator.Equals, ClaimConditionTokenOperand token) =>
                keys.Contains(token.Value),
            (ClaimConditionOperator.In, ClaimConditionTokenSetOperand set) =>
                set.Values.Any(keys.Contains),
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };
    }

    private static bool EvaluateToken(ClaimConditionDefinition definition, string value)
        => (definition.Operator, definition.Operand) switch
        {
            (ClaimConditionOperator.Equals, ClaimConditionTokenOperand token) => token.Value == value,
            (ClaimConditionOperator.In, ClaimConditionTokenSetOperand set) => set.Values.Contains(value),
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

    private static bool EvaluateInteger(ClaimConditionDefinition definition, int value)
        => (definition.Operator, definition.Operand) switch
        {
            (ClaimConditionOperator.Equals, ClaimConditionIntegerOperand i) => value == i.Value,
            (ClaimConditionOperator.LessThan, ClaimConditionIntegerOperand i) => value < i.Value,
            (ClaimConditionOperator.LessThanOrEqual, ClaimConditionIntegerOperand i) => value <= i.Value,
            (ClaimConditionOperator.GreaterThan, ClaimConditionIntegerOperand i) => value > i.Value,
            (ClaimConditionOperator.GreaterThanOrEqual, ClaimConditionIntegerOperand i) => value >= i.Value,
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

    private static string TokenFor(R8ReformStatus status)
        => status switch
        {
            R8ReformStatus.NotApplicableBeforeR8 => "not-applicable-before-r8",
            R8ReformStatus.ReformTarget => "reform-target",
            R8ReformStatus.ReformExempt => "reform-exempt",
            R8ReformStatus.UnchangedBelow15000 => "unchanged-below-15000",
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };
}
