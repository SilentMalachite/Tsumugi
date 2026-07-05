using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class WageMasterUseCaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryFundRepo : IWageFundRepository
    {
        public List<WageFund> Items { get; } = new();
        public Task AddAsync(WageFund f, CancellationToken ct) { Items.Add(f); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(
            Guid officeId, int year, int month, CancellationToken ct)
        {
            var ym = new YearMonth(year, month);
            IReadOnlyList<WageFund> r = Items
                .Where(x => x.OfficeId == officeId && x.Month == ym)
                .OrderBy(x => x.CreatedAt)
                .ToList();
            return Task.FromResult(r);
        }
    }

    private sealed class InMemorySettingsRepo : IWageSettingsRepository
    {
        public List<WageSettings> Items { get; } = new();
        public Task AddAsync(WageSettings s, CancellationToken ct) { Items.Add(s); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid officeId, CancellationToken ct)
        {
            IReadOnlyList<WageSettings> r = Items.Where(x => x.OfficeId == officeId).ToList();
            return Task.FromResult(r);
        }
    }

    [Fact]
    public async Task First_set_creates_new_fund()
    {
        var repo = new InMemoryFundRepo();
        var u = new SetWageFundUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(T0));
        var dto = await u.ExecuteAsync(Guid.NewGuid(), 2026, 7, 300_000, null, "alice", default);
        dto.TotalYen.Should().Be(300_000);
        dto.Kind.Should().Be(RecordKind.New);
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Second_set_appends_correction_referring_origin()
    {
        var repo = new InMemoryFundRepo();
        var officeId = Guid.NewGuid();
        var u = new SetWageFundUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(T0));

        var first = await u.ExecuteAsync(officeId, 2026, 7, 300_000, null, "alice", default);
        var second = await u.ExecuteAsync(officeId, 2026, 7, 280_000, "下方修正", "alice", default);

        second.TotalYen.Should().Be(280_000);
        second.Kind.Should().Be(RecordKind.Correct);
        second.OriginId.Should().Be(first.Id);
        repo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Negative_total_is_rejected()
    {
        var repo = new InMemoryFundRepo();
        var u = new SetWageFundUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(T0));
        var act = async () => await u.ExecuteAsync(Guid.NewGuid(), 2026, 7, -1, null, "a", default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Configure_settings_persists_entity()
    {
        var repo = new InMemorySettingsRepo();
        var u = new ConfigureWageSettingsUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(T0));
        var dto = await u.ExecuteAsync(
            Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
            actor: "alice", default);
        dto.Method.Should().Be(WageMethod.Hourly);
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Configure_settings_fixed_method_requires_daily_yen()
    {
        var repo = new InMemorySettingsRepo();
        var u = new ConfigureWageSettingsUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(T0));
        var act = async () => await u.ExecuteAsync(
            Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Fixed, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder,
            4, null, workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15,
            "alice", default);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*FixedDailyYen*");
    }
}
