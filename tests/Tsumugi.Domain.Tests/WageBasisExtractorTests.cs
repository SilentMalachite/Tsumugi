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
    public void Aggregates_worked_minutes_and_piece_amounts_only_from_present_dates()
    {
        var d1 = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        // 7/2 は出席記録なし → 7/2 の work は除外されるべき
        var w1 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), workedMinutes: 240, pieceCount: 5, pieceUnitYen: 100, points: 0, note: null, "t", T);
        var w2 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), workedMinutes: 180, pieceCount: 3, pieceUnitYen: 100, points: 0, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(new[] { d1 }, new[] { w1, w2 }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.TotalWorkedMinutes.Should().Be(240);        // 7/1 のみ
        i.TotalPieceAmountYen.Should().Be(500);       // 5 * 100 のみ
        i.PresentDays.Should().Be(1);
    }

    [Fact]
    public void Filters_out_months_outside_target()
    {
        var d = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 8, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var inputs = WageBasisExtractor.Build(new[] { d }, Array.Empty<WorkRecord>(), Month);
        inputs.Should().BeEmpty();
    }

    [Fact]
    public void Excludes_work_records_on_absent_days()
    {
        var dPresent = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var dAbsent  = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), Attendance.Absent,  TransportKind.None, false, null, "t", T);
        var wPresent = WorkRecord.NewRecord(Guid.NewGuid(),  R1, new DateOnly(2026, 7, 1), workedMinutes: 120, pieceCount: 0, pieceUnitYen: 0, points: 10, note: null, "t", T);
        var wAbsent  = WorkRecord.NewRecord(Guid.NewGuid(),  R1, new DateOnly(2026, 7, 2), workedMinutes: 999, pieceCount: 9, pieceUnitYen: 99, points: 99, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(new[] { dPresent, dAbsent }, new[] { wPresent, wAbsent }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.PresentDays.Should().Be(1);
        i.TotalWorkedMinutes.Should().Be(120);
        i.TotalPieceAmountYen.Should().Be(0);
        i.TotalPoints.Should().Be(10);
    }

    [Fact]
    public void Excludes_work_records_on_absence_support_days()
    {
        var dSupport = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.AbsenceSupport, TransportKind.None, false, null, "t", T);
        var w        = WorkRecord.NewRecord(Guid.NewGuid(),  R1, new DateOnly(2026, 7, 1), workedMinutes: 60, pieceCount: 1, pieceUnitYen: 50, points: 5, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(new[] { dSupport }, new[] { w }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.PresentDays.Should().Be(0);
        i.TotalWorkedMinutes.Should().Be(0);
        i.TotalPieceAmountYen.Should().Be(0);
        i.TotalPoints.Should().Be(0);
    }

    [Fact]
    public void Excludes_work_records_on_dates_without_any_daily_record()
    {
        var w = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 5), workedMinutes: 300, pieceCount: 0, pieceUnitYen: 0, points: 0, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(Array.Empty<DailyRecord>(), new[] { w }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.RecipientId.Should().Be(R1);
        i.PresentDays.Should().Be(0);
        i.TotalWorkedMinutes.Should().Be(0);
    }

    [Fact]
    public void Excludes_work_records_on_days_whose_daily_record_was_cancelled()
    {
        var originalDailyId = Guid.NewGuid();
        var dNew    = DailyRecord.NewRecord(originalDailyId, R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var dCancel = DailyRecord.Cancellation(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), originalDailyId, "t", T.AddMinutes(1));
        var w       = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), workedMinutes: 200, pieceCount: 0, pieceUnitYen: 0, points: 0, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(new[] { dNew, dCancel }, new[] { w }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.PresentDays.Should().Be(0);
        i.TotalWorkedMinutes.Should().Be(0);
    }
}
