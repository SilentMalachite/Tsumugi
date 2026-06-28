using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

/// <summary>
/// 月次の工賃基礎を抽出する純粋関数。
/// 出席日数は実効 DailyRecord で Attendance.Present のもののみ、
/// 作業実績量は実効 WorkRecord から合算する。
/// </summary>
public static class WageBasisExtractor
{
    public static IReadOnlyList<WageInputs> Build(
        IEnumerable<DailyRecord> dailyRecords,
        IEnumerable<WorkRecord> workRecords,
        YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(dailyRecords);
        ArgumentNullException.ThrowIfNull(workRecords);

        var firstDay = month.FirstDay();
        var lastDay = month.LastDay();

        var dailyByRecipient = dailyRecords
            .Where(r => r.ServiceDate >= firstDay && r.ServiceDate <= lastDay)
            .GroupBy(r => r.RecipientId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var workByRecipient = workRecords
            .Where(r => r.WorkDate >= firstDay && r.WorkDate <= lastDay)
            .GroupBy(r => r.RecipientId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var allRecipients = dailyByRecipient.Keys.Union(workByRecipient.Keys);

        return allRecipients
            .Select(rid =>
            {
                var effDaily = dailyByRecipient.TryGetValue(rid, out var dd)
                    ? DailyRecordPolicy.EffectiveByDate(dd).Values
                    : Enumerable.Empty<DailyRecord>();
                var presentDates = effDaily
                    .Where(r => r.Attendance == Attendance.Present)
                    .Select(r => r.ServiceDate)
                    .ToHashSet();

                var effWork = workByRecipient.TryGetValue(rid, out var ww)
                    ? WorkRecordPolicy.EffectiveByDate(ww).Values
                    : Enumerable.Empty<WorkRecord>();
                var presentWork = effWork.Where(w => presentDates.Contains(w.WorkDate)).ToArray();

                var totalMinutes = presentWork.Sum(w => w.WorkedMinutes ?? 0);
                var totalPiece = presentWork.Sum(w => (w.PieceCount ?? 0) * (w.PieceUnitYen ?? 0));
                var totalPoints = presentWork.Sum(w => w.Points ?? 0);

                return new WageInputs(rid, presentDates.Count, totalMinutes, totalPiece, totalPoints);
            })
            .ToArray();
    }
}
