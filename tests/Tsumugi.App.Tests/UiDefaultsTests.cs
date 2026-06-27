using FluentAssertions;
using Tsumugi.App.Settings;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class UiDefaultsTests
{
    [Fact]
    public void Default_theme_is_dark()
    {
        UiDefaults.DefaultTheme.Should().Be(UiTheme.Dark);
    }

    [Fact]
    public void Reduced_motion_is_on_by_default()
    {
        UiDefaults.ReducedMotion.Should().BeTrue();
    }

    [Fact]
    public void Default_font_size_is_accessible_minimum_14()
    {
        UiDefaults.MinimumFontSize.Should().BeGreaterThanOrEqualTo(14);
    }
}
