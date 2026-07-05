using System.Collections.Generic;
using System.Text;

namespace Tsumugi.Infrastructure.Reporting.Tests;

/// <summary>
/// Noto Sans JP は「山」「田」「工」「支」「一」等、康熙部首 (Kangxi Radicals, U+2F00-2FD5) と字形を
/// 共有する漢字を含んでおり、QuestPDF/SkiaSharp が生成する ToUnicode CMap がその部首の
/// コードポイントを採用することがある (レンダリングされる字形自体は正しい。抽出テキストの
/// みに影響。pdftotext でも同一結果になることを確認済みのため QuestPDF/Skia 側の生成物起因で
/// あり PdfPig 固有ではない)。Directory.Build.props で InvariantGlobalization=true のため
/// string.Normalize(FormKC) は ICU 不使用で分解を行わず使えない。テストで実際に出現する
/// 部首だけを固定表で統合漢字へ畳み込む。
///
/// <see cref="WageStatementPdfGeneratorTests"/> / <see cref="WagePaymentListPdfGeneratorTests"/> の
/// 両テストで重複していた fold ヘルパーを Task 9.6 (final review M-3) でここへ集約。
/// 表は両テストで実際に出現した部首の合併集合。
/// </summary>
internal static class KangxiRadicalNormalizer
{
    private static readonly Dictionary<char, char> KangxiRadicalToIdeograph = new()
    {
        ['⼭'] = '山', // KANGXI RADICAL MOUNTAIN -> 山
        ['⽥'] = '田', // KANGXI RADICAL FIELD -> 田
        ['⼯'] = '工', // KANGXI RADICAL WORK -> 工
        ['⽀'] = '支', // KANGXI RADICAL BRANCH -> 支
        ['⼀'] = '一', // KANGXI RADICAL ONE -> 一
    };

    public static string FoldKangxiRadicals(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(KangxiRadicalToIdeograph.TryGetValue(ch, out var mapped) ? mapped : ch);
        return sb.ToString();
    }
}
