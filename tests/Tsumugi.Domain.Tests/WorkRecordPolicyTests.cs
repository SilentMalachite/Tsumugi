using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WorkRecordPolicyTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 7, 1);
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private static WorkRecord N(int minutes, DateTimeOffset at, Guid? id = null) =>
        WorkRecord.NewRecord(id ?? Guid.NewGuid(), Recipient, Date,
            minutes, null, null, null, null, "tester", at);

    private static WorkRecord C(Guid origin, int minutes, DateTimeOffset at) =>
        WorkRecord.Correction(Guid.NewGuid(), Recipient, Date, origin,
            minutes, null, null, null, "訂正", "tester", at);

    private static WorkRecord X(Guid origin, DateTimeOffset at) =>
        WorkRecord.Cancellation(Guid.NewGuid(), Recipient, Date, origin, "tester", at);

    [Fact]
    public void Empty_returns_null()
        => WorkRecordPolicy.Effective(Array.Empty<WorkRecord>()).Should().BeNull();

    [Fact]
    public void Single_new_is_effective()
    {
        var n = N(240, T0);
        WorkRecordPolicy.Effective(new[] { n }).Should().Be(n);
    }

    [Fact]
    public void Latest_correction_wins_among_siblings()
    {
        var n = N(240, T0);
        var c1 = C(n.Id, 200, T0.AddMinutes(1));
        var c2 = C(n.Id, 180, T0.AddMinutes(2));
        WorkRecordPolicy.Effective(new[] { n, c1, c2 }).Should().Be(c2);
    }

    [Fact]
    public void Cancellation_makes_effective_null()
    {
        var n = N(240, T0);
        var x = X(n.Id, T0.AddMinutes(1));
        WorkRecordPolicy.Effective(new[] { n, x }).Should().BeNull();
    }

    [Fact]
    public void EffectiveByDate_groups_per_day()
    {
        var d1 = new DateOnly(2026, 7, 1);
        var d2 = new DateOnly(2026, 7, 2);
        var n1 = WorkRecord.NewRecord(Guid.NewGuid(), Recipient, d1, 240, null, null, null, null, "t", T0);
        var n2 = WorkRecord.NewRecord(Guid.NewGuid(), Recipient, d2, 360, null, null, null, null, "t", T0);

        var byDate = WorkRecordPolicy.EffectiveByDate(new[] { n1, n2 });
        byDate.Should().HaveCount(2);
        byDate[d1].Should().Be(n1);
        byDate[d2].Should().Be(n2);
    }
}
