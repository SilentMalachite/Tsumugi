using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim.Models;

public enum PercentageBaseScope
{
    PerServiceCodeUnit = 1,
    MonthlyTargetUnitSum = 2,
}

public enum PercentageApplicationKind
{
    Replace = 1,
    Add = 2,
    Subtract = 3,
}

public enum FiledTransitionExclusiveEndRule
{
    AddYearsExclusive = 1,
}

public sealed record ClaimSourceLocator(
    string DocumentId,
    string Sha256,
    string Locator);

public sealed record BasicRewardMasterRow(
    string Key,
    string PaymentBand,
    string StaffingKey,
    string CapacityKey,
    string ServiceCode,
    int Units,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimSourceLocator Source);

public sealed record PercentageAdjustmentMasterRow(
    string Key,
    decimal Percentage,
    PercentageBaseScope BaseScope,
    PercentageApplicationKind ApplicationKind,
    string TargetSelector,
    int CalculationOrder,
    string RoundingRuleId,
    string CalculationStepId,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimSourceLocator Source);

public sealed record RegionUnitPriceMasterRow(
    string Key,
    string RegionKey,
    string ServiceKind,
    decimal UnitPriceYen,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimSourceLocator Source);

public sealed record BurdenCapMasterRow(
    string Key,
    string BurdenCategory,
    int CapYen,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimSourceLocator Source);

public sealed record OfficeClaimProfileTransitionRuleMasterRow(
    string Key,
    ClaimMasterVersion MasterVersion,
    IReadOnlyList<AverageWageBandOption> AllowedAverageWageBandOptions,
    IReadOnlyDictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
        AllowedOptionsByR8ReformStatus,
    DateOnly R8EffectiveDate,
    FiledTransitionExclusiveEndRule FiledTransitionEndRule,
    int FiledTransitionDurationYears,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimSourceLocator Source);

public sealed record ServiceCodeMasterRow(
    string Key,
    string ServiceCode,
    string ServiceKind,
    IReadOnlyList<string> Selectors,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimSourceLocator Source);

public sealed record ClaimCalculationMasterBundle(
    IReadOnlyList<BasicRewardMasterRow> BasicRewards,
    IReadOnlyList<PercentageAdjustmentMasterRow> PercentageAdjustments,
    IReadOnlyList<RegionUnitPriceMasterRow> RegionUnitPrices,
    IReadOnlyList<BurdenCapMasterRow> BurdenCaps,
    IReadOnlyList<OfficeClaimProfileTransitionRuleMasterRow> TransitionRules,
    IReadOnlyList<ServiceCodeMasterRow> ServiceCodes);
