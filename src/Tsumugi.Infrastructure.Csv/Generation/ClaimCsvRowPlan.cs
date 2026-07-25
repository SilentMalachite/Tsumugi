using Tsumugi.Application.Dtos.Claim.Csv;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// 内側レコード1行分の生成計画。<see cref="RowKey"/> は行スコープの階層を表す文字列で、
/// 「祖先の RowKey が子孫の RowKey の接頭辞になる」不変条件を持つ。集約規則
/// （<c>sum(field=...;groupBy=...)</c> 等）は「対象行の RowKey を接頭辞に持つ行」を畳み込む。
/// </summary>
internal sealed record ClaimCsvRowPlan(
    string RecordId,
    string RowKey,
    int? RecipientIndex,
    int? ServiceLineIndex,
    int? DailyRecordIndex)
{
    /// <summary>ファイル全体を 1 行で表す（請求書 総括・集計）。</summary>
    internal const string FileRowKey = "";

    /// <remarks>
    /// スコープ判定は接頭辞一致で行うため、受給者キーは必ず区切り文字で終える。
    /// 区切りが無いと、受給者 1000 のキー "R1000" が受給者 10000 の "R10000" の接頭辞になり、
    /// 集約が別の受給者の行を巻き込む。
    /// </remarks>
    internal static string RecipientKey(int recipientIndex) => $"R{recipientIndex:D4}/";

    internal static string ServiceLineKey(int recipientIndex, int lineIndex) =>
        $"{RecipientKey(recipientIndex)}L{lineIndex:D4}";

    internal static string DailyRecordKey(int recipientIndex, int dayIndex) =>
        $"{RecipientKey(recipientIndex)}D{dayIndex:D4}";

    internal static ClaimCsvRowPlan File(string recordId) =>
        new(recordId, FileRowKey, null, null, null);

    internal static ClaimCsvRowPlan Recipient(string recordId, int recipientIndex) =>
        new(recordId, RecipientKey(recipientIndex), recipientIndex, null, null);

    internal static ClaimCsvRowPlan ServiceLine(string recordId, int recipientIndex, int lineIndex) =>
        new(recordId, ServiceLineKey(recipientIndex, lineIndex), recipientIndex, lineIndex, null);

    internal static ClaimCsvRowPlan DailyRecord(string recordId, int recipientIndex, int dayIndex) =>
        new(recordId, DailyRecordKey(recipientIndex, dayIndex), recipientIndex, null, dayIndex);

    /// <summary>この行が <paramref name="scopeRowKey"/> のスコープ内にあるか。</summary>
    internal bool IsWithin(string scopeRowKey) =>
        scopeRowKey.Length == 0 || RowKey.StartsWith(scopeRowKey, StringComparison.Ordinal);
}

/// <summary>
/// 内側レコードの発生計画。<c>provider-claim-r7-10.json</c> の <c>order</c> に従って
/// recordId 昇順（J111:01 → J111:02 → J121:01..05 → J611:01 → J611:02）に並べる。
/// </summary>
internal static class ClaimCsvRowPlanner
{
    /// <summary>
    /// 経過措置レコード（<c>provider:J121:05</c>）は本スライスのスコープ外のため 0 行にする。
    /// 対応する <c>ContractedProvider.*</c> は finalization snapshot v2 に含まれない。
    /// </summary>
    internal const string TransitionalRecordId = "provider:J121:05";

    internal static IReadOnlyList<ClaimCsvRowPlan> Plan(ClaimCsvDto dto, IReadOnlyList<string> orderedRecordIds)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(orderedRecordIds);

        var rows = new List<ClaimCsvRowPlan>();
        foreach (var recordId in orderedRecordIds)
        {
            rows.AddRange(PlanRecord(dto, recordId));
        }

        return rows;
    }

    private static IEnumerable<ClaimCsvRowPlan> PlanRecord(ClaimCsvDto dto, string recordId)
    {
        switch (recordId)
        {
            case "provider:J111:01":
            case "provider:J111:02":
                // 就労継続支援B型のみを scope とするため、給付種別×サービス種類は単一グループ。
                yield return ClaimCsvRowPlan.File(recordId);
                break;
            case "provider:J121:01":
            case "provider:J121:02":
            case "provider:J121:04":
            case "provider:J611:01":
                for (var index = 0; index < dto.Recipients.Count; index++)
                {
                    yield return ClaimCsvRowPlan.Recipient(recordId, index);
                }

                break;
            case "provider:J121:03":
                for (var index = 0; index < dto.Recipients.Count; index++)
                {
                    for (var line = 0; line < dto.Recipients[index].ServiceLines.Count; line++)
                    {
                        yield return ClaimCsvRowPlan.ServiceLine(recordId, index, line);
                    }
                }

                break;
            case "provider:J611:02":
                for (var index = 0; index < dto.Recipients.Count; index++)
                {
                    for (var day = 0; day < dto.Recipients[index].DailyRecords.Count; day++)
                    {
                        yield return ClaimCsvRowPlan.DailyRecord(recordId, index, day);
                    }
                }

                break;
            case TransitionalRecordId:
                break;
            default:
                throw new ClaimCsvGenerationException(
                    recordId,
                    ClaimCsvGenerationReason.UnknownMappingStatus,
                    "no row plan is defined for this inner record");
        }
    }
}
