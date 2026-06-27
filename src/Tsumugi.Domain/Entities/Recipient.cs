namespace Tsumugi.Domain.Entities;

/// <summary>利用者（同一性マスタ・楽観ロック）。氏名は漢字とカナを分けて保持。</summary>
public sealed record Recipient : Entity
{
    public required string KanjiName { get; init; }
    public required string KanaName { get; init; }
    public required DateOnly DateOfBirth { get; init; }

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
        };
}
