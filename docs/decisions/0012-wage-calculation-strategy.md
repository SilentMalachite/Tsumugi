# ADR 0012: 工賃計算の方式戦略・端数・年度起点

**ステータス**: 確定（2026-07-05 — KouchinModule v5 突合により確定）

## 結論

- **方式戦略**: 4 種類（Piece/Hourly/Fixed/Equal）並存で実装し、既定は `Hourly`。`WageSettings.Method`（期間マスタ）で事業所ごとに変更可。他 3 方式は互換保持。
- **端数規則**: `RoundingRule.HalfUp`（四捨五入）を既定。`RoundingPolicy` に集約し、丸めスコープは「per-rate 集計後 1 回」。
- **余り処理**（按分時の Σ＝原資 維持）: `RemainderPolicy.LargestRemainder`（最大剰余法、同点時は `RecipientId` 昇順）。`ReserveToOffice` も実装（残余を事業所留保）。
- **年度起点**: `FiscalYearStartMonth = 4`（日本会計年度）。KouchinModule v5 突合済み。
- **手当規則**: `WageSettings` に `WorkAllowancePerDayYen`（作業手当日額）/ `SkillAllowanceTiers`（職能手当閾値表）/ `HourUnitMinutes`（工賃時給最小単位）を追加。特別手当は `WageAdjustment` エンティティで受ける（ADR 0018）。
- **時給の期間管理**: 利用者×期間の `RecipientHourlyRate` エンティティで保持（ADR 0019）。
- 確定後の下層訂正は自動再計算しない。再確定は `WageStatement` の `Correction` で履歴に残す。

## 背景

- B型は非雇用であり最低賃金法の対象外。よって最低賃金チェックは入れない。
- KouchinModule.bas v5（`.xlsm`）の実挙動が一次情報。2026-07-05 に `.xlsm` を検査し、以下を確認:
  - `BD5` セル: `= ROUND(…, 0)` 数式 → 四捨五入（HalfUp）を確認。
  - `AY9` セル: `1/96日 = 15分` → `HourUnitMinutes = 15` を確認。
  - 時給は利用者別に `AU12` が月中変動を検知（`MAX(AA9:AA39) != MIN(AA9:AA39)`）。
- 旧 ADR（2026-06-28 作成）は端数規則を `FloorYen` と暫定していたが、突合結果 `HalfUp` に修正。

### 実装補足（丸めスコープの semantic drift）

`.xlsm` の `BD5` は per-day での丸めに相当するが、本実装は **per-rate 集計後 1 回** の丸めを採用している。これはユーザ承認を経た判断であり、日次丸め累積との微差は許容する。一次情報との照合時にユーザへ確認済み。

## 選択肢

1. 既定方式を 1 つに固定 → 事業所運用ごとに変えにくい。却下。
2. 期間マスタ（WageSettings）に委ねて 4 方式並存 → **採用**。
3. プラグイン拡張で追加方式を許す → YAGNI。
4. 端数を `FloorYen` で固定 → KouchinModule 実挙動（`ROUND`）に合わない。却下。

## 決定

期間マスタ（`WageSettings`）に `Method` フィールドを設け、Piece/Hourly/Fixed/Equal の 4 方式を並存実装する。Domain は `IWageMethodStrategy` インターフェースで 4 実装を持ち、Application 層のユースケースがマスタ値に応じて選択する。端数は `RoundingPolicy` に集約し `HalfUp` を既定とする。

## 影響

- Domain は `IWageMethodStrategy` で 4 実装を持つ。`HourlyWageStrategy` がレート別集計後 ROUND（HalfUp）/ 作業手当・職能手当加算に対応。
- `RoundingPolicy` + `RoundingRule.HalfUp` / `.Ceiling` として実装。
- 報酬告示由来の数値は本 ADR に**含めない**（ハードコード禁止）。
- 残 open-question: 平均工賃月額（AC2-8）の正式定義は一次資料入手時にクローズ。
- 参照仕様: `docs/superpowers/specs/2026-07-05-phase4-s0-kouchinmodule-and-avgwage-design.md`
- 関連 ADR: [0018 WageAdjustment append-only](0018-wage-adjustment-append-only.md) / [0019 RecipientHourlyRate 期間マスタ](0019-recipient-hourly-rate-periodic-master.md)
