using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class OfficeViewModelTests
{
    private readonly InMemoryOfficeRepo _repo = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private OfficeViewModel NewVm() => new(
        new RegisterOfficeUseCase(_repo, _uow, _clock),
        new ListOfficesUseCase(_repo));

    [Fact]
    public async Task LoadAsync_populates_items()
    {
        _repo.Add(Office.Create(
            Guid.NewGuid(), "1234567890", "テスト事業所",
            ServiceCategory.TypeB, RegionGrade.None,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var vm = NewVm();
        await vm.LoadAsync();

        vm.Items.Should().ContainSingle(o => o.Name == "テスト事業所");
    }

    [Fact]
    public async Task SaveCommand_with_valid_input_registers_and_clears_error()
    {
        var vm = NewVm();
        vm.OfficeNumber = "1234567890";
        vm.Name = "テスト事業所";
        vm.Category = ServiceCategory.TypeB;
        vm.Region = RegionGrade.Grade1;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task SaveCommand_with_blank_name_sets_error_message()
    {
        var vm = NewVm();
        vm.OfficeNumber = "1234567890";
        vm.Name = "";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().Contain("事業所名");
        vm.IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_with_blank_office_number_sets_error_message()
    {
        var vm = NewVm();
        vm.OfficeNumber = "";
        vm.Name = "テスト事業所";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().Contain("事業所番号");
        vm.IsSaved.Should().BeFalse();
    }
}

internal sealed class InMemoryOfficeRepo : IOfficeRepository
{
    private readonly List<Office> _list = [];
    public void Add(Office o) => _list.Add(o);
    public Task AddAsync(Office o, CancellationToken ct) { _list.Add(o); return Task.CompletedTask; }
    public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<Office?>(_list.FirstOrDefault(o => o.Id == id));
    public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
        Task.FromResult<Office?>(_list.FirstOrDefault(o => o.OfficeNumber == officeNumber));
    public Task UpdateAsync(Office o, CancellationToken ct)
    {
        var idx = _list.FindIndex(x => x.Id == o.Id);
        if (idx >= 0) _list[idx] = o;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Office>>(_list);
}
