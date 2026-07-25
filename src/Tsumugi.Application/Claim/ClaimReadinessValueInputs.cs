using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// readiness の値辞書を組むための入力（エンティティにも確定 snapshot にも依存しない素の値）。
/// </summary>
/// <remarks>
/// 値の組み立て（＝path キーの列挙）は <see cref="ClaimPreparationContextBuilder"/> の 1 か所だけに置く。
/// 確定前（DB 由来）と確定後（snapshot 由来）で別々に辞書を組むと、片方にパスを足して他方を忘れる
/// ドリフトが起きるため、両者はこの型に詰め替えてから同じ組み立てを通す（ADR 0041）。
/// </remarks>
public sealed record ClaimReadinessClaimInput(
    UpperLimitManagementResult? UpperLimitManagementResult,
    int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen,
    ServiceMonth? ExceptionalUsageStartMonth,
    ServiceMonth? ExceptionalUsageEndMonth,
    int? ExceptionalUsageDays,
    int? StandardUsageDayTotal,
    int? SpecialVisitSupportBilledCount,
    int? OffsiteSupportCumulativeDays,
    /// <summary>
    /// 汎用 pass-through 入力（ADR 0042）の値（名前→値）。宣言された名前のうちここに無いものは
    /// 「未入力」として供給される。
    /// </summary>
    IReadOnlyDictionary<string, string>? GenericValues = null)
{
    /// <summary>実効 ClaimInput が無い状態（全項目 null）。</summary>
    public static ClaimReadinessClaimInput Absent { get; } =
        new(null, null, null, null, null, null, null, null, null);
}

/// <param name="MunicipalityNumber">市町村番号。</param>
/// <param name="SubsidyMunicipalityNumber">助成自治体番号。</param>
/// <param name="UpperLimitManagementProviderNumber">上限額管理事業所番号。</param>
public sealed record ClaimReadinessCertificate(
    string? MunicipalityNumber,
    string? SubsidyMunicipalityNumber,
    string? UpperLimitManagementProviderNumber)
{
    public static ClaimReadinessCertificate Absent { get; } = new(null, null, null);
}

/// <param name="CertificateEntryNumber">受給者証の事業者記入欄番号。</param>
/// <param name="FirstServiceDate">初回サービス提供日。</param>
public sealed record ClaimReadinessContractedProvider(
    int? CertificateEntryNumber,
    DateOnly? FirstServiceDate)
{
    public static ClaimReadinessContractedProvider Absent { get; } = new(null, null);
}

/// <param name="PostalCode">郵便番号。</param>
/// <param name="Address">所在地。</param>
/// <param name="PhoneNumber">電話番号。</param>
/// <param name="RepresentativeTitleAndName">代表者職氏名。</param>
public sealed record ClaimReadinessOffice(
    string? PostalCode,
    string? Address,
    string? PhoneNumber,
    string? RepresentativeTitleAndName);
