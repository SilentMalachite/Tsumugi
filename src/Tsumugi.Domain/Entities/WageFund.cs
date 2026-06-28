using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>月次の工賃原資（追記訂正）。OfficeId × Month で一意の実効値を持つ。</summary>
public sealed record WageFund : Entity
{
    public required Guid OfficeId { get; init; }
    public required YearMonth Month { get; init; }
    public required int TotalYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static WageFund NewRecord(
        Guid id, Guid officeId, YearMonth month, int totalYen,
        string? note, string createdBy, DateTimeOffset createdAt)
    {
        if (totalYen < 0)
            throw new ArgumentOutOfRangeException(nameof(totalYen), totalYen, "工賃原資は0円以上で指定してください。");
        return new WageFund
        {
            Id = id,
            OfficeId = officeId,
            Month = month,
            TotalYen = totalYen,
            Kind = RecordKind.New,
            OriginId = null,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }

    public static WageFund Correction(
        Guid id, Guid officeId, YearMonth month, Guid originId, int totalYen,
        string? note, string createdBy, DateTimeOffset createdAt)
    {
        if (totalYen < 0)
            throw new ArgumentOutOfRangeException(nameof(totalYen), totalYen, "工賃原資は0円以上で指定してください。");
        return new WageFund
        {
            Id = id,
            OfficeId = officeId,
            Month = month,
            TotalYen = totalYen,
            Kind = RecordKind.Correct,
            OriginId = originId,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }

    public static WageFund Cancellation(
        Guid id, Guid officeId, YearMonth month, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            OfficeId = officeId,
            Month = month,
            TotalYen = 0,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            Note = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
}
