using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.OfficeCapability;

public sealed class ListOfficeCapabilitiesUseCase(IOfficeCapabilityRepository repo)
{
    public async Task<IReadOnlyList<OfficeCapabilityDto>> ExecuteAsync(Guid officeId, CancellationToken ct)
    {
        var list = await repo.ListByOfficeAsync(officeId, ct);
        return list.Select(c => new OfficeCapabilityDto(c.Id, c.OfficeId, c.Period, c.Flags)).ToArray();
    }
}
