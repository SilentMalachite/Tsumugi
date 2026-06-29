using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class WageCalculationView : UserControl
{
    public WageCalculationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // 事業所選択 ComboBox を実画面で機能させるため、Loaded で VM 初期化を発火させる。
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WageCalculationViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
