using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class RecipientHourlyRateViewModelTests
{
    private readonly InMemoryOfficeRepo _offices = new();
    private readonly InMemoryRecipientRepo _recipients = new();
    private readonly SpyHourlyRateRepo _rates = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private RecipientHourlyRateViewModel NewVm() => new(
        new SetRecipientHourlyRateUseCase(_rates, _uow, new NoopAuditTrail(), _clock),
        new QueryRecipientHourlyRateUseCase(_rates),
        new ListOfficesUseCase(_offices),
        new ListRecipientsUseCase(_recipients));

    private Office MakeOffice(string name = "テスト事業所")
    {
        var o = Office.Create(Guid.NewGuid(), "1234567890", name,
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _offices.Add(o);
        return o;
    }

    private Recipient MakeRecipient(string name = "山田太郎")
    {
        var r = Recipient.Create(Guid.NewGuid(), name, "ヤマダタロウ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);
        return r;
    }

    [Fact]
    public async Task Load_populates_offices_and_recipients()
    {
        var office = MakeOffice();
        var recipient = MakeRecipient();

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.Offices.Should().ContainSingle(o => o.Id == office.Id);
        vm.Recipients.Should().ContainSingle(r => r.Id == recipient.Id);
    }

    [Fact]
    public async Task Save_creates_new_rate_and_refreshes()
    {
        var office = MakeOffice();
        var recipient = MakeRecipient();

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedRecipient = vm.Recipients.Single();
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = null;
        vm.HourlyYen = 1200;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        _rates.Added.Should().ContainSingle();
        _rates.Added[0].OfficeId.Should().Be(office.Id);
        _rates.Added[0].RecipientId.Should().Be(recipient.Id);
        _rates.Added[0].HourlyYen.Should().Be(1200);
        _rates.Added[0].Period.Start.Should().Be(new DateOnly(2026, 4, 1));
        _rates.Added[0].Period.End.Should().BeNull();
        vm.Rates.Should().ContainSingle();
    }

    [Fact]
    public async Task Save_with_period_end_creates_bounded_rate()
    {
        MakeOffice();
        MakeRecipient();

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedRecipient = vm.Recipients.Single();
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2026, 9, 30);
        vm.HourlyYen = 950;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().BeNull();
        _rates.Added.Should().ContainSingle();
        _rates.Added[0].Period.End.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Save_without_office_or_recipient_is_disabled()
    {
        var vm = NewVm();

        // 事業所も利用者も未選択 → 保存不可
        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Save_with_only_office_selected_is_disabled()
    {
        var vm = NewVm();
        vm.SelectedOffice = new OfficeDto(Guid.NewGuid(), "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());

        vm.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Save_with_both_selected_is_enabled()
    {
        var vm = NewVm();
        vm.SelectedOffice = new OfficeDto(Guid.NewGuid(), "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());
        vm.SelectedRecipient = TestRecipients.Make(Guid.NewGuid());

        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task When_both_office_and_recipient_selected_rates_auto_load()
    {
        var office = MakeOffice();
        var recipient = MakeRecipient();

        // 事前にリポジトリへ既存レートを注入
        var period = new DateRange(new DateOnly(2025, 4, 1), null);
        var preexisting = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), office.Id, recipient.Id, period, 900, "u", DateTimeOffset.UnixEpoch);
        _rates.Added.Add(preexisting);

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);

        // 事業所→利用者の順に選択（fire-and-forget が走る）
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedRecipient = vm.Recipients.Single();

        // fire-and-forget タスクの完了を deterministic に待つ（Task.Delay はフレーキー）
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (vm.RefreshRatesCommand.IsRunning && DateTime.UtcNow < timeout)
            await Task.Yield();

        vm.Rates.Should().ContainSingle(r => r.HourlyYen == 900);
    }

    [Fact]
    public async Task RefreshRates_populates_rate_list_for_selection()
    {
        var office = MakeOffice();
        var recipient = MakeRecipient();

        // 事前に保存済みのレートをリポジトリに直接注入
        var period = new DateRange(new DateOnly(2025, 4, 1), null);
        var preexisting = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), office.Id, recipient.Id, period, 1100, "u", DateTimeOffset.UnixEpoch);
        _rates.Added.Add(preexisting);

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedRecipient = vm.Recipients.Single();

        await vm.RefreshRatesCommand.ExecuteAsync(null);

        vm.Rates.Should().ContainSingle(r => r.HourlyYen == 1100);
    }
}

internal sealed class SpyHourlyRateRepo : IRecipientHourlyRateRepository
{
    public List<RecipientHourlyRate> Added { get; } = new();

    public Task AddAsync(RecipientHourlyRate rate, CancellationToken ct)
    {
        Added.Add(rate);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(
        Guid officeId, Guid recipientId, CancellationToken ct)
    {
        IReadOnlyList<RecipientHourlyRate> result =
            Added.Where(r => r.OfficeId == officeId && r.RecipientId == recipientId).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeAsync(
        Guid officeId, CancellationToken ct)
    {
        IReadOnlyList<RecipientHourlyRate> result =
            Added.Where(r => r.OfficeId == officeId).ToList();
        return Task.FromResult(result);
    }
}
