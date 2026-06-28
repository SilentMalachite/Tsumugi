using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 利用者（同一性マスタ・楽観ロック）。氏名は漢字とカナを分けて保持。
/// 障害種別と連絡先（住所/電話/メール/緊急連絡先）を持ち、利用者一覧・登録画面で扱う。
/// より詳細な医療歴・生活歴・受給状況は <see cref="FaceSheet"/> に追記版数で保持する。
/// 「削除」はアーカイブ（論理削除）として表現し、関連する日次記録・受給者証・契約の履歴を破壊しない。
/// </summary>
public sealed record Recipient : Entity
{
    public required string KanjiName { get; init; }
    public required string KanaName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public string? ArchivedBy { get; init; }

    // -------- 障害種別 --------
    public DisabilityCategories Disabilities { get; init; }

    // -------- 連絡先（マスタ「現行」値） --------
    public string? PostalCode { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactRelationship { get; init; }
    public string? EmergencyContactPhone { get; init; }

    public bool IsArchived => ArchivedAt is not null;

    public static Recipient Create(
        Guid id,
        string kanjiName,
        string kanaName,
        DateOnly dateOfBirth,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken,
        DisabilityCategories disabilities = default,
        string? postalCode = null,
        string? address = null,
        string? phoneNumber = null,
        string? emailAddress = null,
        string? emergencyContactName = null,
        string? emergencyContactRelationship = null,
        string? emergencyContactPhone = null) => new()
        {
            Id = id,
            KanjiName = kanjiName,
            KanaName = kanaName,
            DateOfBirth = dateOfBirth,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
            ArchivedAt = null,
            ArchivedBy = null,
            Disabilities = disabilities,
            PostalCode = postalCode,
            Address = address,
            PhoneNumber = phoneNumber,
            EmailAddress = emailAddress,
            EmergencyContactName = emergencyContactName,
            EmergencyContactRelationship = emergencyContactRelationship,
            EmergencyContactPhone = emergencyContactPhone,
        };

    /// <summary>アーカイブ（論理削除）。既にアーカイブ済みなら自身を返す。</summary>
    public Recipient Archive(string actor, DateTimeOffset archivedAt) =>
        IsArchived ? this : this with { ArchivedAt = archivedAt, ArchivedBy = actor };

    /// <summary>アーカイブを解除し、利用者一覧に復元する。</summary>
    public Recipient Restore() =>
        IsArchived ? this with { ArchivedAt = null, ArchivedBy = null } : this;
}
