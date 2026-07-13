# Phase 3-1 Task 13 Claim Master Seed Design

**Status:** Schema v2 review candidate
**Date:** 2026-07-13
**Scope:** Task 13のみ — manifest v2変換・再監査、一次資料取得、R6／R8制度実値seed、機械検証、独立全件照合
**Normative dependency:** `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`

## 1. 結論

Task 13を次の2段階へ分離する。

1. **Audit gate:** 41資料を再取得してSHA-256を検証し、既存14,709行のsource-side manifestをschema v2へ機械変換したうえで、全行を一次資料に対して再監査する。
2. **Conditional seed phase:** audit gateで`schema-gap = 0`、取得率・SHA一致率・locator到達率・row網羅率がすべて100%になった場合だけ、6つのproduction seedへ制度実値を投入する。

Audit gateのcandidateではproduction seedを変更しない。manifest v2再監査とseed投入を別commit・別レビュー対象にし、監査未完了のまま制度値を転記できない構造にする。

## 2. 現在地

Task 12でclaim master file schema v2、Domain型及びruntime validatorが実装済みである。

- 6 seed fileは`schemaVersion = 2`で、現時点の`entries`は空である。
- `service-codes.json`だけが`conditionDefinitions`を持つ。
- `PercentageAdjustmentMasterRow`は廃止され、`UnitAdjustmentMasterRow`へ置換された。
- service codeのunit ruleは`fixed-composite-unit`、`unit-addition`、`formula`の閉じたunionである。
- source provenanceは単一locatorではなく`ClaimSourceRef[]`であり、`evidenceRole`と`supports[]`を持つ。
- 現行manifestはschema v1で、41 documents、14,709 rows、うち13,950 rowsが`schema-gap`である。

従来のTask 13設計にある`masterKind`、`seedKey`及びaggregation 3 fieldのrow直下契約と、割合加減算だけを扱うseed順序は廃止する。

## 3. 目的

令和6年・令和8年の就労継続支援B型制度値を、次の証拠を保持したschema v2 seedとして投入する。

- 再取得した一次資料の未加工バイト。
- source catalogと一致するSHA-256。
- workbook order／sheet／row／cell又はphysical pageへ到達できるlocator。
- source rowから1件以上のproduction targetへの明示的mapping。
- production entry又はconditionの各`ClaimSourceRef.supports[]`に対応する根拠。
- 適用期間と訂正関係。
- 実装担当者とは別のreviewerによる全source row照合。

対象seedは次の6ファイルとする。

- `basic-rewards.json`
- `additions.json`
- `region-unit-prices.json`
- `burden-caps.json`
- `transition-rules.json`
- `service-codes.json`

## 4. 非目標

- Task 14以降のtyped master provider、resolver又はcalculatorを実装しない。
- Task 15以降の平均工賃、割合・固定単位・按分のruntime計算を実装しない。
- Phase 3-2帳票又はPhase 3-3 CSVへ進まない。
- Task 12のDomain型、JSON Schema又はvalidatorをTask 13都合で拡張しない。
- 公式資料の原本をrepositoryへ保存又は再配布しない。
- 不明値を近い版、前版、名称推測又は既存seedから補完しない。
- production値の全量をC# fixtureへ複製しない。

## 5. 全体アーキテクチャ

```text
sources.json + ADRs
        |
        v
raw source acquisition -> SHA receipt -> locator reachability
        |
        v
manifest v1 mechanical migration
        |
        v
manifest v2 full-row re-audit
        |
        +-- schema-gap > 0 -----------------> STOP
        |
        v
AUDIT GATE COMMIT (seed files unchanged)
        |
        v
manifest-driven Red tests
        |
        v
independent masters + interdependent claim components
        |
        v
production validator + focused tests + full CI
        |
        v
SEED CANDIDATE COMMIT -> independent full-row review
        |
        v
evidence commit
```

Audit gateとseed phaseは同じcandidate commitへ混ぜない。audit gate通過後も、全seedがruntime validatorを同時に通るまで部分seedをGreen又は完了として扱わない。

## 6. 一次資料取得契約

### 6.1 閉じた資料集合

取得対象は現行manifestの41 documentsに固定する。各document ID、URL及び期待SHA-256は`src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`を正本とし、Task 13で別URL又は別SHAへ置き換えない。

対象releaseは次の5件とする。

- `claim-master-r6-04`
- `claim-master-r6-06`
- `claim-master-r7-01`
- `claim-master-r7-09`
- `claim-master-r8-06`

### 6.2 一時保存とreceipt

