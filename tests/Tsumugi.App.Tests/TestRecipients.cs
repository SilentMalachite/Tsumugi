using Tsumugi.Application.Dtos;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests;

/// <summary>テスト用の RecipientDto 構築ヘルパー。位置パラメータが多いため集約する。</summary>
internal static class TestRecipients
{
    public static RecipientDto Make(
        Guid id, string kanjiName = "山田", string kanaName = "ヤマダ",
        DateOnly? dateOfBirth = null, Guid? concurrencyToken = null, bool isArchived = false,
        DisabilityCategories disabilities = default,
        string? phoneNumber = null, string? address = null) => new(
            id, kanjiName, kanaName,
            dateOfBirth ?? new DateOnly(1990, 1, 1),
            concurrencyToken ?? Guid.NewGuid(),
            isArchived,
            disabilities,
            PostalCode: null,
            Address: address,
            PhoneNumber: phoneNumber,
            EmailAddress: null,
            EmergencyContactName: null,
            EmergencyContactRelationship: null,
            EmergencyContactPhone: null);
}
