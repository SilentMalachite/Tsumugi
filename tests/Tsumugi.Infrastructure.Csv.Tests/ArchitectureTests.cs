using System.Reflection;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.Tests;

/// <summary>
/// <c>Tsumugi.Infrastructure.Csv</c> は Application / Domain だけを参照する。
/// 永続化（EF Core）・UI（Avalonia）・もう一方の Infrastructure 実装を知らない。
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly string[] Forbidden =
    [
        "Tsumugi.Infrastructure",
        "Tsumugi.Infrastructure.Reporting",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
        "QuestPDF",
    ];

    [Fact]
    public void Infrastructure_csv_does_not_reference_persistence_reporting_or_ui()
    {
        // NOTE: GetReferencedAssemblies() は直接参照のみを返す（推移閉包は対象外）。
        var referenced = typeof(CsvSpecificationCatalog).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        foreach (var forbidden in Forbidden)
        {
            referenced.Should().NotContain(
                name => name == forbidden
                    || name.StartsWith(forbidden + ".", StringComparison.Ordinal),
                because: $"Infrastructure.Csv は {forbidden} を参照してはならない（依存方向厳守）");
        }
    }

    [Fact]
    public void Csv_writer_and_generator_live_in_the_same_assembly_as_the_specification_catalog()
    {
        var expected = typeof(CsvSpecificationCatalog).Assembly;

        typeof(ClaimCsvWriter).Assembly.Should().BeSameAs(expected);
        typeof(CsvCellEncoder).Assembly.Should().BeSameAs(expected);
        typeof(ClaimCsvGenerator).Assembly.Should().BeSameAs(expected);
        typeof(CsvGeneratorRuleParser).Assembly.Should().BeSameAs(expected);
    }

    // NOTE(teeth): CSV 生成器は Application 抽象 IClaimCsvGenerator の実装であり続ける。
    // Application 側が Infrastructure.Csv を直接参照する設計へ戻ると、
    // Tsumugi.Application.Tests.ArchitectureTests が RED になる。
    [Fact]
    public void Claim_csv_generator_implements_the_application_abstraction()
    {
        typeof(ClaimCsvGenerator).Should()
            .Implement<Tsumugi.Application.Abstractions.IClaimCsvGenerator>();
    }

    [Fact]
    public void Infrastructure_csv_does_not_reference_network_libraries()
    {
        string[] forbiddenNetworkAssemblies =
        [
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Net.WebSockets",
            "System.Net.Requests",
            "Microsoft.AspNetCore",
            "Grpc.Net.Client",
            "MailKit",
        ];

        var referenced = typeof(CsvSpecificationCatalog).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        referenced.Should().NotIntersectWith(forbiddenNetworkAssemblies);
    }

    [Fact]
    public void Public_csv_surface_stays_inside_the_csv_namespaces()
    {
        var publicTypes = typeof(CsvSpecificationCatalog).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace is not null)
            .ToArray();

        publicTypes.Should().OnlyContain(type =>
            type.Namespace!.StartsWith("Tsumugi.Infrastructure.Csv", StringComparison.Ordinal));
        publicTypes.Should().NotBeEmpty();
    }

    [Fact]
    public void Assembly_marker_is_present_for_composition_root_scanning()
    {
        typeof(CsvSpecificationCatalog).Assembly.GetName().Name.Should().Be("Tsumugi.Infrastructure.Csv");
        Assembly.GetAssembly(typeof(ClaimCsvGenerator)).Should().NotBeNull();
    }
}
