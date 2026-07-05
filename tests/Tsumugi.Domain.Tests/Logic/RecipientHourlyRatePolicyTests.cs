// tests/Tsumugi.Domain.Tests/Logic/RecipientHourlyRatePolicyTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic;

public class RecipientHourlyRatePolicyTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static DateTimeOffset At(int day) => new(2026, 4, day, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EffectiveYen_returns_null_when_no_records()
    {
        RecipientHourlyRatePolicy.EffectiveYen(
            Array.Empty<RecipientHourlyRate>(), A, new DateOnly(2026, 5, 1))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_returns_yen_within_period()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var r = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r }, A, new DateOnly(2026, 4, 15))
            .Should().Be(350);
    }

    [Fact]
    public void EffectiveYen_uses_latest_correction()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        var corr = RecipientHourlyRate.Correction(Guid.NewGuid(), Office, A, p, newRec.Id, 400, "u", At(2));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, corr }, A, new DateOnly(2026, 4, 15))
            .Should().Be(400);
    }

    [Fact]
    public void EffectiveYen_returns_null_after_cancel()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        var cancel = RecipientHourlyRate.Cancel(Guid.NewGuid(), Office, A, p, newRec.Id, "u", At(3));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, cancel }, A, new DateOnly(2026, 4, 15))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_filters_by_recipient()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var r = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r }, B, new DateOnly(2026, 4, 15))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_follows_correction_of_correction_chain()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        var c1 = RecipientHourlyRate.Correction(Guid.NewGuid(), Office, A, p, newRec.Id, 400, "u", At(2));
        var c2 = RecipientHourlyRate.Correction(Guid.NewGuid(), Office, A, p, c1.Id, 450, "u", At(3));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, c1, c2 }, A, new DateOnly(2026, 4, 15))
            .Should().Be(450);
    }

    [Fact]
    public void EffectiveYen_returns_null_when_cancel_targets_correction()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        var c1 = RecipientHourlyRate.Correction(Guid.NewGuid(), Office, A, p, newRec.Id, 400, "u", At(2));
        var cancel = RecipientHourlyRate.Cancel(Guid.NewGuid(), Office, A, p, c1.Id, "u", At(3));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, c1, cancel }, A, new DateOnly(2026, 4, 15))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_uses_corrected_period_when_correction_changes_period()
    {
        var p1 = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));
        var p2 = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p1, 350, "u", At(1));
        var corr = RecipientHourlyRate.Correction(Guid.NewGuid(), Office, A, p2, newRec.Id, 350, "u", At(2));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, corr }, A, new DateOnly(2026, 5, 15))
            .Should().Be(350);
    }

    [Fact]
    public void EffectiveYen_switches_across_multiple_periods()
    {
        var p1 = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 29));
        var p2 = new DateRange(new DateOnly(2026, 4, 30), new DateOnly(2026, 6, 30));
        var r1 = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p1, 350, "u", At(1));
        var r2 = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p2, 400, "u", At(2));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r1, r2 }, A, new DateOnly(2026, 4, 29)).Should().Be(350);
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r1, r2 }, A, new DateOnly(2026, 4, 30)).Should().Be(400);
    }
}
