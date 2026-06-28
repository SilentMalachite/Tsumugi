# Minor Cleanup: YearMonth 等号オペレータ + WageStatement 重複テスト整理 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Codex review 後の SDD レビューで Minor として記録された 2 件のテスト品質改善を 1 コミットで適用する。

- **#2**: `YearMonthTests.Comparison_operators_match_chronological_order` が `<`/`>` の strict ケースのみアサートし、`<=`/`>=` の等号ケース (`a <= a`, `a >= a`) を直接アサートしていない。等号アサートを 2 行追加して operator 実装の `<= 0` / `>= 0` 分岐を直接覆う。
- **#4**: `WageStatementTests.Negative_amount_throws` (旧) が新規追加した `NewRecord_throws_when_amount_is_negative` (H-4 Task 3) と完全に重複している。旧テストを削除し、テスト一覧を `WithParameterName` 付きの新版 1 本に統一する。

両方とも `tests/Tsumugi.Domain.Tests/` 内の純テスト変更で、production には触らない。

**Architecture:** 純粋なテスト整理。挙動変更なし。

**Tech Stack:** .NET 10 / C# / xUnit / FluentAssertions

## Global Constraints

- 依存方向: `tests/Tsumugi.Domain.Tests/` のみ変更。production 不変。
- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — 0 warnings。
- 既存テストの本質ロジックは破壊しない（#2 は assertion 追加、#4 はメソッド削除のみ）。
- `dotnet format --verify-no-changes` 緑、`dotnet build -warnaserror` 緑、`dotnet test` 全緑を commit 前に確認。
- 1 コミット = 2 ファイル変更（YearMonthTests + WageStatementTests）。両者はテスト品質改善 という単一論理変更。

## File Structure

| Change | File |
|---|---|
| #2 add equal-case assertions | `tests/Tsumugi.Domain.Tests/YearMonthTests.cs` (`Comparison_operators_match_chronological_order` メソッド内) |
| #4 delete duplicate negative-amount test | `tests/Tsumugi.Domain.Tests/WageStatementTests.cs` (旧 `Negative_amount_throws` メソッド全体) |

## 仕様根拠

- Ledger 記録 (`.superpowers/sdd/progress.md`):
  - H-4 Task 4 minor: "operators <= and >= equal-case not directly asserted in new test; existing `Comparable_ordering` covers `CompareTo==0` so risk low."
  - H-4 Task 3 minor: "pre-existing `Negative_amount_throws` overlaps with new precise test; harmless."
- 採用方針: 等号アサート追加で 100% カバレッジ・redundancy 解消で test suite 簡潔化。

---

### Task 1: 2 件の Minor を 1 コミットで適用

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/YearMonthTests.cs` (`Comparison_operators_match_chronological_order` 内に 2 アサート追加)
- Modify: `tests/Tsumugi.Domain.Tests/WageStatementTests.cs` (`Negative_amount_throws` メソッドを削除)

**Interfaces:**
- Consumes: 既存テストインフラ・ヘルパ
- Produces: 既存テスト数が #4 で 1 件減、#2 では数は変わらないが内部アサート 2 件追加。

- [ ] **Step 1.1: `YearMonthTests.Comparison_operators_match_chronological_order` に等号アサートを 2 行追加**

ファイル `tests/Tsumugi.Domain.Tests/YearMonthTests.cs` の `Comparison_operators_match_chronological_order` メソッド本文を読み、既存の `(a <= b).Should().BeTrue();` 行と `(b >= b).Should().BeTrue();` 行の関係を確認する。

その上で、**メソッド内の既存 strict 比較アサート群の中に** 以下 2 行を追加する（既に `(a <= a).Should().BeTrue()` や `(b >= b).Should().BeTrue()` が存在すれば本ステップは不要 → そのまま GREEN）。**未存在の場合のみ**追加:

```csharp
        (a <= a).Should().BeTrue();   // 等号: <= の 0-equality 分岐を直接覆う
        (a >= a).Should().BeTrue();   // 等号: >= の 0-equality 分岐を直接覆う
