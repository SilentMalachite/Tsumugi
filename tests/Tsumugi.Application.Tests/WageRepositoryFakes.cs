using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Tests;

internal sealed class FakeRecipientRepoSeeded(IEnumerable<Recipient> seed) : IRecipientRepository
{
    private readonly List<Recipient> _items = seed.ToList();
    public Task AddAsync(Recipient r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<Recipient?>(_items.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Recipient>>(_items.Where(r => includeArchived || r.ArchivedAt is null).ToList());
}

internal sealed class FakeContractRepoSeeded(IEnumerable<Contract> seed) : IContractRepository
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

internal sealed class FakeDailyRecordRepoSeeded(IEnumerable<DailyRecord> seed) : IDailyRecordRepository
{
    private readonly List<DailyRecord> _items = seed.ToList();
    public Task AddAsync(DailyRecord r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
    public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<DailyRecord?>(_items.FirstOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(Guid rid, DateOnly d, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<DailyRecord>>(_items.Where(r => r.RecipientId == rid && r.ServiceDate == d).ToList());
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(Guid rid, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return Task.FromResult<IReadOnlyList<DailyRecord>>(
            _items.Where(r => r.RecipientId == rid && r.ServiceDate >= from && r.ServiceDate <= to).ToList());
    }
}

internal sealed class FakeWorkRecordRepoSeeded(IEnumerable<WorkRecord> seed) : IWorkRecordRepository
{
    private readonly List<WorkRecord> _items = seed.ToList();
    public Task AddAsync(WorkRecord r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
    public Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<WorkRecord?>(_items.FirstOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(Guid rid, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return Task.FromResult<IReadOnlyList<WorkRecord>>(
            _items.Where(r => r.RecipientId == rid && r.WorkDate >= from && r.WorkDate <= to).ToList());
    }
}

internal sealed class FakeWageFundRepoSeeded(IEnumerable<WageFund> seed) : IWageFundRepository
{
    private readonly List<WageFund> _items = seed.ToList();
    public Task AddAsync(WageFund f, CancellationToken ct) { _items.Add(f); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(Guid officeId, int y, int m, CancellationToken ct)
    {
        var ym = new YearMonth(y, m);
        return Task.FromResult<IReadOnlyList<WageFund>>(_items.Where(f => f.OfficeId == officeId && f.Month == ym).ToList());
    }
}

internal sealed class FakeWageSettingsRepoSeeded(IEnumerable<WageSettings> seed) : IWageSettingsRepository
{
    private readonly List<WageSettings> _items = seed.ToList();
    public Task AddAsync(WageSettings s, CancellationToken ct) { _items.Add(s); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid officeId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WageSettings>>(_items.Where(s => s.OfficeId == officeId).ToList());
}

internal sealed class FakeRecipientHourlyRateRepoSeeded(IEnumerable<RecipientHourlyRate> seed) : IRecipientHourlyRateRepository
{
    private readonly List<RecipientHourlyRate> _items = seed.ToList();
    public Task AddAsync(RecipientHourlyRate rate, CancellationToken ct) { _items.Add(rate); return Task.CompletedTask; }
    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(Guid officeId, Guid recipientId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RecipientHourlyRate>>(
            _items.Where(r => r.OfficeId == officeId && r.RecipientId == recipientId).ToList());
    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeAsync(Guid officeId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RecipientHourlyRate>>(
            _items.Where(r => r.OfficeId == officeId).ToList());
}

internal sealed class FakeWageAdjustmentRepoSeeded(IEnumerable<WageAdjustment> seed) : IWageAdjustmentRepository
{
    private readonly List<WageAdjustment> _items = seed.ToList();
    public Task AddAsync(WageAdjustment adjustment, CancellationToken ct) { _items.Add(adjustment); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(Guid officeId, YearMonth yearMonth, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WageAdjustment>>(
            _items.Where(a => a.OfficeId == officeId && a.YearMonth == yearMonth).ToList());
}
