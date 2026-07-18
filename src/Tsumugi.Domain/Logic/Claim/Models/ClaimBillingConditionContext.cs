namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>
/// 算定条件の入力。値はすべて呼び出し側で閉じる（I/O・時刻に依存しない）。
/// 定員は事業所の実定員（頭数）を渡す。ordinal区分（cap-20-or-lessなど）へのマッピングは
/// マスタ側（<see cref="ClaimConditionDefinition"/> の閾値条件）が担い、呼び出し側はハードコードしない。
/// </summary>
public sealed record ClaimBillingConditionContext(
    string RewardSystem,
    string PaymentBand,
    int CapacityHeadcount,
    string StaffingKey,
    AverageWageBandOption AverageWageBandOption,
    R8ReformStatus R8ReformStatus);
