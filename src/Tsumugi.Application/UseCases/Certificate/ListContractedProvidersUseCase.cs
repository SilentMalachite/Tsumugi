using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed class ListContractedProvidersUseCase(IContractedProviderRepository repo)
{
    public async Task<IReadOnlyList<ContractedProviderDto>> ExecuteAsync(
        Guid certificateId, CancellationToken ct)
    {
        var list = await repo.ListByCertificateAsync(certificateId, ct);
        return list.Select(RegisterContractedProviderUseCase.ToDto).ToArray();
    }
}
