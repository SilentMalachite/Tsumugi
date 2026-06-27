using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class DailyRecordView : UserControl
{
    public DailyRecordView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // 利用者選択 ComboBox を実画面で機能させるため、Loaded で VM 初期化を発火させる。
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DailyRecordViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
