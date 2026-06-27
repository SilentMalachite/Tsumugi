namespace Tsumugi.Domain.Entities;

/// <summary>
/// フェースシート（利用者の基本情報・連絡先・医療情報・生活歴 等のスナップショット）。
/// 利用者と 1:N の追記型。更新は新バージョン挿入により行い、最新の CreatedAt を「現行」として扱う。
/// 個々の項目は自治体・事業所により様式が異なるため自由記述（string）で保持する。
/// 連絡先・医療・受給状況・生活歴・本人意向の 5 グループに分かれる。
/// </summary>
public sealed record FaceSheet : Entity
{
    public required Guid RecipientId { get; init; }

    // -------- 連絡先 --------
    public string? PostalCode { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactRelationship { get; init; }
    public string? EmergencyContactPhone { get; init; }

    // -------- 家族構成 --------
    public string? FamilyComposition { get; init; }
    public string? Cohabitants { get; init; }

    // -------- 医療情報 --------
    public string? PrimaryDoctorName { get; init; }
    public string? PrimaryDoctorHospital { get; init; }
    public string? PrimaryDoctorPhone { get; init; }
    public string? MedicalHistory { get; init; }
    public string? CurrentConditions { get; init; }
    public string? Medications { get; init; }
    public string? Allergies { get; init; }

    // -------- 受給状況 --------
    public bool ReceivesNursingInsurance { get; init; }
    public bool ReceivesDisabilityPension { get; init; }
    public string? PensionDetails { get; init; }

    // -------- 生活歴・本人意向 --------
    public string? LifeHistory { get; init; }
    public string? PersonalWishes { get; init; }
    public string? SupportNeeds { get; init; }
    public string? AssessmentSummary { get; init; }

    public static FaceSheet Create(
        Guid id,
        Guid recipientId,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken,
        string? postalCode = null,
        string? address = null,
        string? phoneNumber = null,
        string? emailAddress = null,
        string? emergencyContactName = null,
        string? emergencyContactRelationship = null,
        string? emergencyContactPhone = null,
        string? familyComposition = null,
        string? cohabitants = null,
        string? primaryDoctorName = null,
        string? primaryDoctorHospital = null,
        string? primaryDoctorPhone = null,
        string? medicalHistory = null,
        string? currentConditions = null,
        string? medications = null,
        string? allergies = null,
        bool receivesNursingInsurance = false,
        bool receivesDisabilityPension = false,
        string? pensionDetails = null,
        string? lifeHistory = null,
        string? personalWishes = null,
        string? supportNeeds = null,
        string? assessmentSummary = null) => new()
        {
            Id = id,
            RecipientId = recipientId,
            PostalCode = postalCode,
            Address = address,
            PhoneNumber = phoneNumber,
            EmailAddress = emailAddress,
            EmergencyContactName = emergencyContactName,
            EmergencyContactRelationship = emergencyContactRelationship,
            EmergencyContactPhone = emergencyContactPhone,
            FamilyComposition = familyComposition,
            Cohabitants = cohabitants,
            PrimaryDoctorName = primaryDoctorName,
            PrimaryDoctorHospital = primaryDoctorHospital,
            PrimaryDoctorPhone = primaryDoctorPhone,
            MedicalHistory = medicalHistory,
            CurrentConditions = currentConditions,
            Medications = medications,
            Allergies = allergies,
            ReceivesNursingInsurance = receivesNursingInsurance,
            ReceivesDisabilityPension = receivesDisabilityPension,
            PensionDetails = pensionDetails,
            LifeHistory = lifeHistory,
            PersonalWishes = personalWishes,
            SupportNeeds = supportNeeds,
            AssessmentSummary = assessmentSummary,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
}
