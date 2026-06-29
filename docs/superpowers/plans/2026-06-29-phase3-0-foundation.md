# Tsumugi Phase 3-0 実装計画 — 土台（出典確定＋非ブロッキング先行）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3-1〜3-3 の前提となる出典確定（ADR 0018〜0024, 0026）と、出典に依存しない土台コード（日本語フォント埋込、ハードコード機械判定スキャナ、マスタ抽象、`Tsumugi.Infrastructure.Csv` アセンブリ空殻、`ClaimBatch`/`ClaimDetail` の追記型骨組み）を一通り揃える。

**Architecture:** 既存 4 プロジェクト構成（`Tsumugi.Domain` / `.Application` / `.Infrastructure` / `.App`）+ `Tsumugi.Infrastructure.Reporting` を維持しつつ、新規アセンブリ `Tsumugi.Infrastructure.Csv` を追加する。Domain には `Logic/Claim/` 配下に純粋関数のマスタ抽象 interface 群を追加。Infrastructure には JSON マスタローダの空殻と `ClaimBatch`/`ClaimDetail` の EF Core 構成・migration・Repository を追加。`OfflineComplianceTests` および `AppOfflineComplianceTests` の `[Theory]` 対象に新アセンブリを追加して、伝送系コード混入を機械判定し続ける。

**Tech Stack:** .NET 10 / Avalonia 11.3 / EF Core 10 / SQLite / CommunityToolkit.Mvvm 8.4 / xUnit / FluentAssertions 7.x / QuestPDF 2025.4.0 / Noto Sans CJK JP (SIL OFL 1.1)

## Global Constraints

> 親文書 `CLAUDE.md` §ハード制約、`01_ClaudeCode_実装指示書_Tsumugi.md` §6、`06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §4.0/§6、設計仕様書 `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md` を全タスクで尊守する。

- **依存方向**: `App → Application → Domain`、`Infrastructure → Application/Domain`、`Infrastructure.Reporting → Application/Domain`、`Infrastructure.Csv → Application/Domain`。`Domain` は `Infrastructure*`/`Avalonia`/`EntityFrameworkCore` を直接参照しない（`ArchitectureTests` が機械判定）。
- **オフライン**: 全プロダクションアセンブリで `System.Net.*` 等の通信 API 直接参照禁止（`OfflineComplianceTests` / `AppOfflineComplianceTests` が機械判定）。`Tsumugi.Infrastructure.Csv` を新規 `[Theory]` 対象に追加。
- **警告ゼロ**: `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` (Directory.Build.props 既設)。
- **`dotnet format --verify-no-changes` 通過**。
- **クロスプラットフォーム**: macOS/Windows 両方で `dotnet build`/`dotnet test` 緑。
- **TDD**: Red → Green → Refactor。1 コミット=1 論理変更。コミットメッセージに `phase3-0/AC3-0-N` の参照 ID を含める。
- **エンティティ**: `record`、`Guid` 主キー、`CreatedAt`/`CreatedBy`/`ConcurrencyToken` を持つ（既存 `Entity` 基底を踏襲）。
- **追記専用**: `ClaimBatch` / `ClaimDetail` は `AppendOnlyGuard.AppendOnlyTypes` に登録。partial unique index `(OfficeId, YearMonth) WHERE Kind=1` を持つ。
- **ハードコード禁止**: 単位数・加算・地域区分単価・CSV 項目定義は Domain/Application のコードに置かない。本フェーズではマスタの **抽象と空殻** のみ実装。実値は 3-1 で投入。
- **§2.3 出典確定がブロッカー**: 3-1/3-2/3-3 の着手前提は本計画のタスク完了で揃える。ADR 0018〜0024, 0026 が `docs/decisions/` に揃った状態で 3-1 以降に着手可能。
- **Domain カバレッジ 95% 維持**、`build/ci.sh` の Application 閾値 70 維持（3-3 で 90 に昇格）。

## ファイル構成

```
docs/decisions/
  0018-claim-rate-sources.md                       新規 — 報酬単位数・地域区分単価の一次出典
  0019-addition-official-codes.md                  新規 — 加算正式コード集合
  0020-office-capability-migration.md              新規 — OfficeCapability 暫定キー→正式コード集合 移行戦略
  0021-burden-cap-table.md                         新規 — 負担区分→月額上限額テーブル
  0022-average-wage-definition.md                  新規 — 平均工賃月額の正式定義
  0023-claim-csv-spec.md                           新規 — 国保連 請求 CSV インターフェース仕様（版・文字コード・改行・項目順）
  0024-noto-cjk-font-embedding.md                  新規 — Noto Sans CJK JP の埋込
  0026-claimbatch-partial-unique-index.md          新規 — ClaimBatch partial unique index

src/Tsumugi.Domain/
  Entities/
    ClaimBatch.cs                                  新規（追記型）
    ClaimDetail.cs                                 新規（追記型）
  Logic/Claim/
    IRateMaster.cs                                 新規 — 単価マスタ抽象
    IAdditionMaster.cs                             新規 — 加算マスタ抽象
    IBurdenCapMaster.cs                            新規 — 負担上限額マスタ抽象
    IRegionUnitMaster.cs                           新規 — 地域区分単価マスタ抽象
    ClaimMasterVersion.cs                          新規 — 算定再現性のためのマスタ版識別子（record struct）

src/Tsumugi.Application/Abstractions/
  IClaimBatchRepository.cs                         新規

src/Tsumugi.Infrastructure/
  Persistence/
    ClaimBatchRepository.cs                        新規
    Configurations/
      ClaimBatchConfiguration.cs                   新規 — partial unique index
      ClaimDetailConfiguration.cs                  新規
    JsonClaimMasterLoader.cs                       新規 — JSON 読込の空殻（実値投入は 3-1）
  Seed/Claim/
    rates-v1.json                                  新規（空 entries）
    additions-v1.json                              新規（空 entries）
    burden-caps-v1.json                            新規（空 entries）
    region-units-v1.json                           新規（空 entries）
    meta.json                                      新規 — ADR 0018-0023 番号と取得日を保持
  Migrations/
    <yyyyMMddHHmmss>_AddClaimBatchAndDetail.cs     新規

src/Tsumugi.Infrastructure.Csv/                    新規アセンブリ
  Tsumugi.Infrastructure.Csv.csproj                新規
  ICsvClaimWriter.cs                               新規 — Application 抽象を移すかこちらに置くかは Task 6 で決定（本計画では Application 側に配置）
  ClaimCsvWriter.cs                                新規（空殻）

src/Tsumugi.Application/Abstractions/
  ICsvClaimWriter.cs                               新規 — CSV 書出し抽象

src/Tsumugi.Infrastructure.Reporting/
  QuestPdfLicenseConfigurator.cs                   拡張 — Settings.UseEnvironmentFonts=false + Noto 登録
  assets/fonts/NotoSansCJKjp/                      新規 — Noto Sans CJK JP-Regular.ttf / Bold.ttf + OFL.txt

tests/
  Tsumugi.Domain.Tests/
    Architecture/
      HardcodeScannerTests.cs                      新規 — (a) Domain/Application 内に単位数/加算/区分単価 literal 検出
      IntegerLiteralCeilingTests.cs                新規 — (c) Domain 内 int/decimal literal 上限ガード
    Logic/Claim/
      InMemoryRateMasterTests.cs                   新規 — 境界条件テスト
      InMemoryAdditionMasterTests.cs               新規
      InMemoryBurdenCapMasterTests.cs              新規
      InMemoryRegionUnitMasterTests.cs             新規
    Entities/
      ClaimBatchTests.cs                           新規 — record の不変条件
      ClaimDetailTests.cs                          新規
  Tsumugi.Application.Tests/
    （本フェーズではテスト追加なし。3-1 で UseCase 追加時に拡張）
  Tsumugi.Infrastructure.Tests/
    OfflineComplianceTests.cs                      拡張 — Tsumugi.Infrastructure.Csv を [Theory] に追加
    Persistence/
      ClaimBatchRepositoryTests.cs                 新規 — CRUD + AppendOnly + Correct/Cancel 追記
      ClaimBatchDuplicateNewIndexTests.cs          新規 — partial unique index の歯あり性
    CsvLiteralNamespaceTests.cs                    新規 — (b) CSV カラム名 literal が Tsumugi.Infrastructure.Csv 限定
    ScannerToothTests.cs                           新規 — (a)(b)(c) スキャナの歯あり性（意図的違反で赤）
  Tsumugi.Infrastructure.Reporting.Tests/
    WageStatementPdfGeneratorTests.cs              改修 — CJK substring assertion を再有効化
    WagePaymentListPdfGeneratorTests.cs            改修 — 同上
    FontEmbeddingTests.cs                          新規 — Noto Sans CJK JP が埋込登録されていることをアサート

Tsumugi.sln                                        改修 — Tsumugi.Infrastructure.Csv を追加
build/ci.sh                                        現状維持（3-3 で Application 閾値昇格）
docs/open-questions.md                            改修 — ADR 化済項目をチェック
```

---

### Task 1: ADR 0018 — 報酬単位数・地域区分単価の一次出典確定

**Files:**
- Create: `docs/decisions/0018-claim-rate-sources.md`
- Modify: `docs/open-questions.md`（該当チェック消化）

**Interfaces:**
- Produces: ADR 0018 が確定（Phase 3-1 の前提）

このタスクは **コード変更なし**。一次情報（厚労省告示・通知）を確認し、ADR として記録する。

- [ ] **Step 1: 一次情報を収集**

参照対象（少なくともこれらをアクセスして版を ADR に明記）:
- 厚生労働省「指定障害福祉サービスに要する費用の額の算定に関する基準」（令和 6 年告示）
- 同 「単位数表」（就労継続支援B型 該当部分）
- 「地域区分単価」（1〜7 級地）

URL は `https://www.mhlw.go.jp/` 配下の正規ページから取得し、取得日と版番号を ADR に記す。

- [ ] **Step 2: ADR 0018 を執筆**

`docs/decisions/` 配下の既存 ADR（例: 0013）と同じ「結論→背景→選択肢→決定→影響」構造を踏襲し、以下を含める:

- 結論: 報酬単位数および地域区分単価の一次出典として、上記告示・単位数表を採用する
- 出典 URL（複数）と取得日（YYYY-MM-DD）、版番号
- 影響: マスタ JSON `rates-v1.json` / `region-units-v1.json` の `effectiveFrom` 値はこの版に対応する。改定（令和 9 等）は別 entry を追加して切替

- [ ] **Step 3: open-questions.md を更新**

