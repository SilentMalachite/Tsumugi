# Phase 3-1 Task 12 Claim Master Schema v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** 13,950件のschema-gapを損失なく表現できるclaim master schema v2を、typed Domain bundle、closed JSON contract、fail-closed runtime validator、synthetic testsとして実装する。

**Architecture:** 6 masterをclean breakでv2へ移行し、service codeにunit rule・condition・component参照を保持する。JSON Schemaはshapeを、runtime validatorはsource correction DAG、期間、参照、condition、closed step/rounding matrix及び種類別cycleを検証し、providerはsource catalog v1のtyped metadataをvalidatorへ渡す。

**Tech Stack:** .NET 10、C# 14、System.Text.Json、JSON Schema draft 2020-12、xUnit、FluentAssertions

---

## 実行契約

- 設計正本: docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md
- Task 13のmanifest、ClaimMasterSeedPhase31Tests、production seed実値は変更しない。
- 各production変更は @superpowers:test-driven-development で、対応testのRed確認後に行う。
- focused test、Domain/Infrastructure test、最後に ./build/ci.sh を順番に実行する。
- 完了直前に @superpowers:verification-before-completion、最終差分に @superpowers:requesting-code-review を使う。
- コミット、push、mergeはこの実装依頼の範囲外とし、ユーザーから明示依頼があるまで行わない。

## ファイル構成

### Modify

- src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs — v2 source、unit rule、amount、condition、component、bundle型。
- src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json — v2 closed public JSON contract。
- src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs — v2 parseとcross-file validation。
- src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs — typed source catalog metadataをvalidatorへ渡す。
- tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs — v2 Domain contract。
- tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs — schema/validator/代表gap/scale contract。
- tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs — provider、embedded schema、v1拒否、混在拒否。
- tests/Tsumugi.Infrastructure.Tests/ClaimMasters/OfficeClaimProfilePolicyProviderTests.cs — v2 sourceRefs fixtureとsanitized error契約。
- tests/Tsumugi.Infrastructure.Tests/ExternalSpecificationLiteralGuard.cs — entry/conditionの全sourceRefs境界検査。
- tests/Tsumugi.Infrastructure.Tests/ClaimSpecificationBoundaryTests.cs — v2 guard fixtureとJSON Pointer契約。
- src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json
- src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json
- src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json
- src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json
- src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json
- src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json

---

### Task 1: v2 Domain contractを固定する

**Files:**
- Modify: tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs
- Modify: src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs

- [x] **Step 1: v2型を要求する失敗testを書く**

ClaimSourceRef、3 unit rule、4 amount、factor、condition、component、UnitAdjustmentMasterRow、ServiceCodeMasterRow.OfficialLabel、bundleのUnitAdjustmentsとConditionDefinitionsを個別testで固定する。fixed compositeは正/負を保持し0を拒否する。

- [x] **Step 2: Domain testがcompile failureになることを確認する**

Run: dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~ClaimCalculationMasterContractTests -v normal

Expected: FAIL because v2 records do not exist.

- [x] **Step 3: 最小のv2 Domain型を実装する**

abstract recordとsealed recordでclosed unionを表す。値保持を基本とし、Domain constructorでは曖昧な0 FinalUnitsだけを拒否する。

- [x] **Step 4: Domain testを通す**

Run: Step 2と同じ。Expected: PASS.

---

### Task 2: public JSON Schema v2と空seed rootを切り替える

**Files:**
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs
- Modify: src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json
- Modify: 6 files under src/Tsumugi.Infrastructure/ClaimMasters/Seed/

- [x] **Step 1: schema root、sourceRefs、union、conditionDefinitionsの失敗testを書く**

schemaVersion 2、entry共通field、supports enum、4 amount、3 unit rule、condition kind/operator、component ref、baseUnitsを要求する。service-codesだけがconditionDefinitionsを持てることも固定する。

- [x] **Step 2: schema testがRedになることを確認する**

Run: dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_schema_resources_express_the_runtime_contract -v normal

Expected: FAIL because schema is v1.

- [x] **Step 3: schemaをv2 closed contractへ置換する**

oneOfでunit rule、amount、formula mode、condition value/valuesを排他化する。各objectはadditionalProperties false、percentage/rateはcanonical decimal string、finalUnitsは0を除外する。

- [x] **Step 4: 6空seedをv2へ切り替える**

全rootをschemaVersion 2へ変更し、service-codes.jsonだけconditionDefinitions空配列を追加する。entriesは空のまま維持する。

- [x] **Step 5: public schema testを通す**

Run: JsonClaimMasterProviderTests。Expected: v2 runtime未実装に直接依存しないschema testがPASS。

---

### Task 3: v2 parse、source provenance、condition contractを実装する

**Files:**
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs
- Modify: src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs
- Modify: src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/OfficeClaimProfilePolicyProviderTests.cs

- [x] **Step 1: 完全なv2 synthetic bundleとnegative casesを書く**

fixtureはbasic reward、4種類のaddition、fixed composite、unit addition、pass-through、factor chain、期間付きconditions、source refsを含める。v1、混在、未知field/kind、canonical decimal違反、condition型違反を個別にRedへする。

- [x] **Step 2: schema testsがRedになることを確認する**

Run: dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimMasterSchemaPhase31Tests -v normal

Expected: FAIL because runtime validator still expects v1 fields.

- [x] **Step 3: providerからtyped source catalog metadataを渡す**

validator inputをSHA dictionaryから、document ID、SHA、correctsを持つinternal typed catalogへ変更する。source catalog自体のschemaVersionは1のまま維持する。

