using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Wage;

public sealed class QueryWageAdjustmentUseCaseTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly YearMonth Ym = YearMonth.FromInt(202605);

    [Fact]
    public async Task Returns_empty_list_when_no_adjustments()
    {
        var repo = new FakeQueryAdjustmentRepo([]);
        var uc = new QueryWageAdjustmentUseCase(repo);

        var result = await uc.ExecuteAsync(Office, Ym, default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_adjustments_for_office_and_month()
    {
        var adjustment = WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, "テスト",
            "alice", DateTimeOffset.UtcNow);
        var repo = new FakeQueryAdjustmentRepo([adjustment]);
        var uc = new QueryWageAdjustmentUseCase(repo);

        var result = await uc.ExecuteAsync(Office, Ym, default);

        result.Should().HaveCount(1);
        result[0].AmountYen.Should().Be(1000);
        result[0].OfficeId.Should().Be(Office);
        result[0].YearMonth.Should().Be(Ym);
    }

    [Fact]
    public async Task Returns_multiple_adjustments_mapped_correctly()
    {
        var adj1 = WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 500, null,
            "alice", DateTimeOffset.UtcNow);
        var adj2 = WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 300, "2件目",
            "bob", DateTimeOffset.UtcNow);
        var repo = new FakeQueryAdjustmentRepo([adj1, adj2]);
        var uc = new QueryWageAdjustmentUseCase(repo);

        var result = await uc.ExecuteAsync(Office, Ym, default);

        result.Should().HaveCount(2);
    }
}

file sealed class FakeQueryAdjustmentRepo(IEnumerable<WageAdjustment> seed) : IWageAdjustmentRepository
{
    private readonly List<WageAdjustment> _items = seed.ToList();
    public Task AddAsync(WageAdjustment adjustment, CancellationToken ct)
    { _items.Add(adjustment); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth ym, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WageAdjustment>>(
            _items.Where(a => a.OfficeId == officeId && a.YearMonth == ym).ToArray());
}
