using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>請求マスタ版と公式出典カタログの閉じた対応を検証・解決する。</summary>
public static class ClaimMasterCatalogPolicy
{
    public static void Validate(
        IReadOnlyCollection<ClaimMasterRelease> releases,
        IReadOnlyCollection<ClaimSourceDocument> sources)
        => SnapshotAndValidate(releases, sources);

    public static ClaimMasterRelease Resolve(
        IReadOnlyCollection<ClaimMasterRelease> releases,
        IReadOnlyCollection<ClaimSourceDocument> sources,
        ServiceMonth serviceMonth)
    {
        ValidateServiceMonth(serviceMonth);
        var releaseSnapshot = SnapshotAndValidate(releases, sources);
        var matches = releaseSnapshot
            .Where(release => release.EffectiveFrom <= serviceMonth
                && (release.EffectiveTo is null || serviceMonth <= release.EffectiveTo.Value))
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"サービス提供年月 {serviceMonth} に適用できる請求マスタ版がありません。");
        }

        return matches[0];
    }

    private static ClaimMasterRelease[] SnapshotAndValidate(
        IReadOnlyCollection<ClaimMasterRelease> releases,
        IReadOnlyCollection<ClaimSourceDocument> sources)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(sources);

        var releaseSnapshot = releases.ToArray();
        var sourceSnapshot = sources.ToArray();
        if (releaseSnapshot.Any(release => release is null))
            throw new ArgumentException("請求マスタ版カタログにnull要素を含められません。", nameof(releases));
        if (sourceSnapshot.Any(source => source is null))
            throw new ArgumentException("出典カタログにnull要素を含められません。", nameof(sources));
        if (releaseSnapshot.Length == 0)
            throw new ArgumentException("請求マスタ版カタログを空にできません。", nameof(releases));

        ValidateSources(sourceSnapshot);
        ValidateReleases(releaseSnapshot, sourceSnapshot);

        return releaseSnapshot;
    }

    private static void ValidateSources(IReadOnlyCollection<ClaimSourceDocument> sources)
    {
        var duplicateSource = sources
            .GroupBy(source => source.DocumentId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateSource is not null)
        {
            throw new ArgumentException(
                $"出典document ID '{duplicateSource}' が重複しています。",
                nameof(sources));
        }

        var sourceIds = sources
            .Select(source => source.DocumentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (source.Supersedes is { } supersedes && !sourceIds.Contains(supersedes))
            {
                throw new ArgumentException(
                    $"出典 '{source.DocumentId}' のsupersedes '{supersedes}' は出典カタログに存在しません。",
                    nameof(sources));
            }
        }
    }

    private static void ValidateReleases(
        IReadOnlyCollection<ClaimMasterRelease> releases,
        IReadOnlyCollection<ClaimSourceDocument> sources)
    {
        var duplicateVersion = releases
            .GroupBy(release => release.Version.Value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateVersion is not null)
        {
            throw new ArgumentException(
                $"請求マスタ版 '{duplicateVersion}' が重複しています。",
                nameof(releases));
        }

        var duplicateStart = releases
            .GroupBy(release => release.EffectiveFrom)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateStart is not null)
        {
            var versions = duplicateStart.Select(release => release.Version.Value).ToArray();
            throw new ArgumentException(
                $"請求マスタ版 '{versions[0]}' と '{versions[1]}' の開始月 {duplicateStart.Key} が重複しています。",
                nameof(releases));
        }

        var sourceIds = sources
            .Select(source => source.DocumentId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var release in releases)
        {
            foreach (var sourceId in release.SourceDocumentIds)
            {
                if (!sourceIds.Contains(sourceId))
                {
                    throw new ArgumentException(
                        $"請求マスタ版 '{release.Version}' の出典document ID '{sourceId}' は出典カタログに存在しません。",
                        nameof(releases));
                }
            }
        }

        var ordered = releases.OrderBy(release => release.EffectiveFrom).ToArray();
        for (var index = 0; index < ordered.Length - 1; index++)
        {
            var current = ordered[index];
            var next = ordered[index + 1];
            if (current.EffectiveTo is null)
            {
                throw new ArgumentException(
                    $"open-endedな請求マスタ版 '{current.Version}' の後に版 '{next.Version}' ({next.EffectiveFrom}) を追加できません。",
                    nameof(releases));
            }

            if (next.EffectiveFrom <= current.EffectiveTo.Value)
            {
                throw new ArgumentException(
                    $"請求マスタ版 '{current.Version}' と '{next.Version}' は {next.EffectiveFrom} で重複しています。",
                    nameof(releases));
            }

            var expectedNext = NextMonth(current.EffectiveTo.Value);
            if (next.EffectiveFrom != expectedNext)
            {
                throw new ArgumentException(
                    $"請求マスタ版 '{current.Version}' と '{next.Version}' の間に暗黙の空白月 {expectedNext} があります。明示的なUnavailable版が必要です。",
                    nameof(releases));
            }
        }
    }

    private static ServiceMonth NextMonth(ServiceMonth month)
        => month.Month == 12
            ? new ServiceMonth(month.Year + 1, 1)
            : new ServiceMonth(month.Year, month.Month + 1);

    private static void ValidateServiceMonth(ServiceMonth serviceMonth)
    {
        if (serviceMonth.Year is < 1900 or > 2200 || serviceMonth.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceMonth),
                serviceMonth,
                $"サービス提供年月 {serviceMonth} が不正です。");
        }
    }
}
