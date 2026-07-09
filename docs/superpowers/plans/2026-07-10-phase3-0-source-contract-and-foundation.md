# Tsumugi Phase 3-0 出典・契約・土台 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3-1以降が制度値やCSV項目を推測せずに実装できるよう、ADR 0020〜0026、公式項目マッピング、版付きマスタ契約、CSV仕様アセンブリ、append-only請求スナップショットの永続化土台を完成させる。

**Architecture:** Domainには年月・版・請求スナップショットと純粋な履歴解決規則だけを置き、Applicationにはマスタ／Repository抽象を置く。Infrastructureは出典付きJSONマスタとEF Core永続化を担当し、公式CSVのレコード・項目定義は新規`Tsumugi.Infrastructure.Csv`へ物理分離する。Phase 3-0では算定、帳票生成、CSVバイト生成、UIを実装せず、3-1〜3-3が利用する検証済み契約だけを作る。

**Tech Stack:** .NET 10 / C# 14 / EF Core 10 + SQLite / System.Text.Json / xUnit / FluentAssertions / 既存のオフライン・アーキテクチャ検査

---

## 0. この計画の位置づけ

- 正本は`06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`と`docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`。
- 本計画は`docs/superpowers/plans/2026-06-29-phase3-0-foundation.md`を置き換える。旧計画は参照・実行しない。
- Phase 3全体を一括実装しない。本計画でPhase 3-0を受け入れた後に、Phase 3-1の新しい実装計画を作る。
- 実行前に`superpowers:using-git-worktrees`を使い、`codex/phase3-0-foundation`の専用worktreeで作業する。現在の`main`上にある無関係な変更を持ち込まない。
- 各実装タスクはRed → Green → Refactorで進め、1コミットを1論理変更に限定する。

### Phase 3-0に含む

- ADR 0020〜0026と一次資料のURL・版・施行日・取得日・SHA-256
- 共通編／事業所編の全レコード・全項目と既存モデルの機械可読マッピング
- `ServiceMonth`と`ProcessingMonth`の型分離
- 出典メタデータ、適用期間、マスタ版の検証・解決契約
- `Tsumugi.Infrastructure.Csv`と同テストプロジェクト
- `ClaimBatch`／`ClaimDetail`のappend-onlyスナップショット、EF migration、Repository
- 制度実値・CSV仕様値の配置境界と歯のあるCI検査
- Csvを含む依存方向・オフライン検査

### Phase 3-0に含まない

- 基本報酬、加算・減算、地域単価、利用者負担の算定実装（Phase 3-1）
- AC3-0-5で不足と確定した既存マスタ／記録モデルの拡張、migration、入力UI（Phase 3-1）
- `CalculateClaimUseCase`／`QueryClaimUseCase`／`CloseClaimUseCase`（Phase 3-1）
- QuestPDF帳票（Phase 3-2。既存ADR 0013と埋込フォントを再利用）
- `IClaimCsvWriter`／CP932 writer／`ExportClaimCsvUseCase`／CSV出力UI（Phase 3-3）
- 伝送、電子証明書、回線、返戻・過誤再請求の自動化

## 1. 固定する設計判断

1. ADR番号は0020〜0026だけを使う。0018／0019はPhase 4 S0で使用済み。
2. `RecordKind`の実コードは既存enumに合わせて`New=1`、`Correct=2`、`Cancel=3`とする。文書上の「Correction」は`RecordKind.Correct`で表現する。
3. `ClaimBatch.OriginId`は常に初代`New.Id`を指す。直前レコードを指す既存`AppendOnlyChainPolicy`はClaimBatchに流用せず、専用`ClaimBatchPolicy`を作る。
4. `ServiceMonth`と`ProcessingMonth`は別の値型とし、相互変換演算子を定義しない。Phase 3-0では両型を確定するが、`ProcessingMonth`を使用するCSV出力APIはPhase 3-3まで作らない。
5. `ClaimDetail`は受給者単位の不変スナップショットとする。入力と算定結果は版付きの決定論的JSONとして保存し、DomainはJSONを解釈しない。具体的なJSON recordとserializerはPhase 3-1で追加する。
6. CSV仕様の公式項目・コード・順序は`Tsumugi.Infrastructure.Csv/Specifications/*.json`だけに置く。Phase 3-0では仕様カタログと検証まで実装し、throw-onlyの`ClaimCsvWriter`空殻は作らない。
7. 報酬マスタの実値ファイルはPhase 3-0でschemaと空の`entries`を用意し、Phase 3-1でADR値を投入する。空マスタを算定に使うフォールバックAPIは作らない。
8. 国保連事業所編にない性別項目を理由に`Recipient.Gender` migrationを作らない。AC3-0-5で不足と判定した項目だけをPhase 3-1へ送る。
9. 単純な「1000超の整数禁止」は採用しない。許可されたデータ領域、出典参照、マスタ`values`配下の実値、CSV仕様項目集合を組み合わせて境界を検査する。

## 2. ファイル責務マップ

### Documentation

```text
docs/decisions/
  0020-claim-master-sources-and-versioning.md
  0021-office-capability-official-codes.md
  0022-burden-cap-master.md
  0023-average-wage-and-r8-transition.md
  0024-kokuhoren-csv-and-field-mapping.md
  0025-claim-rounding-rules.md
  0026-claim-batch-snapshot.md
docs/phase3-claim-field-mapping.md
docs/spec-data/phase3/report-fields-r8-06.json
docs/spec-data/phase3/report-field-mapping-r8-06.json
docs/phase3-0-acceptance.md
docs/open-questions.md
CHANGELOG.md
```

### Domain / Application

```text
src/Tsumugi.Domain/
  ValueObjects/ServiceMonth.cs
  ValueObjects/ProcessingMonth.cs
  Logic/Claim/Models/ClaimMasterVersion.cs
  Logic/Claim/Models/CsvSpecificationVersion.cs
  Logic/Claim/Models/ClaimSourceDocument.cs
  Logic/Claim/Models/ClaimMasterRelease.cs
  Logic/Claim/ClaimMasterCatalogPolicy.cs
  Logic/Claim/ClaimBatchPolicy.cs
  Entities/ClaimBatch.cs
  Entities/ClaimDetail.cs

src/Tsumugi.Application/Abstractions/
  IClaimMasterProvider.cs
  IClaimBatchRepository.cs
```

### Infrastructure / CSV

```text
src/Tsumugi.Infrastructure/
  ClaimMasters/JsonClaimMasterProvider.cs
  ClaimMasters/Schema/source-catalog.schema.json
  ClaimMasters/Schema/claim-master-file.schema.json
  ClaimMasters/Seed/sources.json
  ClaimMasters/Seed/basic-rewards.json
  ClaimMasters/Seed/additions.json
  ClaimMasters/Seed/region-unit-prices.json
  ClaimMasters/Seed/burden-caps.json
  ClaimMasters/Seed/transition-rules.json
  ClaimMasters/Seed/service-codes.json
  Persistence/ClaimBatchRepository.cs
  Persistence/Configurations/ClaimBatchConfiguration.cs
  Persistence/Configurations/ClaimDetailConfiguration.cs
  Migrations/<timestamp>_AddClaimBatchAndDetail.cs
  Migrations/<timestamp>_AddClaimBatchAndDetail.Designer.cs
  Migrations/TsumugiDbContextModelSnapshot.cs

src/Tsumugi.Infrastructure.Csv/
  Tsumugi.Infrastructure.Csv.csproj
  CsvAssemblyMarker.cs
  Specifications/CsvSpecificationCatalog.cs
  Specifications/CsvSpecificationLoader.cs
  Specifications/Models/*.cs
  Specifications/common-r7-10.json
  Specifications/provider-claim-r7-10.json
  Specifications/field-mapping-r7-10.json
  Specifications/sources.json
```

