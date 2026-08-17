using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeRepository
{
    Task AddAsync(Office office, CancellationToken ct);
    Task<Office?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct);
    Task UpdateAsync(Office office, CancellationToken ct);
    Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct);

    /// <summary>
    /// 件数だけを返す。初回起動判定のように件数しか要らない経路で、
    /// 全件の読み出しと DTO 射影を避けるために使う。
    /// 既定実装は <see cref="ListAsync"/> にフォールバックするので、
    /// 件数を安く数えられる実装だけが上書きすればよい。
    /// </summary>
    async Task<int> CountAsync(CancellationToken ct) => (await ListAsync(ct)).Count;
}
