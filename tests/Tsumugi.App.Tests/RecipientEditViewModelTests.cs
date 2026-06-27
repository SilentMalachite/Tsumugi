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
                new FixedClock(DateTimeOffset.UnixEpoch)))
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
    public async Task SaveCommand_with_blank_kanji_sets_error_message_in_friendly_text()
    {
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(new InMemoryRecipientRepo(), new InMemoryUow(),
                new FixedClock(DateTimeOffset.UnixEpoch)))
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
