using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>契約（期間マスタ・実効日付つき追記）。終了日 null は継続中。</summary>
public sealed record Contract : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateRange Period { get; init; }
    public required int ContractedSupplyDays { get; init; }

    public static Contract Create(
        Guid id, Guid recipientId, DateRange period, int contractedSupplyDays,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contractedSupplyDays);
        return new()
        {
            Id = id,
            RecipientId = recipientId,
            Period = period,
            ContractedSupplyDays = contractedSupplyDays,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
