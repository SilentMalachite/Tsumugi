// tests/Tsumugi.Domain.Tests/Entities/WageAdjustmentTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Entities;

public class WageAdjustmentTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly YearMonth Ym = YearMonth.FromInt(202605);

    [Fact]
    public void NewRecord_sets_defaults()
    {
        var w = WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, note: null,
            "tester", DateTimeOffset.UtcNow);
        w.Kind.Should().Be(RecordKind.New);
        w.OriginId.Should().BeNull();
        w.AmountYen.Should().Be(1000);
    }

    [Fact]
    public void NewRecord_rejects_negative_amount()
    {
        var act = () => WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, -1, null,
            "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Correction_rejects_empty_origin_id()
    {
        var act = () => WageAdjustment.Correction(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, Guid.Empty, 500, null,
            "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_rejects_empty_origin_id()
    {
        var act = () => WageAdjustment.Cancel(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, Guid.Empty,
            "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Correction_and_cancel_require_origin_id()
    {
        var act1 = () => WageAdjustment.Correction(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, Guid.Empty, 500, null,
            "tester", DateTimeOffset.UtcNow);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => WageAdjustment.Cancel(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, Guid.Empty,
            "tester", DateTimeOffset.UtcNow);
        act2.Should().Throw<ArgumentException>();
    }
}
