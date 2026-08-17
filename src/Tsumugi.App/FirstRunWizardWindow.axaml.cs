using Avalonia.Controls;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App;

/// <summary>初回登録 ViewModel の寿命通知をウィンドウイベントへ中継する。</summary>
public partial class FirstRunWizardWindow : Window
{
    private bool _registrationCompleted;
    private bool _cancellationRequested;

    public FirstRunWizardWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public FirstRunWizardWindow(FirstRunWizardViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
        viewModel.Registered += OnRegistered;
        viewModel.Cancelled += OnCancelled;
    }

    public event Action? RegistrationCompleted;

    public event Action? CancellationRequested;

    private void OnRegistered()
    {
        _registrationCompleted = true;
        RegistrationCompleted?.Invoke();
    }

    private void OnCancelled() => RequestCancellation();

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_registrationCompleted)
            return;

        // 最後の Window を閉じて OnLastWindowClose から再度終了要求が出ることを防ぐ。
        // 最終終了は App のバックアップ完了後の desktop.Shutdown に委ねる。
        e.Cancel = true;
        RequestCancellation();
    }

    private void RequestCancellation()
    {
        if (_cancellationRequested)
            return;

        _cancellationRequested = true;
        IsEnabled = false;
        CancellationRequested?.Invoke();
    }
}
