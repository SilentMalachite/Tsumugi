# Phase 3-1 Task 13 基準該当B型 公式計算契約・Source Inventory設計

**Status:** Design approved; implementation not started
**Date:** 2026-07-14
**Scope:** Task 13 follow-up — 基準該当就労継続支援B型44行のschema-gap解消、公式計算契約、source inventory及びprovenance契約
**Normative dependencies:**

- `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`
- `docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md`
- `docs/decisions/0020-claim-master-sources-and-versioning.md`
- `docs/decisions/0025-claim-rounding-rules.md`

## 1. 結論

基準該当就労継続支援B型の44 source rowsは、既存の`formula / base-component-pass-through`又は`factor-chain`へ近似せず、公式式、比較対象、地方公共団体補正、最小値選択、最小値選択後の減算及び将来入力する保護施設事務費要件を、閉じた`formula / protected-facility-benchmark-minimum`として保持する。

このfollow-upではschema、Domain型、validator、source catalog、source-side manifest及び静的testsまでを実装対象とする。保護施設事務費の実値、実値provenance entity、migration、入力UI、resolver及びruntime算定は実装せず、算定完了を主張しない。

source inventoryは既存14,718 rowsのidentity順序を保持したまま、44 schema-gap rowsを`seed`へ変換し、公式計算契約を裏付ける8 evidence rowsを末尾へ追加する。完了時の固定値は44 documents、61 ranges、14,726 rows、14,189 seed、537 excluded、0 schema-gapとする。

### 1.1 規範の優先関係

本follow-upは、基準該当B型44 rowsとその根拠資料に限り、Task 12設計15.4の`462841`／`462842` formula fixtures及びTask 13設計の41 documents固定、53 ranges固定、14,718 rows固定、ordered identity SHA-256並びに`schema-gap = 0` gateを置き換える。

競合時は本設計を優先し、次を新しいgateとする。

- documents: 44
- ranges: 61
- rows: 14,726
- seed: 14,189
- excluded: 537
- schema-gap: 0
- identity: 既存14,718 identitiesを順序付き部分列として保持し、8 period-specific evidence identitiesだけを追加

旧Task 13設計の14,718-row ordered identity SHA-256を新inventoryへ適用しない。実装時にTask 12設計15.4の`462841`／`462842` fixturesと対応tests、Task 13設計及びADR 0020の固定件数、rangeごとの`expectedItemCount`、閉じたsource集合、version別`SourceDocumentIds`、検証条件及び完了条件を本設計へ同期する。

## 2. 背景とschema-gap

Task 12のformula unionは次の2 modeだけを持つ。

- `base-component-pass-through`: 既存basic reward componentをそのまま使う。
- `factor-chain`: 既存basic reward componentへ条件付き乗率を順番に適用する。

基準該当B型は、施設固有の金額入力から公式式で単位を算出し、別の基本報酬区分に一致する比較単位と比較して小さい方を採用する。地方公共団体設置の場合は比較側だけを96.5%に補正し、その後に計画未作成、従業者欠如又はサービス管理責任者欠如の減算を適用する。

したがって、既存modeでは次を損失なく表現できない。

- 保護施設事務費を将来のruntime入力として要求すること。
- 公式式の定数、計算順序及び丸め境界。
- 同一定員区分・同一平均工賃月額区分の通常B型基本報酬Ⅱを比較対象とすること。
- 地方公共団体補正が比較側だけへ適用されること。
- 公式式側と補正済み比較側の最小値を採用すること。
- 最小値選択後に複数の減算を順番に適用すること。
- 実値入力に将来必要となる施設・年度・決定主体・原本証拠のprovenance要件。

本設計はこの44 rowsに限定してTask 12のformula契約を拡張する。Task 12設計8.3の一般的な`base-component-pass-through`及び`factor-chain`契約は変更しない。

ただし、Task 12設計15.4で旧shapeの代表fixtureとして固定した`r6-service-codes-2-xlsx / workbook-order=38;row=907 / 462841`及び`row=908 / 462842`は、どちらも本設計の対象44 rowsに含まれる。両fixtureを`protected-facility-benchmark-minimum`へ置き換え、`462841`を`base-component-pass-through`、`462842`をbase component付き`factor-chain`として受理する既存testsを更新する。他のformula fixturesは変更しない。

