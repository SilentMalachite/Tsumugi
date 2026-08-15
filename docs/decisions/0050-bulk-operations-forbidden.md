# ADR 0050: EF Core bulk operations と行を書き換える raw SQL の禁止

- 状態: 確定（2026-08-15）
- 関連: [ADR 0026](0026-claim-batch-snapshot.md)

## 結論

`src/**/*.cs` において `ExecuteUpdate*` / `ExecuteDelete*` を無条件に禁止し、`ExecuteSql*` / `FromSql*` は SQL リテラルの内容で判定する。allowlist は設けない。判定は `tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs` の `BulkOperationsGuard` に閉じ、走査は既存の `SourceCodeScanner` を再利用する。

## 背景

`AppendOnlyGuard.Inspect` は ChangeTracker の `Modified`/`Deleted` しか見ない。EF Core の bulk operations（`ExecuteUpdateAsync`/`ExecuteDeleteAsync`）と raw SQL（`ExecuteSqlRaw`/`ExecuteSqlInterpolated`等）はどちらも ChangeTracker を経由しないため、追記型エンティティ（CLAUDE.md §コーディング規約）を書き換えても実行時ガードが沈黙する。この穴は `docs/open-questions.md`（「AppendOnlyGuard と EF Core bulk operations」）に「現在の Repository 実装に bulk 呼び出しはないが、将来追加する際は別途ガードが必要」として起票されていた。

本 ADR 作成時点の `src/` に該当呼び出しは 0 件である。`ExecuteSql*` は `SqliteBackupService.cs:16` の `VACUUM INTO` 1件のみで、これは行を書き換えない（ハード制約7 のバックアップ手段）。

## 決定

### 決定1: allowlist を作らない

理由は3つ。

1. **現在0件なので初日から成立する。** 例外を許す前提そのものが存在しない状態で allowlist を用意する必要はない。
2. **「不可避な使用」が想定できない。** ハード制約1（オフライン検査）が allowlist を持つのは、サードパーティライブラリの推移的な参照など回避不能な依存が実在するためである。bulk operations と行を書き換える raw SQL にはそのような不可避性が無い — 通常の EF Core 追跡更新／追記で代替できる。
3. **前例。** Phase 3-6 §3-5（`docs/phase3-6-acceptance.md`）で literal guard の allowlist を誤用し、レビューで機構ごと差し替えた経緯がある。allowlist は「本来閉じるべき穴を運用でふさぐ」誘惑を生みやすい。

将来例外が必要になった場合は、本 ADR を改訂して例外機構ごと設計する（無断で allowlist を後付けしない）。

### 決定2: raw SQL は内容基準で判定する

`VACUUM INTO` をパス単位の例外にせず、内容で通す。判定対象は**呼び出しの第1引数**であり、行のどこかにあるリテラルではない（`"` / `$"` / `@"` で始まるかを開き括弧の直後で見る）。第1引数が文字列リテラルでない場合（変数渡し・複数行にまたがるリテラル等、内容を確認できない形）は fail-close で違反として扱う。

**当初 spec は「同一行内に現れる `"…"` 区間のいずれか」を見る定義だった。** しかし Task 2 のレビューで、次の形が素通りすることが判明した。

```csharp
await db.Database.ExecuteSqlRawAsync(sql, ct); // caller: "AdminPanel"
```

SQL 本体は変数 `sql` であり内容を検証できないにもかかわらず、行末コメントの `"AdminPanel"` というリテラルが拾われて `MutatingKeyword` に一致しないため、fail-close に入らず通過してしまう。`SourceCodeScanner.StripLineComment` は行頭が空白＋`//`の場合のみコメントを除去し、行末コメントは除去しないため、この形は実コードで到達可能である。この指摘を受け、判定基準を「呼び出しの第1引数だけを見る」定義へ改訂した。第1引数が文字列リテラルでなければ、行に無関係なリテラルが同居していても無条件で違反（fail-close）にする。

**生文字列リテラル（`"""…"""`）も検証不能な形として fail-close する（最終ブランチレビュー指摘、2026-08-15）。** 第1引数の判定に使う `FirstArgumentLiteral`（`^[$@]{0,2}"([^"]*)"`）は最初の2つの `"` を対にして正規表現を組んでいるため、`"""DELETE FROM ClaimBatches"""` のような生文字列を与えると**先頭2つの `"` が対になって空キャプチャでマッチ成功**する。`if (!literal.Success)` の fail-close に入らず、`MutatingKeyword.IsMatch("")` が `false` を返すため素通りしていた。生文字列リテラルは複数行にまたがりうるうえ区切りの `"` の数も可変（3個以上）で、行単位走査ではそもそも内容を確定できない。そこで `FirstArgumentLiteral` を試す前に `RawStringLiteralStart`（`^[$@]*"{3,}`）で開き `"""` を検出し、無条件で fail-close にする改修を加えた。**この結果、`ExecuteSqlRawAsync("""SELECT 1""")` のような無害な生文字列も違反になるが、これはバグではなく「行内で内容を確認できない形は fail-close」という本決定の方針どおりの意図した挙動である。** 現在の `src/` に生文字列の raw SQL は0件のため、fail-close にしても実害はない。

