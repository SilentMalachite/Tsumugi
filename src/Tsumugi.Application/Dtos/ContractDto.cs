using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record ContractDto(Guid Id, Guid RecipientId, DateRange Period, int ContractedSupplyDays);
