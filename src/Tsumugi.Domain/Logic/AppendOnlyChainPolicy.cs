// src/Tsumugi.Domain/Logic/AppendOnlyChainPolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>
/// 追記型レコード（New → Correct/Cancel）の連鎖解決を共通化する純粋関数。
/// WageFundPolicy 等と同じ規約: OriginId は直前レコードを指し、1 ホップずつ辿る。
/// Cancel は連鎖を終端させる（Cancel 以降の訂正は無効）。
/// </summary>
public static class AppendOnlyChainPolicy
{
    /// <summary>New レコードごとに連鎖を末端まで辿り、末端レコード（Cancel を含む）を列挙する。</summary>
    public static IEnumerable<T> Tips<T>(
        IReadOnlyCollection<T> records,
        Func<T, RecordKind> kind,
        Func<T, Guid?> originId) where T : Entity
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(originId);
        return Walk(records, kind, originId);
    }

    /// <summary>指定した起点レコードから連鎖を末端まで辿り、末端レコード（Cancel を含む）を返す。</summary>
    public static T Tip<T>(
        IReadOnlyCollection<T> records,
        T origin,
        Func<T, RecordKind> kind,
        Func<T, Guid?> originId) where T : Entity
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(originId);

        var current = origin;
        while (kind(current) != RecordKind.Cancel)
        {
            var next = records
                .Where(r => kind(r) != RecordKind.New && originId(r) == current.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ThenBy(r => r.Id)
                .FirstOrDefault();
            if (next is null) break;
            current = next;
        }
        return current;
    }

    private static IEnumerable<T> Walk<T>(
        IReadOnlyCollection<T> records,
        Func<T, RecordKind> kind,
        Func<T, Guid?> originId) where T : Entity
    {
        foreach (var origin in records.Where(r => kind(r) == RecordKind.New))
            yield return Tip(records, origin, kind, originId);
    }

    /// <summary>取消されていない連鎖の実効レコード（末端）のみを列挙する。</summary>
    public static IEnumerable<T> EffectiveTips<T>(
        IReadOnlyCollection<T> records,
        Func<T, RecordKind> kind,
        Func<T, Guid?> originId) where T : Entity =>
        Tips(records, kind, originId).Where(t => kind(t) != RecordKind.Cancel);
}
