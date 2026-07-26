using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// 事業所が体制届で宣言したキーのうち、処理対象月に有効なマスタ行へ結び付かないものを
/// 検出する（ADR 0049）。日付・乱数・I/Oに依存しない純粋関数。
/// </summary>
/// <remarks>
/// 2段構えにしているのは偽陽性を避けるため。体制届には算定に関与しない項目もあり、
/// 「当月に無い」だけで警告すると毎月ノイズが出る。「他の期間では使われている」ことを
/// 条件に加えることで、失効・未施行という本当の穴だけを拾う。
/// </remarks>
public static class OfficeCapabilityCoveragePolicy
{
    public static IReadOnlyList<string> FindUncoveredKeys(
        IReadOnlyCollection<string> declaredKeys,
        IReadOnlyCollection<string> monthConditionValues,
        IReadOnlyCollection<string> allConditionValues)
    {
        ArgumentNullException.ThrowIfNull(declaredKeys);
        ArgumentNullException.ThrowIfNull(monthConditionValues);
        ArgumentNullException.ThrowIfNull(allConditionValues);

        var month = new HashSet<string>(monthConditionValues, StringComparer.Ordinal);
        var all = new HashSet<string>(allConditionValues, StringComparer.Ordinal);

        return declaredKeys
            .Where(key => all.Contains(key) && !month.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// <c>kind: office-capability</c>の条件定義から、operandが運ぶ値文字列を列挙する
    /// （token operandは単一値、token set operandは複数値。他のoperand型は対象外）。
    /// <see cref="FindUncoveredKeys"/>への入力（当月分・全期間分の両方）を組み立てる唯一の
    /// 場所とし、同じ抽出ロジックが呼び出し側ごとに複製されて食い違う事故を防ぐ（ADR 0049）。
    /// </summary>
    public static IReadOnlyList<string> ExtractCapabilityValues(
        IEnumerable<ClaimConditionDefinition> conditionDefinitions)
    {
        ArgumentNullException.ThrowIfNull(conditionDefinitions);

        return conditionDefinitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .SelectMany(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token => new[] { token.Value },
                ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                _ => [],
            })
            .ToArray();
    }
}
