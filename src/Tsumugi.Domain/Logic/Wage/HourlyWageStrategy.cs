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
/// なお ①③ の混在（就労実績があるのに DailyBreakdown が無い利用者が同一バッチに含まれる）は
/// 過少支給防止のため ArgumentException で拒否する。
/// </summary>
public sealed class HourlyWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Hourly;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);

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
            // 混在バッチの防御: 一部の利用者にのみ DailyBreakdown がある状態で、
            // 就労実績のある利用者の時給分を黙って 0 円にしない（過少支給の防止）
            if (hasAnyBreakdown && input.DailyBreakdown is null && input.TotalWorkedMinutes > 0)
                throw new ArgumentException(
                    $"就労実績があるのに DailyBreakdown が未指定の利用者が含まれています（利用者ID: {input.RecipientId}）。" +
                    "時給マスタの設定漏れがないか確認してください。", nameof(inputs));

            var hourlyYen = 0;
            if (input.DailyBreakdown is { } days)
            {
                // 時給単価ごとに総時間を集計してから一括丸め（月中レート変更対応）
                var groups = days
                    .GroupBy(d => d.HourlyYen)
                    .Select(g => (HourlyYen: g.Key, TotalMinutes: g.Sum(d => d.Minutes)));

                foreach (var (yen, totalMins) in groups)
                {
                    if (totalMins < 0)
                        throw new ArgumentException(
                            $"就労時間に負の値が含まれています。", nameof(inputs));
                    if (totalMins % unit != 0)
                        throw new ArgumentException(
                            $"就労時間は{unit}分単位で指定してください。", nameof(inputs));
                    hourlyYen += RoundingPolicy.Round(totalMins / 60m * yen, rule);
                }
            }

            var workAllow = AllowancePolicy.WorkAllowanceYen(settings, input.PresentDays);
            var skillAllow = AllowancePolicy.SkillAllowanceYen(settings, input.TotalWorkedMinutes);

            var total = hourlyYen + workAllow + skillAllow;
            var summary =
                $"時給 {hourlyYen:N0} 円 + 作業手当 {workAllow:N0} 円 + 職能手当 {skillAllow:N0} 円";
            items.Add(new WageLineItem(input.RecipientId, total, summary));
        }
        return items;
    }
}
