using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.WorkRecord;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class WorkRecordViewModelTests
{
    private readonly FakeWorkRecordRepo _repo = new();
    private readonly InMemoryRecipientRepoForWork _recipients = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private WorkRecordViewModel NewVm() => new(
        new RecordWorkUseCase(_repo, _uow, _clock),
        new CorrectWorkUseCase(_repo, _uow, _clock),
        new CancelWorkUseCase(_repo, _uow, _clock),
        new QueryMonthWorkUseCase(_repo),
        new ListRecipientsUseCase(_recipients));

    [Fact]
    public async Task LoadAsync_with_no_recipient_or_month_does_not_throw_and_keeps_cells_empty()
    {
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
        vm.LoadCommand.CanExecute(null).Should().BeFalse();
        vm.SetMonth(2026, 7);
        vm.LoadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_creates_one_cell_per_day_of_month()
    {
        var rid = Guid.NewGuid();
        _recipients.Add(Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var vm = NewVm();
        await vm.InitializeAsync();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 7);
        await vm.LoadAsync();
        vm.Cells.Should().HaveCount(31);
        vm.Cells[0].Date.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task SaveWorkedMinutes_on_empty_cell_records_new_entry()
    {
        var rid = Guid.NewGuid();
        _recipients.Add(Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var vm = NewVm();
        await vm.InitializeAsync();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 7);
        await vm.LoadAsync();

        var cell = vm.Cells[0];
        await cell.SaveWorkedMinutesCommand.ExecuteAsync(240);

        var reloaded = vm.Cells[0];
        reloaded.EffectiveId.Should().NotBeNull();
        reloaded.WorkedMinutes.Should().Be(240);
    }

    [Fact]
    public async Task SaveWorkedMinutes_on_existing_cell_corrects_the_entry()
    {
        var rid = Guid.NewGuid();
        _recipients.Add(Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var vm = NewVm();
        await vm.InitializeAsync();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 7);
        await vm.LoadAsync();

        await vm.Cells[0].SaveWorkedMinutesCommand.ExecuteAsync(240);
        await vm.Cells[0].SaveWorkedMinutesCommand.ExecuteAsync(200);

        vm.Cells[0].WorkedMinutes.Should().Be(200);
        _repo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CancelCommand_makes_effective_null()
    {
        var rid = Guid.NewGuid();
        _recipients.Add(Recipient.Create(rid, "氏名", "シメイ", new DateOnly(1990, 1, 1),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var vm = NewVm();
        await vm.InitializeAsync();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 7);
        await vm.LoadAsync();

        await vm.Cells[0].SaveWorkedMinutesCommand.ExecuteAsync(240);
        await vm.Cells[0].CancelCommand.ExecuteAsync(null);

        vm.Cells[0].EffectiveId.Should().BeNull();
        vm.Cells[0].WorkedMinutes.Should().BeNull();
    }
}

internal sealed class FakeWorkRecordRepo : IWorkRecordRepository
{
    public List<WorkRecord> Items { get; } = new();
    public Task AddAsync(WorkRecord r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
    public Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<WorkRecord?>(Items.FirstOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        IReadOnlyList<WorkRecord> r = Items
            .Where(x => x.RecipientId == recipientId && x.WorkDate >= from && x.WorkDate <= to)
            .ToList();
        return Task.FromResult(r);
    }
}

internal sealed class InMemoryRecipientRepoForWork : IRecipientRepository
{
    private readonly List<Recipient> _items = new();
    public void Add(Recipient r) => _items.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { _items.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<Recipient?>(_items.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Recipient>>(
            _items.Where(r => includeArchived || r.ArchivedAt is null).ToList());
}
