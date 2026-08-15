# Phase 4 S2 設計spec — bulk operations 禁止のソース走査ガードと NetArchTest 見送り

> **Source**: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.2 から派生。
> **Status**: 設計合意済（2026-08-15）。
> **対応 AC**: AC4-12（bulk 禁止スキャナ＋NetArchTest ADR）。
> **想定サイズ**: 小（1 PR、3コミット）。ブロッカー無し。

---

## 1. 目的

`AppendOnlyGuard` が守れない経路を塞ぎ、append-only 不変条件（CLAUDE.md コーディング規約「エンティティは record ＋ 追記型」）を CI で機械担保する。

`AppendOnlyGuard.Inspect` は EF Core の `ChangeTracker` を見て `Modified` / `Deleted` を検出する。しかし `ExecuteUpdateAsync` / `ExecuteDeleteAsync` は ChangeTracker を経由せず SQL を直接発行するため、**追記型エンティティを黙って書き換えても・削除しても、ガードは一切反応しない**。同じことが `ExecuteSqlRawAsync("DELETE FROM …")` にも当てはまる。

現在の `src/` にこれらの呼び出しは 1 件も無い（本設計時に実測。§2.1）。したがって本スライスは**バグ修正ではなく、将来の混入を止めるトリップワイヤの設置**である。

あわせて、依存グラフ検査ツール NetArchTest の採否を決着させ、`docs/open-questions.md` に残る2項をクローズする。

---

## 2. 現状調査（2026-08-15 実施）

### 2.1 `src/` の実測

| 対象 | 実測 |
|---|---|
| `ExecuteUpdate` / `ExecuteUpdateAsync` | **0 件** |
| `ExecuteDelete` / `ExecuteDeleteAsync` | **0 件** |
| `ExecuteSql*` | **1 件**（`src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs:16`） |
| `FromSql*` | 0 件 |
| `PRAGMA` を含む raw SQL | 0 件 |

唯一の `ExecuteSql*` は次の行である。

```csharp
#pragma warning disable EF1002 // VACUUM INTO はパラメータ化不可。シングルクォートをエスケープして埋め込む。
await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);
```

`VACUUM INTO` は DB 全体の一貫したコピーを別ファイルへ書き出す操作であり、**元の行を1件も変更しない**。CLAUDE.md ハード制約7（バックアップ手段の維持）が要求している実装そのものなので、禁止対象にしてはならない。

### 2.2 既存の走査基盤

`tests/Tsumugi.Infrastructure.Tests/SourceCodeScanner.cs` が既に存在する。

- `EnumerateSourceFiles()` が `src/**/*.cs` を列挙し、`obj/` `bin/` `Migrations/` を除外する。
- `Scan(ruleName, predicate)` が行単位で predicate を適用し、`Violation(RelativePath, LineNumber, Line, Rule)` を返す。
- 行頭が `//` の行は空文字へ置換してから predicate へ渡す。

利用者は `CultureExplicitnessGuardTests` と `LoggingPiiGuardTests`。どちらも「テストクラス ＋ `internal static` な判定クラス」を 1 ファイルに同居させ、判定関数をテーブル駆動 `[Theory]` で単体検証する形を取る。**本スライスはこの形をそのまま踏襲し、新しい走査基盤を作らない。**

`Migrations/` の除外は本スライスにとって重要である。既存 migration には `migrationBuilder.Sql("UPDATE \"WageSettings\" SET …")` のような data fixup が3箇所あるが、これは append-only 対象の実行時経路ではなくスキーマ移行の一部であり、走査対象外が正しい。加えて `migrationBuilder.Sql(...)` は `ExecuteSql*` でも `FromSql*` でもないため、仮に除外が外れても本ガードのルール2には掛からない（二重の安全）。

### 2.3 クローズ対象の open questions

| 行 | 内容 |
|---|---|
| `docs/open-questions.md:56` | アーキテクチャ/オフラインテストは直接参照のみを検査。NetArchTest 等の採否を検討する |
| `docs/open-questions.md:61` | `AppendOnlyGuard` は bulk operations を検出できない。`ArchitectureTests` で禁止する案あり |

