using System;
using System.IO;
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class BackupDirectoryServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-backupdir-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListFileNames_returns_file_names_directly_under_the_directory()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();
        var sut = new BackupDirectoryService(location);

        File.WriteAllText(Path.Combine(location.BackupDirectory, "a.db"), "a");
        File.WriteAllText(Path.Combine(location.BackupDirectory, "b.db"), "b");

        var names = sut.ListFileNames();

        names.Should().BeEquivalentTo(["a.db", "b.db"]);
        // パスではなくファイル名だけであること。
        names.Should().NotContain(n => n.Contains(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ListFileNames_returns_empty_when_the_backup_directory_does_not_exist()
    {
        // EnsureSecuredStorage を呼ばないため backups ディレクトリは未作成のまま。
        var location = new SqliteLocationService(_root);
        var sut = new BackupDirectoryService(location);

        sut.ListFileNames().Should().BeEmpty();
    }

    [Fact]
    public void Delete_removes_the_specified_file()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();
        var sut = new BackupDirectoryService(location);
        var path = Path.Combine(location.BackupDirectory, "target.db");
        File.WriteAllText(path, "x");

        sut.Delete("target.db");

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void Delete_of_a_missing_file_does_not_throw()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();
        var sut = new BackupDirectoryService(location);

        var act = () => sut.Delete("does-not-exist.db");

        act.Should().NotThrow();
    }

    [Fact]
    public void Delete_rejects_path_separators_and_leaves_files_outside_the_directory_untouched()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();
        var sut = new BackupDirectoryService(location);

        // 犠牲ファイルはバックアップディレクトリの外（_root 直下）に置く。
        // ガードが効いていれば、以下のどの入力でもこのファイルは消えない。
        var victimPath = Path.Combine(_root, "victim.db");
        File.WriteAllText(victimPath, "victim");

        var maliciousNames = new[]
        {
            "../victim.db",
            Path.Combine("a", "b.db"),
            victimPath, // 絶対パス
        };

        foreach (var name in maliciousNames)
        {
            var act = () => sut.Delete(name);
            act.Should().Throw<ArgumentException>();
        }

        File.Exists(victimPath).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
