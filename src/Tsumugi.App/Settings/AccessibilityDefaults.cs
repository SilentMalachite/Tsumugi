using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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

        // セマンティックな色。既定のダーク背景（Fluent ≒ #202020）に対して
        // WCAG AA（本文 4.5:1）以上を満たす値を選ぶ。画面ごとの直書きは
        // AccessibilityWiringTests が禁止する。
        ["ErrorForeground"] = Brush(0xFF, 0x44, 0x44),          // 対 #202020 ≒ 4.8:1
        ["WarningForeground"] = Brush(0xFF, 0xAA, 0x00),        // 対 #202020 ≒ 8.5:1
        ["WarningPanelBackground"] = Brush(0x55, 0x33, 0x00),   // 暗い琥珀。既定の明るい文字が乗る
        ["WarningPanelBorder"] = Brush(0xD3, 0x9E, 0x00),
        ["SubtleBorder"] = Brush(0x44, 0x44, 0x44),
    };

    // 文字列パースを使わない（カルチャ明示ガードに掛かるうえ、成分の方が読める）。
    private static ImmutableSolidColorBrush Brush(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));

    /// <summary>Application インスタンスにテーマ・リソース・低アニメーション Style を適用する。</summary>
    public static void Apply(AvaloniaApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.RequestedThemeVariant = Theme;

        foreach (var (k, v) in Resources)
        {
            app.Resources[k] = v;
        }

        app.Styles.Add(BuildControlFontSizeStyle());

        if (ReducedMotion)
        {
            app.Styles.Add(BuildReducedMotionStyle());
        }
    }

    // フォント拡大追従: TextBlock はラベルごとに BaseFontSize を指定できるが、
    // TextBox/Button/ComboBox は Fluent テーマの固定値を使う。全 TemplatedControl の
    // 既定を MinimumFontSize に合わせ、ラベルだけ拡大して入力欄が据え置きになるのを防ぐ。
    // 各 View の明示指定（HeadingFontSize 等）はローカル値なので、この Style より優先される。
    private static Style BuildControlFontSizeStyle()
    {
        var style = new Style(s => s.OfType<TemplatedControl>());
        style.Setters.Add(new Setter(
            TemplatedControl.FontSizeProperty, (double)UiDefaults.MinimumFontSize));
        return style;
    }

    // 低アニメーション: あらゆる Control の Transitions を null にし、暗黙のフェード/スライドを抑止する。
    private static Style BuildReducedMotionStyle()
    {
        var style = new Style(s => s.OfType<Control>());
        style.Setters.Add(new Setter(Animatable.TransitionsProperty, null));
        return style;
    }
}
