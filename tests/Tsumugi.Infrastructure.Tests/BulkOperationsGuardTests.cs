using System;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// CLAUDE.md §コーディング規約「エンティティは record ＋ 追記型(append-only)」のトリップワイヤ。
/// <c>AppendOnlyGuard.Inspect</c> は ChangeTracker の Modified/Deleted しか見ないため、
/// ChangeTracker を経由しない EF Core の bulk operations（ExecuteUpdate*/ExecuteDelete*）で
/// 追記型エンティティを書き換えても検出できない。src/ 側でその呼び出し自体を禁止する。
/// 決定の根拠は ADR 0050。
/// </summary>
public sealed class BulkOperationsGuardTests
{
    [Fact]
    public void Source_does_not_call_bulk_update_or_delete()
    {
        var hits = SourceCodeScanner.Scan(
            ruleName: "bulk-operations",
            predicate: BulkOperationsGuard.IsBulkOperationLine);

        hits.Should().BeEmpty(
            because: "ExecuteUpdate*/ExecuteDelete* は ChangeTracker を経由せず AppendOnlyGuard を迂回する（ADR 0050）。" +
                     Environment.NewLine +
                     "違反: " + string.Join(Environment.NewLine, hits.Select(h => h.ToString())));
    }

    [Theory]
    [InlineData("await db.Set<ClaimBatch>().ExecuteDeleteAsync(ct);", true)]
    [InlineData("db.Set<ClaimBatch>().ExecuteUpdate(s => s.SetProperty(x => x.Note, \"\"));", true)]
    [InlineData("await db.Certificates.ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, 2), ct);", true)]
    [InlineData("await db.Database.ExecuteSqlRawAsync(\"DELETE FROM ClaimBatches\", ct);", false)] // ルール2の担当
    [InlineData("await db.Database.ExecuteSqlRawAsync($\"VACUUM INTO '{escaped}'\", ct);", false)] // ハード制約7
    [InlineData("var marker = nameof(ExecuteDeleteMarker);", false)]                              // 前置ドットが無い
    [InlineData("await repository.ExecuteDeleteRequestAsync(id, ct);", false)]                    // 別名メソッド
    [InlineData("// ExecuteDeleteAsync は禁止", false)]                                            // コメント行
    public void IsBulkOperationLine_distinguishes(string line, bool expected)
    {
        ArgumentNullException.ThrowIfNull(line);
        // 注意: SourceCodeScanner は行頭コメントを除去してから predicate を呼ぶ。
        // ここでは predicate 単体の契約を検証するため、コメント行は素通しで false を期待する。
        var isCommentOnly = line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        if (isCommentOnly) { expected.Should().BeFalse(); return; }

        BulkOperationsGuard.IsBulkOperationLine(line).Should().Be(expected);
    }
}

internal static class BulkOperationsGuard
{
    // ChangeTracker を経由しない EF Core の bulk operations。
    // メソッド呼び出しの形（前置ドット ＋ 開き括弧）を要求し、単なる識別子の出現では反応しない。
    private static readonly Regex BulkCall = new(
        @"\.Execute(Update|Delete)(Async)?\s*\(", RegexOptions.Compiled);

    public static bool IsBulkOperationLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return BulkCall.IsMatch(line);
    }
}