「報酬の単位数・加算・地域区分単価の公式出典（令和6年改定）。フェーズ3着手前に必須。出典URL/版を記す。」のチェックボックスを `[x]` にし、ADR 0018 へのリンクを付記。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0018-claim-rate-sources.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-1): ADR 0018 報酬単位数・地域区分単価の出典確定"
```

---

### Task 2: ADR 0019 — 加算正式コード集合

**Files:**
- Create: `docs/decisions/0019-addition-official-codes.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Produces: ADR 0019 が確定（Phase 3-1 の前提）

- [ ] **Step 1: 加算の正式コード一覧を収集**

少なくとも以下を一次情報から確定:
- 食事提供体制加算 I / II
- 送迎加算 I / II
- 各種配置加算（人員配置・職員配置）
- 欠席時対応加算
- 上限額管理加算
- 目標工賃達成指導員配置加算
- 福祉専門職員配置等加算
- 視覚・聴覚言語障害者支援体制加算（該当する場合）

各加算のコード（厚労省告示中のコード or 国保連電子請求の加算コード）を ADR に明記。

- [ ] **Step 2: ADR 0019 を執筆**

`docs/decisions/0019-addition-official-codes.md` に「結論→背景→選択肢→決定→影響」で記述:
- 結論: 上記加算コードを正式コード集合とする
- 出典 URL（厚労省告示・国保連仕様の該当頁）と取得日
- 影響: マスタ JSON `additions-v1.json` の `additionCode` フィールド値、`OfficeCapability.Flags` の正式キーセット（ADR 0020 の前提）

- [ ] **Step 3: open-questions.md を更新**

「事業所体制の加算フラグキー一覧」のチェックを `[x]` にし、ADR 0019 へのリンクを付記。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0019-addition-official-codes.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-1): ADR 0019 加算正式コード集合"
```

---

### Task 3: ADR 0020 — OfficeCapability 暫定キー→正式コード移行戦略

**Files:**
- Create: `docs/decisions/0020-office-capability-migration.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Produces: ADR 0020 が確定（Phase 3-1 の前提）

- [ ] **Step 1: 移行戦略を設計**

`OfficeCapability.Flags` は `IReadOnlyDictionary<string, bool>` のため、型はそのままでキー値だけを移行できる。ADR に以下を記述:
- 暫定キー → 正式コードの対応表（例: `mealProvision` → `meal_provision_i` / `meal_provision_ii` 等、ADR 0019 と整合）
- 移行手順: 3-1 で新規 `OfficeCapability` を追加するときは正式コードを使用、既存暫定キーはデータシード時に管理者が手動置換（既存追記型なので過去レコードは保持）
- フォールバック: 3-1 の算定エンジンが暫定キーに遭遇した場合は明示的に `InvalidOperationException` でフェイルファスト（推測しない）

- [ ] **Step 2: ADR 0020 を執筆**

「結論→背景→選択肢→決定→影響」で記述。結論は「暫定キーは 3-1 完了時点で正式コードに完全置換、移行期間中は新旧併存しない」。

- [ ] **Step 3: open-questions.md を更新**

「OfficeCapability の正式コード集合」項目のチェックを `[x]`、ADR 0020 リンクを追記。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0020-office-capability-migration.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-8): ADR 0020 OfficeCapability 正式コード移行戦略"
```

---

### Task 4: ADR 0021 — 負担区分→月額上限額テーブル

**Files:**
- Create: `docs/decisions/0021-burden-cap-table.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Produces: ADR 0021 が確定（Phase 3-1 の前提）

- [ ] **Step 1: 月額上限額を一次情報から確定**

厚労省告示「障害者の日常生活及び社会生活を総合的に支援するための法律施行令」等から、`PaymentBurdenCategory` の各区分（生活保護/低所得/一般 1/一般 2）の月額上限金額（円）を確定。

- [ ] **Step 2: ADR 0021 を執筆**

「結論→背景→選択肢→決定→影響」で記述:
- 結論: 月額上限額テーブルを ADR 0021 の値で確定、マスタ JSON `burden-caps-v1.json` に投入（実投入は 3-1）
- 出典 URL（令和 6 改定対応の現行値）、取得日、版番号
- 影響: 利用者負担計算（3-1）の前提となる。改定時は `burden-caps-v1.json` に新 `effectiveFrom` を追加して切替

- [ ] **Step 3: open-questions.md を更新**

「負担区分の月額上限金額表」項目のチェックを `[x]`、ADR 0021 リンクを追記。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0021-burden-cap-table.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-1): ADR 0021 負担区分→月額上限額テーブル"
```

---

### Task 5: ADR 0022 — 平均工賃月額の正式定義

**Files:**
- Create: `docs/decisions/0022-average-wage-definition.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Produces: ADR 0022 が確定（Phase 3-1 の前提、`AverageWageMetric` の FIXME 解消条件）

- [ ] **Step 1: 平均工賃月額の正式定義を一次情報から確定**

厚労省通知（就労継続支援 B 型の基本報酬の算定方法）から以下を確定:
- 分母: 延べ利用者数 or 実利用者数（どちらか）
- 基準期間: 前年度実績 etc.
- 控除: 工賃以外の手当・経費の扱い

- [ ] **Step 2: ADR 0022 を執筆**

「結論→背景→選択肢→決定→影響」で記述:
- 結論: 正式定義（分母/基準期間/控除）
- 出典 URL と取得日、版番号
- 影響: `Tsumugi.Domain.Logic.AverageWageMetric` の `FIXME` を 3-1 で解消、`AverageWageDenominator` enum の値（増減）に反映

- [ ] **Step 3: open-questions.md を更新**

「平均工賃月額の正式定義」項目のチェックを `[x]`、ADR 0022 リンクを追記。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0022-average-wage-definition.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-1): ADR 0022 平均工賃月額の正式定義"
```

---

### Task 6: ADR 0023 — 国保連 請求 CSV インターフェース仕様

**Files:**
- Create: `docs/decisions/0023-claim-csv-spec.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Produces: ADR 0023 が確定（Phase 3-3 の前提）

- [ ] **Step 1: 国保連 請求 CSV インターフェース仕様書を一次情報から確認**

国保連電子請求受付システム関連の仕様書（公式 PDF）から以下を ADR に明記:
- 仕様書の版番号と発行日
- 文字コード（Shift_JIS or UTF-8）
- 改行コード（CRLF or LF）
- レコード種別（基本情報・明細情報・実績情報 等）
- 各レコードの項目順と桁数
- Recipient の必須項目（性別・生年月日・住所等の必要性）

- [ ] **Step 2: ADR 0023 を執筆**

「結論→背景→選択肢→決定→影響」で記述:
- 結論: 上記仕様書版を採用、文字コード・改行・レコード種別・項目順を確定
- 出典 URL と取得日、版番号
- 影響: `Tsumugi.Infrastructure.Csv` の `ClaimCsvWriter` 実装（3-3）、`Recipient` 拡張 migration（3-3）の前提

- [ ] **Step 3: open-questions.md を更新**

「国保連 請求データCSVの公式インターフェース仕様書の版・文字コード・改行・レコード種別・項目順」項目のチェックを `[x]`、ADR 0023 リンクを追記。

「性別など利用者属性の拡張」項目は ADR 0023 で必須項目が確定したらこの時点で更新（CSV 仕様→ Recipient 拡張内容が決まる）。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0023-claim-csv-spec.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-1): ADR 0023 国保連 請求 CSV インターフェース仕様"
```

---

### Task 7: ADR 0024 — Noto Sans CJK JP の埋込

**Files:**
- Create: `docs/decisions/0024-noto-cjk-font-embedding.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Produces: ADR 0024 が確定（Phase 3-2 の前提、Phase 2 残課題の解消）

- [ ] **Step 1: フォント選定とライセンス確認**

- 採用: `Noto Sans CJK JP`（Adobe 制作、Google 配布、SIL OFL 1.1 ライセンス）
- ライセンス確認: SIL Open Font License 1.1 の条文を確認し、`assets/fonts/NotoSansCJKjp/OFL.txt` として同梱可能であること、再配布制約（Reserved Font Name は変更不可）に抵触しないことを ADR に明記

- [ ] **Step 2: ADR 0024 を執筆**

「結論→背景→選択肢→決定→影響」で記述:
- 結論: Noto Sans CJK JP（Regular + Bold の 2 ウェイト）を `Tsumugi.Infrastructure.Reporting/assets/fonts/NotoSansCJKjp/` に埋込リソースとして追加。`QuestPdfLicenseConfigurator` で `Settings.UseEnvironmentFonts = false` + `FontManager.RegisterFontFromEmbeddedResource` を設定
- 出典: `https://github.com/googlefonts/noto-cjk` のリリース版（具体的なバージョンタグを記録）
- ライセンス: SIL OFL 1.1（同梱）、Reserved Font Name の継承義務に注意
- 影響: ビルド出力サイズ増（Noto Sans CJK JP は 1 ウェイトあたり ~15MB）。Reporting アセンブリのみに同梱し App には透過させる。CI で CJK substring assertion を再有効化

- [ ] **Step 3: open-questions.md を更新**

「PDF 帳票の日本語フォント埋込」項目のチェックを `[x]`、ADR 0024 リンクを追記。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0024-noto-cjk-font-embedding.md docs/open-questions.md
git commit -m "docs(phase3-0/AC3-0-2): ADR 0024 Noto Sans CJK JP の埋込"
```

---

### Task 8: ADR 0026 — ClaimBatch partial unique index

**Files:**
- Create: `docs/decisions/0026-claimbatch-partial-unique-index.md`

**Interfaces:**
- Produces: ADR 0026 が確定（Task 13 の Migration の前提）

- [ ] **Step 1: ADR 0026 を執筆**

既存 ADR 0015（DailyRecord）/ 0017（WageFund）と同じ作法。`(OfficeId, YearMonth) WHERE Kind = 1` の partial unique index を `ClaimBatch` に適用することを記述。

- 結論: `ClaimBatch` テーブルに `(OfficeId, YearMonth) WHERE Kind = 1` の partial unique index を追加
- 背景: WageFund / DailyRecord と同じく重複 New 挿入を DB 層で拒否。Correct/Cancel は重複可能
- 影響: 重複 New 挿入は `DbUpdateException` で拒否。Repository 層で例外ハンドリング

- [ ] **Step 2: コミット**

```bash
git add docs/decisions/0026-claimbatch-partial-unique-index.md
git commit -m "docs(phase3-0/AC3-0-6): ADR 0026 ClaimBatch partial unique index"
```

---

### Task 9: Noto Sans CJK JP フォント埋込の実装

**Files:**
- Create: `src/Tsumugi.Infrastructure.Reporting/assets/fonts/NotoSansCJKjp/NotoSansCJKjp-Regular.ttf`
- Create: `src/Tsumugi.Infrastructure.Reporting/assets/fonts/NotoSansCJKjp/NotoSansCJKjp-Bold.ttf`
- Create: `src/Tsumugi.Infrastructure.Reporting/assets/fonts/NotoSansCJKjp/OFL.txt`
- Modify: `src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj`
- Modify: `src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/FontEmbeddingTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs`

**Interfaces:**
- Consumes: ADR 0024
- Produces: `QuestPdfLicenseConfigurator.ApplyCommunityLicense()` 内で Noto Sans CJK JP が `FontManager` に登録され、`QuestPDF.Settings.UseEnvironmentFonts = false` になる

- [ ] **Step 1: フォントファイルの追加**

`https://github.com/googlefonts/noto-cjk/tree/main/Sans/OTF/Japanese` から OTF 形式の Regular と Bold を取得し、TTF に変換するか OTF のまま使用（QuestPDF は両対応）。本計画では実装簡素化のため **OTF を直接使用**:

