# Phase 3-1 Task 13 Protected Facility B Formula and Source Inventory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基準該当就労継続支援B型44 source rowsについて、公式式、比較対象、地方公共団体補正、保護施設事務費入力要件及びprovenanceをclosed contractで表現し、Task 13 source inventoryを44 documents／61 ranges／14,726 rows／0 schema-gapへ確定する。

**Architecture:** 既存formula unionへ`protected-facility-benchmark-minimum`を追加し、任意ASTを導入せず制度定数をDomain、JSON Schema、validatorで三重に固定する。既存14,718-row監査結果へ対象44 rowsの決定overlayを適用し、期間別8 evidence rowsを末尾追加する。保護施設事務費実値、production seed転記、resolver及びruntime calculatorは別Taskに残す。

**Tech Stack:** .NET 10、C# 14 records、System.Text.Json、JSON Schema draft 2020-12、Python 3、openpyxl、xUnit、FluentAssertions、jq、curl、Poppler

---

## 実行契約

- 実行worktreeは`/Users/hiro/Projetct/GitHub/Tsumugi/.worktrees/phase3-1-task13-schema-v2-plan`に固定する。
- 設計正本は`docs/superpowers/specs/2026-07-14-phase3-1-task13-protected-facility-b-formula-and-source-inventory-design.md`である。
- 各production変更は`@superpowers:test-driven-development`で、対応testのRed確認後に行う。
- 公式source取得、SHA-256一致、locator到達性又は14,718 decision coverageが1件でも失敗したらrepositoryを変更せず停止する。
- 現在の未コミット`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`は承認済みRed gateとしてTask 6まで保持する。別の変更へ混ぜてcommitしない。
- `basic-rewards.json`、`additions.json`、`region-unit-prices.json`、`burden-caps.json`、`transition-rules.json`及び`service-codes.json`のproduction値は変更しない。44 `productionTargets`は後続のconditional seed phaseで使用する計画revision identityであり、本planではseed算定完了を主張しない。
- `source-catalog.schema.json`、database、migration、View、ViewModel、resolver、calculator、ClaimBatch、帳票及びCSVは変更しない。
- source原本、receipt及びdecision JSONLは`/tmp/tsumugi-phase31-task13/`だけへ置き、gitへ追加しない。
- commit、push、mergeは別権限である。plan内commitは実装sliceのローカルcheckpointであり、push又はmergeを行わない。
- 完了直前に`@superpowers:verification-before-completion`、最終差分に`@superpowers:requesting-code-review`を使用する。

## ファイル構成

### Modify — production contract

- `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs` — 5 source supports、formula基底refactor、新しいclosed formula records。
- `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json` — 第3 formula modeと固定nested shape。
- `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs` — 新mode parse、定数／step／rounding／condition／source coverage validation。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json` — 3公式documentsとrelease source集合。source catalog versionは1のまま。

### Modify — audit data and tooling

- `build/phase3_task13_manifest_v2.py` — 5 supports、44-row overlay、3 documents／8 ranges／8 rowsの決定的finalizer。
- `build/tests/test_phase3_task13_manifest_v2.py` — finalizerのbaseline拒否、identity保存、期間mapping及び固定件数。
- `docs/spec-data/phase3/claim-master-source-row-manifest.json` — 14,718-row監査結果、44 target mappings、8 period-specific evidence rows。

### Modify — tests

- `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs` — Domain union、固定値、既存mode回帰。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs` — schema／validator／source coverage／462841・462842代表fixture。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs` — catalog 66 sources、release source IDs、embedded schema。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs` — 44 documents、61 ranges、14,726 rows、0 gap、identity digest、HTML locator、44 targets、8 evidence rows。

### Modify — normative docs

- `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md` — 462841／462842 fixture置換。
- `docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md` — 旧41／53／14,718 gateの置換。
- `docs/decisions/0020-claim-master-sources-and-versioning.md` — 新規source IDsとrelease bundles。
- `docs/decisions/0025-claim-rounding-rules.md` — 新しい3 calculation step IDsとrounding matrix。

## 固定識別子

```text
Formula mode:
  protected-facility-benchmark-minimum

Source supports:
  unit-rule-formula
  unit-rule-comparison
  unit-rule-local-government-adjustment
  unit-rule-runtime-input
  unit-rule-runtime-input-provenance

Calculation steps:
  claim.step.units.service-code.protected-facility-formula.v1
  claim.step.units.service-code.protected-facility-local-government-benchmark.v1
  claim.step.units.service-code.protected-facility-minimum.v1

Rounding:
  claim.rounding.units.half-up.v1

Runtime input policy:
  protected-facility-administrative-expense-yen
  claim.input.protected-facility-administrative-expense.v1
```

---

### Task 1: Source・監査baselineをhard gateで再検証する

