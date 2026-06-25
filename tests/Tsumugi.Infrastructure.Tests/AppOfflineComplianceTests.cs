using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// Tsumugi.App.dll が外部通信 API を直接参照しないことを CI で機械判定する（AC0-5）。
/// ProjectReference は使わず、ビルド出力 DLL を PEReader で直接読む（Avalonia 実体ロード回避）。
/// </summary>
public sealed class AppOfflineComplianceTests
{
    // 禁止 namespace 集合（このいずれかが前方一致する型はアウト）
    // "System.Net" を先頭に置くことで Dns/NetworkInformation 等も漏れなく検知する。
    // 以下の個別エントリは "System.Net" に包含されるが、意図を明示するために残す。
    private static readonly string[] ForbiddenNamespaces =
    {
        "System.Net",
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebSockets",
        "System.Net.Mail",
    };

    // 禁止具体型集合（namespace 単独では使われていなくても型単位で警戒）
    private static readonly string[] ForbiddenTypes =
    {
        "System.Net.WebClient",
        "System.Net.WebRequest",
        "System.Net.HttpWebRequest",
        "System.Net.HttpWebResponse",
        "System.Net.Http.HttpClient",
        "System.Net.Http.HttpRequestMessage",
        "System.Net.Http.HttpResponseMessage",
        "System.Net.Http.HttpMessageHandler",
        "System.Net.Sockets.Socket",
        "System.Net.Sockets.TcpClient",
        "System.Net.Sockets.UdpClient",
        "System.Net.Mail.SmtpClient",
    };

    // allowlist: 「(完全名) + 理由」の形で明示。**既定は空**。
    // 追加は根拠必須（PRレビューで合意し、本配列に直接書く）。
    // 例: ("System.Net.Sockets.AddressFamily", "Avalonia 内の P/Invoke 列挙だけで通信 API 本体は使わない"),
    private static readonly (string FullName, string Reason)[] Allowlist = Array.Empty<(string FullName, string Reason)>();

    [Fact]
    public void TsumugiApp_dll_does_not_directly_reference_network_apis()
    {
        var appDllPath = AppAssemblyLocator.LocateTsumugiAppDll();
        var referenced = AssemblyMetadataScanner.ScanReferencedTypeFullNames(appDllPath);

        var allowed = Allowlist.Select(a => a.FullName).ToHashSet(StringComparer.Ordinal);

        var hits = referenced
            .Where(name =>
                ForbiddenTypes.Contains(name, StringComparer.Ordinal)
                || ForbiddenNamespaces.Any(ns => name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal)))
            .Where(name => !allowed.Contains(name))
            .ToArray();

        hits.Should().BeEmpty(
            because: "Tsumugi.App は完全オフライン: 外部通信 API を直接参照してはならない。" +
                     "不可避な参照は AppOfflineComplianceTests.Allowlist に理由付きで明示すること。" +
                     Environment.NewLine +
                     "違反: " + string.Join(", ", hits));
    }
}

internal static class AppAssemblyLocator
{
    /// <summary>
    /// テスト bin の AppContext.BaseDirectory から Tsumugi.sln を上向き探索し、
    /// src/Tsumugi.App/bin/{Configuration}/net10.0/Tsumugi.App.dll を組み立てる。
    /// Configuration は test の bin パスから推定（Debug/Release）。
    /// </summary>
    public static string LocateTsumugiAppDll()
    {
        var baseDir = AppContext.BaseDirectory;

        // Configuration を bin パスから抽出（.../bin/Debug/net10.0/ → "Debug"）
        var configuration = ExtractConfigurationFromPath(baseDir);

        // sln ファイルを上向き探索
        var solutionRoot = FindSolutionRoot(baseDir);

        return Path.Combine(
            solutionRoot,
            "src", "Tsumugi.App", "bin", configuration, "net10.0", "Tsumugi.App.dll");
    }

    private static string ExtractConfigurationFromPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (int i = parts.Length - 2; i >= 0; i--)
        {
            if (string.Equals(parts[i], "bin", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }
        throw new InvalidOperationException(
            $"テスト実行ディレクトリから Configuration を推定できない: {path}");
    }

    private static string FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("Tsumugi.sln").Any()) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Tsumugi.sln が祖先方向に見つからない: 起点={startDir}");
    }
}