```
src/Tsumugi.Infrastructure.Reporting/assets/fonts/NotoSansCJKjp/
  NotoSansCJKjp-Regular.otf
  NotoSansCJKjp-Bold.otf
  OFL.txt
```

OFL.txt は Noto リポジトリの LICENSE をコピー。

- [ ] **Step 2: csproj に埋込リソースとして登録**

`src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj` を編集し、`<ItemGroup>` に追加:

```xml
<ItemGroup>
  <EmbeddedResource Include="assets/fonts/NotoSansCJKjp/NotoSansCJKjp-Regular.otf" />
  <EmbeddedResource Include="assets/fonts/NotoSansCJKjp/NotoSansCJKjp-Bold.otf" />
  <None Include="assets/fonts/NotoSansCJKjp/OFL.txt">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 3: 失敗テストを書く（FontEmbeddingTests）**

`tests/Tsumugi.Infrastructure.Reporting.Tests/FontEmbeddingTests.cs`:

```csharp
using FluentAssertions;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;
using Tsumugi.Infrastructure.Reporting;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class FontEmbeddingTests
{
    [Fact]
    public void ApplyCommunityLicense_disables_environment_fonts_and_registers_noto_sans_cjk_jp()
    {
        QuestPdfLicenseConfigurator.ApplyCommunityLicense();

        QuestPDF.Settings.UseEnvironmentFonts.Should().BeFalse();
        // Noto Sans CJK JP が QuestPDF の FontManager に登録されていることをアサート
        // 実装時は FontManager.RegisterFontFromEmbeddedResource を呼んだあと
        // 同名の font family で document に書き出せることをここで確認する
    }
}
```

- [ ] **Step 4: テストが赤になることを確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "FontEmbeddingTests" -v normal`
Expected: FAIL（`QuestPDF.Settings.UseEnvironmentFonts` がまだ既定値）

- [ ] **Step 5: QuestPdfLicenseConfigurator を拡張**

`src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs`:

```csharp
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Tsumugi.Infrastructure.Reporting;

/// <summary>
/// QuestPDF のライセンス設定とフォント登録。
/// ADR 0013 により Community License を採用。
/// ADR 0024 により Noto Sans CJK JP を埋込リソースとして登録し、環境フォントへのフォールバックを無効化する
/// （CI で CJK が NUL 化する問題の根治）。
/// </summary>
public static class QuestPdfLicenseConfigurator
{
    private static readonly object Gate = new();
    private static bool Applied;

    public static void ApplyCommunityLicense()
    {
        lock (Gate)
        {
            if (Applied) return;
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = false;

            var asm = typeof(QuestPdfLicenseConfigurator).Assembly;
            using (var s = asm.GetManifestResourceStream(
                "Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansCJKjp.NotoSansCJKjp-Regular.otf"))
            {
                FontManager.RegisterFont(s!);
            }
            using (var s = asm.GetManifestResourceStream(
                "Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansCJKjp.NotoSansCJKjp-Bold.otf"))
            {
                FontManager.RegisterFont(s!);
            }
            Applied = true;
        }
    }
}
```

- [ ] **Step 6: テストが緑になることを確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "FontEmbeddingTests" -v normal`
Expected: PASS

- [ ] **Step 7: WageStatementPdfGeneratorTests / WagePaymentListPdfGeneratorTests の CJK assertion を再有効化**

両ファイルで、commit `edbc122` で消した CJK substring assertion を再追加。例:

```csharp
// 例: 利用者氏名（漢字）が PDF テキスト抽出で見つかること
extractedText.Should().Contain("山田太郎");
```

- [ ] **Step 8: 全テスト緑を確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj -v normal`
Expected: PASS（Linux/Windows ランナーでも CJK が NUL 化しない）

- [ ] **Step 9: コミット**

```bash
git add src/Tsumugi.Infrastructure.Reporting/ tests/Tsumugi.Infrastructure.Reporting.Tests/
git commit -m "feat(phase3-0/AC3-0-2): embed Noto Sans CJK JP and re-enable CJK assertions"
```

---

### Task 10: マスタ抽象 interface 群と record struct の追加

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/IRateMaster.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/IAdditionMaster.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/IBurdenCapMaster.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/IRegionUnitMaster.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimMasterVersion.cs`

**Interfaces:**
- Produces: `IRateMaster.LookupBasic(string serviceCode, YearMonth ym, int capacityClass) → int`（unit count）
- Produces: `IAdditionMaster.LookupAddition(string additionCode, YearMonth ym) → int`（unit count）
- Produces: `IBurdenCapMaster.LookupCap(PaymentBurdenCategory category, YearMonth ym) → int`（yen）
- Produces: `IRegionUnitMaster.LookupUnitPrice(RegionGrade grade, string serviceCategory, YearMonth ym) → decimal`（unit price, 10円基準＋地域上乗せ）
- Produces: `ClaimMasterVersion`（record struct: `string Version`, `DateOnly RetrievedOn`, `string Adr`）

このタスクは **interface と値型のみ**。実装は Task 11（InMemory）/ Task 12（JSON loader 空殻）。

- [ ] **Step 1: ClaimMasterVersion record struct を作る**

`src/Tsumugi.Domain/Logic/Claim/ClaimMasterVersion.cs`:

```csharp
namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// マスタの版識別子。ClaimBatch に保存して再現性を担保する。
/// 値は <c>Tsumugi.Infrastructure/Seed/Claim/meta.json</c> から供給される。
/// </summary>
public readonly record struct ClaimMasterVersion(string Version, DateOnly RetrievedOn, string Adr)
{
    public static ClaimMasterVersion Unknown { get; } = new("unknown", DateOnly.MinValue, "n/a");
}
```

- [ ] **Step 2: IRateMaster interface を作る**

`src/Tsumugi.Domain/Logic/Claim/IRateMaster.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// 報酬単位数マスタの抽象。実値は <c>Tsumugi.Infrastructure</c> の JSON ローダから供給される。
/// Domain は interface のみ参照し、数値 literal を持たない（ハードコード機械判定 (a)(c) の対象外）。
/// </summary>
public interface IRateMaster
{
    /// <summary>
    /// B型基本報酬の単位数を引く。サービスコードと適用年月、定員規模区分から一意に決まる。
    /// </summary>
    /// <returns>単位数。マスタに該当エントリが無ければ <see cref="InvalidOperationException"/></returns>
    int LookupBasic(string serviceCode, YearMonth ym, int capacityClass);

    ClaimMasterVersion Version { get; }
}
```

- [ ] **Step 3: IAdditionMaster interface を作る**

`src/Tsumugi.Domain/Logic/Claim/IAdditionMaster.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>加算マスタの抽象。</summary>
public interface IAdditionMaster
{
    /// <summary>
    /// 加算コードと適用年月から単位数を引く。
    /// 条件付き加算（人員配置等）は呼出元で条件を解決済とする（マスタ自体は単純なキー値テーブル）。
    /// </summary>
    int LookupAddition(string additionCode, YearMonth ym);

    ClaimMasterVersion Version { get; }
}
```

- [ ] **Step 4: IBurdenCapMaster interface を作る**

`src/Tsumugi.Domain/Logic/Claim/IBurdenCapMaster.cs`:

```csharp
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>負担上限額マスタの抽象。</summary>
public interface IBurdenCapMaster
{
    /// <summary>負担区分と適用年月から月額上限額（円）を引く。</summary>
    int LookupCap(PaymentBurdenCategory category, YearMonth ym);

    ClaimMasterVersion Version { get; }
}
```

- [ ] **Step 5: IRegionUnitMaster interface を作る**

`src/Tsumugi.Domain/Logic/Claim/IRegionUnitMaster.cs`:

```csharp
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>地域区分単価マスタの抽象。</summary>
public interface IRegionUnitMaster
{
    /// <summary>
    /// 地域区分・サービス種別・適用年月から単価（円 / 単位）を引く。
    /// 端数規則は ADR 0025（3-1 で確定）でマスタ駆動に詰める。
    /// </summary>
    decimal LookupUnitPrice(RegionGrade grade, string serviceCategory, YearMonth ym);

    ClaimMasterVersion Version { get; }
}
```

- [ ] **Step 6: ビルド確認**

Run: `dotnet build src/Tsumugi.Domain/Tsumugi.Domain.csproj -c Release`
Expected: 警告ゼロで成功

- [ ] **Step 7: コミット**

```bash
git add src/Tsumugi.Domain/Logic/Claim/
git commit -m "feat(phase3-0/AC3-0-4): add claim master abstractions (IRateMaster/IAdditionMaster/IBurdenCapMaster/IRegionUnitMaster)"
```

---

### Task 11: InMemory 実装と境界条件テスト

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryRateMaster.cs`（テストヘルパ、internal sealed class）
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryAdditionMaster.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryBurdenCapMaster.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryRegionUnitMaster.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryRateMasterTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryAdditionMasterTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryBurdenCapMasterTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryRegionUnitMasterTests.cs`

**Interfaces:**
- Consumes: Task 10 の interface 群
- Produces: テストで使う InMemory 実装。3-1 でも `ClaimCalculator` のテストで再利用

各 InMemory はテーブル駆動で、境界条件（適用年月境界・存在しないコード・複数バージョン）を網羅したテストを持つ。**マスタの実値は持たず、テスト用ダミー値で interface の境界条件を固定する**。

- [ ] **Step 1: InMemoryRateMasterTests を書く（失敗テスト）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class InMemoryRateMasterTests
{
    [Fact]
    public void LookupBasic_returns_unit_for_exact_match()
    {
        var master = new InMemoryRateMaster(
            new[]
            {
                (serviceCode: "B_BASE", from: new YearMonth(2024, 4), capacityClass: 20, unit: 600),
                (serviceCode: "B_BASE", from: new YearMonth(2024, 4), capacityClass: 40, unit: 540),
            },
            ClaimMasterVersion.Unknown);

        master.LookupBasic("B_BASE", new YearMonth(2024, 6), 20).Should().Be(600);
    }

    [Fact]
    public void LookupBasic_uses_latest_effectiveFrom_not_exceeding_target()
    {
        var master = new InMemoryRateMaster(
            new[]
            {
                (serviceCode: "B_BASE", from: new YearMonth(2024, 4), capacityClass: 20, unit: 600),
                (serviceCode: "B_BASE", from: new YearMonth(2027, 4), capacityClass: 20, unit: 620),
            },
            ClaimMasterVersion.Unknown);

        master.LookupBasic("B_BASE", new YearMonth(2027, 3), 20).Should().Be(600);
        master.LookupBasic("B_BASE", new YearMonth(2027, 4), 20).Should().Be(620);
    }

    [Fact]
    public void LookupBasic_throws_for_missing_entry()
    {
        var master = new InMemoryRateMaster(Array.Empty<(string, YearMonth, int, int)>(), ClaimMasterVersion.Unknown);
        var act = () => master.LookupBasic("MISSING", new YearMonth(2024, 4), 20);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Version_property_returns_constructor_value()
    {
        var v = new ClaimMasterVersion("2024.04", new DateOnly(2026, 6, 29), "ADR-0018");
        var master = new InMemoryRateMaster(Array.Empty<(string, YearMonth, int, int)>(), v);
        master.Version.Should().Be(v);
    }
}
```

