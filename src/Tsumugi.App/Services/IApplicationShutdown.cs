namespace Tsumugi.App.Services;

/// <summary>
/// アプリの終了要求。復元は稼働中の DbContext の下で DB ファイルを差し替えるため、
/// 完了後に必ずプロセスを終える必要がある（ADR 0052 決定6。運用上の推奨ではなく要件）。
/// VM から Avalonia のライフタイムを直接触らずテスト可能にするための薄い抽象。
/// </summary>
public interface IApplicationShutdown
{
    void RequestShutdown();
}
