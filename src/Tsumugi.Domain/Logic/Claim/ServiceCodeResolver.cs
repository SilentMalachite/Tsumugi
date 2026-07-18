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
}

public sealed class ServiceCodeResolutionException(ServiceCodeResolutionErrorCode code)
    : Exception($"Service code resolution failed: {code}.")
{
    public ServiceCodeResolutionErrorCode Code { get; } = code;
}

public sealed record ResolvedBasicReward(
    string ServiceCode, string OfficialLabel, int UnitsPerDay, BillingUnit BillingUnit);

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

        return new ResolvedBasicReward(row.ServiceCode, row.OfficialLabel, baseRow.BaseUnits, row.UnitRule.BillingUnit);
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
            // 凍結スコープ（保護施設・基準該当等）のkindはフェイルクローズ
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

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