## 3. 目的

1. 基準該当B型の公式式を任意式文字列へ落とさずtyped contractとして保持する。
2. 比較対象、地方公共団体補正及び最小値選択の適用順序を機械検証できるようにする。
3. 保護施設事務費の実値が未入力でも、必要なruntime入力とprovenance要件をmaster上で表現できるようにする。
4. R6及びR8の対象44 source rowsを公式資料へ追跡可能なproduction targetsへ写像する。
5. Task 13 source inventoryの残存44 schema-gapを0にする。

## 4. 非目標

- 保護施設事務費の実値を入力又はseedしない。
- 実値を保存するentity、table、migration又は入力UIを追加しない。
- 証拠文書をrepositoryへ保存しない。
- formula resolver又はruntime calculatorを実装しない。
- 指定月の請求単位が計算可能になったとは主張しない。
- 通常B型基本報酬Ⅱの比較単位をこのfollow-up内でruntime解決しない。
- 地方公共団体設置、定員区分、平均工賃月額区分又は減算条件のruntime factを解決しない。
- 任意式AST、式文字列、任意定数又は利用者定義演算を導入しない。
- 対象44 rows以外のproduction seed値を変更しない。
- `source-catalog.schema.json`をversion 2へ上げない。

## 5. 公式根拠

### 5.1 採用資料

| 資料 | URL | SHA-256 | locator | 本設計での役割 |
| --- | --- | --- | --- | --- |
| 現行報酬告示・基準該当就労継続支援B型 | https://www.mhlw.go.jp/web/t_doc?dataId=83aa8477&dataType=0&pageNo=6 | `0b5c75203f589701e8d0d3ba7cf192f4873b7aeae023da6e137882b225286768` | `l000002791`、`l000002793` | 公式式、比較対象、地方公共団体補正、4月1日時点入力要件の正本 |
| 保護施設事務費の支弁基準 | https://www.mhlw.go.jp/web/t_doc?dataId=00tc7589&dataType=1 | `e6d94b5279ca33d60daa83f29e6fdb1f5c3d1ba08f076812cf2c0f64a37ba8a5` | `l000000054`、`l000000060`–`l000000062` | 1人当たり月額、施設別・地域別・定員別・加算別、決定及び通知のprovenance正本 |
| 平成31年報酬告示統合版 | https://www.mhlw.go.jp/content/000520560.pdf | `79054870b88b1ca97b3b31a811857ed8a614e59da0b6d14435df30bcb5bf4bc9` | physical page 46–47 | 公式式及び比較構造の継続性を確認するcross-check |
| 令和6年報酬告示改正 | https://www.mhlw.go.jp/content/001239565.pdf | `5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54` | physical page 137–138 | R6の比較対象「ロ」、地方公共団体96.5%及び公式式参照のcross-check |
| 令和8年報酬告示改正 | https://www.mhlw.go.jp/content/001684450.pdf | `f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c` | 対象改正頁 | R8期間の告示改正関係を確認する正本／cross-check |
| 令和6年費用算定留意事項 | https://www.mhlw.go.jp/content/001494356.pdf | `958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1` | 基準該当B型及び共通減算の該当頁 | 計算段階及び端数処理の正本 |
| 令和8年費用算定留意事項 | https://www.mhlw.go.jp/content/001705650.pdf | `0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99` | 基準該当B型及び共通減算の該当頁 | R8期間の計算段階及び端数処理の正本 |
| 令和6年サービスコード表2 | https://www.mhlw.go.jp/content/12200000/20241129010.xlsx | `4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82` | B型sheetの対象44行中R6 22行 | service identity、label、selectors、conditions、factor value、factor順序、適用期間の正本 |
| 令和8年サービスコード表2 | https://www.mhlw.go.jp/content/12200000/001696437.xlsx | `307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049` | B型sheetの対象44行中R8 22行 | R8のservice identity、label、selectors、conditions、factor value、factor順序、適用期間の正本 |

