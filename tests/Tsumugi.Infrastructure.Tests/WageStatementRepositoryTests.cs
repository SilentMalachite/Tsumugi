using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WageStatementRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public WageStatementRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_by_office_month_round_trip()
    {
        var officeId = Guid.NewGuid();
        var rid = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new WageStatementRepository(ctx);

        var s = WageStatement.NewRecord(Guid.NewGuid(), officeId, new YearMonth(2026, 7), rid,
            amountYen: 12_345, basisSummary: "時間割: 600分", createdBy: "t", createdAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(s, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeAndMonthAsync(officeId, 2026, 7, default);
        list.Should().HaveCount(1);
        list[0].AmountYen.Should().Be(12_345);
        list[0].Month.Should().Be(new YearMonth(2026, 7));
    }

    [Fact]
    public async Task Duplicate_new_statement_for_same_office_month_recipient_rejected_by_partial_unique_index()
    {
        var officeId = Guid.NewGuid();
        var rid = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new WageStatementRepository(ctx);

        var first = WageStatement.NewRecord(Guid.NewGuid(), officeId, new YearMonth(2026, 7), rid,
            10_000, "first", "t", DateTimeOffset.UtcNow);
        await repo.AddAsync(first, default);
        await ctx.SaveChangesAsync();

        var dup = WageStatement.NewRecord(Guid.NewGuid(), officeId, new YearMonth(2026, 7), rid,
            20_000, "second", "t", DateTimeOffset.UtcNow);
        await repo.AddAsync(dup, default);

        var act = async () => await ctx.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }
}
