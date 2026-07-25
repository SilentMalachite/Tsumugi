namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>CSV 生成が fail-close した理由。</summary>
public enum ClaimCsvGenerationReason
{
    /// <summary>マッピングの status が未知。</summary>
    UnknownMappingStatus = 1,

    /// <summary>generatorRule の意味論が現行の入力（snapshot v2 + 制度マスタ）から確定できない。</summary>
    UnresolvableRule = 2,

    /// <summary>modelPath / targetProperty が現行の入力に存在しない。</summary>
    UnresolvableModelPath = 3,

    /// <summary>参照先フィールドが解決できない（行スコープ不一致 or 未定義）。</summary>
    UnresolvableFieldReference = 4,

    /// <summary>フィールド参照が循環している。</summary>
    CircularFieldReference = 5,

    /// <summary>値の型が対象フィールドの dataType に写像できない。</summary>
    UnsupportedDataType = 6,

    /// <summary>DTO に必要な行（受給者・明細行・日次記録）が存在しない。</summary>
    MissingRow = 7,
}

/// <summary>
/// CSV 生成の fail-close 例外。氏名・受給者証番号は載せない（CLAUDE.md §ハード制約4）。
/// 受給者の識別が要るときは <see cref="RecipientReferenceCode"/>（内部参照コード）だけを持たせる。
/// </summary>
public sealed class ClaimCsvGenerationException : Exception
{
    public ClaimCsvGenerationException(
        string fieldId,
        ClaimCsvGenerationReason reason,
        string detail,
        string? recipientReferenceCode = null)
        : base($"CSV generation failed: field={fieldId}, reason={reason}, detail={detail}")
    {
        FieldId = fieldId;
        Reason = reason;
        Detail = detail;
        RecipientReferenceCode = recipientReferenceCode;
    }

    public ClaimCsvGenerationException()
        : this(string.Empty, ClaimCsvGenerationReason.UnresolvableRule, "unspecified")
    {
    }

    public ClaimCsvGenerationException(string message)
        : base(message)
    {
        FieldId = string.Empty;
        Reason = ClaimCsvGenerationReason.UnresolvableRule;
        Detail = message;
    }

    public ClaimCsvGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
        FieldId = string.Empty;
        Reason = ClaimCsvGenerationReason.UnresolvableRule;
        Detail = message;
    }

    public string FieldId { get; } = string.Empty;
    public ClaimCsvGenerationReason Reason { get; } = ClaimCsvGenerationReason.UnresolvableRule;
    public string Detail { get; } = string.Empty;

    /// <summary>受給者を指す内部参照コード（受給者証番号ではない）。</summary>
    public string? RecipientReferenceCode { get; }
}
