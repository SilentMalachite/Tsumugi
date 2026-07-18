namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// ADR 0025が定める端数規則を<c>roundingRuleId</c>で引く。アルゴリズムのみを持ち、単位数・単価・割合等の
/// 制度値は保持しない。ここで扱うのは、丸め結果が整数（単位・円）へ収束する規則に限る。ADR 0025決定
/// 「RoundingPolicyの安定roundingRuleId」表の残り2規則（平均工賃の日平均利用者数・平均工賃月額）は
/// 出力が小数点第1位までのdecimalであるか、責務がAverageWageCalculator側にあるため、
/// <c>decimal → int</c>である本APIの<see cref="Apply"/>では扱わない。
/// </summary>
public static class ClaimRoundingRules
{
    /// <summary>
    /// 割合計算結果又は基準該当B型の公式式・地方公共団体比較補正結果である単位数の四捨五入。
    /// ADR 0025決定「RoundingPolicyの安定roundingRuleId」表・`r6-calculation-note` p8〜9。
    /// </summary>
    public const string UnitsHalfUpV1 = "claim.rounding.units.half-up.v1";

    /// <summary>
    /// 月次給付単位数と地域単価の積である総費用額の円未満切捨て。
    /// ADR 0025決定同表・`r6-calculation-note` p9、`r8-grant-decision-administration-202606/202607` p197〜198。
    /// </summary>
    public const string CostFloorYenV1 = "claim.rounding.cost.floor-yen.v1";

    /// <summary>
    /// 総費用額と10/100の積である1割相当額の円未満切捨て。
    /// ADR 0025決定同表・`r8-grant-decision-administration-202606/202607` p197〜198。
    /// </summary>
    public const string BurdenFloorYenV1 = "claim.rounding.burden.floor-yen.v1";

    /// <summary>
    /// <paramref name="roundingRuleId"/>に対応する規則で<paramref name="value"/>を整数へ丸める。
    /// 四捨五入は非負値に対する<see cref="MidpointRounding.AwayFromZero"/>、切捨ては非負値に対する
    /// <see cref="decimal.Floor(decimal)"/>として実装する（ADR 0025・既定のbanker's roundingへ依存しない）。
    /// 未登録の<paramref name="roundingRuleId"/>は既定規則へフォールバックせず例外にする。
    /// </summary>
    public static int Apply(string roundingRuleId, decimal value)
        => roundingRuleId switch
        {
            UnitsHalfUpV1 => checked((int)Math.Round(value, 0, MidpointRounding.AwayFromZero)),
            CostFloorYenV1 or BurdenFloorYenV1 => checked((int)decimal.Floor(value)),
            _ => throw new ClaimCalculationException(ClaimCalculationErrorCode.RoundingRuleUnavailable),
        };
}
