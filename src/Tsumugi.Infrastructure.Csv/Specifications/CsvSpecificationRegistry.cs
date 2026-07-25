using System.Globalization;
using System.Text.Json;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Csv.Specifications;

/// <summary>
/// CSV 仕様の版レジストリ。複数の施行分を<b>並存</b>させ、処理対象年月で適用版を選ぶ。
/// </summary>
/// <remarks>
/// <para>
/// インタフェース仕様書は施行分ごとに更新される（報酬改定の3年周期とは別）。版が変わるたびに
/// 仕様データを差し替えるのではなく、<c>csv-specification-versions.json</c> に
/// <b>適用期間つきで追記</b>し、過去月の再出力も当時の版で行えるようにする。
/// </para>
/// <para>
/// 適用期間の鍵は<b>処理対象年月</b>（提出する月）である。サービス提供年月ではない。項目表の
/// 説明文が「サービス提供年月が平成24年3月以前は…」のように<b>過去のサービス提供年月を現行版の
/// 中で条件分岐</b>させているため、版は提出時点で決まる。
/// </para>
/// </remarks>
public sealed class CsvSpecificationRegistry : IClaimCsvSpecificationVersions
{
    private readonly IReadOnlyList<CsvSpecificationVersionEntry> _entries;
    private readonly IReadOnlyDictionary<string, CsvSpecificationCatalog> _catalogsByVersion;

    private CsvSpecificationRegistry(
        IReadOnlyList<CsvSpecificationVersionEntry> entries,
        IReadOnlyDictionary<string, CsvSpecificationCatalog> catalogsByVersion)
    {
        _entries = entries;
        _catalogsByVersion = catalogsByVersion;
    }

    /// <summary>登録されている版（適用開始の昇順）。</summary>
    public IReadOnlyList<CsvSpecificationVersionEntry> Versions => _entries;

    /// <summary>
    /// 現行版（適用終了が無い最新版）。確定時に記録する版であり、readiness を検証した版でもある。
    /// </summary>
    public string Current => _entries[^1].Version;

    public static CsvSpecificationRegistry LoadEmbedded()
    {
        var assembly = typeof(CsvSpecificationRegistry).Assembly;
        using var stream = CsvSpecificationLoader.OpenEmbeddedFile(
            assembly, "csv-specification-versions.json");
        var file = JsonSerializer.Deserialize<CsvSpecificationVersionFile>(
                stream, CsvSpecificationLoader.SerializerOptionsForRegistry)
            ?? throw new InvalidDataException("CSV specification version registry is null.");

        var entries = ValidateEntries(file);
        var catalogs = entries.ToDictionary(
            entry => entry.Version,
            entry => CsvSpecificationLoader.LoadEmbedded(entry.Version),
            StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!string.Equals(catalogs[entry.Version].Version, entry.Version, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"CSV specification version '{entry.Version}' does not match its catalog version "
                    + $"'{catalogs[entry.Version].Version}'.");
            }
        }

