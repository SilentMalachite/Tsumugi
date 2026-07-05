using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>固定方式: PresentDays × WageSettings.FixedDailyYen をそのまま採用（按分なし）し、作業手当・職能手当を加算する。</summary>
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

        var work = settings.WorkAllowancePerDayYen ?? 0;
        var tiers = settings.SkillAllowanceTiers;

        return inputs
            .Select(i =>
            {
                var baseYen = i.PresentDays * daily;
                var workAllow = i.PresentDays * work;
                var totalHours = i.TotalWorkedMinutes / 60;
                var skillAllow = 0;
                foreach (var t in tiers)
                    if (totalHours >= t.MinHours) skillAllow = t.Yen;
                var total = baseYen + workAllow + skillAllow;
                return new WageLineItem(i.RecipientId, total,
                    $"固定方式: {i.PresentDays}日×{daily:N0}円 + 作業手当{workAllow:N0}円 + 職能手当{skillAllow:N0}円");
            })
            .ToArray();
    }
}