SHA-256は取得した未加工bytesに対して計算する。既存catalog登録済み資料のSHAは実装時にcatalog値と一致させ、設計書へ別値を複製しない。

### 5.2 公式文書からの推論境界

令和6年改正告示の基準該当B型式は`（略）`と記載され、定数を直接列挙していない。そのため、R6改正告示だけが式定数を直接示すとは扱わない。

採用関係を次に固定する。

- 現行報酬告示の統合本文を、式定数、比較対象、地方公共団体補正及び4月1日時点入力要件の`authoritative` sourceとする。
- 平成31年統合版の完全な式と令和6年改正告示physical page 137–138を、制度文言の継続性を確認する`cross-check`とする。
- R6対象rowへの写像は、式そのものの改変ではなく継続性に基づく対応であることをmanifestの`mappingReason`へ明記する。
- R6及びR8の`effective-period`は各期間のサービスコード表及び期間別資料から取得し、現行HTMLから過去期間を推定しない。
- 公式資料にない定数、順序、既定値又は入力値を補完しない。

## 6. 公式計算契約

### 6.1 記号

- `E`: 当該サービス提供月を含む年度の4月1日時点で当該基準該当就労継続支援B型事業所に支弁される保護施設事務費。単位は1人当たり月額・円。
- `B`: 同一定員区分かつ同一平均工賃月額区分に対応する、通常の就労継続支援B型サービス費（Ⅱ）の基本報酬単位。
- `HU(x)`: 公式資料に従う四捨五入により整数単位へ丸める操作。
- `postMinFactor[n]`: 最小値選択後に適用する、公式サービスコード行に記載された第`n`減算倍率。

### 6.2 計算式

```text
formulaUnits = HU(((E / 22 / 0.945 / 10) + 23) * 1.046)

benchmarkUnits =
  if municipality-ownership = local-government
  then HU(B * 0.965)
  else B

prescribedUnits = min(formulaUnits, benchmarkUnits)

finalUnits[0] = prescribedUnits
finalUnits[n] = HU(finalUnits[n - 1] * postMinFactor[n])
```

### 6.3 順序と丸め

1. `formulaUnits`は`E / 22 / 0.945 / 10`、`+ 23`、`* 1.046`の順で計算する。
2. 公式式側は途中で丸めず、`* 1.046`後に1回だけ`HU`を適用する。
3. 地方公共団体設置の場合、`0.965`は`B`だけへ乗じ、その直後に`HU`を適用する。
4. `0.965`を`E`、`formulaUnits`又は最小値選択後の単位へ適用しない。
5. `min`自体では丸めない。
6. 計画未作成、従業者欠如及びサービス管理責任者欠如の倍率は`min`後へ適用する。
7. 複数倍率は配列順に逐次適用し、各factor直後に`HU`を適用する。
8. 人員欠如と計画未作成が同時に記載された公式XLSX行は、OOXML列順に従い、人員欠如factorの後に計画未作成factorを置く。

`HU`の識別子は`claim.rounding.units.half-up.v1`を使用する。式、地方公共団体比較補正、最小値選択及びpost-min factorは別々の`calculationStepId`で識別する。

## 7. Domain model

### 7.1 FormulaUnitRuleの共通部分

現行の抽象`FormulaUnitRule`から`BaseComponentKey`を外し、共通fieldを`BillingUnit`だけにする。既存2 subtypeへ`BaseComponentKey`を移し、既存JSON shape及び意味を維持する。

```text
FormulaUnitRule
  BillingUnit

BaseComponentPassThroughFormulaRule
  BaseComponentKey
  CalculationStepId
  RoundingRuleId

FactorChainFormulaRule
  BaseComponentKey
  Factors[]
```

これにより、新modeへ存在しないbase componentを捏造しない。

### 7.2 新規closed subtype

