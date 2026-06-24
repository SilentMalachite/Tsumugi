using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenForDomain =
    {
        "Tsumugi.Application",
        "Tsumugi.Infrastructure",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
    };

    [Fact]
    public void Domain_does_not_reference_outer_layers()
    {
        // Domain 内の既知の型からアセンブリを取得（型は Task 3 で追加する Entity を使う）
        var domainAssembly = typeof(Tsumugi.Domain.Entities.Entity).Assembly;

        var referencedNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (var forbidden in ForbiddenForDomain)
        {
            referencedNames.Should().NotContain(
                name => name == forbidden || name.StartsWith(forbidden + "."),
                because: $"Domain は {forbidden} を参照してはならない（依存方向厳守）");
        }
    }
}
