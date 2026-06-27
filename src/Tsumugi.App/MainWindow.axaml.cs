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
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        // RecipientList の編集要求を RecipientEdit に橋渡しし、編集タブに切り替える。
        viewModel.RecipientList.EditRequested = dto =>
        {
            viewModel.RecipientEdit.LoadForEdit(
                dto.Id, dto.KanjiName, dto.KanaName, dto.DateOfBirth, dto.ConcurrencyToken);
            MainTabs.SelectedIndex = 1;  // 利用者登録/編集タブ
        };
    }
}
