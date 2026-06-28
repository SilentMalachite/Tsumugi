using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

/// <summary>
/// 利用者の DTO。編集画面で楽観的同時実行を成立させるため、画面が読んだ時点の
/// <see cref="ConcurrencyToken"/> を保持し、更新要求時にそのまま UseCase へ戻す。
/// アーカイブ状態（論理削除）と障害種別・連絡先（マスタ現行値）も併せて公開する。
/// </summary>
public sealed record RecipientDto(
    Guid Id,
    string KanjiName,
    string KanaName,
    DateOnly DateOfBirth,
    Guid ConcurrencyToken,
    bool IsArchived,
    DisabilityCategories Disabilities,
    string? PostalCode,
    string? Address,
    string? PhoneNumber,
    string? EmailAddress,
    string? EmergencyContactName,
    string? EmergencyContactRelationship,
    string? EmergencyContactPhone);
