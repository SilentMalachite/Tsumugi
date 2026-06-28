using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class SetWageFundUseCase(
    IWageFundRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<WageFundDto> ExecuteAsync(
        Guid officeId, int year, int month, int totalYen, string? note,
        string actor, CancellationToken ct)
    {
        if (officeId == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(officeId));
        DateValidator.EnsureYearMonth(year, month);
        ArgumentOutOfRangeException.ThrowIfNegative(totalYen);

        var existing = await repo.ListByOfficeAndMonthAsync(officeId, year, month, ct);
        var effective = WageFundPolicy.Effective(existing);
        var now = clock.GetUtcNow();
        var ym = new YearMonth(year, month);

        var entity = effective is null
            ? WageFund.NewRecord(Guid.NewGuid(), officeId, ym, totalYen, note, actor, now)
            : WageFund.Correction(Guid.NewGuid(), officeId, ym, effective.Id, totalYen, note, actor, now);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static WageFundDto Map(WageFund e) =>
        new(e.Id, e.OfficeId, e.Month.Year, e.Month.Month, e.TotalYen, e.Kind, e.OriginId, e.Note);
}
