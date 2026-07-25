# 版ごとの requirement と確定 snapshot からの readiness 写像（実装計画）

- 作成: 2026-07-25
- 関連: ADR 0039（版レジストリ）/ ADR 0040（新版では試して項目単位で判定）
- 起点: `docs/open-questions.md`「確定前に将来の施行分で検査する」

> 進捗はこのファイルのチェックボックスが単一の正本。

## 目的

1. **版ごとの requirement**: `missing` マッピングから組む readiness 要件を、仕様版ごとに引けるようにする。
2. **確定 snapshot からの readiness 写像**: 確定済み請求が「別の版の要件を満たしているか」を、
   生成を試さずに判定できるようにする。
3. その 2 つを使って **確定前に将来の施行分で警告**する（事前登録した版の要件で不足を知らせる）。

## 事前調査で確定した事実

- readiness のパスは **30 件**（`Certificate` 3 / `ClaimInput` 9 / `ContractedProvider` 2 /
  `IntensiveSupportEpisode` 1 / `DailyRecord` 11 / `Office` 4）。
- **30 件すべて確定 snapshot（`ClaimFinalizationSnapshot`）から導出できる。**
  `Office` / `Certificate` / `Recipient` / `ClaimInput` / `ContractedProvider` /
  `IntensiveSupportEpisode` はそのまま持ち、`DailyRecord` 系は日次配列を集約すれば作れる。
- `ClaimPreparationContext` は値以外に **rowScopes**（導出可能: `BilledDays` と日次配列）、
  **effectiveCertificateCount**（確定 snapshot は実効証 1 件で構成されるので 1）、
  **evidence 状態**（`certificateClaimEvidence` / `upperLimitManagementStatement`）を持つ。
  **evidence 状態は確定 snapshot に無い。**
- 要件評価（条件評価と存在判定）は `ClaimPreparationReadiness` の private static に閉じている。

## 範囲の線引き（重要）

確定 snapshot からの評価は **「その版が要求する項目が確定 snapshot に入っているか」だけ**を答える。
evidence（受給者証の確認記録・上限額管理結果票）や証の重複といった**確定時点の検査は再現しない**。
再現できないものを既定値で埋めると、偽の合格または偽の不足を出すため。
→ 実装上は、snapshot 由来の評価が evidence 系の issue code を出さないことをテストで固定する。

## タスク

### 1. 要件評価の共通化（振る舞い不変のリファクタ）

- [ ] `ClaimPreparationReadiness` の `EvaluateCondition` / `IsPresent` /
      `AddMissingRequirementIssue` を internal な `ClaimRequirementEvaluator` へ抽出する
- [ ] `ClaimPreparationReadiness` は抽出先へ委譲する（既存テストが全緑のままであることが受け入れ基準）

### 2. 版ごとの requirement

- [ ] `IClaimInputRequirementProvider` を `GetRequirements(string specificationVersion)` へ変更
      （版を明示しない入口は残さない。どの版で評価したのか曖昧にしないため）
- [ ] `ClaimInputRequirementProvider` を版レジストリから構築し、版ごとの要件集合をキャッシュする。
      未登録の版は fail-close
- [ ] 帳票側のマッピング（`report-field-mapping-r8-06`）は CSV 仕様版に属さないため、
      **全版に共通で合流**する旨をコメントと doc に明記
- [ ] `ClaimPreparationReadiness.Evaluate(context, specificationVersion)` へ変更し、
      呼び出し側（`CalculateClaimUseCase` / `CloseClaimUseCase`）は
      `IClaimCsvSpecificationVersions.Current` を渡す
- [ ] テスト: 版ごとに要件件数が引けること／未登録版は fail-close

### 3. 確定 snapshot → readiness 値の写像

- [ ] `ClaimFinalizationReadinessContextBuilder`（Application）を追加。
      入力は `ClaimFinalizationSnapshot`、出力は値辞書＋rowScopes
- [ ] `DailyRecord` 系は既存の DB 側集約（`ClaimCalculationSnapshotReader`）と同じ規則で集約する
      （未入力を 0 として供給すると fail-open するもの＝算定時間数は null のまま）
- [ ] **歯**: DB 側 builder が作るキー集合と snapshot 側のキー集合が**完全一致**することをテストで固定
      （どちらかにパスを足して他方を忘れると RED）
- [ ] テスト: 各グループ 1 件以上の往復（値が入る／未入力は NotApplicable）

### 4. 出力側で使う（要件由来の不足を合流）

- [ ] `ExportClaimCsvUseCase.ValidateAsync` に、解決版の要件を確定 snapshot で評価した不足を合流。
      生成由来の issue と `fieldId` で重複排除し、由来（要件／生成）を区別できる形にする
- [ ] テスト: 契約情報の無い確定分で、要件由来と生成由来の両方が出て重複しないこと
- [ ] テスト: snapshot 由来の評価が evidence 系 issue を出さないこと（範囲の線引きの固定）

### 5. 確定前に将来の施行分で警告

- [ ] `IClaimCsvSpecificationVersions` に `UpcomingVersions`（適用開始前の登録済み版）を追加
- [ ] `ClaimPreviewDto` に警告一覧（`UpcomingSpecificationIssues`）を末尾 optional で追加。
      **`IsReady` は変えない**（将来版の不足で確定を止めない）
- [ ] `ClaimPreviewPipeline` が将来版の要件でも評価して警告を積む
- [ ] `ClaimPreparationViewModel` / `ClaimPreparationView` に「次の施行分で必要になる項目」を表示
- [ ] テスト: 将来版を事前登録したとき、現行版では ready のまま将来版の不足が警告に出ること

### 6. 仕上げ

- [ ] `dotnet format` / `./build/ci.sh` 緑
- [ ] ADR 0041 を追加（範囲の線引きと、evidence を再現しない理由を明記）
- [ ] `docs/open-questions.md` の該当項目をクローズ
- [ ] コミット
