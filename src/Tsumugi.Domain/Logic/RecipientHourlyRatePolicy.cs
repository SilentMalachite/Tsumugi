// src/Tsumugi.Domain/Logic/RecipientHourlyRatePolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>RecipientHourlyRate 群から (recipientId, asOf) での実効時給を導出する純粋関数。</summary>
public static class RecipientHourlyRatePolicy
{
    public static int? EffectiveYen(
        IEnumerable<RecipientHourlyRate> records, Guid recipientId, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(records);
        var candidates = records
            .Where(r => r.RecipientId == recipientId)
            .ToList();

        // 連鎖を末端まで解決してから、末端レコードの期間で asOf を判定する
        // （訂正で期間自体が変わった場合も末端の期間が実効）
        var tip = AppendOnlyChainPolicy
            .EffectiveTips(candidates, r => r.Kind, r => r.OriginId)
            .Where(t => t.Period.Contains(asOf))
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .FirstOrDefault();
        return tip?.HourlyYen;
    }
}
