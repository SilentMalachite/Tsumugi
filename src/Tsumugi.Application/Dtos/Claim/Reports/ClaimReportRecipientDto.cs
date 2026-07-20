namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>帳票に印字する受給者氏名（spec §7.4）。</summary>
public sealed record ClaimReportRecipientDto(
    string KanjiName,
    string KanaName);
