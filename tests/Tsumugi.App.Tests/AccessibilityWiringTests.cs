using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Tsumugi.App.Settings;
using Xunit;

namespace Tsumugi.App.Tests;

/// <summary>
/// CLAUDE.md §ハード制約 5（アクセシビリティ既定）の Avalonia 配線を機械判定する。
/// 1. Views/*.axaml が参照する DynamicResource キーは AccessibilityDefaults.Resources が公開する範囲に収まる。
/// 2. Views/*.axaml に FontSize の数値直書きが残らない（必ず DynamicResource 経由）。
/// </summary>
public sealed class AccessibilityWiringTests
{
    private static readonly Regex DynamicResourcePattern =
        new(@"\{DynamicResource\s+([^}\s]+)\s*\}", RegexOptions.Compiled);

    private static readonly Regex HardcodedFontSizePattern =
        new(@"FontSize\s*=\s*""\s*\d", RegexOptions.Compiled);

    private static readonly Regex HardcodedColorPattern =
        new(@"(Foreground|Background|BorderBrush)\s*=\s*""\s*#", RegexOptions.Compiled);

    /// <summary>
    /// 画面の axaml を列挙する。Views/ 配下の UserControl に加え、App 直下に置かれる
    /// Window（MainWindow / FirstRunWizardWindow / StartupFailureWindow …）も対象にする。
    /// App.axaml はリソース定義側なので含めない。
    /// </summary>
    private static IEnumerable<(string RelativePath, string Content)> EnumerateViewXaml()
    {
        var root = RepositoryPaths.Root;
        var appDir = Path.Combine(root, "src", "Tsumugi.App");

        var files = Directory
            .EnumerateFiles(Path.Combine(appDir, "Views"), "*.axaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(appDir, "*Window.axaml", SearchOption.TopDirectoryOnly));

        foreach (var file in files)
        {
            yield return (Path.GetRelativePath(root, file), File.ReadAllText(file));
        }
    }

    [Fact]
    public void Enumeration_covers_window_xaml_outside_the_views_folder()
    {
        // Window は Views/ の外（App 直下）に置かれる。ここを列挙しないと、
        // 新しい画面がハード制約5の機械判定をすり抜ける。
        var paths = EnumerateViewXaml().Select(x => x.RelativePath).ToList();

        paths.Should().Contain(p => p.EndsWith("MainWindow.axaml", StringComparison.Ordinal));
        paths.Should().Contain(p => p.EndsWith("FirstRunWizardWindow.axaml", StringComparison.Ordinal));
        paths.Should().Contain(p => p.EndsWith("StartupFailureWindow.axaml", StringComparison.Ordinal));
        paths.Should().NotContain(p => p.EndsWith("App.axaml", StringComparison.Ordinal),
            because: "App.axaml は画面ではなくリソース定義側");
    }

    [Fact]
    public void All_dynamic_resource_keys_in_views_are_provided_by_AccessibilityDefaults()
    {
        var providedKeys = AccessibilityDefaults.Resources.Keys.ToHashSet(StringComparer.Ordinal);
        var unknown = new List<string>();

        foreach (var (rel, xml) in EnumerateViewXaml())
        {
            foreach (Match m in DynamicResourcePattern.Matches(xml))
            {
                var key = m.Groups[1].Value;
                if (!providedKeys.Contains(key))
                {
                    unknown.Add($"{rel}: {key}");
                }
            }
        }

        unknown.Should().BeEmpty(
            because: "Views で参照する DynamicResource キーは AccessibilityDefaults.Resources で公開されたものに限る。" +
                     Environment.NewLine +
                     "未提供キー参照: " + string.Join(Environment.NewLine, unknown));
    }

    [Fact]
    public void Views_do_not_hardcode_FontSize_numeric_literal()
    {
        var violations = new List<string>();
        foreach (var (rel, xml) in EnumerateViewXaml())
        {
            var lines = xml.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (HardcodedFontSizePattern.IsMatch(lines[i]))
                {
                    violations.Add($"{rel}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            because: "FontSize は UiDefaults.MinimumFontSize に追従するため DynamicResource 経由で指定する。" +
                     Environment.NewLine +
                     "ハードコード違反: " + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Views_do_not_hardcode_color_literals()
    {
        // 既定テーマはダーク。画面ごとに色を直書きすると、テーマ前提が崩れたときに
        // 一箇所ずつ壊れる（例: 明るい警告背景に明るい文字＝読めない）。
        // 色は AccessibilityDefaults のセマンティックなリソース経由で参照する。
        var violations = new List<string>();
        foreach (var (rel, xml) in EnumerateViewXaml())
        {
            var lines = xml.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (HardcodedColorPattern.IsMatch(lines[i]))
                {
                    violations.Add($"{rel}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            because: "Foreground / Background / BorderBrush は DynamicResource 経由で指定する。" +
                     Environment.NewLine +
                     "ハードコード違反: " + string.Join(Environment.NewLine, violations));
    }

}
