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
        wizard.RegistrationCompleted += () => CompleteRegistration(registered, wizard.Close);
        wizard.CancellationRequested += cancelled;
        desktop.MainWindow = wizard;
        wizard.Show();
    }

    /// <summary>
    /// 登録成功時の画面切り替え順序。MainWindow を出してからウィザードを閉じる
    /// （Close を先にすると最後の Window が消えて desktop が終了しうる）。
    /// <paramref name="showMain"/> が失敗したらウィザードは閉じない
    /// — ウィンドウが1つも無い状態で職員を取り残さないため。
    ///
    /// Avalonia を組み立てずに順序を検証できるよう、副作用を引数で受ける。
    /// </summary>
    public static void CompleteRegistration(Action showMain, Action closeWizard)
    {
        ArgumentNullException.ThrowIfNull(showMain);
        ArgumentNullException.ThrowIfNull(closeWizard);

        showMain();
        closeWizard();
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