```text
ProtectedFacilityBenchmarkMinimumRule
  RuntimeInputRequirement
  StatutoryFormula
  Benchmark
  Selection
  Factors[]
  BillingUnit

ProtectedFacilityAdministrativeExpenseRequirement
  Key
  ValueKind
  ValueUnit
  Scope
  AsOfPolicy
  ProvenancePolicyId

ProtectedFacilityStatutoryFormula
  DaysDivisor
  ExpenseAdjustmentDivisor
  UnitPriceDivisorYen
  FixedAdditionUnits
  UpliftRate
  CalculationStepId
  RoundingRuleId

ProtectedFacilityBenchmark
  OfficialSection
  BasicRewardStaffingKey
  PaymentBandMatch
  CapacityMatch
  LocalGovernmentAdjustment

ProtectedFacilityLocalGovernmentAdjustment
  ConditionSelector
  Rate
  Target
  CalculationStepId
  RoundingRuleId

ProtectedFacilityMinimumSelection
  Kind
  CalculationStepId
  RoundingRuleId
```

`Factors[]`は既存`ServiceCodeFormulaFactor`を再利用する。0件を許可し、減算行では1件以上を公式列順で保持する。

### 7.3 固定JSON shape

```json
{
  "kind": "formula",
  "mode": "protected-facility-benchmark-minimum",
  "runtimeInputRequirement": {
    "key": "protected-facility-administrative-expense-yen",
    "valueKind": "entered-yen",
    "valueUnit": "yen-per-person-per-month",
    "scope": "facility-and-service-fiscal-year",
    "asOfPolicy": "service-fiscal-year-april-first",
    "provenancePolicyId": "claim.input.protected-facility-administrative-expense.v1"
  },
  "statutoryFormula": {
    "daysDivisor": 22,
    "expenseAdjustmentDivisor": "0.945",
    "unitPriceDivisorYen": 10,
    "fixedAdditionUnits": 23,
    "upliftRate": "1.046",
    "calculationStepId": "claim.step.units.service-code.protected-facility-formula.v1",
    "roundingRuleId": "claim.rounding.units.half-up.v1"
  },
  "benchmark": {
    "officialSection": "b-type-service-fee-ii",
    "basicRewardStaffingKey": "b-type-service-fee-ii",
    "paymentBandMatch": "same-average-wage-band",
    "capacityMatch": "same-capacity-band",
    "localGovernmentAdjustment": {
      "conditionSelector": "municipality-ownership:local-government",
      "rate": "0.965",
      "target": "comparison-only",
      "calculationStepId": "claim.step.units.service-code.protected-facility-local-government-benchmark.v1",
      "roundingRuleId": "claim.rounding.units.half-up.v1"
    }
  },
  "selection": {
    "kind": "minimum",
    "calculationStepId": "claim.step.units.service-code.protected-facility-minimum.v1",
    "roundingRuleId": null
  },
  "factors": [],
  "billingUnit": "per-day"
}
```

Schemaは上記の制度定数及び識別子を`const`で固定する。Domain constructor及びruntime validatorも同じ値を要求し、JSON Schemaだけを迂回した不正なDomain instanceを拒否する。

### 7.4 禁止する表現

- 公式式を文字列として保存しない。
- 任意の演算子配列又はgeneric ASTへ変換しない。
- `0.945`、`23`、`1.046`又は`0.965`を外部設定値にしない。
- 保護施設事務費が未入力のとき`0`、直近年度値又は別施設値を補完しない。
- 比較対象を自由文字列又は任意component keyで指定しない。
- 地方公共団体補正の対象をformula全体へ広げない。

## 8. Runtime入力要件とprovenance境界

### 8.1 このfollow-upで実装する契約

masterには実値ではなく、次の入力要件だけを保持する。

- key: `protected-facility-administrative-expense-yen`
- value kind: `entered-yen`
- unit: `yen-per-person-per-month`
- scope: 施設及びサービス年度
- as-of policy: サービス提供月を含む年度の4月1日
- provenance policy: `claim.input.protected-facility-administrative-expense.v1`

`entered-yen`は「未入力」と「根拠確認済みの0円」を区別する将来契約である。このfollow-upでは値型又は入力recordを作らず、schema上のrequirement識別子として固定する。

### 8.2 後続実装に予約する実値provenance

後続のappend-only実値recordは、少なくとも次を損失なく保持できなければならない。

