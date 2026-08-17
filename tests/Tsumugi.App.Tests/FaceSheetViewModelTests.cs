using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Recipient;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class FaceSheetViewModelTests
{
    [Fact]
    public void New_view_model_exposes_history_and_selected_changes()
    {
        var sut = new FaceSheetViewModel(
            null!, null!, null!,
            new QueryFaceSheetHistoryUseCase(null!));

        sut.HistoryItems.Should().BeEmpty();
        sut.SelectedHistoryItem.Should().BeNull();
        sut.SelectedChanges.Should().BeEmpty();
    }
}
