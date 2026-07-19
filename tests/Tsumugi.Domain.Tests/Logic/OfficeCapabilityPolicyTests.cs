using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic;

/// <summary>
/// ADR 0021「Phase 3-1の実効レコード選定」の固定:
/// <c>Period.Contains(asOf)</c>候補を Period.Start 降順→CreatedAt 降順で選び、
/// 先頭が一意に決まらなければ曖昧（算定不能）としてフォールバックしない。
/// </summary>
public sealed class OfficeCapabilityPolicyTests
{
    private static readonly Guid OfficeId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateOnly AsOf = new(2025, 4, 1);

    private static OfficeCapability Capability(
        DateOnly start, DateOnly? end, DateTimeOffset createdAt, string flagKey = "cap.x") =>
        OfficeCapability.Create(
            Guid.NewGuid(), OfficeId, new DateRange(start, end),
            new Dictionary<string, bool> { [flagKey] = true },
            "tester", createdAt, Guid.NewGuid());

    [Fact]
    public void Returns_no_effective_record_when_nothing_covers_the_month()
    {
        var records = new[]
        {
            Capability(new(2024, 4, 1), new(2024, 12, 31), DateTimeOffset.UnixEpoch),
        };

        var resolution = OfficeCapabilityPolicy.Resolve(records, AsOf);

        resolution.Effective.Should().BeNull();
        resolution.IsAmbiguous.Should().BeFalse();
    }

    [Fact]
    public void Later_period_start_supersedes_an_earlier_open_ended_record()
    {
        var older = Capability(new(2024, 4, 1), null, DateTimeOffset.UnixEpoch);
        var newer = Capability(new(2025, 4, 1), null, DateTimeOffset.UnixEpoch, "cap.y");

        var resolution = OfficeCapabilityPolicy.Resolve([older, newer], AsOf);

        resolution.Effective.Should().BeSameAs(newer);
    }

    [Fact]
    public void Later_append_supersedes_an_earlier_record_with_the_same_period_start()
    {
        var first = Capability(new(2025, 4, 1), null, DateTimeOffset.UnixEpoch);
        var second = Capability(
            new(2025, 4, 1), null, DateTimeOffset.UnixEpoch.AddDays(1), "cap.y");

        var resolution = OfficeCapabilityPolicy.Resolve([first, second], AsOf);

        resolution.Effective.Should().BeSameAs(second);
    }

    [Fact]
    public void Ties_on_period_start_and_created_at_are_ambiguous_and_fail_closed()
    {
        var first = Capability(new(2025, 4, 1), null, DateTimeOffset.UnixEpoch);
        var second = Capability(new(2025, 4, 1), null, DateTimeOffset.UnixEpoch, "cap.y");

        var resolution = OfficeCapabilityPolicy.Resolve([first, second], AsOf);

        resolution.Effective.Should().BeNull();
        resolution.IsAmbiguous.Should().BeTrue();
    }
}