```text
OfficeId
ServiceFiscalYear
AmountYen: EnteredYen
AsOfDate
DeterminingAuthority
EvidenceDocumentId
OriginalDocumentReference
EvidenceSha256
EvidenceLocator
ConfirmedAt
ConfirmedBy
ConfirmationReason
RootId
Revision
RecordKind
ExpectedHeadId
```

固定条件:

- `AsOfDate`は対象サービス年度の4月1日と一致する。
- `AmountYen`は円単位の整数で、missingを0へ変換しない。
- `DeterminingAuthority`、原本文書参照、SHA-256及びlocatorを一組で保持する。
- 訂正は上書きせず、`RootId + Revision + RecordKind + ExpectedHeadId`によるappend-only履歴とする。
- confirmation actor、時刻及び理由を保持する。

このfield集合は後続設計の最低要件を予約するものであり、本設計の実装完了条件にはentity、migration又はUIを含めない。

## 9. Source provenance supportの拡張

### 9.1 新規supports

`ClaimSourceSupport`及びmanifest `supports[]`へ次の閉じた値を追加する。

```text
unit-rule-formula
unit-rule-comparison
unit-rule-local-government-adjustment
unit-rule-runtime-input
unit-rule-runtime-input-provenance
```

既存`unit-rule-value`へ式、比較対象、地方公共団体補正及びruntime入力をまとめない。`unit-rule-value`はこのmodeではpost-min factorの倍率だけを支持する。

### 9.2 必須coverage

`protected-facility-benchmark-minimum` entryは、既存の`service-identity`、`selectors`、`unit-rule-kind`、`unit-rule-step`、`unit-rule-rounding`及び`effective-period`に加え、次を有効正本で覆う。

| support | 覆う内容 |
| --- | --- |
| `unit-rule-formula` | 22、0.945、10、23、1.046、式の順序及び式側の丸め境界 |
| `unit-rule-comparison` | 通常B型サービス費（Ⅱ）、同一平均工賃月額区分、同一定員区分、最小値選択 |
| `unit-rule-local-government-adjustment` | 地方公共団体条件、0.965、comparison-only、補正直後の丸め |
| `unit-rule-runtime-input` | 保護施設事務費、1人当たり月額、対象年度4月1日時点 |
| `unit-rule-runtime-input-provenance` | 施設別・地域別・定員別・加算別の決定、特別基準及び通知の根拠 |

`factors[]`が非空の場合は、各factorについて既存の`unit-rule-value`、`unit-rule-target`、`unit-rule-step`及び`unit-rule-rounding`を有効正本で覆う。

### 9.3 資料ごとのauthority

- 現行報酬告示HTML: `unit-rule-formula`、`unit-rule-comparison`、`unit-rule-local-government-adjustment`及び`unit-rule-runtime-input`の`authoritative`。
- 保護施設事務費の支弁基準HTML: `unit-rule-runtime-input-provenance`の`authoritative`。
- R6／R8費用算定留意事項: `unit-rule-step`及び`unit-rule-rounding`の`authoritative`。
- R6／R8サービスコードXLSX: `service-identity`、`selectors`、`conditions`、factorの`unit-rule-value`、`unit-rule-target`及び`effective-period`の`authoritative`。
- 平成31年統合版及びR6改正告示: 公式式と比較構造の継続性を裏付ける`cross-check`。
- R8改正告示: R8期間の改正関係を裏付ける`cross-check`。`corrects` chainへ加えず、有効正本candidateへ含めない。

supportごとの有効正本確定はTask 12のcorrection-chain契約を維持する。`cross-check`を有効正本candidateへ昇格させない。

## 10. 対象44 source rows

### 10.1 期間別内訳

| release | effective period | rows | source |
| --- | --- | ---: | --- |
| R6 | 2024-04..2026-05 | 22 | `r6-service-codes-2-xlsx` |
| R8 | 2026-06..open | 22 | `r8-service-codes-2-xlsx` |
| 合計 | — | 44 | — |

各releaseの22 rowsは次のservice code集合に固定する。

