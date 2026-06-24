using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
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
            ctx.Offices.Add(Office.Create(id, "1111111111", "元DB", "u",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync(CancellationToken.None);

            var backupPath = Path.Combine(Path.GetTempPath(), $"tsumugi-bak-{Guid.NewGuid():N}.db");
            var sut = new SqliteBackupService(ctx);
            await sut.BackupToAsync(backupPath, CancellationToken.None);

            File.Exists(backupPath).Should().BeTrue();

            // バックアップを別コンテキストで開いてデータを確認
            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite($"Data Source={backupPath}").Options;
            await using var restored = new TsumugiDbContext(options);
            (await restored.Offices.SingleAsync(o => o.Id == id)).Name.Should().Be("元DB");

            File.Delete(backupPath);
        }
    }
}
