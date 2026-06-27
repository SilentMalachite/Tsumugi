using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class Phase1EntitiesRoundTripTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public Phase1EntitiesRoundTripTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Recipient_round_trips()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Recipients.Add(Recipient.Create(id, "山田", "ヤマダ",
                new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var r = await ctx.Recipients.SingleAsync(x => x.Id == id);
            r.KanjiName.Should().Be("山田");
            r.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        }
    }

    [Fact]
    public async Task Certificate_with_date_range_round_trips()
    {
        var id = Guid.NewGuid();
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Certificates.Add(Certificate.Create(
                id, Guid.NewGuid(), "12345", validity, 22, 9300, "杉並区",
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var c = await ctx.Certificates.SingleAsync(x => x.Id == id);
            c.Validity.Should().Be(validity);
        }
    }

    [Fact]
    public async Task OfficeCapability_flags_round_trip_as_json()
    {
        var id = Guid.NewGuid();
        var flags = new Dictionary<string, bool> { ["mealProvision"] = true, ["transportSupport"] = false };
        await using (var ctx = _fixture.NewContext())
        {
            ctx.OfficeCapabilities.Add(OfficeCapability.Create(
                id, Guid.NewGuid(),
                new DateRange(new DateOnly(2026, 4, 1), null), flags,
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var cap = await ctx.OfficeCapabilities.SingleAsync(x => x.Id == id);
            cap.Flags["mealProvision"].Should().BeTrue();
            cap.Flags["transportSupport"].Should().BeFalse();
        }
    }

    [Fact]
    public async Task DailyRecord_appends_and_round_trips()
    {
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 1);
        var newId = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.DailyRecords.Add(DailyRecord.NewRecord(newId, rid, day,
                Attendance.Present, TransportKind.Round, true, "通常", "u", DateTimeOffset.UnixEpoch));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var r = await ctx.DailyRecords.SingleAsync(x => x.Id == newId);
            r.Attendance.Should().Be(Attendance.Present);
            r.Transport.Should().Be(TransportKind.Round);
            r.MealProvided.Should().BeTrue();
        }
    }
}
