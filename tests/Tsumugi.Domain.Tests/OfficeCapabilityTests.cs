using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class OfficeCapabilityTests
{
    [Fact]
    public void Create_holds_flag_map_as_extensible_set()
    {
        var period = new DateRange(new DateOnly(2026, 4, 1), End: null);
        var flags = new Dictionary<string, bool>
        {
            // ★要・報酬告示突合（暫定）: フラグキーはフェーズ3で正式コードに置換
            ["mealProvision"] = true,
            ["transportSupport"] = false,
        };

        var cap = OfficeCapability.Create(
            id: Guid.NewGuid(), officeId: Guid.NewGuid(),
            period: period, flags: flags,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid());

        cap.Period.Should().Be(period);
        cap.Flags["mealProvision"].Should().BeTrue();
        cap.Flags["transportSupport"].Should().BeFalse();
    }

    [Fact]
    public void Flags_are_defensively_copied()
    {
        var dict = new Dictionary<string, bool> { ["a"] = true };
        var cap = OfficeCapability.Create(Guid.NewGuid(), Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null), dict,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        dict["a"] = false;
        cap.Flags["a"].Should().BeTrue("外部の Dictionary 変更が record の状態を壊してはいけない");
    }
}
