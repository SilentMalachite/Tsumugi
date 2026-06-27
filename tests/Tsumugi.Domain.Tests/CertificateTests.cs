using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class CertificateTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        var c = Certificate.Create(
            id: Guid.NewGuid(),
            recipientId: Guid.NewGuid(),
            certificateNumber: "1234567890",
            validity: validity,
            supplyDays: 22,
            monthlyCostCap: 9300,
            municipality: "杉並区",
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid());

        c.Validity.Should().Be(validity);
        c.SupplyDays.Should().Be(22);
        c.MonthlyCostCap.Should().Be(9300);
        c.Municipality.Should().Be("杉並区");
    }

    [Fact]
    public void Create_rejects_negative_supply_days()
    {
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        Action act = () => Certificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1", validity,
            supplyDays: -1, monthlyCostCap: 0, municipality: "x",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
