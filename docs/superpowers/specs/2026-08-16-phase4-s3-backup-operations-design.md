# Phase 4 S3 設計spec — バックアップ運用化と SQLite 暗号化の決着

> **Source**: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.3 から派生。
> **Status**: 設計合意済（2026-08-16）。
> **対応 AC**: AC4-3（暗号化 ADR 0003 決着）／AC4-4（バックアップ運用化: 終了時自動・世代・復元・保存先権限）。
> **想定サイズ**: 中大。**spec 1本 / 計画 2本**（S3a・S3b）に割る。

---

## 1. 目的

2つある。

1. **ADR 0003 が「暫定」のまま残している暗号化採否を確定する。** 一次情報（SQLCipher の設計文書・.NET パッケージの提供形態・ライセンス）と、実際の端末運用を突き合わせて決める。
2. **バックアップを「手段がある」から「運用できる」へ上げる。** 現在 `IBackupService` / `BackupDatabaseUseCase` は実装されているが**UI から到達できず**、自動実行も世代管理も復元手段も無い。

---

## 2. 現状調査（2026-08-16 実施）

### 2.1 コードベースの実測

| 対象 | 実測 |
|---|---|
| `IBackupService` / `SqliteBackupService` / `BackupDatabaseUseCase` | 実装済み・DI登録済み（`CompositionRoot.cs:40`） |
| バックアップの UI | **無い**（`src/Tsumugi.App` に `BackupDatabaseUseCase` の参照は DI 登録の1箇所のみ） |
| 復元 | **無い** |
| 世代管理 | **無い** |
| 設定の永続化機構 | **無い**（`IOptions` / appsettings / 設定ストアのいずれも存在しない。`src/Tsumugi.App/Settings/` にあるのは `AccessibilityDefaults` と `UiDefaults` で、どちらも永続化しない） |
| 終了フック | `App.axaml.cs` の `desktop.ShutdownRequested` が存在し、現在は `_appScope?.Dispose()` のみ |
| 権限適用ロジック | `SqliteLocationService.EnsureUnix()` / `EnsureWindows()` に**私有メソッドとして埋まっている**（他から再利用できない） |

**ロードマップ §8.3 の前提は誤っている**: 「`AppSettings` の `AutoBackupOnExit` / `MaxGenerations` フィールド追加」と書かれているが、`AppSettings` という型はリポジトリに存在せず、設定を永続化する仕組み自体が無い。§3 決定3 でこの前提を破棄する。

### 2.2 `VACUUM INTO` の制約（一次資料）

`https://www.sqlite.org/lang_vacuum.html` §2.1 より:

- **「INTO 句で指定したファイルは既存であってはならない。空ファイルであれば可。さもなくば VACUUM INTO はエラーで失敗する」**
- 「VACUUM は、実行しようとしている接続にオープン中のトランザクションがあると失敗する」
- 「削除済みコンテンツはバックアップから purge され、フォレンジック上の痕跡が残らない」

1点目が世代管理の実装を規定する（同名で上書きできない）。2点目が終了時フックの実装を規定する（保存処理の途中で呼んではならない）。

### 2.3 SQLCipher の一次情報

`https://www.zetetic.net/sqlcipher/design/`:

- ページ単位 AES-256、ページ毎 HMAC、鍵導出は **PBKDF2-HMAC-SHA512・既定 256,000 反復**、DB 毎に 16 バイトのランダム salt。
- **鍵はアプリケーションが供給する**（"Source key material via a secure method"）。パスフレーズ、または生のバイナリ鍵。
- SQLCipher 自身は暗号実装を持たず、外部の暗号ライブラリを使う。

`https://www.zetetic.net/sqlcipher/sqlcipher-for-dotnet/`:

- **「SQLCipher .NET パッケージは Commercial Edition ライセンス、または SQLCipher Enterprise プログラムを通じてのみ提供される」**。Commercial/Enterprise パッケージは `PRAGMA cipher_license` に有効なライセンスコードを渡す必要があり、渡さないと `SQLITE_AUTH(23)` になる。
- Zetetic は、公開されている `SQLitePCLRaw.bundle_e_sqlcipher` 等を**「当社が使用もサポートもしたことのないサードパーティ製パッケージ」**と明記している。

`https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption`:

- 無償経路は `Microsoft.Data.Sqlite` を外して `Microsoft.Data.Sqlite.Core` ＋ `SQLitePCLRaw.bundle_e_sqlcipher` を入れ、接続文字列の `Password` キーワードで鍵を渡す。
- 既存 DB の暗号化・復号は「解決策ごとに異なる。SQLCipher では `sqlcipher_export()` を使う」。

`https://www.zetetic.net/sqlcipher/open-source/`:

- Community Edition は BSD 相当。**著作権表示とライセンス全文を、利用者から見える場所に掲示する義務**がある（依存する SQLite / OpenSSL 等の告知も含む）。

### 2.4 端末運用の実測（利用者への確認、2026-08-16）

- **1台を複数職員で共有し、OS アカウントも共通。**
- **鍵の紛失は許容できない**（請求データを失うのは致命的）。

---

## 3. 決定

### 決定1: DB 本体は暗号化しない。ADR 0003 を (a)＋(c) で確定する

**共有 OS アカウント運用では、DB 本体の暗号化は職員間の分離を一切提供しない。** DB は 1 つ・鍵も 1 つなので、全職員が同じ鍵を使う（またはログインできる全員が開ける場所に鍵を置く）ことになる。既存の `0700`/`0600` が「その OS ユーザー」に対して開いているのと、保護の粒度は変わらない。

したがって暗号化が実際に守るのは**端末の外へ出たファイル**だけである。その内訳は2つ。

| 経路 | 現行の守り | 暗号化を足した場合の増分 |
|---|---|---|
| 端末そのものの盗難 | OS ディスク暗号化（FileVault / BitLocker） | ほぼ無い（鍵を OS に預ければ層が重なるだけ） |
| DB/バックアップのコピーが外部媒体へ出る | **無い** | **大きい** |

そして §2.4 のとおり鍵の紛失は許容できないため、パスフレーズ方式は選べない（忘れた時点で DB もバックアップも同じ鍵で開けなくなる。SQLCipher に復旧手段は無い）。

よって **(a) 平文＋OS ファイル権限 ＋ (c) OS ディスク暗号化への委譲** で確定する。**FileVault（macOS）／BitLocker（Windows）の有効化を運用要件**として明文化し、S5 の運用ガイドへ引き渡す。

**再評価トリガ**（ADR に書く。これが無いと「確定」が風化する）:

- 職員ごとに OS アカウントを分ける運用へ変わったとき（このとき初めて DB 暗号化が分離の意味を持つ）
- 端末を事業所外へ持ち出す運用が発生したとき
- 鍵の預け先（HSM・IC カード・組織の鍵管理）が用意でき、紛失が復旧可能になったとき

**不採用の記録**: SQLCipher を採る場合、無償経路はベンダー非サポートのサードパーティ製 bundle であり、公式 .NET パッケージは Commercial Edition ライセンスと `PRAGMA cipher_license` を要する（§2.3）。本決定では採らないため、この選択は発生しない。

### 決定2: バックアップを2系統に分け、暗号化は「持ち出す側」にだけ置く

| | 自動（終了時） | 手動（控えを保存） |
|---|---|---|
| 契機 | アプリ終了時に自動 | 利用者が明示的に実行 |
| 保存先 | `<appdata>/Tsumugi/backups/` 固定 | 利用者がダイアログで選ぶ（外部媒体を想定） |
| 暗号化 | 無し | **任意**（パスフレーズを入力したときだけ） |
| 世代管理 | あり（決定4） | 無し |
| 権限 | ディレクトリ 0700 / ファイル 0600（Windows は現在ユーザーのみ DACL） | 同じポリシーを適用。ファイルシステムが対応しない場合は**警告して続行**（外部媒体は FAT32/exFAT のことがある） |

自動側を暗号化しない理由は2つ。無人実行なのでパスフレーズを問えないこと、保護ディレクトリ内に留まるので決定1の分析上の増分が無いこと。

### 決定3: バックアップの設定を持たない（ロードマップ §8.3 の前提を破棄）

自動バックアップは**常時オン**、保持世代は**固定**、保存先は**固定**とする。`AutoBackupOnExit` / `MaxGenerations` のような設定項目も、設定の永続化機構も作らない。理由は3つ。

