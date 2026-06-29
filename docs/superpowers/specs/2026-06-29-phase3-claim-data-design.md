# Tsumugi Phase 3 設計 — 国保連請求データ生成

> **Source**: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` を現状リポジトリ実態（Phase 0〜2 完了）に接地して設計化したもの。
> **Status**: 設計合意済（2026-06-29）。実装計画は writing-plans スキルで本書から派生させる（3-0/3-1/3-2/3-3 の 4 本に分割）。
> **着手規律**: §2.3 の一次出典が未確定の間はそれに依存する実装に着手しない（推測実装の禁止）。

## 目的

就労継続支援B型事業所向け Tsumugi で **国保連請求データの生成**を実装する。責務は「公式インターフェース仕様準拠の請求データ(CSV)を生成し、公式の取込・送信システムにインポートできる状態」まで。**伝送・電子証明書・回線処理は範囲外**（オフライン維持）。

## 責務境界（再確認）

- 出力 CSV は公式インターフェース仕様に厳密準拠する
- アプリは `System.Net.*` を含む通信 API を**全プロダクションアセンブリ**で直接参照しない（オフライン検査の対象に `Tsumugi.Infrastructure.Csv` を追加）
- 報酬の単位数・加算・地域区分単価・CSV 項目定義は**コードに置かず、適用年月でバージョン管理した外部マスタ(seed JSON)** に置く。Domain は純粋な算定ロジックのみを持つ
- 算定金額は整数円、最終額に浮動小数点を混入させない。端数規則は告示準拠でマスタ/設定に明記しテストで固定

## アーキテクチャ

既存の 4 プロジェクト構成（`Tsumugi.Domain` / `.Application` / `.Infrastructure` / `.App`）と、Phase 2 で導入された `Tsumugi.Infrastructure.Reporting` を維持しつつ、新たに `Tsumugi.Infrastructure.Csv` を追加する。

```
src/
  Tsumugi.Domain/                   record・値オブジェクト・報酬算定の純粋ロジック
    Entities/
      ClaimBatch.cs                 [新規] 請求確定スナップショット（append-only）
      ClaimDetail.cs                [新規] 請求明細（ClaimBatch 配下）
    Logic/
      Claim/
        IRateMaster.cs              [新規] 単価マスタ抽象
        IAdditionMaster.cs          [新規] 加算マスタ抽象
        IBurdenCapMaster.cs         [新規] 負担上限額マスタ抽象
        IRegionUnitMaster.cs        [新規] 地域区分単価マスタ抽象
        ClaimCalculator.cs          [新規] 算定本体（純粋関数）
        BasicAllowanceClassifier.cs [新規] B型基本報酬区分の解決（純粋関数）
        AdditionRules/              [新規] 加算ごとに 1 関数（純粋関数）
        BurdenCalculator.cs         [新規] 利用者負担＋月額上限管理
        RoundingPolicy.cs           [新規] 端数規則（ADR 0025）

  Tsumugi.Application/
    Abstractions/
      IClaimBatchRepository.cs      [新規]
      IClaimReportGenerator.cs      [新規] 請求帳票 PDF 抽象
      ICsvClaimWriter.cs            [新規] CSV 書出し抽象
    UseCases/Claim/
      CalculateClaimUseCase.cs      [新規] プレビュー算定
      QueryClaimUseCase.cs          [新規] 確定済参照／未確定再算定
      GenerateClaimReportsUseCase.cs[新規]
      ExportClaimCsvUseCase.cs      [新規]
      CloseClaimUseCase.cs          [新規] 確定スナップショット追記
    Dtos/Claim/                     [新規]

  Tsumugi.Infrastructure/
    Persistence/
      ClaimBatchRepository.cs       [新規]
      Configurations/
        ClaimBatchConfiguration.cs  [新規] partial unique index (OfficeId, YearMonth) WHERE Kind=New
        ClaimDetailConfiguration.cs [新規]
      JsonClaimMasterLoader.cs      [新規] マスタ JSON 読込
    Seed/Claim/                     [新規]
      rates-v1.json
      additions-v1.json
      burden-caps-v1.json
      region-units-v1.json
      meta.json                     出典 ADR 番号 / 改定版 / 取得日 / 取得 URL
    Migrations/
      <yyyyMMddHHmmss>_AddClaimBatchAndDetail.cs [新規]
      <yyyyMMddHHmmss>_ExpandRecipientForCsv.cs  [新規, 3-3 で]

  Tsumugi.Infrastructure.Reporting/
    QuestPdfLicenseConfigurator.cs  [拡張] Settings.UseEnvironmentFonts=false + 埋込フォント登録
    ClaimReportGenerator.cs         [新規]
    assets/fonts/NotoSansCJKjp/     [新規] OFL 1.1 同梱

  Tsumugi.Infrastructure.Csv/       [新規アセンブリ]
    ClaimCsvWriter.cs               ICsvClaimWriter 実装
    Records/                        レコード種別ごとの構造体（ADR 0023 確定後）
    Encoding/                       Shift_JIS/UTF-8 と CRLF 規律
    Tsumugi.Infrastructure.Csv.csproj

  Tsumugi.App/
    ViewModels/Claim/               [新規] ClaimPreview / ClaimReport / ClaimCsvExport / ClaimClose
    Views/Claim/                    [新規] 対応 View
