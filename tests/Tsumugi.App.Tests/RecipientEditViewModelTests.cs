using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.Tests;

public sealed class RecipientEditViewModelTests
{
    [Fact]
    public async Task SaveCommand_with_valid_input_registers_and_clears_error()
    {
        var repo = new InMemoryRecipientRepo();
        var uow = new InMemoryUow();
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(repo, uow,
                new FixedClock(DateTimeOffset.UnixEpoch)),
            new UpdateRecipientUseCase(repo, uow))
        {
            KanjiName = "山田太郎",
            KanaName = "ヤマダタロウ",
            DateOfBirth = new DateOnly(1990, 1, 1),
        };

        await sut.SaveCommand.ExecuteAsync(null);

        sut.SaveErrorMessage.Should().BeNull();
        sut.IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task LoadForEdit_populates_form_from_existing_recipient()
    {
        var repo = new InMemoryRecipientRepo();
        var existing = Tsumugi.Domain.Entities.Recipient.Create(
            Guid.NewGuid(), "山田太郎", "ヤマダタロウ",
            new DateOnly(1990, 1, 1), "seeder", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        repo.Added.Add(existing);
        var uow = new InMemoryUow();
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(repo, uow, new FixedClock(DateTimeOffset.UnixEpoch)),
            new UpdateRecipientUseCase(repo, uow));

        sut.LoadForEdit(existing.Id, existing.KanjiName, existing.KanaName, existing.DateOfBirth);

        sut.EditingId.Should().Be(existing.Id);
        sut.KanjiName.Should().Be("山田太郎");
    }

    [Fact]
    public async Task SaveCommand_with_editing_id_updates_existing_recipient()
    {
        var repo = new InMemoryRecipientRepo();
        var existing = Tsumugi.Domain.Entities.Recipient.Create(
            Guid.NewGuid(), "旧名", "キュウメイ",
            new DateOnly(1990, 1, 1), "seeder", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        repo.Added.Add(existing);
        var uow = new InMemoryUow();
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(repo, uow, new FixedClock(DateTimeOffset.UnixEpoch)),
            new UpdateRecipientUseCase(repo, uow));
        sut.LoadForEdit(existing.Id, existing.KanjiName, existing.KanaName, existing.DateOfBirth);
        sut.KanjiName = "新名";

        await sut.SaveCommand.ExecuteAsync(null);

        sut.IsSaved.Should().BeTrue();
        repo.Added.Single(r => r.Id == existing.Id).KanjiName.Should().Be("新名");
    }

    [Fact]
    public async Task SaveCommand_with_blank_kanji_sets_error_message_in_friendly_text()
    {
        var repo = new InMemoryRecipientRepo();
        var uow = new InMemoryUow();
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(repo, uow, new FixedClock(DateTimeOffset.UnixEpoch)),
            new UpdateRecipientUseCase(repo, uow))
        {
            KanjiName = "",
            KanaName = "ヤマダ",
            DateOfBirth = new DateOnly(1990, 1, 1),
        };

        await sut.SaveCommand.ExecuteAsync(null);

        sut.SaveErrorMessage.Should().Contain("氏名");
        sut.IsSaved.Should().BeFalse();
    }
}

internal sealed class InMemoryUow : Tsumugi.Application.Abstractions.IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
