using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// ADR 0003 追補の権限ポリシーを、任意のディレクトリ／ファイルへ適用する。
/// Unix: dir 0700 / file 0600。Windows: 現在ユーザーのみフルコントロール・継承無効。
/// 既存のゆるい権限は「広げない・狭めるのみ」で冪等に締め直す。
/// </summary>
internal static class SecureFileSystem
{
    private const UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode FileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (IsUnix())
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path, DirectoryMode);
            else File.SetUnixFileMode(path, DirectoryMode);
            return;
        }

        if (OperatingSystem.IsWindows()) { EnsureWindowsDirectory(path); return; }

        throw new PlatformNotSupportedException(
            "サポートされないOSで Tsumugi の保存先を初期化しようとした。");
    }

    public static void EnsureFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (IsUnix())
        {
            // File.Create はプロセス umask 経由の権限で作成し、直後に 0600 へ締める。
            // 管理コードに「モード付きアトミック作成」API は無いため、この瞬間は不可避。
            // 親ディレクトリ 0700 の保護が外部ユーザーを遮るため実害は無視できる。
            if (!File.Exists(path)) { using (File.Create(path)) { } }
            File.SetUnixFileMode(path, FileMode);
            return;
        }

        if (OperatingSystem.IsWindows()) { EnsureWindowsFile(path); return; }

        throw new PlatformNotSupportedException(
            "サポートされないOSで Tsumugi の保存先を初期化しようとした。");
    }

    /// <summary>
    /// 権限適用を試み、失敗しても例外にしない版。外部媒体（FAT32/exFAT 等、
    /// Unix パーミッションも Windows ACL も持たないファイルシステム）向け。
    /// 「安全のための操作」が安全機構のせいで失敗するのを避ける。
    /// </summary>
    public static bool TryEnsureFile(string path)
    {
        try { EnsureFile(path); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (PlatformNotSupportedException) { return false; }
    }

    // CA1416（プラットフォーム互換性アナライザ）に「true を返せば Windows 以外」と
    // 伝えるためのガード注釈。これが無いと、直下の Unix 専用 API 呼び出しが
    // 「全プラットフォームから到達可能」として警告される。
    [UnsupportedOSPlatformGuard("windows")]
    private static bool IsUnix() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsDirectory(string path)
    {
        var currentUser = WindowsIdentity.GetCurrent().User!;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        if (!Directory.Exists(path)) Directory.CreateDirectory(path).SetAccessControl(security);
        else new DirectoryInfo(path).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsFile(string path)
    {
        var currentUser = WindowsIdentity.GetCurrent().User!;
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser, FileSystemRights.FullControl, AccessControlType.Allow));

        if (!File.Exists(path)) { using (File.Create(path)) { } }
        new FileInfo(path).SetAccessControl(security);
    }
}
