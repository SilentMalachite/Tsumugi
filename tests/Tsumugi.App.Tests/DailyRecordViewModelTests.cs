using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class DailyRecordViewModelTests
{
    private readonly FakeDailyRecordRepo _repo = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private DailyRecordViewModel NewVm() => new(
        new RecordDailyRecordUseCase(_repo, _uow, _clock),
        new CorrectDailyRecordUseCase(_repo, _uow, _clock),
        new CancelDailyRecordUseCase(_repo, _uow, _clock),
        new QueryMonthDailyRecordsUseCase(_repo));

    [Fact]
    public async Task LoadAsync_creates_cell_per_day_of_month()
    {
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();
        vm.Cells.Should().HaveCount(30);
    }

    [Fact]
    public async Task Record_then_query_shows_effective_attendance()
    {
        var vm = NewVm();
        var rid = Guid.NewGuid();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        var cell = vm.Cells[0];  // 6/1
        await cell.RecordCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();
        vm.Cells[0].EffectiveAttendance.Should().Be(Attendance.Present);
    }

    [Fact]
    public async Task Cancel_makes_effective_attendance_null_no_destructive_update()
    {
        var vm = NewVm();
        var rid = Guid.NewGuid();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        await vm.Cells[0].RecordCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();
        await vm.Cells[0].CancelCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        vm.Cells[0].EffectiveAttendance.Should().BeNull();
        _repo.Added.Count.Should().Be(2);  // 元レコードは残り、追記で取消行が追加
    }
}

internal sealed class FakeDailyRecordRepo : IDailyRecordRepository
{
    public List<DailyRecord> Added { get; } = new();
    public Task AddAsync(DailyRecord r, CancellationToken ct) { Added.Add(r); return Task.CompletedTask; }
    public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Added.SingleOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(Guid rid, DateOnly d, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyRecord>>(
            Added.Where(r => r.RecipientId == rid && r.ServiceDate == d).ToArray());
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(Guid rid, int y, int m, CancellationToken ct)
    {
        var from = new DateOnly(y, m, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return Task.FromResult<IReadOnlyList<DailyRecord>>(
            Added.Where(r => r.RecipientId == rid && r.ServiceDate >= from && r.ServiceDate <= to).ToArray());
    }
}
