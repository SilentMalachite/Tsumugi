using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 工賃確定 + 帳票 ViewModel。
/// CloseWagesUseCase (D4) で WageStatement を確定し、IWageReportGenerator (E2/E3) で PDF を生成する。
/// PDF のファイル保存は View 側 (Avalonia SaveFileDialog) が byte[] を受け取って実行する。
/// </summary>
public sealed partial class WageStatementViewModel(
    CloseWagesUseCase close,
    QueryWageStatementUseCase query,
    ListRecipientsUseCase listRecipients,
    IWageReportGenerator reportGenerator) : ViewModelBase
{
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private OfficeDto? _office;
    [ObservableProperty] private int _year = DateTime.UtcNow.Year;
    [ObservableProperty] private int _month = DateTime.UtcNow.Month;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasCorrectionHistory;

    public ObservableCollection<WageStatementDto> Statements { get; } = new();
    private readonly Dictionary<Guid, RecipientDto> _recipientCache = new();

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (OfficeId == Guid.Empty) { ErrorMessage = "事業所IDが指定されていません。"; return; }
        IsBusy = true;
        try
        {
            var stmts = await query.ExecuteAsync(OfficeId, Year, Month, default);
            Statements.Clear();
            foreach (var s in stmts) Statements.Add(s);
            HasCorrectionHistory = stmts.Any(s => s.Kind == RecordKind.Correct);

            var recipients = await listRecipients.ExecuteAsync(includeArchived: true, default);
            _recipientCache.Clear();
            foreach (var r in recipients) _recipientCache[r.Id] = r;

            ErrorMessage = null;
        }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task CloseAsync()
    {
        if (OfficeId == Guid.Empty) { ErrorMessage = "事業所IDが指定されていません。"; return; }
        IsBusy = true;
        try
        {
            var result = await close.ExecuteAsync(OfficeId, Year, Month, Environment.UserName, default);
            StatusMessage = $"{Year}年{Month}月分を確定しました（{result.Count} 名）。";
            ErrorMessage = null;
            await RefreshAsync();
        }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>利用者明細 PDF を生成する（保存は View 側）。</summary>
    public byte[]? GenerateStatementPdf(Guid recipientId)
    {
        if (Office is null) { ErrorMessage = "事業所情報が設定されていません。"; return null; }
        var stmt = Statements.FirstOrDefault(s => s.RecipientId == recipientId);
        if (stmt is null) { ErrorMessage = "対象の確定レコードがありません。"; return null; }
        if (!_recipientCache.TryGetValue(recipientId, out var recipient))
        {
            ErrorMessage = "利用者情報が見つかりません。";
            return null;
        }
        return reportGenerator.GenerateStatement(stmt, recipient, Office);
    }

    /// <summary>事業所月次の支払一覧 PDF を生成する（保存は View 側）。</summary>
    public byte[]? GeneratePaymentListPdf()
    {
        if (Office is null) { ErrorMessage = "事業所情報が設定されていません。"; return null; }
        return reportGenerator.GeneratePaymentList(
            Statements.ToArray(), _recipientCache, Office, Year, Month);
    }
}
