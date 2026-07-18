# Phase 3-0 Task 16 Acceptance Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3-0のAC3-0-1〜AC3-0-5を現行コードと再実行可能な証跡で判定し、未完了のPhase 3-1以降と混同せずに引継ぎ文書を完成する。

**Architecture:** 証跡先行・fail-closedで進め、受け入れ条件と停止条件をコード・ADR・テスト・コミットへ双方向に結び付けてから文書を更新する。検証中に実装欠陥、未確定値、未対応fieldId、placeholder、throw-only production経路を検出した場合はPhase 3-0を完了扱いにせず、Task 16の文書コミットへコード修正を混在させない。

**Tech Stack:** Markdown、Git、.NET 10、xUnit、coverlet、`dotnet format`、`build/ci.sh`、NuGet audit

---

## 仕様契約と実行境界

- 上位契約: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` のPhase 3-0とAC3-0-1〜AC3-0-5
- 詳細契約: `docs/superpowers/plans/2026-07-10-phase3-0-source-contract-and-foundation.md` のTask 16とPhase 3-0完了ゲート
- 設計判断: `docs/decisions/0020-claim-master-sources-and-versioning.md`〜`docs/decisions/0026-claim-batch-snapshot.md`
- 項目集合: `docs/phase3-claim-field-mapping.md`。Phase 3-1へ送る未実装入力は現時点で51 mapping entries
- Phase 3-0開始前の基準commit: `ec0b4b9d04deae41523023947bc0eeaf910b169e`。最終レビュー範囲は `ec0b4b9..HEAD`
- Task 16では本番コード、テスト、ADR、seed、migrationを変更しない。検証で不足が判明した場合は停止し、別の修正タスクとして扱う
- `.serena/project.yml`、`graphify-out/`、一時取得資料、テスト結果は既存の利用者作業として保持し、stageしない
- 本計画書`docs/superpowers/plans/2026-07-11-phase3-0-task16-acceptance-closeout.md`は計画成果物であり、Task 16の3文書commitには混在させない

**実行時に使用するskills:**

- Task 1〜8: `@superpowers:subagent-driven-development`（推奨）または`@superpowers:executing-plans`
- Task 7〜9: `@superpowers:verification-before-completion`
- Task 9: `@superpowers:requesting-code-review`

## ファイル構成

**Task 16で変更するファイル:**

- Modify: `docs/open-questions.md` — Phase 3-0で確定した旧質問を根拠付きでクローズし、51件の未実装入力をPhase 3-1 AC3-8へ明示的に引き継ぐ
- Create: `docs/phase3-0-acceptance.md` — AC3-0-1〜5、横断ゲート、停止条件、テスト結果、コミット範囲、deferredを一元化する受入証跡
- Modify: `CHANGELOG.md` — Phase 3-0で完成した契約・土台と、未実装のPhase 3-1〜3-3を分離して記録する

**証跡として読むだけの主なファイル:**

- `docs/decisions/0020-claim-master-sources-and-versioning.md`〜`docs/decisions/0026-claim-batch-snapshot.md`
- `docs/phase3-claim-field-mapping.md`
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/`
- `src/Tsumugi.Infrastructure.Csv/Specifications/`
- `tests/Tsumugi.Domain.Tests/Entities/ClaimBatchTests.cs`
- `tests/Tsumugi.Domain.Tests/Entities/ClaimDetailTests.cs`
- `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimBatchPolicyTests.cs`
- `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimMasterCatalogPolicyTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/ClaimFieldMappingCompletenessTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/CsvSpecificationCompletenessTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimSpecificationBoundaryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimBatchUniqueConstraintTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimFinalizationStoreTests.cs`

---

### Task 1: 受け入れ対象と作業スコープを固定する

