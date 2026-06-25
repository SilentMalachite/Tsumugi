# Claude Code 修正指示 — Tsumugi フェーズ0（Codexレビュー Major 2件）

> 出自: Codexレビュー（条件付き可 / Blockerなし / Major×2）。本書の2件を修正し、再レビューでフェーズ0受け入れ→フェーズ1着手とする。
> 親文書: `01_ClaudeCode_実装指示書_Tsumugi.md`（§6・§7を本修正に合わせて更新済み）。

---

## 結論
(A) オフライン遵守のCI判定に **Tsumugi.App** を含める。(B) SQLiteの**保存先ディレクトリ／DBファイル権限**を実装で強制し、OS別に検証する。両方ともテストファーストで、構造テストが「実際に落ちる」ことまで確認する。

---

## 前提
- 既存 `OfflineComplianceTests`（`tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`）は Domain/Application/Infrastructure を `[InlineData]` で検査済み。App は未対象、`docs/open-questions.md` で手動レビュー扱い。
- DB保存は `src/Tsumugi.App/App.axaml.cs` の `Directory.CreateDirectory` ＋接続文字列生成のみ。権限制御なし。
- SQLitePCLRaw の既知GHSA（patched: None）は ADR/open-questions 管理済み。**今回の対象外**（patched版が出たら再評価、で据え置き）。

---

## スコープ
- 含む: 上記 (A)(B) の実装・テスト・ADR/open-questions更新。
- 含まない: 暗号化(SQLCipher等)の採否（ADR別判断のまま）、機能追加、フェーズ1以降の前倒し。

---

## 仕様

### (A) App層のオフライン検査
- 対象は **ビルド出力の `Tsumugi.App.dll`**。プロジェクト参照ではなく**出力パス解決でDLLを読む**（Avalonia実体をロードしないため）。
- 走査方式: 追加依存を避け **`System.Reflection.Metadata`（BCL）** で `PEReader`/`MetadataReader` を開き、**App自身の `TypeReference`/`MemberReference` のみ**を見る（推移閉包は見ない＝Avalonia内部を巻き込まない）。これが「不可避な推移依存」問題を構造的に回避する肝。
- 禁止集合（参照していたら失敗）: 名前空間 `System.Net` / `System.Net.Http` / `System.Net.Sockets` / `System.Net.WebSockets` / `System.Net.Mail`、具体型 `HttpClient` / `WebClient` / `TcpClient` / `Socket` / `WebRequest` / `HttpRequestMessage` / `SmtpClient` 等。
- **allowlist**: 不可避かつ無害な参照のみ、`(名前空間.型名)＋理由コメント`で明示。**既定は空**。追加は根拠必須。allowlistの肥大は別途指摘対象になる前提で最小に保つ。
- 既存 `OfflineComplianceTests` の `[InlineData]` に App を追加（または同方式の専用ケースを追加）。`./build/ci.sh` は `dotnet test` を回すので、テスト追加で自動的にCI判定に入る。出力でApp検査が走ったと分かるようにする。
- `docs/open-questions.md` の「App は手動レビュー」項目は、自動検査の追加をもって**クローズ**。

### (B) SQLite 保存先／ファイル権限
- `App.axaml.cs` の保存先決定とディレクトリ作成・接続文字列生成を、**Infrastructure 層の小さなサービスに切り出す**（例: `ISqliteLocation` / `SqliteLocationService`）。App は合成ルートでこれを使うだけにする。
- 作成時に権限を強制:
  - **Unix/macOS**: ディレクトリ `0700`、DBファイル `0600`。`Directory.CreateDirectory(path, UnixFileMode)` ＋ `File.SetUnixFileMode` を使用。**WAL/SHM サイドカーもディレクトリ権限で保護される**ことを意図としてコメントに残す。
  - **Windows**: `System.IO.FileSystemAclExtensions` ＋ `DirectorySecurity`/`FileSecurity` で、**現在ユーザー（`WindowsIdentity.GetCurrent().User`）のみフルコントロール・継承無効・継承ACE除去**。Windows専用APIは `OperatingSystem.IsWindows()` でガードし `[SupportedOSPlatform("windows")]` を付す。
- **既存ディレクトリがゆるい権限の場合**: 黙って信頼しない。締め直すか、検証して明示的に失敗させる（どちらを採るかADRに記す）。
- `docs/decisions/0003-sqlite-location-and-encryption.md` を更新: 採用した権限ポリシー（Unix 0700/0600・Windowsユーザーのみ）と検証方法を追記。暗号化判断の節はそのまま。

---

## TDD
- (A): 
  1. まず **意図的な違反**（Appにダミーの `HttpClient` 参照を一時追加）で新テストが**赤**になることを確認 → 構造テストに歯があることの証明。確認後に違反を除去し緑へ。
  2. 正常状態で App を含めた全プロダクションアセンブリが緑。
- (B): OS別ケース。
  - Unix/macOS: 作成後に `File.GetUnixFileMode(db)` が `0600`、ディレクトリが `0700` であることを表明。
  - Windows: DACL が現在ユーザーのみ＋継承無効であることを表明。
  - 非該当OSでは**クリーンにスキップ**（実行環境を汚さない）。
  - 既存ディレクトリがゆるい場合の方針（締め直す/失敗）も1ケースで固定。

---

## 制約 / 品質ゲート
- 依存方向不変（DomainはInfrastructure/Appを参照しない）。新サービスはInfrastructure内。
- `dotnet build` 警告ゼロ / `dotnet test` 全緑 / `dotnet format --verify-no-changes` 通過 / `./build/ci.sh` 緑。
- 権限制御の追加でログに保存先フルパスや個人情報が出ないこと。
- allowlist は最小・理由付き。

---

## 受け入れ基準
- [ ] `Tsumugi.App` がオフライン検査対象に含まれ、禁止API参照で**実際に赤**になることを確認済み（歯のある検査）。
- [ ] allowlist が明示・最小・理由付き（既定は空）。
- [ ] `docs/open-questions.md` の App手動レビュー項目がクローズ。
- [ ] 保存先決定が Infrastructure のサービスへ分離され、App は合成ルートで利用するのみ。
- [ ] Unix 0700/0600・Windowsユーザーのみ のDACLを**作成時に保証**。既存ゆるい権限の扱いを実装。
- [ ] OS別権限検証テストが該当OSで緑・非該当でスキップ。
- [ ] `0003-sqlite-location-and-encryption.md` を権限ポリシーで更新。
- [ ] 親文書 §7 の **AC0-5（App込みのオフラインCI判定）適合**、**AC0-6（権限の強制＋検証）適合**。
