using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>介護給付費・訓練等給付費等 請求明細書（事業所×月次の受給者別明細）の生成入力（spec §7.3）。</summary>
public sealed record ClaimStatementDto(
    ClaimReportOfficeDto Office,
    YearMonth YearMonth,
    IReadOnlyList<RecipientClaimDetailDto> Recipients,
    int TotalUnit,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen,
    ClaimReportSpecVersionDto SpecVersion);