**Files:**

- Read: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `/tmp/tsumugi-phase31-task13/acquisition.jsonl`
- Read: `/tmp/tsumugi-phase31-task13/decisions-blocked-14718/decision-001.jsonl`
- Temporary: `/tmp/tsumugi-phase31-task13/protected-facility-sources/`
- Preserve: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: worktreeとRed gateの開始状態を記録する**

Run:

```bash
mkdir -p /tmp/tsumugi-phase31-task13
git rev-parse HEAD | tee /tmp/tsumugi-phase31-task13/implementation-base.txt
git status --short
git diff -- tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs \
  | shasum -a 256
```

Expected: HEADは設計commit以降、同じcommit IDが`implementation-base.txt`へ保存される。statusはRed gate 1 fileだけ、diff SHA-256は`f07c3d95410decc5242eecf74b430a56590059cb7e1d2162f7cf9e1076c320ac`。異なる場合は停止して差分所有者を確認する。

- [ ] **Step 2: 既存41-document receiptを検証する**

Run:

```bash
jq -e -s '
  length == 41
  and all(.[];
    .result == "PASS"
    and .expectedSha256 == .actualSha256
    and .bytes > 0)
' /tmp/tsumugi-phase31-task13/acquisition.jsonl
```

Expected: exit 0。receipt欠落又は不一致なら、既存Task 13 plan Task 2の取得手順で41 documentsを再取得してから再実行する。

- [ ] **Step 3: 新規3 documentsを2回取得する**

Run:

```bash
mkdir -p /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-1 \
  /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-2
for pass in 1 2; do
  curl -fL --silent --show-error \
    'https://www.mhlw.go.jp/web/t_doc?dataId=83aa8477&dataType=0&pageNo=6' \
    -o "/tmp/tsumugi-phase31-task13/protected-facility-sources/pass-$pass/current-fee-notice-html.html"
  curl -fL --silent --show-error \
    'https://www.mhlw.go.jp/web/t_doc?dataId=00tc7589&dataType=1' \
    -o "/tmp/tsumugi-phase31-task13/protected-facility-sources/pass-$pass/protected-facility-administrative-expense-standard-html.html"
  curl -fL --silent --show-error \
    'https://www.mhlw.go.jp/content/000520560.pdf' \
    -o "/tmp/tsumugi-phase31-task13/protected-facility-sources/pass-$pass/h31-fee-notice-consolidated.pdf"
done
```

Expected: 6 non-empty files。

- [ ] **Step 4: 新規3 documentsのbytesとSHAを固定値へ照合する**

Run:

```bash
shasum -a 256 /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-*/*
wc -c /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-*/*
cmp /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-1/current-fee-notice-html.html \
  /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-2/current-fee-notice-html.html
cmp /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-1/protected-facility-administrative-expense-standard-html.html \
  /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-2/protected-facility-administrative-expense-standard-html.html
cmp /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-1/h31-fee-notice-consolidated.pdf \
  /tmp/tsumugi-phase31-task13/protected-facility-sources/pass-2/h31-fee-notice-consolidated.pdf
```

Expected:

```text
current-fee-notice-html:
  sha256 0b5c75203f589701e8d0d3ba7cf192f4873b7aeae023da6e137882b225286768
  bytes  173638
protected-facility-administrative-expense-standard-html:
  sha256 e6d94b5279ca33d60daa83f29e6fdb1f5c3d1ba08f076812cf2c0f64a37ba8a5
  bytes  361844
h31-fee-notice-consolidated:
  sha256 79054870b88b1ca97b3b31a811857ed8a614e59da0b6d14435df30bcb5bf4bc9
  bytes  321200
```

全`cmp`はexit 0。1 byteでも異なる場合はcatalog又はmanifestを変更せず停止する。

- [ ] **Step 5: 14,718 decision coverageを再適用する**

Run:

```bash
test "$(jq -s 'length' /tmp/tsumugi-phase31-task13/decisions-blocked-14718/*.jsonl)" = 14718
python3 build/phase3_task13_manifest_v2.py apply \
  --manifest docs/spec-data/phase3/claim-master-source-row-manifest.json \
  --decision-dir /tmp/tsumugi-phase31-task13/decisions-blocked-14718 \
  --output /tmp/tsumugi-phase31-task13/manifest-v2-blocked-reverified.json
jq -e '
  (.rows | length) == 14718
  and ([.rows[] | select(.disposition == "seed")] | length) == 14137
  and ([.rows[] | select(.disposition == "excluded")] | length) == 537
  and ([.rows[] | select(.disposition == "schema-gap")] | length) == 44
' /tmp/tsumugi-phase31-task13/manifest-v2-blocked-reverified.json
```

Expected: 全command exit 0。repository変更なし。

---

