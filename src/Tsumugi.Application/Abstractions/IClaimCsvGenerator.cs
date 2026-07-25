using Tsumugi.Application.Dtos.Claim.Csv;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 国保連請求CSVの生成抽象。<see cref="IClaimReportGenerator"/> と同じ責務境界で、
/// Application は CSV 仕様（<c>Tsumugi.Infrastructure.Csv</c>）を直接参照しない。
/// 決定論（同 DTO → 同バイト列）を実装側の契約とする。
/// </summary>
/// <param name="Bytes">CP932 / CRLF の CSV 全体。</param>
/// <param name="FileName">
/// 仕様準拠のファイル名。共通編 1.2.1 は「英字で始まる半角英数字 8 桁以内の任意の文字列に
/// 拡張子として ".CSV" を付加したもの」と定める。
/// </param>
public sealed record ClaimCsvDocument(byte[] Bytes, string FileName);

public interface IClaimCsvGenerator
{
    /// <summary>
    /// CP932 / CRLF の請求CSV全体（外側3レコード＋内側レコード群）と、仕様準拠のファイル名を返す。
    /// ファイル名の規則は CSV 仕様（共通編）に属するため生成側が組み立てる。
    /// 使用する仕様版は <see cref="ClaimCsvDto.ProcessingMonth"/> から決まる
    /// （版の解決は <see cref="IClaimCsvSpecificationVersions"/>。generator に版の property は置かない）。
    /// </summary>
    ClaimCsvDocument Generate(ClaimCsvDto dto);

    /// <summary>
    /// 生成を試みて<b>不足・不整合を全件</b>集める（例外にしない）。空なら生成できる。
    /// <see cref="Generate"/> は最初の1件で fail-close するため、「この月を出すには何が必要か」を
    /// 利用者に見せるにはこちらを使う（ADR 0040）。
    /// </summary>
    IReadOnlyList<ClaimCsvFieldIssue> CollectIssues(ClaimCsvDto dto);
}
