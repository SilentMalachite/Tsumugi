using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientEditViewModel(RegisterRecipientUseCase registerUseCase) : ViewModelBase
{
    [ObservableProperty] private string _kanjiName = string.Empty;
    [ObservableProperty] private string _kanaName = string.Empty;
    [ObservableProperty] private DateOnly _dateOfBirth = new(1990, 1, 1);
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await registerUseCase.ExecuteAsync(
                KanjiName, KanaName, DateOfBirth, actor: Environment.UserName, default);
            SaveErrorMessage = null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }
}
