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
        list.Should().ContainSingle().Which.HourlyYen.Should().Be(1200);
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
