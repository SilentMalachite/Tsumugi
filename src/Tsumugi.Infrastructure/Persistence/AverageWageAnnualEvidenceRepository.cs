using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class AverageWageAnnualEvidenceRepository(TsumugiDbContext db)
    : IAverageWageAnnualEvidenceRepository
{
    public async Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        await db.AverageWageAnnualEvidences.AddAsync(evidence, ct);
    }

    public async Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
        Guid officeId,
        int sourceFiscalYear,
        CancellationToken ct) =>
        await db.AverageWageAnnualEvidences
            .AsNoTracking()
            .Where(evidence => evidence.OfficeId == officeId
                               && evidence.SourceFiscalYear == sourceFiscalYear)
            .OrderBy(evidence => evidence.RootId)
            .ThenBy(evidence => evidence.Revision)
            .ToArrayAsync(ct);
}
