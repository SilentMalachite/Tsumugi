namespace Tsumugi.Infrastructure.Csv.Specifications;

/// <param name="SourceTableCaption">
/// 一次資料でこの表を指す見出し（例「・コントロールレコードフォーマット」）。
/// <b>1 頁に複数の表が並ぶ頁</b>だけ必要（共通編のレコードフォーマットは 3 表が同じ頁にある）。
/// 項目表の機械抽出（ADR 0037）が対象表を選ぶために使う。
/// </param>
public sealed record CsvRecordSpecification(
    string RecordId,
    string ExchangeInformationId,
    string InnerRecordType,
    int Order,
    string SourceDocumentId,
    int SourcePage,
    IReadOnlyList<CsvFieldSpecification> Fields,
    string? SourceTableCaption = null);
