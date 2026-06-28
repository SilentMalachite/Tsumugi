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
            createdBy: "t", createdAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(settings, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeAsync(officeId, default);
        list.Should().HaveCount(1);
        list[0].Method.Should().Be(WageMethod.Hourly);
        list[0].Period.Start.Should().Be(new DateOnly(2026, 4, 1));
    }
}
