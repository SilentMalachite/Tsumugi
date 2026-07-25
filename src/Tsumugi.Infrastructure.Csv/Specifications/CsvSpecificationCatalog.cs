using System.Collections.ObjectModel;

namespace Tsumugi.Infrastructure.Csv.Specifications;

public sealed record CsvSpecificationCatalog
{
    private const string LowercaseHexadecimalCharacters = "0123456789abcdef";

    public CsvSpecificationCatalog(
        string version,
        IReadOnlyList<CsvRecordSpecification> commonRecords,
        IReadOnlyList<CsvRecordSpecification> providerRecords,
        IReadOnlyDictionary<string, CsvFieldMapping> mappingByFieldId,
        IReadOnlyDictionary<string, CsvSourceDocument> sourcesById,
        IReadOnlyList<CsvSpecEvidenceClaim>? evidenceClaims = null,
        IReadOnlyList<CsvSpecEvidenceGap>? evidenceGaps = null)
    {
        ArgumentNullException.ThrowIfNull(commonRecords);
        ArgumentNullException.ThrowIfNull(providerRecords);
        ArgumentNullException.ThrowIfNull(mappingByFieldId);
        ArgumentNullException.ThrowIfNull(sourcesById);

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException("CSV specification version is blank.");
        }

        Version = version;
        EvidenceClaims = [.. evidenceClaims ?? []];
        EvidenceGaps = [.. evidenceGaps ?? []];
        CommonRecords = CopyOrderedRecords(commonRecords);
        ProviderRecords = CopyOrderedRecords(providerRecords);
        MappingByFieldId = CopyDictionary(mappingByFieldId, CopyMapping);
        SourcesById = CopyDictionary(sourcesById, CopySource);

