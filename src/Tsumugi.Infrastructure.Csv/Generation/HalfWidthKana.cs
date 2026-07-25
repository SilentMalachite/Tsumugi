using System.Text;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// 全角のカナ・英数字を半角へ写す。公式の「英数」属性は 1 バイト文字だけを許す
/// （共通編 1.3.2(1)③「半角の英字、数字、カナ文字１文字をそれぞれ 1 バイトで表す」／
/// 「「英数」項目には漢字（2 バイトコード）を混在させない」）ため、氏名カナのように全角で
/// 入力されうる値は出力前にここで半角へ写す。
/// </summary>
/// <remarks>
/// <para>
/// <b>写像表を持たない</b>。半角側の文字（ASCII 0x20〜0x7E と半角カナ U+FF61〜U+FF9F）それぞれの
/// 互換分解（NFKD）を鍵にした逆引き表を実行時に組み、入力を NFKD 正規化して 1 文字ずつ引く。
/// 濁音・半濁音は NFKD で「基底＋結合文字」に分かれるため、<c>ガ</c> は <c>ｶ</c>＋<c>ﾞ</c> の 2 文字へ写る。
/// 正規化は Unicode が定める安定した写像なので、macOS / Windows で同じ結果になる（ハード制約6）。
/// </para>
/// <para>
/// 引けない文字（ひらがな・漢字・康熙部首など）は<b>変換せずに失敗を返す</b>。ひらがなをカタカナへ
/// 寄せる・別字へ丸めるといった推測をすると、記録した氏名と異なる値を請求に載せてしまう。
/// </para>
/// </remarks>
internal static class HalfWidthKana
{
    private const char AsciiFirst = ' ';
    private const char AsciiLast = '~';
    private const char HalfWidthKanaFirst = '｡';
    private const char HalfWidthKanaLast = 'ﾟ';

    private static readonly Dictionary<char, char> HalfWidthByDecomposedCharacter = Build();

    /// <summary>
    /// 半角へ写せたら <see langword="true"/>。写せない文字が 1 つでもあれば <see langword="false"/>
    /// を返し、部分的な変換結果は返さない。
    /// </summary>
    internal static bool TryNarrow(string value, out string narrowed)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormKD))
        {
            if (!HalfWidthByDecomposedCharacter.TryGetValue(character, out var halfWidth))
            {
                narrowed = string.Empty;
                return false;
            }

            builder.Append(halfWidth);
        }

        narrowed = builder.ToString();
        return true;
    }

    private static Dictionary<char, char> Build()
    {
        var map = new Dictionary<char, char>();
        foreach (var halfWidth in HalfWidthCharacters())
        {
            // 半角 1 文字の互換分解は 1 文字（ｱ → ア、ﾞ → U+3099、'A' → 'A'）。
            // 2 文字以上へ分解される文字は逆引きの鍵にならないので採らない。
            var decomposed = halfWidth.ToString().Normalize(NormalizationForm.FormKD);
            if (decomposed.Length != 1) continue;

            // ASCII を先に登録するため、衝突時は ASCII 側が残る（TryAdd）。
            map.TryAdd(decomposed[0], halfWidth);
        }

        return map;
    }

    private static IEnumerable<char> HalfWidthCharacters()
    {
        for (var character = AsciiFirst; character <= AsciiLast; character++)
        {
            yield return character;
        }

        for (var character = HalfWidthKanaFirst; character <= HalfWidthKanaLast; character++)
        {
            yield return character;
        }
    }
}
