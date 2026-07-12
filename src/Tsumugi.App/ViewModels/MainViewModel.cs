using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Tsumugi.App.Navigation;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>MainWindow の DataContext。各 ViewModel と画面間navigationを調停する。</summary>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SemaphoreSlim _navigationGate = new(1, 1);

    public MainViewModel(
        RecipientListViewModel recipientList,
        RecipientEditViewModel recipientEdit,
        DisabilityCertificateViewModel disabilityCertificate,
        FaceSheetViewModel faceSheet,
        CertificateViewModel certificate,
        ContractViewModel contract,
        OfficeViewModel office,
        OfficeCapabilityViewModel officeCapability,
        DailyRecordViewModel dailyRecord,
        WorkRecordViewModel workRecord,
        WageFundSettingsViewModel wageFundSettings,
        RecipientHourlyRateViewModel recipientHourlyRate,
        WageAdjustmentViewModel wageAdjustment,
        WageCalculationViewModel wageCalculation,
        WageStatementViewModel wageStatement,
        ClaimInputViewModel claimInput,
        IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        RecipientList = recipientList;
        RecipientEdit = recipientEdit;
        DisabilityCertificate = disabilityCertificate;
        FaceSheet = faceSheet;
        Certificate = certificate;
        Contract = contract;
        Office = office;
        OfficeCapability = officeCapability;
        DailyRecord = dailyRecord;
        WorkRecord = workRecord;
        WageFundSettings = wageFundSettings;
        RecipientHourlyRate = recipientHourlyRate;
        WageAdjustment = wageAdjustment;
        WageCalculation = wageCalculation;
        WageStatement = wageStatement;
        ClaimInput = claimInput;
        messenger.Register<MainViewModel, AppNavigationMessage>(
            this,
            static (recipient, message) => message.Reply(
                recipient.HandleNavigationAsync(
                    message.Request,
                    message.CancellationToken)));
    }

    [ObservableProperty]
    private AppSection _selectedSection = AppSection.RecipientList;

    [ObservableProperty]
    private NavigationResult? _lastNavigationResult;

    public RecipientListViewModel RecipientList { get; }
    public RecipientEditViewModel RecipientEdit { get; }
    public DisabilityCertificateViewModel DisabilityCertificate { get; }
    public FaceSheetViewModel FaceSheet { get; }
    public CertificateViewModel Certificate { get; }
    public ContractViewModel Contract { get; }
    public OfficeViewModel Office { get; }
    public OfficeCapabilityViewModel OfficeCapability { get; }
    public DailyRecordViewModel DailyRecord { get; }

    // Phase 2 tabs
    public WorkRecordViewModel WorkRecord { get; }
    public WageFundSettingsViewModel WageFundSettings { get; }
    public WageCalculationViewModel WageCalculation { get; }
    public WageStatementViewModel WageStatement { get; }

    public ClaimInputViewModel ClaimInput { get; }

    // Phase 4 S0 tabs
    public RecipientHourlyRateViewModel RecipientHourlyRate { get; }
    public WageAdjustmentViewModel WageAdjustment { get; }

    public void Dispose() => _navigationGate.Dispose();

    private async Task<NavigationResult> HandleNavigationAsync(
        NavigationRequest request,
        CancellationToken ct)
    {
        await _navigationGate.WaitAsync(ct);
        try
        {
            NavigationResult result;
            try
            {
                result = await DispatchAsync(request, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                result = NavigationResult.Failure(
                    request,
                    NavigationErrorCode.InvalidNavigationContext);
            }

            LastNavigationResult = result;
            if (result.IsSuccess)
                SelectedSection = request.Section;
            return result;
        }
        finally
        {
            _navigationGate.Release();
        }
    }

    private async Task<NavigationResult> DispatchAsync(
        NavigationRequest request,
        CancellationToken ct)
    {
        if (request.Section == AppSection.ClaimPreparation)
        {
            return NavigationResult.Failure(
                request,
                NavigationErrorCode.NavigationTargetUnavailable);
        }

        var isValid = request.Section switch
        {
            AppSection.Certificate => await ApplyCertificateContextAsync(request, ct),
            AppSection.DailyRecord => await ApplyDailyRecordContextAsync(request, ct),
            AppSection.Office => await ApplyOfficeContextAsync(request, ct),
            AppSection.ClaimInput => await ApplyClaimInputContextAsync(request, ct),
            AppSection.RecipientList
                or AppSection.RecipientEdit
                or AppSection.DisabilityCertificate
                or AppSection.FaceSheet
                or AppSection.Contract
                or AppSection.OfficeCapability
                or AppSection.WorkRecord
                or AppSection.WageFundSettings
                or AppSection.RecipientHourlyRate
                or AppSection.WageAdjustment
                or AppSection.WageCalculation
                or AppSection.WageStatement => HasNoContext(request),
            _ => false,
        };

        return isValid
            ? NavigationResult.Success(request)
            : NavigationResult.Failure(
                request,
                NavigationErrorCode.InvalidNavigationContext);
    }

    private async Task<bool> ApplyCertificateContextAsync(
        NavigationRequest request,
        CancellationToken ct)
    {
        if (request.OfficeId is not null
            || request.ServiceMonth is not null
            || HasEmptyGuid(request.RecipientId)
            || HasEmptyGuid(request.CertificateId)
            || request.CertificateId is not null && request.RecipientId is null)
            return false;

        return await Certificate.ApplyNavigationContextAsync(
            request.RecipientId,
            request.ServiceDate,
            request.CertificateId,
            ct);
    }

    private async Task<bool> ApplyDailyRecordContextAsync(
        NavigationRequest request,
        CancellationToken ct)
    {
        if (request.CertificateId is not null
            || HasEmptyGuid(request.RecipientId)
            || HasEmptyGuid(request.OfficeId)
            || !TryGetServiceMonth(request.ServiceMonth, out var year, out var month)
            || request.ServiceDate is { } date
                && request.ServiceMonth is not null
                && (date.Year != year || date.Month != month))
            return false;

        return await DailyRecord.ApplyNavigationContextAsync(
            request.OfficeId,
            request.RecipientId,
            request.ServiceDate,
            request.ServiceMonth,
            ct);
    }

    private async Task<bool> ApplyOfficeContextAsync(
        NavigationRequest request,
        CancellationToken ct)
    {
        if (request.RecipientId is not null
            || request.ServiceDate is not null
            || request.CertificateId is not null
            || request.ServiceMonth is not null
            || HasEmptyGuid(request.OfficeId))
            return false;

        return await Office.ApplyNavigationContextAsync(request.OfficeId, ct);
    }

    private async Task<bool> ApplyClaimInputContextAsync(
        NavigationRequest request,
        CancellationToken ct)
    {
        if (request.ServiceDate is not null
            || HasEmptyGuid(request.OfficeId) || HasEmptyGuid(request.RecipientId)
            || HasEmptyGuid(request.CertificateId) || request.OfficeId is null
            || request.RecipientId is null || request.CertificateId is null
            || request.ServiceMonth is null
            || !TryGetServiceMonth(request.ServiceMonth, out _, out _))
            return false;

        return await ClaimInput.ApplyNavigationContextAsync(
            request.OfficeId,
            request.RecipientId,
            request.CertificateId,
            request.ServiceMonth,
            ct);
    }

    private static bool HasNoContext(NavigationRequest request) =>
        request.RecipientId is null
        && request.ServiceDate is null
        && request.CertificateId is null
        && request.OfficeId is null
        && request.ServiceMonth is null;

    private static bool HasEmptyGuid(Guid? value) => value is { } id && id == Guid.Empty;

    private static bool TryGetServiceMonth(
        ServiceMonth? value,
        out int year,
        out int month)
    {
        year = 0;
        month = 0;
        if (value is not { } serviceMonth)
            return true;

        try
        {
            year = serviceMonth.Year;
            month = serviceMonth.Month;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
