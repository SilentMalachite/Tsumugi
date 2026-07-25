namespace Tsumugi.Application.Dtos.Claim.Csv;

/// <summary>
/// 確定済み請求を、処理対象年月に適用される仕様版で出力できるかの判定結果（ADR 0040）。
/// </summary>
/// <param name="FinalizedVersion">確定時に記録されていた仕様版。</param>
/// <param name="ResolvedVersion">処理対象年月に適用される仕様版。</param>
/// <param name="Issues">
/// 不足・不整合の一覧。空なら出力できる。<see cref="FinalizedVersion"/> と
/// <see cref="ResolvedVersion"/> が異なり、かつここが空でない場合は「新版が要求する項目が
/// 確定 snapshot に無い」ことを意味し、入力してから再確定する必要がある。
/// </param>
public sealed record ClaimCsvExportValidationResult(
    string FinalizedVersion,
    string ResolvedVersion,
    IReadOnlyList<ClaimCsvFieldIssue> Issues)
{
    public bool CanExport => Issues.Count == 0;

    public bool UsesNewerVersionThanFinalized =>
        !string.Equals(FinalizedVersion, ResolvedVersion, StringComparison.Ordinal);
}

/// <param name="FieldId">仕様上の項目 ID（どの欄が足りないか）。</param>
/// <param name="Reason">失敗の理由コード（encoder / generator の理由をそのまま運ぶ）。</param>
/// <param name="Detail">補足。氏名・受給者証番号は含めない。</param>
/// <param name="RecipientReferenceCode">どの受給者行かを示す内部参照コード（氏名等は含めない）。</param>
public sealed record ClaimCsvFieldIssue(
    string FieldId,
    string Reason,
    string Detail,
    string? RecipientReferenceCode = null);
