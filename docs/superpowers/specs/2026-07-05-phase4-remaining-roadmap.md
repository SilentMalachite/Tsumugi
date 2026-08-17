# Tsumugi Phase 4 残余 実装ロードマップ（S2〜S5）

> **Source**: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` を接地したもの。
> **Status**: 設計合意済（2026-07-05）/ 現況追記（2026-07-12）/ 現況追記（2026-07-26）/ 現況追記（2026-08-15）/ 現況追記（2026-08-16）/ 現況追記（2026-08-17）。S0・S1・S2・S3a・S4完了、S3b・S5未着手。各未着手スライスの詳細 spec は着手直前に本書から派生させる。Phase 3-4（R8-06制度実値投入。証跡: `docs/phase3-4-acceptance.md`）が2026-07-26に完了し、S2（bulk operations禁止＋NetArchTest ADR）に着手、2026-08-15に完了した（ADR 0050・0051、証跡: 本書 §2・§5、`CHANGELOG.md` Phase 4 S2 節）。続けてS3a（バックアップ運用化＋暗号化決着）に着手、2026-08-16に完了した（ADR 0003改・0052、証跡: 本書 §2・§5、`CHANGELOG.md` Phase 4 S3a 節）。S4（UI補完3点＋ContractedProvider ADR）は2026-08-17に完了した（ADR 0053、証跡: 本書 §2・§5・§8.4、`CHANGELOG.md` Phase 4 S4 節）。S3b（暗号化バックアップ）は独立して未着手のまま残る。次の未着手は **S5**。
> **位置づけ**: Phase 4 全 6 スライスのうち **S0**（`v0.3.0-phase4-s0`）・**S1**（`v0.3.1-phase4-s1`）・**S2**（2026-08-15完了、版タグ未付与）・**S3a**（2026-08-16完了、版タグ未付与）・**S4**（2026-08-17完了、版タグ未付与）は完了。本書は残りのS3b・S5を管理する。平均工賃の正式定義はADR 0023で確定し、請求用実装をPhase 3-1 Task 15へ移管した。

---

## 1. 目的

Phase 4残余（S2〜S5）を独立レビュー可能な単位に分割し、着手順・依存・ACマッピング・ADR計画を管理する。Phase 3（国保連請求）とは独立して進行可能である。

---

## 2. スライス構造

| Slice | 対応系統 | 主成果物 | 主 AC | 主 ADR | 想定サイズ | ブロッカー |
|---|---|---|---|---|---|---|
| **S1 ✅** | (A) フォント埋込＋QuestPDF ライセンス | Noto Sans JP同梱／`QuestPdfLicenseConfigurator`拡張／CJK assertion／NOTICE OFL追記／ADR 0013確定 | AC4-1, AC4-2 | 0013 改 | 完了（`v0.3.1-phase4-s1`） | 解消済み |
| **S2 ✅** | (E) bulk operations 禁止＋NetArchTest ADR | `src/` ソース走査テスト（`ExecuteUpdate*/ExecuteDelete*` 検出）／NetArchTest 見送り ADR／CHANGELOG 更新 | AC4-12 | 0050・0051 | 完了（2026-08-15） | 解消済み |
| **S3a ✅** | (B) バックアップ運用化＋暗号化決着 | 終了時自動バックアップ／世代管理（同日最新1つ×7日）／復元 UseCase／保存先権限強制（`SecureFileSystem` 共通化）／暗号化 ADR 0003 決着（(a) 平文＋OSファイル権限 ＋ (c) OSディスク暗号化への委譲。SQLCipher 不採用） | AC4-3, AC4-4 | 0003 改・0052 | 完了（2026-08-16） | 解消済み |
| **S3b** | (B) 暗号化バックアップ（「控えを保存」の任意暗号化） | AES-256-GCM 単発によるバックアップファイル暗号化（spec §3 決定7） | — | 着手時に再採番 | 中（1 PR） | S3a 完了により着手可能。**未着手** |
| **S4 ✅** | (C) UI 補完 3 点＋ContractedProvider ADR | `DisabilityCertificatePolicy.FindRenewalDue`／`FaceSheetDiff.Compare`／障害種別整合警告純粋関数／既存タブへの埋め込みパネル・バナー／ContractedProvider ADR 0053 | AC4-5〜AC4-8 | 0053 | 完了（2026-08-17） | 解消済み |
| **S5** | (D) 発行＋初回セットアップ＋運用ガイド＋手動 QA | `build/publish.sh`／`build/publish.ps1`／初回セットアップウィザード VM＋View／`docs/operations.md`／`docs/manual-qa.md`／両 OS smoke 実施記録 | AC4-9〜AC4-11 | 着手時に再採番 | 中（1〜2 PR＋実機 QA） | 実機（macOS/Windows）が必要 |
| **Phase 3-1へ移管** | 請求用平均工賃月額 | ADR 0023で正式定義とPhase 2指標との責務分離を確定。`AverageWageCalculator`はPhase 3-1 Task 15で実装 | AC4-14 / AC3-2 | 0023 | Phase 3-1計画で管理 | 定義ブロッカー解消済み |

**運用値-1（KouchinModule突合）はS0で完了済み**。運用値-2（請求用平均工賃月額）はADR 0023で正式定義を確定し、Phase 2の`AverageWageMetric`を破壊変更せず、請求専用実装へ分離した。

---

## 3. 着手順

S1完了後の残順序は **S2 → S3 → S4 → S5**（Phase 4文書 §8）。並行に独立系統として動かせるが、レビュー波を作らないため直列で回す。請求用平均工賃はPhase 3-1計画で別管理する。S2完了（2026-08-15）後の残順序は **S3 → S4 → S5**。S3a完了（2026-08-16）後の残順序は **S4 → S5**。S4完了（2026-08-17）後の残順序は **S5**。S3b（暗号化バックアップ）はS3aから独立して着手できるが**未着手のまま**残っている。

---

## 4. 依存関係

```
S1 (フォント埋込)  ─┐
                    ├──→ Phase 3-2 の "PDF フォント前提" が解消（帳票着手可）
                    └──→ CHANGELOG「本番投入前 deferred」から日本語フォント削除
