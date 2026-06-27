using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 日次記録（取引記録・厳密追記）。決して更新・削除しない。訂正・取消は新レコードで表現する。
/// 更新トークンは持たず、基底の <see cref="Entity.ConcurrencyToken"/> は無視する。
/// </summary>
public sealed record DailyRecord : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateOnly ServiceDate { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public Attendance Attendance { get; init; }
    public TransportKind Transport { get; init; }
    public bool MealProvided { get; init; }
    public string? Note { get; init; }

    public static DailyRecord NewRecord(
        Guid id, Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.New,
            OriginId = null,
            Attendance = attendance,
            Transport = transport,
            MealProvided = mealProvided,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,  // 取引記録は更新しないため未使用
        };

    public static DailyRecord Correction(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.Correct,
            OriginId = originId,
            Attendance = attendance,
            Transport = transport,
            MealProvided = mealProvided,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    public static DailyRecord Cancellation(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            Attendance = Attendance.Discontinued,
            Transport = TransportKind.None,
            MealProvided = false,
            Note = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
}
