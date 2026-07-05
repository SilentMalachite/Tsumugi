using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 工賃調整（特別手当）入力 ViewModel。
/// 事業所 ＋ 対象年月を選択すると、利用者ごとの特別手当額マトリクスを表示・編集できる。
/// </summary>
public sealed partial class WageAdjustmentViewModel(
    RecordWageAdjustmentUseCase recordAdjustment,
    QueryWageAdjustmentUseCase queryAdjustments,
    ListOfficesUseCase listOffices,
    ListRecipientsUseCase listRecipients) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;

    /// <summary>対象年月（YYYYMM 整数形式。例: 202605）。0 は未入力。</summary>
    [ObservableProperty] private int _selectedYearMonthInt;

    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<OfficeDto> Offices { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();
    public ObservableCollection<WageAdjustmentRowViewModel> Rows { get; } = new();

    // ---- 変更トリガ ----

    partial void OnSelectedOfficeChanged(OfficeDto? value)
    {
        SaveAllCommand.NotifyCanExecuteChanged();
        if (value is not null && IsValidYearMonthInt(SelectedYearMonthInt))
            _ = SafeRefreshMatrixAsync();
    }

    partial void OnSelectedYearMonthIntChanged(int value)
    {
        SaveAllCommand.NotifyCanExecuteChanged();
        if (SelectedOffice is not null && IsValidYearMonthInt(value))
            _ = SafeRefreshMatrixAsync();
    }

    private static bool IsValidYearMonthInt(int v) =>
        v is >= 202601 and <= 209912 && v % 100 is >= 1 and <= 12;

    private static YearMonth ToYearMonth(int v) => YearMonth.FromInt(v);

    // ---- コマンド ----

    /// <summary>View の Loaded から呼ばれる初期化フック。事業所・利用者一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadCommand.ExecuteAsync(null);

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var offices = await listOffices.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in offices) Offices.Add(o);

        var recipients = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in recipients) Recipients.Add(r);
    }

    private async Task SafeRefreshMatrixAsync()
    {
        try { await RefreshMatrixAsync(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task RefreshMatrixAsync(CancellationToken ct = default)
    {
        if (SelectedOffice is null || !IsValidYearMonthInt(SelectedYearMonthInt)) return;

        var ym = ToYearMonth(SelectedYearMonthInt);
        var effectiveByRecipient = await queryAdjustments
            .GetEffectiveSpecialAllowanceByRecipientAsync(SelectedOffice.Id, ym, ct);

        Rows.Clear();
        foreach (var recipient in Recipients)
        {
            effectiveByRecipient.TryGetValue(recipient.Id, out var effectiveYen);
            Rows.Add(new WageAdjustmentRowViewModel(recipient, effectiveYen));
        }
    }

    private bool CanSaveAll() =>
        SelectedOffice is not null && IsValidYearMonthInt(SelectedYearMonthInt);

    [RelayCommand(CanExecute = nameof(CanSaveAll))]
    public async Task SaveAllAsync(CancellationToken ct = default)
    {
        if (SelectedOffice is null || !IsValidYearMonthInt(SelectedYearMonthInt)) return;

        var ym = ToYearMonth(SelectedYearMonthInt);
        var dirty = Rows.Where(r => r.IsDirty).ToList();

        try
        {
            foreach (var row in dirty)
            {
                await recordAdjustment.ExecuteAsync(
                    SelectedOffice.Id, row.Recipient.Id, ym,
                    WageAdjustmentType.SpecialAllowance, row.SpecialAllowanceYen,
                    note: null,
                    actor: Environment.UserName, ct);
            }

            foreach (var row in dirty)
                row.ResetDirty();

            ErrorMessage = null;
        }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
    }

}
