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

## 将来版との差分は両方向を示す（2026-07-25 追記）

将来版の情報は「締まる方向」だけでなく**緩む方向**も出す。

| 変化 | 意味 | 表示 | 確定 |
|---|---|---|---|
| `BecomesRequired` | 現行版では求められないが次の施行分で必要になる | 「次の施行分 {版} で必要になります: {項目}」 | 止めない |
| `BecomesOptional` | 現行版では不足として止まっているが次の施行分では求められなくなる | 「次の施行分 {版} では不要になります（この施行分で提出する場合は今も必要）: {項目}」 | **止め続ける** |

緩む方向を伏せると、運用者は「次の施行分まで待てば入力不要な項目」のために入力させられていることに
気付けない。一方で**自動では緩めない**。確定は現行版の要件で止め続ける。理由は、確定した請求を
現行版の適用期間内に提出する可能性があり、その場合は今も必要な項目だから（自動で緩めると提出先で弾かれる）。
どちらの施行分で提出するかは運用者の判断であり、アプリは両方の事実を示すだけにする。

比較は **(受給者, 項目) 単位**。同じ項目で issue code だけが変わる場合（例: 必須 → 条件付きで
条件が評価不能）は「変化なし」として扱い、両方向に出して二重に見せない。

算定不成立（not-ready）のときもこの情報は運ぶ。緩む方向はまさに not-ready のときに現れるため、
早期 return で落とすと意味がなくなる。

## 残り

- 出力時（`ExportClaimCsvUseCase.ValidateAsync`）は解決版の要件だけを見るため、緩んだ項目は
  自動的に不足として出なくなる（追加対応は不要）。

## 追記（2026-07-25・Codex レビュー由来）

1. **日次記録の縮約は 1 か所**（`ClaimDailyRecordReduction`）。確定前（DB 由来）と確定後（snapshot 由来）で
   規則が分かれていた（時刻・区分は「先頭日の値」対「最初に入力された日の値」、受給者確認は「全日確認」対
   「最初の非 Unspecified」）。規則が違うと**確定できた請求が再評価では項目不足**になる。
   正本は `ClaimDailyRecordAggregate` の doc-comment、実装は共有関数だけに置く。
   証跡: `ClaimDailyRecordReductionTests`（6 件）。
2. **要件由来の不足は仕様上の fieldId へ展開して返す**。`ClaimPreparationIssue.FieldCode` は
   モデル path なので、そのまま `ClaimCsvFieldIssue.FieldId` に載せると DTO 契約に反し、生成由来の
   同じ不足と重複排除されず二重に見える。要件が束ねている項目全件へ展開する。
3. **不足一覧は画面に出す**。ViewModel が集めるだけで表示が無く、利用者には「次の項目を入力して
   ください」だけが見えていた（ADR 0040 の約束が UI で果たされていなかった）。
   証跡: `ClaimPreparationView_lists_every_missing_field_of_the_csv_export`。
4. `Current` を「登録済みの最新版」と同一視するテストを直した。将来の施行分を事前登録すると
   正しい実装が落ち、**版の事前登録という主要ユースケースを CI が阻害する**状態だった。
