using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class OfficeCapabilityViewModelTests
{
    private readonly InMemoryOfficeRepo _offices = new();
    private readonly InMemoryOfficeCapabilityRepo _caps = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private OfficeCapabilityViewModel NewVm() => new(
        new RegisterOfficeCapabilityUseCase(_caps, _uow, _clock),
        new ListOfficesUseCase(_offices));

    [Fact]
    public async Task InitializeAsync_loads_offices_for_selection()
    {
        var o = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _offices.Add(o);

        var vm = NewVm();
        await vm.InitializeAsync();

        vm.Offices.Should().ContainSingle(x => x.Id == o.Id);
    }

    [Fact]
    public async Task SaveCommand_registers_capability_for_selected_office()
    {
        var oid = Guid.NewGuid();
        var vm = NewVm();
        vm.SelectedOffice = new Tsumugi.Application.Dtos.OfficeDto(
            oid, "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB,
            Tsumugi.Domain.Enums.RegionGrade.None,
            Guid.NewGuid());
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);
        vm.MealProvision = true;
        vm.TransportSupport = false;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        _caps.Count.Should().Be(1);
        _caps.Last.OfficeId.Should().Be(oid);
        _caps.Last.Flags["mealProvision"].Should().BeTrue();
        _caps.Last.Flags["transportSupport"].Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_without_selected_office_sets_error()
    {
        var vm = NewVm();
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsSaved.Should().BeFalse();
        _caps.Count.Should().Be(0);
    }
}

internal sealed class InMemoryOfficeCapabilityRepo : IOfficeCapabilityRepository
{
    private readonly List<OfficeCapability> _list = [];
    public int Count => _list.Count;
    public OfficeCapability Last => _list[^1];
    public Task AddAsync(OfficeCapability c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OfficeCapability>>(_list.Where(c => c.OfficeId == officeId).ToArray());
    public Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(_list.FirstOrDefault(c => c.OfficeId == officeId && c.Period.Contains(asOf)));
}
