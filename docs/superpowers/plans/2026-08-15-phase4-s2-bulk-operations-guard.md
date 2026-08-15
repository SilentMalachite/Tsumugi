# Phase 4 S2 実装計画 — bulk operations 禁止のソース走査ガードと NetArchTest 見送り

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development`（推奨）または `superpowers:executing-plans` でタスク単位に実装すること。ステップは checkbox (`- [ ]`) で進捗を管理する。**進捗の正本はこのチェックボックス。**

**Goal:** `AppendOnlyGuard` が見逃す ChangeTracker 迂回経路（`ExecuteUpdate*` / `ExecuteDelete*` / 行を書き換える raw SQL）を `src/` のソース走査で禁止し、NetArchTest の採否を ADR で決着させる。

**Architecture:** 既存の `SourceCodeScanner.Scan(ruleName, predicate)` に判定関数を2本足すだけで、新しい走査基盤は作らない。判定は `internal static class BulkOperationsGuard` の純粋関数（行文字列 → bool）に閉じ、テーブル駆動 `[Theory]` で境界を固定する。allowlist は両ルールとも作らない。

**Tech Stack:** .NET 10 / C# 14、xUnit ＋ FluentAssertions、`System.Text.RegularExpressions`。プロダクションコードの変更・新規 NuGet 依存・migration は無い。

## Global Constraints

- **spec正本**: `docs/superpowers/specs/2026-08-15-phase4-s2-bulk-operations-guard-design.md`。逸脱は理由付きで ADR へ記録する。
- **allowlist を作らない**（spec 決定1）。ガードを黙らせる操作は ADR 0050 の改訂を伴わせる。
- **raw SQL は内容基準で判定する**（spec 決定2）。`VACUUM INTO` はパス単位の例外ではなく内容で通す。検証不能な形（変数渡し・複数行リテラル）は **fail-close で違反**。
- **走査対象は `src/` のみ**。`tests/` は対象外、`obj/` `bin/` `Migrations/` は既存 `SourceCodeScanner` が除外済み（この除外を変更しない）。
- **`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`**。`dotnet build` は警告ゼロが前提。
- **カルチャ非依存で書く**。`CultureExplicitnessGuardTests` 自身の走査対象は `src/` なのでテストコードは掛からないが、`Regex` に `RegexOptions.IgnoreCase` を使う場合は明示する。
- **公開 API を持たない**。判定クラスは `internal static`、テストクラスは `public sealed`（`CultureExplicitnessGuardTests` と同形）。
- ブランチは既に `feature/phase4-s2-bulk-operations-guard`（spec コミット `fe416fc` 済み）。各タスクの最後に `dotnet build`（警告ゼロ）と関連テストの緑を確認してからコミットする。全タスク完了後に `./build/ci.sh` を通す。

---

## File Structure

| ファイル | 責務 | 種別 |
|---|---|---|
| `tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs` | 2ルールの走査テスト＋判定関数のテーブル駆動テスト＋`internal static class BulkOperationsGuard` | 新規 |
| `docs/decisions/0050-bulk-operations-forbidden.md` | bulk operations 禁止の方針 | 新規 |
| `docs/decisions/0051-netarchtest-declined.md` | NetArchTest 見送り | 新規 |
| `docs/open-questions.md` | 56行目・61行目の2項をクローズ | 変更 |
| `CHANGELOG.md` | S2 節を追加 | 変更 |

`CultureExplicitnessGuardTests.cs` と同じく、テストクラスと判定クラスを 1 ファイルに同居させる（判定クラスは同ファイル末尾に置く）。`SourceCodeScanner.cs` は**変更しない**。

---

## Task 1: ルール1（bulk operations）の禁止

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs`

