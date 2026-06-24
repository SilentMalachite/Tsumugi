using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OfflineComplianceTests
{
    // オフライン厳守: 全 Tsumugi アセンブリが既知の外部通信ライブラリを参照しないこと。
    private static readonly string[] ForbiddenNetworkAssemblies =
    {
        "System.Net.Http",
        "System.Net.Sockets",
        "Microsoft.AspNetCore",
        "Grpc",
    };

    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    public void Tsumugi_assemblies_do_not_reference_network_libraries(string assemblyName)
    {
        var asm = Assembly.Load(assemblyName);
        var referenced = asm.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();

        foreach (var forbidden in ForbiddenNetworkAssemblies)
        {
            referenced.Should().NotContain(
                n => n == forbidden || n.StartsWith(forbidden + "."),
                because: $"{assemblyName} は外部通信ライブラリ {forbidden} を参照してはならない（オフライン厳守）");
        }
    }
}
