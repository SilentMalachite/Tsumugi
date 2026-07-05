using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WageAdjustmentRepositoryTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    private static readonly Guid OfficeId = Guid.NewGuid();

    public WageAdjustmentRepositoryTests(SqliteFixture f) => _fixture = f;

    [Fact]
    public async Task Add_and_list_returns_by_office_and_month()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var ym = YearMonth.FromInt(202605);

        await using var ctx = _fixture.NewContext();
        var repo = new WageAdjustmentRepository(ctx);

        var w = WageAdjustment.NewRecord(
            Guid.NewGuid(), officeId, recipientId, ym,
            WageAdjustmentType.SpecialAllowance, 1000, null,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(w, default);
        await ctx.SaveChangesAsync();

        var list = await repo.ListByOfficeMonthAsync(officeId, ym, default);
        list.Should().ContainSingle().Which.AmountYen.Should().Be(1000);
    }

    [Fact]
    public async Task Duplicate_new_is_rejected_by_partial_unique_index()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var ym = YearMonth.FromInt(202606);

        await using var ctx = _fixture.NewContext();
        var repo = new WageAdjustmentRepository(ctx);

        var first = WageAdjustment.NewRecord(
            Guid.NewGuid(), officeId, recipientId, ym,
            WageAdjustmentType.SpecialAllowance, 500, null,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(first, default);
        await ctx.SaveChangesAsync();

        var dup = WageAdjustment.NewRecord(
            Guid.NewGuid(), officeId, recipientId, ym,
            WageAdjustmentType.SpecialAllowance, 800, null,
            "u", DateTimeOffset.UtcNow);
        await repo.AddAsync(dup, default);

        var act = async () => await ctx.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }
}
