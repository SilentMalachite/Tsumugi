using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class DailyRecordDuplicateNewIndexTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public DailyRecordDuplicateNewIndexTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Duplicate_new_record_for_same_recipient_and_date_is_rejected_by_index()
    {
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 1);
        await using var ctx = _fixture.NewContext();

        var first = DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, date,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.DailyRecords.Add(first);
        await ctx.SaveChangesAsync();

        var duplicate = DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, date,
            Attendance.Absent, TransportKind.None, mealProvided: false,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.DailyRecords.Add(duplicate);

        var act = async () => await ctx.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }

    [Fact]
    public async Task Correction_record_for_same_date_is_allowed()
    {
        var recipientId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 2);
        await using var ctx = _fixture.NewContext();

        var newRec = DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, date,
            Attendance.Present, TransportKind.None, false, null,
            "tester", DateTimeOffset.UtcNow);
        ctx.DailyRecords.Add(newRec);
        await ctx.SaveChangesAsync();

        var correction = DailyRecord.Correction(
            Guid.NewGuid(), recipientId, date, newRec.Id,
            Attendance.Absent, TransportKind.None, false, "訂正",
            "tester", DateTimeOffset.UtcNow);
        ctx.DailyRecords.Add(correction);

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}
