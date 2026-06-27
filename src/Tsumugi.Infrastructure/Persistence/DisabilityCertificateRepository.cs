using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class DisabilityCertificateRepository(TsumugiDbContext db) : IDisabilityCertificateRepository
{
    public async Task AddAsync(DisabilityCertificate certificate, CancellationToken ct) =>
        await db.DisabilityCertificates.AddAsync(certificate, ct);

    public async Task<IReadOnlyList<DisabilityCertificate>> ListByRecipientAsync(
        Guid recipientId, CancellationToken ct) =>
        await db.DisabilityCertificates.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .OrderByDescending(c => c.IssuedDate)
            .ToListAsync(ct);
}
