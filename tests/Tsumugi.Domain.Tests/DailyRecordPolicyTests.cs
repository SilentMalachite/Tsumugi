using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DailyRecordPolicyTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 6, 1);
    private static DateTimeOffset T(int hour) => new(2026, 6, 2, hour, 0, 0, TimeSpan.Zero);

    private static DailyRecord New(Guid id, Attendance att, int t) =>
        DailyRecord.NewRecord(id, Recipient, Day, att, TransportKind.None, false, null, "u", T(t));
    private static DailyRecord Corr(Guid id, Guid origin, Attendance att, int t) =>
        DailyRecord.Correction(id, Recipient, Day, origin, att, TransportKind.None, false, null, "u", T(t));
    private static DailyRecord Cancel(Guid id, Guid origin, int t) =>
        DailyRecord.Cancellation(id, Recipient, Day, origin, "u", T(t));

    [Fact]
    public void Empty_returns_null()
    {
        DailyRecordPolicy.Effective(Array.Empty<DailyRecord>()).Should().BeNull();
    }

    [Fact]
    public void Single_new_record_is_effective()
    {
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        DailyRecordPolicy.Effective(new[] { n }).Should().Be(n);
    }

    [Fact]
    public void Latest_correction_wins()
    {
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        var c1 = Corr(Guid.NewGuid(), n.Id, Attendance.Absent, 10);
        var c2 = Corr(Guid.NewGuid(), c1.Id, Attendance.AbsenceSupport, 11);
        DailyRecordPolicy.Effective(new[] { c1, n, c2 }).Should().Be(c2);
    }

    [Fact]
    public void Cancellation_makes_effective_null()
    {
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        var x = Cancel(Guid.NewGuid(), n.Id, 10);
        DailyRecordPolicy.Effective(new[] { n, x }).Should().BeNull();
    }

    [Fact]
    public void Re_correction_after_cancellation_is_ignored()
    {
        // 取消後にさらに「取消Idを訂正元」とする訂正が来ても、取消は最終状態として残る
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        var x = Cancel(Guid.NewGuid(), n.Id, 10);
        var c = Corr(Guid.NewGuid(), x.Id, Attendance.Present, 11);
        DailyRecordPolicy.Effective(new[] { n, x, c }).Should().BeNull();
    }

    [Fact]
    public void EffectiveByDate_groups_by_service_date()
    {
        var a = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", T(9));
        var b = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, new DateOnly(2026, 6, 2),
            Attendance.Absent, TransportKind.None, false, null, "u", T(9));
        var map = DailyRecordPolicy.EffectiveByDate(new[] { a, b });
        map.Should().HaveCount(2);
        map[new DateOnly(2026, 6, 1)].Should().Be(a);
        map[new DateOnly(2026, 6, 2)].Should().Be(b);
    }
}
