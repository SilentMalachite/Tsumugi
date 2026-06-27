using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Office;

public sealed class ListOfficesUseCase(IOfficeRepository repo)
{
    public async Task<IReadOnlyList<OfficeDto>> ExecuteAsync(CancellationToken ct)
    {
        var list = await repo.ListAsync(ct);
        return list.Select(o => new OfficeDto(o.Id, o.OfficeNumber, o.Name, o.ServiceCategory, o.RegionGrade)).ToArray();
    }
}
