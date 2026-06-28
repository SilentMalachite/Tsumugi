using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class WageStatementViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Office = Guid.NewGuid();

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    private sealed class InMemoryStatementRepo : IWageStatementRepository
    {
        public List<WageStatement> Items { get; } = new();
        public Task AddAsync(WageStatement s, CancellationToken ct) { Items.Add(s); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageStatement>> ListByOfficeAndMonthAsync(Guid o, int y, int m, CancellationToken ct)
        {
            var ym = new YearMonth(y, m);
            return Task.FromResult<IReadOnlyList<WageStatement>>(Items.Where(s => s.OfficeId == o && s.Month == ym).ToList());
        }
    }

    private sealed class StubReportGenerator : IWageReportGenerator
    {
        public byte[] LastStatement = Array.Empty<byte>();
        public byte[] LastPaymentList = Array.Empty<byte>();
        public byte[] GenerateStatement(WageStatementDto s, RecipientDto r, OfficeDto o)
            => LastStatement = System.Text.Encoding.UTF8.GetBytes($"STMT:{r.KanjiName}:{s.AmountYen}");
        public byte[] GeneratePaymentList(IReadOnlyList<WageStatementDto> ss, IReadOnlyDictionary<Guid, RecipientDto> rs, OfficeDto o, int y, int m)
            => LastPaymentList = System.Text.Encoding.UTF8.GetBytes($"LIST:{o.Name}:{ss.Count}:{ss.Sum(s => s.AmountYen)}");
    }

    private static WageStatementViewModel Build(
        IWageStatementRepository stmtRepo,
        IRecipientRepository recipientRepo,
        params (Tsumugi.Domain.Entities.WorkRecord w, Tsumugi.Domain.Entities.DailyRecord d, Contract c)[] datasets)
    {
        var dailies = datasets.Select(t => t.d).ToArray();
        var works = datasets.Select(t => t.w).ToArray();
        var contracts = datasets.Select(t => t.c).ToArray();
        var settings = WageSettings.Create(Guid.NewGuid(), Office,
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "u", T0);
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), 100_000, null, "u", T0);

        var calc = new CalculateWagesUseCase(
            new WageCalculationViewModelTests_DailyRepoLocal(dailies),
            new WageCalculationViewModelTests_WorkRepoLocal(works),
            new WageCalculationViewModelTests_FundRepoLocal(fund),
            new WageCalculationViewModelTests_SettingsRepoLocal(settings),
            new WageCalculationViewModelTests_ContractRepoLocal(contracts),
            recipientRepo,
            AllStrategies);

        var close = new CloseWagesUseCase(calc, stmtRepo, new InMemoryUow(),
            new FixedClock(T0), new ViewModelNoopAuditTrail());
        var query = new QueryWageStatementUseCase(stmtRepo);
        var listRecipients = new ListRecipientsUseCase(recipientRepo);
        return new WageStatementViewModel(close, query, listRecipients, new StubReportGenerator());
    }

    [Fact]
    public async Task RefreshAsync_with_empty_office_id_sets_error()
    {
        var vm = new WageStatementViewModel(
            null!, null!, null!, new StubReportGenerator());
        vm.OfficeId = Guid.Empty;
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CloseAsync_persists_statements_and_refreshes_list()
    {
        var rid = Guid.NewGuid();
        var r = Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1), "u", T0, Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);
        var datasets = new[]
        {
            (
                Tsumugi.Domain.Entities.WorkRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
                    600, null, null, null, null, "u", T0),
                Tsumugi.Domain.Entities.DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
                    Attendance.Present, TransportKind.None, false, null, "u", T0),
                Contract.Create(Guid.NewGuid(), rid, period, 22, "u", T0, Guid.NewGuid())
            )
        };
        var stmtRepo = new InMemoryStatementRepo();
        var recipientRepo = new InMemoryRecipientRepoForWork();
        recipientRepo.Add(r);

        var vm = Build(stmtRepo, recipientRepo, datasets);
        vm.OfficeId = Office;
        vm.Year = 2026; vm.Month = 7;

        await vm.CloseCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        vm.Statements.Should().ContainSingle();
        vm.Statements[0].AmountYen.Should().Be(100_000);
        vm.StatusMessage.Should().Contain("確定");
    }

    [Fact]
    public async Task GenerateStatementPdf_returns_bytes_after_close()
    {
        var rid = Guid.NewGuid();
        var r = Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1), "u", T0, Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);
        var datasets = new[]
        {
            (
                Tsumugi.Domain.Entities.WorkRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
                    600, null, null, null, null, "u", T0),
                Tsumugi.Domain.Entities.DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
                    Attendance.Present, TransportKind.None, false, null, "u", T0),
                Contract.Create(Guid.NewGuid(), rid, period, 22, "u", T0, Guid.NewGuid())
            )
        };
        var stmtRepo = new InMemoryStatementRepo();
        var recipientRepo = new InMemoryRecipientRepoForWork();
        recipientRepo.Add(r);

        var vm = Build(stmtRepo, recipientRepo, datasets);
        vm.OfficeId = Office;
        vm.Office = new OfficeDto(Office, "0000000001", "テスト事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());
        vm.Year = 2026; vm.Month = 7;
        await vm.CloseCommand.ExecuteAsync(null);

        var bytes = vm.GenerateStatementPdf(rid);
        bytes.Should().NotBeNull();
        System.Text.Encoding.UTF8.GetString(bytes!).Should().Contain("100000");
    }

    [Fact]
    public void GenerateStatementPdf_without_office_sets_error()
    {
        var vm = new WageStatementViewModel(null!, null!, null!, new StubReportGenerator());
        vm.GenerateStatementPdf(Guid.NewGuid()).Should().BeNull();
        vm.ErrorMessage.Should().Contain("事業所");
    }
}

