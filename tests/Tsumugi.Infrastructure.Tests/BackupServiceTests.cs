using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class BackupServiceTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public BackupServiceTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Backup_creates_single_file_that_reopens_with_data()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Offices.Add(Office.Create(id, "1111111111", "元DB", ServiceCategory.TypeB, RegionGrade.None, "u",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync(CancellationToken.None);

            var backupPath = Path.Combine(Path.GetTempPath(), $"tsumugi-bak-{Guid.NewGuid():N}.db");
            var sut = new SqliteBackupService(ctx);
            await sut.BackupToAsync(backupPath, CancellationToken.None);

            File.Exists(backupPath).Should().BeTrue();

            // バックアップを別コンテキストで開いてデータを確認。
            // Windows は open file の削除を許さないため、検証スコープを明示的に閉じてから
            // SqliteConnection.ClearAllPools() でプール接続を解放し、その後に削除する。
            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite($"Data Source={backupPath}").Options;
            await using (var restored = new TsumugiDbContext(options))
            {
                (await restored.Offices.SingleAsync(o => o.Id == id)).Name.Should().Be("元DB");
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { backupPath, backupPath + "-shm", backupPath + "-wal" })
            {
                if (File.Exists(f)) File.Delete(f);
            }
        }
    }
}
