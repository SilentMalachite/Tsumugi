namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>
/// 算定条件の入力。値はすべて呼び出し側で閉じる（I/O・時刻に依存しない）。
/// 定員は事業所の実定員（頭数）を渡す。ordinal区分（cap-20-or-lessなど）へのマッピングは
/// マスタ側（<see cref="ClaimConditionDefinition"/> の閾値条件）が担い、呼び出し側はハードコードしない。
/// </summary>
/// <param name="OfficeCapabilityKeys">
/// 実効な事業所体制届（<c>OfficeCapability</c>）の有効フラグキー集合（ADR 0021の正式one-hotキー、
/// 値trueのキーのみ）。<c>null</c>は「体制届が未取得（判定不能）」を表し、体制条件つき行の解決は
/// フェイルクローズする。空集合は「体制届上いずれの加算体制もない」= 該当加算を算定しない
/// （正しい請求挙動）。production builderは常に非nullを渡す（未登録なら空集合）。
/// </param>
public sealed record ClaimBillingConditionContext(
    string RewardSystem,
    string PaymentBand,
    int CapacityHeadcount,
    string StaffingKey,
    AverageWageBandOption AverageWageBandOption,
    R8ReformStatus R8ReformStatus,
    IReadOnlyCollection<string>? OfficeCapabilityKeys = null);
