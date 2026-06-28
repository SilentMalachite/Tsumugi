using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WageFundRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public WageFundRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_by_office_month_round_trip()
    {
        var officeId = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new WageFundRepository(ctx);

        var fund = WageFund.NewRecord(Guid.NewGuid(), officeId, new YearMonth(2026, 7),
            totalYen: 350_000, note: null, createdBy: "t", createdAt: DateTimeOffset.UtcNow);
        await repo.AddAsync(fund, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeAndMonthAsync(officeId, 2026, 7, default);
        list.Should().HaveCount(1);
        list[0].TotalYen.Should().Be(350_000);
        list[0].Month.Should().Be(new YearMonth(2026, 7));
    }
}
