using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WageFundDuplicateNewIndexTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public WageFundDuplicateNewIndexTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Duplicate_new_record_for_same_office_and_month_is_rejected_by_index()
    {
        var officeId = Guid.NewGuid();
        var month = new YearMonth(2026, 7);
        await using var ctx = _fixture.NewContext();

        var first = WageFund.NewRecord(
            Guid.NewGuid(), officeId, month, totalYen: 100_000,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(first);
        await ctx.SaveChangesAsync();

        var duplicate = WageFund.NewRecord(
            Guid.NewGuid(), officeId, month, totalYen: 50_000,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(duplicate);

        var act = async () => await ctx.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }

    [Fact]
    public async Task Correction_record_for_same_office_and_month_is_allowed()
    {
        var officeId = Guid.NewGuid();
        var month = new YearMonth(2026, 7);
        await using var ctx = _fixture.NewContext();

        var newRec = WageFund.NewRecord(
            Guid.NewGuid(), officeId, month, totalYen: 100_000,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(newRec);
        await ctx.SaveChangesAsync();

        var correction = WageFund.Correction(
            Guid.NewGuid(), officeId, month, originId: newRec.Id, totalYen: 120_000,
            note: "訂正", createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(correction);

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}
