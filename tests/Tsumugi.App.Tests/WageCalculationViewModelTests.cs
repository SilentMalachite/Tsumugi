using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class WageCalculationViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Office = Guid.NewGuid();

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    private sealed class FakeRecipientRepo(params Recipient[] seed) : IRecipientRepository
    {
        private readonly List<Recipient> _items = seed.ToList();
        public Task AddAsync(Recipient r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
        public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Recipient?>(_items.FirstOrDefault(r => r.Id == id));
        public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Recipient>>(_items.Where(r => includeArchived || r.ArchivedAt is null).ToList());
    }

    private sealed class FakeContractRepo(params Contract[] seed) : IContractRepository
    {
        private readonly List<Contract> _items = seed.ToList();
        public Task AddAsync(Contract c, CancellationToken ct) { _items.Add(c); return Task.CompletedTask; }
        public Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid rid, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Contract>>(_items.Where(c => c.RecipientId == rid).ToList());
        public Task<Contract?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct)
            => Task.FromResult<Contract?>(_items.Where(c => c.RecipientId == rid && c.Period.Contains(asOf)).FirstOrDefault());
    }

    private sealed class FakeDailyRepo(params Tsumugi.Domain.Entities.DailyRecord[] seed) : IDailyRecordRepository
    {
        private readonly List<Tsumugi.Domain.Entities.DailyRecord> _items = seed.ToList();
        public Task AddAsync(Tsumugi.Domain.Entities.DailyRecord r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
        public Task<Tsumugi.Domain.Entities.DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Tsumugi.Domain.Entities.DailyRecord?>(_items.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>> ListByRecipientAndDateAsync(Guid rid, DateOnly d, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>>(_items.Where(r => r.RecipientId == rid && r.ServiceDate == d).ToList());
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>> ListByRecipientAndMonthAsync(Guid rid, int y, int m, CancellationToken ct)
        {
            var from = new DateOnly(y, m, 1); var to = from.AddMonths(1).AddDays(-1);
            return Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.DailyRecord>>(
                _items.Where(r => r.RecipientId == rid && r.ServiceDate >= from && r.ServiceDate <= to).ToList());
        }
    }

    private sealed class FakeWorkRepo(params Tsumugi.Domain.Entities.WorkRecord[] seed) : IWorkRecordRepository
    {
        private readonly List<Tsumugi.Domain.Entities.WorkRecord> _items = seed.ToList();
        public Task AddAsync(Tsumugi.Domain.Entities.WorkRecord r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
        public Task<Tsumugi.Domain.Entities.WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<Tsumugi.Domain.Entities.WorkRecord?>(_items.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<Tsumugi.Domain.Entities.WorkRecord>> ListByRecipientAndMonthAsync(Guid rid, int y, int m, CancellationToken ct)
        {
            var from = new DateOnly(y, m, 1); var to = from.AddMonths(1).AddDays(-1);
            return Task.FromResult<IReadOnlyList<Tsumugi.Domain.Entities.WorkRecord>>(
                _items.Where(r => r.RecipientId == rid && r.WorkDate >= from && r.WorkDate <= to).ToList());
        }
    }

    private sealed class FakeFundRepo(params WageFund[] seed) : IWageFundRepository
    {
        private readonly List<WageFund> _items = seed.ToList();
        public Task AddAsync(WageFund f, CancellationToken ct) { _items.Add(f); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(Guid o, int y, int m, CancellationToken ct)
        {
            var ym = new YearMonth(y, m);
            return Task.FromResult<IReadOnlyList<WageFund>>(_items.Where(f => f.OfficeId == o && f.Month == ym).ToList());
        }
    }

    private sealed class FakeSettingsRepo(params WageSettings[] seed) : IWageSettingsRepository
    {
        private readonly List<WageSettings> _items = seed.ToList();
        public Task AddAsync(WageSettings s, CancellationToken ct) { _items.Add(s); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid o, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<WageSettings>>(_items.Where(s => s.OfficeId == o).ToList());
    }

    [Fact]
    public async Task LoadPreviewAsync_with_empty_office_id_sets_error()
    {
        var calc = new CalculateWagesUseCase(
            new FakeDailyRepo(), new FakeWorkRepo(), new FakeFundRepo(),
            new FakeSettingsRepo(), new FakeContractRepo(), new FakeRecipientRepo(),
            AllStrategies);
        var vm = new WageCalculationViewModel(calc) { Year = 2026, Month = 7 };
        await vm.LoadPreviewCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPreviewAsync_with_missing_settings_sets_error_from_usecase()
    {
        var calc = new CalculateWagesUseCase(
            new FakeDailyRepo(), new FakeWorkRepo(), new FakeFundRepo(),
            new FakeSettingsRepo(), new FakeContractRepo(), new FakeRecipientRepo(),
            AllStrategies);
        var vm = new WageCalculationViewModel(calc) { OfficeId = Office, Year = 2026, Month = 7 };
        await vm.LoadPreviewCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Contain("工賃設定");
    }

    [Fact]
    public async Task LoadPreviewAsync_populates_lines_and_summary_on_success()
    {
        var rid = Guid.NewGuid();
        var r = Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1), "u", T0, Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);
        var settings = WageSettings.Create(Guid.NewGuid(), Office, period,
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "u", T0);
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), 100_000, null, "u", T0);
        var daily = Tsumugi.Domain.Entities.DailyRecord.NewRecord(
            Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
            Attendance.Present, TransportKind.None, false, null, "u", T0);
        var work = Tsumugi.Domain.Entities.WorkRecord.NewRecord(
            Guid.NewGuid(), rid, new DateOnly(2026, 7, 1), 600, null, null, null, null, "u", T0);
        var contract = Contract.Create(Guid.NewGuid(), rid, period, 22, "u", T0, Guid.NewGuid());

        var calc = new CalculateWagesUseCase(
            new FakeDailyRepo(daily), new FakeWorkRepo(work),
            new FakeFundRepo(fund), new FakeSettingsRepo(settings),
            new FakeContractRepo(contract), new FakeRecipientRepo(r),
            AllStrategies);
        var vm = new WageCalculationViewModel(calc) { OfficeId = Office, Year = 2026, Month = 7 };

        await vm.LoadPreviewCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        vm.Lines.Should().HaveCount(1);
        vm.Lines[0].AmountYen.Should().Be(100_000);
        vm.Method.Should().Be(WageMethod.Hourly);
        vm.SummaryLine.Should().Contain("100,000");
        vm.HasMismatchWarning.Should().BeFalse();
    }
}
