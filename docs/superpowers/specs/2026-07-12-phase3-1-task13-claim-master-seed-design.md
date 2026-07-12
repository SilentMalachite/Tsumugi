# Phase 3-1 Task 13 Claim Master Seed Design

**Status:** Approved design
**Date:** 2026-07-12
**Scope:** Task 13のみ — 一次資料取得、SHA-256検証、スキーマ適合監査、R6／R8制度実値seed、機械検証、独立全件照合

## 1. 目的

Task 12で確定したclaim master契約へ、令和6年・令和8年の就労継続支援B型の制度実値を、適用期間と再現可能な出典locator付きで投入する。対象は次の6 seedとする。

- `basic-rewards.json`
- `additions.json`
- `region-unit-prices.json`
- `burden-caps.json`
- `transition-rules.json`
- `service-codes.json`

値の正しさをproduction seedと同じ転記に由来するfixtureだけで証明しない。取得バイトのSHA-256、機械検証、実装担当者とは別の全件照合を組み合わせ、誤転記をfail closedで防ぐ。

## 2. 非目標

- Task 14以降のmaster provider、平均工賃算定、service code resolver、割合計算、ClaimCalculatorを実装しない。
- Phase 3-2帳票又はPhase 3-3 CSVへ進まない。
- 公式資料の原本をリポジトリへ保存又は再配布しない。
- SHA不一致、取得不能、locator不明又は制度値不明を近い版・前版・推測値で補完しない。
- Task 13内でTask 12のスキーマを場当たり的に拡張しない。
- production seedの全値を期待fixtureへ複製しない。

## 3. 実行順序

Task 13は次の依存順で実行する。

1. 一次資料取得とSHA-256完全性ゲート。
2. 全転記対象rowのスキーマ適合監査。
3. 空seedに対するcompletenessテストのRed確認。
4. 独立マスタ3種の投入。
5. service codeマスタの投入。
6. service codeを参照する基本報酬の投入。
7. selectorを参照する割合加減算の投入。
8. 6 seedの全体整合検証とCI。
9. 固定したcandidate commitに対する独立全件照合。
10. 取得・照合証跡の確定。

後続工程は、直前の停止ゲートを100%通過するまで開始しない。

## 4. 一次資料取得と完全性ゲート

### 4.1 対象資料

対象資料は、`src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`の次の5 releaseと、ADR 0020、0022、0023、0025が各制度値に指定するdocument IDに限定する。

- `claim-master-r6-04`
- `claim-master-r6-06`
- `claim-master-r7-01`
- `claim-master-r7-09`
- `claim-master-r8-06`

取得対象の閉集合は、`docs/spec-data/phase3/claim-master-source-row-manifest.json`の`documents`で固定する。manifestへ登録できるdocument IDは、5 releaseの`sourceDocumentIds`に含まれ、かつADR 0020、0022、0023、0025が6 master kindの制度値又は相互照合に指定するものだけとする。release bundleの全資料を無条件にTask 13対象へしない。

manifestのdocument ID、URL、期待SHA-256は`src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`と一致させ、手入力した別のURL又はSHAを正本にしない。Internet Archive又は自治体再配布を使うdocument IDも、ADR 0020に固定されたURLと取得バイトを使用する。

### 4.2 一時保存

原本は`/tmp/tsumugi-phase31-task13/sources/`へdocument IDを基にした安定名で保存する。PDF、XLSX、HTMLその他の取得物はgit管理対象へ置かない。ローカル一時パスは証跡へ記録しない。

### 4.3 完全性判定

各取得物について、未加工バイトの次を検証する。

- 取得成功。
- HTTPリダイレクト後の最終取得物が空でない。
- 実測SHA-256が`src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`及びADR 0020の期待値と一致する。
- locatorが指定するworkbook、sheet、row、cell又はphysical pageへ到達できる。

