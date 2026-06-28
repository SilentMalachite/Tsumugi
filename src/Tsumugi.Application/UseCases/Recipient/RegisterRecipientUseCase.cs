using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// 利用者新規登録の入力値。氏名と生年月日は必須、それ以外の障害種別/連絡先は任意。
/// </summary>
public sealed record RegisterRecipientInput(
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

public sealed class RegisterRecipientUseCase(
    IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<RecipientDto> ExecuteAsync(
        RegisterRecipientInput input, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.KanjiName))
            throw new ArgumentException("氏名（漢字）は必須です。", nameof(input));
        if (string.IsNullOrWhiteSpace(input.KanaName))
            throw new ArgumentException("氏名（カナ）は必須です。", nameof(input));
        DateValidator.EnsureValid(input.DateOfBirth, nameof(input));

        var entity = Domain.Entities.Recipient.Create(
            Guid.NewGuid(), input.KanjiName, input.KanaName, input.DateOfBirth,
            actor, clock.GetUtcNow(), Guid.NewGuid(),
            disabilities: input.Disabilities,
            postalCode: input.PostalCode,
            address: input.Address,
            phoneNumber: input.PhoneNumber,
            emailAddress: input.EmailAddress,
            emergencyContactName: input.EmergencyContactName,
            emergencyContactRelationship: input.EmergencyContactRelationship,
            emergencyContactPhone: input.EmergencyContactPhone);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return MapToDto(entity);
    }

    internal static RecipientDto MapToDto(Domain.Entities.Recipient r) => new(
        r.Id, r.KanjiName, r.KanaName, r.DateOfBirth, r.ConcurrencyToken, r.IsArchived,
        r.Disabilities,
        r.PostalCode, r.Address, r.PhoneNumber, r.EmailAddress,
        r.EmergencyContactName, r.EmergencyContactRelationship, r.EmergencyContactPhone);
}
