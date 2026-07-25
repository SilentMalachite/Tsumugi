using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

/// <summary>
/// 制度マスタ（region-unit-prices）から請求CSV用の地域区分コードと単位数単価を解決する。
/// マスタキーと尺度はこの層に閉じ込め、Application/Domain へ漏らさない。
/// </summary>
/// <remarks>
/// 地域区分コードは <c>RegionGrade.GradeN</c> が N 級地そのものであることから、CSV の 2 桁コードを
/// 級地番号のゼロ詰めとする。<c>Other</c> / <c>None</c> に対応する公式コードは本リポジトリの
/// 一次資料から一意に確定できないため fail-close し、<c>docs/open-questions.md</c> に起票している。
/// </remarks>
public sealed class ClaimMasterCsvOfficeContextProvider(IClaimMasterProvider masterProvider)
    : IClaimCsvOfficeContextProvider
{
    private const string ServiceKindKey = "employment-continuation-support";
    private const string RegionKeyPrefix = "region-grade-";
    private const string OtherRegionKey = "region-other";

    /// <summary>単位数単価を CSV へ書くときの尺度（1/1000円単位）。</summary>
    private const int UnitPriceScale = 1000;

    public ClaimCsvOfficeContext Resolve(RegionGrade regionGrade, ServiceMonth serviceMonth) =>
        new(RegionClassificationCode(regionGrade), UnitPriceMilliYen(regionGrade, serviceMonth));

    private static string RegionClassificationCode(RegionGrade regionGrade) => regionGrade switch
    {
        >= RegionGrade.Grade1 and <= RegionGrade.Grade7 =>
            ((int)regionGrade).ToString("D2", CultureInfo.InvariantCulture),
        _ => throw new ClaimCsvExportFailedException(
            fieldId: string.Empty,
            reason: "UnknownRegionClassification",
            detail: "the official region classification code for this grade is not determined by repository sources"),
    };

    private int UnitPriceMilliYen(RegionGrade regionGrade, ServiceMonth serviceMonth)
    {
        var regionKey = regionGrade == RegionGrade.Other
            ? OtherRegionKey
            : $"{RegionKeyPrefix}{(int)regionGrade}";
        var candidates = masterProvider.ResolveCalculationMasters(serviceMonth).RegionUnitPrices
            .Where(row => string.Equals(row.RegionKey, regionKey, StringComparison.Ordinal)
                && string.Equals(row.ServiceKind, ServiceKindKey, StringComparison.Ordinal)
                && row.EffectiveFrom <= serviceMonth
                && (row.EffectiveTo is null || serviceMonth <= row.EffectiveTo))
            .ToArray();

        return candidates.Length == 1
            ? (int)decimal.Round(candidates[0].UnitPriceYen * UnitPriceScale, 0, MidpointRounding.ToZero)
            : throw new ClaimCsvExportFailedException(
                fieldId: string.Empty,
                reason: "UnitPriceUnresolved",
                detail: $"{candidates.Length} unit price rows matched the region for the service month");
    }
}
