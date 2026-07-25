using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// CSV 仕様版の解決。施行分ごとに更新される仕様を並存させ、確定時に記録する版と出力時に使う版を
/// それぞれ与える。実装は CSV 仕様データを所有する層に置く（版文字列を Application に持たない）。
/// </summary>
public interface IClaimCsvSpecificationVersions
{
    /// <summary>現行版。請求確定時に記録する（readiness を検証したのはこの版）。</summary>
    string Current { get; }

    /// <summary>
    /// 処理対象年月に適用される版。該当版が無ければ例外（推測で現行版を使わない）。
    /// </summary>
    string ResolveForProcessingMonth(ProcessingMonth processingMonth);

    /// <summary>
    /// 適用開始前の登録済み版（事前登録した将来の施行分）。確定前に「次の施行分で必要になる項目」を
    /// 警告するために使う。空なら将来版は未登録。
    /// </summary>
    IReadOnlyList<string> UpcomingVersions { get; }
}