S2 (bulk 禁止)      ─→ S3/S4 の Repository/UseCase 追加時に "気付かず ExecuteDelete*" が入る事故を防ぐ
S3a (バックアップ)  ─→ S5 運用ガイド §4 の「バックアップ／復元」節を実装済み前提で書ける（解消済み）
S3a (暗号化 ADR)    ─→ S5 運用ガイド §5「OS ディスク暗号化の設定」の要否が確定（解消済み。FileVault／BitLocker有効化が運用要件と確定）
S4 (UI 補完)        ─→ 独立（前提なし・後続なし。2026-08-17完了）
S5 (配布)           ─→ S1〜S4 完了物すべてが実機 smoke の対象
請求用平均工賃       ─→ ADR 0023の契約をPhase 3-1 Task 15で実装
```

S1→S5・S3a→S5・S4→S5の依存はいずれも解消済み（S5のみ未着手）。S2は独立する。S3b（暗号化バックアップ）はS5運用ガイドの前提ではない（S3aの決定7で扱いを確定済み。運用ガイドはS3aの成果のみを前提に書ける）。**未着手は S5 と独立の S3b**。

---

## 5. AC マッピング（Phase 4 文書 §7）

| AC | 主担当 | 現況 | 備考 |
|---|---|---|---|
| AC4-1 日本語フォント埋込＋NOTICE 帰属 | S1 | ✅ 完了 | CJK substring assertion復活を含む |
| AC4-2 QuestPDF ライセンス ADR 0013 確定 | S1 | ✅ 完了 | 一次情報URL付き |
| AC4-3 暗号化 ADR 0003 決着 | S3a | ✅ 完了 | (a) 平文＋OSファイル権限 ＋ (c) OSディスク暗号化への委譲で確定。SQLCipherは不採用（一次情報URL・再評価トリガ3件付き。ADR 0003追補） |
| AC4-4 バックアップ運用化 | S3a | ✅ 完了 | 終了時自動＋世代（同日最新1つ×7日）＋復元＋保存先権限強制（`SecureFileSystem`）。ADR 0052 |
| AC4-5 精神手帳更新アラート | S4 | ✅ 完了 | `FindRenewalDue`純粋関数＋既存 `DisabilityCertificateView` 埋め込み |
| AC4-6 フェースシート差分表示 | S4 | ✅ 完了 | `FaceSheetDiff.Compare`＋既存 `FaceSheetView` 埋め込み |
| AC4-7 障害種別整合警告 | S4 | ✅ 完了 | 警告のみ／保存は妨げない。証・手帳の両 View にバナー |
| AC4-8 ContractedProvider 運用 ADR | S4 | ✅ 完了 | ADR 0053。工賃=`Contract`／請求CSV=`ContractedProvider`（自社行必須・ADR 0032維持） |
| AC4-9 self-contained 発行 smoke | S5 | 未着手 | `win-x64` / `osx-arm64` |
| AC4-10 初回セットアップウィザード | S5 | 未着手 | DBなし判定＋事業所登録 |
| AC4-11 運用ガイド＋手動 QA 実施記録 | S5 | 未着手 | 両OS各1回 |
| AC4-12 bulk 禁止スキャナ＋NetArchTest ADR | S2 | ✅ 完了 | 意図的違反で赤確認済み（T1〜T3で実測。証跡: ADR 0050 テスト節） |
| AC4-13 KouchinModule 突合正式化 | S0 | ✅ 完了 | ADR 0012 v2 |
| AC4-14 平均工賃月額 正式化 | Phase 3-1へ移管 | 定義確定 | ADR 0023。請求用実装はPhase 3-1 Task 15 |

---

## 6. ADR プラン（2026-07-05時点の番号案）

2026-07-12時点でADR 0020〜0026はPhase 3の決定に使用済みである。S2〜S5で新規ADRを作成するときは、着手時点の次の空き番号へ再採番する。以下の0020〜0024は当初の役割案としてのみ保持する。

| ADR | 位置づけ | Slice | 主内容 |
|---|---|---|---|
| 0003（改） | 決着 | S3 | 暗号化採否（現状権限＋OS ディスク暗号化を運用要件へ、または SQLCipher 採用の場合の移行・バックアップ整合） |
| 0023（実績） | 確定 | Phase 3-1へ移管 | 請求用平均工賃月額の正式定義、Phase 2指標との責務分離 |
| 0013（改） | 決着 | S1 | QuestPDF Community License 採否・年商閾値（一次情報 URL） |
| 再採番（当初0020） | 決着 | S2 | bulk operations 禁止のアーキテクチャテスト方針 |
| 再採番（当初0021） | 決着 | S2 | NetArchTest 採否（推奨: 見送り） |
| 再採番（当初0022） | 決着 | S3 | バックアップ運用化（世代・自動・復元・権限） |
| 0053（実績） | 確定 | S4 | ContractedProvider / Contract 責務分離（工賃=`Contract`／請求CSV=`ContractedProvider`・自社行必須。当初「自社は Contract のみ」案は不採用） |
| 再採番（当初0024） | 決着 | S5 | 配布構成（self-contained・単一ファイル・トリミング既定オフ） |

いずれも「暫定 → 確定」パターンではなく、**初手から確定**として書く（一次情報の抜けがある場合のみ、その項目だけ open-questions 継続）。

---

## 7. 平均工賃月額の移管結果

- ADR 0023で年間工賃支払総額、年間延べ利用者数、年間開所日数、公式中間丸め、R8区分・経過措置を含む請求用正式定義を確定し、`docs/open-questions.md`をクローズした。
- Phase 2の`AverageWageMetric`は工賃支払明細の暫定集計として互換保持し、請求区分には使用しない。
- 請求専用`AverageWageCalculator`、版付きマスタ入力、golden caseはPhase 3-1現行計画Task 15以降で実装する。Phase 4の残余スライスに対するブロッカーではない。

---

## 8. スライスごとの成果物 & commit 粒度

各スライスは **1 系統 = 1 PR 相当**（Phase 4 文書 §3.3）。commit は「TDD の Red→Green→Refactor」単位に細かく割り、末尾コミットで CHANGELOG／ADR／open-questions を更新。

### 8.1 S1（フォント埋込＋QuestPDF）— 完了

- `assets/fonts/NotoSansJP-Regular.otf` / `NotoSansJP-Bold.otf` / `NotoSansJP.LICENSE.txt`を埋め込み。
- `QuestPdfLicenseConfigurator.Initialize()`でCommunity License、環境フォント無効化、埋込フォント登録を冪等化。
- `QuestPdfFontRegistrationTests`、`WageStatementPdfGeneratorTests`、`WagePaymentListPdfGeneratorTests`でresource、CJK、Boldを検証。
- ADR 0013、NOTICE、open questions、Phase 3-2フォント前提、CHANGELOGを同期し、`v0.3.1-phase4-s1`として完了。

### 8.2 S2（bulk 禁止＋NetArchTest 見送り）

- `tests/Tsumugi.Infrastructure.Tests/Architecture/BulkOperationsForbiddenTests.cs`（新規、ソース走査）
- 新規ADR 2件（bulk operations禁止、NetArchTest見送り。番号は着手時に採番）
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
- バックアップ運用ADR（新規、番号は着手時に採番）
- CHANGELOG 更新
- `docs/open-questions.md`（暗号化採否項をクローズ）

### 8.4 S4（UI 補完 3 点＋ContractedProvider ADR）— 完了（2026-08-17）

**実際の採用**（着手時ロードマップ案からの差分）:

- UI は MainWindow への独立タブ追加ではなく、**既存タブへの埋め込み**（`DisabilityCertificateView` / `FaceSheetView` / `CertificateView`）。`AppSection`・ナビ順は変更していない。
- AC4-8 は当初推奨「自社は Contract のみ」ではなく **[ADR 0053](../../decisions/0053-contracted-provider-and-contract-roles.md)**（工賃=`Contract`／請求 CSV=`ContractedProvider`・自事業所行必須）で [ADR 0032](../../decisions/0032-contract-information-as-individual-input.md) と整合させた。二重入力警告・staleness 自動修復は対象外。

実装済み成果物:

- Domain: `DisabilityCertificatePolicy.FindRenewalDue`／`FaceSheetDiff.Compare`／`DisabilityConsistencyPolicy.Detect`
- Application: 上記3 Query UseCase＋DTO／`IDisabilityCertificateRepository.ListAllAsync`／`IFaceSheetRepository.ListByRecipientAsync`
- App: 既存 ViewModel／View へのパネル・バナー埋め込み（`CompositionRoot` に Query 登録）
- Tests: Domain／Application／Infrastructure／App の各層で固定
- `docs/decisions/0053-contracted-provider-and-contract-roles.md`
- `docs/open-questions.md`（手帳更新／差分／整合警告／ContractedProvider の 4 項クローズ）
- CHANGELOG 更新（Phase 4 S4 節）

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
- 配布構成ADR（新規、番号は着手時に採番。トリミング既定オフ）
- 両 OS 実機 smoke 実施 → `docs/manual-qa.md` 追記（S5 の最終コミット）
- CHANGELOG 更新

---

## 9. 参照

- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md`（親文書）
- `docs/superpowers/specs/2026-07-05-phase4-s0-kouchinmodule-and-avgwage-design.md`（S0 完了）
- `docs/open-questions.md`（クローズ対象の網羅リスト）
- `CHANGELOG.md`「本番投入前に必須の deferred」
