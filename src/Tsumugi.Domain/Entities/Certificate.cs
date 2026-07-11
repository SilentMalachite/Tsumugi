using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 受給者証（障害者総合支援法施行規則 様式に準拠）。
/// MHLW 公式様式の主要セクションを保持する：
///   1. 支給決定障害者等情報（受給者証番号・氏名・居住地・性別・保護者 等は <see cref="Recipient"/> と
///      この実体の組合せで表現する。本実体側は発行時点のスナップショット項目を保持する）
///   2. 障害種別 <see cref="DisabilityCategories"/> ＋ <see cref="SupportCategory"/>
///   3. 給付種別と支給決定内容 <see cref="BenefitType"/>, <see cref="ServiceCategory"/>,
///      <see cref="Validity"/>, <see cref="SupplyDays"/>, <see cref="SupplyNotes"/>
///   4. 計画相談支援給付費の支援内容（計画相談支援事業者名・期間）
///   5. 利用者負担に関する事項（負担上限月額・適用区分・上限額管理事業所・食事提供体制加算 等）
///   6. サービス事業者記入欄 → 別エンティティ <see cref="ContractedProvider"/> に正規化（1対多）
/// </summary>
public sealed record Certificate : Entity
{
    public required Guid RootCertificateId { get; init; }
    public required int Revision { get; init; }
    public Guid? ExpectedHeadCertificateId { get; init; }
    public required Guid RecipientId { get; init; }
    public required string CertificateNumber { get; init; }
    public required DateRange Validity { get; init; }
    public required int SupplyDays { get; init; }
    public required int MonthlyCostCap { get; init; }
    public required string Municipality { get; init; }
    public string? MunicipalityNumber { get; init; }
    public string? SubsidyMunicipalityNumber { get; init; }
    public string? UpperLimitManagementProviderNumber { get; init; }

    // -------- 支給決定障害者等：受給者証発行時点のスナップショット --------
    public string? RecipientAddress { get; init; }
    public Gender RecipientGender { get; init; }
    /// <summary>障害児の場合の保護者氏名。成人の場合は null。</summary>
    public string? GuardianName { get; init; }
    /// <summary>続柄（父/母/その他）。<see cref="GuardianName"/> と対で設定。</summary>
    public string? GuardianRelationship { get; init; }

    // -------- 障害種別 --------
    public DisabilityCategories Disabilities { get; init; }
    public SupportCategory SupportCategory { get; init; }

    // -------- 給付種別と支給決定内容 --------
    public BenefitType BenefitType { get; init; } = BenefitType.Training;
    /// <summary>サービス種別（例: 就労継続支援B型）。Phase1 は B 型固定だが将来拡張のため文字列で保持。</summary>
    public string ServiceCategory { get; init; } = "就労継続支援B型";
    /// <summary>支給決定内容に関する特記事項（自由記述）。</summary>
    public string? SupplyNotes { get; init; }

    // -------- 計画相談支援給付費の支援内容 --------
    public string? ConsultationProviderName { get; init; }
    /// <summary>計画相談支援事業所番号（10桁等）。</summary>
    public string? ConsultationProviderNumber { get; init; }
    public DateOnly? ConsultationStart { get; init; }
    public DateOnly? ConsultationEnd { get; init; }

    // -------- 利用者負担に関する事項 --------
    public PaymentBurdenCategory PaymentBurden { get; init; }
    /// <summary>上限額管理事業所名（自治体が指定）。</summary>
    public string? UpperLimitManagementProvider { get; init; }
    /// <summary>食事提供体制加算の対象であるか。</summary>
    public bool MealProvisionApplicable { get; init; }
    /// <summary>高額障害福祉サービス費等の適用があるか。</summary>
    public bool HighCostBenefitApplicable { get; init; }

    public static Certificate Create(
        Guid id,
        Guid recipientId,
        string certificateNumber,
        DateRange validity,
        int supplyDays,
        int monthlyCostCap,
        string municipality,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken,
        // 任意セクション
        string? recipientAddress = null,
        Gender recipientGender = Gender.Unspecified,
        string? guardianName = null,
        string? guardianRelationship = null,
        DisabilityCategories disabilities = default,
        SupportCategory supportCategory = SupportCategory.None,
        BenefitType benefitType = BenefitType.Training,
        string serviceCategory = "就労継続支援B型",
        string? supplyNotes = null,
        string? consultationProviderName = null,
        string? consultationProviderNumber = null,
        DateOnly? consultationStart = null,
        DateOnly? consultationEnd = null,
        PaymentBurdenCategory paymentBurden = PaymentBurdenCategory.Unspecified,
        string? upperLimitManagementProvider = null,
        bool mealProvisionApplicable = false,
        bool highCostBenefitApplicable = false,
        string? municipalityNumber = null,
        string? subsidyMunicipalityNumber = null,
        string? upperLimitManagementProviderNumber = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(supplyDays);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyCostCap);

        return new()
        {
            Id = id,
            RootCertificateId = id,
            Revision = 1,
            ExpectedHeadCertificateId = null,
            RecipientId = recipientId,
            CertificateNumber = certificateNumber,
            Validity = validity,
            SupplyDays = supplyDays,
            MonthlyCostCap = monthlyCostCap,
            Municipality = municipality,
            MunicipalityNumber = municipalityNumber,
            SubsidyMunicipalityNumber = subsidyMunicipalityNumber,
            UpperLimitManagementProviderNumber = upperLimitManagementProviderNumber,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
            RecipientAddress = recipientAddress,
            RecipientGender = recipientGender,
            GuardianName = guardianName,
            GuardianRelationship = guardianRelationship,
            Disabilities = disabilities,
            SupportCategory = supportCategory,
            BenefitType = benefitType,
            ServiceCategory = serviceCategory,
            SupplyNotes = supplyNotes,
            ConsultationProviderName = consultationProviderName,
            ConsultationProviderNumber = consultationProviderNumber,
            ConsultationStart = consultationStart,
            ConsultationEnd = consultationEnd,
            PaymentBurden = paymentBurden,
            UpperLimitManagementProvider = upperLimitManagementProvider,
            MealProvisionApplicable = mealProvisionApplicable,
            HighCostBenefitApplicable = highCostBenefitApplicable,
        };
    }
}
