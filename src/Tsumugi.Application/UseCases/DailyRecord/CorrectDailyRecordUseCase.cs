using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class CorrectDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(
        Guid originId, Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string actor, CancellationToken ct)
    {
        var origin = await repo.FindByIdAsync(originId, ct)
            ?? throw new InvalidOperationException("訂正元レコードが見つかりません。");

        if (origin.Kind == RecordKind.Cancel)
            throw new InvalidOperationException("取消済みレコードは訂正できません。");

        var entity = Domain.Entities.DailyRecord.Correction(
            Guid.NewGuid(), origin.RecipientId, origin.ServiceDate, originId,
            attendance, transport, mealProvided, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordDailyRecordUseCase.Map(entity);
    }
}
