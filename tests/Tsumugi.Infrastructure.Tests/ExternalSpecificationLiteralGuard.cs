using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tsumugi.Infrastructure.Tests;

internal sealed record Violation(
    string RelativePath,
    int LineNumber,
    string Rule,
    string Literal,
    string CatalogPath)
{
    public override string ToString() =>
        $"{RelativePath}:{LineNumber} [{Rule}] literal '{Literal}' from {CatalogPath}";
}

/// <summary>
/// CLAUDE.md §ハード制約 3の外部仕様値が、指定JSON領域からproduction C#へ漏れないことを検査する。
/// </summary>
internal static class ExternalSpecificationLiteralGuard
{
    private const string ClaimMasterDirectory =
        "src/Tsumugi.Infrastructure/ClaimMasters/Seed/";

    private const string ClaimSourceCatalog =
        "src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json";

    private const string CsvSpecificationDirectory =
        "src/Tsumugi.Infrastructure.Csv/Specifications/";

    private const string DomainDirectory = "src/Tsumugi.Domain/";
    private const string ApplicationDirectory = "src/Tsumugi.Application/";
    private const string InfrastructureCsvDirectory = "src/Tsumugi.Infrastructure.Csv/";

    /// <summary>
    /// Narrow, reasoned exceptions for numeric coincidences between an Application/Domain
    /// literal that is fixed for reasons unrelated to claim reward values and a claim-master
    /// seed value that happens to share the same magnitude. Default is empty; add an entry
    /// only when both sides are independently load-bearing and documented, and scope it to
    /// the exact (path, line, literal) so any drift re-triggers the guard instead of silently
    /// widening the exemption.
    /// ClaimAuditEntryFactory.cs's summary-length guard (line 40, literal 512) mirrors
    /// AuditEntryConfiguration's HasMaxLength(512) DB column exactly; ADR 0026 forbids
    /// changing either side. ADR 0027 separately fixed a B-type basic-reward baseUnits of
    /// 512 (cap-81-plus / band-35000-45000 / staff-10-1, service code 462769). Both values
    /// are correct and independently fixed; the match is coincidental.
    /// </summary>
    /// <remarks>
    /// Task 11 additions seeding (ADR 0028) fixes per-count unit values 15/10/6 (welfare
    /// professional staffing) and later families (30, 94, 45-36, 21, 7). Coincidental
    /// matches added for it:
    /// <list type="bullet">
    /// <item>WageSettings.cs AllowedHourUnitMinutes fixes the Phase 2 wage time-unit
    /// divisors of 60 minutes (10 and 15 among them) — unrelated to reward units.</item>
    /// <item>RegisterCertificateUseCase.cs line 67 fixes the 10-digit provider-number
    /// length of the certificate form (ADR 0010) — unrelated to reward units.</item>
    /// <item>ClaimCalculationMasters.cs RequiredStatutoryFormula fixes the protected
    /// facility unit-price divisor 10円 (Task 13 closed contract) — a statutory divisor,
    /// not a per-count unit value.</item>
    /// </list>
    /// </remarks>
    private static readonly (string RelativePath, int LineNumber, string Literal)[]
        KnownCoincidentalLiteralMatches =
        [
            ("src/Tsumugi.Application/Audit/ClaimAuditEntryFactory.cs", 40, "512"),
            ("src/Tsumugi.Domain/Entities/WageSettings.cs", 23, "10"),
            ("src/Tsumugi.Domain/Entities/WageSettings.cs", 23, "15"),
            ("src/Tsumugi.Domain/Entities/WageSettings.cs", 23, "30"),
            ("src/Tsumugi.Application/UseCases/Certificate/RegisterCertificateUseCase.cs", 67, "10"),
            ("src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs", 266, "10"),
        ];

    public static IReadOnlyList<Violation> ScanProduction()
    {
        var solutionRoot = TsumugiAssemblyLocator.FindSolutionRoot();
        var sourceFiles = SourceCodeScanner.EnumerateSourceFiles()
            .Select(file => new SourceFile(
                NormalizePath(file.RelativePath),
                File.ReadAllText(file.FullPath)))
            .ToArray();

        return ScanCore(solutionRoot, sourceFiles);
    }

