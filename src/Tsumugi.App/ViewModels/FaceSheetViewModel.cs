using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// フェースシート画面 VM。利用者を選択すると現行（最新）スナップショットを読み出し、
/// 保存すると新バージョンを追記する（append-only）。
/// </summary>
public sealed partial class FaceSheetViewModel(
    ListRecipientsUseCase listRecipients,
    GetLatestFaceSheetUseCase getLatest,
    SaveFaceSheetUseCase saveUseCase) : ViewModelBase
{
    public ObservableCollection<RecipientDto> Recipients { get; } = new();

    [ObservableProperty] private RecipientDto? _selectedRecipient;

    // 連絡先
    [ObservableProperty] private string _postalCode = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _emailAddress = string.Empty;
    [ObservableProperty] private string _emergencyContactName = string.Empty;
    [ObservableProperty] private string _emergencyContactRelationship = string.Empty;
    [ObservableProperty] private string _emergencyContactPhone = string.Empty;

    // 家族
    [ObservableProperty] private string _familyComposition = string.Empty;
    [ObservableProperty] private string _cohabitants = string.Empty;

    // 医療
    [ObservableProperty] private string _primaryDoctorName = string.Empty;
    [ObservableProperty] private string _primaryDoctorHospital = string.Empty;
    [ObservableProperty] private string _primaryDoctorPhone = string.Empty;
    [ObservableProperty] private string _medicalHistory = string.Empty;
    [ObservableProperty] private string _currentConditions = string.Empty;
    [ObservableProperty] private string _medications = string.Empty;
    [ObservableProperty] private string _allergies = string.Empty;

    // 受給状況
    [ObservableProperty] private bool _receivesNursingInsurance;
    [ObservableProperty] private bool _receivesDisabilityPension;
    [ObservableProperty] private string _pensionDetails = string.Empty;

    // 生活歴・意向
    [ObservableProperty] private string _lifeHistory = string.Empty;
    [ObservableProperty] private string _personalWishes = string.Empty;
    [ObservableProperty] private string _supportNeeds = string.Empty;
    [ObservableProperty] private string _assessmentSummary = string.Empty;

    [ObservableProperty] private DateTimeOffset? _lastUpdatedAt;
    [ObservableProperty] private string? _lastUpdatedBy;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => _ = LoadLatestAsync();

    public Task InitializeAsync(CancellationToken ct = default) => LoadRecipientsAsync(ct);

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    private async Task LoadLatestAsync()
    {
        ClearForm();
        if (SelectedRecipient is not { } r) return;
        var sheet = await getLatest.ExecuteAsync(r.Id, default);
        if (sheet is null) return;
        ApplyFromDto(sheet);
    }

    private void ClearForm()
    {
        PostalCode = string.Empty;
        Address = string.Empty;
        PhoneNumber = string.Empty;
        EmailAddress = string.Empty;
        EmergencyContactName = string.Empty;
        EmergencyContactRelationship = string.Empty;
        EmergencyContactPhone = string.Empty;
        FamilyComposition = string.Empty;
        Cohabitants = string.Empty;
        PrimaryDoctorName = string.Empty;
        PrimaryDoctorHospital = string.Empty;
        PrimaryDoctorPhone = string.Empty;
        MedicalHistory = string.Empty;
        CurrentConditions = string.Empty;
        Medications = string.Empty;
        Allergies = string.Empty;
        ReceivesNursingInsurance = false;
        ReceivesDisabilityPension = false;
        PensionDetails = string.Empty;
        LifeHistory = string.Empty;
        PersonalWishes = string.Empty;
        SupportNeeds = string.Empty;
        AssessmentSummary = string.Empty;
        LastUpdatedAt = null;
        LastUpdatedBy = null;
        SaveErrorMessage = null;
        IsSaved = false;
    }

    private void ApplyFromDto(FaceSheetDto d)
    {
        PostalCode = d.PostalCode ?? string.Empty;
        Address = d.Address ?? string.Empty;
        PhoneNumber = d.PhoneNumber ?? string.Empty;
        EmailAddress = d.EmailAddress ?? string.Empty;
        EmergencyContactName = d.EmergencyContactName ?? string.Empty;
        EmergencyContactRelationship = d.EmergencyContactRelationship ?? string.Empty;
        EmergencyContactPhone = d.EmergencyContactPhone ?? string.Empty;
        FamilyComposition = d.FamilyComposition ?? string.Empty;
        Cohabitants = d.Cohabitants ?? string.Empty;
        PrimaryDoctorName = d.PrimaryDoctorName ?? string.Empty;
        PrimaryDoctorHospital = d.PrimaryDoctorHospital ?? string.Empty;
        PrimaryDoctorPhone = d.PrimaryDoctorPhone ?? string.Empty;
        MedicalHistory = d.MedicalHistory ?? string.Empty;
        CurrentConditions = d.CurrentConditions ?? string.Empty;
        Medications = d.Medications ?? string.Empty;
        Allergies = d.Allergies ?? string.Empty;
        ReceivesNursingInsurance = d.ReceivesNursingInsurance;
        ReceivesDisabilityPension = d.ReceivesDisabilityPension;
        PensionDetails = d.PensionDetails ?? string.Empty;
        LifeHistory = d.LifeHistory ?? string.Empty;
        PersonalWishes = d.PersonalWishes ?? string.Empty;
        SupportNeeds = d.SupportNeeds ?? string.Empty;
        AssessmentSummary = d.AssessmentSummary ?? string.Empty;
        LastUpdatedAt = d.CreatedAt;
        LastUpdatedBy = d.CreatedBy;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (SelectedRecipient is not { } r)
            {
                SaveErrorMessage = "利用者を選択してください。";
                IsSaved = false;
                return;
            }
            var input = new SaveFaceSheetInput(r.Id)
            {
                PostalCode = N(PostalCode),
                Address = N(Address),
                PhoneNumber = N(PhoneNumber),
                EmailAddress = N(EmailAddress),
                EmergencyContactName = N(EmergencyContactName),
                EmergencyContactRelationship = N(EmergencyContactRelationship),
                EmergencyContactPhone = N(EmergencyContactPhone),
                FamilyComposition = N(FamilyComposition),
                Cohabitants = N(Cohabitants),
                PrimaryDoctorName = N(PrimaryDoctorName),
                PrimaryDoctorHospital = N(PrimaryDoctorHospital),
                PrimaryDoctorPhone = N(PrimaryDoctorPhone),
                MedicalHistory = N(MedicalHistory),
                CurrentConditions = N(CurrentConditions),
                Medications = N(Medications),
                Allergies = N(Allergies),
                ReceivesNursingInsurance = ReceivesNursingInsurance,
                ReceivesDisabilityPension = ReceivesDisabilityPension,
                PensionDetails = N(PensionDetails),
                LifeHistory = N(LifeHistory),
                PersonalWishes = N(PersonalWishes),
                SupportNeeds = N(SupportNeeds),
                AssessmentSummary = N(AssessmentSummary),
            };
            var saved = await saveUseCase.ExecuteAsync(input, Environment.UserName, default);
            LastUpdatedAt = saved.CreatedAt;
            LastUpdatedBy = saved.CreatedBy;
            SaveErrorMessage = null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    private static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
