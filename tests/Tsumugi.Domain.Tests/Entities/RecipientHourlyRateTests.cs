// tests/Tsumugi.Domain.Tests/Entities/RecipientHourlyRateTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Entities;

public class RecipientHourlyRateTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly DateRange Period =
        new(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));

    [Fact]
    public void NewRecord_sets_defaults()
    {
        var r = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, Period, 350,
            "tester", DateTimeOffset.UtcNow);
        r.Kind.Should().Be(RecordKind.New);
        r.OriginId.Should().BeNull();
        r.HourlyYen.Should().Be(350);
    }

    [Fact]
    public void NewRecord_rejects_negative_hourly()
    {
        var act = () => RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, Period, -1,
            "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Correction_requires_origin_id()
    {
        var origin = Guid.NewGuid();
        var r = RecipientHourlyRate.Correction(
            Guid.NewGuid(), Office, Recipient, Period, origin, 400,
            "tester", DateTimeOffset.UtcNow);
        r.Kind.Should().Be(RecordKind.Correct);
        r.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancel_requires_origin_id()
    {
        var origin = Guid.NewGuid();
        var r = RecipientHourlyRate.Cancel(
            Guid.NewGuid(), Office, Recipient, Period, origin,
            "tester", DateTimeOffset.UtcNow);
        r.Kind.Should().Be(RecordKind.Cancel);
        r.HourlyYen.Should().Be(0);
    }
}
