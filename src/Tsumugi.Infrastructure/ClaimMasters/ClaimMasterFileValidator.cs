using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

internal static class ClaimMasterFileValidator
{
    private const string SupportedSchemaVersion = "1";
    private const string PercentageRoundingRuleId = "claim.rounding.units.half-up.v1";
    private const string PerServiceCodeStepId =
        "claim.step.units.per-service-code.percentage.v1";
    private const string MonthlyTargetStepId =
        "claim.step.units.monthly-target.percentage.v1";

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

    internal static ClaimCalculationMasterBundle ValidateAll(
        IReadOnlyDictionary<string, Stream> masterFiles,
        IReadOnlyDictionary<string, string> knownSourceSha256ByDocumentId)
    {
        ArgumentNullException.ThrowIfNull(masterFiles);
        ArgumentNullException.ThrowIfNull(knownSourceSha256ByDocumentId);

        var missing = ExpectedFiles.Keys.Except(masterFiles.Keys, StringComparer.Ordinal).ToArray();
        var extra = masterFiles.Keys.Except(ExpectedFiles.Keys, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || extra.Length != 0)
        {
            throw new InvalidDataException(
                $"Claim master filenames must match exactly. Missing: [{string.Join(", ", missing)}]; extra: [{string.Join(", ", extra)}].");
        }

        var basicRewards = new List<BasicRewardMasterRow>();
        var percentageAdjustments = new List<PercentageAdjustmentMasterRow>();
        var regionUnitPrices = new List<RegionUnitPriceMasterRow>();
        var burdenCaps = new List<BurdenCapMasterRow>();
        var transitionRules = new List<OfficeClaimProfileTransitionRuleMasterRow>();
        var serviceCodes = new List<ServiceCodeMasterRow>();

        foreach (var expected in ExpectedFiles)
        {
            var stream = masterFiles[expected.Key];
            if (stream is null)
                throw new ArgumentException($"Claim master stream '{expected.Key}' cannot be null.", nameof(masterFiles));
            if (!stream.CanRead)
                throw new ArgumentException($"Claim master stream '{expected.Key}' must be readable.", nameof(masterFiles));

            MasterFile file;
            try
            {
                file = Deserialize(stream, expected.Key);
            }
            catch (Exception exception)
                when (string.Equals(expected.Value, "transition-rules", StringComparison.Ordinal)
                      && exception is InvalidDataException or ArgumentException or InvalidOperationException)
            {
                throw new ClaimMasterPolicyUnavailableException(
                    ClaimMasterPolicyUnavailableCode.InvalidMaster);
            }

            ValidateHeader(expected.Key, expected.Value, file);
            try
            {
                var parsedEntries = file.Entries.Select(entry =>
                    ParseEntry(
                        expected.Key,
                        expected.Value,
                        entry,
                        knownSourceSha256ByDocumentId,
                        basicRewards,
                        percentageAdjustments,
                        regionUnitPrices,
                        burdenCaps,
                        transitionRules,
                        serviceCodes)).ToArray();
                ValidateEntryIdentityAndPeriods(expected.Key, parsedEntries);
            }
            catch (ClaimMasterPolicyUnavailableException)
            {
                throw;
            }
            catch (Exception exception)
                when (string.Equals(expected.Value, "transition-rules", StringComparison.Ordinal)
                      && exception is InvalidDataException or ArgumentException or InvalidOperationException)
            {
                throw new ClaimMasterPolicyUnavailableException(
                    ClaimMasterPolicyUnavailableCode.InvalidMaster);
            }
        }

        ValidateReferences(basicRewards, percentageAdjustments, serviceCodes);
        ValidateSelectorCycles(percentageAdjustments);
        ValidateCalculationOrder(percentageAdjustments);

        return new ClaimCalculationMasterBundle(
            basicRewards,
            percentageAdjustments,
            regionUnitPrices,
            burdenCaps,
            transitionRules,
            serviceCodes);
    }

    private static MasterFile Deserialize(Stream stream, string fileName)
    {
        try
        {
            return JsonSerializer.Deserialize<MasterFile>(stream, SerializerOptions)
                ?? throw new InvalidDataException($"Claim master file '{fileName}' is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' contains invalid JSON.",
                exception);
        }
    }

