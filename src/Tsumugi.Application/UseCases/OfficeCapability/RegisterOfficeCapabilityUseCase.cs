using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.OfficeCapability;

public sealed class RegisterOfficeCapabilityUseCase(
    IOfficeCapabilityRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(OfficeCapabilityDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        Guid officeId, DateRange period, IReadOnlyDictionary<string, bool> flags,
        string actor, CancellationToken ct)
    {
        DateValidator.EnsureRange(period.Start, period.End, nameof(period));
        ArgumentNullException.ThrowIfNull(flags);

        var existing = await repo.ListByOfficeAsync(officeId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Period).Append(period).ToArray();
        if (PeriodPolicy.DetectOverlaps(ranges).Count > 0)
            warnings.Add("同一事業所の体制期間が重複しています。");

        var entity = Domain.Entities.OfficeCapability.Create(
            Guid.NewGuid(), officeId, period, flags,
            actor, clock.GetUtcNow(), Guid.NewGuid());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return (new OfficeCapabilityDto(entity.Id, entity.OfficeId, entity.Period, entity.Flags),
                warnings);
    }
}