1. **設定機構がゼロから必要になる**（§2.1）。S3 の主目的はバックアップの運用化であって設定基盤の新設ではない。
2. **復元は DB が壊れたときに使う機能**である。設定を DB テーブルに置くと「壊れて読めない DB から、復元に必要な設定を読む」ことになる。JSON ファイルにすれば回避できるが、1 のコストは残る。
3. 現場に必要なのは「バックアップが確実に取られていること」であって、取らない選択肢ではない。

将来設定が必要になったときは、本 spec ではなく設定基盤のスライスとして起こす。

### 決定4: 保持規則は「同日は最新1つ・直近7日分」

`BackupGenerationPolicy` を Application の**純粋関数**として置く。

```
SelectForDeletion(既存ファイル名の集合, 基準日) → 削除するファイル名の集合
```

規則:

1. ファイル名 `tsumugi-backup-YYYYMMDD-HHmmss.db` から日付と時刻を読む。命名規則に合致しないファイルは**対象外**（触らない）。
2. 同一 `YYYYMMDD` の中では**最も新しい 1 つだけ残す**。
3. 残った日付のうち、基準日から数えて**新しい 7 日分だけ残す**。
4. `pre-restore-` 接頭辞のファイルは**対象外**（決定5）。

「最新 N ファイルを残す」にしない理由: 1 日に何度も起動終了した日があると、その日のファイルだけで N 件が埋まり、**1 週間分の履歴が 1 日分に潰れる**。

日付は引数で受け取り、関数は `DateTime.Now` に依存しない（CLAUDE.md「純粋関数は日付/乱数/I/O に依存しない」）。

### 決定5: 復元は退避してから置換し、完了後にアプリを終了する

`RestoreDatabaseUseCase` の手順:

1. **AuditEntry を記録し、保存する。**
2. 現行 DB を `pre-restore-YYYYMMDD-HHmmss.db` として backups ディレクトリへ退避する。**この接頭辞は世代管理の対象外**とし、自動削除しない（誤った復元からの最後の綱であるため。溜まった分の削除は利用者の手に委ねる）。
3. `SqliteConnection.ClearAllPools()` を呼び、`Microsoft.Data.Sqlite` の接続プールが握っているファイルハンドルを解放する。
4. バックアップを一時名へコピーし、`-wal` / `-shm` サイドカーを削除してから、一時ファイルを DB ファイルへ移動する。

置換後にアプリを終了し、再起動を促す。

**1 が先である理由（実装計画作成時に訂正、2026-08-16）**: 置換すると現行 DB は失われるため、監査を後に書くと**復元してきた側の DB**（＝バックアップ時点の内容）へ書くことになり、「復元した」という事実がどこにも残らない。監査を先に保存しておけば、直後に取る**退避スナップショットへ記録ごと写る**。当初この節は監査を置換の後に置いていたが、それでは目的を達しない。

**4 の順序（コピー → サイドカー削除 → 移動）の理由（Task 5 レビューを受けた訂正、2026-08-16）**: 現行 DB へ直接 `File.Copy(overwrite: true)` すると非原子的で、途中で失敗したとき壊れた DB が残る。`SqliteBackupService` が同じ理由で一時名→移動を採っているのに、より高リスクな復元側が弱い書き方なのは筋が通らない。またサイドカー削除を置換の後に回すと、**新しい中身と古い WAL が同居する窓**が開く。コピー → 削除 → 移動の順にすると、この窓が閉じ、コピー失敗時も現行 DB が無傷で残る。

**再起動を促す理由**: 稼働中の `DbContext` の下でファイルを差し替えると、EF Core の内部状態・開いている接続・ChangeTracker が古い DB を指したまま残る。再起動が最も確実で、実装も検証も単純になる。なお `ClearAllPools()` が閉じるのは**アイドル状態のプール接続だけ**で、他所で開きっぱなしの接続を強制的に閉じはしない。復元後の再起動は運用上の推奨ではなく、この前提を成立させるための要件である。

**監査記録にフルパスを書かない**（CLAUDE.md ハード制約4）。自動バックアップと退避は固定ディレクトリなのでファイル名だけで情報量は落ちない。手動バックアップの保存先は利用者が選ぶため、**ファイル名もパスも記録せず**「外部保存先へ手動バックアップを実行した」という事実と日時のみを記録する。

### 決定6: 権限適用ロジックを `SecureFileSystem` へ抽出する

`SqliteLocationService` に私有メソッドとして埋まっている OS 別権限ロジックを、Infrastructure の `SecureFileSystem` へ抽出する。

