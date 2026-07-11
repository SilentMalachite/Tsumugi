using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IClaimInputRepository
{
    Task AddAsync(ClaimInput input, CancellationToken ct);
    Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
        Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct);
}

public interface IIntensiveSupportEpisodeRepository
{
    Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct);
    Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
        Guid officeId, Guid recipientId, CancellationToken ct);
}

public interface IAverageWageAnnualEvidenceRepository
{
    Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct);
    Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
        Guid officeId, int sourceFiscalYear, CancellationToken ct);
}

public interface IOfficeClaimProfileRepository
{
    Task AddAsync(OfficeClaimProfile profile, CancellationToken ct);
    Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
        Guid officeId, CancellationToken ct);
}

public interface ICertificateClaimEvidenceRepository
{
    Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct);
    Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
        Guid certificateId, CancellationToken ct);
}

public interface IUpperLimitManagementStatementRepository
{
    Task AddAsync(
        UpperLimitManagementStatement statement,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines,
        CancellationToken ct);

    Task<IReadOnlyList<UpperLimitManagementStatementAggregate>> ListHistoryAggregatesAsync(
        Guid managingOfficeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        CancellationToken ct);
}
