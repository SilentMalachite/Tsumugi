using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class QueryRecipientHourlyRateUseCase(IRecipientHourlyRateRepository repo)
{
    public async Task<IReadOnlyList<RecipientHourlyRateDto>> ExecuteAsync(
        Guid officeId, Guid recipientId, CancellationToken ct)
    {
        var entities = await repo.ListByOfficeRecipientAsync(officeId, recipientId, ct);
        return entities.Select(SetRecipientHourlyRateUseCase.Map).ToList();
    }
}
