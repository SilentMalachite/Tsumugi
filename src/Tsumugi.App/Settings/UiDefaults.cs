namespace Tsumugi.App.Settings;

public enum UiTheme { Dark, Light }

/// <summary>
/// アクセシビリティ既定（仕様 §4.6）: ダークテーマ・低アニメーション・フォント拡大追従。
/// 利用者設定で上書き可能だが既定値は常にここに固定する。
/// </summary>
public static class UiDefaults
{
    public const UiTheme DefaultTheme = UiTheme.Dark;
    public const bool ReducedMotion = true;
    public const int MinimumFontSize = 14;
}
