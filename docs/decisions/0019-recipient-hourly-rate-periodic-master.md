# ADR 0019: RecipientHourlyRate を利用者×期間の時給期間マスタとして導入

**ステータス**: 確定（2026-07-05）

## 結論

`RecipientHourlyRate` を利用者×期間の時給を表す独立 append-only エンティティとして導入する。3 ファクトリ（New / Correction / Cancel）と `RecipientHourlyRatePolicy.EffectiveYen(records, recipientId, asOf)` 純粋関数を Domain に配置し、`HourlyWageStrategy` が Application 層経由で日単位の実効時給を引き当てる。

## 背景

KouchinModule v5（`.xlsm`）の突合（2026-07-05）で、`AU12` セルが月中の時給変動を検知する式（`MAX(AA9:AA39) != MIN(AA9:AA39)`）を持つことを確認した。これは:

- 時給が **利用者ごとの属性**（事業所一律ではない）
- 月中に時給が変動しうる（例: 月途中の昇給）
- 変動があった場合、日単位で正確に按分する必要がある

時給を単純な「利用者プロパティ」として `Recipient` に持たせると、期間外の参照ができなくなる。`WorkRecord` に時給列を追加すると変更前後の履歴が個別レコードに分散し、遡及修正が困難になる。

## 選択肢

### A: `WorkRecord` に時給列を追加

- 利点: テーブル追加なし。
- 欠点: 時給変更のたびに全 WorkRecord を更新するか、変更履歴が個別レコードに分散する。追記型モデルと相性が悪い。却下。

### B: `WageSettings` に利用者マップを追加

- 利点: 既存テーブルを使い回せる。
- 欠点: `WageSettings` は事業所設定マスタ（期間 × 事業所）。ここに利用者マップを持たせると二軸（office × recipient）の期間管理が複雑化する。かつ利用者ごとの時給変動履歴が保持できない。却下。

### C: 独立 append-only エンティティ `RecipientHourlyRate`（採用）

- 利点: 期間管理が明確（`DateRange` で表現）。追記型なので変更履歴が残る。`RecipientHourlyRatePolicy.EffectiveYen(asOf)` で特定日の実効時給を純粋関数で導出できる。月中変動も正確に表現できる。
- 欠点: 新テーブルが増える。

## 決定

選択肢 C を採用。`RecipientHourlyRate` を以下の仕様で実装する:

- **3 ファクトリ**: `RecipientHourlyRate.New(…)` / `.Correction(origin, …)` / `.Cancel(origin, …)`
- **`DateRange Period`**: 有効期間。オープンエンド（`To = null`）で「現在から継続」を表現
- **純粋関数**: `RecipientHourlyRatePolicy.EffectiveYen(records, recipientId, asOf)` — 特定日の実効時給（円）を導出
- **DB テーブル**: `RecipientHourlyRates`（`RecipientHourlyRateConfiguration`）

`HourlyWageStrategy` は Application 層の `IRecipientHourlyRateRepository` 経由で日単位の実効時給を引き当て、`DailyHourlyBasis` 値オブジェクトに詰めて計算する。

## 影響

- 実装は Phase 4 S0 の Tasks 3 / 6 / 7 / 9 で完了済み。
- Application 層に 2 ユースケース（`SetRecipientHourlyRateUseCase` / `QueryRecipientHourlyRateUseCase`）を追加。
- Infrastructure に `RecipientHourlyRates` テーブルを追加（Phase 4 S0 マイグレーション）。
- App に「利用者時給」タブを追加。
- `HourlyWageStrategy` が `RecipientHourlyRatePolicy.EffectiveYen` を呼ぶ形に拡張。月中に時給変動がある場合は日単位での集計が可能。
- 関連 ADR: [0012 工賃計算の方式戦略・端数・年度起点](0012-wage-calculation-strategy.md)