### Task 2: Domain formula contractをTDDで追加する

**Files:**

- Modify: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs`
- Modify: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`

- [ ] **Step 1: 5 source supportsのRed testを書く**

```csharp
ClaimSourceSupport[] supports =
[
    ClaimSourceSupport.UnitRuleFormula,
    ClaimSourceSupport.UnitRuleComparison,
    ClaimSourceSupport.UnitRuleLocalGovernmentAdjustment,
    ClaimSourceSupport.UnitRuleRuntimeInput,
    ClaimSourceSupport.UnitRuleRuntimeInputProvenance,
];
supports.Should().OnlyHaveUniqueItems();
```

- [ ] **Step 2: 新formula modeのRed testを書く**

完全な`ProtectedFacilityBenchmarkMinimumRule`を生成し、次をassertする。

```csharp
rule.BillingUnit.Should().Be(BillingUnit.PerDay);
rule.RuntimeInputRequirement.Key.Should()
    .Be("protected-facility-administrative-expense-yen");
rule.StatutoryFormula.Should().Be(new ProtectedFacilityStatutoryFormula(
    22, 0.945m, 10, 23, 1.046m,
    "claim.step.units.service-code.protected-facility-formula.v1",
    "claim.rounding.units.half-up.v1"));
rule.Benchmark.OfficialSection.Should().Be("b-type-service-fee-ii");
rule.Benchmark.LocalGovernmentAdjustment.Target.Should().Be("comparison-only");
rule.Selection.Kind.Should().Be("minimum");
rule.Selection.RoundingRuleId.Should().BeNull();
rule.Factors.Should().BeSameAs(factors);
```

同じtestで既存`BaseComponentPassThroughRule`と`FactorChainRule`が`BaseComponentKey`を保持し続けることをassertする。

- [ ] **Step 3: focused Domain testを実行してRedを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimCalculationMasterContractTests \
  -v minimal
```

Expected: compile FAIL because新enum values／recordsが存在しない。

- [ ] **Step 4: formula基底と既存subtypeを最小refactorする**

```csharp
public abstract record FormulaUnitRule(BillingUnit BillingUnit)
    : ServiceCodeUnitRule(BillingUnit);

public sealed record BaseComponentPassThroughRule(
    string BaseComponentKey,
    string CalculationStepId,
    string? RoundingRuleId,
    BillingUnit BillingUnit) : FormulaUnitRule(BillingUnit);

public sealed record FactorChainRule(
    string BaseComponentKey,
    IReadOnlyList<ServiceCodeFormulaFactor> Factors,
    BillingUnit BillingUnit) : FormulaUnitRule(BillingUnit);
```

既存2 subtypeのJSON shape、step、rounding又は意味を変更しない。

- [ ] **Step 5: nested formula recordsを追加する**

```csharp
public sealed record ProtectedFacilityAdministrativeExpenseRequirement(
    string Key,
    string ValueKind,
    string ValueUnit,
    string Scope,
    string AsOfPolicy,
    string ProvenancePolicyId);

public sealed record ProtectedFacilityStatutoryFormula(
    int DaysDivisor,
    decimal ExpenseAdjustmentDivisor,
    int UnitPriceDivisorYen,
    int FixedAdditionUnits,
    decimal UpliftRate,
    string CalculationStepId,
    string RoundingRuleId);

public sealed record ProtectedFacilityLocalGovernmentAdjustment(
    string ConditionSelector,
    decimal Rate,
    string Target,
    string CalculationStepId,
    string RoundingRuleId);

public sealed record ProtectedFacilityBenchmark(
    string OfficialSection,
    string BasicRewardStaffingKey,
    string PaymentBandMatch,
    string CapacityMatch,
    ProtectedFacilityLocalGovernmentAdjustment LocalGovernmentAdjustment);

public sealed record ProtectedFacilityMinimumSelection(
    string Kind,
    string CalculationStepId,
    string? RoundingRuleId);
```

- [ ] **Step 6: top-level ruleを固定値検証付きで追加する**

`ProtectedFacilityBenchmarkMinimumRule`は明示constructorを使い、次の全条件を満たさない引数を`ArgumentException`又は`ArgumentOutOfRangeException`で拒否する。

```text
BillingUnit                         = PerDay
RuntimeInputRequirement            = 設計書§7.3の6 fixed strings
StatutoryFormula                   = 22 / 0.945 / 10 / 23 / 1.046 / fixed step / half-up
Benchmark                          = b-type-service-fee-ii / same band / same capacity
LocalGovernmentAdjustment          = municipality-ownership:local-government / 0.965 / comparison-only / fixed step / half-up
Selection                          = minimum / fixed step / null rounding
Factors                            = non-null; empty allowed
```

任意式、任意定数又は`BaseComponentKey`をpropertyへ追加しない。

- [ ] **Step 7: focused Domain testをGreenにする**

Run: Step 3と同じ。

Expected: PASS、0 failures。

- [ ] **Step 8: Domain sliceをcommitする**

```bash
git add \
  src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs
git commit -m "feat(phase3-1): model protected facility formula contract"
```

---

### Task 3: JSON Schemaのclosed modeをTDDで追加する

**Files:**

- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`

- [ ] **Step 1: embedded schemaのRed assertionsを書く**

`Embedded_schema_resources_express_the_runtime_contract`へ次を追加する。

```csharp
var formulaRule = definitions.GetProperty("formulaRule");
formulaRule.GetProperty("oneOf").GetArrayLength().Should().Be(3);

var protectedRule = definitions
    .GetProperty("protectedFacilityBenchmarkMinimumRule");
Required(protectedRule).Should().Equal(
    "kind", "mode", "runtimeInputRequirement", "statutoryFormula",
    "benchmark", "selection", "factors", "billingUnit");
protectedRule.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
```

`sourceSupport.enum`に5 tokensが存在することもassertする。

- [ ] **Step 2: nested constのRed assertionsを書く**

同じembedded schema testで6 nested definitionsの`required`、`additionalProperties = false`及び次の`const`値を直接assertする。

```text
runtime input requirement: key / valueKind / valueUnit / scope / asOfPolicy / provenancePolicyId
statutory formula: 22 / "0.945" / 10 / 23 / "1.046" / fixed step / half-up
benchmark: b-type-service-fee-ii / same-average-wage-band / same-capacity-band
local adjustment: municipality-ownership:local-government / "0.965" / comparison-only / fixed step / half-up
selection: minimum / fixed step / null rounding
billingUnit: per-day
```

- [ ] **Step 3: focused embedded schema testを実行してRedを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_schema_resources_express_the_runtime_contract \
  -v minimal
```

Expected: FAIL because第3 modeと5 supportsがschemaにない。

- [ ] **Step 4: source support enumを拡張する**

`$defs.sourceSupport.enum`へ次だけを追加する。

```json
"unit-rule-formula",
"unit-rule-comparison",
"unit-rule-local-government-adjustment",
"unit-rule-runtime-input",
"unit-rule-runtime-input-provenance"
```

- [ ] **Step 5: 6 nested closed definitionsを追加する**

各objectを`additionalProperties: false`とし、設計書§7.3の文字列、整数及びdecimal stringを`const`で固定する。decimalはJSON numberへ変換せず、既存canonical decimal string方針に従い`"0.945"`、`"1.046"`及び`"0.965"`を保持する。

- [ ] **Step 6: 第3 formula ruleをoneOfへ追加する**

```json
"formulaRule": {
  "oneOf": [
    { "$ref": "#/$defs/baseComponentPassThroughRule" },
    { "$ref": "#/$defs/factorChainRule" },
    { "$ref": "#/$defs/protectedFacilityBenchmarkMinimumRule" }
  ]
}
```

新modeの`factors`は`formulaFactor` itemsを再利用し、`minItems`を設定しない。

- [ ] **Step 7: focused schema testsをGreenにする**

Run: Step 3と同じ。

Expected: PASS、0 failures。

- [ ] **Step 8: JSON Schema sliceをcommitする**

```bash
git add \
  src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs
git commit -m "feat(phase3-1): add protected facility formula schema"
```

---

### Task 4: Runtime validatorと代表fixturesをTDDで実装する

**Files:**

- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`

- [ ] **Step 1: 462841の新mode Red fixtureを書く**

既存`service-pass-through / workbook-order=38;row=907 / 462841`を、設計書§7.3と同じ`protected-facility-benchmark-minimum`、空`factors`、空`componentRefs`へ置換する。source refsはsupportごとの別documentを使う。

```text
r6-service-codes-2-xlsx:
  service-identity, selectors, unit-rule-kind, conditions, effective-period
current-fee-notice-html:
  unit-rule-formula, unit-rule-comparison,
  unit-rule-local-government-adjustment, unit-rule-runtime-input
protected-facility-administrative-expense-standard-html:
  unit-rule-runtime-input-provenance
r6-calculation-note:
  unit-rule-step, unit-rule-rounding
h31-fee-notice-consolidated:
  cross-check only
```

- [ ] **Step 2: 462842と2-factor Red fixturesを書く**

`workbook-order=38;row=908 / 462842`はplan-not-created factor `0.7`を1件持つ新modeにする。別fixtureで人員欠如`0.7`をorder 1、計画未作成`0.5`をorder 2にし、各factorが既存percentage stepとhalf-upを保持することをassertする。

- [ ] **Step 3: validator negative Red casesを書く**

次を個別mutationで拒否する。

