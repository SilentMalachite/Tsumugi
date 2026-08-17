using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// 利用者のフェースシート履歴を CreatedAt 昇順で返し、各版の直前版差分を付与する。
/// Domain の <see cref="FaceSheetDiff"/> は Application 内で閉じ、呼び出し側へは漏らさない。
/// </summary>
public sealed class QueryFaceSheetHistoryUseCase(IFaceSheetRepository repo)
{
    public async Task<IReadOnlyList<FaceSheetHistoryDto>> ExecuteAsync(
        Guid recipientId, CancellationToken ct)
    {
        var versions = await repo.ListByRecipientAsync(recipientId, ct);
        return versions.Select((sheet, index) => new FaceSheetHistoryDto(
            SaveFaceSheetUseCase.ToDto(sheet),
            index == 0
                ? []
                : FaceSheetDiff.Compare(versions[index - 1], sheet)
                    .Select(change => new FaceSheetChangeDto(
                        change.PropertyName, change.OldValue, change.NewValue))
                    .ToArray()))
            .ToArray();
    }
}
