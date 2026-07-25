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
    /// <summary>生成に使う CSV 仕様の版。確定済み請求が記録した版との一致確認に使う。</summary>
    string SpecificationVersion { get; }

    /// <summary>
    /// CP932 / CRLF の請求CSV全体（外側3レコード＋内側レコード群）と、仕様準拠のファイル名を返す。
    /// ファイル名の規則は CSV 仕様（共通編）に属するため生成側が組み立てる。
    /// </summary>
    ClaimCsvDocument Generate(ClaimCsvDto dto);
}
