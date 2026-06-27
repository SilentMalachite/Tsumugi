using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DailyRecordTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 6, 1);

    [Fact]
    public void NewRecord_has_no_originId()
    {
        var r = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.Round, mealProvided: true,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.New);
        r.OriginId.Should().BeNull();
    }

    [Fact]
    public void Correction_carries_originId()
    {
        var origin = Guid.NewGuid();
        var r = DailyRecord.Correction(Guid.NewGuid(), Recipient, Day, origin,
            Attendance.Absent, TransportKind.None, mealProvided: false,
            note: "病気のため", createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.Correct);
        r.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancellation_carries_originId()
    {
        var origin = Guid.NewGuid();
        var r = DailyRecord.Cancellation(Guid.NewGuid(), Recipient, Day, origin,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.Cancel);
        r.OriginId.Should().Be(origin);
    }
}
