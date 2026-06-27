using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class DailyRecordViewModelTests
{
    private readonly FakeDailyRecordRepo _repo = new();
    private readonly InMemoryRecipientRepoForDaily _recipients = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private DailyRecordViewModel NewVm() => new(
        new RecordDailyRecordUseCase(_repo, _uow, _clock),
        new CorrectDailyRecordUseCase(_repo, _uow, _clock),
        new CancelDailyRecordUseCase(_repo, _uow, _clock),
        new QueryMonthDailyRecordsUseCase(_repo),
        new ListRecipientsUseCase(_recipients));

    [Fact]
    public async Task LoadAsync_with_no_recipient_or_month_does_not_throw_and_keeps_cells_empty()
    {
        // F5 押下時に Year=Month=0 / RecipientId=Empty で DateTime.DaysInMonth(0,0) に落ちないこと。
        var vm = NewVm();
        await vm.LoadAsync();
        vm.Cells.Should().BeEmpty();
    }

    [Fact]
    public void LoadCommand_is_disabled_when_recipient_or_month_unset()
    {
        var vm = NewVm();
        vm.LoadCommand.CanExecute(null).Should().BeFalse();

        vm.SetRecipient(Guid.NewGuid());
        vm.LoadCommand.CanExecute(null).Should().BeFalse();  // 年月未指定

        vm.SetMonth(2026, 6);
        vm.LoadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_populates_recipients_for_view_lifecycle()
    {
        var r = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);

        var vm = NewVm();
        await vm.InitializeAsync();

        vm.Recipients.Should().ContainSingle(x => x.Id == r.Id);
    }

    [Fact]
    public async Task LoadRecipientsAsync_populates_recipients_for_selection()
    {
        var r = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);

        var vm = NewVm();
        await vm.LoadRecipientsAsync();

        vm.Recipients.Should().ContainSingle(x => x.Id == r.Id);
    }

    [Fact]
    public void SelectedRecipient_synchronises_RecipientId_and_enables_load()
    {
        var vm = NewVm();
        vm.SetMonth(2026, 6);
        var dto = new Tsumugi.Application.Dtos.RecipientDto(
            Guid.NewGuid(), "氏名", "シメイ", new DateOnly(1990, 1, 1), Guid.NewGuid(), IsArchived: false);

        vm.SelectedRecipient = dto;

        vm.RecipientId.Should().Be(dto.Id);
        vm.LoadCommand.CanExecute(null).Should().BeTrue();
    }

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
    public async Task SetAttendance_routes_to_record_when_no_effective()
    {
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        var cell = vm.Cells[0];
        cell.EffectiveId.Should().BeNull();

        await cell.SetAttendanceCommand.ExecuteAsync(Attendance.Present);

        _repo.Added.Should().HaveCount(1);
        _repo.Added[0].Kind.Should().Be(RecordKind.New);
    }

    [Fact]
    public async Task SetAttendance_routes_to_correct_when_effective_exists()
    {
        // R2-H2: 既存記録の出欠変更時、UI から訂正経路（CorrectCommand）に届かないと
        // RecordDailyRecordUseCase の同一日 New 重複拒否で例外になる。
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        await vm.Cells[0].SetAttendanceCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();  // EffectiveId を反映

        await vm.Cells[0].SetAttendanceCommand.ExecuteAsync(Attendance.Absent);

        _repo.Added.Should().HaveCount(2);
        _repo.Added[1].Kind.Should().Be(RecordKind.Correct);
        _repo.Added[1].Attendance.Should().Be(Attendance.Absent);
        _repo.Added[1].OriginId.Should().Be(_repo.Added[0].Id);
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

internal sealed class InMemoryRecipientRepoForDaily : IRecipientRepository
{
    private readonly List<Recipient> _list = [];
    public void Add(Recipient r) => _list.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { _list.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_list.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct)
    {
        var idx = _list.FindIndex(x => x.Id == r.Id);
        if (idx >= 0) _list[idx] = r;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        IEnumerable<Recipient> source = includeArchived ? _list : _list.Where(r => !r.IsArchived);
        return Task.FromResult<IReadOnlyList<Recipient>>(source.ToArray());
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