**Files:**
- Read: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md:163`
- Read: `docs/superpowers/plans/2026-07-10-phase3-0-source-contract-and-foundation.md:1282`
- Read: `docs/phase3-claim-field-mapping.md:577`

- [ ] **Step 1: branch、HEAD、利用者変更を記録する**

Run:

```bash
git status --short --branch
git rev-parse HEAD
git rev-parse origin/main
```

Expected: `main...origin/main`で両HEADが一致する。`.serena/project.yml`、本計画書、`graphify-out/`が表示されてもTask 16の対象へ含めない。

- [ ] **Step 2: Phase 3-0のレビュー範囲を固定する**

Run:

```bash
git show --no-patch --format='%H %P %s' 0ca1214
git log --reverse --oneline ec0b4b9..HEAD
```

Expected: `0ca1214`の親が`ec0b4b9`で、範囲の先頭が`docs(phase3-0): add source contract and foundation plan`、末尾がTask 15完了commit以降の現行HEADである。

- [ ] **Step 3: 変更可能ファイルを3件に固定する**

実装担当の作業メモへ次のallowlistを記録する。

```text
docs/open-questions.md
docs/phase3-0-acceptance.md
CHANGELOG.md
```

Expected: 検証で他ファイルの変更が必要になった場合はTask 16を停止し、完了文書を作らない。

---

### Task 2: 文書更新前にPhase 3-0停止条件を監査する

**Files:**
- Read: `docs/decisions/0020-claim-master-sources-and-versioning.md`
- Read: `docs/decisions/0021-office-capability-official-codes.md`
- Read: `docs/decisions/0022-burden-cap-master.md`
- Read: `docs/decisions/0023-average-wage-and-r8-transition.md`
- Read: `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`
- Read: `docs/decisions/0025-claim-rounding-rules.md`
- Read: `docs/decisions/0026-claim-batch-snapshot.md`
- Read: `docs/phase3-claim-field-mapping.md`

- [ ] **Step 1: ADR 0020〜0026の必須メタデータを目視監査する**

各ADRについて、状態、一次資料URL、版、施行日または適用年月、取得日、SHA-256、結論、コード／次フェーズへの影響を確認する。`null`は資料に訂正日等が存在しないことを表す正規値として一律エラーにせず、必須欄の欠落だけを停止条件にする。

- [ ] **Step 2: placeholder候補を機械検索する**

Run:

```bash
rg -n -i 'TBD|TODO|PLACEHOLDER|placeholder|仮コード|未確定' \
  docs/decisions/002{0,1,2,3,4,5,6}-*.md \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed \
  src/Tsumugi.Infrastructure.Csv/Specifications
```

Expected: 仕様値や実装を先送りする未解決placeholderが0件。説明文中の禁止語など正当なhitがある場合は、ファイル・行・除外根拠を`docs/phase3-0-acceptance.md`の監査欄へ記録する。

- [ ] **Step 3: 51件のdeferred集合を再計数する**

Run:

```bash
sed -n '/^## Phase 3-1へ送る未実装入力/,/^未実装項目は/p' \
  docs/phase3-claim-field-mapping.md \
  | rg '^\| (Certificate|ClaimInput|ContractedProvider|IntensiveSupportEpisode|DailyRecord|Office)\.' \
  | wc -l
```

Expected: `51`。各行に`target`、`fieldId`、`UI`、`migration`があり、末尾に推測禁止とfail-closed条件がある。

- [ ] **Step 4: productionの意図的write停止境界を確認する**

Run:

```bash
rg -n 'UnavailableClaimSnapshotValidationCodecRegistry|IValidatedClaimSnapshotReader|throw new NotSupportedException|throw new NotImplementedException' src tests
```

Expected: Phase 3-0のproduction codec未登録は`UnavailableClaimSnapshotValidationCodecRegistry`による明示的fail-closedであり、Phase 3-1のvalidated readerが未実装であることと整合する。未説明のthrow-only production実装があれば停止する。

- [ ] **Step 5: 停止条件の判定を記録する**

Expected: 未確定値、未対応fieldId、TBD、仮コード、空SHA、未説明のthrow-only production実装が0件ならTask 3へ進む。1件でも説明できないものがあればTask 16を中断し、`docs/open-questions.md`を完了へ変更しない。

---

### Task 3: targeted testでAC3-0-1〜5の証跡を採取する

**Files:**
- Test: `tests/Tsumugi.Domain.Tests/`
- Test: `tests/Tsumugi.Application.Tests/`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/`
- Test: `tests/Tsumugi.Infrastructure.Tests/`
- Test: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`

- [ ] **Step 1: Domainのclaim契約を実行する**

Run:

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj \
  --filter 'FullyQualifiedName~Claim' -v normal
```