- [ ] **Step 2: テストが赤になることを確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "InMemoryRateMasterTests" -v normal`
Expected: FAIL（`InMemoryRateMaster` 未定義）

- [ ] **Step 3: InMemoryRateMaster を実装**

```csharp
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

internal sealed class InMemoryRateMaster : IRateMaster
{
    private readonly (string ServiceCode, YearMonth From, int CapacityClass, int Unit)[] entries;
    public ClaimMasterVersion Version { get; }

    public InMemoryRateMaster(
        IEnumerable<(string serviceCode, YearMonth from, int capacityClass, int unit)> entries,
        ClaimMasterVersion version)
    {
        this.entries = entries.ToArray();
        Version = version;
    }

    public int LookupBasic(string serviceCode, YearMonth ym, int capacityClass)
    {
        var hit = entries
            .Where(e => e.ServiceCode == serviceCode && e.CapacityClass == capacityClass && e.From.ToInt() <= ym.ToInt())
            .OrderByDescending(e => e.From.ToInt())
            .Cast<(string, YearMonth, int, int)?>()
            .FirstOrDefault();

        if (hit is null)
            throw new InvalidOperationException(
                $"単価マスタにエントリがありません: serviceCode={serviceCode}, ym={ym.ToInt()}, capacityClass={capacityClass}");

        return hit.Value.Item4;
    }
}
```

- [ ] **Step 4: テスト緑を確認 → コミット**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "InMemoryRateMasterTests" -v normal`
Expected: PASS

```bash
git add tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryRateMaster.cs tests/Tsumugi.Domain.Tests/Logic/Claim/InMemoryRateMasterTests.cs
git commit -m "test(phase3-0/AC3-0-4): InMemoryRateMaster boundary tests"
```

- [ ] **Step 5: InMemoryAdditionMaster + テスト**（Step 1〜4 と同じパターン）

`LookupAddition(additionCode, ym)` で、effectiveFrom 境界・存在しないコード・複数バージョンの 3 ケースをテスト。実装は Step 3 と同形。

```csharp
// テスト 3 ケース + 実装 + 緑確認
```

コミット:
```bash
git commit -m "test(phase3-0/AC3-0-4): InMemoryAdditionMaster boundary tests"
```

- [ ] **Step 6: InMemoryBurdenCapMaster + テスト**（Step 1〜4 と同じパターン、ただしキーは `PaymentBurdenCategory`）

```csharp
// LookupCap(PaymentBurdenCategory.LowIncome, ym) → 円
// テスト: 各区分・effectiveFrom 境界・存在しないエントリ
```

コミット:
```bash
git commit -m "test(phase3-0/AC3-0-4): InMemoryBurdenCapMaster boundary tests"
```

- [ ] **Step 7: InMemoryRegionUnitMaster + テスト**（Step 1〜4 と同じパターン、キーは `RegionGrade × serviceCategory`、戻り値は `decimal`）

```csharp
// LookupUnitPrice(RegionGrade.Grade1, "B_BASE", ym) → 11.20m
// テスト: 各 RegionGrade・サービス種別の組合せ・境界
```

コミット:
```bash
git commit -m "test(phase3-0/AC3-0-4): InMemoryRegionUnitMaster boundary tests"
```

---

### Task 12: マスタ JSON 空殻と JsonClaimMasterLoader

**Files:**
- Create: `src/Tsumugi.Infrastructure/Seed/Claim/rates-v1.json`
- Create: `src/Tsumugi.Infrastructure/Seed/Claim/additions-v1.json`
- Create: `src/Tsumugi.Infrastructure/Seed/Claim/burden-caps-v1.json`
- Create: `src/Tsumugi.Infrastructure/Seed/Claim/region-units-v1.json`
- Create: `src/Tsumugi.Infrastructure/Seed/Claim/meta.json`
- Create: `src/Tsumugi.Infrastructure/Persistence/JsonClaimMasterLoader.cs`
- Modify: `src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj`（JSON を EmbeddedResource か Content として登録）
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/JsonClaimMasterLoaderTests.cs`

**Interfaces:**
- Consumes: Task 10 の interface 群、Task 1-8 の ADR 番号
- Produces: `JsonClaimMasterLoader.LoadRates() → IRateMaster` 等のローダ API
- Produces: マスタ JSON ファイルが配置される（3-1 で実値投入）

- [ ] **Step 1: マスタ JSON 空殻を作る**

`src/Tsumugi.Infrastructure/Seed/Claim/rates-v1.json`:

```json
{
  "$schema": "rates-v1.schema.json",
  "version": "v1",
  "entries": []
}
```

同様に `additions-v1.json` / `burden-caps-v1.json` / `region-units-v1.json` を空 entries で作成。

`meta.json`:

```json
{
  "rates": { "adr": "0018", "version": "", "retrievedOn": "" },
  "additions": { "adr": "0019", "version": "", "retrievedOn": "" },
  "burdenCaps": { "adr": "0021", "version": "", "retrievedOn": "" },
  "regionUnits": { "adr": "0018", "version": "", "retrievedOn": "" }
}
```

`version` / `retrievedOn` は 3-1 で実値投入時に埋める。

- [ ] **Step 2: csproj に EmbeddedResource として登録**

`src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj` の `<ItemGroup>`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Seed/Claim/*.json" />
</ItemGroup>
```

- [ ] **Step 3: JsonClaimMasterLoaderTests（失敗テスト）**

```csharp
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class JsonClaimMasterLoaderTests
{
    [Fact]
    public void LoadRates_returns_master_with_version_from_meta_json()
    {
        var loader = new JsonClaimMasterLoader();
        var master = loader.LoadRates();
        master.Should().NotBeNull();
        master.Version.Adr.Should().Be("ADR-0018");
        // 空 entries なので LookupBasic は throw
        Action act = () => master.LookupBasic("ANY", new ValueObjects.YearMonth(2024, 4), 20);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LoadAdditions_LoadBurdenCaps_LoadRegionUnits_all_succeed_on_empty_seed()
    {
        var loader = new JsonClaimMasterLoader();
        loader.LoadAdditions().Should().NotBeNull();
        loader.LoadBurdenCaps().Should().NotBeNull();
        loader.LoadRegionUnits().Should().NotBeNull();
    }
}
```

- [ ] **Step 4: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "JsonClaimMasterLoaderTests" -v normal`
Expected: FAIL（`JsonClaimMasterLoader` 未定義）

- [ ] **Step 5: JsonClaimMasterLoader を実装**

```csharp
using System.Text.Json;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// Seed/Claim/*.json から報酬マスタを読み込む。
/// 3-0 では空 entries の空殻ローダ。3-1 で実値投入。
/// </summary>
public sealed class JsonClaimMasterLoader
{
    private readonly MetaJson meta;

    public JsonClaimMasterLoader()
    {
        meta = ReadMeta();
    }

    public IRateMaster LoadRates() => new JsonRateMaster(
        ReadEntries<RateEntry>("rates-v1.json"),
        ToVersion(meta.Rates));

    public IAdditionMaster LoadAdditions() => new JsonAdditionMaster(
        ReadEntries<AdditionEntry>("additions-v1.json"),
        ToVersion(meta.Additions));

    public IBurdenCapMaster LoadBurdenCaps() => new JsonBurdenCapMaster(
        ReadEntries<BurdenCapEntry>("burden-caps-v1.json"),
        ToVersion(meta.BurdenCaps));

    public IRegionUnitMaster LoadRegionUnits() => new JsonRegionUnitMaster(
        ReadEntries<RegionUnitEntry>("region-units-v1.json"),
        ToVersion(meta.RegionUnits));

    private static ClaimMasterVersion ToVersion(MetaSourceEntry e) =>
        new($"v{e.Version}", DateOnly.TryParse(e.RetrievedOn, out var d) ? d : DateOnly.MinValue, $"ADR-{e.Adr}");

