using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Styling;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App.Settings;

/// <summary>
/// CLAUDE.md ハード制約 5（アクセシビリティ既定）と仕様 §4.6 を Avalonia へ流し込むヘルパー。
/// テーマ・低アニメーション・フォント拡大追従を一箇所で表現し、各 View はリソース参照で従う。
/// </summary>
public static class AccessibilityDefaults
{
    public static ThemeVariant Theme => ToThemeVariant(UiDefaults.DefaultTheme);

    private static ThemeVariant ToThemeVariant(UiTheme theme) => theme switch
    {
        UiTheme.Light => ThemeVariant.Light,
        _ => ThemeVariant.Dark,
    };

    public static bool ReducedMotion => UiDefaults.ReducedMotion;

    /// <summary>各 View が DynamicResource で参照するリソース。フォント拡大に追従する寸法を提供する。</summary>
    public static IReadOnlyDictionary<string, object> Resources { get; } = new Dictionary<string, object>
    {
        // 本文・キャプション最小サイズ（UiDefaults.MinimumFontSize に同期）。
        ["BaseFontSize"] = (double)UiDefaults.MinimumFontSize,
        // 見出し用（最小 +4 でコントラスト確保）。
        ["HeadingFontSize"] = (double)(UiDefaults.MinimumFontSize + 4),
        // 日次記録セルなど固定寸法の代わりに使う、フォントに比例した1辺サイズ。
        ["DailyCellSize"] = (double)(UiDefaults.MinimumFontSize * 7),
    };

    /// <summary>Application インスタンスにテーマ・リソース・低アニメーション Style を適用する。</summary>
    public static void Apply(AvaloniaApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.RequestedThemeVariant = Theme;

        foreach (var (k, v) in Resources)
        {
            app.Resources[k] = v;
        }

        if (ReducedMotion)
        {
            app.Styles.Add(BuildReducedMotionStyle());
        }
    }

    // 低アニメーション: あらゆる Control の Transitions を null にし、暗黙のフェード/スライドを抑止する。
    private static Style BuildReducedMotionStyle()
    {
        var style = new Style(s => s.OfType<Control>());
        style.Setters.Add(new Setter(Animatable.TransitionsProperty, null));
        return style;
    }
}
