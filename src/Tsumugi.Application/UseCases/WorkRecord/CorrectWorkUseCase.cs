using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.WorkRecord;

public sealed class CorrectWorkUseCase(
    IWorkRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<WorkRecordDto> ExecuteAsync(
        Guid originId,
        int? workedMinutes, int? pieceCount, int? pieceUnitYen, int? points,
        string? note, string actor, CancellationToken ct)
    {
        var origin = await repo.FindByIdAsync(originId, ct)
            ?? throw new InvalidOperationException("訂正元の作業実績が見つかりません。");

        if (origin.Kind == RecordKind.Cancel)
            throw new InvalidOperationException("取消済みの作業実績は訂正できません。");

        var sameDay = (await repo.ListByRecipientAndMonthAsync(
                origin.RecipientId, origin.WorkDate.Year, origin.WorkDate.Month, ct))
            .Where(r => r.WorkDate == origin.WorkDate)
            .ToArray();
        var effective = WorkRecordPolicy.Effective(sameDay);
        if (effective is null || effective.Id != originId)
            throw new InvalidOperationException("訂正元の作業実績は現行の実効状態ではありません。最新状態を再読込してください。");

        var entity = Domain.Entities.WorkRecord.Correction(
            Guid.NewGuid(), origin.RecipientId, origin.WorkDate, originId,
            workedMinutes, pieceCount, pieceUnitYen, points, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordWorkUseCase.Map(entity);
    }
}
