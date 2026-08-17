using Avalonia.Controls;
using Tsumugi.App.Startup;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App;

/// <summary>初回登録 ViewModel の寿命通知をウィンドウイベントへ中継する。</summary>
public partial class FirstRunWizardWindow : Window
{
    private readonly FirstRunWizardLifecycle _lifecycle;

    public FirstRunWizardWindow()
    {
        InitializeComponent();

        // 状態遷移は FirstRunWizardLifecycle が持つ（Avalonia 非依存で検証できる）。
        _lifecycle = new FirstRunWizardLifecycle(
            onRegistrationCompleted: () => RegistrationCompleted?.Invoke(),
            onCancellationRequested: () =>
            {
                IsEnabled = false;
                CancellationRequested?.Invoke();
            });

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

    private void OnRegistered() => _lifecycle.NotifyRegistered();

    private void OnCancelled() => _lifecycle.RequestCancellation();

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_lifecycle.ShouldAllowClose)
            return;

        // 最後の Window を閉じて OnLastWindowClose から再度終了要求が出ることを防ぐ。
        // 最終終了は App のバックアップ完了後の desktop.Shutdown に委ねる。
        e.Cancel = true;
        _lifecycle.RequestCancellation();
    }
}
