# 0039 CSV仕様を版レジストリで並存させ、処理対象年月で選ぶ

- 状態: 採用（2026-07-25）
- 関連: ADR 0024（国保連CSVと項目マッピング）/ ADR 0037（項目表の機械抽出）/ ADR 0038（行単位の出典）
- 一次資料: `interface-index-r7-10`（仕様書索引）/ `interface-index-r8-06-page-observed-30bf116b`（令和8年6月施行分の掲載ページ）

## 結論

CSV仕様を施行分ごとに**差し替える**のではなく、`csv-specification-versions.json` に
**適用期間つきで追記**して並存させる。適用版は**処理対象年月**（提出する月）で選び、
該当版が無ければ推測で現行版を使わず fail-close する。

確定時に記録する版も生成時に選ぶ版も、この 1 か所（`IClaimCsvSpecificationVersions`）から取る。

## 背景

### 版が動いたときに「作り直し」になっていた

インタフェース仕様書は施行分ごとに更新される（報酬改定の3年周期とは別で、実際の索引には
平成23年6月〜令和8年6月の 30 以上の施行分が並ぶ）。仕様データは `r7-10` の 1 版だけを
`LoadEmbedded()` が固定で読む構造で、版が動けば全体を書き換えるしかなかった。

### 版識別子が確定側と生成側で食い違っていた（production 不能のバグ）

- 確定（`CloseClaimUseCase`）が記録していた値: `ClaimFinalizationVersions.CsvSpecificationVersion`
  = **`"field-mapping-r7-10"`**（Phase 3-0 の暫定定数。doc comment に「Phase 3-2・3-3 の実装時に
  実仕様版へ置き換える」と書かれたまま残っていた）
- 生成（`ClaimCsvGenerator.SpecificationVersion`）が返していた値: catalog の版 = **`"r7-10"`**
- `ExportClaimCsvUseCase` は両者の等値を要求していた → **production では常に
  `CsvSpecificationVersionMismatch` で CSV が出せない**

CSV 出力の配線テストは行を直接 seed し、その fixture が `"r7-10"`（生成側の値）を使っていたため
検出できなかった。**実 `CloseClaimUseCase` → 実 `ExportClaimCsvUseCase` を通す統合テストが無かった**
ことが原因。

## 決定

1. **版レジストリ** `csv-specification-versions.json`。1 版は
   `version` / `label` / `effectiveFromProcessingMonth` / `effectiveToProcessingMonth` /
   `sourceRefs`（ADR 0038 と同型。`supports: ["applicability-period"]`）/ `applicabilityNote`。
   仕様ファイルは版接尾辞で並存させる（`common-{v}.json` / `provider-claim-{v}.json` /
   `field-mapping-{v}.json` / `spec-evidence-{v}.json`）。`sources.json` は版をまたいで共有する。
2. **選択の鍵は処理対象年月**（提出月）。サービス提供年月ではない。根拠は項目表の説明文が
   「サービス提供年月が平成24年3月以前は…」のように**過去のサービス提供年月を現行版の中で
   条件分岐**させていること（＝版は提出時点で決まる）。
3. **読み込み時に版の並びを検証**する: 重複・欠落・複数の無期限版・期間の逆転・出典欠落は
   すべて `InvalidDataException`。無期限（`effectiveTo: null`）は最新版だけに許し、かつ必須。
4. **確定時に記録する版は現行版**（`IClaimCsvSpecificationVersions.Current`）。この版文字列は
   PreviewHash にも入るため、プレビュー・確定・出力が同じ出所を使う（暫定定数は削除した）。
5. **出力時は処理対象年月に適用される版**を使い、出力履歴にも**実際に使った版**を記録する。
   確定時の版と食い違う場合は従来どおり fail-close（`CsvSpecificationVersionMismatch`）。
   適用版が無い場合は `CsvSpecificationVersionUnavailable`。

## 令和7年10月施行分が現行である根拠

索引には後続の「令和８年６月施行分」があるが、その掲載ページ
（`interface-index-r8-06-page-observed-30bf116b`、2026-07-25 取得・SHA-256 登録）には
**「インタフェース仕様書（都道府県編）」とその修正履歴だけ**が載っており、共通編・事業所編は
改訂されていない。したがって事業所からの請求に用いる共通編・事業所編は令和7年10月施行分が現行で、
`effectiveTo` は null。次の施行分で共通編・事業所編が改訂されたら、この版に `effectiveTo` を入れて
新版を追記する。

## 追記（2026-07-25・Codex レビュー由来）

適用期間の出典は**件数と注記だけでなく内容を照合する**。`sources.json` の SHA-256 と一致すること、
位置と原文引用があること、`applicability-period` を支持する出典が 1 件以上あることを読み込み時に検証し、
どれかが欠けると起動時に fail-close する（`CsvSpecificationRegistry.ValidateApplicabilityEvidence`）。
索引が差し替わったのに件数しか見ないと、**新しい施行分に気づかないまま旧版で請求データを作れてしまう**
（ADR 0038 の fail-close をレジストリ側にも効かせる）。証跡:
`A_stale_pinned_hash_for_the_applicability_source_fails_closed` /
`An_unregistered_applicability_document_fails_closed` /
`A_source_that_does_not_support_the_applicability_period_fails_closed`。

## 影響

- `IClaimCsvGenerator` から `SpecificationVersion` を外した（版の解決は 1 か所に集約）。
  `ClaimCsvGenerator` は単一 catalog 構成（テスト・診断用）と registry 構成（production）の 2 通り。
- `CalculateClaimUseCase` / `CloseClaimUseCase` / `ExportClaimCsvUseCase` が
  `IClaimCsvSpecificationVersions` を受け取る。
- `ClaimFinalizationVersions` から `CsvSpecificationVersion` を削除した。
- **実 close → 実 export の統合テストを追加**した
  （`ClaimPreviewProductionWiringTests.Real_close_then_generate_flow_...` の末尾）。
  これを入れた時点で上記の版不一致バグが RED として現れ、修正後に GREEN になった。
  同テストは CSV まで通すため、受給者証番号を仕様の 10 桁に収め、契約情報（`ContractedProvider`）を
  実 DB に seed する必要があった（どちらも従来は帳票だけを見ていて露出していなかった）。
- 処理対象年月 2025-09 以前（r7-10 の適用開始前）は fail-close する。過去分の再出力は、当時の版を
  レジストリへ追記して初めて可能になる（現時点では r7-10 より前の版は未登録）。

## 残り

- 版ごとの readiness（`ClaimInputRequirementProvider`）はまだ単一版。新版で必須項目が増えた場合、
  確定済み請求を新版で出力してよいかの判定は未実装（`docs/open-questions.md` で追跡）。
- 過去施行分（令和6年6月以前）の仕様データは未登録。必要になったら追記する。
