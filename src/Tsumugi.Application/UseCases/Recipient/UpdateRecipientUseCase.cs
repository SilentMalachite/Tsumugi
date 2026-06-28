using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// 利用者更新の入力値。氏名と生年月日は必須、それ以外の障害種別/連絡先は任意。
/// </summary>
public sealed record UpdateRecipientInput(
    Guid Id,
    Guid ExpectedConcurrencyToken,
    string KanjiName,
    string KanaName,
    DateOnly DateOfBirth)
{
    public DisabilityCategories Disabilities { get; init; }
    public string? PostalCode { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactRelationship { get; init; }
    public string? EmergencyContactPhone { get; init; }
}

public sealed class UpdateRecipientUseCase(
    IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock, IAuditTrail audit)
{
    public async Task ExecuteAsync(
        UpdateRecipientInput input, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Id == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(input));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));
        var existing = await repo.FindByIdAsync(input.Id, ct)
            ?? throw new InvalidOperationException("利用者が見つかりません。");
        if (existing.ConcurrencyToken != input.ExpectedConcurrencyToken)
            throw new OptimisticConcurrencyException(
                nameof(Tsumugi.Domain.Entities.Recipient), input.Id);
        if (string.IsNullOrWhiteSpace(input.KanjiName))
            throw new ArgumentException("氏名（漢字）は必須です。", nameof(input));
        if (string.IsNullOrWhiteSpace(input.KanaName))
            throw new ArgumentException("氏名（カナ）は必須です。", nameof(input));
        DateValidator.EnsureValid(input.DateOfBirth, nameof(input));

        var updated = existing with
        {
            KanjiName = input.KanjiName,
            KanaName = input.KanaName,
            DateOfBirth = input.DateOfBirth,
            Disabilities = input.Disabilities,
            PostalCode = input.PostalCode,
            Address = input.Address,
            PhoneNumber = input.PhoneNumber,
            EmailAddress = input.EmailAddress,
            EmergencyContactName = input.EmergencyContactName,
            EmergencyContactRelationship = input.EmergencyContactRelationship,
            EmergencyContactPhone = input.EmergencyContactPhone,
        };
        await repo.UpdateAsync(updated, ct);
        await audit.RecordAsync(actor, AuditAction.Update, nameof(Tsumugi.Domain.Entities.Recipient),
            input.Id, clock.GetUtcNow(),
            summary: $"kanji={input.KanjiName}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