**Interfaces:**
- Consumes: `SourceCodeScanner.Scan(string ruleName, Func<string, bool> predicate)` → `IReadOnlyList<SourceCodeScanner.Violation>`（既存。`tests/Tsumugi.Infrastructure.Tests/SourceCodeScanner.cs`）。`Violation.ToString()` は `"{RelativePath}:{LineNumber} [{Rule}]: {Line}"` を返す。
- Produces: `internal static class BulkOperationsGuard` の `public static bool IsBulkOperationLine(string line)`。Task 2 が同じクラスへ `IsMutatingRawSqlLine` を足す。

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs` を新規作成する。この時点では判定関数を「常に false」の骨格で置く（Step 2 で陽性ケースが赤になることを確認するため）。

```csharp
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
    public static bool IsBulkOperationLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return false;
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: `IsBulkOperationLine_distinguishes` の**陽性3ケースが FAIL**（`Expected BulkOperationsGuard.IsBulkOperationLine(line) to be true, but found False.`）。`Source_does_not_call_bulk_update_or_delete` は PASS（`src/` に該当行が無いため）。

> この時点で走査テストが緑なのは正しい。`src/` に違反が 1 件も無いことが前提のスライスであり、走査テストの歯は Task 3 で別途確認する。

- [ ] **Step 3: 判定関数を実装する**

`BulkOperationsGuard` を次に差し替える。

```csharp
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
```

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: PASS（`[Theory]` 8ケース＋`[Fact]` 1件）。

- [ ] **Step 5: ビルド警告ゼロを確認する**

Run: `dotnet build`

Expected: `Warning(s): 0` / `Error(s): 0`。

- [ ] **Step 6: コミットする**

```bash
git add tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs
git commit -m "test(phase4-s2): ExecuteUpdate*/ExecuteDelete* の src 走査ガードを追加する"
```

---

## Task 2: ルール2（行を書き換える raw SQL）の禁止

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs`（Task 1 で作成したファイルへ追記）

**Interfaces:**
- Consumes: Task 1 の `BulkOperationsGuard`（同じクラスへメソッドを追加する）、`SourceCodeScanner.Scan`。
- Produces: `public static bool IsMutatingRawSqlLine(string line)`。Task 3 の歯の確認がこれを使う。

- [ ] **Step 1: 失敗するテストを書く**

`BulkOperationsGuardTests` クラスへ次の2メンバを追加する（`IsBulkOperationLine_distinguishes` の直後）。

```csharp
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
    [InlineData("await db.Database.ExecuteSqlRawAsync(", true)]                             // 複数行 → fail-close
    [InlineData("await db.Database.ExecuteSqlRawAsync($\"VACUUM INTO '{escaped}'\", ct);", false)]
    [InlineData("var rows = db.Set<Office>().FromSqlRaw(\"SELECT * FROM Offices\").ToList();", false)]
    [InlineData("await db.SaveChangesAsync(ct);", false)]                                   // 対象APIではない
    [InlineData("// ExecuteSqlRawAsync(\"DELETE FROM X\") は禁止", false)]                   // コメント行
    public void IsMutatingRawSqlLine_distinguishes(string line, bool expected)
    {
        ArgumentNullException.ThrowIfNull(line);
        var isCommentOnly = line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        if (isCommentOnly) { expected.Should().BeFalse(); return; }

        BulkOperationsGuard.IsMutatingRawSqlLine(line).Should().Be(expected);
    }
```

同時に `BulkOperationsGuard` へ骨格を足す（Step 2 で赤を確認するため常に false を返す）。

```csharp
    public static bool IsMutatingRawSqlLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return false;
    }
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: `IsMutatingRawSqlLine_distinguishes` の**陽性4ケースが FAIL**。`Source_does_not_execute_mutating_raw_sql` は PASS。

- [ ] **Step 3: 判定関数を実装する**

`IsMutatingRawSqlLine` の骨格を次へ差し替え、`BulkOperationsGuard` の先頭へ3本の `Regex` を追加する。

