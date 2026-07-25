# 0041 readiness を版ごとに引き、確定 snapshot でも評価できるようにする

- 状態: 採用（2026-07-25）
- 関連: ADR 0039（版レジストリ）/ ADR 0040（新版では試して項目単位で判定）
- 計画: `docs/superpowers/plans/2026-07-25-version-aware-readiness.md`

## 結論

1. readiness 要件は**仕様版ごとに**引く（`IClaimInputRequirementProvider.GetRequirements(version)`）。
   版を明示しない入口は置かない。
2. **確定 snapshot からも readiness の値を組める**ようにし、確定済み請求が別の版の要件を
   満たしているかを、生成を試さずに判定できるようにする。
3. これを使って **確定前に、事前登録済みの将来の施行分で必要になる項目を警告**する。
   警告は `IsReady` を変えない（まだ施行前の版のせいで今月の確定を止めない）。

## 範囲の線引き

確定 snapshot からの評価が答えるのは **「その版が要求する項目が確定 snapshot に入っているか」だけ**。
受給者証の確認記録（evidence）・上限額管理結果票・証の重複といった**確定時点の検査は再現しない**
（確定 snapshot がそれらを運ばない）。再現できないものを既定値で埋めると、偽の合格または偽の不足を出す。
そのため `ClaimFinalizationReadinessContextBuilder` が返す issue は `MissingRequiredField` と
`UnresolvedRequirementCondition` に限られ、それをテストで固定する。

## 決定

- **値の組み立て（path キーを書く場所）を 1 関数に集約した。** 当初計画は「2 つの builder ＋
  キー集合一致テスト」だったが、確定前（DB 由来）と確定後（snapshot 由来）の値を素の値レコード
  （`ClaimReadinessClaimInput` / `ClaimReadinessCertificate` / `ClaimReadinessContractedProvider` /
  `ClaimReadinessOffice`）へ詰め替えてから**同じ組み立て関数**を通す形にした。
  ドリフトの可能性自体を消せるため（キー集合一致テストは念のため残してある）。
- **条件評価も 1 か所に集約した**（`ClaimRequirementEvaluator`）。2 か所に持つと、版が動いたときに
  「確定時は通ったのに出力時は落ちる（またはその逆）」の検証ができなくなる。
- **帳票側のマッピング**（`report-field-mapping-r8-06`）は CSV 仕様版に属さないため、
  全版へ共通で合流する（帳票の様式は別の文書系列で改訂される）。
- 未登録の版の要件を求められたら fail-close する。「requirement が引けないから現行版で代用」は、
  新版で必須になった項目を確認せずに通すことになる。
- 出力時の検証（`ExportClaimCsvUseCase.ValidateAsync`）は **2 つの由来**を合わせて返す。
  (1) 要件由来: 解決版の要件を確定 snapshot で評価した不足。(2) 生成由来: 実際に生成を試して
  encoder が落ちた項目。同じ項目は fieldId で重複排除する。

## 影響

- `ClaimPreparationReadiness.Evaluate(context, specificationVersion)` に変更。
  呼び出し側は `IClaimCsvSpecificationVersions.Current` を渡す。
- `ClaimPreviewDto.UpcomingSpecificationIssues`（末尾 optional）を追加。
  `ClaimPreparationViewModel.UpcomingSpecificationWarnings` が「次の施行分 {版}: {項目}」を表示する。
- 歯の確認:
  - `The_snapshot_supplies_exactly_the_paths_that_readiness_knows`（キー集合の一致）
  - `Unentered_billed_hours_stay_not_applicable`（未入力を 0 にすると fail-open する項目）
  - `The_snapshot_evaluation_never_reports_evidence_issues`（範囲の線引き）
  - `An_unregistered_version_fails_closed`
  - `Execute_warns_about_upcoming_versions_without_blocking_readiness`（警告は確定を止めない）

## 残り

- 将来版の警告は**現行版に無い不足だけ**を出す（現行版で既に出ている issue は重複させない）。
  将来版で条件が変わって「現行版では必須だが新版では任意」になる場合の差分表示は未実装。
