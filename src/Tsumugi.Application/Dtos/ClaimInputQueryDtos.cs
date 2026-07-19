using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record QueryClaimInputWorkspaceRequest(
    Guid OfficeId,
    Guid RecipientId,
    Guid CertificateId,
    ServiceMonth ServiceMonth,
    int SourceFiscalYear);

public sealed record QueryIntensiveSupportEpisodeRequest(
    Guid OfficeId,
    Guid RecipientId);

public sealed record ClaimInputRevisionChainDto<TRevision>(
    Guid RootId,
    Guid CurrentHeadId,
    Guid? EffectiveHeadId,
    IReadOnlyList<TRevision> Revisions);

public sealed record ClaimInputWorkspaceDto(
    ClaimInputRevisionChainDto<ClaimInputQueryRevisionDto>? ClaimInputChain,
    ClaimInputRevisionChainDto<AverageWageAnnualEvidenceQueryRevisionDto>?
        AverageWageAnnualEvidenceChain,
    IReadOnlyList<ClaimInputRevisionChainDto<OfficeClaimProfileQueryRevisionDto>>
        OfficeClaimProfileChains,
    IReadOnlyList<ClaimInputRevisionChainDto<CertificateClaimEvidenceQueryRevisionDto>>
        CertificateClaimEvidenceChains,
    ClaimInputRevisionChainDto<UpperLimitManagementStatementQueryRevisionDto>?
        UpperLimitManagementStatementChain);

public sealed record ClaimInputQueryRevisionDto(
    Guid Id,
    Guid OfficeId,
    Guid RecipientId,
    ServiceMonth ServiceMonth,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    UpperLimitManagementResult? UpperLimitManagementResult,
    int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen,
    ServiceMonth? ExceptionalUsageStartMonth,
    ServiceMonth? ExceptionalUsageEndMonth,
    int? ExceptionalUsageDays,
    int? StandardUsageDayTotal,
    DateTimeOffset CreatedAt,
    string CreatedBy);

/// <param name="ComputedAverageWageMonthYen">
/// ADR 0023の正式式（<c>AverageWageFormula</c>）で当該revisionの確認済み実績から算出した
/// 平均工賃月額（円）。<c>Completeness=Complete</c>かつ3入力が有効な場合だけ値を持ち、
/// それ以外（取消revision・実績不完全）は<c>null</c>（Task 13）。
/// 区分（band option）の導出は行わない: 数値境界（PaymentBandマスタ）が未実装のため、
/// 導出はdocs/open-questions.mdへ起票のうえ保留し、宣言済み
/// <c>OfficeClaimProfile.AverageWageBandOption</c>が引き続き入力の正となる。
/// </param>
public sealed record AverageWageAnnualEvidenceQueryRevisionDto(
    Guid Id,
    Guid OfficeId,
    int SourceFiscalYear,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    int? AnnualWagePaidYen,
    int? AnnualExtendedUsers,
    int? AnnualOpeningDays,
    FiscalYearCompleteness? Completeness,
    string? EvidenceDocumentId,
    string? DailyEvidenceReference,
    string? MonthlyEvidenceReference,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    string? ConfirmationReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    int? ComputedAverageWageMonthYen);

public sealed record OfficeClaimProfileQueryRevisionDto(
    Guid Id,
    Guid OfficeId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    ClaimMasterVersion? MasterVersion,
    R8ReformStatus? ReformStatus,
    AverageWageBandOption? AverageWageBandOption,
    DateOnly? DesignationDate,
    DateOnly? SupportStartDate,
    VersionedAverageWageBandOption? EarlierRegisteredBandOption,
    ServiceMonth? EarlierRegistrationMonth,
    VersionedAverageWageBandOption? LaterRegisteredBandOption,
    ServiceMonth? LaterRegistrationMonth,
    string? ReformComparisonEvidenceDocumentId,
    DateRange? FiledTransitionPeriod,
    string? FiledTransitionEvidenceDocumentId,
    string? EvidenceDocumentId,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    string? ConfirmationReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    int? CapacityHeadcount,
    string? StaffingKey,
    string? RegionKey);

public sealed record CertificateClaimEvidenceQueryRevisionDto(
    Guid Id,
    Guid CertificateId,
    DateRange Validity,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    EnteredYen MonthlyCostCap,
    UpperLimitManagementApplicability UpperLimitManagementApplicability,
    string? UpperLimitManagementOfficeNumber,
    Article31SpecialBurdenStatus Article31Status,
    EnteredYen Article31AmountYen,
    DateRange? Article31EffectivePeriod,
    string? OriginalDocumentReference,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    string? ConfirmationReason,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record UpperLimitManagementStatementLineQueryDto(
    Guid Id,
    Guid StatementId,
    int LineNumber,
    string OfficeNumber,
    string OfficeName,
    EnteredYen TotalCostYen,
    EnteredYen PreManagementBurdenYen,
    EnteredYen ManagedBurdenYen,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record UpperLimitManagementStatementQueryRevisionDto(
    Guid Id,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    ServiceMonth ServiceMonth,
    Guid RecipientId,
    Guid CertificateId,
    Guid ManagingOfficeId,
    string MunicipalityNumber,
    string CertificateNumber,
    EnteredYen CertificateMonthlyCostCap,
    UpperLimitManagementApplicability UpperLimitManagementApplicability,
    string CertificateManagingOfficeNumber,
    string ManagingOfficeNumber,
    string ManagingOfficeName,
    string OriginalCreationKind,
    DateTimeOffset? ReceivedAt,
    string? OriginalDocumentReference,
    bool IsConfirmed,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedBy,
    string? ConfirmationReason,
    UpperLimitManagementResult Result,
    EnteredYen TotalCostYen,
    EnteredYen TotalPreManagementBurdenYen,
    EnteredYen TotalManagedBurdenYen,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    IReadOnlyList<UpperLimitManagementStatementLineQueryDto> Lines);

public sealed record IntensiveSupportEpisodeQueryRevisionDto(
    Guid Id,
    Guid OfficeId,
    Guid RecipientId,
    Guid RootId,
    int Revision,
    RecordKind Kind,
    Guid? ExpectedHeadId,
    DateOnly? StartDate,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record IntensiveSupportEpisodeHistoryDto(
    Guid? RootId,
    Guid? CurrentHeadId,
    Guid? EffectiveHeadId,
    IReadOnlyList<IntensiveSupportEpisodeQueryRevisionDto> Revisions);

public enum ClaimInputQueryErrorCode
{
    InvalidRequest = 1,
    InvalidHistory = 2,
    MasterUnavailable = 3,
}

public sealed class ClaimInputQueryException : Exception
{
    public ClaimInputQueryException(ClaimInputQueryErrorCode code)
        : base($"Claim input query failed: {code}.")
    {
        Code = code;
    }

    public ClaimInputQueryErrorCode Code { get; }
}
