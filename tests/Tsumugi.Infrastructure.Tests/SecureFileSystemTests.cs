using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// ADR 0003 追補の権限ポリシーを、DB 以外のファイル（バックアップ）にも適用できる形へ抽出したもの。
/// Unix: dir 0700 / file 0600。Windows: 現在ユーザーのみフルコントロール・継承無効。
/// </summary>
public sealed class SecureFileSystemTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-securefs-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureDirectory_creates_the_directory_when_missing()
    {
        SecureFileSystem.EnsureDirectory(_root);
        Directory.Exists(_root).Should().BeTrue();
    }

    [Fact]
    public void EnsureDirectory_is_idempotent()
    {
        SecureFileSystem.EnsureDirectory(_root);
        var act = () => SecureFileSystem.EnsureDirectory(_root);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureFile_creates_the_file_when_missing()
    {
        SecureFileSystem.EnsureDirectory(_root);
        var file = Path.Combine(_root, "x.db");
        SecureFileSystem.EnsureFile(file);
        File.Exists(file).Should().BeTrue();
    }

    [Fact]
    public void Unix_directory_mode_is_0700_and_file_mode_is_0600()
    {
        // 該当 OS 以外は早期 return（xUnit 2.x のため Skip.If は使わない）
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        SecureFileSystem.EnsureDirectory(_root);
        var file = Path.Combine(_root, "x.db");
        SecureFileSystem.EnsureFile(file);

        const UnixFileMode dirMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        const UnixFileMode fileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        File.GetUnixFileMode(_root).Should().Be(dirMode);
        File.GetUnixFileMode(file).Should().Be(fileMode);
    }

    [Fact]
    public void Unix_tightens_an_existing_loose_mode()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        Directory.CreateDirectory(_root);
        File.SetUnixFileMode(_root,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        SecureFileSystem.EnsureDirectory(_root);

        File.GetUnixFileMode(_root).Should().Be(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public void TryEnsureFile_returns_false_instead_of_throwing_when_the_path_is_unusable()
    {
        // 存在しないディレクトリ配下のファイルには権限を適用できない。
        // 外部媒体（FAT32/exFAT 等）で権限適用が失敗する状況の代理。
        var unusable = Path.Combine(_root, "no-such-dir", "x.db");
        SecureFileSystem.TryEnsureFile(unusable).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
