using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

public enum ClaimCalculationErrorCode
{
    RegionUnitPriceUnavailable = 1,
    InvalidInput = 2,
    RoundingRuleUnavailable = 3,

    /// <summary>
    /// 加算行のunit ruleが本算定パイプラインの実装範囲外（未知のamount種別・未束縛の
    /// countSelector・想定外のbillingUnit/rounding等）。推測して算定せずフェイルクローズする。
    /// </summary>
    UnsupportedAdditionRule = 4,
}

public sealed class ClaimCalculationException(ClaimCalculationErrorCode code)
    : Exception($"Claim calculation failed: {code}.")
{
    public ClaimCalculationErrorCode Code { get; } = code;
}

/// <summary>
/// 利用者1名分の当月請求入力。<see cref="CertificateMonthlyCapYen"/>は受給者証記載の負担上限月額を
/// 表す整数（円）で、nullを許容しない。ADR 0025のfail-closedテーブルは「確認不能な証上限」を
/// 「上限なし」へ読み替えることを禁じており、算定不能として扱わなければならない。したがって
/// <c>CertificateClaimEvidence.MonthlyCostCap</c>（<c>EnteredYen</c>のtri-state）を本APIへ渡す
/// 呼び出し側（Task 9のbuilder）は、未確認（unconfirmed）の証拠をreadiness gateで弾いたうえで
/// 確定値のみをここへ渡す責務を負う。tri-stateの「未確認」判定はこの境界（呼び出し側）に閉じ込め、
/// 本関数はnullを受理しない・fail-openしない純粋関数として総額を必ず返す。
/// 法31条特例・制度上限・上限額管理結果・同一事業所内調整はADR 0025が定める後続ステップであり、
/// 本スライスでは未実装（別タスクで追加する）。
/// </summary>
/// <param name="AbsenceSupportCount">
/// 欠席時対応の実施回数。実効DailyRecord（訂正・取消反映後）の<c>Attendance=AbsenceSupport</c>
/// 日数（ADR 0028決定5）。月次上限はマスタ行の<see cref="UnitsPerCountAmount.MonthlyCountCap"/>で
/// 算定側がcapする。
/// </param>
/// <param name="MealProvidedDays">
/// 対象利用者への食事提供日数。実効Present日の<c>DailyRecord.MealProvided=true</c>日数に
/// 受給者証の<c>Certificate.MealProvisionApplicable</c>を掛けた値（対象外なら0。builderが適用）。
/// </param>
/// <param name="TransportOneWayCount">
/// 送迎の片道回数。実効Present日の<c>DailyRecord.Transport</c>を片道換算
/// （Outbound/Inbound=1、Round=2）した月次合計（ADR 0028決定5）。
/// </param>
/// <param name="InitialPeriodServiceDays">
/// 利用開始日から30日以内のサービス提供日数（初期加算）。ADR 0028決定5のとおり利用開始日の
/// ストレージが未実装（gap）のため、production builderは常に0を渡す（対応する行はseedしない）。
/// 契約期間開始日からの黙示マッピングは行わない。
/// </param>
/// <param name="TransportSamePremisesOneWayCount">
/// 同一敷地内送迎の片道回数。ADR 0028決定5のとおり同一敷地内判別のストレージが未実装（gap）の
/// ため、production builderは常に0を渡す（対応する行はseedしない）。
/// </param>
public sealed record RecipientClaimSource(
    Guid RecipientId,
    int BilledDays,
    int BenefitRatePercent,
    int CertificateMonthlyCapYen,
    int AbsenceSupportCount = 0,
    int MealProvidedDays = 0,
    int TransportOneWayCount = 0,
    int InitialPeriodServiceDays = 0,
    int TransportSamePremisesOneWayCount = 0);

