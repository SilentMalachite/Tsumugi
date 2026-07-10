# ADR 0025: 報酬計算の端数規則と固定小数契約

**ステータス**: 確定（2026-07-10）

## 結論

就労継続支援B型の請求算定は、次の順序と端数規則を固定する。

1. 平均工賃月額はADR 0023の正式式を使う。開所日1日当たりの平均利用者数を小数点第1位までとし、小数点第2位以下があれば切り上げ、最後に平均工賃月額の円未満を四捨五入する。本ADRはこの規則を変更又は重複定義せず、安定したrule IDを与えて参照する。
2. 基本単位数へ割合による加算又は減算を適用するたびに、小数点以下を四捨五入して整数単位へ戻す。複数割合を最後に一括して乗算・丸めしてはならない。公式サービスコード表の合成単位数は既に端数処理済みの整数として受け取り、再丸めしない。
3. 整数のサービスコード単位数に当月の算定回数を乗じてサービス単位数を求め、サービス種別ごとに一月分を整数加算して給付単位数を求める。この乗算と加算では丸めは発生しない。
4. サービス種別ごとの月次給付単位数に、当該事業所・サービス種別・サービス月へ適用される一単位の地域単価を1回だけ乗じ、円未満を切り捨てて総費用額を求める。各サービスコード明細を個別に円換算して切り捨てた後に合算してはならない。
5. 1割相当額は、整数円の総費用額に`10 / 100`を乗じ、円未満を切り捨てる。法第31条の特例給付が受給者証へ明記されている場合は、明記された整数円と1割相当額の小さい方を使い、割合又は金額を推測しない。
6. 利用者負担は、1割相当額、受給者証の個別上限、制度上限、同一事業所内の調整及び正式な上限額管理結果をこの順で適用する。上限額との`min`、左からの充当及び正式結果票の転記は全て整数円の演算であり、追加の丸めを行わない。データの優先関係とR6成人B型の算定不能境界はADR 0022に従う。
7. B型の請求額・給付費は、整数円の総費用額から整数円の決定利用者負担額を控除して求める。`総費用額 × 90 / 100`を別に計算して丸めてはならない。B型経路にA型事業者減免を混入させない。

金額の保存型と外部出力は非負の整数円、地域単価、割合及び丸め前の中間値は`decimal`とする。`double`及び`float`は、Domain、Application、マスタ読込、テストデータ生成のいずれにも使用しない。全演算はchecked contextで範囲を検証し、オーバーフロー又は整数円へ変換できない値を算定不能とする。

## 背景

2026-06-29版のPhase 3-1計画には「各明細を円未満切捨て後に合算」とする案がある。しかし公式事務処理要領は、サービスコード単位数に算定回数を乗じ、サービス種別ごとに一月分のサービス単位数を合算して給付単位数を求め、その給付単位数へ単位数単価を乗じる順序を定めている。明細単位の円換算は、公式順序と異なる結果を生み得る。

同じ「端数処理」でも、平均工賃、割合加減算後の単位数、月次給付単位数、総費用額、1割相当額、決定利用者負担額及び給付費では、演算単位と丸め位置が異なる。単一の`MultiplyAndFloor`へ集約すると、割合加減算の四捨五入、月次合算前後、正式結果票の転記を混同する危険がある。

またADR 0012の`RoundingRule.HalfUp`は利用者へ支払う工賃計算の契約であり、本ADRの障害福祉サービス報酬請求とは別である。型名を共有してもrule ID、入力、適用順及び監査出典を共有してはならない。

## 選択肢

### A: 段階別rule IDと公式順序を固定する（採用）

- 公式例の中間値をそのまま再現できる。
- 月次給付単位数の合算前後を区別できる。
- 平均工賃、単位、円及び正式結果票の責務を分離できる。

### B: 全ての計算を最後に1回だけ丸める

割合を適用するたびに整数単位へ戻す公式規則と、公式例の`411 -> 617`に一致しないため採用しない。

### C: サービスコード明細ごとに円未満を切り捨てて合算する

公式事務処理要領の「サービス種別ごとに一月分のサービス単位数を合算してから単価を乗じる」順序と一致しないため採用しない。

