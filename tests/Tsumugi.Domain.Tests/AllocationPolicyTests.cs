using FluentAssertions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class AllocationPolicyTests
{
    private static (Guid Key, decimal Weight) S(int idx, decimal w) =>
        (new Guid($"00000000-0000-0000-0000-{idx:D12}"), w);

    private static int Sum(IReadOnlyList<(Guid Key, int AmountYen)> allocs) => allocs.Sum(a => a.AmountYen);

    [Fact]
    public void Empty_shares_returns_empty()
    {
        var r = AllocationPolicy.Allocate(
            Array.Empty<(Guid, decimal)>(), 1000, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        r.Should().BeEmpty();
    }

    [Fact]
    public void All_zero_weights_yield_zero_amounts()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 0m), S(2, 0m) }, 1000,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        r.Should().AllSatisfy(t => t.AmountYen.Should().Be(0));
    }

    [Fact]
    public void Even_split_when_weights_equal()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 1m), S(2, 1m), S(3, 1m) }, 300,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        Sum(r).Should().Be(300);
        r.Should().AllSatisfy(t => t.AmountYen.Should().Be(100));
    }

    [Fact]
    public void Largest_remainder_distributes_leftover_yen()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 1m), S(2, 1m), S(3, 1m) }, 100,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        Sum(r).Should().Be(100);
        r[0].AmountYen.Should().Be(34);
        r[1].AmountYen.Should().Be(33);
        r[2].AmountYen.Should().Be(33);
    }

    [Fact]
    public void Reserve_to_office_dumps_remainder_to_office_key()
    {
        var officeKey = new Guid("00000000-0000-0000-0000-000099999999");
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 1m), S(2, 1m), S(3, 1m) }, 100,
            RoundingRule.FloorYen, RemainderPolicy.ReserveToOffice, officeKey);
        Sum(r).Should().Be(100);
        r.Should().Contain(t => t.Key == officeKey && t.AmountYen == 1);
        r.Where(t => t.Key != officeKey).Sum(t => t.AmountYen).Should().Be(99);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(7, 100)]
    [InlineData(13, 100000)]
    [InlineData(17, 99991)]
    public void Sum_invariant_holds_for_random_weights(int count, int total)
    {
        var rng = new Random(count * 31 + total);
        var shares = Enumerable.Range(1, count)
            .Select(i => S(i, (decimal)rng.NextDouble() * 100))
            .ToArray();
        var r = AllocationPolicy.Allocate(
            shares, total, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        Sum(r).Should().Be(total, "Σ AmountYen == totalYen 不変条件");
        r.Should().AllSatisfy(t => t.AmountYen.Should().BeGreaterThanOrEqualTo(0));
    }
}