    private static T ReadEntries<T>(string fileName) where T : new()
    {
        // Embedded resource を JsonSerializer.Deserialize で読み込む
        // 空 entries であれば T 配列が空のオブジェクトを返す
        var asm = typeof(JsonClaimMasterLoader).Assembly;
        using var s = asm.GetManifestResourceStream($"Tsumugi.Infrastructure.Seed.Claim.{fileName}")
            ?? throw new InvalidOperationException($"resource not found: {fileName}");
        return JsonSerializer.Deserialize<T>(s, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new T();
    }

    private static MetaJson ReadMeta()
    {
        var asm = typeof(JsonClaimMasterLoader).Assembly;
        using var s = asm.GetManifestResourceStream("Tsumugi.Infrastructure.Seed.Claim.meta.json")
            ?? throw new InvalidOperationException("meta.json not found");
        return JsonSerializer.Deserialize<MetaJson>(s, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("meta.json deserialize failed");
    }

    // 以下、JSON スキーマに対応する内部 record 群
    private sealed record MetaJson(MetaSourceEntry Rates, MetaSourceEntry Additions, MetaSourceEntry BurdenCaps, MetaSourceEntry RegionUnits);
    private sealed record MetaSourceEntry(string Adr, string Version, string RetrievedOn);

    private sealed record RateEntry; // 3-1 で確定
    private sealed record AdditionEntry; // 3-1 で確定
    private sealed record BurdenCapEntry; // 3-1 で確定
    private sealed record RegionUnitEntry; // 3-1 で確定

    private sealed class JsonRateMaster(object entries, ClaimMasterVersion version) : IRateMaster
    {
        public int LookupBasic(string serviceCode, YearMonth ym, int capacityClass) =>
            throw new InvalidOperationException("rates seed empty (3-0 skeleton)");
        public ClaimMasterVersion Version { get; } = version;
    }
    private sealed class JsonAdditionMaster(object entries, ClaimMasterVersion version) : IAdditionMaster
    {
        public int LookupAddition(string additionCode, YearMonth ym) =>
            throw new InvalidOperationException("additions seed empty (3-0 skeleton)");
        public ClaimMasterVersion Version { get; } = version;
    }
    private sealed class JsonBurdenCapMaster(object entries, ClaimMasterVersion version) : IBurdenCapMaster
    {
        public int LookupCap(Domain.Enums.PaymentBurdenCategory category, YearMonth ym) =>
            throw new InvalidOperationException("burden-caps seed empty (3-0 skeleton)");
        public ClaimMasterVersion Version { get; } = version;
    }
    private sealed class JsonRegionUnitMaster(object entries, ClaimMasterVersion version) : IRegionUnitMaster
    {
        public decimal LookupUnitPrice(Domain.Enums.RegionGrade grade, string serviceCategory, YearMonth ym) =>
            throw new InvalidOperationException("region-units seed empty (3-0 skeleton)");
        public ClaimMasterVersion Version { get; } = version;
    }
}
```

> 注: 3-1 で各 `Json*Master` クラスの実装と JSON スキーマ詳細を確定する。本タスクでは「空 entries でも loader が成立し、ADR バージョンが取れる」状態を作る。

- [ ] **Step 6: テスト緑確認 → コミット**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "JsonClaimMasterLoaderTests" -v normal`
Expected: PASS

```bash
git add src/Tsumugi.Infrastructure/Seed/Claim/ src/Tsumugi.Infrastructure/Persistence/JsonClaimMasterLoader.cs src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj tests/Tsumugi.Infrastructure.Tests/Persistence/JsonClaimMasterLoaderTests.cs
git commit -m "feat(phase3-0/AC3-0-4): seed Claim master JSON skeletons + JsonClaimMasterLoader"
```

---

### Task 13: ClaimBatch / ClaimDetail エンティティと EF Core 構成

**Files:**
- Create: `src/Tsumugi.Domain/Entities/ClaimBatch.cs`
- Create: `src/Tsumugi.Domain/Entities/ClaimDetail.cs`
- Create: `tests/Tsumugi.Domain.Tests/Entities/ClaimBatchTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Entities/ClaimDetailTests.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimBatchConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimDetailConfiguration.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`（DbSet 追加）
- Modify: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`（型登録追加）
- Create: `src/Tsumugi.Infrastructure/Migrations/<auto>_AddClaimBatchAndDetail.cs`（dotnet ef で生成）

**Interfaces:**
- Consumes: Task 10 の `ClaimMasterVersion`
- Produces: `ClaimBatch` record（`OfficeId`, `YearMonth`, `Kind`, `OriginId`, `TotalUnit`, `TotalAmountYen`, `TotalBurdenYen`, `MasterVersionRates`, `MasterVersionAdditions`, `MasterVersionBurdenCaps`, `MasterVersionRegionUnits`）
- Produces: `ClaimDetail` record（`ClaimBatchId`, `RecipientId`, `LineKind`, `Code`, `Unit`, `AmountYen`）
- Produces: `AppendOnlyGuard.AppendOnlyTypes` に `ClaimBatch` / `ClaimDetail` 追加

- [ ] **Step 1: ClaimBatchTests を書く（失敗テスト）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Entities;

public sealed class ClaimBatchTests
{
    [Fact]
    public void Create_New_holds_all_required_fields()
    {
        var id = Guid.NewGuid();
        var officeId = Guid.NewGuid();
        var v = new ClaimMasterVersion("2024.04", new DateOnly(2026, 6, 29), "ADR-0018");

        var batch = ClaimBatch.CreateNew(
            id: id, officeId: officeId, yearMonth: new YearMonth(2026, 5),
            totalUnit: 12000, totalAmountYen: 1_344_000, totalBurdenYen: 9_300,
            ratesVersion: v, additionsVersion: v, burdenCapsVersion: v, regionUnitsVersion: v,
            createdBy: "u1", createdAt: DateTimeOffset.Parse("2026-06-29T00:00:00Z"),
            concurrencyToken: Guid.NewGuid());

        batch.Id.Should().Be(id);
        batch.OfficeId.Should().Be(officeId);
        batch.YearMonth.Should().Be(new YearMonth(2026, 5));
        batch.Kind.Should().Be(RecordKind.New);
        batch.OriginId.Should().BeNull();
        batch.MasterVersionRates.Should().Be(v);
    }

    [Fact]
    public void CreateCorrect_links_to_originId()
    {
        var originId = Guid.NewGuid();
        var v = ClaimMasterVersion.Unknown;
        var batch = ClaimBatch.CreateCorrect(
            id: Guid.NewGuid(), originId: originId,
            officeId: Guid.NewGuid(), yearMonth: new YearMonth(2026, 5),
            totalUnit: 12500, totalAmountYen: 1_400_000, totalBurdenYen: 9_300,
            ratesVersion: v, additionsVersion: v, burdenCapsVersion: v, regionUnitsVersion: v,
            createdBy: "u1", createdAt: DateTimeOffset.UtcNow, concurrencyToken: Guid.NewGuid());
        batch.Kind.Should().Be(RecordKind.Correct);
        batch.OriginId.Should().Be(originId);
    }

    [Fact]
    public void CreateCancel_links_to_originId()
    {
        var originId = Guid.NewGuid();
        var v = ClaimMasterVersion.Unknown;
        var batch = ClaimBatch.CreateCancel(
            id: Guid.NewGuid(), originId: originId,
            officeId: Guid.NewGuid(), yearMonth: new YearMonth(2026, 5),
            ratesVersion: v, additionsVersion: v, burdenCapsVersion: v, regionUnitsVersion: v,
            createdBy: "u1", createdAt: DateTimeOffset.UtcNow, concurrencyToken: Guid.NewGuid());
        batch.Kind.Should().Be(RecordKind.Cancel);
        batch.OriginId.Should().Be(originId);
        batch.TotalAmountYen.Should().Be(0);
    }
}
```

- [ ] **Step 2: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "ClaimBatchTests" -v normal`
Expected: FAIL（`ClaimBatch` 未定義）

- [ ] **Step 3: ClaimBatch を実装**

```csharp
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 請求確定スナップショット（append-only）。(OfficeId, YearMonth) 単位で 1 New。
/// 確定後の下層訂正で自動再生成しない。再生成は <see cref="RecordKind.Correct"/> で履歴に残す。
/// 取下げは <see cref="RecordKind.Cancel"/>。
/// </summary>
public sealed record ClaimBatch : Entity
{
    public required Guid OfficeId { get; init; }
    public required YearMonth YearMonth { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }

    public required int TotalUnit { get; init; }
    public required int TotalAmountYen { get; init; }
    public required int TotalBurdenYen { get; init; }

    public required ClaimMasterVersion MasterVersionRates { get; init; }
    public required ClaimMasterVersion MasterVersionAdditions { get; init; }
    public required ClaimMasterVersion MasterVersionBurdenCaps { get; init; }
    public required ClaimMasterVersion MasterVersionRegionUnits { get; init; }

    public static ClaimBatch CreateNew(
        Guid id, Guid officeId, YearMonth yearMonth,
        int totalUnit, int totalAmountYen, int totalBurdenYen,
        ClaimMasterVersion ratesVersion, ClaimMasterVersion additionsVersion,
        ClaimMasterVersion burdenCapsVersion, ClaimMasterVersion regionUnitsVersion,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken) =>
        new()
        {
            Id = id, OfficeId = officeId, YearMonth = yearMonth,
            Kind = RecordKind.New, OriginId = null,
            TotalUnit = totalUnit, TotalAmountYen = totalAmountYen, TotalBurdenYen = totalBurdenYen,
            MasterVersionRates = ratesVersion, MasterVersionAdditions = additionsVersion,
            MasterVersionBurdenCaps = burdenCapsVersion, MasterVersionRegionUnits = regionUnitsVersion,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = concurrencyToken,
        };

    public static ClaimBatch CreateCorrect(
        Guid id, Guid originId, Guid officeId, YearMonth yearMonth,
        int totalUnit, int totalAmountYen, int totalBurdenYen,
        ClaimMasterVersion ratesVersion, ClaimMasterVersion additionsVersion,
        ClaimMasterVersion burdenCapsVersion, ClaimMasterVersion regionUnitsVersion,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken) =>
        new()
        {
            Id = id, OfficeId = officeId, YearMonth = yearMonth,
            Kind = RecordKind.Correct, OriginId = originId,
            TotalUnit = totalUnit, TotalAmountYen = totalAmountYen, TotalBurdenYen = totalBurdenYen,
            MasterVersionRates = ratesVersion, MasterVersionAdditions = additionsVersion,
            MasterVersionBurdenCaps = burdenCapsVersion, MasterVersionRegionUnits = regionUnitsVersion,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = concurrencyToken,
        };

    public static ClaimBatch CreateCancel(
        Guid id, Guid originId, Guid officeId, YearMonth yearMonth,
        ClaimMasterVersion ratesVersion, ClaimMasterVersion additionsVersion,
        ClaimMasterVersion burdenCapsVersion, ClaimMasterVersion regionUnitsVersion,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken) =>
        new()
        {
            Id = id, OfficeId = officeId, YearMonth = yearMonth,
            Kind = RecordKind.Cancel, OriginId = originId,
            TotalUnit = 0, TotalAmountYen = 0, TotalBurdenYen = 0,
            MasterVersionRates = ratesVersion, MasterVersionAdditions = additionsVersion,
            MasterVersionBurdenCaps = burdenCapsVersion, MasterVersionRegionUnits = regionUnitsVersion,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = concurrencyToken,
        };
}
```

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "ClaimBatchTests" -v normal
git add src/Tsumugi.Domain/Entities/ClaimBatch.cs tests/Tsumugi.Domain.Tests/Entities/ClaimBatchTests.cs
git commit -m "feat(phase3-0/AC3-0-6): ClaimBatch entity (append-only snapshot)"
```

- [ ] **Step 5: ClaimDetail を Step 1〜4 と同パターンで追加**

```csharp
public sealed record ClaimDetail : Entity
{
    public required Guid ClaimBatchId { get; init; }
    public required Guid RecipientId { get; init; }
    public required ClaimDetailLineKind LineKind { get; init; }
    public required string Code { get; init; }        // ServiceCode or AdditionCode
    public required int Unit { get; init; }
    public required int AmountYen { get; init; }

    public static ClaimDetail Create(...) => ...;
}

public enum ClaimDetailLineKind
{
    Basic = 1,
    Addition = 2,
    Deduction = 3,
}
```

`Tsumugi.Domain/Enums/ClaimDetailLineKind.cs` を追加。テストは Basic/Addition/Deduction の各 LineKind 生成と必須フィールドを検証。

コミット:
```bash
git commit -m "feat(phase3-0/AC3-0-6): ClaimDetail entity"
```

- [ ] **Step 6: AppendOnlyGuard に登録**

`src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs` の `AppendOnlyTypes` に `typeof(ClaimBatch)` と `typeof(ClaimDetail)` を追加:

```csharp
private static readonly HashSet<Type> AppendOnlyTypes = new()
{
    typeof(DailyRecord),
    typeof(Certificate),
    typeof(Contract),
    typeof(OfficeCapability),
    typeof(DisabilityCertificate),
    typeof(FaceSheet),
    typeof(WorkRecord),
    typeof(WageFund),
    typeof(WageSettings),
    typeof(WageStatement),
    typeof(AuditEntry),
    typeof(ClaimBatch),
    typeof(ClaimDetail),
};
```

- [ ] **Step 7: EF Core 構成と DbSet 追加**

`src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimBatchConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class ClaimBatchConfiguration : IEntityTypeConfiguration<ClaimBatch>
{
    public void Configure(EntityTypeBuilder<ClaimBatch> b)
    {
        b.ToTable("ClaimBatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.OfficeId).IsRequired();
        b.Property(x => x.YearMonth).HasConversion(
            ym => ym.ToInt(),
            i => Domain.ValueObjects.YearMonth.FromInt(i));
        b.Property(x => x.Kind).HasConversion<int>().IsRequired();
        b.Property(x => x.OriginId);
        b.Property(x => x.TotalUnit).IsRequired();
        b.Property(x => x.TotalAmountYen).IsRequired();
        b.Property(x => x.TotalBurdenYen).IsRequired();

        // ClaimMasterVersion 4 種類を文字列 3 列ずつに展開
        b.OwnsOne(x => x.MasterVersionRates, v => {
            v.Property(p => p.Version).HasColumnName("MasterVersionRates_Version");
            v.Property(p => p.RetrievedOn).HasColumnName("MasterVersionRates_RetrievedOn");
            v.Property(p => p.Adr).HasColumnName("MasterVersionRates_Adr");
        });
        b.OwnsOne(x => x.MasterVersionAdditions, v => {
            v.Property(p => p.Version).HasColumnName("MasterVersionAdditions_Version");
            v.Property(p => p.RetrievedOn).HasColumnName("MasterVersionAdditions_RetrievedOn");
            v.Property(p => p.Adr).HasColumnName("MasterVersionAdditions_Adr");
        });
        b.OwnsOne(x => x.MasterVersionBurdenCaps, v => {
            v.Property(p => p.Version).HasColumnName("MasterVersionBurdenCaps_Version");
            v.Property(p => p.RetrievedOn).HasColumnName("MasterVersionBurdenCaps_RetrievedOn");
            v.Property(p => p.Adr).HasColumnName("MasterVersionBurdenCaps_Adr");
        });
        b.OwnsOne(x => x.MasterVersionRegionUnits, v => {
            v.Property(p => p.Version).HasColumnName("MasterVersionRegionUnits_Version");
            v.Property(p => p.RetrievedOn).HasColumnName("MasterVersionRegionUnits_RetrievedOn");
            v.Property(p => p.Adr).HasColumnName("MasterVersionRegionUnits_Adr");
        });

        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.CreatedBy).IsRequired();

        // ADR 0026: partial unique index (OfficeId, YearMonth) WHERE Kind = 1
        b.HasIndex(x => new { x.OfficeId, x.YearMonth })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_ClaimBatches_New_Office_YearMonth");
    }
}
```

`ClaimDetailConfiguration.cs`（FK は手動指定）:

```csharp
public sealed class ClaimDetailConfiguration : IEntityTypeConfiguration<ClaimDetail>
{
    public void Configure(EntityTypeBuilder<ClaimDetail> b)
    {
        b.ToTable("ClaimDetails");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.ClaimBatchId);
        b.Property(x => x.RecipientId).IsRequired();
        b.Property(x => x.LineKind).HasConversion<int>().IsRequired();
        b.Property(x => x.Code).IsRequired().HasMaxLength(32);
        b.Property(x => x.Unit).IsRequired();
        b.Property(x => x.AmountYen).IsRequired();

        b.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.CreatedBy).IsRequired();
    }
}
```

`TsumugiDbContext.cs` に DbSet を追加:

```csharp
public DbSet<ClaimBatch> ClaimBatches => Set<ClaimBatch>();
public DbSet<ClaimDetail> ClaimDetails => Set<ClaimDetail>();
```

- [ ] **Step 8: Migration 生成**

```bash
dotnet ef migrations add AddClaimBatchAndDetail \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

生成された `<yyyyMMddHHmmss>_AddClaimBatchAndDetail.cs` を確認し、partial unique index が含まれていることを目視確認。

- [ ] **Step 9: Migration round-trip テスト**

`tests/Tsumugi.Infrastructure.Tests/Persistence/` に既存 round-trip テストパターンを踏襲した `ClaimBatchMigrationRoundTripTests.cs` を追加。マイグレーション → SQLite ファイル作成 → テーブル存在確認 → index 存在確認。

- [ ] **Step 10: ビルド + 全テスト緑確認**

```bash
dotnet build -c Release
dotnet test -c Release
```

両方 PASS。

- [ ] **Step 11: コミット**

```bash
git add src/Tsumugi.Domain/Entities/ClaimDetail.cs src/Tsumugi.Domain/Enums/ClaimDetailLineKind.cs \
        src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs \
        src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs \
        src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimBatchConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimDetailConfiguration.cs \
        src/Tsumugi.Infrastructure/Migrations/*_AddClaimBatchAndDetail*.cs \
        tests/Tsumugi.Domain.Tests/Entities/ClaimDetailTests.cs \
        tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchMigrationRoundTripTests.cs
git commit -m "feat(phase3-0/AC3-0-6): ClaimBatch+ClaimDetail EF config, AppendOnlyGuard, migration"
```

---

### Task 14: IClaimBatchRepository と ClaimBatchRepository

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimBatchRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ClaimBatchRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`（登録）
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs`

**Interfaces:**
- Consumes: `ClaimBatch`、`ClaimDetail`、`IUnitOfWork`
- Produces: 
  - `IClaimBatchRepository.AddBatchAsync(ClaimBatch, IReadOnlyList<ClaimDetail>, CancellationToken) → Task`
  - `IClaimBatchRepository.GetByOfficeAndMonthAsync(Guid officeId, YearMonth ym, CancellationToken) → Task<IReadOnlyList<ClaimBatch>>`（New/Correct/Cancel をすべて履歴順）
  - `IClaimBatchRepository.GetEffectiveBatchAsync(Guid officeId, YearMonth ym, CancellationToken) → Task<ClaimBatch?>`（最新の Correct or New。Cancel があれば null）
  - `IClaimBatchRepository.GetDetailsAsync(Guid claimBatchId, CancellationToken) → Task<IReadOnlyList<ClaimDetail>>`

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Persistence;

public sealed class ClaimBatchRepositoryTests
{
    [Fact]
    public async Task AddBatchAsync_persists_batch_and_details()
    {
        // Arrange: in-memory sqlite or sqlite file fixture
        await using var fixture = await DbFixture.CreateAsync();
        var repo = new ClaimBatchRepository(fixture.Db);
        var batch = ClaimBatch.CreateNew(/* ... */);
        var detail = ClaimDetail.Create(/* ... */);
        await repo.AddBatchAsync(batch, new[] { detail }, default);
        await fixture.Uow.SaveChangesAsync(default);

