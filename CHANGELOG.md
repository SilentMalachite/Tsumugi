# 変更履歴 / Changelog

本書は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に準拠し、バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) を採用します。

日付フォーマットは `YYYY-MM-DD`（JST）。

## [Unreleased]

### 計画
- フェーズ 3（国保連請求 CSV 生成）— 報酬告示・CSV インターフェース仕様の出典確定後に着手。

### 本番投入前に必須の deferred
- QuestPDF Community License の事業所年商閾値確認（ADR 0013 / `docs/open-questions.md`）。
- PDF 帳票の日本語フォント埋込（Noto Sans CJK JP）。漢字抽出が CJK 互換ブロックに化けるため、運用投入前に `assets/fonts/` 追加 + `Settings.UseEnvironmentFonts = false` + `FontManager.RegisterFontFromEmbeddedResource` を実施。
- KouchinModule.bas v5 の実挙動突合 → ADR 0012 暫定値の正式化。
- 平均工賃月額（AC2-8）の厚労省告示/通知突合 → 正式定義確定。

---

## [0.2.0-phase2] - 2026-06-29

フェーズ 2（工賃計算）完了。作業実績・WageFund / WageSettings の期間マスタ・4 方式（Piece/Hourly/Fixed/Equal）の素計算・按分 Σ=原資 不変条件・WageStatement 確定スナップショット・PDF 帳票（QuestPDF）・AuditEntry を画面まで通し、AC2-1 〜 AC2-10 のうち AC2-8（正式定義未確定）を除く 9 件を達成。Codex review 由来の HIGH 4 + MEDIUM 3 + LOW 1 + Minor 2 件もすべて解消。

### 追加（Added）

#### Domain（純粋ロジック）
- 5 エンティティ（`record` ＋ 追記型）: `WageFund` / `WageSettings` / `WageStatement` / `WorkRecord` / `AuditEntry`、加えて Phase 1 carryover の `FaceSheet` / `DisabilityCertificate`
- 純粋ロジック: `AllocationPolicy`（Σ=原資 不変条件・LargestRemainder/ReserveToOffice の決定的余り処理）、`WageBasisExtractor`（実効 `Present` のみ集計）、`WageCalculator`（戦略選択）、`WageSettingsPolicy` / `WageFundPolicy` / `WageStatementPolicy`（追記型実効状態導出）、`FiscalYearPolicy`、`AverageWageMetric`（暫定式）
- 4 工賃方式戦略: `PieceWageStrategy` / `HourlyWageStrategy` / `FixedWageStrategy` / `EqualWageStrategy`（共通 `IWageMethodStrategy`）
- 値オブジェクト: `YearMonth`（`YYYYMM` 整数双方向変換 / 演算子 / 月境界 / 年度境界）
- 列挙: `WageMethod` / `RoundingRule` / `RemainderPolicy` / `AuditAction`

#### Application（ユースケース）
- 工賃: `SetWageFundUseCase` / `ConfigureWageSettingsUseCase` / `CalculateWagesUseCase` / `CloseWagesUseCase` / `QueryWageStatementUseCase`
- 作業実績: `RecordWorkUseCase` / `CorrectWorkUseCase` / `CancelWorkUseCase` / `QueryMonthWorkUseCase`
- DTO: `WageFundDto` / `WageSettingsDto` / `WagePreviewDto` / `WageStatementDto` / `WorkRecordDto`
- リポジトリ抽象: `IWageFundRepository` / `IWageSettingsRepository` / `IWageStatementRepository` / `IWorkRecordRepository` / `IAuditEntryRepository`
- PDF 抽象: `IWageReportGenerator`（明細 + 支払一覧）
- 監査ログ抽象: `IAuditTrail`（`AppendAsync(AuditAction, Guid, string?)`）

#### Infrastructure（EF Core / SQLite）
- 4 リポジトリ実装: `WageFundRepository` / `WageSettingsRepository` / `WageStatementRepository` / `WorkRecordRepository`
- 監査ログ: `AuditEntryRepository` / `AuditTrail`（ADR 0014: append-only 専用テーブル）
- EF Core configuration: `WageFundConfiguration`（`(OfficeId, MonthKey) WHERE Kind=1` partial unique index, ADR 0017）/ `WageSettingsConfiguration` / `WageStatementConfiguration` / `WorkRecordConfiguration` / `AuditEntryConfiguration` / `DailyRecordConfiguration`（`(RecipientId, ServiceDate) WHERE Kind=1` partial unique index, ADR 0015）
- マイグレーション: `20260628015004_DailyRecordDuplicateNewIndex` / `20260628045957_Phase2Wage` / `20260628204038_WageFundDuplicateNewIndex`
- AppendOnlyGuard を Phase 2 エンティティ（WorkRecord / WageFund / WageSettings / WageStatement / AuditEntry）に拡張

