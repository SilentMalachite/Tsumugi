namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>帳票に印字する受給者証情報（spec §7.4）。</summary>
public sealed record ClaimReportCertificateDto(
    string CertificateNumber,
    string MunicipalityNumber,
    string? SubsidyMunicipalityNumber,
    int MonthlyCostCap,
    string? UpperLimitManagementProviderNumber,
    string? UpperLimitManagementProviderName);
