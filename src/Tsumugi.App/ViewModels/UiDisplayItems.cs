using Tsumugi.Application.Dtos;

namespace Tsumugi.App.ViewModels;

public sealed record RenewalDueDisplayItem
{
    public RenewalDueDisplayItem(
        Guid recipientId, string? recipientName, DateOnly nextRenewalDate, int remainingDays)
    {
        RecipientId = recipientId;
        RecipientName = string.IsNullOrWhiteSpace(recipientName) ? "利用者情報未取得" : recipientName;
        NextRenewalDate = nextRenewalDate;
        RemainingDays = remainingDays;
    }

    public Guid RecipientId { get; init; }
    public string RecipientName { get; init; }
    public DateOnly NextRenewalDate { get; init; }
    public int RemainingDays { get; init; }
}

public sealed record FaceSheetChangeDisplayItem(string PropertyName, string? OldValue, string? NewValue)
{
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["PostalCode"] = "郵便番号",
        ["Address"] = "住所",
        ["PhoneNumber"] = "電話番号",
        ["EmailAddress"] = "メールアドレス",
        ["EmergencyContactName"] = "緊急連絡先氏名",
        ["EmergencyContactRelationship"] = "緊急連絡先との続柄",
        ["EmergencyContactPhone"] = "緊急連絡先電話番号",
        ["FamilyComposition"] = "家族構成",
        ["Cohabitants"] = "同居人",
        ["PrimaryDoctorName"] = "主治医",
        ["PrimaryDoctorHospital"] = "医療機関",
        ["PrimaryDoctorPhone"] = "主治医連絡先",
        ["MedicalHistory"] = "既往歴",
        ["CurrentConditions"] = "現症",
        ["Medications"] = "服薬",
        ["Allergies"] = "アレルギー",
        ["ReceivesNursingInsurance"] = "介護保険の受給",
        ["ReceivesDisabilityPension"] = "障害年金の受給",
        ["PensionDetails"] = "年金詳細",
        ["LifeHistory"] = "生活歴",
        ["PersonalWishes"] = "本人の希望・意向",
        ["SupportNeeds"] = "支援上の留意事項",
        ["AssessmentSummary"] = "アセスメント要約",
    };

    public static FaceSheetChangeDisplayItem From(FaceSheetChangeDto change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new(Labels.GetValueOrDefault(change.PropertyName, change.PropertyName),
            FormatBoolean(change.PropertyName, change.OldValue),
            FormatBoolean(change.PropertyName, change.NewValue));
    }

    private static string? FormatBoolean(string propertyName, string? value) =>
        propertyName is "ReceivesNursingInsurance" or "ReceivesDisabilityPension"
            ? value switch { "True" => "あり", "False" => "なし", _ => value }
            : value;
}
