using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class UpperLimitManagementStatementRepository(TsumugiDbContext db)
    : IUpperLimitManagementStatementRepository
{
    public async Task AddAsync(
        UpperLimitManagementStatement statement,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(lines);
        await db.UpperLimitManagementStatements.AddAsync(statement, ct);
        await db.UpperLimitManagementStatementLines.AddRangeAsync(lines, ct);
    }

    public async Task<IReadOnlyList<UpperLimitManagementStatementAggregate>> ListHistoryAggregatesAsync(
        Guid managingOfficeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        CancellationToken ct)
    {
        var headers = await db.UpperLimitManagementStatements
            .AsNoTracking()
            .Where(statement => statement.ManagingOfficeId == managingOfficeId
                                && statement.RecipientId == recipientId
                                && statement.ServiceMonth == serviceMonth)
            .OrderBy(statement => statement.CertificateId)
            .ThenBy(statement => statement.RootId)
            .ThenBy(statement => statement.Revision)
            .ToArrayAsync(ct);
        if (headers.Length == 0)
        {
            return [];
        }

        var statementIds = headers.Select(statement => statement.Id).ToArray();
        var lines = await db.UpperLimitManagementStatementLines
            .AsNoTracking()
            .Where(line => statementIds.Contains(line.StatementId))
            .OrderBy(line => line.StatementId)
            .ThenBy(line => line.LineNumber)
            .ToArrayAsync(ct);
        var linesByStatementId = lines.ToLookup(line => line.StatementId);

        return headers
            .Select(header => new UpperLimitManagementStatementAggregate(
                header,
                linesByStatementId[header.Id]))
            .ToArray();
    }
}