#### Infrastructure.Reporting（新規プロジェクト）
- QuestPDF Community License 固定 (`QuestPdfLicenseConfigurator`, ADR 0013)
- `WageStatementPdfGenerator`: 利用者明細 PDF + 事業所月次の工賃支払一覧 PDF
- 発行日は注入された `TimeProvider` 経由（決定論的、L-1 対応）

#### App（Avalonia UI / MVVM）
- 4 ViewModel: `WorkRecordViewModel`（日次セル編集）/ `WageFundSettingsViewModel`（原資 + 設定）/ `WageCalculationViewModel`（プレビュー）/ `WageStatementViewModel`（確定 + PDF 保存）
- `WorkCellViewModel`（日次セル単位の Hourly 入力）
- 4 View: `WorkRecordView` / `WageFundSettingsView` / `WageCalculationView` / `WageStatementView`（事業所選択 ComboBox + 年月入力 + アクションボタン）
- `MainViewModel` を Phase 2 タブで拡張（合成ルートに 4 VM 追加、F6 達成）
- `YenFormatter`（`N0` + InvariantCulture 固定、AC2-10）
- `IFileSaveService` + `AvaloniaFileSaveService`（M-2: AC2-7 PDF 保存ダイアログ抽象 + Avalonia `IStorageProvider` 実装）

#### CI 機械判定の強化
- `OfflineComplianceTests` の `[Theory]` に `Tsumugi.Infrastructure.Reporting` を追加（M-1）
- `AppOfflineComplianceTests` の P/Invoke + URL 検査に `Tsumugi.Infrastructure.Reporting` を追加（Phase 2 初期）
- `build/ci.sh` の Domain カバレッジ閾値を **70 → 95** に昇格（H-4）
- `ArchitectureTests` を Domain / Application / Infrastructure / Infrastructure.Reporting の 4 層で依存方向検証
- `AppendOnlyGuardPhase2Tests` で Phase 2 エンティティ群の追記型違反検出

#### ADR
- [0010 Certificate MHLW form shape](docs/decisions/0010-certificate-mhlw-form-shape.md)（Phase 1 carryover）
- [0011 DisabilityCertificate and FaceSheet](docs/decisions/0011-disability-certificate-and-face-sheet.md)（Phase 1 carryover）
- [0012 工賃計算の方式戦略・端数・年度起点](docs/decisions/0012-wage-calculation-strategy.md)
- [0013 PDF 生成エンジン (QuestPDF) の採否](docs/decisions/0013-pdf-engine-questpdf.md)
- [0014 監査ログ (AuditEntry) を append-only で導入](docs/decisions/0014-audit-trail-append-only.md)
- [0015 DailyRecord 重複 New を SQLite partial unique index で防止](docs/decisions/0015-dailyrecord-duplicate-new-index.md)
- [0016 ゼロ重み時の按分挙動](docs/decisions/0016-zero-weight-allocation.md)（Codex H-2 対応）
- [0017 WageFund 重複 New を SQLite partial unique index で防止](docs/decisions/0017-wagefund-duplicate-new-index.md)（Codex M-3 対応）

### 修正（Fixed）— Codex review 後の改修

#### HIGH
- **H-1**: `WageBasisExtractor` が非 `Present` 日の `WorkRecord` も合算していた（AC2-5 違反）。recipient×date の実効 `Present` セットで `WorkRecord` をフィルタするよう修正。Absent / AbsenceSupport / DailyRecord なし / Cancellation 後の WorkRecord がすべて除外されることをテストで検証。
- **H-2**: `AllocationPolicy.Allocate` が `totalWeight=0 && totalYen>0` で Σ ≠ 原資 を返していた（AC2-4 違反）。ADR 0016 採用、`RemainderPolicy` 別に分岐（`LargestRemainder` は `InvalidOperationException`、`ReserveToOffice` は全額留保）。
- **H-3**: 工賃 3 VM (`WageFundSettings` / `WageCalculation` / `WageStatement`) に事業所選択 UI が無く、実 UI から `OfficeId` を入れられなかった。`OfficeCapabilityViewModel` パターンで `ListOfficesUseCase` 注入 + `SelectedOffice` バインド + ComboBox 配線を 3 VM/View に適用。`WageStatement` では `Office` (PDF 生成必須) も同時更新。
- **H-4**: Domain ≥95% カバレッジ条件未達 (88.15%) + CI 閾値が 70% のまま。`WageStatementPolicyTests` (0→100%) / `FaceSheetTests` (0→100%) / `WageStatement.NewRecord/Correction validation` (59→100%) / `YearMonth` boundaries/operators/roundtrip (70→100%) を追加し Domain line coverage を **85.83% → 98.03%** に。CI 閾値を **70 → 95** に昇格。

