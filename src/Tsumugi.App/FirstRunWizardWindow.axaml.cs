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
        if (!_registrationCompleted)
            RequestCancellation();
    }

    private void RequestCancellation()
    {
        if (_cancellationRequested)
            return;

        _cancellationRequested = true;
        CancellationRequested?.Invoke();
    }
}