---

## 3. 決定

### 決定1: `src/` 全体で無条件に禁止し、allowlist を作らない

`ExecuteUpdate*` / `ExecuteDelete*` は**例外なく違反**とする。理由は3つ。

1. **現在 0 件なので、初日から違反ゼロで成立する。** 抜け道を先に用意する必要が無い。
2. **「不可避な使用」が想定できない。** CLAUDE.md ハード制約1 のオフライン検査は理由付き allowlist を持つが、あれは推移的に通信APIを参照してしまう不可避の依存が現実に存在するためである。bulk operations にその事情は無い（append-only を捨てる判断をした時にだけ必要になり、それは ADR を伴う設計変更である）。
3. **前例の教訓。** Phase 3-6 §3-5 で literal guard の allowlist を誤用し、レビューで機構ごと差し替えている。既定が空の allowlist は「そこへ足せば黙る」という誤用の入口になる。

将来どうしても必要になった場合は、**この ADR を改訂して例外機構ごと設計する**。ガードを黙らせる操作が必ず ADR の改訂を伴うようにするのが本決定の意図である。

### 決定2: raw SQL は「識別子」ではなく「内容」で判定する

`ExecuteSql*` / `FromSql*` を含む行は、**SQL リテラルの中身**を見て判定する。

```
違反 ⟺ 呼び出しの第1引数が文字列リテラルでない
      ∨ そのリテラルが DML キーワード（INSERT|UPDATE|DELETE|REPLACE|DROP|ALTER|TRUNCATE）を単語境界で含む
```

こうすると `VACUUM INTO` は**内容によって通る**ため、§2.1 の唯一の呼び出しに対してパス単位の例外を切らずに済む。決定1の「allowlist を作らない」を両ルールで貫ける。

`$"VACUUM INTO '{escaped}'"` の補間穴が保持するのは保存先パスであって SQL 命令ではないため、キーワード判定はリテラル本文（補間穴を含んだままの文字列）に対して行えばよい。

**判定対象は「呼び出しの第1引数」であり、「行のどこかにあるリテラル」ではない**（Task 2 レビュー指摘を受けた改訂、2026-08-15）。マッチした `ExecuteSql*` / `FromSql*` の開き括弧の直後を見て、`"` / `$"` / `@"` で始まっていなければ**その時点で検証不能**とする。当初は「同一行内に現れる `"…"` 区間のいずれか」を見る定義だったが、それだと次が素通りする。

```csharp
await db.Database.ExecuteSqlRawAsync(sql, ct); // caller: "AdminPanel"
```

SQL 本体は変数 `sql` で検証できないのに、行末コメントの `"AdminPanel"` がリテラルとして拾われるため「リテラルが 0 件」に該当せず、DML キーワードも含まないので合格してしまう。`SourceCodeScanner` は行頭コメントしか除去しないため、この形は実コードで到達可能である。第1引数だけを見る定義にすると、無関係なリテラルが同居しても fail-close が効く。

**検証不能な形（変数渡し・複数行にまたがるリテラル）は違反として扱う（fail-close）。** 「行内で確認できないものは通す」にすると、`ExecuteSqlRawAsync(sql)` と書くだけでルール2を無力化でき、歯の無い門番になる。現在の `src/` には検証不能な形の呼び出しが存在しないため、fail-close にしても初日から緑である。

キーワードに `CREATE` を含めないのは、一時テーブル作成のような読み取り側の正当な用途を巻き込むためである。本ガードの目的は**行を書き換える経路**を塞ぐことなので、対象を DML と破壊的 DDL に限る。

### 決定3: NetArchTest は採用しない

依存グラフ検査ツール（NetArchTest 等）を導入せず、現行の反射ベースのアーキテクチャテスト（`ArchitectureTests` 4本・`OfflineComplianceTests` / `AppOfflineComplianceTests`）を維持する。

