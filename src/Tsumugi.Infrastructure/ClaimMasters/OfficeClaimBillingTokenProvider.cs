using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

/// <summary>
/// ADR 0027のトークン語彙（regionKey / serviceKind / rewardSystem）を型付き事業所マスタから解決する。
/// 語彙はseed（<c>ClaimMasters/Seed</c>）の正準キーであり、Domain/Applicationへは
/// ハードコードできない（<c>ClaimSpecificationBoundaryTests</c>）ため、seedと同居する本クラスが
/// 値として供給する。<see cref="RegionGrade"/>のN級地⇔region-grade-Nの対応は名義的1:1
/// （B型⇔employment-continuation-support-b。ADR 0027 決定1の語彙表）であり、制度数値は含まない。
/// </summary>
/// <remarks>
/// 利用定員（実頭数）と人員配置区分は、ADR 0021が<c>OfficeClaimProfile</c>の構造化入力と定める
/// 項目（<see cref="OfficeClaimProfile.CapacityHeadcount"/> / <see cref="OfficeClaimProfile.StaffingKey"/>）
/// にそのまま対応する（Task 9bでprofile拡張）。未入力（<c>null</c>）はreadiness issue
/// （OfficeClaimProfile.CapacityHeadcount / OfficeClaimProfile.StaffingClass）へ変換して
/// フェイルクローズする（<c>ClaimCalculationRequestBuilder</c>）。
/// </remarks>
/// <remarks>
/// 地域区分tokenは<see cref="OfficeClaimProfile.RegionKey"/>が入力されていればそれを優先し
/// （マスタのregion-unit-price語彙から選ぶ想定。ADR 0021はこの構造化フィールドを明示していないが、
/// StaffingKey/CapacityHeadcountと同じ「基本報酬選択の構造化入力」に位置付ける）、未入力時は
/// <see cref="Office.RegionGrade"/>由来の名義的既定へフォールバックする。Office.RegionGradeは
/// 必須項目で大半の既存事業所が既に設定済みのため、profile拡張だけでは請求プレビューの
/// readinessを後退させない（Task 9b実装時の判断）。
/// <b>profile.RegionKeyが上書き・Office.RegionGradeがフォールバック。両方が値を持ちかつ
/// 不一致のときはフェイルクローズする（controller decision 2026-07-19, Task 9b fix round）。</b>
/// 両ソースが揃って不一致の場合は片方を無言で採用せず<see cref="ClaimBillingConditionTokens.RegionKey"/>を
/// <c>null</c>・<see cref="ClaimBillingConditionTokens.RegionKeyConflict"/>を<c>true</c>で返し、
/// 呼び出し側（<c>ClaimCalculationRequestBuilder</c>）が地域区分不一致専用のreadiness issueへ変換する。
/// </remarks>
public sealed class OfficeClaimBillingTokenProvider : IClaimBillingTokenProvider
{
    /// <summary>
    /// countSelectorトークン（ADR 0028決定2）→<see cref="ClaimCountMetric"/>の束縛（Task 11）。
    /// seedの正準文字列はDomain/Applicationへハードコードできないため、seedと同居する本クラスが
    /// 値として供給する。この束縛にないcountSelectorを持つ加算行はClaimCalculatorが
    /// フェイルクローズする。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ClaimCountMetric> CountSelectorBindings =
        new Dictionary<string, ClaimCountMetric>(StringComparer.Ordinal)
        {
            ["count.b46.service-days.v1"] = ClaimCountMetric.ServiceDays,
            ["count.b46.initial-period-service-days.v1"] = ClaimCountMetric.InitialPeriodServiceDays,
            ["count.b46.absence-support.v1"] = ClaimCountMetric.AbsenceSupport,
            ["count.b46.meal-provided-days.v1"] = ClaimCountMetric.MealProvidedDays,
            ["count.b46.transport-one-way.v1"] = ClaimCountMetric.TransportOneWay,
            ["count.b46.transport-one-way.same-premises.v1"] = ClaimCountMetric.TransportOneWaySamePremises,
            ["previous-year-six-month-employment-count"] = ClaimCountMetric.PreviousYearSixMonthEmployment,
        };

    /// <summary>
    /// <see cref="PaymentBurdenCategory"/>→burden-caps.json正準key（ADR 0022の完全一致対応表・
    /// Task 12）。正準文字列はDomain/Applicationへハードコードできないため、seedと同居する
    /// 本クラスが値として供給する。<see cref="PaymentBurdenCategory.Unspecified"/>は非入力状態
    /// であり制度額0の区分ではないため、意図的にこの対応表へ含めない
    /// （<c>ClaimCalculationRequestBuilder</c>が未対応区分としてissue化する）。
    /// </summary>
    private static readonly IReadOnlyDictionary<PaymentBurdenCategory, string> BurdenCategoryTokens =
        new Dictionary<PaymentBurdenCategory, string>
        {
            [PaymentBurdenCategory.Welfare] = "welfare",
            [PaymentBurdenCategory.LowIncome] = "low-income",
            [PaymentBurdenCategory.General1] = "general-1",
            [PaymentBurdenCategory.General2] = "general-2",
        };

    public ClaimBillingConditionTokens Resolve(
        Office office, OfficeClaimProfile? profile, ServiceMonth serviceMonth)
    {
        ArgumentNullException.ThrowIfNull(office);
        _ = serviceMonth.ToInt(); // 語彙はR6-04以降共通（ADR 0027）。版分岐が必要になった時点で使用する。

        var (regionKey, regionKeyConflict) = ResolveRegionKey(office, profile);
        return new ClaimBillingConditionTokens(
            RewardSystem: office.ServiceCategory switch
            {
                ServiceCategory.TypeB => "employment-continuation-support-b",
                _ => null,
            },
            RegionKey: regionKey,
            RegionUnitPriceServiceKind: "employment-continuation-support",
            CapacityHeadcount: profile?.CapacityHeadcount,
            StaffingKey: profile?.StaffingKey,
            RegionKeyConflict: regionKeyConflict,
            CountSelectorBindings: CountSelectorBindings,
            BurdenCategoryTokens: BurdenCategoryTokens);
    }

    private static (string? RegionKey, bool Conflict) ResolveRegionKey(
        Office office, OfficeClaimProfile? profile)
    {
        var profileRegionKey = string.IsNullOrWhiteSpace(profile?.RegionKey) ? null : profile.RegionKey;
        var regionGradeToken = RegionKeyFromGrade(office.RegionGrade);

        if (profileRegionKey is not null
            && regionGradeToken is not null
            && !string.Equals(profileRegionKey, regionGradeToken, StringComparison.Ordinal))
        {
            // 両ソースが揃って不一致：どちらを採用するか本クラスが推測してはならない
            // （controller decision 2026-07-19）。tokenはnullで返し、呼び出し側に
            // 専用issueとして可視化させる。
            return (null, true);
        }

        return (profileRegionKey ?? regionGradeToken, false);
    }

    private static string? RegionKeyFromGrade(RegionGrade regionGrade) => regionGrade switch
    {
        RegionGrade.Grade1 => "region-grade-1",
        RegionGrade.Grade2 => "region-grade-2",
        RegionGrade.Grade3 => "region-grade-3",
        RegionGrade.Grade4 => "region-grade-4",
        RegionGrade.Grade5 => "region-grade-5",
        RegionGrade.Grade6 => "region-grade-6",
        RegionGrade.Grade7 => "region-grade-7",
        RegionGrade.Other => "region-other",
        _ => null,
    };
}
