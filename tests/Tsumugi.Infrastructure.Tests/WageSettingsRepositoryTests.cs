using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WageSettingsRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public WageSettingsRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_by_office_round_trip()
    {
        var officeId = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new WageSettingsRepository(ctx);

        var settings = WageSettings.Create(
            Guid.NewGuid(), officeId,
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
            createdBy: "t", createdAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(settings, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeAsync(officeId, default);
        list.Should().HaveCount(1);
        list[0].Method.Should().Be(WageMethod.Hourly);
        list[0].Period.Start.Should().Be(new DateOnly(2026, 4, 1));
    }

    /// <summary>
    /// Smoke test: WageSettings with HourUnitMinutes=15 and empty SkillAllowanceTiers
    /// survives a full round-trip through the DB without JsonException.
    /// Guards the fix for review findings:
    ///   - SkillAllowanceTiersJson defaultValue "" caused JsonException on load.
    ///   - HourUnitMinutes defaultValue 0 was not in AllowedHourUnitMinutes.
    /// </summary>
    [Fact]
    public async Task WageSettings_with_fixed_defaults_round_trips_without_exception()
    {
        var officeId = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new WageSettingsRepository(ctx);

        var settings = WageSettings.Create(
            Guid.NewGuid(), officeId,
            new DateRange(new DateOnly(2026, 1, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
            createdBy: "migration-fix-test", createdAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(settings, default);
        await ctx.SaveChangesAsync();

        await using var ctx2 = _fixture.NewContext();
        var repo2 = new WageSettingsRepository(ctx2);
        var loaded = await repo2.ListByOfficeAsync(officeId, default);

        loaded.Should().ContainSingle();
        loaded[0].HourUnitMinutes.Should().Be(15);
        loaded[0].SkillAllowanceTiers.Should().BeEmpty();
    }
}
