using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Claim;

/// <summary>
/// readiness に渡す日次記録 1 行分（縮約の入力）。DB 由来（訂正反映済みの実効レコード）と
/// 確定 snapshot 由来のどちらもこの形へ詰め替えてから縮約する。
/// </summary>
public sealed record ClaimReadinessDailyRow(
    DateOnly ServiceDate,
    Attendance Attendance,
    TimeOnly? ServiceStartTime,
    TimeOnly? ServiceEndTime,
    int? SpecialVisitSupportMinutes,
    bool OffsiteSupportApplied,
    MedicalCoordinationType MedicalCoordinationType,
    TrialUseSupportType TrialUseSupportType,
    bool RegionalCollaborationApplied,
    bool IntensiveSupportApplied,
    bool EmergencyAdmissionApplied,
    RecipientConfirmationStatus RecipientConfirmation,
    int? SpecialVisitSupportBilledHours);

/// <summary>
/// 日次記録から <see cref="ClaimDailyRecordAggregate"/> を作る<b>唯一の縮約</b>。
/// </summary>
/// <remarks>
/// 確定前（DB 由来）と確定後（snapshot 由来）で縮約規則が違うと、<b>同じ請求が確定できたのに
/// 再評価では項目不足と判定される</b>（例: 初日だけ時刻未入力・一部の日だけ受給者確認済み）。
/// 規則は <see cref="ClaimDailyRecordAggregate"/> の doc-comment が正本で、実装はここだけに置く。
/// </remarks>
public static class ClaimDailyRecordReduction
{
    /// <summary>本体報酬を算定する日（<see cref="Attendance.Present"/>）だけを母集団にする。</summary>
    public static IReadOnlyList<ClaimReadinessDailyRow> PresentDaysInOrder(
        IEnumerable<ClaimReadinessDailyRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return [.. rows
            .Where(row => row.Attendance == Attendance.Present)
            .OrderBy(row => row.ServiceDate)];
    }

    /// <summary>対象日が 1 件も無ければ <see cref="ClaimDailyRecordAggregate.Empty"/>。</summary>
    public static ClaimDailyRecordAggregate Reduce(IEnumerable<ClaimReadinessDailyRow> rows)
    {
        var presentDays = PresentDaysInOrder(rows);
        if (presentDays.Count == 0) return ClaimDailyRecordAggregate.Empty;

        return new ClaimDailyRecordAggregate(
            // 時刻・区分系は「暦日昇順で最初に値が入力された日」を代表にする。先頭日の未入力を
            // そのまま代表にすると、後日に入力があっても不足扱いになる。
            ServiceStartTime: presentDays
                .Select(row => row.ServiceStartTime)
                .FirstOrDefault(value => value is not null),
            ServiceEndTime: presentDays
                .Select(row => row.ServiceEndTime)
                .FirstOrDefault(value => value is not null),
            SpecialVisitSupportMinutesTotal: presentDays
                .Sum(row => row.SpecialVisitSupportMinutes ?? 0),
            OffsiteSupportApplied: presentDays.Any(row => row.OffsiteSupportApplied),
            MedicalCoordinationType: presentDays
                .Select(row => row.MedicalCoordinationType)
                .FirstOrDefault(value => value != MedicalCoordinationType.Unspecified),
            TrialUseSupportType: presentDays
                .Select(row => row.TrialUseSupportType)
                .FirstOrDefault(value => value != TrialUseSupportType.Unspecified),
            RegionalCollaborationApplied: presentDays.Any(row => row.RegionalCollaborationApplied),
            IntensiveSupportApplied: presentDays.Any(row => row.IntensiveSupportApplied),
            EmergencyAdmissionApplied: presentDays.Any(row => row.EmergencyAdmissionApplied),
            RecipientConfirmation: presentDays
                .Select(row => row.RecipientConfirmation)
                .FirstOrDefault(value => value != RecipientConfirmationStatus.Unspecified),
            // どの日にも入力が無ければ null（未入力）。0 を返すと「入力済みの 0」と区別できず
            // 要求条件（自己参照でない）が fail-open する。
            SpecialVisitSupportBilledHoursTotal: presentDays
                .Any(row => row.SpecialVisitSupportBilledHours is not null)
                ? presentDays.Sum(row => row.SpecialVisitSupportBilledHours ?? 0)
                : null);
    }
}