### Tests / solution wiring

```text
tests/Tsumugi.Domain.Tests/
  ValueObjects/ServiceMonthTests.cs
  ValueObjects/ProcessingMonthTests.cs
  Logic/Claim/ClaimMasterCatalogPolicyTests.cs
  Logic/Claim/ClaimBatchPolicyTests.cs
  Entities/ClaimBatchTests.cs
  Entities/ClaimDetailTests.cs

tests/Tsumugi.Infrastructure.Csv.Tests/
  Tsumugi.Infrastructure.Csv.Tests.csproj
  CsvSpecificationLoaderTests.cs
  CsvSpecificationCompletenessTests.cs
  ClaimFieldMappingCompletenessTests.cs

tests/Tsumugi.Infrastructure.Tests/
  ClaimMasters/JsonClaimMasterProviderTests.cs
  ClaimSpecificationBoundaryTests.cs
  ExternalSpecificationLiteralGuard.cs
  Persistence/ClaimBatchRepositoryTests.cs
  ClaimBatchDuplicateNewIndexTests.cs
  AppendOnlyGuardPhase3Tests.cs
  ClaimBatchMigrationTests.cs
  OfflineComplianceTests.cs
  AppOfflineComplianceTests.cs
  KokuhorenTransmissionSeparationTests.cs
  ArchitectureTests.cs
  Tsumugi.Infrastructure.Tests.csproj

tests/Tsumugi.App.Tests/
  CompositionRootTests.cs

Tsumugi.sln
src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj
src/Tsumugi.Infrastructure/DependencyInjection.cs
src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs
src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs
```

---

### Task 0: 専用worktreeとベースラインを確認する

**Files:** 変更なし

- [ ] **Step 1: 専用worktreeを作る**

`superpowers:using-git-worktrees`を使い、`codex/phase3-0-foundation`ブランチのworktreeを作る。現在の`main`にある`.serena/project.yml`や`graphify-out/`を持ち込まない。

- [ ] **Step 2: ブランチと作業ツリーを確認する**

Run: `git branch --show-current && git status --short`

Expected: ブランチが`codex/phase3-0-foundation`で、意図しない変更がない。

- [ ] **Step 3: ベースラインCIを実行する**

Run: `./build/ci.sh`

Expected: 最後に`==> CI OK`。失敗した場合はPhase 3-0を開始せず、ベースライン障害として切り分ける。

---

### Task 1: ADR 0020で令和6／令和8の出典と版管理を確定する

**Files:**
- Create: `docs/decisions/0020-claim-master-sources-and-versioning.md`

- [ ] **Step 1: 一次資料を一時領域へ取得する**

厚労省の令和6年度改定ページ、令和8年度改定ページ、令和8年6月報酬構造・サービスコードページから、就労継続支援B型に関係する告示、留意事項通知、報酬算定構造、サービスコード表、体制等状況一覧、訂正資料、Q&Aを取得する。リポジトリへPDFを保存しない。

Run: `mkdir -p "${TMPDIR:-/tmp}/tsumugi-phase3-sources"`

各URLは`curl --fail --location --output <file> <official-url>`で取得する。

- [ ] **Step 2: 取得ファイルのハッシュを計算する**

Run: `shasum -a 256 "${TMPDIR:-/tmp}/tsumugi-phase3-sources"/*`

Expected: 全ファイルが64桁のSHA-256を持つ。HTMLエラーページでないことを`file`と先頭バイトで確認する。

- [ ] **Step 3: ADRを既存形式で記述する**

ADRは「決定→背景→選択肢→決定根拠→影響」の順とし、資料ごとに次を記録する。

```text
documentId / title / publisher / effectiveAt / retrievedAt / url / sha256 / supersedes / correctionNote
```

2024-04／06と2026-06のどちらが各マスタ群に適用されるかを明記し、ページ上の「変更なし」も版選択の根拠として残す。制度値はこのADRに出典付きで記録するが、C#へ転記しない。

- [ ] **Step 4: 文書検査とコミット**

Run: `git diff --check -- docs/decisions/0020-claim-master-sources-and-versioning.md`

```bash
git add docs/decisions/0020-claim-master-sources-and-versioning.md
git commit -m "docs(phase3-0/AC3-0-1): fix R6 and R8 claim source versions"
```

---

### Task 2: ADR 0021で加算コードとOfficeCapability移行を確定する

**Files:**
- Create: `docs/decisions/0021-office-capability-official-codes.md`

- [ ] **Step 1: 正式コード集合を抽出する**

ADR 0020のサービスコード表・体制等状況一覧から、B型の基本報酬選択、人員配置、食事提供、送迎、欠席時対応、上限額管理その他の対象加算・減算について、表示名、請求サービスコード、決定サービスコード、適用開始月、廃止月、必要入力を一覧化する。

- [ ] **Step 2: 暫定キーの扱いを決める**

`mealProvision`／`transportSupport`を正式コードへ推測変換しない。既存値はAC3-8の入力UIで明示的に再登録し、3-1のマスタ解決で暫定キーを検出した場合は算定不能とする。

- [ ] **Step 3: ADRを書く**

`OfficeCapability.Flags`のキー命名規則、適用期間、旧キー一覧、移行操作、未知コード時のフェイルクローズ、出典頁を記述する。

- [ ] **Step 4: 検査とコミット**

Run: `git diff --check -- docs/decisions/0021-office-capability-official-codes.md`

```bash
git add docs/decisions/0021-office-capability-official-codes.md
git commit -m "docs(phase3-0/AC3-0-1): define official capability codes"
```

---

### Task 3: ADR 0022で負担上限額マスタを確定する

**Files:**
- Create: `docs/decisions/0022-burden-cap-master.md`

- [ ] **Step 1: 制度区分と証記載値を分離する**

`PaymentBurdenCategory`に対応する制度上限、`Certificate.MonthlyCostCap`、上限額管理結果の優先関係を一次資料から整理する。

- [ ] **Step 2: 適用年月と出典を記録する**

各区分について`effectiveFrom`、`effectiveTo`、金額、根拠文書ID・頁を記録する。自治体や受給者証の実値を制度マスタで上書きしないことを決定事項にする。

- [ ] **Step 3: ADRを書く**

未指定区分、証上限未入力、上限額管理結果未入力を0円扱いせず算定不能とする条件を明文化する。

- [ ] **Step 4: 検査とコミット**

