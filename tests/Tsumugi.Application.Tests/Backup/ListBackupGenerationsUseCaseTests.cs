using System.Collections.Generic;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Backup;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

public sealed class ListBackupGenerationsUseCaseTests
{
    private sealed class FakeBackupDirectory(params string[] existing) : IBackupDirectory
    {
        public IReadOnlyList<string> ListFileNames() => existing;
        public void Delete(string fileName) { }
    }

    [Fact]
    public void Excludes_files_without_the_backup_extension()
    {
        var dir = new FakeBackupDirectory(
            "tsumugi-backup-20260816-090000.db",
            "readme.txt",
            "x.db.tmp");
        var uc = new ListBackupGenerationsUseCase(dir);

        uc.Execute().Should().BeEquivalentTo(["tsumugi-backup-20260816-090000.db"]);
    }

    [Fact]
    public void Includes_pre_restore_snapshots()
    {
        // pre-restore-*.db は世代管理（自動削除）の対象外だが、復元 UI の一覧には出す仕様
        // （ListBackupGenerationsUseCase の doc comment: 「退避（pre-restore）も含める」）。
        var dir = new FakeBackupDirectory(
            "tsumugi-backup-20260816-090000.db",
            "pre-restore-20260816-080000.db");
        var uc = new ListBackupGenerationsUseCase(dir);

        uc.Execute().Should().BeEquivalentTo(
        [
            "tsumugi-backup-20260816-090000.db",
            "pre-restore-20260816-080000.db",
        ]);
    }

    [Fact]
    public void Orders_from_newest_to_oldest()
    {
        var dir = new FakeBackupDirectory(
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260817-090000.db",
            "tsumugi-backup-20260101-090000.db");
        var uc = new ListBackupGenerationsUseCase(dir);

        uc.Execute().Should().Equal(
            "tsumugi-backup-20260817-090000.db",
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260101-090000.db");
    }
}
