using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.WorkRecord;

public sealed class RecordWorkUseCase(
    IWorkRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<WorkRecordDto> ExecuteAsync(
        Guid recipientId, DateOnly workDate,
        int? workedMinutes, int? pieceCount, int? pieceUnitYen, int? points,
        string? note, string actor, CancellationToken ct)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(recipientId));
        DateValidator.EnsureValid(workDate, nameof(workDate));

        var month = await repo.ListByRecipientAndMonthAsync(recipientId, workDate.Year, workDate.Month, ct);
        if (month.Any(r => r.WorkDate == workDate && r.Kind == RecordKind.New))
            throw new InvalidOperationException("同一日に作業実績の新規記録が既に存在します。訂正または取消を使用してください。");

        var entity = Domain.Entities.WorkRecord.NewRecord(
            Guid.NewGuid(), recipientId, workDate,
            workedMinutes, pieceCount, pieceUnitYen, points, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static WorkRecordDto Map(Domain.Entities.WorkRecord e) =>
        new(e.Id, e.RecipientId, e.WorkDate, e.Kind, e.OriginId,
            e.WorkedMinutes, e.PieceCount, e.PieceUnitYen, e.Points, e.Note);
}
