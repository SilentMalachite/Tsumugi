using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tsumugi.App.ViewModels;

/// <summary>職能手当段（閾値行）の編集 ViewModel。削除コールバックで親コレクションから自身を取り除く。</summary>
public sealed partial class SkillAllowanceTierViewModel : ViewModelBase
{
    private readonly Action<SkillAllowanceTierViewModel> _onRemove;

    [ObservableProperty] private int _minHours;
    [ObservableProperty] private int _yen;

    public SkillAllowanceTierViewModel(Action<SkillAllowanceTierViewModel> onRemove)
        => _onRemove = onRemove;

    /// <summary>行を削除する（DataTemplate 内ボタンから呼ばれる）。</summary>
    [RelayCommand]
    private void Remove() => _onRemove(this);
}
