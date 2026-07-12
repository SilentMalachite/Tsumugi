using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Abstractions;

public interface IClaimInputRequirementProvider
{
    IReadOnlyList<ClaimInputRequirement> GetRequirements();
}
