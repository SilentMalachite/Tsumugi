namespace Tsumugi.Infrastructure.Csv.Writer;

/// <summary>CSV セル/レコードのエンコード失敗理由。すべて fail-close（部分出力を残さない）。</summary>
public enum CsvEncodingReason
{
    /// <summary>CP932 変換後のバイト数が maxBytes を超えた。</summary>
    OverByteWidth = 1,

    /// <summary>CP932 で表現できない文字が含まれる。</summary>
    NonRepresentableCharacter = 2,

    /// <summary>NUL 文字が含まれる。</summary>
    NulCharacter = 3,

    /// <summary>requiredWhen=always の項目が空。</summary>
    MissingRequired = 4,

    /// <summary>allowedCodes に無い値。</summary>
    UnknownCode = 5,

    /// <summary>spec の quoteRule が encoder の既知集合に無い。</summary>
    UnknownQuoteRule = 6,

    /// <summary>セルの fieldId が spec の fieldId と一致しない。</summary>
    FieldIdMismatch = 7,

    /// <summary>値に CR / LF が含まれる（行構造を壊す）。</summary>
    LineBreakInValue = 8,

    /// <summary>quoteRule=crlf の項目（行終端）に値が入っている。</summary>
    CrlfFieldMustBeEmpty = 9,

    /// <summary>セル数と spec の項目数が一致しない。</summary>
    FieldCountMismatch = 10,

    /// <summary>制御文字が含まれる。</summary>
    ControlCharacter = 11,

    /// <summary>spec の dataType が許す文字集合から外れている。</summary>
    InvalidCharacterForDataType = 12,
}

/// <summary>
/// CSV エンコード失敗。氏名・受給者証番号は含めない（CLAUDE.md §ハード制約4）。
/// <see cref="Detail"/> にも値そのものを載せず、バイト数・文字コード等の構造情報だけを載せる。
/// </summary>
public sealed class CsvEncodingException : Exception
{
    public CsvEncodingException(string fieldId, CsvEncodingReason reason, string detail)
        : base($"CSV encoding failed: field={fieldId}, reason={reason}, detail={detail}")
    {
        FieldId = fieldId;
        Reason = reason;
        Detail = detail;
    }

    public CsvEncodingException()
        : base("CSV encoding failed.")
    {
        FieldId = string.Empty;
        Reason = CsvEncodingReason.OverByteWidth;
        Detail = string.Empty;
    }

    public CsvEncodingException(string message)
        : base(message)
    {
        FieldId = string.Empty;
        Reason = CsvEncodingReason.OverByteWidth;
        Detail = string.Empty;
    }

    public CsvEncodingException(string message, Exception innerException)
        : base(message, innerException)
    {
        FieldId = string.Empty;
        Reason = CsvEncodingReason.OverByteWidth;
        Detail = string.Empty;
    }

    public string FieldId { get; }
    public CsvEncodingReason Reason { get; }
    public string Detail { get; }
}
