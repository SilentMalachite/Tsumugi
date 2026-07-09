# Tsumugi Phase 3 設計 — 国保連請求データ生成

> **Source**: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`
> **Status**: 2026-06-29 設計合意、2026-07-10 現行制度・現行リポジトリへ再接地
> **計画規律**: 2026-06-29付けのPhase 3既存計画は、ADR番号、フォント前提、令和8年6月改定、CSVレコード構造、Recipient拡張前提が古い。本書からサブフェーズごとに再計画するまで実行しない。

## 1. 目的

就労継続支援B型事業所向けTsumugiに、次の一連のオフライン請求機能を追加する。

1. 実効記録と期間マスタから請求をプレビューする
2. 公式様式の実績記録票・請求書・請求明細書を生成する
3. 請求をappend-onlyスナップショットとして確定する
4. 確定済みスナップショットから公式仕様準拠CSVを生成する

責務は公式の取込・送信システムへインポートできるデータ生成までとし、伝送、電子証明書、回線、返戻・過誤再請求の自動化は行わない。

## 2. 設計原則

- **制度値の外部化**: 単位数、閾値、加算、地域単価、負担上限、サービスコード、交換情報IDは出典付き外部データとする。
- **純粋算定**: Domainの請求計算は日付、DB、ファイル、UIに依存しない。
- **適用年月版**: 令和6年版と令和8年6月版を同一コードで切り替える。
- **確定後不変**: 下層訂正で確定請求を自動更新しない。再確定はCorrectionを追記する。
- **提出は確定後のみ**: CSVはプレビューから生成せず、確定済み実効ClaimBatchから生成する。
- **バイト準拠**: CP932、CRLF、順序、桁、引用、必須条件をバイトテストで固定する。
- **フェイルクローズ**: 不足・未知・変換不能を推測補完せず、出力前に全件検証して停止する。
- **オフライン**: 全プロダクションアセンブリで通信API直接参照を禁止する。

## 3. 一次資料と適用版

### 3.1 報酬

- [令和6年度障害福祉サービス等報酬改定](https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00009.html)
- [令和8年度障害福祉サービス等報酬改定](https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00013.html)
- [令和8年6月 報酬算定構造・サービスコード表等](https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html)

マスタには少なくとも2024-04/06以降の令和6版と2026-06以降の令和8版を収録する。改定ページだけでなく、告示、留意事項通知、Q&A、報酬算定構造、サービスコード表、体制等状況一覧表を相互参照し、修正資料を反映する。

### 3.2 平均工賃月額

令和6年度改定後の正式式を採用する。

```text
年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12
```

以前の「各月の工賃支払対象者数」を分母にする式や除外規則は、新式と混在させない。令和8年6月の基本報酬区分見直しと経過措置は別の版付き規則として扱う。

### 3.3 CSV・帳票

- [インタフェース仕様書 共通編（令和7年10月）](https://www.mhlw.go.jp/content/12200000/001565560.pdf)
- [インタフェース仕様書 事業所編（令和7年10月）](https://www.mhlw.go.jp/content/12200000/001565561.pdf)
- [インタフェース仕様書一覧](https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000045136.html)
- [令和8年6月 報酬算定構造・サービスコード表等](https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html)

令和8年6月資料では実績記録票、請求書明細書、記載例、決定サービス設定が「変更なし」とされている。したがってCSV共通規則・事業所項目構造は令和7年10月版を基準とし、令和8年6月の報酬・サービスコード差分を組み合わせる。

国保連事業所編に性別項目はない。`Recipient.Gender`の追加を前提にせず、ADR 0024で既存モデルとの項目マッピングを行い、不足する公式必須項目だけを追加する。

項目マッピングは3-0の完了条件とする。請求書、明細書、実績記録票の各項目について、既存モデルの具体的プロパティ、必須条件、不足時の追加先、migration要否を一覧化する。現行`DailyRecord`は出欠・送迎・食事・備考を中心とし、提供時間や帳票固有の加算入力欄を持たないため、既存値や自由記述から推定して帳票を埋めない。

### 3.4 出典トレーサビリティ

各制度データ束は次を保持する。

```text
documentId
title
publisher
publishedOrEffectiveAt
retrievedAt
url
sha256
supersedes
notes
```

算定結果と確定スナップショットには、使用した報酬マスタ束、CSV仕様束、アプリ版を記録する。

## 4. アーキテクチャ

既存の依存方向を維持し、CSVだけを新規アセンブリへ物理分離する。

```text
App
 ├─ Application
 │   └─ Domain
 ├─ Infrastructure
 │   ├─ Application
 │   └─ Domain
 ├─ Infrastructure.Reporting
 │   ├─ Application
 │   └─ Domain
 └─ Infrastructure.Csv
     ├─ Application
     └─ Domain