```bash
git diff --check -- docs/decisions/0022-burden-cap-master.md
git add docs/decisions/0022-burden-cap-master.md
git commit -m "docs(phase3-0/AC3-0-1): define burden cap source contract"
```

---

### Task 4: ADR 0023で平均工賃月額と令和8経過措置を確定する

**Files:**
- Create: `docs/decisions/0023-average-wage-and-r8-transition.md`

- [ ] **Step 1: 正式式と通常ルールを記録する**

次の式について、分子・分母、年度、開所日数、12か月未満の扱い、0除算、端数処理の根拠頁を記録する。

```text
年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12
```

- [ ] **Step 2: 令和8区分と経過措置を記録する**

新しい閾値、施行月、届出済み区分、過去区分、対象外条件、経過措置終了条件を区別する。平均工賃だけから届出区分を推測しない。

- [ ] **Step 3: 既存`AverageWageMetric`との責務差を記録する**

Phase 2工賃計算の集計指標を破壊変更せず、Phase 3-1で`Logic/Claim/AverageWageCalculator`を別実装することを決定する。

- [ ] **Step 4: 検査とコミット**

```bash
git diff --check -- docs/decisions/0023-average-wage-and-r8-transition.md
git add docs/decisions/0023-average-wage-and-r8-transition.md
git commit -m "docs(phase3-0/AC3-0-1): define average wage and R8 transition"
```

---

### Task 5: CSV仕様アセンブリとテストプロジェクトを新設する

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj`
- Create: `src/Tsumugi.Infrastructure.Csv/CsvAssemblyMarker.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj`
- Modify: `Tsumugi.sln`
- Modify: `tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ArchitectureTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/KokuhorenTransmissionSeparationTests.cs`

- [ ] **Step 1: 失敗するアーキテクチャテストを追加する**

`ArchitectureTests`にCsvアセンブリ検査を追加する。Csvが参照してよいのはBCL、Application、Domainだけで、`Tsumugi.Infrastructure`、Reporting、App、Avalonia、EF Core、QuestPDFを禁止する。Infrastructure側からCsvへの逆参照も禁止する。

- [ ] **Step 2: CSVプロジェクトを作る**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Tsumugi.Infrastructure.Csv</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Tsumugi.Application/Tsumugi.Application.csproj" />
    <ProjectReference Include="../Tsumugi.Domain/Tsumugi.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="Specifications/*.json" />
  </ItemGroup>
</Project>
```

`CsvAssemblyMarker`はアセンブリ位置取得用の空`public static class`だけを持つ。writerや`NotImplementedException`は追加しない。

- [ ] **Step 3: テストプロジェクトを作る**

既存`Tsumugi.Infrastructure.Reporting.Tests.csproj`と同じxUnit／FluentAssertions構成にし、Csvプロジェクトだけを参照する。

- [ ] **Step 4: solutionへ追加する**

```bash
dotnet sln add src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj
dotnet sln add tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj
```

- [ ] **Step 5: オフライン検査の全列挙へCsvを追加する**

`OfflineComplianceTests`のassembly reference検査、`AppOfflineComplianceTests`のP/Invoke検査とURL literal検査の全`InlineData`へ`Tsumugi.Infrastructure.Csv`を追加する。`Assembly.Load`で解決できるよう、Infrastructure.TestsからCsvプロジェクトを参照する。

`KokuhorenTransmissionSeparationTests`の禁止伝送型・禁止伝送語彙の両Theoryには、既存のReportingと新規Csvを追加する。これによりCsvへX509／署名／伝送固有語彙が混入した場合も赤になる。

- [ ] **Step 6: 検査を実行する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ArchitectureTests|FullyQualifiedName~OfflineComplianceTests|FullyQualifiedName~AppOfflineComplianceTests|FullyQualifiedName~KokuhorenTransmissionSeparationTests" -v normal`

Expected: PASS。Csvが検査対象から外れると落ちるテストデータ数もアサートする。

- [ ] **Step 7: コミット**

```bash
git add Tsumugi.sln src/Tsumugi.Infrastructure.Csv/CsvAssemblyMarker.cs src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj tests/Tsumugi.Infrastructure.Tests/ArchitectureTests.cs tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs tests/Tsumugi.Infrastructure.Tests/KokuhorenTransmissionSeparationTests.cs
git commit -m "build(phase3-0/AC3-0-3): add CSV specification assembly"
```

---

### Task 6: ADR 0024と全項目マッピングを機械可読化する

