using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IClaimMasterProvider
{
    ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth);
    ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth);

    /// <summary>
    /// 登録済みマスタの<b>全期間</b>にわたる<c>kind: office-capability</c>条件定義の値集合。
    /// 「当月に無い」と「そもそも請求に効かないキー」を区別するために使う（ADR 0049）。
    /// </summary>
    IReadOnlySet<string> AllOfficeCapabilityConditionValues();
}
