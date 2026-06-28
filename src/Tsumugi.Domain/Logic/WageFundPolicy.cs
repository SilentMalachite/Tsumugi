using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

public static class WageFundPolicy
{
    public static WageFund? Effective(IEnumerable<WageFund> records)
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
}