Expected: PASS。`ClaimBatch`、`ClaimDetail`、`ClaimBatchPolicy`、`ClaimMasterCatalogPolicy`が対象になる。

- [ ] **Step 2: Applicationのfinalization契約を実行する**

Run:

```bash
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj \
  --filter 'FullyQualifiedName~Claim' -v normal
```

Expected: PASS。snapshot envelope、operation registry、canonical operation、audit payloadが対象になる。

- [ ] **Step 3: CSV仕様・全項目mappingを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj -v normal
```

Expected: PASS。公式CSV catalog、3帳票inventory、全fieldIdの集合一致、human-readable mappingとの一致が対象になる。

- [ ] **Step 4: Infrastructureのclaim・offline・architectureを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj \
  --filter 'FullyQualifiedName~Claim|FullyQualifiedName~OfflineCompliance|FullyQualifiedName~Architecture' \
  -v normal
```

Expected: PASS。master loader、hardcode境界、migration往復、partial unique index、append-only guard、repository、finalization store、依存方向、ネットワーク／P/Invoke／URL literal検査が対象になる。

- [ ] **Step 5: production DIのfail-closed配線を実行する**

Run:

```bash
dotnet test tests/Tsumugi.App.Tests/Tsumugi.App.Tests.csproj \
  --filter 'FullyQualifiedName~CompositionRootTests' -v normal
```

Expected: PASS。productionがunavailable codec registryを登録し、Phase 3-0時点で未検証snapshotを書けない配線を維持する。

- [ ] **Step 6: 実行結果を一時メモへ転記する**

各コマンドについて実行日、対象、成功／失敗、合計test数、失敗数を記録する。リポジトリへ結果ファイルを生成しない。

---

### Task 4: `docs/open-questions.md`を事実に同期する

**Files:**
- Modify: `docs/open-questions.md:5-7`
- Modify: `docs/open-questions.md:17-27`
- Modify: `docs/open-questions.md:42`
- Read: `docs/decisions/0020-claim-master-sources-and-versioning.md`
- Read: `docs/decisions/0021-office-capability-official-codes.md`
- Read: `docs/decisions/0022-burden-cap-master.md`
- Read: `docs/decisions/0023-average-wage-and-r8-transition.md`
- Read: `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`

- [ ] **Step 1: Phase 3着手前の出典2項目をクローズする**

報酬出典と国保連CSV仕様のcheckboxを`[x]`へ変更し、2026-07-11クローズ、ADR 0020／0024、source catalog、再現可能SHA検査を根拠として追記する。公式資料そのものをCHANGELOGへ複製しない。

- [ ] **Step 2: hardcode機械判定をクローズする**

`ClaimSpecificationBoundaryTests`の次の保証を記載する。

```text
制度数値・文字列literalの外部catalog境界
CSV tokenのTsumugi.Infrastructure.Csv境界
未知／空source、誤配置catalog、invalid JSON/C#のfail-closed
```

Expected: 「Phase 3で追加予定」という未来形を削除し、現行テスト名とADR 0020／0024へリンクする。

- [ ] **Step 3: OfficeCapabilityと制度値の質問をクローズする**

次を個別に`[x]`へ変更する。

```text
事業所体制の加算フラグキー一覧 / OfficeCapability正式コード -> ADR 0021
食事提供体制加算・高額障害福祉サービス費等 -> ADR 0020/0021とversioned masterへ引継ぎ
負担区分の月額上限 -> ADR 0022
平均工賃月額 -> ADR 0023
```

Expected: 「値をC#へ直書きして実装済み」とは書かず、Phase 3-0では一次資料・版・解決規則を確定し、実計算はPhase 3-1と明記する。

- [ ] **Step 4: 性別拡張の質問を結論付きでクローズする**

事業所編に性別fieldが存在しないため`Recipient.Gender` migrationを追加しないこと、Certificate側の既存性別を請求用に推測流用しないこと、AC3-0-5の全件mappingが根拠であることを記載する。

- [ ] **Step 5: Phase 3-1 AC3-8のopen itemを新設する**

次の内容を1件の集約項目として追加する。