### D: 給付費を総費用額の90%として独立計算する

決定利用者負担額には受給者証上限、特例給付、同一事業所内調整及び上限額管理結果が反映される。公式は総費用額から決定利用者負担額を控除するため採用しない。

## 決定

### 数値表現

| 値 | 型 | 単位 / scale | 制約 |
| --- | --- | --- | --- |
| サービスコード単位数、サービス単位数、給付単位数 | `int`又は範囲検証中の`long` | 整数単位 | 負値禁止。永続化・出力前に公式桁数と`int`範囲を検証 |
| 地域単価 | `decimal` | 円 / 単位。公式値を十進数で保持 | `double` / `float`経由禁止。ADR 0020の適用版から一意に解決 |
| 加減算割合、給付率 | `decimal` | 無次元 | 文字列又は整数の分子・分母から`decimal`へ厳密変換。二進浮動小数経由禁止 |
| 丸め前中間値 | `decimal` | 人、単位又は円 | rule IDに対応する一時値だけを保持し、別段階へ未丸めで渡さない |
| 平均工賃月額、総費用額、1割相当額、上限額、決定利用者負担額、給付費 | `int`又は範囲検証中の`long` | 整数円 | 負値禁止。未入力0と正式な0円を区別する |

JSONの地域単価及び割合は、C#の`decimal`へ直接読み込む。テストの`InlineData`を含め、`10.91d`、`0.1f`又は`double`からのcastを使用しない。四捨五入は非負値に対する`MidpointRounding.AwayFromZero`、切捨ては非負値に対する`decimal.Floor`として実装し、既定のbanker's roundingへ依存しない。

### `RoundingPolicy`の安定rule ID

rule IDは確定スナップショットに保存する文字列であり、名称の変更、同じIDへの別規則の上書き及び不明IDの既定規則へのフォールバックを禁止する。

| 適用順 | rule ID | 入力 | 出力 | 丸め単位・方向 | sourceDocumentId / 物理頁 |
| ---: | --- | --- | --- | --- | --- |
| 1a | `claim.avg-wage.daily-users.ceil-1dp.r6-corrected.v1` | `annualExtendedUsers / annualOpeningDays`の`decimal` | 小数点第1位の`decimal`人 | 小数点第2位以下があれば正方向へ切上げ | ADR 0023、`r6-qa-v2` p11を`r6-qa-corr-2` p4で訂正 |
| 1b | `claim.avg-wage.monthly-yen.half-up.v1` | `annualWagePaidYen / roundedDailyAverageUsers / 12`の`decimal`円 | 整数円 | 円未満四捨五入 | ADR 0023、`r6-qa-corr-2` p4 |
| 2 | `claim.units.percentage.half-up-each.v1` | sourceで指定された整数単位と`decimal`割合 | 整数単位 | 割合加減算を行うたび小数点以下四捨五入 | `r6-calculation-note` p8〜9、`r8-calculation-note` p8〜10 |
| 3a | `claim.units.service-line.multiply-count.v1` | 端数処理済み整数単位、当月算定回数 | 整数サービス単位 | 丸めなし、checked整数乗算 | `r8-grant-decision-administration-202606` p197、`r8-grant-decision-administration-202607` p197 |
| 3b | `claim.units.monthly-service-kind.sum.v1` | 同一受給者・同一事業所番号・同一サービス種別の整数サービス単位列 | 整数給付単位 | 丸めなし、月次整数合算 | 同上 p197〜198 |
| 4a | `claim.region-unit-price.exact-decimal.v1` | サービス月、地域区分、サービス種別 | `decimal`円 / 単位 | 丸めなし。適用版の公式値をそのまま選択 | ADR 0020、`mhlw-unit-price-notice-observed-946c3d96` HTML pageNo=1、`r6-calculation-note` p9、`r8-calculation-note` p10 |
| 4b | `claim.cost.monthly-service-kind.floor-yen.v1` | 整数給付単位、`decimal`地域単価 | 整数円の総費用額 | 積の円未満切捨て。サービス種別月次合算後に1回 | `r6-calculation-note` p9、`r8-calculation-note` p9〜10、`r8-grant-decision-administration-202606` p197〜198、`r8-grant-decision-administration-202607` p197〜198 |
| 5 | `claim.burden.ten-percent.floor-yen.v1` | 整数円の総費用額、`decimal`の`10 / 100` | 整数円の1割相当額 | 円未満切捨て | `r8-grant-decision-administration-202606` p197〜198、`r8-grant-decision-administration-202607` p197〜198 |
| 6a | `claim.burden.cap.minimum-yen.v1` | 1割相当額、法31条特例額、受給者証上限、制度上限 | 整数円の暫定負担額 | 丸めなし、検証済み整数円の`min` | ADR 0022、`r6-disability-support-guide-202404` p9、`r8-grant-decision-administration-202606` p197〜198、`r8-grant-decision-administration-202607` p197〜198 |
| 6b | `claim.burden.in-office-order.allocate-yen.v1` | サービス種別別暫定負担額、受給者証上限、公式優先順 | サービス種別別調整後負担額 | 丸めなし、左から上限到達まで整数円を充当 | ADR 0022、`r8-grant-decision-administration-202606` p198〜199、`r8-grant-decision-administration-202607` p198〜199 |
| 6c | `claim.burden.upper-limit-result.allocate-yen.v1` | 検証済み管理結果額、サービス種別別の管理前最終負担額 | サービス種別別決定利用者負担額 | 丸めなし、正式結果額へ到達するまで公式順に整数円を転記 | ADR 0022、`r8-grant-decision-administration-202606` p182〜186、p199、`r8-grant-decision-administration-202607` p182〜186、p199 |
| 7 | `claim.benefit.cost-minus-decided-burden.v1` | 整数円の総費用額、整数円の決定利用者負担額 | 整数円の請求額・給付費 | 丸めなし、checked減算 | `mhlw-disability-support-act-observed-4b8f2824` 29条3項、`r8-grant-decision-administration-202606` p197、p199、`r8-grant-decision-administration-202607` p197、p199 |

