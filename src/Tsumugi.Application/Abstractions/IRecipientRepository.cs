using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IRecipientRepository
{
    Task AddAsync(Recipient recipient, CancellationToken ct);
    Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(Recipient recipient, CancellationToken ct);

    /// <summary>
    /// 利用者を一覧する。<paramref name="includeArchived"/> が true のときアーカイブ済みも含む。
    /// 既定 (false) は未アーカイブのみ返す。
    /// </summary>
    Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct);
}
