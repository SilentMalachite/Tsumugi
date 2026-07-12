using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 受給者証管理画面の ViewModel。MHLW 様式の主要セクションを入力できる。
///   1. 支給決定障害者等情報 (利用者選択 + 住所/性別/保護者)
///   2. 障害種別 (4 分類 + 障害支援区分)
///   3. 給付種別と支給決定内容 (期間/支給量/月額上限/特記事項)
///   4. 計画相談支援
///   5. 利用者負担に関する事項
///   6. サービス事業者記入欄 (ContractedProvider 別子コレクション)
/// </summary>
public sealed partial class CertificateViewModel(
    ListExpiringCertificatesUseCase listExpiring,
    RegisterCertificateUseCase registerUseCase,
    ListRecipientsUseCase listRecipients,
    ListCertificatesByRecipientUseCase listByRecipient,
    RegisterContractedProviderUseCase registerProvider,
    ListContractedProvidersUseCase listProviders) : ViewModelBase
{
    public ObservableCollection<ExpiringCertificateDto> ExpiringItems { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();
    public ObservableCollection<CertificateDto> CertificatesForRecipient { get; } = new();
    public ObservableCollection<ContractedProviderDto> ContractedProviders { get; } = new();

    public IReadOnlyList<Gender> GenderOptions { get; } =
        new[] { Gender.Unspecified, Gender.Male, Gender.Female, Gender.Other };
    public IReadOnlyList<BenefitType> BenefitTypeOptions { get; } =
        new[] { BenefitType.Care, BenefitType.Training, BenefitType.ChildSupport };
    public IReadOnlyList<SupportCategory> SupportCategoryOptions { get; } = new[]
    {
        SupportCategory.None,
        SupportCategory.Category1, SupportCategory.Category2, SupportCategory.Category3,
        SupportCategory.Category4, SupportCategory.Category5, SupportCategory.Category6,
    };
    public IReadOnlyList<PaymentBurdenCategory> PaymentBurdenOptions { get; } = new[]
    {
        PaymentBurdenCategory.Unspecified,
        PaymentBurdenCategory.Welfare,
        PaymentBurdenCategory.LowIncome,
        PaymentBurdenCategory.General1,
        PaymentBurdenCategory.General2,
    };

    [ObservableProperty] private int _thresholdDays = 30;
    [ObservableProperty] private DateOnly _asOfDate = DateOnly.FromDateTime(DateTime.Today);

    // 1. 支給決定障害者等
    [ObservableProperty] private RecipientDto? _selectedRecipient;
    [ObservableProperty] private Guid _recipientId;
    [ObservableProperty] private string _certificateNumber = string.Empty;
    [ObservableProperty] private string _recipientAddress = string.Empty;
    [ObservableProperty] private Gender _recipientGender = Gender.Unspecified;
    [ObservableProperty] private string _guardianName = string.Empty;
    [ObservableProperty] private string _guardianRelationship = string.Empty;

    // 2. 障害種別
    [ObservableProperty] private bool _disabilityPhysical;
    [ObservableProperty] private bool _disabilityIntellectual;
    [ObservableProperty] private bool _disabilityMental;
    [ObservableProperty] private bool _disabilityIntractable;
    [ObservableProperty] private SupportCategory _supportCategory = SupportCategory.None;

    // 3. 給付種別と支給決定内容
    [ObservableProperty] private BenefitType _benefitType = BenefitType.Training;
    [ObservableProperty] private string _serviceCategory = "就労継続支援B型";
    [ObservableProperty] private DateOnly _validityStart = new(2026, 4, 1);
    [ObservableProperty] private DateOnly? _validityEnd;
    [ObservableProperty] private int _supplyDays = 23;
    [ObservableProperty] private int _monthlyCostCap;
    [ObservableProperty] private string _municipality = string.Empty;
    [ObservableProperty] private string _municipalityNumber = string.Empty;
    [ObservableProperty] private string _supplyNotes = string.Empty;

    // 4. 計画相談支援
    [ObservableProperty] private string _consultationProviderName = string.Empty;
    [ObservableProperty] private string _consultationProviderNumber = string.Empty;
    [ObservableProperty] private DateOnly? _consultationStart;
    [ObservableProperty] private DateOnly? _consultationEnd;

    // 5. 利用者負担に関する事項
    [ObservableProperty] private PaymentBurdenCategory _paymentBurden = PaymentBurdenCategory.Unspecified;
    [ObservableProperty] private string _upperLimitManagementProvider = string.Empty;
    [ObservableProperty] private bool _mealProvisionApplicable;
    [ObservableProperty] private bool _highCostBenefitApplicable;

    // 共通
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private string? _overlapWarning;
    [ObservableProperty] private bool _isSaved;

    // 6. サービス事業者記入欄（受給者証選択後に Add）
    [ObservableProperty] private CertificateDto? _selectedCertificate;
    [ObservableProperty] private string _providerNumber = string.Empty;
    [ObservableProperty] private string _providerName = string.Empty;
    [ObservableProperty] private string _providerServiceCategory = "就労継続支援B型";
    [ObservableProperty] private int _providerSupplyDays = 23;
    [ObservableProperty] private DateOnly _providerContractDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly? _providerTerminationDate;
    [ObservableProperty] private string _providerNotes = string.Empty;
    [ObservableProperty] private string? _providerSaveErrorMessage;

    partial void OnSelectedRecipientChanged(RecipientDto? value)
    {
        RecipientId = value?.Id ?? Guid.Empty;
        _ = ReloadCertificatesAsync();
    }

    partial void OnSelectedCertificateChanged(CertificateDto? value)
        => _ = ReloadProvidersAsync();

    /// <summary>View の Loaded から呼ばれる初期化フック。利用者一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadRecipientsAsync(ct);

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    [RelayCommand]
    public async Task RefreshAsync()
        => await LoadAsync(AsOfDate, ThresholdDays);

    public async Task LoadAsync(DateOnly asOf, int thresholdDays)
    {
        ExpiringItems.Clear();
        var hits = await listExpiring.ExecuteAsync(asOf, thresholdDays, default);
        foreach (var h in hits) ExpiringItems.Add(h);
    }

    private async Task ReloadCertificatesAsync()
    {
        CertificatesForRecipient.Clear();
        SelectedCertificate = null;
        if (RecipientId == Guid.Empty) return;
        var list = await listByRecipient.ExecuteAsync(RecipientId, default);
        foreach (var c in list) CertificatesForRecipient.Add(c);
    }

    private async Task ReloadProvidersAsync()
    {
        ContractedProviders.Clear();
        if (SelectedCertificate is not { } cert) return;
        var list = await listProviders.ExecuteAsync(cert.Id, default);
        foreach (var p in list) ContractedProviders.Add(p);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var input = new RegisterCertificateInput(
                RecipientId, CertificateNumber,
                new DateRange(ValidityStart, ValidityEnd),
                SupplyDays, MonthlyCostCap, Municipality)
            {
                RecipientAddress = NullIfEmpty(RecipientAddress),
                RecipientGender = RecipientGender,
                GuardianName = NullIfEmpty(GuardianName),
                GuardianRelationship = NullIfEmpty(GuardianRelationship),
                Disabilities = new DisabilityCategories(
                    DisabilityPhysical, DisabilityIntellectual, DisabilityMental, DisabilityIntractable),
                SupportCategory = SupportCategory,
                BenefitType = BenefitType,
                ServiceCategory = ServiceCategory,
                SupplyNotes = NullIfEmpty(SupplyNotes),
                ConsultationProviderName = NullIfEmpty(ConsultationProviderName),
                ConsultationProviderNumber = NullIfEmpty(ConsultationProviderNumber),
                ConsultationStart = ConsultationStart,
                ConsultationEnd = ConsultationEnd,
                PaymentBurden = PaymentBurden,
                UpperLimitManagementProvider = NullIfEmpty(UpperLimitManagementProvider),
                MealProvisionApplicable = MealProvisionApplicable,
                HighCostBenefitApplicable = HighCostBenefitApplicable,
                MunicipalityNumber = NullIfEmpty(MunicipalityNumber),
            };

            var (_, warnings) = await registerUseCase.ExecuteAsync(input, Environment.UserName, default);
            SaveErrorMessage = null;
            OverlapWarning = warnings.Count > 0 ? string.Join(" ", warnings) : null;
            IsSaved = true;
            await ReloadCertificatesAsync();
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    [RelayCommand]
    private async Task AddProviderAsync()
    {
        try
        {
            if (SelectedCertificate is not { } cert)
            {
                ProviderSaveErrorMessage = "対象の受給者証を選択してください。";
                return;
            }
            await registerProvider.ExecuteAsync(
                cert.Id, ProviderNumber, ProviderName, ProviderServiceCategory,
                ProviderSupplyDays, ProviderContractDate, ProviderTerminationDate,
                NullIfEmpty(ProviderNotes),
                Environment.UserName, default);
            ProviderSaveErrorMessage = null;
            // フォームを初期化
            ProviderNumber = string.Empty;
            ProviderName = string.Empty;
            ProviderNotes = string.Empty;
            ProviderTerminationDate = null;
            await ReloadProvidersAsync();
        }
        catch (ArgumentException ex)
        {
            ProviderSaveErrorMessage = ex.Message;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
