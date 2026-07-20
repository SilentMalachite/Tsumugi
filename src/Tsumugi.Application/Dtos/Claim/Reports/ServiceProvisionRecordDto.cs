using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>サービス提供実績記録票（A4、利用者×月次）の生成入力（spec §7.1）。</summary>
public sealed record ServiceProvisionRecordDto(
    ClaimReportOfficeDto Office,
    ClaimReportRecipientDto Recipient,
    ClaimReportCertificateDto Certificate,
    YearMonth YearMonth,
    IReadOnlyList<DailyServiceRecordDto> Days,
    IntensiveSupportEpisodeDto? IntensiveSupport,
    ClaimReportSpecVersionDto SpecVersion);
