using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageBasisExtractorTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Counts_only_present_days_per_recipient()
    {
        var d = new[]
        {
            DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T),
            DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), Attendance.Absent, TransportKind.None, false, null, "t", T),
            DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 3), Attendance.AbsenceSupport, TransportKind.None, false, null, "t", T),
            DailyRecord.NewRecord(Guid.NewGuid(), R2, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T),
        };
        var w = Array.Empty<WorkRecord>();
        var inputs = WageBasisExtractor.Build(d, w, Month);

        inputs.Should().HaveCount(2);
        inputs.First(i => i.RecipientId == R1).PresentDays.Should().Be(1);
        inputs.First(i => i.RecipientId == R2).PresentDays.Should().Be(1);
    }

    [Fact]
    public void Aggregates_worked_minutes_and_piece_amounts_from_effective_work_records()
    {
        var d1 = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var w1 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), workedMinutes: 240, pieceCount: 5, pieceUnitYen: 100, points: 0, note: null, "t", T);
        var w2 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), workedMinutes: 180, pieceCount: 3, pieceUnitYen: 100, points: 0, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(new[] { d1 }, new[] { w1, w2 }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.TotalWorkedMinutes.Should().Be(420);
        i.TotalPieceAmountYen.Should().Be(800);
        i.PresentDays.Should().Be(1);
    }

    [Fact]
    public void Filters_out_months_outside_target()
    {
        var d = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 8, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var inputs = WageBasisExtractor.Build(new[] { d }, Array.Empty<WorkRecord>(), Month);
        inputs.Should().BeEmpty();
    }
}
