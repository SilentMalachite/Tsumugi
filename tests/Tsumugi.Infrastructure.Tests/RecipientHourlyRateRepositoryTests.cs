using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class RecipientHourlyRateRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public RecipientHourlyRateRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_returns_by_office_and_recipient()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var period = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30));

        await using var ctx = _fixture.NewContext();
        var repo = new RecipientHourlyRateRepository(ctx);

        var rate = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period, 1200,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(rate, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeRecipientAsync(officeId, recipientId, default);
        var loaded = list.Should().ContainSingle().Subject;
        loaded.HourlyYen.Should().Be(1200);
        loaded.Period.Start.Should().Be(new DateOnly(2026, 4, 1));
        loaded.Period.End.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task Add_open_ended_period_round_trips_null_end()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var period = new DateRange(new DateOnly(2026, 7, 1), null);

        await using var ctx = _fixture.NewContext();
        var repo = new RecipientHourlyRateRepository(ctx);

        var rate = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period, 1500,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(rate, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeRecipientAsync(officeId, recipientId, default);
        var loaded = list.Should().ContainSingle().Subject;
        loaded.Period.Start.Should().Be(new DateOnly(2026, 7, 1));
        loaded.Period.End.Should().BeNull();
    }

    [Fact]
    public async Task Two_new_records_with_different_period_starts_succeed()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var period1 = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        var period2 = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        await using var ctx = _fixture.NewContext();
        var repo = new RecipientHourlyRateRepository(ctx);

        var r1 = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period1, 1000,
            "u", DateTimeOffset.UtcNow);
        var r2 = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period2, 1200,
            "u", DateTimeOffset.UtcNow);

        await repo.AddAsync(r1, default);
        await repo.AddAsync(r2, default);
        var savedRows = await ctx.SaveChangesAsync();

        savedRows.Should().Be(2);
    }

    [Fact]
    public async Task Duplicate_new_is_rejected_by_partial_unique_index()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var period = new DateRange(new DateOnly(2026, 7, 1), null);

        await using var ctx = _fixture.NewContext();
        var repo = new RecipientHourlyRateRepository(ctx);

        var first = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period, 1000,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(first, default);
        await ctx.SaveChangesAsync();

        var dup = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), officeId, recipientId, period, 1500,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(dup, default);

        var act = async () => await ctx.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }
}
