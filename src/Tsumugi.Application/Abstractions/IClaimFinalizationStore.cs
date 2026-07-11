namespace Tsumugi.Application.Abstractions;

public interface IClaimFinalizationStore
{
    Task<ClaimFinalizationResult> CommitAsync(ClaimFinalizationDraft draft, CancellationToken ct);
}
