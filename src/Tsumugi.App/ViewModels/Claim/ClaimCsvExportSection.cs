using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.Services;
using Tsumugi.Application.Claim;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels.Claim;

/// <summary>
/// 請求確定画面の「国保連CSV出力」セクション（1画面1責務の内訳）。責務は
/// 「確定済み revision の選択状態を受け取る・処理対象年月を入力する・生成して保存する・失敗を表示する」
/// に限定し、入力の補完は <c>ClaimInputView</c> 側に置く（ADR 0030）。
/// </summary>
/// <remarks>
/// 表示するエラーは項目 ID・理由・内部参照コードだけで、氏名・受給者証番号・保存先フルパスは出さない
/// （CLAUDE.md §ハード制約4）。処理対象年月はサービス提供年月とは独立した入力である（AC3-7）。
/// </remarks>
public sealed partial class ClaimCsvExportSection(
    ExportClaimCsvUseCase exportClaimCsv,
    IFileSaveService fileSaveService) : ViewModelBase
{
    private const string FileTypeName = "国保連請求CSV";
    private const string FileExtension = ".csv";
    private const string NotFinalizedMessage = "確定済みの請求がありません。先に請求を確定してください。";
    private const string SaveFailedMessage = "CSVの保存に失敗しました。しばらくしてから再試行してください。";
    private const string MonthRangeMessage = "処理対象年月を正しく入力してください。";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCsvCommand))]
    private bool _hasFinalizedRevision;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCsvCommand))]
    private int _processingYear = DateTime.UtcNow.Year;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCsvCommand))]
    private int _processingMonth = DateTime.UtcNow.Month;

    [ObservableProperty] private string? _lastSavedFileName;
    [ObservableProperty] private string? _errorMessage;

    /// <summary>対象事業所ID。親ViewModelがOffice選択・履歴更新のたびに反映する。</summary>
    public Guid OfficeId { get; set; }

    /// <summary>対象サービス提供年月。確定済revisionが無い間はnull（親ViewModelが反映）。</summary>
    public ServiceMonth? ServiceMonth { get; set; }

    /// <summary>操作者。監査列（出力履歴の作成者）に記録する。</summary>
    public string Actor { get; set; } = string.Empty;

    private bool CanGenerateCsv() =>
        HasFinalizedRevision
        && ProcessingYear is >= 1900 and <= 2200
        && ProcessingMonth is >= 1 and <= 12;

    [RelayCommand(CanExecute = nameof(CanGenerateCsv))]
    public async Task GenerateCsvAsync(CancellationToken ct = default)
    {
        if (ServiceMonth is not { } serviceMonth || OfficeId == Guid.Empty)
        {
            ErrorMessage = NotFinalizedMessage;
            return;
        }

        ProcessingMonth processingMonth;
        try
        {
            processingMonth = new ProcessingMonth(ProcessingYear, ProcessingMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            ErrorMessage = MonthRangeMessage;
            return;
        }

        try
        {
            var result = await exportClaimCsv.ExecuteAsync(
                OfficeId, serviceMonth, processingMonth, Actor, ct);
            var saved = await fileSaveService.SaveAsync(
                result.Bytes, result.SuggestedFileName, FileTypeName, FileExtension, ct);
            if (saved)
            {
                LastSavedFileName = result.SuggestedFileName;
                ErrorMessage = null;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ClaimBatchNotFinalizedException)
        {
            ErrorMessage = NotFinalizedMessage;
        }
        catch (ClaimCsvExportFailedException exception)
        {
            ErrorMessage = FormatExportFailure(exception);
        }
        catch (IOException)
        {
            ErrorMessage = SaveFailedMessage;
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = SaveFailedMessage;
        }
    }

    /// <summary>
    /// 失敗理由の表示文言。項目 ID と理由、あれば内部参照コードだけを載せる。
    /// 氏名・受給者証番号・保存先フルパスは決して含めない。
    /// </summary>
    private static string FormatExportFailure(ClaimCsvExportFailedException exception)
    {
        var field = string.IsNullOrEmpty(exception.FieldId) ? "（項目特定不可）" : exception.FieldId;
        var reference = string.IsNullOrEmpty(exception.RecipientReferenceCode)
            ? string.Empty
            : $" 参照コード: {exception.RecipientReferenceCode}";
        return $"CSVを出力できませんでした。項目: {field} / 理由: {exception.Reason}{reference}";
    }
}