```

DomainはEF Core、Avalonia、QuestPDF、CSV、ファイルI/Oを参照しない。

### 4.1 ファイル責務

```text
src/
  Tsumugi.Domain/
    Entities/
      ClaimBatch.cs                 確定請求のappend-onlyヘッダ
      ClaimDetail.cs                提出再現可能な請求明細スナップショット
    Logic/Claim/
      ClaimCalculator.cs            算定全体の純粋関数
      AverageWageCalculator.cs      正式平均工賃月額
      BasicRewardResolver.cs        報酬区分・経過措置・サービスコード解決
      AdditionCalculator.cs         加算・減算
      BurdenCalculator.cs           利用者負担・上限管理
      RoundingPolicy.cs             固定小数と端数規則
      Models/                       入出力値、版情報、検証結果
      Masters/                      マスタ参照interface

  Tsumugi.Application/
    Abstractions/
      IClaimBatchRepository.cs
      IClaimReportGenerator.cs
      IClaimCsvWriter.cs
      IClaimMasterProvider.cs
    UseCases/Claim/
      CalculateClaimUseCase.cs
      QueryClaimUseCase.cs
      CloseClaimUseCase.cs
      GenerateClaimReportsUseCase.cs
      ExportClaimCsvUseCase.cs
    Validation/ClaimPreflightValidator.cs
    Dtos/Claim/

  Tsumugi.Infrastructure/
    Persistence/
      ClaimBatchRepository.cs
      Configurations/ClaimBatchConfiguration.cs
      Configurations/ClaimDetailConfiguration.cs
    ClaimMasters/
      JsonClaimMasterProvider.cs
      Schema/
      Seed/
        basic-rewards.json
        additions.json
        region-unit-prices.json
        burden-caps.json
        transition-rules.json
        service-codes.json
        sources.json
    Migrations/<timestamp>_AddClaimBatchAndDetail.cs
    Migrations/<timestamp>_ExpandClaimInputs.cs     ADR 0024で不足確定時のみ

  Tsumugi.Infrastructure.Reporting/
    ClaimReportGenerator.cs
    ClaimReports/
      ServicePerformanceReport.cs
      BenefitClaimForm.cs
      BenefitClaimDetailForm.cs

  Tsumugi.Infrastructure.Csv/
    ClaimCsvWriter.cs
    CsvEncoding.cs
    CsvFieldWriter.cs
    Specifications/
      common-r7-10.json
      provider-claim-r7-10.json
      sources.json
    Records/
      ControlRecord.cs
      DataRecord.cs
      EndRecord.cs
      Claim/
      Performance/

  Tsumugi.App/
    ViewModels/Claim/
    Views/Claim/
```

既存ファイルが肥大化する場合も、Phase 3と無関係な分割・整理は行わない。

## 5. ドメインモデル

### 5.1 Claim calculation input

```text
ClaimCalculationInput
  OfficeSnapshot
  OfficeClaimProfile
  IReadOnlyList<RecipientClaimInput>
  ServiceMonth
  ClaimMasterVersion
