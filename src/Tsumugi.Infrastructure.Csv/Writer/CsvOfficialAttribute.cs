namespace Tsumugi.Infrastructure.Csv.Writer;

/// <summary>
/// 公式の属性区分（共通編 1.3.2(1)③、物理 10〜11 頁）と、区分ごとに使える文字種。
/// </summary>
/// <remarks>
/// <para>条文（<c>common-r7-10</c> 物理 10 頁）は「<b>特に記載が無い限り</b>、以下の形式でデータを
/// 設定することを表す」と前置きしたうえで各区分を定める。したがって<b>項目の内容欄に個別の指示が
/// ある項目は、その指示が優先する</b>（例: コントロールレコードの市町村番号は「市町村以外の場合は
/// 0 を設定する」ため、コード値属性の「全桁 0 は未設定」という一般規則を当てない）。</para>
/// <para>各区分の定義:</para>
/// <list type="bullet">
/// <item><description><b>英数</b>: 「半角の英字、数字、カナ文字１文字をそれぞれ 1 バイトで表す。
/// 半角の英小文字は使用できない」／物理 11 頁「「英数」項目には漢字（2 バイトコード）を混在させない」。
/// 同 1.2.2(4) のデータ設定例は英数項目にスペースと二重引用符を含む値を示すため、この 2 文字は許す。</description></item>
/// <item><description><b>数値</b>／<b>コード値</b>: 「0，1，2，～，9 の数字 1 桁をそれぞれ 1 バイトで表す」。</description></item>
/// <item><description><b>漢字</b>: 「漢字 1 文字をそれぞれ 2 バイトで表す」／物理 11 頁
/// 「「漢字」項目には半角の英字、数字、カナ文字（1 バイトコード）を混在させない」。</description></item>
/// </list>
/// <para>数値属性は仕様上マイナス符号を許す（同③ ※2）が、どの項目が負値を取り得るかは項目単位で
/// 宣言されておらず、本アプリの生成器は負値を出さない。許容範囲を広げずに数字のみとし、
/// 負値が来たら fail-close する。</para>
/// </remarks>
internal static class CsvOfficialAttribute
{
    /// <summary>半角の英字・数字・カナ文字（1 バイト）。半角英小文字は使用不可。</summary>
    internal const string AlphaNumeric = "英数";

    /// <summary>0〜9 の数字（1 バイト）。</summary>
    internal const string Numeric = "数値";

    /// <summary>0〜9 の数字（1 バイト）。全桁 0 は未設定として扱われる（項目側の指示が優先）。</summary>
    internal const string CodeValue = "コード値";

    /// <summary>漢字（2 バイト）。半角の英字・数字・カナ文字を混在させない。</summary>
    internal const string Kanji = "漢字";

    /// <summary>
    /// 公式表が属性欄を空にしている項目（データレコードの可変長ペイロード）。文字種規則を持たない。
    /// </summary>
    internal const string Unspecified = "";

    private const char HalfWidthKanaFirst = '｡';
    private const char HalfWidthKanaLast = 'ﾟ';

    /// <summary>spec が宣言できる属性区分の閉じた集合。</summary>
    internal static IReadOnlySet<string> Known { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AlphaNumeric,
            Numeric,
            CodeValue,
            Kanji,
            Unspecified,
        };

    /// <summary>属性区分が許す文字集合から外れた最初の文字の位置。無ければ -1。</summary>
    /// <remarks>返すのは位置だけ。値そのものは例外に載せない（CLAUDE.md §ハード制約4）。</remarks>
    internal static int IndexOfDisallowedCharacter(string officialAttribute, string value) =>
        officialAttribute switch
        {
            Unspecified => -1,
            Numeric or CodeValue => IndexWhere(value, character => !char.IsAsciiDigit(character)),
            AlphaNumeric => IndexWhere(value, character => !IsAllowedInAlphaNumeric(character)),
            Kanji => IndexWhere(value, IsSingleByteLetterDigitOrKana),
            _ => throw new ArgumentOutOfRangeException(nameof(officialAttribute)),
        };

    private static int IndexWhere(string value, Func<char, bool> predicate)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (predicate(value[index])) return index;
        }

        return -1;
    }

    /// <summary>英数属性が許す文字（半角英大文字・数字・半角カナ、＋設定例が示すスペースと二重引用符）。</summary>
    private static bool IsAllowedInAlphaNumeric(char character) =>
        char.IsAsciiDigit(character)
        || char.IsAsciiLetterUpper(character)
        || character is ' ' or '"'
        || IsHalfWidthKana(character);

    /// <summary>漢字属性に混在させてはならない 1 バイトコード（半角の英字・数字・カナ文字）。</summary>
    private static bool IsSingleByteLetterDigitOrKana(char character) =>
        char.IsAsciiLetter(character)
        || char.IsAsciiDigit(character)
        || IsHalfWidthKana(character);

    private static bool IsHalfWidthKana(char character) =>
        character is >= HalfWidthKanaFirst and <= HalfWidthKanaLast;
}
