# 0034 「サービス利用日数」と「利用日数」を別定義として扱う

- 状態: 採用（2026-07-25）
- 関連: ADR 0024（国保連CSVと項目マッピング）/ ADR 0028（主要加算の実値）/ ADR 0033（一次資料からの仕様適合）
- 一次資料: `provider-r7-10`（インタフェース仕様書 事業所編、SHA-256 `44e94a7817bde2b9…`）

## 結論

請求明細書の 2 つの日数項目は**公式に別定義**であり、同じ値を出してはならない。

| 項目 | 公式定義（事業所編） | 本アプリの正本 |
|---|---|---|
| `provider:J121:02:010`「利用日数」 | ③利用日数の設定方法: 当該月に実際にサービス利用した日数。「※上限額管理加算などの加算（**欠席時対応加算は除く**）のみを利用した場合も１日とカウントする」 | `BilledDays`（本体報酬算定日数） |
| `provider:J121:04:009`「サービス利用日数」 | 項目説明: 「本体報酬を算定しない日において、各種加算のみを算定した場合も 1 日とカウントする。**上限額管理加算、欠席時対応加算等も 1 日とカウントする**」 | `ServiceUsageDays`（本体報酬算定日数＋加算のみ算定日数） |

## 背景

Phase 3-3 の実装は両項目に `BilledDays`（実効 DailyRecord の `Attendance=Present` 日数）を返していた。
Codex レビューが `J121:04:009` について「加算のみ算定した日が落ちる」と指摘し、上記条文で確定した。
欠席時対応加算は `Attendance=AbsenceSupport` の日に算定するため `Present` 日数には含まれず、
**欠席時対応加算を算定した月はサービス利用日数が過少になっていた**。

同時に、`J121:02:010` については条文が明示的に「欠席時対応加算は除く」としており、
**こちらは `BilledDays` で正しい**ことも確認できた。両項目を同一視した実装が誤りだった。

## 決定

1. `RecipientClaimResult` に `AbsenceSupportBilledDays`（マスタの月次上限で cap した後の
   欠席時対応加算算定日数）を追加する。cap はマスタ値なので、cap を知っている算定器が返す。
2. 確定時に `ClaimFinalizationSnapshot.ServiceUsageDays` =
   `BilledDays + AbsenceSupportBilledDays` を焼き込む。確定時点の値を正本にする
   （Phase 3-2 の doctrine）。
3. CSV は `J121:04:009` に `ServiceUsageDays` を、`J121:02:010` に `BilledDays` を用いる。
4. Phase 3-3 より前に確定した snapshot は `ServiceUsageDays` を持たないため、
   `BilledDays` で代用せず **fail-close** する（過少値を黙って出さない）。

## 影響

- `RecipientClaimResult` / `ClaimFinalizationSnapshot` はいずれも**末尾の任意パラメータ**で追加した
  ため、既存の位置引数呼び出しは変更不要。
- golden CSV 4 種を再生成した（`J121:04:009` が 5 → 6 に変わり、`J121:02:010` の 5 と区別される）。
- 上限額管理加算のみを算定した日は、本アプリが当該加算に日付を持たないため 0 として扱う
  （`docs/open-questions.md` の未確定事項）。
