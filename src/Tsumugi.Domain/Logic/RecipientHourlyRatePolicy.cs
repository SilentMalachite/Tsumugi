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
        var groups = records
            .Where(r => r.RecipientId == recipientId)
            .Where(r => r.Period.Contains(asOf))
            .GroupBy(r => r.Kind == RecordKind.New ? r.Id : r.OriginId ?? r.Id);

        int? latest = null;
        DateTimeOffset latestAt = DateTimeOffset.MinValue;
        foreach (var g in groups)
        {
            var ordered = g.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
            if (ordered.Count == 0) continue;
            var last = ordered[^1];
            if (last.Kind == RecordKind.Cancel) continue;
            if (last.CreatedAt >= latestAt)
            {
                latestAt = last.CreatedAt;
                latest = last.HourlyYen;
            }
        }
        return latest;
    }
}
