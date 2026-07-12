using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

public sealed class JsonClaimMasterProvider : IClaimMasterProvider, IOfficeClaimProfilePolicyProvider
{
    private const string SupportedSchemaVersion = "1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly string[] MasterFileNames =
    [
        "basic-rewards.json",
        "additions.json",
        "region-unit-prices.json",
        "burden-caps.json",
        "transition-rules.json",
        "service-codes.json",
    ];

    private readonly ImmutableArray<ClaimMasterRelease> _releases;
    private readonly ImmutableArray<ClaimSourceDocument> _sources;
    private readonly ClaimCalculationMasterBundle _calculationMasters;

    private JsonClaimMasterProvider(
        IEnumerable<ClaimMasterRelease> releases,
        IEnumerable<ClaimSourceDocument> sources,
        ClaimCalculationMasterBundle calculationMasters)
    {
        _releases = [.. releases];
        _sources = [.. sources];
        _calculationMasters = calculationMasters;
    }

    public static JsonClaimMasterProvider LoadEmbedded()
    {
        var assembly = typeof(JsonClaimMasterProvider).Assembly;
        using var sourceSchema = OpenEmbedded(
            assembly,
            ".ClaimMasters.Schema.source-catalog.schema.json");
        using var masterSchema = OpenEmbedded(
            assembly,
            ".ClaimMasters.Schema.claim-master-file.schema.json");
        ValidateSchemaResource(sourceSchema, "source-catalog.schema.json");
        ValidateSchemaResource(masterSchema, "claim-master-file.schema.json");

        using var sources = OpenEmbedded(assembly, ".ClaimMasters.Seed.sources.json");
        var masters = new Dictionary<string, Stream>(StringComparer.Ordinal);
        try
        {
            foreach (var fileName in MasterFileNames)
            {
                masters.Add(
                    fileName,
                    OpenEmbedded(assembly, $".ClaimMasters.Seed.{fileName}"));
            }

            return LoadPolicy(sources, masters);
        }
        finally
        {
            foreach (var stream in masters.Values)
                stream.Dispose();
        }
    }

    internal static JsonClaimMasterProvider Load(
        Stream sources,
        IReadOnlyDictionary<string, Stream> masters) =>
        Load(sources, masters, sanitizeTransitionHeaders: false);

    internal static JsonClaimMasterProvider LoadPolicy(
        Stream sources,
        IReadOnlyDictionary<string, Stream> masters) =>
        Load(sources, masters, sanitizeTransitionHeaders: true);

    private static JsonClaimMasterProvider Load(
        Stream sources,
        IReadOnlyDictionary<string, Stream> masters,
        bool sanitizeTransitionHeaders)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(masters);

        var catalog = Deserialize<SourceCatalogFile>(sources, "sources.json");
        ValidateSchemaVersion(catalog.SchemaVersion, "sources.json");
        ValidateCatalog(catalog);

        var sourceSha256ByDocumentId = catalog.Sources
            .ToDictionary(
                source => source.DocumentId,
                source => source.Sha256,
                StringComparer.Ordinal);
        var calculationMasters = ClaimMasterFileValidator.ValidateAll(
            masters,
            sourceSha256ByDocumentId,
            sanitizeTransitionHeaders);
        ValidateTransitionRuleReferences(calculationMasters.TransitionRules, catalog.Releases);

        var domainSources = catalog.Sources.Select(ToDomainSource).ToImmutableArray();
        var releases = catalog.Releases.Select(ToDomainRelease).ToImmutableArray();
        ClaimMasterCatalogPolicy.Validate(releases, domainSources);

