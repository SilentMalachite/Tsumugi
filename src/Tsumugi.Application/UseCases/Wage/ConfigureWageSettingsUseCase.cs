using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class ConfigureWageSettingsUseCase(
    IWageSettingsRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<WageSettingsDto> ExecuteAsync(
        Guid officeId, DateRange period,
        WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
        int fiscalYearStartMonth, int? fixedDailyYen,
        int? workAllowancePerDayYen,
        IReadOnlyList<SkillAllowanceTier>? skillAllowanceTiers,
        int hourUnitMinutes,
        string actor, CancellationToken ct)
    {
        if (officeId == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(officeId));
        DateValidator.EnsureRange(period.Start, period.End, nameof(period));

        var entity = WageSettings.Create(
            Guid.NewGuid(), officeId, period,
            method, rounding, remainder, fiscalYearStartMonth, fixedDailyYen,
            workAllowancePerDayYen, skillAllowanceTiers, hourUnitMinutes,
            actor, clock.GetUtcNow());

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static WageSettingsDto Map(WageSettings e) =>
        new(e.Id, e.OfficeId, e.Period, e.Method, e.Rounding, e.Remainder,
            e.FiscalYearStartMonth, e.FixedDailyYen,
            e.WorkAllowancePerDayYen, e.SkillAllowanceTiers, e.HourUnitMinutes);
}
