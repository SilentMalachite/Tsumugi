using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record OfficeDto(
    Guid Id,
    string OfficeNumber,
    string Name,
    ServiceCategory ServiceCategory,
    RegionGrade RegionGrade);
