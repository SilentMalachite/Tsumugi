using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>
/// Hourly 方式の二段階実装。
/// ① DailyBreakdown が指定されている場合: レートごとに合算した総時間に
///   ROUND(合計分/60 × 時給) を適用し、作業手当・職能手当を加算（KouchinModule v5 方式）。
/// ② DailyBreakdown なし・fund あり: 従来の WageFund 比例配分（後方互換）。
/// ③ DailyBreakdown なし・fund なし: 時給 0 ＋ 作業手当・職能手当のみ。
/// </summary>
public sealed class HourlyWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Hourly;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);

        var work = settings.WorkAllowancePerDayYen ?? 0;
        var tiers = settings.SkillAllowanceTiers;
        var rule = settings.Rounding;
        var unit = settings.HourUnitMinutes;

        // 後方互換: 全入力に DailyBreakdown がなく fund が指定された場合は旧来の時間比例配分
        var hasAnyBreakdown = inputs.Any(i => i.DailyBreakdown is not null);
        if (!hasAnyBreakdown && fund is not null)
        {
            var shares = inputs
                .Select(i => (i.RecipientId, (decimal)i.TotalWorkedMinutes))
                .ToArray();
            var alloc = AllocationPolicy.Allocate(
                shares, fund.TotalYen, settings.Rounding, settings.Remainder,
                officeReserveKey: settings.Remainder == RemainderPolicy.ReserveToOffice
                    ? settings.OfficeId
                    : null);
            return inputs
                .Select(i => new WageLineItem(
                    i.RecipientId,
                    alloc.First(a => a.Key == i.RecipientId).AmountYen,
                    $"時間割方式: {i.TotalWorkedMinutes}分 / 原資{fund.TotalYen:N0}円"))
                .ToArray();
        }

        // KouchinModule 方式: DailyBreakdown を用いたレートごと集計丸め ＋ 手当加算
        var items = new List<WageLineItem>(inputs.Count);
        foreach (var input in inputs)
        {
            var hourlyYen = 0;
            if (input.DailyBreakdown is { } days)
            {
                // 時給単価ごとに総時間を集計してから一括丸め（月中レート変更対応）
                var groups = days
                    .GroupBy(d => d.HourlyYen)
                    .Select(g => (HourlyYen: g.Key, TotalMinutes: g.Sum(d => d.Minutes)));

                foreach (var (yen, totalMins) in groups)
                {
                    if (totalMins % unit != 0)
                        throw new ArgumentException(
                            $"就労時間は{unit}分単位で指定してください。", nameof(inputs));
                    hourlyYen += RoundingPolicy.Round(totalMins / 60m * yen, rule);
                }
            }

            var workAllow = input.PresentDays * work;

            var totalHours = input.TotalWorkedMinutes / 60;
            var skillAllow = 0;
            foreach (var t in tiers)
            {
                if (totalHours >= t.MinHours) skillAllow = t.Yen;
            }

            var total = hourlyYen + workAllow + skillAllow;
            var summary =
                $"時給 {hourlyYen:N0} 円 + 作業手当 {workAllow:N0} 円 + 職能手当 {skillAllow:N0} 円";
            items.Add(new WageLineItem(input.RecipientId, total, summary));
        }
        return items;
    }
}
