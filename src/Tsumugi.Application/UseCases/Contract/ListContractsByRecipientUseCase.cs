using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Contract;

public sealed class ListContractsByRecipientUseCase(IContractRepository repo)
{
    public async Task<IReadOnlyList<ContractDto>> ExecuteAsync(Guid recipientId, CancellationToken ct)
    {
        var list = await repo.ListByRecipientAsync(recipientId, ct);
        return list.Select(c => new ContractDto(c.Id, c.RecipientId, c.Period, c.ContractedSupplyDays)).ToArray();
    }
}
