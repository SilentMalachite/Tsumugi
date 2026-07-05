// tests/Tsumugi.Domain.Tests/Logic/WageAdjustmentPolicyTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic;

public class WageAdjustmentPolicyTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly YearMonth Ym = YearMonth.FromInt(202605);
    private static DateTimeOffset At(int day) => new(2026, 5, day, 0, 0, 0, TimeSpan.Zero);

    private static WageAdjustment New(int amount, DateTimeOffset at) =>
        WageAdjustment.NewRecord(Guid.NewGuid(), Office, A, Ym,
            WageAdjustmentType.SpecialAllowance, amount, null, "u", at);

    [Fact]
    public void Effective_returns_zero_when_empty()
    {
        WageAdjustmentPolicy.EffectiveYen(
            Array.Empty<WageAdjustment>(), A, Ym, WageAdjustmentType.SpecialAllowance)
            .Should().Be(0);
    }

    [Fact]
    public void Effective_returns_new_when_only_new()
    {
        var r = New(1000, At(1));
        WageAdjustmentPolicy.EffectiveYen(new[] { r }, A, Ym,
            WageAdjustmentType.SpecialAllowance).Should().Be(1000);
    }

    [Fact]
    public void Effective_applies_correction_over_new()
    {
        var n = New(1000, At(1));
        var c = WageAdjustment.Correction(Guid.NewGuid(), Office, A, Ym,
            WageAdjustmentType.SpecialAllowance, n.Id, 1500, null, "u", At(2));
        WageAdjustmentPolicy.EffectiveYen(new[] { n, c }, A, Ym,
            WageAdjustmentType.SpecialAllowance).Should().Be(1500);
    }

    [Fact]
    public void Effective_returns_zero_after_cancel()
    {
        var n = New(1000, At(1));
        var x = WageAdjustment.Cancel(Guid.NewGuid(), Office, A, Ym,
            WageAdjustmentType.SpecialAllowance, n.Id, "u", At(3));
        WageAdjustmentPolicy.EffectiveYen(new[] { n, x }, A, Ym,
            WageAdjustmentType.SpecialAllowance).Should().Be(0);
    }

    [Fact]
    public void SumEffective_sums_types()
    {
        var n = New(1000, At(1));
        WageAdjustmentPolicy.SumEffective(new[] { n }, A, Ym).Should().Be(1000);
    }
}
