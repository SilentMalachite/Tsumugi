using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    private static readonly Regex CSharpStringLiteralPattern = new(
        """(?<raw>(?<delimiter>"{3,})(?<rawValue>[\s\S]*?)\k<delimiter>)|(?<verbatim>@"(?<verbatimValue>(?:""|[^"])*)")|(?<regular>"(?<regularValue>(?:\\.|[^"\\])*)")""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CSharpNumberLiteralPattern = new(
        """(?<![\p{L}\p{N}_.])(?<number>\d[\d_]*(?:\.\d[\d_]*)?(?:[eE][+-]?\d[\d_]*)?)(?<suffix>[uUlLfFdDmM]{0,2})(?![\p{L}\p{N}_.])""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            ValidateSourceReference(
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

    private static void ValidateSourceReference(
        string relativePath,
        JsonElement entry,
        string entryPointer,
        int entryLine,
        IReadOnlySet<string> knownSources,
        List<Violation> violations)
    {
        var sourceDocumentId = entry.TryGetProperty("sourceDocumentId", out var source) &&
                               source.ValueKind == JsonValueKind.String
            ? source.GetString()
            : null;
        if (!string.IsNullOrWhiteSpace(sourceDocumentId) && knownSources.Contains(sourceDocumentId))
        {
            return;
        }

        violations.Add(new Violation(
            relativePath,
            entryLine,
            "claim-master-source",
            string.IsNullOrWhiteSpace(sourceDocumentId) ? "<missing>" : sourceDocumentId,
            CatalogPath(relativePath, entryPointer + "/sourceDocumentId")));
    }

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
                if (HasAbsoluteValueAtLeastTen(element))
                {
                    catalogLiterals.Add(new CatalogLiteral(
                        LiteralKind.Number,
                        rawNumber,
                        NormalizeCatalogNumber(rawNumber),
                        CatalogPath(relativePath, pointer)));
                }
                break;
        }
    }

    private static bool HasAbsoluteValueAtLeastTen(JsonElement number)
    {
        if (number.TryGetDecimal(out var decimalValue))
        {
            return decimalValue >= 10m || decimalValue <= -10m;
        }

        return number.TryGetDouble(out var doubleValue) &&
               (doubleValue >= 10d || doubleValue <= -10d);
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

            foreach (var sourceLiteral in EnumerateCSharpLiterals(sourceFile.Text))
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
                        matches,
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

    private static List<SourceLiteral> EnumerateCSharpLiterals(string text)
    {
        var literals = new List<SourceLiteral>();
        var codeWithoutComments = StripComments(text);
        var codeWithoutStrings = codeWithoutComments.ToCharArray();

        foreach (Match match in CSharpStringLiteralPattern.Matches(codeWithoutComments))
        {
            var value = match.Groups["raw"].Success
                ? match.Groups["rawValue"].Value
                : match.Groups["verbatim"].Success
                    ? match.Groups["verbatimValue"].Value.Replace("\"\"", "\"", StringComparison.Ordinal)
                    : Regex.Unescape(match.Groups["regularValue"].Value);
            literals.Add(new SourceLiteral(
                LiteralKind.String,
                value,
                GetLineNumber(codeWithoutComments, match.Index)));
            BlankExceptNewLines(codeWithoutStrings, match.Index, match.Length);
        }

        var numericSource = new string(codeWithoutStrings);
        foreach (Match match in CSharpNumberLiteralPattern.Matches(numericSource))
        {
            literals.Add(new SourceLiteral(
                LiteralKind.Number,
                match.Groups["number"].Value.Replace("_", string.Empty, StringComparison.Ordinal),
                GetLineNumber(numericSource, match.Index)));
        }

        foreach (var hole in EnumerateInterpolationHoles(codeWithoutComments))
        {
            literals.AddRange(EnumerateCSharpLiterals(hole.Text)
                .Select(literal => literal with
                {
                    LineNumber = literal.LineNumber + hole.LineOffset,
                }));
        }

        return literals;
    }

    private static List<InterpolationHole> EnumerateInterpolationHoles(string text)
    {
        var holes = new List<InterpolationHole>();
        var index = 0;
        while (index < text.Length)
        {
            if (TryReadInterpolatedStringStart(text, index, out var interpolation))
            {
                index = interpolation.QuoteIndex + interpolation.QuoteCount;
                while (index < text.Length)
                {
                    if (IsInterpolationClosingDelimiter(text, index, interpolation))
                    {
                        index += interpolation.QuoteCount;
                        break;
                    }

                    if (!interpolation.IsRaw &&
                        !interpolation.IsVerbatim &&
                        text[index] == '\\' &&
                        index + 1 < text.Length)
                    {
                        index += 2;
                        continue;
                    }

                    if (interpolation.IsVerbatim &&
                        text[index] == '"' &&
                        index + 1 < text.Length &&
                        text[index + 1] == '"')
                    {
                        index += 2;
                        continue;
                    }

                    var braceCount = interpolation.IsRaw ? interpolation.DollarCount : 1;
                    if (text[index] == '{' &&
                        CountRun(text, index, '{') >= braceCount &&
                        (interpolation.IsRaw || !StartsWith(text, index, "{{")))
                    {
                        var holeStart = index + braceCount;
                        var holeEnd = FindInterpolationHoleEnd(text, holeStart, braceCount);
                        if (holeEnd < 0)
                        {
                            index = text.Length;
                            break;
                        }

                        var holeText = text[holeStart..holeEnd];
                        var expressionLength = GetInterpolationExpressionLength(holeText);
                        holes.Add(new InterpolationHole(
                            holeText[..expressionLength],
                            GetLineNumber(text, holeStart) - 1));
                        index = holeEnd + braceCount;
                        continue;
                    }

                    if (!interpolation.IsRaw &&
                        (StartsWith(text, index, "{{") || StartsWith(text, index, "}}")))
                    {
                        index += 2;
                        continue;
                    }

                    index++;
                }

                continue;
            }

            if (text[index] == '"')
            {
                var quoteCount = CountRun(text, index, '"');
                index = quoteCount >= 3
                    ? SkipRawString(text, index, quoteCount)
                    : SkipQuotedLiteral(text, index, '"', isVerbatim: false);
                continue;
            }

            if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index = SkipQuotedLiteral(text, index + 1, '"', isVerbatim: true);
                continue;
            }

            if (text[index] == '\'')
            {
                index = SkipQuotedLiteral(text, index, '\'', isVerbatim: false);
                continue;
            }

            index++;
        }

        return holes;
    }

    private static bool TryReadInterpolatedStringStart(
        string text,
        int index,
        out InterpolatedStringStart interpolation)
    {
        interpolation = default;
        var dollarCount = 0;
        var isVerbatim = false;
        var quoteIndex = index;

        if (text[index] == '$')
        {
            dollarCount = CountRun(text, index, '$');
            quoteIndex += dollarCount;
            if (quoteIndex < text.Length && text[quoteIndex] == '@')
            {
                if (dollarCount != 1) return false;
                isVerbatim = true;
                quoteIndex++;
            }
        }
        else if (text[index] == '@' &&
                 index + 2 < text.Length &&
                 text[index + 1] == '$')
        {
            dollarCount = 1;
            isVerbatim = true;
            quoteIndex = index + 2;
        }
        else
        {
            return false;
        }

        if (quoteIndex >= text.Length || text[quoteIndex] != '"') return false;
        var quoteCount = CountRun(text, quoteIndex, '"');
        if (quoteCount == 2 || isVerbatim && quoteCount != 1) return false;

        interpolation = new InterpolatedStringStart(
            quoteIndex,
            quoteCount,
            dollarCount,
            isVerbatim,
            quoteCount >= 3);
        return true;
    }

    private static bool IsInterpolationClosingDelimiter(
        string text,
        int index,
        InterpolatedStringStart interpolation)
    {
        if (text[index] != '"') return false;
        if (interpolation.IsRaw)
        {
            return CountRun(text, index, '"') >= interpolation.QuoteCount;
        }

        return !interpolation.IsVerbatim ||
               index + 1 >= text.Length ||
               text[index + 1] != '"';
    }

    private static int FindInterpolationHoleEnd(
        string text,
        int startIndex,
        int closingBraceCount)
    {
        var nestedBraceDepth = 0;
        var index = startIndex;
        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                var quoteCount = CountRun(text, index, '"');
                index = quoteCount >= 3
                    ? SkipRawString(text, index, quoteCount)
                    : SkipQuotedLiteral(text, index, '"', isVerbatim: false);
                continue;
            }

            if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index = SkipQuotedLiteral(text, index + 1, '"', isVerbatim: true);
                continue;
            }

            if (text[index] == '\'')
            {
                index = SkipQuotedLiteral(text, index, '\'', isVerbatim: false);
                continue;
            }

            if (text[index] == '{')
            {
                nestedBraceDepth++;
                index++;
                continue;
            }

            if (text[index] == '}')
            {
                if (nestedBraceDepth == 0 &&
                    CountRun(text, index, '}') >= closingBraceCount)
                {
                    return index;
                }

                if (nestedBraceDepth > 0) nestedBraceDepth--;
            }

            index++;
        }

        return -1;
    }

    private static int GetInterpolationExpressionLength(string hole)
    {
        var parenthesesDepth = 0;
        var bracketsDepth = 0;
        var bracesDepth = 0;
        var conditionalDepth = 0;
        var index = 0;

        while (index < hole.Length)
        {
            if (hole[index] == '"')
            {
                var quoteCount = CountRun(hole, index, '"');
                index = quoteCount >= 3
                    ? SkipRawString(hole, index, quoteCount)
                    : SkipQuotedLiteral(hole, index, '"', isVerbatim: false);
                continue;
            }

            if (hole[index] == '@' && index + 1 < hole.Length && hole[index + 1] == '"')
            {
                index = SkipQuotedLiteral(hole, index + 1, '"', isVerbatim: true);
                continue;
            }

            if (hole[index] == '\'')
            {
                index = SkipQuotedLiteral(hole, index, '\'', isVerbatim: false);
                continue;
            }

            switch (hole[index])
            {
                case '(':
                    parenthesesDepth++;
                    break;
                case ')':
                    if (parenthesesDepth > 0) parenthesesDepth--;
                    break;
                case '[':
                    bracketsDepth++;
                    break;
                case ']':
                    if (bracketsDepth > 0) bracketsDepth--;
                    break;
                case '{':
                    bracesDepth++;
                    break;
                case '}':
                    if (bracesDepth > 0) bracesDepth--;
                    break;
            }

            var isTopLevel = parenthesesDepth == 0 && bracketsDepth == 0 && bracesDepth == 0;
            if (!isTopLevel)
            {
                index++;
                continue;
            }

            if (hole[index] == ',') return index;

            if (hole[index] == '?')
            {
                if (index + 1 < hole.Length && hole[index + 1] == '[')
                {
                    bracketsDepth++;
                    index += 2;
                    continue;
                }

                if (index + 1 < hole.Length && hole[index + 1] is '?' or '.')
                {
                    index += 2;
                    continue;
                }

                conditionalDepth++;
                index++;
                continue;
            }

            if (hole[index] == ':')
            {
                if (index + 1 < hole.Length && hole[index + 1] == ':' ||
                    index > 0 && hole[index - 1] == ':')
                {
                    index++;
                    continue;
                }

                if (conditionalDepth == 0) return index;
                conditionalDepth--;
            }

            index++;
        }

        return hole.Length;
    }

    private static string StripComments(string text)
    {
        var result = text.ToCharArray();
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                var quoteCount = CountRun(text, index, '"');
                index = quoteCount >= 3
                    ? SkipRawString(text, index, quoteCount)
                    : SkipQuotedLiteral(text, index, '"', isVerbatim: false);
                continue;
            }

            if (text[index] == '@' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index = SkipQuotedLiteral(text, index + 1, '"', isVerbatim: true);
                continue;
            }

            if (text[index] == '\'')
            {
                index = SkipQuotedLiteral(text, index, '\'', isVerbatim: false);
                continue;
            }

            if (StartsWith(text, index, "//"))
            {
                var end = text.IndexOf('\n', index);
                end = end < 0 ? text.Length : end;
                BlankExceptNewLines(result, index, end - index);
                index = end;
                continue;
            }

            if (StartsWith(text, index, "/*"))
            {
                var closing = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                var end = closing < 0 ? text.Length : closing + 2;
                BlankExceptNewLines(result, index, end - index);
                index = end;
                continue;
            }

            index++;
        }

        return new string(result);
    }

    private static int SkipRawString(string text, int index, int quoteCount)
    {
        var delimiter = new string('"', quoteCount);
        var end = text.IndexOf(delimiter, index + quoteCount, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + quoteCount;
    }

    private static int SkipQuotedLiteral(
        string text,
        int quoteIndex,
        char delimiter,
        bool isVerbatim)
    {
        var index = quoteIndex + 1;
        while (index < text.Length)
        {
            if (text[index] == delimiter)
            {
                if (isVerbatim && index + 1 < text.Length && text[index + 1] == delimiter)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            if (!isVerbatim && text[index] == '\\' && index + 1 < text.Length)
            {
                index += 2;
                continue;
            }

            index++;
        }

        return text.Length;
    }

    private static void BlankExceptNewLines(char[] text, int index, int length)
    {
        var end = index + length;
        for (var position = index; position < end; position++)
        {
            if (text[position] is not '\r' and not '\n') text[position] = ' ';
        }
    }

    private static string NormalizeCatalogNumber(string literal) =>
        literal.Length > 0 && literal[0] == '-' ? literal[1..] : literal;

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

    private static int CountRun(string text, int index, char character)
    {
        var count = 0;
        while (index + count < text.Length && text[index + count] == character) count++;
        return count;
    }

    private static bool StartsWith(string text, int index, string value) =>
        index + value.Length <= text.Length &&
        text.AsSpan(index, value.Length).SequenceEqual(value.AsSpan());

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

    private sealed record InterpolationHole(string Text, int LineOffset);

    private readonly record struct InterpolatedStringStart(
        int QuoteIndex,
        int QuoteCount,
        int DollarCount,
        bool IsVerbatim,
        bool IsRaw);
}
