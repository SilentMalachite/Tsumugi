using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class FaceSheetView : UserControl
{
    public FaceSheetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FaceSheetViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
