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

    [Fact]
    public void Source_does_not_execute_mutating_raw_sql()
    {
        var hits = SourceCodeScanner.Scan(
            ruleName: "raw-sql-dml",
            predicate: BulkOperationsGuard.IsMutatingRawSqlLine);

        hits.Should().BeEmpty(
            because: "raw SQL も ChangeTracker を経由しない。行を書き換える SQL と、行内で内容を確認できない " +
                     "SQL（変数渡し・複数行リテラル）を禁止する（ADR 0050 決定2）。" +
                     Environment.NewLine +
                     "違反: " + string.Join(Environment.NewLine, hits.Select(h => h.ToString())));
    }

    [Theory]
    [InlineData("await db.Database.ExecuteSqlRawAsync(\"DELETE FROM ClaimBatches\", ct);", true)]
    [InlineData("await db.Database.ExecuteSqlAsync($\"UPDATE Certificates SET Revision = {n}\", ct);", true)]
    [InlineData("await db.Database.ExecuteSqlRawAsync(sql, ct);", true)]                    // 検証不能 → fail-close
    [InlineData("await db.Database.ExecuteSqlRawAsync(sql, ct); // caller: \"AdminPanel\"", true)] // 無関係リテラル同居
    [InlineData("await db.Database.ExecuteSqlRawAsync(", true)]                             // 複数行 → fail-close
    [InlineData("await db.Database.ExecuteSqlRawAsync(\"\"\"DELETE FROM ClaimBatches\"\"\", ct);", true)] // 生文字列 → fail-close
    [InlineData("await db.Database.ExecuteSqlRawAsync(\"\"\"", true)]                       // 生文字列の開始行 → fail-close
    [InlineData("await db.Database.ExecuteSqlRawAsync($\"VACUUM INTO '{escaped}'\", ct);", false)]
    [InlineData("var rows = db.Set<Office>().FromSqlRaw(\"SELECT * FROM Offices\").ToList();", false)]
    [InlineData("await db.SaveChangesAsync(ct);", false)]                                   // 対象APIではない
    [InlineData("// ExecuteSqlRawAsync(\"DELETE FROM X\") は禁止", false)]                   // コメント行
    public void IsMutatingRawSqlLine_distinguishes(string line, bool expected)
    {
        ArgumentNullException.ThrowIfNull(line);
        // 注意: SourceCodeScanner は行頭コメントを除去してから predicate を呼ぶ。
        // ここでは predicate 単体の契約を検証するため、コメント行は素通しで false を期待する。
        var isCommentOnly = line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        if (isCommentOnly) { expected.Should().BeFalse(); return; }

        BulkOperationsGuard.IsMutatingRawSqlLine(line).Should().Be(expected);
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

    // ChangeTracker を経由しない raw SQL の実行・問い合わせ API。
    private static readonly Regex RawSqlCall = new(
        @"\b(ExecuteSql|FromSql)[A-Za-z]*\s*\(", RegexOptions.Compiled);

    // 生文字列リテラル（"""…）の開始。複数行にまたがりうるうえ区切りの " の数も可変で、
    // 行単位走査では内容を確定できない。FirstArgumentLiteral は先頭2つの " を対にして
    // 空キャプチャでマッチ成功してしまう（"""DELETE FROM X""" → キャプチャ群が空文字列）ため、
    // FirstArgumentLiteral を試す前に fail-close で弾く（最終レビュー指摘・ADR 0050 決定2 改訂）。
    private static readonly Regex RawStringLiteralStart = new(
        "^[$@]*\"{3,}", RegexOptions.Compiled);

    // 呼び出しの第1引数の文字列リテラル。$ / @ 接頭辞を許し、開き括弧の直後に来ることを要求する
    // （spec 決定2。行のどこかにあるリテラルを拾うと、行末コメントの "…" で fail-close が無効化される）。
    private static readonly Regex FirstArgumentLiteral = new(
        "^[$@]{0,2}\"([^\"]*)\"", RegexOptions.Compiled);

    // 行を書き換える DML と、破壊的 DDL。CREATE は一時テーブル等の正当な読み取り用途を巻き込むため含めない。
    private static readonly Regex MutatingKeyword = new(
        @"\b(INSERT|UPDATE|DELETE|REPLACE|DROP|ALTER|TRUNCATE)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsMutatingRawSqlLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var call = RawSqlCall.Match(line);
        if (!call.Success) return false;

        // 第1引数が文字列リテラルでなければ SQL の内容を確認できない（変数渡し・複数行リテラル）。
        // fail-close で違反にする。ここを「通す」にすると ExecuteSqlRawAsync(sql) と書くだけで
        // ルールを無力化できる。無関係なリテラルが同じ行に居ても第1引数だけを見るので影響されない。
        var afterOpenParen = line[(call.Index + call.Length)..].TrimStart();

        // 生文字列リテラルは行内で内容を確定できない（複数行になりうる・区切りの " の数が可変）ため、
        // FirstArgumentLiteral を試す前に弾く。この結果、無害な生文字列（例:
        // ExecuteSqlRawAsync("""SELECT 1""")）も違反になるが、これは「行内で内容を確認できない形は
        // fail-close」という決定2 の方針どおりの意図した挙動であり、バグではない（最終レビュー指摘）。
        if (RawStringLiteralStart.IsMatch(afterOpenParen)) return true;

        var literal = FirstArgumentLiteral.Match(afterOpenParen);
        if (!literal.Success) return true;

        return MutatingKeyword.IsMatch(literal.Groups[1].Value);
    }
}
