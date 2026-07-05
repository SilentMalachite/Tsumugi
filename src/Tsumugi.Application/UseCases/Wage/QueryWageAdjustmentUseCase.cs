using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class QueryWageAdjustmentUseCase(IWageAdjustmentRepository repo)
{
    public async Task<IReadOnlyList<WageAdjustmentDto>> ExecuteAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct)
    {
        var entities = await repo.ListByOfficeMonthAsync(officeId, yearMonth, ct);
        return entities.Select(RecordWageAdjustmentUseCase.Map).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetEffectiveSpecialAllowanceByRecipientAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct)
    {
        var entities = await repo.ListByOfficeMonthAsync(officeId, yearMonth, ct);
        return entities
            .Select(e => e.RecipientId)
            .Distinct()
            .ToDictionary(
                rid => rid,
                rid => WageAdjustmentPolicy.EffectiveYen(entities, rid, yearMonth,
                           WageAdjustmentType.SpecialAllowance));
    }
}
