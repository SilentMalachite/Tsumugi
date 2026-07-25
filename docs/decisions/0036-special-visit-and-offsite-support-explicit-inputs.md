# 0036 訪問支援特別加算・施設外支援の導出不能項目を個別入力にし、時間数の単位を公式書式に合わせる

- 状態: 採用（2026-07-25）
- 関連: ADR 0028（主要加算の実値）/ ADR 0032（契約情報の個別入力）/ ADR 0033（登録済み一次資料からの仕様適合）/ ADR 0034
- 一次資料: `provider-r7-10`（インタフェース仕様書 事業所編、SHA-256 `44e94a7817bde2b9…`）、
  `r8-calculation-note`（留意事項通知、SHA `0c4f357f…`）、`r6-employment-guidance`（就労系留意事項通知、SHA `185fa992…`）

## 結論

明細情報の 4 項目のうち **2 項目は日次実績から導出できない**ため個別入力にし、
**1 項目は単位が誤っていた**ため公式書式へ直した。導出できる 2 項目は `generated` を維持する。

| 公式項目 | 桁 | 公式の項目説明 | 決定 |
|---|---|---|---|
| `provider:J611:01:051` 訪問支援特別加算（回）（サービス提供回数） | 2 | 「訪問支援特別加算のサービス提供回数の合計を設定」 | **`generated` 維持**（日次実績から数えられる） |
| `provider:J611:01:052` 訪問支援特別加算（回）（算定回数） | 2 | 「訪問支援特別加算の算定回数の合計を設定」 | **`missing` → `ClaimInput.SpecialVisitSupportBilledCount`**（月次入力） |
| `provider:J611:01:053` 施設外支援 当月（日） | 2 | 「就労継続支援において、当月の施設外支援を行った日数を設定」 | **`generated` 維持** |
| `provider:J611:01:054` 施設外支援 累計（日／１８０日） | 3 | 「就労継続支援において、施設外支援の累計日数を設定」 | **`missing` → `ClaimInput.OffsiteSupportCumulativeDays`**（月次入力） |
| `provider:J611:02:027` 訪問支援特別加算（サービス提供時間数） | 4 | 「実際にサービス提供した時間数（**時間**）を整数部 2 桁＋小数部 2 桁で設定、例：**1.5 時間→0150**（または 150）」 | **`existing` → `DailyRecord.SpecialVisitSupportMinutes:hundredthsOfHour`**（分→1/100 時間へ変換） |
| `provider:J611:02:028` 訪問支援特別加算（算定時間数） | 2 | 「算定する時間数（**時間**）を設定（**整数**）」 | **`missing` の targetProperty を `DailyRecord.SpecialVisitSupportBilledHours` へ修正**（日次入力） |

## 背景

### 導出できない理由

- **算定回数**: 留意事項通知 2(6)⑨ は「所要時間については、**実際に要した時間により算定されるのではなく**、
  計画に基づいて行われるべき指定サービス等に要する時間に基づき算定される」「1月に2回算定する場合は、
  加算算定後又は利用後、**再度5日間以上連続して利用がなかった場合にのみ**対象」と定める。
  実績（訪問した日・分）から算定回数は決まらない。
- **施設外支援の累計**: 就労系留意事項通知 1(1)① は「**毎年4月1日に始まり翌年3月31日をもって終わる1年間に
  180日間を限度**として算定する。この『180日間』とは利用者が**実際に利用した日数の合計数**」と定める。
  直近180日のローリング窓ではなく**年度累計**であり、当月分しか持たない確定 snapshot から算出できない。
  当月分を含めるかは公式資料に明記が無いため、**運用者が明細書の『累計』欄へ設定する値をそのまま出す**。

実装にはこの 2 項目の `generatorRule`（`count(...;measure=billableOccurrences)` /
`count(...;window=official180DayWindow)`）が宣言されていたが、resolver は両者を未実装として
fail-close していた。つまり該当加算を算定する月は CSV が出せなかった。

### 単位の誤り

`:027` は `missing` として `DailyRecord.SpecialVisitSupportMinutes`（**分**）をそのまま数値で出していた。
公式書式は 1/100 時間（1.5 時間 → `150`）なので、90 分の日に `90` を出していた（＝0.9 時間の意味になる）。
入力欄（分）は既にあるため「入力が無い＝`missing`」でもなかった。

## 決定

1. 追加する列は 3 つ。単位を名前に含める。
   - `ClaimInput.SpecialVisitSupportBilledCount`（回・月次）
   - `ClaimInput.OffsiteSupportCumulativeDays`（日・月次）
   - `DailyRecord.SpecialVisitSupportBilledHours`（**時間**・日次・整数）。既存の
     `SpecialVisitSupportMinutes`（**分**）とは別項目。UI ラベルも両方に単位を明記する。
2. 検証は**負値の禁止だけ**。180 日上限・回数上限のような制度実値はコード・DB 制約に書かない
   （CLAUDE.md §ハード制約3）。
3. `:027` は `modelPath` に**単位接尾辞**を宣言する形にした（`<Entity>.<Property>:<unit>`、
   語彙は `ClaimCsvModelPath.KnownUnitSuffixes` の閉じた集合）。公式の尺度を C# の識別子ではなく
   spec JSON 側に置くため。分→1/100 時間は `分 × 100 / 60` の**厳密変換**で、割り切れない分値
   （3 の倍数でない分）は**丸めずに fail-close** する（公式の丸め規則が未確定。`docs/open-questions.md` で追跡）。
4. readiness: 3 項目とも `ClaimPreparationContextBuilder` が値を供給する。
   `DailyRecord.SpecialVisitSupportBilledHours` は当月 SUM を供給するが、**未入力は `null`（NotApplicable）**
   として供給する。0 を供給すると「入力済みの 0」と区別できず、要求条件
   （`modelNonZero(DailyRecord.SpecialVisitSupportMinutes)`＝自己参照でない条件）が真でも通ってしまう
   fail-open になる。そのため `ClaimDailyRecordAggregate.SpecialVisitSupportBilledHoursTotal` は `int?`。
5. PreviewHash の入力 snapshot（`ClaimRecipientSnapshotWriter`）にも月次 2 項目を含める。
   含めないと、プレビュー後にこれらを書き換えても同じ hash で確定でき、確定 snapshot が
   「プレビューで見た内容」と食い違う。

## 影響

- `field-mapping-r7-10.json` の status 分布: `generated` 374→372 / `existing` 28→29 / `missing` 31→32
  （総数 443 は不変）。generatorRule 件数 374→372。
- readiness の対象パスは 27→30、fieldId は 52→53。
- 該当加算を算定する月は、これら 3 項目が未入力なら**確定前の readiness で止まる**（従来は確定できて
  CSV 生成時に落ちていた）。
- golden CSV 4 種を再生成した（`J611:01` の項目 51/52/53/54 と `J611:02` の項目 27/28/29 が空欄から値へ）。
- 未実装だった `measure=billableOccurrences` / `window=official180DayWindow` の拒否分岐は、
  「導出不能な rule を宣言したら落ちる」歯として残す（どの rule からも使われなくなった）。
