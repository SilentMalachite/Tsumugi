using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record SetClaimInputRequest(
    Guid OfficeId,
    Guid RecipientId,
    ServiceMonth ServiceMonth,
    RecordKind Kind,
    Guid? ExpectedHeadId)
{
    public UpperLimitManagementResult? UpperLimitManagementResult { get; init; }
    public int? UpperLimitManagedAmountYen { get; init; }
    public int? MunicipalSubsidyAmountYen { get; init; }
    public ServiceMonth? ExceptionalUsageStartMonth { get; init; }
    public ServiceMonth? ExceptionalUsageEndMonth { get; init; }
    public int? ExceptionalUsageDays { get; init; }
    public int? StandardUsageDayTotal { get; init; }
}

public sealed record SetIntensiveSupportEpisodeRequest(
    Guid OfficeId,
    Guid RecipientId,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    DateOnly? StartDate);

public sealed record SetAverageWageAnnualEvidenceRequest(
    Guid OfficeId,
    int SourceFiscalYear,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    RecordKind Kind,
    Guid? ExpectedHeadId)
{
    public int? AnnualWagePaidYen { get; init; }
    public int? AnnualExtendedUsers { get; init; }
    public int? AnnualOpeningDays { get; init; }
    public FiscalYearCompleteness? Completeness { get; init; }
    public string? EvidenceDocumentId { get; init; }
    public string? DailyEvidenceReference { get; init; }
    public string? MonthlyEvidenceReference { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
}

public sealed record SetOfficeClaimProfileRequest(
    Guid OfficeId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    RecordKind Kind,
    Guid? ExpectedHeadId)
{
    public ClaimMasterVersion? MasterVersion { get; init; }
    public R8ReformStatus? ReformStatus { get; init; }
    public AverageWageBandOption? AverageWageBandOption { get; init; }
    public DateOnly? DesignationDate { get; init; }
    public DateOnly? SupportStartDate { get; init; }
    public VersionedAverageWageBandOption? EarlierRegisteredBandOption { get; init; }
    public ServiceMonth? EarlierRegistrationMonth { get; init; }
    public VersionedAverageWageBandOption? LaterRegisteredBandOption { get; init; }
    public ServiceMonth? LaterRegistrationMonth { get; init; }
    public string? ReformComparisonEvidenceDocumentId { get; init; }
    public DateRange? FiledTransitionPeriod { get; init; }
    public string? FiledTransitionEvidenceDocumentId { get; init; }
    public string? EvidenceDocumentId { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
    public int? CapacityHeadcount { get; init; }
    public string? StaffingKey { get; init; }
    public string? RegionKey { get; init; }
}

public sealed record SetCertificateClaimEvidenceRequest(
    Guid CertificateId,
    DateRange Validity,
    RecordKind Kind,
    Guid? ExpectedHeadId)
{
    public EnteredYen MonthlyCostCap { get; init; }
    public UpperLimitManagementApplicability UpperLimitManagementApplicability { get; init; }
    public string? UpperLimitManagementOfficeNumber { get; init; }
    public Article31SpecialBurdenStatus Article31Status { get; init; }
    public EnteredYen Article31AmountYen { get; init; }
    public DateRange? Article31EffectivePeriod { get; init; }
    public string? OriginalDocumentReference { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
}

public sealed record UpperLimitManagementStatementLineRequest(
    int LineNumber,
    string OfficeNumber,
    string OfficeName,
    EnteredYen TotalCostYen,
    EnteredYen PreManagementBurdenYen,
    EnteredYen ManagedBurdenYen);

public sealed record SetUpperLimitManagementStatementRequest(
    ServiceMonth ServiceMonth,
    Guid RecipientId,
    Guid CertificateId,
    Guid ManagingOfficeId,
    RecordKind Kind,
    Guid? ExpectedHeadId)
{
    public string MunicipalityNumber { get; init; } = string.Empty;
    public string CertificateNumber { get; init; } = string.Empty;
    public EnteredYen CertificateMonthlyCostCap { get; init; }
    public UpperLimitManagementApplicability UpperLimitManagementApplicability { get; init; }
    public string CertificateManagingOfficeNumber { get; init; } = string.Empty;
    public string ManagingOfficeNumber { get; init; } = string.Empty;
    public string ManagingOfficeName { get; init; } = string.Empty;
    public string OriginalCreationKind { get; init; } = string.Empty;
    public DateTimeOffset? ReceivedAt { get; init; }
    public string? OriginalDocumentReference { get; init; }
    public bool IsConfirmed { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
    public UpperLimitManagementResult Result { get; init; }
    public EnteredYen TotalCostYen { get; init; }
    public EnteredYen TotalPreManagementBurdenYen { get; init; }
    public EnteredYen TotalManagedBurdenYen { get; init; }
    public IReadOnlyList<UpperLimitManagementStatementLineRequest> Lines { get; init; } = [];
}

public sealed record ClaimInputRevisionDto(
    Guid Id,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public enum ClaimInputSaveErrorCode
{
    InvalidRequest = 1,
    InvalidHistory = 2,
    ExpectedHeadRequired = 3,
    ExpectedHeadMismatch = 4,
    InvalidValue = 5,
    MasterUnavailable = 6,
}

public enum ClaimInputFieldCode
{
    None = 0,
    Actor = 1,
    Identity = 2,
    RecordKind = 3,
    ExpectedHead = 4,
    History = 5,
    Values = 6,
    Lines = 7,
}

public sealed class ClaimInputSaveException : Exception
{
    public ClaimInputSaveException(
        ClaimInputSaveErrorCode code,
        ClaimInputFieldCode fieldCode)
        : base($"Claim input save failed: {code} ({fieldCode}).")
    {
        Code = code;
        FieldCode = fieldCode;
    }

    public ClaimInputSaveErrorCode Code { get; }
    public ClaimInputFieldCode FieldCode { get; }
}