原本は`/tmp/tsumugi-phase31-task13/sources/`へdocument IDを基にした安定名で保存する。取得ごとに次をJSONL receiptへ記録する。

- document ID
- catalog URL
- expected SHA-256
- actual SHA-256
- byte count
- UTC取得日時
- PASS／FAIL

取得失敗、空ファイル、SHA不一致又は同一document IDの複数ファイルは即時停止とする。receipt及び原本はgitへ追加しない。最終証跡はreceiptから生成し、mtime又は記憶から再構成しない。

### 6.3 locator到達性

- XLSXはworkbook order、row及び必要なcellが実ファイル内に存在することを確認する。
- PDFはphysical pageが`pdfinfo`のpage count内にあり、対象頁を抽出できることを確認する。
- HTMLはcatalog取得バイト内でmanifest locatorが一意に解決できることを確認する。

到達できないrowは`excluded`へ落とさず停止する。

## 7. Manifest v2契約

### 7.1 rootとdocuments

rootは次の閉じた形とする。

```json
{
  "schemaVersion": "2",
  "documents": [],
  "rows": []
}
```

`documents`はv1のdocument ID、SHA、role及び`extractionRanges`を保持する。document順序、range順序及びrangeの`expectedItemCount`を機械移行で変更しない。一次資料の再監査で誤りを確認した場合だけ、変更理由とbefore／after件数をaudit evidenceへ記録して修正する。

### 7.2 row identity

各rowは次を保持する。

```text
sourceDocumentId
rangeId
sourceLocator
sourceLabel
effectiveFrom
effectiveTo
disposition
productionTargets[]
exclusionReason
```

row identityは`sourceDocumentId + rangeId + sourceLocator`とする。v1からv2への移行でrowの追加、削除、重複、並べ替えを行わない。v1の`masterKind`、`seedKey`、`aggregationId`、`aggregationKind`及び`aggregationReason`はrow直下から削除し、v2では禁止する。

### 7.3 productionTargets

`productionTargets[]`は次の閉じた形とする。

```json
{
  "masterKind": "service-codes",
  "seedKey": "stable-production-key",
  "mappingRole": "primary",
  "supports": ["service-identity", "effective-period"],
  "mappingReason": null
}
```

`mappingRole`は次の閉集合とする。

- `primary`: source rowがproduction targetの主たるidentity又は値を定義する。
- `component`: source rowを正規化したbasic reward又はunit adjustment componentへ対応させる。
- `supporting-evidence`: source rowを訂正、条件又は相互照合の補助根拠として対応させる。

`supports[]`はproduction schema v2の`ClaimSourceSupport`と同じ次の閉集合を使い、非空・重複禁止とする。

```text
service-identity
selectors
unit-rule-kind
unit-rule-value
unit-rule-target
unit-rule-step
unit-rule-rounding
conditions
effective-period
master-values
```

`component`及び`supporting-evidence`は非空`mappingReason`を必須とする。`primary`は`mappingReason = null`を許可する。

production targetの`masterKind`は6 seed file kindに加え、`service-codes.json`内の`conditionDefinitions`を区別するaudit-only kind `service-code-conditions`を許可する。このkindの`seedKey`はcondition definitionの実keyと一致させる。これによりconditionのsource refsもentryと同じ完全性検査へ含める。

### 7.4 disposition

- `seed`: `productionTargets`を1件以上持ち、`exclusionReason = null`とする。
- `excluded`: `productionTargets`を空にし、具体的な非空`exclusionReason`を持つ。
- `schema-gap`: `productionTargets`を空にし、schema v2へ損失なく写像できない理由を持つ。

1 source rowから複数production entryへのprojectionは複数targetで表す。複数source rowから1 production entryへの集約は同じ`masterKind + seedKey`を参照する。source row数とproduction row数の単純一致は要求せず、distinct target key集合とproduction key集合を比較する。

### 7.5 provenance対応

manifestの`mappingRole`はproductionの`ClaimSourceEvidenceRole`を置き換えない。production source refについて次を独立に検証する。

- `documentId`、`locator`及び`supports[]`がmanifest targetと一致する。
- `sha256`がsource catalogと一致する。
- `evidenceRole = authoritative | correction | cross-check`がsource catalogの`corrects` graph及び採用した正本関係と矛盾しない。
- supportごとの有効正本がTask 12のcorrection-chain契約でちょうど1件に確定する。

## 8. Manifest v2変換と再監査

### 8.1 機械変換

変換は再実行可能な専用audit toolで行う。toolは次だけを機械的に実施し、公式上の意味を推測しない。

