using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class ContractTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var period = new DateRange(new DateOnly(2026, 4, 1), End: null);
        var c = Contract.Create(
            id: Guid.NewGuid(), recipientId: Guid.NewGuid(),
            period: period, contractedSupplyDays: 22,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid());

        c.Period.Should().Be(period);
        c.ContractedSupplyDays.Should().Be(22);
    }
}