**Files:**
- Create: `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`
- Create: `docs/phase3-claim-field-mapping.md`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/common-r7-10.json`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/provider-claim-r7-10.json`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/field-mapping-r7-10.json`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/sources.json`
- Create: `docs/spec-data/phase3/report-fields-r8-06.json`
- Create: `docs/spec-data/phase3/report-field-mapping-r8-06.json`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/CsvSpecificationCompletenessTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/ClaimFieldMappingCompletenessTests.cs`

- [ ] **Step 1: 仕様JSONのschemaを固定する**

`provider-claim-r7-10.json`の各レコードを次の形で記録する。

```json
{
  "recordId": "provider:<exchange-id>:<inner-record-type>",
  "exchangeInformationId": "<official-id>",
  "innerRecordType": "<official-type>",
  "order": 1,
  "sourceDocumentId": "provider-r7-10",
  "sourcePage": 1,
  "fields": [
    {
      "fieldId": "provider:<exchange-id>:<inner-record-type>:001",
      "position": 1,
      "officialName": "<official-name>",
      "requiredWhen": "always",
      "dataType": "numeric|text|date|yearMonth|code",
      "maxBytes": 1,
      "quoteRule": "<official-rule>",
      "allowedCodes": []
    }
  ]
}
```

山括弧はschema説明用であり、実ファイルへ残さない。共通編の外側コントロール／データ／エンドレコードも同じく全項目を収録する。

- [ ] **Step 2: 事業所編の全対象レコードを転記する**

請求書、請求明細書の基本・日数・明細・集計・契約、実績記録票の基本・明細を含め、B型提出に必要な全レコードを転記する。簡略化した「基本・明細・集計の3種」へ縮退させない。

- [ ] **Step 3: 共通編・事業所編の全fieldIdを入力源へマッピングする**

`field-mapping-r7-10.json`は共通編と事業所編を合わせた全`fieldId`に対し、必ず次のいずれかを持つ。

```json
{
  "fieldId": "...",
  "status": "existing",
  "modelPath": "Certificate.CertificateNumber",
  "requiredCondition": "...",
  "notes": ""
}
```

```json
{
  "fieldId": "...",
  "status": "missing",
  "targetModel": "DailyRecord",
  "targetProperty": "<proposed-property>",
  "migrationRequired": true,
  "uiSurface": "DailyRecordView",
  "requiredCondition": "...",
  "notes": "Phase 3-1 AC3-8"
}
```

コントロールレコードの処理対象年月など、独立した操作入力は`status: "explicitInput"`と`inputContract: "ProcessingMonth"`を持たせる。レコード種別や件数など仕様から決定論的に作る値は`status: "generated"`と`generatorRule`を持たせる。自由記述からの推定、複数候補、空`modelPath`を許可しない。性別は公式項目が存在しない限りmappingへ追加しない。

- [ ] **Step 4: 3帳票の独立field inventoryとmappingを作る**

`report-fields-r8-06.json`には公式様式・記載例を正本として、次の3 artifactをCSV fieldとは独立したIDで全件収録する。

```text
report:service-performance:<section>:<position>
report:benefit-claim-form:<section>:<position>
report:benefit-claim-detail:<section>:<position>
```

各項目は`artifactId`、section、position、officialName、requiredWhen、sourceDocumentId、sourcePageを持つ。`report-field-mapping-r8-06.json`は全report field IDを`existing`／`missing`／`explicitInput`／`generated`のいずれかへ対応させる。CSVに同義項目がある場合も、`sameMeaningAsCsvFieldId`で関連付けるだけにし、帳票inventory自体をCSV field一覧から生成しない。

- [ ] **Step 5: 完全性テストを先に書く**

以下を検証する。

- 共通編／事業所編の`recordId`、`fieldId`、`position`が重複しない
- 各レコードの`position`が1から連続する
- `maxBytes > 0`、`sourcePage > 0`、`sourceDocumentId`が`sources.json`に存在する
- common側とprovider側を合わせた全`fieldId`集合とmapping側の全`fieldId`集合が完全一致する
- `existing`は`modelPath`必須、`missing`は`targetModel`／`targetProperty`／`migrationRequired`／`uiSurface`必須
- `explicitInput`は`inputContract`必須、`generated`は`generatorRule`必須
- `status`に`unknown`、`tbd`、`assumed`を許可しない
- `sources.json`のURL、版、取得日、SHA-256がADR 0024と一致する
- 3帳票それぞれのinventory field集合とreport mapping集合が完全一致する
- 帳票ごとのsection／positionが重複せず、一次資料から固定した期待件数と一致する
- `sameMeaningAsCsvFieldId`がある場合は実在するCSV fieldを指す

`CsvSpecificationCompletenessTests`はembedded CSV JSONを`JsonDocument`で直接読む。`ClaimFieldMappingCompletenessTests`はsolution rootを解決して`docs/spec-data/phase3/*.json`を読み、Csv loader実装前でもコンパイル・実行できるようにする。

- [ ] **Step 6: テストを赤で確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj --filter "FullyQualifiedName~CsvSpecificationCompletenessTests|FullyQualifiedName~ClaimFieldMappingCompletenessTests" -v normal`

Expected: 不足しているrecord／field／mappingを具体的なIDで列挙してFAILする。

- [ ] **Step 7: 全データを埋めて緑にする**

Expected: PASS。件数をテストへ固定し、将来フィールドが脱落した場合に赤になるようにする。件数は一次資料から数えた実数を使い、計画書の推測値を使わない。

- [ ] **Step 8: ADRと人間向け一覧を書く**

ADR 0024にはCP932、CRLF、外側3レコード、処理対象年月、引用、最大バイト数、共通編／事業所編・3帳票様式の版選択を記録する。`docs/phase3-claim-field-mapping.md`にはCSVと3帳票の全fieldIdの対応、相互参照、Phase 3-1へ送る`missing`一覧を記載する。

- [ ] **Step 9: 検査とコミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj -v normal
git diff --check -- docs/decisions/0024-kokuhoren-csv-and-field-mapping.md docs/phase3-claim-field-mapping.md docs/spec-data/phase3 src/Tsumugi.Infrastructure.Csv/Specifications tests/Tsumugi.Infrastructure.Csv.Tests/CsvSpecificationCompletenessTests.cs tests/Tsumugi.Infrastructure.Csv.Tests/ClaimFieldMappingCompletenessTests.cs
git add docs/decisions/0024-kokuhoren-csv-and-field-mapping.md docs/phase3-claim-field-mapping.md docs/spec-data/phase3/report-fields-r8-06.json docs/spec-data/phase3/report-field-mapping-r8-06.json src/Tsumugi.Infrastructure.Csv/Specifications/common-r7-10.json src/Tsumugi.Infrastructure.Csv/Specifications/provider-claim-r7-10.json src/Tsumugi.Infrastructure.Csv/Specifications/field-mapping-r7-10.json src/Tsumugi.Infrastructure.Csv/Specifications/sources.json tests/Tsumugi.Infrastructure.Csv.Tests/CsvSpecificationCompletenessTests.cs tests/Tsumugi.Infrastructure.Csv.Tests/ClaimFieldMappingCompletenessTests.cs
git commit -m "docs(phase3-0/AC3-0-5): map every official claim field"
```

---

### Task 7: CSV仕様カタログをTDDで実装する

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/Models/CsvSourceDocument.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/Models/CsvFieldSpecification.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/Models/CsvRecordSpecification.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/Models/CsvFieldMapping.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/CsvSpecificationCatalog.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/CsvSpecificationLoader.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/CsvSpecificationLoaderTests.cs`

- [ ] **Step 1: 失敗テストを書く**

`LoadEmbedded()`が4つのJSONを読み、レコード順・field位置・mappingを返すことを確認する。別の`Load(Stream...)`では、重複ID、位置欠番、不正SHA、未知source、mapping不足、空の必須条件を個別に拒否する。

- [ ] **Step 2: 赤を確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj --filter "FullyQualifiedName~CsvSpecificationLoaderTests" -v normal`

Expected: `CsvSpecificationLoader`未定義でFAIL。

- [ ] **Step 3: 最小実装を書く**

`System.Text.Json`で厳格にdeserializeし、未知プロパティは拒否する。`CsvSpecificationCatalog`のconstructorで全不変条件を検証し、エラーにはPIIではなく`recordId`／`fieldId`を含める。

公開APIは次に限定する。

```csharp
public sealed class CsvSpecificationLoader
{
    public static CsvSpecificationCatalog LoadEmbedded();
    internal static CsvSpecificationCatalog Load(
        Stream common, Stream provider, Stream mapping, Stream sources);
}

public sealed record CsvSpecificationCatalog(
    string Version,
    IReadOnlyList<CsvRecordSpecification> CommonRecords,
    IReadOnlyList<CsvRecordSpecification> ProviderRecords,
    IReadOnlyDictionary<string, CsvFieldMapping> MappingByFieldId,
    IReadOnlyDictionary<string, CsvSourceDocument> SourcesById);
```

テストから`internal Load`を呼べるようCsv csprojへ`InternalsVisibleTo="Tsumugi.Infrastructure.Csv.Tests"`を追加する。

- [ ] **Step 4: 緑を確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj -v normal`

Expected: PASS。

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj src/Tsumugi.Infrastructure.Csv/Specifications/Models src/Tsumugi.Infrastructure.Csv/Specifications/CsvSpecificationCatalog.cs src/Tsumugi.Infrastructure.Csv/Specifications/CsvSpecificationLoader.cs tests/Tsumugi.Infrastructure.Csv.Tests/CsvSpecificationLoaderTests.cs
git commit -m "feat(phase3-0/AC3-0-3): validate embedded CSV specification catalog"
```

---

### Task 8: ADR 0025で報酬計算の端数規則を確定する

**Files:**
- Create: `docs/decisions/0025-claim-rounding-rules.md`

- [ ] **Step 1: 丸めが発生する全段階を列挙する**

平均工賃月額、単位数合計、地域単価乗算、総費用額、給付額、利用者負担、上限額管理について、丸め単位・方向・順序・根拠頁を別々に記録する。

- [ ] **Step 2: 固定小数表現を決定する**

金額は整数円、単価は`decimal`とし、`double`／`float`を禁止する。中間値をいつ丸めるかを公式例と照合し、「最後に一括丸め」などの推測を禁止する。

- [ ] **Step 3: ADRを書く**

Phase 3-1の`RoundingPolicy`が受け取るrule IDと、公式ケースの期待値を表形式で記録する。

- [ ] **Step 4: 検査とコミット**

```bash
git diff --check -- docs/decisions/0025-claim-rounding-rules.md
git add docs/decisions/0025-claim-rounding-rules.md
git commit -m "docs(phase3-0/AC3-0-1): define claim rounding stages"
```

---

### Task 9: ADR 0026で請求スナップショット規律を確定する

**Files:**
- Create: `docs/decisions/0026-claim-batch-snapshot.md`

- [ ] **Step 1: 履歴規律を記述する**

`(OfficeId, ServiceMonth)`ごとに初回`New`は1件、再確定は`Correct`、取下げは`Cancel`とする。全`Correct`／`Cancel`の`OriginId`は初代`New.Id`を指す。

- [ ] **Step 2: 実効版の決定規則を記述する**

`CreatedAt`昇順、同時刻は`Id`昇順で決定論的に並べ、末尾が`Cancel`なら実効版なしとする。孤立したCorrect／Cancel、異なる初代Newへの参照、Cancel後のCorrect、複数Cancelは不正履歴としてフェイルクローズする。

- [ ] **Step 3: スナップショット形式を決定する**

`ClaimDetail`は受給者単位とし、`SnapshotSchemaVersion`、`InputSnapshotJson`、`CalculationSnapshotJson`、合計を保持する。JSONはPhase 3-1の型付きrecordから決定論的に生成し、帳票・CSVは確定時JSONだけを読む。下層データを再読込しない。

- [ ] **Step 4: DB制約を記述する**

`(OfficeId, ServiceMonthKey) WHERE Kind = 1`のpartial unique index、`ClaimDetails(ClaimBatchId, RecipientId)` unique index、FKの`DeleteBehavior.Restrict`、AppendOnlyGuardを決定する。

- [ ] **Step 5: 検査とコミット**

```bash
git diff --check -- docs/decisions/0026-claim-batch-snapshot.md
git add docs/decisions/0026-claim-batch-snapshot.md
git commit -m "docs(phase3-0/AC3-0-1): define append-only claim snapshots"
```

---

### Task 10: 年月・版・出典契約をDomainへ追加する

**Files:**
- Create: `src/Tsumugi.Domain/ValueObjects/ServiceMonth.cs`
- Create: `src/Tsumugi.Domain/ValueObjects/ProcessingMonth.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimMasterVersion.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/CsvSpecificationVersion.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimSourceDocument.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimMasterRelease.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimMasterCatalogPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/ValueObjects/ServiceMonthTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/ValueObjects/ProcessingMonthTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimMasterCatalogPolicyTests.cs`

- [ ] **Step 1: 年月型の失敗テストを書く**

両型について年1900〜2200、月1〜12、`ToInt`／`FromInt`、比較、文字列表現をテストする。reflectionで相互の`op_Implicit`／`op_Explicit`が存在しないことも固定する。

- [ ] **Step 2: 赤を確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~ServiceMonthTests|FullyQualifiedName~ProcessingMonthTests" -v normal`

Expected: 型未定義でFAIL。

- [ ] **Step 3: 年月型を実装する**

既存`YearMonth`と同じ検証・比較を持つが、公開変換演算子を持たせない。

```csharp
public readonly record struct ServiceMonth : IComparable<ServiceMonth>
{
    public int Year { get; }
    public int Month { get; }
    public ServiceMonth(int year, int month) { /* YearMonthと同じ範囲検証 */ }
    public int ToInt() => Year * 100 + Month;
    public static ServiceMonth FromInt(int value) => new(value / 100, value % 100);
    public int CompareTo(ServiceMonth other) => ToInt().CompareTo(other.ToInt());
    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
