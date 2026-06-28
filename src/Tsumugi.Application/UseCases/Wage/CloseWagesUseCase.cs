using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class CloseWagesUseCase(
    CalculateWagesUseCase preview,
    IWageStatementRepository statementRepo,
    IUnitOfWork uow,
    TimeProvider clock,
    IAuditTrail audit)
{
    public async Task<IReadOnlyList<WageStatementDto>> ExecuteAsync(
        Guid officeId, int year, int month, string actor, CancellationToken ct)
    {
        if (officeId == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(officeId));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));
        DateValidator.EnsureYearMonth(year, month);

        var calc = await preview.ExecuteAsync(officeId, year, month, ct);
        var existing = await statementRepo.ListByOfficeAndMonthAsync(officeId, year, month, ct);
        var existingNewByRecipient = existing
            .Where(s => s.Kind == RecordKind.New)
            .GroupBy(s => s.RecipientId)
            .ToDictionary(g => g.Key, g => g.First());
        var now = clock.GetUtcNow();
        var ym = new YearMonth(year, month);
        var results = new List<WageStatementDto>(calc.Lines.Count);

        foreach (var line in calc.Lines)
        {
            var stmt = existingNewByRecipient.TryGetValue(line.RecipientId, out var origin)
                ? WageStatement.Correction(Guid.NewGuid(), officeId, ym, line.RecipientId, origin.Id,
                    line.AmountYen, line.BasisSummary, actor, now)
                : WageStatement.NewRecord(Guid.NewGuid(), officeId, ym, line.RecipientId,
                    line.AmountYen, line.BasisSummary, actor, now);
            await statementRepo.AddAsync(stmt, ct);
            results.Add(Map(stmt, year, month));
        }

        await audit.RecordAsync(actor, AuditAction.Update, nameof(WageStatement),
            officeId, now,
            summary: $"close wages {year}-{month:D2}; total={calc.TotalAllocatedYen}円; recipients={results.Count}",
            ct);
        await uow.SaveChangesAsync(ct);
        return results;
    }

    internal static WageStatementDto Map(WageStatement s, int year, int month) =>
        new(s.Id, s.OfficeId, year, month, s.RecipientId,
            s.AmountYen, s.BasisSummary, s.Kind, s.OriginId);
}
