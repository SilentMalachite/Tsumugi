namespace Tsumugi.App.Startup;

/// <summary>初回起動判定に応じて最初のデスクトップ画面を表示する。</summary>
public sealed class FirstRunDesktopStartupOrchestrator(
    FirstRunStartupCoordinator coordinator,
    IInitialWindowHost host)
{
    public async Task StartAsync()
    {
        try
        {
            var destination = await coordinator.DecideAsync();
            if (destination == FirstRunStartupDestination.Main)
            {
                host.ShowMain();
                return;
            }

            host.ShowWizard(host.ShowMain, host.Shutdown);
        }
        catch (Exception)
        {
            // fire-and-forget の起動処理から未監視例外を残さない。
            host.Shutdown();
        }
    }
}