```

依存方向は変更なし: `App → Application → Domain`、`Infrastructure → Application/Domain`、`Infrastructure.Reporting → Application/Domain`、`Infrastructure.Csv → Application/Domain`。`Domain` は EF/Avalonia/Reporting/Csv を直接参照しない（`ArchitectureTests` 機械判定）。

## 全体構造（4 サブフェーズ）

### Phase 3-0 土台フェーズ（出典確定＋非ブロッキング先行）
- §2.3-1〜6 を一次情報で確認し ADR 0018〜0023 として文書化
- Noto Sans CJK JP 埋込（ADR 0024）→ Phase 2 残課題の解消
- ハードコード機械判定スキャナ (a)(b)(c) を ArchitectureTests / OfflineComplianceTests に追加
- マスタ抽象（interface + InMemory 実装）と JSON ローダ空殻
- `Tsumugi.Infrastructure.Csv` アセンブリ新設（空殻＋オフライン検査追加）
- `ClaimBatch` / `ClaimDetail` 骨組み（AppendOnlyGuard 登録、partial unique index、migration）
- `ci.sh` Application 閾値底上げ準備（3-3 完了時に 70→90 へ）

### Phase 3-1 報酬算定エンジン＋マスタ実値
- ADR 0018/0019/0021/0022 確定後に着手
- B 型基本報酬区分（平均工賃月額連動）の純粋関数
- 加算/減算マスタ駆動の純粋関数（食事・送迎・配置・欠席時対応・上限管理 等）
- 地域区分単価計算（端数規則含む、ADR 0025）
- 利用者負担計算＋月額上限管理
- 適用年月差し替えテスト（令和 6 → 令和 9）
- Application: `CalculateClaimUseCase` / `QueryClaimUseCase`

### Phase 3-2 帳票（フォント埋込済の Reporting に追加）
- AC3-0-2（フォント埋込）が緑になってから着手
- サービス提供実績記録票（A4, 利用者・月次）
- 介護給付費／訓練等給付費等 請求書／明細書（事業所・月次）
- Application: `IClaimReportGenerator` / `GenerateClaimReportsUseCase`
- App: 帳票出力 View + ViewModel + `IFileSaveService` 経由保存

### Phase 3-3 CSV 生成＋請求確定
- ADR 0023 確定後に着手
- `Tsumugi.Infrastructure.Csv` に正式仕様のレコード種別を実装
- Recipient 拡張（性別等の CSV 必須項目）＋migration
- `ClaimBatch` 確定スナップショット追記（`CloseClaimUseCase`）
- 再生成は `Correct` レコード（新スナップショット）として履歴に残す
- バイト単位スナップショットテスト
- App: CSV エクスポート / 請求確定 View + ViewModel
- `ci.sh` の Application カバレッジ閾値 70 → 90 へ昇格

## マスタ JSON 設計

### 配置とスキーマ

`src/Tsumugi.Infrastructure/Seed/Claim/` 配下にマスタ種別ごとに 1 ファイル、適用開始年月でファイル内 entries を持つ。

| ファイル | 内容 | キー |
| --- | --- | --- |
| `rates-v1.json` | 報酬単位数マスタ | (サービスコード, 適用開始年月, 定員規模) |
| `additions-v1.json` | 加算マスタ | (加算コード, 適用開始年月, 条件) |
| `burden-caps-v1.json` | 負担上限額マスタ | (PaymentBurdenCategory, 適用開始年月) |
| `region-units-v1.json` | 地域区分単価マスタ | (RegionGrade, ServiceCategory, 適用開始年月) |
| `meta.json` | 出典トレーサビリティ | ADR 番号 / 改定版 / 取得日 / 取得 URL |

### Domain が参照する抽象

```
Tsumugi.Domain.Logic.Claim
  IRateMaster.LookupBasic(ServiceCode, YearMonth, CapacityClass) → UnitCount
  IAdditionMaster.LookupAddition(AdditionCode, YearMonth, Conditions) → UnitCount | Rate
  IBurdenCapMaster.LookupCap(PaymentBurdenCategory, YearMonth) → MonthlyCapYen
  IRegionUnitMaster.LookupUnitPrice(RegionGrade, ServiceCategory, YearMonth) → UnitPriceMoney
