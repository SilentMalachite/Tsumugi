using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>出来高方式: 実効 WorkRecord の Σ(PieceCount × PieceUnitYen) をそのまま採用し、作業手当・職能手当を加算する。</summary>
public sealed class PieceWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Piece;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);

        var work = settings.WorkAllowancePerDayYen ?? 0;
        var tiers = settings.SkillAllowanceTiers;

        return inputs
            .Select(i =>
            {
                var workAllow = i.PresentDays * work;
                var totalHours = i.TotalWorkedMinutes / 60;
                var skillAllow = 0;
                foreach (var t in tiers)
                    if (totalHours >= t.MinHours) skillAllow = t.Yen;
                var total = i.TotalPieceAmountYen + workAllow + skillAllow;
                return new WageLineItem(i.RecipientId, total,
                    $"出来高方式: 合計{i.TotalPieceAmountYen:N0}円 + 作業手当{workAllow:N0}円 + 職能手当{skillAllow:N0}円");
            })
            .ToArray();
    }
}
