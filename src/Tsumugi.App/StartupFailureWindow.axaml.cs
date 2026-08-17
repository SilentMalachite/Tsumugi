using Avalonia.Controls;

namespace Tsumugi.App;

/// <summary>
/// 起動処理が失敗したことだけを伝える最小画面。
/// 閉じると最後のウィンドウが無くなり、App の終了時バックアップを経て終了する。
/// </summary>
public partial class StartupFailureWindow : Window
{
    public StartupFailureWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
    }

    public StartupFailureWindow(string message) : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        MessageText.Text = message;
    }
}
