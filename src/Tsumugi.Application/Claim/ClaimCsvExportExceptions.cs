namespace Tsumugi.Application.Claim;

/// <summary>確定済みの実効 <c>ClaimBatch</c> が無い状態で CSV 出力を要求した。</summary>
public sealed class ClaimBatchNotFinalizedException : Exception
{
    public ClaimBatchNotFinalizedException(Guid officeId, string serviceMonth)
        : base($"確定済みの請求が見つかりません（事業所 {officeId:N} / {serviceMonth}）。先に確定してください。")
    {
        OfficeId = officeId;
        ServiceMonth = serviceMonth;
    }

    public ClaimBatchNotFinalizedException()
        : base("確定済みの請求が見つかりません。")
    {
        ServiceMonth = string.Empty;
    }

    public ClaimBatchNotFinalizedException(string message)
        : base(message) => ServiceMonth = string.Empty;

    public ClaimBatchNotFinalizedException(string message, Exception innerException)
        : base(message, innerException) => ServiceMonth = string.Empty;

    public Guid OfficeId { get; }
    public string ServiceMonth { get; } = string.Empty;
}

/// <summary>
/// CSV 生成の fail-close。UI へ出せるのは項目 ID と理由と内部参照コードだけで、
/// 氏名・受給者証番号は含めない（CLAUDE.md §ハード制約4）。
/// </summary>
public sealed class ClaimCsvExportFailedException : Exception
{
    public ClaimCsvExportFailedException(
        string fieldId,
        string reason,
        string detail,
        string? recipientReferenceCode = null)
        : base($"CSV出力に失敗しました（項目 {fieldId} / 理由 {reason}）。")
    {
        FieldId = fieldId;
        Reason = reason;
        Detail = detail;
        RecipientReferenceCode = recipientReferenceCode;
    }

    public ClaimCsvExportFailedException()
        : base("CSV出力に失敗しました。")
    {
        FieldId = string.Empty;
        Reason = string.Empty;
        Detail = string.Empty;
    }

    public ClaimCsvExportFailedException(string message)
        : base(message)
    {
        FieldId = string.Empty;
        Reason = string.Empty;
        Detail = string.Empty;
    }

    public ClaimCsvExportFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
        FieldId = string.Empty;
        Reason = string.Empty;
        Detail = string.Empty;
    }

    /// <summary>失敗した CSV 項目 ID（例: <c>provider:J121:01:008</c>）。</summary>
    public string FieldId { get; } = string.Empty;

    /// <summary>失敗理由の機械可読トークン（例: <c>OverByteWidth</c>）。</summary>
    public string Reason { get; } = string.Empty;

    /// <summary>構造情報のみの詳細。値そのものは載せない。</summary>
    public string Detail { get; } = string.Empty;

    /// <summary>受給者を指す内部参照コード（受給者証番号ではない）。</summary>
    public string? RecipientReferenceCode { get; }
}