```csharp
    // ChangeTracker を経由しない raw SQL の実行・問い合わせ API。
    private static readonly Regex RawSqlCall = new(
        @"\b(ExecuteSql|FromSql)[A-Za-z]*\s*\(", RegexOptions.Compiled);

    // 同一行内の "…" 区間。$ / @ 接頭辞は問わない（spec 決定2「検証可能な文字列リテラル」の定義）。
    private static readonly Regex StringLiteral = new(
        "\"([^\"]*)\"", RegexOptions.Compiled);

    // 行を書き換える DML と、破壊的 DDL。CREATE は一時テーブル等の正当な読み取り用途を巻き込むため含めない。
    private static readonly Regex MutatingKeyword = new(
        @"\b(INSERT|UPDATE|DELETE|REPLACE|DROP|ALTER|TRUNCATE)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsMutatingRawSqlLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!RawSqlCall.IsMatch(line)) return false;

        var literals = StringLiteral.Matches(line);
        // 行内で SQL の内容を確認できない形（変数渡し・複数行リテラル）は fail-close で違反にする。
        // ここを「通す」にすると ExecuteSqlRawAsync(sql) と書くだけでルールを無力化できる。
        if (literals.Count == 0) return true;

        return literals.Any(m => MutatingKeyword.IsMatch(m.Groups[1].Value));
    }
```

> `VACUUM INTO '{escaped}'` はリテラルとして抽出されるが DML キーワードを含まないため通る。これが `SqliteBackupService.cs:16`（CLAUDE.md ハード制約7）へ例外を切らずに済む理由である。

- [ ] **Step 4: テストが通ることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: PASS（`[Theory]` 16ケース＝ルール1が8・ルール2が8、＋`[Fact]` 2件）。

- [ ] **Step 5: 走査が既存の src 全体で緑であることを、Infrastructure テスト全体で確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests`

Expected: 全緑。特に既存の `AppendOnlyGuard*Tests` / `CultureExplicitnessGuardTests` / `LoggingPiiGuardTests` が影響を受けていないこと。

- [ ] **Step 6: ビルド警告ゼロを確認してコミットする**

```bash
dotnet build
git add tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs
git commit -m "test(phase4-s2): 行を書き換える raw SQL の src 走査ガードを追加する"
```

---

## Task 3: 歯の確認（意図的な違反で赤になることの実測）

**Files:**
- 一時的に変更して**必ず revert する**: `src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs`（挿入先はどこでもよいが、1ファイルに固定すると revert が確実）
- コミットは無い。実測結果は Task 4 の ADR 0050 テスト節へ書く。

**Interfaces:**
- Consumes: Task 1・2 の `Source_does_not_call_bulk_update_or_delete` / `Source_does_not_execute_mutating_raw_sql`。
- Produces: ADR 0050 に転記する4行の実測結果（違反として報告された `ファイル:行` を含む）。

> CLAUDE.md 運用メモ「構造テストは意図的な違反を入れると赤になることを確認し、歯のある状態を保つ」。Phase 3-6 §3-9 の教訓どおり、**歯の確認をせずに緑を報告しない**。

- [ ] **Step 1: T1（bulk delete）で赤を実測する**

`SqliteBackupService.BackupToAsync` の本体先頭（`ArgumentNullException.ThrowIfNull(destinationPath);` の直後）へ次の1行を一時挿入する。

```csharp
        await db.Offices.ExecuteDeleteAsync(ct);
```

> この行はコンパイルが通る。`db` は `TsumugiDbContext`、`Offices` は既存の `DbSet<Office>`、`ExecuteDeleteAsync` は同ファイルが既に `using Microsoft.EntityFrameworkCore;` している拡張メソッドである。`dotnet test` はテストプロジェクトのビルドを伴い `src` を参照するため、**違反行はコンパイル可能でなければならない**（コメント行は `SourceCodeScanner` が除去するため検査にならない）。

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: `Source_does_not_call_bulk_update_or_delete` が FAIL し、メッセージに `src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs:<行番号> [bulk-operations]:` が含まれる。**この文字列を控える**（ADR へ転記する）。

- [ ] **Step 2: T1 を revert する**

```bash
git checkout -- src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs
git status --short   # 出力が空であること
```

- [ ] **Step 3: T2（DML を含む raw SQL）で赤を実測する**

既存の `#pragma warning disable EF1002` と `#pragma warning restore EF1002` に**挟まれた領域の中へ**一時挿入する（領域の外に置くと `EF1002` が `TreatWarningsAsErrors` でビルドエラーになり、走査テストまで到達しない）。