**残る限界（ADR に明記する）**: `GetReferencedAssemblies()` は推移的参照をたどらないため、現行の依存方向検査・オフライン検査はいずれも**直接参照のみ**を見ている。本決定はこの穴を承知のうえで、ツール導入のコストに見合わないと判断するものである。

**再検討トリガ**（ADR に書く。これが無いと「見送り」が風化する）:

- プロダクションアセンブリが第三者ライブラリを新規に直接参照し、その推移閉包を目視で追えなくなったとき
- 依存方向違反が実際に1件でも本番コードへ混入したとき（現在は0件）
- 配布（S5）で self-contained 発行の内容物を検証する必要が生じ、同じ機構を再利用できるとき

決定3は決定1・2 と独立だが、どちらも「アーキテクチャ制約を CI で機械判定する」系統であり、ロードマップ §8.2 が同一スライスに束ねているため同じ PR で扱う。

### 決定4: 配置は既存レイアウトに合わせる（ロードマップからの逸脱）

ロードマップ §8.2 は `tests/Tsumugi.Infrastructure.Tests/Architecture/BulkOperationsForbiddenTests.cs` を指定するが、本リポジトリに `Architecture/` サブディレクトリは存在せず、同種のガード（`CultureExplicitnessGuardTests` / `LoggingPiiGuardTests` / `AppendOnlyGuard*Tests` / `OfflineComplianceTests`）はすべて `tests/Tsumugi.Infrastructure.Tests/` 直下にフラットに置かれている。既存レイアウトへ合わせ、**`tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs`** とする。この逸脱は ADR に理由付きで記録する。

---

## 4. 実装

### 4.1 新規ファイル 1 本

`tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs`

```
public sealed class BulkOperationsGuardTests
  ├ Source_does_not_call_bulk_update_or_delete()      … SourceCodeScanner.Scan("bulk-operations", …)
  ├ Source_does_not_execute_mutating_raw_sql()        … SourceCodeScanner.Scan("raw-sql-dml", …)
  ├ [Theory] IsBulkOperationLine_distinguishes(...)
  └ [Theory] IsMutatingRawSqlLine_distinguishes(...)

internal static class BulkOperationsGuard
  ├ IsBulkOperationLine(string line) : bool
  └ IsMutatingRawSqlLine(string line) : bool
```

判定は正規表現で行う（`CultureExplicitnessGuard` と同じ粒度）。`RegexOptions.Compiled`、カルチャ非依存で書く。

### 4.2 判定の境界（`[Theory]` で固定する組）

| 入力 | ルール1 | ルール2 |
|---|---|---|
| `db.Set<X>().ExecuteDeleteAsync(ct);` | 違反 | — |
| `db.Set<X>().ExecuteUpdate(s => s.SetProperty(...));` | 違反 | — |
| `db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);` | 非違反 | **非違反** |
| `db.Database.ExecuteSqlRawAsync("DELETE FROM ClaimBatches", ct);` | 非違反 | 違反 |
| `db.Database.ExecuteSqlRawAsync(sql, ct);` | 非違反 | **違反（検証不能）** |
| `db.Set<X>().FromSqlRaw("SELECT * FROM X");` | 非違反 | 非違反 |
| `db.Database.ExecuteSqlRawAsync(sql, ct); // caller: "AdminPanel"` | 非違反 | **違反（第1引数がリテラルでない）** |
| `// ExecuteDeleteAsync は禁止` | 非違反（基盤が除去） | 非違反 |
| `var name = nameof(ExecuteDeleteMarker);` | 非違反（`.` 前置を要求） | — |

最終行は誤検出の境界である。ルール1 は `.ExecuteDelete` のように**メソッド呼び出しの形**（前置ドット＋開き括弧）を要求し、単なる識別子の出現では反応しない。

### 4.3 コミット粒度

1. **Red**: `BulkOperationsGuardTests` を追加し、判定関数を「常に false を返す」骨格で置いて `[Theory]` の陽性ケースが赤になることを確認する。
2. **Green**: 判定関数を実装し、`[Theory]` 全件と `src/` 走査2本を緑にする。
3. **文書**: ADR 2件、`docs/open-questions.md` 2項クローズ、`CHANGELOG.md`。

