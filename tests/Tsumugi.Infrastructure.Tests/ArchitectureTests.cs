using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenForInfrastructure =
    {
        "Tsumugi.App",
        "Avalonia",
        // Microsoft.EntityFrameworkCore は許可
    };

    [Fact]
    public void Infrastructure_does_not_reference_ui_layers()
    {
        var infrastructureAssembly = typeof(Tsumugi.Infrastructure.Persistence.TsumugiDbContext).Assembly;

        // NOTE: GetReferencedAssemblies() returns DIRECT references only; transitive references are not walked (tracked in docs/open-questions.md).
        var referencedNames = infrastructureAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (var forbidden in ForbiddenForInfrastructure)
        {
            referencedNames.Should().NotContain(
                name => name == forbidden || name.StartsWith(forbidden + "."),
                because: $"Infrastructure は {forbidden} を参照してはならない（依存方向厳守）");
        }
    }
}