```markdown
- [ ] **[Phase 3-1 / AC3-8] 公式請求入力51 mapping entriesの実装**: `docs/phase3-claim-field-mapping.md` の「Phase 3-1へ送る未実装入力」を正本とし、各fieldIdについて記載済みtarget、UI、migrationを実装する。既存備考、別用途bool、0、空文字から推測せず、モデル・migration・実UI・validated readerが揃うまでclaim finalizationをfail closedとする。
```

Expected: 51件を`docs/open-questions.md`へ複製せず、正本へのリンクと件数、完了条件だけを置く。

- [ ] **Step 6: 無関係なopen questionが保持されたことを確認する**

Run:

```bash
git diff -- docs/open-questions.md
```

Expected: SQLite監査抑制、暗号化、Phase 1／2／4の未解決事項など、Task 16外の状態は変化しない。

---

### Task 5: `docs/phase3-0-acceptance.md`を作成する

**Files:**
- Create: `docs/phase3-0-acceptance.md`
- Read: `docs/phase2-acceptance.md`
- Read: `docs/decisions/0020-claim-master-sources-and-versioning.md`〜`docs/decisions/0026-claim-batch-snapshot.md`
- Read: `docs/phase3-claim-field-mapping.md`

- [ ] **Step 1: 文書ヘッダと判定規則を書く**

冒頭に更新日、仕様契約、レビュー範囲`ec0b4b9..HEAD`、証跡採取時HEADを記載する。判定は`✅ accepted`、`❌ blocked`の2値とし、根拠不足を`partial`で完了扱いにしない。Task 9の最終レビューが終わるまでは、理由を「最終レビュー待ち」として`❌ blocked`を維持する。

- [ ] **Step 2: AC3-0-1〜5の証跡表を書く**

次の列を持つ表を作成する。

```markdown
| AC | 状態 | ADR・仕様 | 主な実装 | 主要テスト | 代表commit | 実行結果 |
| --- | --- | --- | --- | --- | --- | --- |
```

最低限、次を対応付ける。

```text
AC3-0-1 -> ADR 0020〜0026、source SHA/cross-reference tests、
  9dcf165 / 807e08b / 56d8110 / 09f1f75 / 9cb4e00 / 95f8d4c /
  82c2863 / a3ef614 / a3dfa90 / a7ee1b6 / c05aa4e / 22393b8 / 3978fb9
AC3-0-2 -> ClaimMasterCatalogPolicy、JsonClaimMasterProvider、ClaimSpecificationBoundaryTests、
  58125ee / c68fe44 / 41d5b79 / 5dd2d8f / 2aa8a86 / a1ce396 / 9cdc302 /
  e3fb79b / 7baabb4 / 95066fd / c7ed6ae / 44a5010 / c89e9ad / 2d48107
AC3-0-3 -> Tsumugi.Infrastructure.Csv、Csv.Tests、architecture/offline guards、
  8d193f9 / 5ecc154 / f6a9420 / c2b9f9e
AC3-0-4 -> ClaimBatch/Detail、policy、guard、index、migration、repository/finalization store、
  35ca6fa / dce5ce5 / 23ae4a6 / 875cee6 / 1280f5c / 8b5c225 / 47a656e /
  38dd67a / b1c7839 / 419be08
AC3-0-5 -> machine inventories、mapping doc、51 missing entries、ClaimFieldMappingCompletenessTests、
  164cac2 / 6ce1011 / 85e1fe0 / 7949635 / 6e92151
```

commitは包含関係を誤認しないよう、範囲記法ではなく上記の代表commitを列挙する。横断CI修正`dab2102`はAC行へ混在させず、横断品質ゲート欄へ記録する。

- [ ] **Step 3: 横断品質ゲート欄を書く**

`dotnet format`、`./build/ci.sh`、Domain/Application coverage、offline、architecture、NuGet auditについて、実行コマンドと当日の実測結果を記載する。過去の1084 tests、Domain 98.12%、Application 85.85%をコピーせず、Task 16で再実行した値を採用する。

- [ ] **Step 4: 停止条件監査欄を書く**

