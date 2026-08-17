using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

/// <summary>初回起動ウィザード専用の事業所登録 ViewModel。</summary>
public sealed partial class FirstRunWizardViewModel(
    RegisterFirstRunUseCase registerFirstRun) : ViewModelBase
{
    private const string UnexpectedRegisterFailureMessage =
        "登録に失敗しました。入力内容を確認して再度お試しください。";

    private const string WindowSwitchFailureMessage =
        "登録は完了しましたが、画面の切り替えに失敗しました。キャンセルでアプリを終了し、再起動してください。";

    // 登録が永続化された後は true。再実行すると「既に登録されています」になり原因が読めないため封じる。
    private bool _registrationCompleted;

    [ObservableProperty] private string _officeNumber = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ServiceCategory _category = ServiceCategory.TypeB;
    [ObservableProperty] private RegionGrade _region = RegionGrade.None;
    [ObservableProperty] private string _postalCode = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _representativeTitleAndName = string.Empty;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaving;

    /// <summary>登録成功時に Window 側が購読する寿命イベント。</summary>
    public Action? Registered { get; set; }

    /// <summary>キャンセル時に Window 側が購読する寿命イベント。</summary>
    public Action? Cancelled { get; set; }

    // CancellationToken 引数を付けない（AsyncRelayCommand の再入が先行実行をキャンセルするのを防ぐ）。
    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (IsSaving || _registrationCompleted)
            return;

        IsSaving = true;
        var succeeded = false;
        try
        {
            var input = new RegisterFirstRunInput(
                OfficeNumber,
                Name,
                Category,
                Region,
                NullIfEmpty(PostalCode),
                NullIfEmpty(Address),
                NullIfEmpty(PhoneNumber),
                NullIfEmpty(RepresentativeTitleAndName));

            await registerFirstRun.ExecuteAsync(
                input, Environment.UserName, CancellationToken.None);

            SaveErrorMessage = null;
            succeeded = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            SaveErrorMessage = ex.Message;
        }
        catch (Exception)
        {
            SaveErrorMessage = UnexpectedRegisterFailureMessage;
        }
        finally
        {
            IsSaving = false;
        }

        // 永続化成功後・IsSaving=false の状態で呼ぶ。callback 例外は登録失敗にしない。
        if (!succeeded)
            return;

        _registrationCompleted = true;
        RegisterCommand.NotifyCanExecuteChanged();

        try
        {
            Registered?.Invoke();
        }
        catch (Exception)
        {
            // Registered は MainWindow の構築・差し替えを行う。ここで例外を漏らすと
            // AsyncRelayCommand の async void 経路で UI スレッドへ再スローされ、
            // 事業所を永続化した直後にプロセスが落ちる。
            // 例外本文は保存先パス等を含みうるため出さない（CLAUDE.md ハード制約4）。
            SaveErrorMessage = WindowSwitchFailureMessage;
        }
    }

    private bool CanRegister() => !_registrationCompleted;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (IsSaving)
            return;

        Cancelled?.Invoke();
    }

    private bool CanCancel() => !IsSaving;

    partial void OnIsSavingChanged(bool value) =>
        CancelCommand.NotifyCanExecuteChanged();

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
