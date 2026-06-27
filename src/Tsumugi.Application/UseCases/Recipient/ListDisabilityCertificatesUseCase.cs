using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class ListDisabilityCertificatesUseCase(IDisabilityCertificateRepository repo)
{
    public async Task<IReadOnlyList<DisabilityCertificateDto>> ExecuteAsync(
        Guid recipientId, CancellationToken ct)
    {
        var list = await repo.ListByRecipientAsync(recipientId, ct);
        return list.Select(RegisterDisabilityCertificateUseCase.ToDto).ToArray();
    }
}