```

`OfficeClaimProfile`は期間マスタとし、次を明示する。

- 報酬体系
- 人員配置
- 利用定員
- 届出済み基本報酬区分
- 令和8年経過措置の適用情報
- `OfficeCapability`正式コード集合
- 有効期間と作成根拠

令和8年経過措置は過去区分や届出判断を必要とするため、平均工賃だけから推測しない。経過措置なしの場合は計算区分と届出区分の一致を検証する。経過措置ありの場合は、公式届出情報とマスタ規則から許容区分を検証する。

### 5.2 Claim result

```text
ClaimResult
  OfficeId
  ServiceMonth
  MasterVersion
  CsvSpecificationVersion
  RecipientResults
  Totals
  Warnings
```

金額は整数円、単価は固定小数値とする。`double`/`float`は使用しない。

### 5.3 ClaimBatch / ClaimDetail

| 項目 | 設計 |
| --- | --- |
| 粒度 | `(OfficeId, ServiceMonth)` |
| 履歴 | `RecordKind.New/Correct/Cancel`、`OriginId`は初代New |
| 重複防止 | `(OfficeId, ServiceMonth) WHERE Kind = 1` partial unique index |
| 自動更新 | 禁止。下層訂正は未確定プレビュー差分として表示 |
| 再確定 | 明示操作でCorrectionを追記 |
| 再現性 | 事業所・受給者・証・実績・算定明細・版情報をスナップショット |
| 監査 | `CreatedAt`、`CreatedBy`、`IAuditTrail` |

`ClaimDetail`も親のappend-only規律に従い、直接更新・削除を禁止する。

## 6. マスタ設計

### 6.1 適用版の選択

各エントリは次を持つ。

```json
{
  "effectiveFrom": 202606,
  "effectiveTo": null,
  "sourceDocumentId": "...",
  "key": "...",
  "value": "..."
}
```

- 適用版は`effectiveFrom <= ServiceMonth`のうち最新を選ぶ。
- 同一キー・同一開始月の重複、版の空白、逆転した期間は起動時に拒否する。
- JSONの値を差し替えるだけで制度改定に追随できるようにする。
- 令和9仮データは使用しない。実在する令和6/令和8の境界で切替を実証する。

### 6.2 平均工賃月額

入力は前年度の工賃総額、延べ利用者数、年間開所日数。12か月未満の新規指定等、通常式と異なる公式規則がある場合は別rule IDとして外部化し、根拠なしに一般式へフォールバックしない。

### 6.3 報酬区分と経過措置

`BasicRewardResolver`は以下を受け取る。

```text
ServiceMonth
AverageWageMonthlyYen
CapacityClass
StaffingClass
RewardSystem
DeclaredBracket
TransitionContext
IBasicRewardMaster
```

閾値、単位数、中間区分、経過措置の適用条件はマスタに置く。Domainは条件照合と結果構築だけを行う。

## 7. CSV設計

### 7.1 ファイル全体

共通編に従い、ファイルは次の順序とする。

1. コントロールレコード（レコード種別`1`）
2. データレコード（レコード種別`2`、1件以上）
3. エンドレコード（レコード種別`3`）

レコード番号は1からの連番。コントロールレコードの件数はデータレコードだけを数える。

`ServiceMonth`と`ProcessingMonth`を混同しない。コントロールレコードの処理対象年月は国保連で電算処理する年月であり、サービス提供年月とは別入力とする。

両者は同じ`YearMonth`を使い回さず、別の値オブジェクト型として定義する。相互の暗黙変換は設けず、`ExportClaimCsvUseCase`は`ProcessingMonth`を必須引数として受け取る。

### 7.2 エンコーディング

- Windows標準Shift_JISとしてコードページ932を使用する。
- 改行は常に`0x0D 0x0A`。
- 区切りは`,`。
- 仕様に従って引用し、引用内の`"`は二重化する。
- 最大長は文字数ではなくCP932変換後のバイト数で検証する。
- 変換不能文字、禁止文字、制御文字は対象項目を示して拒否する。
- `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`の初期化責務はCsvアセンブリ内に閉じる。

