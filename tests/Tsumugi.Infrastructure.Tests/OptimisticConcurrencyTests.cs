using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OptimisticConcurrencyTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public OptimisticConcurrencyTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Concurrent_Office_update_throws_DbUpdateConcurrencyException()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Offices.Add(Office.Create(id, "1", "x",
                ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }

        await using var ctxA = _fixture.NewContext();
        await using var ctxB = _fixture.NewContext();
        var a = await ctxA.Offices.SingleAsync(o => o.Id == id);
        var b = await ctxB.Offices.SingleAsync(o => o.Id == id);

        // ctxA wins the race: update Name AND rotate ConcurrencyToken so the DB value changes
        ctxA.Entry(a).Property(nameof(Office.Name)).CurrentValue = "A";
        ctxA.Entry(a).Property(nameof(Office.Name)).IsModified = true;
        ctxA.Entry(a).Property(nameof(Office.ConcurrencyToken)).CurrentValue = Guid.NewGuid();
        ctxA.Entry(a).Property(nameof(Office.ConcurrencyToken)).IsModified = true;
        await ctxA.SaveChangesAsync();

        // ctxB still holds the stale ConcurrencyToken — must throw
        ctxB.Entry(b).Property(nameof(Office.Name)).CurrentValue = "B";
        ctxB.Entry(b).Property(nameof(Office.Name)).IsModified = true;

        Func<Task> act = () => ctxB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