`MutatingKeyword` は `INSERT`/`UPDATE`/`DELETE`/`REPLACE`/`DROP`/`ALTER`/`TRUNCATE` を対象とし、`CREATE` は含めない。`CREATE TEMP TABLE` 等の一時テーブル作成は行の書き換えを伴わない正当な読み取り用途で使われうるため、これを一律禁止に巻き込まないためである。

### 決定3: 配置は既存レイアウトへ合わせる

ロードマップ（`07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §8.2）は `Architecture/BulkOperationsForbiddenTests.cs` という配置を指定していたが、本リポジトリの同種ガード（`ClaimSpecificationBoundaryTests`・`AppOfflineComplianceTests` 等）はすべて `tests/Tsumugi.Infrastructure.Tests/` 直下にフラット配置されているため、新規に `Architecture/` サブディレクトリを作らず既存レイアウトへ合わせた。

## 選択肢

### A: 検査しない（不採用）

現状維持。`AppendOnlyGuard` の穴が open-questions に起票されたまま放置される。不採用。

### B: append-only 型に対する呼び出しだけを禁止する（不採用）

ロードマップの元案。行単位の正規表現走査では、`ExecuteDeleteAsync` の呼び出し対象が append-only 型（`ClaimBatch`・`ClaimDetail`等）かどうかを判定できない（型解決には Roslyn の意味解析が要る）。src/ 全体を対象にした方が実装が単純で、かつ「非 append-only 型に対する bulk operations」も将来の追記型化に対して安全側に倒せる。不採用。

### C: 識別子基準で `ExecuteSql*` を一律禁止し `SqliteBackupService` を allowlist する（不採用）

内容ではなく識別子だけで判定する案。決定1（allowlist を作らない）と矛盾するため不採用。

### D: 採用案

決定1〜3のとおり。allowlist なし・内容基準・既存レイアウト踏襲。

## 影響

### 残る限界

1. **行単位走査のため、意図的な回避は検出できない。** `ExecuteDeleteAsync` を別名メソッドでラップして呼ぶ、SQL 文字列を複数の変数へ分割してから連結する、といった意図的な迂回は検出できない。本ガードは「気付かずに混入する」事故を止めるものであり、悪意ある回避を防ぐものではない。
   **`tests/` と `Migrations/` の除外理由は異なる。** `tests/` はそもそも走査範囲外である — `SourceCodeScanner.EnumerateSourceFiles()`（`SourceCodeScanner.cs:26`）が列挙する走査根はソリューションルート直下の `src` であり、`tests/` はこの列挙に一度も現れない。一方 `Migrations/` は `src` の内側にあるため明示的な除外が要る — `SourceCodeScanner.cs:32` の `if (file.Contains(...Migrations...)) continue;` が個別に弾いている。
   **ADO.NET を直接叩く経路も未カバーである。** `src/Tsumugi.Infrastructure/Persistence/ClaimCalculationSnapshotReader.cs` と `ClaimFinalizationStore.cs` は既に `(SqliteConnection)db.Database.GetDbConnection()` で生の `SqliteConnection` を取得している。ここから `connection.CreateCommand()` で DML を撃つ経路や `Database.SqlQueryRaw<T>("DELETE … RETURNING …")` は、`RawSqlCall` が `ExecuteSql|FromSql` しか見ないため検出できない。意図的な回避ではなく素直な代替手段であり、本質的に同じ限界の一部である。もっとも**現状この2ファイルの用途は `BeginTransaction(deferred:)` のみ**で、`CreateCommand`/`CommandText`/`ExecuteNonQuery` は `src/` に0件のため、現時点でこの穴は実害なく塞がっている。
2. **リテラル内のエスケープされた二重引用符でキーワードが引用符ペアの外に落ちると見逃す。** `FirstArgumentLiteral` は `"([^"]*)"` で最初の `"..."` を単純に対にするため、`"SELECT \"DELETE ME\""` のようにエスケープされた `"` を含むリテラルでは、意図した文字列より手前で閉じたと誤認識し、キーワードが引用符ペアの外側に落ちて判定から漏れる可能性がある。現在の `src/` にこの形は存在しない（該当なし）。
3. **同一の物理行に `ExecuteSql*` 呼び出しが2つ以上あると、2つ目を見逃す。** `RawSqlCall.Match(line)` は leftmost match のみを返すため、`if (ro) …ExecuteSqlRaw("SELECT 1"); else …ExecuteSqlRaw("DELETE FROM X");` のような行では1つ目の呼び出ししか判定されず、2つ目（`DELETE FROM X`）を見逃す。現在の `src/` に `ExecuteSql*` 呼び出しは `SqliteBackupService.cs:16` の1箇所しかないため該当なし。
4. **偽陽性側の性質。** ここまでは偽陰性（見逃し）だが、逆に無害なコードが違反として弾かれる形もある。`SourceCodeScanner.StripLineComment` は**行頭が空白＋`//`の行のみ**をコメント除去の対象にするため、`src/` に次のような解説コメントを書くと違反になる。
   ```csharp
   var x = 1; // 旧実装は db.Offices.ExecuteDeleteAsync(ct) を使っていた
   var x = 1; // FromSqlRaw("DELETE FROM X") は禁止
   ```
   前者は `bulk-operations`、後者は `raw-sql-dml` の違反として報告される。allowlist を作らない設計（決定1）のため、直す手段はコメントの言い換えだけである。**将来の運用者は「allowlist を足せば黙る」とは考えず、対象 API の綴りをコメントに書かない形へ言い換えること。** 同様に、生文字列リテラル（`"""…"""`。決定2 参照）と、名前付き引数で渡した第1引数（`ExecuteSqlRawAsync(sql: "SELECT 1", ct)`）も内容を行内で確定できない形として fail-close 側に倒れ、無害でも違反になる。いずれも安全側（見逃しではなく過検知）に倒す設計判断であり、バグではない。

