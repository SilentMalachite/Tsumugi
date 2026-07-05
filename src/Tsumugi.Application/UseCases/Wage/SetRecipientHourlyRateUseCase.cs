using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
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

        var entity = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period, hourlyYen,
            actor, clock.GetUtcNow());

        await repo.AddAsync(entity, ct);
        await audit.RecordAsync(
            actor, AuditAction.Register, nameof(RecipientHourlyRate),
            entity.Id, clock.GetUtcNow(),
            $"RecipientHourlyRate 追記 {hourlyYen}円/時", ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static RecipientHourlyRateDto Map(RecipientHourlyRate e) =>
        new(e.Id, e.OfficeId, e.RecipientId, e.Period, e.HourlyYen,
            e.Kind, e.OriginId, e.Note);
}
