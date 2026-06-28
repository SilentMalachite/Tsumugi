using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 作業実績（取引記録・厳密追記）。DailyRecord と同型の追記機構。
/// 計測値は方式に応じて null 可（方式ごとに使う列が変わる）。
/// </summary>
public sealed record WorkRecord : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateOnly WorkDate { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public int? WorkedMinutes { get; init; }
    public int? PieceCount { get; init; }
    public int? PieceUnitYen { get; init; }
    public int? Points { get; init; }
    public string? Note { get; init; }

    public static WorkRecord NewRecord(
        Guid id, Guid recipientId, DateOnly workDate,
        int? workedMinutes, int? pieceCount, int? pieceUnitYen, int? points,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            WorkDate = workDate,
            Kind = RecordKind.New,
            OriginId = null,
            WorkedMinutes = workedMinutes,
            PieceCount = pieceCount,
            PieceUnitYen = pieceUnitYen,
            Points = points,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    public static WorkRecord Correction(
        Guid id, Guid recipientId, DateOnly workDate, Guid originId,
        int? workedMinutes, int? pieceCount, int? pieceUnitYen, int? points,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            WorkDate = workDate,
            Kind = RecordKind.Correct,
            OriginId = originId,
            WorkedMinutes = workedMinutes,
            PieceCount = pieceCount,
            PieceUnitYen = pieceUnitYen,
            Points = points,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    public static WorkRecord Cancellation(
        Guid id, Guid recipientId, DateOnly workDate, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            WorkDate = workDate,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            WorkedMinutes = null,
            PieceCount = null,
            PieceUnitYen = null,
            Points = null,
            Note = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
}
