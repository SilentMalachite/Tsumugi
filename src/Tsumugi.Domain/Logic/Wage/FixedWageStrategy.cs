using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>固定方式: PresentDays × WageSettings.FixedDailyYen をそのまま採用（按分なし）。</summary>
public sealed class FixedWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Fixed;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.FixedDailyYen is not { } daily)
            throw new InvalidOperationException("Fixed 方式では WageSettings.FixedDailyYen が必要です。");
        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId, i.PresentDays * daily,
                $"固定方式: {i.PresentDays}日×{daily:N0}円"))
            .ToArray();
    }
}
