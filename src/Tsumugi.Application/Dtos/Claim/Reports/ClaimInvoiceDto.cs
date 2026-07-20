using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>介護給付費・訓練等給付費等 請求書（事業所×月次の集計）の生成入力（spec §7.2）。</summary>
public sealed record ClaimInvoiceDto(
    ClaimReportOfficeDto Office,
    YearMonth YearMonth,
    int TotalUnit,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen,
    ClaimReportSpecVersionDto SpecVersion);