```text
固定定数／IDの不一致
new modeにbaseComponentKey又はcomponentRefs追加
local condition definition欠落又は期間不足
factor orderの穴／重複
factor selectorがentry conditionSelectorsのsubsetでない
factorなしで5 new supportsのどれか欠落
factorありでunit-rule-value/target/step/rounding欠落
current authoritative sourceなしでH31 cross-checkだけ存在
```

- [ ] **Step 4: focused validator testsを実行してRedを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests.Load_reads_representative_gap_rows_into_typed_unit_rules|FullyQualifiedName~ClaimMasterSchemaPhase31Tests.Load_accepts_signed_proration_and_pass_through_boundary_fixtures|FullyQualifiedName~ClaimMasterSchemaPhase31Tests.Load_rejects_invalid_protected_facility' \
  -v minimal
```

Expected: FAIL becausevalidatorが新mode／supportsを解釈できない。

- [ ] **Step 5: support token parseを実装する**

`ParseSupport`、`SupportToken`及びDomain enumの対応を5件追加し、未知tokenのfail-closeを維持する。

- [ ] **Step 6: new mode parserを実装する**

`ParseFormulaRule`へ第3branchを追加する。各nested objectで`RequireProperties`を呼び、設計書§7.3の値と完全一致しなければfield名付き`InvalidDataException`を投げる。`factors`は0件を許可し、存在するfactorは既存`ParseFactor`を通す。

- [ ] **Step 7: source coverage matrixを実装する**

`ParseServiceCode`のswitchへ新rule caseを追加し、次をrequired supportsへ加える。

```csharp
ClaimSourceSupport.UnitRuleFormula
ClaimSourceSupport.UnitRuleComparison
ClaimSourceSupport.UnitRuleLocalGovernmentAdjustment
ClaimSourceSupport.UnitRuleRuntimeInput
ClaimSourceSupport.UnitRuleRuntimeInputProvenance
ClaimSourceSupport.UnitRuleStep
ClaimSourceSupport.UnitRuleRounding
```

`Factors.Count > 0`の場合だけ既存`UnitRuleValue`と`UnitRuleTarget`も要求する。

- [ ] **Step 8: reference validationをsubtype別にする**

`case FormulaUnitRule formula: ValidateBaseComponent(...)`を廃止し、既存2 subtypeだけをbase component検証へ渡す。新modeは`componentRefs`空を要求し、local adjustmentのcondition selectorがservice期間全体で既知condition definitionに解決することを検証する。factor条件は従来どおりentry条件のsubsetとする。

- [ ] **Step 9: focused validator testsをGreenにする**

Run: Step 4と同じ。

Expected: PASS、0 failures。462841／462842は旧pass-through／factor-chainとして受理されない。

- [ ] **Step 10: schema／validator回帰を実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_schema_resources_express_the_runtime_contract' \
  -v minimal
```

Expected: PASS、0 failures。

- [ ] **Step 11: validator sliceをcommitする**

```bash
git add \
  src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs
git commit -m "feat(phase3-1): validate protected facility formula rules"
```

---

### Task 5: Source catalogとrelease bundlesをTDDで拡張する

**Files:**

- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`

- [ ] **Step 1: catalog 66-source Red testを書く**

`ExpectedSourceIds`の末尾へ次を順番に追加し、embedded catalogが全66 IDsを同順で保持することを要求する。

```text
current-fee-notice-html
protected-facility-administrative-expense-standard-html
h31-fee-notice-consolidated
```

各sourceのURL、SHA-256、`retrievedAt = 2026-07-14`、null relation及び非空`applicabilityNote`を個別assertする。

- [ ] **Step 2: release source集合のRed testを書く**

```text
claim-master-r6-04 / r6-06 / r7-01 / r7-09:
  current-fee-notice-html
  protected-facility-administrative-expense-standard-html
  h31-fee-notice-consolidated

claim-master-r8-06:
  current-fee-notice-html
  protected-facility-administrative-expense-standard-html
```

H31をR8 source listへ追加しない。

- [ ] **Step 3: focused provider testを実行してRedを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_source_catalog|FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_release' \
  -v minimal
```

Expected: FAIL because3 sourcesとrelease refsが未登録。

- [ ] **Step 4: 3 source recordsをappendする**

source順は既存63 recordsを保持して末尾appendとする。URLとSHAはTask 1固定値を使い、`supersedes`、`corrects`、`supplements`、`correctionNote`はnullとする。

