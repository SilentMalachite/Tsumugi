using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

public enum ClaimCalculationErrorCode
{
    RegionUnitPriceUnavailable = 1,
    InvalidInput = 2,
    RoundingRuleUnavailable = 3,
}

public sealed class ClaimCalculationException(ClaimCalculationErrorCode code)
    : Exception($"Claim calculation failed: {code}.")
{
    public ClaimCalculationErrorCode Code { get; } = code;
}

/// <summary>
/// 利用者1名分の当月請求入力。<see cref="CertificateMonthlyCapYen"/>がnullの場合は受給者証個別上限の
/// 指定なし（1割相当額をそのまま利用者負担とする）を表す。法31条特例・制度上限・上限額管理結果・
/// 同一事業所内調整はADR 0025が定める後続ステップであり、本スライスでは未実装（別タスクで追加する）。
/// </summary>
public sealed record RecipientClaimSource(
    Guid RecipientId, int BilledDays, int BenefitRatePercent, int? CertificateMonthlyCapYen);

public sealed record ClaimCalculationRequest(
    ServiceMonth Month,
    ClaimBillingConditionContext Conditions,
    string RegionKey,
    string ServiceKind,
    IReadOnlyList<RecipientClaimSource> Recipients);

public sealed record RecipientClaimResult(
    Guid RecipientId,
    string ServiceCode,
    int BilledDays,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen);

public sealed record ClaimCalculationResult(
    IReadOnlyList<RecipientClaimResult> Details,
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen);

/// <summary>
/// 基本報酬のみ（割合加減算・月次対象合算・法31条特例・上限額管理結果を含まない）の請求算定パイプライン。
/// ADR 0025の適用順のうち「最終給付単位数×地域単価→総費用額切捨て→1割相当額切捨て→証上限min→
/// 給付費＝総費用額－決定利用者負担額」に対応する最小実装。<see cref="ServiceCodeResolver.ResolveBasicReward"/>
/// （Task 5）が返す単位数はサービスコードの合成単位数（既に端数処理済み）であり、本関数では再丸めしない。
/// </summary>
public static class ClaimCalculator
{
    public static ClaimCalculationResult Calculate(
        ClaimCalculationMasterBundle masters, ClaimCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(masters);
        ArgumentNullException.ThrowIfNull(request);

        var resolved = ServiceCodeResolver.ResolveBasicReward(masters, request.Month, request.Conditions);
        var unitPrice = masters.RegionUnitPrices.SingleOrDefault(
                p => p.RegionKey == request.RegionKey && p.ServiceKind == request.ServiceKind)
            ?? throw new ClaimCalculationException(ClaimCalculationErrorCode.RegionUnitPriceUnavailable);

        var details = request.Recipients
            .Select(source => BuildDetail(resolved, unitPrice, source))
            .ToArray();

        return new ClaimCalculationResult(
            details,
            TotalUnits: details.Sum(d => d.TotalUnits),
            TotalCostYen: details.Sum(d => d.TotalCostYen),
            TotalBenefitYen: details.Sum(d => d.BenefitYen),
            TotalBurdenYen: details.Sum(d => d.BurdenYen));
    }

    private static RecipientClaimResult BuildDetail(
        ResolvedBasicReward resolved, RegionUnitPriceMasterRow unitPrice, RecipientClaimSource source)
    {
        // 月の日数上限は暦月で31。0日以下・32日以上・給付率が0〜100外・上限額が負値は入力エラー（フェイルクローズ）。
        if (source.BilledDays is <= 0 or > 31
            || source.BenefitRatePercent is < 0 or > 100
            || source.CertificateMonthlyCapYen is < 0)
        {
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);
        }

        // ADR 0025: 月次給付単位数は整数合算（丸めなし）。
        var totalUnits = checked(resolved.UnitsPerDay * source.BilledDays);

        // ADR 0025: 総費用額＝給付単位数×地域単価の円未満切捨て。
        var totalCostYen = ClaimRoundingRules.Apply(
            ClaimRoundingRules.CostFloorYenV1, totalUnits * unitPrice.UnitPriceYen);

        // ADR 0025: 1割相当額＝総費用額×10/100の円未満切捨て。BenefitRatePercent（給付率）の残余を
        // 負担割合として用いる。標準の給付率90（費用額の9割給付・1割負担）では(100-90)/100=10/100と一致する。
        var burdenSharePercent = 100 - source.BenefitRatePercent;
        var statutoryBurdenYen = ClaimRoundingRules.Apply(
            ClaimRoundingRules.BurdenFloorYenV1, totalCostYen * (decimal)burdenSharePercent / 100m);

        var burdenYen = source.CertificateMonthlyCapYen is { } cap
            ? Math.Min(statutoryBurdenYen, cap)
            : statutoryBurdenYen;

        // ADR 0025: 給付費＝総費用額－決定利用者負担額。総費用額×90%を別計算しない。
        var benefitYen = totalCostYen - burdenYen;

        return new RecipientClaimResult(
            source.RecipientId,
            resolved.ServiceCode,
            source.BilledDays,
            totalUnits,
            totalCostYen,
            benefitYen,
            burdenYen);
    }
}
