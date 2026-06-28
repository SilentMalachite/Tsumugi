using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// アーカイブ済み利用者を復元する。未アーカイブの場合は冪等に成功する。
/// </summary>
public sealed class RestoreRecipientUseCase(
    IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock, IAuditTrail audit)
{
    public async Task ExecuteAsync(
        Guid id, Guid expectedConcurrencyToken, string actor, CancellationToken ct)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(id));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));

        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("利用者が見つかりません。");
        if (existing.ConcurrencyToken != expectedConcurrencyToken)
            throw new OptimisticConcurrencyException(nameof(Tsumugi.Domain.Entities.Recipient), id);

        if (!existing.IsArchived)
        {
            return;
        }

        var restored = existing.Restore();
        await repo.UpdateAsync(restored, ct);
        await audit.RecordAsync(actor, AuditAction.Restore, nameof(Tsumugi.Domain.Entities.Recipient),
            id, clock.GetUtcNow(), summary: null, ct);
        await uow.SaveChangesAsync(ct);
    }
}