`RoundingPolicy`は端数変換だけを担い、どの基本単位へどの割合を適用するか、処遇改善加算の対象合計、サービス種別、地域単価、優先順又は上限額管理結果を選ばない。これらは適用版の外部マスタ及びADR 0022・0023の検証済み入力が決める。

### 適用順

```text
ADR 0023の平均工賃計算
  -> PaymentBand / 実在service-code解決
  -> 割合加減算ごとに整数単位へ四捨五入
  -> 端数処理済み単位 × 当月算定回数
  -> サービス種別ごとの月次給付単位数を整数合算
  -> 月次給付単位数 × decimal地域単価を円未満切捨て
  -> 総費用額 × 10 / 100を円未満切捨て
  -> 法31条特例額・証上限・制度上限のmin
  -> 同一事業所内の公式順調整
  -> 対象者だけ正式な上限額管理結果を公式順に転記
  -> 総費用額 - 決定利用者負担額 = B型の請求額・給付費
```

同一受給者に複数サービス種別がある場合、総費用額と1割相当額はサービス種別ごとに算出してから、公式順で利用者負担を配分する。全サービス種別の給付単位数を先に一括合算して単一の単価を乗じない。

### 公式ケースの期待値

一次資料に数値が明記されたケースだけを公式ケースとして固定する。一次資料に具体値がない1割相当額、上限額管理及び給付費の境界テストは、上表の式から作る仕様テストとして公式数値例と区別する。

| case ID | 公式入力 | 公式期待値 | 検証する禁止事項 | sourceDocumentId / 物理頁 |
| --- | --- | --- | --- | --- |
| `official.units.sequential-01` | `587単位 × 0.70` | `410.9 -> 411単位` | 割合適用後の未丸め値を次段へ渡さない | `r6-calculation-note` p8〜9、`r8-calculation-note` p8〜9 |
| `official.units.sequential-02` | `411単位 × 1.5` | `616.5 -> 617単位` | `587 × 0.70 × 1.5 = 616.35`を最後に1回丸めない | 同上 p9 |
| `official.units.monthly-rate-01` | `587単位 × 6回 = 3,522単位`、`3,522 × 0.15` | `528.3 -> 528単位` | 月次合算対象の割合を日次・明細ごとに分割しない | 同上 p9 |
| `official.cost.region-01` | `617単位 × 4回 = 2,468単位`、`2,468 × 11.20円` | `27,641.6 -> 27,641円` | 1円未満を四捨五入しない | `r6-calculation-note` p9、`r8-calculation-note` p9〜10 |
| `official.avg-wage.daily-users-01` | `14.679人` | `14.7人` | 訂正前の小数点第2位四捨五入を使わない | ADR 0023、`r6-qa-v2` p11、`r6-qa-corr-2` p4 |