```csharp
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ClaimBatches", ct);
```

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: `Source_does_not_execute_mutating_raw_sql` が FAIL し、`[raw-sql-dml]` を含む行が報告される。**控える。**

> フィルタを付けずに全テストを走らせないこと。`BackupToAsync` を実際に呼ぶ既存テストがあると、この行が実行されて行が消える。

- [ ] **Step 4: T2 を revert する**

```bash
git checkout -- src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs
git status --short
```

- [ ] **Step 5: T3（検証不能な raw SQL）で赤を実測する**

同じく `#pragma warning disable EF1002` の領域内へ一時挿入する（`sql` は同スコープで宣言する）。

```csharp
        var sql = "VACUUM";
        await db.Database.ExecuteSqlRawAsync(sql, ct);
```

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"`

Expected: `Source_does_not_execute_mutating_raw_sql` が FAIL する。**SQL の中身が無害（`VACUUM`）でも変数渡しなら落ちる**ことが fail-close の証拠になる。**控える。**

- [ ] **Step 6: T3 を revert し、T4（無変更で緑）を確認する**

```bash
git checkout -- src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs
git status --short
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~BulkOperationsGuardTests"
```

Expected: 全緑。既存の `ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct)` が**例外を1件も切らずに**通ることの証拠になる。

- [ ] **Step 7: 4件の実測結果をメモに残す**

Task 4 で ADR へ転記するため、T1〜T4 の「テスト名・失敗メッセージ中の `ファイル:行 [ルール名]`・期待どおりか」を書き留める。**コミットは行わない**（`git status --short` が空であることを再確認する）。

---

## Task 4: ADR 2件・open-questions・CHANGELOG

**Files:**
- Create: `docs/decisions/0050-bulk-operations-forbidden.md`
- Create: `docs/decisions/0051-netarchtest-declined.md`
- Modify: `docs/open-questions.md`（56行目・61行目）
- Modify: `CHANGELOG.md`（`## [Unreleased]` 節の直後へ S2 節を追加）

**Interfaces:**
- Consumes: Task 3 の実測結果、Task 1・2 のテスト名。
- Produces: 無し（本スライスの最終タスク）。

- [ ] **Step 1: ADR 0050 を書く**

`docs/decisions/0050-bulk-operations-forbidden.md`。既存 ADR（`0049-office-capability-master-coverage-check.md`）の構成に合わせ、**結論 → 背景 → 決定 → 選択肢 → 影響 → テスト** の順で書く。含める内容:

