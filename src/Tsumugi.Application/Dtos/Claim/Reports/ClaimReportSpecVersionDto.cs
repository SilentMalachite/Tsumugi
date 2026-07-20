namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>帳票フッタに印字する各種仕様バージョン（spec §7.4）。</summary>
public sealed record ClaimReportSpecVersionDto(
    string ClaimMasterVersion,
    string CsvSpecificationVersion,
    string ReportSpecificationVersion);
