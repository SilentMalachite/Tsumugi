# ADR 0018: WageAdjustment を append-only 特別手当レコードとして導入

**ステータス**: 確定（2026-07-05）

## 結論

`WageAdjustment` を利用者×月の任意特別手当を表す独立 append-only エンティティとして導入する。3 ファクトリ（New / Correction / Cancel）と `WageAdjustmentPolicy.SumEffective` 純粋関数を Domain に配置し、工賃計算の最終ステップで手当合計を加算する。

## 背景

KouchinModule v5（`.xlsm`）の突合（2026-07-05）で、工賃集計シートの G 列に「特別手当」の手入力欄があることを確認した。この特別手当は:

- **利用者ごと・月ごとに金額が異なる**（事業所一律ではない）
- 作業手当（`WorkAllowancePerDayYen`）/ 職能手当（`SkillAllowanceTiers`）とは異なり、事業所ルールで機械的に算出できない
- 臨時的・任意的支給のため、恒久的な Strategy として表現するのは semantics が合わない

既存の 4 方式 Strategy（Piece/Hourly/Fixed/Equal）はいずれも「計算ロジック」の差分であり、「利用者ごとの任意金額」を持つ方式を追加することは ADR 0012 の設計境界（方式戦略は事業所設定・汎用ロジック）を逸脱する。

## 選択肢

### A: 5 番目の Strategy として実装

- 利点: 既存フレームワークを使い回せる。
- 欠点: `IWageMethodStrategy` の責務（計算方式）と「任意金額レコードの保持」が混在する。ADR 0012 の「4 方式で固定」を破る。却下。

### B: 独立 append-only レコード `WageAdjustment`（採用）

- 利点: 特別手当を「計算ロジック」とは独立したデータ事実として扱える。追記型なので変更履歴が残る。Correction / Cancel ファクトリで誤り訂正・取消が可能。
- 欠点: 新テーブルが増える。

### C: `WageSettings` に手当欄を追加

- 利点: 追加テーブルなし。
- 欠点: `WageSettings` は事業所の設定マスタであり、利用者ごとの個別金額を持つ設計にはなじまない。月ごとの任意支給額が表現できない。却下。

## 決定

選択肢 B を採用。`WageAdjustment` を以下の仕様で実装する:

- **3 ファクトリ**: `WageAdjustment.New(…)` / `.Correction(origin, …)` / `.Cancel(origin, …)`
- **純粋関数**: `WageAdjustmentPolicy.EffectiveYen(records, recipientId, month)` — 指定利用者×月の実効金額を導出
- **純粋関数**: `WageAdjustmentPolicy.SumEffective(records, month)` — 月次全利用者の手当合計（工賃集計に加算）
- **DB テーブル**: `WageAdjustments`（`WageAdjustmentConfiguration` で partial unique index なし、New が月複数あっても意味上正当）

## 影響

- 実装は Phase 4 S0 の Tasks 4 / 6 / 8 で完了済み。
- Application 層に 2 ユースケース（`RecordWageAdjustmentUseCase` / `QueryWageAdjustmentUseCase`）を追加。
- Infrastructure に `WageAdjustments` テーブルを追加（Phase 4 S0 マイグレーション）。
- App に「特別手当」タブを追加。
- `WageCalculator` の最終ステップで `WageAdjustmentPolicy.SumEffective` を加算するよう拡張。
- 関連 ADR: [0012 工賃計算の方式戦略・端数・年度起点](0012-wage-calculation-strategy.md)
