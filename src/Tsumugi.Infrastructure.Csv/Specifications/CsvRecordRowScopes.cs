namespace Tsumugi.Infrastructure.Csv.Specifications;

/// <summary>内側レコード1行が発生するスコープ。</summary>
internal enum CsvRecordRowScope
{
    /// <summary>ファイル全体で1行（請求書 総括・集計）。</summary>
    File = 1,

    /// <summary>受給者×サービス提供年月で1行。</summary>
    Recipient = 2,

    /// <summary>サービス明細（サービスコード）ごとに1行。</summary>
    ServiceLine = 3,

    /// <summary>サービス提供日ごとに1行。</summary>
    DailyRecord = 4,
}

/// <summary>
/// レコードの行スコープ表。<b>行の発生計画（<c>ClaimCsvRowPlanner</c>）と、
/// 汎用 pass-through 入力（ADR 0042）が宣言できる範囲の判定が同じ表を引く</b>。
/// 2 か所に書くと、片方だけ新レコードを足して他方が黙って別スコープ扱いにするドリフトが起きる。
/// </summary>
internal static class CsvRecordRowScopes
{
    private static readonly Dictionary<string, CsvRecordRowScope> ByRecordId =
        new(StringComparer.Ordinal)
        {
            // 就労継続支援B型のみを scope とするため、給付種別×サービス種類は単一グループ。
            ["provider:J111:01"] = CsvRecordRowScope.File,
            ["provider:J111:02"] = CsvRecordRowScope.File,
            ["provider:J121:01"] = CsvRecordRowScope.Recipient,
            ["provider:J121:02"] = CsvRecordRowScope.Recipient,
            ["provider:J121:04"] = CsvRecordRowScope.Recipient,
            // provider:J121:05 は「契約情報」レコード。受給者ごとに1行必須であり、省略できない。
            ["provider:J121:05"] = CsvRecordRowScope.Recipient,
            ["provider:J611:01"] = CsvRecordRowScope.Recipient,
            ["provider:J121:03"] = CsvRecordRowScope.ServiceLine,
            ["provider:J611:02"] = CsvRecordRowScope.DailyRecord,
        };

    /// <summary>行スコープ。未知のレコードは <c>null</c>（呼び出し側が fail-close する）。</summary>
    internal static CsvRecordRowScope? Of(string recordId) =>
        ByRecordId.TryGetValue(recordId, out var scope) ? scope : null;

    /// <summary>項目 ID（<c>edition:record:inner:item</c>）が属するレコード ID。</summary>
    internal static string RecordIdOf(string fieldId)
    {
        var parts = fieldId.Split(':');
        return parts.Length >= 3 ? string.Join(':', parts[..3]) : fieldId;
    }
}
