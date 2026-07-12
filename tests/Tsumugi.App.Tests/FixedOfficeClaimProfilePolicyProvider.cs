using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.App.Tests;

internal sealed class FixedOfficeClaimProfilePolicyProvider(OfficeClaimProfilePolicy policy)
    : IOfficeClaimProfilePolicyProvider
{
    public OfficeClaimProfilePolicy Resolve(ClaimMasterVersion masterVersion) => policy;
}
