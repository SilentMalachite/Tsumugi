using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>事業所マスタ（同一性マスタ・楽観ロック）。</summary>
public sealed record Office : Entity
{
    public required string OfficeNumber { get; init; }
    public required string Name { get; init; }
    public required ServiceCategory ServiceCategory { get; init; }
    public required RegionGrade RegionGrade { get; init; }
    public string? PostalCode { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? RepresentativeTitleAndName { get; init; }

    public static Office Create(
        Guid id,
        string officeNumber,
        string name,
        ServiceCategory serviceCategory,
        RegionGrade regionGrade,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken,
        string? postalCode = null,
        string? address = null,
        string? phoneNumber = null,
        string? representativeTitleAndName = null) => new()
        {
            Id = id,
            OfficeNumber = officeNumber,
            Name = name,
            ServiceCategory = serviceCategory,
            RegionGrade = regionGrade,
            PostalCode = postalCode,
            Address = address,
            PhoneNumber = phoneNumber,
            RepresentativeTitleAndName = representativeTitleAndName,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
}
