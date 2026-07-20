# Phase 3-3 国保連請求 CSV 生成 設計spec

- 対象フェーズ: Phase 3-3（`06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §4.3 / §7 3-3）
- 前提: Phase 3-1（垂直スライス）・Phase 3-2（snapshot v2 + 3帳票）マージ済み
- 起票日: 2026-07-20
- 正本の受け入れ基準: AC3-7（独立入力 `ProcessingMonth` がコントロールレコードへ、CP932/CRLF、外側3レコード、公式内側レコード順、バイトスナップショット一致、CSVは確定済み実効 `ClaimBatch` のみから）

## 1. 目的とスコープ

### 1.1 目的

確定済みの実効 `ClaimBatch` から、国保連（公式取込・送信システム）が取り込める請求 CSV を、Tsumugi 単独で生成できる状態に到達する。伝送・電子証明書・回線処理は範囲外（CLAUDE.md §責務境界）。

### 1.2 含む

- `Tsumugi.Infrastructure.Csv` に CSV writer 群を新設し、既存 `CsvSpecificationCatalog` を消費する
- 交換情報識別番号ごとの `IProviderRecordBuilder<T>` 群（recordType 9 種）
- CP932 / CRLF / byte 幅 / 引用規則を担う `CsvCellEncoder`、外側3レコードを組み立てる `ClaimCsvWriter`
- `ExportClaimCsvUseCase`（Application）と `IClaimCsvExportRepository`（interface）
- `ClaimCsvExport` エンティティ（Domain, append-only record）とマイグレーション
- `CsvExportView` / `CsvExportViewModel`、および `ClaimInputView` の `provider:*` セクション拡張
- `ClaimInputRequirementProvider` への `provider:*` 追加宣言（Phase 3-2 と同じ fail-closed パターン）
- R8.6 サービスコード表の seed JSON 追加と ADR
- 自前 golden CSV 3 種（Normal / Correction / CJK 混入 fail）＋ individual 適合テスト
- 依存方向テスト・オフライン検査・決定論テスト・sha256 履歴テスト

### 1.3 含まない

- 伝送、電子証明書、電子請求受付システム連携
- CSV の再取込やアップロード応答の解釈
- 令和8年6月の請求書・明細書・実績記録票の項目構造変更（指示書 §4.3 で「変更なし」と確定）
- 上限管理事業所番号のマスタ運用フロー（既存の `provider:J611:*` を消費するのみで、マスタ管理 UI は Phase 4 以降）
- Avalonia GUI での手動貫通確認（`docs/open-questions.md` の残課題に統合）

## 2. 現状資産と再利用

| 資産 | 用途 |
|---|---|
| `src/Tsumugi.Infrastructure.Csv/Specifications/provider-claim-r7-10.json` | fieldId・byte 幅・required・code・引用規則の正本 |
| `src/Tsumugi.Infrastructure.Csv/Specifications/field-mapping-r7-10.json` | entity path → fieldId マッピング |
| `CsvSpecificationLoader` / `CsvSpecificationCatalog` | JSON をロードし field / record を辞書化 |
| `Tsumugi.Domain.ValueObjects.ProcessingMonth` / `ServiceMonth` | 別型で暗黙変換なし |
| `ClaimBatchAggregate` | 確定状態（`Finalized`）を保持 |
| `ClaimInputRequirementProvider`（Csv） | `report:*` 21 フィールド + certificate/office の `provider:*` 一部を宣言済み |
| `Real_embedded_requirement_provider_*` テスト群 | production wiring での fail-closed 固定パターン |
| ADR 0014 / 0024 / 0026 / 0029 / 0030 | append-only / kokuhoren-csv / claim-batch-snapshot / codec-v2 / report-input-ui-responsibility |

## 3. アーキテクチャ

### 3.1 レイヤと依存方向（既存規律を継承）

```
App (CsvExportView, ClaimInputView 拡張)
  └→ Application (ExportClaimCsvUseCase, IClaimCsvExportRepository)
        └→ Domain (ClaimCsvExport, ProcessingMonth, ClaimBatch)
              ↑
Infrastructure.Csv (IProviderRecordBuilder<T>, CsvCellEncoder, ClaimCsvWriter, RecordIdRouter)
  └→ Application/Domain のみ参照

