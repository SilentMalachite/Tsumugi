using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Contract;

public sealed class RegisterContractUseCase(
    IContractRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(ContractDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        Guid recipientId, DateRange period, int contractedSupplyDays,
        string actor, CancellationToken ct)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(recipientId));
        DateValidator.EnsureRange(period.Start, period.End, nameof(period));

        var existing = await repo.ListByRecipientAsync(recipientId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Period).Append(period).ToArray();
        if (PeriodPolicy.DetectOverlaps(ranges).Count > 0)
            warnings.Add("同一利用者の契約期間が重複しています。");

        var entity = Domain.Entities.Contract.Create(
            Guid.NewGuid(), recipientId, period, contractedSupplyDays,
            actor, clock.GetUtcNow(), Guid.NewGuid());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return (new ContractDto(entity.Id, entity.RecipientId, entity.Period, entity.ContractedSupplyDays),
                warnings);
    }
}
