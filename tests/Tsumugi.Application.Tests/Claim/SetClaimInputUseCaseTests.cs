using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class SetClaimInputUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 1, 2, 3, TimeSpan.Zero);
    private static readonly ServiceMonth Month = new(2026, 6);

    [Fact]
    public async Task Execute_appends_new_correction_cancel_and_reentry_as_distinct_revisions()
    {
        var repo = new FakeClaimInputRepository();
        var uow = new FakeUnitOfWork();
        var sut = new SetClaimInputUseCase(repo, uow, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, expectedHeadId: null),
            "operator", default);
        var corrected = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, created.Id) with
            {
                UpperLimitManagedAmountYen = 1_500,
            },
            "operator", default);
        var cancelled = await sut.ExecuteAsync(
            CancelRequest(officeId, recipientId, corrected.Id),
            "operator", default);
        var reentered = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, cancelled.Id) with
            {
                UpperLimitManagedAmountYen = 2_000,
            },
            "operator", default);

        repo.Items.Select(item => item.Revision).Should().Equal(1, 2, 3, 4);
        repo.Items.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        repo.Items.Select(item => item.RootId).Should().OnlyContain(rootId => rootId == created.RootId);
        repo.Items[2].Kind.Should().Be(RecordKind.Cancel);
        repo.Items[2].UpperLimitManagementResult.Should().BeNull();
        repo.Items[3].ExpectedHeadId.Should().Be(cancelled.Id);
        repo.Items[3].UpperLimitManagedAmountYen.Should().Be(2_000);
        repo.Items.Should().OnlyContain(item => item.CreatedAt == Now && item.CreatedBy == "operator");
        reentered.Revision.Should().Be(4);
        uow.SaveCalls.Should().Be(4);
    }

    [Fact]
    public async Task Execute_rejects_missing_and_stale_expected_head_with_closed_errors()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);

        var missing = () => sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, null), "operator", default);
        var stale = () => sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, Guid.NewGuid()),
            "operator", default);

        (await missing.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Should().Match<ClaimInputSaveException>(error =>
                error.Code == ClaimInputSaveErrorCode.ExpectedHeadRequired
                && error.FieldCode == ClaimInputFieldCode.ExpectedHead);
        (await stale.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Should().Match<ClaimInputSaveException>(error =>
                error.Code == ClaimInputSaveErrorCode.ExpectedHeadMismatch
                && error.FieldCode == ClaimInputFieldCode.ExpectedHead);
        repo.Items.Should().ContainSingle(item => item.Id == created.Id);
    }

    [Fact]
    public async Task Execute_rejects_expected_head_from_another_root()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var selectedRecipientId = Guid.NewGuid();
        var selected = await sut.ExecuteAsync(
            ValidRequest(officeId, selectedRecipientId, RecordKind.New, null),
            "operator", default);
        var other = await sut.ExecuteAsync(
            ValidRequest(officeId, Guid.NewGuid(), RecordKind.New, null),
            "operator", default);

        var act = () => sut.ExecuteAsync(
            ValidRequest(officeId, selectedRecipientId, RecordKind.Correct, other.Id),
            "operator", default);

        (await act.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Code.Should().Be(ClaimInputSaveErrorCode.ExpectedHeadMismatch);
        repo.Items.Should().ContainSingle(item => item.RootId == selected.RootId);
    }

    [Fact]
    public async Task Execute_rejects_empty_identity_with_closed_field_code()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));

        var act = () => sut.ExecuteAsync(
            ValidRequest(Guid.Empty, Guid.NewGuid(), RecordKind.New, null),
            "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Identity);
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_empty_actor_without_echoing_it()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));

        var act = () => sut.ExecuteAsync(
            ValidRequest(Guid.NewGuid(), Guid.NewGuid(), RecordKind.New, null),
            " ", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Actor);
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_corrupt_existing_history_with_sanitized_error()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);
        repo.Items[0] = repo.Items[0] with { Revision = 2 };

        var act = () => sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.Correct, created.Id),
            "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidHistory);
        error.FieldCode.Should().Be(ClaimInputFieldCode.History);
        error.Message.Should().NotContain("Revision");
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_rejects_unknown_record_kind()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var request = ValidRequest(
            Guid.NewGuid(), Guid.NewGuid(), (RecordKind)999, expectedHeadId: null);

        var act = () => sut.ExecuteAsync(request, "operator", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidRequest);
        error.FieldCode.Should().Be(ClaimInputFieldCode.RecordKind);
        error.Message.Should().NotContain("999");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_rejects_replay_instead_of_reusing_the_previous_operation()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var created = await sut.ExecuteAsync(
            ValidRequest(officeId, recipientId, RecordKind.New, null), "operator", default);
        var request = ValidRequest(officeId, recipientId, RecordKind.Correct, created.Id);
        await sut.ExecuteAsync(request, "operator", default);

        var replay = () => sut.ExecuteAsync(request, "operator", default);

        (await replay.Should().ThrowAsync<ClaimInputSaveException>())
            .Which.Code.Should().Be(ClaimInputSaveErrorCode.ExpectedHeadMismatch);
        repo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_rejects_cross_field_values_without_echoing_input()
    {
        var repo = new FakeClaimInputRepository();
        var sut = new SetClaimInputUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Now));
        var request = ValidRequest(Guid.NewGuid(), Guid.NewGuid(), RecordKind.New, null) with
        {
            ExceptionalUsageEndMonth = null,
            MunicipalSubsidyAmountYen = 987_654,
        };

        var act = () => sut.ExecuteAsync(request, "operator-secret", default);

        var error = (await act.Should().ThrowAsync<ClaimInputSaveException>()).Which;
        error.Code.Should().Be(ClaimInputSaveErrorCode.InvalidValue);
        error.FieldCode.Should().Be(ClaimInputFieldCode.Values);
        error.Message.Should().NotContain("987654").And.NotContain("operator-secret");
        repo.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Intensive_support_episode_allows_cancelled_history_to_be_reentered()
    {
        var repo = new FakeIntensiveSupportEpisodeRepository();
        var uow = new FakeUnitOfWork();
        var sut = new SetIntensiveSupportEpisodeUseCase(
            repo, uow, new FixedTimeProvider(Now));
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var created = await sut.ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                officeId, recipientId, RecordKind.New, null, new DateOnly(2026, 6, 1)),
            "operator", default);
        var cancelled = await sut.ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                officeId, recipientId, RecordKind.Cancel, created.Id, null),
            "operator", default);
        var reentered = await sut.ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                officeId, recipientId, RecordKind.Correct, cancelled.Id, new DateOnly(2026, 7, 1)),
            "operator", default);

        repo.Items.Select(item => item.Kind).Should()
            .Equal(RecordKind.New, RecordKind.Cancel, RecordKind.Correct);
        reentered.Revision.Should().Be(3);
        repo.Items[^1].StartDate.Should().Be(new DateOnly(2026, 7, 1));
        uow.SaveCalls.Should().Be(3);
    }

    private static SetClaimInputRequest ValidRequest(
        Guid officeId,
        Guid recipientId,
        RecordKind kind,
        Guid? expectedHeadId) =>
        new(officeId, recipientId, Month, kind, expectedHeadId)
        {
            UpperLimitManagementResult = UpperLimitManagementResult.Result2,
            UpperLimitManagedAmountYen = 1_000,
            MunicipalSubsidyAmountYen = 500,
            ExceptionalUsageStartMonth = Month,
            ExceptionalUsageEndMonth = Month,
            ExceptionalUsageDays = 10,
            StandardUsageDayTotal = 22,
        };

    private static SetClaimInputRequest CancelRequest(
        Guid officeId,
        Guid recipientId,
        Guid expectedHeadId) =>
        new(officeId, recipientId, Month, RecordKind.Cancel, expectedHeadId);

    private sealed class FakeClaimInputRepository : IClaimInputRepository
    {
        public List<ClaimInput> Items { get; } = [];

        public Task AddAsync(ClaimInput input, CancellationToken ct)
        {
            Items.Add(input);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
            Guid officeId,
            Guid recipientId,
            ServiceMonth serviceMonth,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClaimInput>>(Items
                .Where(item => item.OfficeId == officeId
                               && item.RecipientId == recipientId
                               && item.ServiceMonth == serviceMonth)
                .ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeIntensiveSupportEpisodeRepository
        : IIntensiveSupportEpisodeRepository
    {
        public List<IntensiveSupportEpisode> Items { get; } = [];

        public Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct)
        {
            Items.Add(episode);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
            Guid officeId,
            Guid recipientId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IntensiveSupportEpisode>>(Items
                .Where(item => item.OfficeId == officeId && item.RecipientId == recipientId)
                .ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