```

挿入位置: 既存の `(a <= b).Should().BeTrue();` 行の **直後** が自然。

**注意**: もし既存テストに `(b >= b).Should().BeTrue()` のような同一インスタンスの reflexive アサートが既に含まれていれば、そちらは `>=` の equality を覆っているので、追加は不足分のみで OK。RC2 確認: H-4 Task 4 reviewer report ("the operator test only probes the strictly-less / strictly-greater side of `<=` and `>=`") を信頼し、両方とも未存在と仮定。実ファイル読解結果に応じて調整。

- [ ] **Step 1.2: `WageStatementTests.Negative_amount_throws` を削除**

ファイル `tests/Tsumugi.Domain.Tests/WageStatementTests.cs` を読み、旧 `Negative_amount_throws` メソッド全体（`[Fact]` 属性 + メソッド本体 + 閉じ `}`）を削除する。

このメソッドは H-4 Task 3 で追加された `NewRecord_throws_when_amount_is_negative` (line ~?) と同じ入力 (`amountYen: -1`) を同じ guard (`ArgumentOutOfRangeException.ThrowIfNegative`) でテストするが、`WithParameterName` を持たない劣化版。新版が同等以上のカバレッジを提供するので旧版は安全に削除可能。

削除後、ファイル内に空行が連続する場合は 1 行に整形する。

- [ ] **Step 1.3: ビルド・format・focused test**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~YearMonthTests|FullyQualifiedName~WageStatementTests"
```

Expected:
- ビルド 0 warnings、format clean。
- YearMonthTests: 既存件数のまま全 PASS（assertion 追加だけ）。
- WageStatementTests: 件数が 1 減ったまま全 PASS（重複削除）。

- [ ] **Step 1.4: ソリューション全体テスト**

```bash
dotnet test
```

Expected: 全 PASS（前 baseline 470 から `Negative_amount_throws` 削除分の 1 件減で 469、または同等数）。

- [ ] **Step 1.5: コミット**

```bash
git add tests/Tsumugi.Domain.Tests/YearMonthTests.cs \
        tests/Tsumugi.Domain.Tests/WageStatementTests.cs
git commit -m "test(phase2): cleanup - YearMonth equality assertions + drop duplicate WageStatement test"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- Ledger Minor #2 (YearMonth equality)・#4 (WageStatement duplicate) を解消。
- #1 (culture) は `InvariantGlobalization=true` で実質 mitigated 済み（ledger 確認） → fix 不要。
- #3 (null-guard fact 集約) は cosmetic のみ → スキップ。
- #5 (ComboBox `x:DataType`) はプロジェクト全体方針の話 → スコープ外。

**2. Placeholder scan:** TBD/TODO なし。アサート 2 行は具体、削除対象も具体。

**3. Type consistency:**
- `YearMonth` 演算子 `<=`/`>=` は `CompareTo(other) <= 0`/`>= 0` で実装 (`src/Tsumugi.Domain/ValueObjects/YearMonth.cs:37-38`)。`(a <= a)` は `CompareTo == 0` 経由で true。アサート真理値と整合。
- `WageStatement.NewRecord` 第 5 引数 `amountYen` の guard は `ArgumentOutOfRangeException.ThrowIfNegative` (`src/Tsumugi.Domain/Entities/WageStatement.cs:20`) — H-4 Task 3 で `NewRecord_throws_when_amount_is_negative` がより厳密にアサート済み（`WithParameterName("amountYen")`）。旧テスト削除でカバレッジ低下なし。

**4. 影響範囲:**
- Production 完全に不変。
- テスト数 1 件減（削除分のみ）。
- カバレッジは Domain.YearMonth で等号分岐の branch 完全カバー、`WageStatement` は同等以上を維持。
