using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 障害者手帳画面 VM。利用者を選択し、その利用者の手帳履歴を一覧 + 新規追加する。
/// 厳密追記（更新は新レコード）のため等級改定/更新は新規登録として記録する。
/// </summary>
public sealed partial class DisabilityCertificateViewModel(
    ListRecipientsUseCase listRecipients,
    RegisterDisabilityCertificateUseCase registerUseCase,
    ListDisabilityCertificatesUseCase listUseCase,
    QueryDisabilityCertificateRenewalsUseCase queryRenewals,
    QueryDisabilityConsistencyUseCase queryConsistency) : ViewModelBase
{
    public ObservableCollection<RecipientDto> Recipients { get; } = new();
    public ObservableCollection<DisabilityCertificateDto> Items { get; } = new();
    public ObservableCollection<RenewalDueDisplayItem> RenewalDueItems { get; } = new();
    public ObservableCollection<string> ConsistencyWarnings { get; } = new();

    public IReadOnlyList<DisabilityCertificateType> TypeOptions { get; } = new[]
    {
        DisabilityCertificateType.Physical,
        DisabilityCertificateType.Intellectual,
        DisabilityCertificateType.Mental,
    };

    [ObservableProperty] private RecipientDto? _selectedRecipient;

    // 追加フォーム
    [ObservableProperty] private DisabilityCertificateType _type = DisabilityCertificateType.Physical;
    [ObservableProperty] private string _grade = string.Empty;
    [ObservableProperty] private string _subtype = string.Empty;
    [ObservableProperty] private DateOnly _issuedDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private DateOnly? _nextRenewalDate;
    [ObservableProperty] private string _issuingAuthority = string.Empty;
    [ObservableProperty] private string _certificateNumber = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;
    [ObservableProperty] private int _thresholdDays = 30;
    [ObservableProperty] private DateOnly _asOfDate = DateOnly.FromDateTime(DateTime.Today);

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => _ = ReloadForRecipientAsync();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadRecipientsAsync(ct);
        await RefreshAlertsAsync();
    }

    public async Task LoadRecipientsAsync(CancellationToken ct = default)
    {
        var list = await listRecipients.ExecuteAsync(includeArchived: false, ct);
        Recipients.Clear();
        foreach (var r in list) Recipients.Add(r);
    }

    private async Task ReloadAsync()
    {
        Items.Clear();
        if (SelectedRecipient is not { } r) return;
        var list = await listUseCase.ExecuteAsync(r.Id, default);
        foreach (var c in list) Items.Add(c);
    }

    private async Task ReloadForRecipientAsync()
    {
        await ReloadAsync();
        await ReloadConsistencyWarningsAsync();
    }

    [RelayCommand]
    public async Task RefreshAlertsAsync()
    {
        var alerts = await queryRenewals.ExecuteAsync(AsOfDate, ThresholdDays, default);
        var recipientsForAlerts = await listRecipients.ExecuteAsync(includeArchived: true, default);
        RenewalDueItems.Clear();
        foreach (var alert in alerts)
        {
            var recipientName = recipientsForAlerts
                .SingleOrDefault(recipient => recipient.Id == alert.RecipientId)?.KanjiName;
            RenewalDueItems.Add(new RenewalDueDisplayItem(
                alert.RecipientId, recipientName, alert.NextRenewalDate, alert.RemainingDays));
        }
        if (SelectedRecipient is not null)
            await ReloadConsistencyWarningsAsync();
    }

    private async Task ReloadConsistencyWarningsAsync()
    {
        ConsistencyWarnings.Clear();
        if (SelectedRecipient is not { } recipient) return;
        var warnings = await queryConsistency.ExecuteAsync(recipient.Id, AsOfDate, default);
        foreach (var warning in warnings) ConsistencyWarnings.Add(FormatConsistencyWarning(warning));
    }

    private static string FormatConsistencyWarning(DisabilityConsistencyWarningDto warning) =>
        $"{(warning.Type switch
        {
            DisabilityCertificateType.Physical => "身体障害",
            DisabilityCertificateType.Intellectual => "知的障害",
            DisabilityCertificateType.Mental => "精神障害",
            _ => throw new ArgumentOutOfRangeException(nameof(warning)),
        })}は{(warning.Direction switch
        {
            Tsumugi.Domain.Logic.DisabilityConsistencyDirection.CertificateOnly => "受給者証にはありますが、対応する手帳がありません。",
            Tsumugi.Domain.Logic.DisabilityConsistencyDirection.HandbookOnly => "手帳にはありますが、受給者証にありません。",
            _ => throw new ArgumentOutOfRangeException(nameof(warning)),
        })}";

    [RelayCommand]
    private async Task AddAsync()
    {
        try
        {
            if (SelectedRecipient is not { } r)
            {
                SaveErrorMessage = "利用者を選択してください。";
                IsSaved = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(Grade))
            {
                SaveErrorMessage = "等級は必須です。";
                IsSaved = false;
                return;
            }
            if (string.IsNullOrWhiteSpace(IssuingAuthority))
            {
                SaveErrorMessage = "発行自治体は必須です。";
                IsSaved = false;
                return;
            }

            await registerUseCase.ExecuteAsync(
                r.Id, Type, Grade, IssuedDate, IssuingAuthority,
                Environment.UserName, default,
                subtype: NullIfEmpty(Subtype),
                nextRenewalDate: NextRenewalDate,
                certificateNumber: NullIfEmpty(CertificateNumber),
                notes: NullIfEmpty(Notes));

            SaveErrorMessage = null;
            IsSaved = true;

            // 入力リセット（次の入力に備える）
            Grade = string.Empty;
            Subtype = string.Empty;
            CertificateNumber = string.Empty;
            Notes = string.Empty;
            NextRenewalDate = null;

            await ReloadAsync();
            await RefreshAlertsAsync();
            await ReloadConsistencyWarningsAsync();
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
