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
            r.SpecialVisitSupportBilledHours.Should().BeNull();
        }
    }

    [Fact]
    public async Task DailyRecord_round_trips_special_visit_support_billed_hours_independently_of_minutes()
    {
        // provider:J611:02:027（サービス提供時間数・分）と provider:J611:02:028（算定時間数・時間）は
        // 別列として保存され、互いに換算されない。
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 2);
        var newId = Guid.NewGuid();
        var correctionId = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.DailyRecords.Add(DailyRecord.NewRecord(newId, rid, day,
                Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch,
                specialVisitSupportMinutes: 90,
                specialVisitSupportBilledHours: 0));
            ctx.DailyRecords.Add(DailyRecord.Correction(correctionId, rid, day, newId,
                Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch,
                specialVisitSupportMinutes: 90,
                specialVisitSupportBilledHours: 3));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            var created = await ctx.DailyRecords.SingleAsync(x => x.Id == newId);
            var corrected = await ctx.DailyRecords.SingleAsync(x => x.Id == correctionId);

            created.SpecialVisitSupportMinutes.Should().Be(90);
            created.SpecialVisitSupportBilledHours.Should().Be(0);
            corrected.SpecialVisitSupportMinutes.Should().Be(90);
            corrected.SpecialVisitSupportBilledHours.Should().Be(3);
        }
    }

    [Fact]
    public async Task DailyRecord_cancellation_stores_no_special_visit_support_billed_hours()
    {
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 3);
        var newId = Guid.NewGuid();
        var cancelId = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.DailyRecords.Add(DailyRecord.NewRecord(newId, rid, day,
                Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch,
                specialVisitSupportBilledHours: 4));
            ctx.DailyRecords.Add(DailyRecord.Cancellation(cancelId, rid, day, newId,
                "u", DateTimeOffset.UnixEpoch));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _fixture.NewContext())
        {
            (await ctx.DailyRecords.SingleAsync(x => x.Id == newId))
                .SpecialVisitSupportBilledHours.Should().Be(4);
            (await ctx.DailyRecords.SingleAsync(x => x.Id == cancelId))
                .SpecialVisitSupportBilledHours.Should().BeNull();
        }
    }
}
