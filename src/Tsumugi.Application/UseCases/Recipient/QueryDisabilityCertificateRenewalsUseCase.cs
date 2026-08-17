using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>精神手帳の更新期日候補を全件照会し DTO 化する。</summary>
public sealed class QueryDisabilityCertificateRenewalsUseCase(IDisabilityCertificateRepository repo)
{
    public async Task<IReadOnlyList<DisabilityCertificateRenewalDueDto>> ExecuteAsync(
        DateOnly asOf, int thresholdDays, CancellationToken ct)
    {
        var all = await repo.ListAllAsync(ct);
        var current = all
            .GroupBy(certificate => new { certificate.RecipientId, certificate.Type })
            .Select(group => group
                .OrderByDescending(certificate => certificate.IssuedDate)
                .ThenByDescending(certificate => certificate.CreatedAt)
                .First())
            .ToArray();
        var hits = DisabilityCertificatePolicy.FindRenewalDue(current, asOf, thresholdDays);
        // NextRenewalDate!.Value: FindRenewalDue は更新日 null を除外する。
        return hits.Select(hit => new DisabilityCertificateRenewalDueDto(
            hit.Certificate.Id,
            hit.Certificate.RecipientId,
            hit.Certificate.Type,
            hit.Certificate.Grade,
            hit.Certificate.NextRenewalDate!.Value,
            hit.RemainingDays))
            .ToArray();
    }
}
