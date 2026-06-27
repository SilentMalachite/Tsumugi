using Avalonia.Controls;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
