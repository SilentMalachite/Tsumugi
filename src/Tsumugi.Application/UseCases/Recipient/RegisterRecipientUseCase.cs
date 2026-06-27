using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class RegisterRecipientUseCase(
    IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<RecipientDto> ExecuteAsync(
        string kanjiName, string kanaName, DateOnly dateOfBirth,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kanjiName))
            throw new ArgumentException("氏名（漢字）は必須です。", nameof(kanjiName));
        if (string.IsNullOrWhiteSpace(kanaName))
            throw new ArgumentException("氏名（カナ）は必須です。", nameof(kanaName));
        DateValidator.EnsureValid(dateOfBirth, nameof(dateOfBirth));

        var entity = Domain.Entities.Recipient.Create(
            Guid.NewGuid(), kanjiName, kanaName, dateOfBirth,
            actor, clock.GetUtcNow(), Guid.NewGuid());

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return new RecipientDto(entity.Id, entity.KanjiName, entity.KanaName, entity.DateOfBirth);
    }
}