```text
current-fee-notice-html:
  publisher 厚生労働省
  effectiveAt 2026-06-01
  publishedAt null
  applicabilityNote 現行統合本文を式・比較・地方公共団体補正・4月1日入力要件の正本とし、R6対応はcontinuity mappingReasonを必須とする

protected-facility-administrative-expense-standard-html:
  publisher 厚生労働省
  effectiveAt 2023-04-01
  publishedAt 2023-03-28
  applicabilityNote 保護施設事務費の施設別・地域別・定員別・通知provenanceに使用する

h31-fee-notice-consolidated:
  publisher 厚生労働省
  effectiveAt 2019-04-01
  publishedAt null
  applicabilityNote R6式継続性のcross-checkだけに使用し有効正本candidateへ含めない
```

- [ ] **Step 5: 5 release listsを更新する**

既存source ID順を変えず各release末尾へ上記の期間別集合を追加する。source catalog schema versionは`1`のままにする。

- [ ] **Step 6: focused provider testsをGreenにする**

Run: Step 3と同じ。

Expected: PASS、0 failures。

- [ ] **Step 7: catalog sliceをcommitする**

```bash
git add \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs
git commit -m "docs(phase3-1): register protected facility formula sources"
```

---

### Task 6: 44-row overlayと8 evidence rowsでmanifestをGreenにする

**Files:**

- Modify: `build/tests/test_phase3_task13_manifest_v2.py`
- Modify: `build/phase3_task13_manifest_v2.py`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Modify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `/tmp/tsumugi-phase31-task13/manifest-v2-blocked-reverified.json`
- Read: `/tmp/tsumugi-phase31-task13/sources/r6-service-codes-2-xlsx.xlsx`
- Read: `/tmp/tsumugi-phase31-task13/sources/r8-service-codes-2-xlsx.xlsx`

- [ ] **Step 1: Python finalizerのRed testsを書く**

次を個別testで固定する。

```text
baseline identity SHA != 90fb... なら拒否
baseline 14,718 / 14,137 / 537 / 44以外なら拒否
44 gap identityが設計書§10.1の3 range集合以外なら拒否
既存14,718 identitiesが最終rowsの順序付き部分列
final documents 44 / ranges 61 / rows 14,726
final seed 14,189 / excluded 537 / schema-gap 0
final ordered identity SHA c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7
8 evidence rowsが単一期間22 targetsだけを持つ
R6 evidenceがR8 targetを持たず、R8 evidenceがR6 targetを持たない
```

- [ ] **Step 2: manifest C# Red testsを最終gateへ更新する**

既存未コミット`Source_manifest_v2_has_no_schema_gaps_before_seed_transcription`を保持し、次を追加／更新する。

```csharp
documents.Should().HaveCount(44);
ranges.Should().HaveCount(61);
rows.Should().HaveCount(14_726);
rows.Count(IsSeed).Should().Be(14_189);
rows.Count(IsExcluded).Should().Be(537);
rows.Should().NotContain(IsSchemaGap);
CalculateOrderedIdentityDigest(rows).Should().Be(
    "c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7");
```

row uniquenessは`documentId + rangeId + sourceLocator`で比較する。旧`documentId + sourceLocator`比較は、期間別HTML logical rowsを誤って重複扱いするため置換する。

- [ ] **Step 3: Redを実行する**

Run:

```bash
python3 -m unittest build.tests.test_phase3_task13_manifest_v2 -v
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest \
  -v minimal
```

Expected: Pythonはfinalizer未実装、C#は41／53／14,718及び13,959 gapsでFAIL。

- [ ] **Step 4: converterのclosed supportsを拡張する**

`ALLOWED_SUPPORTS`へ5 tokensを追加する。既存10 tokens、role、target fields及びdecision identity contractは変更しない。

- [ ] **Step 5: `finalize-protected-facility` commandを実装する**

CLI:

```bash
python3 build/phase3_task13_manifest_v2.py finalize-protected-facility \
  --manifest /tmp/tsumugi-phase31-task13/manifest-v2-blocked-reverified.json \
  --source-catalog src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json \
  --output /tmp/tsumugi-phase31-task13/manifest-v2-protected-facility-final.json
```

commandは次だけを決定的に行う。

1. baseline件数とSHAを検証する。
2. 対象44 identitiesを次の順で`seed`へ変換し、`service-code-{officialCode}` primary targetを付与する。
3. R6 continuity targetへ非空`mappingReason`を付与する。
4. R6／R8計算留意事項の既存period rowへ、同期間targetの`unit-rule-step`／`unit-rule-rounding` supporting mappingを追加する。
5. R8告示physical page 56をR8 targetsの`cross-check` mappingにする。
6. 3 documents、8 extraction ranges及び8 rowsを設計書§11.2順で追加する。
7. final件数、単一期間mapping、subsequence及びidentity SHAを再検証してatomic writeする。

対象code順:

```text
462841 462842 462843 462844 462845 462846
46C841 46C842 46C843 46C844 46C845 46C846
46D841 46D842 46D843 46D844 46D845 46D846
46E841 46E844 46F841 46F844
```

