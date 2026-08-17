using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Startup;

/// <summary>Avalonia のデスクトップライフタイムへ最初のウィンドウを表示する。</summary>
public sealed class AvaloniaInitialWindowHost(
    IClassicDesktopStyleApplicationLifetime desktop,
    IServiceProvider services) : IInitialWindowHost
{
    public void ShowMain()
    {
        var mainViewModel = services.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow(mainViewModel);
        desktop.MainWindow = mainWindow;
        mainWindow.Show();
    }

    public void ShowWizard(Action registered, Action cancelled)
    {
        ArgumentNullException.ThrowIfNull(registered);
        ArgumentNullException.ThrowIfNull(cancelled);

        var wizardViewModel = services.GetRequiredService<FirstRunWizardViewModel>();
        var wizard = new FirstRunWizardWindow(wizardViewModel);
        wizard.RegistrationCompleted += () =>
        {
            // MainWindow を差し替えてから閉じる。Close を先にすると desktop が終了しうる。
            registered();
            wizard.Close();
        };
        wizard.CancellationRequested += cancelled;
        desktop.MainWindow = wizard;
        wizard.Show();
    }

    public void ShowStartupFailure(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        // 代入だけでは表示されない。閉じると OnLastWindowClose から
        // 終了時バックアップを経て終了する。
        var window = new StartupFailureWindow(message);
        desktop.MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// <see cref="IClassicDesktopStyleApplicationLifetime.TryShutdown"/> で
    /// <c>ShutdownRequested</c> を発火し、App の終了時バックアップへ通す。
    /// 直接 <c>Shutdown()</c> すると handler を bypass する。
    /// </summary>
    public void Shutdown() => desktop.TryShutdown();
}
