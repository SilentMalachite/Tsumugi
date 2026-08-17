using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class DisabilityCertificateView : UserControl
{
    public DisabilityCertificateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DisabilityCertificateViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
