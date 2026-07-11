using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>平均工賃年間根拠の追記型履歴と正式入力を検証する。</summary>
public static class AverageWageAnnualEvidencePolicy
{
    public static void ValidateHistory(IReadOnlyCollection<AverageWageAnnualEvidence> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return;

        var ordered = history.OrderBy(item => item.Revision).ToArray();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            if (!ids.Add(item.Id)) throw Invalid("AverageWageAnnualEvidence履歴内でIDが重複しています。");
            if (item.Revision != index + 1) throw Invalid("AverageWageAnnualEvidenceのRevisionが連続していません。");
        }

        var root = ordered[0];
        if (root.Id == Guid.Empty || root.RootId != root.Id) throw Invalid("Revision 1はroot自身でなければなりません。");
        if (root.Kind != RecordKind.New || root.ExpectedHeadId is not null) throw Invalid("Revision 1の履歴metadataが不正です。");
        if (root.OfficeId == Guid.Empty) throw Invalid("事業所IDが空です。");

        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            if (item.Id == Guid.Empty || item.RootId != root.Id) throw Invalid("root IDが履歴内で一致していません。");
            if (item.OfficeId != root.OfficeId || item.SourceFiscalYear != root.SourceFiscalYear
                || item.PeriodStart != root.PeriodStart || item.PeriodEnd != root.PeriodEnd)
                throw Invalid("平均工賃根拠の事業所、年度又は期間が履歴内で一致していません。");
            if (item.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid("未知のRecordKindです。");
            if (index > 0)
            {
                if (item.Kind == RecordKind.New) throw Invalid("Revision 1以外にNewを追加できません。");
                if (item.ExpectedHeadId is null || item.ExpectedHeadId == Guid.Empty || item.ExpectedHeadId != ordered[index - 1].Id)
                    throw Invalid("expected headは直前Revisionを指す必要があります。");
            }

            if (item.Kind == RecordKind.Cancel) ValidateCancellation(item);
            else ValidateValue(item);
        }
    }

    public static AverageWageAnnualEvidence? Effective(IReadOnlyCollection<AverageWageAnnualEvidence> history)
    {
        ValidateHistory(history);
        var head = history.Count == 0 ? null : history.MaxBy(item => item.Revision);
        return head?.Kind == RecordKind.Cancel ? null : head;
    }

    public static int NextRevision(IReadOnlyCollection<AverageWageAnnualEvidence> history)
    {
        ValidateHistory(history);
        return history.Count == 0 ? 1 : checked(history.Max(item => item.Revision) + 1);
    }

    private static void ValidateValue(AverageWageAnnualEvidence item)
    {
        if (item.SourceFiscalYear is < 1900 or > 2199
            || item.PeriodStart != new DateOnly(item.SourceFiscalYear, 4, 1)
            || item.PeriodEnd != new DateOnly(item.SourceFiscalYear + 1, 3, 31))
            throw Invalid("対象年度と実績期間が一致していません。");
        if (item.AnnualWagePaidYen is null or < 0) throw Invalid("年間工賃支払総額が不正です。");
        if (item.AnnualExtendedUsers is null or <= 0) throw Invalid("年間延べ利用者数が不正です。");
        if (item.AnnualOpeningDays is null or <= 0) throw Invalid("年間開所日数が不正です。");
        if (item.Completeness != FiscalYearCompleteness.Complete) throw Invalid("対象年度の実績が完全ではありません。");
        if (string.IsNullOrWhiteSpace(item.EvidenceDocumentId)
            || string.IsNullOrWhiteSpace(item.DailyEvidenceReference)
            || string.IsNullOrWhiteSpace(item.MonthlyEvidenceReference)
            || item.ConfirmedAt is null || item.ConfirmedAt == DateTimeOffset.MinValue
            || string.IsNullOrWhiteSpace(item.ConfirmedBy) || string.IsNullOrWhiteSpace(item.ConfirmationReason))
            throw Invalid("平均工賃根拠の確認証跡が不足しています。");
    }

    private static void ValidateCancellation(AverageWageAnnualEvidence item)
    {
        if (item.AnnualWagePaidYen is not null || item.AnnualExtendedUsers is not null
            || item.AnnualOpeningDays is not null || item.Completeness is not null
            || item.EvidenceDocumentId is not null || item.DailyEvidenceReference is not null
            || item.MonthlyEvidenceReference is not null || item.ConfirmedAt is not null
            || item.ConfirmedBy is not null || item.ConfirmationReason is not null)
            throw Invalid("取消には平均工賃の業務値を保持できません。");
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
