using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// ADR 0023の平均工賃月額正式式（年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12）を
/// 固定する。丸め順は (1) 日平均利用者数を小数点第2位以下があれば第1位へ切り上げ、
/// (2) 最終の円未満を四捨五入（r6-qa-v2 問24 + r6-qa-corr-2 訂正）。
/// </summary>
public sealed class AverageWageFormulaTests
{
    [Theory]
    // 日平均14.675 → 切上げ14.7 → 3,000,000 / 14.7 / 12 = 17,006.80… → 17,007円
    [InlineData(3_000_000, 3_522, 240, 17_007)]
    // ADR 0023の中間丸め例: 14.679 → 14.7（1,000,000 / 14.7 / 12 = 5,668.93… → 5,669円）
    [InlineData(1_000_000, 14_679, 1_000, 5_669)]
    // 割り切れる場合は丸め補正なし: 10.0人 → 1,200,000 / 10 / 12 = 10,000円ちょうど
    [InlineData(1_200_000, 2_400, 240, 10_000)]
    // 小数点第1位までなら切上げしない: 14.6 → 100,000 / 14.6 / 12 = 570.77… → 571円
    [InlineData(100_000, 146, 10, 571)]
    // 小数点第2位以下は四捨五入ではなく常に切上げ: 1/3 = 0.333… → 0.4 → 120 / 0.4 / 12 = 25円
    [InlineData(120, 1, 3, 25)]
    // 最終の円未満は四捨五入（0.5は切上げ）: 1.0人 → 90 / 1 / 12 = 7.5 → 8円
    [InlineData(90, 1, 1, 8)]
    // 最終の円未満は四捨五入（0.5未満は切捨て）: 89 / 1 / 12 = 7.41… → 7円
    [InlineData(89, 1, 1, 7)]
    // 支払実績0円は正当な入力（0除算ではない）: 0 / 0.5 / 12 = 0円
    [InlineData(0, 100, 200, 0)]
    // 上限近傍でも整数円レンジに収まる: 0.1人 → int.MaxValue / 0.1 / 12 = 1,789,569,705.8… → 1,789,569,706円
    [InlineData(int.MaxValue, 1, 10, 1_789_569_706)]
    public void Calculate_applies_the_official_order_and_rounding(
        int annualWagePaidYen, int annualExtendedUsers, int annualOpeningDays, int expectedYen)
    {
        AverageWageFormula.Calculate(annualWagePaidYen, annualExtendedUsers, annualOpeningDays)
            .Should().Be(expectedYen);
    }

    [Theory]
    // 開所日0（0除算）を0円・区分8へ倒さない（ADR 0023 フェイルクローズ条件）
    [InlineData(1_000_000, 100, 0)]
    // 延べ利用者0（分子0の分母）も算定不能
    [InlineData(1_000_000, 0, 200)]
    // 負値は全て拒否
    [InlineData(-1, 100, 200)]
    [InlineData(1_000_000, -100, 200)]
    [InlineData(1_000_000, 100, -200)]
    public void Calculate_fails_closed_on_zero_or_negative_inputs(
        int annualWagePaidYen, int annualExtendedUsers, int annualOpeningDays)
    {
        var action = () =>
            AverageWageFormula.Calculate(annualWagePaidYen, annualExtendedUsers, annualOpeningDays);

        action.Should().Throw<ClaimCalculationException>()
            .Which.Code.Should().Be(ClaimCalculationErrorCode.InvalidInput);
    }
}
