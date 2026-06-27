using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Office;

public sealed class UpdateOfficeUseCase(IOfficeRepository repo, IUnitOfWork uow)
{
    public async Task ExecuteAsync(
        Guid id, string name, ServiceCategory category, RegionGrade region,
        CancellationToken ct)
    {
        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("事業所が見つかりません。");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));

        var updated = existing with
        {
            Name = name,
            ServiceCategory = category,
            RegionGrade = region,
            ConcurrencyToken = Guid.NewGuid(),
        };
        await repo.UpdateAsync(updated, ct);
        await uow.SaveChangesAsync(ct);
    }
}