取得率の分母はmanifestの`documents`件数、SHA一致率の分母は取得済みdocument件数、locator到達率の分母はmanifestの`rows`件数とする。いずれかが100%未満なら、該当資料に依存する転記を開始しない。同一URLの差替えを検出した場合も、Task 13で既存document ID又はSHAを上書きしない。

### 4.4 source-side row manifest

`docs/spec-data/phase3/claim-master-source-row-manifest.json`を、公式資料側の対象閉集合と転記漏れ検出の正本として作成する。

```json
{
  "schemaVersion": "1",
  "documents": [
    {
      "documentId": "...",
      "sourceSha256": "...",
      "role": "authoritative",
      "extractionRanges": ["workbook-order=38;rows=..."]
    }
  ],
  "rows": [
    {
      "sourceDocumentId": "...",
      "sourceLocator": "...",
      "sourceLabel": "...",
      "effectiveFrom": "2026-06",
      "effectiveTo": null,
      "disposition": "seed",
      "masterKind": "service-codes",
      "seedKey": "...",
      "exclusionReason": null
    }
  ]
}
```

`role`は`authoritative`又は`cross-check`、`disposition`は`seed`、`excluded`又は`schema-gap`の閉集合とする。ADRが指定する抽出範囲内のB型関連rowを、採用rowだけでなく除外rowも含めて全件列挙する。

- `seed`: `masterKind`と`seedKey`を必須とし、production seedのちょうど1 rowへ対応させる。
- `excluded`: `exclusionReason`を必須とし、A型専用、別service、見出し、重複掲載などの具体的理由を記録する。
- `schema-gap`: 現行6型へ損失なく分類できない理由を必須とし、Task 13を停止する。

各documentの`extractionRanges`、manifestのrow数、公式資料から実際に列挙したrow数を一致させる。独立reviewerはseedだけでなく、この範囲とmanifestの全rowを照合する。

## 5. スキーマ適合監査

### 5.1 目的

Task 12の現行契約は、基本報酬、割合加減算、地域単価、負担上限、経過措置、service codeを表現する。一方、公式表には固定単位の独立加減算又は合成済み単位が含まれる可能性がある。転記開始前に、対象rowを現行型へ損失なく写像できることを確認する。

### 5.2 分類

全転記対象rowを次のいずれか1つへ分類する。

| 分類 | 現行の保存先 |
| --- | --- |
| 基本報酬単位 | `BasicRewardMasterRow` / `basic-rewards.json` |
| 割合加減算 | `PercentageAdjustmentMasterRow` / `additions.json` |
| 地域単価 | `RegionUnitPriceMasterRow` / `region-unit-prices.json` |
| 負担上限 | `BurdenCapMasterRow` / `burden-caps.json` |
| 経過措置 | `OfficeClaimProfileTransitionRuleMasterRow` / `transition-rules.json` |
| service codeとselector | `ServiceCodeMasterRow` / `service-codes.json` |

監査はmanifestの全rowについて、公式上の意味、必要な数値、適用期間、参照関係、source locatorが分類先の全必須fieldへ保存できることを確認する。採用rowは`seed`、対象外rowは`excluded`、分類不能rowは`schema-gap`として理由付きで記録する。

### 5.3 停止条件

次のいずれかが1件でもあればTask 13を停止する。

- どの分類にも入らない。
- 2つ以上の分類へ曖昧に入る。
- 公式値又は意味を現行fieldへ損失なく保存できない。
- 固定単位又は合成済み単位を割合として偽装しなければ保存できない。
- source rowとproduction rowが1対1又は明示した集約関係にならない。

実装計画は監査フェーズと条件付きseedフェーズに分ける。監査フェーズは資料取得、manifest作成、SHA／locator確認、分類までを完結させる。`schema-gap`が0件の場合だけseedフェーズを実行する。

停止時はmanifestを監査結果として保持し、Task 12へ戻る設計変更として、Domain model、JSON Schema、validator、schema testsの影響範囲を提示する。Task 13のseed転記と同じ変更へ混ぜない。これにより、スキーマ適合性が未確定でも実装計画の監査フェーズは単独で実行可能にする。