### 7.3 データレコード内部

事業所編の交換情報識別番号と内側レコード種別を仕様データで定義する。少なくとも次を扱う。

- 介護給付費等請求書の基本・明細情報
- 介護給付費等明細書の基本・日数・明細・集計・契約情報
- サービス提供実績記録票の基本・明細情報

「基本・明細・集計の3種だけ」という旧計画の簡略化は採用しない。

### 7.4 出力前検証

`ClaimPreflightValidator`が全件を走査し、次を集約して返す。

- 必須項目不足
- 未知のサービスコード・交換情報ID
- 桁・範囲・コード値不正
- CP932変換不能・禁止文字
- マスタ版・CSV仕様版不一致
- `ServiceMonth`/`ProcessingMonth`不正
- `ProcessingMonth`未入力、またはコントロールレコードへの誤マッピング
- 未確定ClaimBatchからの出力要求

1件でもエラーがあればStreamへ1バイトも書かない。

### 7.5 決定論と監査

CSVは`(実効ClaimBatch, ProcessingMonth, CsvSpecificationVersion)`が同じなら同一バイトを返す。出力時はファイル本体をDBへ保存せず、バイト数、SHA-256、版、処理対象年月、操作者を`AuditEntry`へ記録する。氏名・受給者証番号・保存先はログに残さない。

## 8. 帳票設計

Phase 4 S1 / ADR 0013で完成したNoto Sans JP埋込と`QuestPdfLicenseConfigurator.Initialize()`を再利用する。新しいフォントADRは作らない。

| 帳票 | 入力 | 検証 |
| --- | --- | --- |
| サービス提供実績記録票 | 実効`ClaimBatch`/`ClaimDetail`内の実績・事業所・受給者証スナップショット | 日付、提供状況、送迎・食事・欠席、CJK |
| 請求書 | 実効`ClaimBatch`/`ClaimDetail`の事業所・市町村集計 | 件数、単位数、費用、請求額、負担額 |
| 請求明細書 | 実効`ClaimBatch`/`ClaimDetail`の受給者別明細 | 基本・日数・明細・集計・契約、合計 |

3帳票とも生成時に現行`DailyRecord`、`Certificate`、`Office`等を再読込せず、確定時のスナップショットだけを入力とする。同一入力と同一`TimeProvider`で同一バイトを返す。既知の康熙部首抽出問題は既存`KangxiRadicalNormalizer`をテスト時だけ使用し、表示グリフと合計値の検証を省略しない。

## 9. ApplicationとUIのデータフロー

```text
事業所・対象月・届出設定を選択
  → CalculateClaimUseCase
  → ClaimPreflightValidator
  → ClaimCalculator（純粋関数）
  → プレビュー・警告・確定済みとの差分
  → CloseClaimUseCase
  → ClaimBatch/ClaimDetailをNewまたはCorrectionで追記
  ├─ GenerateClaimReportsUseCase → PDF → IFileSaveService
  └─ ExportClaimCsvUseCase
       → 実効ClaimBatchを再読込
       → CSV preflight
       → CP932 bytes
       → IFileSaveService
       → AuditEntryへhash・版・処理年月
```

UIは1画面1責務を守る。

| View | 責務 |
| --- | --- |
| `ClaimPreviewView` | 入力選択、算定、警告、差分 |
| `ClaimCloseView` | 確定、再確定、取下げ |
| `ClaimReportView` | 確定済み帳票の生成・保存 |
| `ClaimCsvExportView` | 確定済みCSVの処理対象年月指定・保存 |

Office選択を各Viewで明示し、テストだけが`OfficeId`を直接注入する構造にしない。保存キャンセルはエラー扱いせず、状態を変更しない。

## 10. エラーハンドリング

