using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record CalculateClaimRequest(Guid OfficeId, ServiceMonth ServiceMonth);

public sealed record CloseClaimRequest(Guid OfficeId, ServiceMonth ServiceMonth, string ExpectedPreviewHash);

public sealed record CancelClaimRequest(Guid OfficeId, ServiceMonth ServiceMonth);

public sealed record QueryClaimRequest(Guid OfficeId, ServiceMonth ServiceMonth);

public sealed record ClaimPreviewDetailDto(
    Guid RecipientId,
    string ServiceCode,
    int BilledDays,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen);

/// <summary>
/// 算定プレビュー。<see cref="IsReady"/>が偽のとき算定はスキップされ、
/// <see cref="PreviewHash"/>は空・<see cref="Details"/>は空・合計は0を返す。
/// <see cref="ClaimMasterVersion"/>は月に適用される版が解決できないとき空文字。
/// </summary>
public sealed record ClaimPreviewDto(
    ServiceMonth ServiceMonth,
    string ClaimMasterVersion,
    string PreviewHash,
    IReadOnlyList<ClaimPreviewDetailDto> Details,
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen,
    IReadOnlyList<ClaimPreparationIssue> Issues,
    bool IsReady)
{
    public static ClaimPreviewDto NotReady(
        ServiceMonth serviceMonth,
        string claimMasterVersion,
        IReadOnlyList<ClaimPreparationIssue> issues)
        => new(
            serviceMonth,
            claimMasterVersion,
            PreviewHash: "",
            Details: [],
            TotalUnits: 0,
            TotalCostYen: 0,
            TotalBenefitYen: 0,
            TotalBurdenYen: 0,
            issues,
            IsReady: false);
}

public sealed record ClaimBatchRevisionDto(Guid BatchId, int Revision, bool IsReplay);

public sealed record ClaimBatchDetailDto(
    Guid RecipientId,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen);

/// <summary>確定請求履歴の1 revision（検証はrepository/policy側の責務。本DTOは写像のみ）。</summary>
public sealed record ClaimBatchHistoryDto(
    Guid BatchId,
    int Revision,
    RecordKind Kind,
    Guid? OriginId,
    Guid FinalizationOperationId,
    string ClaimMasterVersion,
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    IReadOnlyList<ClaimBatchDetailDto> Details);
