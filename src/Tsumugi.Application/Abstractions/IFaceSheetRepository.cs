using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IFaceSheetRepository
{
    Task AddAsync(FaceSheet faceSheet, CancellationToken ct);
    /// <summary>
    /// 利用者の最新フェースシート（CreatedAt 降順の先頭）を返す。未登録なら null。
    /// </summary>
    Task<FaceSheet?> FindLatestByRecipientAsync(Guid recipientId, CancellationToken ct);

    /// <summary>
    /// 利用者のフェースシート履歴を CreatedAt 昇順で返す。別利用者は混ぜない。
    /// </summary>
    Task<IReadOnlyList<FaceSheet>> ListByRecipientAsync(Guid recipientId, CancellationToken ct);
}
