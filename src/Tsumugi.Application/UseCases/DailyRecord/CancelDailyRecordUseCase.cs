using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;

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

        // R3-H1: 同日全レコードから現行実効を求め、originId が現行実効でなければ拒否する。
        var sameDay = await repo.ListByRecipientAndDateAsync(origin.RecipientId, origin.ServiceDate, ct);
        var effective = DailyRecordPolicy.Effective(sameDay);
        if (effective is null || effective.Id != originId)
            throw new InvalidOperationException("取消元レコードは現行の実効状態ではありません。最新状態を再読込してください。");

        var entity = Domain.Entities.DailyRecord.Cancellation(
            Guid.NewGuid(), origin.RecipientId, origin.ServiceDate, originId,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordDailyRecordUseCase.Map(entity);
    }
}
