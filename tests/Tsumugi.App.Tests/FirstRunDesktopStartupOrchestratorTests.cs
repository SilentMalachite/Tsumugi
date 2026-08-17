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
    public async Task StartAsync_when_coordinator_fails_shuts_down_once_without_leaking_exception()
    {
        var repo = new InMemoryOfficeRepo
        {
            BeforeListAsync = _ => throw new InvalidOperationException("list failed"),
        };
        var host = new FakeInitialWindowHost();
        var sut = new FirstRunDesktopStartupOrchestrator(
            new FirstRunStartupCoordinator(new ListOfficesUseCase(repo)), host);

        var act = () => sut.StartAsync();

        await act.Should().NotThrowAsync();
        host.ShowMainCount.Should().Be(0);
        host.ShowWizardCount.Should().Be(0);
        host.ShutdownCount.Should().Be(1);
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

    private static FirstRunStartupCoordinator NewCoordinator() =>
        new(new ListOfficesUseCase(new InMemoryOfficeRepo()));

    private sealed class FakeInitialWindowHost : IInitialWindowHost
    {
        public int ShowMainCount { get; private set; }
        public int ShowWizardCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public Action? Registered { get; private set; }
        public Action? Cancelled { get; private set; }

        public void ShowMain() => ShowMainCount++;

        public void ShowWizard(Action registered, Action cancelled)
        {
            ShowWizardCount++;
            Registered = registered;
            Cancelled = cancelled;
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
}
