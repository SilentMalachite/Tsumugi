using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class OfficeCapabilityView : UserControl
{
    public OfficeCapabilityView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OfficeCapabilityViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
