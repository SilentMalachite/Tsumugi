using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>受給者証（期間マスタ・実効日付つき追記）。</summary>
public sealed record Certificate : Entity
{
    public required Guid RecipientId { get; init; }
    public required string CertificateNumber { get; init; }
    public required DateRange Validity { get; init; }
    public required int SupplyDays { get; init; }
    public required int MonthlyCostCap { get; init; }
    public required string Municipality { get; init; }

    public static Certificate Create(
        Guid id,
        Guid recipientId,
        string certificateNumber,
        DateRange validity,
        int supplyDays,
        int monthlyCostCap,
        string municipality,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(supplyDays);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyCostCap);

        return new()
        {
            Id = id,
            RecipientId = recipientId,
            CertificateNumber = certificateNumber,
            Validity = validity,
            SupplyDays = supplyDays,
            MonthlyCostCap = monthlyCostCap,
            Municipality = municipality,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
