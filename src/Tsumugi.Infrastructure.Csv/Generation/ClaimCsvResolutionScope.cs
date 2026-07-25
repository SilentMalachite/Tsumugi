using Tsumugi.Application.Dtos.Claim.Csv;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>モデル経路を解決するときの行スコープ（どの受給者・どの明細行・どの日次記録か）。</summary>
internal sealed record ClaimCsvResolutionScope(string FieldId, ClaimCsvDto Dto, ClaimCsvRowPlan Row)
{
    internal ClaimCsvRecipientDto? Recipient =>
        Row.RecipientIndex is { } index ? Dto.Recipients[index] : null;

    internal ClaimCsvRecipientDto RequireRecipient(string path) =>
        Recipient ?? throw new ClaimCsvGenerationException(
            FieldId,
            ClaimCsvGenerationReason.MissingRow,
            $"model path '{path}' requires a recipient row but the record '{Row.RecordId}' has none");

    internal ClaimCsvServiceLineDto RequireLine(string path) =>
        Row.ServiceLineIndex is { } line && Recipient is { } recipient
            ? recipient.ServiceLines[line]
            : throw new ClaimCsvGenerationException(
                FieldId,
                ClaimCsvGenerationReason.MissingRow,
                $"model path '{path}' requires a service line row but the record '{Row.RecordId}' has none");

    internal ClaimCsvDailyRecordDto RequireDay(string path) =>
        Row.DailyRecordIndex is { } day && Recipient is { } recipient
            ? recipient.DailyRecords[day]
            : throw new ClaimCsvGenerationException(
                FieldId,
                ClaimCsvGenerationReason.MissingRow,
                $"model path '{path}' requires a daily record row but the record '{Row.RecordId}' has none");

    /// <summary>
    /// 集約規則の対象になる日次記録を、行スコープ付きで列挙する（受給者スコープならその受給者、
    /// ファイルスコープなら全受給者）。各要素は当該日を指す解決スコープであり、
    /// 呼び出し側が日次記録の位置を探し直す必要がない。
    /// </summary>
    internal IEnumerable<ClaimCsvResolutionScope> EnumerateDailyRecordScopes()
    {
        if (Row.RecipientIndex is { } recipientIndex)
        {
            return DailyRecordScopesFor(recipientIndex);
        }

        return Enumerable.Range(0, Dto.Recipients.Count).SelectMany(DailyRecordScopesFor);
    }

    private IEnumerable<ClaimCsvResolutionScope> DailyRecordScopesFor(int recipientIndex) =>
        Enumerable.Range(0, Dto.Recipients[recipientIndex].DailyRecords.Count)
            .Select(dayIndex => this with
            {
                Row = Row with { RecipientIndex = recipientIndex, DailyRecordIndex = dayIndex },
            });
}