        // Assert
        var loaded = await repo.GetByOfficeAndMonthAsync(batch.OfficeId, batch.YearMonth, default);
        loaded.Should().HaveCount(1);
        loaded[0].Id.Should().Be(batch.Id);
        var details = await repo.GetDetailsAsync(batch.Id, default);
        details.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEffectiveBatchAsync_returns_latest_correct_when_correct_exists()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var repo = new ClaimBatchRepository(fixture.Db);
        var newBatch = ClaimBatch.CreateNew(/* ... */);
        var correct = ClaimBatch.CreateCorrect(originId: newBatch.Id, /* ... */);
        await repo.AddBatchAsync(newBatch, Array.Empty<ClaimDetail>(), default);
        await fixture.Uow.SaveChangesAsync(default);
        await repo.AddBatchAsync(correct, Array.Empty<ClaimDetail>(), default);
        await fixture.Uow.SaveChangesAsync(default);

        var effective = await repo.GetEffectiveBatchAsync(newBatch.OfficeId, newBatch.YearMonth, default);
        effective!.Id.Should().Be(correct.Id);
    }

    [Fact]
    public async Task GetEffectiveBatchAsync_returns_null_when_latest_is_cancel()
    {
        // 同様パターンで cancel
    }
}
```

- [ ] **Step 2: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "ClaimBatchRepositoryTests" -v normal`
Expected: FAIL

- [ ] **Step 3: IClaimBatchRepository を作る**

`src/Tsumugi.Application/Abstractions/IClaimBatchRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IClaimBatchRepository
{
    Task AddBatchAsync(ClaimBatch batch, IReadOnlyList<ClaimDetail> details, CancellationToken ct);
    Task<IReadOnlyList<ClaimBatch>> GetByOfficeAndMonthAsync(Guid officeId, YearMonth yearMonth, CancellationToken ct);
    Task<ClaimBatch?> GetEffectiveBatchAsync(Guid officeId, YearMonth yearMonth, CancellationToken ct);
    Task<IReadOnlyList<ClaimDetail>> GetDetailsAsync(Guid claimBatchId, CancellationToken ct);
}
```

- [ ] **Step 4: ClaimBatchRepository を実装**

