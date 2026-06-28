using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class AuditEntryTests
{
    [Fact]
    public void Create_records_all_fields()
    {
        var t = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var target = Guid.NewGuid();
        var e = AuditEntry.Create(
            Guid.NewGuid(), actor: "alice", action: AuditAction.Update,
            targetType: "Office", targetId: target,
            occurredAt: t, summary: "officeName changed", createdAt: t, createdBy: "alice");
        e.Actor.Should().Be("alice");
        e.Action.Should().Be(AuditAction.Update);
        e.TargetType.Should().Be("Office");
        e.TargetId.Should().Be(target);
        e.OccurredAt.Should().Be(t);
        e.Summary.Should().Be("officeName changed");
    }
}
