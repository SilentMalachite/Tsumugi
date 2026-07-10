using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>確定請求ヘッダのroot-origin追記履歴をRevisionだけで検証・解決する。</summary>
public static class ClaimBatchPolicy
{
    public static void ValidateHistory(IReadOnlyCollection<ClaimBatch> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return;

        var ordered = history.OrderBy(batch => batch.Revision).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var expectedRevision = index + 1;
            if (ordered[index].Revision != expectedRevision)
                throw Invalid($"Revision {expectedRevision} が欠落しているか重複しています。");
        }

        var root = ordered[0];
        if (root.Kind != RecordKind.New)
            throw Invalid("Revision 1 はNewでなければなりません。");
        if (ordered.Count(batch => batch.Kind == RecordKind.New) != 1)
            throw Invalid("NewはRevision 1の1件だけでなければなりません。");
        if (root.OriginId is not null
            || root.ExpectedHeadBatchId is not null
            || root.ExpectedHeadRevision is not null)
            throw Invalid("NewはOriginまたはExpectedHeadを持てません。");

        var officeId = root.OfficeId;
        var serviceMonth = root.ServiceMonth;
        _ = serviceMonth.ToInt();
        var cancellationSeen = false;

        for (var index = 0; index < ordered.Length; index++)
        {
            var batch = ordered[index];
            if (batch.OfficeId != officeId || batch.ServiceMonth != serviceMonth)
                throw Invalid("履歴に異なるOfficeIdまたはServiceMonthが混在しています。");
            if (batch.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid($"未知のRecordKind {batch.Kind} です。");

            ValidateTotals(batch);

            if (index == 0) continue;
            if (cancellationSeen)
                throw Invalid("Cancelの後にrecordを追加できません。");
            if (batch.Kind == RecordKind.New)
                throw Invalid("Revision 1以外にNewを追加できません。");
            if (batch.OriginId is null || batch.OriginId == Guid.Empty
                || batch.OriginId != root.Id)
                throw Invalid("CorrectまたはCancelのOriginIdは初代Newを指さなければなりません。");
            if (batch.ExpectedHeadBatchId is null || batch.ExpectedHeadBatchId == Guid.Empty
                || batch.ExpectedHeadBatchId != ordered[index - 1].Id)
                throw Invalid("ExpectedHeadBatchIdは直前Revisionを指さなければなりません。");
            if (batch.ExpectedHeadRevision != batch.Revision - 1)
                throw Invalid("ExpectedHeadRevisionはRevisionの直前でなければなりません。");

            cancellationSeen = batch.Kind == RecordKind.Cancel;
        }
    }

    public static ClaimBatch? Head(IReadOnlyCollection<ClaimBatch> history)
    {
        ValidateHistory(history);
        return history.Count == 0 ? null : history.MaxBy(batch => batch.Revision);
    }

    public static int NextRevision(IReadOnlyCollection<ClaimBatch> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return 1;

        var nextRevision = checked(history.Max(batch => batch.Revision) + 1);
        ValidateHistory(history);
        return nextRevision;
    }

    private static void ValidateTotals(ClaimBatch batch)
    {
        if (batch.TotalUnits < 0
            || batch.TotalCostYen < 0
            || batch.TotalBenefitYen < 0
            || batch.TotalBurdenYen < 0)
            throw Invalid("請求合計は0以上でなければなりません。");

        if (batch.Kind == RecordKind.Cancel
            && (batch.TotalUnits != 0
                || batch.TotalCostYen != 0
                || batch.TotalBenefitYen != 0
                || batch.TotalBurdenYen != 0))
            throw Invalid("Cancelの請求合計はすべて0でなければなりません。");
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
