using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Office;

public sealed class UpdateOfficeUseCase(
    IOfficeRepository repo, IUnitOfWork uow, TimeProvider clock, IAuditTrail audit)
{
    public async Task ExecuteAsync(
        Guid id, Guid expectedConcurrencyToken,
        string name, ServiceCategory category, RegionGrade region,
        string actor,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(id));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));
        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("事業所が見つかりません。");
        if (existing.ConcurrencyToken != expectedConcurrencyToken)
            throw new OptimisticConcurrencyException(nameof(Tsumugi.Domain.Entities.Office), id);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));

        var updated = existing with
        {
            Name = name,
            ServiceCategory = category,
            RegionGrade = region,
        };
        await repo.UpdateAsync(updated, ct);
        await audit.RecordAsync(actor, AuditAction.Update, nameof(Tsumugi.Domain.Entities.Office),
            id, clock.GetUtcNow(),
            summary: $"name={name}; category={category}; region={region}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
