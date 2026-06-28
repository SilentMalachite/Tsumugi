using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>作業実績の訂正・取消の実効状態を導出する純粋関数。DailyRecordPolicy と同型。</summary>
public static class WorkRecordPolicy
{
    public static WorkRecord? Effective(IEnumerable<WorkRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var list = records.OrderBy(r => r.CreatedAt).ToArray();
        if (list.Length == 0) return null;

        var origin = list.FirstOrDefault(r => r.Kind == RecordKind.New);
        if (origin is null) return null;

        var current = origin;
        while (true)
        {
            var next = list
                .Where(r => r.OriginId == current.Id && r.Kind != RecordKind.New)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null) return current;
            if (next.Kind == RecordKind.Cancel) return null;
            current = next;
        }
    }

    public static IReadOnlyDictionary<DateOnly, WorkRecord> EffectiveByDate(
        IEnumerable<WorkRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var result = new Dictionary<DateOnly, WorkRecord>();
        foreach (var group in records.GroupBy(r => r.WorkDate))
            if (Effective(group) is { } eff) result[group.Key] = eff;
        return result;
    }
}
