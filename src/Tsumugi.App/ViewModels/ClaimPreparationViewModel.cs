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
using Tsumugi.Domain.Logic.Claim;
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
    ExportClaimCsvUseCase exportClaimCsv,
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

    /// <summary>
    /// 施設区分（<c>OfficeClaimProfile.FacilityClassification</c>）が未入力のまま、施設区分条件を
    /// 持つ行へ到達したときの固定文言。ADR 0047 はこれを readiness の不足項目にしない判断を
    /// しているため、入力すべき欄を名指しできるのはこの境界だけになる。
    /// </summary>
    private const string FacilityClassificationRequiredMessage =
        "施設区分が未入力です。事業所請求設定で施設区分を入力してから、もう一度実行してください。";

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

    /// <summary>
    /// 事前登録済みの将来の施行分での要求の変化（ADR 0041）。**確定を止めない情報**で、
    /// 「次の施行分で必要になる項目」と「次の施行分では不要になる項目」の両方を示す。
    /// 表示は項目コードと版だけ（氏名・受給者証番号は出さない）。
    /// </summary>
    public ObservableCollection<string> UpcomingSpecificationWarnings { get; } = [];

    /// <summary>
    /// 体制届で宣言されたが当月に有効なマスタ行が無いキー（ADR 0049）。**確定を止めない情報**で、
    /// 無音で加算0円になる経路を可視化する警告。表示はキー文字列のみ（氏名・受給者証番号は出さない）。
    /// </summary>
    public ObservableCollection<string> CapabilityCoverageWarnings { get; } = [];

    public ObservableCollection<ClaimBatchHistoryDto> History { get; } = [];

    /// <summary>「帳票出力」セクション（Task 14）。確定済revisionの有無と受給者一覧は
    /// <see cref="RefreshReportSectionAsync"/>で本ViewModelから都度反映する。</summary>
    public ClaimReportSection ReportSection { get; } = new(generateClaimReports, fileSaveService);

    /// <summary>「国保連CSV出力」セクション（Phase 3-3）。確定済revisionの有無と対象を
    /// <see cref="RefreshReportSectionAsync"/>で本ViewModelから都度反映する。
    /// 処理対象年月はセクション側の独立入力（AC3-7）。</summary>
    public ClaimCsvExportSection CsvExportSection { get; } = new(exportClaimCsv, fileSaveService);

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
            Replace(
                UpcomingSpecificationWarnings,
                (preview.UpcomingSpecificationIssues ?? []).Select(FormatUpcomingChange));
            Replace(CapabilityCoverageWarnings, preview.CapabilityCoverageWarnings ?? []);
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
            UpcomingSpecificationWarnings.Clear();
            CapabilityCoverageWarnings.Clear();
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
            UpcomingSpecificationWarnings.Clear();
            CapabilityCoverageWarnings.Clear();
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
        UpcomingSpecificationWarnings.Clear();
        CapabilityCoverageWarnings.Clear();
        History.Clear();
        ErrorMessage = null;
        CancelCommand.NotifyCanExecuteChanged();
        ReportSection.HasFinalizedRevision = false;
        CsvExportSection.HasFinalizedRevision = false;
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

        CsvExportSection.OfficeId = context.OfficeId;
        CsvExportSection.ServiceMonth = context.ServiceMonth;
        CsvExportSection.Actor = Environment.UserName;
        CsvExportSection.HasFinalizedRevision = hasFinalizedRevision;
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

    /// <summary>
    /// <see cref="ServiceCodeResolutionException"/> を含める理由: 算定は
    /// <c>ClaimPreviewPipeline</c> から <c>ClaimCalculator.Calculate</c> を同期呼び出しするため、
    /// マスタ解決の失敗はそのまま <see cref="PreviewAsync"/>／<see cref="CloseAsync"/> へ伝播する。
    /// ここで受けないと <c>AsyncRelayCommand</c>（<c>FlowExceptionsToTaskScheduler</c> 無し）から
    /// 未捕捉例外として抜け、グローバルハンドラも無いためアプリが終了する。
    /// </summary>
    private static bool IsHandledClaimException(Exception ex) =>
        ex is ClaimFinalizationException or ClaimMasterPolicyUnavailableException
            or ClaimInputSaveException or ServiceCodeResolutionException or ArgumentException;

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
        // 施設区分の未入力（ADR 0047・0048）だけは入力すべき欄を名指しする。他の解決失敗は
        // マスタ側の不整合であり利用者の入力では直せないため汎用文言に倒す。
        ServiceCodeResolutionException
        {
            Code: ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved,
        } => FacilityClassificationRequiredMessage,
        _ => GenericFailureMessage,
    };

    /// <summary>
    /// 次の施行分での変化の表示文言。緩む方向は「今月の確定は止まったままである」ことが伝わる言い方にする
    /// （自動で緩めると、現行版の適用期間内に提出したときに提出先で弾かれる）。
    /// </summary>
    private static string FormatUpcomingChange(ClaimUpcomingSpecificationIssue change) =>
        change.Change switch
        {
            ClaimUpcomingSpecificationChange.BecomesRequired =>
                $"次の施行分 {change.SpecificationVersion} で必要になります: {change.Issue.FieldCode}",
            ClaimUpcomingSpecificationChange.BecomesOptional =>
                $"次の施行分 {change.SpecificationVersion} では不要になります"
                + $"（この施行分で提出する場合は今も必要）: {change.Issue.FieldCode}",
            _ => $"次の施行分 {change.SpecificationVersion}: {change.Issue.FieldCode}",
        };

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private sealed record WorkspaceContext(Guid OfficeId, ServiceMonth ServiceMonth);
}
