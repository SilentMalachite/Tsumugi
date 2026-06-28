using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
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

    private sealed class FakeFileSaveService : Tsumugi.App.Services.IFileSaveService
    {
        public byte[]? LastSavedBytes { get; private set; }
        public string? LastSuggestedFileName { get; private set; }
        public string? LastFileTypeName { get; private set; }
        public string? LastExtension { get; private set; }
        public bool ReturnValue { get; init; } = true;
        public Exception? ThrowOnSave { get; init; }

        public Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName, string extension, CancellationToken ct = default)
        {
            if (ThrowOnSave is not null) throw ThrowOnSave;
            LastSavedBytes = bytes;
            LastSuggestedFileName = suggestedFileName;
            LastFileTypeName = fileTypeName;
            LastExtension = extension;
            return Task.FromResult(ReturnValue);
        }
    }

    private static WageStatementViewModel Build(
        IWageStatementRepository? stmtRepo = null,
        IRecipientRepository? recipientRepo = null,
        (Tsumugi.Domain.Entities.WorkRecord w, Tsumugi.Domain.Entities.DailyRecord d, Contract c)[]? datasets = null,
        Tsumugi.Domain.Entities.Office[]? offices = null,
        Tsumugi.App.Services.IFileSaveService? fileSaveService = null)
    {
        var ds = datasets ?? Array.Empty<(Tsumugi.Domain.Entities.WorkRecord, Tsumugi.Domain.Entities.DailyRecord, Contract)>();
        var dailies = ds.Select(t => t.d).ToArray();
        var works = ds.Select(t => t.w).ToArray();
        var contracts = ds.Select(t => t.c).ToArray();
        var settings = WageSettings.Create(Guid.NewGuid(), Office,
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "u", T0);
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), 100_000, null, "u", T0);

        var recipientRepoSafe = recipientRepo ?? new InMemoryRecipientRepoForWork();
        var calc = new CalculateWagesUseCase(
            new WageCalculationViewModelTests_DailyRepoLocal(dailies),
            new WageCalculationViewModelTests_WorkRepoLocal(works),
            new WageCalculationViewModelTests_FundRepoLocal(fund),
            new WageCalculationViewModelTests_SettingsRepoLocal(settings),
            new WageCalculationViewModelTests_ContractRepoLocal(contracts),
            recipientRepoSafe,
            AllStrategies);

        var stmtRepoSafe = stmtRepo ?? new InMemoryStatementRepo();
        var close = new CloseWagesUseCase(calc, stmtRepoSafe, new InMemoryUow(),
            new FixedClock(T0), new ViewModelNoopAuditTrail());
        var query = new QueryWageStatementUseCase(stmtRepoSafe);
        var listRecipients = new ListRecipientsUseCase(recipientRepoSafe);
        var officeRepo = new InMemoryOfficeRepo();
        foreach (var o in offices ?? Array.Empty<Tsumugi.Domain.Entities.Office>()) officeRepo.Add(o);
        return new WageStatementViewModel(close, query, listRecipients, new StubReportGenerator(),
            new ListOfficesUseCase(officeRepo),
            fileSaveService ?? new FakeFileSaveService());
    }

    [Fact]
    public async Task RefreshAsync_with_empty_office_id_sets_error()
    {
        var vm = new WageStatementViewModel(
            null!, null!, null!, new StubReportGenerator(),
            new ListOfficesUseCase(new InMemoryOfficeRepo()),
            new FakeFileSaveService());
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
        var vm = new WageStatementViewModel(null!, null!, null!, new StubReportGenerator(),
            new ListOfficesUseCase(new InMemoryOfficeRepo()),
            new FakeFileSaveService());
        vm.GenerateStatementPdf(Guid.NewGuid()).Should().BeNull();
        vm.ErrorMessage.Should().Contain("事業所");
    }

    [Fact]
    public async Task InitializeAsync_loads_offices_for_selection()
    {
        var o = Tsumugi.Domain.Entities.Office.Create(Guid.NewGuid(), "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var vm = Build(offices: new[] { o });

        await vm.InitializeAsync();

        vm.Offices.Should().ContainSingle(x => x.Id == o.Id);
    }

    [Fact]
    public void Setting_SelectedOffice_updates_OfficeId_and_Office()
    {
        var vm = Build();
        var oid = Guid.NewGuid();
        var dto = new Tsumugi.Application.Dtos.OfficeDto(
            oid, "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB,
            Tsumugi.Domain.Enums.RegionGrade.None,
            Guid.NewGuid());

        vm.SelectedOffice = dto;

        vm.OfficeId.Should().Be(oid);
        vm.Office.Should().Be(dto);
    }

    [Fact]
    public void Clearing_SelectedOffice_resets_OfficeId_and_Office()
    {
        var vm = Build();
        var dto = new Tsumugi.Application.Dtos.OfficeDto(
            Guid.NewGuid(), "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB,
            Tsumugi.Domain.Enums.RegionGrade.None,
            Guid.NewGuid());
        vm.SelectedOffice = dto;

        vm.SelectedOffice = null;

        vm.OfficeId.Should().Be(Guid.Empty);
        vm.Office.Should().BeNull();
    }

    private static async Task<(WageStatementViewModel vm, FakeFileSaveService fake, Guid stmtId)> BuildWithClosedStatementAsync()
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

        var fake = new FakeFileSaveService();
        var vm = Build(stmtRepo, recipientRepo, datasets, fileSaveService: fake);
        vm.OfficeId = Office;
        vm.Office = new OfficeDto(Office, "0000000001", "テスト事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());
        vm.Year = 2026; vm.Month = 7;

        await vm.CloseCommand.ExecuteAsync(null);

        return (vm, fake, rid);
    }

    [Fact]
    public async Task SaveSelectedStatementPdf_without_selection_sets_error()
    {
        var fake = new FakeFileSaveService();
        var vm = Build(fileSaveService: fake);

        await vm.SaveSelectedStatementPdfCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        fake.LastSavedBytes.Should().BeNull();
    }

    [Fact]
    public async Task SaveSelectedStatementPdf_invokes_service_with_pdf_bytes_when_statement_selected()
    {
        var (vm, fake, _) = await BuildWithClosedStatementAsync();
        vm.SelectedStatement = vm.Statements.First();

        await vm.SaveSelectedStatementPdfCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        fake.LastSavedBytes.Should().NotBeNullOrEmpty();
        fake.LastSuggestedFileName.Should().StartWith("工賃明細_");
        fake.LastFileTypeName.Should().Be("PDF");
        fake.LastExtension.Should().Be(".pdf");
        vm.StatusMessage.Should().Contain("保存しました");
    }

    [Fact]
    public async Task SavePaymentListPdf_without_statements_sets_error()
    {
        var fake = new FakeFileSaveService();
        var vm = Build(fileSaveService: fake);

        await vm.SavePaymentListPdfCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        fake.LastSavedBytes.Should().BeNull();
    }

    [Fact]
    public async Task SavePaymentListPdf_invokes_service_with_pdf_bytes()
    {
        var (vm, fake, _) = await BuildWithClosedStatementAsync();

        await vm.SavePaymentListPdfCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        fake.LastSavedBytes.Should().NotBeNullOrEmpty();
        fake.LastSuggestedFileName.Should().StartWith("工賃支払一覧_");
        fake.LastSuggestedFileName.Should().EndWith(".pdf");
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
