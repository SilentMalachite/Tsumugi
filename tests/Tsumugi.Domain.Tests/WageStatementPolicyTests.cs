using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests;

public sealed class WageStatementPolicyTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);

    private static WageStatement New(Guid id, Guid rid, int yen, DateTimeOffset createdAt) =>
        WageStatement.NewRecord(id, Office, Month, rid, yen, "基礎要約", "tester", createdAt);

    private static WageStatement Correct(Guid id, Guid rid, Guid originId, int yen, DateTimeOffset createdAt) =>
        WageStatement.Correction(id, Office, Month, rid, originId, yen, "訂正要約", "tester", createdAt);

    private static WageStatement Cancel(Guid id, Guid rid, Guid originId, DateTimeOffset createdAt) =>
        new()
        {
            Id = id,
            OfficeId = Office,
            Month = Month,
            RecipientId = rid,
            AmountYen = 0,
            BasisSummary = "取消",
            Kind = RecordKind.Cancel,
            OriginId = originId,
            CreatedBy = "tester",
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    [Fact]
    public void Effective_returns_null_for_empty_input()
    {
        WageStatementPolicy.Effective(Array.Empty<WageStatement>()).Should().BeNull();
    }

    [Fact]
    public void Effective_returns_null_when_no_new_origin_present()
    {
        var orphan = Correct(Guid.NewGuid(), R1, originId: Guid.NewGuid(), 1000, T);
        WageStatementPolicy.Effective(new[] { orphan }).Should().BeNull();
    }

    [Fact]
    public void Effective_returns_origin_when_no_correction_or_cancel_follows()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var result = WageStatementPolicy.Effective(new[] { origin });
        result.Should().NotBeNull();
        result!.Id.Should().Be(origin.Id);
        result.AmountYen.Should().Be(10_000);
    }

    [Fact]
    public void Effective_returns_latest_correction_chain()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var c1 = Correct(Guid.NewGuid(), R1, origin.Id, 11_000, T.AddMinutes(1));
        var c2 = Correct(Guid.NewGuid(), R1, c1.Id, 12_000, T.AddMinutes(2));
        var result = WageStatementPolicy.Effective(new[] { origin, c1, c2 });
        result.Should().NotBeNull();
        result!.Id.Should().Be(c2.Id);
        result.AmountYen.Should().Be(12_000);
    }

    [Fact]
    public void Effective_returns_null_when_chain_ends_with_cancel()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var cancel = Cancel(Guid.NewGuid(), R1, origin.Id, T.AddMinutes(1));
        WageStatementPolicy.Effective(new[] { origin, cancel }).Should().BeNull();
    }

    [Fact]
    public void Effective_picks_latest_correction_when_multiple_children_same_origin()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var c_old = Correct(Guid.NewGuid(), R1, origin.Id, 11_000, T.AddMinutes(1));
        var c_new = Correct(Guid.NewGuid(), R1, origin.Id, 13_000, T.AddMinutes(2));
        var result = WageStatementPolicy.Effective(new[] { origin, c_old, c_new });
        result.Should().NotBeNull();
        result!.Id.Should().Be(c_new.Id);
        result.AmountYen.Should().Be(13_000);
    }

    [Fact]
    public void EffectiveByRecipient_groups_per_recipient_and_skips_cancelled_chains()
    {
        var o1 = New(Guid.NewGuid(), R1, 10_000, T);
        var o2 = New(Guid.NewGuid(), R2, 20_000, T);
        var cancel2 = Cancel(Guid.NewGuid(), R2, o2.Id, T.AddMinutes(1));
        var result = WageStatementPolicy.EffectiveByRecipient(new[] { o1, o2, cancel2 });
        result.Should().HaveCount(1);
        result.Should().ContainKey(R1);
        result[R1].AmountYen.Should().Be(10_000);
        result.Should().NotContainKey(R2);
    }

    [Fact]
    public void Effective_and_EffectiveByRecipient_throw_on_null_input()
    {
        FluentActions.Invoking(() => WageStatementPolicy.Effective(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => WageStatementPolicy.EffectiveByRecipient(null!))
            .Should().Throw<ArgumentNullException>();
    }
}
