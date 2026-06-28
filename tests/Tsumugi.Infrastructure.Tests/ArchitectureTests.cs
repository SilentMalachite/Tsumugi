using System.Linq;
using System.Reflection;
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

    // Domain は他レイヤを一切知らない（CLAUDE.md §依存方向）。
    private static readonly string[] ForbiddenForDomain =
    {
        "Tsumugi.Application",
        "Tsumugi.Infrastructure",
        "Tsumugi.Infrastructure.Reporting",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
        "QuestPDF",
    };

    // Application は Domain のみ参照（Infrastructure/UI/EF/QuestPDF を知らない）。
    private static readonly string[] ForbiddenForApplication =
    {
        "Tsumugi.Infrastructure",
        "Tsumugi.Infrastructure.Reporting",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
        "QuestPDF",
    };

    // Reporting は Application 経由でのみ受け取る。Infrastructure（永続化）/ EF / UI を知らない。
    // NOTE: Application DTO が Domain enum を露出するため、Reporting.dll は Tsumugi.Domain への
    // トランジティブな直接参照を必然的に持つ（RecordKind 等）。Phase 1 既存パターンと同様、許容する。
    private static readonly string[] ForbiddenForReporting =
    {
        "Tsumugi.Infrastructure",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
    };

    [Fact]
    public void Infrastructure_does_not_reference_ui_layers()
    {
        var asm = typeof(Tsumugi.Infrastructure.Persistence.TsumugiDbContext).Assembly;
        AssertDoesNotReference(asm, ForbiddenForInfrastructure);
    }

    [Fact]
    public void Domain_does_not_reference_outer_layers()
    {
        var asm = LocateAssembly("Tsumugi.Domain");
        AssertDoesNotReference(asm, ForbiddenForDomain);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_ui()
    {
        var asm = LocateAssembly("Tsumugi.Application");
        AssertDoesNotReference(asm, ForbiddenForApplication);
    }

    [Fact]
    public void Reporting_does_not_reference_persistence_or_ui()
    {
        var asm = LocateAssembly("Tsumugi.Infrastructure.Reporting");
        AssertDoesNotReference(asm, ForbiddenForReporting);
    }

    /// <summary>
    /// 指定アセンブリの DIRECT 参照のみを検査する（GetReferencedAssemblies はトランジティブをたどらない、
    /// open-questions.md の既知限界）。
    /// </summary>
    private static void AssertDoesNotReference(Assembly assembly, string[] forbidden)
    {
        var referencedNames = assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (var f in forbidden)
        {
            referencedNames.Should().NotContain(
                name => name == f || name.StartsWith(f + "."),
                because: $"{assembly.GetName().Name} は {f} を参照してはならない（依存方向厳守）");
        }
    }

    private static Assembly LocateAssembly(string assemblyName)
    {
        var path = TsumugiAssemblyLocator.LocateProductionDll(assemblyName);
        // LoadFile は同名アセンブリの多重ロードを許す（同テスト内の独立 Assert に必要）。
        return Assembly.LoadFile(path);
    }
}
