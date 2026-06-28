using System.Globalization;

namespace Tsumugi.App.Formatting;

/// <summary>
/// 整数円表示の純粋ヘルパ。CultureInfo.InvariantCulture を使い OS/ロケール差で揺れない（CultureExplicitnessGuard 順守）。
/// 金額はハード制約により整数円のみ扱う（CLAUDE.md）。
/// </summary>
public static class YenFormatter
{
    public static string Format(int yen) =>
        $"{yen.ToString("N0", CultureInfo.InvariantCulture)} 円";
}
