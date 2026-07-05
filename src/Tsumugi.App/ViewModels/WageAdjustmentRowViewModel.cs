using CommunityToolkit.Mvvm.ComponentModel;
using Tsumugi.Application.Dtos;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 工賃調整マトリクスの 1 行（利用者 × 特別手当）。
/// </summary>
public sealed partial class WageAdjustmentRowViewModel : ObservableObject
{
    [ObservableProperty] private int _specialAllowanceYen;
    [ObservableProperty] private bool _isDirty;

    public RecipientDto Recipient { get; }

    public WageAdjustmentRowViewModel(RecipientDto recipient, int initialYen)
    {
        Recipient = recipient;
        // 初期値はバッキングフィールド直接代入 → OnChanged が発火しない
        _specialAllowanceYen = initialYen;
        _isDirty = false;
    }

    partial void OnSpecialAllowanceYenChanged(int value) => IsDirty = true;

    /// <summary>保存完了後にダーティフラグをリセットする。</summary>
    internal void ResetDirty() => IsDirty = false;
}
