using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record OfficeCapabilityDto(
    Guid Id, Guid OfficeId, DateRange Period, IReadOnlyDictionary<string, bool> Flags);