```

実装は `Tsumugi.Infrastructure/Persistence/JsonClaimMasterLoader.cs` で起動時に JSON を読み込み。**オフラインなのでファイルバンドルで OK**。

### 適用年月差し替え（無改修切替）

ファイル内 `effectiveFrom` を増やすだけで令和 6 改定 → 令和 9 改定が切替可能であることをテストで実証（AC3-4）。Domain は interface のみ参照しているため、マスタ実値の変更は再ビルド不要（JSON 入替で済む）。

## `ClaimBatch` の append-only 規律

| 項目 | 内容 |
| --- | --- |
| 粒度 | `(OfficeId, YearMonth)` 単位で 1 行 |
| 永続化 | `record` + append-only（`AppendOnlyGuard.AppendOnlyTypes` 登録） |
| 重複 New 防止 | partial unique index `(OfficeId, YearMonth) WHERE Kind = New`（ADR 0026） |
| 履歴 | `RecordKind` ∈ `{ New, Correct, Cancel }`（既存 enum）、`OriginId` で初代 New を指す |
| 再現性 | 算定時のマスタ版バージョン（`meta.json` から）を保持 |
| 監査 | `CreatedAt` / `CreatedBy` / `ConcurrencyToken`（既存 `Entity` 基底） |
| 明細 | `ClaimDetail` を `ClaimBatchId` で結合（受給者×明細行：基本報酬・各加算） |

**再生成は `Correct` で表現**: 確定後に実績や受給者証が訂正されても自動再算定しない。明示的に `CloseClaimUseCase` を再実行すると `Correct` レコードが積まれる。返戻・過誤調整の運用フロー自動化は本フェーズの範囲外（運用ガイドへ）。請求取下げは `Cancel` レコードとして表現。

## `Tsumugi.Infrastructure.Csv` — 新アセンブリ

### 理由
- CSV カラム名 literal の名前空間境界 (b) を機械判定する以上、**境界を物理的に区切るのが安全**（名前空間だけだと `Tsumugi.Infrastructure` の他コードから漏れる）
- `OfflineComplianceTests` の `[Theory]` パターン（Reporting と同じ流儀）に乗る
- 依存: `Tsumugi.Application` / `Tsumugi.Domain` のみ参照、外部依存は標準 `System.Text.Encoding` / `System.IO` のみ

### 構成
```
src/Tsumugi.Infrastructure.Csv/
  ClaimCsvWriter.cs               ICsvClaimWriter 実装
  Records/                        レコード種別ごとの構造体（ADR 0023 確定後）
  Encoding/                       Shift_JIS/UTF-8（仕様確認後に確定）と CRLF 規律
  Tsumugi.Infrastructure.Csv.csproj
