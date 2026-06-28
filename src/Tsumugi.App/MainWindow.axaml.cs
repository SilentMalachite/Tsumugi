using System.ComponentModel;
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
            viewModel.RecipientEdit.LoadForEdit(dto);
            MainTabs.SelectedIndex = 1;  // 利用者登録/編集タブ
        };

        // 起動時に一覧を初期ロード（View 単体に自動ロード手段がないため Window 側で起動）。
        _ = viewModel.RecipientList.LoadAsync();

        // 利用者一覧タブに切り替わるたびに最新を再取得（登録/更新の反映を保証）。
        MainTabs.SelectionChanged += (_, _) =>
        {
            if (MainTabs.SelectedIndex == 0)
            {
                _ = viewModel.RecipientList.LoadAsync();
            }
        };

        // 登録/更新成功時（IsSaved=true）にも一覧を更新（同タブ内のみ変更しても反映するため）。
        viewModel.RecipientEdit.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecipientEditViewModel.IsSaved)
                && viewModel.RecipientEdit.IsSaved)
            {
                _ = viewModel.RecipientList.LoadAsync();
            }
        };
    }
}
