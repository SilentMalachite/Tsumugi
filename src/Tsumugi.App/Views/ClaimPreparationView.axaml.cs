using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class ClaimPreparationView : UserControl
{
    public ClaimPreparationView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ClaimPreparationViewModel viewModel)
            await viewModel.InitializeAsync();
    }
}