```
SecureFileSystem.EnsureDirectory(string path)   // Unix 0700 / Windows: 現在ユーザーのみ・継承無効
SecureFileSystem.EnsureFile(string path)        // Unix 0600 / Windows: 現在ユーザーのみ・継承無効
TryEnsureFile(string path) → bool               // 失敗を許容する版（外部媒体向け）
```

`SqliteLocationService` はこれを呼ぶ側に変わる。**振る舞いは変えない**ため、既存の `SqliteLocationServiceTests` がそのまま回帰検出に効く。

`TryEnsureFile` を分ける理由: 外部媒体は FAT32/exFAT のことがあり、Unix パーミッションも Windows ACL も適用できない。ここで例外にすると、**バックアップという「安全のための操作」が安全機構のせいで失敗する**。警告を返して続行する。

### 決定7: 暗号化バックアップの形式

- **AES-256-GCM 単発**（.NET の `AesGcm`）。
- 鍵導出は **PBKDF2-HMAC-SHA512・256,000 反復・16 バイトのランダム salt**。反復数と PRF は SQLCipher が公表している既定値（§2.3）に合わせる。自前で数字を決めない。
- ファイル形式: `マジック(8B) | 版(1B) | salt(16B) | nonce(12B) | tag(16B) | 暗号文`。
- 単発 GCM のため全体をメモリに載せる。**512MB を超える DB は明示的にエラー**にし、その旨を利用者に伝える（B型事業所の規模では到達しない。到達したらチャンク化を別スライスで設計する）。
- 復号は本アプリの復元画面から行う。**他のツールで開くことはできない**ことを運用ガイドへ書く。

---

## 4. 実装

### 4.1 S3a — バックアップ運用化（計画1本目）

| 層 | ファイル | 内容 |
|---|---|---|
| Application | `Abstractions/IBackupService.cs` | **既存の `BackupToAsync(destinationPath, ct)` のまま変えない**。これは「1ファイルを書き出す」プリミティブであり、保存先の決定・世代削除・監査は UseCase 側が持つ（Infrastructure を賢くしない） |
| Application | `Abstractions/IDatabaseFileLocation.cs`（新規） | `DatabasePath` / `BackupDirectory`（`<appdata>/Tsumugi/backups/`）を持つ。保存先の知識は保存先を知る型に置く。Infrastructure の `ISqliteLocation.cs` とは別で、`SqliteLocationService` が両方を実装する |
| Application | `Logic/BackupGenerationPolicy.cs`（新規） | 決定4の純粋関数 |
| Application | `UseCases/RunScheduledBackupUseCase.cs`（新規） | 終了時に呼ぶ。バックアップ→世代削除→監査 |
| Application | `UseCases/RestoreDatabaseUseCase.cs`（新規） | 決定5 |
| Infrastructure | `Persistence/SecureFileSystem.cs`（新規） | 決定6 |
| Infrastructure | `Persistence/SqliteLocationService.cs` | 権限ロジックを `SecureFileSystem` へ委譲。`BackupDirectory` を公開 |
| Infrastructure | `Persistence/SqliteBackupService.cs` | 一時名→移動、既存ファイル衝突の回避（§2.2）、権限適用 |
| App | `App.axaml.cs` | `ShutdownRequested` で `RunScheduledBackupUseCase` を呼ぶ |
| App | `ViewModels/BackupViewModel.cs`＋`Views/BackupView.axaml`（新規） | 下記3操作＋世代一覧の表示 |

UI の3操作は保存先で分かれる。

| 操作 | 保存先 | 世代管理 |
|---|---|---|
| **今すぐバックアップ** | 固定 backups ディレクトリ（終了時自動と同じ経路を手動で叩くだけ） | あり |
| **控えを保存** | 利用者がダイアログで選ぶ（外部媒体想定）。S3b でパスフレーズ任意 | 無し |
| **復元** | backups ディレクトリの世代一覧から選ぶ。**外部ファイルピッカーは無い**（実装との差異。下記の穴を参照） | — |

**既知の穴**: 「控えを保存」で外部媒体へ出したファイルを画面から戻す手段が無い。復旧するには利用者が `<appdata>/Tsumugi/backups/` へファイルを手でコピーする必要があるが、そのパスはハード制約4（ログ・画面にフルパスを出さない）により画面のどこにも表示されない。ADR 0052 の残る限界へも記録した。S3b または S5（運用ガイド）で扱う。