Infrastructure (ClaimCsvExportRepository, EF Core, Migrations)
  └→ Application/Domain のみ参照
```

- `Tsumugi.Domain` は Infrastructure/Csv/UI を知らない。
- `Tsumugi.Infrastructure.Csv` は Domain/Application のみ参照する。EF Core は参照しない。
- 依存方向テストは Phase 3-2 と同じ shape で `Tsumugi.Infrastructure.Csv` 新規シンボル群を追加検証する。

### 3.2 データフロー（1 本のスライス）

```
[UI]
  CsvExportView
    ・確定済み ClaimBatch 一覧を表示（Aggregate 経由）
    ・ProcessingMonth (YYYYMM) 必須入力
    ・「CSV 生成」押下

[Application]
  ExportClaimCsvUseCase.Execute(claimBatchId, processingMonth)
    1. ClaimBatchAggregate 取得 → state != Finalized なら例外
    2. ClaimCsvSpecCatalog から適用版を解決（spec version + reward master version）
    3. RecordIdRouter が ClaimBatch を recordType 9 種に振り分け
    4. 各 IProviderRecordBuilder<T> が strong-typed に CsvRow を組み立て
    5. CsvFieldSpecification に照合（byte 幅 / required / code 値 / 引用規則）
    6. CsvCellEncoder が CP932 変換（変換不能は fieldId 付き例外）
    7. ClaimCsvWriter が外側3レコード（control=1, data=2..n, end=3）に整形
    8. sha256 を計算し ClaimCsvExport（append-only）に書き込み
    9. バイト列 + 提案ファイル名 を UseCase 結果として返す

[App/UI]
  IFileSaveService でユーザーが指定した先へ保存
