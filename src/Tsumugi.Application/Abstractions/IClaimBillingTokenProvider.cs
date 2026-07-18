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
    ClaimBillingConditionTokens Resolve(Office office, ServiceMonth serviceMonth);
}

/// <summary>
/// 解決済みの算定条件トークン束。<c>null</c>メンバは「解決不能（readiness issueへ）」を意味する。
/// <see cref="CapacityHeadcount"/>（利用定員の実頭数）と<see cref="StaffingKey"/>（人員配置区分token）は
/// ADR 0021が<c>OfficeClaimProfile</c>の構造化入力と定める項目に対応するが、現行エンティティには
/// 未実装のため、production実装は常に<c>null</c>を返す（将来taskでprofile拡張後に解決される）。
/// </summary>
public sealed record ClaimBillingConditionTokens(
    string? RewardSystem,
    string? RegionKey,
    string? RegionUnitPriceServiceKind,
    int? CapacityHeadcount,
    string? StaffingKey);
