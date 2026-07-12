using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class QueryIntensiveSupportEpisodeUseCaseTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000001101");
    private static readonly Guid RecipientId = Guid.Parse("00000000-0000-0000-0000-000000001102");
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public async Task Execute_returns_empty_history_after_successful_empty_load()
    {
        var repo = new FakeEpisodeRepository();
        var sut = new QueryIntensiveSupportEpisodeUseCase(repo);

        var result = await sut.ExecuteAsync(
            new QueryIntensiveSupportEpisodeRequest(OfficeId, RecipientId), default);

        result.RootId.Should().BeNull();
        result.CurrentHeadId.Should().BeNull();
        result.EffectiveHeadId.Should().BeNull();
        result.Revisions.Should().BeEmpty();
        repo.LastQuery.Should().Be((OfficeId, RecipientId));
    }

    [Fact]
    public async Task Execute_returns_every_revision_in_order_and_reentry_as_effective_head()
    {
        var rootId = Guid.Parse("00000000-0000-0000-0000-000000001201");
        var cancelId = Guid.Parse("00000000-0000-0000-0000-000000001202");
        var headId = Guid.Parse("00000000-0000-0000-0000-000000001203");
        var created = Revision(
            rootId, rootId, 1, RecordKind.New, null, new DateOnly(2026, 6, 1));
        var cancelled = Revision(cancelId, rootId, 2, RecordKind.Cancel, rootId, null);
        var reentered = Revision(
            headId, rootId, 3, RecordKind.Correct, cancelId, new DateOnly(2026, 7, 1));
        var repo = new FakeEpisodeRepository([reentered, created, cancelled]);

        var result = await new QueryIntensiveSupportEpisodeUseCase(repo).ExecuteAsync(
            new QueryIntensiveSupportEpisodeRequest(OfficeId, RecipientId), default);

        result.RootId.Should().Be(rootId);
        result.CurrentHeadId.Should().Be(headId);
        result.EffectiveHeadId.Should().Be(headId);
        result.Revisions.Select(item => item.Revision).Should().Equal(1, 2, 3);
        result.Revisions[0].Should().BeEquivalentTo(
            created, options => options.ExcludingMissingMembers());
        result.Revisions[1].Kind.Should().Be(RecordKind.Cancel);
        result.Revisions[1].StartDate.Should().BeNull();
        result.Revisions[2].Should().BeEquivalentTo(
            reentered, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Execute_keeps_cancel_as_current_head_and_clears_effective_head()
    {
        var rootId = Guid.Parse("00000000-0000-0000-0000-000000001301");
        var cancelId = Guid.Parse("00000000-0000-0000-0000-000000001302");
        var repo = new FakeEpisodeRepository([
            Revision(rootId, rootId, 1, RecordKind.New, null, new DateOnly(2026, 6, 1)),
            Revision(cancelId, rootId, 2, RecordKind.Cancel, rootId, null),
        ]);

        var result = await new QueryIntensiveSupportEpisodeUseCase(repo).ExecuteAsync(
            new QueryIntensiveSupportEpisodeRequest(OfficeId, RecipientId), default);

        result.CurrentHeadId.Should().Be(cancelId);
        result.EffectiveHeadId.Should().BeNull();
        result.Revisions.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Execute_rejects_empty_identity_without_loading(
        bool emptyOffice,
        bool emptyRecipient)
    {
        var repo = new FakeEpisodeRepository();
        var request = new QueryIntensiveSupportEpisodeRequest(
            emptyOffice ? Guid.Empty : OfficeId,
            emptyRecipient ? Guid.Empty : RecipientId);

        var act = () => new QueryIntensiveSupportEpisodeUseCase(repo)
            .ExecuteAsync(request, default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidRequest);
        repo.LoadCalls.Should().Be(0);
    }

    [Fact]
    public async Task Execute_rejects_corrupt_history_without_leaking_domain_message()
    {
        var rootId = Guid.Parse("00000000-0000-0000-0000-000000001401");
        var repo = new FakeEpisodeRepository([
            Revision(rootId, rootId, 2, RecordKind.New, null, new DateOnly(2026, 6, 1)),
        ]);

        var act = () => new QueryIntensiveSupportEpisodeUseCase(repo).ExecuteAsync(
            new QueryIntensiveSupportEpisodeRequest(OfficeId, RecipientId), default);

        var error = (await act.Should().ThrowAsync<ClaimInputQueryException>()).Which;
        error.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
        error.Message.Should().NotContain("Revision").And.NotContain("IntensiveSupportEpisode");
    }

    [Fact]
    public async Task Execute_rejects_multiple_root_histories_for_the_same_business_key()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000001501");
        var second = Guid.Parse("00000000-0000-0000-0000-000000001502");
        var repo = new FakeEpisodeRepository([
            Revision(first, first, 1, RecordKind.New, null, new DateOnly(2026, 6, 1)),
            Revision(second, second, 1, RecordKind.New, null, new DateOnly(2026, 7, 1)),
        ]);

        var act = () => new QueryIntensiveSupportEpisodeUseCase(repo).ExecuteAsync(
            new QueryIntensiveSupportEpisodeRequest(OfficeId, RecipientId), default);

        (await act.Should().ThrowAsync<ClaimInputQueryException>())
            .Which.Code.Should().Be(ClaimInputQueryErrorCode.InvalidHistory);
    }

    private static IntensiveSupportEpisode Revision(
        Guid id,
        Guid rootId,
        int revision,
        RecordKind kind,
        Guid? expectedHeadId,
        DateOnly? startDate) =>
        new()
        {
            Id = id,
            OfficeId = OfficeId,
            RecipientId = RecipientId,
            RootId = rootId,
            Revision = revision,
            Kind = kind,
            ExpectedHeadId = expectedHeadId,
            StartDate = startDate,
            CreatedAt = Now.AddMinutes(revision),
            CreatedBy = $"operator-{revision}",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private sealed class FakeEpisodeRepository : IIntensiveSupportEpisodeRepository
    {
        public FakeEpisodeRepository(IEnumerable<IntensiveSupportEpisode>? seed = null)
        {
            Items = seed?.ToList() ?? [];
        }

        public List<IntensiveSupportEpisode> Items { get; }
        public int LoadCalls { get; private set; }
        public (Guid, Guid)? LastQuery { get; private set; }

        public Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
            Guid officeId, Guid recipientId, CancellationToken ct)
        {
            LoadCalls++;
            LastQuery = (officeId, recipientId);
            return Task.FromResult<IReadOnlyList<IntensiveSupportEpisode>>(Items);
        }
    }
}
