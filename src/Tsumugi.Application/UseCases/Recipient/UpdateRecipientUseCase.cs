using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Validation;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class UpdateRecipientUseCase(IRecipientRepository repo, IUnitOfWork uow)
{
    public async Task ExecuteAsync(
        Guid id, string kanjiName, string kanaName, DateOnly dateOfBirth,
        string actor, CancellationToken ct)
    {
        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("利用者が見つかりません。");
        if (string.IsNullOrWhiteSpace(kanjiName))
            throw new ArgumentException("氏名（漢字）は必須です。", nameof(kanjiName));
        if (string.IsNullOrWhiteSpace(kanaName))
            throw new ArgumentException("氏名（カナ）は必須です。", nameof(kanaName));
        DateValidator.EnsureValid(dateOfBirth, nameof(dateOfBirth));

        var updated = existing with { KanjiName = kanjiName, KanaName = kanaName, DateOfBirth = dateOfBirth };
        await repo.UpdateAsync(updated, ct);
        await uow.SaveChangesAsync(ct);
        _ = actor;  // 監査ログ拡張用フック（フェーズ1では使用しない）
    }
}
