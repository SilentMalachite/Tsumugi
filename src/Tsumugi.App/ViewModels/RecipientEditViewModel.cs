using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientEditViewModel(
    RegisterRecipientUseCase registerUseCase,
    UpdateRecipientUseCase updateUseCase) : ViewModelBase
{
    [ObservableProperty] private string _kanjiName = string.Empty;
    [ObservableProperty] private string _kanaName = string.Empty;
    [ObservableProperty] private DateOnly _dateOfBirth = new(1990, 1, 1);

    // 障害種別
    [ObservableProperty] private bool _disabilityPhysical;
    [ObservableProperty] private bool _disabilityIntellectual;
    [ObservableProperty] private bool _disabilityMental;
    [ObservableProperty] private bool _disabilityIntractable;

    // 連絡先
    [ObservableProperty] private string _postalCode = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _emailAddress = string.Empty;
    [ObservableProperty] private string _emergencyContactName = string.Empty;
    [ObservableProperty] private string _emergencyContactRelationship = string.Empty;
    [ObservableProperty] private string _emergencyContactPhone = string.Empty;

    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    // 編集モード: EditingId が null なら新規登録、値があれば更新。
    [ObservableProperty] private Guid? _editingId;
    // 楽観的同時実行: LoadForEdit 時点のトークンを保持し、Update でそのまま渡す。
    private Guid _editingConcurrencyToken;

    /// <summary>RecipientList から渡された既存利用者をフォームに展開する。</summary>
    public void LoadForEdit(RecipientDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EditingId = dto.Id;
        _editingConcurrencyToken = dto.ConcurrencyToken;
        KanjiName = dto.KanjiName;
        KanaName = dto.KanaName;
        DateOfBirth = dto.DateOfBirth;
        DisabilityPhysical = dto.Disabilities.Physical;
        DisabilityIntellectual = dto.Disabilities.Intellectual;
        DisabilityMental = dto.Disabilities.Mental;
        DisabilityIntractable = dto.Disabilities.Intractable;
        PostalCode = dto.PostalCode ?? string.Empty;
        Address = dto.Address ?? string.Empty;
        PhoneNumber = dto.PhoneNumber ?? string.Empty;
        EmailAddress = dto.EmailAddress ?? string.Empty;
        EmergencyContactName = dto.EmergencyContactName ?? string.Empty;
        EmergencyContactRelationship = dto.EmergencyContactRelationship ?? string.Empty;
        EmergencyContactPhone = dto.EmergencyContactPhone ?? string.Empty;
        SaveErrorMessage = null;
        IsSaved = false;
    }

    private DisabilityCategories CurrentDisabilities => new(
        DisabilityPhysical, DisabilityIntellectual, DisabilityMental, DisabilityIntractable);

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (EditingId is { } id)
            {
                var update = new UpdateRecipientInput(
                    id, _editingConcurrencyToken, KanjiName, KanaName, DateOfBirth)
                {
                    Disabilities = CurrentDisabilities,
                    PostalCode = N(PostalCode),
                    Address = N(Address),
                    PhoneNumber = N(PhoneNumber),
                    EmailAddress = N(EmailAddress),
                    EmergencyContactName = N(EmergencyContactName),
                    EmergencyContactRelationship = N(EmergencyContactRelationship),
                    EmergencyContactPhone = N(EmergencyContactPhone),
                };
                await updateUseCase.ExecuteAsync(update, Environment.UserName, default);
            }
            else
            {
                var register = new RegisterRecipientInput(KanjiName, KanaName, DateOfBirth)
                {
                    Disabilities = CurrentDisabilities,
                    PostalCode = N(PostalCode),
                    Address = N(Address),
                    PhoneNumber = N(PhoneNumber),
                    EmailAddress = N(EmailAddress),
                    EmergencyContactName = N(EmergencyContactName),
                    EmergencyContactRelationship = N(EmergencyContactRelationship),
                    EmergencyContactPhone = N(EmergencyContactPhone),
                };
                await registerUseCase.ExecuteAsync(register, Environment.UserName, default);
            }
            SaveErrorMessage = null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
        catch (Tsumugi.Application.OptimisticConcurrencyException)
        {
            SaveErrorMessage = "他のユーザに先に更新されています。一覧から再選択して最新状態を読み込んでください。";
            IsSaved = false;
        }
    }

    [RelayCommand]
    private void Discard()
    {
        KanjiName = string.Empty;
        KanaName = string.Empty;
        DateOfBirth = new DateOnly(1990, 1, 1);
        DisabilityPhysical = DisabilityIntellectual = DisabilityMental = DisabilityIntractable = false;
        PostalCode = Address = PhoneNumber = EmailAddress = string.Empty;
        EmergencyContactName = EmergencyContactRelationship = EmergencyContactPhone = string.Empty;
        SaveErrorMessage = null;
        IsSaved = false;
        EditingId = null;
        _editingConcurrencyToken = Guid.Empty;
    }

    private static string? N(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
