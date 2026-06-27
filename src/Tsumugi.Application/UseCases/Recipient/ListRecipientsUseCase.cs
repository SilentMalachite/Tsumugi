using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class ListRecipientsUseCase(IRecipientRepository repo)
{
    public async Task<IReadOnlyList<RecipientDto>> ExecuteAsync(CancellationToken ct)
    {
        var list = await repo.ListAsync(ct);
        return list.Select(r => new RecipientDto(r.Id, r.KanjiName, r.KanaName, r.DateOfBirth)).ToArray();
    }
}
