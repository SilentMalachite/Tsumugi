using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ContractedProviderRepository(TsumugiDbContext db) : IContractedProviderRepository
{
    public async Task AddAsync(ContractedProvider provider, CancellationToken ct) =>
        await db.ContractedProviders.AddAsync(provider, ct);

    public Task<ContractedProvider?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.ContractedProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task UpdateAsync(ContractedProvider provider, CancellationToken ct)
    {
        db.ContractedProviders.Update(provider);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ContractedProvider>> ListByCertificateAsync(
        Guid certificateId, CancellationToken ct) =>
        await db.ContractedProviders.AsNoTracking()
            .Where(p => p.CertificateId == certificateId)
            .OrderBy(p => p.ContractDate)
            .ToListAsync(ct);
}
