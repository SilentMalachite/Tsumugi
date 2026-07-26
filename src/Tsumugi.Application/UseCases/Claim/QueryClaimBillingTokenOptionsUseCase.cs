using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// <c>OfficeClaimProfile</c>のStaffingKey/RegionKey選択肢と、<c>OfficeCapability</c>の
/// 公式体制届キーの選択番号。UIはこの値だけを選択肢として提示し、語彙を自前で
/// ハードコードしない（CLAUDE.md ハード制約3）。
/// </summary>
public sealed record ClaimBillingTokenOptionsDto(
    IReadOnlyList<string> StaffingKeyOptions,
    IReadOnlyList<string> RegionKeyOptions,
    IReadOnlyList<int> TreatmentImprovementOptions,
    IReadOnlyList<int> TreatmentImprovementVBandOptions);

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
            return new ClaimBillingTokenOptionsDto([], [], [], []);
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

        var treatmentImprovementOptions = CapabilityOptionCodes(
            masters.ConditionDefinitions, "mhlw.b46.capability.treatment-improvement.");
        var treatmentImprovementVBandOptions = CapabilityOptionCodes(
            masters.ConditionDefinitions, "mhlw.b46.capability.treatment-improvement-v-band.");

        return new ClaimBillingTokenOptionsDto(
            staffingKeyOptions,
            regionKeyOptions,
            treatmentImprovementOptions,
            treatmentImprovementVBandOptions);
    }

    /// <summary>
    /// <c>kind: office-capability</c>の条件定義のうち、指定した接頭辞を持つキーの
    /// 選択番号部分を昇順で列挙する。接頭辞が完全一致で終端することを要求するため、
    /// <c>treatment-improvement.</c>は<c>treatment-improvement-v-band.</c>を拾わない
    /// （前者の接頭辞の直後は必ず数字であり、ハイフンで始まる後者は候補にならない）。
    /// </summary>
    private static int[] CapabilityOptionCodes(
        IReadOnlyList<ClaimConditionDefinition> definitions, string prefix)
        => definitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .SelectMany(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token => new[] { token.Value },
                ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                _ => [],
            })
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value[prefix.Length..])
            .Select(TryParseInvariantInt)
            .Where(code => code is not null)
            .Select(code => code!.Value)
            .Distinct()
            .OrderBy(code => code)
            .ToArray();

    private static int? TryParseInvariantInt(string suffix) =>
        int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : null;
}
