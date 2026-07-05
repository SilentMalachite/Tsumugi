using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardPhase4Tests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public AppendOnlyGuardPhase4Tests(SqliteFixture f) => _fixture = f;

    [Fact]
    public void Append_only_types_include_recipient_hourly_rate()
    {
        AppendOnlyGuard.GetAppendOnlyTypesForTests().Should().Contain(typeof(RecipientHourlyRate));
    }

    // NOTE(teeth): If AppendOnlyGuard.Inspect() stops covering RecipientHourlyRate,
    // this test goes RED — proving the guard is wired and the test has real teeth.
    [Fact]
    public async Task Modifying_RecipientHourlyRate_throws()
    {
        var id = Guid.NewGuid();
        var period = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30));

        await using var ctx = _fixture.NewContext();
        ctx.Set<RecipientHourlyRate>().Add(
            RecipientHourlyRate.NewRecord(
                id, Guid.NewGuid(), Guid.NewGuid(), period,
                1000, "u", DateTimeOffset.UtcNow));
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Set<RecipientHourlyRate>().SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(RecipientHourlyRate.HourlyYen)).CurrentValue = 999;
        ctx.Entry(loaded).Property(nameof(RecipientHourlyRate.HourlyYen)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(e => e.EntityName == nameof(RecipientHourlyRate));
    }
}
