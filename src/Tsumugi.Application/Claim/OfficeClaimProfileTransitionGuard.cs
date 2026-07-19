using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Application.Claim;

/// <summary>
/// ADR 0023の経過措置（版付き許可option集合 × <see cref="R8ReformStatus"/>）を、書込時の
/// <c>OfficeClaimProfilePolicy</c>だけでなくruntime写像（snapshot→算定）でも検証する（Task 13）。
/// 値の正は対象月マスタ束のtransition-rules行（seed由来）であり、本guardは制度値を持たない。
/// </summary>
/// <remarks>
/// 検証は2点に閉じる:
/// <list type="number">
/// <item>profileの<c>MasterVersion</c>に対応するtransition-rules行が対象月に一意へ存在すること。
/// 版のeditionは月を分割する（R6-04 / R6-06 / R8-06）ため、この一致がR8-06境界での
/// 旧版profile残留（例: snapshot readerが期限切れprofileを返すケース）も遮断する。</item>
/// <item>宣言済み<c>R8ReformStatus</c>×版付き数値optionの組合せが行の状態別許可集合に
/// 含まれること（ADR 0023 フェイルクローズ条件「R8ReformStatusと許可option群の不一致」）。</item>
/// </list>
/// profile欠落・master版未設定・状態未確定・非数値optionは既存のreadiness／request builder
/// 経路が先に算定不能へ倒すため、本guardは重複issueを作らず空を返す（推測もしない）。
/// 不一致は<see cref="ClaimPreparationIssueCode.ReformTransitionMismatch"/>で可視化し、
/// 算定・確定を停止する。
/// </remarks>
public static class OfficeClaimProfileTransitionGuard
{
    internal const string MasterVersionField =
        nameof(OfficeClaimProfile) + "." + nameof(OfficeClaimProfile.MasterVersion);

    public static IReadOnlyList<ClaimPreparationIssue> Validate(
        IReadOnlyList<OfficeClaimProfileTransitionRuleMasterRow> monthTransitionRules,
        OfficeClaimProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(monthTransitionRules);

        if (profile is null
            || profile.MasterVersion is not { } masterVersion
            || profile.ReformStatus is not { } reformStatus
            || reformStatus == R8ReformStatus.Unknown
            || profile.AverageWageBandOption is not { } option
            || option.Kind != AverageWageBandOptionKind.Numeric)
        {
            return [];
        }

        var rows = monthTransitionRules
            .Where(row => row.MasterVersion == masterVersion)
            .Take(2)
            .ToArray();
        if (rows.Length != 1)
        {
            return
            [
                new ClaimPreparationIssue(
                    ClaimPreparationIssueCode.ReformTransitionMismatch,
                    RecipientId: null,
                    MasterVersionField,
                    ClaimInputDestination.ClaimInput),
            ];
        }

        if (!rows[0].AllowedOptionsByR8ReformStatus.TryGetValue(reformStatus, out var allowedOptions)
            || !allowedOptions.Contains(option))
        {
            return
            [
                new ClaimPreparationIssue(
                    ClaimPreparationIssueCode.ReformTransitionMismatch,
                    RecipientId: null,
                    ClaimCalculationRequestBuilder.AverageWageBandOptionField,
                    ClaimInputDestination.ClaimInput),
            ];
        }

        return [];
    }
}