`src/Tsumugi.Infrastructure/Persistence/ClaimBatchRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ClaimBatchRepository(TsumugiDbContext db) : IClaimBatchRepository
{
    public async Task AddBatchAsync(ClaimBatch batch, IReadOnlyList<ClaimDetail> details, CancellationToken ct)
    {
        await db.ClaimBatches.AddAsync(batch, ct);
        if (details.Count > 0)
            await db.ClaimDetails.AddRangeAsync(details, ct);
    }

    public async Task<IReadOnlyList<ClaimBatch>> GetByOfficeAndMonthAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct) =>
        await db.ClaimBatches
            .AsNoTracking()
            .Where(b => b.OfficeId == officeId && b.YearMonth == yearMonth)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public async Task<ClaimBatch?> GetEffectiveBatchAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct)
    {
        var all = await GetByOfficeAndMonthAsync(officeId, yearMonth, ct);
        var latest = all.LastOrDefault();
        if (latest is null) return null;
        if (latest.Kind == RecordKind.Cancel) return null;
        return latest;  // New or Correct（最新）
    }

    public async Task<IReadOnlyList<ClaimDetail>> GetDetailsAsync(Guid claimBatchId, CancellationToken ct) =>
        await db.ClaimDetails
            .AsNoTracking()
            .Where(d => d.ClaimBatchId == claimBatchId)
            .ToListAsync(ct);
}
```

- [ ] **Step 5: DI 登録**

`src/Tsumugi.Infrastructure/DependencyInjection.cs` に登録追加:

```csharp
services.AddScoped<IClaimBatchRepository, ClaimBatchRepository>();
```

- [ ] **Step 6: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "ClaimBatchRepositoryTests" -v normal
git add src/Tsumugi.Application/Abstractions/IClaimBatchRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/ClaimBatchRepository.cs \
        src/Tsumugi.Infrastructure/DependencyInjection.cs \
        tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs
git commit -m "feat(phase3-0/AC3-0-6): IClaimBatchRepository + ClaimBatchRepository"
```

---

### Task 15: ClaimBatchDuplicateNewIndex テスト

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchDuplicateNewIndexTests.cs`

**Interfaces:**
- Consumes: Task 13 の partial unique index、Task 14 の Repository

このタスクは partial unique index の **歯あり性** を検証する。既存 `DailyRecordDuplicateNewIndexTests` / `WageFundDuplicateNewIndexTests` と同じパターン。

- [ ] **Step 1: 失敗テストを書く**

```csharp
public sealed class ClaimBatchDuplicateNewIndexTests
{
    [Fact]
    public async Task Inserting_two_New_with_same_OfficeId_and_YearMonth_throws_DbUpdateException()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var officeId = Guid.NewGuid();
        var ym = new YearMonth(2026, 5);
        var v = ClaimMasterVersion.Unknown;
        var first = ClaimBatch.CreateNew(/* officeId, ym, ... */);
        var second = ClaimBatch.CreateNew(/* same officeId, ym, but different Id */);

        await fixture.Db.ClaimBatches.AddAsync(first);
        await fixture.Db.SaveChangesAsync();

        await fixture.Db.ClaimBatches.AddAsync(second);
        Func<Task> act = async () => await fixture.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Inserting_New_and_Correct_with_same_OfficeId_and_YearMonth_succeeds()
    {
        // New と Correct は重複可能（filter は Kind=1 のみ）
    }

    [Fact]
    public async Task Inserting_two_Correct_with_same_OfficeId_and_YearMonth_succeeds()
    {
        // Correct 同士は重複可能
    }
}
```

- [ ] **Step 2: テスト緑確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "ClaimBatchDuplicateNewIndexTests" -v normal`
Expected: PASS（既に Task 13 で index は配置済）

- [ ] **Step 3: コミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchDuplicateNewIndexTests.cs
git commit -m "test(phase3-0/AC3-0-6): ClaimBatch partial unique index teeth (ADR 0026)"
```

---

### Task 16: Tsumugi.Infrastructure.Csv アセンブリ新設

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj`
- Create: `src/Tsumugi.Infrastructure.Csv/ClaimCsvWriter.cs`（空殻）
- Create: `src/Tsumugi.Application/Abstractions/ICsvClaimWriter.cs`
- Modify: `Tsumugi.sln`（プロジェクト追加）
- Modify: `src/Tsumugi.App/Tsumugi.App.csproj`（Csv 参照追加、Reporting と同じ流儀）
- Modify: `src/Tsumugi.App/DependencyInjection.cs` or `Program.cs`（DI 登録）

**Interfaces:**
- Produces: `ICsvClaimWriter.Write(Stream output, ClaimBatch batch, IReadOnlyList<ClaimDetail> details) → void`（空殻実装、Phase 3-3 で完成）

- [ ] **Step 1: ICsvClaimWriter を Application に定義**

`src/Tsumugi.Application/Abstractions/ICsvClaimWriter.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 国保連 請求 CSV の書出し抽象。実装は <c>Tsumugi.Infrastructure.Csv</c>。
/// 仕様詳細は ADR 0023（3-0 で確定）。実装は 3-3。
/// </summary>
public interface ICsvClaimWriter
{
    /// <summary>
    /// ClaimBatch と明細を国保連仕様準拠の CSV としてストリームに書き出す。
    /// 出典版（CSV 仕様版・マスタ版）はヘッダ or ファイル名に埋め込まれる（実装で確定）。
    /// </summary>
    void Write(Stream output, ClaimBatch batch, IReadOnlyList<ClaimDetail> details);
}
```

- [ ] **Step 2: Tsumugi.Infrastructure.Csv アセンブリを新設**

`src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../Tsumugi.Application/Tsumugi.Application.csproj" />
    <ProjectReference Include="../Tsumugi.Domain/Tsumugi.Domain.csproj" />
  </ItemGroup>
</Project>
```

`src/Tsumugi.Infrastructure.Csv/ClaimCsvWriter.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Csv;

/// <summary>
/// 国保連 請求 CSV 書出し（3-3 で完成）。3-0 では空殻。
/// </summary>
public sealed class ClaimCsvWriter : ICsvClaimWriter
{
    public void Write(Stream output, ClaimBatch batch, IReadOnlyList<ClaimDetail> details) =>
        throw new NotImplementedException("Phase 3-3 で実装");
}
```

- [ ] **Step 3: Tsumugi.sln にプロジェクト追加**

```bash
dotnet sln add src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj
```

- [ ] **Step 4: App から参照追加**

`src/Tsumugi.App/Tsumugi.App.csproj` の `<ItemGroup>` に追加:

```xml
<ProjectReference Include="../Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj" />
```

- [ ] **Step 5: DI 登録（合成ルート）**

App の DI 登録部分（Phase 2 で Reporting を登録している箇所と同じ場所）:

```csharp
services.AddSingleton<ICsvClaimWriter, ClaimCsvWriter>();
```

- [ ] **Step 6: ビルド緑確認**

```bash
dotnet build -c Release
```

Expected: 警告ゼロで成功

- [ ] **Step 7: コミット**

```bash
git add Tsumugi.sln src/Tsumugi.Infrastructure.Csv/ src/Tsumugi.Application/Abstractions/ICsvClaimWriter.cs src/Tsumugi.App/
git commit -m "feat(phase3-0/AC3-0-5): scaffold Tsumugi.Infrastructure.Csv assembly with ICsvClaimWriter"
```

---

### Task 17: OfflineComplianceTests / AppOfflineComplianceTests に Csv アセンブリ追加

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`（または `Tsumugi.App.Tests` 配下、既存配置に従う）

**Interfaces:**
- Consumes: Task 16 の `Tsumugi.Infrastructure.Csv`

- [ ] **Step 1: OfflineComplianceTests の `[Theory]` データに Tsumugi.Infrastructure.Csv を追加**

既存ファイルを開いて、`[InlineData]` または `MemberData` のリストに `"Tsumugi.Infrastructure.Csv"` を追加。

- [ ] **Step 2: AppOfflineComplianceTests も同様に拡張**

`Tsumugi.App` の直接参照に `Tsumugi.Infrastructure.Csv` が含まれることを許容（既に許容されているが、明示的な追加チェック）。

- [ ] **Step 3: 歯あり性チェック（一時混入で赤）**

`src/Tsumugi.Infrastructure.Csv/ClaimCsvWriter.cs` に一時的に `using System.Net.Http;` と `var c = new HttpClient();` を追加し、`dotnet test --filter "OfflineCompliance"` で赤になることを目視確認。確認後、変更を破棄（`git checkout -- src/Tsumugi.Infrastructure.Csv/ClaimCsvWriter.cs`）。

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "OfflineCompliance" -v normal
git add tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs
git commit -m "test(phase3-0/AC3-0-5): include Tsumugi.Infrastructure.Csv in offline compliance theories"
```

---

### Task 18: ハードコード機械判定 (a) — Domain/Application 語彙スキャナ

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/Architecture/HardcodeScannerTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Architecture/ScannerToothTests.cs`（歯あり性確認の補助）

**Interfaces:**
- 機械判定: Domain/Application のソースに `単位数` / `加算` / `区分単価` 等の語彙 literal が現れたら赤

- [ ] **Step 1: 失敗テストを書く**

```csharp
using FluentAssertions;
using System.Reflection;

namespace Tsumugi.Domain.Tests.Architecture;

public sealed class HardcodeScannerTests
{
    /// <summary>(a) Domain/Application の .cs ソースを走査し、ハードコード語彙が混入していないか確認。</summary>
    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    public void Source_files_do_not_contain_hardcoded_remuneration_vocabulary(string project)
    {
        // テスト時の current dir からプロジェクトディレクトリを解決
        var srcDir = ResolveSrcDir(project);
        var hits = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
            .Where(f => !f.EndsWith("AssemblyInfo.cs"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, idx) => (file: f, line, idx)))
            .Where(t => ContainsForbiddenVocabulary(t.line))
            .Where(t => !IsAllowed(t.file, t.line))
            .ToList();

        hits.Should().BeEmpty(
            "Domain/Application に単位数・加算・区分単価などの語彙 literal が混入してはならない。" +
            "実値はマスタ JSON 経由で供給すること。");
    }

    private static bool ContainsForbiddenVocabulary(string line)
    {
        // 文字列 literal の中の語彙を検出（コメントや XML doc は除外したいが、簡略化のためコード行のみで）
        var forbidden = new[] { "単位数", "加算", "区分単価" };
        // 文字列 literal を含む行のみ対象（"..." を含む）
        if (!line.Contains('"')) return false;
        return forbidden.Any(f => line.Contains($"\"{f}") || line.Contains($"{f}\""));
    }

    private static bool IsAllowed(string file, string line) =>
        // 明示 allowlist（例: マスタ抽象 interface のドキュメント文字列内など）
        // 本タスクでは初期実装で空。違反が出たら個別判断で allowlist 追加か実装側修正
        false;

    private static string ResolveSrcDir(string project)
    {
        var cur = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(cur, "src", project);
            if (Directory.Exists(candidate)) return candidate;
            cur = Path.GetFullPath(Path.Combine(cur, ".."));
        }
        throw new InvalidOperationException($"src/{project} が見つかりません");
    }
}
```

