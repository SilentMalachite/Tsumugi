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

public sealed class WageAdjustmentViewModelTests
{
    private readonly InMemoryOfficeRepo _officeRepo = new();
    private readonly InMemoryRecipientRepo _recipientRepo = new();
    private readonly SpyWageAdjustmentRepo _adjRepo = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

    private WageAdjustmentViewModel NewVm() => new(
        new RecordWageAdjustmentUseCase(_adjRepo, _uow, new NoopAuditTrail(), _clock),
        new QueryWageAdjustmentUseCase(_adjRepo),
        new ListOfficesUseCase(_officeRepo),
        new ListRecipientsUseCase(_recipientRepo));

    private Office MakeOffice(string name = "テスト事業所")
    {
        var o = Office.Create(Guid.NewGuid(), "1234567890", name,
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _officeRepo.Add(o);
        return o;
    }

    private Recipient MakeRecipient(string name = "山田太郎")
    {
        var r = Recipient.Create(Guid.NewGuid(), name, "ヤマダタロウ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipientRepo.Add(r);
        return r;
    }

    private WageAdjustment MakeAdjustment(
        Guid officeId, Guid recipientId, YearMonth ym, int amountYen,
        DateTimeOffset? createdAt = null)
    {
        var adj = WageAdjustment.NewRecord(
            Guid.NewGuid(), officeId, recipientId, ym,
            WageAdjustmentType.SpecialAllowance, amountYen, note: null,
            "u", createdAt ?? _clock.GetUtcNow());
        _adjRepo.PreSeeded.Add(adj);
        return adj;
    }

    // ----- テスト -----

    [Fact]
    public async Task Load_populates_offices_and_recipients()
    {
        var office = MakeOffice();
        var r1 = MakeRecipient("利用者A");
        var r2 = MakeRecipient("利用者B");

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.Offices.Should().ContainSingle(o => o.Id == office.Id);
        vm.Recipients.Should().HaveCount(2)
            .And.Contain(r => r.Id == r1.Id)
            .And.Contain(r => r.Id == r2.Id);
    }

    [Fact]
    public async Task RefreshMatrix_when_office_and_ym_selected_populates_rows()
    {
        var office = MakeOffice();
        var r1 = MakeRecipient("利用者A");
        var r2 = MakeRecipient("利用者B");
        var ym = new YearMonth(2026, 5);

        // r1 に 5000 円の特別手当を事前登録
        MakeAdjustment(office.Id, r1.Id, ym, 5000);
        // r2 は登録なし → 0 円

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();

        // SelectedYearMonthInt を設定すると auto-refresh が fire-and-forget で走る
        vm.SelectedYearMonthInt = 202605;
        await vm.RefreshMatrixCommand.ExecuteAsync(null); // 確定的な完了を待つ

        vm.Rows.Should().HaveCount(2);
        vm.Rows.Single(row => row.Recipient.Id == r1.Id).SpecialAllowanceYen.Should().Be(5000);
        vm.Rows.Single(row => row.Recipient.Id == r2.Id).SpecialAllowanceYen.Should().Be(0);
    }

    [Fact]
    public async Task RefreshMatrix_rows_are_not_dirty_after_load()
    {
        var office = MakeOffice();
        MakeRecipient("利用者A");
        var ym = new YearMonth(2026, 5);
        MakeAdjustment(office.Id, _recipientRepo.Added[0].Id, ym, 3000);

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedYearMonthInt = 202605;
        await vm.RefreshMatrixCommand.ExecuteAsync(null);

        vm.Rows.Should().AllSatisfy(row => row.IsDirty.Should().BeFalse());
    }

    [Fact]
    public async Task SaveAll_persists_only_dirty_rows()
    {
        var office = MakeOffice();
        var r1 = MakeRecipient("利用者A");
        var r2 = MakeRecipient("利用者B");
        var ym = new YearMonth(2026, 5);
        MakeAdjustment(office.Id, r1.Id, ym, 5000);

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedYearMonthInt = 202605;
        await vm.RefreshMatrixCommand.ExecuteAsync(null);

        // r1 の行を変更（ダーティにする）
        var rowR1 = vm.Rows.Single(row => row.Recipient.Id == r1.Id);
        rowR1.SpecialAllowanceYen = 7000; // IsDirty = true
        // r2 は変更なし

        await vm.SaveAllCommand.ExecuteAsync(null);

        // 変更された r1 だけが保存される
        _adjRepo.Added.Should().ContainSingle(a => a.RecipientId == r1.Id && a.AmountYen == 7000);
        // r2 は保存されない
        _adjRepo.Added.Should().NotContain(a => a.RecipientId == r2.Id);
    }

    [Fact]
    public async Task SaveAll_no_dirty_rows_is_noop()
    {
        var office = MakeOffice();
        MakeRecipient("利用者A");
        var ym = new YearMonth(2026, 5);

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedYearMonthInt = 202605;
        await vm.RefreshMatrixCommand.ExecuteAsync(null);

        // 何も変更せずに SaveAll
        await vm.SaveAllCommand.ExecuteAsync(null);

        _adjRepo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAll_resets_dirty_flags()
    {
        var office = MakeOffice();
        MakeRecipient("利用者A");
        var ym = new YearMonth(2026, 5);

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedYearMonthInt = 202605;
        await vm.RefreshMatrixCommand.ExecuteAsync(null);

        vm.Rows[0].SpecialAllowanceYen = 1000; // dirty
        await vm.SaveAllCommand.ExecuteAsync(null);

        vm.Rows.Should().AllSatisfy(row => row.IsDirty.Should().BeFalse());
    }

    [Theory]
    [InlineData(202600)] // month 0
    [InlineData(202613)] // month 13
    [InlineData(202699)] // month 99
    public async Task Invalid_month_component_does_not_trigger_refresh(int yyyymm)
    {
        var office = MakeOffice();
        MakeRecipient("利用者A");

        var vm = NewVm();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedOffice = vm.Offices.Single();

        // 無効な年月を設定 → auto-refresh は起動しない
        vm.SelectedYearMonthInt = yyyymm;
        // Rows は空のまま（RefreshMatrix が起動されていない）
        vm.Rows.Should().BeEmpty();
        vm.SaveAllCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveAll_without_office_is_disabled()
    {
        var vm = NewVm();
        vm.SaveAllCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveAll_with_invalid_ym_is_disabled()
    {
        var vm = NewVm();
        _ = MakeOffice();
        vm.SelectedOffice = new OfficeDto(Guid.NewGuid(), "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());
        vm.SelectedYearMonthInt = 0; // 無効

        vm.SaveAllCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SaveAll_with_office_and_valid_ym_is_enabled()
    {
        var vm = NewVm();
        vm.SelectedOffice = new OfficeDto(Guid.NewGuid(), "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, Guid.NewGuid());
        vm.SelectedYearMonthInt = 202605;

        vm.SaveAllCommand.CanExecute(null).Should().BeTrue();
    }
}

// ----- テスト用のフェイク -----

internal sealed class SpyWageAdjustmentRepo : IWageAdjustmentRepository
{
    public List<WageAdjustment> Added { get; } = new();
    public List<WageAdjustment> PreSeeded { get; } = new();

    public Task AddAsync(WageAdjustment adjustment, CancellationToken ct)
    {
        Added.Add(adjustment);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct)
    {
        IReadOnlyList<WageAdjustment> result =
            PreSeeded.Where(a => a.OfficeId == officeId && a.YearMonth == yearMonth).ToList();
        return Task.FromResult(result);
    }
}