R6 locators:

```text
workbook-order=38;row=907..912
workbook-order=40;row=1807..1818
workbook-order=41;row=607..610
```

R8 locators:

```text
workbook-order=38;row=1987..1992
workbook-order=40;row=3967..3978
workbook-order=41;row=1327..1330
```

上記codeはrow順から生成せず、Task 1でSHA検証した各XLSXのservice code cellと一致することを別verificationで要求する。

- [ ] **Step 6: `html-lines` range contractを実装する**

extraction rangeは次のclosed shapeとする。

```json
{
  "rangeId": "r8-protected-facility-b-current-consolidated",
  "kind": "html-lines",
  "lineFrom": 2791,
  "lineTo": 2793,
  "expectedItemCount": 1
}
```

locator grammarは`html:lines=l<digits>(-l<digits>)?(,l<digits>(-l<digits>)?)*`だけを許可し、全lineがrange内かつ取得bytesの正規化行で一意に存在することをC# testとphysical auditで検証する。

- [ ] **Step 7: 公式XLSXの44 code cellsを照合する**

workspace Pythonの`openpyxl`で2 workbooksをread-onlyで開き、上記44 locatorのservice code cellを抽出する。各releaseでcode配列が上記22 codesとordinal一致し、factor columnsのOOXML列順が人員欠如→計画未作成であることをassertする。

Expected: R6 22件、R8 22件、差異0。失敗時はfinalizer outputをrepositoryへ反映しない。

- [ ] **Step 8: finalizerを実行して固定集計を確認する**

Run:

```bash
python3 build/phase3_task13_manifest_v2.py finalize-protected-facility \
  --manifest /tmp/tsumugi-phase31-task13/manifest-v2-blocked-reverified.json \
  --source-catalog src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json \
  --output /tmp/tsumugi-phase31-task13/manifest-v2-protected-facility-final.json
jq -e '
  (.documents | length) == 44
  and ([.documents[].extractionRanges[]] | length) == 61
  and (.rows | length) == 14726
  and ([.rows[] | select(.disposition == "seed")] | length) == 14189
  and ([.rows[] | select(.disposition == "excluded")] | length) == 537
  and ([.rows[] | select(.disposition == "schema-gap")] | length) == 0
' /tmp/tsumugi-phase31-task13/manifest-v2-protected-facility-final.json
```

Expected: exit 0。

- [ ] **Step 9: verified outputをrepository manifestへ反映する**

`apply_patch`で`docs/spec-data/phase3/claim-master-source-row-manifest.json`をverified outputと同一内容へ置換する。`cp`、shell redirect又は生成原本のgit追加を行わない。

- [ ] **Step 10: Python／manifest testsをGreenにする**

Run:

```bash
python3 -m unittest build.tests.test_phase3_task13_manifest_v2 -v
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest \
  -v minimal
jq empty docs/spec-data/phase3/claim-master-source-row-manifest.json
git diff --check
```

Expected: 全tests PASS、JSON／diff check exit 0。

- [ ] **Step 11: manifest sliceをcommitする**

```bash
git add \
  build/phase3_task13_manifest_v2.py \
  build/tests/test_phase3_task13_manifest_v2.py \
  docs/spec-data/phase3/claim-master-source-row-manifest.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "fix(phase3-1): close protected facility schema gaps"
```

---

### Task 7: Normative documentsを新gateへ同期する

**Files:**

- Modify: `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`
- Modify: `docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md`
- Modify: `docs/decisions/0020-claim-master-sources-and-versioning.md`
- Modify: `docs/decisions/0025-claim-rounding-rules.md`

- [ ] **Step 1: Task 12 fixturesを同期する**

§15.4の`462841 / row=907`をpass-through、`462842 / row=908`をbase-component factor-chainとする旧記述を削除し、両方が`protected-facility-benchmark-minimum`であること、462842だけがplan factorを持つことへ更新する。他の代表fixtureは変更しない。

- [ ] **Step 2: Task 13 audit gateを同期する**

旧41 documents／53 ranges／14,718 rows／ordered SHAを、本follow-up対象では次へ置換する。

```text
44 documents
61 ranges
14,726 rows
14,189 seed
537 excluded
0 schema-gap
c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7 ordered identity SHA-256
```

既存14,718 identitiesが順序付き部分列であることも記録する。

- [ ] **Step 3: ADR 0020を同期する**

3 source records、R6系4 releasesの3 IDs、R8 releaseの2 IDs及び公式文書のauthority／continuity境界を追加する。R8告示を`corrects`へ変更せずcross-checkとする。

- [ ] **Step 4: ADR 0025を同期する**

closed matrixへ次を追加する。