`BackupGenerationPolicy` を Application に置く理由: Domain は業務ドメイン（報酬算定・工賃）の純粋ロジックの場であり、ファイル名の保持規則はアプリケーションの都合であるため。純粋関数である点は変わらない。

### 4.2 S3b — 暗号化バックアップ（計画2本目）

| 層 | ファイル | 内容 |
|---|---|---|
| Application | `Abstractions/IBackupEncryptionService.cs`（新規） | `EncryptAsync` / `DecryptAsync` |
| Infrastructure | `Persistence/AesBackupEncryptionService.cs`（新規） | 決定7 |
| App | `BackupViewModel` | 「控えを保存」にパスフレーズ入力（任意）を足す。復元でパスフレーズを問う |

S3a だけで AC4-3／AC4-4 は満たせる。S3b は独立して後回しにできる。

---

## 5. テスト

- `BackupGenerationPolicy`: テーブル駆動。同日複数・7日境界・命名規則外のファイル・`pre-restore-` 接頭辞・空集合。
- 復元の往復: 一時ディレクトリで実 SQLite に対して バックアップ→書き換え→復元→内容一致 を検証。`-wal`/`-shm` の削除も確認する。
- 権限: OS 別に `[Fact]` を立て、該当 OS 以外は早期 `return`（既存 `SqliteLocationServiceTests` の方式に合わせる。xUnit 2.x のため `Skip.If` は使わない）。backups ディレクトリと生成されたバックアップファイルの両方を検査する。
- `VACUUM INTO` の既存ファイル衝突: 既存ファイルがある状態でバックアップを実行し、**エラーにならず**新しい世代が作られることを検証する（§2.2 の制約に実装が対応していることの証拠）。
- 終了時フック: ViewModel／UseCase レベルで、`ShutdownRequested` 相当の呼び出しがバックアップと世代削除を1回ずつ行うことを検証する。
- 暗号化（S3b）: 往復（暗号化→復号→バイト一致）、誤ったパスフレーズで復号が失敗すること、tag 改竄で失敗すること、512MB 超で明示エラーになること。

---

## 6. 文書

- `docs/decisions/0003-sqlite-location-and-encryption.md` を**改訂**し、暗号化採否を確定へ（決定1）。再評価トリガを明記する。
- 新規 ADR: バックアップ運用化（決定2〜6。番号は着手時の空き番号。現在の最大は 0051）。
- 新規 ADR: 暗号化バックアップの形式（決定7。S3b で作る）。
- `docs/open-questions.md` の暗号化採否項をクローズする。
- `CHANGELOG.md` に S3 節。「本番投入前に必須の deferred」から「SQLite 暗号化方針の決定」「バックアップ自動化」を削除する。
- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` の S3 行・AC4-3／AC4-4 行を完了へ。
- **NOTICE への追記は不要**（SQLCipher を採らないため、§2.3 のライセンス掲示義務は発生しない）。

---

## 7. スコープ外

- 設定の永続化機構（決定3）。
- DB 本体の暗号化（決定1）。
- 世代の外部媒体への自動同期・スケジュール実行（起動中の定期バックアップ）。終了時のみとする。
- 運用ガイド本体（S5）。本 spec は「FileVault 有効化を運用要件とする」ことを決めるだけで、文書は S5 が書く。
- Windows 実機での確認。**利用可能な実機は macOS のみ**であり、Windows 固有経路（DACL・`ClearAllPools` 後のファイル置換）は自動テストとコードレビューで担保し、実機確認は S5 の手動 QA へ送る（そこでも Windows 実機は無いため、未確認項目として残る）。

---

## 8. 参照

- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.3（親。AC4-3／AC4-4）
- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md`
- `docs/decisions/0003-sqlite-location-and-encryption.md`（改訂対象）
- SQLite `VACUUM`: https://www.sqlite.org/lang_vacuum.html
- SQLCipher Design: https://www.zetetic.net/sqlcipher/design/
- SQLCipher for .NET: https://www.zetetic.net/sqlcipher/sqlcipher-for-dotnet/
- SQLCipher Community Edition ライセンス: https://www.zetetic.net/sqlcipher/open-source/
- Microsoft.Data.Sqlite 暗号化: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption
