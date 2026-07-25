# 0040 新しい施行分では「試して項目単位で判定する」

- 状態: 採用（2026-07-25）
- 関連: ADR 0039（版レジストリと処理対象年月での選択）/ ADR 0032（契約情報の個別入力）
- 決定者: 利用者（方1＋方2＋`Current` 修正）

## 結論

確定時の版と、処理対象年月に適用される版が違っても**入口では止めない**。解決した版で生成を試し、
足りない項目があれば**項目単位で** fail-close する。「この月を新版で出すのに何が必要か」は
**全件まとめて**返す（`ExportClaimCsvUseCase.ValidateAsync`）。

## 背景

新しい施行分が必須項目を増やしたときの状況は 3 つに分かれる。

| パターン | 確定済み snapshot で出せるか |
|---|---|
| ① 新版は項目を増やさない（レイアウト微修正・説明の明確化） | 出せる |
| ② 必須項目が増えたが、そのデータは snapshot が既に持っている | 出せる |
| ③ snapshot に無いデータを要求する | 出せない（入力＋再確定が必要） |

ADR 0039 の実装は 3 つを区別せず、版が違えば全部 `CsvSpecificationVersionMismatch` で止めていた。
③には正しいが、①②では**不要な再確定**を強いる（revision が増え、対象月をすべて閉じ直す手間がかかる）。

判定器はすでに存在した: 生成器と encoder は項目単位で fail-close する（`MissingRequired` /
`UnresolvableModelPath` など、いずれも fieldId を持つ）。足りなかったのは
「入口で塞がないこと」と「最初の1件で止めずに全件集めること」だけだった。

## 決定

1. **出力は解決した版で試す。** 版の違いそのものでは止めない。生成できればそれが答え（①②）。
   出力履歴には**実際に使った版**と**確定時の版**の両方を記録する
   （`ClaimCsvExport.CsvSpecificationVersion` / `FinalizedCsvSpecificationVersion`）。
   両者が異なる行は「旧版で確定した請求を新版で出した」という監査上の事実として残る。
2. **不足は全件返す。** `IClaimCsvGenerator.CollectIssues(dto)` が、生成を試して
   `ClaimCsvFieldIssue`（fieldId / reason / detail / 受給者の内部参照コード）を集める。
   `ExportClaimCsvUseCase.ValidateAsync` が確定時の版・解決版・不足一覧を返し、
   UI（`ClaimCsvExportSection.MissingFieldSummaries`）が一覧で示す。
   氏名・受給者証番号は載せない（CLAUDE.md §ハード制約4）。
3. **`Current` は「その時点で適用される版」**（`TimeProvider.GetLocalNow` 基準）。
   「登録済みの最新版」ではない。次の施行分を**事前登録**したとき、最新版はまだ適用開始前なので、
   それを確定時に記録すると出力側（処理対象年月で解決）と必ず食い違い、**全件が版不一致**になる。
   暦月（施行月に入ったか）の判定なので local 時刻を使う（監査のタイムスタンプは従来どおり UTC）。
   端末の日付がどの版の適用期間にも入らない場合は fail-close する。

## ③のときの運用手順

1. 出力を試す → 項目単位で失敗し、不足一覧が画面に出る。
2. 不足項目を入力する（`ClaimInputView` / `DailyRecordView` / `CertificateView`）。
3. 請求を**再確定**する（Correct revision）。確定時の版が新版になり、readiness も新版で検証される。
4. 再度出力する。

## 影響

- `ClaimCsvExports` に `FinalizedCsvSpecificationVersion` 列を追加
  （migration `Phase33ClaimCsvExportFinalizedSpecVersion`。既存行は空文字ではなく
  `NewRecord` の既定で「使った版と同じ」になる。既存行は移行時に既定値 "" が入るため、
  過去行の解釈は「確定時の版＝使った版」とみなす）。
- 版が違っても出せるようになったため、**版の差分レビュー（ADR 0037）と証跡（ADR 0038）が
  いっそう重要**になる。項目の意味が変わる改訂は機械的には検出できないので、
  新版を登録する作業自体が差分レビューを伴う。
- 歯の確認: 入口で版不一致を弾く実装に戻すと
  `Real_wiring_exports_under_the_version_that_applies_to_the_processing_month` が RED。
  `CollectIssues` を最初の 1 件で打ち切ると
  `Real_wiring_collects_every_missing_field_instead_of_only_the_first` が RED。
  `Current` を「登録済みの最新版」に戻すと
  `Current_is_the_version_in_force_now_not_the_newest_registered` が RED。

## 残り

- 版ごとの readiness（`IClaimInputRequirementProvider.GetRequirements(version)`）と、
  確定 snapshot から readiness 値を作る写像は未実装。段1・2 で「出せるか」は判定できるため、
  確定前に将来版で検査したい場合にだけ必要になる（`docs/open-questions.md` で追跡）。
