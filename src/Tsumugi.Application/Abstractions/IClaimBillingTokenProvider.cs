using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 算定条件contextへ渡す正準トークン（ADR 0027のseed語彙）と事業所側条件入力を、
/// 型付きの事業所マスタから解決する。seedの正準文字列はDomain/Applicationへ
/// ハードコードできない（`ClaimSpecificationBoundaryTests` のclaim-master-literal検査）ため、
/// seedと同居するInfrastructure実装が値として供給し、本層は値だけを受け取る。
/// 解決できない要素は<c>null</c>で返し、呼び出し側（<c>ClaimCalculationRequestBuilder</c>）が
/// <c>ClaimPreparationIssue</c>へ変換してフェイルクローズする（推測しない）。
/// </summary>
public interface IClaimBillingTokenProvider
{
    /// <summary>
    /// <paramref name="profile"/>はスナップショットの実効<c>OfficeClaimProfile</c>
    /// （<see cref="ClaimCalculationSnapshot.Profile"/>）を渡す。定員(実頭数)・人員配置区分token
    /// はprofile必須（未入力はnull）。地域区分tokenの解決は実装依存
    /// （<c>OfficeClaimBillingTokenProvider</c>参照。Task 9b）。
    /// </summary>
    ClaimBillingConditionTokens Resolve(Office office, OfficeClaimProfile? profile, ServiceMonth serviceMonth);
}

/// <summary>
/// 解決済みの算定条件トークン束。<c>null</c>メンバは「解決不能（readiness issueへ）」を意味する。
/// <see cref="CapacityHeadcount"/>（利用定員の実頭数）と<see cref="StaffingKey"/>（人員配置区分token）は
/// ADR 0021が<c>OfficeClaimProfile</c>の構造化入力と定める項目で、<c>OfficeClaimProfile</c>から
/// そのまま供給する（Task 9bでprofile拡張）。
/// </summary>
public sealed record ClaimBillingConditionTokens(
    string? RewardSystem,
    string? RegionKey,
    string? RegionUnitPriceServiceKind,
    int? CapacityHeadcount,
    string? StaffingKey);