        ValidateSources();
        ValidateUniqueRecordIds();
        ValidateRecords(CommonRecords, "common");
        ValidateRecords(ProviderRecords, "provider");
        ValidateFieldIdsAndMappings();
        ValidateEvidence();
    }

    public string Version { get; }

    /// <summary>本文（規則）由来・他文書依拠の判断に対する行単位の出典（ADR 0038）。</summary>
    public IReadOnlyList<CsvSpecEvidenceClaim> EvidenceClaims { get; }

    /// <summary>出典が未付与であることを明示している対象（縮めていく一覧）。</summary>
    public IReadOnlyList<CsvSpecEvidenceGap> EvidenceGaps { get; }

    public IReadOnlyList<CsvRecordSpecification> CommonRecords { get; }

    public IReadOnlyList<CsvRecordSpecification> ProviderRecords { get; }

    public IReadOnlyDictionary<string, CsvFieldMapping> MappingByFieldId { get; }

    public IReadOnlyDictionary<string, CsvSourceDocument> SourcesById { get; }

    private static ReadOnlyCollection<CsvRecordSpecification> CopyOrderedRecords(
        IReadOnlyList<CsvRecordSpecification> records) =>
        Array.AsReadOnly(records
            .OrderBy(record => record.Order)
            .Select(record => record with
            {
                Fields = Array.AsReadOnly(record.Fields
                    .OrderBy(field => field.Position)
                    .Select(field => field with
                    {
                        AllowedCodes = CopyList(field.AllowedCodes),
                    })
                    .ToArray()),
            })
            .ToArray());

    private static ReadOnlyDictionary<string, TValue> CopyDictionary<TValue>(
        IReadOnlyDictionary<string, TValue> source,
        Func<TValue, TValue> copyValue) =>
        new ReadOnlyDictionary<string, TValue>(
            source.ToDictionary(
                item => item.Key,
                item => copyValue(item.Value),
                StringComparer.Ordinal));

    private static ReadOnlyCollection<T> CopyList<T>(IReadOnlyList<T> source) =>
        Array.AsReadOnly(source.ToArray());

    private static CsvFieldMapping CopyMapping(CsvFieldMapping mapping) =>
        mapping with
        {
            SourceContracts = mapping.SourceContracts is null
                ? null
                : Array.AsReadOnly(mapping.SourceContracts
                    .Select(sourceContract => sourceContract.Clone())
                    .ToArray()),
            SourceFieldIds = mapping.SourceFieldIds is null
                ? null
                : CopyList(mapping.SourceFieldIds),
        };

    private static CsvSourceDocument CopySource(CsvSourceDocument source) =>
        source with
        {
            SourceSheets = source.SourceSheets is null
                ? null
                : CopyList(source.SourceSheets),
            ApplicablePages = source.ApplicablePages is null
                ? null
                : CopyList(source.ApplicablePages),
            ApplicablePageTextSha256 = source.ApplicablePageTextSha256 is null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    source.ApplicablePageTextSha256.ToDictionary(
                        item => item.Key,
                        item => item.Value,
                        StringComparer.Ordinal)),
            LiveCheck = source.LiveCheck?.Clone(),
        };

    /// <summary>
    /// 証跡台帳の検証。<b>各 ref の SHA-256 が <c>sources.json</c> の登録値と一致すること</b>を要求するため、
    /// 一次資料を差し替えて <c>sources.json</c> を更新すると、その文書に依拠する claim が
    /// 「根拠を再検証せよ」として fail-close する（どの claim かを例外メッセージが名指しする）。
    /// </summary>
    private void ValidateEvidence()
    {
        var claimKinds = new[] { "rule", "field", "record" };
        var evidenceRoles = new[] { "authoritative", "cross-check" };
        var supports = new[]
        {
            "quote-rule", "prohibited-characters", "zero-value-rule", "file-name-rule", "data-kind",
            "code-table", "field-semantics", "count-rule", "unit-and-format", "derivability",
            "record-purpose",
        };

        var duplicate = EvidenceClaims
            .GroupBy(claim => claim.ClaimId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate spec evidence claimId '{duplicate}'.");
        }

        foreach (var claim in EvidenceClaims)
        {
            if (string.IsNullOrWhiteSpace(claim.ClaimId)
                || string.IsNullOrWhiteSpace(claim.Decision)
                || !claimKinds.Contains(claim.ClaimKind, StringComparer.Ordinal)
                || claim.SourceRefs.Count == 0)
            {
                throw new InvalidDataException($"Spec evidence claim '{claim.ClaimId}' is incomplete.");
            }

            RequireClaimTarget(claim);

            foreach (var sourceRef in claim.SourceRefs)
            {
                if (!SourcesById.TryGetValue(sourceRef.DocumentId, out var document))
                {
                    throw new InvalidDataException(
                        $"Spec evidence claim '{claim.ClaimId}' cites unregistered document "
                        + $"'{sourceRef.DocumentId}'.");
                }

                if (!string.Equals(sourceRef.Sha256, document.Sha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Spec evidence claim '{claim.ClaimId}' pins a stale SHA-256 for document "
                        + $"'{sourceRef.DocumentId}'. Re-verify the citation against the new document "
                        + "before updating the pinned hash.");
                }

                if (string.IsNullOrWhiteSpace(sourceRef.Locator)
                    || !evidenceRoles.Contains(sourceRef.EvidenceRole, StringComparer.Ordinal)
                    || sourceRef.Supports.Count == 0
                    || sourceRef.Supports.Any(support => !supports.Contains(support, StringComparer.Ordinal))
                    || (sourceRef.Quote is not null && string.IsNullOrWhiteSpace(sourceRef.Quote)))
                {
                    throw new InvalidDataException(
                        $"Spec evidence claim '{claim.ClaimId}' has an invalid source reference.");
                }
            }
        }

        foreach (var gap in EvidenceGaps)
        {
            if (string.IsNullOrWhiteSpace(gap.ClaimId)
                || string.IsNullOrWhiteSpace(gap.Reason)
                || string.IsNullOrWhiteSpace(gap.TrackedIn))
            {
                throw new InvalidDataException("Spec evidence gap entries must be fully described.");
            }
        }
    }

    /// <summary>claimId が実在する項目・レコードを指していることを要求する（rule: は横断規則なので対象外）。</summary>
    private void RequireClaimTarget(CsvSpecEvidenceClaim claim)
    {
        switch (claim.ClaimKind)
        {
            case "field" when !MappingByFieldId.ContainsKey(claim.ClaimId):
                throw new InvalidDataException(
                    $"Spec evidence claim '{claim.ClaimId}' does not match any fieldId.");
            case "record" when !CommonRecords.Concat(ProviderRecords)
                .Any(record => string.Equals(record.RecordId, claim.ClaimId, StringComparison.Ordinal)):
                throw new InvalidDataException(
                    $"Spec evidence claim '{claim.ClaimId}' does not match any recordId.");
            case "rule" when !claim.ClaimId.StartsWith("rule:", StringComparison.Ordinal):
                throw new InvalidDataException(
                    $"Spec evidence rule claim '{claim.ClaimId}' must use the 'rule:' prefix.");
            default:
                break;
        }
    }

    private void ValidateSources()
    {
        foreach (var item in SourcesById)
        {
            var source = item.Value;
            if (!string.Equals(item.Key, source.SourceDocumentId, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(source.SourceDocumentId))
            {
                throw new InvalidDataException(
                    $"sourceDocumentId '{item.Key}' does not match its catalog key.");
            }

            if (source.Sha256.Length != 64
                || source.Sha256.Any(character =>
                    !LowercaseHexadecimalCharacters.Contains(character, StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"sourceDocumentId '{source.SourceDocumentId}' has an invalid sha256.");
            }

            if (string.IsNullOrWhiteSpace(source.Title)
                || string.IsNullOrWhiteSpace(source.Version)
                || string.IsNullOrWhiteSpace(source.RetrievedAt)
                || string.IsNullOrWhiteSpace(source.Url)
                || source.SizeBytes <= 0)
            {
                throw new InvalidDataException(
                    $"sourceDocumentId '{source.SourceDocumentId}' has incomplete metadata.");
            }
        }
    }

    private void ValidateRecords(
        IReadOnlyList<CsvRecordSpecification> records,
        string groupName)
    {
        var expectedOrders = Enumerable.Range(1, records.Count);
        if (!records.Select(record => record.Order).SequenceEqual(expectedOrders))
        {
            throw new InvalidDataException($"{groupName} record order must be contiguous from 1.");
        }

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.RecordId))
            {
                throw new InvalidDataException("recordId is blank.");
            }

            if (!SourcesById.ContainsKey(record.SourceDocumentId))
            {
                throw new InvalidDataException(
                    $"recordId '{record.RecordId}' references unknown sourceDocumentId '{record.SourceDocumentId}'.");
            }

            if (record.SourcePage <= 0)
            {
                throw new InvalidDataException($"recordId '{record.RecordId}' has an invalid sourcePage.");
            }

            var expectedPositions = Enumerable.Range(1, record.Fields.Count);
            if (!record.Fields.Select(field => field.Position).SequenceEqual(expectedPositions))
            {
                throw new InvalidDataException(
                    $"recordId '{record.RecordId}' field position must be contiguous from 1.");
            }

            foreach (var field in record.Fields)
            {
                ValidateField(field);
            }
        }
    }

    private void ValidateUniqueRecordIds()
    {
        var duplicateRecordId = CommonRecords.Concat(ProviderRecords)
            .GroupBy(record => record.RecordId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateRecordId is not null)
        {
            throw new InvalidDataException($"Duplicate recordId '{duplicateRecordId}'.");
        }
    }

    private static void ValidateField(CsvFieldSpecification field)
    {
        if (string.IsNullOrWhiteSpace(field.FieldId))
        {
            throw new InvalidDataException("fieldId is blank.");
        }

        if (string.IsNullOrWhiteSpace(field.RequiredWhen))
        {
            throw new InvalidDataException($"fieldId '{field.FieldId}' has a blank requiredWhen.");
        }

        if (string.IsNullOrWhiteSpace(field.OfficialName)
            || string.IsNullOrWhiteSpace(field.DataType)
            || string.IsNullOrWhiteSpace(field.QuoteRule)
            || string.IsNullOrWhiteSpace(field.RequiredWhenSource)
            || field.AllowedCodes is null
            || field.MaxBytes <= 0
            || field.SourcePage <= 0)
        {
            throw new InvalidDataException($"fieldId '{field.FieldId}' has an invalid specification.");
        }
    }

    private void ValidateFieldIdsAndMappings()
    {
        var fields = CommonRecords.Concat(ProviderRecords)
            .SelectMany(record => record.Fields)
            .ToArray();
        var duplicateFieldId = fields
            .GroupBy(field => field.FieldId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateFieldId is not null)
        {
            throw new InvalidDataException($"Duplicate fieldId '{duplicateFieldId}'.");
        }

        foreach (var field in fields)
        {
            if (!MappingByFieldId.TryGetValue(field.FieldId, out var mapping))
            {
                throw new InvalidDataException($"fieldId '{field.FieldId}' is missing a mapping.");
            }

            ValidateMapping(field, mapping);
        }

        var fieldIds = fields.Select(field => field.FieldId).ToHashSet(StringComparer.Ordinal);
        var orphanMappingId = MappingByFieldId.Keys.FirstOrDefault(id => !fieldIds.Contains(id));
        if (orphanMappingId is not null)
        {
            throw new InvalidDataException($"mapping fieldId '{orphanMappingId}' has no CSV field.");
        }

        foreach (var mapping in MappingByFieldId.Values)
        {
            var unknownSourceFieldId = mapping.SourceFieldIds?
                .FirstOrDefault(sourceFieldId => !fieldIds.Contains(sourceFieldId));
            if (unknownSourceFieldId is not null)
            {
                throw new InvalidDataException(
                    $"fieldId '{mapping.FieldId}' references unknown source fieldId '{unknownSourceFieldId}'.");
            }
        }
    }

    private static void ValidateMapping(
        CsvFieldSpecification field,
        CsvFieldMapping mapping)
    {
        if (!string.Equals(field.FieldId, mapping.FieldId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' does not match mapping fieldId '{mapping.FieldId}'.");
        }

        if (string.IsNullOrWhiteSpace(mapping.RequiredCondition))
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' has a blank mapping requiredCondition.");
        }

        if (!string.Equals(field.RequiredWhen, mapping.RequiredCondition, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' required condition differs from its mapping.");
        }

        var hasGeneratorRule = mapping.GeneratorRule is not null;
        var hasModelPath = mapping.ModelPath is not null;
        var hasInputContract = mapping.InputContract is not null;
        var hasMigrationContract = mapping.MigrationRequired is not null
            || mapping.TargetModel is not null
            || mapping.TargetProperty is not null
            || mapping.UiSurface is not null;
        var hasDependencies = mapping.SourceContracts is not null
            || mapping.SourceFieldIds is not null;
        // crossFieldGroup は入力補完（missing）だけが持てる追加宣言。
        if (mapping.CrossFieldGroup is not null
            && !string.Equals(mapping.Status, "missing", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' declares a crossFieldGroup outside the 'missing' status.");
        }

        var validStatus = mapping.Status switch
        {
            "generated" => !string.IsNullOrWhiteSpace(mapping.GeneratorRule)
                && !hasModelPath
                && !hasInputContract
                && !hasMigrationContract,
            "existing" => !string.IsNullOrWhiteSpace(mapping.ModelPath)
                && !hasGeneratorRule
                && !hasInputContract
                && !hasMigrationContract
                && !hasDependencies,
            "explicitInput" => !string.IsNullOrWhiteSpace(mapping.InputContract)
                && !hasGeneratorRule
                && !hasModelPath
                && !hasMigrationContract
                && !hasDependencies,
            "missing" => mapping.MigrationRequired is true
                && !string.IsNullOrWhiteSpace(mapping.TargetModel)
                && !string.IsNullOrWhiteSpace(mapping.TargetProperty)
                && !string.IsNullOrWhiteSpace(mapping.UiSurface)
                && !hasGeneratorRule
                && !hasModelPath
                && !hasInputContract
                && !hasDependencies,
            _ => false,
        };
        if (!validStatus)
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' has an invalid mapping status contract.");
        }

        if (mapping.SourceFieldIds?.Any(sourceFieldId =>
                string.IsNullOrWhiteSpace(sourceFieldId)) is true)
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' has a blank sourceFieldId.");
        }
    }
}
