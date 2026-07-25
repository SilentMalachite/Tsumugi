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
            "record-purpose", "derived-byte-length", "pass-through",
            // 属性区分（英数 / 数値 / コード値 / 漢字）が定める文字種と、
            // 一般規則と項目の内容欄が食い違うときにどちらが優先するか（共通編 1.3.2(1)③）。
            "character-class", "rule-precedence",
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

        // storage: "generic" は「算定に効かない転記項目」に限る。その判断は台帳の claim で示す。
        foreach (var mapping in MappingByFieldId.Values.Where(IsGenericStorage))
        {
            var claim = EvidenceClaims.FirstOrDefault(item =>
                string.Equals(item.ClaimId, mapping.FieldId, StringComparison.Ordinal));
            if (claim is null
                || !claim.SourceRefs.Any(sourceRef => sourceRef.Supports.Contains(
                    "pass-through", StringComparer.Ordinal)))
            {
                throw new InvalidDataException(
                    $"fieldId '{mapping.FieldId}' declares storage 'generic' but the evidence ledger has "
                    + "no 'pass-through' claim showing that the value does not affect the calculation.");
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

        ValidateGenericInputNames();

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

    /// <summary>汎用入力の宣言モデル名。この model 名の値は算定入力へは一切渡さない。</summary>
    internal const string GenericInputModel = "ClaimGenericInput";

    /// <summary>
    /// 汎用入力を実装している画面。宣言だけで欄が出るのはこの画面に限る
    /// （他の画面を宣言しても入力欄が無く、確定できないまま fail-close するだけになる）。
    /// </summary>
    private const string GenericInputSurface = "ClaimInputView";

    internal static bool IsGenericStorage(CsvFieldMapping mapping) =>
        string.Equals(mapping.Storage, "generic", StringComparison.Ordinal);

    /// <summary>
    /// <c>storage</c> の契約。既定（未宣言）は型付き。<c>generic</c> は宣言（ラベル・属性・桁数）を
    /// 必須にし、項目定義の属性・桁数と一致させる（UI と入力検証が spec だけで駆動できる状態を保つ）。
    /// </summary>
    private static void ValidateStorageContract(CsvFieldSpecification field, CsvFieldMapping mapping)
    {
        if (mapping.Storage is not null and not "typed" and not "generic")
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' declares an unknown storage '{mapping.Storage}'.");
        }

        if (!IsGenericStorage(mapping))
        {
            if (mapping.GenericInput is not null)
            {
                throw new InvalidDataException(
                    $"fieldId '{field.FieldId}' declares genericInput without storage 'generic'.");
            }

            return;
        }

        if (!string.Equals(mapping.Status, "missing", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' may declare storage 'generic' only with the 'missing' status.");
        }

        if (mapping.GenericInput is not { } declaration)
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' declares storage 'generic' without a genericInput block.");
        }

        if (string.IsNullOrWhiteSpace(declaration.Label)
            || string.IsNullOrWhiteSpace(declaration.Help)
            || !string.Equals(declaration.DataType, field.DataType, StringComparison.Ordinal)
            || declaration.MaxBytes != field.MaxBytes)
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' genericInput must carry a label, a help text and the "
                + "same dataType and maxBytes as the official field definition.");
        }

        // 保存は受給者×サービス提供年月で1個・入力は1画面だけ実装している。日ごと明細やサービス明細に
        // 宣言すると同じ値が全行へ複製され、ファイル単位のレコードでは受給者行が無く生成が落ちる。
        // 対応済みスコープ以外は「動くように見えて誤った CSV を作る」ので読み込み時に拒否する。
        if (CsvRecordRowScopes.Of(CsvRecordRowScopes.RecordIdOf(field.FieldId))
            != CsvRecordRowScope.Recipient)
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' may declare storage 'generic' only on a record whose rows "
                + "occur once per recipient and service month (ADR 0042 supports the monthly scope only).");
        }

        if (!string.Equals(mapping.UiSurface, GenericInputSurface, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"fieldId '{field.FieldId}' declares storage 'generic' for the unsupported uiSurface "
                + $"'{mapping.UiSurface}' (only '{GenericInputSurface}' renders declared generic inputs).");
        }
    }

    /// <summary>
    /// 汎用入力名（<c>targetProperty</c>）の一貫性。複数の項目が同じ値を運ぶ宣言は許すが、
    /// <b>見せ方・型・桁数が食い違う宣言は拒否する</b>（同名の欄が2つ並び、保存時に重複キーで落ちる）。
    /// </summary>
    private void ValidateGenericInputNames()
    {
        foreach (var group in MappingByFieldId.Values
            .Where(IsGenericStorage)
            .GroupBy(mapping => mapping.TargetProperty!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            var declarations = group
                .Select(mapping => (mapping.GenericInput!, mapping.UiSurface))
                .Distinct()
                .ToArray();
            if (declarations.Length > 1)
            {
                throw new InvalidDataException(
                    $"generic input name '{group.Key}' is declared with conflicting labels, data types, "
                    + $"byte lengths or surfaces by fieldIds "
                    + $"{string.Join(", ", group.Select(mapping => mapping.FieldId).Order(StringComparer.Ordinal))}.");
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

        ValidateStorageContract(field, mapping);

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
            // storage: "generic" は Domain の型付き列を増やさないので migrationRequired は false。
            "missing" when IsGenericStorage(mapping) => mapping.MigrationRequired is false
                && string.Equals(mapping.TargetModel, GenericInputModel, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(mapping.TargetProperty)
                && !string.IsNullOrWhiteSpace(mapping.UiSurface)
                && !hasGeneratorRule
                && !hasModelPath
                && !hasInputContract
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
