# Tsumugi Phase 4 残余 実装ロードマップ（S1〜S5 + 後段）

> **Source**: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` を接地したもの。
> **Status**: 設計合意済（2026-07-05）。各スライスの詳細 spec は本書から派生させる（着手直前）。
> **位置づけ**: Phase 4 全 6 スライスのうち **S0** は完了（`docs/superpowers/specs/2026-07-05-phase4-s0-kouchinmodule-and-avgwage-design.md`, `v0.3.0-phase4-s0`）。本書は残り 5 スライスと後段（AC2-8 一次資料入手時）の全体設計。

---

## 1. 目的

Phase 4 残余（S1〜S5）を独立レビュー可能な単位に分割し、着手順・依存・AC マッピング・ADR 計画を固定する。Phase 3（国保連請求）が出典待ちで凍結中でも本ロードマップは進行可能。

---

## 2. スライス構造

| Slice | 対応系統 | 主成果物 | 主 AC | 主 ADR | 想定サイズ | ブロッカー |
|---|---|---|---|---|---|---|
| **S1** | (A) フォント埋込＋QuestPDF ライセンス | `assets/fonts/` NotoSansCJKJP 同梱／`QuestPdfLicenseConfigurator` 拡張／CJK substring assertion 復活／NOTICE OFL 追記／ADR 0013 確定 | AC4-1, AC4-2 | 0013 改 | 中（1〜2 PR） | なし（QuestPDF 閾値は一次情報 URL 確認） |
| **S2** | (E) bulk operations 禁止＋NetArchTest ADR | `src/` ソース走査テスト（`ExecuteUpdate*/ExecuteDelete*` 検出）／NetArchTest 見送り ADR／CHANGELOG 更新 | AC4-12 | 0020（bulk 禁止）／0021（NetArchTest 見送り） | 小（1 PR） | なし |
| **S3** | (B) バックアップ運用化＋暗号化決着 | 終了時自動バックアップ／世代管理／復元 UseCase／保存先権限強制／暗号化 ADR 0003 決着（推奨: OS ディスク暗号化 + 現状権限）／CHANGELOG＋運用ガイド橋渡し | AC4-3, AC4-4 | 0003 改／0022（バックアップ運用） | 中大（2〜3 PR） | 暗号化採否は一次情報（SQLCipher 脅威モデル）確認 |
| **S4** | (C) UI 補完 3 点＋ContractedProvider ADR | `DisabilityCertificatePolicy.FindRenewalDue`／`FaceSheetDiff.Compare`／障害種別整合警告純粋関数／View × 3／ContractedProvider ADR | AC4-5〜AC4-8 | 0023（ContractedProvider 運用） | 中大（1〜2 PR、View 3 枚） | なし |
| **S5** | (D) 発行＋初回セットアップ＋運用ガイド＋手動 QA | `build/publish.sh`／`build/publish.ps1`／初回セットアップウィザード VM＋View／`docs/operations.md`／`docs/manual-qa.md`／両 OS smoke 実施記録 | AC4-9〜AC4-11 | 0024（配布構成・トリミング既定オフ） | 中（1〜2 PR＋実機 QA） | 実機（macOS/Windows）が必要 |
| **後段** | 平均工賃月額（AC2-8 完了） | `AverageWageMetric` 正式定義差替／テスト更新／CHANGELOG／open-questions クローズ | AC4-14 | 0012 補記 | 極小（値差替） | **一次資料入手待ち**（厚労省告示/通知） |

**運用値-1（KouchinModule 突合）は S0 で完了済**。運用値-2（平均工賃月額）は S0 で構造整備済み → 差替 1 コミットで完了できる状態。

---

## 3. 着手順

確定: **S1 → S2 → S3 → S4 → S5**（Phase 4 文書 §8）。並行に独立系統として動かせるが、レビュー波を作らないため直列で回す。**後段（平均工賃）**は一次資料入手時に順序と独立に差替コミットで実施。

---

## 4. 依存関係

```
S1 (フォント埋込)  ─┐
                    ├──→ Phase 3-2 の "PDF フォント前提" が解消（帳票着手可）
                    └──→ CHANGELOG「本番投入前 deferred」から日本語フォント削除
