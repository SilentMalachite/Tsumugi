using System;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// CLAUDE.md §ハード制約 6「カルチャ依存（数値/日付整形）に OS 差の地雷を作らない」のトリップワイヤ。
/// `.Parse(...)` / `.ParseExact(...)` と書式付き `.ToString("...")` には `CultureInfo` を明示する。
/// globalization有効下でもOSのCulture差を生まないよう、policyとしてCIで機械担保する。
/// </summary>
public sealed class CultureExplicitnessGuardTests
{
    [Fact]
    public void Source_does_not_call_Parse_or_ToString_without_explicit_culture()
    {
        var hits = SourceCodeScanner.Scan(
            ruleName: "culture",
            predicate: CultureExplicitnessGuard.IsViolatingLine);

        hits.Should().BeEmpty(
            because: "Parse/ParseExact/ToString に CultureInfo を明示すること。" +
                     Environment.NewLine +
                     "違反: " + string.Join(Environment.NewLine, hits.Select(h => h.ToString())));
    }

    [Theory]
    [InlineData(@"DateOnly.Parse(s);", true)]
    [InlineData(@"int.TryParse(""1"", out var n);", true)]
    [InlineData(@"d.ToString(""yyyy-MM-dd"");", true)]
    [InlineData(@"DateOnly.ParseExact(s, ""yyyy-MM-dd"", CultureInfo.InvariantCulture)", false)]
    [InlineData(@"d.ToString(""O"", CultureInfo.InvariantCulture)", false)]
    [InlineData(@"d.ToString()", false)]                                     // 無引数は対象外
    [InlineData(@"int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture)", false)]
    [InlineData(@"// d.ToString(""yyyy"") は禁止例", false)]                  // コメント
    public void IsViolatingLine_distinguishes(string line, bool expected)
    {
        ArgumentNullException.ThrowIfNull(line);
        var isCommentOnly = line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        if (isCommentOnly) { expected.Should().BeFalse(); return; }
        CultureExplicitnessGuard.IsViolatingLine(line).Should().Be(expected);
    }
}

internal static class CultureExplicitnessGuard
{
    // 対象呼び出し: Parse 系（型名は緩く）、書式文字列付き ToString。
    private static readonly Regex ParseCall = new(
        @"\.(Parse|TryParse|ParseExact|TryParseExact)\s*\(", RegexOptions.Compiled);

    private static readonly Regex ToStringWithFormat = new(
        @"\.ToString\(\s*""[^""]+""", RegexOptions.Compiled);

    public static bool IsViolatingLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        // CultureInfo / Invariant をどこかで言及していれば OK（同行内の明示と見なす）。
        if (line.Contains("CultureInfo", StringComparison.Ordinal)) return false;
        if (line.Contains("Invariant", StringComparison.Ordinal)) return false;

        return ParseCall.IsMatch(line) || ToStringWithFormat.IsMatch(line);
    }
}
