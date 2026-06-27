using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed class ListCertificatesByRecipientUseCase(ICertificateRepository repo)
{
    public async Task<IReadOnlyList<CertificateDto>> ExecuteAsync(
        Guid recipientId, CancellationToken ct)
    {
        var list = await repo.ListByRecipientAsync(recipientId, ct);
        return list
            .OrderByDescending(c => c.Validity.Start)
            .Select(RegisterCertificateUseCase.MapToDto)
            .ToArray();
    }
}
