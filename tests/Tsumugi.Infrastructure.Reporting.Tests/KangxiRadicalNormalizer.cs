using System.Collections.Generic;
using System.Text;

namespace Tsumugi.Infrastructure.Reporting.Tests;

/// <summary>
/// Noto Sans JP は「山」「田」「工」「支」「一」等、康熙部首 (Kangxi Radicals, U+2F00-2FD5) と字形を
/// 共有する漢字を含んでおり、QuestPDF/SkiaSharp が生成する ToUnicode CMap がその部首の
/// コードポイントを採用することがある (レンダリングされる字形自体は正しい。抽出テキストの
/// みに影響。pdftotext でも同一結果になることを確認済みのため QuestPDF/Skia 側の生成物起因で
/// あり PdfPig 固有ではない)。globalization有効下ではstring.Normalize(FormKC)も利用可能だが、
/// 抽出テキスト内の他の互換文字まで変換しないよう、テストで実際に出現する部首だけを
/// 固定表で統合漢字へ畳み込む。
///
/// <see cref="WageStatementPdfGeneratorTests"/> / <see cref="WagePaymentListPdfGeneratorTests"/> の
/// 両テストで重複していた fold ヘルパーを Task 9.6 (final review M-3) でここへ集約。
/// 表は全テストで実際に出現した部首の合併集合（phase3-2/task 10 で
/// <see cref="ClaimReportGeneratorServiceProvisionRecordTests"/> 用に月/用/日/欠/食/入/力を追加）。
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
        ['⽉'] = '月', // KANGXI RADICAL MOON -> 月
        ['⽤'] = '用', // KANGXI RADICAL USE -> 用
        ['⽇'] = '日', // KANGXI RADICAL SUN -> 日
        ['⽋'] = '欠', // KANGXI RADICAL LACK -> 欠
        ['⾷'] = '食', // KANGXI RADICAL EAT -> 食
        ['⼊'] = '入', // KANGXI RADICAL ENTER -> 入
        ['⼒'] = '力', // KANGXI RADICAL POWER -> 力
        ['‧'] = '・', // HYPHENATION POINT (U+2027) -> KATAKANA MIDDLE DOT (U+30FB)
                     // phase3-2/task 11: 「介護給付費・訓練等給付費等請求書」の中点がこの字形で抽出される。
    };

    public static string FoldKangxiRadicals(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(KangxiRadicalToIdeograph.TryGetValue(ch, out var mapped) ? mapped : ch);
        return sb.ToString();
    }
}