| calculationStepId | roundingRuleId |
| --- | --- |
| `claim.step.units.service-code.protected-facility-formula.v1` | `claim.rounding.units.half-up.v1` |
| `claim.step.units.service-code.protected-facility-local-government-benchmark.v1` | `claim.rounding.units.half-up.v1` |
| `claim.step.units.service-code.protected-facility-minimum.v1` | null／RoundingPolicy呼出しなし |

post-min factorsは既存`claim.step.units.per-service-code.percentage.v1`を逐次使用し、各factor直後にhalf-upすることを明記する。

- [ ] **Step 5: stale gateを検索する**

Run:

```bash
rg -n '41 documents|53 ranges|14,718 rows|90fb9d309e878d22f0d4bb867c4fe36c3fab83ad45938b64da2d5b3bfd34dee7|462841.*base-component-pass-through|462842.*factor-chain' \
  docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md \
  docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md
```

Expected: 本follow-up以前の履歴説明以外に現行gateとしてのmatchなし。

- [ ] **Step 6: normative docsをcommitする**

```bash
git add \
  docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md \
  docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md \
  docs/decisions/0020-claim-master-sources-and-versioning.md \
  docs/decisions/0025-claim-rounding-rules.md
git commit -m "docs(phase3-1): sync protected facility formula contract"
```

---

### Task 8: 全範囲を検証し独立reviewで閉じる

**Files:**

- Verify: Task 2–7の全変更path
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`

- [ ] **Step 1: Python tool testsをfreshに実行する**

```bash
python3 -m unittest build.tests.test_phase3_task13_manifest_v2 -v
```

Expected: PASS、0 failures／errors。

- [ ] **Step 2: Domain／Infrastructure focused testsをfreshに実行する**

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimCalculationMasterContractTests \
  -v minimal
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest' \
  -v minimal
```

Expected: 2 commandsともPASS、0 failures。

- [ ] **Step 3: final manifest invariantsを再計算する**

```bash
jq -e '
  (.documents | length) == 44
  and ([.documents[].extractionRanges[]] | length) == 61
  and (.rows | length) == 14726
  and ([.rows[] | select(.disposition == "seed")] | length) == 14189
  and ([.rows[] | select(.disposition == "excluded")] | length) == 537
  and ([.rows[] | select(.disposition == "schema-gap")] | length) == 0
' docs/spec-data/phase3/claim-master-source-row-manifest.json
jq -c '.rows[] | [.sourceDocumentId,.rangeId,.sourceLocator]' \
  docs/spec-data/phase3/claim-master-source-row-manifest.json \
  | shasum -a 256
```

Expected: jq exit 0、SHA `c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7`。

- [ ] **Step 4: production seed非変更を確認する**

Task 1で保存した実装開始commitを読み込んで実行する。

```bash
IMPLEMENTATION_BASE=$(< /tmp/tsumugi-phase31-task13/implementation-base.txt)
git diff --exit-code "$IMPLEMENTATION_BASE"..HEAD -- \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json
```

Expected: exit 0、diffなし。`sources.json`はcatalog変更対象なのでこのcheckへ含めない。

- [ ] **Step 5: formattingとpatch hygieneを確認する**

```bash
dotnet format Tsumugi.sln --verify-no-changes
git diff --check
git status --short
```

Expected: formatter／diff check exit 0、status clean。

- [ ] **Step 6: full CIをfreshに実行する**

`@superpowers:verification-before-completion`を読み、次を実行する。

```bash
./build/ci.sh
```

Expected: `CI OK`。

- [ ] **Step 7: independent code reviewを実行する**

`@superpowers:requesting-code-review`へ次を渡す。

```text
Spec:
  docs/superpowers/specs/2026-07-14-phase3-1-task13-protected-facility-b-formula-and-source-inventory-design.md
Review range:
  Task 1の/tmp/tsumugi-phase31-task13/implementation-base.txtに保存したcommit..HEAD
Required focus:
  official formula constants and order
  comparison-only 0.965
  period-specific evidence mapping
  44/61/14726/14189/537/0 invariants
  R6 continuity inference boundary
  no production seed/runtime completion claim
Verification:
  focused tests and fresh CI output
```

Major指摘があれば修正し、影響範囲のRed／Greenとfull CIを再実行する。

- [ ] **Step 8: final scopeを監査する**

```bash
IMPLEMENTATION_BASE=$(< /tmp/tsumugi-phase31-task13/implementation-base.txt)
git log --oneline "$IMPLEMENTATION_BASE"..HEAD
git diff --name-only "$IMPLEMENTATION_BASE"..HEAD
git status --short
```

Expected: 差分は本planのModify一覧だけ、status clean。完了報告では「公式計算契約とsource inventoryを実装」「runtime入力要件を保持」とだけ述べ、保護施設事務費実値のseed転記、resolver又はruntime算定完了を主張しない。
