using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class EfUnitOfWork(TsumugiDbContext db) : IUnitOfWork
{
    /// <summary>
    /// UseCase 単位のトランザクション境界。Save 後に ChangeTracker を解放し、
    /// 単一スコープで使い回す DbContext に追跡が蓄積して同一キー再更新が衝突するのを防ぐ
    /// （Avalonia デスクトップアプリで App ライフタイムの単一 Scope を採用しているため必須）。
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        var result = await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        return result;
    }
}
