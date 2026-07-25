using System.Text;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Writer;

/// <summary>
/// spec JSON（<see cref="CsvFieldSpecification"/>）駆動で 1 セル / 1 レコードのバイト列を作る。
/// 引用規則・バイト幅・CP932 変換・コード値の判定をここに一元化し、値の解釈は builder 側に置かない。
/// </summary>
/// <remarks>
/// <para>
/// <b>バイト幅の意味</b>: <see cref="CsvFieldSpecification.MaxBytes"/> は「引用符を除いた内容の
/// CP932 バイト数」の上限として扱う。根拠は spec JSON 内の一致で、
/// <c>provider:J611:01</c> の sum(maxBytes) + 区切りカンマ数 = 822 が
/// <c>common:outer:data:003</c>（データ、maxBytes=822）と完全に一致すること。
/// 引用符を内容長に含める解釈ではこの一致が成立しない。
/// </para>
/// <para>
/// <b>引用規則</b>: spec の文言「quote when comma, double quote, space, or kanji is present;
/// double embedded double quote」を literal に実装する（カンマ / 二重引用符 / 空白 / 漢字）。
/// 全角カナ・全角記号のみの値を引用するかは公式資料から一意に確定できないため引用しない側に寄せ、
/// 未確定事項として <c>docs/open-questions.md</c> に起票している。
/// </para>
/// </remarks>
public static class CsvCellEncoder
{
    /// <summary>spec JSON が使う条件付き引用規則の文言（正本と一字一句一致させる）。</summary>
    public const string ConditionalQuoteRule =
        "quote when comma, double quote, space, or kanji is present; double embedded double quote";

    /// <summary>外側レコードの末尾「ブランク」項目＝行終端 CRLF を表す規則。</summary>
    public const string CrlfQuoteRule = "crlf";

    private const byte Comma = (byte)',';
    private const byte Quote = (byte)'"';
    private const int Cp932CodePage = 932;

    private static readonly byte[] CrlfBytes = [(byte)'\r', (byte)'\n'];

