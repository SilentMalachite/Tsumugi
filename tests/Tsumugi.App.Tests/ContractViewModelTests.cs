using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Contract;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class ContractViewModelTests
{
    private readonly InMemoryContractRepo _repo = new();
    private readonly InMemoryRecipientRepoForContract _recipients = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private ContractViewModel NewVm() => new(
        new RegisterContractUseCase(_repo, _uow, _clock),
        new ListContractsByRecipientUseCase(_repo),
        new ListRecipientsUseCase(_recipients));

    [Fact]
    public async Task LoadAsync_populates_items_for_recipient()
    {
        var recipientId = Guid.NewGuid();
        _repo.Add(Contract.Create(
            Guid.NewGuid(), recipientId,
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            23, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var vm = NewVm();
        vm.RecipientId = recipientId;
        await vm.LoadAsync();

        vm.Items.Should().ContainSingle(c => c.ContractedSupplyDays == 23);
    }

    [Fact]
    public async Task SaveCommand_with_valid_input_registers_and_clears_error()
    {
        var vm = NewVm();
        vm.RecipientId = Guid.NewGuid();
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);
        vm.ContractedSupplyDays = 23;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        vm.OverlapWarning.Should().BeNull();
    }

    [Fact]
    public async Task SaveCommand_with_end_before_start_sets_error_message()
    {
        var vm = NewVm();
        vm.RecipientId = Guid.NewGuid();
        vm.PeriodStart = new DateOnly(2027, 1, 1);
        vm.PeriodEnd = new DateOnly(2026, 1, 1); // End before Start

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_with_empty_recipient_sets_error_and_does_not_persist()
    {
        var vm = NewVm();
        // RecipientId は default(Guid) = Guid.Empty のまま
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);
        vm.ContractedSupplyDays = 23;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsSaved.Should().BeFalse();
        _repo.AddedCount.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_populates_recipients_for_view_lifecycle()
    {
        var r = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);

        var vm = NewVm();
        await vm.InitializeAsync();

        // View の Loaded から InitializeAsync が呼ばれる契約。
        // ここで Recipients が空のままだと ComboBox は実画面で永久に空。
        vm.Recipients.Should().ContainSingle(x => x.Id == r.Id);
    }

    [Fact]
    public async Task LoadRecipientsAsync_populates_recipients_for_selection()
    {
        var r1 = Recipient.Create(Guid.NewGuid(), "氏名一", "シメイイチ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r1);

        var vm = NewVm();
        await vm.LoadRecipientsAsync();

        vm.Recipients.Should().ContainSingle(r => r.Id == r1.Id);
    }

    [Fact]
    public void SelectedRecipient_synchronises_RecipientId()
    {
        var vm = NewVm();
        var dto = new Tsumugi.Application.Dtos.RecipientDto(
            Guid.NewGuid(), "氏名二", "シメイニ", new DateOnly(1990, 1, 1));

        vm.SelectedRecipient = dto;

        vm.RecipientId.Should().Be(dto.Id);
    }

    [Fact]
    public async Task SaveCommand_with_overlapping_period_sets_overlap_warning()
    {
        var recipientId = Guid.NewGuid();
        _repo.Add(Contract.Create(
            Guid.NewGuid(), recipientId,
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            23, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var vm = NewVm();
        vm.RecipientId = recipientId;
        vm.PeriodStart = new DateOnly(2026, 10, 1);
        vm.PeriodEnd = new DateOnly(2027, 9, 30); // overlaps with existing
        vm.ContractedSupplyDays = 20;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.IsSaved.Should().BeTrue();
        vm.OverlapWarning.Should().NotBeNullOrEmpty();
    }
}

internal sealed class InMemoryContractRepo : IContractRepository
{
    private readonly List<Contract> _list = [];
    public int AddedCount => _list.Count;
    public void Add(Contract c) => _list.Add(c);
    public Task AddAsync(Contract c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Contract>>(_list.Where(c => c.RecipientId == recipientId).ToArray());
    public Task<Contract?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult<Contract?>(_list.FirstOrDefault(c => c.RecipientId == recipientId && c.Period.Contains(asOf)));
}

internal sealed class InMemoryRecipientRepoForContract : IRecipientRepository
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
    public Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Recipient>>(_list.ToArray());
}
