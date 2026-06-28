# ADR 0012: 工賃計算の方式戦略・端数・年度起点

## 結論
- 方式戦略を 4 種類（Piece/Hourly/Fixed/Equal）並存で実装し、既定は `WageSettings.Method`（期間マスタ）で事業所運用に委ねる。
- 端数規則: `RoundingRule.FloorYen`（円未満切り捨て）を暫定既定。
- 余り処理（按分時の Σ＝原資 維持）: `RemainderPolicy.LargestRemainder`（最大剰余法、同点時は `RecipientId` 昇順）を暫定既定。`ReserveToOffice` も実装（残余を事業所留保）。
- 年度起点: `FiscalYearStartMonth=4`（日本会計年度）。
- 確定後の下層訂正は自動再計算しない。再確定は `WageStatement` の `Correction` で履歴に残す。

## 背景
- B型は非雇用であり最低賃金法の対象外。よって最低賃金チェックは入れない。
- KouchinModule.bas v5 の実挙動が一次情報。本 ADR は突合完了までの**暫定**で、突合後に「確定」へ書き換える。

## 選択肢
1. 既定方式を 1 つに固定 → 事業所運用ごとに変えにくい。却下。
2. 期間マスタ（WageSettings）に委ねて 4 方式並存 → **採用**。
3. プラグイン拡張で追加方式を許す → YAGNI。

## 決定
期間マスタ（`WageSettings`）に `Method` フィールドを設け、Piece/Hourly/Fixed/Equal の 4 方式を並存実装する。Domain は `IWageMethodStrategy` インターフェースで 4 実装を持ち、Application 層のユースケースがマスタ値に応じて選択する。

## 影響
- Domain は `IWageMethodStrategy` で 4 実装を持つ。
- 報酬告示由来の数値は本 ADR に**含めない**（ハードコード禁止）。
- 突合後に open-questions の該当チェックボックスを閉じる。
