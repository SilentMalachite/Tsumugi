using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos.Claim.Reports;

/// <summary>帳票に印字する事業所情報（spec §7.4）。</summary>
public sealed record ClaimReportOfficeDto(
    string OfficeNumber,
    string OfficeName,
    RegionGrade RegionGrade,
    string PostalCode,
    string Address,
    string PhoneNumber,
    string RepresentativeTitleAndName);