- 基本、地方公共団体及び計画未作成の組合せ6件: `462841`–`462846`
- 従業者欠如と地方公共団体／計画未作成の組合せ12件: `46C841`–`46C846`、`46D841`–`46D846`
- サービス管理責任者欠如4件: `46E841`、`46E844`、`46F841`、`46F844`

公式service code、official label、condition selectors及びfactor rateはXLSX source rowから転記し、上記code rangeから生成しない。

### 10.2 Factor契約

- 計画未作成は公式rowに従い`0.7`又は`0.5`をpost-min factorとして保持する。
- 従業者欠如は公式rowに従い`0.7`又は`0.5`をpost-min factorとして保持する。
- サービス管理責任者欠如は公式rowに従い`0.7`又は`0.5`をpost-min factorとして保持する。
- 地方公共団体の`0.965`はfactorへ入れず、`benchmark.localGovernmentAdjustment`に固定する。
- 人員欠如と計画未作成の複合rowは、公式XLSXの列順どおり人員欠如、計画未作成の順で`factors[]`へ格納する。

### 10.3 Manifest mapping

- 44 source rowsの`disposition`を`schema-gap`から`seed`へ変更する。
- 各rowは対応する`service-codes` production revisionへ`mappingRole = primary`で1件以上写像する。
- `supports[]`はそのrowが実際に示すservice identity、selectors、conditions、factor値、factor順序及びeffective periodだけを列挙する。
- 公式式、比較対象、地方公共団体補正又はruntime入力をサービスコードXLSXが直接示さない場合、そのsupportをXLSX rowへ付与しない。
- R6 rowの継続性対応は非空`mappingReason`へ明記する。R6改正告示の`（略）`を式定数の直接根拠として記録しない。

## 11. Source inventory追加

### 11.1 新規documents

既存41 documentsへ次の3 documentsを追加する。document IDは実装時に次へ固定する。

```text
current-fee-notice-html
protected-facility-administrative-expense-standard-html
h31-fee-notice-consolidated
```

source catalogはversion 1のまま、title、publisher、effectiveAt、publishedAt、retrievedAt、URL、SHA-256、supersedes／corrects／supplements、applicabilityNote及びcorrectionNoteを既存schemaに従って保持する。

### 11.2 新規8 evidence rows

次の8 period-specific logical rowsをmanifestへ追加する。同じsource locatorをR6とR8の両方で使う場合は、期間別`rangeId`へ分ける。row identityは`sourceDocumentId + rangeId + sourceLocator`であるため衝突しない。

| document | range ID | locator | period | mapping role | 主なsupports |
| --- | --- | --- | --- | --- | --- |
| `r6-fee-notice` | `r6-protected-facility-b-comparison` | `pdf:physical-page=137` | 2024-04..2026-05 | `supporting-evidence` | `unit-rule-comparison`、R6 continuity |
| `r6-fee-notice` | `r6-protected-facility-b-local-government` | `pdf:physical-page=138` | 2024-04..2026-05 | `supporting-evidence` | `unit-rule-local-government-adjustment`、R6 continuity |
| `current-fee-notice-html` | `r6-protected-facility-b-current-consolidated` | `html:lines=l000002791,l000002793` | 2024-04..2026-05 | `supporting-evidence` | formula、comparison、minimum、local-government、runtime input |
| `current-fee-notice-html` | `r8-protected-facility-b-current-consolidated` | `html:lines=l000002791,l000002793` | 2026-06..open | `supporting-evidence` | formula、comparison、minimum、local-government、runtime input |
| `protected-facility-administrative-expense-standard-html` | `r6-protected-facility-administrative-expense-provenance` | `html:lines=l000000054,l000000060-l000000062` | 2024-04..2026-05 | `supporting-evidence` | per-person monthly、facility/region/capacity/additions、special standard、notification |
| `protected-facility-administrative-expense-standard-html` | `r8-protected-facility-administrative-expense-provenance` | `html:lines=l000000054,l000000060-l000000062` | 2026-06..open | `supporting-evidence` | per-person monthly、facility/region/capacity/additions、special standard、notification |
| `h31-fee-notice-consolidated` | `r6-protected-facility-b-formula-continuity` | `pdf:physical-page=46` | 2024-04..2026-05 | `supporting-evidence` | formula continuity and comparison |
| `h31-fee-notice-consolidated` | `r6-protected-facility-b-local-government-continuity` | `pdf:physical-page=47` | 2024-04..2026-05 | `supporting-evidence` | local-government continuity |

