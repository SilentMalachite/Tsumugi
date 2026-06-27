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

    private static IEnumerable<(string RelativePath, string Content)> EnumerateViewXaml()
    {
        var root = FindSolutionRoot();
        var viewsDir = Path.Combine(root, "src", "Tsumugi.App", "Views");
        foreach (var file in Directory.EnumerateFiles(viewsDir, "*.axaml", SearchOption.AllDirectories))
        {
            yield return (Path.GetRelativePath(root, file), File.ReadAllText(file));
        }
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

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("Tsumugi.sln").Any()) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Tsumugi.sln が祖先方向に見つからない");
    }
}
