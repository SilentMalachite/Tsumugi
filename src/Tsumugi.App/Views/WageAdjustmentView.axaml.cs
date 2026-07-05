using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class WageAdjustmentView : UserControl
{
    public WageAdjustmentView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // 事業所・利用者 ComboBox を実画面で機能させるため、Loaded で VM 初期化を発火させる。
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WageAdjustmentViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
