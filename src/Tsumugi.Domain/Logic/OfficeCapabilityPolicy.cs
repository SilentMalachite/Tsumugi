using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

/// <summary>
/// 実効体制届の解決結果。<see cref="IsAmbiguous"/>が<c>true</c>のとき<see cref="Effective"/>は
/// <c>null</c>であり、呼び出し側はreadiness issueへ変換してフェイルクローズする（算定不能）。
/// 候補0件（<see cref="Effective"/>=<c>null</c>かつ非ambiguous）は「体制届未登録」を表す。
/// </summary>
public sealed record OfficeCapabilityResolution(OfficeCapability? Effective, bool IsAmbiguous);

/// <summary>
/// 事業所体制届（期間マスタ・追記型）の実効レコード解決（ADR 0021）。
/// <c>Period.Contains(asOf)</c>を満たす候補を <c>Period.Start</c> 降順→<c>CreatedAt</c> 降順で
/// 優先し先頭1件を実効とする（後の開始日・後の追記が先行レコードを暗黙にsupersede）。
/// 先頭が一意に決まらない場合（<c>Period.Start</c>と<c>CreatedAt</c>がともに同値）は
/// ID順等へフォールバックせず曖昧として返す。
/// </summary>
public static class OfficeCapabilityPolicy
{
    public static OfficeCapabilityResolution Resolve(
        IReadOnlyCollection<OfficeCapability> records, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(records);

        var candidates = records
            .Where(record => record.Period.Contains(asOf))
            .OrderByDescending(record => record.Period.Start)
            .ThenByDescending(record => record.CreatedAt)
            .ToArray();

        if (candidates.Length == 0)
            return new OfficeCapabilityResolution(Effective: null, IsAmbiguous: false);

        if (candidates.Length > 1
            && candidates[0].Period.Start == candidates[1].Period.Start
            && candidates[0].CreatedAt == candidates[1].CreatedAt)
        {
            return new OfficeCapabilityResolution(Effective: null, IsAmbiguous: true);
        }

        return new OfficeCapabilityResolution(candidates[0], IsAmbiguous: false);
    }
}
