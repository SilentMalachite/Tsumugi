using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;

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

        // R3-H1: 同日全レコードから現行実効を求め、originId が「現行実効」でなければ拒否する。
        // 取消が sibling として既に入っている場合、Policy は同一 origin の最新子を採るため、
        // ここで拒否しないと取消後に訂正を追加して取消を上書きできてしまう。
        var sameDay = await repo.ListByRecipientAndDateAsync(origin.RecipientId, origin.ServiceDate, ct);
        var effective = DailyRecordPolicy.Effective(sameDay);
        if (effective is null || effective.Id != originId)
            throw new InvalidOperationException("訂正元レコードは現行の実効状態ではありません。最新状態を再読込してください。");

        var entity = Domain.Entities.DailyRecord.Correction(
            Guid.NewGuid(), origin.RecipientId, origin.ServiceDate, originId,
            attendance, transport, mealProvided, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordDailyRecordUseCase.Map(entity);
    }
}