/// <param name="CountSelectorBindings">
/// countSelectorトークン（seed正準文字列）→<see cref="ClaimCountMetric"/>の束縛。トークン文字列は
/// Domain/Applicationへハードコードできないため（<c>ClaimSpecificationBoundaryTests</c>）、seedと
/// 同居するInfrastructure（<c>OfficeClaimBillingTokenProvider</c>）が値として供給する。
/// 未束縛のcountSelectorを持つ加算行に遭遇した場合はフェイルクローズ
/// （<see cref="ClaimCalculationErrorCode.UnsupportedAdditionRule"/>）。
/// </param>
public sealed record ClaimCalculationRequest(
    ServiceMonth Month,
    ClaimBillingConditionContext Conditions,
    string RegionKey,
    string ServiceKind,
    IReadOnlyList<RecipientClaimSource> Recipients,
    IReadOnlyDictionary<string, ClaimCountMetric>? CountSelectorBindings = null);

/// <summary>加算明細行（serviceCodeごと・単位数0の行は生成しない）。</summary>
public sealed record RecipientClaimAdditionLine(
    string ServiceCode,
    string OfficialLabel,
    int Units);

/// <param name="AdditionLines">
/// 加算明細行（ADR 0028）。<see cref="TotalUnits"/>は基本報酬＋本明細行の合算値。
/// </param>
public sealed record RecipientClaimResult(
    Guid RecipientId,
    string ServiceCode,
    int BilledDays,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen,
    IReadOnlyList<RecipientClaimAdditionLine> AdditionLines);

public sealed record ClaimCalculationResult(
    IReadOnlyList<RecipientClaimResult> Details,
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen);

/// <summary>
/// 基本報酬＋主要加算（ADR 0028: 固定単位系・月次割合系）の請求算定パイプライン。
/// ADR 0025の適用順のうち「月次給付単位数（基本＋固定単位加算の整数合算＋割合加算の丸め後加算）×
/// 地域単価→総費用額切捨て→1割相当額切捨て→証上限min→給付費＝総費用額－決定利用者負担額」に
/// 対応する。法31条特例・上限額管理結果は未実装（別タスク）。
/// <see cref="ServiceCodeResolver.ResolveBasicReward"/>（Task 5）が返す単位数はサービスコードの
/// 合成単位数（既に端数処理済み）であり、本関数では再丸めしない。加算の丸めはマスタ行が指定する
/// <c>roundingRuleId</c>（<see cref="ClaimRoundingRules"/>）にのみ従う。
/// </summary>
public static class ClaimCalculator
{
    public static ClaimCalculationResult Calculate(
        ClaimCalculationMasterBundle masters, ClaimCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(masters);
        ArgumentNullException.ThrowIfNull(request);

        var resolved = ServiceCodeResolver.ResolveBasicReward(masters, request.Month, request.Conditions);
        var additions = ServiceCodeResolver.ResolveAdditions(masters, request.Month, request.Conditions);
        var unitPrice = masters.RegionUnitPrices.SingleOrDefault(
                p => p.RegionKey == request.RegionKey && p.ServiceKind == request.ServiceKind)
            ?? throw new ClaimCalculationException(ClaimCalculationErrorCode.RegionUnitPriceUnavailable);

        var details = request.Recipients
            .Select(source => BuildDetail(resolved, additions, request.CountSelectorBindings, unitPrice, source))
            .ToArray();

        return new ClaimCalculationResult(
            details,
            TotalUnits: details.Sum(d => d.TotalUnits),
            TotalCostYen: details.Sum(d => d.TotalCostYen),
            TotalBenefitYen: details.Sum(d => d.BenefitYen),
            TotalBurdenYen: details.Sum(d => d.BurdenYen));
    }

    private static RecipientClaimResult BuildDetail(
        ResolvedBasicReward resolved,
        IReadOnlyList<ResolvedUnitAddition> additions,
        IReadOnlyDictionary<string, ClaimCountMetric>? countBindings,
        RegionUnitPriceMasterRow unitPrice,
        RecipientClaimSource source)
    {
        // 月の日数上限は暦月で31。0日以下・32日以上・給付率が0〜100外・上限額が負値は入力エラー（フェイルクローズ）。
        // 加算実績カウントも同様に閉域で検証する（負値、日数系の月日数超過、送迎の片道2回/日超過）。
        if (source.BilledDays is <= 0 or > 31
            || source.BenefitRatePercent is < 0 or > 100
            || source.CertificateMonthlyCapYen is < 0
            || source.AbsenceSupportCount is < 0 or > 31
            || source.MealProvidedDays < 0 || source.MealProvidedDays > source.BilledDays
            || source.InitialPeriodServiceDays < 0 || source.InitialPeriodServiceDays > source.BilledDays
            || source.TransportOneWayCount < 0
            || source.TransportOneWayCount > checked(2 * source.BilledDays)
            || source.TransportSamePremisesOneWayCount < 0
            || source.TransportSamePremisesOneWayCount > checked(2 * source.BilledDays))
        {
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);
        }

