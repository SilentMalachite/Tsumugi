using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class QueryMonthDailyRecordsUseCase(IDailyRecordRepository repo)
{
    public async Task<IReadOnlyDictionary<DateOnly, DailyRecordDto>> ExecuteAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        DateValidator.EnsureYearMonth(year, month);
        var raw = await repo.ListByRecipientAndMonthAsync(recipientId, year, month, ct);
        var effective = DailyRecordPolicy.EffectiveByDate(raw);
        return effective.ToDictionary(
            kv => kv.Key,
            kv => RecordDailyRecordUseCase.Map(kv.Value));
    }
}
