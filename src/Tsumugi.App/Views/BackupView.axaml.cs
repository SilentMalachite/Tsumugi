using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class BackupView : UserControl
{
    public BackupView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // 実画面で世代一覧を表示するため、Loaded で VM の読み込みを発火させる。
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm) await vm.LoadAsync();
    }
}
