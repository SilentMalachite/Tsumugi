using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class FirstRunWizardViewModelTests
{
    private readonly InMemoryOfficeRepo _repo = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private FirstRunWizardViewModel NewVm() => new(
        new RegisterFirstRunUseCase(
            new RegisterOfficeUseCase(_repo, _uow, _clock)));

    private static void FillValid(FirstRunWizardViewModel vm)
    {
        vm.OfficeNumber = "1234567890";
        vm.Name = "つむぎ作業所";
        vm.Category = ServiceCategory.TypeB;
        vm.Region = RegionGrade.Grade4;
        vm.PostalCode = "100-0001";
        vm.Address = "東京都千代田区1-1";
        vm.PhoneNumber = "03-1234-5678";
        vm.RepresentativeTitleAndName = "管理者 山田太郎";
    }

    [Fact]
    public async Task RegisterCommand_with_valid_input_persists_and_invokes_Registered()
    {
        var vm = NewVm();
        FillValid(vm);
        var registered = 0;
        vm.Registered += () => registered++;

        await vm.RegisterCommand.ExecuteAsync(null);

        registered.Should().Be(1);
        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaving.Should().BeFalse();
        var stored = (await _repo.ListAsync(default)).Single();
        stored.OfficeNumber.Should().Be("1234567890");
        stored.Name.Should().Be("つむぎ作業所");
        stored.RepresentativeTitleAndName.Should().Be("管理者 山田太郎");
        stored.CreatedBy.Should().Be(Environment.UserName);
    }

    [Fact]
    public async Task RegisterCommand_with_region_None_sets_error_and_does_not_register()
    {
        var vm = NewVm();
        FillValid(vm);
        vm.Region = RegionGrade.None;
        var registered = 0;
        vm.Registered += () => registered++;

        await vm.RegisterCommand.ExecuteAsync(null);

        registered.Should().Be(0);
        vm.SaveErrorMessage.Should().Contain("地域区分");
        (await _repo.ListAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterCommand_with_blank_required_sets_error_and_does_not_register()
    {
        var vm = NewVm();
        FillValid(vm);
        vm.Name = "";
        var registered = 0;
        vm.Registered += () => registered++;

        await vm.RegisterCommand.ExecuteAsync(null);

        registered.Should().Be(0);
        vm.SaveErrorMessage.Should().Contain("事業所名");
        (await _repo.ListAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterCommand_with_duplicate_office_number_sets_error_and_does_not_register()
    {
        _repo.Add(Office.Create(
            Guid.NewGuid(), "1234567890", "既存",
            ServiceCategory.TypeB, RegionGrade.Grade4,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var vm = NewVm();
        FillValid(vm);
        var registered = 0;
        vm.Registered += () => registered++;

        await vm.RegisterCommand.ExecuteAsync(null);

        registered.Should().Be(0);
        vm.SaveErrorMessage.Should().Contain("既に登録");
        (await _repo.ListAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task CancelCommand_does_not_persist_and_invokes_Cancelled()
    {
        var vm = NewVm();
        FillValid(vm);
        var cancelled = 0;
        vm.Cancelled += () => cancelled++;

        vm.CancelCommand.Execute(null);

        cancelled.Should().Be(1);
        (await _repo.ListAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterCommand_converts_blank_optional_fields_to_null()
    {
        var vm = NewVm();
        FillValid(vm);
        vm.PostalCode = "   ";
        vm.Address = "";
        vm.PhoneNumber = " \t ";
        vm.RepresentativeTitleAndName = "  ";

        await vm.RegisterCommand.ExecuteAsync(null);

        // NullIfEmpty 後は null として渡るため、空白 optional の validation には当たらない。
        vm.SaveErrorMessage.Should().BeNull();
        var stored = (await _repo.ListAsync(default)).Single();
        stored.PostalCode.Should().BeNull();
        stored.Address.Should().BeNull();
        stored.PhoneNumber.Should().BeNull();
        stored.RepresentativeTitleAndName.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCommand_ignores_reentrant_call_while_saving()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repo.BeforeAddAsync = async _ => await gate.Task;
        var vm = NewVm();
        FillValid(vm);
        var registered = 0;
        vm.Registered += () => registered++;

        var first = vm.RegisterCommand.ExecuteAsync(null);
        // IsSaving が true になるまで待つ（最初の await 前にフラグを立てる実装を前提）。
        await WaitUntilAsync(() => vm.IsSaving);

        var second = vm.RegisterCommand.ExecuteAsync(null);
        await second; // 二重実行は即座に何もしないで終わる

        gate.SetResult();
        await first;

        registered.Should().Be(1);
        (await _repo.ListAsync(default)).Should().ContainSingle();
        vm.IsSaving.Should().BeFalse();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        condition().Should().BeTrue("条件がタイムアウト前に成立すること");
    }
}