ブランチ: `feature/phase4-s2-bulk-operations-guard`。

---

## 5. 歯の確認（CLAUDE.md 運用メモ）

「意図的な違反を入れると赤になる」ことを**両ルールについて実測**し、証跡を ADR のテスト節へ記録する。

| # | 挿入する違反 | 期待 |
|---|---|---|
| T1 | `src/` の任意の Repository へ `await db.Set<ClaimBatch>().ExecuteDeleteAsync(ct);` を一時追加 | `Source_does_not_call_bulk_update_or_delete` が赤（ファイル:行 付きで報告） |
| T2 | 同じ場所へ `await db.Database.ExecuteSqlRawAsync("DELETE FROM ClaimBatches", ct);` を一時追加 | `Source_does_not_execute_mutating_raw_sql` が赤 |
| T3 | 同じ場所へ `await db.Database.ExecuteSqlRawAsync(sql, ct);` を一時追加 | `Source_does_not_execute_mutating_raw_sql` が赤（fail-close が効いている証拠） |
| T4 | `SqliteBackupService.cs` を無変更のまま全テスト実行 | 緑（VACUUM INTO が内容で通る証拠） |

T1〜T3 は確認後に必ず revert する（コミットしない）。T4 は通常の緑実行そのものである。

---

## 6. 文書の更新

### 6.1 ADR 0050（bulk operations 禁止）

結論 → 背景（`AppendOnlyGuard` の穴）→ 決定1・2・4 → 選択肢（allowlist あり／append-only 型限定／識別子基準の raw SQL 禁止、いずれも不採用の理由）→ 影響（残る限界・歯の確認の実測結果）。

**残る限界として明記する**: 行単位走査であるため、`ExecuteDeleteAsync` を別名でラップして呼ぶ・SQL を複数行に分割して組み立てる、といった回避は検出できない。本ガードは「気付かずに混入する」事故を止めるものであり、意図的な回避を防ぐものではない。

### 6.2 ADR 0051（NetArchTest 見送り）

結論（採用しない）→ 背景（推移的参照の穴）→ 決定3 → 選択肢（採用／自前で推移閉包を張る／見送り）→ 影響（残る限界＋再検討トリガ）。

自前で推移閉包を張る案を不採用にする理由も書く: BCL まで到達するため偽陽性の調整が必要で、S2 の射程（1 PR）を超える。

### 6.3 open-questions

56行目・61行目の2項を、それぞれ ADR 0051・ADR 0050 を参照してクローズする（既存の記法どおり `- [x]` へ変更し、クローズ日と ADR 番号を追記する）。

### 6.4 CHANGELOG

`## Phase 4 S2 完了 (2026-08-15)` 節を追加する。S0/S1 は個別の acceptance doc を持たず CHANGELOG の版セクションで証跡を残しているため、**S2 も専用の acceptance doc を作らない**。歯の確認の実測結果は ADR 0050 のテスト節が持つ。

---

## 7. スコープ外

- **`tests/` の走査**: 対象は `src/` のみ（既存 `SourceCodeScanner` の契約どおり）。テストコードが bulk operations でフィクスチャを掃除することは禁止しない。
- **`Migrations/` の走査**: §2.2 のとおり除外を維持する。
- **`AppendOnlyGuard` 自体の拡張**: ChangeTracker を見る実行時ガードは変更しない。本スライスは静的検査だけを足す。
- **推移的参照の検査**: 決定3のとおり見送る。
- **S3 以降**（バックアップ運用化・UI 補完・配布）。

---

## 8. 参照

- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.2（親。AC4-12）
- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md`
- `tests/Tsumugi.Infrastructure.Tests/SourceCodeScanner.cs`（再利用する基盤）
- `tests/Tsumugi.Infrastructure.Tests/CultureExplicitnessGuardTests.cs`（踏襲する形）
- `docs/phase3-6-acceptance.md` §3-5（allowlist 誤用の前例）、§3-9（歯の確認の作り方）
