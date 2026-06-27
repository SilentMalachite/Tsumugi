using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// `src/**/*.cs` を行単位で走査し、ルール違反を検出する共通基盤。
/// 各ハード制約のトリップワイヤから呼び出される。
/// </summary>
internal static class SourceCodeScanner
{
    public sealed record Violation(string RelativePath, int LineNumber, string Line, string Rule)
    {
        public override string ToString() => $"{RelativePath}:{LineNumber} [{Rule}]: {Line.Trim()}";
    }

    /// <summary>
    /// `src/` 配下の全 .cs ファイル（生成ファイル `obj/`, `bin/` 除外）を列挙する。
    /// </summary>
    public static IEnumerable<(string RelativePath, string FullPath, string[] Lines)> EnumerateSourceFiles()
    {
        var root = TsumugiAssemblyLocator.FindSolutionRoot();
        var srcRoot = Path.Combine(root, "src");
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            // 生成物・ビルド出力は走査対象外。
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            // EF Core Migrations は機械生成で、規約スキャンの対象から外す。
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;

            var rel = Path.GetRelativePath(root, file);
            var lines = File.ReadAllLines(file);
            yield return (rel, file, lines);
        }
    }

    /// <summary>
    /// 各行に対し predicate を適用し、ヒットを Violation として返す。
    /// </summary>
    public static IReadOnlyList<Violation> Scan(string ruleName, Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var hits = new List<Violation>();
        foreach (var (rel, _, lines) in EnumerateSourceFiles())
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // 行コメントは除外（//... 以降は意図的な記述）。
                var noComment = StripLineComment(line);
                if (predicate(noComment))
                {
                    hits.Add(new Violation(rel, i + 1, line, ruleName));
                }
            }
        }
        return hits;
    }

    private static string StripLineComment(string line)
    {
        // 文字列リテラル中の // は対象外にしたいが、本スキャナは粗いため
        // 行頭が空白＋"//"なら丸ごとコメント扱いにする最低限の保護のみ。
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return string.Empty;
        return line;
    }
}
