using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;

namespace Tsumugi.Infrastructure.Tests;

public sealed class ClaimSpecificationBoundaryTests
{
    [Fact]
    public void Production_source_keeps_external_specification_literals_in_their_catalogs()
    {
        var violations = ExternalSpecificationLiteralGuard.ScanProduction();

        violations.Should().BeEmpty(
            because: "報酬告示・CSV仕様値は指定JSON領域だけに置くこと。" +
                     Environment.NewLine +
                     "違反: " + string.Join(Environment.NewLine, violations.Select(v => v.ToString())));
    }

    [Fact]
    public void Scan_detects_nested_master_number_literal_in_claim_domain_source()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/LeakedUnits.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class LeakedUnits { private const int Units = 777; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "777");

        violation.RelativePath.Should().Be("src/Tsumugi.Domain/Logic/Claim/LeakedUnits.cs");
        violation.LineNumber.Should().Be(1);
        violation.CatalogPath.Should().EndWith("#/entries/0/values/nested/thresholds/1");
        violation.ToString().Should().Contain(violation.RelativePath + ":1").And.Contain(violation.CatalogPath);
    }

    [Fact]
    public void Scan_detects_nested_master_string_literal_in_application_source()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Application/Claims/LeakedCode.cs",
            "namespace Tsumugi.Application.Claims; internal static class LeakedCode { private const string Code = \"NESTED-CODE\"; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "NESTED-CODE");

        violation.RelativePath.Should().Be("src/Tsumugi.Application/Claims/LeakedCode.cs");
        violation.CatalogPath.Should().EndWith("#/entries/0/values/nested/code");
    }

    [Fact]
    public void Scan_detects_master_number_literal_inside_interpolation_hole()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/InterpolatedUnits.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class InterpolatedUnits { " +
            "internal static string Value => $\"{777}\"; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "777");

        violation.RelativePath.Should().Be("src/Tsumugi.Domain/Logic/Claim/InterpolatedUnits.cs");
        violation.LineNumber.Should().Be(1);
    }

    [Fact]
    public void Scan_detects_master_string_literal_inside_interpolation_hole()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Application/Claims/InterpolatedCode.cs",
            "namespace Tsumugi.Application.Claims; internal static class InterpolatedCode { " +
            "internal static string Value => $\"{Echo(\"NESTED-CODE\")}\"; " +
            "private static string Echo(string value) => value; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "NESTED-CODE");

        violation.RelativePath.Should().Be("src/Tsumugi.Application/Claims/InterpolatedCode.cs");
        violation.LineNumber.Should().Be(1);
    }

    [Fact]
    public void Scan_detects_all_csv_boundary_token_kinds_outside_infrastructure_csv()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Infrastructure/Claims/LeakedCsvTokens.cs",
            "namespace Tsumugi.Infrastructure.Claims; internal static class LeakedCsvTokens { " +
            "private const string Field = \"csv:field:001\"; private const string Exchange = \"J999\"; " +
            "private const string Record = \"42\"; }");

        var violations = fixture.Scan()
            .Where(v => v.Rule == "csv-specification-literal")
            .OrderBy(v => v.Literal, StringComparer.Ordinal)
            .ToArray();

        violations.Select(v => v.Literal).Should().Equal("42", "J999", "csv:field:001");
        violations.Should().OnlyContain(v => v.CatalogPath.Contains("#/records/0/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, "<missing>")]
    [InlineData("unknown-source", "unknown-source")]
    public void Scan_detects_missing_or_unknown_master_source_reference(
        string? sourceDocumentId,
        string expectedLiteral)
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteMaster(sourceDocumentId);

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-source");

        violation.RelativePath.Should().Be(SpecificationFixture.MasterPath);
        violation.LineNumber.Should().BeGreaterThan(0);
        violation.Literal.Should().Be(expectedLiteral);
        violation.CatalogPath.Should().Be(SpecificationFixture.MasterPath + "#/entries/0/sourceDocumentId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scan_detects_missing_or_blank_master_effective_from(string? effectiveFrom)
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteMaster("known-source", effectiveFrom);

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-effective-from");

        violation.RelativePath.Should().Be(SpecificationFixture.MasterPath);
        violation.LineNumber.Should().BeGreaterThan(0);
        violation.CatalogPath.Should().Be(SpecificationFixture.MasterPath + "#/entries/0/effectiveFrom");
    }

    [Fact]
    public void Scan_detects_claim_master_and_csv_specification_outside_allowed_directories()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write("src/Tsumugi.Domain/Catalogs/claim-master.json", SpecificationFixture.MasterJson);
        fixture.Write("src/Tsumugi.Application/Catalogs/csv-specification.json", SpecificationFixture.CsvJson);

        var violations = fixture.Scan()
            .Where(v => v.Rule is "claim-master-location" or "csv-specification-location")
            .OrderBy(v => v.Rule, StringComparer.Ordinal)
            .ToArray();

        violations.Select(v => v.Rule).Should().Equal("claim-master-location", "csv-specification-location");
        violations.Should().OnlyContain(v => v.LineNumber > 0 && v.CatalogPath.Contains('#', StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_ignores_comments_excluded_paths_unrelated_literals_and_allowed_catalog_consumers()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/AllowedExamples.cs",
            "namespace Tsumugi.Domain.Logic.Claim;\n" +
            "internal static class AllowedExamples\n" +
            "{\n" +
            "    // 777 and \"NESTED-CODE\" are documentation only.\n" +
            "    /* 777 and \"CLAIM-CODE\" are block-comment examples. */\n" +
            "    private const int Year = 2026;\n" +
            "    private const int Buffer = 4096;\n" +
            "    private const decimal ExistingWage = 88m;\n" +
            "    private const int OneDigitCatalogValue = 9;\n" +
            "    private const int NumericSubstring = 7770;\n" +
            "    private const string StringPrefix = \"NESTED-CODE-SUFFIX\";\n" +
            "    private const string Url = \"https://example.test/777\";\n" +
            "}\n");
        fixture.Write(
            "src/Tsumugi.Infrastructure.Csv/AllowedCsvTokens.cs",
            "internal static class AllowedCsvTokens { private const string A = \"csv:field:001\"; " +
            "private const string B = \"J999\"; private const string C = \"42\"; }");
        fixture.Write("src/Other/obj/Leak.cs", "internal class Leak { const int V = 777; }");
        fixture.Write("src/Other/bin/Leak.cs", "internal class Leak { const string V = \"NESTED-CODE\"; }");
        fixture.Write("src/Other/Migrations/Leak.cs", "internal class Leak { const int V = 777; }");
        fixture.Write("tests/Leak.cs", "internal class Leak { const int V = 777; }");

        fixture.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_fails_closed_for_invalid_json_and_names_the_file()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write("src/Tsumugi.Domain/Catalogs/broken.json", "{ not-json }");

        var violation = Assert.Single(fixture.Scan(), v => v.Rule == "json-parse");

        violation.RelativePath.Should().Be("src/Tsumugi.Domain/Catalogs/broken.json");
        violation.LineNumber.Should().BeGreaterThan(0);
        violation.ToString().Should().Contain("broken.json");
    }

    private sealed class SpecificationFixture : IDisposable
    {
        public const string MasterPath =
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/fixture-master.json";

        private const string SourcesPath =
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json";

        private const string CsvPath =
            "src/Tsumugi.Infrastructure.Csv/Specifications/fixture-csv.json";

        public const string MasterJson =
            "{\n" +
            "  \"schemaVersion\": \"1\",\n" +
            "  \"masterKind\": \"fixture\",\n" +
            "  \"entries\": [\n" +
            "    {\n" +
            "      \"effectiveFrom\": \"2026-06\",\n" +
            "      \"sourceDocumentId\": \"known-source\",\n" +
            "      \"values\": {\n" +
            "        \"units\": 123,\n" +
            "        \"serviceCode\": \"CLAIM-CODE\",\n" +
            "        \"nested\": { \"thresholds\": [9, 777], \"code\": \"NESTED-CODE\" }\n" +
            "      }\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";

        public const string CsvJson =
            "{\n" +
            "  \"schemaVersion\": 1,\n" +
            "  \"specificationVersion\": \"fixture\",\n" +
            "  \"records\": [\n" +
            "    {\n" +
            "      \"fieldId\": \"csv:field:001\",\n" +
            "      \"exchangeInformationId\": \"J999\",\n" +
            "      \"innerRecordType\": \"42\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n";

        private const string SourcesJson =
            "{\n" +
            "  \"schemaVersion\": \"1\",\n" +
            "  \"sources\": [\n" +
            "    { \"documentId\": \"known-source\" }\n" +
            "  ]\n" +
            "}\n";

        public SpecificationFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"tsumugi-spec-guard-{Guid.NewGuid():N}");
            Write(MasterPath, MasterJson);
            Write(SourcesPath, SourcesJson);
            Write(CsvPath, CsvJson);
        }

        private string Root { get; }

        public IReadOnlyList<Violation> Scan() => ExternalSpecificationLiteralGuard.Scan(Root);

        public void WriteMaster(string? sourceDocumentId, string? effectiveFrom = "2026-06")
        {
            var effectiveFromProperty = effectiveFrom is null
                ? string.Empty
                : "      \"effectiveFrom\": \"" + effectiveFrom + "\",\n";
            var sourceProperty = sourceDocumentId is null
                ? string.Empty
                : "      \"sourceDocumentId\": \"" + sourceDocumentId + "\",\n";

            Write(
                MasterPath,
                "{\n" +
                "  \"schemaVersion\": \"1\",\n" +
                "  \"masterKind\": \"fixture\",\n" +
                "  \"entries\": [\n" +
                "    {\n" +
                effectiveFromProperty +
                sourceProperty +
                "      \"values\": { \"units\": 123 }\n" +
                "    }\n" +
                "  ]\n" +
                "}\n");
        }

        public void Write(string relativePath, string contents)
        {
            var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