```

`ProcessingMonth`も同形で、コード共有のための共通public型や暗黙変換は作らない。

- [ ] **Step 4: 版・出典モデルの失敗テストを書く**

空版、空documentId、HTTPSでないURL、64桁でないSHA-256、開始月より前の終了月を拒否するテストを書く。

モデルの契約は次に固定する。

```csharp
public sealed record ClaimSourceDocument(
    string DocumentId,
    string Title,
    string Publisher,
    DateOnly EffectiveAt,
    DateOnly RetrievedAt,
    string Url,
    string Sha256,
    string? Supersedes,
    string? Notes);

public sealed record ClaimMasterRelease(
    ClaimMasterVersion Version,
    ServiceMonth EffectiveFrom,
    ServiceMonth? EffectiveTo,
    IReadOnlyList<string> SourceDocumentIds);
```

`CsvSpecificationVersion`はClaimBatchへ保存するDomain値型であり、Csvアセンブリのcatalog自体はTask 7時点ではJSONの`string Version`を返す。請求を確定するPhase 3-1のApplication境界で、検証済み文字列から値型を構築してClaimBatchへ保存する。

- [ ] **Step 5: カタログ規則の失敗テストを書く**

正常な2024-04版／2026-06版、重複開始月、重複version、期間重複、期間空白、未知sourceDocumentId、対象月に版なしをテストする。

- [ ] **Step 6: 最小実装を書く**

```csharp
public static class ClaimMasterCatalogPolicy
{
    public static void Validate(
        IReadOnlyCollection<ClaimMasterRelease> releases,
        IReadOnlyCollection<ClaimSourceDocument> sources);

    public static ClaimMasterRelease Resolve(
        IReadOnlyCollection<ClaimMasterRelease> releases,
        IReadOnlyCollection<ClaimSourceDocument> sources,
        ServiceMonth serviceMonth);
}
```

空白期間は通常の連続版では拒否する。制度上意図的な非適用期間が必要になった場合は、明示的な`Unavailable` releaseを追加し、暗黙の穴を作らない。

- [ ] **Step 7: テストとカバレッジを確認する**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~ServiceMonthTests|FullyQualifiedName~ProcessingMonthTests|FullyQualifiedName~ClaimMasterCatalogPolicyTests" -v normal
dotnet test tests/Tsumugi.Domain.Tests -c Release -p:CollectCoverage=true -p:Include="[Tsumugi.Domain]*" -p:Threshold=95 -p:ThresholdType=line -p:ThresholdStat=total
```

Expected: PASS、Domain 95%以上。

- [ ] **Step 8: コミット**

