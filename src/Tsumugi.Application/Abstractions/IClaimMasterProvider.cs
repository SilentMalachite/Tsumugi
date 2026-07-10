using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IClaimMasterProvider
{
    ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth);
}
