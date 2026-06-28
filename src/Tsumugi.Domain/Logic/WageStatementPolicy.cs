using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>工賃確定スナップショットの訂正履歴から実効レコードを導出する純粋関数。</summary>
public static class WageStatementPolicy
{
    public static WageStatement? Effective(IEnumerable<WageStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        var list = statements.OrderBy(s => s.CreatedAt).ToArray();
        if (list.Length == 0) return null;

        var origin = list.FirstOrDefault(s => s.Kind == RecordKind.New);
        if (origin is null) return null;

        var current = origin;
        while (true)
        {
            var next = list
                .Where(s => s.OriginId == current.Id && s.Kind != RecordKind.New)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();
            if (next is null) return current;
            if (next.Kind == RecordKind.Cancel) return null;
            current = next;
        }
    }

    public static IReadOnlyDictionary<Guid, WageStatement> EffectiveByRecipient(
        IEnumerable<WageStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        var result = new Dictionary<Guid, WageStatement>();
        foreach (var group in statements.GroupBy(s => s.RecipientId))
            if (Effective(group) is { } eff) result[group.Key] = eff;
        return result;
    }
}
