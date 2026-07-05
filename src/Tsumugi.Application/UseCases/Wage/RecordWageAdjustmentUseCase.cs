using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
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
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor が空です。", nameof(actor));

        var entity = WageAdjustment.NewRecord(
            Guid.NewGuid(), officeId, recipientId, yearMonth,
            type, amountYen, note, actor, clock.GetUtcNow());

        await repo.AddAsync(entity, ct);
        await audit.RecordAsync(
            actor, AuditAction.Register, nameof(WageAdjustment),
            entity.Id, clock.GetUtcNow(),
            $"WageAdjustment 追記 {type} {amountYen}円", ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static WageAdjustmentDto Map(WageAdjustment e) =>
        new(e.Id, e.OfficeId, e.RecipientId, e.YearMonth, e.Type, e.AmountYen,
            e.Kind, e.OriginId, e.Note);
}
