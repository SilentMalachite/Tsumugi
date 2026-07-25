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
/// <b>引用規則</b>: 共通編 1.2.2(4)（物理6頁）は「英数属性、数値属性、コード値属性および漢字属性の
/// 項目はデータの両側をダブルコーテーションで囲む。ただし、各項目の内容に『カンマ』
/// 『ダブルコーテーション』『スペース(0x20)』および<b>漢字（2 バイトコード）</b>を含まない場合は、
/// データの両側のダブルコーテーションを省略することができる」と定める。
/// <b>「漢字」は 2 バイトコードとして定義されている</b>ため、全角カナ・全角記号も引用対象になる
/// （表意文字だけを見ると全角カナ氏名が引用されず仕様違反になる）。
/// </para>
/// <para>
/// <b>使用不可能文字</b>: 共通編 1.2.2(3)① はシングルコーテーション（0x27）を交換情報で使用不可と
/// 定める。制御文字（0x00〜0x1F）も同 (4) で禁止されている。
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
    private const char SingleQuote = '\'';
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

        // 制御文字（0x00〜0x1F）は共通編 1.2.2(4) で使用不可。DEL 等も安全側で弾く。
        if (raw.Any(char.IsControl))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.ControlCharacter,
                "the value contains a control character");
        }

        // 共通編 1.2.2(3)① の使用不可能文字。
        if (raw.Contains(SingleQuote, StringComparison.Ordinal))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.ProhibitedCharacter,
                "the value contains a single quotation mark, which the specification forbids");
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

        RequireCharactersAllowedByDataType(specification, raw);

        var content = EncodeContent(specification, raw);

        // 属性区分の検証は CP932 変換の後に置く。CP932 で表現できない文字は「属性違反」ではなく
        // 「そもそも交換情報に載せられない文字」なので、先に NonRepresentableCharacter を返す。
        RequireCharactersAllowedByOfficialAttribute(specification, raw);

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

    /// <summary>
    /// spec の <c>dataType</c> が定める文字集合を検証する。数値・年月・日付欄に記号や英字が
    /// 混じったまま出力すると、取込側で弾かれるか別の値として解釈される。
    /// </summary>
    private static void RequireCharactersAllowedByDataType(CsvFieldSpecification specification, string raw)
    {
        var digitsOnly = specification.DataType switch
        {
            "numeric" or "yearMonth" or "date" => true,
            _ => false,
        };
        if (!digitsOnly) return;

        if (!raw.All(char.IsAsciiDigit))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.InvalidCharacterForDataType,
                $"dataType '{specification.DataType}' allows ASCII digits only");
        }
    }

    /// <summary>
    /// 公式の属性区分（<c>英数</c> / <c>数値</c> / <c>コード値</c> / <c>漢字</c>）が許す文字種を強制する
    /// （共通編 1.3.2(1)③）。英数項目に全角カナ氏名を載せる・漢字項目に半角数字を混ぜる、といった
    /// 違反は取込側で弾かれるため、生成時に fail-close する。
    /// </summary>
    private static void RequireCharactersAllowedByOfficialAttribute(
        CsvFieldSpecification specification,
        string raw)
    {
        if (!CsvOfficialAttribute.Known.Contains(specification.OfficialAttribute))
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.UnknownOfficialAttribute,
                "the official attribute is not known to the encoder");
        }

        var index = CsvOfficialAttribute.IndexOfDisallowedCharacter(specification.OfficialAttribute, raw);
        if (index >= 0)
        {
            throw Fail(
                specification.FieldId,
                CsvEncodingReason.InvalidCharacterForOfficialAttribute,
                $"official attribute '{specification.OfficialAttribute}' does not allow the character "
                + $"at index {index}");
        }
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
    /// 「カンマ / 二重引用符 / スペース(0x20) / 漢字（2 バイトコード）」のいずれかを含むなら引用する
    /// （共通編 1.2.2(4)）。仕様は「漢字」を<b>2 バイトコード</b>として定義しているため、
    /// 表意文字に限らず全角カナ・全角記号・全角スペースも引用対象になる。
    /// 表意文字だけを見ると、全角カナ氏名が引用されず仕様違反の行を出してしまう。
    /// </summary>
    private static bool RequiresQuoting(string value)
    {
        foreach (var character in value)
        {
            if (character is ',' or '"' or ' ') return true;
            if (IsTwoByteInCp932(character)) return true;
        }

        return false;
    }

    /// <summary>CP932 で 2 バイトになる文字か（＝仕様のいう「漢字（2 バイトコード）」）。</summary>
    private static bool IsTwoByteInCp932(char character)
    {
        if (char.IsAscii(character)) return false;
        try
        {
            return Cp932.GetByteCount([character]) > 1;
        }
        catch (EncoderFallbackException)
        {
            // CP932 で表現できない文字は EncodeContent 側で fail-close する。
            return false;
        }
    }

    private static CsvEncodingException Fail(string fieldId, CsvEncodingReason reason, string detail) =>
        new(fieldId, reason, detail);
}
