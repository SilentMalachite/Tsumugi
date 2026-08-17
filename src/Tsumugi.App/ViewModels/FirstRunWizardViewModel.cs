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

    [RelayCommand]
    private async Task RegisterAsync(CancellationToken ct = default)
    {
        if (IsSaving)
            return;

        IsSaving = true;
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

            await registerFirstRun.ExecuteAsync(input, Environment.UserName, ct);

            SaveErrorMessage = null;
            Registered?.Invoke();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
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
    }

    [RelayCommand]
    private void Cancel()
    {
        Cancelled?.Invoke();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
