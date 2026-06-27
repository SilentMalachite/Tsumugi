using System;
using System.Linq;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// 通信関連の P/Invoke と URL 文字列の純粋判定。AssemblyMetadataScanner の生データに対して適用する。
/// テストでは独立に検出ロジックを検証し、本番アセンブリへの適用は AppOfflineComplianceTests から行う。
/// </summary>
internal static class NetworkArtifactRules
{
    // 通信用 Win32/POSIX ネイティブライブラリ。P/Invoke 経由でこれらを呼ぶと CLR の Type 参照を介さず
    // ネットワークアクセスが可能になるため、別経路で検出する必要がある。
    private static readonly string[] ForbiddenNativeLibraryPrefixes =
    {
        "ws2_32",     // Winsock
        "wininet",    // WinINet
        "winhttp",    // WinHTTP
        "iphlpapi",   // IP Helper
        "libcurl",    // curl
        "libssh",     // SSH
        "libsystem_network",  // macOS network stack
    };

    // 外部通信を示唆する URL スキーム。文字列リテラルに混入していれば違反候補。
    // file:/// と data: と javascript: は対象外（前者はローカル、後者2つは送信に直結しない）。
    private static readonly string[] ForbiddenUrlSchemes =
    {
        "http://", "https://", "ftp://", "ftps://",
        "ws://", "wss://", "smtp://",
        "telnet://", "ldap://", "ldaps://",
    };

    public static bool IsForbiddenPInvoke(string dllName)
    {
        ArgumentNullException.ThrowIfNull(dllName);
        var normalized = dllName.ToLowerInvariant();
        return ForbiddenNativeLibraryPrefixes.Any(p => normalized.StartsWith(p, StringComparison.Ordinal));
    }

    public static bool ContainsForbiddenUrl(string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);
        var normalized = literal.ToLowerInvariant();
        return ForbiddenUrlSchemes.Any(scheme => normalized.Contains(scheme, StringComparison.Ordinal));
    }
}
