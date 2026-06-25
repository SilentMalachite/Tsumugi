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
