# ADR 0052: バックアップ運用化（終了時自動・世代管理・復元・保存先権限）

- 状態: 確定（2026-08-16）
- 関連: [ADR 0003](0003-sqlite-location-and-encryption.md)、[ADR 0014](0014-audit-trail-append-only.md)、[ADR 0050](0050-bulk-operations-forbidden.md)

## 結論

バックアップを**自動（終了時・固定保存先・世代管理・平文）**と**手動（利用者が保存先を選ぶ「控えを保存」）**の2系統に分ける。設定（自動バックアップの有効／無効・保持世代数）は持たない。保存先権限は `SecureFileSystem` へ抽出し、DB とバックアップの双方へ同じポリシーを適用する。復元は退避してから置換し、完了後にアプリを終了する。暗号化は本 ADR の対象外（S3b で別途扱う。[ADR 0003 追補](0003-sqlite-location-and-encryption.md#追補-暗号化採否の確定2026-08-16--ac4-3) を参照）。

## 背景

現状調査（2026-08-16実施）により次が判明した。

- `IBackupService` / `SqliteBackupService` / `BackupDatabaseUseCase` は実装済み・DI登録済みだが、**バックアップの UI が存在しない**（`src/Tsumugi.App` からの参照は DI 登録の1箇所のみ）。復元・世代管理も**存在しない**。
- **設定の永続化機構が存在しない**。`IOptions` / appsettings / 設定ストアのいずれも無く、`src/Tsumugi.App/Settings/` にある `AccessibilityDefaults` / `UiDefaults` はどちらも永続化しない。
- 終了フック（`App.axaml.cs` の `desktop.ShutdownRequested`）は存在するが、当初は `_appScope?.Dispose()` のみでバックアップを取らない。
- 権限適用ロジックは `SqliteLocationService` の `EnsureUnix()` / `EnsureWindows()` に私有メソッドとして埋まっており、バックアップ側から再利用できない。

**ロードマップ §8.3 の前提は誤っていた**。「`AppSettings` の `AutoBackupOnExit` / `MaxGenerations` フィールド追加」と書かれていたが、`AppSettings` という型はリポジトリに存在せず、設定を永続化する仕組み自体が無い。決定1でこの前提を破棄する。

## 決定

### 決定1: 設定を持たない（ロードマップ §8.3 の前提を破棄）

自動バックアップは**常時オン**、保持世代は**固定**（決定2）、保存先は**固定**（`<appdata>/Tsumugi/backups/`）とする。`AutoBackupOnExit` / `MaxGenerations` のような設定項目も、設定の永続化機構も新設しない。

理由は3つ。

1. **設定機構がゼロから必要になる**（背景参照）。本スライスの主目的はバックアップの運用化であって設定基盤の新設ではない。
2. **復元は DB が壊れたときに使う機能である。** 設定を DB テーブルへ置くと「壊れて読めない DB から、復元に必要な設定を読む」ことになってしまう。JSON ファイル等へ逃がせば回避はできるが、1 のコストは残る。
3. 現場に必要なのは「バックアップが確実に取られていること」であって、取らない選択肢を用意することではない。

将来、設定が本当に必要になったときは本 ADR の対象を広げず、設定基盤のスライスとして別途起こす。

### 決定2: 保持規則は「同日は最新1つ・直近7日分」

`BackupGenerationPolicy.SelectForDeletion(fileNames, asOf)`（Application の純粋関数、日付/乱数/I/Oに依存しない）で実装する。

1. ファイル名 `tsumugi-backup-YYYYMMDD-HHmmss.db` から日付と時刻を読む。命名規則に合致しないファイルは対象外（触らない）。
2. 同一 `YYYYMMDD` の中では最も新しい1つだけ残す。
3. 残った日付のうち、基準日から数えて新しい7日分だけ残す。
4. `pre-restore-` 接頭辞のファイルは対象外（決定5）。

「最新 N ファイルを残す」にしない理由は、**1日に何度も起動終了すると、その日のファイルだけで N 件が埋まり、1週間分の履歴が1日分に潰れる**ためである。同日1つ・直近7日分の規則なら、1日に何度終了しても当日分は1件に集約され、過去7日の履歴が保たれる。

### 決定3: `VACUUM INTO` の一時名→移動

一次資料（<https://www.sqlite.org/lang_vacuum.html> §2.1）は次のとおり定める。

> INTO 句で指定したファイルは既存であってはならない。空ファイルであれば可。さもなくば VACUUM INTO はエラーで失敗する。

同名バックアップファイルへの再書き込みが起こりうる（同一秒内の連続実行、あるいは既存の残骸ファイル）ため、`SqliteBackupService` は**一時名へ書いてから移動**する。既存ファイルへの直接書き込みを試みて失敗させることはしない。書き出し後に `SecureFileSystem.TryEnsureFile` で権限を締める。

同資料は「VACUUM は、実行しようとしている接続にオープン中のトランザクションがあると失敗する」とも定める。これが終了時フックの実装を規定する（保存処理の途中で `VACUUM INTO` を呼んではならない）。

### 決定4: 復元の順序（監査 → 保存 → 退避 → 置換）

`RestoreDatabaseUseCase` は次の順で実行する。

1. `AuditEntry`（`AuditAction.Backup`）を記録し、保存する。
2. 現行 DB を `pre-restore-YYYYMMDD-HHmmss.db` として backups ディレクトリへ退避する。
3. `SqliteConnection.ClearAllPools()` を呼び、`Microsoft.Data.Sqlite` の接続プールが握っているファイルハンドルを解放する。
4. バックアップを一時名へコピーし、`-wal` / `-shm` サイドカーを削除してから、一時ファイルを DB ファイルへ移動する（`SqliteRestoreService`。コピー失敗時は `finally` で一時ファイルを後始末し、現行 DB は無傷のまま残る）。

**監査を先に置く理由**: 置換すると現行 DB は定義上失われる。監査を後に書くと「復元してきた側の DB」（＝バックアップ時点の内容）へ書くことになり、「復元した」という事実がどこにも残らない。監査を先に保存しておけば、直後に取る**退避スナップショット（手順2）へ記録ごと写る**。これにより、復元の記録は現行 DB ではなく退避スナップショットの中に残る。

**手順4の順序（コピー→サイドカー削除→移動）の理由**: 現行 DB へ直接 `File.Copy(overwrite: true)` すると非原子的で、途中失敗時に壊れた DB が残る。`SqliteBackupService`（決定3）が同じ理由で一時名→移動を採っているのに、より高リスクな復元側が弱い書き方なのは筋が通らない。またサイドカー削除を置換の後に回すと、新しい中身と古い WAL が同居する窓が開く。コピー→削除→移動の順にすると、この窓が閉じ、コピー失敗時も現行 DB が無傷で残る。

監査記録には**フルパスを含めない**（CLAUDE.md ハード制約4）。自動バックアップと退避は固定ディレクトリのためファイル名だけで情報量は落ちない。手動バックアップ（「控えを保存」）の保存先は利用者が選ぶため、ファイル名もパスも記録せず「外部保存先へ手動バックアップを実行した」という事実と日時のみを記録する。

### 決定5: `pre-restore-` は世代管理の対象外

決定2の `BackupGenerationPolicy` は `pre-restore-` 接頭辞のファイルを一律スキップする。**誤った復元からの最後の綱**であるため、自動削除しない。代わりに復元を繰り返すと溜まっていくが、削除は利用者の手に委ねる（誤操作からの回復手段を、回復手段自身が自動で消してしまう事態を避ける）。

### 決定6: 復元後は再起動

置換後にアプリを終了し、再起動を促す。稼働中の `DbContext` の下でファイルを差し替えると、EF Core の内部状態・開いている接続・`ChangeTracker` が古い DB を指したまま残る。`ClearAllPools()` が閉じるのは**アイドル状態のプール接続だけ**で、他所で開きっぱなしの接続を強制的に閉じるわけではない。再起動が最も確実で、実装も検証も単純になるため、復元後の再起動は運用上の推奨ではなく、この前提を成立させるための要件とする。

### 決定7: 外部媒体では権限適用の失敗を許容

「控えを保存」は利用者が任意の保存先を選べるため、FAT32/exFAT のような Unix パーミッションも Windows ACL も適用できないファイルシステムが対象になりうる。ここで例外にすると、**「安全のための操作」が安全機構のせいで失敗する**ことになる。`SecureFileSystem.TryEnsureFile` は失敗を許容する版として用意し、権限適用に失敗しても警告のうえ処理を続行する。自動バックアップ（固定保存先・`<appdata>` 配下）は通常 NTFS/APFS 等であるため、こちらは `EnsureDirectory` / `EnsureFile`（失敗時は例外）を使う。

### 決定8: 権限適用ロジックを `SecureFileSystem` へ抽出する

`SqliteLocationService` に私有メソッドとして埋まっていた OS 別権限ロジック（Unix: ディレクトリ `0700` / ファイル `0600`。Windows: 現在ユーザーのみ DACL・継承無効）を、Infrastructure の `SecureFileSystem`（`internal static`）へ抽出した。`EnsureDirectory` / `EnsureFile` / `TryEnsureFile` の3メソッドを持ち、`SqliteLocationService` はこれを呼ぶ側に変わる。**振る舞いは変えていない**ため、既存の `SqliteLocationServiceTests` は無変更のまま回帰検出に使える。DB とバックアップの双方が同じポリシーを共有することで、保存先ごとに権限規則が食い違う事故を防ぐ。

## 選択肢

### A: 設定機構を新設し `AutoBackupOnExit` / `MaxGenerations` を持たせる（不採用）

ロードマップ §8.3 の当初案。決定1のとおり、設定機構自体が存在せず新設コストが大きいこと、復元機能が壊れた DB を前提にする以上 DB へ設定を置きにくいことから不採用。

### B: 世代管理を「最新 N ファイル保持」にする（不採用）

決定2のとおり、1日に何度も起動終了すると当日分だけで枠が埋まり、1週間分の履歴が1日分に潰れる。不採用。

### C: バックアップ先へ毎回直接 `VACUUM INTO` する（不採用）

決定3のとおり、`VACUUM INTO` は既存ファイルへ書けない（一次資料）。タイムスタンプ付きファイル名にしても同一秒内の再実行や残骸ファイルで衝突しうるため、一時名へ書いてから移動する方式を採る。

### D: 復元時に現行 DB ファイルへ直接上書きコピーする（不採用）

決定4のとおり、`File.Copy(overwrite: true)` は非原子的で、途中失敗時に壊れた DB が残る。コピー→サイドカー削除→移動の順を採る。

### E: 採用案

決定1〜8のとおり。設定なし・同日最新1つ×7日・一時名経由の書き込み／復元・監査優先・`pre-restore-` を世代管理から除外・復元後再起動・外部媒体では権限失敗を許容・権限ロジックの共通化。

## 影響

### 残る限界

1. **終了時フックの `catch (Exception)` は意図的な握り潰しである。** 「終了できないアプリ」は「バックアップされないアプリ」より悪いという判断による。失敗は画面に出ず、本アプリはログ機構を持たないため記録も残らない。
2. **バックアップ実行中に利用者がもう一度終了操作をすると、再入ガード（`_shutdownBackupDone`）により素通りする。** ガードは `await` の前に立てるため、2回目の `ShutdownRequested` は `e.Cancel` を立てずに素通りし、実行中のバックアップが中断され、`_appScope.Dispose()` が走らないまま終了しうる。「一度だけキャンセルする」パターンに内在する限界であり、これを塞ごうとすると「終了できないアプリ」のリスクと引き換えになる。
3. **広い `catch (Exception)` は `GetRequiredService` の DI 設定ミスまで握る。** バックアップ処理自体の失敗（ディスクフル・権限エラー等）と DI 構成の欠陥を区別できない。
4. **`ShutdownRequested` 経路は headless テストで検証できない。** 実画面の描画・タブ順・フォーカス移動も同様に、手動 QA でしか確認できない。
5. **起動中の定期バックアップは無い。** アプリを終了せずに使い続けると、その間はバックアップが増えない。
6. **「控えを保存」で外部媒体へ出るファイルは平文である。** 暗号化は S3b の対象（[ADR 0003 追補](0003-sqlite-location-and-encryption.md#追補-暗号化採否の確定2026-08-16--ac4-3)参照）。ADR 0003 追補の分析における「唯一の実質的な穴」（端末外へ出るコピー）は、S3a の時点では開いたままである。
7. **`ExportBackupCopyUseCase` はバックアップ全体をメモリに載せる。** B型事業所の規模では問題にならないが、DB が巨大化した場合はストリーミングへの作り替えが要る。
8. **画面のエラー文言は固定文字列で、例外の詳細を出さない。** 生のファイル I/O 例外（`File.Move` / `File.Copy` / `File.Delete`）の `.Message` にはフルパスが埋め込まれることがあり（CLAUDE.md ハード制約4）、それを画面へそのまま出すことはできない。本アプリはログ機構を持たないため詳細情報はどこにも残らず、診断性とのトレードオフになっている。
9. **`MainViewModel` が `BackupViewModel` を必須依存に持つため、接続文字列だけの `CompositionRoot.Build(string)` オーバーロードでは `MainViewModel` を解決できない。** バックアップ・復元は DB ファイルの実体パスを要するため、保存先を知る `CompositionRoot.Build(SqliteLocationService)` オーバーロードでのみ `BackupViewModel` 一式を登録する。接続文字列版は DI の部分検査（該当4箇所）専用になった。既存の接続文字列版の**シグネチャ・振る舞いは不変**である。
10. **Windows 実機での確認ができない。** 利用可能な実機は macOS のみである。DACL 適用・`ClearAllPools()` 後のファイル置換・非 NTFS 媒体での `SetAccessControl` の例外型は、自動テストとレビューで担保している。

## テスト

- `tests/Tsumugi.Infrastructure.Tests/SecureFileSystemTests.cs`（6件） — 抽出後の `EnsureDirectory` / `EnsureFile` / `TryEnsureFile` を検証。
- `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs`（無変更） — 抽出前と同じ OS 別権限テストが回帰検出として機能することを確認。
- `tests/Tsumugi.Application.Tests/Backup/BackupGenerationPolicyTests.cs`（11件） — 決定2の同日最新1つ・直近7日分・`pre-restore-` 除外・命名規則外ファイル除外をテーブル駆動で固定。
- `tests/Tsumugi.Infrastructure.Tests/SqliteBackupServiceTests.cs`（5件） / `BackupServiceTests.cs`（1件） — 一時名→移動、既存ファイルがある宛先への書き込みが失敗しないこと、書き出し後の権限締めを検証。
- `tests/Tsumugi.Infrastructure.Tests/SqliteRestoreServiceTests.cs`（3件） — コピー→サイドカー削除→移動の順序、コピー失敗時に現行 DB が無傷であること、一時ファイルの後始末を検証。
- `tests/Tsumugi.Application.Tests/Backup/RestoreDatabaseUseCaseTests.cs`（3件） — 監査→保存→退避→置換の順序、`pre-restore-` 命名、ファイル名以外（パス区切りを含む入力）を渡すと `ArgumentException` になることを検証。
- `tests/Tsumugi.Application.Tests/Backup/RunScheduledBackupUseCaseTests.cs`（3件） — バックアップ→世代削除→監査の順序を検証。
- `tests/Tsumugi.Application.Tests/Backup/ListBackupGenerationsUseCaseTests.cs`（3件） / `tests/Tsumugi.Infrastructure.Tests/BackupDirectoryServiceTests.cs`（5件） — ディレクトリ直下の列挙・削除、パス区切りを含む入力の `ArgumentException` を検証。
- `tests/Tsumugi.App.Tests/ViewModels/BackupViewModelTests.cs`（10件） — 「今すぐバックアップ」「控えを保存」「選択した世代へ復元」の3操作、復元の arm→confirm 2段階（選択変更での arm 解除を含む）、`ExportBackupCopyUseCase` の一時ファイルが保護ディレクトリ内に作られ `finally` で削除されることを検証。
- `tests/Tsumugi.App.Tests/BackupWiringTests.cs`（1件） / `CompositionRootTests.cs` — 終了時フックとDI配線（`CompositionRoot.Build(SqliteLocationService)` 経由）を検証。
