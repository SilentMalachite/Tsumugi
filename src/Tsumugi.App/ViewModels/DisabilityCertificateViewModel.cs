using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
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
    ListDisabilityCertificatesUseCase listUseCase) : ViewModelBase
{
    public ObservableCollection<RecipientDto> Recipients { get; } = new();
    public ObservableCollection<DisabilityCertificateDto> Items { get; } = new();

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

    partial void OnSelectedRecipientChanged(RecipientDto? value)
        => _ = ReloadAsync();

    public Task InitializeAsync(CancellationToken ct = default) => LoadRecipientsAsync(ct);

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
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
