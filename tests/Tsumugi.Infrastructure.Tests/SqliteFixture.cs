using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>テストごとに使い捨ての一時ファイルSQLiteを用意し、マイグレーションを適用する。</summary>
public sealed class SqliteFixture : IDisposable
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"tsumugi-test-{Guid.NewGuid():N}.db");

    public TsumugiDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options;
        var ctx = new TsumugiDbContext(options);
        ctx.Database.Migrate(); // 空DB→現行スキーマまで適用
        return ctx;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { DbPath, DbPath + "-shm", DbPath + "-wal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
