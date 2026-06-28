using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WorkRecordRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public WorkRecordRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_by_recipient_month_round_trip()
    {
        var rid = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new WorkRecordRepository(ctx);

        var rec = WorkRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 15),
            workedMinutes: 240, pieceCount: null, pieceUnitYen: null, points: null,
            note: null, createdBy: "t", createdAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(rec, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByRecipientAndMonthAsync(rid, 2026, 7, default);
        list.Should().HaveCount(1);
        list[0].WorkedMinutes.Should().Be(240);

        var other = await repo.ListByRecipientAndMonthAsync(rid, 2026, 8, default);
        other.Should().BeEmpty();
    }
}
