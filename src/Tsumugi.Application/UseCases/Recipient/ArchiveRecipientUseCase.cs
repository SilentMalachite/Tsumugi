using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// 利用者をアーカイブ（論理削除）する。物理削除は行わない。
/// 既にアーカイブ済みの場合は冪等に成功する。
/// </summary>
public sealed class ArchiveRecipientUseCase(
    IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock)
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

        if (existing.IsArchived)
        {
            // 冪等: 何もしない。
            return;
        }

        var archived = existing.Archive(actor, clock.GetUtcNow());
        await repo.UpdateAsync(archived, ct);
        await uow.SaveChangesAsync(ct);
    }
}