1. root `schemaVersion`を`2`へ変更する。
2. 全rowへ空の`productionTargets`を追加する。
3. v1で`disposition = seed`かつ1対1対応が確定済みのrowだけ、旧`masterKind + seedKey`を`primary` targetへ移す。
4. v1のaggregation fieldを削除する。
5. documents、ranges、row identity、期間、label、disposition及び除外理由を保持する。
6. before／afterのdocument数、range数、row数及びrow identity SHA-256を出力する。

13,950件の既存`schema-gap`を機械的に`seed`へ変更しない。

### 8.2 全行再監査

SHA検証済み原本に対して全14,709行を再監査し、各rowを次へ分類する。

- 独立master値: region unit price、burden cap、transition rule。
- basic reward component。
- unit adjustment component: `fixed-units`、`units-per-count`、`percentage-of-target`、`prorated-units`。
- service code identity、selector、condition及び次のunit rule。
  - `fixed-composite-unit`
  - `unit-addition`
  - `formula / base-component-pass-through`
  - `formula / factor-chain`
- 対象外row。
- schema v2でも損失なく表現できないrow。

各`seed` rowは値・identity・期間・target・step・rounding・conditionのどれを裏付けるかを`supports[]`へ明示する。名称の部分一致だけでselector、condition、component又はstable keyを生成しない。

### 8.3 scaleと分割単位

再監査作業はdocument／range単位に分割できるが、gate判定は必ずmanifest全体へ行う。各rangeで次を記録する。

- expected rows
- reviewed rows
- seed／excluded／schema-gap counts
- locator failures
- unresolved mappings

途中結果のrange単位承認をTask 13全体の承認として扱わない。最終candidateは全rangeを再集計する。

## 9. 独立Audit Gate

seed phaseへ進む条件を次に固定する。

- manifest document数が41。
- manifest row数が14,709。
- range別`expectedItemCount`合計とrow数が一致する。
- v1とv2のrow identity集合が完全一致する。
- 取得率、SHA一致率、locator到達率が100%。
- 全rowのdisposition契約が有効。
- `schema-gap = 0`。
- `seed`全rowが1件以上のproduction targetを持つ。
- targetの`mappingRole`、`supports[]`及び`mappingReason`が有効。
- 旧v1 mapping fieldが残っていない。
- 6 production seed fileがTask 12完了時点から未変更で、空のままである。
- manifest v2 tests、source catalog tests及び物理locator auditが成功する。

gate成功時はmanifest v2、audit tool及びmanifest audit testsだけをaudit candidate commitにする。`schema-gap > 0`、source矛盾又はlocator failureがあれば、監査成果を保持して停止し、seed phaseへ進まない。

## 10. 条件付きSeed Phase

### 10.1 Red gate

manifestのdistinct production target集合と、6 seedのentry及びcondition key集合を比較するcompleteness testを先に追加する。audit gate直後はseedが空であるため、testが期待した理由でRedになることを確認する。

### 10.2 依存順

seed投入は次の順で進める。

1. 独立master: `burden-caps.json`、`region-unit-prices.json`、`transition-rules.json`。
2. `service-codes.json`のcondition definitions。
3. 相互依存slice: `basic-rewards.json`、`additions.json`及び`service-codes.json` entries。
4. 6 seed全体のruntime validation。

basic reward、unit adjustment及びservice codeはcomponent ref、service code、selector及び期間で相互依存するため、別々の完成commitにしない。focused Red／Greenはmaster別に実行できるが、3ファイルが同時に整合し、production validatorを通るまで単一candidateとして保持する。

### 10.3 production data契約

- 全6 fileを`schemaVersion = 2`とする。
- 全entry及びconditionは1件以上の`sourceRefs`を持つ。
- production targetが要求する全supportをsource refsの和集合で覆う。
- service code、component、selector及びcondition参照を全適用月で解決する。
- `fixed-composite-unit.FinalUnits`と`BasicRewardMasterRow.BaseUnits`を混同しない。
- `UnitAdjustmentMasterRow.Amount`とservice codeの`unit-addition.amount`を構造的に一致させる。
- `base-component-pass-through`へfactor又はroundingを追加しない。
- `factor-chain`は1始まりの連続orderとstepごとのroundingを保持する。
- future releaseを過去月へ遡及せず、retired service code、condition又はadditionを直近値で延長しない。
- 公式上の値を持たない行又は推測が必要な行を投入しない。

## 11. 検証設計

### 11.1 Manifest audit tests

`ClaimMasterSeedPhase31Tests`で次を検証する。

