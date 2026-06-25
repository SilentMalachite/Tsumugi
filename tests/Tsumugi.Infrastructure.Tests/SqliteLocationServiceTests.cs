using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class SqliteLocationServiceTests
{
    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), $"tsumugi-loc-{Guid.NewGuid():N}");

    private static bool IsUnix =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void EnsureSecuredStorage_creates_dir_0700_and_db_0600_on_unix()
    {
        if (!IsUnix) return; // 非該当OSではクリーンにスキップ

        var dir = NewTempDir();
        try
        {
            var svc = new SqliteLocationService(dir);
            svc.EnsureSecuredStorage();

            Directory.Exists(dir).Should().BeTrue();
            File.Exists(svc.DatabasePath).Should().BeTrue();

#pragma warning disable CA1416 // if (!IsUnix) return; が先行しているため Windows では到達しない
            var dirMode = File.GetUnixFileMode(dir);
            var dbMode = File.GetUnixFileMode(svc.DatabasePath);
#pragma warning restore CA1416

            dirMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                "ディレクトリは所有者のみ rwx（0700）であるべき");
            dbMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                "DBファイルは所有者のみ rw（0600）であるべき");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EnsureSecuredStorage_sets_current_user_only_dacl_on_windows()
    {
        if (!OperatingSystem.IsWindows()) return; // 非該当OSではスキップ

        var dir = NewTempDir();
        try
        {
            var svc = new SqliteLocationService(dir);
            svc.EnsureSecuredStorage();

            Directory.Exists(dir).Should().BeTrue();
            File.Exists(svc.DatabasePath).Should().BeTrue();

            AssertWindowsDaclIsCurrentUserOnly(dir, isDirectory: true);
            AssertWindowsDaclIsCurrentUserOnly(svc.DatabasePath, isDirectory: false);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

#pragma warning disable CA1416 // platform-guarded via OperatingSystem.IsWindows()
    private static void AssertWindowsDaclIsCurrentUserOnly(string path, bool isDirectory)
    {
        var currentSid = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        System.Security.AccessControl.AuthorizationRuleCollection rules;
        bool inheritanceProtected;

        if (isDirectory)
        {
            var sec = new DirectoryInfo(path).GetAccessControl();
            rules = sec.GetAccessRules(true, true,
                typeof(System.Security.Principal.SecurityIdentifier));
            inheritanceProtected = sec.AreAccessRulesProtected;
        }
        else
        {
            var sec = new FileInfo(path).GetAccessControl();
            rules = sec.GetAccessRules(true, true,
                typeof(System.Security.Principal.SecurityIdentifier));
            inheritanceProtected = sec.AreAccessRulesProtected;
        }

        inheritanceProtected.Should().BeTrue("継承は無効化されているべき");

        foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules)
        {
            rule.IdentityReference.Value.Should().Be(currentSid.Value,
                "DACL のすべての ACE は現在ユーザーに対するもののみであるべき");
            rule.AccessControlType.Should().Be(
                System.Security.AccessControl.AccessControlType.Allow);
            rule.FileSystemRights.Should().Be(
                System.Security.AccessControl.FileSystemRights.FullControl);
        }
    }
#pragma warning restore CA1416
}