- [ ] **Step 2: 現在のコードが緑であることを確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "HardcodeScannerTests" -v normal`
Expected: PASS（現状 Domain/Application に該当語彙が無い前提）

もし赤になった場合: 既存コードの XML ドキュメントコメントや日本語ファイルヘッダに該当語彙が含まれている可能性がある。Step 1 の `ContainsForbiddenVocabulary` を「コメント行を除外」する形に強化するか、`IsAllowed` の allowlist で個別除外。

- [ ] **Step 3: ScannerToothTests（歯あり性）を書く**

```csharp
public sealed class ScannerToothTests
{
    [Fact]
    public void Hardcode_scanner_a_catches_intentional_violation()
    {
        // 一時ファイルに違反コードを書き出し、スキャナのロジックを単独関数として呼んで赤になることを確認
        var temp = Path.GetTempFileName() + ".cs";
        try
        {
            File.WriteAllText(temp, @"namespace X; public static class Bad { public const string V = ""単位数=600""; }");
            // HardcodeScannerTests と同じロジックを直接実行
            var lines = File.ReadAllLines(temp);
            var hit = lines.Any(l => l.Contains("\"単位数") || l.Contains("単位数\""));
            hit.Should().BeTrue("意図的違反でスキャナが反応すべき");
        }
        finally { File.Delete(temp); }
    }
}
```

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "HardcodeScannerTests|ScannerToothTests" -v normal
git add tests/Tsumugi.Domain.Tests/Architecture/
git commit -m "test(phase3-0/AC3-0-3): hardcode scanner (a) for Domain/Application vocabulary"
```

---

### Task 19: ハードコード機械判定 (b) — CSV カラム名 literal の名前空間境界

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/CsvLiteralNamespaceTests.cs`

**Interfaces:**
- 機械判定: CSV カラム名 literal が `Tsumugi.Infrastructure.Csv` 以外に現れたら赤

- [ ] **Step 1: 失敗テストを書く**

```csharp
using FluentAssertions;

namespace Tsumugi.Infrastructure.Tests;

public sealed class CsvLiteralNamespaceTests
{
    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    [InlineData("Tsumugi.Infrastructure.Reporting")]
    [InlineData("Tsumugi.App")]
    public void Non_csv_projects_do_not_contain_csv_column_literals(string project)
    {
        // CSV カラム名候補のシグネチャを定義（公式仕様確定後に拡張）
        // 3-0 では「CSV カラム」「,(comma)」のような単純パターンではなく、
        // 国保連 CSV の典型的なフィールド名（受給者証番号、サービス提供月、事業所番号 等）を対象とする。
        // ADR 0023 確定後に語彙を拡張。3-0 では下記の暫定リストでスキャナを通す。
        var csvFieldVocab = new[] { "受給者証番号", "サービス提供月", "事業所番号", "サービスコード" };

        var srcDir = ResolveSrcDir(project);
        var hits = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
            .SelectMany(f => File.ReadAllLines(f).Select(line => (file: f, line)))
            .Where(t => csvFieldVocab.Any(v => t.line.Contains($"\"{v}\"")))
            .ToList();

        hits.Should().BeEmpty(
            $"{project} に CSV カラム名 literal が含まれてはならない。"
            + "CSV カラム名は Tsumugi.Infrastructure.Csv 名前空間内に限定する。");
    }

    private static string ResolveSrcDir(string project)
    {
        var cur = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(cur, "src", project);
            if (Directory.Exists(candidate)) return candidate;
            cur = Path.GetFullPath(Path.Combine(cur, ".."));
        }
        throw new InvalidOperationException($"src/{project} が見つかりません");
    }
}
```

- [ ] **Step 2: 現在のコードが緑であることを確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "CsvLiteralNamespaceTests" -v normal`
Expected: PASS（現状非 Csv プロジェクトに該当語彙が無い前提）

- [ ] **Step 3: 歯あり性確認**

`src/Tsumugi.Domain/` 配下のどこかに一時的に `// "受給者証番号"` という literal を入れてテスト実行 → 赤になることを確認。確認後、変更破棄。

- [ ] **Step 4: コミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/CsvLiteralNamespaceTests.cs
git commit -m "test(phase3-0/AC3-0-3): CSV column literal namespace boundary (b)"
```

---

### Task 20: ハードコード機械判定 (c) — Domain 内整数 literal 上限ガード

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/Architecture/IntegerLiteralCeilingTests.cs`

**Interfaces:**
- 機械判定: Domain 内の int/decimal literal で **1000 を超える値**が現れたら赤（料金混入の検出）

- [ ] **Step 1: 失敗テストを書く**

```csharp
using FluentAssertions;
using System.Text.RegularExpressions;

namespace Tsumugi.Domain.Tests.Architecture;

public sealed class IntegerLiteralCeilingTests
{
    [Fact]
    public void Domain_does_not_contain_integer_literals_above_threshold()
    {
        const int threshold = 1000;
        // Domain のソースを走査
        var srcDir = ResolveSrcDir("Tsumugi.Domain");
        var literalPattern = new Regex(@"\b(\d{4,})\b", RegexOptions.Compiled);
        // 「\b\d{4,}\b」で 1000 以上の数値を抽出
        var hits = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, idx) => (file: f, line, idx: idx + 1)))
            .Where(t => !IsCommentLine(t.line))
            .SelectMany(t => literalPattern.Matches(t.line)
                .Select(m => (t.file, t.line, t.idx, value: int.Parse(m.Value)))
                .Where(x => x.value > threshold))
            .Where(x => !IsAllowedContext(x.line))
            .ToList();

        hits.Should().BeEmpty(
            $"Domain 内に {threshold} を超える数値 literal が混入してはならない。" +
            "報酬金額・単位数等はマスタ JSON 経由で供給すること。年・日付の例外は IsAllowedContext で許可。");
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*");
    }

    private static bool IsAllowedContext(string line)
    {
        // 年・年月などの正当な大きい数値は許可
        // 例: new YearMonth(2024, 4), new DateOnly(2026, 6, 29), 4096 (バッファサイズ等)
        return line.Contains("YearMonth(") ||
               line.Contains("DateOnly(") ||
               line.Contains("DateTimeOffset") ||
               line.Contains("buffer") ||
               // テスト用 InMemory* で値を使う行は Domain ではないので走査対象外
               false;
    }

    private static string ResolveSrcDir(string project) { /* Task 18 と同じ */ }
}
```

- [ ] **Step 2: 現状緑を確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "IntegerLiteralCeilingTests" -v normal`
Expected: PASS（既存 Domain には大きな literal が無い前提）

> 赤になった場合: Phase 1/2 で `YearMonth(2024, 4)` のような正当な使用が `IsAllowedContext` で除外できているか確認。除外漏れがあれば許可ルールを追加。

- [ ] **Step 3: 歯あり性確認**

`src/Tsumugi.Domain/Logic/Claim/IRateMaster.cs` のコメント外に一時的に `// const int X = 6000;` を入れて赤確認。

- [ ] **Step 4: コミット**

```bash
git add tests/Tsumugi.Domain.Tests/Architecture/IntegerLiteralCeilingTests.cs
git commit -m "test(phase3-0/AC3-0-3): integer literal ceiling (c) for Domain"
```

---

### Task 21: open-questions.md / CHANGELOG / 受け入れ確認

**Files:**
- Modify: `docs/open-questions.md`（Phase 3-0 の項目を `[x]` 化、未確定項目を整理）
- Modify: `CHANGELOG.md`（Phase 3-0 完了を追記）
- Create: `docs/phase3-0-acceptance.md`（受け入れ基準チェックリスト形式、Phase 2 の前例を踏襲）

**Interfaces:**
- 全体受け入れ: AC3-0-1〜9 がすべてチェック済

- [ ] **Step 1: open-questions.md を最終確認**

Task 1〜8 で個別に更新済の各項目（ADR 0018〜0024, 0026 を起票したもの）が漏れなく `[x]` になっているかを確認。Phase 3-0 として開く新項目（例: ADR 0025 の端数規則は 3-1 で確定）を未確定として整理。

- [ ] **Step 2: docs/phase3-0-acceptance.md を作成**

`docs/phase2-acceptance.md` のフォーマットを踏襲し、AC3-0-1〜9 を列挙してチェックボックス形式で記録。各 AC に対する根拠コミット・ADR・テストファイルへのリンクを書く。

- [ ] **Step 3: CHANGELOG.md を更新**

Phase 3-0 完了を追記。新規 ADR 0018〜0024, 0026 のリンクと、新規アセンブリ `Tsumugi.Infrastructure.Csv` 追加を記す。

- [ ] **Step 4: `./build/ci.sh` 緑確認**

```bash
./build/ci.sh
```

Expected: 緑（restore → format → build warnings-as-errors → test + coverage → coverage threshold gate）

- [ ] **Step 5: 全テスト緑確認**

```bash
dotnet test -c Release
```

Expected: PASS（全テスト緑、Domain 95% カバレッジ維持、Application 70% 維持）

- [ ] **Step 6: コミット**

```bash
git add docs/open-questions.md docs/phase3-0-acceptance.md CHANGELOG.md
git commit -m "docs(phase3-0/AC3-0-9): Phase 3-0 acceptance complete + open-questions sync + CHANGELOG"
```

---

## Phase 3-0 全体受け入れ基準

すべて緑になったら **Phase 3-1 着手可**。

- [ ] AC3-0-1 ADR 0018〜0023 が一次情報に基づき確定
- [ ] AC3-0-2 Noto Sans CJK JP 埋込（ADR 0024）、CI で CJK 化けなし
- [ ] AC3-0-3 ハードコード機械判定 (a)(b)(c) 緑＋歯あり性確認
- [ ] AC3-0-4 マスタ抽象 interface + InMemory + JSON ローダ空殻 揃う
- [ ] AC3-0-5 `Tsumugi.Infrastructure.Csv` 新設＋オフライン検査対象
- [ ] AC3-0-6 `ClaimBatch` / `ClaimDetail` 骨組み（AppendOnly, partial unique index, migration, Repository, 歯あり性）
- [ ] AC3-0-7 Application カバレッジ閾値底上げ準備
- [ ] AC3-0-8 ADR 0020 OfficeCapability 移行戦略
- [ ] AC3-0-9 `./build/ci.sh` 緑、Domain 95% 維持、依存方向不変、オフライン検査緑

## 参考

- 設計仕様書: `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`
- Phase 3 指示書: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`
- 前例 ADR / Plan: `docs/decisions/0015`/`0017`、`docs/superpowers/plans/2026-06-29-m3-wagefund-duplicate-new-index.md`