```

### メタ情報の埋込
生成 CSV のヘッダ or ファイル名に **マスタ版 / CSV 仕様版 / 生成日時** を含める（再現性）。

## ハードコード機械判定 (a)(b)(c)

| ID | 対象 | 実装場所 |
| --- | --- | --- |
| (a) | Domain/Application 内に `単位数`/`加算`/`区分単価` 等の語彙 literal が現れたら赤 | `tests/Tsumugi.Domain.Tests/ArchitectureTests/HardcodeScannerTests.cs` |
| (b) | CSV カラム名 literal が `Tsumugi.Infrastructure.Csv` 以外に現れたら赤 | `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests` ファミリに `CsvLiteralNamespaceTests` |
| (c) | Domain 内の int/decimal literal 上限ガード（例: 1000 超を禁止） | `tests/Tsumugi.Domain.Tests/ArchitectureTests/IntegerLiteralCeilingTests.cs` |

各スキャナに**意図的違反で赤になるテスト**（歯のある検査）を併設。除外: テストプロジェクト、`Tsumugi.Infrastructure/Seed/Claim/*.json`、属性付きの allowlist エントリ。

## 既存資産の改修ポイント

| 既存資産 | 改修 | 着手フェーズ |
| --- | --- | --- |
| `AverageWageMetric` | 正式定義 ADR 0022 確定後、`FIXME` を解消。分母切替の構造は維持 | 3-1 |
| `OfficeCapability.Flags` | 暫定キー（`mealProvision`/`transportSupport`）→ 正式コード集合へ置換（ADR 0020 / 0019） | 3-0（ADR）→ 3-1（コード置換） |
| `Recipient` | CSV 必須項目（性別等）を追加、`Certificate.RecipientGender` と整合 | 3-3 |
| `QuestPdfLicenseConfigurator` | `Settings.UseEnvironmentFonts = false` + 埋込フォント登録 | 3-0 |
| `ci.sh` Application 閾値 | 70 → 90 へ昇格 | 3-3 完了時 |

## UI 範囲

Phase 2 と同じ流儀（CommunityToolkit.Mvvm + `ObservableObject` + `ICommand`、AccessibilityDefaults 踏襲）。View XAML 構造は実装時に決定、設計時はビヘイビアまで。

| View | 機能 |
| --- | --- |
| `ClaimPreviewView` | 月次プレビュー（明細・利用者負担表示、再算定可能） |
| `ClaimReportView` | 帳票出力（実績記録票/請求書/明細書、`IFileSaveService` で保存） |
| `ClaimCsvExportView` | CSV エクスポート（保存先指定、出典版表示） |
| `ClaimCloseView` | 請求確定（確定済の場合は再実行が `Correct` レコードを追加する旨を警告ダイアログで明示） |

## 受け入れ基準

### Phase 3-0
- **AC3-0-1** §2.3-1〜6 の各項目について、一次情報に基づき ADR 0018〜0023 を文書化（出典 URL／改定版／取得日／結論／影響範囲）
- **AC3-0-2** Noto Sans CJK JP を `assets/fonts/` に SIL OFL 1.1 同梱で追加、`QuestPdfLicenseConfigurator` で埋込登録（ADR 0024）。既存 PDF テストで CJK substring assertion を再有効化し、Linux/Windows CI ランナーで NUL 化しない
- **AC3-0-3** ハードコード機械判定 (a)(b)(c) が CI で緑、各スキャナに歯あり性テスト
- **AC3-0-4** マスタ抽象 interface 群と InMemory 実装が揃い、JSON ローダ空殻が配置される
- **AC3-0-5** `Tsumugi.Infrastructure.Csv` アセンブリ新設、`OfflineComplianceTests` / `AppOfflineComplianceTests` の `[Theory]` 対象
- **AC3-0-6** `ClaimBatch` / `ClaimDetail` 骨組み（型・`AppendOnlyGuard` 登録・partial unique index・migration・Repository）と `ClaimBatchDuplicateNewIndexTests` 緑
- **AC3-0-7** Application カバレッジ閾値 70 → 90 への昇格準備（3-3 完了時に実引き上げ）
- **AC3-0-8** `OfficeCapability` 正式コード集合への移行 ADR 0020 と移行スキーマ
- **AC3-0-9** `./build/ci.sh` 緑、`dotnet format` 通過、依存方向不変、Domain カバレッジ 95% 維持、オフライン検査（App/Reporting/Csv）緑

### Phase 3-1
- **AC3-1** 単価/加算/負担上限が seed JSON（適用年月版）から供給、Domain 内に数値 literal 無し（(a)(c) 緑）
- **AC3-2** B 型基本報酬区分が `AverageWageMetric`（ADR 0022 確定後）連動でマスタ駆動解決
- **AC3-3** 加算/減算・地域区分単価・利用者負担上限がマスタ駆動で算定、既知ケース対応表で分岐網羅 100% 目標
- **AC3-4** 適用年月差し替え（令和 6 → 令和 9）で算定結果が無改修切替されることをテストで実証
- **AC3-1-add** Domain カバレッジ 95% 維持、報酬算定モジュール（`Tsumugi.Domain.Logic.Claim`）は 100% 目標

### Phase 3-2
- **AC3-5** 日本語フォント埋込が有効化され、CI で CJK が化けない
- **AC3-6** 3 種類の帳票（実績記録票・請求書・明細書）を出力でき、抽出テキスト＋合計＋CJK が検証され、決定化されている（同入力＋同 TimeProvider → 同バイト）

### Phase 3-3
- **AC3-7** 請求 CSV が公式仕様（ADR 0023 出典版明記）準拠、バイト単位スナップショット緑、カラム名 literal が `Tsumugi.Infrastructure.Csv` 限定（(b) 緑）、新規アセンブリがオフライン検査対象
- **AC3-8** Recipient の CSV 必須項目拡張＋migration、`Certificate.RecipientGender` と整合
- **AC3-9** 請求確定が append-only スナップショットで固定、確定後の下層訂正で自動再生成しない。再生成は `Correct` レコードで履歴に残す（取下げは `Cancel`）。`AppendOnlyGuard` 登録・違反で赤・partial unique index
- **AC3-3-add** `ci.sh` の Application カバレッジ閾値を 70 → 90 へ昇格

### 横断（Phase 3 全体）
- **AC3-10** 伝送系コードが存在せず、オフライン検査（App/Reporting/Csv）が緑。`./build/ci.sh` 緑・`dotnet format` 通過・依存方向不変・Domain カバレッジ 95% 維持・Application 90% 達成（3-3 完了時）

## 新規 ADR 一覧

| ADR | タイトル | 着手フェーズ |
| --- | --- | --- |
| 0018 | 報酬単位数・地域区分単価の一次出典（令和 6 改定） | 3-0 |
| 0019 | 加算正式コード集合（食事提供 I/II・送迎 I/II 等） | 3-0 |
| 0020 | OfficeCapability 正式コード集合への移行戦略 | 3-0 |
| 0021 | PaymentBurdenCategory → 月額上限額テーブル | 3-0 |
| 0022 | 平均工賃月額の正式定義（分母・基準期間・控除） | 3-0 |
| 0023 | 国保連 請求 CSV インターフェース仕様（版・文字コード・改行・項目順） | 3-0 |
| 0024 | Noto Sans CJK JP の埋込（SIL OFL 1.1） | 3-0 |
| 0025 | 報酬計算の端数規則（単位×単価の切り捨て規律） | 3-1 |
| 0026 | ClaimBatch partial unique index `(OfficeId, YearMonth) WHERE Kind=New` | 3-0 |

## テスト戦略

### Phase 3-0
- ADR レビュー（手動）: 各 ADR が一次情報 URL を含み、後続フェーズ着手の前提として参照可能
- フォント埋込決定論: 既存 `Statement_pdf_is_deterministic_for_same_inputs_and_same_timeprovider` を CJK 抽出付きで再検証
- スキャナ歯あり性: 各スキャナに意図的違反コードを混入させ、赤になることを `ScannerToothTests`（仮）で固定
- `ClaimBatch` CRUD + AppendOnly + duplicate-new + partial unique index: 既存 `DailyRecord` / `WageFund` の往復テストパターンを踏襲
- マスタ抽象テスト: InMemory 実装で各 `Lookup*` の境界条件（適用年月境界・存在しないコード・複数バージョン）を網羅
- `Tsumugi.Infrastructure.Csv` オフライン検査: `OfflineComplianceTests` の `[Theory]` に追加し、意図的 `HttpClient` 混入で赤になることを確認

### Phase 3-1
- Domain: 報酬算定のテーブル駆動テスト（分岐網羅 100% 目標）。基本報酬区分解決、各加算/減算、地域区分単価、利用者負担上限
- マスタ外部化: 単位数/加算/単価が JSON 由来で、Domain に literal が無いこと（(a)(c) 緑）
- 適用年月差し替え: 同入力で令和 6 / 令和 9 を切替えて結果が異なることをテストで固定

### Phase 3-2
- 帳票: 抽出テキスト＋合計＋CJK substring の検証、決定化（同入力＋同 TimeProvider → 同バイト）

### Phase 3-3
- CSV: バイト単位スナップショット（文字コード・改行・桁・項目順）
- カラム名 literal が `Tsumugi.Infrastructure.Csv` 限定であること（(b) 緑）
- 新追記型 CRUD（`ClaimBatch` の `Correct` / `Cancel` レコード）、`AppendOnlyGuard` 違反で赤（意図的違反で確認）
- Recipient 拡張 migration の往復

### Phase 3 全体
- App: プレビュー / エクスポート / 確定コマンドの ViewModel テスト、金額整形（InvariantCulture）

## エラーハンドリング

- 必須出典が未確定のマスタコードを参照: `ClaimCalculator` が `InvalidOperationException` でフェイルファスト（推測値で計算させない）
- 確定済 ClaimBatch の再 New 試行: partial unique index で `DbUpdateException`
- CSV 生成時の必須項目欠落: `ClaimCsvWriter` が `InvalidOperationException` でフェイルファスト（推測埋めしない）
- マスタ JSON のスキーマ違反: 起動時に `JsonClaimMasterLoader` が例外（CI でも検出される）

## 制約 / 品質ゲート

- 親文書 `CLAUDE.md` のハード制約全項目を維持
- `01 §6` 全項目維持
- オフライン検査に新規アセンブリ（`Tsumugi.Infrastructure.Csv` / `Tsumugi.Infrastructure.Reporting` 追加分）を必ず含める
- 伝送・電子証明書・`System.Net.*` を持ち込まない
- 報酬/CSV の数値・項目定義をハードコードしない（(a)(b)(c) 機械判定）
- 金額は整数円、最終額に浮動小数点を混入させない
- 算定は分岐網羅 100% 目標、適用年月差し替えの無改修切替をテストで実証
- 日本語フォント埋込（Noto OFL 1.1）を ADR 化し帳票で有効化
- §2.3 の必須出典は一次情報で ADR 化してから該当サブフェーズに着手
- 新規設計判断（マスタ構造、請求確定の再生成規律、CSV 名前空間境界、フォント埋込）は `docs/decisions/` 0018〜0026 に ADR 化

## 着手手順（要約）

1. **Phase 3-0**: §2.3 の一次出典を確認し ADR 0018〜0024 と 0026 を起票（0025 は 3-1）。並行で フォント埋込・スキャナ・マスタ抽象・Csv アセンブリ・ClaimBatch 骨組み・カバレッジ準備を実装
2. **Phase 3-1**: ADR 0018/0019/0021/0022 が確定したらマスタ実値投入＋算定純粋関数を TDD で実装。ADR 0025（端数規則）も合わせて確定
3. **Phase 3-2**: AC3-0-2（フォント埋込）が緑になったら実績記録票・請求書/明細書を Reporting に追加
4. **Phase 3-3**: ADR 0023 が確定したら Csv アセンブリに正式仕様を実装、Recipient 拡張＋migration、`CloseClaimUseCase` で確定スナップショット。Application 閾値を 70 → 90 へ
5. 各サブフェーズで `./build/ci.sh` 緑・依存方向不変・Domain 95% 維持を確認して受け入れへ

## 参考

- `01_ClaudeCode_実装指示書_Tsumugi.md`（全体仕様の正本）
- `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`（本書の元）
- `docs/open-questions.md`（§2.3 と Phase 1/2 引継ぎの未確定事項）
- `docs/decisions/0006-office-capability-flag-set.md`（OfficeCapability 暫定キーの根拠）
- `docs/decisions/0013-pdf-engine-questpdf.md`（QuestPDF Community 採用）
- `docs/decisions/0014-audit-trail-append-only.md`（追記専用の規律）
- `docs/decisions/0015-dailyrecord-duplicate-new-index.md` / `0017-wagefund-duplicate-new-index.md`（partial unique index 作法の前例）
