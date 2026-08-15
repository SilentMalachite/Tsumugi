using System.Globalization;

namespace Tsumugi.Application.Backup;

/// <summary>
/// バックアップファイル名の生成と解析。時刻に依存しない（生成時刻は引数で受け取る）。
/// 自動バックアップ: tsumugi-backup-yyyyMMdd-HHmmss.db
/// 復元前の退避:     pre-restore-yyyyMMdd-HHmmss.db（世代管理の対象外）
/// </summary>
public static class BackupFileName
{
    public const string AutomaticPrefix = "tsumugi-backup-";
    public const string PreRestorePrefix = "pre-restore-";
    public const string Extension = ".db";

    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    public static string Create(DateTimeOffset at) =>
        AutomaticPrefix + at.ToString(TimestampFormat, CultureInfo.InvariantCulture) + Extension;

    public static string CreatePreRestore(DateTimeOffset at) =>
        PreRestorePrefix + at.ToString(TimestampFormat, CultureInfo.InvariantCulture) + Extension;

    /// <summary>自動バックアップ名だけを解析する。pre-restore や規則外の名前は false。</summary>
    public static bool TryParse(string fileName, out DateTimeOffset at)
    {
        at = default;
        ArgumentNullException.ThrowIfNull(fileName);

        if (!fileName.StartsWith(AutomaticPrefix, StringComparison.Ordinal)) return false;
        if (!fileName.EndsWith(Extension, StringComparison.Ordinal)) return false;

        var stamp = fileName[AutomaticPrefix.Length..^Extension.Length];
        if (!DateTimeOffset.TryParseExact(
                stamp, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        at = parsed;
        return true;
    }
}
