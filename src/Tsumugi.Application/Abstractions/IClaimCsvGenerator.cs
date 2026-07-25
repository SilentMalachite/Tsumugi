using Tsumugi.Application.Dtos.Claim.Csv;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 国保連請求CSVの生成抽象。<see cref="IClaimReportGenerator"/> と同じ責務境界で、
/// Application は CSV 仕様（<c>Tsumugi.Infrastructure.Csv</c>）を直接参照しない。
/// 決定論（同 DTO → 同バイト列）を実装側の契約とする。
/// </summary>
public interface IClaimCsvGenerator
{
    /// <summary>CP932 / CRLF の請求CSV全体（外側3レコード＋内側レコード群）を返す。</summary>
    byte[] Generate(ClaimCsvDto dto);
}