internal sealed class ViewModelNoopAuditTrail : Tsumugi.Application.Audit.IAuditTrail
{
    public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct) => Task.CompletedTask;
}

// Locally-scoped fake repos for WageStatementViewModelTests to avoid leaking into the WageCalculation set.
internal sealed class WageCalculationViewModelTests_DailyRepoLocal(IEnumerable<Tsumugi.Domain.Entities.DailyRecord> seed) : IDailyRecordRepository
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

internal sealed class WageCalculationViewModelTests_WorkRepoLocal(IEnumerable<Tsumugi.Domain.Entities.WorkRecord> seed) : IWorkRecordRepository
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

internal sealed class WageCalculationViewModelTests_FundRepoLocal(WageFund seed) : IWageFundRepository
{
    private readonly List<WageFund> _items = new() { seed };
    public Task AddAsync(WageFund f, CancellationToken ct) { _items.Add(f); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(Guid o, int y, int m, CancellationToken ct)
    {
        var ym = new YearMonth(y, m);
        return Task.FromResult<IReadOnlyList<WageFund>>(_items.Where(f => f.OfficeId == o && f.Month == ym).ToList());
    }
}

internal sealed class WageCalculationViewModelTests_SettingsRepoLocal(WageSettings seed) : IWageSettingsRepository
{
    private readonly List<WageSettings> _items = new() { seed };
    public Task AddAsync(WageSettings s, CancellationToken ct) { _items.Add(s); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid o, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WageSettings>>(_items.Where(s => s.OfficeId == o).ToList());
}

internal sealed class WageCalculationViewModelTests_ContractRepoLocal(IEnumerable<Contract> seed) : IContractRepository
{
    private readonly List<Contract> _items = seed.ToList();
    public Task AddAsync(Contract c, CancellationToken ct) { _items.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid rid, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contract>>(_items.Where(c => c.RecipientId == rid).ToList());
    public Task<Contract?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct)
        => Task.FromResult<Contract?>(_items.Where(c => c.RecipientId == rid && c.Period.Contains(asOf)).FirstOrDefault());
}
