using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class RecordDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(
        Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided, string? note,
        string actor, CancellationToken ct)
    {
        DateValidator.EnsureValid(serviceDate, nameof(serviceDate));

        var existing = await repo.ListByRecipientAndDateAsync(recipientId, serviceDate, ct);
        if (existing.Any(r => r.Kind == Domain.Enums.RecordKind.New))
            throw new InvalidOperationException("同一日に新規記録が既に存在します。訂正または取消を使用してください。");

        var entity = Domain.Entities.DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, serviceDate,
            attendance, transport, mealProvided, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static DailyRecordDto Map(Domain.Entities.DailyRecord e) =>
        new(e.Id, e.RecipientId, e.ServiceDate, e.Kind, e.OriginId,
            e.Attendance, e.Transport, e.MealProvided, e.Note);
}