- schema v2 root及びclosed row contract。
- 41 documents、14,709 rows及びrow identity preservation。
- range count、document、SHA及びcatalog release整合。
- dispositionとproduction targetの必須組合せ。
- mapping role、supports及びmapping reasonの閉集合。
- `schema-gap = 0`停止ゲート。
- 旧v1 field不在。

物理locator到達性はSHA検証済み原本が必要なため、repository testと分離したaudit commandで検証する。repository testだけで100%到達を主張しない。

### 11.2 Seed tests

- manifest target集合とproduction entry／condition key集合の完全一致。
- source refのdocument、SHA、locator、evidence role及びsupports整合。
- master別期間、key、値型及び閉集合。
- service code unit rule、condition、component及びselector参照。
- 2024-04、2024-06、2025-01、2025-09、2026-05、2026-06の版境界。
- `JsonClaimMasterProviderTests`及び`ClaimSpecificationBoundaryTests`の回帰。
- `ClaimMasterFileValidator.ValidateAll`相当のproduction経路。

production全値を期待fixtureへ複製しない。代表境界値だけを一次資料の固定locatorから独立に選び、全件性はmanifest mappingと独立reviewで証明する。

### 11.3 品質ゲート

focused testsの後に`./build/ci.sh`を実行し、警告・エラー0、全tests成功、coverage、architecture及びoffline gate通過を要求する。

## 12. Candidate commitと独立全件照合

### 12.1 commit境界

1. **Audit candidate:** manifest v2、audit tool、manifest audit tests。seed変更なし。
2. **Seed candidate:** 6 seed及びseed tests。全seedがGreenになった時点で固定する。
3. **Evidence commit:** 独立reviewと最終verificationの証跡。

### 12.2 reviewer分離

seed転記を行っていないfresh reviewerへ、seed candidate commit、manifest、6 seed、source catalog、ADRs及びSHA検証済み原本だけを渡す。reviewerは次を全件確認する。

1. 全documentのSHA再計算。
2. 全rangeのrow countとlocator到達。
3. 全rowのdisposition、production targets、mapping role及びsupports。
4. 全production entry／conditionのkey、値、code、期間、source refs。
5. source refのevidence roleとcorrection chain。
6. file別・range別・総row count及びdiscrepancy count。

合格条件はSHA、locator、manifest coverage、source row reviewが全て100%、未説明除外0、schema gap 0、値・code・期間・mapping discrepancy 0、判定`Approved`とする。

Issues Foundの場合は新しいseed candidateを作り、同じreviewerが全rowを再レビューする。差分rowだけの確認で合格にしない。

## 13. エラー処理と停止条件

次のいずれかで即時停止する。

- 取得失敗、空レスポンス又はSHA不一致。
- locator不明、複数解決又は到達不能。
- v1／v2でrow identity又はrange countが理由なく変化する。
- source間で値、意味、訂正関係又は期間が矛盾する。
- schema v2へ損失なく写像できない。
- supportごとの有効正本が0件又は複数件になる。
- service code、component、selector又はcondition参照が解決しない。
- 独立reviewでdiscrepancyが残る。

一次資料から一意に解消できない事項だけを`docs/open-questions.md`へ起票する。停止中はTask 14へ進まず、Task 13完了証跡を作成しない。

## 14. 実装時の変更対象

### Audit gate

- Modify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Create: `build/phase3_task13_manifest_v2.py`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

### Conditional seed phase

- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Create: `docs/phase3-1-master-transcription-review.md`

`docs/open-questions.md`は未解決事項が生じた場合だけ変更する。Task 12のDomain、schema、validator及びschema testsはTask 13へ含めない。

## 15. 完了条件

Task 13は次を全て満たした場合だけ完了とする。

- audit gateがseed変更なしで独立して通過している。
- 41 documents、14,709 source rows及び全rangeがmanifest v2へ保持されている。
- 取得率、SHA一致率、locator到達率、manifest coverageが100%。
- `schema-gap = 0`。
- manifest target集合とproduction entry／condition key集合が完全一致する。
- 全production source refがmanifest mappingとsource catalogへ一致する。
- 6 seedがschema v2 runtime validatorを同時に通る。
- R6-04、R6-06、R7-01、R7-09及びR8-06の期間境界が一意である。
- focused tests及び`./build/ci.sh`が成功する。
- seed candidateが独立全件照合で`Approved`となる。
- `docs/phase3-1-master-transcription-review.md`が実candidate commitと実数を参照する。
- 原本、receipt及び一時抽出物がgitへ混入していない。