### 未確定時とフェイルクローズ

| 境界 | 算定不能条件 |
| --- | --- |
| 平均工賃 | ADR 0023の入力、適用版、完全性又は丸めrule IDが欠ける。既存`AverageWageMetric`又はADR 0012の工賃丸めへフォールバックしない |
| 単位数 | 割合を適用する基礎単位、対象合計、適用順又は公式service-code rowを一意にできない。複数割合を一括乗算しない |
| 地域単価 | サービス月・地域・サービス種別に対する公式単価を一意に解決できない。10円又は直近値を既定値にしない |
| 総費用額 | サービス種別、月次給付単位数又は単価が不明、範囲超過、負値。明細別円換算又は全サービス種別一括換算へ切り替えない |
| 利用者負担 | 受給者証の法31条特例、個別上限、入力済み状態又は適用期間を確認できない。特例割合・金額を推測しない |
| 上限額管理 | ADR 0022の対象状態、成人B型に適用できる当月版一次資料又は確定済み正式結果票が欠ける。特に2024-04〜2026-05のR6成人B型で対象者は算定不能 |
| 給付費 | 決定利用者負担額が未確定、負値、総費用額超過又はB型以外の減免が混入。90%乗算へ置換しない |
| 数値型 | `double` / `float`経由、非有限値、overflow、公式桁数超過又は未登録rule ID。丸めモードを既定選択しない |

### 版境界

- `claim-master-r6-04`（2024-04〜05）及び`claim-master-r6-06`（2024-06〜2026-05）は、単位数・金額換算について`r6-calculation-note` p8〜9を使う。
- `claim-master-r8-06`（2026-06以降）は`r8-calculation-note` p8〜10を使う。R8改定は平均工賃区分を変更するが、本ADRの割合単位四捨五入と金額換算切捨てを変更していない。
- R8の請求集計、1割相当額、同一事業所内調整、上限額管理結果及び給付費は、ADR 0020登録済みの令和8年6月版と、現行liveの令和8年7月版のphysical pages 197〜199で式と順序が一致することを確認した。令和8年7月版を2026-05以前へ遡及しない。
- 平均工賃の令和6／令和8境界はADR 0023に従う。本ADRは区分境界、経過措置又は平均工賃の式を再定義しない。

## 出典と再検証

物理頁はPDF先頭を1頁とする。2026-07-10に下表のlive資料を2回以上取得し、同一SHA-256とサイズを確認した。令和8年6月事務処理要領はlive URLが404のため再取得済みとは扱わず、ADR 0020の不変byte識別子を使用する。ADR 0020に登録済みのIDを優先し、現行liveで置換された事務処理要領だけADR 0024登録済みの令和8年7月IDを併記する。

