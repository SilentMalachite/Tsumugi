using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class QueryWageStatementUseCase(IWageStatementRepository repo)
{
    public async Task<IReadOnlyList<WageStatementDto>> ExecuteAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        if (officeId == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(officeId));
        DateValidator.EnsureYearMonth(year, month);

        var raw = await repo.ListByOfficeAndMonthAsync(officeId, year, month, ct);
        var effective = WageStatementPolicy.EffectiveByRecipient(raw);
        return effective.Values
            .Select(s => CloseWagesUseCase.Map(s, year, month))
            .ToArray();
    }
}
