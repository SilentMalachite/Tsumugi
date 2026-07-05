using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IRecipientHourlyRateRepository
{
    Task AddAsync(RecipientHourlyRate rate, CancellationToken ct);
    Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(
        Guid officeId, Guid recipientId, CancellationToken ct);
}