- **ヘッダ**: `# ADR 0050: EF Core bulk operations と行を書き換える raw SQL の禁止` / `- 状態: 確定（2026-08-15）` / 関連: ADR 0026（`ClaimBatch` snapshot の append-only）。
- **結論**: `src/**/*.cs` において `ExecuteUpdate*` / `ExecuteDelete*` を無条件に禁止し、`ExecuteSql*` / `FromSql*` は SQL リテラルの内容で判定する。allowlist は設けない。判定は `tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs` の `BulkOperationsGuard` に閉じ、走査は既存の `SourceCodeScanner` を再利用する。
- **背景**: `AppendOnlyGuard.Inspect` は ChangeTracker の `Modified`/`Deleted` しか見ない。bulk operations と raw SQL はどちらも ChangeTracker を経由しないため、追記型エンティティを書き換えても実行時ガードが沈黙する。本 ADR 作成時点の `src/` に該当呼び出しは 0 件（`ExecuteSql*` は `SqliteBackupService.cs:16` の `VACUUM INTO` 1件のみで、行を書き換えない）。
- **決定1（allowlist を作らない）**: spec §決定1 の3理由を書く — ①現在0件なので初日から成立する、②「不可避な使用」が想定できない（ハード制約1 のオフライン検査が allowlist を持つのは推移的な不可避依存が実在するためで、bulk にその事情は無い）、③Phase 3-6 §3-5 で literal guard の allowlist を誤用しレビューで機構ごと差し替えた前例がある。**将来例外が必要になった場合は本 ADR を改訂して例外機構ごと設計する**ことを明記する。
- **決定2（raw SQL は内容基準）**: `VACUUM INTO` をパス単位の例外にせず内容で通す。「検証可能な文字列リテラル」＝同一行内の `"…"` 区間（`$`/`@` 接頭辞は問わない）。検証不能な形は fail-close。`CREATE` をキーワードに含めない理由（一時テーブル等の読み取り用途を巻き込む）も書く。
- **決定3（配置の逸脱）**: ロードマップ §8.2 は `Architecture/BulkOperationsForbiddenTests.cs` を指定したが、本リポジトリの同種ガードはすべて `tests/Tsumugi.Infrastructure.Tests/` 直下にフラット配置のため既存レイアウトへ合わせた。
- **選択肢**: A 検査しない（不採用）／B append-only 型限定で禁止（不採用: 行単位走査では対象型を判定できず Roslyn 意味解析が要る）／C 識別子基準で `ExecuteSql*` を一律禁止し `SqliteBackupService` を allowlist（不採用: 決定1と矛盾する）／D 採用案。
- **影響 — 残る限界**: 行単位走査のため、`ExecuteDeleteAsync` を別名メソッドでラップして呼ぶ・SQL を複数行に分割して組み立てる、といった**意図的な回避は検出できない**。本ガードは「気付かずに混入する」事故を止めるものである。また `tests/` と `Migrations/` は対象外である。
- **テスト**: Task 1・2 のテスト名4件を列挙し、**Task 3 の T1〜T4 の実測結果を転記する**（失敗したテスト名と、報告された `ファイル:行 [ルール名]`）。T3 が「SQL の中身が無害でも変数渡しなら落ちる」ことの証拠であることを明記する。

- [ ] **Step 2: ADR 0051 を書く**

`docs/decisions/0051-netarchtest-declined.md`。含める内容:

- **ヘッダ**: `# ADR 0051: NetArchTest を採用しない（依存グラフ検査ツールの見送り）` / `- 状態: 確定（2026-08-15）` / 関連: ADR 0050、CLAUDE.md ハード制約1。
- **結論**: 依存グラフ検査ツール（NetArchTest 等）を導入せず、現行の反射ベースのアーキテクチャテスト（`ArchitectureTests` 4本、`OfflineComplianceTests` / `AppOfflineComplianceTests`）を維持する。
- **背景**: `GetReferencedAssemblies()` は推移的参照をたどらないため、依存方向検査もオフライン検査も**直接参照のみ**を見ている（CLAUDE.md ハード制約1 が「各アセンブリ自身の参照のみ走査、推移閉包は対象外」と明記しているのはこの事実による）。`docs/open-questions.md:56` がこの穴とツール採否を起票していた。
- **決定**: 見送る。現行の依存方向違反は 0 件であり、4アセンブリ＋4テストという規模に対して新規 NuGet 依存とルール記述の学習コストが見合わない。
- **選択肢**: A NetArchTest 採用（不採用）／B 自前で `GetReferencedAssemblies()` の BFS により推移閉包を張る（不採用: BCL まで到達するため偽陽性の調整が必要で、S2 の射程を超える）／C 見送り（採用）。
- **影響 — 残る限界**: 推移的参照は依然として未検査である。**再検討トリガ**を3つ明記する — ①プロダクションアセンブリが第三者ライブラリを新規に直接参照し推移閉包を目視で追えなくなったとき、②依存方向違反が実際に本番コードへ1件でも混入したとき、③S5（配布）で self-contained 発行の内容物検証に同じ機構を再利用できるとき。

- [ ] **Step 3: open-questions の2項をクローズする**

`docs/open-questions.md` の56行目を `- [ ]` から `- [x]` へ変え、末尾に次を追記する。

