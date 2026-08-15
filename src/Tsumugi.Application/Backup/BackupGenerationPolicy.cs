namespace Tsumugi.Application.Backup;

/// <summary>
/// バックアップ世代の保持規則（spec 決定4）。純粋関数。日付は引数で受け取る。
/// 1. 命名規則に合致しないファイル・pre-restore の退避は対象外（触らない）。
/// 2. 同一日の中では最も新しい 1 つだけ残す。
/// 3. 残った日付のうち、基準日から数えて新しい 7 日分だけ残す。
/// </summary>
public static class BackupGenerationPolicy
{
    /// <summary>保持する日数。</summary>
    public const int RetainedDays = 7;

    public static IReadOnlyList<string> SelectForDeletion(
        IEnumerable<string> fileNames, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var parsed = new List<(string Name, DateTimeOffset At)>();
        foreach (var name in fileNames)
        {
            if (BackupFileName.TryParse(name, out var at)) parsed.Add((name, at));
        }

        var deletions = new List<string>();

        var byDay = parsed
            .GroupBy(x => DateOnly.FromDateTime(x.At.UtcDateTime))
            .OrderByDescending(g => g.Key)
            .ToList();

        for (var dayIndex = 0; dayIndex < byDay.Count; dayIndex++)
        {
            var group = byDay[dayIndex];
            var dayIsRetained =
                dayIndex < RetainedDays && group.Key > asOf.AddDays(-RetainedDays);

            if (!dayIsRetained)
            {
                deletions.AddRange(group.Select(x => x.Name));
                continue;
            }

            // 同日は最新 1 つだけ残す。同時刻が複数ある場合は名前順で決定論的に選ぶ。
            var survivor = group
                .OrderByDescending(x => x.At)
                .ThenByDescending(x => x.Name, StringComparer.Ordinal)
                .First();

            deletions.AddRange(group.Where(x => x.Name != survivor.Name).Select(x => x.Name));
        }

        return deletions.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
