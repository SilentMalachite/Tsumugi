using CommunityToolkit.Mvvm.ComponentModel;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 事業所の入力フォーム状態。初回セットアップウィザードと事業所管理画面が共有する。
///
/// 両画面が同じ8項目を別々に宣言していたため、項目の追加・検証文言・空白の扱いを
/// 変えるたびに2箇所を直す必要があり、比較するテストも無いまま静かに乖離しうる。
/// </summary>
public abstract partial class OfficeFormViewModelBase : ViewModelBase
{
    [ObservableProperty] private string _officeNumber = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ServiceCategory _category = ServiceCategory.TypeB;
    [ObservableProperty] private RegionGrade _region = RegionGrade.None;
    [ObservableProperty] private string _postalCode = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phoneNumber = string.Empty;
    [ObservableProperty] private string _representativeTitleAndName = string.Empty;
    [ObservableProperty] private string? _saveErrorMessage;

    // Application 層へ渡す任意項目。両画面が NullIfEmpty(...) を4回ずつ並べていたのをここへ寄せる。
    protected string? OptionalPostalCodeInput => InputText.NullIfEmpty(PostalCode);
    protected string? OptionalAddressInput => InputText.NullIfEmpty(Address);
    protected string? OptionalPhoneNumberInput => InputText.NullIfEmpty(PhoneNumber);
    protected string? OptionalRepresentativeTitleAndNameInput =>
        InputText.NullIfEmpty(RepresentativeTitleAndName);

    /// <summary>入力欄を初期値へ戻す。エラー表示や選択状態は呼び出し側の責務。</summary>
    protected void ResetForm()
    {
        OfficeNumber = string.Empty;
        Name = string.Empty;
        Category = ServiceCategory.TypeB;
        Region = RegionGrade.None;
        PostalCode = string.Empty;
        Address = string.Empty;
        PhoneNumber = string.Empty;
        RepresentativeTitleAndName = string.Empty;
    }
}
