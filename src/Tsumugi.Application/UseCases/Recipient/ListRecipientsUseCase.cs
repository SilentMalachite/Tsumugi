using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class ListRecipientsUseCase(IRecipientRepository repo)
{
    /// <summary>
    /// 利用者一覧を取得する。<paramref name="includeArchived"/> が true のときアーカイブ済みも含む。
    /// </summary>
    public async Task<IReadOnlyList<RecipientDto>> ExecuteAsync(
        bool includeArchived, CancellationToken ct)
    {
        var list = await repo.ListAsync(includeArchived, ct);
        return list.Select(r => new RecipientDto(
            r.Id, r.KanjiName, r.KanaName, r.DateOfBirth, r.ConcurrencyToken, r.IsArchived)).ToArray();
    }
}
