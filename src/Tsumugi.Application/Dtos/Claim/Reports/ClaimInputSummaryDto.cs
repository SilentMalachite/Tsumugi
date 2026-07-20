namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>請求明細書に印字する請求入力の要約（spec §7.4）。</summary>
public sealed record ClaimInputSummaryDto(
    string? UpperLimitManagementResult,
    int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen);
