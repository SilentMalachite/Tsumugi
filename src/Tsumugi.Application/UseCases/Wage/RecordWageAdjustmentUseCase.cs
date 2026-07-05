using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class RecordWageAdjustmentUseCase(
    IWageAdjustmentRepository repo, IUnitOfWork uow,
    IAuditTrail audit, TimeProvider clock)
{
    public async Task<WageAdjustmentDto> ExecuteAsync(
        Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, int amountYen, string? note,
        string actor, CancellationToken ct)
    {
        var results = await ExecuteManyAsync(
            officeId, new[] { (recipientId, amountYen) }, yearMonth, type, note, actor, ct);
        return results[0];
    }

    /// <summary>
    /// 複数利用者分を 1 トランザクションで追記する。
    /// 既に実効レコードがある利用者×月×種別は SetWageFundUseCase と同じ規約で Correction を積む。
    /// </summary>
    public async Task<IReadOnlyList<WageAdjustmentDto>> ExecuteManyAsync(
        Guid officeId, IReadOnlyList<(Guid RecipientId, int AmountYen)> entries,
        YearMonth yearMonth, WageAdjustmentType type, string? note,
        string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor が空です。", nameof(actor));
        if (entries.Count == 0) return Array.Empty<WageAdjustmentDto>();

        // バッチ内の同一利用者×月×種別も追跡し、直前に積んだレコードへ hop-by-hop で連鎖させる
        // （DB 未反映のうちに 2 件目以降が既存の New と衝突しないようにするため）
        var seen = new List<WageAdjustment>(
            await repo.ListByOfficeMonthAsync(officeId, yearMonth, ct));
        var now = clock.GetUtcNow();
        var results = new List<WageAdjustmentDto>(entries.Count);
        foreach (var (recipientId, amountYen) in entries)
        {
            var effective = WageAdjustmentPolicy.EffectiveRecord(
                seen, recipientId, yearMonth, type);
            var entity = effective is null
                ? WageAdjustment.NewRecord(
                    Guid.NewGuid(), officeId, recipientId, yearMonth,
                    type, amountYen, note, actor, now)
                : WageAdjustment.Correction(
                    Guid.NewGuid(), officeId, recipientId, yearMonth,
                    type, effective.Id, amountYen, note, actor, now);

            await repo.AddAsync(entity, ct);
            await audit.RecordAsync(
                actor,
                effective is null ? AuditAction.Register : AuditAction.Update,
                nameof(WageAdjustment), entity.Id, now,
                $"WageAdjustment {(effective is null ? "追記" : "訂正")} {type} {amountYen}円", ct);
            results.Add(Map(entity));
            seen.Add(entity);
        }
        await uow.SaveChangesAsync(ct);
        return results;
    }

    internal static WageAdjustmentDto Map(WageAdjustment e) =>
        new(e.Id, e.OfficeId, e.RecipientId, e.YearMonth, e.Type, e.AmountYen,
            e.Kind, e.OriginId, e.Note);
}
