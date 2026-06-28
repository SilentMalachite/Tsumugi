using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.WorkRecord;

public sealed class QueryMonthWorkUseCase(IWorkRecordRepository repo)
{
    public async Task<IReadOnlyDictionary<DateOnly, WorkRecordDto>> ExecuteAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        DateValidator.EnsureYearMonth(year, month);
        var raw = await repo.ListByRecipientAndMonthAsync(recipientId, year, month, ct);
        var effective = WorkRecordPolicy.EffectiveByDate(raw);
        return effective.ToDictionary(
            kv => kv.Key,
            kv => RecordWorkUseCase.Map(kv.Value));
    }
}
