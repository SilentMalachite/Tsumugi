// src/Tsumugi.Domain/Entities/RecipientHourlyRate.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>利用者×期間の時給マスタ（追記型）。KouchinModule v5 は月中変動を扱うため期間マスタで持つ。</summary>
public sealed record RecipientHourlyRate : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required DateRange Period { get; init; }
    public required int HourlyYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static RecipientHourlyRate NewRecord(
        Guid id, Guid officeId, Guid recipientId, DateRange period, int hourlyYen,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hourlyYen);
        return new RecipientHourlyRate
        {
            Id = id,
            OfficeId = officeId,
            RecipientId = recipientId,
            Period = period,
            HourlyYen = hourlyYen,
            Kind = RecordKind.New,
            OriginId = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static RecipientHourlyRate Correction(
        Guid id, Guid officeId, Guid recipientId, DateRange period, Guid originId, int hourlyYen,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hourlyYen);
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new RecipientHourlyRate
        {
            Id = id,
            OfficeId = officeId,
            RecipientId = recipientId,
            Period = period,
            HourlyYen = hourlyYen,
            Kind = RecordKind.Correct,
            OriginId = originId,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static RecipientHourlyRate Cancel(
        Guid id, Guid officeId, Guid recipientId, DateRange period, Guid originId,
        string createdBy, DateTimeOffset createdAt)
    {
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new RecipientHourlyRate
        {
            Id = id,
            OfficeId = officeId,
            RecipientId = recipientId,
            Period = period,
            HourlyYen = 0,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