- [x] **Step 4: v2 header、entry、sourceRefs、typed unionをparseする**

検証順は設計書§13の順を守る。全errorはfile、entry/condition key、field、可能なら参照先を含める。

- [x] **Step 5: supports単位の有効正本を検証する**

correctsをnewerからolderでindex化し、candidate reachable subgraphのcycle、複数maximal、chain外authoritativeを拒否する。基点authoritativeと採用末端correctionはsourceRefsに必要だが、中間correction refは省略可能とする。

- [x] **Step 6: condition shape、期間、coverage、AND intersectionを検証する**

token/booleanは集合intersection、integerは開閉境界付きintervalとして空集合を判定する。未定義、未使用、期間未coverageを拒否し、明示retirementは許可する。

- [x] **Step 7: focused schema testsを通す**

Run: Step 2と同じ。Expected: PASS.

- [x] **Step 8: policy providerのv2 sourceRefs fixtureを通す**

旧sourceLocator削除testをsourceRefs欠落又は不正refへ置換し、transition ruleの全sourceRefs documentがreleaseに所属するfail-closed契約を固定する。

Run: dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~OfficeClaimProfilePolicyProviderTests -v normal

Expected: PASS.

---

### Task 4: component、selector graph、step/rounding matrixを実装する

**Files:**
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs
- Modify: src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs

- [x] **Step 1: cross-file negative casesを追加する**

component不足/role/期間不足、serviceとadditionの構造不一致、target空集合/自己参照、種類別cycle、未知runtime selector、factor subset/order/rate、mode混在、proration非正値とmatrix不整合を個別にRedへする。

- [x] **Step 2: 追加testが期待理由でRedになることを確認する**

Run: Task 3 Step 2と同じ。

- [x] **Step 3: 期間indexとselector indexでcross-file validationを実装する**

component annotation、addition target graph、service unit-addition target graphを混ぜずに検証する。月境界でactive rowを解決し、各依存月でちょうど1件になることを要求する。

- [x] **Step 4: closed step/rounding matrixを実装する**

設計書§8.4だけを許可する。factor orderは1始まり連続、rateは0超1以下、pass-throughはfactorなし/rounding nullとする。

- [x] **Step 5: cross-file testsを通す**

Run: Task 3 Step 2と同じ。Expected: PASS.

---

### Task 5: 代表gap、scale、embedded provider、全体品質ゲートを閉じる

**Files:**
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs
- Modify: tests/Tsumugi.Infrastructure.Tests/ExternalSpecificationLiteralGuard.cs
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimSpecificationBoundaryTests.cs

- [x] **Step 1: 6代表fixtureと14,709件scale testを追加する**

設計書§15.4のrow 7、941、908、913、1044、907を最小fixtureで固定する。scale testは厳格な時間assertを置かず、validatorが正常終了することだけを確認する。

- [x] **Step 2: external specification guardをsourceRefsへ移行する**

entryとconditionDefinitionsの全sourceRefs[].documentIdを検査し、未知documentと空sourceRefsをfail-closedで報告する。ClaimSpecificationBoundaryTestsのv1 fixtureとsourceDocumentId JSON Pointerをv2へ更新する。

- [x] **Step 3: focused testsを順に実行する**

Run:

    dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~ClaimCalculationMasterContractTests -v normal
    dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~OfficeClaimProfilePolicyProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' -v normal

Expected: PASS, warnings 0.

- [x] **Step 4: Domain/Infrastructure testを実行する**

Run:

    dotnet test tests/Tsumugi.Domain.Tests -v minimal
    dotnet test tests/Tsumugi.Infrastructure.Tests -v minimal

Expected: PASS, warnings 0.

- [x] **Step 5: formatterとdiff checkを実行する**

Run:

    dotnet format --verify-no-changes
    git diff --check

Expected: exit 0.

- [x] **Step 6: 最終CIを実行する**

@superpowers:verification-before-completion を読み、fresh ./build/ci.sh が CI OK になることを確認する。

- [x] **Step 7: 最終差分をレビューする**

@superpowers:requesting-code-review で設計書§18、変更17ファイル、focused testとCI結果を渡し、Major指摘がなくなるまで修正と再検証する。

---

### Task 6: follow-upでpercentage application kindを追加する

**Files:**
- Modify: docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md
- Modify: tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs
- Modify: tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs
- Modify: src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs
- Modify: src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json
- Modify: src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs

- [x] **Step 1: applicationKind contractの失敗testを書く**

Domainで`add`、`subtract`、`replace`の値保持を要求する。runtime validatorでfield欠落と未知値を拒否し、public JSON Schemaで必須closed enumであることを固定する。

- [x] **Step 2: focused testが期待理由でRedになることを確認する**

Run:

    dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~ClaimCalculationMasterContractTests -v normal
    dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_schema_resources_express_the_runtime_contract' -v normal

Expected: FAIL because Domain enum、JSON Schema及びvalidatorに`applicationKind`がない。

- [x] **Step 3: required closed contractを実装する**

Domainへ`PercentageApplicationKind`を追加し、`PercentageOfTargetAmount`で保持する。JSON fieldは`applicationKind`とし、`add | subtract | replace`以外を拒否する。既定値やsigned percentageへの変換は追加しない。

- [x] **Step 4: focused testと回帰testを通す**

Run:

    dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~ClaimCalculationMasterContractTests -v normal
    dotnet test tests/Tsumugi.Infrastructure.Tests --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests' -v normal

Expected: PASS, warnings 0. Task 13 manifest、seed及び監査decisionは変更しない。
