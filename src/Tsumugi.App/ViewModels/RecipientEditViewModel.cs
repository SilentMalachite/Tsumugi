using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientEditViewModel(
    RegisterRecipientUseCase registerUseCase,
    UpdateRecipientUseCase updateUseCase) : ViewModelBase
{
    [ObservableProperty] private string _kanjiName = string.Empty;
    [ObservableProperty] private string _kanaName = string.Empty;
    [ObservableProperty] private DateOnly _dateOfBirth = new(1990, 1, 1);
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    // 編集モード: EditingId が null なら新規登録、値があれば更新。
    [ObservableProperty] private Guid? _editingId;

    /// <summary>RecipientList から渡された既存利用者をフォームに展開する。</summary>
    public void LoadForEdit(Guid id, string kanjiName, string kanaName, DateOnly dateOfBirth)
    {
        EditingId = id;
        KanjiName = kanjiName;
        KanaName = kanaName;
        DateOfBirth = dateOfBirth;
        SaveErrorMessage = null;
        IsSaved = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (EditingId is { } id)
            {
                await updateUseCase.ExecuteAsync(
                    id, KanjiName, KanaName, DateOfBirth, actor: Environment.UserName, default);
            }
            else
            {
                await registerUseCase.ExecuteAsync(
                    KanjiName, KanaName, DateOfBirth, actor: Environment.UserName, default);
            }
            SaveErrorMessage = null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    [RelayCommand]
    private void Discard()
    {
        KanjiName = string.Empty;
        KanaName = string.Empty;
        DateOfBirth = default;
        SaveErrorMessage = null;
        IsSaved = false;
        EditingId = null;
    }
}
