namespace Tsumugi.Domain.Entities;

/// <summary>事業所マスタ（フェーズ0は最小。フェーズ1でサービス種別・地域区分・体制届を拡張）。</summary>
public sealed record Office : Entity
{
    public required string OfficeNumber { get; init; }
    public required string Name { get; init; }

    public static Office Create(
        Guid id,
        string officeNumber,
        string name,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken) => new()
        {
            Id = id,
            OfficeNumber = officeNumber,
            Name = name,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
}
