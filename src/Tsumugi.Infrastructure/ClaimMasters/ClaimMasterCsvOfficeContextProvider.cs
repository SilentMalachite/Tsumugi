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
/// 地域区分の公式コードは CSV 仕様（共通編のコード一覧）に属するため本クラスでは扱わない。
/// <c>Tsumugi.Infrastructure.Csv</c> の <c>RegionClassificationCodeCatalog</c> が解決する。
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
        new(UnitPriceMilliYen(regionGrade, serviceMonth));

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
