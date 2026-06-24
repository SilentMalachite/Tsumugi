using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OfficeRoundTripTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public OfficeRoundTripTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_apply_then_insert_then_read_round_trips()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Offices.Add(Office.Create(id, "1234567890", "つむぎ", "tester",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = _fixture.NewContext())
        {
            var loaded = await ctx.Offices.SingleAsync(o => o.Id == id);
            loaded.OfficeNumber.Should().Be("1234567890");
            loaded.ConcurrencyToken.Should().NotBe(Guid.Empty);
        }
    }

    [Fact]
    public async Task Concurrent_update_is_detected_by_token()
    {
        var id = Guid.NewGuid();
        await using (var seed = _fixture.NewContext())
        {
            seed.Offices.Add(Office.Create(id, "9000000000", "種", "u",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // 2つのコンテキストで同じ行を読み、片方を先に更新する。
        await using var ctxA = _fixture.NewContext();
        await using var ctxB = _fixture.NewContext();
        var a = await ctxA.Offices.SingleAsync(o => o.Id == id);
        var b = await ctxB.Offices.SingleAsync(o => o.Id == id);

        ctxA.Entry(a).Property(x => x.Name).CurrentValue = "A更新";
        await ctxA.SaveChangesAsync(CancellationToken.None); // トークンが変わる

        ctxB.Entry(b).Property(x => x.Name).CurrentValue = "B更新";
        var act = () => ctxB.SaveChangesAsync(CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
