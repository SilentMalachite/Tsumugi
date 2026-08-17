using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Office;

/// <summary>
/// 事業所の登録件数だけを返す。初回起動判定のように件数しか要らない経路で、
/// <see cref="ListOfficesUseCase"/> の全件読み出しと DTO 射影を避けるために使う。
/// </summary>
public sealed class CountOfficesUseCase(IOfficeRepository repo)
{
    public Task<int> ExecuteAsync(CancellationToken ct) => repo.CountAsync(ct);
}