#### MEDIUM
- **M-1**: `Tsumugi.Infrastructure.Reporting` がオフライン直接参照スキャン対象外だった。`OfflineComplianceTests.Tsumugi_assemblies_do_not_reference_network_libraries` の `[Theory]` に `Tsumugi.Infrastructure.Reporting` を追加。
- **M-2**: AC2-7 PDF 生成が UI から到達不可（VM メソッドが `RelayCommand` ではなく View にもボタン無し）。`IFileSaveService` 抽象 + `AvaloniaFileSaveService` を新設、`WageStatementViewModel` に `SelectedStatement` + `SaveSelectedStatementPdfCommand` / `SavePaymentListPdfCommand` を追加、View に保存ボタン 2 つを配線。`FakeFileSaveService` で VM テストを担保。
- **M-3**: `WageFund` の月次 New 重複が DB で防げず、レース条件下で `WageFundPolicy.Effective` の解釈が壊れる可能性があった。ADR 0017 採用、`WageFundConfiguration` に `(OfficeId, MonthKey) WHERE Kind=1` の partial unique index を追加 (migration `20260628204038_WageFundDuplicateNewIndex`)。

#### LOW
- **L-1**: `WageStatementPdfGenerator` が `DateTime.UtcNow` を直接参照しており、同一入力でも帳票が日付依存だった。`TimeProvider` 注入に置換し、`Statement_pdf_is_deterministic_for_same_inputs_and_same_timeprovider` で同値性を固定。`CompositionRoot` は変更不要（`TimeProvider.System` 既登録）。

#### Minor
- `YearMonthTests.Comparison_operators_match_chronological_order` に `<=`/`>=` 等号アサート 2 件追加（CS1718 を別インスタンス比較で回避）。
- `WageStatementTests` から重複していた旧 `Negative_amount_throws` を削除（H-4 で追加した `NewRecord_throws_when_amount_is_negative` が `WithParameterName` を含む厳密な superset）。

### 受け入れ基準達成（AC2-1〜AC2-10）
9/10 達成。AC2-8（平均工賃月額）のみ正式定義未確定で暫定式実装。詳細は [`docs/phase2-acceptance.md`](docs/phase2-acceptance.md) 参照。

### テスト
469 件（Domain 146 / Application 96 / Infrastructure 119 / Reporting 5 / App 103）、Domain 行カバレッジ **98.03%**（CI gate **≥95%**）。

### 既知の制約
- AC2-8 平均工賃月額の正式定義は厚労省告示/通知突合まで暫定（`AverageWageMetric` テストで分母切替に強い構造を固定）。
- ADR 0012 の方式・端数・年度起点・余り処理の暫定既定値は KouchinModule.bas v5 突合まで暫定。
- PDF 帳票の日本語フォント未埋込（運用投入前に追加必須、`docs/open-questions.md` 参照）。
- QuestPDF Community License の事業所年商閾値確認は運用投入前。
- Avalonia GUI の見た目（フォント拡大追従・Reduce Motion・タブ順）は手動 QA（`docs/open-questions.md` Phase 1 持ち越し）。

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

[Unreleased]: https://github.com/SilentMalachite/Tsumugi/compare/v0.2.0-phase2...HEAD
[0.2.0-phase2]: https://github.com/SilentMalachite/Tsumugi/compare/v0.1.0-phase1...v0.2.0-phase2
[0.1.0-phase1]: https://github.com/SilentMalachite/Tsumugi/compare/v0.0.1-phase0...v0.1.0-phase1
[0.0.1-phase0]: https://github.com/SilentMalachite/Tsumugi/releases/tag/v0.0.1-phase0
