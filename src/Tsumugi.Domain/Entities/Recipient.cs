namespace Tsumugi.Domain.Entities;

/// <summary>
/// 利用者（同一性マスタ・楽観ロック）。氏名は漢字とカナを分けて保持。
/// 「削除」はアーカイブ（論理削除）として表現し、関連する日次記録・受給者証・契約の履歴を破壊しない。
/// </summary>
public sealed record Recipient : Entity
{
    public required string KanjiName { get; init; }
    public required string KanaName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public string? ArchivedBy { get; init; }

    public bool IsArchived => ArchivedAt is not null;

    public static Recipient Create(
        Guid id,
        string kanjiName,
        string kanaName,
        DateOnly dateOfBirth,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken) => new()
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
        };

    /// <summary>アーカイブ（論理削除）。既にアーカイブ済みなら自身を返す。</summary>
    public Recipient Archive(string actor, DateTimeOffset archivedAt) =>
        IsArchived ? this : this with { ArchivedAt = archivedAt, ArchivedBy = actor };

    /// <summary>アーカイブを解除し、利用者一覧に復元する。</summary>
    public Recipient Restore() =>
        IsArchived ? this with { ArchivedAt = null, ArchivedBy = null } : this;
}