未確定値、未対応fieldId、TBD、仮コード、空SHA、throw-only productionの各項目を列挙し、監査方法と結果を記載する。正当な検索hitがある場合は除外根拠を具体的に書く。

- [ ] **Step 5: deferredとPhase 3-1着手条件を書く**

次を明示する。

```text
51 mapping entriesはAC3-0-5で識別完了したdeferredであり、AC3-8の未実装範囲
Phase 3-1〜3-3は未実装
請求CSV生成、帳票生成、報酬算定、validated readerは未完了
production codec unavailableはPhase 3-0の意図的fail-closed境界
2026-06-29付け旧Phase 3-1〜3-3計画をそのまま実行せず、新しいPhase 3-1計画を作る
```

- [ ] **Step 6: 既知SQLite advisoryを記録する**

`GHSA-2m69-gcr7-jv3q`は新規脆弱性ではなく、`Directory.Build.props`と`docs/open-questions.md`で追跡中の既知抑制であること、解除条件を短く記載する。auditの実測出力と矛盾する場合は受け入れを止める。

---

### Task 6: `CHANGELOG.md`へPhase 3-0の実績を追記する

**Files:**
- Modify: `CHANGELOG.md:7-14`
- Read: `docs/phase3-0-acceptance.md`

- [ ] **Step 1: staleな「Phase 3は出典確定後に着手」を置換する**

`[Unreleased]`へPhase 3-0完了節を追加し、少なくとも次を記載する。

```text
ADR 0020〜0026と再現可能な一次資料catalog
versioned claim master契約とhardcode境界
Tsumugi.Infrastructure.Csvと公式CSV／3帳票の全field mapping
ClaimBatch／ClaimDetailのappend-only永続化土台
```

- [ ] **Step 2: 未実装範囲を同じ節で明記する**

Phase 3-1〜3-3、51件の入力拡張、報酬算定、validated claim finalization、請求CSV／帳票生成は未実装と記載する。「フェーズ3完了」「請求CSV生成完了」とは書かない。

- [ ] **Step 3: 受入文書へのリンクを追加する**

詳細証跡の正本として`docs/phase3-0-acceptance.md`へリンクし、CHANGELOGへテスト数や長いcommit一覧を重複させない。

---

### Task 7: 全品質ゲートを再実行して受入文書を確定する

**Files:**
- Modify: `docs/phase3-0-acceptance.md`
- Read: `build/ci.sh`
- Read: `Directory.Build.props`

- [ ] **Step 1: Markdown差分の形式を確認する**

Run:

```bash
git diff --check -- docs/open-questions.md docs/phase3-0-acceptance.md CHANGELOG.md
```

Expected: 出力なし、exit 0。

- [ ] **Step 2: formatter gateを実行する**

Run:

```bash
dotnet format --verify-no-changes
```

Expected: 変更なし、exit 0。

- [ ] **Step 3: full CIを実行する**

Run:

```bash
./build/ci.sh
```

Expected: `==> CI OK`、全test PASS、Domain line coverage 95%以上、Application line coverage 70%以上。

- [ ] **Step 4: dependency vulnerabilityを監査する**

Run:

```bash
dotnet list package --vulnerable --include-transitive
```

Expected: 新規未抑制脆弱性なし。既知SQLite抑制だけなら`docs/phase3-0-acceptance.md`の記述と一致する。

- [ ] **Step 5: 実測値で受入証跡を更新する**

`docs/phase3-0-acceptance.md`へfull CIの総test数、Domain/Application coverage、各コマンドの結果を転記する。推定値や前回値を残さない。

- [ ] **Step 6: 文書更新後の最終形式確認を行う**

Run:

```bash
git diff --check -- docs/open-questions.md docs/phase3-0-acceptance.md CHANGELOG.md
```

Expected: 出力なし、exit 0。

---

### Task 8: 変更スコープを検証してレビュー候補をstageする