②③はいずれも Task 2 のレビュー（Minor 指摘）で識別されたものであり、現在の `src/` には該当する形が存在しないことを確認している。将来これらの形が現れた場合は、本 ADR を改訂して判定ロジックを拡張する。

## テスト

`tests/Tsumugi.Infrastructure.Tests/BulkOperationsGuardTests.cs` に次の4件を実装した（`[Fact]` 2件・`[Theory]` 19ケースの合計21件。最終ブランチレビューで生文字列リテラルのケースを2件追加し、`[Theory] IsMutatingRawSqlLine_distinguishes` が9ケースから11ケースへ増えた）。

- `[Fact] Source_does_not_call_bulk_update_or_delete()` — ルール名 `bulk-operations`。`src/` 全体を `BulkOperationsGuard.IsBulkOperationLine` で走査する。
- `[Fact] Source_does_not_execute_mutating_raw_sql()` — ルール名 `raw-sql-dml`。`src/` 全体を `BulkOperationsGuard.IsMutatingRawSqlLine` で走査する。
- `[Theory] IsBulkOperationLine_distinguishes` — 8ケース。
- `[Theory] IsMutatingRawSqlLine_distinguishes` — 11ケース（うち2件は生文字列リテラルの fail-close: `"""DELETE FROM ClaimBatches"""` と、生文字列の開始行のみの `"""`）。

### 歯の確認（意図的な違反を一時挿入した実測。`SqliteBackupService.cs` を対象に4回実施、いずれも revert 済み）

**T1: bulk delete（`await db.Offices.ExecuteDeleteAsync(ct);` を `BackupToAsync` 本体先頭に挿入）**

`Source_does_not_call_bulk_update_or_delete` が FAIL（他18件は合格、19件中1件失敗）。報告された文字列:

```
src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs:11 [bulk-operations]: await db.Offices.ExecuteDeleteAsync(ct);
```

**T2: DML を含む raw SQL（`await db.Database.ExecuteSqlRawAsync("DELETE FROM ClaimBatches", ct);` を既存の `VACUUM INTO` 行の直前・同じ pragma 領域内に挿入）**

`Source_does_not_execute_mutating_raw_sql` が FAIL（他18件は合格、19件中1件失敗）。報告された文字列:

```
src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs:16 [raw-sql-dml]: await db.Database.ExecuteSqlRawAsync("DELETE FROM ClaimBatches", ct);
```

同じ pragma 領域内に無害な `VACUUM INTO` 行が同居していたが、誤検知せず違反行のみが単独で報告された。

**T3: 検証不能な raw SQL（変数渡し。fail-close の証拠）**

```csharp
var sql = "VACUUM";
await db.Database.ExecuteSqlRawAsync(sql, ct);
```

を同じ pragma 領域内に挿入。`Source_does_not_execute_mutating_raw_sql` が FAIL（他18件は合格、19件中1件失敗）。報告された文字列:

```
src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs:17 [raw-sql-dml]: await db.Database.ExecuteSqlRawAsync(sql, ct);
```

**SQL の中身自体は無害（`"VACUUM"`）だが、第1引数が文字列リテラルでなく変数（`sql`）であるため `FirstArgumentLiteral` にマッチせず、fail-close 経路が発火して違反と判定された。** これは「SQL の中身が無害でも変数渡しなら落ちる」ことの実測上の証拠である。

**T4: 無変更のベースライン確認**

挿入なし。既存コードの `await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);` のみが残る状態で、19件全緑（失敗0・合格19・スキップ0）。既存の `VACUUM INTO` 呼び出しが例外を1件も切らずに通過することを確認した。

各回とも `git status --short` は空に復元し、コミットは作成していない。詳細は `.superpowers/sdd/2026-08-15-phase4-s2-bulk-operations-guard/task-3-report.md` を参照。