        return new CsvSpecificationRegistry(entries, catalogs);
    }

    /// <summary>処理対象年月に適用される仕様。該当版が無ければ fail-close する。</summary>
    public CsvSpecificationCatalog Resolve(ProcessingMonth processingMonth) =>
        _catalogsByVersion[ResolveForProcessingMonth(processingMonth)];

    /// <inheritdoc/>
    public string ResolveForProcessingMonth(ProcessingMonth processingMonth) =>
        ResolveVersion(_entries, processingMonth)
        ?? throw new InvalidOperationException(
            $"処理対象年月 {processingMonth} に適用されるCSV仕様版が登録されていません。"
            + $"（登録済み: {string.Join(", ", _entries.Select(Describe))}）");

    /// <summary>
    /// 適用期間の解決（純関数）。<paramref name="entries"/> は適用開始の昇順・重複と欠落なしを前提とする。
    /// </summary>
    internal static string? ResolveVersion(
        IReadOnlyList<CsvSpecificationVersionEntry> entries,
        ProcessingMonth processingMonth)
    {
        var key = MonthKey(processingMonth.Year, processingMonth.Month);
        foreach (var entry in entries)
        {
            var from = MonthKey(entry.EffectiveFromProcessingMonth);
            var to = entry.EffectiveToProcessingMonth is null
                ? int.MaxValue
                : MonthKey(entry.EffectiveToProcessingMonth);
            if (key >= from && key <= to) return entry.Version;
        }

        return null;
    }

    /// <summary>
    /// 版の並びを検証する。重複・欠落・複数の無期限版はいずれも「どの版で出すべきか」を曖昧にするため
    /// 読み込み時に落とす。
    /// </summary>
    internal static IReadOnlyList<CsvSpecificationVersionEntry> ValidateEntries(
        CsvSpecificationVersionFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"CSV specification version registry has unsupported schemaVersion {file.SchemaVersion}.");
        }

        if (file.Versions.Count == 0)
        {
            throw new InvalidDataException("CSV specification version registry is empty.");
        }

        var entries = file.Versions
            .OrderBy(entry => MonthKey(entry.EffectiveFromProcessingMonth))
            .ToArray();

        if (entries.Select(entry => entry.Version).Distinct(StringComparer.Ordinal).Count() != entries.Length)
        {
            throw new InvalidDataException("CSV specification versions must be unique.");
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.Version) || string.IsNullOrWhiteSpace(entry.Label))
            {
                throw new InvalidDataException("CSV specification version entries must be labelled.");
            }

            if (entry.SourceRefs.Count == 0 || string.IsNullOrWhiteSpace(entry.ApplicabilityNote))
            {
                throw new InvalidDataException(
                    $"CSV specification version '{entry.Version}' must cite its applicability period.");
            }

            var from = MonthKey(entry.EffectiveFromProcessingMonth);
            var to = entry.EffectiveToProcessingMonth is null
                ? (int?)null
                : MonthKey(entry.EffectiveToProcessingMonth);
            if (to is not null && to < from)
            {
                throw new InvalidDataException(
                    $"CSV specification version '{entry.Version}' ends before it begins.");
            }

            var isLast = index == entries.Length - 1;
            if (isLast != (to is null))
            {
                throw new InvalidDataException(
                    "Only the newest CSV specification version may be open ended, and it must be.");
            }

            if (isLast) continue;

            // 次版は「前版の終了月の翌月」から始まること（重複も欠落も許さない）。
            var next = MonthKey(entries[index + 1].EffectiveFromProcessingMonth);
            if (next != NextMonthKey(to!.Value))
            {
                throw new InvalidDataException(
                    $"CSV specification versions '{entry.Version}' and '{entries[index + 1].Version}' "
                    + "must be contiguous with no overlap.");
            }
        }

        return entries;
    }

    private static string Describe(CsvSpecificationVersionEntry entry) =>
        $"{entry.Version}({entry.EffectiveFromProcessingMonth}〜{entry.EffectiveToProcessingMonth ?? "現行"})";

    private static int MonthKey(string yearMonth)
    {
        var parts = yearMonth.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || month is < 1 or > 12)
        {
            throw new InvalidDataException(
                $"CSV specification version period '{yearMonth}' must be formatted as yyyy-MM.");
        }

        return MonthKey(year, month);
    }

    private static int MonthKey(int year, int month) => (year * 12) + (month - 1);

    private static int NextMonthKey(int monthKey) => monthKey + 1;
}

/// <param name="Version">仕様版の識別子（各仕様ファイルの版接尾辞と一致させる）。</param>
/// <param name="Label">人が読む施行分の名前。</param>
/// <param name="EffectiveFromProcessingMonth">適用開始の処理対象年月（yyyy-MM）。</param>
/// <param name="EffectiveToProcessingMonth">適用終了の処理対象年月（yyyy-MM）。現行版は null。</param>
/// <param name="ApplicabilityNote">期間をそう決めた理由（次版で何が改訂されたか等）。</param>
public sealed record CsvSpecificationVersionEntry(
    string Version,
    string Label,
    string EffectiveFromProcessingMonth,
    string? EffectiveToProcessingMonth,
    IReadOnlyList<CsvSpecSourceRef> SourceRefs,
    string ApplicabilityNote);

public sealed record CsvSpecificationVersionFile(
    int SchemaVersion,
    string SpecificationVersion,
    IReadOnlyList<CsvSpecificationVersionEntry> Versions);
