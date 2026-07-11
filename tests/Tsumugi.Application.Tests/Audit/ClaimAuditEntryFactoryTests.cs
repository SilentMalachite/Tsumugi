using FluentAssertions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Tests.Audit;

public sealed class ClaimAuditEntryFactoryTests
{
    [Fact]
    public void Create_uses_only_fixed_order_allowlist_and_stays_compact()
    {
        var payload = new ClaimAuditPayload(
            "claim-finalized", Guid.Parse("10000000-0000-0000-0000-000000000000"),
            Guid.Parse("20000000-0000-0000-0000-000000000000"),
            Guid.Parse("30000000-0000-0000-0000-000000000000"),
            new ServiceMonth(2026, 6), RecordKind.Correct, 123,
            Guid.Parse("40000000-0000-0000-0000-000000000000"), new string('a', 64));

        var entry = new ClaimAuditEntryFactory().Create(
            Guid.Parse("50000000-0000-0000-0000-000000000000"), "actor", payload,
            DateTimeOffset.UnixEpoch);

        entry.Summary.Should().Be(
            "eventCode=claim-finalized;batchId=10000000-0000-0000-0000-000000000000;operationId=20000000-0000-0000-0000-000000000000;officeId=30000000-0000-0000-0000-000000000000;serviceMonth=2026-06;kind=2;revision=123;rootId=40000000-0000-0000-0000-000000000000;operationHash=" + new string('a', 64));
        entry.Summary!.Length.Should().BeLessThanOrEqualTo(512);
        entry.Actor.Should().Be("actor");
        entry.TargetId.Should().Be(payload.BatchId);
        entry.Action.Should().Be(AuditAction.Register);
    }

    [Fact]
    public void Create_rejects_free_form_event_code()
    {
        var payload = new ClaimAuditPayload(
            "recipient-name", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new ServiceMonth(2026, 6), RecordKind.New, 1, null, new string('a', 64));

        FluentActions.Invoking(() => new ClaimAuditEntryFactory().Create(
                Guid.NewGuid(), "actor", payload, DateTimeOffset.UnixEpoch))
            .Should().Throw<ArgumentException>();
    }
}
