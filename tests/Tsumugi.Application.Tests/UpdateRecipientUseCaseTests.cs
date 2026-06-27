using FluentAssertions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class UpdateRecipientUseCaseTests
{
    private static readonly DateTimeOffset FixedNow =
        new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero);

    private static Recipient SeedRecipient(FakeRecipientRepository repo, string kanjiName = "山田太郎")
    {
        var r = Recipient.Create(
            Guid.NewGuid(), kanjiName, "ヤマダタロウ",
            new DateOnly(1990, 1, 1), "seeder", FixedNow, Guid.NewGuid());
        repo.Added.Add(r);
        return r;
    }

    [Fact]
    public async Task Updates_name_and_preserves_created_at()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var sut = new UpdateRecipientUseCase(repo, uow);
        var original = SeedRecipient(repo);

        await sut.ExecuteAsync(original.Id, original.ConcurrencyToken,
            "田中花子", "タナカハナコ", new DateOnly(1985, 5, 10), "editor", default);

        var stored = repo.Added.Single(r => r.Id == original.Id);
        stored.KanjiName.Should().Be("田中花子");
        stored.KanaName.Should().Be("タナカハナコ");
        stored.CreatedAt.Should().Be(original.CreatedAt);
        stored.CreatedBy.Should().Be(original.CreatedBy);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Throws_when_recipient_not_found()
    {
        var sut = new UpdateRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork());
        Func<Task> act = () => sut.ExecuteAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            "田中", "タナカ", new DateOnly(1985, 5, 10), "editor", default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Rejects_empty_id_before_db_lookup()
    {
        var sut = new UpdateRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork());
        Func<Task> act = () => sut.ExecuteAsync(
            Guid.Empty, expectedConcurrencyToken: Guid.NewGuid(),
            "田中", "タナカ", new DateOnly(1985, 5, 10), "editor", default);
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "id");
    }

    [Fact]
    public async Task Update_throws_OptimisticConcurrencyException_when_token_is_stale()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var sut = new UpdateRecipientUseCase(repo, uow);
        var original = SeedRecipient(repo);

        var staleToken = Guid.NewGuid();
        Func<Task> act = () => sut.ExecuteAsync(
            original.Id, expectedConcurrencyToken: staleToken,
            "田中花子", "タナカハナコ", new DateOnly(1985, 5, 10), "editor", default);

        await act.Should().ThrowAsync<Tsumugi.Application.OptimisticConcurrencyException>();
    }

    [Fact]
    public async Task Update_succeeds_when_token_matches_current()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var sut = new UpdateRecipientUseCase(repo, uow);
        var original = SeedRecipient(repo);

        await sut.ExecuteAsync(
            original.Id, expectedConcurrencyToken: original.ConcurrencyToken,
            "田中花子", "タナカハナコ", new DateOnly(1985, 5, 10), "editor", default);

        repo.Added.Single(r => r.Id == original.Id).KanjiName.Should().Be("田中花子");
    }

    [Fact]
    public async Task Rejects_blank_kanji_name()
    {
        var repo = new FakeRecipientRepository();
        var original = SeedRecipient(repo);
        var sut = new UpdateRecipientUseCase(repo, new FakeUnitOfWork());
        Func<Task> act = () => sut.ExecuteAsync(
            original.Id, original.ConcurrencyToken,
            " ", "タナカ", new DateOnly(1985, 5, 10), "editor", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Rejects_blank_kana_name()
    {
        var repo = new FakeRecipientRepository();
        var original = SeedRecipient(repo);
        var sut = new UpdateRecipientUseCase(repo, new FakeUnitOfWork());
        Func<Task> act = () => sut.ExecuteAsync(
            original.Id, original.ConcurrencyToken,
            "田中花子", " ", new DateOnly(1985, 5, 10), "editor", default);
        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.ParamName.Should().Be("kanaName");
    }

    [Fact]
    public async Task Rejects_invalid_date_of_birth()
    {
        var repo = new FakeRecipientRepository();
        var original = SeedRecipient(repo);
        var sut = new UpdateRecipientUseCase(repo, new FakeUnitOfWork());
        Func<Task> act = () => sut.ExecuteAsync(
            original.Id, original.ConcurrencyToken,
            "田中", "タナカ", DateOnly.MinValue, "editor", default);
        await act.Should().ThrowAsync<DateValidationException>();
    }
}

public sealed class ListRecipientsUseCaseTests
{
    [Fact]
    public async Task Returns_empty_list_when_no_recipients()
    {
        var repo = new FakeRecipientRepository();
        var sut = new ListRecipientsUseCase(repo);
        var result = await sut.ExecuteAsync(default);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Maps_all_recipients_to_dto()
    {
        var repo = new FakeRecipientRepository();
        repo.Added.Add(Recipient.Create(Guid.NewGuid(), "山田太郎", "ヤマダタロウ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        repo.Added.Add(Recipient.Create(Guid.NewGuid(), "田中花子", "タナカハナコ",
            new DateOnly(1985, 5, 10), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var sut = new ListRecipientsUseCase(repo);

        var result = await sut.ExecuteAsync(default);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(d => d.KanjiName == "山田太郎");
        result.Should().ContainSingle(d => d.KanjiName == "田中花子");
    }
}
