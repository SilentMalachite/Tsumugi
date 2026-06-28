using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class WageFundSettingsViewModelTests
{
    private readonly InMemoryFundRepo _funds = new();
    private readonly InMemorySettingsRepo _settings = new();
    private readonly InMemoryOfficeRepo _offices = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private WageFundSettingsViewModel NewVm() => new(
        new SetWageFundUseCase(_funds, _uow, _clock),
        new ConfigureWageSettingsUseCase(_settings, _uow, _clock),
        new ListOfficesUseCase(_offices));

    [Fact]
    public async Task SaveFundAsync_persists_with_valid_input()
    {
        var vm = NewVm();
        vm.OfficeId = Guid.NewGuid();
        vm.Year = 2026;
        vm.Month = 7;
        vm.TotalYen = 350_000;

        await vm.SaveFundCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        _funds.Items.Should().ContainSingle();
        _funds.Items[0].TotalYen.Should().Be(350_000);
    }

    [Fact]
    public async Task SaveFundAsync_with_invalid_office_id_sets_error_message()
    {
        var vm = NewVm();
        vm.OfficeId = Guid.Empty;
        vm.Year = 2026; vm.Month = 7; vm.TotalYen = 100_000;

        await vm.SaveFundCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        _funds.Items.Should().BeEmpty();
    }

    [Fact]
    public void TotalYen_change_updates_formatted_display()
    {
        var vm = NewVm();
        vm.TotalYen = 12345;
        vm.FormattedTotalYen.Should().Be("12,345 円");
    }

    [Fact]
    public async Task SaveSettingsAsync_with_fixed_method_missing_daily_yen_sets_error()
    {
        var vm = NewVm();
        vm.OfficeId = Guid.NewGuid();
        vm.Method = WageMethod.Fixed;
        vm.FixedDailyYen = null;

        await vm.SaveSettingsCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("FixedDailyYen");
        _settings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveSettingsAsync_with_valid_input_persists()
    {
        var vm = NewVm();
        vm.OfficeId = Guid.NewGuid();
        vm.Method = WageMethod.Hourly;

        await vm.SaveSettingsCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        _settings.Items.Should().ContainSingle();
    }

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
    public void Setting_SelectedOffice_updates_OfficeId()
    {
        var vm = NewVm();
        var oid = Guid.NewGuid();
        var dto = new OfficeDto(
            oid, "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB,
            Tsumugi.Domain.Enums.RegionGrade.None,
            Guid.NewGuid());

        vm.SelectedOffice = dto;

        vm.OfficeId.Should().Be(oid);

        vm.SelectedOffice = null;
        vm.OfficeId.Should().Be(Guid.Empty);
    }
}

internal sealed class InMemoryFundRepo : IWageFundRepository
{
    public List<WageFund> Items { get; } = new();
    public Task AddAsync(WageFund f, CancellationToken ct) { Items.Add(f); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        var ym = new YearMonth(year, month);
        IReadOnlyList<WageFund> r = Items.Where(x => x.OfficeId == officeId && x.Month == ym).ToList();
        return Task.FromResult(r);
    }
}

internal sealed class InMemorySettingsRepo : IWageSettingsRepository
{
    public List<WageSettings> Items { get; } = new();
    public Task AddAsync(WageSettings s, CancellationToken ct) { Items.Add(s); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid officeId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WageSettings>>(Items.Where(s => s.OfficeId == officeId).ToList());
}
