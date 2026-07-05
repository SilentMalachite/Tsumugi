using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class SetRecipientHourlyRateUseCase(
    IRecipientHourlyRateRepository repo, IUnitOfWork uow,
    IAuditTrail audit, TimeProvider clock)
{
    public async Task<RecipientHourlyRateDto> ExecuteAsync(
        Guid officeId, Guid recipientId, DateRange period, int hourlyYen,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor が空です。", nameof(actor));

        // 同一開始日の New が既にあれば SetWageFundUseCase と同じ規約で Correction を積む
        // （partial unique index (OfficeId, RecipientId, PeriodStart) WHERE Kind=New との整合）
        var existing = await repo.ListByOfficeRecipientAsync(officeId, recipientId, ct);
        var origin = existing
            .Where(r => r.Kind == RecordKind.New && r.Period.Start == period.Start)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();

        var now = clock.GetUtcNow();
        RecipientHourlyRate entity;
        if (origin is null)
        {
            entity = RecipientHourlyRate.NewRecord(
                Guid.NewGuid(), officeId, recipientId, period, hourlyYen,
                actor, now);
        }
        else
        {
            var tip = AppendOnlyChainPolicy.Tip(existing, origin, r => r.Kind, r => r.OriginId);
            if (tip.Kind == RecordKind.Cancel)
                throw new InvalidOperationException(
                    $"開始日 {period.Start:yyyy-MM-dd} の時給期間は取消済みのため再登録できません。別の開始日で登録してください。");
            entity = RecipientHourlyRate.Correction(
                Guid.NewGuid(), officeId, recipientId, period, tip.Id, hourlyYen,
                actor, now);
        }

        await repo.AddAsync(entity, ct);
        await audit.RecordAsync(
            actor,
            origin is null ? AuditAction.Register : AuditAction.Update,
            nameof(RecipientHourlyRate),
            entity.Id, now,
            $"RecipientHourlyRate {(origin is null ? "追記" : "訂正")} {hourlyYen}円/時", ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static RecipientHourlyRateDto Map(RecipientHourlyRate e) =>
        new(e.Id, e.OfficeId, e.RecipientId, e.Period, e.HourlyYen,
            e.Kind, e.OriginId, e.Note);
}
