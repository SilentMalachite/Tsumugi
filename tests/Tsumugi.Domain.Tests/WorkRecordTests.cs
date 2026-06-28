using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WorkRecordTests
{
    private static readonly DateTimeOffset Clock = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewRecord_sets_kind_and_clears_origin()
    {
        var id = Guid.NewGuid();
        var rec = WorkRecord.NewRecord(
            id, recipientId: Guid.NewGuid(), workDate: new DateOnly(2026, 7, 1),
            workedMinutes: 240, pieceCount: null, pieceUnitYen: null, points: null,
            note: null, createdBy: "tester", createdAt: Clock);

        rec.Id.Should().Be(id);
        rec.Kind.Should().Be(RecordKind.New);
        rec.OriginId.Should().BeNull();
        rec.WorkedMinutes.Should().Be(240);
        rec.PieceCount.Should().BeNull();
    }

    [Fact]
    public void Correction_carries_origin()
    {
        var origin = Guid.NewGuid();
        var rec = WorkRecord.Correction(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 1), originId: origin,
            workedMinutes: 200, pieceCount: null, pieceUnitYen: null, points: null,
            note: "訂正", createdBy: "tester", createdAt: Clock);
        rec.Kind.Should().Be(RecordKind.Correct);
        rec.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancellation_zeroes_measurements()
    {
        var origin = Guid.NewGuid();
        var rec = WorkRecord.Cancellation(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 1), origin,
            createdBy: "tester", createdAt: Clock);
        rec.Kind.Should().Be(RecordKind.Cancel);
        rec.OriginId.Should().Be(origin);
        rec.WorkedMinutes.Should().BeNull();
        rec.PieceCount.Should().BeNull();
        rec.PieceUnitYen.Should().BeNull();
        rec.Points.Should().BeNull();
    }
}
