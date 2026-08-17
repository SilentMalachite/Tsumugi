using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

/// <summary>フェースシート業務フィールドの1件分の差分。</summary>
public sealed record FaceSheetChange(string PropertyName, string? OldValue, string? NewValue);

/// <summary>2版のフェースシート業務フィールド差分（純粋関数。反射禁止）。</summary>
public static class FaceSheetDiff
{
    /// <summary>
    /// 明示した業務プロパティだけを固定順で比較し、変更があったものだけ返す。
    /// Id / RecipientId / CreatedBy / CreatedAt / ConcurrencyToken は除外。
    /// null と空文字は別値として扱う。bool は <see cref="bool.ToString"/> で文字列化する。
    /// </summary>
    public static IReadOnlyList<FaceSheetChange> Compare(FaceSheet older, FaceSheet newer)
    {
        ArgumentNullException.ThrowIfNull(older);
        ArgumentNullException.ThrowIfNull(newer);

        var changes = new List<FaceSheetChange>();

        AddIfChanged(changes, "PostalCode", older.PostalCode, newer.PostalCode);
        AddIfChanged(changes, "Address", older.Address, newer.Address);
        AddIfChanged(changes, "PhoneNumber", older.PhoneNumber, newer.PhoneNumber);
        AddIfChanged(changes, "EmailAddress", older.EmailAddress, newer.EmailAddress);
        AddIfChanged(changes, "EmergencyContactName", older.EmergencyContactName, newer.EmergencyContactName);
        AddIfChanged(changes, "EmergencyContactRelationship", older.EmergencyContactRelationship, newer.EmergencyContactRelationship);
        AddIfChanged(changes, "EmergencyContactPhone", older.EmergencyContactPhone, newer.EmergencyContactPhone);
        AddIfChanged(changes, "FamilyComposition", older.FamilyComposition, newer.FamilyComposition);
        AddIfChanged(changes, "Cohabitants", older.Cohabitants, newer.Cohabitants);
        AddIfChanged(changes, "PrimaryDoctorName", older.PrimaryDoctorName, newer.PrimaryDoctorName);
        AddIfChanged(changes, "PrimaryDoctorHospital", older.PrimaryDoctorHospital, newer.PrimaryDoctorHospital);
        AddIfChanged(changes, "PrimaryDoctorPhone", older.PrimaryDoctorPhone, newer.PrimaryDoctorPhone);
        AddIfChanged(changes, "MedicalHistory", older.MedicalHistory, newer.MedicalHistory);
        AddIfChanged(changes, "CurrentConditions", older.CurrentConditions, newer.CurrentConditions);
        AddIfChanged(changes, "Medications", older.Medications, newer.Medications);
        AddIfChanged(changes, "Allergies", older.Allergies, newer.Allergies);
        AddIfChanged(changes, "ReceivesNursingInsurance", older.ReceivesNursingInsurance.ToString(), newer.ReceivesNursingInsurance.ToString());
        AddIfChanged(changes, "ReceivesDisabilityPension", older.ReceivesDisabilityPension.ToString(), newer.ReceivesDisabilityPension.ToString());
        AddIfChanged(changes, "PensionDetails", older.PensionDetails, newer.PensionDetails);
        AddIfChanged(changes, "LifeHistory", older.LifeHistory, newer.LifeHistory);
        AddIfChanged(changes, "PersonalWishes", older.PersonalWishes, newer.PersonalWishes);
        AddIfChanged(changes, "SupportNeeds", older.SupportNeeds, newer.SupportNeeds);
        AddIfChanged(changes, "AssessmentSummary", older.AssessmentSummary, newer.AssessmentSummary);

        return changes;
    }

    private static void AddIfChanged(
        List<FaceSheetChange> changes,
        string propertyName,
        string? oldValue,
        string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            changes.Add(new FaceSheetChange(propertyName, oldValue, newValue));
    }
}
