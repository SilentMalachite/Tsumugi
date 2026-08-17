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

    [Fact]
    public async Task RegisterCommand_does_not_propagate_exception_from_Registered_callback()
    {
        // Registered は Window 差し替え（MainWindow 構築）を呼ぶ。ここで例外が漏れると
        // AsyncRelayCommand の async void 経路で UI スレッドへ再スローされ、
        // 事業所を永続化した直後にプロセスが落ちる。
        var vm = NewVm();
        FillValid(vm);
        vm.Registered += () => throw new InvalidOperationException("window switch failed");

        var act = () => vm.RegisterCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
        vm.IsSaving.Should().BeFalse();
        (await _repo.ListAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterCommand_reports_window_switch_failure_without_exposing_exception_detail()
    {
        var vm = NewVm();
        FillValid(vm);
        vm.Registered += () => throw new InvalidOperationException("window switch failed");

        await vm.RegisterCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrWhiteSpace();
        vm.SaveErrorMessage.Should().NotContain("window switch failed",
            because: "例外本文には保存先パス等が混ざりうる（CLAUDE.md ハード制約4）");
        vm.SaveErrorMessage.Should().Contain("再起動",
            because: "登録は完了しているので、職員がとるべき行動は再起動だと伝える");
    }

    [Fact]
    public async Task RegisterCommand_is_disabled_after_registration_so_a_failed_switch_cannot_retry()
    {
        // 登録済みで再実行すると「既に登録されています」になり、職員には原因が読めない。
        var vm = NewVm();
        FillValid(vm);
        vm.Registered += () => throw new InvalidOperationException("window switch failed");

        await vm.RegisterCommand.ExecuteAsync(null);

        vm.RegisterCommand.CanExecute(null).Should().BeFalse();
        (await _repo.ListAsync(default)).Should().ContainSingle();
    }

    [Fact]
    public async Task CancelCommand_stays_available_after_a_failed_window_switch()
    {
        // 唯一の脱出経路。終了後の再起動では Office が 1 件あるので MainWindow へ進む。
        var vm = NewVm();
        FillValid(vm);
        vm.Registered += () => throw new InvalidOperationException("window switch failed");
        var cancelled = 0;
        vm.Cancelled += () => cancelled++;

        await vm.RegisterCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null);

        cancelled.Should().Be(1);
    }

    [Fact]
    public async Task CancelCommand_while_saving_does_not_invoke_Cancelled()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repo.BeforeAddAsync = async _ => await gate.Task;
        var vm = NewVm();
        FillValid(vm);
        var cancelled = 0;
        vm.Cancelled += () => cancelled++;

        var first = vm.RegisterCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => vm.IsSaving);

        vm.CancelCommand.Execute(null);

        cancelled.Should().Be(0);
        vm.CancelCommand.CanExecute(null).Should().BeFalse();

        gate.SetResult();
        await first;

        cancelled.Should().Be(0);
        vm.CancelCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterCommand_reentry_does_not_cancel_in_flight_registration()
    {
        var enteredAdd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedTokens = new List<CancellationToken>();
        var cancelledAfterReentry = false;
        _repo.BeforeAddAsync = async ct =>
        {
            observedTokens.Add(ct);
            enteredAdd.SetResult();
            await gate.Task;
            // 再入後も先行登録の CT がキャンセルされていないこと。
            cancelledAfterReentry = ct.IsCancellationRequested;
        };
        var vm = NewVm();
        FillValid(vm);
        var registered = 0;
        vm.Registered += () => registered++;

        var first = vm.RegisterCommand.ExecuteAsync(null);
        await enteredAdd.Task;

        var second = vm.RegisterCommand.ExecuteAsync(null);
        await second;

        gate.SetResult();
        await first;

        cancelledAfterReentry.Should().BeFalse();
        observedTokens.Should().ContainSingle();
        observedTokens[0].CanBeCanceled.Should().BeFalse();
        registered.Should().Be(1);
        (await _repo.ListAsync(default)).Should().ContainSingle();
        vm.SaveErrorMessage.Should().BeNull();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        condition().Should().BeTrue("条件がタイムアウト前に成立すること");
    }
}
