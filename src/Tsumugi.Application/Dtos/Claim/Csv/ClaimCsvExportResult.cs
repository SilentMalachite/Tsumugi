namespace Tsumugi.Application.Dtos.Claim.Csv;

/// <param name="Bytes">CP932 / CRLF の CSV 全体。</param>
/// <param name="SuggestedFileName">保存ダイアログの初期ファイル名。個人情報は含めない。</param>
/// <param name="Sha256">出力バイト列の SHA-256（64文字の小文字16進数）。</param>
public sealed record ClaimCsvExportResult(
    byte[] Bytes,
    string SuggestedFileName,
    string Sha256);
