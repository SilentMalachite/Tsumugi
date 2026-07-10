using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenForInfrastructure =
    {
        "Tsumugi.Infrastructure.Csv",
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

    // Csv は仕様解釈と生成に限定し、永続化・PDF・UI・各フレームワークを知らない。
    private static readonly string[] ForbiddenForCsv =
    {
        "Tsumugi.Infrastructure",
        "Tsumugi.Infrastructure.Reporting",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
        "QuestPDF",
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

    [Fact]
    public void Csv_does_not_reference_persistence_reporting_or_ui()
    {
        var asm = LocateAssembly("Tsumugi.Infrastructure.Csv");
        AssertDoesNotReference(asm, ForbiddenForCsv);
        AssertReferencesOnlyBclApplicationAndDomain(asm);
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

    private static void AssertReferencesOnlyBclApplicationAndDomain(Assembly assembly)
    {
        var unexpectedReferences = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name =>
                name is not "Tsumugi.Application" and not "Tsumugi.Domain"
                && name is not "mscorlib" and not "netstandard" and not "Microsoft.CSharp"
                && name is not "System"
                && !name.StartsWith("System.", StringComparison.Ordinal)
                && !name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal))
            .ToArray();

        unexpectedReferences.Should().BeEmpty(
            because: "Csv は BCL、Tsumugi.Application、Tsumugi.Domain だけを参照できる");
    }

    private static Assembly LocateAssembly(string assemblyName)
    {
        var path = TsumugiAssemblyLocator.LocateProductionDll(assemblyName);
        File.Exists(path).Should().BeTrue(
            because: $"{assemblyName} をアーキテクチャ検査対象としてビルドできなければならない");
        // LoadFile は同名アセンブリの多重ロードを許す（同テスト内の独立 Assert に必要）。
        return Assembly.LoadFile(path);
    }
}

internal static class InlineDataCoverage
{
    internal static readonly string[] NonUiProductionAssemblies =
    {
        "Tsumugi.Domain",
        "Tsumugi.Application",
        "Tsumugi.Infrastructure",
        "Tsumugi.Infrastructure.Reporting",
        "Tsumugi.Infrastructure.Csv",
    };

    internal static readonly string[] AllProductionAssemblies =
    {
        "Tsumugi.App",
        "Tsumugi.Domain",
        "Tsumugi.Application",
        "Tsumugi.Infrastructure",
        "Tsumugi.Infrastructure.Reporting",
        "Tsumugi.Infrastructure.Csv",
    };

    internal static void AssertMethodCovers(
        Type declaringType,
        string methodName,
        string[] expectedAssemblies)
    {
        var method = declaringType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        method.Should().NotBeNull(because: $"{declaringType.Name}.{methodName} が存在する必要がある");

        var rows = method!
            .GetCustomAttributes<InlineDataAttribute>()
            .SelectMany(attribute => attribute.GetData(method))
            .ToArray();

        rows.Should().HaveCount(
            expectedAssemblies.Length,
            because: $"{declaringType.Name}.{methodName} は対象アセンブリを漏れなく1回ずつ検査する必要がある");
        rows.Should().OnlyContain(
            row => row.Length == 1 && row[0] is string,
            because: "アセンブリ検査の InlineData は単一のアセンブリ名で構成する");

        var actualAssemblies = rows.Select(row => (string)row[0]).ToArray();
        actualAssemblies.Should().BeEquivalentTo(
            expectedAssemblies,
            options => options.WithoutStrictOrdering(),
            because: $"{declaringType.Name}.{methodName} の検査対象から本番アセンブリを外してはならない");
    }
}