各evidence rowは親rowと同じ期間の22 production revisionsだけへ`supporting-evidence`で写像し、非空`mappingReason`を持つ。R6 rowをR8 revisionへ、又はR8 rowをR6 revisionへ写像しない。1 evidence rowから同一期間内の複数revisionへの写像は既存manifest v2契約どおり複数`productionTargets[]`で表す。

`html:lines` locatorは、カンマ区切りの単一line又は閉区間だけを許すclosed grammarとしてmanifest audit toolへ追加する。listed lineをすべて同一取得bytes内で一意に解決できなければ停止する。

### 11.3 順序とidentity保存

- 既存14,718 row identitiesは削除、重複又は並べ替えを行わない。
- 既存14,718 identitiesが新manifest identitiesの順序付き部分列であることをtestで保証する。
- 8 evidence rowsは上記document／range宣言順で追加し、各rowは単一期間だけを持つ。
- source inventoryの行数増加は8件だけとする。

### 11.4 固定件数

実装前の再監査結果:

```text
documents   41
ranges      53
rows        14,718
seed        14,137
excluded       537
schema-gap      44
```

44 schema-gap rowsのseed化後:

```text
documents   41
ranges      53
rows        14,718
seed        14,181
excluded       537
schema-gap       0
```

8 evidence rows追加後の最終契約:

```text
documents   44
ranges      61
rows        14,726
seed        14,189
excluded       537
schema-gap       0
```

`14,189 + 537 = 14,726`を固定invariantとして検証する。

## 12. Schema及びvalidator

### 12.1 JSON Schema

`claim-master-file.schema.json`へ次を追加する。

- formula `mode`の第3値`protected-facility-benchmark-minimum`。
- 7.3のclosed object definitions。
- 全制度定数、selector、target、step、rounding及びbilling unitの`const`。
- `factors`の既存factor schema再利用。
- `additionalProperties = false`。
- `ClaimSourceSupport`の5 enum値。

既存2 formula modeのrequired properties及びprohibited propertiesは維持する。新modeでは`baseComponentKey`を禁止する。

### 12.2 Runtime validator

validatorは少なくとも次をfail-closeで検証する。

- 新modeの全固定定数及びIDが7.3と完全一致する。
- `billingUnit = per-day`。
- formula、local-government adjustment及び各factorのrounding IDが正しい。
- selectionの`roundingRuleId = null`。
- `conditionSelector`がentry又はcondition definitionから解決できる。
- factorのorderが1から連続し、重複しない。
- factor condition selectorsがentry条件のsubsetである。
- 新modeの必須source supportが有効正本でちょうど1系統に確定する。
- factorなしentryへ`unit-rule-value`を必須化しない。
- factorありentryはfactorのvalue、target、step及びrounding coverageを欠けない。
- `BaseComponentKey`を要求せず、存在した場合は拒否する。

## 13. Tests

### 13.1 Domain及びschema tests

- 新modeの代表fixtureをdeserializeしてtyped subtypeへ到達できる。
- 7.3の完全なfixtureを受理する。
- 各定数、mode、selector、target、step、rounding、billing unitの改変を拒否する。
- required field欠落、unknown field及び`baseComponentKey`混入を拒否する。
- 既存2 formula modeが後方互換のまま受理される。
- 5つの新support enumを受理し、未知supportを拒否する。

### 13.2 計算契約の静的tests

runtime計算結果を検証するのではなく、次の構造を検証する。

- 地方公共団体補正targetが`comparison-only`である。
- formulaとbenchmarkの後に`minimum`が置かれる。
- post-min factorsがselectionの後段として保持される。
- 複合rowで人員欠如factorが計画未作成factorより前にある。
- factorごとにhalf-up roundingが指定される。
- factorなし、1 factor及び2 factorsの代表rowをtypedに保持できる。

### 13.3 Inventory及びprovenance tests

