using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeClaimProfilePolicyProvider
{
    OfficeClaimProfilePolicy Resolve(ClaimMasterVersion masterVersion);
}

public enum ClaimMasterPolicyUnavailableCode
{
    Unavailable = 1,
    Ambiguous = 2,
    InvalidMaster = 3,
}

public sealed class ClaimMasterPolicyUnavailableException : Exception
{
    public ClaimMasterPolicyUnavailableException(ClaimMasterPolicyUnavailableCode code)
        : base($"Claim master policy unavailable: {code}.")
    {
        Code = code;
    }

    public ClaimMasterPolicyUnavailableCode Code { get; }
}