```bash
git add src/Tsumugi.Domain/ValueObjects/ServiceMonth.cs src/Tsumugi.Domain/ValueObjects/ProcessingMonth.cs src/Tsumugi.Domain/Logic/Claim/Models src/Tsumugi.Domain/Logic/Claim/ClaimMasterCatalogPolicy.cs tests/Tsumugi.Domain.Tests/ValueObjects/ServiceMonthTests.cs tests/Tsumugi.Domain.Tests/ValueObjects/ProcessingMonthTests.cs tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimMasterCatalogPolicyTests.cs
git commit -m "feat(phase3-0/AC3-0-2): add claim month and source version contracts"
```

---

### Task 11: 出典付きJSONマスタproviderをTDDで実装する

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimMasterProvider.cs`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Schema/source-catalog.schema.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Create: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs`
- Modify: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`
- Modify: `src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Application抽象を定義する**

```csharp
public interface IClaimMasterProvider
{
    ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth);
}
```

Phase 3-0では版解決だけを公開する。基本報酬等のlookup APIは各schemaと実値が入るPhase 3-1で追加する。

- [ ] **Step 2: providerと全master fileの失敗テストを書く**

embedded `sources.json`から2026-05と2026-06の異なる版を解決すること、重複・空白・未知source・不正SHA・schema version不一致を拒否することをテストする。さらに6つのmaster file全てについて、未知`masterKind`、期待ファイル欠落、重複key＋effectiveFrom、逆転期間、同一keyの期間重複、同一keyの期間空白、未知source、空`values`（entryが存在する場合）、未知propertyを個別に拒否する。

- [ ] **Step 3: 赤を確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~JsonClaimMasterProviderTests" -v normal`

Expected: provider未定義でFAIL。

- [ ] **Step 4: JSON契約を作る**

`sources.json`にはADR 0020の実URL、版、施行日、取得日、SHA-256、2024／2026 releaseを入れる。各マスタファイルは次の形で作り、3-0では`entries`を空にする。

```json
{
  "schemaVersion": "1",
  "masterKind": "basic-rewards",
  "entries": []
}
```

空文字、`TBD`、仮URL、ゼロ埋めSHAを残さない。schemaファイルは値の形、`effectiveFrom`／`effectiveTo`、`sourceDocumentId`、`key`、`values`を定義する。

- [ ] **Step 5: providerとmaster file validatorを実装する**

`LoadEmbedded()`とテスト用`internal Load(Stream sources, IReadOnlyDictionary<string, Stream> masters)`を持たせる。`System.Text.Json`のweb defaultsを使い、property名の大文字小文字違い、未知property、nullを拒否する。deserialize後に`ClaimMasterCatalogPolicy.Validate`を必ず通す。

`ClaimMasterFileValidator`は起動時に6ファイルを全て読み、次を検証する。

```csharp
internal static class ClaimMasterFileValidator
{
    internal static void ValidateAll(
        IReadOnlyDictionary<string, Stream> masterFiles,
        IReadOnlySet<string> knownSourceDocumentIds);
}
```

期待する`masterKind`集合を完全一致で固定し、各entryの`key`、適用期間、source参照、`values` object、同一keyの期間重複と期間空白を検証する。意図的な非適用期間は明示的な`Unavailable` entryを要求し、暗黙の穴を許可しない。3-0の空`entries`は有効だが、entryが1件でもある場合の空`values`は無効とする。provider constructorは版解決だけを行う場合でもvalidatorを必ず実行し、不正master fileを未使用のまま見逃さない。

- [ ] **Step 6: resourceとDIを配線する**

Infrastructure csprojへ`ClaimMasters/Schema/*.json`と`ClaimMasters/Seed/*.json`をEmbeddedResource登録する。DIは遅延生成の`AddSingleton<IClaimMasterProvider, JsonClaimMasterProvider>()`にせず、登録処理中に全resourceを検証してからinstance登録する。

```csharp
var claimMasterProvider = JsonClaimMasterProvider.LoadEmbedded();
services.AddSingleton<IClaimMasterProvider>(claimMasterProvider);
```

これによりPhase 3-0にconsumerがなくても、`AddTsumugiInfrastructure`実行時に不正masterで起動構成が失敗する。

`tests/Tsumugi.App.Tests/CompositionRootTests.cs`には、service descriptorがfactory／implementation typeではなく検証済み`ImplementationInstance`を持つこと、scopeから同じ`IClaimMasterProvider`を解決でき、2026-06版を返すことを追加する。

- [ ] **Step 7: 緑を確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~JsonClaimMasterProviderTests" -v normal
dotnet test tests/Tsumugi.App.Tests/Tsumugi.App.Tests.csproj --filter "FullyQualifiedName~CompositionRootTests" -v normal
```

Expected: PASS。

- [ ] **Step 8: コミット**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimMasterProvider.cs src/Tsumugi.Infrastructure/ClaimMasters src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj src/Tsumugi.Infrastructure/DependencyInjection.cs tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs tests/Tsumugi.App.Tests/CompositionRootTests.cs
git commit -m "feat(phase3-0/AC3-0-2): load versioned claim master metadata"
```

---

