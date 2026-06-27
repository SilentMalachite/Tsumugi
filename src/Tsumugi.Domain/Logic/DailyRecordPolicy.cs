using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>日次記録の訂正・取消の実効状態を導出する純粋関数。</summary>
public static class DailyRecordPolicy
{
    /// <summary>
    /// (Recipient, ServiceDate) ごとの全レコードから実効レコードを返す。
    /// アルゴリズム: 新規レコードを起点に、自分を OriginId とする「次の訂正/取消」を辿る。
    /// 同一 OriginId に複数の訂正・取消（兄弟）がぶら下がる場合は CreatedAt 最新を採用する。
    /// 取消に当たったらその時点で実効は null。
    /// </summary>
    public static DailyRecord? Effective(IEnumerable<DailyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var list = records.OrderBy(r => r.CreatedAt).ToArray();
        if (list.Length == 0) return null;

        var origin = list.FirstOrDefault(r => r.Kind == RecordKind.New);
        if (origin is null) return null;

        var current = origin;
        while (true)
        {
            // 兄弟が複数あるときは最新を採る（最古を採ると stale な訂正が勝ってしまう）。
            var next = list
                .Where(r => r.OriginId == current.Id && r.Kind != RecordKind.New)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null) return current;
            if (next.Kind == RecordKind.Cancel) return null;
            current = next;  // Correct
        }
    }

    /// <summary>月次ビュー用：日付ごとの実効レコードのマップを返す。</summary>
    public static IReadOnlyDictionary<DateOnly, DailyRecord> EffectiveByDate(
        IEnumerable<DailyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var result = new Dictionary<DateOnly, DailyRecord>();
        foreach (var group in records.GroupBy(r => r.ServiceDate))
        {
            if (Effective(group) is { } eff)
                result[group.Key] = eff;
        }
        return result;
    }
}
