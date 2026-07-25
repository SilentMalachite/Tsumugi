namespace Tsumugi.Infrastructure.Csv.Writer;

/// <summary>
/// CSV 1セルの生値。<paramref name="Raw"/> は CP932 変換前・引用符付与前の内容で、
/// 値なしは空文字で表す（null を使わない）。
/// </summary>
public readonly record struct CsvCell(string FieldId, string Raw);
