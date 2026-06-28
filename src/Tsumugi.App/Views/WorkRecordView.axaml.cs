using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class WorkRecordView : UserControl
{
    public WorkRecordView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkRecordViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
