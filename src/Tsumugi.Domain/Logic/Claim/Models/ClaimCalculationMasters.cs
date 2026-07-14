using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim.Models;

public enum PercentageBaseScope
{
    PerServiceCodeUnit = 1,
    MonthlyTargetUnitSum = 2,
}

public enum PercentageApplicationKind
{
    Add = 1,
    Subtract = 2,
    Replace = 3,
}

public enum BillingUnit
{
    PerDay = 1,
    PerMonth = 2,
    PerUse = 3,
}

public enum ClaimSourceEvidenceRole
{
    Authoritative = 1,
    Correction = 2,
    CrossCheck = 3,
}

public enum ClaimSourceSupport
{
    ServiceIdentity = 1,
    Selectors = 2,
    UnitRuleKind = 3,
    UnitRuleValue = 4,
    UnitRuleTarget = 5,
    UnitRuleStep = 6,
    UnitRuleRounding = 7,
    Conditions = 8,
    EffectivePeriod = 9,
    MasterValues = 10,
}

public enum ClaimComponentMasterKind
{
    BasicRewards = 1,
    Additions = 2,
}

public enum ClaimComponentRole
{
    Base = 1,
    Adjustment = 2,
}

public enum ClaimConditionKind
{
    RewardSystem = 1,
    PaymentBand = 2,
    Capacity = 3,
    Staffing = 4,
    AverageWageBand = 5,
    PlanStatus = 6,
    ShortageDuration = 7,
    MunicipalityOwnership = 8,
    R8ReformStatus = 9,
    FacilityClassification = 10,
    EmploymentOutcomeCount = 11,
}

public enum ClaimConditionOperator
{
    Equals = 1,
    In = 2,
    LessThan = 3,
    LessThanOrEqual = 4,
    GreaterThan = 5,
    GreaterThanOrEqual = 6,
}

public enum FiledTransitionExclusiveEndRule
{
    AddYearsExclusive = 1,
}

public sealed record ClaimSourceRef(
    string DocumentId,
    string Sha256,
    string Locator,
    ClaimSourceEvidenceRole EvidenceRole,
    IReadOnlyList<ClaimSourceSupport> Supports);

public abstract record UnitAdjustmentAmount;

public sealed record FixedUnitsAmount(int AddedUnits) : UnitAdjustmentAmount;

public sealed record UnitsPerCountAmount(
    int UnitsPerCount,
    string CountSelector) : UnitAdjustmentAmount;

public sealed record PercentageOfTargetAmount(
    decimal Percentage,
    PercentageApplicationKind ApplicationKind,
    PercentageBaseScope PercentageBaseScope,
    string TargetSelector,
    int CalculationOrder) : UnitAdjustmentAmount;

public sealed record ProratedUnitsAmount(
    int PoolUnitsPerStaff,
    string StaffCountSelector,
    string RecipientCountSelector,
    int? MaximumRecipientsPerStaff) : UnitAdjustmentAmount;

public abstract record ServiceCodeUnitRule(BillingUnit BillingUnit);

public sealed record FixedCompositeUnitRule : ServiceCodeUnitRule
{
    public FixedCompositeUnitRule(int finalUnits, BillingUnit billingUnit)
        : base(billingUnit)
    {
        if (finalUnits == 0)
            throw new ArgumentOutOfRangeException(nameof(finalUnits), "Final units must be nonzero.");

        FinalUnits = finalUnits;
    }

    public int FinalUnits { get; }
}

public sealed record UnitAdditionRule(
    string AdjustmentComponentKey,
    UnitAdjustmentAmount Amount,
    string CalculationStepId,
    string? RoundingRuleId,
    BillingUnit BillingUnit) : ServiceCodeUnitRule(BillingUnit);

public abstract record FormulaUnitRule(
    string BaseComponentKey,
    BillingUnit BillingUnit) : ServiceCodeUnitRule(BillingUnit);

public sealed record BaseComponentPassThroughRule(
    string BaseComponentKey,
    string CalculationStepId,
    string? RoundingRuleId,
    BillingUnit BillingUnit) : FormulaUnitRule(BaseComponentKey, BillingUnit)
;

public sealed record ServiceCodeFormulaFactor(
    int Order,
    decimal Rate,
    IReadOnlyList<string> ConditionSelectors,
    string CalculationStepId,
    string RoundingRuleId);

public sealed record FactorChainRule(
    string BaseComponentKey,
    IReadOnlyList<ServiceCodeFormulaFactor> Factors,
    BillingUnit BillingUnit) : FormulaUnitRule(BaseComponentKey, BillingUnit);

public sealed record ClaimComponentRef(
    ClaimComponentMasterKind MasterKind,
    string Key,
    ClaimComponentRole Role);

public abstract record ClaimConditionOperand;

public sealed record ClaimConditionTokenOperand(string Value) : ClaimConditionOperand;

public sealed record ClaimConditionTokenSetOperand(
    IReadOnlyList<string> Values) : ClaimConditionOperand;

public sealed record ClaimConditionIntegerOperand(int Value) : ClaimConditionOperand;

public sealed record ClaimConditionBooleanOperand(bool Value) : ClaimConditionOperand;

public sealed record ClaimConditionDefinition(
    string Key,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    ClaimConditionKind Kind,
    ClaimConditionOperator Operator,
    ClaimConditionOperand Operand,
    IReadOnlyList<ClaimSourceRef> SourceRefs);

public sealed record BasicRewardMasterRow(
    string Key,
    string PaymentBand,
    string StaffingKey,
    string CapacityKey,
    string ServiceCode,
    int BaseUnits,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    IReadOnlyList<ClaimSourceRef> SourceRefs);

public sealed record UnitAdjustmentMasterRow(
    string Key,
    UnitAdjustmentAmount Amount,
    string CalculationStepId,
    string? RoundingRuleId,
    BillingUnit BillingUnit,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    IReadOnlyList<ClaimSourceRef> SourceRefs);

public sealed record RegionUnitPriceMasterRow(
    string Key,
    string RegionKey,
    string ServiceKind,
    decimal UnitPriceYen,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    IReadOnlyList<ClaimSourceRef> SourceRefs);

public sealed record BurdenCapMasterRow(
    string Key,
    string BurdenCategory,
    int CapYen,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    IReadOnlyList<ClaimSourceRef> SourceRefs);

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
    IReadOnlyList<ClaimSourceRef> SourceRefs);

public sealed record ServiceCodeMasterRow(
    string Key,
    string ServiceCode,
    string OfficialLabel,
    string ServiceKind,
    IReadOnlyList<string> Selectors,
    IReadOnlyList<string> ConditionSelectors,
    ServiceCodeUnitRule UnitRule,
    IReadOnlyList<ClaimComponentRef> ComponentRefs,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    IReadOnlyList<ClaimSourceRef> SourceRefs);

public sealed record ClaimCalculationMasterBundle(
    IReadOnlyList<BasicRewardMasterRow> BasicRewards,
    IReadOnlyList<UnitAdjustmentMasterRow> UnitAdjustments,
    IReadOnlyList<RegionUnitPriceMasterRow> RegionUnitPrices,
    IReadOnlyList<BurdenCapMasterRow> BurdenCaps,
    IReadOnlyList<OfficeClaimProfileTransitionRuleMasterRow> TransitionRules,
    IReadOnlyList<ServiceCodeMasterRow> ServiceCodes,
    IReadOnlyList<ClaimConditionDefinition> ConditionDefinitions);