S2 (bulk 禁止)      ─→ S3/S4 の Repository/UseCase 追加時に "気付かず ExecuteDelete*" が入る事故を防ぐ
S3 (バックアップ)   ─→ S5 運用ガイド §4 の「バックアップ／復元」節を実装済み前提で書ける
S3 (暗号化 ADR)     ─→ S5 運用ガイド §5「OS ディスク暗号化の設定」の要否が確定
S4 (UI 補完)        ─→ 独立（前提なし・後続なし）
S5 (配布)           ─→ S1〜S4 完了物すべてが実機 smoke の対象
後段 (平均工賃)     ─→ Phase 3-1 の入力になる。一次資料入手時に差替 1 コミット
```

強い依存は S1→S5、S3→S5 のみ。S2/S4 は独立。S3 spec の一次情報確認と S1 spec の QuestPDF 一次情報確認は並行可能。

---

## 5. AC マッピング（Phase 4 文書 §7）

| AC | 主担当 | S0 反映 | 備考 |
|---|---|---|---|
| AC4-1 日本語フォント埋込＋NOTICE 帰属 | S1 | - | CJK substring assertion 復活を含む |
| AC4-2 QuestPDF ライセンス ADR 0013 確定 | S1 | - | 一次情報 URL 付き |
| AC4-3 暗号化 ADR 0003 決着 | S3 | - | SQLCipher 採否含む |
| AC4-4 バックアップ運用化 | S3 | - | 終了時自動＋世代＋復元＋権限 |
| AC4-5 精神手帳更新アラート | S4 | - | `FindRenewalDue` 純粋関数 |
| AC4-6 フェースシート差分表示 | S4 | - | `FaceSheetDiff.Compare` 純粋関数 |
| AC4-7 障害種別整合警告 | S4 | - | 警告のみ／保存は妨げない |
| AC4-8 ContractedProvider 運用 ADR | S4 | - | 推奨: 自社は Contract、他事業所は ContractedProvider |
| AC4-9 self-contained 発行 smoke | S5 | - | `win-x64` / `osx-arm64` |
| AC4-10 初回セットアップウィザード | S5 | - | DB なし判定＋事業所登録 |
| AC4-11 運用ガイド＋手動 QA 実施記録 | S5 | - | 両 OS 各 1 回 |
| AC4-12 bulk 禁止スキャナ＋NetArchTest ADR | S2 | - | 意図的違反で赤確認 |
| AC4-13 KouchinModule 突合正式化 | S0 | ✅ 完了 | ADR 0012 v2 |
| AC4-14 平均工賃月額 正式化 | 後段 | 構造整備済 | 値差替のみ |

---

## 6. ADR プラン（次番号 0020〜）

| ADR | 位置づけ | Slice | 主内容 |
|---|---|---|---|
| 0003（改） | 決着 | S3 | 暗号化採否（現状権限＋OS ディスク暗号化を運用要件へ、または SQLCipher 採用の場合の移行・バックアップ整合） |
| 0012（補記） | 補記 | 後段 | AC2-8 平均工賃月額 正式定義（一次資料入手時のみ） |
| 0013（改） | 決着 | S1 | QuestPDF Community License 採否・年商閾値（一次情報 URL） |
| **0020**（新） | 決着 | S2 | bulk operations 禁止のアーキテクチャテスト方針 |
| **0021**（新） | 決着 | S2 | NetArchTest 採否（推奨: 見送り） |
| **0022**（新） | 決着 | S3 | バックアップ運用化（世代・自動・復元・権限） |
| **0023**（新） | 決着 | S4 | ContractedProvider / Contract 運用方針（推奨: 自社は Contract のみ） |
| **0024**（新） | 決着 | S5 | 配布構成（self-contained・単一ファイル・トリミング既定オフ） |

いずれも「暫定 → 確定」パターンではなく、**初手から確定**として書く（一次情報の抜けがある場合のみ、その項目だけ open-questions 継続）。

---

## 7. 保留（後段：平均工賃月額）

- **入手先**: 厚労省 社会・援護局長通知「就労継続支援B型の平均工賃月額の算定方法について」（直近改正）。運用者に依頼済み扱いの位置づけ（`docs/open-questions.md` に入手先明記）。
- **入手時のコミット計画**（1 コミット完結）:
  1. `AverageWageMetric.Calculate` の分母切替・除外者パラメータを正式定義に合わせて確定
  2. `AverageWageMetricTests` を正式期待値で更新（暫定テストは delete、旧参照値を CHANGELOG に転記）
  3. `docs/open-questions.md` の当該項を `[x]` へ
  4. CHANGELOG「本番投入前に必須の deferred」から AC2-8 を除去
- **Phase 3-1 との関係**: Phase 3-1（基本報酬区分算定）が本値を入力にする。**Phase 3 着手前に完了必須**。

---

## 8. スライスごとの成果物 & commit 粒度

各スライスは **1 系統 = 1 PR 相当**（Phase 4 文書 §3.3）。commit は「TDD の Red→Green→Refactor」単位に細かく割り、末尾コミットで CHANGELOG／ADR／open-questions を更新。

### 8.1 S1（フォント埋込＋QuestPDF）

- `assets/fonts/NotoSansCJKJP-Regular.otf` ほか（Bold 含む weight 別ファイル、SIL OFL 1.1）
- `assets/fonts/NotoSansCJKJP.LICENSE.txt`（OFL 全文）
- `NOTICE` に OFL 帰属追記
- `src/Tsumugi.Infrastructure.Reporting/`
  - `QuestPdfLicenseConfigurator` 拡張（`FontManager.RegisterFontFromEmbeddedResource`／`Settings.UseEnvironmentFonts = false`）
  - `Tsumugi.Infrastructure.Reporting.csproj` に `<EmbeddedResource>` 追記
- `tests/Tsumugi.Infrastructure.Tests/Reporting/`
  - `EmbeddedFontResolutionTests`（新規）
  - `WageStatementPdfGeneratorTests` / `WagePaymentListPdfGeneratorTests`（CJK substring assertion 復活・Bold ASCII 復活）
- `docs/decisions/0013-pdf-engine-questpdf.md` 改（一次情報 URL 付きで確定）
- `docs/open-questions.md`（QuestPDF ライセンス項・日本語フォント項をクローズ）
- `docs/superpowers/plans/2026-06-29-phase3-2-reports.md` からフォント前提タスクを除去
- CHANGELOG 更新

### 8.2 S2（bulk 禁止＋NetArchTest 見送り）

- `tests/Tsumugi.Infrastructure.Tests/Architecture/BulkOperationsForbiddenTests.cs`（新規、ソース走査）
- `docs/decisions/0020-bulk-operations-forbidden.md`（新規）
- `docs/decisions/0021-netarchtest-decline.md`（新規）
- `docs/open-questions.md`（bulk 未検出項・NetArchTest 採否項をクローズ）
- CHANGELOG 更新

### 8.3 S3（バックアップ＋暗号化）

- Domain: なし（純粋ロジックは後述の Policy のみ）
- Application:
  - `IBackupPolicy.cs`（新規、世代選定純粋関数）
  - `BackupGenerationPolicy.cs`（新規、日付名リスト→古い順削除の純粋関数）
  - `RestoreDatabaseUseCase.cs`（新規、復元前退避 + AuditEntry）
  - `RunScheduledBackupUseCase.cs`（新規、終了時呼出）
  - `AppSettings` の `AutoBackupOnExit` / `MaxGenerations` フィールド追加
- Infrastructure:
  - `SqliteBackupService` 拡張（世代管理・保存先権限強制）
  - Backup 保存先 `SqliteLocationService` 準拠の権限適用ロジック共通化
- App:
  - `App.axaml.cs` 終了フックで `RunScheduledBackupUseCase` を呼ぶ
  - `BackupSettingsViewModel` / `BackupSettingsView`（設定タブに配置）
  - `RestoreDialogViewModel` / `RestoreDialogView`
- Tests: BackupGenerationPolicy（テーブル駆動）／Restore 往復／保存先権限（OS 別）／終了時フックの ViewModel テスト
- `docs/decisions/0003-sqlite-location-and-encryption.md` 改（暗号化決着）
- `docs/decisions/0022-backup-operations.md`（新規）
- CHANGELOG 更新
- `docs/open-questions.md`（暗号化採否項をクローズ）

### 8.4 S4（UI 補完 3 点＋ContractedProvider ADR）

- Domain:
  - `Logic/DisabilityCertificatePolicy.cs`（`FindRenewalDue` 純粋関数）
  - `Logic/FaceSheetDiff.cs`（`Compare(older, newer) → 変更項目リスト` 純粋関数）
  - `Logic/DisabilityConsistencyPolicy.cs`（受給者証×手帳の不整合検出 純粋関数）
- Application:
  - `Dtos/FaceSheetVersionDto.cs`（版一覧要素）
  - `Dtos/FaceSheetChangeDto.cs`（差分項目）
  - `Dtos/DisabilityConsistencyWarningDto.cs`
  - `QueryDisabilityCertificateRenewalsUseCase.cs`
  - `QueryFaceSheetHistoryUseCase.cs`
  - `QueryDisabilityConsistencyUseCase.cs`
- App:
  - `DisabilityCertificateAlertsViewModel` / View（既存の受給者証アラートと同型）
  - `FaceSheetHistoryViewModel` / View（版一覧＋差分表示）
  - `DisabilityConsistencyBannerViewModel` / 既存 View にバナー配置
  - `MainWindow` にタブ追加
- Tests: 3 純粋関数テーブル駆動＋ ViewModel テスト
- `docs/decisions/0023-contracted-provider-vs-contract.md`（新規、推奨: 自社は Contract のみ／二重入力を警告）
- `docs/open-questions.md`（手帳更新／差分／整合警告／ContractedProvider の 4 項クローズ）
- CHANGELOG 更新

### 8.5 S5（配布＋初回セットアップ＋運用ガイド＋手動 QA）

- `build/publish.sh`（macOS）／`build/publish.ps1`（Windows）
- Application:
  - `FirstRunPolicy.cs`（純粋関数、DB 有無→初回判定）
  - `RegisterFirstRunUseCase.cs`（事業所登録＋管理者名）
- App:
  - `FirstRunWizardViewModel` / `FirstRunWizardView`
  - `App.axaml.cs` の起動フローに `FirstRunPolicy` を挿入
- `docs/operations.md`（新規、日本語運用ガイド）
- `docs/manual-qa.md`（新規、実施記録テーブル付き）
- `docs/decisions/0024-distribution-and-publish.md`（新規、トリミング既定オフ）
- 両 OS 実機 smoke 実施 → `docs/manual-qa.md` 追記（S5 の最終コミット）
- CHANGELOG 更新

---

## 9. 参照

- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md`（親文書）
- `docs/superpowers/specs/2026-07-05-phase4-s0-kouchinmodule-and-avgwage-design.md`（S0 完了）
- `docs/open-questions.md`（クローズ対象の網羅リスト）
- `CHANGELOG.md`「本番投入前に必須の deferred」
