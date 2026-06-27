using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>フェースシートの入力値。任意フィールドは null で省略可能。</summary>
public sealed record SaveFaceSheetInput(Guid RecipientId)
{
    public string? PostalCode { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? EmailAddress { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactRelationship { get; init; }
    public string? EmergencyContactPhone { get; init; }
    public string? FamilyComposition { get; init; }
    public string? Cohabitants { get; init; }
    public string? PrimaryDoctorName { get; init; }
    public string? PrimaryDoctorHospital { get; init; }
    public string? PrimaryDoctorPhone { get; init; }
    public string? MedicalHistory { get; init; }
    public string? CurrentConditions { get; init; }
    public string? Medications { get; init; }
    public string? Allergies { get; init; }
    public bool ReceivesNursingInsurance { get; init; }
    public bool ReceivesDisabilityPension { get; init; }
    public string? PensionDetails { get; init; }
    public string? LifeHistory { get; init; }
    public string? PersonalWishes { get; init; }
    public string? SupportNeeds { get; init; }
    public string? AssessmentSummary { get; init; }
}

/// <summary>
/// フェースシートを「現行版として保存」する。
/// 物理的には新しい行を追記（append-only）し、<see cref="GetLatestFaceSheetUseCase"/> が
/// 最新を「現行」として返す。
/// </summary>
public sealed class SaveFaceSheetUseCase(
    IFaceSheetRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<FaceSheetDto> ExecuteAsync(
        SaveFaceSheetInput input, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.RecipientId == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(input));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));

        var entity = FaceSheet.Create(
            id: Guid.NewGuid(),
            recipientId: input.RecipientId,
            createdBy: actor,
            createdAt: clock.GetUtcNow(),
            concurrencyToken: Guid.NewGuid(),
            postalCode: input.PostalCode,
            address: input.Address,
            phoneNumber: input.PhoneNumber,
            emailAddress: input.EmailAddress,
            emergencyContactName: input.EmergencyContactName,
            emergencyContactRelationship: input.EmergencyContactRelationship,
            emergencyContactPhone: input.EmergencyContactPhone,
            familyComposition: input.FamilyComposition,
            cohabitants: input.Cohabitants,
            primaryDoctorName: input.PrimaryDoctorName,
            primaryDoctorHospital: input.PrimaryDoctorHospital,
            primaryDoctorPhone: input.PrimaryDoctorPhone,
            medicalHistory: input.MedicalHistory,
            currentConditions: input.CurrentConditions,
            medications: input.Medications,
            allergies: input.Allergies,
            receivesNursingInsurance: input.ReceivesNursingInsurance,
            receivesDisabilityPension: input.ReceivesDisabilityPension,
            pensionDetails: input.PensionDetails,
            lifeHistory: input.LifeHistory,
            personalWishes: input.PersonalWishes,
            supportNeeds: input.SupportNeeds,
            assessmentSummary: input.AssessmentSummary);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    internal static FaceSheetDto ToDto(FaceSheet e) => new(
        e.Id, e.RecipientId, e.CreatedAt, e.CreatedBy,
        e.PostalCode, e.Address, e.PhoneNumber, e.EmailAddress,
        e.EmergencyContactName, e.EmergencyContactRelationship, e.EmergencyContactPhone,
        e.FamilyComposition, e.Cohabitants,
        e.PrimaryDoctorName, e.PrimaryDoctorHospital, e.PrimaryDoctorPhone,
        e.MedicalHistory, e.CurrentConditions, e.Medications, e.Allergies,
        e.ReceivesNursingInsurance, e.ReceivesDisabilityPension, e.PensionDetails,
        e.LifeHistory, e.PersonalWishes, e.SupportNeeds, e.AssessmentSummary);
}
