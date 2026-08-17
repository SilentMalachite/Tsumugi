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
        catch (Exception ex)
        {
            // fire-and-forget の起動処理から未監視例外を残さない。
            // ただし無言終了にはしない。ウィンドウを一つも出さずに終了すると
            // 職員には「何も起きない」としか見えず、docs/operations.md §6 の
            // 報告手順（エラー文言の安全な要約）も踏めない。
            ReportStartupFailure(ex);
        }
    }

    private void ReportStartupFailure(Exception ex)
    {
        try
        {
            host.ShowStartupFailure(BuildSafeSummary(ex));
        }
        catch (Exception)
        {
            // 失敗画面すら出せないなら、ウィンドウ無しで居座らせず終了する。
            host.Shutdown();
        }
    }

    /// <summary>
    /// 例外本文は保存先フルパスや氏名を含みうるため載せない（CLAUDE.md ハード制約4）。
    /// 種別だけを残し、職員が管理者へ伝えられる手掛かりにする。
    /// </summary>
    private static string BuildSafeSummary(Exception ex) =>
        $"起動処理に失敗しました（{ex.GetType().Name}）。" +
        "データの保存先を確認し、解消しない場合はこの画面の内容を管理者へ連絡してください。";
}
