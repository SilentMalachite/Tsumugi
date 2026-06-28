using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>工賃確定スナップショット（取引記録・厳密追記）。再確定は Correction で履歴に残す。</summary>
public sealed record WageStatement : Entity
{
    public required Guid OfficeId { get; init; }
    public required YearMonth Month { get; init; }
    public required Guid RecipientId { get; init; }
    public required int AmountYen { get; init; }
    public required string BasisSummary { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }

    public static WageStatement NewRecord(
        Guid id, Guid officeId, YearMonth month, Guid recipientId,
        int amountYen, string basisSummary, string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        ArgumentException.ThrowIfNullOrEmpty(basisSummary);
        return new WageStatement
        {
            Id = id,
            OfficeId = officeId,
            Month = month,
            RecipientId = recipientId,
            AmountYen = amountYen,
            BasisSummary = basisSummary,
            Kind = RecordKind.New,
            OriginId = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }

    public static WageStatement Correction(
        Guid id, Guid officeId, YearMonth month, Guid recipientId, Guid originId,
        int amountYen, string basisSummary, string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        ArgumentException.ThrowIfNullOrEmpty(basisSummary);
        return new WageStatement
        {
            Id = id,
            OfficeId = officeId,
            Month = month,
            RecipientId = recipientId,
            AmountYen = amountYen,
            BasisSummary = basisSummary,
            Kind = RecordKind.Correct,
            OriginId = originId,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }
}
