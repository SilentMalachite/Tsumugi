using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class CancelDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(Guid originId, string actor, CancellationToken ct)
    {
        var origin = await repo.FindByIdAsync(originId, ct)
            ?? throw new InvalidOperationException("取消元レコードが見つかりません。");

        if (origin.Kind == RecordKind.Cancel)
            throw new InvalidOperationException("取消済みレコードを再度取り消すことはできません。");

        var entity = Domain.Entities.DailyRecord.Cancellation(
            Guid.NewGuid(), origin.RecipientId, origin.ServiceDate, originId,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordDailyRecordUseCase.Map(entity);
    }
}
