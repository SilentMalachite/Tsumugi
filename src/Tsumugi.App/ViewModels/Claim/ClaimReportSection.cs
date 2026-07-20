using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.Services;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels.Claim;

/// <summary>
/// 請求確定画面の「帳票出力」セクション（1画面1責務の内訳）。3種PDF
/// （サービス提供実績記録票／請求書／請求明細書）の保存コマンドを持つ。
/// <see cref="ClaimPreparationViewModel"/>がOffice/対象月/確定履歴の有無・選択可能な
/// 受給者一覧をその都度反映する「親から状態を押し込まれる子セクション」。
/// エラーメッセージは固定文言のみ（氏名・保存先フルパスをログ/画面に出さない。CLAUDE.md ハード制約4）。
/// </summary>
public sealed partial class ClaimReportSection(
    GenerateClaimReportsUseCase generateClaimReports,
    IFileSaveService fileSaveService) : ViewModelBase
{
    private const string SaveFailedMessage = "帳票の保存に失敗しました。しばらくしてから再試行してください。";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveServiceProvisionRecordCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveClaimInvoiceCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveClaimStatementCommand))]
    private bool _hasFinalizedRevision;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveServiceProvisionRecordCommand))]
    private ClaimReportRecipientOption? _selectedRecipient;

    [ObservableProperty] private string? _lastSavedFileName;
    [ObservableProperty] private string? _errorMessage;

    /// <summary>確定済revisionの受給者から選べる候補。親ViewModelが履歴更新の都度反映する。</summary>
    public ObservableCollection<ClaimReportRecipientOption> Recipients { get; } = [];

    /// <summary>対象事業所ID。親ViewModelがOffice選択・履歴更新のたびに反映する。</summary>
    public Guid OfficeId { get; set; }

    /// <summary>対象年月。確定済revisionが無い間はnull（親ViewModelが反映）。</summary>
    public ServiceMonth? ServiceMonth { get; set; }

    /// <summary>ファイル名生成に使う事業所表示情報。親ViewModelが反映する。</summary>
    public OfficeDto? Office { get; set; }

    private string OfficeDisplayName => Office?.Name ?? "事業所";

    private bool CanSaveServiceProvisionRecord() => HasFinalizedRevision && SelectedRecipient is not null;

    private bool CanSaveInvoiceOrStatement() => HasFinalizedRevision;

    [RelayCommand(CanExecute = nameof(CanSaveServiceProvisionRecord))]
    public async Task SaveServiceProvisionRecordAsync(CancellationToken ct = default)
    {
        if (SelectedRecipient is not { } recipient || ServiceMonth is not { } month) return;

        var fileName = $"{recipient.KanjiName}_サービス提供実績記録票_{month.Year:D4}-{month.Month:D2}.pdf";
        await GenerateAndSaveAsync(
            fileName,
            () => generateClaimReports.GenerateServiceProvisionRecordAsync(
                OfficeId, month, recipient.RecipientId, ct),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanSaveInvoiceOrStatement))]
    public async Task SaveClaimInvoiceAsync(CancellationToken ct = default)
    {
        if (ServiceMonth is not { } month) return;

        var fileName = $"{OfficeDisplayName}_請求書_{month.Year:D4}-{month.Month:D2}.pdf";
        await GenerateAndSaveAsync(
            fileName,
            () => generateClaimReports.GenerateClaimInvoiceAsync(OfficeId, month, ct),
            ct);
    }

    [RelayCommand(CanExecute = nameof(CanSaveInvoiceOrStatement))]
    public async Task SaveClaimStatementAsync(CancellationToken ct = default)
    {
        if (ServiceMonth is not { } month) return;

        var fileName = $"{OfficeDisplayName}_請求明細書_{month.Year:D4}-{month.Month:D2}.pdf";
        await GenerateAndSaveAsync(
            fileName,
            () => generateClaimReports.GenerateClaimStatementAsync(OfficeId, month, ct),
            ct);
    }

    private async Task GenerateAndSaveAsync(
        string suggestedFileName, Func<Task<byte[]>> generate, CancellationToken ct)
    {
        ErrorMessage = null;
        try
        {
            var bytes = await generate();
            var saved = await fileSaveService.SaveAsync(bytes, suggestedFileName, "PDF", ".pdf", ct);
            if (saved) LastSavedFileName = suggestedFileName;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = SaveFailedMessage;
        }
        catch (IOException)
        {
            ErrorMessage = SaveFailedMessage;
        }
    }
}

/// <summary>帳票出力の受給者選択肢（表示名のみ。編集用途ではないため<see cref="RecipientDto"/>を流用しない）。</summary>
public sealed record ClaimReportRecipientOption(Guid RecipientId, string KanjiName);