    public static IReadOnlyList<Violation> Scan(string solutionRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionRoot);
        var fullRoot = Path.GetFullPath(solutionRoot);
        var srcRoot = Path.Combine(fullRoot, "src");
        if (!Directory.Exists(srcRoot))
        {
            throw new DirectoryNotFoundException($"Production source directory not found: {srcRoot}");
        }

        var sourceFiles = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsExcludedPath(file))
            .Select(file => new SourceFile(
                NormalizePath(Path.GetRelativePath(fullRoot, file)),
                File.ReadAllText(file)))
            .ToArray();

        return ScanCore(fullRoot, sourceFiles);
    }

    private static Violation[] ScanCore(
        string solutionRoot,
        IReadOnlyList<SourceFile> sourceFiles)
    {
        var violations = new List<Violation>();
        var parsedJsonFiles = ParseJsonFiles(solutionRoot, violations);

        try
        {
            var knownSources = CollectKnownClaimSources(parsedJsonFiles);
            var masterLiterals = new List<CatalogLiteral>();
            var csvLiterals = new List<CatalogLiteral>();

            foreach (var jsonFile in parsedJsonFiles)
            {
                var root = jsonFile.Document.RootElement;
                if (IsClaimMaster(root))
                {
                    InspectClaimMaster(jsonFile, knownSources, masterLiterals, violations);
                }

                if (IsCsvSpecification(root))
                {
                    InspectCsvSpecification(jsonFile, csvLiterals, violations);
                }
            }

            InspectSourceFiles(sourceFiles, masterLiterals, csvLiterals, violations);
            return violations
                .OrderBy(violation => violation.RelativePath, StringComparer.Ordinal)
                .ThenBy(violation => violation.LineNumber)
                .ThenBy(violation => violation.Rule, StringComparer.Ordinal)
                .ThenBy(violation => violation.CatalogPath, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            foreach (var jsonFile in parsedJsonFiles) jsonFile.Document.Dispose();
        }
    }

    private static List<ParsedJsonFile> ParseJsonFiles(
        string solutionRoot,
        List<Violation> violations)
    {
        var srcRoot = Path.Combine(solutionRoot, "src");
        var parsedFiles = new List<ParsedJsonFile>();

        foreach (var fullPath in Directory.EnumerateFiles(srcRoot, "*.json", SearchOption.AllDirectories)
                     .Where(file => !IsExcludedPath(file))
                     .OrderBy(file => file, StringComparer.Ordinal))
        {
            var relativePath = NormalizePath(Path.GetRelativePath(solutionRoot, fullPath));
            var text = File.ReadAllText(fullPath);
            try
            {
                parsedFiles.Add(new ParsedJsonFile(
                    relativePath,
                    text,
                    JsonDocument.Parse(text)));
            }
            catch (JsonException exception)
            {
                var lineNumber = checked((int)Math.Min((exception.LineNumber ?? 0) + 1, int.MaxValue));
                violations.Add(new Violation(
                    relativePath,
                    lineNumber,
                    "json-parse",
                    exception.Message,
                    relativePath + "#"));
            }
        }

        return parsedFiles;
    }

    private static HashSet<string> CollectKnownClaimSources(
        IEnumerable<ParsedJsonFile> jsonFiles)
    {
        var sourceCatalog = jsonFiles.SingleOrDefault(
            file => string.Equals(file.RelativePath, ClaimSourceCatalog, StringComparison.Ordinal));
        if (sourceCatalog is null ||
            !sourceCatalog.Document.RootElement.TryGetProperty("sources", out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return sources.EnumerateArray()
            .Where(source => source.ValueKind == JsonValueKind.Object)
            .Select(source => source.TryGetProperty("documentId", out var documentId) &&
                              documentId.ValueKind == JsonValueKind.String
                ? documentId.GetString()
                : null)
            .Where(documentId => !string.IsNullOrWhiteSpace(documentId))
            .Select(documentId => documentId!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsClaimMaster(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("masterKind", out var masterKind) &&
        masterKind.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(masterKind.GetString()) &&
        root.TryGetProperty("entries", out var entries) &&
        entries.ValueKind == JsonValueKind.Array;

    private static bool IsCsvSpecification(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("specificationVersion", out var specificationVersion) &&
        specificationVersion.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(specificationVersion.GetString()) &&
        (HasArrayProperty(root, "records") || HasArrayProperty(root, "mappings"));

    private static bool HasArrayProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Array;

    private static void InspectClaimMaster(
        ParsedJsonFile jsonFile,
        IReadOnlySet<string> knownSources,
        ICollection<CatalogLiteral> catalogLiterals,
        List<Violation> violations)
    {
        var root = jsonFile.Document.RootElement;
        if (!jsonFile.RelativePath.StartsWith(ClaimMasterDirectory, StringComparison.Ordinal))
        {
            violations.Add(new Violation(
                jsonFile.RelativePath,
                FindPropertyLine(jsonFile.Text, "masterKind"),
                "claim-master-location",
                root.GetProperty("masterKind").GetString()!,
                jsonFile.RelativePath + "#/masterKind"));
        }

        var searchOffset = 0;
        var entryIndex = 0;
        foreach (var entry in root.GetProperty("entries").EnumerateArray())
        {
            var entryPointer = $"/entries/{entryIndex}";
            var entryOffset = FindElementOffset(jsonFile.Text, entry, searchOffset);
            searchOffset = Math.Max(entryOffset, searchOffset) + entry.GetRawText().Length;
            var entryLine = GetLineNumber(jsonFile.Text, entryOffset);

            if (entry.ValueKind != JsonValueKind.Object)
            {
                violations.Add(new Violation(
                    jsonFile.RelativePath,
                    entryLine,
                    "claim-master-entry",
                    entry.ValueKind.ToString(),
                    CatalogPath(jsonFile.RelativePath, entryPointer)));
                entryIndex++;
                continue;
            }

            ValidateEffectiveFrom(jsonFile.RelativePath, entry, entryPointer, entryLine, violations);
            ValidateSourceReferences(
                jsonFile.RelativePath,
                entry,
                entryPointer,
                entryLine,
                knownSources,
                violations);

            if (entry.TryGetProperty("values", out var values))
            {
                CollectMasterValues(
                    values,
                    entryPointer + "/values",
                    jsonFile.RelativePath,
                    catalogLiterals);
            }

            entryIndex++;
        }

        if (root.TryGetProperty("conditionDefinitions", out var conditions)
            && conditions.ValueKind == JsonValueKind.Array)
        {
            searchOffset = 0;
            var conditionIndex = 0;
            foreach (var condition in conditions.EnumerateArray())
            {
                var conditionPointer = $"/conditionDefinitions/{conditionIndex}";
                var conditionOffset = FindElementOffset(jsonFile.Text, condition, searchOffset);
                searchOffset = Math.Max(conditionOffset, searchOffset) +
                               condition.GetRawText().Length;
                ValidateSourceReferences(
                    jsonFile.RelativePath,
                    condition,
                    conditionPointer,
                    GetLineNumber(jsonFile.Text, conditionOffset),
                    knownSources,
                    violations);
                conditionIndex++;
            }
        }
    }

    private static void ValidateEffectiveFrom(
        string relativePath,
        JsonElement entry,
        string entryPointer,
        int entryLine,
        List<Violation> violations)
    {
        if (entry.TryGetProperty("effectiveFrom", out var effectiveFrom) &&
            effectiveFrom.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(effectiveFrom.GetString()))
        {
            return;
        }

        violations.Add(new Violation(
            relativePath,
            entryLine,
            "claim-master-effective-from",
            "<missing>",
            CatalogPath(relativePath, entryPointer + "/effectiveFrom")));
    }

    private static void ValidateSourceReferences(
        string relativePath,
        JsonElement element,
        string elementPointer,
        int elementLine,
        IReadOnlySet<string> knownSources,
        List<Violation> violations)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("sourceRefs", out var sourceRefs)
            || sourceRefs.ValueKind != JsonValueKind.Array)
        {
            AddSourceViolation(
                relativePath,
                elementPointer + "/sourceRefs",
                elementLine,
                "<missing>",
                violations);
            return;
        }

        if (sourceRefs.GetArrayLength() == 0)
        {
            AddSourceViolation(
                relativePath,
                elementPointer + "/sourceRefs",
                elementLine,
                "<empty>",
                violations);
            return;
        }

        var sourceIndex = 0;
        foreach (var sourceRef in sourceRefs.EnumerateArray())
        {
            var documentId = sourceRef.ValueKind == JsonValueKind.Object
                             && sourceRef.TryGetProperty("documentId", out var documentIdElement)
                             && documentIdElement.ValueKind == JsonValueKind.String
                ? documentIdElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(documentId) || !knownSources.Contains(documentId))
            {
                AddSourceViolation(
                    relativePath,
                    elementPointer + "/sourceRefs/" +
                    sourceIndex.ToString(CultureInfo.InvariantCulture) + "/documentId",
                    elementLine,
                    string.IsNullOrWhiteSpace(documentId) ? "<missing>" : documentId,
                    violations);
            }

            sourceIndex++;
        }
    }

    private static void AddSourceViolation(
        string relativePath,
        string pointer,
        int line,
        string literal,
        List<Violation> violations) =>
        violations.Add(new Violation(
            relativePath,
            line,
            "claim-master-source",
            literal,
            CatalogPath(relativePath, pointer)));

    private static void CollectMasterValues(
        JsonElement element,
        string pointer,
        string relativePath,
        ICollection<CatalogLiteral> catalogLiterals)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectMasterValues(
                        property.Value,
                        pointer + "/" + EscapeJsonPointer(property.Name),
                        relativePath,
                        catalogLiterals);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectMasterValues(
                        item,
                        pointer + "/" + index.ToString(CultureInfo.InvariantCulture),
                        relativePath,
                        catalogLiterals);
                    index++;
                }
                break;
            case JsonValueKind.String:
                var stringValue = element.GetString()!;
                if (stringValue.Length > 0)
                {
                    catalogLiterals.Add(new CatalogLiteral(
                        LiteralKind.String,
                        stringValue,
                        stringValue,
                        CatalogPath(relativePath, pointer)));
                }
                break;
            case JsonValueKind.Number:
                var rawNumber = element.GetRawText();
                var canonicalNumber = ParseDecimalNumber(rawNumber);
                if (canonicalNumber.HasAbsoluteValueAtLeastTen)
                {
                    catalogLiterals.Add(new CatalogLiteral(
                        LiteralKind.Number,
                        rawNumber,
                        canonicalNumber.MatchValue,
                        CatalogPath(relativePath, pointer)));
                }
                break;
        }
    }

    private static void InspectCsvSpecification(
        ParsedJsonFile jsonFile,
        ICollection<CatalogLiteral> catalogLiterals,
        List<Violation> violations)
    {
        var root = jsonFile.Document.RootElement;
        if (!jsonFile.RelativePath.StartsWith(CsvSpecificationDirectory, StringComparison.Ordinal))
        {
            violations.Add(new Violation(
                jsonFile.RelativePath,
                FindPropertyLine(jsonFile.Text, "specificationVersion"),
                "csv-specification-location",
                root.GetProperty("specificationVersion").GetString()!,
                jsonFile.RelativePath + "#/specificationVersion"));
        }

        CollectCsvLiterals(root, string.Empty, jsonFile.RelativePath, catalogLiterals);
    }

    private static void CollectCsvLiterals(
        JsonElement element,
        string pointer,
        string relativePath,
        ICollection<CatalogLiteral> catalogLiterals)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPointer = pointer + "/" + EscapeJsonPointer(property.Name);
                    if (property.Name is "fieldId" or "exchangeInformationId" or "innerRecordType" &&
                        property.Value.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrEmpty(property.Value.GetString()))
                    {
                        var value = property.Value.GetString()!;
                        catalogLiterals.Add(new CatalogLiteral(
                            LiteralKind.String,
                            value,
                            value,
                            CatalogPath(relativePath, propertyPointer)));
                    }

                    CollectCsvLiterals(
                        property.Value,
                        propertyPointer,
                        relativePath,
                        catalogLiterals);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectCsvLiterals(
                        item,
                        pointer + "/" + index.ToString(CultureInfo.InvariantCulture),
                        relativePath,
                        catalogLiterals);
                    index++;
                }
                break;
        }
    }

    private static void InspectSourceFiles(
        IEnumerable<SourceFile> sourceFiles,
        IReadOnlyCollection<CatalogLiteral> masterLiterals,
        IReadOnlyCollection<CatalogLiteral> csvLiterals,
        ICollection<Violation> violations)
    {
        var masterStrings = masterLiterals
            .Where(literal => literal.Kind == LiteralKind.String)
            .ToLookup(literal => literal.MatchValue, StringComparer.Ordinal);
        var masterNumbers = masterLiterals
            .Where(literal => literal.Kind == LiteralKind.Number)
            .ToLookup(literal => literal.MatchValue, StringComparer.Ordinal);
        var csvStrings = csvLiterals.ToLookup(
            literal => literal.MatchValue,
            StringComparer.Ordinal);

        foreach (var sourceFile in sourceFiles)
        {
            var isClaimLayer =
                sourceFile.RelativePath.StartsWith(DomainDirectory, StringComparison.Ordinal) ||
                sourceFile.RelativePath.StartsWith(ApplicationDirectory, StringComparison.Ordinal);
            var isOutsideInfrastructureCsv =
                !sourceFile.RelativePath.StartsWith(InfrastructureCsvDirectory, StringComparison.Ordinal);

            foreach (var sourceLiteral in EnumerateCSharpLiterals(sourceFile, violations))
            {
                if (isClaimLayer)
                {
                    var matches = sourceLiteral.Kind == LiteralKind.String
                        ? masterStrings[sourceLiteral.MatchValue]
                        : masterNumbers[sourceLiteral.MatchValue];
                    AddLiteralViolations(
                        sourceFile.RelativePath,
                        sourceLiteral,
                        "claim-master-literal",
                        matches.Where(match => !IsKnownCoincidentalMatch(
                            sourceFile.RelativePath,
                            sourceLiteral.LineNumber,
                            match.DisplayValue)),
                        violations);
                }

                if (isOutsideInfrastructureCsv && sourceLiteral.Kind == LiteralKind.String)
                {
                    AddLiteralViolations(
                        sourceFile.RelativePath,
                        sourceLiteral,
                        "csv-specification-literal",
                        csvStrings[sourceLiteral.MatchValue],
                        violations);
                }
            }
        }
    }

    private static bool IsKnownCoincidentalMatch(
        string relativePath,
        int lineNumber,
        string literal) =>
        Array.IndexOf(
            KnownCoincidentalLiteralMatches,
            (relativePath, lineNumber, literal)) >= 0;

    private static void AddLiteralViolations(
        string relativePath,
        SourceLiteral sourceLiteral,
        string rule,
        IEnumerable<CatalogLiteral> catalogMatches,
        ICollection<Violation> violations)
    {
        foreach (var catalogLiteral in catalogMatches)
        {
            violations.Add(new Violation(
                relativePath,
                sourceLiteral.LineNumber,
                rule,
                catalogLiteral.DisplayValue,
                catalogLiteral.CatalogPath));
        }
    }

    private static List<SourceLiteral> EnumerateCSharpLiterals(
        SourceFile sourceFile,
        ICollection<Violation> violations)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            sourceFile.Text,
            parseOptions,
            path: sourceFile.RelativePath);
        var errors = syntaxTree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        foreach (var diagnostic in errors)
        {
            var lineNumber = diagnostic.Location.IsInSource
                ? diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1
                : 1;
            violations.Add(new Violation(
                sourceFile.RelativePath,
                lineNumber,
                "csharp-parse",
                diagnostic.Id + ": " + diagnostic.GetMessage(CultureInfo.InvariantCulture),
                sourceFile.RelativePath + "#syntax"));
        }

        if (errors.Length > 0) return [];

        var root = syntaxTree.GetRoot();
        var disabledTokens = root.DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            .SelectMany(trivia => SyntaxFactory.ParseTokens(
                trivia.ToString(),
                initialTokenPosition: trivia.SpanStart,
                options: parseOptions))
            .ToArray();
        var disabledErrors = disabledTokens
            .SelectMany(token => token.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => (Token: token, Diagnostic: diagnostic)))
            .ToArray();

        foreach (var (token, diagnostic) in disabledErrors)
        {
            violations.Add(new Violation(
                sourceFile.RelativePath,
                GetLineNumber(sourceFile.Text, token.SpanStart),
                "csharp-parse",
                diagnostic.Id + ": " + diagnostic.GetMessage(CultureInfo.InvariantCulture),
                sourceFile.RelativePath + "#syntax"));
        }

        if (disabledErrors.Length > 0) return [];

        return root.DescendantTokens()
            .Concat(disabledTokens)
            .DistinctBy(token => (token.SpanStart, token.Span.Length, token.RawKind))
            .OrderBy(token => token.SpanStart)
            .Where(token => !IsEnumMemberDiscriminant(token))
            .Select(token => ToSourceLiteral(syntaxTree, token))
            .OfType<SourceLiteral>()
            .ToList();
    }

    /// <summary>
    /// enumメンバの判別値（<c>Kind = 10</c> 等）は型契約上の構造値であり、報酬告示の単位数等の
    /// 外部仕様値をここへ「置く」ことは機能しない（値として参照した瞬間に別の式リテラルとして
    /// 検出される）。加算seed投入（Task 11）以降、単位数（10・15等）と既存enum判別値の偶然一致が
    /// 常態化するため、enumメンバ判別値の数値リテラルのみ走査対象から除外する。
    /// disabled-textから復元したトークンは構文木を持たないため本判定の対象外（従来どおり検出）。
    /// </summary>
    private static bool IsEnumMemberDiscriminant(SyntaxToken token) =>
        token.IsKind(SyntaxKind.NumericLiteralToken)
        && token.Parent?.AncestorsAndSelf().OfType<EnumMemberDeclarationSyntax>().Any() == true;

    private static SourceLiteral? ToSourceLiteral(SyntaxTree syntaxTree, SyntaxToken token)
    {
        var lineNumber = syntaxTree.GetLineSpan(token.Span).StartLinePosition.Line + 1;
        if (token.IsKind(SyntaxKind.NumericLiteralToken))
        {
            return new SourceLiteral(
                LiteralKind.Number,
                NormalizeCSharpNumber(token),
                lineNumber);
        }

        return IsStringLiteralToken(token.Kind())
            ? new SourceLiteral(LiteralKind.String, token.ValueText, lineNumber)
            : null;
    }

    private static bool IsStringLiteralToken(SyntaxKind kind) =>
        kind is SyntaxKind.StringLiteralToken
            or SyntaxKind.SingleLineRawStringLiteralToken
            or SyntaxKind.MultiLineRawStringLiteralToken
            or SyntaxKind.Utf8StringLiteralToken
            or SyntaxKind.Utf8SingleLineRawStringLiteralToken
            or SyntaxKind.Utf8MultiLineRawStringLiteralToken;

    private static string NormalizeCSharpNumber(SyntaxToken token)
    {
        var literal = token.Text.Replace("_", string.Empty, StringComparison.Ordinal);
        if (literal.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeBasedInteger(literal, prefixLength: 2, radix: 16);
        }

        if (literal.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeBasedInteger(literal, prefixLength: 2, radix: 2);
        }

        return NormalizeDecimalNumber(StripCSharpNumericSuffix(literal));
    }

    private static string NormalizeBasedInteger(string literal, int prefixLength, int radix)
    {
        var digitEnd = literal.Length;
        while (digitEnd > prefixLength && literal[digitEnd - 1] is 'u' or 'U' or 'l' or 'L')
        {
            digitEnd--;
        }

        var value = BigInteger.Zero;
        for (var index = prefixLength; index < digitEnd; index++)
        {
            value = value * radix + GetRadixDigit(literal[index]);
        }

        return NormalizeDecimalNumber(value.ToString(CultureInfo.InvariantCulture));
    }

    private static int GetRadixDigit(char character) =>
        character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => throw new InvalidOperationException("Roslyn returned an invalid based integer literal."),
        };

    private static string StripCSharpNumericSuffix(string literal)
    {
        var suffixStart = literal.Length;
        while (suffixStart > 0 && literal[suffixStart - 1] is
               'u' or 'U' or 'l' or 'L' or 'f' or 'F' or 'd' or 'D' or 'm' or 'M')
        {
            suffixStart--;
        }

        return literal[..suffixStart];
    }

    private static string NormalizeDecimalNumber(string literal) => ParseDecimalNumber(literal).MatchValue;

    private static CanonicalDecimal ParseDecimalNumber(string literal)
    {
        var unsignedLiteral = literal.AsSpan();
        if (unsignedLiteral.Length > 0 && unsignedLiteral[0] is '+' or '-')
        {
            unsignedLiteral = unsignedLiteral[1..];
        }

        var exponentMarker = unsignedLiteral.IndexOf('e');
        if (exponentMarker < 0) exponentMarker = unsignedLiteral.IndexOf('E');

        var explicitExponent = exponentMarker < 0
            ? BigInteger.Zero
            : BigInteger.Parse(
                unsignedLiteral[(exponentMarker + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture);
        var significand = exponentMarker < 0
            ? unsignedLiteral
            : unsignedLiteral[..exponentMarker];
        var decimalPoint = significand.IndexOf('.');
        var fractionalDigitCount = decimalPoint < 0
            ? 0
            : significand.Length - decimalPoint - 1;
        var digits = significand.ToString().Replace(".", string.Empty, StringComparison.Ordinal);

        var firstNonZero = 0;
        while (firstNonZero < digits.Length && digits[firstNonZero] == '0') firstNonZero++;
        if (firstNonZero == digits.Length) return new CanonicalDecimal("0", BigInteger.Zero);

        var decimalExponent = explicitExponent - fractionalDigitCount;
        var significantEnd = digits.Length;
        while (significantEnd > firstNonZero && digits[significantEnd - 1] == '0')
        {
            significantEnd--;
            decimalExponent++;
        }

        return new CanonicalDecimal(digits[firstNonZero..significantEnd], decimalExponent);
    }

    private static int FindElementOffset(string text, JsonElement element, int startIndex)
    {
        var offset = text.IndexOf(element.GetRawText(), startIndex, StringComparison.Ordinal);
        return offset < 0 ? 0 : offset;
    }

    private static int FindPropertyLine(string text, string propertyName)
    {
        var offset = text.IndexOf('"' + propertyName + '"', StringComparison.Ordinal);
        return GetLineNumber(text, Math.Max(offset, 0));
    }

    private static int GetLineNumber(string text, int offset) =>
        1 + CountNewLines(text.AsSpan(0, Math.Clamp(offset, 0, text.Length)));

    private static int CountNewLines(ReadOnlySpan<char> text)
    {
        var count = 0;
        foreach (var character in text)
        {
            if (character == '\n') count++;
        }
        return count;
    }

    private static bool IsExcludedPath(string path) =>
        ContainsDirectory(path, "obj") ||
        ContainsDirectory(path, "bin") ||
        ContainsDirectory(path, "Migrations");

    private static bool ContainsDirectory(string path, string directoryName)
    {
        var separator = Path.DirectorySeparatorChar;
        var alternateSeparator = Path.AltDirectorySeparatorChar;
        return path.Contains(separator + directoryName + separator, StringComparison.Ordinal) ||
               path.Contains(alternateSeparator + directoryName + alternateSeparator, StringComparison.Ordinal);
    }

    private static string CatalogPath(string relativePath, string pointer) =>
        relativePath + "#" + pointer;

    private static string EscapeJsonPointer(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private enum LiteralKind
    {
        String,
        Number,
    }

    private sealed record SourceFile(string RelativePath, string Text);

    private sealed record ParsedJsonFile(
        string RelativePath,
        string Text,
        JsonDocument Document);

    private sealed record CatalogLiteral(
        LiteralKind Kind,
        string DisplayValue,
        string MatchValue,
        string CatalogPath);

    private sealed record SourceLiteral(
        LiteralKind Kind,
        string MatchValue,
        int LineNumber);

    private readonly record struct CanonicalDecimal(
        string SignificantDigits,
        BigInteger DecimalExponent)
    {
        public bool HasAbsoluteValueAtLeastTen =>
            SignificantDigits != "0" && DecimalExponent + SignificantDigits.Length >= 2;

        public string MatchValue => DecimalExponent.IsZero
            ? SignificantDigits
            : SignificantDigits + "E" + DecimalExponent.ToString(CultureInfo.InvariantCulture);
    }
}
