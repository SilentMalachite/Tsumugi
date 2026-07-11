using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class IntensiveSupportEpisodePolicyTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000201");
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000000202");
    private static readonly Guid RootId = Guid.Parse("00000000-0000-0000-0000-000000000203");

    [Fact]
    public void Empty_history_has_no_effective_episode_and_next_revision_is_one()
    {
        IntensiveSupportEpisodePolicy.ValidateHistory([]);

        IntensiveSupportEpisodePolicy.Effective([]).Should().BeNull();
        IntensiveSupportEpisodePolicy.NextRevision([]).Should().Be(1);
    }

    [Fact]
    public void New_correction_cancel_and_correction_reentry_form_a_valid_chain()
    {
        var root = New();
        var correction = Correct(root) with { StartDate = new DateOnly(2026, 7, 2) };
        var cancellation = Cancel(correction);
        var reentry = Correct(cancellation) with { StartDate = new DateOnly(2026, 7, 3) };

        var history = new[] { reentry, root, cancellation, correction };

        FluentActions.Invoking(() => IntensiveSupportEpisodePolicy.ValidateHistory(history))
            .Should().NotThrow();
        IntensiveSupportEpisodePolicy.Effective(history).Should().Be(reentry);
        IntensiveSupportEpisodePolicy.NextRevision(history).Should().Be(5);
    }

    [Fact]
    public void Cancel_head_has_no_effective_episode()
    {
        var root = New();
        var cancellation = Cancel(root);

        IntensiveSupportEpisodePolicy.Effective([root, cancellation]).Should().BeNull();
    }

    [Theory]
    [InlineData(RecordKind.New)]
    [InlineData(RecordKind.Correct)]
    public void New_and_correction_require_start_date(RecordKind kind)
    {
        var root = New();
        var history = kind == RecordKind.New
            ? new[] { root with { StartDate = null } }
            : new[] { root, Correct(root) with { StartDate = null } };

        FluentActions.Invoking(() => IntensiveSupportEpisodePolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_rejects_start_date()
    {
        var root = New();
        var cancellation = Cancel(root) with { StartDate = root.StartDate };

        FluentActions.Invoking(
                () => IntensiveSupportEpisodePolicy.ValidateHistory([root, cancellation]))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_without_start_date_is_valid()
    {
        var root = New();
        var cancellation = Cancel(root);

        FluentActions.Invoking(
                () => IntensiveSupportEpisodePolicy.ValidateHistory([root, cancellation]))
            .Should().NotThrow();
    }

    public static TheoryData<string, IReadOnlyCollection<IntensiveSupportEpisode>> InvalidHistories()
    {
        var root = New();
        var correction = Correct(root);
        var otherRootId = Guid.Parse("10000000-0000-0000-0000-000000000203");

        return new()
        {
            { "missing revision", new[] { root, correction with { Revision = 3 } } },
            { "duplicate revision and branch", new[] { root, correction, Correct(root) } },
            { "duplicate id", new[] { root, correction with { Id = root.Id } } },
            { "root id mismatch", new[] { root, correction with { RootId = otherRootId } } },
            { "expected head mismatch", new[] { root, correction with { ExpectedHeadId = Guid.NewGuid() } } },
            { "root is not self", new[] { root with { RootId = otherRootId } } },
            { "office id changed", new[] { root, correction with { OfficeId = Guid.NewGuid() } } },
            { "recipient id changed", new[] { root, correction with { RecipientId = Guid.NewGuid() } } },
            { "new after root", new[] { root, correction with { Kind = RecordKind.New } } },
            { "unknown kind", new[] { root, correction with { Kind = (RecordKind)999 } } },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidHistories))]
    public void Invalid_history_is_rejected(
        string _,
        IReadOnlyCollection<IntensiveSupportEpisode> history)
    {
        FluentActions.Invoking(() => IntensiveSupportEpisodePolicy.ValidateHistory(history))
            .Should().Throw<InvalidOperationException>();
    }

    private static IntensiveSupportEpisode New() => new()
    {
        Id = RootId,
        OfficeId = OfficeId,
        RecipientId = RecipientId,
        RootId = RootId,
        Revision = 1,
        Kind = RecordKind.New,
        ExpectedHeadId = null,
        StartDate = new DateOnly(2026, 7, 1),
        CreatedAt = DateTimeOffset.UnixEpoch,
        CreatedBy = "tester",
        ConcurrencyToken = Guid.Parse("00000000-0000-0000-0000-000000000204"),
    };

    private static IntensiveSupportEpisode Correct(IntensiveSupportEpisode head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Correct,
        ExpectedHeadId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private static IntensiveSupportEpisode Cancel(IntensiveSupportEpisode head) => head with
    {
        Id = Guid.NewGuid(),
        RootId = head.RootId,
        Revision = head.Revision + 1,
        Kind = RecordKind.Cancel,
        ExpectedHeadId = head.Id,
        StartDate = null,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };
}
