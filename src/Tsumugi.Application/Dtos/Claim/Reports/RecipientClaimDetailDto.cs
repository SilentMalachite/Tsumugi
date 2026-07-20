namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>請求明細書における受給者1名分の明細（spec §7.3）。</summary>
public sealed record RecipientClaimDetailDto(
    ClaimReportRecipientDto Recipient,
    ClaimReportCertificateDto Certificate,
    IReadOnlyList<ClaimLineDto> Lines,
    int SubtotalUnit,
    int SubtotalCostYen,
    int SubtotalBenefitYen,
    int SubtotalBurdenYen,
    ClaimInputSummaryDto ClaimInput);
