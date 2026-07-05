// src/Tsumugi.Domain/Entities/WageAdjustment.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>利用者×月の任意手当（追記型）。KouchinModule 工賃集計 G 列（特別手当）等を受ける。</summary>
public sealed record WageAdjustment : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required YearMonth YearMonth { get; init; }
    public required WageAdjustmentType Type { get; init; }
    public required int AmountYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static WageAdjustment NewRecord(
        Guid id, Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, int amountYen, string? note,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        return new WageAdjustment
        {
            Id = id,
            OfficeId = officeId,
            RecipientId = recipientId,
            YearMonth = yearMonth,
            Type = type,
            AmountYen = amountYen,
            Kind = RecordKind.New,
            OriginId = null,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static WageAdjustment Correction(
        Guid id, Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, Guid originId, int amountYen, string? note,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new WageAdjustment
        {
            Id = id,
            OfficeId = officeId,
            RecipientId = recipientId,
            YearMonth = yearMonth,
            Type = type,
            AmountYen = amountYen,
            Kind = RecordKind.Correct,
            OriginId = originId,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static WageAdjustment Cancel(
        Guid id, Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, Guid originId,
        string createdBy, DateTimeOffset createdAt)
    {
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new WageAdjustment
        {
            Id = id,
            OfficeId = officeId,
            RecipientId = recipientId,
            YearMonth = yearMonth,
            Type = type,
            AmountYen = 0,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            Note = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
