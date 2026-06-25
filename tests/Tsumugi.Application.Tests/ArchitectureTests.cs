using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenForApplication =
    {
        "Tsumugi.Infrastructure",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",  // Application は EF Core を知らない（抽象経由のみ）
    };

    [Fact]
    public void Application_does_not_reference_outer_layers()
    {
        var applicationAssembly = typeof(Tsumugi.Application.UseCases.RegisterOfficeUseCase).Assembly;

        // NOTE: GetReferencedAssemblies() returns DIRECT references only; transitive references are not walked (tracked in docs/open-questions.md).
        var referencedNames = applicationAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (var forbidden in ForbiddenForApplication)
        {
            referencedNames.Should().NotContain(
                name => name == forbidden || name.StartsWith(forbidden + "."),
                because: $"Application は {forbidden} を参照してはならない（依存方向厳守）");
        }
    }
}
