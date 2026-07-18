using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// <c>OfficeClaimProfile</c>のStaffingKey/RegionKey選択肢。UIはこの値だけを選択肢として提示し、
/// 語彙を自前でハードコードしない。
/// </summary>
public sealed record ClaimBillingTokenOptionsDto(
    IReadOnlyList<string> StaffingKeyOptions,
    IReadOnlyList<string> RegionKeyOptions);

/// <summary>
/// <c>OfficeClaimProfile.StaffingKey</c> / <c>OfficeClaimProfile.RegionKey</c> 入力欄向けに、
/// 対象サービス月のマスタ束から選択肢を列挙する。StaffingKeyは<c>kind: staffing</c>条件定義の
/// token operand（Equals）・token set operand（In）の値を、RegionKeyはregion-unit-price行の
/// <c>RegionKey</c>を、それぞれ重複排除して返す（ADR 0027の語彙はseedにのみ存在し、
/// Application/UI層にハードコードしない）。
/// </summary>
/// <remarks>
/// マスタが対象月で未解決（<see cref="ClaimMasterPolicyUnavailableException"/>）の場合は空の
/// 選択肢を返す。入力欄はこの選択肢に無い自由な文字列も受け付け得るが、版と一致しない値は
/// 算定/readiness側でフェイルクローズされる（このuse caseは入力補助であり、正誤判定ではない）。
/// </remarks>
public sealed class QueryClaimBillingTokenOptionsUseCase(IClaimMasterProvider masterProvider)
{
    public ClaimBillingTokenOptionsDto Execute(ServiceMonth serviceMonth)
    {
        _ = serviceMonth.ToInt();

        ClaimCalculationMasterBundle masters;
        try
        {
            masters = masterProvider.ResolveCalculationMasters(serviceMonth);
        }
        catch (ClaimMasterPolicyUnavailableException)
        {
            return new ClaimBillingTokenOptionsDto([], []);
        }

        var staffingKeyOptions = masters.ConditionDefinitions
            .Where(condition => condition.Kind == ClaimConditionKind.Staffing)
            .SelectMany(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token => new[] { token.Value },
                ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                _ => [],
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var regionKeyOptions = masters.RegionUnitPrices
            .Select(row => row.RegionKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new ClaimBillingTokenOptionsDto(staffingKeyOptions, regionKeyOptions);
    }
}
