using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

/// <summary>
/// 事業所の DTO。編集画面で楽観的同時実行を成立させるため、画面が読んだ時点の
/// <see cref="ConcurrencyToken"/> を保持し、更新要求時にそのまま UseCase へ戻す。
/// </summary>
public sealed record OfficeDto(
    Guid Id,
    string OfficeNumber,
    string Name,
    ServiceCategory ServiceCategory,
    RegionGrade RegionGrade,
    Guid ConcurrencyToken,
    string? PostalCode = null,
    string? Address = null,
    string? PhoneNumber = null,
    string? RepresentativeTitleAndName = null);
