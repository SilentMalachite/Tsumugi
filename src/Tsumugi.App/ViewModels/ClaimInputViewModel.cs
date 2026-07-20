using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public enum ClaimInputAggregate
{
    ClaimInput = 0,
    AverageWageAnnualEvidence = 1,
    OfficeClaimProfile = 2,
    CertificateClaimEvidence = 3,
    UpperLimitManagementStatement = 4,
}

public sealed partial class ClaimInputViewModel(
    ListOfficesUseCase listOffices,
    ListRecipientsUseCase listRecipients,
    ListCertificatesByRecipientUseCase listCertificates,
    QueryClaimInputWorkspaceUseCase queryWorkspace,
    SetClaimInputUseCase setClaimInput,
    SetAverageWageAnnualEvidenceUseCase setAverageWage,
    SetOfficeClaimProfileUseCase setOfficeProfile,
    SetCertificateClaimEvidenceUseCase setCertificateEvidence,
    SetUpperLimitManagementStatementUseCase setStatement,
    QueryClaimBillingTokenOptionsUseCase queryBillingTokenOptions) : ViewModelBase
{
    private const string ReloadMessage = "請求入力履歴を再読込してください。";
    private const string InvalidMessage = "請求入力の内容を確認してください。";
    private const string MasterUnavailableMessage = "請求制度マスターを利用できません。";

    private readonly ListOfficesUseCase _listOffices = listOffices;
    private readonly ListRecipientsUseCase _listRecipients = listRecipients;
    private readonly ListCertificatesByRecipientUseCase _listCertificates = listCertificates;
    private readonly QueryClaimInputWorkspaceUseCase _queryWorkspace = queryWorkspace;
    private readonly SetClaimInputUseCase _setClaimInput = setClaimInput;
    private readonly SetAverageWageAnnualEvidenceUseCase _setAverageWage = setAverageWage;
    private readonly SetOfficeClaimProfileUseCase _setOfficeProfile = setOfficeProfile;
    private readonly SetCertificateClaimEvidenceUseCase _setCertificateEvidence = setCertificateEvidence;
    private readonly SetUpperLimitManagementStatementUseCase _setStatement = setStatement;
    private readonly QueryClaimBillingTokenOptionsUseCase _queryBillingTokenOptions =
        queryBillingTokenOptions;

    private WorkspaceContext? _loadedContext;
    private ClaimInputQueryRevisionDto? _loadedClaimInput;
    private bool _claimInputReentry;
    private bool _averageWageReentry;
    private bool _officeProfileReentry;
    private bool _certificateEvidenceReentry;
    private bool _statementReentry;

    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private CertificateDto? _selectedCertificate;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private Guid _certificateId;
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;
    [ObservableProperty] private int _sourceFiscalYear;
    [ObservableProperty] private bool _workspaceLoaded;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private ClaimInputAggregate _selectedAggregate;

    [ObservableProperty] private Guid? _claimInputCurrentHeadId;
    [ObservableProperty] private Guid? _claimInputEffectiveHeadId;
    [ObservableProperty] private UpperLimitManagementResult? _upperLimitManagementResult;
    [ObservableProperty] private int? _upperLimitManagedAmountYen;
    [ObservableProperty] private int? _municipalSubsidyAmountYen;

    [ObservableProperty] private Guid? _averageWageCurrentHeadId;
    [ObservableProperty] private Guid? _averageWageEffectiveHeadId;
    [ObservableProperty] private DateOnly _averageWagePeriodStart;
    [ObservableProperty] private DateOnly _averageWagePeriodEnd;
    [ObservableProperty] private int? _annualWagePaidYen;
    [ObservableProperty] private int? _annualExtendedUsers;
    [ObservableProperty] private int? _annualOpeningDays;
    [ObservableProperty] private FiscalYearCompleteness? _completeness;
    [ObservableProperty] private string? _averageWageEvidenceDocumentId;
    [ObservableProperty] private string? _dailyEvidenceReference;
    [ObservableProperty] private string? _monthlyEvidenceReference;
    [ObservableProperty] private DateTimeOffset? _averageWageConfirmedAt;
    [ObservableProperty] private string? _averageWageConfirmedBy;
    [ObservableProperty] private string? _averageWageConfirmationReason;

    [ObservableProperty]
    private ClaimInputRevisionChainDto<OfficeClaimProfileQueryRevisionDto>?
        _selectedOfficeProfileChain;
    [ObservableProperty] private Guid? _officeProfileCurrentHeadId;
    [ObservableProperty] private Guid? _officeProfileEffectiveHeadId;
    [ObservableProperty] private DateOnly _profileEffectiveFrom;
    [ObservableProperty] private DateOnly? _profileEffectiveTo;
    [ObservableProperty] private ClaimMasterVersion? _masterVersion;
    [ObservableProperty] private R8ReformStatus? _reformStatus;
    [ObservableProperty] private AverageWageBandOption? _averageWageBandOption;
    [ObservableProperty] private DateOnly? _designationDate;
    [ObservableProperty] private DateOnly? _supportStartDate;
    [ObservableProperty] private VersionedAverageWageBandOption? _earlierRegisteredBandOption;
    [ObservableProperty] private ServiceMonth? _earlierRegistrationMonth;
    [ObservableProperty] private VersionedAverageWageBandOption? _laterRegisteredBandOption;
    [ObservableProperty] private ServiceMonth? _laterRegistrationMonth;
    [ObservableProperty] private string? _reformComparisonEvidenceDocumentId;
    [ObservableProperty] private DateRange? _filedTransitionPeriod;
    [ObservableProperty] private string? _filedTransitionEvidenceDocumentId;
    [ObservableProperty] private string? _officeProfileEvidenceDocumentId;
    [ObservableProperty] private DateTimeOffset? _officeProfileConfirmedAt;
    [ObservableProperty] private string? _officeProfileConfirmedBy;
    [ObservableProperty] private string? _officeProfileConfirmationReason;
    [ObservableProperty] private int? _capacityHeadcount;
    [ObservableProperty] private string? _staffingKey;
    [ObservableProperty] private string? _regionKey;

    [ObservableProperty]
    private ClaimInputRevisionChainDto<CertificateClaimEvidenceQueryRevisionDto>?
        _selectedCertificateEvidenceChain;
    [ObservableProperty] private Guid? _certificateEvidenceCurrentHeadId;
    [ObservableProperty] private Guid? _certificateEvidenceEffectiveHeadId;
    [ObservableProperty] private DateOnly _certificateValidityStart;
    [ObservableProperty] private DateOnly? _certificateValidityEnd;
    [ObservableProperty] private bool _monthlyCostCapIsEntered;
    [ObservableProperty] private int? _monthlyCostCapYen;
    [ObservableProperty] private UpperLimitManagementApplicability _upperLimitManagementApplicability;
    [ObservableProperty] private string? _upperLimitManagementOfficeNumber;
    [ObservableProperty] private Article31SpecialBurdenStatus _article31Status;
    [ObservableProperty] private bool _article31AmountIsEntered;
    [ObservableProperty] private int? _article31AmountYen;
    [ObservableProperty] private DateRange? _article31EffectivePeriod;
    [ObservableProperty] private string? _certificateOriginalDocumentReference;
    [ObservableProperty] private DateTimeOffset? _certificateEvidenceConfirmedAt;
    [ObservableProperty] private string? _certificateEvidenceConfirmedBy;
    [ObservableProperty] private string? _certificateEvidenceConfirmationReason;

    [ObservableProperty] private Guid? _statementCurrentHeadId;
    [ObservableProperty] private Guid? _statementEffectiveHeadId;
    [ObservableProperty] private string _municipalityNumber = string.Empty;
    [ObservableProperty] private string _statementCertificateNumber = string.Empty;
    [ObservableProperty] private bool _statementCertificateMonthlyCostCapIsEntered;
    [ObservableProperty] private int? _statementCertificateMonthlyCostCapYen;
    [ObservableProperty]
    private UpperLimitManagementApplicability
        _statementUpperLimitManagementApplicability;
    [ObservableProperty] private string _certificateManagingOfficeNumber = string.Empty;
    [ObservableProperty] private string _managingOfficeNumber = string.Empty;
    [ObservableProperty] private string _managingOfficeName = string.Empty;
    [ObservableProperty] private string _originalCreationKind = string.Empty;
    [ObservableProperty] private DateTimeOffset? _statementReceivedAt;
    [ObservableProperty] private string? _statementOriginalDocumentReference;
    [ObservableProperty] private bool _statementIsConfirmed;
    [ObservableProperty] private DateTimeOffset? _statementConfirmedAt;
    [ObservableProperty] private string? _statementConfirmedBy;
    [ObservableProperty] private string? _statementConfirmationReason;
    [ObservableProperty]
    private UpperLimitManagementResult _statementResult =
        Tsumugi.Domain.Logic.Claim.Models.UpperLimitManagementResult.Result1;
    [ObservableProperty] private bool _statementTotalCostIsEntered;
    [ObservableProperty] private int? _statementTotalCostYen;
    [ObservableProperty] private bool _statementTotalPreManagementBurdenIsEntered;
    [ObservableProperty] private int? _statementTotalPreManagementBurdenYen;
    [ObservableProperty] private bool _statementTotalManagedBurdenIsEntered;
    [ObservableProperty] private int? _statementTotalManagedBurdenYen;
    [ObservableProperty] private UpperLimitManagementStatementLineViewModel? _selectedStatementLine;

    public ObservableCollection<OfficeDto> Offices { get; } = [];
    public ObservableCollection<RecipientDto> Recipients { get; } = [];
    public ObservableCollection<CertificateDto> Certificates { get; } = [];
    public ObservableCollection<ClaimInputQueryRevisionDto> ClaimInputRevisions { get; } = [];
    public ObservableCollection<AverageWageAnnualEvidenceQueryRevisionDto>
        AverageWageRevisions
    { get; } = [];
    public ObservableCollection<ClaimInputRevisionChainDto<OfficeClaimProfileQueryRevisionDto>>
        OfficeProfileChains
    { get; } = [];
    public ObservableCollection<OfficeClaimProfileQueryRevisionDto>
        OfficeProfileRevisions
    { get; } = [];
    public ObservableCollection<
        ClaimInputRevisionChainDto<CertificateClaimEvidenceQueryRevisionDto>>
        CertificateEvidenceChains
    { get; } = [];
    public ObservableCollection<CertificateClaimEvidenceQueryRevisionDto>
        CertificateEvidenceRevisions
    { get; } = [];
    public ObservableCollection<UpperLimitManagementStatementQueryRevisionDto>
        StatementRevisions
    { get; } = [];
    public ObservableCollection<UpperLimitManagementStatementLineViewModel> StatementLines { get; } = [];
    public ObservableCollection<string> StaffingKeyOptions { get; } = [];
    public ObservableCollection<string> RegionKeyOptions { get; } = [];

    public IReadOnlyList<ClaimInputAggregate> AggregateOptions { get; } =
        Enum.GetValues<ClaimInputAggregate>();
    public IReadOnlyList<UpperLimitManagementResult> UpperLimitResultOptions { get; } =
        Enum.GetValues<UpperLimitManagementResult>();
    public IReadOnlyList<FiscalYearCompleteness> CompletenessOptions { get; } =
        Enum.GetValues<FiscalYearCompleteness>();
    public IReadOnlyList<R8ReformStatus> ReformStatusOptions { get; } =
        Enum.GetValues<R8ReformStatus>();
    public IReadOnlyList<UpperLimitManagementApplicability> ApplicabilityOptions { get; } =
        Enum.GetValues<UpperLimitManagementApplicability>();
    public IReadOnlyList<Article31SpecialBurdenStatus> Article31StatusOptions { get; } =
        Enum.GetValues<Article31SpecialBurdenStatus>();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var offices = await _listOffices.ExecuteAsync(ct);
        var recipients = await _listRecipients.ExecuteAsync(includeArchived: false, ct);
        Replace(Offices, offices);
        Replace(Recipients, recipients);
    }

    public async Task<bool> ApplyNavigationContextAsync(
        Guid? officeId,
        Guid? recipientId,
        Guid? certificateId,
        ServiceMonth? serviceMonth,
        CancellationToken ct = default)
    {
        if (officeId is not { } office || recipientId is not { } recipient
            || certificateId is not { } certificate || serviceMonth is not { } targetMonth
            || office == Guid.Empty || recipient == Guid.Empty || certificate == Guid.Empty)
            return false;

        await InitializeAsync(ct);
        SelectedOffice = Offices.SingleOrDefault(item => item.Id == office);
        SelectedRecipient = Recipients.SingleOrDefault(item => item.Id == recipient);
        if (SelectedOffice is null || SelectedRecipient is null) return false;
        await LoadCertificatesForRecipientAsync(recipient, ct);
        SelectedCertificate = Certificates.SingleOrDefault(item => item.Id == certificate);
        if (SelectedCertificate is null) return false;
        Year = targetMonth.Year;
        Month = targetMonth.Month;
        var serviceFiscalYear = targetMonth.Month >= 4
            ? targetMonth.Year : targetMonth.Year - 1;
        SourceFiscalYear = serviceFiscalYear - 1;
        await LoadAsync(ct);
        return WorkspaceLoaded;
    }

    [RelayCommand]
    public async Task LoadCertificatesAsync() =>
        await LoadCertificatesForRecipientAsync(RecipientId, default);

    [RelayCommand]
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!TryCaptureContext(out var context))
        {
            ClearWorkspace();
            ErrorMessage = "事業所、利用者、受給者証、年月、根拠年度を選択してください。";
            return;
        }

        ClearWorkspace();
        try
        {
            var workspace = await _queryWorkspace.ExecuteAsync(
                new QueryClaimInputWorkspaceRequest(
                    context.OfficeId, context.RecipientId, context.CertificateId,
                    context.ServiceMonth, context.SourceFiscalYear), ct);
            if (!MatchesContext(context)) return;
            ApplyWorkspace(workspace);
            RefreshBillingTokenOptions(context.ServiceMonth);
            _loadedContext = context;
            WorkspaceLoaded = true;
            ErrorMessage = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ClaimInputQueryException)
        {
            if (MatchesContext(context)) ErrorMessage = ReloadMessage;
        }
        catch (InvalidOperationException)
        {
            if (MatchesContext(context)) ErrorMessage = ReloadMessage;
        }
        catch (ArgumentException)
        {
            if (MatchesContext(context)) ErrorMessage = InvalidMessage;
        }
    }

    [RelayCommand]
    public async Task SaveClaimInputAsync()
    {
        if (!EnsureLoaded()) return;
        if (ClaimInputCurrentHeadId is not null && ClaimInputEffectiveHeadId is null
            && !_claimInputReentry)
        {
            ErrorMessage = "取消済みの上限額管理結果は「再入力」から入力してください。";
            return;
        }

        var kind = ClaimInputCurrentHeadId is null ? RecordKind.New : RecordKind.Correct;
        var preserve = ClaimInputEffectiveHeadId is not null ? _loadedClaimInput : null;
        await SaveAndReloadAsync(() => _setClaimInput.ExecuteAsync(
            new SetClaimInputRequest(OfficeId, RecipientId, CurrentServiceMonth(), kind,
                ClaimInputCurrentHeadId)
            {
                UpperLimitManagementResult = UpperLimitManagementResult,
                UpperLimitManagedAmountYen = UpperLimitManagedAmountYen,
                MunicipalSubsidyAmountYen = MunicipalSubsidyAmountYen,
                ExceptionalUsageStartMonth = preserve?.ExceptionalUsageStartMonth,
                ExceptionalUsageEndMonth = preserve?.ExceptionalUsageEndMonth,
                ExceptionalUsageDays = preserve?.ExceptionalUsageDays,
                StandardUsageDayTotal = preserve?.StandardUsageDayTotal,
            }, Environment.UserName, default));
    }

    [RelayCommand]
    public async Task CancelClaimInputAsync()
    {
        if (!EnsureLoaded() || ClaimInputCurrentHeadId is null)
        {
            ErrorMessage ??= "取消対象の上限額管理結果がありません。";
            return;
        }
        await SaveAndReloadAsync(() => _setClaimInput.ExecuteAsync(
            new SetClaimInputRequest(OfficeId, RecipientId, CurrentServiceMonth(),
                RecordKind.Cancel, ClaimInputCurrentHeadId),
            Environment.UserName, default));
    }

    [RelayCommand]
    public void ReenterClaimInput()
    {
        if (!EnsureCancelled(ClaimInputCurrentHeadId, ClaimInputEffectiveHeadId)) return;
        _claimInputReentry = true;
        UpperLimitManagementResult = null;
        UpperLimitManagedAmountYen = null;
        MunicipalSubsidyAmountYen = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveAverageWageAsync()
    {
        if (!EnsureLoaded() || !EnsureReentry(
                AverageWageCurrentHeadId, AverageWageEffectiveHeadId, _averageWageReentry,
                "取消済みの平均工賃根拠は「再入力」から入力してください。")) return;
        var kind = AverageWageCurrentHeadId is null ? RecordKind.New : RecordKind.Correct;
        await SaveAndReloadAsync(() => _setAverageWage.ExecuteAsync(
            new SetAverageWageAnnualEvidenceRequest(
                OfficeId, SourceFiscalYear, AverageWagePeriodStart, AverageWagePeriodEnd,
                kind, AverageWageCurrentHeadId)
            {
                AnnualWagePaidYen = AnnualWagePaidYen,
                AnnualExtendedUsers = AnnualExtendedUsers,
                AnnualOpeningDays = AnnualOpeningDays,
                Completeness = Completeness,
                EvidenceDocumentId = AverageWageEvidenceDocumentId,
                DailyEvidenceReference = DailyEvidenceReference,
                MonthlyEvidenceReference = MonthlyEvidenceReference,
                ConfirmedAt = AverageWageConfirmedAt,
                ConfirmedBy = AverageWageConfirmedBy,
                ConfirmationReason = AverageWageConfirmationReason,
            }, Environment.UserName, default));
    }

    [RelayCommand]
    public async Task CancelAverageWageAsync() => await CancelAverageWageCoreAsync();

    [RelayCommand]
    public void ReenterAverageWage()
    {
        if (!EnsureCancelled(AverageWageCurrentHeadId, AverageWageEffectiveHeadId)) return;
        ClearAverageWageValues();
        _averageWageReentry = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveOfficeProfileAsync()
    {
        if (!EnsureLoaded() || !EnsureReentry(
                OfficeProfileCurrentHeadId, OfficeProfileEffectiveHeadId, _officeProfileReentry,
                "取消済みの事業所請求設定は「再入力」から入力してください。")) return;
        var kind = OfficeProfileCurrentHeadId is null ? RecordKind.New : RecordKind.Correct;
        await SaveAndReloadAsync(() => _setOfficeProfile.ExecuteAsync(
            new SetOfficeClaimProfileRequest(
                OfficeId, ProfileEffectiveFrom, ProfileEffectiveTo, kind,
                OfficeProfileCurrentHeadId)
            {
                MasterVersion = MasterVersion,
                ReformStatus = ReformStatus,
                AverageWageBandOption = AverageWageBandOption,
                DesignationDate = DesignationDate,
                SupportStartDate = SupportStartDate,
                EarlierRegisteredBandOption = EarlierRegisteredBandOption,
                EarlierRegistrationMonth = EarlierRegistrationMonth,
                LaterRegisteredBandOption = LaterRegisteredBandOption,
                LaterRegistrationMonth = LaterRegistrationMonth,
                ReformComparisonEvidenceDocumentId = ReformComparisonEvidenceDocumentId,
                FiledTransitionPeriod = FiledTransitionPeriod,
                FiledTransitionEvidenceDocumentId = FiledTransitionEvidenceDocumentId,
                EvidenceDocumentId = OfficeProfileEvidenceDocumentId,
                ConfirmedAt = OfficeProfileConfirmedAt,
                ConfirmedBy = OfficeProfileConfirmedBy,
                ConfirmationReason = OfficeProfileConfirmationReason,
                CapacityHeadcount = CapacityHeadcount,
                StaffingKey = StaffingKey,
                RegionKey = RegionKey,
            }, Environment.UserName, default));
    }

    [RelayCommand]
    public async Task CancelOfficeProfileAsync()
    {
        if (!EnsureLoaded() || OfficeProfileCurrentHeadId is null) return;
        await SaveAndReloadAsync(() => _setOfficeProfile.ExecuteAsync(
            new SetOfficeClaimProfileRequest(
                OfficeId, ProfileEffectiveFrom, ProfileEffectiveTo, RecordKind.Cancel,
                OfficeProfileCurrentHeadId), Environment.UserName, default));
    }

    [RelayCommand]
    public void ReenterOfficeProfile()
    {
        if (!EnsureCancelled(OfficeProfileCurrentHeadId, OfficeProfileEffectiveHeadId)) return;
        ClearOfficeProfileValues();
        _officeProfileReentry = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveCertificateEvidenceAsync()
    {
        if (!EnsureLoaded() || !EnsureReentry(
                CertificateEvidenceCurrentHeadId, CertificateEvidenceEffectiveHeadId,
                _certificateEvidenceReentry,
                "取消済みの受給者証請求根拠は「再入力」から入力してください。")) return;
        var kind = CertificateEvidenceCurrentHeadId is null ? RecordKind.New : RecordKind.Correct;
        await SaveAndReloadAsync(() => _setCertificateEvidence.ExecuteAsync(
            new SetCertificateClaimEvidenceRequest(
                CertificateId, new DateRange(CertificateValidityStart, CertificateValidityEnd),
                kind, CertificateEvidenceCurrentHeadId)
            {
                MonthlyCostCap = Money(MonthlyCostCapIsEntered, MonthlyCostCapYen),
                UpperLimitManagementApplicability = UpperLimitManagementApplicability,
                UpperLimitManagementOfficeNumber = UpperLimitManagementOfficeNumber,
                Article31Status = Article31Status,
                Article31AmountYen = Money(Article31AmountIsEntered, Article31AmountYen),
                Article31EffectivePeriod = Article31EffectivePeriod,
                OriginalDocumentReference = CertificateOriginalDocumentReference,
                ConfirmedAt = CertificateEvidenceConfirmedAt,
                ConfirmedBy = CertificateEvidenceConfirmedBy,
                ConfirmationReason = CertificateEvidenceConfirmationReason,
            }, Environment.UserName, default));
    }

    [RelayCommand]
    public async Task CancelCertificateEvidenceAsync()
    {
        if (!EnsureLoaded() || CertificateEvidenceCurrentHeadId is null) return;
        await SaveAndReloadAsync(() => _setCertificateEvidence.ExecuteAsync(
            new SetCertificateClaimEvidenceRequest(
                CertificateId, new DateRange(CertificateValidityStart, CertificateValidityEnd),
                RecordKind.Cancel, CertificateEvidenceCurrentHeadId),
            Environment.UserName, default));
    }

    [RelayCommand]
    public void ReenterCertificateEvidence()
    {
        if (!EnsureCancelled(CertificateEvidenceCurrentHeadId,
                CertificateEvidenceEffectiveHeadId)) return;
        ClearCertificateEvidenceValues();
        _certificateEvidenceReentry = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    public async Task SaveStatementAsync()
    {
        if (!EnsureLoaded() || !EnsureReentry(
                StatementCurrentHeadId, StatementEffectiveHeadId, _statementReentry,
                "取消済みの上限額管理結果票は「再入力」から入力してください。")) return;
        var kind = StatementCurrentHeadId is null ? RecordKind.New : RecordKind.Correct;
        await SaveAndReloadAsync(() => _setStatement.ExecuteAsync(
            new SetUpperLimitManagementStatementRequest(
                CurrentServiceMonth(), RecipientId, CertificateId, OfficeId, kind,
                StatementCurrentHeadId)
            {
                MunicipalityNumber = MunicipalityNumber,
                CertificateNumber = StatementCertificateNumber,
                CertificateMonthlyCostCap = Money(
                    StatementCertificateMonthlyCostCapIsEntered,
                    StatementCertificateMonthlyCostCapYen),
                UpperLimitManagementApplicability = StatementUpperLimitManagementApplicability,
                CertificateManagingOfficeNumber = CertificateManagingOfficeNumber,
                ManagingOfficeNumber = ManagingOfficeNumber,
                ManagingOfficeName = ManagingOfficeName,
                OriginalCreationKind = OriginalCreationKind,
                ReceivedAt = StatementReceivedAt,
                OriginalDocumentReference = StatementOriginalDocumentReference,
                IsConfirmed = StatementIsConfirmed,
                ConfirmedAt = StatementConfirmedAt,
                ConfirmedBy = StatementConfirmedBy,
                ConfirmationReason = StatementConfirmationReason,
                Result = StatementResult,
                TotalCostYen = Money(StatementTotalCostIsEntered, StatementTotalCostYen),
                TotalPreManagementBurdenYen = Money(
                    StatementTotalPreManagementBurdenIsEntered,
                    StatementTotalPreManagementBurdenYen),
                TotalManagedBurdenYen = Money(
                    StatementTotalManagedBurdenIsEntered,
                    StatementTotalManagedBurdenYen),
                Lines = StatementLines.OrderBy(line => line.LineNumber)
                    .Select(line => line.ToRequest()).ToArray(),
            }, Environment.UserName, default));
    }

    [RelayCommand]
    public async Task CancelStatementAsync()
    {
        if (!EnsureLoaded() || StatementCurrentHeadId is null) return;
        await SaveAndReloadAsync(() => _setStatement.ExecuteAsync(
            new SetUpperLimitManagementStatementRequest(
                CurrentServiceMonth(), RecipientId, CertificateId, OfficeId,
                RecordKind.Cancel, StatementCurrentHeadId),
            Environment.UserName, default));
    }

    [RelayCommand]
    public void ReenterStatement()
    {
        if (!EnsureCancelled(StatementCurrentHeadId, StatementEffectiveHeadId)) return;
        ClearStatementValues();
        _statementReentry = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    public void AddStatementLine()
    {
        var line = new UpperLimitManagementStatementLineViewModel
        {
            LineNumber = StatementLines.Count == 0
                ? 1 : StatementLines.Max(item => item.LineNumber) + 1,
        };
        StatementLines.Add(line);
        SelectedStatementLine = line;
    }

    [RelayCommand]
    public void RemoveStatementLine()
    {
        if (SelectedStatementLine is not { } selected) return;
        StatementLines.Remove(selected);
        SelectedStatementLine = null;
    }

    [RelayCommand]
    public async Task SaveCurrentAggregateAsync()
    {
        switch (SelectedAggregate)
        {
            case ClaimInputAggregate.ClaimInput: await SaveClaimInputAsync(); break;
            case ClaimInputAggregate.AverageWageAnnualEvidence: await SaveAverageWageAsync(); break;
            case ClaimInputAggregate.OfficeClaimProfile: await SaveOfficeProfileAsync(); break;
            case ClaimInputAggregate.CertificateClaimEvidence:
                await SaveCertificateEvidenceAsync(); break;
            case ClaimInputAggregate.UpperLimitManagementStatement:
                await SaveStatementAsync(); break;
        }
    }

    [RelayCommand]
    public void DiscardCurrentEdit()
    {
        switch (SelectedAggregate)
        {
            case ClaimInputAggregate.ClaimInput: ApplyClaimInputValues(_loadedClaimInput); break;
            case ClaimInputAggregate.AverageWageAnnualEvidence:
                ApplyAverageWageValues(AverageWageRevisions.LastOrDefault()); break;
            case ClaimInputAggregate.OfficeClaimProfile:
                ApplyOfficeProfileChain(SelectedOfficeProfileChain); break;
            case ClaimInputAggregate.CertificateClaimEvidence:
                ApplyCertificateEvidenceChain(SelectedCertificateEvidenceChain); break;
            case ClaimInputAggregate.UpperLimitManagementStatement:
                ApplyStatementValues(StatementRevisions.LastOrDefault()); break;
        }
        ErrorMessage = null;
    }

    partial void OnSelectedOfficeChanged(OfficeDto? value) => OfficeId = value?.Id ?? Guid.Empty;

    partial void OnSelectedRecipientChanged(RecipientDto? value)
    {
        RecipientId = value?.Id ?? Guid.Empty;
        SelectedCertificate = null;
        Certificates.Clear();
    }

    partial void OnSelectedCertificateChanged(CertificateDto? value) =>
        CertificateId = value?.Id ?? Guid.Empty;

    partial void OnOfficeIdChanged(Guid value) => ClearWorkspace();
    partial void OnRecipientIdChanged(Guid value) => ClearWorkspace();
    partial void OnCertificateIdChanged(Guid value) => ClearWorkspace();
    partial void OnYearChanged(int value) => ClearWorkspace();
    partial void OnMonthChanged(int value) => ClearWorkspace();
    partial void OnSourceFiscalYearChanged(int value) => ClearWorkspace();

    partial void OnSelectedOfficeProfileChainChanged(
        ClaimInputRevisionChainDto<OfficeClaimProfileQueryRevisionDto>? value) =>
        ApplyOfficeProfileChain(value);

    partial void OnSelectedCertificateEvidenceChainChanged(
        ClaimInputRevisionChainDto<CertificateClaimEvidenceQueryRevisionDto>? value) =>
        ApplyCertificateEvidenceChain(value);

    private async Task LoadCertificatesForRecipientAsync(Guid recipientId, CancellationToken ct)
    {
        Certificates.Clear();
        SelectedCertificate = null;
        if (recipientId == Guid.Empty) return;
        var certificates = await _listCertificates.ExecuteAsync(recipientId, ct);
        if (RecipientId != recipientId) return;
        Replace(Certificates, certificates);
    }

    private void RefreshBillingTokenOptions(ServiceMonth serviceMonth)
    {
        // StaffingKey/RegionKeyの選択肢はマスタ（staffing条件token・region-unit-price行）から
        // 列挙する。ハードコードしない（Task 9b）。
        var options = _queryBillingTokenOptions.Execute(serviceMonth);
        Replace(StaffingKeyOptions, options.StaffingKeyOptions);
        Replace(RegionKeyOptions, options.RegionKeyOptions);
    }

    private void ApplyWorkspace(ClaimInputWorkspaceDto workspace)
    {
        var claim = workspace.ClaimInputChain;
        ClaimInputCurrentHeadId = claim?.CurrentHeadId;
        ClaimInputEffectiveHeadId = claim?.EffectiveHeadId;
        Replace(ClaimInputRevisions, claim?.Revisions ?? []);
        _loadedClaimInput = Last(claim?.Revisions);
        ApplyClaimInputValues(_loadedClaimInput);

        var wage = workspace.AverageWageAnnualEvidenceChain;
        AverageWageCurrentHeadId = wage?.CurrentHeadId;
        AverageWageEffectiveHeadId = wage?.EffectiveHeadId;
        Replace(AverageWageRevisions, wage?.Revisions ?? []);
        ApplyAverageWageValues(Last(wage?.Revisions));

        Replace(OfficeProfileChains, workspace.OfficeClaimProfileChains);
        SelectedOfficeProfileChain = OfficeProfileChains.Count == 0
            ? null : OfficeProfileChains[0];
        Replace(CertificateEvidenceChains, workspace.CertificateClaimEvidenceChains);
        SelectedCertificateEvidenceChain = CertificateEvidenceChains.Count == 0
            ? null : CertificateEvidenceChains[0];

        var statement = workspace.UpperLimitManagementStatementChain;
        StatementCurrentHeadId = statement?.CurrentHeadId;
        StatementEffectiveHeadId = statement?.EffectiveHeadId;
        Replace(StatementRevisions, statement?.Revisions ?? []);
        ApplyStatementValues(Last(statement?.Revisions));
    }

    private void ApplyClaimInputValues(ClaimInputQueryRevisionDto? value)
    {
        UpperLimitManagementResult = value?.UpperLimitManagementResult;
        UpperLimitManagedAmountYen = value?.UpperLimitManagedAmountYen;
        MunicipalSubsidyAmountYen = value?.MunicipalSubsidyAmountYen;
        _claimInputReentry = false;
    }

    private void ApplyAverageWageValues(AverageWageAnnualEvidenceQueryRevisionDto? value)
    {
        AverageWagePeriodStart = value?.PeriodStart ?? default;
        AverageWagePeriodEnd = value?.PeriodEnd ?? default;
        AnnualWagePaidYen = value?.AnnualWagePaidYen;
        AnnualExtendedUsers = value?.AnnualExtendedUsers;
        AnnualOpeningDays = value?.AnnualOpeningDays;
        Completeness = value?.Completeness;
        AverageWageEvidenceDocumentId = value?.EvidenceDocumentId;
        DailyEvidenceReference = value?.DailyEvidenceReference;
        MonthlyEvidenceReference = value?.MonthlyEvidenceReference;
        AverageWageConfirmedAt = value?.ConfirmedAt;
        AverageWageConfirmedBy = value?.ConfirmedBy;
        AverageWageConfirmationReason = value?.ConfirmationReason;
        _averageWageReentry = false;
    }

    private void ApplyOfficeProfileChain(
        ClaimInputRevisionChainDto<OfficeClaimProfileQueryRevisionDto>? chain)
    {
        OfficeProfileCurrentHeadId = chain?.CurrentHeadId;
        OfficeProfileEffectiveHeadId = chain?.EffectiveHeadId;
        Replace(OfficeProfileRevisions, chain?.Revisions ?? []);
        var value = Last(chain?.Revisions);
        ProfileEffectiveFrom = value?.EffectiveFrom ?? default;
        ProfileEffectiveTo = value?.EffectiveTo;
        MasterVersion = value?.MasterVersion;
        ReformStatus = value?.ReformStatus;
        AverageWageBandOption = value?.AverageWageBandOption;
        DesignationDate = value?.DesignationDate;
        SupportStartDate = value?.SupportStartDate;
        EarlierRegisteredBandOption = value?.EarlierRegisteredBandOption;
        EarlierRegistrationMonth = value?.EarlierRegistrationMonth;
        LaterRegisteredBandOption = value?.LaterRegisteredBandOption;
        LaterRegistrationMonth = value?.LaterRegistrationMonth;
        ReformComparisonEvidenceDocumentId = value?.ReformComparisonEvidenceDocumentId;
        FiledTransitionPeriod = value?.FiledTransitionPeriod;
        FiledTransitionEvidenceDocumentId = value?.FiledTransitionEvidenceDocumentId;
        OfficeProfileEvidenceDocumentId = value?.EvidenceDocumentId;
        OfficeProfileConfirmedAt = value?.ConfirmedAt;
        OfficeProfileConfirmedBy = value?.ConfirmedBy;
        OfficeProfileConfirmationReason = value?.ConfirmationReason;
        CapacityHeadcount = value?.CapacityHeadcount;
        StaffingKey = value?.StaffingKey;
        RegionKey = value?.RegionKey;
        _officeProfileReentry = false;
    }

    private void ApplyCertificateEvidenceChain(
        ClaimInputRevisionChainDto<CertificateClaimEvidenceQueryRevisionDto>? chain)
    {
        CertificateEvidenceCurrentHeadId = chain?.CurrentHeadId;
        CertificateEvidenceEffectiveHeadId = chain?.EffectiveHeadId;
        Replace(CertificateEvidenceRevisions, chain?.Revisions ?? []);
        var value = Last(chain?.Revisions);
        CertificateValidityStart = value?.Validity.Start ?? default;
        CertificateValidityEnd = value?.Validity.End;
        MonthlyCostCapIsEntered = value?.MonthlyCostCap.IsEntered ?? false;
        MonthlyCostCapYen = value?.MonthlyCostCap.ValueYen;
        UpperLimitManagementApplicability =
            value?.UpperLimitManagementApplicability ?? default;
        UpperLimitManagementOfficeNumber = value?.UpperLimitManagementOfficeNumber;
        Article31Status = value?.Article31Status ?? default;
        Article31AmountIsEntered = value?.Article31AmountYen.IsEntered ?? false;
        Article31AmountYen = value?.Article31AmountYen.ValueYen;
        Article31EffectivePeriod = value?.Article31EffectivePeriod;
        CertificateOriginalDocumentReference = value?.OriginalDocumentReference;
        CertificateEvidenceConfirmedAt = value?.ConfirmedAt;
        CertificateEvidenceConfirmedBy = value?.ConfirmedBy;
        CertificateEvidenceConfirmationReason = value?.ConfirmationReason;
        _certificateEvidenceReentry = false;
    }

    private void ApplyStatementValues(UpperLimitManagementStatementQueryRevisionDto? value)
    {
        MunicipalityNumber = value?.MunicipalityNumber ?? string.Empty;
        StatementCertificateNumber = value?.CertificateNumber ?? string.Empty;
        StatementCertificateMonthlyCostCapIsEntered =
            value?.CertificateMonthlyCostCap.IsEntered ?? false;
        StatementCertificateMonthlyCostCapYen = value?.CertificateMonthlyCostCap.ValueYen;
        StatementUpperLimitManagementApplicability =
            value?.UpperLimitManagementApplicability ?? default;
        CertificateManagingOfficeNumber = value?.CertificateManagingOfficeNumber ?? string.Empty;
        ManagingOfficeNumber = value?.ManagingOfficeNumber ?? string.Empty;
        ManagingOfficeName = value?.ManagingOfficeName ?? string.Empty;
        OriginalCreationKind = value?.OriginalCreationKind ?? string.Empty;
        StatementReceivedAt = value?.ReceivedAt;
        StatementOriginalDocumentReference = value?.OriginalDocumentReference;
        StatementIsConfirmed = value?.IsConfirmed ?? false;
        StatementConfirmedAt = value?.ConfirmedAt;
        StatementConfirmedBy = value?.ConfirmedBy;
        StatementConfirmationReason = value?.ConfirmationReason;
        StatementResult = value?.Result
            ?? Tsumugi.Domain.Logic.Claim.Models.UpperLimitManagementResult.Result1;
        StatementTotalCostIsEntered = value?.TotalCostYen.IsEntered ?? false;
        StatementTotalCostYen = value?.TotalCostYen.ValueYen;
        StatementTotalPreManagementBurdenIsEntered =
            value?.TotalPreManagementBurdenYen.IsEntered ?? false;
        StatementTotalPreManagementBurdenYen = value?.TotalPreManagementBurdenYen.ValueYen;
        StatementTotalManagedBurdenIsEntered = value?.TotalManagedBurdenYen.IsEntered ?? false;
        StatementTotalManagedBurdenYen = value?.TotalManagedBurdenYen.ValueYen;
        Replace(StatementLines, value?.Lines.OrderBy(line => line.LineNumber)
            .Select(UpperLimitManagementStatementLineViewModel.FromDto) ?? []);
        _statementReentry = false;
    }

    private async Task CancelAverageWageCoreAsync()
    {
        if (!EnsureLoaded() || AverageWageCurrentHeadId is null) return;
        await SaveAndReloadAsync(() => _setAverageWage.ExecuteAsync(
            new SetAverageWageAnnualEvidenceRequest(
                OfficeId, SourceFiscalYear, AverageWagePeriodStart, AverageWagePeriodEnd,
                RecordKind.Cancel, AverageWageCurrentHeadId),
            Environment.UserName, default));
    }

    private async Task SaveAndReloadAsync(Func<Task<ClaimInputRevisionDto>> save)
    {
        try
        {
            await save();
            await LoadAsync();
        }
        catch (ClaimInputSaveException ex)
        {
            var message = ex.Code switch
            {
                ClaimInputSaveErrorCode.ExpectedHeadMismatch
                    or ClaimInputSaveErrorCode.InvalidHistory => ReloadMessage,
                ClaimInputSaveErrorCode.MasterUnavailable => MasterUnavailableMessage,
                _ => InvalidMessage,
            };
            await ReloadAfterFailureAsync(message);
        }
        catch (InvalidOperationException)
        {
            await ReloadAfterFailureAsync(ReloadMessage);
        }
        catch (ArgumentException)
        {
            await ReloadAfterFailureAsync(InvalidMessage);
        }
    }

    private async Task ReloadAfterFailureAsync(string message)
    {
        await LoadAsync();
        ErrorMessage = message;
    }

    private bool EnsureLoaded()
    {
        if (WorkspaceLoaded && _loadedContext is { } loaded && MatchesContext(loaded)) return true;
        ErrorMessage = ReloadMessage;
        return false;
    }

    private bool EnsureReentry(Guid? currentHeadId, Guid? effectiveHeadId, bool reentry,
        string message)
    {
        if (currentHeadId is null || effectiveHeadId is not null || reentry) return true;
        ErrorMessage = message;
        return false;
    }

    private bool EnsureCancelled(Guid? currentHeadId, Guid? effectiveHeadId)
    {
        if (!EnsureLoaded()) return false;
        if (currentHeadId is not null && effectiveHeadId is null) return true;
        ErrorMessage = "取消済みの履歴を選択してください。";
        return false;
    }

    private bool TryCaptureContext(out WorkspaceContext context)
    {
        context = default!;
        if (OfficeId == Guid.Empty || RecipientId == Guid.Empty || CertificateId == Guid.Empty
            || Year is < 1900 or > 2200 || Month is < 1 or > 12
            || SourceFiscalYear is < 1900 or > 2199) return false;
        context = new WorkspaceContext(
            OfficeId, RecipientId, CertificateId, new ServiceMonth(Year, Month), SourceFiscalYear);
        return true;
    }

    private bool MatchesContext(WorkspaceContext context) =>
        OfficeId == context.OfficeId && RecipientId == context.RecipientId
        && CertificateId == context.CertificateId && Year == context.ServiceMonth.Year
        && Month == context.ServiceMonth.Month && SourceFiscalYear == context.SourceFiscalYear;

    private ServiceMonth CurrentServiceMonth() => new(Year, Month);

    private void ClearWorkspace()
    {
        WorkspaceLoaded = false;
        _loadedContext = null;
        _loadedClaimInput = null;
        ClaimInputCurrentHeadId = null;
        ClaimInputEffectiveHeadId = null;
        AverageWageCurrentHeadId = null;
        AverageWageEffectiveHeadId = null;
        OfficeProfileCurrentHeadId = null;
        OfficeProfileEffectiveHeadId = null;
        CertificateEvidenceCurrentHeadId = null;
        CertificateEvidenceEffectiveHeadId = null;
        StatementCurrentHeadId = null;
        StatementEffectiveHeadId = null;
        ClaimInputRevisions.Clear();
        AverageWageRevisions.Clear();
        OfficeProfileChains.Clear();
        OfficeProfileRevisions.Clear();
        CertificateEvidenceChains.Clear();
        CertificateEvidenceRevisions.Clear();
        StatementRevisions.Clear();
        StatementLines.Clear();
        ApplyClaimInputValues(null);
        ApplyAverageWageValues(null);
        SelectedOfficeProfileChain = null;
        ApplyOfficeProfileChain(null);
        SelectedCertificateEvidenceChain = null;
        ApplyCertificateEvidenceChain(null);
        ApplyStatementValues(null);
        _claimInputReentry = false;
        _averageWageReentry = false;
        _officeProfileReentry = false;
        _certificateEvidenceReentry = false;
        _statementReentry = false;
    }

    private void ClearAverageWageValues()
    {
        AnnualWagePaidYen = null;
        AnnualExtendedUsers = null;
        AnnualOpeningDays = null;
        Completeness = null;
        AverageWageEvidenceDocumentId = null;
        DailyEvidenceReference = null;
        MonthlyEvidenceReference = null;
        AverageWageConfirmedAt = null;
        AverageWageConfirmedBy = null;
        AverageWageConfirmationReason = null;
    }

    private void ClearOfficeProfileValues()
    {
        MasterVersion = null;
        ReformStatus = null;
        AverageWageBandOption = null;
        DesignationDate = null;
        SupportStartDate = null;
        EarlierRegisteredBandOption = null;
        EarlierRegistrationMonth = null;
        LaterRegisteredBandOption = null;
        LaterRegistrationMonth = null;
        ReformComparisonEvidenceDocumentId = null;
        FiledTransitionPeriod = null;
        FiledTransitionEvidenceDocumentId = null;
        OfficeProfileEvidenceDocumentId = null;
        OfficeProfileConfirmedAt = null;
        OfficeProfileConfirmedBy = null;
        OfficeProfileConfirmationReason = null;
        CapacityHeadcount = null;
        StaffingKey = null;
        RegionKey = null;
    }

    private void ClearCertificateEvidenceValues()
    {
        MonthlyCostCapIsEntered = false;
        MonthlyCostCapYen = null;
        UpperLimitManagementApplicability = default;
        UpperLimitManagementOfficeNumber = null;
        Article31Status = default;
        Article31AmountIsEntered = false;
        Article31AmountYen = null;
        Article31EffectivePeriod = null;
        CertificateOriginalDocumentReference = null;
        CertificateEvidenceConfirmedAt = null;
        CertificateEvidenceConfirmedBy = null;
        CertificateEvidenceConfirmationReason = null;
    }
    private void ClearStatementValues() => ApplyStatementValues(null);

    private static EnteredYen Money(bool entered, int? value) => new(entered, value);

    private static T? Last<T>(IReadOnlyList<T>? items) where T : class =>
        items is { Count: > 0 } ? items[^1] : null;

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    private sealed record WorkspaceContext(
        Guid OfficeId,
        Guid RecipientId,
        Guid CertificateId,
        ServiceMonth ServiceMonth,
        int SourceFiscalYear);
}

public sealed partial class UpperLimitManagementStatementLineViewModel : ObservableObject
{
    [ObservableProperty] private int _lineNumber;
    [ObservableProperty] private string _officeNumber = string.Empty;
    [ObservableProperty] private string _officeName = string.Empty;
    [ObservableProperty] private bool _totalCostIsEntered;
    [ObservableProperty] private int? _totalCostYen;
    [ObservableProperty] private bool _preManagementBurdenIsEntered;
    [ObservableProperty] private int? _preManagementBurdenYen;
    [ObservableProperty] private bool _managedBurdenIsEntered;
    [ObservableProperty] private int? _managedBurdenYen;

    public UpperLimitManagementStatementLineRequest ToRequest() => new(
        LineNumber, OfficeNumber, OfficeName,
        new EnteredYen(TotalCostIsEntered, TotalCostYen),
        new EnteredYen(PreManagementBurdenIsEntered, PreManagementBurdenYen),
        new EnteredYen(ManagedBurdenIsEntered, ManagedBurdenYen));

    public static UpperLimitManagementStatementLineViewModel FromDto(
        UpperLimitManagementStatementLineQueryDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new()
        {
            LineNumber = value.LineNumber,
            OfficeNumber = value.OfficeNumber,
            OfficeName = value.OfficeName,
            TotalCostIsEntered = value.TotalCostYen.IsEntered,
            TotalCostYen = value.TotalCostYen.ValueYen,
            PreManagementBurdenIsEntered = value.PreManagementBurdenYen.IsEntered,
            PreManagementBurdenYen = value.PreManagementBurdenYen.ValueYen,
            ManagedBurdenIsEntered = value.ManagedBurdenYen.IsEntered,
            ManagedBurdenYen = value.ManagedBurdenYen.ValueYen,
        };
    }
}
