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

    /// <summary>
    /// 宣言キーKが当月に生きているのに、Kを含む行がすべて他のcapabilityキーも要求していて、
    /// 宣言集合では1行も成立しない場合を検出する（ADR 0049の一般化。<see cref="FindUncoveredKeys"/>
    /// とは排反 — 前者は「Kが当月に無い」、こちらは「Kが当月にある」が前提）。
    /// </summary>
    /// <param name="declaredKeys">事業所が体制届で宣言したキー集合。</param>
    /// <param name="monthCapabilityRows">
    /// 当月に有効なservice-code行ごとの、capability種別の条件の値集合リスト
    /// （<see cref="ExtractCapabilityValueSets"/>で1行分ずつ組み立てる。1行 = 条件のリストで、
    /// 各条件は受理可能な値の集合）。1行の要件は「すべての条件の値集合が宣言集合と重なる」こと。
    /// </param>
    /// <remarks>
    /// 判定規則。宣言キーKについて:
    /// (1) Kが monthCapabilityRows のどの行のどの条件の値にも現れない → 対象外
    /// （<see cref="FindUncoveredKeys"/>の領分、または請求に効かないキー）。
    /// (2) Kを含む行が存在するが、そのどれも充足可能でない → 報告する。
    /// (3) Kを含む充足可能な行が1つでもある → 報告しない。
    /// </remarks>
    public static IReadOnlyList<string> FindUnsatisfiableDeclaredKeys(
        IReadOnlyCollection<string> declaredKeys,
        IReadOnlyList<IReadOnlyList<IReadOnlySet<string>>> monthCapabilityRows)
    {
        ArgumentNullException.ThrowIfNull(declaredKeys);
        ArgumentNullException.ThrowIfNull(monthCapabilityRows);

        var declared = new HashSet<string>(declaredKeys, StringComparer.Ordinal);
        var keysInRows = new HashSet<string>(
            monthCapabilityRows.SelectMany(row => row.SelectMany(values => values)),
            StringComparer.Ordinal);

        bool IsSatisfiable(IReadOnlyList<IReadOnlySet<string>> row) =>
            row.All(conditionValues => conditionValues.Overlaps(declared));

        return declaredKeys
            .Where(keysInRows.Contains)
            .Where(key => !monthCapabilityRows
                .Where(row => row.Any(conditionValues => conditionValues.Contains(key)))
                .Any(IsSatisfiable))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 1行分の条件定義（<see cref="Models.ServiceCodeMasterRow.ConditionSelectors"/>を解決した
    /// もの。他種別の条件が混在してよい）から、<c>kind: office-capability</c>のものだけを取り出し、
    /// 条件ごとの受理可能な値集合を<b>平坦化せずに</b>列挙する（<see cref="FindUnsatisfiableDeclaredKeys"/>
    /// の1行分の入力を組み立てるためのヘルパ）。token operandは単一値集合、token set operandは
    /// 複数値集合。他のoperand型・office-capability以外のkindは対象外（<see cref="ExtractCapabilityValues"/>
    /// と同じ契約。特にfacility-classification等の混在は偽陽性の原因になるため必ず除外する）。
    /// </summary>
    public static IReadOnlyList<IReadOnlySet<string>> ExtractCapabilityValueSets(
        IEnumerable<ClaimConditionDefinition> rowConditionDefinitions)
    {
        ArgumentNullException.ThrowIfNull(rowConditionDefinitions);

        return rowConditionDefinitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .Select(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token =>
                    (IReadOnlySet<string>?)new HashSet<string>([token.Value], StringComparer.Ordinal),
                ClaimConditionTokenSetOperand set => new HashSet<string>(set.Values, StringComparer.Ordinal),
                _ => null,
            })
            .Where(valueSet => valueSet is not null)
            .Select(valueSet => valueSet!)
            .ToArray();
    }
}
