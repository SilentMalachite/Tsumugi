using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

internal sealed record ClaimSourceCatalogMetadata(
    string DocumentId,
    string Sha256,
    IReadOnlyList<string> Corrects);

internal static class ClaimMasterFileValidator
{
    private const string SupportedSchemaVersion = "2";
    private const string HalfUpRounding = "claim.rounding.units.half-up.v1";
    private const string FixedStep = "claim.step.units.service-code.fixed.v1";
    private const string MultiplyCountStep =
        "claim.step.units.service-code.multiply-count.v1";
    private const string PerServicePercentageStep =
        "claim.step.units.per-service-code.percentage.v1";
    private const string MonthlyPercentageStep =
        "claim.step.units.monthly-target.percentage.v1";
    private const string ProrationStep =
        "claim.step.units.service-code.prorate-by-recipient-count.v1";
    private const string PassThroughStep =
        "claim.step.units.service-code.base-component-pass-through.v1";

    private static readonly IReadOnlyDictionary<string, string> ExpectedFiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["basic-rewards.json"] = "basic-rewards",
            ["additions.json"] = "additions",
            ["region-unit-prices.json"] = "region-unit-prices",
            ["burden-caps.json"] = "burden-caps",
            ["transition-rules.json"] = "transition-rules",
            ["service-codes.json"] = "service-codes",
        };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static PreparedMasterFiles Prepare(
        IReadOnlyDictionary<string, Stream> masterFiles,
        bool sanitizeTransitionHeaders = false)
    {
        ArgumentNullException.ThrowIfNull(masterFiles);
        ValidateFileSet(masterFiles);

        var files = ExpectedFiles.ToDictionary(
            pair => pair.Key,
            pair => DeserializeWithPolicy(
                masterFiles[pair.Key],
                pair.Key,
                sanitizeTransitionHeaders),
            StringComparer.Ordinal);

        foreach (var expected in ExpectedFiles)
            ValidateHeaderWithPolicy(
                expected.Key,
                expected.Value,
                files[expected.Key],
                sanitizeTransitionHeaders);

        ValidateShapes(files, sanitizeTransitionHeaders);
        return new PreparedMasterFiles(files, sanitizeTransitionHeaders);
    }

    internal static ClaimCalculationMasterBundle ValidateAll(
        PreparedMasterFiles prepared,
        IReadOnlyCollection<ClaimSourceCatalogMetadata> sourceCatalog)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(sourceCatalog);
        var sources = BuildSourceIndex(sourceCatalog);
        var files = prepared.Files;
        var sanitizeTransitionHeaders = prepared.SanitizeTransitionHeaders;
        var authority = new SourceAuthorityValidator(sources);
        var conditionDefinitions = ParseConditions(
            files["service-codes.json"],
            sources,
            authority);
        var basicRewards = ParseEntries(
            files["basic-rewards.json"],
            sources,
            entry => ParseBasicReward(entry, sources, authority));
        var unitAdjustments = ParseEntries(
            files["additions.json"],
            sources,
            entry => ParseUnitAdjustment(entry, sources, authority));
        var regionUnitPrices = ParseEntries(
            files["region-unit-prices.json"],
            sources,
            entry => ParseRegionUnitPrice(entry, sources, authority));
        var burdenCaps = ParseEntries(
            files["burden-caps.json"],
            sources,
            entry => ParseBurdenCap(entry, sources, authority));
        var transitionRules = ParseTransitionEntriesWithPolicy(
            files["transition-rules.json"],
            sources,
            authority,
            sanitizeTransitionHeaders);
        var serviceCodes = ParseEntries(
            files["service-codes.json"],
            sources,
            entry => ParseServiceCode(entry, sources, authority));

        ValidatePeriods("basic-rewards.json", basicRewards.Select(ToPeriod));
        ValidatePeriods("additions.json", unitAdjustments.Select(ToPeriod));
        ValidatePeriods("region-unit-prices.json", regionUnitPrices.Select(ToPeriod));
        ValidatePeriods("burden-caps.json", burdenCaps.Select(ToPeriod));
        ValidatePeriods("transition-rules.json", transitionRules.Select(ToPeriod));
        ValidatePeriods("service-codes.json", serviceCodes.Select(ToPeriod));
        ValidatePeriods(
            "service-codes.json",
            conditionDefinitions.Select(condition =>
                new PeriodRow(condition.Key, condition.EffectiveFrom, condition.EffectiveTo)));

        ValidateServiceIdentity(serviceCodes);
        ValidateConditions(serviceCodes, conditionDefinitions);
        ValidateReferences(basicRewards, unitAdjustments, serviceCodes);
        ValidateAdjustmentCycles(unitAdjustments);
        ValidateServiceTargetCycles(serviceCodes);
        ValidateCalculationOrder(unitAdjustments);

        return new ClaimCalculationMasterBundle(
            basicRewards,
            unitAdjustments,
            regionUnitPrices,
            burdenCaps,
            transitionRules,
            serviceCodes,
            conditionDefinitions);
    }

    private static void ValidateShapes(
        Dictionary<string, MasterFile> files,
        bool sanitizeTransitionHeaders)
    {
        var sources = new Dictionary<string, ClaimSourceCatalogMetadata>(StringComparer.Ordinal);
        var authority = new SourceAuthorityValidator(sources, enabled: false);
        _ = ParseConditions(
            files["service-codes.json"],
            sources,
            authority,
            validateSourceCatalog: false);
        _ = ParseEntries(
            files["basic-rewards.json"],
            sources,
            entry => ParseBasicReward(entry, sources, authority),
            validateSourceCatalog: false);
        _ = ParseEntries(
            files["additions.json"],
            sources,
            entry => ParseUnitAdjustment(
                entry,
                sources,
                authority,
                validateRuntimeSelectors: false),
            validateSourceCatalog: false);
        _ = ParseEntries(
            files["region-unit-prices.json"],
            sources,
            entry => ParseRegionUnitPrice(entry, sources, authority),
            validateSourceCatalog: false);
        _ = ParseEntries(
            files["burden-caps.json"],
            sources,
            entry => ParseBurdenCap(entry, sources, authority),
            validateSourceCatalog: false);
        _ = ParseTransitionEntriesWithPolicy(
            files["transition-rules.json"],
            sources,
            authority,
            sanitizeTransitionHeaders,
            validateSourceCatalog: false,
            validateCrossReferences: false);
        _ = ParseEntries(
            files["service-codes.json"],
            sources,
            entry => ParseServiceCode(
                entry,
                sources,
                authority,
                validateCrossReferences: false),
            validateSourceCatalog: false);
    }

    private static void ValidateFileSet(IReadOnlyDictionary<string, Stream> masterFiles)
    {
        var missing = ExpectedFiles.Keys.Except(masterFiles.Keys, StringComparer.Ordinal).ToArray();
        var extra = masterFiles.Keys.Except(ExpectedFiles.Keys, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || extra.Length != 0)
        {
            throw new InvalidDataException(
                $"Claim master filenames must match exactly. Missing: [{string.Join(", ", missing)}]; extra: [{string.Join(", ", extra)}].");
        }

        foreach (var pair in masterFiles)
        {
            if (pair.Value is null)
                throw new ArgumentException(
                    $"Claim master stream '{pair.Key}' cannot be null.",
                    nameof(masterFiles));
            if (!pair.Value.CanRead)
                throw new ArgumentException(
                    $"Claim master stream '{pair.Key}' must be readable.",
                    nameof(masterFiles));
        }
    }

    private static Dictionary<string, ClaimSourceCatalogMetadata> BuildSourceIndex(
        IReadOnlyCollection<ClaimSourceCatalogMetadata> sourceCatalog)
    {
        if (sourceCatalog.Any(source => source is null))
            throw new InvalidDataException("Source catalog metadata cannot contain null.");
        var duplicate = sourceCatalog
            .GroupBy(source => source.DocumentId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
            throw new InvalidDataException($"Source catalog metadata duplicates '{duplicate}'.");
        return sourceCatalog.ToDictionary(source => source.DocumentId, StringComparer.Ordinal);
    }

    private static MasterFile DeserializeWithPolicy(
        Stream stream,
        string fileName,
        bool sanitizeTransitionHeaders)
    {
        try
        {
            return Deserialize(stream, fileName);
        }
        catch (Exception exception)
            when (sanitizeTransitionHeaders
                  && string.Equals(fileName, "transition-rules.json", StringComparison.Ordinal)
                  && exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.InvalidMaster);
        }
    }

    private static MasterFile Deserialize(Stream stream, string fileName)
    {
        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(stream, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' contains invalid JSON.",
                exception);
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid(fileName, "<root>", "<root>", "must be an object");
        var actual = root.EnumerateObject().Select(property => property.Name).ToArray();
        var allowed = new[] { "schemaVersion", "masterKind", "conditionDefinitions", "entries" };
        var unknown = actual.Except(allowed, StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
            throw Invalid(fileName, "<root>", unknown, "is not allowed");

        var schemaVersion = RequiredString(root, "schemaVersion", fileName, "<root>");
        var masterKind = RequiredString(root, "masterKind", fileName, "<root>");
        var entriesElement = Required(root, "entries", fileName, "<root>");
        if (entriesElement.ValueKind != JsonValueKind.Array)
            throw Invalid(fileName, "<root>", "entries", "must be an array");
        var entries = entriesElement.EnumerateArray()
            .Select(element => element.Clone())
            .ToArray();
        IReadOnlyList<JsonElement>? conditions = null;
        if (root.TryGetProperty("conditionDefinitions", out var conditionsElement))
        {
            if (conditionsElement.ValueKind != JsonValueKind.Array)
                throw Invalid(
                    fileName,
                    "<root>",
                    "conditionDefinitions",
                    "must be an array");
            conditions = conditionsElement.EnumerateArray()
                .Select(element => element.Clone())
                .ToArray();
        }

        return new MasterFile(fileName, schemaVersion, masterKind, entries, conditions);
    }

    private static void ValidateHeaderWithPolicy(
        string fileName,
        string expectedKind,
        MasterFile file,
        bool sanitizeTransitionHeaders)
    {
        try
        {
            ValidateHeader(fileName, expectedKind, file);
        }
        catch (Exception exception)
            when (sanitizeTransitionHeaders
                  && string.Equals(fileName, "transition-rules.json", StringComparison.Ordinal)
                  && exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.InvalidMaster);
        }
    }

    private static void ValidateHeader(
        string fileName,
        string expectedKind,
        MasterFile file)
    {
        if (!string.Equals(file.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(
                fileName,
                "<root>",
                "schemaVersion",
                $"unsupported value '{file.SchemaVersion}'");
        }

        if (!string.Equals(file.MasterKind, expectedKind, StringComparison.Ordinal))
        {
            throw Invalid(
                fileName,
                "<root>",
                "masterKind",
                $"must be '{expectedKind}'");
        }

        var isServiceCode = string.Equals(
            expectedKind,
            "service-codes",
            StringComparison.Ordinal);
        if (isServiceCode != (file.ConditionDefinitions is not null))
        {
            throw Invalid(
                fileName,
                "<root>",
                "conditionDefinitions",
                isServiceCode
                    ? "is required for service-codes"
                    : "is allowed only for service-codes");
        }
    }

    private static T[] ParseEntries<T>(
        MasterFile file,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        Func<RawEntry, T> parse,
        bool validateSourceCatalog = true) =>
        file.Entries.Select(entry => parse(ParseEnvelope(
            entry,
            file,
            sources,
            validateSourceCatalog))).ToArray();

    private static OfficeClaimProfileTransitionRuleMasterRow[]
        ParseTransitionEntriesWithPolicy(
            MasterFile file,
            IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
            SourceAuthorityValidator authority,
            bool sanitizeTransitionHeaders,
            bool validateSourceCatalog = true,
            bool validateCrossReferences = true)
    {
        try
        {
            return ParseEntries(
                file,
                sources,
                entry => ParseTransitionRule(
                    entry,
                    sources,
                    authority,
                    validateCrossReferences),
                validateSourceCatalog);
        }
        catch (Exception exception)
            when (sanitizeTransitionHeaders
                  && exception is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.InvalidMaster);
        }
    }

    private static RawEntry ParseEnvelope(
        JsonElement element,
        MasterFile file,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        bool validateSourceCatalog)
    {
        var key = element.ValueKind == JsonValueKind.Object
                  && element.TryGetProperty("key", out var keyElement)
                  && keyElement.ValueKind == JsonValueKind.String
            ? keyElement.GetString() ?? "<entry>"
            : "<entry>";
        RequireProperties(
            element,
            file.FileName,
            key,
            "<entry>",
            "key",
            "effectiveFrom",
            "effectiveTo",
            "sourceRefs",
            "values");
        key = RequiredString(element, "key", file.FileName, key);
        var effectiveFrom = ParseMonth(
            RequiredString(element, "effectiveFrom", file.FileName, key),
            file.FileName,
            key,
            "effectiveFrom");
        var effectiveTo = NullableMonth(element, "effectiveTo", file.FileName, key);
        if (effectiveTo is { } end && end < effectiveFrom)
            throw Invalid(file.FileName, key, "effectiveTo", "reverses the effective range");
        var sourceRefs = ParseSourceRefs(
            Required(element, "sourceRefs", file.FileName, key),
            file.FileName,
            key,
            sources,
            validateSourceCatalog);
        var values = Required(element, "values", file.FileName, key);
        if (values.ValueKind != JsonValueKind.Object)
            throw Invalid(file.FileName, key, "values", "must be an object");
        return new RawEntry(
            file.FileName,
            key,
            effectiveFrom,
            effectiveTo,
            sourceRefs,
            values);
    }

    private static ClaimConditionDefinition[] ParseConditions(
        MasterFile serviceFile,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority,
        bool validateSourceCatalog = true)
    {
        return serviceFile.ConditionDefinitions!
            .Select(element => ParseCondition(
                element,
                serviceFile.FileName,
                sources,
                authority,
                validateSourceCatalog))
            .ToArray();
    }

    private static ClaimConditionDefinition ParseCondition(
        JsonElement element,
        string fileName,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority,
        bool validateSourceCatalog)
    {
        var key = element.ValueKind == JsonValueKind.Object
                  && element.TryGetProperty("key", out var keyElement)
                  && keyElement.ValueKind == JsonValueKind.String
            ? keyElement.GetString() ?? "<condition>"
            : "<condition>";
        var @operator = RequiredString(element, "operator", fileName, key);
        var propertyNames = @operator == "in"
            ? new[]
            {
                "key", "effectiveFrom", "effectiveTo", "kind", "operator", "values",
                "sourceRefs",
            }
            : new[]
            {
                "key", "effectiveFrom", "effectiveTo", "kind", "operator", "value",
                "sourceRefs",
            };
        RequireProperties(element, fileName, key, "<condition>", propertyNames);
        key = RequiredString(element, "key", fileName, key);
        var effectiveFrom = ParseMonth(
            RequiredString(element, "effectiveFrom", fileName, key),
            fileName,
            key,
            "effectiveFrom");
        var effectiveTo = NullableMonth(element, "effectiveTo", fileName, key);
        if (effectiveTo is { } end && end < effectiveFrom)
            throw Invalid(fileName, key, "effectiveTo", "reverses the effective range");
        var kind = ParseConditionKind(
            RequiredString(element, "kind", fileName, key),
            fileName,
            key);
        var conditionOperator = ParseConditionOperator(@operator, fileName, key);
        var operand = ParseConditionOperand(
            element,
            kind,
            conditionOperator,
            fileName,
            key);
        var sourceRefs = ParseSourceRefs(
            Required(element, "sourceRefs", fileName, key),
            fileName,
            key,
            sources,
            validateSourceCatalog);
        ValidateSupports(
            sourceRefs,
            [ClaimSourceSupport.Conditions, ClaimSourceSupport.EffectivePeriod],
            authority,
            fileName,
            key);
        return new ClaimConditionDefinition(
            key,
            effectiveFrom,
            effectiveTo,
            kind,
            conditionOperator,
            operand,
            sourceRefs);
    }

    private static ClaimConditionKind ParseConditionKind(
        string value,
        string fileName,
        string key) => value switch
        {
            "reward-system" => ClaimConditionKind.RewardSystem,
            "payment-band" => ClaimConditionKind.PaymentBand,
            "capacity" => ClaimConditionKind.Capacity,
            "staffing" => ClaimConditionKind.Staffing,
            "average-wage-band" => ClaimConditionKind.AverageWageBand,
            "plan-status" => ClaimConditionKind.PlanStatus,
            "shortage-duration" => ClaimConditionKind.ShortageDuration,
            "municipality-ownership" => ClaimConditionKind.MunicipalityOwnership,
            "r8-reform-status" => ClaimConditionKind.R8ReformStatus,
            "facility-classification" => ClaimConditionKind.FacilityClassification,
            "employment-outcome-count" => ClaimConditionKind.EmploymentOutcomeCount,
            _ => throw Invalid(fileName, key, "kind", $"unknown condition kind '{value}'"),
        };

    private static ClaimConditionOperator ParseConditionOperator(
        string value,
        string fileName,
        string key) => value switch
        {
            "equals" => ClaimConditionOperator.Equals,
            "in" => ClaimConditionOperator.In,
            "less-than" => ClaimConditionOperator.LessThan,
            "less-than-or-equal" => ClaimConditionOperator.LessThanOrEqual,
            "greater-than" => ClaimConditionOperator.GreaterThan,
            "greater-than-or-equal" => ClaimConditionOperator.GreaterThanOrEqual,
            _ => throw Invalid(fileName, key, "operator", $"unknown operator '{value}'"),
        };

    private static ClaimConditionOperand ParseConditionOperand(
        JsonElement element,
        ClaimConditionKind kind,
        ClaimConditionOperator @operator,
        string fileName,
        string key)
    {
        var isToken = kind is
            ClaimConditionKind.RewardSystem or
            ClaimConditionKind.PaymentBand or
            ClaimConditionKind.Staffing or
            ClaimConditionKind.AverageWageBand or
            ClaimConditionKind.PlanStatus or
            ClaimConditionKind.R8ReformStatus or
            ClaimConditionKind.FacilityClassification;
        var isInteger = kind is
            ClaimConditionKind.Capacity or
            ClaimConditionKind.ShortageDuration or
            ClaimConditionKind.EmploymentOutcomeCount;

        if (isToken && @operator is ClaimConditionOperator.Equals)
        {
            var value = RequiredString(element, "value", fileName, key);
            ValidateConditionToken(kind, value, fileName, key);
            return new ClaimConditionTokenOperand(value);
        }

        if (isToken && @operator is ClaimConditionOperator.In)
        {
            var values = StringArray(
                Required(element, "values", fileName, key),
                fileName,
                key,
                "values",
                requireNonEmpty: true);
            foreach (var value in values)
                ValidateConditionToken(kind, value, fileName, key);
            return new ClaimConditionTokenSetOperand(values);
        }

        if (isInteger
            && @operator is not ClaimConditionOperator.In)
        {
            var value = NonNegativeInt(element, "value", fileName, key);
            return new ClaimConditionIntegerOperand(value);
        }

        if (kind is ClaimConditionKind.MunicipalityOwnership
            && @operator is ClaimConditionOperator.Equals)
        {
            var valueElement = Required(element, "value", fileName, key);
            if (valueElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                throw Invalid(fileName, key, "value", "must be boolean");
            return new ClaimConditionBooleanOperand(valueElement.GetBoolean());
        }

        throw Invalid(
            fileName,
            key,
            "operator",
            $"is not allowed for condition kind '{kind}'");
    }

    private static void ValidateConditionToken(
        ClaimConditionKind kind,
        string value,
        string fileName,
        string key)
    {
        if (kind is ClaimConditionKind.PlanStatus
            && value is not ("created" or "not-created"))
        {
            throw Invalid(fileName, key, "value", $"unknown plan-status '{value}'");
        }

        if (kind is ClaimConditionKind.R8ReformStatus
            && value is not (
                "not-applicable-before-r8" or
                "reform-target" or
                "reform-exempt" or
                "unchanged-below-15000"))
        {
            throw Invalid(fileName, key, "value", $"unknown r8-reform-status '{value}'");
        }
    }

    private static BasicRewardMasterRow ParseBasicReward(
        RawEntry entry,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority)
    {
        RequireProperties(
            entry.Values,
            entry.FileName,
            entry.Key,
            "values",
            "paymentBand",
            "staffingKey",
            "capacityKey",
            "serviceCode",
            "baseUnits");
        ValidateSupports(
            entry.SourceRefs,
            [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod],
            authority,
            entry.FileName,
            entry.Key);
        return new BasicRewardMasterRow(
            entry.Key,
            RequiredString(entry.Values, "paymentBand", entry.FileName, entry.Key),
            RequiredString(entry.Values, "staffingKey", entry.FileName, entry.Key),
            RequiredString(entry.Values, "capacityKey", entry.FileName, entry.Key),
            RequiredString(entry.Values, "serviceCode", entry.FileName, entry.Key),
            NonNegativeInt(entry.Values, "baseUnits", entry.FileName, entry.Key),
            entry.EffectiveFrom,
            entry.EffectiveTo,
            entry.SourceRefs);
    }

    private static UnitAdjustmentMasterRow ParseUnitAdjustment(
        RawEntry entry,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority,
        bool validateRuntimeSelectors = true)
    {
        RequireProperties(
            entry.Values,
            entry.FileName,
            entry.Key,
            "values",
            "amount",
            "calculationStepId",
            "roundingRuleId",
            "billingUnit");
        var amount = ParseAmount(
            Required(entry.Values, "amount", entry.FileName, entry.Key),
            entry.FileName,
            entry.Key,
            validateRuntimeSelectors);
        var calculationStepId = RequiredString(
            entry.Values,
            "calculationStepId",
            entry.FileName,
            entry.Key);
        var roundingRuleId = NullableString(
            entry.Values,
            "roundingRuleId",
            entry.FileName,
            entry.Key);
        ValidateAmountMatrix(
            amount,
            calculationStepId,
            roundingRuleId,
            entry.FileName,
            entry.Key,
            "values");
        ValidateSupports(
            entry.SourceRefs,
            [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod],
            authority,
            entry.FileName,
            entry.Key);
        return new UnitAdjustmentMasterRow(
            entry.Key,
            amount,
            calculationStepId,
            roundingRuleId,
            ParseBillingUnit(
                RequiredString(entry.Values, "billingUnit", entry.FileName, entry.Key),
                entry.FileName,
                entry.Key),
            entry.EffectiveFrom,
            entry.EffectiveTo,
            entry.SourceRefs);
    }

    private static RegionUnitPriceMasterRow ParseRegionUnitPrice(
        RawEntry entry,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority)
    {
        RequireProperties(
            entry.Values,
            entry.FileName,
            entry.Key,
            "values",
            "regionKey",
            "serviceKind",
            "unitPriceYen");
        ValidateSupports(
            entry.SourceRefs,
            [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod],
            authority,
            entry.FileName,
            entry.Key);
        return new RegionUnitPriceMasterRow(
            entry.Key,
            RequiredString(entry.Values, "regionKey", entry.FileName, entry.Key),
            RequiredString(entry.Values, "serviceKind", entry.FileName, entry.Key),
            NonNegativeDecimalString(
                entry.Values,
                "unitPriceYen",
                entry.FileName,
                entry.Key),
            entry.EffectiveFrom,
            entry.EffectiveTo,
            entry.SourceRefs);
    }

    private static BurdenCapMasterRow ParseBurdenCap(
        RawEntry entry,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority)
    {
        RequireProperties(
            entry.Values,
            entry.FileName,
            entry.Key,
            "values",
            "burdenCategory",
            "capYen");
        ValidateSupports(
            entry.SourceRefs,
            [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod],
            authority,
            entry.FileName,
            entry.Key);
        return new BurdenCapMasterRow(
            entry.Key,
            RequiredString(entry.Values, "burdenCategory", entry.FileName, entry.Key),
            NonNegativeInt(entry.Values, "capYen", entry.FileName, entry.Key),
            entry.EffectiveFrom,
            entry.EffectiveTo,
            entry.SourceRefs);
    }

    private static OfficeClaimProfileTransitionRuleMasterRow ParseTransitionRule(
        RawEntry entry,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority,
        bool validateCrossReferences = true)
    {
        RequireProperties(
            entry.Values,
            entry.FileName,
            entry.Key,
            "values",
            "masterVersion",
            "allowedAverageWageBandOptions",
            "allowedOptionsByR8ReformStatus",
            "r8EffectiveDate",
            "filedTransitionEndRule",
            "filedTransitionDurationYears");
        var options = ParseOptions(
            Required(
                entry.Values,
                "allowedAverageWageBandOptions",
                entry.FileName,
                entry.Key),
            entry.FileName,
            entry.Key);
        var optionsByStatusElement = Required(
            entry.Values,
            "allowedOptionsByR8ReformStatus",
            entry.FileName,
            entry.Key);
        if (optionsByStatusElement.ValueKind != JsonValueKind.Object
            || !optionsByStatusElement.EnumerateObject().Any())
        {
            throw Invalid(
                entry.FileName,
                entry.Key,
                "allowedOptionsByR8ReformStatus",
                "must be a non-empty object");
        }

        var allowedSet = options.ToHashSet();
        var optionsByStatus = optionsByStatusElement.EnumerateObject().ToDictionary(
            property => ParseR8Status(property.Name, entry.FileName, entry.Key),
            property => (IReadOnlyCollection<AverageWageBandOption>)ParseOptions(
                property.Value,
                entry.FileName,
                entry.Key),
            EqualityComparer<R8ReformStatus>.Default);
        if (validateCrossReferences
            && optionsByStatus.Values.Any(statusOptions =>
                statusOptions.Count == 0
                || statusOptions.Any(option => !allowedSet.Contains(option))))
        {
            throw Invalid(
                entry.FileName,
                entry.Key,
                "allowedOptionsByR8ReformStatus",
                "must contain non-empty subsets of allowedAverageWageBandOptions");
        }

        var endRule = RequiredString(
            entry.Values,
            "filedTransitionEndRule",
            entry.FileName,
            entry.Key) switch
        {
            "add-years-exclusive" => FiledTransitionExclusiveEndRule.AddYearsExclusive,
            var value => throw Invalid(
                entry.FileName,
                entry.Key,
                "filedTransitionEndRule",
                $"unknown value '{value}'"),
        };
        ValidateSupports(
            entry.SourceRefs,
            [ClaimSourceSupport.MasterValues, ClaimSourceSupport.EffectivePeriod],
            authority,
            entry.FileName,
            entry.Key);
        return new OfficeClaimProfileTransitionRuleMasterRow(
            entry.Key,
            new ClaimMasterVersion(RequiredString(
                entry.Values,
                "masterVersion",
                entry.FileName,
                entry.Key)),
            options,
            optionsByStatus,
            ParseDate(
                RequiredString(
                    entry.Values,
                    "r8EffectiveDate",
                    entry.FileName,
                    entry.Key),
                entry.FileName,
                entry.Key,
                "r8EffectiveDate"),
            endRule,
            PositiveInt(
                entry.Values,
                "filedTransitionDurationYears",
                entry.FileName,
                entry.Key),
            entry.EffectiveFrom,
            entry.EffectiveTo,
            entry.SourceRefs);
    }

    private static ServiceCodeMasterRow ParseServiceCode(
        RawEntry entry,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        SourceAuthorityValidator authority,
        bool validateCrossReferences = true)
    {
        RequireProperties(
            entry.Values,
            entry.FileName,
            entry.Key,
            "values",
            "serviceCode",
            "officialLabel",
            "serviceKind",
            "selectors",
            "conditionSelectors",
            "unitRule",
            "componentRefs");
        var selectors = StringArray(
            Required(entry.Values, "selectors", entry.FileName, entry.Key),
            entry.FileName,
            entry.Key,
            "selectors",
            requireNonEmpty: true);
        var conditionSelectors = StringArray(
            Required(entry.Values, "conditionSelectors", entry.FileName, entry.Key),
            entry.FileName,
            entry.Key,
            "conditionSelectors",
            requireNonEmpty: false);
        var unitRule = ParseUnitRule(
            Required(entry.Values, "unitRule", entry.FileName, entry.Key),
            entry.FileName,
            entry.Key,
            conditionSelectors,
            validateCrossReferences);
        var componentRefs = ParseComponentRefs(
            Required(entry.Values, "componentRefs", entry.FileName, entry.Key),
            entry.FileName,
            entry.Key);
        var requiredSupports = new HashSet<ClaimSourceSupport>
        {
            ClaimSourceSupport.ServiceIdentity,
            ClaimSourceSupport.Selectors,
            ClaimSourceSupport.UnitRuleKind,
            ClaimSourceSupport.EffectivePeriod,
        };
        switch (unitRule)
        {
            case FixedCompositeUnitRule:
                requiredSupports.Add(ClaimSourceSupport.UnitRuleValue);
                break;
            case UnitAdditionRule addition:
                requiredSupports.Add(ClaimSourceSupport.UnitRuleValue);
                requiredSupports.Add(ClaimSourceSupport.UnitRuleStep);
                if (addition.Amount is
                    UnitsPerCountAmount or
                    PercentageOfTargetAmount or
                    ProratedUnitsAmount)
                {
                    requiredSupports.Add(ClaimSourceSupport.UnitRuleTarget);
                }

                if (addition.Amount is PercentageOfTargetAmount or ProratedUnitsAmount)
                    requiredSupports.Add(ClaimSourceSupport.UnitRuleRounding);
                break;
            case BaseComponentPassThroughRule:
                requiredSupports.Add(ClaimSourceSupport.UnitRuleTarget);
                requiredSupports.Add(ClaimSourceSupport.UnitRuleStep);
                break;
            case FactorChainRule:
                requiredSupports.Add(ClaimSourceSupport.UnitRuleValue);
                requiredSupports.Add(ClaimSourceSupport.UnitRuleTarget);
                requiredSupports.Add(ClaimSourceSupport.UnitRuleStep);
                requiredSupports.Add(ClaimSourceSupport.UnitRuleRounding);
                break;
        }

        if (conditionSelectors.Length > 0)
            requiredSupports.Add(ClaimSourceSupport.Conditions);
        ValidateSupports(
            entry.SourceRefs,
            requiredSupports,
            authority,
            entry.FileName,
            entry.Key);
        return new ServiceCodeMasterRow(
            entry.Key,
            RequiredString(entry.Values, "serviceCode", entry.FileName, entry.Key),
            RequiredString(entry.Values, "officialLabel", entry.FileName, entry.Key),
            RequiredString(entry.Values, "serviceKind", entry.FileName, entry.Key),
            selectors,
            conditionSelectors,
            unitRule,
            componentRefs,
            entry.EffectiveFrom,
            entry.EffectiveTo,
            entry.SourceRefs);
    }

    private static UnitAdjustmentAmount ParseAmount(
        JsonElement element,
        string fileName,
        string key,
        bool validateRuntimeSelectors = true)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid(fileName, key, "amount", "must be an object");
        var kind = RequiredString(element, "kind", fileName, key);
        return kind switch
        {
            "fixed-units" => ParseFixedUnits(element, fileName, key),
            "units-per-count" => ParseUnitsPerCount(
                element,
                fileName,
                key,
                validateRuntimeSelectors),
            "percentage-of-target" => ParsePercentageOfTarget(element, fileName, key),
            "prorated-units" => ParseProratedUnits(
                element,
                fileName,
                key,
                validateRuntimeSelectors),
            _ => throw Invalid(fileName, key, "amount.kind", $"unknown value '{kind}'"),
        };
    }

    private static FixedUnitsAmount ParseFixedUnits(
        JsonElement element,
        string fileName,
        string key)
    {
        RequireProperties(element, fileName, key, "amount", "kind", "addedUnits");
        return new FixedUnitsAmount(PositiveInt(element, "addedUnits", fileName, key));
    }

    private static UnitsPerCountAmount ParseUnitsPerCount(
        JsonElement element,
        string fileName,
        string key,
        bool validateRuntimeSelectors)
    {
        RequireProperties(
            element,
            fileName,
            key,
            "amount",
            "kind",
            "unitsPerCount",
            "countSelector");
        var selector = RequiredString(element, "countSelector", fileName, key);
        if (validateRuntimeSelectors
            && !string.Equals(
                selector,
                "previous-year-six-month-employment-count",
                StringComparison.Ordinal))
        {
            throw Invalid(fileName, key, "countSelector", $"unknown value '{selector}'");
        }

        return new UnitsPerCountAmount(
            PositiveInt(element, "unitsPerCount", fileName, key),
            selector);
    }

    private static PercentageOfTargetAmount ParsePercentageOfTarget(
        JsonElement element,
        string fileName,
        string key)
    {
        RequireProperties(
            element,
            fileName,
            key,
            "amount",
            "kind",
            "percentage",
            "applicationKind",
            "percentageBaseScope",
            "targetSelector",
            "calculationOrder");
        var applicationKind = RequiredString(
            element,
            "applicationKind",
            fileName,
            key) switch
        {
            "add" => PercentageApplicationKind.Add,
            "subtract" => PercentageApplicationKind.Subtract,
            "replace" => PercentageApplicationKind.Replace,
            var value => throw Invalid(
                fileName,
                key,
                "applicationKind",
                $"unknown value '{value}'"),
        };
        var baseScope = RequiredString(
            element,
            "percentageBaseScope",
            fileName,
            key) switch
        {
            "per-service-code-unit" => PercentageBaseScope.PerServiceCodeUnit,
            "monthly-target-unit-sum" => PercentageBaseScope.MonthlyTargetUnitSum,
            var value => throw Invalid(
                fileName,
                key,
                "percentageBaseScope",
                $"unknown value '{value}'"),
        };
        return new PercentageOfTargetAmount(
            PositiveDecimalString(element, "percentage", fileName, key),
            applicationKind,
            baseScope,
            RequiredString(element, "targetSelector", fileName, key),
            PositiveInt(element, "calculationOrder", fileName, key));
    }

    private static ProratedUnitsAmount ParseProratedUnits(
        JsonElement element,
        string fileName,
        string key,
        bool validateRuntimeSelectors)
    {
        var hasMaximumRecipientsPerStaff = element.TryGetProperty(
            "maximumRecipientsPerStaff",
            out _);
        if (hasMaximumRecipientsPerStaff)
        {
            RequireProperties(
                element,
                fileName,
                key,
                "amount",
                "kind",
                "poolUnitsPerStaff",
                "staffCountSelector",
                "recipientCountSelector",
                "maximumRecipientsPerStaff");
        }
        else
        {
            RequireProperties(
                element,
                fileName,
                key,
                "amount",
                "kind",
                "poolUnitsPerStaff",
                "staffCountSelector",
                "recipientCountSelector");
        }
        var staffSelector = RequiredString(
            element,
            "staffCountSelector",
            fileName,
            key);
        var recipientSelector = RequiredString(
            element,
            "recipientCountSelector",
            fileName,
            key);
        if (validateRuntimeSelectors
            && !string.Equals(
                staffSelector,
                "medical-coordination-v-visiting-nurse-count",
                StringComparison.Ordinal))
        {
            throw Invalid(
                fileName,
                key,
                "staffCountSelector",
                $"unknown value '{staffSelector}'");
        }

        if (validateRuntimeSelectors
            && !string.Equals(
                recipientSelector,
                "medical-coordination-v-supported-recipient-count",
                StringComparison.Ordinal))
        {
            throw Invalid(
                fileName,
                key,
                "recipientCountSelector",
                $"unknown value '{recipientSelector}'");
        }

        return new ProratedUnitsAmount(
            PositiveInt(element, "poolUnitsPerStaff", fileName, key),
            staffSelector,
            recipientSelector,
            hasMaximumRecipientsPerStaff
                ? PositiveInt(element, "maximumRecipientsPerStaff", fileName, key)
                : null);
    }

    private static ServiceCodeUnitRule ParseUnitRule(
        JsonElement element,
        string fileName,
        string key,
        IReadOnlyList<string> entryConditionSelectors,
        bool validateCrossReferences)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid(fileName, key, "unitRule", "must be an object");
        var kind = RequiredString(element, "kind", fileName, key);
        return kind switch
        {
            "fixed-composite-unit" => ParseFixedCompositeRule(element, fileName, key),
            "unit-addition" => ParseUnitAdditionRule(
                element,
                fileName,
                key,
                validateCrossReferences),
            "formula" => ParseFormulaRule(
                element,
                fileName,
                key,
                entryConditionSelectors,
                validateCrossReferences),
            _ => throw Invalid(fileName, key, "unitRule", $"unknown kind '{kind}'"),
        };
    }

    private static FixedCompositeUnitRule ParseFixedCompositeRule(
        JsonElement element,
        string fileName,
        string key)
    {
        RequireProperties(
            element,
            fileName,
            key,
            "unitRule",
            "kind",
            "finalUnits",
            "billingUnit");
        var finalUnits = Integer(element, "finalUnits", fileName, key);
        if (finalUnits == 0)
            throw Invalid(fileName, key, "finalUnits", "must be nonzero");
        return new FixedCompositeUnitRule(
            finalUnits,
            ParseBillingUnit(
                RequiredString(element, "billingUnit", fileName, key),
                fileName,
                key));
    }

    private static UnitAdditionRule ParseUnitAdditionRule(
        JsonElement element,
        string fileName,
        string key,
        bool validateRuntimeSelectors)
    {
        RequireProperties(
            element,
            fileName,
            key,
            "unitRule",
            "kind",
            "adjustmentComponentKey",
            "amount",
            "calculationStepId",
            "roundingRuleId",
            "billingUnit");
        var amount = ParseAmount(
            Required(element, "amount", fileName, key),
            fileName,
            key,
            validateRuntimeSelectors);
        var step = RequiredString(element, "calculationStepId", fileName, key);
        var rounding = NullableString(element, "roundingRuleId", fileName, key);
        ValidateAmountMatrix(amount, step, rounding, fileName, key, "unitRule");
        return new UnitAdditionRule(
            RequiredString(element, "adjustmentComponentKey", fileName, key),
            amount,
            step,
            rounding,
            ParseBillingUnit(
                RequiredString(element, "billingUnit", fileName, key),
                fileName,
                key));
    }

    private static FormulaUnitRule ParseFormulaRule(
        JsonElement element,
        string fileName,
        string key,
        IReadOnlyList<string> entryConditionSelectors,
        bool validateCrossReferences)
    {
        var mode = RequiredString(element, "mode", fileName, key);
        if (string.Equals(mode, "base-component-pass-through", StringComparison.Ordinal))
        {
            RequireProperties(
                element,
                fileName,
                key,
                "unitRule",
                "kind",
                "mode",
                "baseComponentKey",
                "calculationStepId",
                "roundingRuleId",
                "billingUnit");
            var step = RequiredString(element, "calculationStepId", fileName, key);
            var rounding = NullableString(element, "roundingRuleId", fileName, key);
            if (!string.Equals(step, PassThroughStep, StringComparison.Ordinal)
                || rounding is not null)
            {
                throw Invalid(
                    fileName,
                    key,
                    "unitRule",
                    "base-component-pass-through has an invalid step or rounding rule");
            }

            return new BaseComponentPassThroughRule(
                RequiredString(element, "baseComponentKey", fileName, key),
                step,
                rounding,
                ParseBillingUnit(
                    RequiredString(element, "billingUnit", fileName, key),
                    fileName,
                    key));
        }

        if (!string.Equals(mode, "factor-chain", StringComparison.Ordinal))
            throw Invalid(fileName, key, "unitRule.mode", $"unknown value '{mode}'");
        RequireProperties(
            element,
            fileName,
            key,
            "unitRule",
            "kind",
            "mode",
            "baseComponentKey",
            "factors",
            "billingUnit");
        var factorsElement = Required(element, "factors", fileName, key);
        if (factorsElement.ValueKind != JsonValueKind.Array)
            throw Invalid(fileName, key, "factors", "must be an array");
        var factors = factorsElement.EnumerateArray()
            .Select(factor => ParseFactor(
                factor,
                fileName,
                key,
                entryConditionSelectors,
                validateCrossReferences))
            .ToArray();
        if (factors.Length == 0)
            throw Invalid(fileName, key, "factors", "must be non-empty");
        var orders = factors.Select(factor => factor.Order).Order().ToArray();
        if (validateCrossReferences
            && !orders.SequenceEqual(Enumerable.Range(1, orders.Length)))
            throw Invalid(fileName, key, "factors.order", "must be unique and contiguous from one");
        return new FactorChainRule(
            RequiredString(element, "baseComponentKey", fileName, key),
            factors,
            ParseBillingUnit(
                RequiredString(element, "billingUnit", fileName, key),
                fileName,
                key));
    }

    private static ServiceCodeFormulaFactor ParseFactor(
        JsonElement element,
        string fileName,
        string key,
        IReadOnlyList<string> entryConditionSelectors,
        bool validateCrossReferences)
    {
        RequireProperties(
            element,
            fileName,
            key,
            "factor",
            "order",
            "rate",
            "conditionSelectors",
            "calculationStepId",
            "roundingRuleId");
        var selectors = StringArray(
            Required(element, "conditionSelectors", fileName, key),
            fileName,
            key,
            "factor.conditionSelectors",
            requireNonEmpty: true);
        if (validateCrossReferences
            && selectors.Except(entryConditionSelectors, StringComparer.Ordinal).Any())
        {
            throw Invalid(
                fileName,
                key,
                "factor.conditionSelectors",
                "must be a subset of entry conditionSelectors");
        }

        var step = RequiredString(element, "calculationStepId", fileName, key);
        var rounding = RequiredString(element, "roundingRuleId", fileName, key);
        if (!string.Equals(step, PerServicePercentageStep, StringComparison.Ordinal)
            || !string.Equals(rounding, HalfUpRounding, StringComparison.Ordinal))
        {
            throw Invalid(fileName, key, "factor", "has an invalid step or rounding rule");
        }

        var rate = PositiveDecimalString(element, "rate", fileName, key);
        if (rate > 1m)
            throw Invalid(fileName, key, "rate", "must be less than or equal to one");
        return new ServiceCodeFormulaFactor(
            PositiveInt(element, "order", fileName, key),
            rate,
            selectors,
            step,
            rounding);
    }

    private static ClaimComponentRef[] ParseComponentRefs(
        JsonElement element,
        string fileName,
        string key)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw Invalid(fileName, key, "componentRefs", "must be an array");
        var refs = element.EnumerateArray().Select(component =>
        {
            RequireProperties(
                component,
                fileName,
                key,
                "componentRefs",
                "masterKind",
                "key",
                "role");
            var masterKind = RequiredString(
                component,
                "masterKind",
                fileName,
                key) switch
            {
                "basic-rewards" => ClaimComponentMasterKind.BasicRewards,
                "additions" => ClaimComponentMasterKind.Additions,
                var value => throw Invalid(
                    fileName,
                    key,
                    "componentRefs.masterKind",
                    $"unknown value '{value}'"),
            };
            var role = RequiredString(component, "role", fileName, key) switch
            {
                "base" => ClaimComponentRole.Base,
                "adjustment" => ClaimComponentRole.Adjustment,
                var value => throw Invalid(
                    fileName,
                    key,
                    "componentRefs.role",
                    $"unknown value '{value}'"),
            };
            if ((role is ClaimComponentRole.Base)
                != (masterKind is ClaimComponentMasterKind.BasicRewards))
            {
                throw Invalid(
                    fileName,
                    key,
                    "componentRefs",
                    "base must refer to basic-rewards and adjustment to additions");
            }

            return new ClaimComponentRef(
                masterKind,
                RequiredString(component, "key", fileName, key),
                role);
        }).ToArray();
        if (refs.Distinct().Count() != refs.Length)
            throw Invalid(fileName, key, "componentRefs", "must be unique");
        return refs;
    }

    private static void ValidateAmountMatrix(
        UnitAdjustmentAmount amount,
        string step,
        string? rounding,
        string fileName,
        string key,
        string field)
    {
        var valid = amount switch
        {
            FixedUnitsAmount =>
                string.Equals(step, FixedStep, StringComparison.Ordinal)
                && rounding is null,
            UnitsPerCountAmount =>
                string.Equals(step, MultiplyCountStep, StringComparison.Ordinal)
                && rounding is null,
            PercentageOfTargetAmount
            {
                PercentageBaseScope: PercentageBaseScope.PerServiceCodeUnit,
            } =>
                string.Equals(step, PerServicePercentageStep, StringComparison.Ordinal)
                && string.Equals(rounding, HalfUpRounding, StringComparison.Ordinal),
            PercentageOfTargetAmount
            {
                PercentageBaseScope: PercentageBaseScope.MonthlyTargetUnitSum,
            } =>
                string.Equals(step, MonthlyPercentageStep, StringComparison.Ordinal)
                && string.Equals(rounding, HalfUpRounding, StringComparison.Ordinal),
            ProratedUnitsAmount =>
                string.Equals(step, ProrationStep, StringComparison.Ordinal)
                && string.Equals(rounding, HalfUpRounding, StringComparison.Ordinal),
            _ => false,
        };
        if (!valid)
            throw Invalid(fileName, key, field, "has an invalid step and rounding matrix");
    }

    private static BillingUnit ParseBillingUnit(
        string value,
        string fileName,
        string key) => value switch
        {
            "per-day" => BillingUnit.PerDay,
            "per-month" => BillingUnit.PerMonth,
            "per-use" => BillingUnit.PerUse,
            _ => throw Invalid(fileName, key, "billingUnit", $"unknown value '{value}'"),
        };

    private static AverageWageBandOption[] ParseOptions(
        JsonElement element,
        string fileName,
        string key)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw Invalid(fileName, key, "allowedAverageWageBandOptions", "must be an array");
        var options = element.EnumerateArray().Select(option =>
        {
            RequireProperties(
                option,
                fileName,
                key,
                "averageWageBandOption",
                "kind",
                "officialOptionCode");
            var kind = RequiredString(option, "kind", fileName, key) switch
            {
                "numeric" => AverageWageBandOptionKind.Numeric,
                "filed-transition" => AverageWageBandOptionKind.FiledTransition,
                "production-activity-support" =>
                    AverageWageBandOptionKind.ProductionActivitySupport,
                var value => throw Invalid(
                    fileName,
                    key,
                    "averageWageBandOption.kind",
                    $"unknown value '{value}'"),
            };
            return new AverageWageBandOption(
                kind,
                PositiveInt(option, "officialOptionCode", fileName, key));
        }).ToArray();
        if (options.Length == 0 || options.Distinct().Count() != options.Length)
        {
            throw Invalid(
                fileName,
                key,
                "allowedAverageWageBandOptions",
                "must be non-empty and unique");
        }

        return options;
    }

    private static R8ReformStatus ParseR8Status(
        string value,
        string fileName,
        string key) => value switch
        {
            "not-applicable-before-r8" => R8ReformStatus.NotApplicableBeforeR8,
            "reform-target" => R8ReformStatus.ReformTarget,
            "reform-exempt" => R8ReformStatus.ReformExempt,
            "unchanged-below-15000" => R8ReformStatus.UnchangedBelow15000,
            _ => throw Invalid(
                fileName,
                key,
                "allowedOptionsByR8ReformStatus",
                $"unknown status '{value}'"),
        };

    private static ClaimSourceRef[] ParseSourceRefs(
        JsonElement element,
        string fileName,
        string key,
        IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
        bool validateSourceCatalog)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw Invalid(fileName, key, "sourceRefs", "must be an array");
        var refs = element.EnumerateArray().Select(sourceRef =>
        {
            RequireProperties(
                sourceRef,
                fileName,
                key,
                "sourceRefs",
                "documentId",
                "sha256",
                "locator",
                "evidenceRole",
                "supports");
            var documentId = RequiredString(
                sourceRef,
                "documentId",
                fileName,
                key);
            var sha256 = RequiredString(sourceRef, "sha256", fileName, key);
            ValidateSha256(sha256, fileName, key, "sourceRefs.sha256");
            if (validateSourceCatalog)
            {
                if (!sources.TryGetValue(documentId, out var source))
                {
                    throw Invalid(
                        fileName,
                        key,
                        "sourceRefs.documentId",
                        $"unknown document '{documentId}'");
                }

                if (!string.Equals(sha256, source.Sha256, StringComparison.Ordinal))
                {
                    throw Invalid(
                        fileName,
                        key,
                        "sourceRefs.sha256",
                        $"does not match document '{documentId}'");
                }
            }

            var evidenceRole = RequiredString(
                sourceRef,
                "evidenceRole",
                fileName,
                key) switch
            {
                "authoritative" => ClaimSourceEvidenceRole.Authoritative,
                "correction" => ClaimSourceEvidenceRole.Correction,
                "cross-check" => ClaimSourceEvidenceRole.CrossCheck,
                var value => throw Invalid(
                    fileName,
                    key,
                    "sourceRefs.evidenceRole",
                    $"unknown value '{value}'"),
            };
            var supports = StringArray(
                    Required(sourceRef, "supports", fileName, key),
                    fileName,
                    key,
                    "sourceRefs.supports",
                    requireNonEmpty: true)
                .Select(value => ParseSupport(value, fileName, key))
                .ToArray();
            return new ClaimSourceRef(
                documentId,
                sha256,
                RequiredString(sourceRef, "locator", fileName, key),
                evidenceRole,
                supports);
        }).ToArray();
        if (refs.Length == 0)
            throw Invalid(fileName, key, "sourceRefs", "must be non-empty");
        var duplicate = refs
            .GroupBy(
                source => (source.DocumentId, source.Locator, source.EvidenceRole))
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw Invalid(
                fileName,
                key,
                "sourceRefs",
                $"duplicates document '{duplicate.Value.DocumentId}' locator '{duplicate.Value.Locator}'");
        }

        return refs;
    }

    private static ClaimSourceSupport ParseSupport(
        string value,
        string fileName,
        string key) => value switch
        {
            "service-identity" => ClaimSourceSupport.ServiceIdentity,
            "selectors" => ClaimSourceSupport.Selectors,
            "unit-rule-kind" => ClaimSourceSupport.UnitRuleKind,
            "unit-rule-value" => ClaimSourceSupport.UnitRuleValue,
            "unit-rule-target" => ClaimSourceSupport.UnitRuleTarget,
            "unit-rule-step" => ClaimSourceSupport.UnitRuleStep,
            "unit-rule-rounding" => ClaimSourceSupport.UnitRuleRounding,
            "conditions" => ClaimSourceSupport.Conditions,
            "effective-period" => ClaimSourceSupport.EffectivePeriod,
            "master-values" => ClaimSourceSupport.MasterValues,
            _ => throw Invalid(
                fileName,
                key,
                "sourceRefs.supports",
                $"unknown value '{value}'"),
        };

    private static string SupportToken(ClaimSourceSupport support) => support switch
    {
        ClaimSourceSupport.ServiceIdentity => "service-identity",
        ClaimSourceSupport.Selectors => "selectors",
        ClaimSourceSupport.UnitRuleKind => "unit-rule-kind",
        ClaimSourceSupport.UnitRuleValue => "unit-rule-value",
        ClaimSourceSupport.UnitRuleTarget => "unit-rule-target",
        ClaimSourceSupport.UnitRuleStep => "unit-rule-step",
        ClaimSourceSupport.UnitRuleRounding => "unit-rule-rounding",
        ClaimSourceSupport.Conditions => "conditions",
        ClaimSourceSupport.EffectivePeriod => "effective-period",
        ClaimSourceSupport.MasterValues => "master-values",
        _ => throw new InvalidOperationException("Source support is closed."),
    };

    private static void ValidateSupports(
        IReadOnlyList<ClaimSourceRef> sourceRefs,
        IEnumerable<ClaimSourceSupport> required,
        SourceAuthorityValidator authority,
        string fileName,
        string key)
    {
        foreach (var support in sourceRefs
                     .SelectMany(source => source.Supports)
                     .Concat(required)
                     .Distinct())
            authority.Validate(sourceRefs, support, fileName, key);
    }

    private static void ValidatePeriods(
        string fileName,
        IEnumerable<PeriodRow> source)
    {
        foreach (var group in source.GroupBy(row => row.Key, StringComparer.Ordinal))
        {
            var rows = group.OrderBy(row => row.EffectiveFrom).ToArray();
            for (var index = 0; index < rows.Length - 1; index++)
            {
                var current = rows[index];
                var next = rows[index + 1];
                if (current.EffectiveTo is null
                    || next.EffectiveFrom <= current.EffectiveTo.Value)
                {
                    throw Invalid(
                        fileName,
                        group.Key,
                        "effectiveFrom/effectiveTo",
                        "contains an overlap");
                }
            }
        }
    }

    private static void ValidateServiceIdentity(
        IReadOnlyList<ServiceCodeMasterRow> serviceCodes)
    {
        foreach (var group in serviceCodes.GroupBy(
                     row => row.ServiceCode,
                     StringComparer.Ordinal))
        {
            var rows = group.OrderBy(row => row.EffectiveFrom).ToArray();
            for (var first = 0; first < rows.Length; first++)
            {
                for (var second = first + 1; second < rows.Length; second++)
                {
                    if (!Overlaps(
                            rows[first].EffectiveFrom,
                            rows[first].EffectiveTo,
                            rows[second].EffectiveFrom,
                            rows[second].EffectiveTo))
                    {
                        continue;
                    }

                    if (!string.Equals(
                            rows[first].OfficialLabel,
                            rows[second].OfficialLabel,
                            StringComparison.Ordinal))
                    {
                        throw Invalid(
                            "service-codes.json",
                            rows[second].Key,
                            "officialLabel",
                            $"conflicts for serviceCode '{group.Key}'");
                    }
                }
            }
        }
    }

    private static void ValidateConditions(
        IReadOnlyList<ServiceCodeMasterRow> serviceCodes,
        IReadOnlyList<ClaimConditionDefinition> definitions)
    {
        var byKey = definitions
            .GroupBy(definition => definition.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var used = serviceCodes
            .SelectMany(service => service.ConditionSelectors)
            .Concat(serviceCodes
                .SelectMany(service => service.UnitRule is FactorChainRule chain
                    ? chain.Factors.SelectMany(factor => factor.ConditionSelectors)
                    : []))
            .ToHashSet(StringComparer.Ordinal);
        var unused = byKey.Keys.Except(used, StringComparer.Ordinal).FirstOrDefault();
        if (unused is not null)
            throw Invalid(
                "service-codes.json",
                unused,
                "conditionDefinitions",
                "is unused");

        foreach (var service in serviceCodes)
        {
            foreach (var selector in service.ConditionSelectors)
            {
                if (!byKey.TryGetValue(selector, out var rows)
                    || !CoversRange(
                        service.EffectiveFrom,
                        service.EffectiveTo,
                        rows.Select(row => (row.EffectiveFrom, row.EffectiveTo))))
                {
                    throw Invalid(
                        "service-codes.json",
                        service.Key,
                        "conditionSelectors",
                        $"condition '{selector}' does not cover the service period");
                }
            }

            ValidateConditionIntersection(service, byKey);
        }
    }

    private static void ValidateConditionIntersection(
        ServiceCodeMasterRow service,
        Dictionary<string, ClaimConditionDefinition[]> byKey)
    {
        if (service.ConditionSelectors.Count == 0)
            return;
        var relevant = service.ConditionSelectors
            .SelectMany(selector => byKey[selector])
            .ToArray();
        var boundaries = new HashSet<ServiceMonth> { service.EffectiveFrom };
        foreach (var definition in relevant)
        {
            if (definition.EffectiveFrom >= service.EffectiveFrom
                && IsWithin(
                    definition.EffectiveFrom,
                    service.EffectiveFrom,
                    service.EffectiveTo))
            {
                boundaries.Add(definition.EffectiveFrom);
            }

            if (definition.EffectiveTo is { } end)
            {
                var next = NextMonth(end);
                if (IsWithin(next, service.EffectiveFrom, service.EffectiveTo))
                    boundaries.Add(next);
            }
        }

        foreach (var month in boundaries)
        {
            var active = service.ConditionSelectors
                .Select(selector => byKey[selector].Single(row =>
                    IsWithin(month, row.EffectiveFrom, row.EffectiveTo)))
                .ToArray();
            foreach (var dimension in active.GroupBy(row => row.Kind))
            {
                if (!HasConditionIntersection(dimension.ToArray()))
                {
                    throw Invalid(
                        "service-codes.json",
                        service.Key,
                        "conditionSelectors",
                        $"has an empty intersection for '{dimension.Key}'");
                }
            }
        }
    }

    private static bool HasConditionIntersection(
        ClaimConditionDefinition[] definitions)
    {
        if (definitions[0].Operand is
            ClaimConditionTokenOperand or
            ClaimConditionTokenSetOperand)
        {
            HashSet<string>? intersection = null;
            foreach (var definition in definitions)
            {
                var values = definition.Operand switch
                {
                    ClaimConditionTokenOperand token => new[] { token.Value },
                    ClaimConditionTokenSetOperand set => set.Values,
                    _ => throw new InvalidOperationException("Token condition is closed."),
                };
                intersection = intersection is null
                    ? values.ToHashSet(StringComparer.Ordinal)
                    : intersection.Intersect(values, StringComparer.Ordinal)
                        .ToHashSet(StringComparer.Ordinal);
            }

            return intersection is { Count: > 0 };
        }

        if (definitions[0].Operand is ClaimConditionBooleanOperand)
        {
            return definitions
                .Select(definition =>
                    ((ClaimConditionBooleanOperand)definition.Operand).Value)
                .Distinct()
                .Count() == 1;
        }

        var lower = 0L;
        long upper = int.MaxValue;
        foreach (var definition in definitions)
        {
            var value = ((ClaimConditionIntegerOperand)definition.Operand).Value;
            switch (definition.Operator)
            {
                case ClaimConditionOperator.Equals:
                    lower = Math.Max(lower, value);
                    upper = Math.Min(upper, value);
                    break;
                case ClaimConditionOperator.LessThan:
                    upper = Math.Min(upper, (long)value - 1);
                    break;
                case ClaimConditionOperator.LessThanOrEqual:
                    upper = Math.Min(upper, value);
                    break;
                case ClaimConditionOperator.GreaterThan:
                    lower = Math.Max(lower, (long)value + 1);
                    break;
                case ClaimConditionOperator.GreaterThanOrEqual:
                    lower = Math.Max(lower, value);
                    break;
                default:
                    throw new InvalidOperationException("Integer condition operator is closed.");
            }
        }

        return lower <= upper;
    }

    private static void ValidateReferences(
        IReadOnlyList<BasicRewardMasterRow> basicRewards,
        IReadOnlyList<UnitAdjustmentMasterRow> unitAdjustments,
        IReadOnlyList<ServiceCodeMasterRow> serviceCodes)
    {
        var servicesByCode = serviceCodes
            .GroupBy(row => row.ServiceCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var servicesBySelector = serviceCodes
            .SelectMany(row => row.Selectors.Select(selector => (selector, row)))
            .GroupBy(item => item.selector, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.row).ToArray(),
                StringComparer.Ordinal);
        var basicsByKey = basicRewards
            .GroupBy(row => row.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var adjustmentsByKey = unitAdjustments
            .GroupBy(row => row.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var reward in basicRewards)
        {
            if (!servicesByCode.TryGetValue(reward.ServiceCode, out var services)
                || !CoversRange(
                    reward.EffectiveFrom,
                    reward.EffectiveTo,
                    services.Select(row => (row.EffectiveFrom, row.EffectiveTo))))
            {
                throw Invalid(
                    "basic-rewards.json",
                    reward.Key,
                    "serviceCode",
                    $"does not resolve '{reward.ServiceCode}' for the whole period");
            }
        }

        foreach (var adjustment in unitAdjustments)
        {
            ValidateAmountTarget(
                adjustment.Key,
                adjustment.Amount,
                adjustment.EffectiveFrom,
                adjustment.EffectiveTo,
                servicesBySelector,
                sourceServiceKey: null,
                "additions.json");
        }

        foreach (var service in serviceCodes)
        {
            foreach (var component in service.ComponentRefs)
            {
                var periods = component.MasterKind switch
                {
                    ClaimComponentMasterKind.BasicRewards
                        when basicsByKey.TryGetValue(component.Key, out var rows) =>
                        rows.Select(row => (row.EffectiveFrom, row.EffectiveTo)),
                    ClaimComponentMasterKind.Additions
                        when adjustmentsByKey.TryGetValue(component.Key, out var rows) =>
                        rows.Select(row => (row.EffectiveFrom, row.EffectiveTo)),
                    _ => [],
                };
                if (!CoversRange(service.EffectiveFrom, service.EffectiveTo, periods))
                {
                    throw Invalid(
                        "service-codes.json",
                        service.Key,
                        "componentRefs",
                        $"component '{component.Key}' does not cover the service period");
                }
            }

            switch (service.UnitRule)
            {
                case UnitAdditionRule addition:
                    ValidateAdjustmentComponent(
                        service,
                        addition,
                        adjustmentsByKey);
                    ValidateAmountTarget(
                        service.Key,
                        addition.Amount,
                        service.EffectiveFrom,
                        service.EffectiveTo,
                        servicesBySelector,
                        service.Key,
                        "service-codes.json");
                    break;
                case FormulaUnitRule formula:
                    ValidateBaseComponent(service, formula, basicsByKey);
                    break;
            }
        }
    }

    private static void ValidateAdjustmentComponent(
        ServiceCodeMasterRow service,
        UnitAdditionRule rule,
        Dictionary<string, UnitAdjustmentMasterRow[]> adjustmentsByKey)
    {
        var matchingRefs = service.ComponentRefs.Where(component =>
                component.MasterKind is ClaimComponentMasterKind.Additions
                && component.Role is ClaimComponentRole.Adjustment
                && string.Equals(
                    component.Key,
                    rule.AdjustmentComponentKey,
                    StringComparison.Ordinal))
            .ToArray();
        if (matchingRefs.Length != 1
            || !adjustmentsByKey.TryGetValue(rule.AdjustmentComponentKey, out var rows))
        {
            throw Invalid(
                "service-codes.json",
                service.Key,
                "adjustmentComponentKey",
                $"does not resolve '{rule.AdjustmentComponentKey}' exactly once");
        }

        foreach (var row in rows.Where(row => Overlaps(
                     row.EffectiveFrom,
                     row.EffectiveTo,
                     service.EffectiveFrom,
                     service.EffectiveTo)))
        {
            if (row.Amount != rule.Amount
                || !string.Equals(
                    row.CalculationStepId,
                    rule.CalculationStepId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    row.RoundingRuleId,
                    rule.RoundingRuleId,
                    StringComparison.Ordinal)
                || row.BillingUnit != rule.BillingUnit)
            {
                throw Invalid(
                    "service-codes.json",
                    service.Key,
                    "unitRule",
                    $"does not structurally match adjustment '{rule.AdjustmentComponentKey}'");
            }
        }
    }

    private static void ValidateBaseComponent(
        ServiceCodeMasterRow service,
        FormulaUnitRule rule,
        Dictionary<string, BasicRewardMasterRow[]> basicsByKey)
    {
        var matchingRefs = service.ComponentRefs.Where(component =>
                component.MasterKind is ClaimComponentMasterKind.BasicRewards
                && component.Role is ClaimComponentRole.Base
                && string.Equals(component.Key, rule.BaseComponentKey, StringComparison.Ordinal))
            .ToArray();
        if (matchingRefs.Length != 1
            || !basicsByKey.ContainsKey(rule.BaseComponentKey))
        {
            throw Invalid(
                "service-codes.json",
                service.Key,
                "baseComponentKey",
                $"does not resolve '{rule.BaseComponentKey}' exactly once");
        }
    }

    private static void ValidateAmountTarget(
        string sourceKey,
        UnitAdjustmentAmount amount,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        Dictionary<string, ServiceCodeMasterRow[]> servicesBySelector,
        string? sourceServiceKey,
        string fileName)
    {
        if (amount is not PercentageOfTargetAmount percentage)
            return;
        if (!servicesBySelector.TryGetValue(percentage.TargetSelector, out var targets)
            || !CoversRange(
                effectiveFrom,
                effectiveTo,
                targets.Select(row => (row.EffectiveFrom, row.EffectiveTo))))
        {
            throw Invalid(
                fileName,
                sourceKey,
                "targetSelector",
                $"does not resolve '{percentage.TargetSelector}' for the whole period");
        }

        if (sourceServiceKey is not null
            && targets.All(target =>
                string.Equals(target.Key, sourceServiceKey, StringComparison.Ordinal)))
        {
            throw Invalid(
                fileName,
                sourceKey,
                "targetSelector",
                "resolves only to the source service");
        }
    }

    private static void ValidateAdjustmentCycles(
        IReadOnlyList<UnitAdjustmentMasterRow> rows)
    {
        var percentageRows = rows
            .Where(row => row.Amount is PercentageOfTargetAmount)
            .ToArray();
        foreach (var boundary in PeriodBoundaries(percentageRows.Select(row =>
                     (row.EffectiveFrom, row.EffectiveTo))))
        {
            var dependencies = percentageRows
                .Where(row => IsWithin(boundary, row.EffectiveFrom, row.EffectiveTo))
                .ToDictionary(
                    row => row.Key,
                    row => new[]
                    {
                        ((PercentageOfTargetAmount)row.Amount).TargetSelector,
                    }.AsEnumerable(),
                    StringComparer.Ordinal);
            ValidateDirectedCycles(dependencies, "additions.json", "targetSelector");
        }
    }

    private static void ValidateServiceTargetCycles(
        IReadOnlyList<ServiceCodeMasterRow> rows)
    {
        foreach (var boundary in PeriodBoundaries(rows.Select(row =>
                     (row.EffectiveFrom, row.EffectiveTo))))
        {
            var activeRows = rows
                .Where(row => IsWithin(boundary, row.EffectiveFrom, row.EffectiveTo))
                .ToArray();
            var bySelector = activeRows
                .SelectMany(row => row.Selectors.Select(selector => (selector, row.Key)))
                .GroupBy(item => item.selector, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Key).Distinct(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            var dependencies = activeRows
                .Where(row => row.UnitRule is UnitAdditionRule
                {
                    Amount: PercentageOfTargetAmount,
                })
                .ToDictionary(
                    row => row.Key,
                    row =>
                    {
                        var selector =
                            ((PercentageOfTargetAmount)((UnitAdditionRule)row.UnitRule).Amount)
                            .TargetSelector;
                        return bySelector.GetValueOrDefault(selector) ?? [];
                    },
                    StringComparer.Ordinal);
            ValidateDirectedCycles(
                dependencies,
                "service-codes.json",
                "unitRule.amount.targetSelector");
        }
    }

    private static void ValidateDirectedCycles(
        IReadOnlyDictionary<string, IEnumerable<string>> dependencies,
        string fileName,
        string field)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in dependencies.Keys)
        {
            if (HasCycle(
                    key,
                    dependencies,
                    new HashSet<string>(StringComparer.Ordinal),
                    visited))
            {
                throw Invalid(fileName, key, field, "contains a cycle");
            }
        }
    }

    private static bool HasCycle(
        string key,
        IReadOnlyDictionary<string, IEnumerable<string>> dependencies,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(key)) return false;
        if (!visiting.Add(key)) return true;
        var hasCycle = dependencies.TryGetValue(key, out var targets)
                       && targets.Any(target =>
                           dependencies.ContainsKey(target)
                           && HasCycle(target, dependencies, visiting, visited));
        visiting.Remove(key);
        visited.Add(key);
        return hasCycle;
    }

    private static void ValidateCalculationOrder(
        IReadOnlyList<UnitAdjustmentMasterRow> rows)
    {
        var percentageRows = rows
            .Where(row => row.Amount is PercentageOfTargetAmount)
            .ToArray();
        foreach (var boundary in PeriodBoundaries(percentageRows.Select(row =>
                     (row.EffectiveFrom, row.EffectiveTo))))
        {
            var active = percentageRows.Where(row =>
                IsWithin(boundary, row.EffectiveFrom, row.EffectiveTo));
            foreach (var group in active.GroupBy(
                         row => ((PercentageOfTargetAmount)row.Amount).TargetSelector,
                         StringComparer.Ordinal))
            {
                var actual = group
                    .Select(row => ((PercentageOfTargetAmount)row.Amount).CalculationOrder)
                    .Order()
                    .ToArray();
                if (!actual.SequenceEqual(Enumerable.Range(1, actual.Length)))
                {
                    throw Invalid(
                        "additions.json",
                        group.Key,
                        "calculationOrder",
                        "must be unique and contiguous from one");
                }
            }
        }
    }

    private static ServiceMonth[] PeriodBoundaries(
        IEnumerable<(ServiceMonth From, ServiceMonth? To)> periods) => periods
        .Select(period => period.From)
        .Concat(periods
            .Where(period => period.To is not null)
            .Select(period => NextMonth(period.To!.Value)))
        .Distinct()
        .Order()
        .ToArray();

    private static bool CoversRange(
        ServiceMonth dependentFrom,
        ServiceMonth? dependentTo,
        IEnumerable<(ServiceMonth From, ServiceMonth? To)> source)
    {
        var periods = source
            .Where(period => Overlaps(
                period.From,
                period.To,
                dependentFrom,
                dependentTo))
            .OrderBy(period => period.From)
            .ToArray();
        var cursor = dependentFrom;
        foreach (var period in periods)
        {
            if (period.From > cursor)
                return false;
            if (period.To is null)
                return true;
            if (dependentTo is { } end && period.To.Value >= end)
                return true;
            var next = NextMonth(period.To.Value);
            if (next > cursor)
                cursor = next;
        }

        return false;
    }

    private static bool Overlaps(
        ServiceMonth firstFrom,
        ServiceMonth? firstTo,
        ServiceMonth secondFrom,
        ServiceMonth? secondTo) =>
        (firstTo is null || secondFrom <= firstTo.Value)
        && (secondTo is null || firstFrom <= secondTo.Value);

    private static bool IsWithin(
        ServiceMonth month,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo) =>
        effectiveFrom <= month
        && (effectiveTo is null || month <= effectiveTo.Value);

    private static PeriodRow ToPeriod(BasicRewardMasterRow row) =>
        new(row.Key, row.EffectiveFrom, row.EffectiveTo);

    private static PeriodRow ToPeriod(UnitAdjustmentMasterRow row) =>
        new(row.Key, row.EffectiveFrom, row.EffectiveTo);

    private static PeriodRow ToPeriod(RegionUnitPriceMasterRow row) =>
        new(row.Key, row.EffectiveFrom, row.EffectiveTo);

    private static PeriodRow ToPeriod(BurdenCapMasterRow row) =>
        new(row.Key, row.EffectiveFrom, row.EffectiveTo);

    private static PeriodRow ToPeriod(OfficeClaimProfileTransitionRuleMasterRow row) =>
        new(row.Key, row.EffectiveFrom, row.EffectiveTo);

    private static PeriodRow ToPeriod(ServiceCodeMasterRow row) =>
        new(row.Key, row.EffectiveFrom, row.EffectiveTo);

    private static JsonElement Required(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var value))
        {
            throw Invalid(fileName, key, propertyName, "is required");
        }

        return value;
    }

    private static string RequiredString(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        var element = Required(parent, propertyName, fileName, key);
        if (element.ValueKind != JsonValueKind.String)
            throw Invalid(fileName, key, propertyName, "must be text");
        var value = element.GetString()!;
        ValidateRequiredText(value, fileName, key, propertyName);
        return value;
    }

    private static string? NullableString(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        var element = Required(parent, propertyName, fileName, key);
        if (element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind != JsonValueKind.String)
            throw Invalid(fileName, key, propertyName, "must be text or null");
        var value = element.GetString()!;
        ValidateRequiredText(value, fileName, key, propertyName);
        return value;
    }

    private static string[] StringArray(
        JsonElement element,
        string fileName,
        string key,
        string field,
        bool requireNonEmpty)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw Invalid(fileName, key, field, "must be an array");
        var values = element.EnumerateArray().Select(item =>
        {
            if (item.ValueKind != JsonValueKind.String)
                throw Invalid(fileName, key, field, "must contain text");
            var value = item.GetString()!;
            ValidateRequiredText(value, fileName, key, field);
            return value;
        }).ToArray();
        if (requireNonEmpty && values.Length == 0)
            throw Invalid(fileName, key, field, "must be non-empty");
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw Invalid(fileName, key, field, "must be unique");
        return values;
    }

    private static void RequireProperties(
        JsonElement element,
        string fileName,
        string key,
        string field,
        params string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid(fileName, key, field, "must be an object");
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        var unknown = actual.Except(required, StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
            throw Invalid(fileName, key, unknown, "is not allowed");
        var missing = required.Except(actual, StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null)
            throw Invalid(fileName, key, missing, "is required");
    }

    private static int NonNegativeInt(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        var value = Integer(parent, propertyName, fileName, key);
        if (value < 0)
            throw Invalid(fileName, key, propertyName, "must be nonnegative");
        return value;
    }

    private static int PositiveInt(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        var value = Integer(parent, propertyName, fileName, key);
        if (value <= 0)
            throw Invalid(fileName, key, propertyName, "must be positive");
        return value;
    }

    private static int Integer(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        var element = Required(parent, propertyName, fileName, key);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
            throw Invalid(fileName, key, propertyName, "must be an integer");
        return value;
    }

    private static decimal NonNegativeDecimalString(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key) =>
        CanonicalDecimalString(parent, propertyName, fileName, key, allowZero: true);

    private static decimal PositiveDecimalString(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key) =>
        CanonicalDecimalString(parent, propertyName, fileName, key, allowZero: false);

    private static decimal CanonicalDecimalString(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key,
        bool allowZero)
    {
        var text = RequiredString(parent, propertyName, fileName, key);
        if (text.Any(character => !char.IsAsciiDigit(character) && character != '.')
            || text.Count(character => character == '.') > 1
            || text[0] == '.'
            || text[^1] == '.'
            || text.Length > 1 && text[0] == '0' && text[1] != '.'
            || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || value < 0
            || !allowZero && value == 0)
        {
            throw Invalid(
                fileName,
                key,
                propertyName,
                allowZero
                    ? "must be a canonical nonnegative decimal string"
                    : "must be a canonical positive decimal string");
        }

        return value;
    }

    private static void ValidateSha256(
        string value,
        string fileName,
        string key,
        string field)
    {
        if (value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw Invalid(
                fileName,
                key,
                field,
                "must be 64 lowercase hexadecimal characters");
        }
    }

    private static DateOnly ParseDate(
        string value,
        string fileName,
        string key,
        string field)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw Invalid(fileName, key, field, "must be YYYY-MM-DD");
        }

        return result;
    }

    private static ServiceMonth? NullableMonth(
        JsonElement parent,
        string propertyName,
        string fileName,
        string key)
    {
        var element = Required(parent, propertyName, fileName, key);
        if (element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind != JsonValueKind.String)
            throw Invalid(fileName, key, propertyName, "must be YYYY-MM or null");
        return ParseMonth(element.GetString()!, fileName, key, propertyName);
    }

    private static ServiceMonth ParseMonth(
        string value,
        string fileName,
        string key,
        string field)
    {
        if (value.Length != 7
            || value[4] != '-'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year is < 1900 or > 2200
            || month is < 1 or > 12)
        {
            throw Invalid(fileName, key, field, "must be YYYY-MM");
        }

        return new ServiceMonth(year, month);
    }

    private static ServiceMonth NextMonth(ServiceMonth month) => month.Month == 12
        ? new ServiceMonth(month.Year + 1, 1)
        : new ServiceMonth(month.Year, month.Month + 1);

    private static void ValidateRequiredText(
        string value,
        string fileName,
        string key,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
            throw Invalid(fileName, key, field, "must be non-blank without outer whitespace");
    }

    private static InvalidDataException Invalid(
        string fileName,
        string key,
        string field,
        string message) =>
        new($"Claim master file '{fileName}' key '{key}' field '{field}' {message}.");

    private sealed class SourceAuthorityValidator
    {
        private readonly bool _enabled;
        private readonly IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> _sources;
        private readonly Dictionary<(string From, string To), bool> _reachability = new();
        private readonly HashSet<string> _validated = new(StringComparer.Ordinal);

        internal SourceAuthorityValidator(
            IReadOnlyDictionary<string, ClaimSourceCatalogMetadata> sources,
            bool enabled = true)
        {
            _sources = sources;
            _enabled = enabled;
        }

        internal void Validate(
            IReadOnlyList<ClaimSourceRef> sourceRefs,
            ClaimSourceSupport support,
            string fileName,
            string key)
        {
            if (!_enabled)
                return;

            var relevant = sourceRefs
                .Where(source =>
                    source.EvidenceRole is not ClaimSourceEvidenceRole.CrossCheck
                    && source.Supports.Contains(support))
                .ToArray();
            var mixedRoleDocument = relevant
                .GroupBy(source => source.DocumentId, StringComparer.Ordinal)
                .FirstOrDefault(group => group
                    .Select(source => source.EvidenceRole)
                    .Distinct()
                    .Skip(1)
                    .Any());
            var token = SupportToken(support);
            if (mixedRoleDocument is not null)
            {
                throw Invalid(
                    fileName,
                    key,
                    token,
                    $"document '{mixedRoleDocument.Key}' declares multiple evidence roles");
            }

            var candidates = relevant
                .GroupBy(source => source.DocumentId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            if (candidates.Length == 0)
                throw Invalid(fileName, key, token, "has no authoritative source");
            var cacheKey = token + ":" + string.Join(
                ",",
                candidates
                    .OrderBy(source => source.DocumentId, StringComparer.Ordinal)
                    .Select(source => $"{source.DocumentId}/{source.EvidenceRole}"));
            if (_validated.Contains(cacheKey))
                return;

            var authoritative = candidates
                .Where(source =>
                    source.EvidenceRole is ClaimSourceEvidenceRole.Authoritative)
                .Select(source => source.DocumentId)
                .ToArray();
            if (authoritative.Length == 0)
                throw Invalid(fileName, key, token, "has no authoritative base document");
            foreach (var correction in candidates.Where(source =>
                         source.EvidenceRole is ClaimSourceEvidenceRole.Correction))
            {
                if (!authoritative.Any(baseDocument =>
                        Reaches(correction.DocumentId, baseDocument)))
                {
                    throw Invalid(
                        fileName,
                        key,
                        token,
                        $"correction '{correction.DocumentId}' does not reach an authoritative document");
                }
            }

            if (ContainsReachableCycle(
                    candidates.Select(source => source.DocumentId).ToArray()))
            {
                throw Invalid(fileName, key, token, "contains a correction cycle");
            }

            var candidateIds = candidates
                .Select(source => source.DocumentId)
                .ToArray();
            var maximal = candidateIds.Where(candidate =>
                    !candidateIds.Any(other =>
                        !string.Equals(other, candidate, StringComparison.Ordinal)
                        && Reaches(other, candidate)))
                .ToArray();
            if (maximal.Length != 1)
            {
                throw Invalid(
                    fileName,
                    key,
                    token,
                    "has multiple authoritative maxima");
            }

            if (authoritative.Any(baseDocument =>
                    !Reaches(maximal[0], baseDocument)))
            {
                throw Invalid(
                    fileName,
                    key,
                    token,
                    "has an authoritative document outside the selected correction chain");
            }

            _validated.Add(cacheKey);
        }

        private bool Reaches(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.Ordinal))
                return true;
            if (_reachability.TryGetValue((from, to), out var cached))
                return cached;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(from);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current))
                    continue;
                foreach (var target in _sources[current].Corrects)
                {
                    if (string.Equals(target, to, StringComparison.Ordinal))
                    {
                        _reachability[(from, to)] = true;
                        return true;
                    }

                    pending.Push(target);
                }
            }

            _reachability[(from, to)] = false;
            return false;
        }

        private bool ContainsReachableCycle(IReadOnlyList<string> starts)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var start in starts)
            {
                if (ContainsCycle(
                        start,
                        new HashSet<string>(StringComparer.Ordinal),
                        visited))
                    return true;
            }

            return false;
        }

        private bool ContainsCycle(
            string documentId,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visited.Contains(documentId)) return false;
            if (!visiting.Add(documentId)) return true;
            foreach (var target in _sources[documentId].Corrects)
            {
                if (ContainsCycle(target, visiting, visited))
                    return true;
            }

            visiting.Remove(documentId);
            visited.Add(documentId);
            return false;
        }
    }

    internal sealed class PreparedMasterFiles
    {
        internal PreparedMasterFiles(
            Dictionary<string, MasterFile> files,
            bool sanitizeTransitionHeaders)
        {
            Files = files;
            SanitizeTransitionHeaders = sanitizeTransitionHeaders;
        }

        internal Dictionary<string, MasterFile> Files { get; }

        internal bool SanitizeTransitionHeaders { get; }
    }

    internal sealed record MasterFile(
        string FileName,
        string SchemaVersion,
        string MasterKind,
        IReadOnlyList<JsonElement> Entries,
        IReadOnlyList<JsonElement>? ConditionDefinitions);

    private sealed record RawEntry(
        string FileName,
        string Key,
        ServiceMonth EffectiveFrom,
        ServiceMonth? EffectiveTo,
        IReadOnlyList<ClaimSourceRef> SourceRefs,
        JsonElement Values);

    private sealed record PeriodRow(
        string Key,
        ServiceMonth EffectiveFrom,
        ServiceMonth? EffectiveTo);
}
