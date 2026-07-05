// src/Tsumugi.Domain/Logic/WageAdjustmentPolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

public static class WageAdjustmentPolicy
{
    /// <summary>(recipientId, ym, type) の実効レコード（取消されていない連鎖の末端）を返す。</summary>
    public static WageAdjustment? EffectiveRecord(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym, WageAdjustmentType type)
    {
        ArgumentNullException.ThrowIfNull(records);
        var candidates = records
            .Where(r => r.RecipientId == recipientId && r.YearMonth == ym && r.Type == type)
            .ToList();

        // 同一キーの New は partial unique index で一意だが、防御的に最新連鎖を採用する
        return AppendOnlyChainPolicy
            .EffectiveTips(candidates, r => r.Kind, r => r.OriginId)
            .OrderByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .FirstOrDefault();
    }

    public static int EffectiveYen(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym, WageAdjustmentType type) =>
        EffectiveRecord(records, recipientId, ym, type)?.AmountYen ?? 0;

    public static int SumEffective(
        IEnumerable<WageAdjustment> records, Guid recipientId, YearMonth ym)
    {
        var materialized = records as IReadOnlyCollection<WageAdjustment> ?? records.ToList();
        return Enum.GetValues<WageAdjustmentType>()
            .Sum(t => EffectiveYen(materialized, recipientId, ym, t));
    }
}
