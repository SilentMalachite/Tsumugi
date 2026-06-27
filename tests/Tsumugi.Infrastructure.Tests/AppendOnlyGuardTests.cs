using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public AppendOnlyGuardTests(SqliteFixture f) => _fixture = f;

    // NOTE(teeth): If AppendOnlyGuard.Inspect() is removed from TsumugiDbContext,
    // this test goes RED — proving the guard is wired and the test has real teeth.
    [Fact]
    public async Task Modifying_DailyRecord_throws()
    {
        var rid = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var rec = DailyRecord.NewRecord(id, rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        ctx.DailyRecords.Add(rec);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.DailyRecords.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(DailyRecord.Note)).CurrentValue = "after the fact";
        ctx.Entry(loaded).Property(nameof(DailyRecord.Note)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(e => e.EntityName == nameof(DailyRecord));
    }

    [Fact]
    public async Task Deleting_DailyRecord_throws()
    {
        var rid = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.DailyRecords.Add(DailyRecord.NewRecord(id, rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch));
        await ctx.SaveChangesAsync();

        ctx.DailyRecords.Remove(await ctx.DailyRecords.SingleAsync(x => x.Id == id));

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>();
    }

    [Fact]
    public async Task Modifying_Certificate_throws_period_master_is_append_only()
    {
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.Certificates.Add(Certificate.Create(id, Guid.NewGuid(), "1",
            new Domain.ValueObjects.DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Certificates.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(Certificate.Municipality)).CurrentValue = "別市";
        ctx.Entry(loaded).Property(nameof(Certificate.Municipality)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(e => e.EntityName == nameof(Certificate));
    }

    // NOTE(teeth): SaveChangesAsync(CancellationToken) しか override していないと、
    // 呼び出し側が ctx.SaveChangesAsync(acceptAllChangesOnSuccess, ct) を直接叩くだけで
    // AppendOnlyGuard と更新トークン回転を素通りできる。bool overload も覆っていることを担保する。
    [Fact]
    public async Task Modifying_DailyRecord_via_async_bool_overload_throws()
    {
        var rid = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.DailyRecords.Add(DailyRecord.NewRecord(id, rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch));
        await ctx.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None);

        var loaded = await ctx.DailyRecords.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(DailyRecord.Note)).CurrentValue = "after the fact";
        ctx.Entry(loaded).Property(nameof(DailyRecord.Note)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync(acceptAllChangesOnSuccess: false, CancellationToken.None);
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(e => e.EntityName == nameof(DailyRecord));
    }

    [Fact]
    public async Task Modifying_Office_is_allowed_identity_master_uses_token()
    {
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.Offices.Add(Office.Create(id, "1", "x",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Offices.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(Office.Name)).CurrentValue = "y";
        ctx.Entry(loaded).Property(nameof(Office.Name)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}
