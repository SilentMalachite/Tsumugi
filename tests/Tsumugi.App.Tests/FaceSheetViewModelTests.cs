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

    [Theory]
    [InlineData("ReceivesDisabilityPension", "True", "あり")]
    [InlineData("ReceivesDisabilityPension", "False", "なし")]
    [InlineData("Address", "東京都", "東京都")]
    [InlineData("UnknownProperty", "値", "値")]
    public void Change_display_item_localizes_known_fields_and_preserves_values(
        string propertyName, string oldValue, string expectedOldValue)
    {
        var item = FaceSheetChangeDisplayItem.From(
            new Tsumugi.Application.Dtos.FaceSheetChangeDto(propertyName, oldValue, "新値"));

        item.PropertyName.Should().Be(propertyName == "ReceivesDisabilityPension" ? "障害年金の受給" :
            propertyName == "Address" ? "住所" : propertyName);
        item.OldValue.Should().Be(expectedOldValue);
    }
}
