namespace Tsumugi.App.Startup;

/// <summary>初期画面の表示とアプリ終了を抽象化する。</summary>
public interface IInitialWindowHost
{
    void ShowMain();

    void ShowWizard(Action registered, Action cancelled);

    void Shutdown();
}