    static CsvCellEncoder()
    {
        // CP932 は .NET 既定のエンコーディングに含まれないため、明示的に登録する（macOS/Windows 共通）。
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp932 = Encoding.GetEncoding(
            Cp932CodePage,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    /// <summary>CP932（fallback は例外）。呼び出し側でも decode 検証に使う。</summary>
    public static Encoding Cp932 { get; }

    /// <summary>1 セルをバイト列にする。</summary>
    public static ReadOnlyMemory<byte> EncodeCell(CsvCell cell, CsvFieldSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        if (!string.Equals(cell.FieldId, specification.FieldId, StringComparison.Ordinal))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.FieldIdMismatch,
                $"cell field id '{cell.FieldId}' does not match the specification");
        }

        var raw = cell.Raw ?? string.Empty;

        switch (specification.QuoteRule)
        {
            case CrlfQuoteRule:
                if (raw.Length != 0)
                {
                    throw Fail(
                        specification.FieldId,
                        CsvEncodingReason.CrlfFieldMustBeEmpty,
                        "a line terminator field must carry no value");
                }

                return CrlfBytes;
            case ConditionalQuoteRule:
                break;
            default:
                throw Fail(
                    specification.FieldId,
                    CsvEncodingReason.UnknownQuoteRule,
                    "the quote rule is not known to the encoder");
        }

        if (raw.Contains('\0', StringComparison.Ordinal))
        {
            throw Fail(specification.FieldId, CsvEncodingReason.NulCharacter, "the value contains NUL");
        }

        if (raw.Contains('\r', StringComparison.Ordinal) || raw.Contains('\n', StringComparison.Ordinal))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.LineBreakInValue,
                "the value contains CR or LF");
        }

        if (raw.Length == 0)
        {
            return string.Equals(specification.RequiredWhen, "always", StringComparison.Ordinal)
                ? throw Fail(
                    specification.FieldId,
                    CsvEncodingReason.MissingRequired,
                    "the field is always required but the value is empty")
                : ReadOnlyMemory<byte>.Empty;
        }

        if (specification.AllowedCodes.Count > 0
            && !specification.AllowedCodes.Contains(raw, StringComparer.Ordinal))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.UnknownCode,
                $"the value is not one of the {specification.AllowedCodes.Count} allowed codes");
        }

        var content = EncodeContent(specification, raw);
        if (content.Length > specification.MaxBytes)
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.OverByteWidth,
                $"content byte length {content.Length} exceeds max {specification.MaxBytes}");
        }

        if (!RequiresQuoting(raw)) return content;

        // 二重引用符を含む値はエスケープで内容が伸びる。伸びた後の内容も幅上限に収める。
        var escaped = EncodeContent(specification, raw.Replace("\"", "\"\"", StringComparison.Ordinal));
        if (escaped.Length > specification.MaxBytes)
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.OverByteWidth,
                $"escaped content byte length {escaped.Length} exceeds max {specification.MaxBytes}");
        }

        var quoted = new byte[escaped.Length + 2];
        quoted[0] = Quote;
        escaped.CopyTo(quoted.AsSpan(1));
        quoted[^1] = Quote;
        return quoted;
    }

    /// <summary>
    /// 1 レコード分のセルを spec の項目順で連結する。末尾が <see cref="CrlfQuoteRule"/> の項目なら
    /// その直前にカンマを置かず、行終端として CRLF を書く。
    /// </summary>
    public static ReadOnlyMemory<byte> EncodeFields(
        IReadOnlyList<CsvCell> cells,
        IReadOnlyList<CsvFieldSpecification> specifications)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(specifications);

        if (cells.Count != specifications.Count)
        {
            throw Fail(
                specifications.Count > 0 ? specifications[0].FieldId : string.Empty,
                CsvEncodingReason.FieldCountMismatch,
                $"cell count {cells.Count} does not match specification count {specifications.Count}");
        }

        using var buffer = new MemoryStream();
        for (var index = 0; index < cells.Count; index++)
        {
            var specification = specifications[index];
            var isTerminator = string.Equals(specification.QuoteRule, CrlfQuoteRule, StringComparison.Ordinal);
            if (index > 0 && !isTerminator) buffer.WriteByte(Comma);
            buffer.Write(EncodeCell(cells[index], specification).Span);
        }

        return buffer.ToArray();
    }

    private static byte[] EncodeContent(CsvFieldSpecification specification, string value)
    {
        try
        {
            return Cp932.GetBytes(value);
        }
        catch (EncoderFallbackException)
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.NonRepresentableCharacter,
                "the value contains a character that CP932 cannot represent");
        }
    }

    /// <summary>
    /// 「カンマ / 二重引用符 / 空白 / 漢字」のいずれかを含むなら引用する。
    /// 空白は半角(U+0020)と全角(U+3000)の両方を対象にする。
    /// </summary>
    private static bool RequiresQuoting(string value)
    {
        foreach (var character in value)
        {
            if (character is ',' or '"' or ' ' or '　') return true;
            if (IsKanji(character)) return true;
        }

        return false;
    }

    /// <summary>
    /// 漢字判定。表意文字ブロックと、氏名で漢字として扱われる繰り返し記号（々〆〇）を対象にする。
    /// CP932 で表現できない拡張ブロックは <see cref="EncodeContent"/> 側で fail-close する。
    /// </summary>
    private static bool IsKanji(char character) => character
        is (>= '々' and <= '〇') // 々 〆 〇
        or (>= '㐀' and <= '䶿') // CJK 統合漢字 拡張A
        or (>= '一' and <= '鿿') // CJK 統合漢字
        or (>= '豈' and <= '﫿'); // CJK 互換漢字

    private static CsvEncodingException Fail(string fieldId, CsvEncodingReason reason, string detail) =>
        new(fieldId, reason, detail);
}