    private static void ValidateHeader(string fileName, string expectedKind, MasterFile file)
    {
        if (!string.Equals(file.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' has unsupported schemaVersion '{file.SchemaVersion}'.");
        }

        if (!string.Equals(file.MasterKind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' must have masterKind '{expectedKind}'.");
        }

        ArgumentNullException.ThrowIfNull(file.Entries);
        if (file.Entries.Any(entry => entry is null))
            throw new InvalidDataException($"Claim master file '{fileName}' cannot contain null entries.");
    }

    private static ParsedEntry ParseEntry(
        string fileName,
        string masterKind,
        MasterEntry entry,
        IReadOnlyDictionary<string, string> knownSourceSha256ByDocumentId,
        List<BasicRewardMasterRow> basicRewards,
        List<PercentageAdjustmentMasterRow> percentageAdjustments,
        List<RegionUnitPriceMasterRow> regionUnitPrices,
        List<BurdenCapMasterRow> burdenCaps,
        List<OfficeClaimProfileTransitionRuleMasterRow> transitionRules,
        List<ServiceCodeMasterRow> serviceCodes)
    {
        ValidateRequiredText(entry.Key, "key", fileName);
        ValidateRequiredText(entry.SourceDocumentId, "sourceDocumentId", fileName);
        ValidateSha256(entry.SourceSha256, fileName);
        ValidateRequiredText(entry.SourceLocator, "sourceLocator", fileName);
        if (!knownSourceSha256ByDocumentId.TryGetValue(entry.SourceDocumentId, out var sourceSha256))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' refers to unknown sourceDocumentId '{entry.SourceDocumentId}'.");
        }

        if (!string.Equals(entry.SourceSha256, sourceSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' source SHA does not match its source document.");
        }

        var effectiveFrom = ParseMonth(entry.EffectiveFrom, "effectiveFrom", fileName);
        var effectiveTo = entry.EffectiveTo is null
            ? (ServiceMonth?)null
            : ParseMonth(entry.EffectiveTo, "effectiveTo", fileName);
        if (effectiveTo is { } end && end < effectiveFrom)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' key '{entry.Key}' has a reversed effective range.");
        }

        if (entry.Values.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' key '{entry.Key}' values must be an object.");
        }

        var source = new ClaimSourceLocator(
            entry.SourceDocumentId,
            entry.SourceSha256,
            entry.SourceLocator);
        switch (masterKind)
        {
            case "basic-rewards":
                basicRewards.Add(ParseBasicReward(entry, effectiveFrom, effectiveTo, source, fileName));
                break;
            case "additions":
                percentageAdjustments.Add(ParsePercentageAdjustment(entry, effectiveFrom, effectiveTo, source, fileName));
                break;
            case "region-unit-prices":
                regionUnitPrices.Add(ParseRegionUnitPrice(entry, effectiveFrom, effectiveTo, source, fileName));
                break;
            case "burden-caps":
                burdenCaps.Add(ParseBurdenCap(entry, effectiveFrom, effectiveTo, source, fileName));
                break;
            case "transition-rules":
                try
                {
                    transitionRules.Add(ParseTransitionRule(
                        entry, effectiveFrom, effectiveTo, source, fileName));
                }
                catch (ClaimMasterPolicyUnavailableException)
                {
                    throw;
                }
                catch (Exception exception)
                    when (exception is InvalidDataException or ArgumentException or InvalidOperationException)
                {
                    throw new ClaimMasterPolicyUnavailableException(
                        ClaimMasterPolicyUnavailableCode.InvalidMaster);
                }
                break;
            case "service-codes":
                serviceCodes.Add(ParseServiceCode(entry, effectiveFrom, effectiveTo, source, fileName));
                break;
            default:
                throw new InvalidDataException($"Claim master file '{fileName}' has an unknown master kind.");
        }

        return new ParsedEntry(entry.Key, effectiveFrom, effectiveTo);
    }

    private static BasicRewardMasterRow ParseBasicReward(
        MasterEntry entry,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        ClaimSourceLocator source,
        string fileName)
    {
        RequireProperties(entry.Values, fileName,
            "paymentBand", "staffingKey", "capacityKey", "serviceCode", "units");
        return new BasicRewardMasterRow(
            entry.Key,
            RequiredString(entry.Values, "paymentBand", fileName),
            RequiredString(entry.Values, "staffingKey", fileName),
            RequiredString(entry.Values, "capacityKey", fileName),
            RequiredString(entry.Values, "serviceCode", fileName),
            NonNegativeInt(entry.Values, "units", fileName),
            effectiveFrom,
            effectiveTo,
            source);
    }

    private static PercentageAdjustmentMasterRow ParsePercentageAdjustment(
        MasterEntry entry,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        ClaimSourceLocator source,
        string fileName)
    {
        RequireProperties(entry.Values, fileName,
            "percentage", "percentageBaseScope", "percentageApplicationKind",
            "targetSelector", "calculationOrder", "roundingRuleId", "calculationStepId");
        var baseScope = RequiredString(entry.Values, "percentageBaseScope", fileName) switch
        {
            "per-service-code-unit" => PercentageBaseScope.PerServiceCodeUnit,
            "monthly-target-unit-sum" => PercentageBaseScope.MonthlyTargetUnitSum,
            _ => throw new InvalidDataException(
                $"Claim master file '{fileName}' has an unknown percentageBaseScope."),
        };
        var applicationKind = RequiredString(
            entry.Values, "percentageApplicationKind", fileName) switch
        {
            "replace" => PercentageApplicationKind.Replace,
            "add" => PercentageApplicationKind.Add,
            "subtract" => PercentageApplicationKind.Subtract,
            _ => throw new InvalidDataException(
                $"Claim master file '{fileName}' has an unknown percentageApplicationKind."),
        };
        var roundingRuleId = RequiredString(entry.Values, "roundingRuleId", fileName);
        if (!string.Equals(roundingRuleId, PercentageRoundingRuleId, StringComparison.Ordinal))
            throw new InvalidDataException($"Claim master file '{fileName}' has an unknown roundingRuleId.");
        var calculationStepId = RequiredString(entry.Values, "calculationStepId", fileName);
        var expectedStepId = baseScope switch
        {
            PercentageBaseScope.PerServiceCodeUnit => PerServiceCodeStepId,
            PercentageBaseScope.MonthlyTargetUnitSum => MonthlyTargetStepId,
            _ => throw new InvalidOperationException("Percentage base scope is closed."),
        };
        if (!string.Equals(calculationStepId, expectedStepId, StringComparison.Ordinal))
            throw new InvalidDataException($"Claim master file '{fileName}' has a mismatched calculationStepId.");

        var calculationOrder = PositiveInt(entry.Values, "calculationOrder", fileName);
        return new PercentageAdjustmentMasterRow(
            entry.Key,
            NonNegativeDecimalString(entry.Values, "percentage", fileName),
            baseScope,
            applicationKind,
            RequiredString(entry.Values, "targetSelector", fileName),
            calculationOrder,
            roundingRuleId,
            calculationStepId,
            effectiveFrom,
            effectiveTo,
            source);
    }

    private static RegionUnitPriceMasterRow ParseRegionUnitPrice(
        MasterEntry entry,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        ClaimSourceLocator source,
        string fileName)
    {
        RequireProperties(entry.Values, fileName, "regionKey", "serviceKind", "unitPriceYen");
        return new RegionUnitPriceMasterRow(
            entry.Key,
            RequiredString(entry.Values, "regionKey", fileName),
            RequiredString(entry.Values, "serviceKind", fileName),
            NonNegativeDecimalString(entry.Values, "unitPriceYen", fileName),
            effectiveFrom,
            effectiveTo,
            source);
    }

    private static BurdenCapMasterRow ParseBurdenCap(
        MasterEntry entry,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        ClaimSourceLocator source,
        string fileName)
    {
        RequireProperties(entry.Values, fileName, "burdenCategory", "capYen");
        return new BurdenCapMasterRow(
            entry.Key,
            RequiredString(entry.Values, "burdenCategory", fileName),
            NonNegativeInt(entry.Values, "capYen", fileName),
            effectiveFrom,
            effectiveTo,
            source);
    }

    private static OfficeClaimProfileTransitionRuleMasterRow ParseTransitionRule(
        MasterEntry entry,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        ClaimSourceLocator source,
        string fileName)
    {
        RequireProperties(entry.Values, fileName,
            "masterVersion", "allowedAverageWageBandOptions", "allowedOptionsByR8ReformStatus",
            "r8EffectiveDate", "filedTransitionEndRule", "filedTransitionDurationYears");
        var options = ParseOptions(
            Required(entry.Values, "allowedAverageWageBandOptions", fileName), fileName);
        var optionsByStatusElement = Required(
            entry.Values, "allowedOptionsByR8ReformStatus", fileName);
        if (optionsByStatusElement.ValueKind != JsonValueKind.Object
            || !optionsByStatusElement.EnumerateObject().Any())
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' allowedOptionsByR8ReformStatus must be a non-empty object.");
        }

        var allowedSet = options.ToHashSet();
        var optionsByStatus = optionsByStatusElement.EnumerateObject().ToDictionary(
            property => ParseR8Status(property.Name, fileName),
            property => (IReadOnlyCollection<AverageWageBandOption>)ParseOptions(property.Value, fileName),
            EqualityComparer<R8ReformStatus>.Default);
        if (optionsByStatus.Values.Any(statusOptions =>
                statusOptions.Count == 0 || statusOptions.Any(option => !allowedSet.Contains(option))))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' status options must be a non-empty subset of allowed options.");
        }

