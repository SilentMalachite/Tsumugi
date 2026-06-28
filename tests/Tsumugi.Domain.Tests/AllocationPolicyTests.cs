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
    public void Zero_weights_with_zero_total_yields_all_zero()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 0m), S(2, 0m) }, 0,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        r.Should().HaveCount(2);
        r.Should().AllSatisfy(t => t.AmountYen.Should().Be(0));
    }

    [Fact]
    public void Zero_weights_with_positive_total_and_largest_remainder_throws()
    {
        var act = () => AllocationPolicy.Allocate(
            new[] { S(1, 0m), S(2, 0m) }, 1000,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("配分対象の総重みが 0 のため、原資 1,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
    }

    [Fact]
    public void Zero_weights_with_positive_total_and_reserve_to_office_dumps_all_to_office()
    {
        var officeKey = new Guid("00000000-0000-0000-0000-000099999999");
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 0m), S(2, 0m) }, 1000,
            RoundingRule.FloorYen, RemainderPolicy.ReserveToOffice, officeKey);
        r.Should().Contain(t => t.Key == officeKey && t.AmountYen == 1000);
        r.Where(t => t.Key != officeKey).Sum(t => t.AmountYen).Should().Be(0);
        r.Sum(t => t.AmountYen).Should().Be(1000);
    }

    [Fact]
    public void Zero_weights_with_positive_total_and_reserve_to_office_present_in_shares_writes_to_existing_office_entry()
    {
        var officeKey = new Guid("00000000-0000-0000-0000-000099999999");
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 0m), (officeKey, 0m), S(2, 0m) }, 1000,
            RoundingRule.FloorYen, RemainderPolicy.ReserveToOffice, officeKey);
        r.Should().HaveCount(3);
        r.First(t => t.Key == officeKey).AmountYen.Should().Be(1000);
        r.Where(t => t.Key != officeKey).Sum(t => t.AmountYen).Should().Be(0);
        r.Sum(t => t.AmountYen).Should().Be(1000);
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
