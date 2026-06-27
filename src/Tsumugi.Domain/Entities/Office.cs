using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>事業所マスタ（同一性マスタ・楽観ロック）。</summary>
public sealed record Office : Entity
{
    public required string OfficeNumber { get; init; }
    public required string Name { get; init; }
    public required ServiceCategory ServiceCategory { get; init; }
    public required RegionGrade RegionGrade { get; init; }

    public static Office Create(
        Guid id,
        string officeNumber,
        string name,
        ServiceCategory serviceCategory,
        RegionGrade regionGrade,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken) => new()
        {
            Id = id,
            OfficeNumber = officeNumber,
            Name = name,
            ServiceCategory = serviceCategory,
            RegionGrade = regionGrade,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
}