```

### 3.3 主要コンポーネント

| コンポーネント | 責務 | 所属 |
|---|---|---|
| `ClaimCsvExport` | 出力履歴 record（append-only）: `Id`, `ClaimBatchId`, `ProcessingMonth`, `CsvSpecVersion`, `RewardMasterVersion`, `Sha256`, `CreatedAtUtc`, `CreatedBy` | Domain |
| `ExportClaimCsvUseCase` | 上記フローの調停。確定済みガード・失敗時履歴の**非追記**・成功時履歴追記 | Application |
| `IClaimCsvExportRepository` | 追記のみ（`AppendAsync`）・照会（`ListByBatchAsync`） | Application |
| `RecordIdRouter` | ClaimBatch を recordType（`provider:J111:01` 等）ごとの入力 DTO へ振り分ける | Infrastructure.Csv |
| `IProviderRecordBuilder<T>` (9 実装) | recordType 単位で `CsvRow` を組む strong-typed builder | Infrastructure.Csv |
| `CsvCellEncoder` | byte 幅 / 引用規則 / CP932 変換の一元化。fail-close で `ClaimCsvExportFailedException` | Infrastructure.Csv |
| `ClaimCsvWriter` | control/data/end の外側3レコードを組み立て、CRLF を付与 | Infrastructure.Csv |
| `ClaimCsvExportFailedException` | `RecordId`, `FieldId`, `Reason`, `RowIndex`, `RecipientReferenceCode?`（**氏名・受給者証番号は含めない**） | Application |
| `CsvExportViewModel` | 確定済み ClaimBatch 選択・ProcessingMonth 入力・エラー表示・保存 | App |

### 3.4 recordType 9 種と builder マッピング

| recordId | 内側レコード | builder 入力型（案） |
|---|---|---|
| `provider:J111:01` | 介護給付費・訓練等給付費請求書 総括 | `ClaimInvoiceSource` |
| `provider:J111:02` | 上記 集計 | `ClaimInvoiceTotalsSource` |
| `provider:J121:01` | 明細書 契約情報 | `ClaimContractSource` |
| `provider:J121:02` | 明細書 サマリ | `ClaimStatementSummarySource` |
| `provider:J121:03` | 明細書 サービス単位数明細（複数行） | `ClaimStatementLineSource` |
| `provider:J121:04` | 明細書 集計・例外利用日 | `ClaimStatementAggregateSource` |
| `provider:J121:05` | 明細書 経過措置 | `ClaimTransitionalSource` |
| `provider:J611:01` | 上限管理結果票 総括 | `UpperLimitManagementHeaderSource` |
| `provider:J611:02` | 上限管理結果票 明細 | `UpperLimitManagementLineSource` |

※ 実際の対応は `field-mapping-r7-10.json` の恒久 completeness テスト（`ClaimFieldMappingCompletenessTests`）で固定する。

## 4. `provider:*` フィールドの入力 UI と readiness

### 4.1 UI 配置

ADR 0030（report-input-ui-responsibility）に整合し、**人手入力は `ClaimInputView` に集約**。CSV 出力画面は「確定済み ClaimBatch 選択 + `ProcessingMonth` + エラー表示 + 保存」に責務を限定する。

`ClaimInputView` に `provider:*` 追加セクションを設ける。既存の Certificate / Office / Upper limit / Municipal subsidy と並列に、以下カテゴリを追加する（正確なフィールド ID は `field-mapping-r7-10.json` に従い、実装計画で列挙する）：

- 契約情報（provider:J121:01）— 契約支給量、開始日、初回契約日、事業者記入欄
- 経過措置・例外利用日（provider:J121:04:030-033、provider:J121:05）— 例外利用開始月・終了月・日数・標準利用日総数、経過措置種別
- 上限管理関連 追加項目（provider:J611:*）— 管理対象月・管理事業所内訳（既存 `Certificate.UpperLimitManagementProviderNumber` と連動）
- 明細書 集計 追加項目（provider:J121:02/04）— 保護者負担額・その他調整項目

### 4.2 孤立 4 フィールドの cross-field readiness

Phase 3-2 の Known limitations §5 で残された `provider:J121:04:030-033`（`ExceptionalUsageStartMonth` / `EndMonth` / `Days` / `StandardUsageDayTotal`）は、Phase 3-3 で **cross-field Any-merge 条件**を宣言する：

- 「4 つのいずれかが値を持てば、残り 3 つを required 化」
- `ClaimInputRequirementProvider` に登録し、`Provider_combines_exceptional_usage_cross_field_condition_via_any` 相当のユニットテストで宣言を固定
- `Real_embedded_requirement_provider_requires_all_exceptional_usage_fields_when_any_is_entered` / `_does_not_require_when_all_absent` を production wiring テストに追加

### 4.3 spec JSON からの completeness

`field-mapping-r7-10.json` に**すべての `provider:*` fieldId に対応する `entityPath` を存在させる**（`ClaimFieldMappingCompletenessTests` 拡張）。マッピング欠落は CI で赤化する。

## 5. R8.6 サービスコード表の統合

### 5.1 前提

指示書 §4.3 末尾：「令和8年6月資料で『請求書明細書・実績記録票は変更なし』とされたため、CSV 項目構造は令和7年10月事業所編を基準にし、令和8年6月のサービスコード表を組み合わせる」

### 5.2 方針

- 新規 seed JSON `src/Tsumugi.Infrastructure/Data/Seed/service-code-r8-06.json` を追加
- 既存 rewardmaster（R6 系、ADR 0027/0028）とは**別テーブル・別 effectiveFrom**（`effectiveFrom = 2026-06`）で管理
- CSV 生成時のサービスコード解決は `IServiceCodeCatalog`（`Tsumugi.Infrastructure.Csv` 新規 interface）越しに行い、Application/Domain は直接この JSON を知らない
- ADR **0031-service-code-r8-06-service-code-table.md** を新規起票し、出典（一次資料 URL・SHA256・取得日）・スキーマ・effectiveFrom を記録

### 5.3 open-question の解消

`docs/open-questions.md` の該当項目に **「Phase 3-3 で ADR 0031 を起票して解消」** の追記を行い、着地点を明示する。

## 6. エラー処理と fail-close

| 事象 | 検出タイミング | 例外 | UI 表示 |
|---|---|---|---|
| 確定していない ClaimBatch を選択 | UseCase 冒頭 | `ClaimBatchNotFinalizedException` | 「未確定のため出力できません。先に確定してください」 |
| `provider:*` 必須項目欠落（cross-field 含む） | `ClaimInputRequirementProvider` readiness 判定 | `ClaimCsvReadinessException` | 該当 fieldId ラベルの列挙（氏名・受給者証番号は出さない） |
| byte 幅超過 | `CsvCellEncoder` | `ClaimCsvExportFailedException(Reason=OverByteWidth)` | fieldId + 位置 |
| CP932 変換不能 | `CsvCellEncoder`（`EncoderFallbackException`） | `ClaimCsvExportFailedException(Reason=NonRepresentableCharacter)` | fieldId + 位置 + 参照コード（受給者番号ではなくシステム内 code） |
| 引用規則違反 | `CsvCellEncoder` | 同上 | 同上 |
| コード値未登録 | builder | `ClaimCsvExportFailedException(Reason=UnknownCodeValue)` | 同上 |

**失敗時は ClaimCsvExport 履歴を追記しない**。中途半端な出力は残さない。バイト列を返さず、UseCase 呼び出し側で例外→UI 表示のみ。

## 7. 決定論

- 同一 `ClaimBatch` + `ProcessingMonth` + spec 版 + reward master 版 + service code 版 なら**バイト列一致**（sha256 一致）
- `CreatedAtUtc` は `ITimeProvider`（`FakeTimeProvider` 差し替え可）から取得
- ソート順は builder 内で入力に対して安定なキー（例：`RecipientCode`, `ServiceCode`, `LineIndex`）で確定
- 3 帳票と同じ決定論テストパターン（`Generate_is_deterministic_for_same_inputs_and_timeprovider` 相当）を CSV に対して実施

## 8. テスト戦略

### 8.1 Csv 単体（`Tsumugi.Infrastructure.Csv.Tests`）

- `IProviderRecordBuilder<T>` 9 種の適合テスト（Theory で fieldId × 期待セル値）
- `CsvCellEncoder`：byte 幅・quote・CP932 fail・NUL 混入 fail
- `ClaimCsvWriter`：control=1 / data=2..n / end=3 の順序と件数
- 既存 completeness テスト拡張：`Provider_registers_provider_star_all_fields`

### 8.2 Application（`Tsumugi.Application.Tests`）

- `ExportClaimCsvUseCaseTests`：確定済みガード、fail-close 経路（byte 幅 / CP932 / cross-field readiness）、成功時 sha256、履歴追記
- `ClaimCsvExportRepository` は Infrastructure.Tests 側

### 8.3 Production wiring（`Tsumugi.Infrastructure.Tests`）

- 実 `JsonClaimMasterProvider`・実 spec catalog・実 `ClaimCsvSpecCatalog` を通した 3 種 golden CSV
  - `Normal`：通常請求 1 人 5 明細
  - `Correction`：訂正レコード（追記型）
  - `Cjk_and_kangxi_normalization`：Kangxi radical → CJK Unified への `KangxiRadicalNormalizer` 経路の byte 一致
- 孤立 4 フィールドの production wiring fail-closed テスト（§4.2）

### 8.4 Architecture / offline

- 依存方向テスト：`Tsumugi.Infrastructure.Csv` の新規シンボル群が Application/Domain のみに依存
- オフライン検査：`Tsumugi.Infrastructure.Csv` を対象アセンブリに含めた通信 API allowlist（既定空）

## 9. マイグレーション（EF Core）

- テーブル `ClaimCsvExport`（列：Id, ClaimBatchId FK, ProcessingMonth, CsvSpecVersion, RewardMasterVersion, ServiceCodeVersion, Sha256, CreatedAtUtc, CreatedBy）
- **append-only のため更新トークンは持たせない**（`ClaimBatch` 等 update パスを持つエンティティとは扱いが異なる）。訂正・取消は「新しい `ClaimCsvExport` 行を追記する」形で表現し、既存行は不変
- FK: `ClaimBatchId` は既存 `ClaimBatch` に対する外部キー制約、`OnDelete: Restrict`（履歴を守る）
- Migration 名：`Phase33ClaimCsvExport`

## 10. ADR / open-questions の更新

- **ADR 0031 新規**：`0031-service-code-r8-06-service-code-table.md`（R8.6 サービスコード表の出典・スキーマ・effectiveFrom）
- **ADR 0024 補足**：CSV writer のハイブリッド方式（strong-typed builder + spec-driven encoding）を追記
- **ADR 0030 補足**：`provider:*` 追加も `ClaimInputView` に集約する（責務境界の明示）
- **`docs/open-questions.md`**：孤立 4 フィールド項目に「Phase 3-3 で cross-field readiness 化」の完了記録、GUI 手動貫通確認は継続項目のまま

## 11. 受け入れ基準マッピング

| AC | 検証手段 |
|---|---|
| AC3-7 (`ProcessingMonth` → コントロールレコード) | golden CSV `Normal` の control record の processing-month バイト位置固定検証 |
| AC3-7 (CP932/CRLF) | `CsvCellEncoder` 単体テスト + golden CSV バイト完全一致 |
| AC3-7 (外側3レコード) | `ClaimCsvWriter` 単体テスト（control=1, data=2..n, end=3） |
| AC3-7 (公式内側レコード順) | `RecordIdRouter` テストで J111:01 → J111:02 → J121:01..05 → J611:01/02 順を固定 |
| AC3-7 (バイトスナップショット一致) | 3 種 golden CSV の byte-exact 比較 |
| CSV は確定済み実効 ClaimBatch のみから | `ClaimBatchNotFinalizedException` 経路テスト + preview→CSV 経路が型上到達不能 |
| 決定論 | `Generate_csv_is_deterministic_for_same_inputs_and_timeprovider` |
| CJK 正規化 | Kangxi radical 混入 golden CSV でバイト一致 |
| 依存方向 / オフライン | 既存 architecture test 拡張 |

## 12. リスクと緩和

| リスク | 緩和 |
|---|---|
| R8.6 サービスコード表の一次資料が入手困難 | ADR 0031 に「取得先・SHA256・取得日」を必ず埋める。埋められなければ `docs/open-questions.md` に落として Phase 3-3 スコープから除外を明示 |
| 30 フィールド分の UI 追加で ClaimInputView が肥大化 | セクション（`Expander`）で折り畳み、`ClaimInputViewModel` を partial に分割 |
| CP932 例外の粒度不足で氏名・受給者証番号がログに出る | ロガーは `RecipientReferenceCode`（内部 code）のみを持たせ、氏名・番号は禁止（CLAUDE.md §制約4） |
| Aavlonia GUI 手動貫通確認未実施 | 既存 open-question に統合、CI 自動化テスト（ViewModelTests + production wiring）で担保 |

## 13. 想定される着手順（実装計画への申し送り）

1. `ClaimCsvExport` エンティティ + マイグレーション + repository（fail-closed テスト同伴）
2. `CsvCellEncoder`（byte 幅 / quote / CP932 / NUL）単体テスト先行
3. `IProviderRecordBuilder<T>` 9 種を空実装で骨組み → 順次埋める
4. `ClaimCsvWriter`（control/data/end フレーム）+ `RecordIdRouter`
5. `ClaimInputRequirementProvider` に `provider:*` 追加宣言 + 孤立 4 フィールドの cross-field 条件
6. `ClaimInputView` に `provider:*` セクション追加（既存 partial pattern 踏襲）
7. `ExportClaimCsvUseCase` + `IClaimCsvExportRepository`（append-only）
8. `CsvExportView` / `CsvExportViewModel`
9. R8.6 サービスコード表 seed + ADR 0031
10. 3 種 golden CSV fixture 追加と production wiring バイト一致テスト
11. 依存方向・オフライン検査の拡張
12. `docs/phase3-3-acceptance.md` を書き起こし、AC3-7 5 項目のセルフチェック
13. `./build/ci.sh` 全緑を確認して Codex レビューへ

---

## 参照

- CLAUDE.md（プロジェクト常設指示）
- `01_ClaudeCode_実装指示書_Tsumugi.md`（全体仕様）
- `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`（Phase 3 正本）
- `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`（Phase 3-1 spec）
- `docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md`（Phase 3-2 spec）
- `docs/phase3-2-acceptance.md`（Phase 3-2 受け入れ証跡・Known limitations）
- `docs/decisions/0014-audit-trail-append-only.md`
- `docs/decisions/0020-claim-master-sources-and-versioning.md`
- `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`
- `docs/decisions/0026-claim-batch-snapshot.md`
- `docs/decisions/0029-claim-snapshot-codec-v2.md`
- `docs/decisions/0030-report-input-ui-responsibility.md`
- `docs/open-questions.md`
