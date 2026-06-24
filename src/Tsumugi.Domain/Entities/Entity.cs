namespace Tsumugi.Domain.Entities;

/// <summary>全永続エンティティの基底。Guid主キー＋監査列＋楽観ロック用トークンを持つ。</summary>
public abstract record Entity
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }

    /// <summary>楽観的同時実行の更新トークン（SQLiteに rowversion が無いため Guid で代替）。</summary>
    public required Guid ConcurrencyToken { get; init; }
}
