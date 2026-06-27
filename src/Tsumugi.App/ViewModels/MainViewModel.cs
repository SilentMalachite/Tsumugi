namespace Tsumugi.App.ViewModels;

/// <summary>MainWindow の DataContext。各 ViewModel をタブ対応プロパティとして公開する。</summary>
public sealed class MainViewModel(
    RecipientListViewModel recipientList,
    RecipientEditViewModel recipientEdit,
    CertificateViewModel certificate,
    ContractViewModel contract,
    OfficeViewModel office,
    DailyRecordViewModel dailyRecord) : ViewModelBase
{
    public RecipientListViewModel RecipientList { get; } = recipientList;
    public RecipientEditViewModel RecipientEdit { get; } = recipientEdit;
    public CertificateViewModel Certificate { get; } = certificate;
    public ContractViewModel Contract { get; } = contract;
    public OfficeViewModel Office { get; } = office;
    public DailyRecordViewModel DailyRecord { get; } = dailyRecord;
}
