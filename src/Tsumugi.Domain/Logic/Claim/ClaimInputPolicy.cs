using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>月次請求固有入力の追記型revision chainを検証し、実効版を解決する。</summary>
public static class ClaimInputPolicy
{
    public static void ValidateHistory(IReadOnlyCollection<ClaimInput> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return;

        var ordered = history.OrderBy(input => input.Revision).ToArray();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (!ids.Add(ordered[index].Id))
                throw Invalid("ClaimInput履歴内でIDが重複しています。");

            var expectedRevision = index + 1;
            if (ordered[index].Revision != expectedRevision)
                throw Invalid($"ClaimInput Revision {expectedRevision} が欠落しているか重複しています。");
        }

        var root = ordered[0];
        if (root.Id == Guid.Empty || root.RootId == Guid.Empty || root.Id != root.RootId)
            throw Invalid("ClaimInput Revision 1はroot自身でなければなりません。");
        if (root.Kind != RecordKind.New || root.ExpectedHeadId is not null)
            throw Invalid("ClaimInput Revision 1はNewで、expected headを持たない必要があります。");
        if (root.OfficeId == Guid.Empty || root.RecipientId == Guid.Empty)
            throw Invalid("ClaimInputの事業所または利用者IDが空です。");

        var officeId = root.OfficeId;
        var recipientId = root.RecipientId;
        var serviceMonth = root.ServiceMonth;
        _ = serviceMonth.ToInt();

        for (var index = 0; index < ordered.Length; index++)
        {
            var input = ordered[index];
            if (input.Id == Guid.Empty || input.RootId != root.Id)
                throw Invalid("ClaimInputのroot IDが履歴内で一致していません。");
            if (input.OfficeId != officeId
                || input.RecipientId != recipientId
                || input.ServiceMonth != serviceMonth)
                throw Invalid("ClaimInput履歴に異なる事業所、利用者またはサービス月が混在しています。");
            if (input.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid($"未知のRecordKind {input.Kind} です。");
            if (input.Kind == RecordKind.Cancel
                && (input.UpperLimitManagementResult is not null
                    || input.UpperLimitManagedAmountYen is not null
                    || input.MunicipalSubsidyAmountYen is not null
                    || input.ExceptionalUsageStartMonth is not null
                    || input.ExceptionalUsageEndMonth is not null
                    || input.ExceptionalUsageDays is not null
                    || input.StandardUsageDayTotal is not null))
                throw Invalid("ClaimInputのCancelは請求入力値を持てません。");
            if (input.UpperLimitManagementResult is { } result && !Enum.IsDefined(result))
                throw Invalid("未知の上限額管理結果です。");

            if (index == 0) continue;
            if (input.Kind == RecordKind.New)
                throw Invalid("ClaimInput Revision 1以外にNewを追加できません。");
            if (input.ExpectedHeadId is null
                || input.ExpectedHeadId == Guid.Empty
                || input.ExpectedHeadId != ordered[index - 1].Id)
                throw Invalid("ClaimInputのexpected headは直前Revisionを指す必要があります。");
        }
    }

    public static ClaimInput? Effective(IReadOnlyCollection<ClaimInput> history)
    {
        ValidateHistory(history);
        if (history.Count == 0) return null;

        var head = history.MaxBy(input => input.Revision)!;
        return head.Kind == RecordKind.Cancel ? null : head;
    }

    public static int NextRevision(IReadOnlyCollection<ClaimInput> history)
    {
        ValidateHistory(history);
        return history.Count == 0
            ? 1
            : checked(history.Max(input => input.Revision) + 1);
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
