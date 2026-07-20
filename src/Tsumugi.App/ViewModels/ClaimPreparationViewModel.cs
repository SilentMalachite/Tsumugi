using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.ViewModels.Claim;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 請求確定画面。プレビュー（<see cref="CalculateClaimUseCase"/>）→確定
/// （<see cref="CloseClaimUseCase"/>）→取下げ（<see cref="CancelClaimUseCase"/>）→履歴
/// （<see cref="QueryClaimUseCase"/>）→帳票出力（<see cref="ReportSection"/>）を1画面で調停する。
/// エラーメッセージは常に固定文言（氏名・受給者証番号・保存先フルパスを含めない。ハード制約4）。
/// </summary>
public sealed partial class ClaimPreparationViewModel(
    ListOfficesUseCase listOffices,
    CalculateClaimUseCase calculateClaim,
    CloseClaimUseCase closeClaim,
    CancelClaimUseCase cancelClaim,
    QueryClaimUseCase queryClaim,
    ListRecipientsUseCase listRecipients,
    GenerateClaimReportsUseCase generateClaimReports,
    Tsumugi.App.Services.IFileSaveService fileSaveService) : ViewModelBase
{
    private const string ContextRequiredMessage = "事業所と対象月を選択してください。";
    private const string MasterUnavailableMessage = "請求制度マスターを利用できません。";
    private const string ConflictMessage =
        "プレビュー内容が古い可能性があります。再度プレビューしてから確定してください。";
    private const string HistoryConflictMessage =
        "確定履歴が更新されています。再読込してから操作してください。";
    private const string NoActiveHistoryMessage = "取下げ対象の確定請求がありません。";
    private const string GenericFailureMessage = "処理に失敗しました。しばらくしてから再試行してください。";

    private readonly ListOfficesUseCase _listOffices = listOffices;
    private readonly CalculateClaimUseCase _calculateClaim = calculateClaim;
    private readonly CloseClaimUseCase _closeClaim = closeClaim;
    private readonly CancelClaimUseCase _cancelClaim = cancelClaim;
    private readonly QueryClaimUseCase _queryClaim = queryClaim;
    private readonly ListRecipientsUseCase _listRecipients = listRecipients;

    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private int _year = DateTime.UtcNow.Year;
    [ObservableProperty] private int _month = DateTime.UtcNow.Month;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CloseCommand))]
    private ClaimPreviewDto? _preview;

    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<OfficeDto> Offices { get; } = [];
    public ObservableCollection<ClaimPreparationIssue> Issues { get; } = [];
    public ObservableCollection<ClaimBatchHistoryDto> History { get; } = [];

    /// <summary>「帳票出力」セクション（Task 14）。確定済revisionの有無と受給者一覧は
    /// <see cref="RefreshReportSectionAsync"/>で本ViewModelから都度反映する。</summary>
    public ClaimReportSection ReportSection { get; } = new(generateClaimReports, fileSaveService);

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var offices = await _listOffices.ExecuteAsync(ct);
        Replace(Offices, offices);
    }

    [RelayCommand]
    public async Task PreviewAsync(CancellationToken ct = default)
    {
        if (!TryCaptureContext(out var context))
        {
            ErrorMessage = ContextRequiredMessage;
            return;
        }

        try
        {
            var preview = await _calculateClaim.ExecuteAsync(
                new CalculateClaimRequest(context.OfficeId, context.ServiceMonth), ct);
            Preview = preview;
            Replace(Issues, preview.Issues);
            ErrorMessage = null;
            await RefreshHistoryAsync(context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsHandledClaimException(ex))
        {
            ErrorMessage = MapError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (Preview is not { IsReady: true } preview || !TryCaptureContext(out var context))
            return;

        try
        {
            await _closeClaim.ExecuteAsync(
                new CloseClaimRequest(context.OfficeId, context.ServiceMonth, preview.PreviewHash),
                Environment.UserName,
                ct);
            Preview = null;
            Issues.Clear();
            ErrorMessage = null;
            await RefreshHistoryAsync(context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsHandledClaimException(ex))
        {
            ErrorMessage = MapError(ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public async Task CancelAsync(CancellationToken ct = default)
    {
        if (!TryCaptureContext(out var context))
        {
            ErrorMessage = ContextRequiredMessage;
            return;
        }

        if (!CanCancel())
        {
            ErrorMessage = NoActiveHistoryMessage;
            return;
        }

        try
        {
            await _cancelClaim.ExecuteAsync(
                new CancelClaimRequest(context.OfficeId, context.ServiceMonth),
                Environment.UserName,
                ct);
            Preview = null;
            Issues.Clear();
            ErrorMessage = null;
            await RefreshHistoryAsync(context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsHandledClaimException(ex))
        {
            ErrorMessage = MapError(ex);
        }
    }

    private bool CanClose() => Preview is { IsReady: true };

    private bool CanCancel() => History.Count > 0 && History[^1].Kind != RecordKind.Cancel;

    partial void OnSelectedOfficeChanged(OfficeDto? value) => OfficeId = value?.Id ?? Guid.Empty;

    partial void OnOfficeIdChanged(Guid value) => ClearPreviewAndHistory();
    partial void OnYearChanged(int value) => ClearPreviewAndHistory();
    partial void OnMonthChanged(int value) => ClearPreviewAndHistory();

    private void ClearPreviewAndHistory()
    {
        Preview = null;
        Issues.Clear();
        History.Clear();
        ErrorMessage = null;
        CancelCommand.NotifyCanExecuteChanged();
        ReportSection.HasFinalizedRevision = false;
        ReportSection.Recipients.Clear();
        ReportSection.SelectedRecipient = null;
    }

    private async Task RefreshHistoryAsync(WorkspaceContext context, CancellationToken ct)
    {
        var history = await _queryClaim.ExecuteAsync(
            new QueryClaimRequest(context.OfficeId, context.ServiceMonth), ct);
        Replace(History, history);
        CancelCommand.NotifyCanExecuteChanged();
        await RefreshReportSectionAsync(context, ct);
    }

    /// <summary>
    /// 「帳票出力」セクション（Task 14）へOffice/対象月・確定済revisionの有無・
    /// 選択可能な受給者一覧を反映する。確定済revision＝<see cref="CanCancel"/>と同じ判定
    /// （<see cref="RecordKind.Cancel"/>以外の最新履歴）。受給者氏名はHistoryのDetailに含まれない
    /// ため、<see cref="ListRecipientsUseCase"/>で別途解決してクロス参照する
    /// （Phase 2 <c>WageStatementViewModel</c>の<c>_recipientCache</c>と同じ方式）。
    /// </summary>
    private async Task RefreshReportSectionAsync(WorkspaceContext context, CancellationToken ct)
    {
        ReportSection.OfficeId = context.OfficeId;
        ReportSection.ServiceMonth = context.ServiceMonth;
        ReportSection.Office = SelectedOffice;

        var hasFinalizedRevision = CanCancel();
        ReportSection.HasFinalizedRevision = hasFinalizedRevision;
        var latest = hasFinalizedRevision ? History[^1] : null;

        ReportSection.Recipients.Clear();
        if (latest is { Details.Count: > 0 })
        {
            var recipients = await _listRecipients.ExecuteAsync(includeArchived: true, ct);
            var kanjiNameById = recipients.ToDictionary(recipient => recipient.Id, recipient => recipient.KanjiName);
            foreach (var detail in latest.Details)
            {
                if (kanjiNameById.TryGetValue(detail.RecipientId, out var kanjiName))
                    ReportSection.Recipients.Add(new ClaimReportRecipientOption(detail.RecipientId, kanjiName));
            }
        }

        if (ReportSection.SelectedRecipient is { } selected
            && ReportSection.Recipients.All(option => option.RecipientId != selected.RecipientId))
        {
            ReportSection.SelectedRecipient = null;
        }
    }

    private bool TryCaptureContext(out WorkspaceContext context)
    {
        context = default!;
        if (OfficeId == Guid.Empty || Year is < 1900 or > 2200 || Month is < 1 or > 12)
            return false;
        context = new WorkspaceContext(OfficeId, new ServiceMonth(Year, Month));
        return true;
    }

    private static bool IsHandledClaimException(Exception ex) =>
        ex is ClaimFinalizationException or ClaimMasterPolicyUnavailableException
            or ClaimInputSaveException or ArgumentException;

    private static string MapError(Exception ex) => ex switch
    {
        ClaimFinalizationException finalization => finalization.Code switch
        {
            ClaimErrorCode.InvalidOperationPayload => ConflictMessage,
            ClaimErrorCode.InvalidHistory => HistoryConflictMessage,
            _ => GenericFailureMessage,
        },
        ClaimMasterPolicyUnavailableException => MasterUnavailableMessage,
        ClaimInputSaveException => GenericFailureMessage,
        _ => GenericFailureMessage,
    };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private sealed record WorkspaceContext(Guid OfficeId, ServiceMonth ServiceMonth);
}