        return new JsonClaimMasterProvider(releases, domainSources, calculationMasters);
    }

    public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth)
        => ClaimMasterCatalogPolicy.Resolve(_releases, _sources, serviceMonth);

    public OfficeClaimProfilePolicy Resolve(ClaimMasterVersion masterVersion)
    {
        try
        {
            _ = masterVersion.Value;
            var candidates = _calculationMasters.TransitionRules
                .Where(rule => rule.MasterVersion == masterVersion)
                .ToArray();
            if (candidates.Length == 0)
            {
                throw new ClaimMasterPolicyUnavailableException(
                    ClaimMasterPolicyUnavailableCode.Unavailable);
            }

            if (candidates.Length != 1)
            {
                throw new ClaimMasterPolicyUnavailableException(
                    ClaimMasterPolicyUnavailableCode.Ambiguous);
            }

            var row = candidates[0];
            var optionRules = _calculationMasters.TransitionRules.Select(rule =>
                new AverageWageBandOptionVersionRule(
                    rule.MasterVersion,
                    rule.EffectiveFrom,
                    rule.EffectiveTo,
                    rule.AllowedAverageWageBandOptions,
                    rule.AllowedOptionsByR8ReformStatus)).ToArray();
            return new OfficeClaimProfilePolicy(
                row.MasterVersion,
                optionRules,
                row.R8EffectiveDate,
                designation => ResolveFiledTransitionExclusiveEnd(row, designation));
        }
        catch (ClaimMasterPolicyUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.InvalidMaster);
        }
    }

    private static DateOnly ResolveFiledTransitionExclusiveEnd(
        OfficeClaimProfileTransitionRuleMasterRow row,
        DateOnly designation) => row.FiledTransitionEndRule switch
        {
            FiledTransitionExclusiveEndRule.AddYearsExclusive =>
                designation.AddYears(row.FiledTransitionDurationYears),
            _ => throw new InvalidOperationException("Filed transition end rule is closed."),
        };

    private static void ValidateTransitionRuleReferences(
        IReadOnlyCollection<OfficeClaimProfileTransitionRuleMasterRow> rows,
        IReadOnlyCollection<Release> releases)
    {
        foreach (var row in rows)
        {
            var release = releases.SingleOrDefault(item =>
                string.Equals(item.MasterVersion, row.MasterVersion.Value, StringComparison.Ordinal));
            if (release is null
                || !release.SourceDocumentIds.Contains(
                    row.Source.DocumentId,
                    StringComparer.Ordinal))
            {
                throw new ClaimMasterPolicyUnavailableException(
                    ClaimMasterPolicyUnavailableCode.InvalidMaster);
            }
        }
    }

    private static Stream OpenEmbedded(Assembly assembly, string exactSuffix)
    {
        var matches = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(exactSuffix, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                $"Embedded claim master resource '{exactSuffix}' must exist exactly once; found {matches.Length}.");
        }

        return assembly.GetManifestResourceStream(matches[0])
            ?? throw new InvalidDataException(
                $"Embedded claim master resource '{matches[0]}' could not be opened.");
    }

    private static void ValidateSchemaResource(Stream stream, string fileName)
    {
        try
        {
            var root = JsonSerializer.Deserialize<JsonElement>(stream, SerializerOptions);
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Claim master schema '{fileName}' must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Claim master schema '{fileName}' is not valid JSON.",
                exception);
        }
    }

    private static T Deserialize<T>(Stream stream, string fileName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(stream, SerializerOptions)
                ?? throw new InvalidDataException($"Claim master resource '{fileName}' is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Claim master resource '{fileName}' contains invalid JSON.",
                exception);
        }
    }

    private static void ValidateCatalog(SourceCatalogFile catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog.Sources);
        ArgumentNullException.ThrowIfNull(catalog.Releases);
        if (catalog.Sources.Count == 0)
            throw new InvalidDataException("Source catalog must contain at least one source.");
        if (catalog.Sources.Any(source => source is null))
            throw new InvalidDataException("Source catalog cannot contain null sources.");
        if (catalog.Releases.Any(release => release is null))
            throw new InvalidDataException("Source catalog cannot contain null releases.");

        var duplicateSourceId = catalog.Sources
            .GroupBy(source => source.DocumentId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateSourceId is not null)
            throw new InvalidDataException($"Duplicate source documentId '{duplicateSourceId}'.");

        foreach (var source in catalog.Sources)
            ValidateSource(source);

        var knownSourceIds = catalog.Sources
            .Select(source => source.DocumentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var source in catalog.Sources)
        {
            ValidateRelations(source, source.Supersedes, "supersedes", knownSourceIds);
            ValidateRelations(source, source.Corrects, "corrects", knownSourceIds);
            ValidateRelations(source, source.Supplements, "supplements", knownSourceIds);
            if (source.Supersedes is { Count: > 1 })
            {
                throw new InvalidDataException(
                    $"Source '{source.DocumentId}' has more than one supersedes target.");
            }

            var hasRelations = source.Supersedes is { Count: > 0 }
                || source.Corrects is { Count: > 0 }
                || source.Supplements is { Count: > 0 };
            if (hasRelations && source.CorrectionNote is null)
            {
                throw new InvalidDataException(
                    $"Source '{source.DocumentId}' must have correctionNote when a source relation exists.");
            }

            if (!hasRelations && source.CorrectionNote is not null)
            {
                throw new InvalidDataException(
                    $"Source '{source.DocumentId}' cannot have correctionNote without a source relation.");
            }
        }

        foreach (var release in catalog.Releases)
            ValidateRelease(release);
    }

    private static void ValidateSource(SourceDocument source)
    {
        ValidateRequiredText(source.DocumentId, "documentId");
        ValidateRequiredText(source.Title, "title");
        ValidateRequiredText(source.Publisher, "publisher");
        ParseDate(source.EffectiveAt, "effectiveAt", source.DocumentId);
        if (source.PublishedAt is not null)
            ParseDate(source.PublishedAt, "publishedAt", source.DocumentId);
        ParseDate(source.RetrievedAt, "retrievedAt", source.DocumentId);

        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || source.Url.Length != source.Url.Trim().Length)
        {
            throw new InvalidDataException($"Source '{source.DocumentId}' URL must be absolute HTTPS.");
        }

        if (source.Sha256.Length != 64
            || source.Sha256.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException(
                $"Source '{source.DocumentId}' sha256 must be 64 lowercase hexadecimal characters.");
        }

        ValidateOptionalText(source.ApplicabilityNote, "applicabilityNote");
        ValidateOptionalText(source.CorrectionNote, "correctionNote");
    }

    private static void ValidateRelations(
        SourceDocument source,
        IReadOnlyList<string>? relations,
        string relationName,
        HashSet<string> knownSourceIds)
    {
        if (relations is null)
            return;
        if (relations.Count == 0)
            throw new InvalidDataException($"Source '{source.DocumentId}' {relationName} cannot be empty.");
        if (relations.Any(relation => relation is null))
            throw new InvalidDataException($"Source '{source.DocumentId}' {relationName} cannot contain null.");

        var duplicate = relations.GroupBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Source '{source.DocumentId}' {relationName} contains duplicate '{duplicate}'.");
        }

        foreach (var relation in relations)
        {
            ValidateRequiredText(relation, relationName);
            if (!knownSourceIds.Contains(relation))
            {
                throw new InvalidDataException(
                    $"Source '{source.DocumentId}' {relationName} target '{relation}' does not exist.");
            }
        }
    }

    private static void ValidateRelease(Release release)
    {
        ValidateRequiredText(release.MasterVersion, "masterVersion");
        ParseMonth(release.EffectiveFrom, "effectiveFrom", release.MasterVersion);
        if (release.EffectiveTo is not null)
            ParseMonth(release.EffectiveTo, "effectiveTo", release.MasterVersion);
        ArgumentNullException.ThrowIfNull(release.SourceDocumentIds);
        foreach (var sourceId in release.SourceDocumentIds)
            ValidateRequiredText(sourceId, "sourceDocumentIds");
    }

    private static ClaimSourceDocument ToDomainSource(SourceDocument source)
    {
        var notes = new[] { source.ApplicabilityNote, source.CorrectionNote }
            .Where(note => note is not null)
            .Select(note => note!)
            .ToArray();
        return new ClaimSourceDocument(
            source.DocumentId,
            source.Title,
            source.Publisher,
            ParseDate(source.EffectiveAt, "effectiveAt", source.DocumentId),
            ParseDate(source.RetrievedAt, "retrievedAt", source.DocumentId),
            source.Url,
            source.Sha256,
            source.Supersedes?.SingleOrDefault(),
            notes.Length == 0 ? null : string.Join(" ", notes));
    }

    private static ClaimMasterRelease ToDomainRelease(Release release) => new(
        new ClaimMasterVersion(release.MasterVersion),
        ParseMonth(release.EffectiveFrom, "effectiveFrom", release.MasterVersion),
        release.EffectiveTo is null
            ? null
            : ParseMonth(release.EffectiveTo, "effectiveTo", release.MasterVersion),
        release.SourceDocumentIds);

    private static DateOnly ParseDate(string value, string propertyName, string context)
    {
        var parsed = DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
        if (!parsed)
        {
            throw new InvalidDataException(
                $"Source '{context}' property '{propertyName}' must be YYYY-MM-DD.");
        }

        return date;
    }

    private static ServiceMonth ParseMonth(string value, string propertyName, string context)
    {
        if (value.Length != 7
            || value[4] != '-'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year is < 1900 or > 2200
            || month is < 1 or > 12)
        {
            throw new InvalidDataException(
                $"Release '{context}' property '{propertyName}' must be YYYY-MM.");
        }

        return new ServiceMonth(year, month);
    }

    private static void ValidateSchemaVersion(string actual, string fileName)
    {
        if (!string.Equals(actual, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Claim master resource '{fileName}' has unsupported schemaVersion '{actual}'.");
        }
    }

    private static void ValidateRequiredText(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
        {
            throw new InvalidDataException(
                $"Property '{propertyName}' must be non-blank without outer whitespace.");
        }
    }

    private static void ValidateOptionalText(string? value, string propertyName)
    {
        if (value is not null)
            ValidateRequiredText(value, propertyName);
    }

    private sealed record SourceCatalogFile(
        string SchemaVersion,
        IReadOnlyList<SourceDocument> Sources,
        IReadOnlyList<Release> Releases);

    private sealed record SourceDocument(
        string DocumentId,
        string Title,
        string Publisher,
        string EffectiveAt,
        string? PublishedAt,
        string RetrievedAt,
        string Url,
        string Sha256,
        IReadOnlyList<string>? Supersedes,
        IReadOnlyList<string>? Corrects,
        IReadOnlyList<string>? Supplements,
        string? ApplicabilityNote,
        string? CorrectionNote);

    private sealed record Release(
        string MasterVersion,
        string EffectiveFrom,
        string? EffectiveTo,
        IReadOnlyList<string> SourceDocumentIds);
}
