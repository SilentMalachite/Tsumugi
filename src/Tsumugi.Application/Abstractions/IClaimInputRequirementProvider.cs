using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Abstractions;

public interface IClaimInputRequirementProvider
{
    /// <summary>
    /// 指定した CSV 仕様版の readiness 要件。版を明示しない入口は置かない
    /// （「どの版で評価したのか」が曖昧になると、版が動いたときに検証できなくなる）。
    /// 未登録の版は例外（推測で現行版の要件を使わない）。
    /// </summary>
    IReadOnlyList<ClaimInputRequirement> GetRequirements(string specificationVersion);
}
