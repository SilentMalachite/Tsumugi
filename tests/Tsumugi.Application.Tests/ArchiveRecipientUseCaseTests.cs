using FluentAssertions;
using Tsumugi.Application;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Tests;

public sealed class ArchiveRecipientUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 28, 0, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public async Task Archives_recipient_sets_metadata_and_excludes_from_default_list()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var token = Guid.NewGuid();
        var r = Recipient.Create(Guid.NewGuid(), "削除一郎", "サクジョイチロウ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, token);
        repo.Added.Add(r);

        var sut = new ArchiveRecipientUseCase(repo, uow, new FixedClock(), new NoopAuditTrail());

        await sut.ExecuteAsync(r.Id, token, "operator", default);

        var stored = repo.Added.Single();
        stored.IsArchived.Should().BeTrue();
        stored.ArchivedAt.Should().Be(Now);
        stored.ArchivedBy.Should().Be("operator");

        var defaultList = await repo.ListAsync(includeArchived: false, default);
        defaultList.Should().BeEmpty();
        var fullList = await repo.ListAsync(includeArchived: true, default);
        fullList.Should().ContainSingle();
    }

    [Fact]
    public async Task Throws_on_concurrency_token_mismatch()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var r = Recipient.Create(Guid.NewGuid(), "ね", "ネ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        repo.Added.Add(r);

        var sut = new ArchiveRecipientUseCase(repo, uow, new FixedClock(), new NoopAuditTrail());

        var act = () => sut.ExecuteAsync(r.Id, Guid.NewGuid(), "operator", default);
        await act.Should().ThrowAsync<OptimisticConcurrencyException>();
    }

    [Fact]
    public async Task Idempotent_when_already_archived()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var token = Guid.NewGuid();
        var r = Recipient.Create(Guid.NewGuid(), "ね", "ネ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, token)
            .Archive("first", DateTimeOffset.UnixEpoch.AddDays(1));
        repo.Added.Add(r);
        var originalArchivedAt = r.ArchivedAt;

        var sut = new ArchiveRecipientUseCase(repo, uow, new FixedClock(), new NoopAuditTrail());
        await sut.ExecuteAsync(r.Id, token, "second", default);

        repo.Added.Single().ArchivedAt.Should().Be(originalArchivedAt);
        repo.Added.Single().ArchivedBy.Should().Be("first");
    }

    [Fact]
    public async Task Rejects_empty_id_and_blank_actor()
    {
        var sut = new ArchiveRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork(), new FixedClock(), new NoopAuditTrail());

        await sut.Invoking(s => s.ExecuteAsync(Guid.Empty, Guid.NewGuid(), "u", default))
            .Should().ThrowAsync<ArgumentException>();
        await sut.Invoking(s => s.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), "  ", default))
            .Should().ThrowAsync<ArgumentException>();
    }
}

public sealed class RestoreRecipientUseCaseTests
{
    [Fact]
    public async Task Restores_archived_recipient_back_into_default_list()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var token = Guid.NewGuid();
        var r = Recipient.Create(Guid.NewGuid(), "復元二郎", "フクゲンジロウ",
            new DateOnly(1985, 8, 8), "u", DateTimeOffset.UnixEpoch, token)
            .Archive("u", DateTimeOffset.UnixEpoch.AddDays(1));
        repo.Added.Add(r);

        var sut = new RestoreRecipientUseCase(repo, uow,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());
        await sut.ExecuteAsync(r.Id, token, "operator", default);

        var stored = repo.Added.Single();
        stored.IsArchived.Should().BeFalse();
        stored.ArchivedAt.Should().BeNull();
        stored.ArchivedBy.Should().BeNull();

        var defaultList = await repo.ListAsync(includeArchived: false, default);
        defaultList.Should().ContainSingle();
    }

    [Fact]
    public async Task Throws_on_concurrency_token_mismatch()
    {
        var repo = new FakeRecipientRepository();
        var r = Recipient.Create(Guid.NewGuid(), "ね", "ネ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid())
            .Archive("u", DateTimeOffset.UnixEpoch.AddDays(1));
        repo.Added.Add(r);

        var sut = new RestoreRecipientUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());
        var act = () => sut.ExecuteAsync(r.Id, Guid.NewGuid(), "operator", default);
        await act.Should().ThrowAsync<OptimisticConcurrencyException>();
    }

    [Fact]
    public async Task Idempotent_when_not_archived()
    {
        var repo = new FakeRecipientRepository();
        var token = Guid.NewGuid();
        var r = Recipient.Create(Guid.NewGuid(), "ね", "ネ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, token);
        repo.Added.Add(r);

        var sut = new RestoreRecipientUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());
        await sut.ExecuteAsync(r.Id, token, "operator", default);

        repo.Added.Single().IsArchived.Should().BeFalse();
    }
}
