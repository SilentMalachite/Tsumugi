// src/Tsumugi.Domain/Logic/WageAdjustmentPolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

public static class WageAdjustmentPolicy
{
    public static int EffectiveYen(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym, WageAdjustmentType type)
    {
        ArgumentNullException.ThrowIfNull(records);
        var chain = records
            .Where(r => r.RecipientId == recipientId && r.YearMonth == ym && r.Type == type)
            .GroupBy(r => r.Kind == RecordKind.New ? r.Id : r.OriginId ?? r.Id)
            .Select(g => g.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList())
            .Where(g => g.Count > 0 && g[^1].Kind != RecordKind.Cancel)
            .OrderByDescending(g => g[^1].CreatedAt)
            .FirstOrDefault();
        return chain is null ? 0 : chain[^1].AmountYen;
    }

    public static int SumEffective(
        IEnumerable<WageAdjustment> records, Guid recipientId, YearMonth ym)
    {
        var materialized = records as IReadOnlyCollection<WageAdjustment> ?? records.ToList();
        return Enum.GetValues<WageAdjustmentType>()
            .Sum(t => EffectiveYen(materialized, recipientId, ym, t));
    }
}
