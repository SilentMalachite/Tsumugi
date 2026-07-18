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

    [Theory]
    [InlineData("1e20", "1e20")]
    [InlineData("1e20", "10e19")]
    [InlineData("1e20", "1_0.0_0e1_9F")]
    [InlineData("1e+020", "0010.00e+0_19D")]
    [InlineData("10.00", "1e1")]
    [InlineData("-1e20", "-100000000000000000000m")]
    [InlineData("1e300", "10e299D")]
    [InlineData("16", "0x10UL")]
    [InlineData("16", "0b1_0000")]
    [InlineData("1000", "0x3E8UL")]
    public void Scan_detects_equivalent_master_number_literal_spellings(
        string jsonNumber,
        string csharpLiteral)
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteMasterNumber(jsonNumber);
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/EquivalentNumber.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class EquivalentNumber { " +
            "internal static object Value => " + csharpLiteral + "; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == jsonNumber);

        violation.LineNumber.Should().Be(1);
    }

    [Fact]
    public void Scan_does_not_match_non_equivalent_exponent_number()
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteMasterNumber("1e20");
        fixture.Write(
            "src/Tsumugi.Application/Claims/DifferentExponent.cs",
            "namespace Tsumugi.Application.Claims; internal static class DifferentExponent { " +
            "internal static double Value => 1e21; }");

        fixture.Scan().Should().NotContain(
            v => v.Rule == "claim-master-literal" && v.Literal == "1e20");
    }

    [Theory]
    [InlineData("9.99999999999999999999999999999")]
    [InlineData("-9.99999999999999999999999999999")]
    public void Scan_ignores_master_number_below_ten_even_when_double_rounds_to_ten(string number)
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteMasterNumber(number);
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/BelowTen.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class BelowTen { " +
            "internal static double Value => " + number + "; }");

        fixture.Scan().Should().NotContain(
            v => v.Rule == "claim-master-literal" && v.Literal == number);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("-10")]
    public void Scan_detects_master_number_at_exact_absolute_ten(string number)
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteMasterNumber(number);
        fixture.Write(
            "src/Tsumugi.Application/Claims/ExactTen.cs",
            "namespace Tsumugi.Application.Claims; internal static class ExactTen { " +
            "internal static int Value => " + number + "; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == number);

        violation.LineNumber.Should().Be(1);
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

    [Theory]
    [InlineData("\"\"\"NESTED-CODE\"\"\"")]
    [InlineData("\"NESTED-CODE\"u8")]
    [InlineData("\"\"\"NESTED-CODE\"\"\"u8")]
    public void Scan_detects_raw_and_utf8_master_string_literal_tokens(string literal)
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Application/Claims/CompilerStringTokens.cs",
            "namespace Tsumugi.Application.Claims; internal static class CompilerStringTokens { " +
            "internal static object Value => " + literal + "; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "NESTED-CODE");

        violation.LineNumber.Should().Be(1);
    }

    [Fact]
    public void Scan_detects_master_literals_in_trace_conditional_branch()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Application/Claims/TraceConditional.cs",
            """
            namespace Tsumugi.Application.Claims;
            internal static class TraceConditional
            {
            #if TRACE
                private const int Units = 777;
                private const string Code = "NESTED-CODE";
            #endif
            }
            """);

        var violations = fixture.Scan()
            .Where(v => v.Rule == "claim-master-literal")
            .OrderBy(v => v.LineNumber)
            .ToArray();

        violations.Select(v => (v.Literal, v.LineNumber)).Should().Equal(
            ("777", 5),
            ("NESTED-CODE", 6));
    }

    [Theory]
    [InlineData("#if NEVER_DEFINED\n    private const int Units = 777;\n#endif", 5)]
    [InlineData("#if NEVER_DEFINED\n#if ALSO_NEVER_DEFINED\n    private const int Units = 777;\n#endif\n#endif", 6)]
    public void Scan_detects_master_literal_in_unknown_conditional_branch(
        string conditionalBranch,
        int expectedLineNumber)
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/UnknownConditional.cs",
            "namespace Tsumugi.Domain.Logic.Claim;\n" +
            "internal static class UnknownConditional\n" +
            "{\n" +
            conditionalBranch + "\n" +
            "}\n");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "777");

        violation.LineNumber.Should().Be(expectedLineNumber);
    }

    [Fact]
    public void Scan_fails_closed_for_malformed_literal_in_disabled_conditional_branch()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/BrokenConditional.cs",
            """
            namespace Tsumugi.Domain.Logic.Claim;
            internal static class BrokenConditional
            {
            #if NEVER_DEFINED
                private const string Code = "unterminated;
            #endif
            }
            """);

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "csharp-parse");

        violation.RelativePath.Should().Be("src/Tsumugi.Domain/Logic/Claim/BrokenConditional.cs");
        violation.LineNumber.Should().Be(5);
        violation.Literal.Should().StartWith("CS");
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
    public void Scan_ignores_master_numbers_used_only_as_interpolation_format()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/InterpolatedFormat.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class InterpolatedFormat { " +
            "internal static string Value => $\"{42:777}\"; }");

        fixture.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_ignores_format_after_nullable_generic_expression()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Application/Claims/NullableGenericFormat.cs",
            "namespace Tsumugi.Application.Claims; internal static class NullableGenericFormat { " +
            "internal static string Value => $\"{Echo<string?>(\"SAFE\"):777}\"; }");

        fixture.Scan().Should().BeEmpty();
    }

    [Fact]
    public void Scan_detects_master_number_used_as_interpolation_alignment()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/InterpolatedAlignment.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class InterpolatedAlignment { " +
            "internal static string Value => $\"{42,777}\"; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "777");

        violation.LineNumber.Should().Be(1);
    }

    [Fact]
    public void Scan_detects_master_string_inside_generic_call_in_interpolation_hole()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Application/Claims/InterpolatedGeneric.cs",
            "namespace Tsumugi.Application.Claims; internal static class InterpolatedGeneric { " +
            "internal static string Value => $\"{Echo<int, string>(\"NESTED-CODE\")}\"; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "NESTED-CODE");

        violation.LineNumber.Should().Be(1);
    }

    [Theory]
    [InlineData("(true ? 777 : 0):000")]
    [InlineData("new[] { 777, 1 }[0]:000")]
    [InlineData("Echo(\"a:b,c\", 777):000")]
    [InlineData("(':' == ':' ? 777 : 0):000")]
    [InlineData("matrix?[0, 777]:000")]
    public void Scan_keeps_literals_inside_interpolation_expression_delimiters(string hole)
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/InterpolatedExpression.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal static class InterpolatedExpression { " +
            "internal static string Value => $\"{" + hole + "}\"; }");

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-literal" && v.Literal == "777");

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
    [InlineData("", "<empty>")]
    [InlineData("unknown-source", "unknown-source")]
    public void Scan_detects_missing_empty_or_unknown_master_source_reference(
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
        violation.CatalogPath.Should().Be(
            SpecificationFixture.MasterPath +
            (string.IsNullOrEmpty(sourceDocumentId)
                ? "#/entries/0/sourceRefs"
                : "#/entries/0/sourceRefs/0/documentId"));
    }

    [Theory]
    [InlineData(null, "<missing>")]
    [InlineData("", "<empty>")]
    [InlineData("unknown-source", "unknown-source")]
    public void Scan_detects_missing_empty_or_unknown_condition_source_reference(
        string? sourceDocumentId,
        string expectedLiteral)
    {
        using var fixture = new SpecificationFixture();
        fixture.WriteCondition(sourceDocumentId);

        var violation = Assert.Single(
            fixture.Scan(),
            v => v.Rule == "claim-master-source");

        violation.RelativePath.Should().Be(SpecificationFixture.MasterPath);
        violation.LineNumber.Should().BeGreaterThan(0);
        violation.Literal.Should().Be(expectedLiteral);
        violation.CatalogPath.Should().Be(
            SpecificationFixture.MasterPath +
            (string.IsNullOrEmpty(sourceDocumentId)
                ? "#/conditionDefinitions/0/sourceRefs"
                : "#/conditionDefinitions/0/sourceRefs/0/documentId"));
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
    public void Scan_suppresses_only_the_exact_known_coincidental_literal_match()
    {
        // Mirrors the real ClaimAuditEntryFactory.cs:40 exemption: ADR 0026 fixes the
        // audit summary-length guard at 512 to match AuditEntryConfiguration's
        // HasMaxLength(512) DB column, and ADR 0027 separately fixes a basic-reward
        // baseUnits of 512. The allowlist must suppress only that exact
        // (path, line, literal) triple, not the whole file or every 512 literal.
        using var fixture = new SpecificationFixture();
        fixture.WriteMasterNumber("512");

        var exemptFileLines = Enumerable.Range(1, 34)
            .Select(_ => "        // padding")
            .Append("        if (summary.Length > 512) throw new InvalidOperationException();")
            .Append("        if (summary.Length > 512) throw new InvalidOperationException();");
        fixture.Write(
            "src/Tsumugi.Application/Audit/ClaimAuditEntryFactory.cs",
            "namespace Tsumugi.Application.Audit;\n" +
            "internal static class ClaimAuditEntryFactory\n{\n" +
            "    private static void Check(string summary)\n    {\n" +
            string.Join('\n', exemptFileLines) +
            "\n    }\n}\n");
        fixture.Write(
            "src/Tsumugi.Application/Audit/OtherFile.cs",
            "namespace Tsumugi.Application.Audit; internal static class OtherFile { " +
            "internal static int Threshold => 512; }");

        var violations = fixture.Scan()
            .Where(v => v.Rule == "claim-master-literal")
            .ToArray();

        // Line 40 (the exact known exemption) is suppressed; line 41 in the very same
        // file (same literal, different line) and the unrelated OtherFile.cs are not.
        violations.Should().NotContain(v =>
            v.RelativePath == "src/Tsumugi.Application/Audit/ClaimAuditEntryFactory.cs"
            && v.LineNumber == 40);
        violations.Should().Contain(v =>
            v.RelativePath == "src/Tsumugi.Application/Audit/ClaimAuditEntryFactory.cs"
            && v.LineNumber == 41
            && v.Literal == "512");
        violations.Should().Contain(v =>
            v.RelativePath == "src/Tsumugi.Application/Audit/OtherFile.cs"
            && v.Literal == "512");
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

    [Fact]
    public void Scan_fails_closed_for_invalid_csharp_and_names_the_file()
    {
        using var fixture = new SpecificationFixture();
        fixture.Write(
            "src/Tsumugi.Domain/Logic/Claim/Broken.cs",
            "namespace Tsumugi.Domain.Logic.Claim; internal class Broken { string Value = \"unterminated; }");

        var violations = fixture.Scan()
            .Where(v => v.Rule == "csharp-parse")
            .ToArray();

        violations.Should().NotBeEmpty()
            .And.OnlyContain(v => v.RelativePath == "src/Tsumugi.Domain/Logic/Claim/Broken.cs");
        violations.Should().OnlyContain(v => v.LineNumber > 0 && v.Literal.StartsWith("CS", StringComparison.Ordinal));
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
            "  \"schemaVersion\": \"2\",\n" +
            "  \"masterKind\": \"fixture\",\n" +
            "  \"entries\": [\n" +
            "    {\n" +
            "      \"effectiveFrom\": \"2026-06\",\n" +
            "      \"sourceRefs\": [{ \"documentId\": \"known-source\" }],\n" +
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

        public void WriteMasterNumber(string number)
        {
            Write(
                MasterPath,
                "{\n" +
                "  \"schemaVersion\": \"2\",\n" +
                "  \"masterKind\": \"fixture\",\n" +
                "  \"entries\": [\n" +
                "    {\n" +
                "      \"effectiveFrom\": \"2026-06\",\n" +
                "      \"sourceRefs\": [{ \"documentId\": \"known-source\" }],\n" +
                "      \"values\": { \"units\": " + number + " }\n" +
                "    }\n" +
                "  ]\n" +
                "}\n");
        }

        public void WriteMaster(string? sourceDocumentId, string? effectiveFrom = "2026-06")
        {
            var effectiveFromProperty = effectiveFrom is null
                ? string.Empty
                : "      \"effectiveFrom\": \"" + effectiveFrom + "\",\n";
            var sourceProperty = SourceRefsProperty(sourceDocumentId, "      ");

            Write(
                MasterPath,
                "{\n" +
                "  \"schemaVersion\": \"2\",\n" +
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

        public void WriteCondition(string? sourceDocumentId)
        {
            Write(
                MasterPath,
                "{\n" +
                "  \"schemaVersion\": \"2\",\n" +
                "  \"masterKind\": \"fixture\",\n" +
                "  \"conditionDefinitions\": [\n" +
                "    {\n" +
                "      \"key\": \"condition-1\",\n" +
                "      \"effectiveFrom\": \"2026-06\",\n" +
                SourceRefsProperty(sourceDocumentId, "      ") +
                "      \"kind\": \"capacity\",\n" +
                "      \"operator\": \"equals\",\n" +
                "      \"value\": 20\n" +
                "    }\n" +
                "  ],\n" +
                "  \"entries\": []\n" +
                "}\n");
        }

        private static string SourceRefsProperty(string? sourceDocumentId, string indent)
        {
            if (sourceDocumentId is null)
                return string.Empty;
            if (sourceDocumentId.Length == 0)
                return indent + "\"sourceRefs\": [],\n";
            return indent + "\"sourceRefs\": [{ \"documentId\": \"" +
                   sourceDocumentId + "\" }],\n";
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
