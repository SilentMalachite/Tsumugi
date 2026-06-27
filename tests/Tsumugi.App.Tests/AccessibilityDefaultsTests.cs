using Avalonia.Styling;
using FluentAssertions;
using Tsumugi.App.Settings;
using Xunit;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App.Tests;

public sealed class AccessibilityDefaultsTests
{
    [Fact]
    public void Theme_resolves_to_avalonia_dark_variant()
    {
        AccessibilityDefaults.Theme.Should().Be(ThemeVariant.Dark);
    }

    [Fact]
    public void Reduced_motion_mirrors_ui_defaults()
    {
        AccessibilityDefaults.ReducedMotion.Should().Be(UiDefaults.ReducedMotion);
    }

    [Fact]
    public void Resources_expose_base_font_size_meeting_minimum()
    {
        AccessibilityDefaults.Resources.Should().ContainKey("BaseFontSize");
        var v = (double)AccessibilityDefaults.Resources["BaseFontSize"];
        v.Should().BeGreaterThanOrEqualTo(UiDefaults.MinimumFontSize);
    }

    [Fact]
    public void Resources_heading_font_is_strictly_larger_than_base()
    {
        var heading = (double)AccessibilityDefaults.Resources["HeadingFontSize"];
        var baseSize = (double)AccessibilityDefaults.Resources["BaseFontSize"];
        heading.Should().BeGreaterThan(baseSize);
    }

    [Fact]
    public void Resources_daily_cell_size_scales_with_base_font()
    {
        // フォント拡大に追従するよう、固定 80x80 ではなく BaseFontSize に比例する寸法を提供する。
        var cell = (double)AccessibilityDefaults.Resources["DailyCellSize"];
        var baseSize = (double)AccessibilityDefaults.Resources["BaseFontSize"];
        cell.Should().BeGreaterThan(baseSize * 4);
    }

    [Fact]
    public void Apply_sets_requested_theme_variant_on_application()
    {
        var app = new TestableApplication();
        AccessibilityDefaults.Apply(app);
        app.RequestedThemeVariant.Should().Be(ThemeVariant.Dark);
    }

    [Fact]
    public void Apply_merges_font_resources_into_application()
    {
        var app = new TestableApplication();
        AccessibilityDefaults.Apply(app);
        app.Resources.Should().ContainKey("BaseFontSize");
        app.Resources.Should().ContainKey("HeadingFontSize");
        app.Resources.Should().ContainKey("DailyCellSize");
    }

    [Fact]
    public void Apply_attaches_a_reduced_motion_style_when_flag_is_on()
    {
        // UiDefaults.ReducedMotion=true 前提の検証。
        var app = new TestableApplication();
        AccessibilityDefaults.Apply(app);
        app.Styles.Should().NotBeEmpty();
    }

    // Avalonia Application を直接 new するためのテスト用具体型。
    private sealed class TestableApplication : AvaloniaApplication { }
}
