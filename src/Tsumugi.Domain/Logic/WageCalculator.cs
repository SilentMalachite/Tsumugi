using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;

namespace Tsumugi.Domain.Logic;

/// <summary>方式戦略の選択と委譲のみを行う純粋関数（数値計算は各戦略が持つ）。</summary>
public static class WageCalculator
{
    public static IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<IWageMethodStrategy> strategies,
        WageMethod method,
        IReadOnlyList<WageInputs> inputs,
        WageFund? fund,
        WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);

        var strategy = strategies.FirstOrDefault(s => s.Method == method)
            ?? throw new InvalidOperationException(
                $"工賃方式 {method} に対応する戦略が登録されていません。");
        return strategy.Calculate(inputs, fund, settings);
    }
}