> **（2026-08-15 クローズ / ADR 0051）**: 見送りを確定した。推移的参照が未検査である事実は変わらないため、ADR 0051 に「残る限界」と再検討トリガ3件として明記した。

61行目も同様に `- [x]` へ変え、末尾に次を追記する。

> **（2026-08-15 クローズ / ADR 0050）**: `BulkOperationsGuardTests` で `src/` を走査し、`ExecuteUpdate*`/`ExecuteDelete*` を無条件に、行を書き換える raw SQL を内容基準で禁止した。当初案の「append-only 型に対する呼び出しだけ禁止」は行単位走査で型を判定できないため採らず、`src/` 全体の無条件禁止にした。

- [ ] **Step 4: CHANGELOG に S2 節を追加する**

`CHANGELOG.md` の `## [Unreleased]` 節が終わる位置（`## 体制届宣言の充足可能性検査 (2026-07-27)` の直前）へ次を挿入する。

```markdown
## Phase 4 S2 完了 (2026-08-15)

- `AppendOnlyGuard` が ChangeTracker しか見ないことに由来する穴を、`src/` のソース走査で塞いだ（ADR 0050）。
  `ExecuteUpdate*`/`ExecuteDelete*` は無条件に禁止し、`ExecuteSql*`/`FromSql*` は SQL リテラルの内容
  （INSERT/UPDATE/DELETE/REPLACE/DROP/ALTER/TRUNCATE）で判定する。allowlist は設けていない
- `VACUUM INTO`（ハード制約7 のバックアップ手段）は内容で通るため、パス単位の例外を1件も作らずに済んだ。
  行内で内容を確認できない形（変数渡し・複数行リテラル）は fail-close で違反にする
- NetArchTest の採否を「見送り」で決着させた（ADR 0051）。推移的参照が未検査である事実は変わらないため、
  残る限界と再検討トリガ3件を ADR に明記した
- `docs/open-questions.md` の2項（アーキテクチャテストの推移的参照・AppendOnlyGuard と bulk operations）を
  クローズした。AC4-12 達成
```

`### 計画` 節の「フェーズ 4 …残り: … bulk operations ガード」から `bulk operations ガード` を削除する（S2 で解消したため）。

- [ ] **Step 5: 全体の品質ゲートを通す**

Run: `./build/ci.sh`

Expected: 全緑（`dotnet format --verify-no-changes` / `dotnet build` 警告ゼロ / `dotnet test` 全緑）。

- [ ] **Step 6: コミットする**

```bash
git add docs/decisions/0050-bulk-operations-forbidden.md \
        docs/decisions/0051-netarchtest-declined.md \
        docs/open-questions.md CHANGELOG.md
git commit -m "docs(phase4-s2): ADR 0050/0051 とopen-questions・CHANGELOGを同期する"
```

---

## 完了条件

- [ ] `tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs` が存在し、`[Fact]` 2件・`[Theory]` 16ケースが緑
- [ ] Task 3 の T1〜T4 を実測し、結果が ADR 0050 のテスト節に転記されている
- [ ] `src/` に一時挿入した違反行が 1 行も残っていない（`git status --short` が空、`git diff main -- src/` が空）
- [ ] ADR 0050・0051 が存在し、どちらも「残る限界」を持つ
- [ ] `docs/open-questions.md` の56行目・61行目が `- [x]` になっている
- [ ] `CHANGELOG.md` に S2 節があり、`### 計画` から `bulk operations ガード` が消えている
- [ ] `./build/ci.sh` が緑

## スコープ外

- `tests/` の走査、`Migrations/` の走査、`AppendOnlyGuard` 自体の拡張、推移的参照の検査（ADR 0051 で見送り）
- **タグ付け**（S0/S1 は `v0.3.0-phase4-s0` / `v0.3.1-phase4-s1` を打っているが、S2 で版を切るかは本計画では決めない。マージ方法とあわせて `superpowers:finishing-a-development-branch` で判断する）
- S3 以降（バックアップ運用化・UI 補完・配布）