## 6. seed転記の依存順

### 6.1 独立マスタ

最初に次の3 seedを投入する。

- `burden-caps.json`
- `region-unit-prices.json`
- `transition-rules.json`

これらはservice code又はselector参照を持たない。4負担区分、地域区分単価、28 payment band optionとR8経過措置の期間・閉集合を個別に検証する。

### 6.2 service code

次に`service-codes.json`へ、適用版ごとの就労継続支援B型service code、service kind、selectorを投入する。R8 B型はADR 0020で指定された`r8-service-codes-2-xlsx`のworkbook順38〜41を正本とし、R6は対応するR6 workbook範囲を使用する。PDF、報酬構造、告示は相互照合に使うが、locator不明の値を作らない。

### 6.3 基本報酬

`basic-rewards.json`はservice code投入後に作成する。各rowの`serviceCode`は同じ適用期間全体を覆うservice code rowへちょうど1件解決しなければならない。payment band、staffing、capacity、units、service codeの組を一意にする。

### 6.4 割合加減算

`additions.json`は最後に投入する。各`targetSelector`は同じ適用期間全体を覆う1件以上のservice code selectorへ解決する。`percentageBaseScope`、`percentageApplicationKind`、`calculationOrder`、`roundingRuleId`、`calculationStepId`はADR 0025とsource rowから明示的に決め、名称又は割合だけから推測しない。

## 7. 共通provenance契約

全production rowは次を保持する。

- 安定した`key`。
- `effectiveFrom`と任意の`effectiveTo`。
- `sourceDocumentId`。
- `sourceSha256`。
- sheet / row / cell、workbook-order / row又はphysical pageを一意に示す`sourceLocator`。
- master kind固有のtyped `values`。

同じ制度値を複数releaseで継続使用する場合も、supersedes chainと適用期間を維持する。将来版を過去月へ遡及せず、期間の穴を直近値で埋めない。

## 8. テスト設計

