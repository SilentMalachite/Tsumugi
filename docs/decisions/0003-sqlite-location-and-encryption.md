# ADR 0003: SQLite 保存先とファイル権限・暗号化採否

- 結論: DBは `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` 配下の `Tsumugi/tsumugi.db` に保存する。実体パスは .NET SDK 版・OS・環境変数（例: macOS の `XDG_CONFIG_HOME`）に依存し、macOS では `~/Library/Application Support/Tsumugi/tsumugi.db`（.NET 10 既定）または `~/.config/Tsumugi/tsumugi.db`、Windows では `%APPDATA%\Tsumugi\tsumugi.db` となる（追補の補足ノート参照）。暗号化はフェーズ0では未採用（要再評価）。
- 背景: 個人情報を扱う（§2.2, §6-6）。完全オフライン端末運用前提。
- 選択肢: (a) 平文+OSファイル権限 / (b) SQLCipher / (c) OS全体ディスク暗号化に委譲。
- 決定: 暫定 (a)+(c)。保存先は `Environment.SpecialFolder.ApplicationData` で解決（実装済み）。SQLCipher 採否は運用端末の要件確定後に再判断（open-questions に残す）。
- 影響:
  - 保存先パス解決を合成ルートに集約済み。
  - ログに個人情報を出さない実装規約を維持。
  - macOS での `ApplicationData` 実解決は .NET SDK 版によって `~/Library/Application Support/Tsumugi`（.NET 10 既定）あるいは `~/.config/Tsumugi` に振れる。前者は macOS ネイティブ慣例、後者は XDG 慣例。どちらの実体パスでも本追補の権限ポリシー（ディレクトリ 0700 / ファイル 0600 ／ Windows DACL）が適用されるため機能には影響しない。`SpecialFolder.LocalApplicationData` への切り替え可否は open-questions として残す。

## 追補: 権限ポリシー（2026-06-26 / AC0-6）

