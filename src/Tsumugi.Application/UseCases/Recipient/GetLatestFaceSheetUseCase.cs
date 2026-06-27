using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// 利用者の現行フェースシート（CreatedAt 降順の先頭）を取得する。
/// 未登録の場合は null を返す。
/// </summary>
public sealed class GetLatestFaceSheetUseCase(IFaceSheetRepository repo)
{
    public async Task<FaceSheetDto?> ExecuteAsync(Guid recipientId, CancellationToken ct)
    {
        if (recipientId == Guid.Empty) return null;
        var entity = await repo.FindLatestByRecipientAsync(recipientId, ct);
        return entity is null ? null : SaveFaceSheetUseCase.ToDto(entity);
    }
}
