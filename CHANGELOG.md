# 変更履歴 / Changelog

本書は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) を採用します。

日付フォーマットは `YYYY-MM-DD`（JST）。

## [Unreleased]

### 計画
- フェーズ 2（工賃計算）— 出来高/時間/固定の3方式、締め日・端数規則の確定後に着手。
- フェーズ 3（国保連請求 CSV 生成）— 報酬告示・CSV インターフェース仕様の出典確定後に着手。

---

## [0.1.0-phase1] - 2026-06-28

フェーズ 1（マスタ + 記録）完了。利用者・受給者証・契約・事業所・事業所体制・日次記録の CRUD を画面まで通し、楽観的同時実行と追記型データモデルを実行時不変条件として強制。

### 追加（Added）

#### Domain（純粋ロジック）
- 6 エンティティ（`record` ＋ 追記型）: `Office` / `Recipient` / `Certificate` / `Contract` / `OfficeCapability` / `DailyRecord`
- 純粋ロジック: `DailyRecordPolicy`（実効状態導出、sibling 最新採用）、`PeriodPolicy`（期間重複・空白検出）、`CertificatePolicy`（残日数・しきい値）
- 値オブジェクト: `DateRange`
- 列挙: `Attendance` / `TransportKind` / `RecordKind` / `ServiceCategory` / `RegionGrade` / `Sex`

#### Application（ユースケース）
- Recipient: 登録 / 更新 / 一覧
- Certificate: 登録 / 期限アラート一覧
- Contract: 登録 / 利用者別一覧
- Office: 登録 / 更新 / 一覧
- OfficeCapability: 登録 / 事業所別一覧
- DailyRecord: 記録 / 訂正 / 取消 / 月次クエリ
- 信頼境界 `DateValidator`（`DateOnly`、両端含む、暦日比較で固定）
- 楽観的同時実行: `OptimisticConcurrencyException` を Application 層に新設し、UpdateOffice / UpdateRecipient が `expectedConcurrencyToken` を引数で受け取り衝突検知（ADR 0007）

#### Infrastructure（EF Core / SQLite）
- `TsumugiDbContext` で `SaveChanges` / `SaveChangesAsync` の bool overload を override し、`AppendOnlyGuard.Inspect` + 更新トークン回転を全保存経路で強制
- `EfUnitOfWork.SaveChangesAsync` で `ChangeTracker.Clear()` を呼び、単一スコープ DbContext でも同一キー再更新が追跡衝突しない（ADR 0008）
- 6 リポジトリ実装（Office / Recipient / Certificate / Contract / OfficeCapability / DailyRecord）
- マイグレーション `AddPhase1Entities`
- `DateRangeJson` シリアライザ（`CultureInfo.InvariantCulture` 明示 / `ParseExact("yyyy-MM-dd")`）

#### App（Avalonia UI / MVVM）
- 7 タブ構成 MainWindow（利用者一覧 / 利用者登録/編集 / 受給者証 / 契約 / 日次記録 / 事業所 / 事業所体制）
- 各 View の Loaded ライフサイクルから VM の `InitializeAsync` を呼び出す配線
- 利用者一覧から登録/編集タブへの自動切替ブリッジ（MainWindow code-behind 経由）
- 日次記録セルの `SetAttendanceCommand`（`EffectiveId` の有無で Record/Correct 自動分岐）
- 契約画面の F5 一覧更新 + 保存後自動再読込
- `DateOnlyConverter` が parse 失敗時に `BindingNotification(Error)` を返す（Today に黙って fallback しない）
- `AccessibilityDefaults.Apply` でテーマ・低アニメーション Style・フォント拡大追従リソース（`BaseFontSize` / `HeadingFontSize` / `DailyCellSize`）を `Application` に適用

#### CI 機械判定トリップワイヤ（CLAUDE.md ハード制約に対応）
- §1 オフライン拡張: P/Invoke（`ws2_32` / `wininet` / `winhttp` / `libcurl` 等）+ URL リテラル（`http(s)/ftp(s)/ws(s)/smtp/telnet/ldap(s)` 大小無視）の 4 アセンブリ走査
- §2 国保連伝送系: `X509Certificate(2)` / `SignedXml` / `SmtpClient` 等の TypeRef + 「電子請求受付システム」「取込・送信システム」リテラルの混入検出
- §4 ログ PII: `ILogger/Console/Trace/Debug` 呼び出しと PII 識別子（`KanjiName/KanaName/DateOfBirth/CertificateNumber/Municipality/ConnectionString/DbPath`）の同行同居検出
- §5 アクセシビリティ配線: Views/*.axaml の `{DynamicResource}` キー検証 + FontSize 数値直書き再混入検出 + 主要 View が要求 Binding を晒していることの XAML テキスト検証
- §6 カルチャ依存: `.Parse/.ParseExact/.ToString("…")` の `CultureInfo` 未明示行を src/ で検出

#### ADR
- [0004 attendance integrated into DailyRecord](docs/decisions/0004-attendance-integrated-into-daily-record.md)
- [0005 certificate period overlap policy](docs/decisions/0005-certificate-period-overlap-policy.md)
- [0006 office capability flag set](docs/decisions/0006-office-capability-flag-set.md)
- [0007 optimistic concurrency token round-trip](docs/decisions/0007-optimistic-concurrency-token-roundtrip.md)
- [0008 single-scope DbContext + ChangeTracker.Clear](docs/decisions/0008-single-scope-dbcontext.md)
- [0009 DailyRecord stale-origin rejection at use case](docs/decisions/0009-dailyrecord-stale-origin-rejection.md)

### 修正（Fixed）
Codex レビュー Round 1〜6 で挙がった全 14 件（H 8 + M 4 + L 2）を取り込み済み。代表例:
- DbContext 単一スコープでの追跡衝突（同一 Id 再更新で `InvalidOperationException`）を `ChangeTracker.Clear()` で解消
- `DailyRecordPolicy.Effective` の sibling 最古採用バグを最新採用に修正、加えて取消後 stale origin への訂正/取消を Application 層で拒否
- 全 Register 系 UseCase で `Guid.Empty` 拒否（孤立レコード防止）
- `DateOnlyConverter.ConvertBack` の parse 失敗時 Today fallback を `BindingNotification(Error)` 化
- 契約画面 / 日次記録画面が View Loaded から利用者一覧をロードする配線
- 受給者証登録 / 事業所体制登録 / 事業所更新 / 利用者更新の各 UseCase を画面まで通す

### 受け入れ基準達成（AC1-1〜AC1-9）
全 9 項目達成。詳細は [04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md §7](04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md) 参照。

### テスト
246 件（Domain 36 / Application 54 / Infrastructure 91 / App 65）、行カバレッジ Total 92.79%。

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

[Unreleased]: https://github.com/SilentMalachite/Tsumugi/compare/v0.1.0-phase1...HEAD
[0.1.0-phase1]: https://github.com/SilentMalachite/Tsumugi/compare/v0.0.1-phase0...v0.1.0-phase1
[0.0.1-phase0]: https://github.com/SilentMalachite/Tsumugi/releases/tag/v0.0.1-phase0