- documents 44、ranges 61、rows 14,726、seed 14,189、excluded 537、schema-gap 0。
- 対象service code集合がR6 22、R8 22で重複なく存在する。
- 44 rowsすべてが対応production revisionへ`primary` mappingを持つ。
- 8 evidence rowsすべてが1件以上の`supporting-evidence` mappingを持つ。
- 既存14,718 row identitiesが順序付き部分列として保存される。
- 新規3 documentsのURL、SHA-256及びlocatorがsource catalog／manifestで一致する。
- HTML line locator、PDF physical page及びXLSX workbook rowが到達可能である。
- production source refsとmanifest targetsがsupport単位で双方向一致する。
- R6の式継続性mappingReasonが非空で、`（略）`資料を式定数の直接根拠へしていない。

### 13.4 Regression

- 既存Task 12 schema／Domain／validator tests。
- Claim master source inventory tests。
- Claim master provider／bundle testsのsource document期待集合。
- `git diff --check`。
- repository標準CI。

## 14. 実装対象ファイル

変更範囲は次の最小集合に限定する。実際のパスは既存型及びtest配置に合わせる。

- `src/Tsumugi.Domain/ClaimMasters/`配下のformula Domain型とsource support enum。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/claim-master-file.schema.json`。
- `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`及び必要なserializer binding。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`。
- `docs/spec-data/phase3/claim-master-source-row-manifest.json`。
- manifest変換／監査toolの既存ファイル。
- 上記に対応するfocused test files。
- Task 12設計及びtestsの`462841`／`462842`旧formula fixtures。
- `docs/decisions/0020-claim-master-sources-and-versioning.md`のsource catalog、version別source集合及びinventory更新。
- `docs/decisions/0025-claim-rounding-rules.md`のclosed step／rounding matrix更新。
- 依存するTask 13設計の旧固定件数、source集合、ordered identity gate及び完了条件の同期。

次は変更しない。

- 6 production seed filesのうち対象44 service-code revisions以外。
- database entity及びmigration。
- View、ViewModel及び入力UI。
- calculator、resolver及び請求確定処理。
- 帳票、CSV及びClaimBatch。
- `source-catalog.schema.json`のversion。

## 15. 実装順序

1. Red testsで新mode、support enum、44-row集合、8 evidence rows及び固定件数を表す。
2. Domain型とJSON Schemaを追加する。
3. runtime validatorをfail-closeで拡張する。
4. source catalogへ3 documentsを追加する。
5. manifest変換を再実行可能な形で更新し、44 rowsをseed化して8 evidence rowsを追加する。
6. 対象44 production service-code revisionsへ新modeとsource refsを写像する。
7. Task 13設計、ADR 0020及びADR 0025を新inventory、source集合、step／rounding及びruntime入力境界へ同期する。
8. focused tests、full claim-master tests、標準CIの順で検証する。

## 16. 完了条件

- 公式式、比較対象、地方公共団体補正、最小値選択、post-min factor及び丸め境界がclosed typed contractで保持される。
- 保護施設事務費の必要性、単位、施設・年度scope、4月1日policy及び将来provenance policyがmasterで保持される。
- 実値未入力と0円を混同するruntime実装を導入していない。
- 44 schema-gap rowsが公式service code identityを保ったままproduction revisionsへ写像される。
- 8 evidence rowsと3 source documentsが追加される。
- manifestが44 documents、61 ranges、14,726 rows、14,189 seed、537 excluded、0 schema-gapになる。
- 既存14,718 identitiesの順序が保存される。
- production refs、manifest targets及びsource catalogのprovenanceが双方向一致する。
- R6改正告示の`（略）`を式定数の直接根拠と誤記しない。
- focused tests及び標準CIが成功する。
- 保護施設事務費の実値入力、resolver及びruntime算定を完了したとは記録しない。

## 17. 実装後の明示的な残課題

本設計の実装完了後も、保護施設事務費の実値record、append-only revision、証拠取込、入力UI、通常B型基本報酬Ⅱの比較単位解決、地方公共団体条件解決及び公式計算のruntime実行は未実装である。これらは別Taskの設計承認を経て実装する。