        var r8EffectiveDate = ParseDate(
            RequiredString(entry.Values, "r8EffectiveDate", fileName),
            "r8EffectiveDate",
            fileName);
        var endRule = RequiredString(entry.Values, "filedTransitionEndRule", fileName) switch
        {
            "add-years-exclusive" => FiledTransitionExclusiveEndRule.AddYearsExclusive,
            _ => throw new InvalidDataException(
                $"Claim master file '{fileName}' has an unknown filedTransitionEndRule."),
        };
        var durationYears = PositiveInt(
            entry.Values, "filedTransitionDurationYears", fileName);

        return new OfficeClaimProfileTransitionRuleMasterRow(
            entry.Key,
            new ClaimMasterVersion(RequiredString(entry.Values, "masterVersion", fileName)),
            options,
            optionsByStatus,
            r8EffectiveDate,
            endRule,
            durationYears,
            effectiveFrom,
            effectiveTo,
            source);
    }

    private static ServiceCodeMasterRow ParseServiceCode(
        MasterEntry entry,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        ClaimSourceLocator source,
        string fileName)
    {
        RequireProperties(entry.Values, fileName, "serviceCode", "serviceKind", "selectors");
        var selectorsElement = Required(entry.Values, "selectors", fileName);
        if (selectorsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Claim master file '{fileName}' selectors must be an array.");
        var selectors = selectorsElement.EnumerateArray()
            .Select(item => RequiredStringElement(item, "selectors", fileName))
            .ToArray();
        if (selectors.Length == 0
            || selectors.Distinct(StringComparer.Ordinal).Count() != selectors.Length)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' selectors must be non-empty and unique.");
        }

        return new ServiceCodeMasterRow(
            entry.Key,
            RequiredString(entry.Values, "serviceCode", fileName),
            RequiredString(entry.Values, "serviceKind", fileName),
            selectors,
            effectiveFrom,
            effectiveTo,
            source);
    }

    private static AverageWageBandOption[] ParseOptions(JsonElement element, string fileName)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Claim master file '{fileName}' option list must be an array.");
        var options = element.EnumerateArray().Select(option =>
        {
            RequireProperties(option, fileName, "kind", "officialOptionCode");
            var kind = RequiredString(option, "kind", fileName) switch
            {
                "numeric" => AverageWageBandOptionKind.Numeric,
                "filed-transition" => AverageWageBandOptionKind.FiledTransition,
                "production-activity-support" => AverageWageBandOptionKind.ProductionActivitySupport,
                _ => throw new InvalidDataException(
                    $"Claim master file '{fileName}' has an unknown average wage option kind."),
            };
            return new AverageWageBandOption(
                kind,
                PositiveInt(option, "officialOptionCode", fileName));
        }).ToArray();
        if (options.Length == 0 || options.Distinct().Count() != options.Length)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' option list must be non-empty and unique.");
        }

        return options;
    }

    private static R8ReformStatus ParseR8Status(string value, string fileName) => value switch
    {
        "not-applicable-before-r8" => R8ReformStatus.NotApplicableBeforeR8,
        "reform-target" => R8ReformStatus.ReformTarget,
        "reform-exempt" => R8ReformStatus.ReformExempt,
        "unchanged-below-15000" => R8ReformStatus.UnchangedBelow15000,
        _ => throw new InvalidDataException(
            $"Claim master file '{fileName}' has an unknown R8 reform status."),
    };

    private static void ValidateEntryIdentityAndPeriods(
        string fileName,
        IReadOnlyCollection<ParsedEntry> parsed)
    {
        var duplicate = parsed
            .GroupBy(entry => (entry.Key, entry.EffectiveFrom))
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' has duplicate key '{duplicate.Value.Key}' and effectiveFrom '{duplicate.Value.EffectiveFrom}'.");
        }

        foreach (var group in parsed.GroupBy(entry => entry.Key, StringComparer.Ordinal))
            ValidatePeriods(fileName, group.Key, group.OrderBy(entry => entry.EffectiveFrom).ToArray());
    }

    private static void ValidateReferences(
        IReadOnlyCollection<BasicRewardMasterRow> basicRewards,
        IReadOnlyCollection<PercentageAdjustmentMasterRow> percentageAdjustments,
        IReadOnlyCollection<ServiceCodeMasterRow> serviceCodes)
    {
        if (basicRewards.Any(reward => !serviceCodes.Any(serviceCode =>
                string.Equals(serviceCode.ServiceCode, reward.ServiceCode, StringComparison.Ordinal)
                && Covers(serviceCode.EffectiveFrom, serviceCode.EffectiveTo,
                    reward.EffectiveFrom, reward.EffectiveTo))))
            throw new InvalidDataException("A basic reward refers to an unknown service code.");

        if (percentageAdjustments.Any(row =>
                !serviceCodes.Any(serviceCode =>
                    serviceCode.Selectors.Contains(row.TargetSelector, StringComparer.Ordinal)
                    && Covers(serviceCode.EffectiveFrom, serviceCode.EffectiveTo,
                        row.EffectiveFrom, row.EffectiveTo))
                || string.Equals(row.Key, row.TargetSelector, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("A percentage adjustment has an unknown or cyclic selector.");
        }
    }

    private static bool Covers(
        ServiceMonth referenceFrom,
        ServiceMonth? referenceTo,
        ServiceMonth dependentFrom,
        ServiceMonth? dependentTo) =>
        referenceFrom <= dependentFrom
        && (dependentTo is null
            ? referenceTo is null
            : referenceTo is null || referenceTo.Value >= dependentTo.Value);

    private static void ValidateCalculationOrder(
        IReadOnlyCollection<PercentageAdjustmentMasterRow> rows)
    {
        var boundaries = rows.Select(row => row.EffectiveFrom)
            .Concat(rows
                .Where(row => row.EffectiveTo is not null)
                .Select(row => NextMonth(row.EffectiveTo!.Value)))
            .Distinct()
            .Order()
            .ToArray();
        foreach (var boundary in boundaries)
        {
            var activeRows = rows.Where(row =>
                row.EffectiveFrom <= boundary
                && (row.EffectiveTo is null || boundary <= row.EffectiveTo.Value));
            foreach (var selectorRows in activeRows.GroupBy(
                         row => row.TargetSelector,
                         StringComparer.Ordinal))
            {
                var actual = selectorRows.Select(row => row.CalculationOrder).Order().ToArray();
                var expected = Enumerable.Range(1, actual.Length).ToArray();
                if (!actual.SequenceEqual(expected))
                {
                    throw new InvalidDataException(
                        "Percentage adjustment calculationOrder must be unique and contiguous from one.");
                }
            }
        }
    }

    private static void ValidateSelectorCycles(
        IReadOnlyCollection<PercentageAdjustmentMasterRow> rows)
    {
        foreach (var month in rows.Select(row => row.EffectiveFrom).Distinct().Order())
        {
            var activeRows = rows
                .Where(row => row.EffectiveFrom <= month
                              && (row.EffectiveTo is null || month <= row.EffectiveTo.Value))
                .ToArray();
            var dependencies = activeRows.ToDictionary(
                row => row.Key,
                row => row.TargetSelector,
                StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in dependencies.Keys)
            {
                if (HasSelectorCycle(
                        key,
                        dependencies,
                        new HashSet<string>(StringComparer.Ordinal),
                        visited))
                {
                    throw new InvalidDataException(
                        "Percentage adjustment selectors contain a dependency cycle.");
                }
            }
        }
    }

    private static bool HasSelectorCycle(
        string key,
        IReadOnlyDictionary<string, string> dependencies,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(key)) return false;
        if (!visiting.Add(key)) return true;

        var hasCycle = dependencies.TryGetValue(key, out var target)
                       && dependencies.ContainsKey(target)
                       && HasSelectorCycle(target, dependencies, visiting, visited);
        visiting.Remove(key);
        visited.Add(key);
        return hasCycle;
    }

    private static void ValidatePeriods(
        string fileName,
        string key,
        IReadOnlyList<ParsedEntry> entries)
    {
        for (var index = 0; index < entries.Count - 1; index++)
        {
            var current = entries[index];
            var next = entries[index + 1];
            if (current.EffectiveTo is null)
            {
                throw new InvalidDataException(
                    $"Claim master file '{fileName}' key '{key}' has entries after an open-ended range.");
            }

            if (next.EffectiveFrom <= current.EffectiveTo.Value)
            {
                throw new InvalidDataException(
                    $"Claim master file '{fileName}' key '{key}' has overlapping ranges.");
            }

            var expectedNext = NextMonth(current.EffectiveTo.Value);
            if (next.EffectiveFrom != expectedNext)
            {
                throw new InvalidDataException(
                    $"Claim master file '{fileName}' key '{key}' has a gap at '{expectedNext}'.");
            }
        }

        if (entries.Count > 0 && entries[^1].EffectiveTo is { } lastEnd)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' key '{key}' ends at '{lastEnd}' and leaves an implicit future gap.");
        }
    }

    private static void RequireProperties(
        JsonElement element,
        string fileName,
        params string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Claim master file '{fileName}' values must be an object.");
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != required.Length
            || actual.Except(required, StringComparer.Ordinal).Any()
            || required.Except(actual, StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' values properties do not match the closed contract.");
        }
    }

    private static JsonElement Required(JsonElement parent, string propertyName, string fileName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
            throw new InvalidDataException($"Claim master file '{fileName}' lacks '{propertyName}'.");
        return value;
    }

    private static string RequiredString(JsonElement parent, string propertyName, string fileName) =>
        RequiredStringElement(Required(parent, propertyName, fileName), propertyName, fileName);

    private static string RequiredStringElement(JsonElement element, string propertyName, string fileName)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"Claim master file '{fileName}' property '{propertyName}' must be text.");
        var value = element.GetString()!;
        ValidateRequiredText(value, propertyName, fileName);
        return value;
    }

    private static int NonNegativeInt(JsonElement parent, string propertyName, string fileName)
    {
        var value = Integer(parent, propertyName, fileName);
        if (value < 0)
            throw new InvalidDataException($"Claim master file '{fileName}' property '{propertyName}' must be nonnegative.");
        return value;
    }

    private static int PositiveInt(JsonElement parent, string propertyName, string fileName)
    {
        var value = Integer(parent, propertyName, fileName);
        if (value <= 0)
            throw new InvalidDataException($"Claim master file '{fileName}' property '{propertyName}' must be positive.");
        return value;
    }

    private static int Integer(JsonElement parent, string propertyName, string fileName)
    {
        var element = Required(parent, propertyName, fileName);
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
            throw new InvalidDataException($"Claim master file '{fileName}' property '{propertyName}' must be an integer.");
        return value;
    }

    private static decimal NonNegativeDecimalString(
        JsonElement parent,
        string propertyName,
        string fileName)
    {
        var text = RequiredString(parent, propertyName, fileName);
        if (text.Any(character => !char.IsAsciiDigit(character) && character != '.')
            || text.Count(character => character == '.') > 1
            || text[0] == '.'
            || text[^1] == '.'
            || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var value)
            || value < 0)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' property '{propertyName}' must be a nonnegative decimal string.");
        }

        return value;
    }

    private static void ValidateSha256(string value, string fileName)
    {
        if (value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' sourceSha256 must be 64 lowercase hexadecimal characters.");
        }
    }

    private static DateOnly ParseDate(string value, string propertyName, string fileName)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result))
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' property '{propertyName}' must be YYYY-MM-DD.");
        }

        return result;
    }

    private static ServiceMonth ParseMonth(string value, string propertyName, string fileName)
    {
        if (value.Length != 7
            || value[4] != '-'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year is < 1900 or > 2200
            || month is < 1 or > 12)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' property '{propertyName}' must be YYYY-MM.");
        }

        return new ServiceMonth(year, month);
    }

    private static ServiceMonth NextMonth(ServiceMonth month) => month.Month == 12
        ? new ServiceMonth(month.Year + 1, 1)
        : new ServiceMonth(month.Year, month.Month + 1);

    private static void ValidateRequiredText(string value, string propertyName, string fileName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != value.Trim().Length)
        {
            throw new InvalidDataException(
                $"Claim master file '{fileName}' property '{propertyName}' must be non-blank without outer whitespace.");
        }
    }

    private sealed record MasterFile(
        string SchemaVersion,
        string MasterKind,
        IReadOnlyList<MasterEntry> Entries);

    private sealed record MasterEntry(
        string Key,
        string EffectiveFrom,
        string? EffectiveTo,
        string SourceDocumentId,
        string SourceSha256,
        string SourceLocator,
        JsonElement Values);

    private sealed record ParsedEntry(
        string Key,
        ServiceMonth EffectiveFrom,
        ServiceMonth? EffectiveTo);
}