        // ADR 0025: 月次給付単位数は整数合算（丸めなし）。
        var baseUnits = checked(resolved.UnitsPerDay * source.BilledDays);
        var additionLines = BuildAdditionLines(resolved, additions, countBindings, source, baseUnits);
        var totalUnits = additionLines.Aggregate(baseUnits, (sum, line) => checked(sum + line.Units));

        // ADR 0025: 総費用額＝給付単位数×地域単価の円未満切捨て。
        var totalCostYen = ClaimRoundingRules.Apply(
            ClaimRoundingRules.CostFloorYenV1, totalUnits * unitPrice.UnitPriceYen);

        // ADR 0025: 1割相当額＝総費用額×10/100の円未満切捨て。BenefitRatePercent（給付率）の残余を
        // 負担割合として用いる。標準の給付率90（費用額の9割給付・1割負担）では(100-90)/100=10/100と一致する。
        var burdenSharePercent = 100 - source.BenefitRatePercent;
        var statutoryBurdenYen = ClaimRoundingRules.Apply(
            ClaimRoundingRules.BurdenFloorYenV1, totalCostYen * (decimal)burdenSharePercent / 100m);

        var burdenYen = Math.Min(statutoryBurdenYen, source.CertificateMonthlyCapYen);

        // ADR 0025: 給付費＝総費用額－決定利用者負担額。総費用額×90%を別計算しない。
        var benefitYen = totalCostYen - burdenYen;

