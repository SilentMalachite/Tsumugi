using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OfflineComplianceTests
{
    // オフライン厳守: 全 Tsumugi アセンブリが既知の外部通信ライブラリを参照しないこと。
    // "System.Net" プレフィックスで Dns/NetworkInformation/Primitives 等を漏れなく捕捉する。
    private static readonly string[] ForbiddenNetworkAssemblies =
    {
        "System.Net",
        "System.Net.Http",
        "System.Net.Sockets",
        "Microsoft.AspNetCore",
        "Grpc",
    };

    // allowlist: (AssemblyNamePrefix, Reason) 形式。**既定は空**。
    // 追加は根拠必須（「通信しない理由」を具体的に記述。空 Reason は CI 違反扱い）。
    private static readonly (string AssemblyNamePrefix, string Reason)[] AssemblyAllowlist =
        Array.Empty<(string, string)>();

    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    public void Tsumugi_assemblies_do_not_reference_network_libraries(string assemblyName)
    {
        // NOTE: GetReferencedAssemblies() returns DIRECT references only; transitive references are not walked (tracked in docs/open-questions.md).
        var asm = Assembly.Load(assemblyName);
        var referenced = asm.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();

        // allowlist に理由のない要素は設計違反として即失敗させる
        var emptyReasonEntries = AssemblyAllowlist.Where(e => string.IsNullOrWhiteSpace(e.Reason)).ToArray();
        emptyReasonEntries.Should().BeEmpty(
            because: "AssemblyAllowlist の各エントリは Reason を必須とする（空文字列・空白禁止）");

        var allowedPrefixes = AssemblyAllowlist.Select(e => e.AssemblyNamePrefix).ToArray();

        foreach (var forbidden in ForbiddenNetworkAssemblies)
        {
            var violations = referenced
                .Where(n => n == forbidden || n.StartsWith(forbidden + ".", StringComparison.Ordinal))
                .Where(n => !allowedPrefixes.Any(p => n == p || n.StartsWith(p + ".", StringComparison.Ordinal)))
                .ToArray();

            violations.Should().BeEmpty(
                because: $"{assemblyName} は外部通信ライブラリ {forbidden} を参照してはならない（オフライン厳守）。" +
                         "不可避な参照は OfflineComplianceTests.AssemblyAllowlist に理由付きで明示すること。" +
                         Environment.NewLine +
                         "違反: " + string.Join(", ", violations));
        }
    }
}
