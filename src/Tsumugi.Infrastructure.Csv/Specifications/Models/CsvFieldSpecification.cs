namespace Tsumugi.Infrastructure.Csv.Specifications;

/// <param name="OfficialAttribute">
/// 公式の属性区分（<c>英数</c> / <c>数値</c> / <c>コード値</c> / <c>漢字</c>）。共通編 1.3.2(1)③ は
/// 区分ごとに使える文字種を定めるため、<see cref="Writer.CsvCellEncoder"/> が強制するには
/// 項目定義が区分を運ぶ必要がある。値は ADR 0037 の機械抽出（<c>*-item-tables.json</c>）と
/// 完全一致し、<c>ItemTableCrossCheckTests</c> が固定する。公式表が属性欄を空にしている項目
/// （データレコードの可変長ペイロード）だけ空文字になる。
/// </param>
public sealed record CsvFieldSpecification(
    string FieldId,
    int Position,
    string OfficialName,
    string RequiredWhen,
    string OfficialAttribute,
    string DataType,
    int MaxBytes,
    string QuoteRule,
    IReadOnlyList<string> AllowedCodes,
    int SourcePage,
    string RequiredWhenSource,
    string? AllowedCodesSource = null);