### Task 12: 制度実値・CSV仕様値の配置境界をCIで固定する

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/ExternalSpecificationLiteralGuard.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimSpecificationBoundaryTests.cs`

- [ ] **Step 1: 歯あり性テストを先に書く**

一時ディレクトリに最小JSONとC#を作り、次を検出するunit testを書く。

- マスタ`values`配下の数値を`Logic/Claim/*.cs`へ直接書く
- マスタ`values`配下の正式コードをApplicationへ文字列literalで書く
- CSVの`fieldId`／交換情報ID／内側レコード種別をCsv以外のC#へ書く
- source参照なしのmaster entryを追加する
- 許可ディレクトリ外へclaim用JSONを追加する

- [ ] **Step 2: production scanの失敗テストを書く**

`SourceCodeScanner`の既存列挙を再利用し、次を検証する。

1. 報酬マスタJSONは`src/Tsumugi.Infrastructure/ClaimMasters/Seed/`だけに存在する。
2. CSV仕様JSONは`src/Tsumugi.Infrastructure.Csv/Specifications/`だけに存在する。
3. master entryの`values`配下にある文字列と10以上の数値tokenが、DomainまたはApplicationのproduction C#全体へliteralとして現れない。
4. CSV仕様の`fieldId`、交換情報ID、内側レコード種別がCsvプロジェクト外のproduction C#へliteralとして現れない。
5. すべてのentryが既知`sourceDocumentId`と適用開始月を持つ。

既存Domain全体の大きな数値を一律禁止しない。年、バッファ、既存工賃値などの誤検知を作らない。

- [ ] **Step 3: 赤を確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ClaimSpecificationBoundaryTests" -v normal`

Expected: guard未実装でFAIL。

- [ ] **Step 4: guardを最小実装する**

検査結果は`relativePath:line`と違反したcatalog pathを返す。コメントとテストコード、`obj`、`bin`、Migrationsを除外する。allowlistを作る場合は`(path, literal, reason)`を必須とし、空reasonをCI違反にする。

- [ ] **Step 5: 緑を確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ClaimSpecificationBoundaryTests" -v normal`

Expected: production scanと歯あり性unit testがともにPASS。

- [ ] **Step 6: コミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/ExternalSpecificationLiteralGuard.cs tests/Tsumugi.Infrastructure.Tests/ClaimSpecificationBoundaryTests.cs
git commit -m "test(phase3-0/AC3-0-2): enforce external claim specification boundaries"
```

---

### Task 13: ClaimBatch／ClaimDetailと履歴解決規則をTDDで実装する

**Files:**
- Create: `src/Tsumugi.Domain/Entities/ClaimBatch.cs`
- Create: `src/Tsumugi.Domain/Entities/ClaimDetail.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimBatchPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Entities/ClaimBatchTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Entities/ClaimDetailTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimBatchPolicyTests.cs`

- [ ] **Step 1: entityの失敗テストを書く**

`NewRecord`が`OriginId=null`、`Correction`／`Cancellation`が空でない初代New IDを要求すること、負の合計を拒否すること、Cancel合計が0であることをテストする。Detailはbatch／recipient ID、schema version、2つのJSON、非負合計を必須とする。

- [ ] **Step 2: 赤を確認する**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~ClaimBatchTests|FullyQualifiedName~ClaimDetailTests" -v normal`

Expected: 型未定義でFAIL。

- [ ] **Step 3: 最小entityを実装する**

`ClaimBatch`の永続項目を次に固定する。

```text
OfficeId / ServiceMonth / Kind / OriginId
TotalUnits / TotalCostYen / TotalBenefitYen / TotalBurdenYen
ClaimMasterVersion / CsvSpecificationVersion / ApplicationVersion
EntityのId / CreatedAt / CreatedBy / ConcurrencyToken
```

`ClaimDetail`は次を持つ。

```text
ClaimBatchId / RecipientId / SnapshotSchemaVersion
InputSnapshotJson / CalculationSnapshotJson
TotalUnits / TotalCostYen / BenefitYen / BurdenYen
EntityのId / CreatedAt / CreatedBy / ConcurrencyToken
```

JSONの空白検査だけをDomainで行い、deserializeや公式項目検証は行わない。

- [ ] **Step 4: 履歴規則の失敗テストを書く**

次をテーブル駆動でテストする。

- Newのみ → Newが実効
- New + Correct → Correctが実効
- New + Correct + Correct（全て初代NewをOriginIdに持つ）→ 最新Correctが実効
- New + Cancel → null
- 同一CreatedAt → Id順で決定論的
- Newなし、複数New、初代New以外のOriginId、Cancel後Correct、複数Cancel → 例外
- 異なる`OfficeId`または`ServiceMonth`が1つの履歴集合に混在する → 例外

- [ ] **Step 5: 専用policyを実装する**

```csharp
public static class ClaimBatchPolicy
{
    public static ClaimBatch? Effective(IReadOnlyCollection<ClaimBatch> history);
    public static void ValidateHistory(IReadOnlyCollection<ClaimBatch> history);
}
```

`AppendOnlyChainPolicy`を呼ばない。あちらはOriginIdが直前レコードを指す契約であり、ClaimBatchと異なる。

- [ ] **Step 6: Domainテストとカバレッジを確認する**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~ClaimBatch" -v normal
dotnet test tests/Tsumugi.Domain.Tests -c Release -p:CollectCoverage=true -p:Include="[Tsumugi.Domain]*" -p:Threshold=95 -p:ThresholdType=line -p:ThresholdStat=total
```

Expected: PASS、Domain 95%以上。

- [ ] **Step 7: コミット**

```bash
git add src/Tsumugi.Domain/Entities/ClaimBatch.cs src/Tsumugi.Domain/Entities/ClaimDetail.cs src/Tsumugi.Domain/Logic/Claim/ClaimBatchPolicy.cs tests/Tsumugi.Domain.Tests/Entities/ClaimBatchTests.cs tests/Tsumugi.Domain.Tests/Entities/ClaimDetailTests.cs tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimBatchPolicyTests.cs
git commit -m "feat(phase3-0/AC3-0-4): add append-only claim snapshot domain"
```

---

### Task 14: EF Core構成、AppendOnlyGuard、migrationを実装する

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimBatchConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimDetailConfiguration.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddClaimBatchAndDetail.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddClaimBatchAndDetail.Designer.cs`
- Modify: `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimBatchDuplicateNewIndexTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs`

- [ ] **Step 1: guardとindexの失敗テストを書く**

ClaimBatch／DetailのModified・Deletedが`AppendOnlyViolationException`になること、同一Office／ServiceMonthの2件目NewがSQLite error 19で拒否されること、Correct／Cancelはpartial indexを通ることを書く。

- [ ] **Step 2: DbSetとAppendOnlyGuardを追加する**

`TsumugiDbContext`へ`ClaimBatches`／`ClaimDetails`を追加し、`AppendOnlyTypes`へ両型を追加する。

- [ ] **Step 3: EF configurationを書く**

- `ServiceMonth`は`YYYYMM`の`ServiceMonthKey`整数列へ変換する
- `ClaimMasterVersion`／`CsvSpecificationVersion`はそれぞれ最大長64の文字列列へ変換する
- `ApplicationVersion`／`SnapshotSchemaVersion`は最大長64
- JSON列はrequired。SQLiteの`TEXT`として保存する
- `ClaimBatches(OfficeId, ServiceMonthKey) WHERE Kind = 1`をuniqueにする
- `ClaimDetails(ClaimBatchId, RecipientId)`をuniqueにする
- Detail→Batch FKは`DeleteBehavior.Restrict`
- `ClaimBatch.OriginId`→`ClaimBatch.Id`の自己参照FKも`DeleteBehavior.Restrict`にし、存在しない起点IDをDBで拒否する
- `OriginId`、`ClaimBatchId`へindexを付ける
- `CreatedBy`最大64、`ConcurrencyToken`はconcurrency token

- [ ] **Step 4: migrationを生成する**

```bash
dotnet ef migrations add AddClaimBatchAndDetail --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

- [ ] **Step 5: migrationを目視レビューする**

生成されたUp／Down、filter文字列`"Kind" = 1`、列名、FK Restrict、2つのunique index、snapshot更新を確認する。手作業でtimestampやsnapshotを捏造しない。

- [ ] **Step 6: migration往復テストを書く**

最新migrationまで適用→Claimテーブル／index存在→直前migrationへ戻す→Claimテーブル不存在→再適用、を一時SQLiteで確認する。

- [ ] **Step 7: テストを実行する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AppendOnlyGuardPhase3Tests|FullyQualifiedName~ClaimBatchDuplicateNewIndexTests|FullyQualifiedName~ClaimBatchMigrationTests" -v normal`

Expected: PASS。

- [ ] **Step 8: 生成ファイルを確認してコミットする**

Run: `git status --short`

`<timestamp>`を実際の生成名へ置き換えて、対象だけを明示stageする。

```bash
git add src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimBatchConfiguration.cs src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimDetailConfiguration.cs src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddClaimBatchAndDetail.cs src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddClaimBatchAndDetail.Designer.cs src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs tests/Tsumugi.Infrastructure.Tests/ClaimBatchDuplicateNewIndexTests.cs tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs
git commit -m "feat(phase3-0/AC3-0-4): persist append-only claim snapshots"
```

---

### Task 15: ClaimBatchRepositoryをTDDで実装する

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimBatchRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ClaimBatchRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs`
- Modify: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`

- [ ] **Step 1: Repository契約を定義する**

```csharp
public interface IClaimBatchRepository
{
    Task AddAsync(ClaimBatch batch, IReadOnlyList<ClaimDetail> details, CancellationToken ct);
    Task<IReadOnlyList<ClaimBatch>> ListHistoryAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
    Task<ClaimBatch?> GetEffectiveAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
    Task<IReadOnlyList<ClaimDetail>> ListDetailsAsync(
        Guid claimBatchId, CancellationToken ct);
}
```

Repositoryは`SaveChangesAsync`を呼ばない。呼出元のUnitOfWorkが保存時点を決める。

- [ ] **Step 2: 失敗テストを書く**

次を検証する。

- Add後、SaveChanges前は別contextから見えず、SaveChanges後にbatch＋detailsが見える
- detailの`ClaimBatchId`が引数batchと異なる場合は追加前に拒否する
- 既存履歴と追加batchを合わせて`ClaimBatchPolicy.ValidateHistory`を通し、孤立Correct／CancelやCancel後Correctを追加前に拒否する
- 読取は`AsNoTracking`
- New／Correct履歴から`ClaimBatchPolicy`でCorrectを返す
- Cancel後はnull
- 不正履歴は握りつぶさず例外
- SQLiteのDateTimeOffset制限を避け、取得後に`CreatedAt`／`Id`で決定論的に並べる

- [ ] **Step 3: 赤を確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ClaimBatchRepositoryTests" -v normal`

Expected: Repository未定義でFAIL。

- [ ] **Step 4: 最小実装を書く**

`ListHistoryAsync`はOffice／ServiceMonthだけをSQLで絞り、`ToListAsync`後に`OrderBy(CreatedAt).ThenBy(Id)`する。`GetEffectiveAsync`は同じ履歴を`ClaimBatchPolicy.Effective`へ渡す。Detailも`AsNoTracking`で`RecipientId`／`Id`順に返す。

- [ ] **Step 5: DIへ登録する**

`services.AddScoped<IClaimBatchRepository, ClaimBatchRepository>()`をInfrastructure登録へ追加する。

`tests/Tsumugi.App.Tests/CompositionRootTests.cs`へ、scopeから`IClaimBatchRepository`を解決できるテストを追加する。

- [ ] **Step 6: 緑を確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ClaimBatchRepositoryTests" -v normal
dotnet test tests/Tsumugi.App.Tests/Tsumugi.App.Tests.csproj --filter "FullyQualifiedName~CompositionRootTests" -v normal
```

Expected: PASS。

- [ ] **Step 7: コミット**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimBatchRepository.cs src/Tsumugi.Infrastructure/Persistence/ClaimBatchRepository.cs src/Tsumugi.Infrastructure/DependencyInjection.cs tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs tests/Tsumugi.App.Tests/CompositionRootTests.cs
git commit -m "feat(phase3-0/AC3-0-4): add claim snapshot repository"
```

---

### Task 16: 引継ぎ文書とPhase 3-0受け入れ証跡を完成する

**Files:**
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Create: `docs/phase3-0-acceptance.md`

- [ ] **Step 1: open questionsを同期する**

次を実際のADR／テストへリンクしてクローズする。

- 報酬・CSVハードコード機械判定
- OfficeCapability正式コード
- 性別など利用者属性の拡張（事業所編に性別なし。Recipient migrationなし）
- 食事提供／高額障害福祉サービス費等の制度値
- 負担区分の月額上限
- 平均工賃月額の正式定義

AC3-0-5で`missing`になった項目はクローズせず、Phase 3-1 AC3-8として項目ID、追加先、UI、migration要否を起票する。一次資料から一意に決まらない事項が1件でもあれば3-0完了扱いにしない。

- [ ] **Step 2: 受け入れ文書を書く**

`docs/phase3-0-acceptance.md`にAC3-0-1〜5を列挙し、各ACへADR、実装ファイル、テスト名、コミット、実行結果を結び付ける。

- [ ] **Step 3: CHANGELOGへ追記する**

Phase 3-0で追加した契約と、まだ実装していない3-1〜3-3の範囲を明確に分ける。「請求CSV生成完了」と誤記しない。

- [ ] **Step 4: targeted testsを実行する**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~Claim" -v normal
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj -v normal
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Claim|FullyQualifiedName~OfflineCompliance|FullyQualifiedName~Architecture" -v normal
```

Expected: 全PASS。

- [ ] **Step 5: 全品質ゲートを実行する**

```bash
dotnet format --verify-no-changes
./build/ci.sh
dotnet list package --vulnerable --include-transitive
```

Expected:

- format差分なし
- `==> CI OK`
- Domain 95%以上、Application 70%以上を維持（90%昇格はPhase 3-3）
- 新規未抑制の脆弱性なし。既知SQLite抑制だけなら根拠を受け入れ文書へ記録

- [ ] **Step 6: staged scopeを検証する**

```bash
git status --short
git diff --check
```

意図しない`.serena/`、`graphify-out/`、一時取得PDF、テスト結果をstageしない。

- [ ] **Step 7: 文書をコミットする**

```bash
git add docs/open-questions.md CHANGELOG.md docs/phase3-0-acceptance.md
git commit -m "docs(phase3-0): record AC3-0 acceptance evidence"
```

- [ ] **Step 8: Phase 3-0の最終レビューを依頼する**

レビュー対象はPhase 3-0開始commitからHEADまで。AC3-0-1〜5、依存方向、オフライン、append-only、公式項目の全件対応を確認し、Critical／High／Mediumが0件になるまで3-1へ進まない。

---

## 3. Phase 3-0完了ゲート

- [ ] **AC3-0-1:** ADR 0020〜0026がURL、版、施行日、取得日、SHA-256、結論、影響を持ち、placeholderがない。
- [ ] **AC3-0-2:** マスタschema、出典版、適用年月解決、重複・空白・未知sourceの拒否がテストされている。
- [ ] **AC3-0-3:** `Tsumugi.Infrastructure.Csv`とCsv.Testsがsolutionに入り、依存方向・assembly reference・P/Invoke・URL literalの検査対象になっている。
- [ ] **AC3-0-4:** ClaimBatch／Detail、専用履歴policy、AppendOnlyGuard、partial unique index、migration往復、Repositoryが緑。
- [ ] **AC3-0-5:** 共通編／事業所編の全fieldIdが`existing`／`missing`／`explicitInput`／`generated`へ一意に分類され、3帳票を含む集合一致テストが緑。性別の推測追加がない。
- [ ] **横断:** Domain 95%以上、Application 70%以上、format、build、test、offline、architecture、脆弱性確認が緑。
- [ ] **停止条件:** 未確定値、未対応fieldId、`TBD`、仮コード、空SHA、throw-only production実装が0件。

Phase 3-0が受け入れられたら、本計画を延長せず、AC3-1／2／3／4／8／9だけを対象にPhase 3-1の新しい実装計画を作成する。制度上の非適用期間を表す`Unavailable` release／entryが実際に必要な場合は、その計画でJSON discriminatorとDomain表現を確定してから実値を投入する。
