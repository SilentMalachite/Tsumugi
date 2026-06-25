# 変更履歴 / Changelog

本書は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) を採用します。

日付フォーマットは `YYYY-MM-DD`（JST）。

## [Unreleased]

### 計画
- フェーズ 1（マスタ + 記録）— 利用者・受給者証・サービス提供実績・出欠の CRUD、受給者証期限アラート、訂正履歴の追記型。

---

## [0.0.1-phase0] - 2026-06-26

フェーズ 0（プロジェクト基盤）完了。動作するアプリのスケルトン + 全フェーズに渡る品質ゲートを整備。

### 追加（Added）

- ソリューションスケルトン: `Tsumugi.Domain` / `Tsumugi.Application` / `Tsumugi.Infrastructure` / `Tsumugi.App` の 4 層構成（クリーンアーキテクチャ）
- DI 合成ルート: `CompositionRoot.Build(connectionString)` で Infrastructure を差し替え可能
- EF Core 10.x + SQLite 永続化、初期マイグレーション、一時ファイル SQLite 往復テスト
- `VACUUM INTO` バックアップユースケース（`IBackupService` / `SqliteBackupService` / `BackupDatabaseUseCase`）
- SQLite 保存先サービス `SqliteLocationService`（`ISqliteLocation`）
  - Unix: ディレクトリ `0700` / DB ファイル `0600`
  - Windows: 現在ユーザーのみフルコントロール / 継承無効 / 継承 ACE 除去
  - 既存ゆるい権限を起動時に **冪等に締め直す**
- オフライン検査
  - `OfflineComplianceTests` — `Tsumugi.Domain` / `Tsumugi.Application` / `Tsumugi.Infrastructure` のアセンブリ参照を検査（`System.Net*` / `Microsoft.AspNetCore` / `Grpc` 禁止）
  - `AppOfflineComplianceTests` — `System.Reflection.Metadata` の `PEReader`/`MetadataReader` で `Tsumugi.App.dll` の `TypeReference`/`MemberReference` を直接走査（推移閉包は対象外、Avalonia 実体ロード回避）
  - 両側に `(プレフィックス, 理由)` 形式の allowlist（既定空、空 Reason ガード付き）
- アーキテクチャ検査
  - `tests/Tsumugi.Domain.Tests/ArchitectureTests.cs` — Domain ⊅ 外側
  - `tests/Tsumugi.Application.Tests/ArchitectureTests.cs` — Application ⊅ {Infrastructure, App, Avalonia, EF Core}
  - `tests/Tsumugi.Infrastructure.Tests/ArchitectureTests.cs` — Infrastructure ⊅ {App, Avalonia}
- 品質ゲート CI スクリプト: `build/ci.sh` / `build/ci.ps1`（restore → format → build → test → coverage → offline → architecture）
- カバレッジ閾値: Domain / Application の行カバレッジ 70% 以上を CI で強制（Domain 100% / Application 84.61% 達成）
- ADR
  - [0001 record と EF Core 整合](docs/decisions/0001-record-and-ef-core.md)
  - [0002 FluentAssertions 7.x 固定](docs/decisions/0002-fluentassertions-version.md)
  - [0003 SQLite 保存先と権限ポリシー](docs/decisions/0003-sqlite-location-and-encryption.md)
- ドキュメント
  - [01_ClaudeCode_実装指示書_Tsumugi.md](01_ClaudeCode_実装指示書_Tsumugi.md) — 全体仕様
  - [03_ClaudeCode_Phase0修正指示_Tsumugi.md](03_ClaudeCode_Phase0修正指示_Tsumugi.md) — Codex Major×2 修正範囲
  - [docs/open-questions.md](docs/open-questions.md) — 未確定事項の起票簿

### セキュリティ（Security）

- `Tsumugi.App` を含む全プロダクションアセンブリで `System.Net.*` への直接参照を CI で禁止（AC0-5）
- SQLite ディレクトリ / ファイル権限を OS レベルで作成時強制（AC0-6）
- ログに氏名・受給者証番号・保存先フルパスを出さない実装規約
- 既知の脆弱性 `GHSA-2m69-gcr7-jv3q`（`SQLitePCLRaw.lib.e_sqlite3` 2.1.11）を advisory-scoped に抑制（修正版なし、オフライン運用で許容）

### 既知の制約

- アーキテクチャテストとオフラインテストは `GetReferencedAssemblies()` ベースで **直接参照のみ** を検査（推移閉包は対象外、フェーズ 1 以降の検討事項として open-questions に記載）
- macOS 上の `Environment.SpecialFolder.ApplicationData` 実解決は .NET SDK 版／環境変数依存（`~/Library/Application Support/` または `~/.config/`）。権限ポリシーは両パスで適用される
- フェーズ 0 では SQLCipher 等の DB 暗号化は **未採用**。OS ファイル権限 + OS 全体ディスク暗号化に委譲（運用要件確定後に ADR 0003 で再評価）
- フェーズ 1 以降への持ち越し: PEReader スキャナの nested type ブラインドスポット、OS 検出 API 統一、Windows テスト `[SkipOnPlatform]` 化、`WindowsIdentity.GetCurrent().User` null ガード、teeth meta-test 自動化、Infrastructure カバレッジゲート

---

[Unreleased]: https://github.com/SilentMalachite/Tsumugi/compare/v0.0.1-phase0...HEAD
[0.0.1-phase0]: https://github.com/SilentMalachite/Tsumugi/releases/tag/v0.0.1-phase0
