using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>集中的支援開始日の追記型revision chainを検証し、実効版を解決する。</summary>
public static class IntensiveSupportEpisodePolicy
{
    public static void ValidateHistory(IReadOnlyCollection<IntensiveSupportEpisode> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (history.Count == 0) return;

        var ordered = history.OrderBy(episode => episode.Revision).ToArray();
        var ids = new HashSet<Guid>();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (!ids.Add(ordered[index].Id))
                throw Invalid("IntensiveSupportEpisode履歴内でIDが重複しています。");

            var expectedRevision = index + 1;
            if (ordered[index].Revision != expectedRevision)
                throw Invalid(
                    $"IntensiveSupportEpisode Revision {expectedRevision} が欠落しているか重複しています。");
        }

        var root = ordered[0];
        if (root.Id == Guid.Empty || root.RootId == Guid.Empty || root.Id != root.RootId)
            throw Invalid("IntensiveSupportEpisode Revision 1はroot自身でなければなりません。");
        if (root.Kind != RecordKind.New || root.ExpectedHeadId is not null)
            throw Invalid(
                "IntensiveSupportEpisode Revision 1はNewで、expected headを持たない必要があります。");
        if (root.OfficeId == Guid.Empty || root.RecipientId == Guid.Empty)
            throw Invalid("IntensiveSupportEpisodeの事業所または利用者IDが空です。");

        var officeId = root.OfficeId;
        var recipientId = root.RecipientId;

        for (var index = 0; index < ordered.Length; index++)
        {
            var episode = ordered[index];
            if (episode.Id == Guid.Empty || episode.RootId != root.Id)
                throw Invalid("IntensiveSupportEpisodeのroot IDが履歴内で一致していません。");
            if (episode.OfficeId != officeId || episode.RecipientId != recipientId)
                throw Invalid("IntensiveSupportEpisode履歴に異なる事業所または利用者が混在しています。");
            if (episode.Kind is not RecordKind.New and not RecordKind.Correct and not RecordKind.Cancel)
                throw Invalid($"未知のRecordKind {episode.Kind} です。");
            if (episode.Kind == RecordKind.Cancel && episode.StartDate is not null)
                throw Invalid("IntensiveSupportEpisodeのCancelは開始日を持てません。");
            if (episode.Kind != RecordKind.Cancel && episode.StartDate is null)
                throw Invalid("IntensiveSupportEpisodeのNewまたはCorrectには開始日が必要です。");

            if (index == 0) continue;
            if (episode.Kind == RecordKind.New)
                throw Invalid("IntensiveSupportEpisode Revision 1以外にNewを追加できません。");
            if (episode.ExpectedHeadId is null
                || episode.ExpectedHeadId == Guid.Empty
                || episode.ExpectedHeadId != ordered[index - 1].Id)
                throw Invalid("IntensiveSupportEpisodeのexpected headは直前Revisionを指す必要があります。");
        }
    }

    public static IntensiveSupportEpisode? Effective(
        IReadOnlyCollection<IntensiveSupportEpisode> history)
    {
        ValidateHistory(history);
        if (history.Count == 0) return null;

        var head = history.MaxBy(episode => episode.Revision)!;
        return head.Kind == RecordKind.Cancel ? null : head;
    }

    public static int NextRevision(IReadOnlyCollection<IntensiveSupportEpisode> history)
    {
        ValidateHistory(history);
        return history.Count == 0
            ? 1
            : checked(history.Max(episode => episode.Revision) + 1);
    }

    private static InvalidOperationException Invalid(string message) => new(message);
}
