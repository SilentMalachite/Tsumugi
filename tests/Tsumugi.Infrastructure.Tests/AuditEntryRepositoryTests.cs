using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AuditEntryRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public AuditEntryRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_by_target_round_trip()
    {
        var targetId = Guid.NewGuid();
        var occurred = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);
        await using var ctx = _fixture.NewContext();
        var repo = new AuditEntryRepository(ctx);

        var e = AuditEntry.Create(
            Guid.NewGuid(), actor: "alice", action: AuditAction.Update,
            targetType: "Office", targetId: targetId,
            occurredAt: occurred, summary: "name changed",
            createdAt: occurred, createdBy: "alice");
        await repo.AddAsync(e, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByTargetAsync("Office", targetId, default);
        list.Should().HaveCount(1);
        list[0].Actor.Should().Be("alice");
        list[0].Action.Should().Be(AuditAction.Update);
        list[0].Summary.Should().Be("name changed");

        var none = await repo.ListByTargetAsync("Recipient", targetId, default);
        none.Should().BeEmpty();
    }
}