| 条件 | 動作 |
| --- | --- |
| 対象月に適用マスタなし | 算定停止。欠落キーと対象月を表示 |
| 令和8経過措置入力不足 | 算定停止。必要な届出・過去区分を表示 |
| 届出区分と計算区分不整合 | 確定禁止。差分と根拠版を表示 |
| 必須証情報・契約情報不足 | 該当受給者を列挙して確定/CSV禁止 |
| CP932変換不能・禁止文字 | 項目名と対象を表示し、0バイト出力 |
| 二重New | DB制約で拒否し、最新実効版を再読込 |
| 楽観ロック競合 | 再読込を促し、自動上書きしない |
| 保存ダイアログキャンセル | 正常終了、監査行・状態変更なし |
| 途中I/O失敗 | 完了扱いにせず、部分ファイルの扱いをUIで通知 |

例外メッセージやログに氏名、受給者証番号、保存先フルパスを含めない。

## 11. TDD・品質ゲート

### 11.1 Domain

- 平均工賃正式式のゼロ・境界・端数
- 2026-05/2026-06の版切替
- 令和8区分境界と経過措置
- 基本報酬、加算・減算、地域単価、利用者負担
- `Logic.Claim`分岐100%目標、Domain全体95%以上

### 11.2 マスタ

- スキーマ、出典、SHA-256、適用期間
- 重複・空白・未知コードで起動失敗
- 制度実値がDomain/Applicationへ漏れていない
- 交換情報ID・サービスコードが指定データ領域外へ漏れていない

単純な「1000超の整数を全面禁止」だけに依存せず、許可領域・型・出典メタデータを組み合わせて誤検知と抜けを抑える。

### 11.3 CSV

- コントロール/データ/エンドの順序と連番
- CP932、CRLF、引用、空欄、ゼロ
- 文字数とバイト数の差
- 変換不能文字・禁止文字で0バイト出力
- 請求書、明細書、実績記録票のバイトスナップショット
- 同一入力の決定論

### 11.4 Persistence / Application / App

- AppendOnlyGuard、二重New、Correction、Cancel
- migration往復
- 確定前CSV拒否
- 確定後の下層訂正で自動変更なし
- 再計算差分、再確定、保存キャンセル
- Office/対象月/処理対象年月の実UI配線
- Phase 3完了時Applicationカバレッジ90%以上

### 11.5 横断

- Csv/Reporting/Appを含むオフライン検査
- 依存方向テスト
- `dotnet format --verify-no-changes`
- `./build/ci.sh`
- 意図的違反で各境界テストが赤になること

## 12. サブフェーズ

### Phase 3-0: 出典・契約・土台

- ADR 0020〜0026確定
- 公式帳票・CSV全項目と既存モデルのマッピング、不足項目一覧
- マスタschemaと版メタデータ
- `Tsumugi.Infrastructure.Csv`空殻とオフライン境界
- `ClaimBatch`/`ClaimDetail`、Repository、migration、AppendOnlyGuard、partial unique index
- 制度値・CSV仕様値の境界検査

### Phase 3-1: 報酬算定

- 不足確定した請求入力モデル・migration・入力UI
- 令和6/令和8の実値マスタ
- `OfficeClaimProfile`
- 平均工賃正式式
- 基本報酬区分、経過措置、加算・減算、地域単価、負担上限
- Calculate/Query/Close UseCase
- `ClaimBatch`の初回確定、再確定、取下げ

### Phase 3-2: 帳票

- 3-0の項目マッピングと3-1の請求入力拡張を着手条件とする
- 3-1で確定した実効`ClaimBatch`の読取
- 3帳票
- `IClaimReportGenerator`
- Reporting実装
- ViewModel/Viewと保存

### Phase 3-3: CSV・出力

- 公式レコード構造とCP932 writer
- `ClaimPreflightValidator`
- Export UseCase
- CSV出力UI
- Applicationカバレッジ90%
- Phase 3全体受け入れ

## 13. ADR採番

ADR 0018/0019はPhase 4 S0で使用済み。Phase 3では次を使用する。

