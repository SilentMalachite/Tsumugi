namespace Tsumugi.App.ViewModels;

/// <summary>MainWindow の DataContext。各 ViewModel をタブ対応プロパティとして公開する。</summary>
public sealed class MainViewModel(
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
    WageCalculationViewModel wageCalculation,
    WageStatementViewModel wageStatement) : ViewModelBase
{
    public RecipientListViewModel RecipientList { get; } = recipientList;
    public RecipientEditViewModel RecipientEdit { get; } = recipientEdit;
    public DisabilityCertificateViewModel DisabilityCertificate { get; } = disabilityCertificate;
    public FaceSheetViewModel FaceSheet { get; } = faceSheet;
    public CertificateViewModel Certificate { get; } = certificate;
    public ContractViewModel Contract { get; } = contract;
    public OfficeViewModel Office { get; } = office;
    public OfficeCapabilityViewModel OfficeCapability { get; } = officeCapability;
    public DailyRecordViewModel DailyRecord { get; } = dailyRecord;

    // Phase 2 tabs
    public WorkRecordViewModel WorkRecord { get; } = workRecord;
    public WageFundSettingsViewModel WageFundSettings { get; } = wageFundSettings;
    public WageCalculationViewModel WageCalculation { get; } = wageCalculation;
    public WageStatementViewModel WageStatement { get; } = wageStatement;
}
