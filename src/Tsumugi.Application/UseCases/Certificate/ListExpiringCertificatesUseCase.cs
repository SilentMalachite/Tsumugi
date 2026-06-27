using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed class ListExpiringCertificatesUseCase(ICertificateRepository repo)
{
    public async Task<IReadOnlyList<ExpiringCertificateDto>> ExecuteAsync(
        DateOnly asOf, int thresholdDays, CancellationToken ct)
    {
        var all = await repo.ListAllAsync(ct);
        var hits = CertificatePolicy.FindExpiring(all, asOf, thresholdDays);
        return hits.Select(h => new ExpiringCertificateDto(
            h.Certificate.Id, h.Certificate.RecipientId, h.Certificate.CertificateNumber,
            h.Certificate.Validity.End!.Value, h.RemainingDays)).ToArray();
    }
}