| ADR | タイトル | 主な確定事項 |
| --- | --- | --- |
| 0020 | 令和6/令和8報酬マスタの出典と版管理 | 告示・通知・サービスコード・施行境界 |
| 0021 | 加算コード集合とOfficeCapability移行 | 正式キー、暫定キー変換、移行期限 |
| 0022 | 負担上限額マスタ | 区分、金額、証記載値との優先関係 |
| 0023 | 平均工賃月額と令和8経過措置 | 正式式、区分、届出入力、経過措置 |
| 0024 | 国保連CSV仕様 | 版、CP932、CRLF、外側/内側レコード、項目マッピング |
| 0025 | 報酬計算の端数規則 | 単位ごとの丸め順序と固定小数 |
| 0026 | 請求確定スナップショット | append-only、partial index、再確定、CSV確定後限定 |

フォント・QuestPDFはADR 0013を参照する。

## 14. 受け入れ基準

### Phase 3-0

- **AC3-0-1** ADR 0020〜0026が一次資料とハッシュを持つ
- **AC3-0-2** マスタschema・出典版・適用年月の検証が緑
- **AC3-0-3** Csvアセンブリが依存方向・オフライン検査対象
- **AC3-0-4** ClaimBatch/Detail、AppendOnlyGuard、partial index、migrationが緑
- **AC3-0-5** 公式帳票・CSVの全項目について既存対応、不足項目、必須条件、追加先、migration要否が確定する

### Phase 3-1

- **AC3-1** 令和6/令和8の制度値が外部マスタから供給される
- **AC3-2** 平均工賃正式式、令和8区分、経過措置が正しく解決される
- **AC3-3** 基本報酬・加算・減算・地域単価・負担上限が公式ケースと一致する
- **AC3-4** 2026-05/06の版境界が無改修で切り替わる
- **AC3-8** AC3-0-5で不足と確定した入力だけがモデル・migration・実UIへ追加され、全公式必須項目を入力できる
- **AC3-9** 初回確定・再確定・取下げがappend-onlyの`New`/`Correction`/`Cancel`で履歴化される

### Phase 3-2

- **AC3-5** 既存埋込フォントを使いCJK・合計・決定論が緑
- **AC3-6** 3帳票をUIから保存できる

### Phase 3-3

- **AC3-7** 独立入力した`ProcessingMonth`がコントロールレコードの処理対象年月へ設定され、CP932/CRLFと公式レコード構造のCSVがバイト一致する
- CSVは3-1で確定した実効ClaimBatchからのみ生成される

### 横断

- **AC3-10** 伝送コードなし、オフライン検査緑、Domain 95%以上、Application 90%以上、CI/format/依存方向が緑

## 15. リスクと対策

| リスク | 影響 | 対策 |
| --- | --- | --- |
| 令和8経過措置を平均工賃だけで推測 | 誤請求 | 届出済み区分・過去区分を期間マスタで明示 |
| ServiceMonthとProcessingMonthの混同 | 国保連取込エラー | 型・UI・テストで別項目にする |
| Shift_JISを一般的な名称だけで選ぶ | Windows拡張文字差 | CP932を明示しバイトスナップショット化 |
| 文字数だけの桁検証 | 漢字項目のバイト超過 | 変換後バイト数で検証 |
| プレビューからCSV生成 | 確定内容との不一致 | 確定済み実効ClaimBatch限定 |
| 性別追加を先行 | 不要migration・二重管理 | 公式項目マッピング後、不足項目だけ追加 |
| ADR番号衝突 | 追跡不能 | Phase 3を0020〜0026へ再採番 |
| 旧計画の実行 | 古い制度・CSV構造を実装 | 本書からサブフェーズごとに再計画 |

## 16. 実装移行条件

1. 本書と実装指示書のレビュー完了
2. 3-0実装計画を本書から再作成
3. ADR 0020〜0026を一次資料に基づき確定
4. 出典に依存しない土台からTDDで着手
5. 各サブフェーズを個別に受け入れて次へ進む

不確定事項は`docs/open-questions.md`へ戻す。推測値、仮コード、暫定CSVを「後で差し替える」前提で本番経路へ入れない。