        return new RecipientClaimResult(
            source.RecipientId,
            resolved.ServiceCode,
            source.BilledDays,
            totalUnits,
            totalCostYen,
            benefitYen,
            burdenYen,
            additionLines);
    }

    /// <summary>
    /// 加算明細行を構築する（ADR 0028）。第1パス: 固定単位系（<see cref="FixedUnitsAmount"/>＝
    /// 算定単位×1、1日につきは×請求日数。<see cref="UnitsPerCountAmount"/>＝整数単位×回数、
    /// 丸めなし、マスタ行のMonthlyCountCapで回数を上限capする）。第2パス: 割合系
    /// （<see cref="PercentageOfTargetAmount"/>）は「targetSelectorをSelectorsに含む行」
    /// （基本報酬＋固定単位加算。%行自身は含まない＝複利にしない）の単位合計を基底に、
    /// <see cref="ClaimRoundingRules"/>のマスタ指定規則で丸めてから加算する。CalculationOrderの
    /// 昇順で適用し、同一orderの複数行はマスタ不整合としてフェイルクローズする。
    /// </summary>
    private static List<RecipientClaimAdditionLine> BuildAdditionLines(
        ResolvedBasicReward resolved,
        IReadOnlyList<ResolvedUnitAddition> additions,
        IReadOnlyDictionary<string, ClaimCountMetric>? countBindings,
        RecipientClaimSource source,
        int baseUnits)
    {
        var lines = new List<RecipientClaimAdditionLine>();
        var targetComponents = new List<(IReadOnlyList<string> Selectors, int Units)>
        {
            (resolved.Selectors ?? [], baseUnits),
        };

        foreach (var addition in additions)
        {
            if (addition.Amount is PercentageOfTargetAmount) continue;

            var units = addition.Amount switch
            {
                FixedUnitsAmount fixedAmount =>
                    FixedUnits(fixedAmount, addition, source),
                UnitsPerCountAmount perCount =>
                    UnitsPerCount(perCount, addition, countBindings, source),
                _ => throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule),
            };
            if (units == 0) continue;

            lines.Add(new RecipientClaimAdditionLine(addition.ServiceCode, addition.OfficialLabel, units));
            targetComponents.Add((addition.Selectors, units));
        }

        var percentageAdditions = additions
            .Where(addition => addition.Amount is PercentageOfTargetAmount)
            .OrderBy(addition => ((PercentageOfTargetAmount)addition.Amount).CalculationOrder)
            .ThenBy(addition => addition.ServiceCode, StringComparer.Ordinal)
            .ToArray();
        var duplicatedOrder = percentageAdditions
            .GroupBy(addition => ((PercentageOfTargetAmount)addition.Amount).CalculationOrder)
            .Any(group => group.Count() > 1);
        if (duplicatedOrder)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);

        foreach (var addition in percentageAdditions)
        {
            var units = PercentageUnits(
                (PercentageOfTargetAmount)addition.Amount, addition, targetComponents);
            if (units == 0) continue;

            lines.Add(new RecipientClaimAdditionLine(addition.ServiceCode, addition.OfficialLabel, units));
        }

        return lines;
    }

    private static int FixedUnits(
        FixedUnitsAmount amount, ResolvedUnitAddition addition, RecipientClaimSource source)
    {
        if (amount.AddedUnits <= 0)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);
        if (addition.RoundingRuleId is not null)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule);

        return addition.BillingUnit switch
        {
            BillingUnit.PerDay => checked(amount.AddedUnits * source.BilledDays),
            BillingUnit.PerMonth => amount.AddedUnits,
            // 1回につき（PerUse）の固定単位は回数実績を持たないamount種別では算定できない。
            _ => throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule),
        };
    }

    private static int UnitsPerCount(
        UnitsPerCountAmount amount,
        ResolvedUnitAddition addition,
        IReadOnlyDictionary<string, ClaimCountMetric>? countBindings,
        RecipientClaimSource source)
    {
        if (amount.UnitsPerCount <= 0 || amount.MonthlyCountCap is < 1)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);
        if (addition.RoundingRuleId is not null)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule);
        if (countBindings is null
            || !countBindings.TryGetValue(amount.CountSelector, out var metric))
        {
            throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule);
        }

        var count = metric switch
        {
            ClaimCountMetric.ServiceDays => source.BilledDays,
            ClaimCountMetric.AbsenceSupport => source.AbsenceSupportCount,
            ClaimCountMetric.MealProvidedDays => source.MealProvidedDays,
            ClaimCountMetric.TransportOneWay => source.TransportOneWayCount,
            ClaimCountMetric.InitialPeriodServiceDays => source.InitialPeriodServiceDays,
            ClaimCountMetric.TransportOneWaySamePremises => source.TransportSamePremisesOneWayCount,
            // PreviousYearSixMonthEmployment等、実績フィールド未実装のmetricはフェイルクローズ。
            _ => throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule),
        };
        if (amount.MonthlyCountCap is { } cap)
            count = Math.Min(count, cap);

        return checked(amount.UnitsPerCount * count);
    }

    private static int PercentageUnits(
        PercentageOfTargetAmount amount,
        ResolvedUnitAddition addition,
        IReadOnlyList<(IReadOnlyList<string> Selectors, int Units)> targetComponents)
    {
        // 本スライスの割合加算はADR 0028決定4（月次対象合計への加算）のみ。他形態はフェイルクローズ。
        if (amount.ApplicationKind != PercentageApplicationKind.Add
            || amount.PercentageBaseScope != PercentageBaseScope.MonthlyTargetUnitSum)
        {
            throw new ClaimCalculationException(ClaimCalculationErrorCode.UnsupportedAdditionRule);
        }

        if (amount.Percentage <= 0m)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);
        if (addition.RoundingRuleId is not { } roundingRuleId)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.RoundingRuleUnavailable);

        var target = targetComponents
            .Where(component => component.Selectors.Contains(amount.TargetSelector, StringComparer.Ordinal))
            .Aggregate(0, (sum, component) => checked(sum + component.Units));

        return ClaimRoundingRules.Apply(roundingRuleId, target * amount.Percentage);
    }
}