### 8.1 Red

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`を新規作成し、現在の空seedに対して失敗することを先に確認する。

### 8.2 seed別検証

1つのtest class内でmaster kindごとの検証を分離し、段階的にfilter実行できるようにする。各kindで次を検査する。

- 期待するrelease、件数、key集合。
- effective periodの開始・終了、穴・重複。
- 必須値の閉集合と非負性。
- source document ID、SHA、非空locator。
- 代表的な公式値と版境界。
- manifestの`disposition = seed`全件とproduction seedの`masterKind + seedKey`集合の完全一致。
- manifestの`excluded`全件に非空の除外理由があり、production seedへ混入しないこと。
- `schema-gap`が1件でもあればseed completenessを成功させないこと。

production seedの全数値をfixtureへ複製しない。代表値はproduction seedとは別に一次資料の指定位置から選ぶ。

### 8.3 全体整合

6 seedを`ClaimMasterFileValidator.ValidateAll`相当のproduction経路で同時に検証し、次を確認する。

- basic rewardからservice codeへの参照が0件又は複数件にならない。
- percentage adjustmentのselectorが空集合にならない。
- 同一対象の`calculationOrder`に穴、重複、循環がない。
- master keyとeffective periodに重複がない。
- 5 releaseの2026-05／2026-06境界をコード変更なしで切り替えられる。
- source catalogにないdocument ID又は不一致SHAを拒否する。
- manifestの抽出範囲件数、source-side row数、`seed + excluded + schema-gap`件数が一致する。

既存の`JsonClaimMasterProviderTests`と`ClaimSpecificationBoundaryTests`も回帰確認として実行する。

### 8.4 最終品質ゲート

focused testsの後に`./build/ci.sh`を実行し、警告・エラー0、全テスト成功、既存coverage／architecture／offline gate通過を要求する。

## 9. 独立全件照合

### 9.1 candidate commit

source-side manifest、seed 6ファイル、`ClaimMasterSeedPhase31Tests.cs`をcandidate data commitとして先に固定する。レビュー対象はworking treeではなく、このcommit hashとする。`schema-gap`がある場合はcandidate data commitを作らず、監査結果をblockedとして報告する。

### 9.2 reviewer分離

転記を行っていない別担当者又はfresh subagentをsource-data reviewerに割り当てる。reviewerへ渡す入力は次に限定する。

- candidate data commit hash。
- source-side manifestのpath。
- 6 seedのpath。
- ADR 0020、0022、0023、0025。
- SHA検証済み一次資料の一時path。
- source catalog。

reviewerは各`extractionRanges`が公式資料のB型対象範囲を漏れなく覆うこと、範囲内の全rowがmanifestへ存在すること、各rowの`disposition`と理由が妥当であることを先に確認する。その後、`seed`全rowのdocument ID、SHA、locator、key、値・code、effective periodをproduction seedと一次資料へ照合する。seed別row数、除外row数、総source row数を機械集計し、照合数がmanifest総row数と一致することを確認する。

### 9.3 判定

合格条件は次の全てとする。

- SHA一致率100%。
- locator到達率100%。
- source-side manifest網羅率100%。
- 全source row照合率100%。
- 値、code、期間のdiscrepancy 0。
- 未説明の除外row及び`schema-gap` 0。
- reviewer判定`Approved`。

Issues Foundの場合、実装担当がseedを修正して新candidate commitを作り、同じ独立reviewerが全rowを再レビューする。差分行だけの再レビューでは合格にしない。

## 10. 証跡とcommit境界

`docs/phase3-1-master-transcription-review.md`は、candidate data commitのレビューがApprovedになった後に作成する。次を記録する。

- 対象candidate commit hash。
- document ID、URL、期待SHA、実測SHA、bytes、取得日時、判定。
- スキーマ適合監査の分類別row数、除外row数、分類不能数、判定。
- manifest総row数、seed別row数、照合数、discrepancy数。
- reviewer task ID又は氏名、実施日時、最終判定。
- focused testsと`./build/ci.sh`の結果。

証跡はcandidate data commitとは別のevidence commitにする。これにより、証跡内の対象commit hashと実際にレビューしたデータを一致させる。

## 11. エラー処理

次の事象はすべて停止条件とする。

- 取得失敗又は空レスポンス。
- SHA不一致。
- locator不明又は到達不能。
- source間で値又は適用期間が矛盾する。
- スキーマ分類不能又は損失のある写像。
- service code又はselector参照不整合。
- 期間の穴、重複又は将来版の遡及。
- 独立レビューのdiscrepancy。

停止理由が公式資料から一意に解消できない場合は`docs/open-questions.md`へ起票する。Task 13の完了証跡を先に作らない。

## 12. 変更対象

### Task 13で変更するファイル

- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Create: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Create: `docs/phase3-1-master-transcription-review.md`

`docs/open-questions.md`は未解決事項が生じた場合だけ変更する。

### 停止ゲートでのみ候補になるTask 12再変更

スキーマ適合監査が失敗した場合、次はTask 13の変更対象へ自動追加しない。別途、変更理由と対象を報告してTask 12の再設計対象とする。

- `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`
- `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`

## 13. 完了条件

Task 13は次の全条件を満たした場合だけ完了とする。

- 取得率、SHA一致率、locator到達率が100%。
- スキーマ適合監査で分類不能・曖昧分類・損失写像が0件。
- source-side manifestが指定範囲内の採用・除外rowを100%列挙し、未説明の除外がない。
- 6 seedが空でなく、対象版・対象rowを網羅する。
- focused testsと`./build/ci.sh`が成功する。
- candidate data commitが独立全件照合でApproved。
- `docs/phase3-1-master-transcription-review.md`がcandidate commitと全件数を一意に参照する。
- 原本がgit管理対象へ混入していない。
