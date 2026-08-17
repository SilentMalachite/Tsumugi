using FluentAssertions;
using Tsumugi.App.Startup;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class FirstRunDesktopStartupOrchestratorTests
{
    [Fact]
    public async Task StartAsync_when_no_offices_shows_wizard_with_host_callbacks()
    {
        var host = new FakeInitialWindowHost();
        var sut = new FirstRunDesktopStartupOrchestrator(NewCoordinator(), host);

        await sut.StartAsync();

        host.ShowWizardCount.Should().Be(1);
        host.ShowMainCount.Should().Be(0);
        host.ShutdownCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_when_office_exists_shows_main()
    {
        var repo = new InMemoryOfficeRepo();
        repo.Add(Office.Create(
            Guid.NewGuid(), "1234567890", "既存事業所",
            ServiceCategory.TypeB, RegionGrade.Grade4,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var host = new FakeInitialWindowHost();
        var sut = new FirstRunDesktopStartupOrchestrator(
            new FirstRunStartupCoordinator(new ListOfficesUseCase(repo)), host);

        await sut.StartAsync();

        host.ShowWizardCount.Should().Be(0);
        host.ShowMainCount.Should().Be(1);
        host.ShutdownCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_registered_callback_shows_main()
    {
        var host = new FakeInitialWindowHost();
        var sut = new FirstRunDesktopStartupOrchestrator(NewCoordinator(), host);

        await sut.StartAsync();
        host.Registered!.Invoke();

        host.ShowMainCount.Should().Be(1);
        host.ShutdownCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_cancelled_callback_shuts_down()
    {
        var host = new FakeInitialWindowHost();
        var sut = new FirstRunDesktopStartupOrchestrator(NewCoordinator(), host);

        await sut.StartAsync();
        host.Cancelled!.Invoke();

        host.ShowMainCount.Should().Be(0);
        host.ShutdownCount.Should().Be(1);
    }

    [Fact]
    public async Task StartAsync_when_coordinator_fails_reports_the_failure_instead_of_exiting_silently()
    {
        // 無言終了だと職員には「ダブルクリックしても何も起きない」としか見えず、
        // docs/operations.md §6 の報告手順（エラー文言の安全な要約）も踏めない。
        var host = new FakeInitialWindowHost();
        var sut = NewFailingSut(host, new InvalidOperationException("list failed"));

        var act = () => sut.StartAsync();

        await act.Should().NotThrowAsync();
        host.ShowMainCount.Should().Be(0);
        host.ShowWizardCount.Should().Be(0);
        host.StartupFailureMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task StartAsync_startup_failure_summary_names_the_error_kind_without_exception_detail()
    {
        var host = new FakeInitialWindowHost();
        var sut = NewFailingSut(host, new InvalidOperationException("list failed"));

        await sut.StartAsync();

        var message = host.StartupFailureMessages.Single();
        message.Should().Contain(nameof(InvalidOperationException),
            because: "職員が報告できる手掛かりを残す（docs/operations.md §6）");
        message.Should().NotContain("list failed",
            because: "例外本文は保存先フルパス等を含みうる（CLAUDE.md ハード制約4）");
    }

    [Fact]
    public async Task StartAsync_falls_back_to_shutdown_when_the_failure_screen_cannot_be_shown()
    {
        var host = new FakeInitialWindowHost { ShowStartupFailureThrows = true };
        var sut = NewFailingSut(host, new InvalidOperationException("list failed"));

        var act = () => sut.StartAsync();

        await act.Should().NotThrowAsync();
        host.ShutdownCount.Should().Be(1);
    }

    private static FirstRunDesktopStartupOrchestrator NewFailingSut(
        IInitialWindowHost host, Exception failure)
    {
        var repo = new InMemoryOfficeRepo { BeforeListAsync = _ => throw failure };
        return new FirstRunDesktopStartupOrchestrator(
            new FirstRunStartupCoordinator(new ListOfficesUseCase(repo)), host);
    }

    [Fact]
    public void FirstRunWizardWindow_wires_commands_events_and_required_choices()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "FirstRunWizardWindow.axaml"));
        var codeBehind = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "FirstRunWizardWindow.axaml.cs"));

        xaml.Should().Contain("x:DataType=\"vm:FirstRunWizardViewModel\"");
        xaml.Should().Contain("Command=\"{Binding RegisterCommand}\"");
        xaml.Should().Contain("Command=\"{Binding CancelCommand}\"");
        xaml.Should().Contain("Gesture=\"Ctrl+Enter\"");
        xaml.Should().Contain("Gesture=\"Escape\"");
        xaml.Should().NotContain("<enums:RegionGrade>None</enums:RegionGrade>");
        codeBehind.Should().Contain("Registered");
        codeBehind.Should().Contain("Cancelled");
        codeBehind.Should().Contain("Closing");

        var host = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "Startup", "AvaloniaInitialWindowHost.cs"));
        host.IndexOf("registered();", StringComparison.Ordinal)
            .Should().BeLessThan(host.IndexOf("wizard.Close();", StringComparison.Ordinal),
                because: "登録成功時はメインウィンドウを先に設定してからウィザードを閉じる");
    }

    [Fact]
    public void AvaloniaInitialWindowHost_shutdown_requests_via_TryShutdown_not_direct_Shutdown()
    {
        var root = FindRepositoryRoot();
        var host = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "Startup", "AvaloniaInitialWindowHost.cs"));
        var app = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "App.axaml.cs"));

        host.Should().Contain("TryShutdown(",
            because: "TryShutdown は ShutdownRequested を発火し、終了時バックアップを経由できる");
        host.Should().NotContain("desktop.Shutdown(",
            because: "直接 Shutdown すると ShutdownRequested を bypass してバックアップを飛ばす");

        app.IndexOf("ShutdownRequested += OnShutdownRequested", StringComparison.Ordinal)
            .Should().BeLessThan(
                app.IndexOf("orchestrator.StartAsync()", StringComparison.Ordinal),
                because: "初回終了要求より先に終了時バックアップ handler を登録する");
        app.Should().Contain("desktop.Shutdown(",
            because: "バックアップ完了後の最終終了は再入 guard 付きで Shutdown する");
    }

    [Fact]
    public void AvaloniaInitialWindowHost_shows_each_window_before_replacing_or_closing_wizard()
    {
        var root = FindRepositoryRoot();
        var host = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "Startup", "AvaloniaInitialWindowHost.cs"));

        host.Should().Contain(
            "desktop.MainWindow = mainWindow;\n        mainWindow.Show();",
            because: "MainWindow の代入だけでは表示されないため");
        host.Should().Contain(
            "desktop.MainWindow = wizard;\n        wizard.Show();",
            because: "Wizard の代入だけでは表示されないため");
        host.IndexOf("mainWindow.Show();", StringComparison.Ordinal)
            .Should().BeLessThan(host.IndexOf("wizard.Close();", StringComparison.Ordinal),
                because: "登録成功時は MainWindow の Show 完了後にのみ Wizard を閉じる");
    }

    [Fact]
    public void FirstRunWizardWindow_cancels_unfinished_close_and_disables_interaction_before_single_shutdown_request()
    {
        var root = FindRepositoryRoot();
        var codeBehind = File.ReadAllText(Path.Combine(
            root, "src", "Tsumugi.App", "FirstRunWizardWindow.axaml.cs"));

        codeBehind.Should().Contain("e.Cancel = true;",
            because: "未完了 wizard の Close は OnLastWindowClose を発火させない");
        codeBehind.Should().Contain("IsEnabled = false;",
            because: "キャンセル確定後はバックアップ待ち中の登録操作を禁止する");
        CountOccurrences(codeBehind, "CancellationRequested?.Invoke();").Should().Be(1,
            because: "Cancel と Closing は単一の終了要求経路へ集約する");
    }

    private static FirstRunStartupCoordinator NewCoordinator() =>
        new(new ListOfficesUseCase(new InMemoryOfficeRepo()));

    private sealed class FakeInitialWindowHost : IInitialWindowHost
    {
        public int ShowMainCount { get; private set; }
        public int ShowWizardCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public Action? Registered { get; private set; }
        public Action? Cancelled { get; private set; }
        public List<string> StartupFailureMessages { get; } = [];
        public bool ShowStartupFailureThrows { get; init; }

        public void ShowMain() => ShowMainCount++;

        public void ShowWizard(Action registered, Action cancelled)
        {
            ShowWizardCount++;
            Registered = registered;
            Cancelled = cancelled;
        }

        public void ShowStartupFailure(string message)
        {
            if (ShowStartupFailureThrows)
                throw new InvalidOperationException("failure screen unavailable");

            StartupFailureMessages.Add(message);
        }

        public void Shutdown() => ShutdownCount++;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.EnumerateFiles("Tsumugi.sln").Any())
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Tsumugi.sln が祖先方向に見つからない");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