| sourceDocumentId | URL | SHA-256 / size | 本ADRで使用する箇所 |
| --- | --- | --- | --- |
| `r6-qa-v2` | https://www.mhlw.go.jp/content/001250243.pdf | `bd773dfe577b8a83fd99b28fd10f8cdc1b55d47114a03fd64c128777e54e6b8d` / 368,262 bytes。ADR 0020登録値と一致 | p11。問24の平均工賃月額と公式例。丸め方向は次行の正誤を必ず適用 |
| `r6-qa-corr-2` | https://www.mhlw.go.jp/content/001250239.pdf | `ee37c492b6989ad874f4be0f8febddab2ff351e6fe5bb0afff04223a7e2df8df` / 121,736 bytes。ADR 0020登録値と一致 | p4。問24を小数点第2位切上げへ訂正し、最終円未満四捨五入を確認 |
| `r6-calculation-note` | https://www.mhlw.go.jp/content/001494356.pdf | `958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1` / 4,193,553 bytes。ADR 0020登録値と一致 | p8〜9。割合を適用するたび四捨五入、合成コードは端数処理済み、円換算切捨て、公式例 |
| `r8-calculation-note` | https://www.mhlw.go.jp/content/001705650.pdf | `0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99` / 2,677,112 bytes。ADR 0020登録値と一致 | p8〜10。同じ単位数・円換算規則と公式例 |
| `mhlw-unit-price-notice-observed-946c3d96` | https://www.mhlw.go.jp/web/t_doc?dataId=83aa8493&dataType=0&pageNo=1 | `946c3d969ffd4128db15106d25ce6d26ff108f5460a7618e3df96352e42c0c1b` / 52,785 bytes | HTML pageNo=1。基準額10円と地域別割合。具体値と版選択はADR 0020を参照 |
| `mhlw-disability-support-act-observed-4b8f2824` | https://www.mhlw.go.jp/web/t_doc?dataId=83aa7574&dataType=0&pageNo=1 | `4b8f2824d25351a7b97f37461eaa0825be5046bfc3ab4d87595e1ab86a9443dc` / 204,793 bytes | HTML 29条3項、31条。給付費を費用額から決定負担額の控除で求める法的順序と特例給付を照合 |
| `r6-disability-support-guide-202404` | https://www.mhlw.go.jp/content/12200000/001327493.pdf | `3ead1b2d235ff15e6d0c71a129e7ef880e119272a00acd043e635aee8c637469` / 3,124,049 bytes。ADR 0020登録値と一致 | p9。R6の制度上限4区分。証実値を上書きせず、ADR 0022の補助上限として使用 |
| `r8-grant-decision-administration-202606` | ADR 0020に記録された旧公式URL（2026-07-10のlive確認では404） | `d6e1672245370d2d7bb9a4258622ae3e631d0a6144c8e0c9ea51e2018a146f1e` / 1,998,305 bytes（ADR 0020の不変byte識別子） | p182〜186、p197〜199。上限額管理、月次給付単位数、総費用額、1割相当額、決定負担額、給付費 |
| `r8-grant-decision-administration-202607` | https://www.mhlw.go.jp/content/12200000/001721666.pdf | `1a94220c99986f353e4c63c095c156448271ecad1d7bf0d9e197d3b8ca06de65` / 1,999,016 bytes | p182〜186、p197〜199。令和8年6月版の本ADR対象式・順序を現行liveで再検証 |

障害者総合支援法29条3項は、月次の給付額を、サービス種類ごとの基準費用額の合計から負担能力等を勘案した額を控除した額と定め、同項2号はその額を費用額の10%相当額以下とする。31条の特例は市町村が定める額を使う。`mhlw-disability-support-act-observed-4b8f2824`は2026-07-10に3回取得し、全bytesが一致したlive法令証拠である。これは給付費を決定利用者負担額の控除で求める法的順序の照合に使い、地域単価又は端数方向の値ソースには使わない。

## 影響

- Phase 3-1の`RoundingPolicy`は、上表のrule IDを閉じた集合として実装し、公式例をテーブル駆動テストにする。
- `ClaimCalculator`はサービスコード明細の円額を先に作らず、サービス種別ごとの月次給付単位数を構築してから地域単価を適用する。
- `BurdenCalculator`は1割相当額の切捨てだけを`RoundingPolicy`へ委譲し、証上限、制度上限、正式結果票及び優先順をADR 0022の入力契約として扱う。
- 確定スナップショットは、rule ID、丸め前値、丸め後値、適用順、masterVersion、sourceDocumentIds及びsource pageを保持する。合成サービスコードでは「公式表で端数処理済み」であることとsource rowを保持する。
- 2026-06-29版Phase 3-1計画の`MultiplyAndFloor`を明細ごとに呼ぶ設計例、`double`の`InlineData`及びR9仮データは現行設計の実装契約にしない。Phase 3-1再計画時に本ADRと2026-06-29設計の現行化注記へ合わせる。
- ADR 0012の工賃支払額用`RoundingPolicy`と、請求算定用rule IDのnamespace又は型を分け、同名の`HalfUp`を暗黙共有しない。