**Files:**
- Modify: `docs/open-questions.md`
- Create: `docs/phase3-0-acceptance.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: working tree全体を確認する**

Run:

```bash
git status --short
git diff --stat
git diff -- docs/open-questions.md docs/phase3-0-acceptance.md CHANGELOG.md
```

Expected: Task 16の意図した文書差分だけを説明できる。`.serena/project.yml`、本計画書、`graphify-out/`は未stageのまま保持する。

- [ ] **Step 2: 3文書だけをstageする**

Run:

```bash
git add docs/open-questions.md docs/phase3-0-acceptance.md CHANGELOG.md
git diff --cached --name-only
```

Expected:

```text
CHANGELOG.md
docs/open-questions.md
docs/phase3-0-acceptance.md
```

- [ ] **Step 3: staged差分の完全性を確認する**

Run:

```bash
git diff --cached --check
git diff --cached
```

Expected: whitespace errorなし。3文書が相互に矛盾せず、Phase 3-1〜3-3を完了扱いしていない。

- [ ] **Step 4: blocked状態のレビュー候補として保持する**

Expected: `docs/phase3-0-acceptance.md`は「最終レビュー待ち」の`❌ blocked`であり、この時点ではcommitしない。

---

### Task 9: Phase 3-0の最終レビューを実施する

**Files:**
- Review: `ec0b4b9..HEAD`の全commit済み変更
- Review: `git diff --cached`のTask 16文書候補
- Review: `docs/phase3-0-acceptance.md`

- [ ] **Step 1: 最終レビュー対象を採取する**

Run:

```bash
git log --reverse --oneline ec0b4b9..HEAD
git diff --stat ec0b4b9..HEAD
git diff --name-status ec0b4b9..HEAD
git diff --cached --stat
git diff --cached --name-status
```

Expected: commit済み範囲にPhase 3-0の計画、ADR、source／spec catalogs、mapping、Domain/Application/Infrastructure/Csv/App wiring、testsが含まれ、cached差分にTask 16の3文書だけが含まれる。

- [ ] **Step 2: AC3-0-1〜5と横断条件をレビューする**

レビュー観点を次へ限定する。

```text
AC3-0-1: 出典、版、適用時期、取得日、SHA、結論、影響
AC3-0-2: master schema、版解決、重複／空白／gap／未知source拒否
AC3-0-3: Csv assembly、依存方向、assembly reference、offline、P/Invoke、URL literal
AC3-0-4: ClaimBatch/Detail、履歴policy、append-only、index、migration、repository、fail-closed finalization
AC3-0-5: 全fieldIdの一意分類、3帳票、51 missing entries、性別推測なし
横断: format、CI、coverage、vulnerability、停止条件
```

- [ ] **Step 3: Critical／High／Medium 0件を確認する**

Expected: 0件ならPhase 3-0を受け入れ可能とする。1件でもあれば`❌ blocked`を維持し、Task 16を停止する。

- [ ] **Step 4: 指摘がある場合は別タスクへ切り出す**

コード、ADR、seed、migration、テストの修正が必要ならTask 16を停止し、受入状態を`❌ blocked`のまま保持する。修正は別計画・別タスク・別commitで実施し、その着地後にTask 1から証跡採取と最終レビューをやり直す。

- [ ] **Step 5: 合格時だけ受入状態を確定する**

Critical／High／Medium 0件の場合に限り、`docs/phase3-0-acceptance.md`を`✅ accepted`へ変更し、レビュー実施日、レビュー対象HEAD、結果0件を記録する。

- [ ] **Step 6: accepted変更をstageして最終差分を確認する**

Run:

```bash
git add docs/phase3-0-acceptance.md
git diff --cached --check
git diff --cached --name-only
git diff --cached
```

Expected: staged対象は`CHANGELOG.md`、`docs/open-questions.md`、`docs/phase3-0-acceptance.md`の3件だけ。3文書が`✅ accepted`とレビュー0件で整合し、Phase 3-1〜3-3を完了扱いしていない。

- [ ] **Step 7: 文書コミットを作成する**

Run:

```bash
git commit -m 'docs(phase3-0): record AC3-0 acceptance evidence'
```

Expected: 最終レビュー合格後に、3文書だけを含むcommitが作成される。

- [ ] **Step 8: Phase 3-1への引継ぎを固定する**

Phase 3-0受け入れ後は、既存計画を延長せず、AC3-1／2／3／4／8／9だけを対象に新しいPhase 3-1設計・実装計画を作成する。Task 16の完了はPhase 3-1実装の開始を意味しない。
