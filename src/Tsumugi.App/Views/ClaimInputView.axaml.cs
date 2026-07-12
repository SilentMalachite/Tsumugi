using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class ClaimInputView : UserControl
{
    public ClaimInputView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ClaimInputViewModel viewModel)
            await viewModel.InitializeAsync();
    }
}
