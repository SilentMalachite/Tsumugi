using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.ClaimMasters;

/// <summary>
/// ADR 0027のトークン語彙（regionKey / serviceKind / rewardSystem）を型付き事業所マスタから解決する。
/// 語彙はseed（<c>ClaimMasters/Seed</c>）の正準キーであり、Domain/Applicationへは
/// ハードコードできない（<c>ClaimSpecificationBoundaryTests</c>）ため、seedと同居する本クラスが
/// 値として供給する。対応は名義的1:1（<see cref="RegionGrade"/>のN級地⇔region-grade-N、
/// B型⇔employment-continuation-support-b。ADR 0027 決定1の語彙表）であり、制度数値は含まない。
/// </summary>
/// <remarks>
/// 利用定員（実頭数）と人員配置区分は、ADR 0021が<c>OfficeClaimProfile</c>の構造化入力と定める
/// 項目（capacityClass/staffingClass）に対応するが、現行エンティティに未実装のため常に<c>null</c>を
/// 返す。<c>ClaimCalculationRequestBuilder</c>がreadiness issue
/// （OfficeClaimProfile.CapacityHeadcount / OfficeClaimProfile.StaffingClass）へ変換して
/// フェイルクローズする。profile拡張タスクの実装後にここを実データ解決へ差し替える。
/// </remarks>
public sealed class OfficeClaimBillingTokenProvider : IClaimBillingTokenProvider
{
    public ClaimBillingConditionTokens Resolve(Office office, ServiceMonth serviceMonth)
    {
        ArgumentNullException.ThrowIfNull(office);
        _ = serviceMonth.ToInt(); // 語彙はR6-04以降共通（ADR 0027）。版分岐が必要になった時点で使用する。

        return new ClaimBillingConditionTokens(
            RewardSystem: office.ServiceCategory switch
            {
                ServiceCategory.TypeB => "employment-continuation-support-b",
                _ => null,
            },
            RegionKey: office.RegionGrade switch
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
            },
            RegionUnitPriceServiceKind: "employment-continuation-support",
            CapacityHeadcount: null,
            StaffingKey: null);
    }
}
