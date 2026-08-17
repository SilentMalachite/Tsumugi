namespace Tsumugi.App.Startup;

/// <summary>初期画面の表示とアプリ終了を抽象化する。</summary>
public interface IInitialWindowHost
{
    void ShowMain();

    void ShowWizard(Action registered, Action cancelled);

    /// <summary>
    /// 起動処理が失敗したことを職員へ提示する。無言終了にすると
    /// 「ダブルクリックしても何も起きない」としか見えず、報告もできない。
    /// </summary>
    /// <param name="message">保存先パスや氏名を含まない安全な要約。</param>
    void ShowStartupFailure(string message);

    void Shutdown();
}
