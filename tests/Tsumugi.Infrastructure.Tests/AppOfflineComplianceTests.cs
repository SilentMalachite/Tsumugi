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
        var appDllPath = TsumugiAssemblyLocator.LocateProductionDll("Tsumugi.App");
        var referenced = AssemblyMetadataScanner.ScanReferencedTypeFullNames(appDllPath);

        // 空 Reason の allowlist 追加を CI で失格させる（OfflineComplianceTests の AssemblyAllowlist と方針統一）。
        var emptyReasonEntries = Allowlist
            .Where(a => string.IsNullOrWhiteSpace(a.Reason))
            .Select(a => a.FullName)
            .ToArray();
        emptyReasonEntries.Should().BeEmpty(
            because: "Allowlist エントリには根拠を Reason に記すこと（空文字禁止）。"
                     + Environment.NewLine
                     + "Reason 空: " + string.Join(", ", emptyReasonEntries));

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

    // P/Invoke 経由のネットワーク呼び出しも禁止（CLR の TypeRef を介さずに通信できる経路）。
    // 既定の allowlist は空。やむを得ない場合のみ、(DllName, Reason) を埋めて明示する。
    // 設計メモ: UrlLiteralAllowlist と異なりアセンブリ単位のスコープ化はまだ導入していない
    // （Task 9.5 時点でエントリ 0 件のため必要性なし）。将来 P/Invoke の許可が必要になった際は
    // UrlLiteralAllowlist と同様に (DllName, AssemblyName?, Reason) へ拡張し、
    // .Where(e => e.AssemblyName is null || ...) の絞り込みを追加すること。
    private static readonly (string DllName, string Reason)[] PInvokeAllowlist =
        Array.Empty<(string, string)>();

    [Theory]
    [InlineData("Tsumugi.App")]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    [InlineData("Tsumugi.Infrastructure.Reporting")]
    public void Tsumugi_assemblies_do_not_pinvoke_into_network_native_libraries(string assemblyName)
    {
        var dll = TsumugiAssemblyLocator.LocateProductionDll(assemblyName);
        var imports = AssemblyMetadataScanner.ScanPInvokeImports(dll);

        var allowed = PInvokeAllowlist
            .Where(e => !string.IsNullOrWhiteSpace(e.Reason))
            .Select(e => e.DllName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var violations = imports
            .Where(i => NetworkArtifactRules.IsForbiddenPInvoke(i.DllName))
            .Where(i => !allowed.Contains(i.DllName))
            .ToArray();

        violations.Should().BeEmpty(
            because: $"{assemblyName} は通信系ネイティブライブラリへ P/Invoke してはならない。" +
                     Environment.NewLine +
                     "違反: " + string.Join(", ", violations.Select(v => $"{v.DllName}!{v.MethodName}")));
    }

    // URL リテラル混入（http:// / https:// / ftp:// 等）も禁止。
    // ヒット時はソースを修正するか、本配列に (UrlPrefix, AssemblyName, Reason) を追加すること。
    // AssemblyName は任意（nullable）: null の場合は全 5 アセンブリに対して該当スキームを許可する
    // （危険なため既定では使用しないこと）。特定のアセンブリ名を指定した場合は、
    // そのアセンブリの検査時のみ該当スキームを許可し、他のアセンブリでは引き続き検出対象とする。
    private static readonly (string UrlPrefix, string? AssemblyName, string Reason)[] UrlLiteralAllowlist =
        new (string UrlPrefix, string? AssemblyName, string Reason)[]
        {
            ("http://",
             "Tsumugi.Infrastructure.Reporting",
             "Noto Sans JP (assets/fonts/) の font `name` テーブルに埋込済の Copyright URL " +
             "`http://www.adobe.com/`。SIL OFL 1.1 帰属要件により font metadata は改変不可、" +
             "また Reporting アセンブリはネットワーク通信を一切行わない (System.Net.* 参照なし)。" +
             "URL 混入は文字列リテラルとしてスキャンで検出されるが実害なし。"),
        };

    [Theory]
    [InlineData("Tsumugi.App")]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    [InlineData("Tsumugi.Infrastructure.Reporting")]
    public void Tsumugi_assemblies_do_not_embed_external_url_literals(string assemblyName)
    {
        var dll = TsumugiAssemblyLocator.LocateProductionDll(assemblyName);

        var allowed = UrlLiteralAllowlist
            .Where(e => !string.IsNullOrWhiteSpace(e.Reason))
            .Where(e => e.AssemblyName is null || string.Equals(e.AssemblyName, assemblyName, StringComparison.Ordinal))
            .Select(e => e.UrlPrefix.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        // 禁止スキーム集合は NetworkArtifactRules に一元化。検索は ASCII 大小無視で行うため、
        // "Https://" や "HTTP://" のような混在ケースも漏れなく拾う。
        var hits = NetworkArtifactRules.ForbiddenUrlSchemes
            .Where(scheme => !allowed.Contains(scheme.ToLowerInvariant()))
            .Where(scheme => AssemblyMetadataScanner.ContainsRawUtf16SubstringIgnoreCase(dll, scheme))
            .ToArray();

        hits.Should().BeEmpty(
            because: $"{assemblyName} に外部通信 URL リテラルが埋め込まれている可能性。" +
                     "ソースから除去するか、UrlLiteralAllowlist に理由付きで明示すること。" +
                     Environment.NewLine +
                     "検出: " + string.Join(", ", hits));
    }
}

internal static class TsumugiAssemblyLocator
{
    /// <summary>
    /// テスト bin の AppContext.BaseDirectory から Tsumugi.sln を上向き探索し、
    /// src/{assemblyName}/bin/{Configuration}/net10.0/{assemblyName}.dll を組み立てる。
    /// Configuration は test の bin パスから推定（Debug/Release）。
    /// </summary>
    public static string LocateProductionDll(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyName);
        var baseDir = AppContext.BaseDirectory;
        var configuration = ExtractConfigurationFromPath(baseDir);
        var solutionRoot = FindSolutionRoot(baseDir);

        return Path.Combine(
            solutionRoot, "src", assemblyName, "bin", configuration, "net10.0", $"{assemblyName}.dll");
    }

    public static string FindSolutionRoot() => FindSolutionRoot(AppContext.BaseDirectory);

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
