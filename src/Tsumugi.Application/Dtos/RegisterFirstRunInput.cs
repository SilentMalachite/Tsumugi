using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

/// <summary>初回起動ウィザードからの事業所登録入力。</summary>
public sealed record RegisterFirstRunInput(
    string OfficeNumber,
    string Name,
    ServiceCategory ServiceCategory,
    RegionGrade RegionGrade,
    string? PostalCode = null,
    string? Address = null,
    string? PhoneNumber = null,
    string? RepresentativeTitleAndName = null);
