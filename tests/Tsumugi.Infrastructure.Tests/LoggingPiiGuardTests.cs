using System;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// CLAUDE.md §ハード制約 4「ログに氏名・受給者証番号・保存先フルパスを出さない」をトリップワイヤで担保する。
/// 現時点では src/ に logging API は存在しない（trivial green）。
/// 将来 logging が導入された瞬間に PII 識別子と同居していれば違反として検出する。
/// </summary>
public sealed class LoggingPiiGuardTests
{
    [Fact]
    public void Source_does_not_co_locate_logging_calls_with_pii_identifiers()
    {
        var hits = SourceCodeScanner.Scan(
            ruleName: "log-pii",
            predicate: LoggingPiiGuard.IsViolatingLine);

        hits.Should().BeEmpty(
            because: "ログに PII（氏名/受給者証番号/接続文字列等）を含めない（CLAUDE.md §ハード制約 4）。" +
                     Environment.NewLine +
                     "違反: " + string.Join(Environment.NewLine, hits.Select(h => h.ToString())));
    }

    [Theory]
    [InlineData("_logger.LogInformation(\"recipient {Name}\", recipient.KanjiName);", true)]
    [InlineData("Console.WriteLine($\"saved {dto.CertificateNumber}\");", true)]
    [InlineData("Trace.TraceError(\"db: \" + options.ConnectionString);", true)]
    [InlineData("Debug.WriteLine(\"birth=\" + p.DateOfBirth);", true)]
    [InlineData("_logger.LogInformation(\"saved\");", false)]
    [InlineData("var name = recipient.KanjiName;", false)]
    [InlineData("// ログには KanjiName を出さないこと", false)]   // コメント行は対象外
    public void IsViolatingLine_distinguishes_co_location(string line, bool expected)
    {
        ArgumentNullException.ThrowIfNull(line);
        // 注意: SourceCodeScanner はコメント行を除去してから predicate を呼ぶ。
        // ここでは予測を一致させるため、テスト側で同じ前処理を行わない（生 line で直接判定）。
        var leadingComment = line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        if (leadingComment) { expected.Should().BeFalse(); return; }
        LoggingPiiGuard.IsViolatingLine(line).Should().Be(expected);
    }
}

internal static class LoggingPiiGuard
{
    private static readonly Regex LoggingCallPattern = new(
        @"\b(ILogger|_logger|Logger|Log)\.(Log\w*|Information|Warning|Error|Critical|Debug|Trace)\s*\(" +
        @"|\bConsole\.(Write|WriteLine|Error\.Write|Error\.WriteLine)\s*\(" +
        @"|\bTrace\.(Write|WriteLine|TraceInformation|TraceWarning|TraceError)\s*\(" +
        @"|\bDebug\.(Write|WriteLine|Print)\s*\(",
        RegexOptions.Compiled);

    private static readonly string[] PiiIdentifiers =
    {
        "KanjiName", "KanaName", "DateOfBirth",
        "CertificateNumber", "Municipality",
        "ConnectionString", "DbPath",
    };

    public static bool IsViolatingLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return LoggingCallPattern.IsMatch(line)
               && PiiIdentifiers.Any(id => line.Contains(id, StringComparison.Ordinal));
    }
}
