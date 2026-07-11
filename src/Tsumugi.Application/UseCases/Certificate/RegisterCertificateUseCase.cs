using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Certificate;

/// <summary>
/// 受給者証の入力値（MHLW 様式準拠）。任意セクション項目は既定値で省略可能。
/// </summary>
public sealed record RegisterCertificateInput(
    Guid RecipientId,
    string CertificateNumber,
    DateRange Validity,
    int SupplyDays,
    int MonthlyCostCap,
    string Municipality)
{
    // 支給決定障害者等
    public string? RecipientAddress { get; init; }
    public Gender RecipientGender { get; init; } = Gender.Unspecified;
    public string? GuardianName { get; init; }
    public string? GuardianRelationship { get; init; }
    // 障害種別
    public DisabilityCategories Disabilities { get; init; }
    public SupportCategory SupportCategory { get; init; } = SupportCategory.None;
    // 給付種別と支給決定内容
    public BenefitType BenefitType { get; init; } = BenefitType.Training;
    public string ServiceCategory { get; init; } = "就労継続支援B型";
    public string? SupplyNotes { get; init; }
    // 計画相談支援
    public string? ConsultationProviderName { get; init; }
    public string? ConsultationProviderNumber { get; init; }
    public DateOnly? ConsultationStart { get; init; }
    public DateOnly? ConsultationEnd { get; init; }
    // 利用者負担に関する事項
    public PaymentBurdenCategory PaymentBurden { get; init; } = PaymentBurdenCategory.Unspecified;
    public string? UpperLimitManagementProvider { get; init; }
    public bool MealProvisionApplicable { get; init; }
    public bool HighCostBenefitApplicable { get; init; }
    public string? MunicipalityNumber { get; init; }
    public string? SubsidyMunicipalityNumber { get; init; }
    public string? UpperLimitManagementProviderNumber { get; init; }
}

internal static class CertificateClaimInputValidator
{
    internal static void Validate(
        string? municipalityNumber,
        string? subsidyMunicipalityNumber,
        string? upperLimitManagementProviderNumber)
    {
        ValidateDigits(
            municipalityNumber,
            6,
            nameof(RegisterCertificateInput.MunicipalityNumber),
            required: true);
        ValidateDigits(
            subsidyMunicipalityNumber,
            6,
            nameof(RegisterCertificateInput.SubsidyMunicipalityNumber),
            required: false);
        ValidateDigits(
            upperLimitManagementProviderNumber,
            10,
            nameof(RegisterCertificateInput.UpperLimitManagementProviderNumber),
            required: false);
    }

    private static void ValidateDigits(
        string? value,
        int length,
        string parameterName,
        bool required)
    {
        if (!required && value is null)
            return;
        if (value is null
            || value.Length != length
            || value.Any(character => character is not (>= '0' and <= '9')))
            throw new ArgumentException(
                $"{parameterName}は{length}桁の半角数字で入力してください。",
                parameterName);
    }
}

public sealed class RegisterCertificateUseCase(
    ICertificateRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(CertificateDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        RegisterCertificateInput input, string actor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.RecipientId == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(input));
        if (string.IsNullOrWhiteSpace(input.CertificateNumber))
            throw new ArgumentException("受給者証番号は必須です。", nameof(input));
        CertificateClaimInputValidator.Validate(
            input.MunicipalityNumber,
            input.SubsidyMunicipalityNumber,
            input.UpperLimitManagementProviderNumber);
        DateValidator.EnsureRange(input.Validity.Start, input.Validity.End, nameof(input));
        if (input.ConsultationStart is { } cs && input.ConsultationEnd is { } ce && cs > ce)
            throw new ArgumentException("計画相談支援期間の開始は終了以前である必要があります。", nameof(input));

        var existing = await repo.ListByRecipientAsync(input.RecipientId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Validity).Append(input.Validity).ToArray();
        var overlaps = PeriodPolicy.DetectOverlaps(ranges);
        if (overlaps.Count > 0)
            warnings.Add("同一利用者の受給者証期間が重複しています。意図的か確認してください。");
        var gaps = PeriodPolicy.DetectGaps(ranges);
        if (gaps.Count > 0)
            warnings.Add("受給者証期間に空白があります。連続性を確認してください。");

        var entity = Domain.Entities.Certificate.Create(
            id: Guid.NewGuid(),
            recipientId: input.RecipientId,
            certificateNumber: input.CertificateNumber,
            validity: input.Validity,
            supplyDays: input.SupplyDays,
            monthlyCostCap: input.MonthlyCostCap,
            municipality: input.Municipality,
            createdBy: actor,
            createdAt: clock.GetUtcNow(),
            concurrencyToken: Guid.NewGuid(),
            recipientAddress: input.RecipientAddress,
            recipientGender: input.RecipientGender,
            guardianName: input.GuardianName,
            guardianRelationship: input.GuardianRelationship,
            disabilities: input.Disabilities,
            supportCategory: input.SupportCategory,
            benefitType: input.BenefitType,
            serviceCategory: input.ServiceCategory,
            supplyNotes: input.SupplyNotes,
            consultationProviderName: input.ConsultationProviderName,
            consultationProviderNumber: input.ConsultationProviderNumber,
            consultationStart: input.ConsultationStart,
            consultationEnd: input.ConsultationEnd,
            paymentBurden: input.PaymentBurden,
            upperLimitManagementProvider: input.UpperLimitManagementProvider,
            mealProvisionApplicable: input.MealProvisionApplicable,
            highCostBenefitApplicable: input.HighCostBenefitApplicable,
            municipalityNumber: input.MunicipalityNumber,
            subsidyMunicipalityNumber: input.SubsidyMunicipalityNumber,
            upperLimitManagementProviderNumber: input.UpperLimitManagementProviderNumber);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);

        return (MapToDto(entity), warnings);
    }

    internal static CertificateDto MapToDto(Domain.Entities.Certificate e) => new(
        e.Id, e.RootCertificateId, e.Revision, e.ExpectedHeadCertificateId,
        e.RecipientId, e.CertificateNumber, e.Validity,
        e.SupplyDays, e.MonthlyCostCap, e.Municipality,
        e.MunicipalityNumber, e.SubsidyMunicipalityNumber,
        e.UpperLimitManagementProviderNumber,
        e.RecipientAddress, e.RecipientGender, e.GuardianName, e.GuardianRelationship,
        e.Disabilities, e.SupportCategory,
        e.BenefitType, e.ServiceCategory, e.SupplyNotes,
        e.ConsultationProviderName, e.ConsultationProviderNumber,
        e.ConsultationStart, e.ConsultationEnd,
        e.PaymentBurden, e.UpperLimitManagementProvider,
        e.MealProvisionApplicable, e.HighCostBenefitApplicable);
}