- **Unix (Linux/macOS)**: ディレクトリ `0700`、DBファイル `0600`。`Directory.CreateDirectory(path, UnixFileMode)` ＋ `File.SetUnixFileMode` で**作成時に強制**。WAL/SHM サイドカーはディレクトリ権限で保護される。
- **Windows**: 現在ユーザー (`WindowsIdentity.GetCurrent().User`) のみフルコントロール。`DirectorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false)` で**継承無効・継承 ACE 除去**。同等のポリシーを DBファイルにも適用。Windows 専用 API は `[SupportedOSPlatform("windows")]` でガードする。
- **既存ゆるい権限の扱い**: 起動時に**冪等に締め直す**（広げない・狭めるのみ）。理由: 単一ユーザーデスクトップアプリで保存先はユーザー自身の `ApplicationData` 配下である／毎回失敗にすると業務継続が阻害される／締め直しはユーザー意図を破壊しない。所有者検証ポリシー（マルチユーザー乗っ取り対策）はフェーズ1以降で追加検討。
- **検証**: `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs` で OS 別に `[Fact]` を立て、該当 OS 以外は早期 `return` でスキップ（xUnit 2.x のため `Skip.If` は使わない）。
- **実装場所**: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`（Infrastructure 層）。App は合成ルートでサービスを 1 回構築し `EnsureSecuredStorage()` を呼ぶのみ。

暗号化（SQLCipher 等）採否は本追補の対象外（open-questions.md 引き続き）。

> 補足: 2026-06-26 の .NET 10 macOS 環境で `Environment.SpecialFolder.ApplicationData` の実解決は `~/Library/Application Support/Tsumugi/` となることを smoke test で確認した（ADR 本文の `~/.config/Tsumugi/` 記述は .NET 7 当時の挙動。環境変数 `XDG_CONFIG_HOME` の有無や SDK 版で揺れる。実体パスがどちらでも、本追補の権限ポリシー（ディレクトリ 0700 / ファイル 0600）は適用される）。

## 追補: 暗号化採否の確定（2026-08-16 / AC4-3）

### 結論

**(a) 平文＋OS ファイル権限 ＋ (c) OS ディスク暗号化への委譲**で確定する。SQLCipher（選択肢 (b)）は採らない。ADR本文・上記追補の暫定判断（「要再評価」「引き続き open-questions」）はこれで解消する。

### 判断の根拠となった運用実測

2026-08-16 に利用者へ確認した。

- **1台を複数職員で共有し、OS アカウントも共通**である。
- **鍵の紛失は許容できない**（請求データを失うことは致命的である）。

### 構造的な理由

**共有 OS アカウント運用では、DB 本体の暗号化は職員間の分離を一切提供しない。** DB は 1 つ・鍵も 1 つなので、全職員が同じ鍵を使う（あるいはログインできる全員が開ける場所に鍵を置く）ことになる。これは既存の `0700`/`0600`（追補「権限ポリシー」参照）が「その OS ユーザー」に対して開いているのと保護の粒度が変わらない。

したがって暗号化が実際に守れるのは**端末の外へ出たファイル**（DB／バックアップのコピーが外部媒体へ渡る経路）だけであり、端末そのものの盗難は元々 OS ディスク暗号化（FileVault／BitLocker）の役割で、DB 暗号化を足しても増分はほぼ無い。

加えて、鍵の紛失が許容できないためパスフレーズ方式は選べない。パスフレーズを忘れた時点で DB もバックアップも同じ鍵で開けなくなり、**SQLCipher に復旧手段は無い**（§一次情報参照）。

### 一次情報

- SQLCipher の設計（<https://www.zetetic.net/sqlcipher/design/>）: ページ単位 AES-256、ページ毎 HMAC。鍵導出は **PBKDF2-HMAC-SHA512・既定 256,000 反復**、DB 毎に 16 バイトのランダム salt。**鍵はアプリケーションが供給する**（"Source key material via a secure method"）。SQLCipher 自身は暗号実装を持たず外部の暗号ライブラリに依存する。
- SQLCipher for .NET（<https://www.zetetic.net/sqlcipher/sqlcipher-for-dotnet/>）: **公式 .NET パッケージは Commercial Edition ライセンス、または SQLCipher Enterprise プログラムを通じてのみ提供される**。Commercial/Enterprise パッケージは `PRAGMA cipher_license` に有効なライセンスコードを渡す必要があり、渡さないと `SQLITE_AUTH(23)` になる。公開されている `SQLitePCLRaw.bundle_e_sqlcipher` 等は Zetetic 自身が「当社が使用もサポートもしたことのないサードパーティ製パッケージ」と明記している。
- Microsoft.Data.Sqlite の暗号化手順（<https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/encryption>）: 無償経路は `Microsoft.Data.Sqlite` を外して `Microsoft.Data.Sqlite.Core` ＋ `SQLitePCLRaw.bundle_e_sqlcipher` を入れ、接続文字列の `Password` キーワードで鍵を渡す構成になる（＝上記の非サポート サードパーティ製 bundle 依存）。既存 DB の暗号化・復号は「解決策ごとに異なる。SQLCipher では `sqlcipher_export()` を使う」。
- SQLCipher Open Source（Community Edition）のライセンス（<https://www.zetetic.net/sqlcipher/open-source/>）: BSD 相当。**著作権表示とライセンス全文を、利用者から見える場所に掲示する義務**がある（依存する SQLite／OpenSSL 等の告知も含む）。

### 運用要件

**FileVault（macOS）／BitLocker（Windows）の有効化**を運用要件として明文化する。S5 の運用ガイド（`docs/operations.md`）へ引き渡す。

### 再評価トリガ（3件）

以下のいずれかが発生した時点で本決定を再評価する。

1. **職員ごとに OS アカウントを分ける運用へ変わったとき**（このとき初めて DB 暗号化が職員間分離の意味を持つ）。
2. **端末を事業所外へ持ち出す運用が発生したとき**。
3. **鍵の預け先（HSM・IC カード・組織の鍵管理）が用意でき、紛失が復旧可能になったとき**。

### NOTICE への追記は不要

SQLCipher を採らないため、Community Edition の掲示義務（上記一次情報参照）は発生しない。`NOTICE` への追記は不要である。

### 不採用の記録

選択肢 (b) SQLCipher は、無償経路がベンダー非サポートのサードパーティ製 bundle に依存し、公式 .NET パッケージは Commercial Edition ライセンスと `PRAGMA cipher_license` を要する（§一次情報）。本決定はこれを採らないため、ライセンス取得やパスフレーズ運用の検討は発生しない。

### 影響

- ADR 0003 本文・上記「追補: 権限ポリシー」の暫定判断（「暗号化はフェーズ0では未採用（要再評価）」「暗号化採否は本追補の対象外（open-questions.md 引き続き）」）は本追補で解消し、DB 本体の暗号化は不採用のまま確定する。
- `docs/open-questions.md` の「SQLite 暗号化（SQLCipher等）の採否」項をクローズする。
- バックアップ運用（[ADR 0052](0052-backup-operations.md)）は、この結論を前提に「自動バックアップは平文・手動で持ち出す控えのみ将来的に暗号化を検討する（S3b）」という設計を採る。
