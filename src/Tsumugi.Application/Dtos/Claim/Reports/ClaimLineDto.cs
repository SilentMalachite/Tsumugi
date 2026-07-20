namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>請求明細書の明細1行分（spec §7.3）。</summary>
public sealed record ClaimLineDto(
    ClaimDetailLineKind Kind,
    string ServiceCode,
    int Unit,
    int Count,
    int AmountYen);
