using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

/// <summary>
/// 受給者証 DTO（MHLW 様式準拠）。
/// 任意セクション項目は適宜 null / 既定値で省略可能。
/// </summary>
public sealed record CertificateDto(
    Guid Id,
    Guid RootCertificateId,
    int Revision,
    Guid? ExpectedHeadCertificateId,
    Guid RecipientId,
    string CertificateNumber,
    DateRange Validity,
    int SupplyDays,
    int MonthlyCostCap,
    string Municipality,
    string? MunicipalityNumber,
    string? SubsidyMunicipalityNumber,
    string? UpperLimitManagementProviderNumber,
    // 支給決定障害者等：発行時点スナップショット
    string? RecipientAddress,
    Gender RecipientGender,
    string? GuardianName,
    string? GuardianRelationship,
    // 障害種別
    DisabilityCategories Disabilities,
    SupportCategory SupportCategory,
    // 給付種別と支給決定内容
    BenefitType BenefitType,
    string ServiceCategory,
    string? SupplyNotes,
    // 計画相談支援
    string? ConsultationProviderName,
    string? ConsultationProviderNumber,
    DateOnly? ConsultationStart,
    DateOnly? ConsultationEnd,
    // 利用者負担に関する事項
    PaymentBurdenCategory PaymentBurden,
    string? UpperLimitManagementProvider,
    bool MealProvisionApplicable,
    bool HighCostBenefitApplicable);
