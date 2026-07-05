using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class CalculateWagesUseCaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Office = Guid.NewGuid();

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    private static Recipient Rec(Guid id) => Recipient.Create(
        id, "氏名", "シメイ", new DateOnly(1990, 1, 1), "t", T0, Guid.NewGuid());

    private static Contract ContractFor(Guid recipientId, DateRange period) => Contract.Create(
        Guid.NewGuid(), recipientId, period, contractedSupplyDays: 22,
        createdBy: "t", createdAt: T0, concurrencyToken: Guid.NewGuid());

    private static WageSettings Settings(WageMethod method, int? fixedYen = null) => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        method, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, fixedYen,
        workAllowancePerDayYen: null, skillAllowanceTiers: null, hourUnitMinutes: 15, "t", T0);

    private static WageFund Fund(int yen) =>
        WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), yen, null, "t", T0);

    private static Tsumugi.Domain.Entities.DailyRecord Present(Guid rid, DateOnly date) =>
        Tsumugi.Domain.Entities.DailyRecord.NewRecord(
            Guid.NewGuid(), rid, date, Attendance.Present, TransportKind.None, false, null, "t", T0);

    private static Tsumugi.Domain.Entities.WorkRecord Work(Guid rid, DateOnly date, int minutes) =>
        Tsumugi.Domain.Entities.WorkRecord.NewRecord(
            Guid.NewGuid(), rid, date, minutes, null, null, null, null, "t", T0);

    private sealed class FakeRecipientRepo(IEnumerable<Recipient> seed) : IRecipientRepository
    {
        private readonly List<Recipient> _items = seed.ToList();
        public Task AddAsync(Recipient r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
        public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Recipient?>(_items.FirstOrDefault(r => r.Id == id));
        public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Recipient>>(_items.Where(r => includeArchived || r.ArchivedAt is null).ToList());
    }

    private sealed class FakeContractRepo(IEnumerable<Contract> seed) : IContractRepository
    {
        private readonly List<Contract> _items = seed.ToList();
        public Task AddAsync(Contract c, CancellationToken ct) { _items.Add(c); return Task.CompletedTask; }
        public Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid rid, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Contract>>(_items.Where(c => c.RecipientId == rid).ToList());
        public Task<Contract?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct)
            => Task.FromResult<Contract?>(_items
                .Where(c => c.RecipientId == rid && c.Period.Contains(asOf))
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault());
    }

    private sealed class FakeDailyRepo(IEnumerable<Tsumugi.Domain.Entities.DailyRecord> seed) : IDailyRecordRepository
    {
        private readonly List<Tsumugi.Domain.Entities.DailyRecord> _items = seed.ToList();
        public Task AddAsync(Tsumugi.Domain.Entities.DailyRecord r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
        public Task<Tsumugi.Domain.Entities.DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Tsumugi.Domain.Entities.DailyRecord?>(_items.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>> ListByRecipientAndDateAsync(Guid rid, DateOnly d, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>>(_items.Where(r => r.RecipientId == rid && r.ServiceDate == d).ToList());
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>> ListByRecipientAndMonthAsync(Guid rid, int year, int month, CancellationToken ct)
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            return Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>>(
                _items.Where(r => r.RecipientId == rid && r.ServiceDate >= from && r.ServiceDate <= to).ToList());
        }
    }

    private sealed class FakeWorkRepo(IEnumerable<Tsumugi.Domain.Entities.WorkRecord> seed) : IWorkRecordRepository
    {
        private readonly List<Tsumugi.Domain.Entities.WorkRecord> _items = seed.ToList();
        public Task AddAsync(Tsumugi.Domain.Entities.WorkRecord r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
        public Task<Tsumugi.Domain.Entities.WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Tsumugi.Domain.Entities.WorkRecord?>(_items.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.WorkRecord>> ListByRecipientAndMonthAsync(Guid rid, int year, int month, CancellationToken ct)
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            return Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.WorkRecord>>(
                _items.Where(r => r.RecipientId == rid && r.WorkDate >= from && r.WorkDate <= to).ToList());
        }
    }

    private sealed class FakeFundRepo(IEnumerable<WageFund> seed) : IWageFundRepository
    {
        private readonly List<WageFund> _items = seed.ToList();
        public Task AddAsync(WageFund f, CancellationToken ct) { _items.Add(f); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(Guid officeId, int y, int m, CancellationToken ct)
        {
            var ym = new YearMonth(y, m);
            return Task.FromResult<IReadOnlyList<WageFund>>(_items.Where(f => f.OfficeId == officeId && f.Month == ym).ToList());
        }
    }

    private sealed class FakeSettingsRepo(IEnumerable<WageSettings> seed) : IWageSettingsRepository
    {
        private readonly List<WageSettings> _items = seed.ToList();
        public Task AddAsync(WageSettings s, CancellationToken ct) { _items.Add(s); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid officeId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<WageSettings>>(_items.Where(s => s.OfficeId == officeId).ToList());
    }

    private sealed class FakeHourlyRateRepo : IRecipientHourlyRateRepository
    {
        public Task AddAsync(Tsumugi.Domain.Entities.RecipientHourlyRate rate, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.RecipientHourlyRate>> ListByOfficeRecipientAsync(Guid officeId, Guid recipientId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.RecipientHourlyRate>>(Array.Empty<Tsumugi.Domain.Entities.RecipientHourlyRate>());
    }

    private sealed class FakeAdjustmentRepo : IWageAdjustmentRepository
    {
        public Task AddAsync(Tsumugi.Domain.Entities.WageAdjustment adjustment, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.WageAdjustment>> ListByOfficeMonthAsync(Guid officeId, YearMonth yearMonth, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.WageAdjustment>>(Array.Empty<Tsumugi.Domain.Entities.WageAdjustment>());
    }

    [Fact]
    public async Task Missing_settings_throws()
    {
        var u = new CalculateWagesUseCase(
            new FakeDailyRepo(Array.Empty<Tsumugi.Domain.Entities.DailyRecord>()),
            new FakeWorkRepo(Array.Empty<Tsumugi.Domain.Entities.WorkRecord>()),
            new FakeFundRepo(Array.Empty<WageFund>()),
            new FakeSettingsRepo(Array.Empty<WageSettings>()),
            new FakeContractRepo(Array.Empty<Contract>()),
            new FakeRecipientRepo(Array.Empty<Recipient>()),
            new FakeHourlyRateRepo(), new FakeAdjustmentRepo(),
            AllStrategies);
        var act = async () => await u.ExecuteAsync(Office, 2026, 7, default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*工賃設定が見つかりません*");
    }

    [Fact]
    public async Task Hourly_requires_fund()
    {
        var u = new CalculateWagesUseCase(
            new FakeDailyRepo(Array.Empty<Tsumugi.Domain.Entities.DailyRecord>()),
            new FakeWorkRepo(Array.Empty<Tsumugi.Domain.Entities.WorkRecord>()),
            new FakeFundRepo(Array.Empty<WageFund>()),
            new FakeSettingsRepo(new[] { Settings(WageMethod.Hourly) }),
            new FakeContractRepo(Array.Empty<Contract>()),
            new FakeRecipientRepo(Array.Empty<Recipient>()),
            new FakeHourlyRateRepo(), new FakeAdjustmentRepo(),
            AllStrategies);
        var act = async () => await u.ExecuteAsync(Office, 2026, 7, default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WageFund*");
    }

    [Fact]
    public async Task Piece_method_returns_per_recipient_amounts()
    {
        var r1 = Rec(Guid.NewGuid());
        var r2 = Rec(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);

        var u = new CalculateWagesUseCase(
            new FakeDailyRepo(new[] { Present(r1.Id, new DateOnly(2026, 7, 1)), Present(r2.Id, new DateOnly(2026, 7, 1)) }),
            new FakeWorkRepo(new[]
            {
                Tsumugi.Domain.Entities.WorkRecord.NewRecord(Guid.NewGuid(), r1.Id, new DateOnly(2026, 7, 1), 240, 5, 100, 0, null, "t", T0),
                Tsumugi.Domain.Entities.WorkRecord.NewRecord(Guid.NewGuid(), r2.Id, new DateOnly(2026, 7, 1), 240, 3, 100, 0, null, "t", T0),
            }),
            new FakeFundRepo(Array.Empty<WageFund>()),
            new FakeSettingsRepo(new[] { Settings(WageMethod.Piece) }),
            new FakeContractRepo(new[] { ContractFor(r1.Id, period), ContractFor(r2.Id, period) }),
            new FakeRecipientRepo(new[] { r1, r2 }),
            new FakeHourlyRateRepo(), new FakeAdjustmentRepo(),
            AllStrategies);

        var preview = await u.ExecuteAsync(Office, 2026, 7, default);
        preview.Method.Should().Be(WageMethod.Piece);
        preview.TotalFundYen.Should().Be(0);
        preview.Lines.Should().HaveCount(2);
        preview.TotalAllocatedYen.Should().Be(800);  // 5*100 + 3*100
    }

    [Fact]
    public async Task Hourly_method_preserves_sigma_invariant()
    {
        var r1 = Rec(Guid.NewGuid());
        var r2 = Rec(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);

        var u = new CalculateWagesUseCase(
            new FakeDailyRepo(new[] { Present(r1.Id, new DateOnly(2026, 7, 1)), Present(r2.Id, new DateOnly(2026, 7, 1)) }),
            new FakeWorkRepo(new[]
            {
                Work(r1.Id, new DateOnly(2026, 7, 1), 600),
                Work(r2.Id, new DateOnly(2026, 7, 1), 400),
            }),
            new FakeFundRepo(new[] { Fund(100_000) }),
            new FakeSettingsRepo(new[] { Settings(WageMethod.Hourly) }),
            new FakeContractRepo(new[] { ContractFor(r1.Id, period), ContractFor(r2.Id, period) }),
            new FakeRecipientRepo(new[] { r1, r2 }),
            new FakeHourlyRateRepo(), new FakeAdjustmentRepo(),
            AllStrategies);

        var preview = await u.ExecuteAsync(Office, 2026, 7, default);
        preview.TotalFundYen.Should().Be(100_000);
        preview.TotalAllocatedYen.Should().Be(100_000);
        preview.Lines.First(l => l.RecipientId == r1.Id).AmountYen.Should().Be(60_000);
        preview.Lines.First(l => l.RecipientId == r2.Id).AmountYen.Should().Be(40_000);
    }

    [Fact]
    public async Task Recipients_without_contract_are_excluded()
    {
        var r1 = Rec(Guid.NewGuid());
        var r2 = Rec(Guid.NewGuid());  // 契約なし

        var u = new CalculateWagesUseCase(
            new FakeDailyRepo(new[] { Present(r1.Id, new DateOnly(2026, 7, 1)), Present(r2.Id, new DateOnly(2026, 7, 1)) }),
            new FakeWorkRepo(new[]
            {
                Work(r1.Id, new DateOnly(2026, 7, 1), 600),
                Work(r2.Id, new DateOnly(2026, 7, 1), 600),
            }),
            new FakeFundRepo(new[] { Fund(100_000) }),
            new FakeSettingsRepo(new[] { Settings(WageMethod.Hourly) }),
            new FakeContractRepo(new[] { ContractFor(r1.Id, new DateRange(new DateOnly(2026, 4, 1), null)) }),
            new FakeRecipientRepo(new[] { r1, r2 }),
            new FakeHourlyRateRepo(), new FakeAdjustmentRepo(),
            AllStrategies);

        var preview = await u.ExecuteAsync(Office, 2026, 7, default);
        preview.Lines.Should().ContainSingle();
        preview.Lines[0].RecipientId.Should().Be(r1.Id);
        preview.Lines[0].AmountYen.Should().Be(100_000);
    }

    [Fact]
    public async Task Hourly_with_all_zero_minutes_and_positive_fund_throws_to_preserve_sigma_invariant()
    {
        var r1 = Rec(Guid.NewGuid());
        var r2 = Rec(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);

        var u = new CalculateWagesUseCase(
            new FakeDailyRepo(new[] { Present(r1.Id, new DateOnly(2026, 7, 1)), Present(r2.Id, new DateOnly(2026, 7, 1)) }),
            new FakeWorkRepo(new[]
            {
                Work(r1.Id, new DateOnly(2026, 7, 1), 0),
                Work(r2.Id, new DateOnly(2026, 7, 1), 0),
            }),
            new FakeFundRepo(new[] { Fund(100_000) }),
            new FakeSettingsRepo(new[] { Settings(WageMethod.Hourly) }),
            new FakeContractRepo(new[] { ContractFor(r1.Id, period), ContractFor(r2.Id, period) }),
            new FakeRecipientRepo(new[] { r1, r2 }),
            new FakeHourlyRateRepo(), new FakeAdjustmentRepo(),
            AllStrategies);

        var act = async () => await u.ExecuteAsync(Office, 2026, 7, default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("配分対象の総重みが 0 のため、原資 100,000 円を最大剰余法で配分できません。事業所留保へ切り替えるか、原資を 0 円に設定してください。");
    }
}
