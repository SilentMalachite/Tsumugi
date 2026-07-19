namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// ADR 0023が確定した平均工賃月額の正式式
/// 「年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12」の純粋計算。
/// 日付・乱数・I/Oに依存せず、入力は<see cref="Entities.AverageWageAnnualEvidence"/>の
/// 確認済み前年度実績（<c>AnnualWagePaidYen</c> / <c>AnnualExtendedUsers</c> /
/// <c>AnnualOpeningDays</c>）を値で受ける。
/// </summary>
/// <remarks>
/// 計算順と丸めはADR 0023「計算順と丸め」を変更しない:
/// <list type="number">
/// <item>日平均利用者数 = 延べ利用者数 ÷ 開所日数（十進数）。</item>
/// <item>小数点第2位以下が1つでもあれば小数点第1位へ正方向切上げ（例 14.679 → 14.7。
/// r6-qa-v2 物理11頁 問24を r6-qa-corr-2 物理4頁が「四捨五入」から「切り上げ」へ訂正）。</item>
/// <item>年間工賃支払総額 ÷ 丸め後日平均利用者数 ÷ 12。12は実開所月数へ置換しない。</item>
/// <item>円未満を四捨五入し、非負整数円とする。</item>
/// </list>
/// 開所日0・延べ利用者0・負値・整数円レンジ超過は0円又は区分8へ倒さず
/// <see cref="ClaimCalculationException"/>（<see cref="ClaimCalculationErrorCode.InvalidInput"/>）で
/// 算定を停止する（ADR 0023 フェイルクローズ条件）。区分（band）導出は本関数の責務外:
/// 数値境界はPaymentBandマスタが未実装のため導出せず、算出額の提示に限る
/// （docs/open-questions.md参照）。Phase 2の<c>AverageWageMetric</c>は暫定集計として別定義の
/// まま変更しない（ADR 0023 責務分離）。
/// </remarks>
public static class AverageWageFormula
{
    /// <summary>平均工賃月額（円）を算出する。</summary>
    /// <param name="annualWagePaidYen">年間工賃支払総額（円、非負）。</param>
    /// <param name="annualExtendedUsers">年間延べ利用者数（人日、正の整数）。</param>
    /// <param name="annualOpeningDays">年間開所日数（日、正の整数）。</param>
    /// <exception cref="ClaimCalculationException">
    /// 分母0・負値・レンジ超過（<see cref="ClaimCalculationErrorCode.InvalidInput"/>）。
    /// </exception>
    public static int Calculate(
        int annualWagePaidYen,
        int annualExtendedUsers,
        int annualOpeningDays)
    {
        if (annualWagePaidYen < 0 || annualExtendedUsers <= 0 || annualOpeningDays <= 0)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);

        var rawDailyAverageUsers = (decimal)annualExtendedUsers / annualOpeningDays;
        var roundedDailyAverageUsers = Math.Ceiling(rawDailyAverageUsers * 10m) / 10m;
        if (roundedDailyAverageUsers <= 0m)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);

        var rawAverageWageMonth = annualWagePaidYen / roundedDailyAverageUsers / 12m;
        var baseAverageWageMonthYen = Math.Round(
            rawAverageWageMonth, 0, MidpointRounding.AwayFromZero);
        if (baseAverageWageMonthYen is < 0m or > int.MaxValue)
            throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);

        return (int)baseAverageWageMonthYen;
    }
}
