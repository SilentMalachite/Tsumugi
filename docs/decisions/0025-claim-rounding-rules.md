# ADR 0025: 報酬計算の端数規則と固定小数契約

**ステータス**: 確定（2026-07-10）

## 結論

就労継続支援B型の請求算定は、次の順序と端数規則を固定する。

1. 平均工賃月額はADR 0023の正式式を使う。開所日1日当たりの平均利用者数を小数点第1位までとし、小数点第2位以下があれば切り上げ、最後に平均工賃月額の円未満を四捨五入する。本ADRはこの規則を変更又は重複定義せず、安定した`roundingRuleId`を与えて参照する。
2. 割合加減算は固定の線形順序では処理しない。適用版の公式source rowが持つ`percentageBaseScope`と`percentageApplicationKind`で基礎単位、丸め位置及び結果の加減先を決める。`PerServiceCodeUnit`は整数の基礎単位へ割合を適用して四捨五入した後に回数を乗じる。`MonthlyTargetUnitSum`は端数処理済み単位へ回数を乗じた整数を対象集合で月次合算してから割合を適用・四捨五入し、得た整数の加算又は減算単位を月次給付単位へ反映する。異なるscopeを同じ順序へ寄せない。
3. 割合による単位数を求めるたびに小数点以下を四捨五入して整数単位へ戻す。複数割合を最後に一括して乗算・丸めしてはならない。公式サービスコード表の合成単位数は既に端数処理済みの整数として受け取り、再丸めしない。整数の回数乗算、対象合算、加算又は減算では丸めを行わない。
4. source rowどおりに全てのper-service-code結果とmonthly-target結果を反映した後、サービス種別ごとの最終月次給付単位数を確定する。その整数給付単位数に、当該事業所・サービス種別・サービス月へ適用される一単位の地域単価を1回だけ乗じ、円未満を切り捨てて総費用額を求める。各サービスコード明細を個別に円換算して切り捨てた後に合算してはならない。
5. 1割相当額は、整数円の総費用額に`10 / 100`を乗じ、円未満を切り捨てる。法第31条の特例給付は三値の構造化入力で確認し、`Applicable`かつ有効な非負整数円だけを1割相当額との`min`へ参加させる。`Unknown`、null又は自由記述から不適用・割合・金額を推測しない。
6. 利用者負担は、法第31条特例の検証結果、受給者証の個別上限、制度上限、同一事業所内の調整及び正式な上限額管理結果を順に適用する。上限額との`min`、左からの充当及び正式結果票の転記は全て整数円の演算であり、追加の丸めを行わない。データの優先関係とR6成人B型の算定不能境界はADR 0022に従う。
7. B型の請求額・給付費は、整数円の総費用額から整数円の決定利用者負担額を控除して求める。`総費用額 × 90 / 100`を別に計算して丸めてはならない。B型経路にA型事業者減免を混入させない。

金額の保存型と外部出力は非負の整数円、地域単価、割合及び丸め前の中間値は`decimal`とする。`double`及び`float`は、Domain、Application、マスタ読込、テストデータ生成のいずれにも使用しない。全演算はchecked contextで範囲を検証し、オーバーフロー又は整数円へ変換できない値を算定不能とする。

## 背景

2026-06-29版のPhase 3-1計画には「各明細を円未満切捨て後に合算」とする案がある。しかし公式事務処理要領は、サービスコード単位数に算定回数を乗じ、サービス種別ごとに一月分のサービス単位数を合算して給付単位数を求め、その給付単位数へ単位数単価を乗じる順序を定めている。明細単位の円換算は、公式順序と異なる結果を生み得る。

同じ「端数処理」でも、平均工賃、割合加減算後の単位数、月次給付単位数、総費用額、1割相当額、決定利用者負担額及び給付費では、演算単位と丸め位置が異なる。単一の`MultiplyAndFloor`へ集約すると、割合加減算の四捨五入、月次合算前後、正式結果票の転記を混同する危険がある。

またADR 0012の`RoundingRule.HalfUp`は利用者へ支払う工賃計算の契約であり、本ADRの障害福祉サービス報酬請求とは別である。型名を共有しても`roundingRuleId`、`calculationStepId`、入力、適用順及び監査出典を共有してはならない。

## 選択肢

### A: `roundingRuleId`と`calculationStepId`を分離して公式順序を固定する（採用）

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
| 丸め前中間値 | `decimal` | 人、単位又は円 | `roundingRuleId`に対応する一時値だけを保持し、別段階へ未丸めで渡さない |
| 平均工賃月額、総費用額、1割相当額、上限額、決定利用者負担額、給付費 | `int`又は範囲検証中の`long` | 整数円 | 負値禁止。未入力0と正式な0円を区別する |

JSONの地域単価及び割合は、C#の`decimal`へ直接読み込む。テストの`InlineData`を含め、`10.91d`、`0.1f`又は`double`からのcastを使用しない。四捨五入は非負値に対する`MidpointRounding.AwayFromZero`、切捨ては非負値に対する`decimal.Floor`として実装し、既定のbanker's roundingへ依存しない。

### 割合加減算のsource row契約

割合を持つ公式service-code row又は外部マスタrowは、少なくとも次を必須にする。名称又は割合値だけからscopeと加減先を推測しない。

| field | 閉じた値 / 内容 | 検証 |
| --- | --- | --- |
| `percentageBaseScope` | `PerServiceCodeUnit` / `MonthlyTargetUnitSum` | 未知値、null又は版外の値を拒否 |
| `percentageApplicationKind` | `Replace` / `Add` / `Subtract` | source rowが定める結果の反映方法と完全一致させる |
| `targetSelector` | 基礎単位又は月次対象service-code集合を一意に指定する版付きselector | 空集合、複数候補、自己参照又は未登録codeを拒否 |
| `calculationOrder` | 同じ対象へ複数割合を適用する場合の公式順序と依存先 | 穴、重複、循環又は順序不明を拒否 |
| `roundingRuleId` | `claim.rounding.units.half-up.v1` | 別rule又は未登録ruleへのフォールバック禁止 |
| `calculationStepId` | `PerServiceCodeUnit`は`claim.step.units.per-service-code.percentage.v1`、`MonthlyTargetUnitSum`は`claim.step.units.monthly-target.percentage.v1` | scopeと矛盾するstep、欠落又は未登録stepを拒否。multiply / sum / applyは後掲の固定pipeline stepを記録 |
| `sourceDocumentId` / `sourceLocator` | ADR 0020の適用版sourceとPDF physical page又はservice-code row | 適用月、source SHA又はrowを一意に検証 |

`PerServiceCodeUnit`は、source rowが指定する整数の基礎単位に割合を乗じ、`claim.rounding.units.half-up.v1`で整数へ戻し、`Replace` / `Add` / `Subtract`を反映した端数処理済みservice-code単位を作る。同じservice-codeへ割合を連続適用する場合は、直前の丸め済み整数を次の基礎値とする。その後に当月算定回数を整数乗算する。

`MonthlyTargetUnitSum`は、対象service-codeごとに上記の端数処理済み単位へ当月算定回数を乗じ、その整数列を`targetSelector`どおり月次合算する。合計へ割合を乗じて`claim.rounding.units.half-up.v1`で整数の加算又は減算単位を求め、`percentageApplicationKind`どおり最終給付単位数へ反映する。特別地域加算及び福祉・介護職員等処遇改善加算のような月次対象を、per-line計算へ変更しない。

### 法第31条特例給付の構造化入力

Phase 3-1は、自由記述やnullable金額ではなく、次の三値を持つ`Article31SpecialBurdenStatus`を追加する。

| status | 意味 | 必須条件 | 算定 |
| --- | --- | --- | --- |
| `Unknown` | 受給者証原本を未確認又は状態不明 | 金額を請求根拠に使用しない | 算定不能 |
| `NotApplicable` | 原本で法31条特例がないことを確認済み | 非適用を確認した`effectiveFrom` / `effectiveTo`、受給者証原本document reference、確認日時・確認者・確認根拠が必須。特例額はnull | 1割相当額をそのまま後続上限処理へ渡す |
| `Applicable` | 原本に市町村決定の特例負担額が明記済み | 非負整数円`amountYen`、`effectiveFrom`、`effectiveTo`、受給者証原本document reference、確認日時・確認者・確認根拠が全て必須 | 対象サービス日だけ`min(oneTenthYen, amountYen)`を後続へ渡す |

`Applicable`の0円は、原本参照と入力済み状態を検証できる場合だけ有効である。対象月内で状態、金額又は適用期間が切り替わり、各サービス日に適用する値を一意にできない場合は算定不能とする。`status = null`、`amountYen = null`、既存の`SupplyNotes`その他の自由記述から`NotApplicable`又は金額を推測しない。`NotApplicable`なのに金額がある、`Applicable`なのに必須項目がない、対象日が有効期間外、原本と登録スナップショットが不一致のときも拒否する。

本ADR決定時点のDomainにはこの三値、特例額の入力済み状態、期間及び原本参照がなかった。2026-07-12時点で`Article31SpecialBurdenStatus`、入力済み金額、期間、原本参照、migration、Application DTO、保存ユースケースは実装済みで、入力UIと原本再確認フローが残る。既存null又は自由記述を自動移行しない。

### `RoundingPolicy`の安定`roundingRuleId`

`RoundingPolicy`の閉集合は、端数を別の精度へ変換する次の5規則だけとする。単価選択、整数乗算・合算、`min`、配分及び減算は受け取らない。同じIDへの別規則の上書き、不明IDの既定規則へのフォールバック及び`calculationStepId`を`roundingRuleId`として渡すことを禁止する。

| roundingRuleId | 入力 | 出力 | 単位・方向 | sourceDocumentId / 物理頁 |
| --- | --- | --- | --- | --- |
| `claim.rounding.average-wage.daily-users.ceil-1dp.r6-corrected.v1` | 日平均利用者数の`decimal` | 小数点第1位の`decimal`人 | 小数点第2位以下があれば正方向へ切上げ | ADR 0023、`r6-qa-v2` p11を`r6-qa-corr-2` p4で訂正 |
| `claim.rounding.average-wage.monthly-yen.half-up.v1` | 丸め前平均工賃月額の`decimal`円 | 整数円 | 円未満四捨五入 | ADR 0023、`r6-qa-corr-2` p4 |
| `claim.rounding.units.half-up.v1` | source rowの割合計算結果又は基準該当B型の公式式・地方公共団体比較補正結果である`decimal`単位 | 整数単位 | 小数点以下四捨五入 | `r6-calculation-note` p8〜9、`r8-calculation-note` p8〜10、`current-fee-notice-html`、`h31-fee-notice-consolidated` p46〜47 |
| `claim.rounding.cost.floor-yen.v1` | 月次給付単位数と地域単価の積である`decimal`円 | 整数円 | 円未満切捨て | `r6-calculation-note` p9、`r8-calculation-note` p9〜10、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.rounding.burden.floor-yen.v1` | 総費用額と`10 / 100`の積である`decimal`円 | 整数円 | 円未満切捨て | `r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |

### 安定`calculationStepId`

`ClaimCalculator`、`AverageWageCalculator`及び`BurdenCalculator`は、選択・整数演算・配分の意味を次の`calculationStepId`で記録する。端数が生じるstepだけ、表で指定した`roundingRuleId`を`RoundingPolicy`へ渡す。`—（呼出しなし）`のstepは`RoundingPolicy`を呼ばず、監査上の`roundingRuleId`もnullとする。nullから計算stepを推測せず、`calculationStepId`は必ず記録する。

| calculationStepId | 入力 -> 出力 | roundingRuleId | sourceDocumentId / 物理頁 |
| --- | --- | --- | --- |
| `claim.step.average-wage.daily-users.divide.v1` | 年間延べ利用者数、年間開所日数 -> 日平均利用者数 | `claim.rounding.average-wage.daily-users.ceil-1dp.r6-corrected.v1` | ADR 0023 |
| `claim.step.average-wage.monthly.divide.v1` | 年間工賃、丸め済み日平均利用者数、12 -> 平均工賃月額 | `claim.rounding.average-wage.monthly-yen.half-up.v1` | ADR 0023 |
| `claim.step.units.per-service-code.percentage.v1` | 整数基礎単位、割合、`calculationOrder` -> 端数処理済み整数service-code単位 | `claim.rounding.units.half-up.v1` | `r6-calculation-note` p8〜9、`r8-calculation-note` p8〜10 |
| `claim.step.units.service-code.protected-facility-formula.v1` | 保護施設事務費、制度定数及び固定順序 -> 基準該当B型公式式の整数単位 | `claim.rounding.units.half-up.v1` | `current-fee-notice-html`、`h31-fee-notice-consolidated` p46 |
| `claim.step.units.service-code.protected-facility-local-government-benchmark.v1` | 通常B型比較単位、地方公共団体補正率 -> 補正済み整数比較単位 | `claim.rounding.units.half-up.v1` | `current-fee-notice-html`、`h31-fee-notice-consolidated` p47 |
| `claim.step.units.service-code.protected-facility-minimum.v1` | 公式式側整数単位、補正済み比較側整数単位 -> 小さい方の整数単位 | —（呼出しなし） | `current-fee-notice-html`、`r6-fee-notice` p137〜138 |
| `claim.step.units.service-code.multiply-count.v1` | 端数処理済み整数単位、回数 -> 整数サービス単位 | —（呼出しなし） | `r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197 |
| `claim.step.units.monthly-service-kind.sum.v1` | 同一サービス種別の整数サービス単位列 -> 月次基礎給付単位数 | —（呼出しなし） | `r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.step.units.monthly-target.sum.v1` | `targetSelector`対象の整数サービス単位列 -> 月次対象単位合計 | —（呼出しなし） | `r6-calculation-note` p8〜9、`r8-calculation-note` p8〜10 |
| `claim.step.units.monthly-target.percentage.v1` | 月次対象単位合計、割合 -> 整数の加算又は減算単位 | `claim.rounding.units.half-up.v1` | 同上 |
| `claim.step.units.monthly-target.apply.v1` | 月次基礎単位、整数の加算又は減算単位、`percentageApplicationKind` -> 最終給付単位数 | —（呼出しなし） | `r6-calculation-note` / `r8-calculation-note` p8〜10、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.step.region-unit-price.select.v1` | サービス月、地域、サービス種別 -> `decimal`地域単価 | —（呼出しなし） | ADR 0020、`mhlw-unit-price-notice-observed-946c3d96` HTML pageNo=1 |
| `claim.step.cost.monthly-service-kind.multiply-unit-price.v1` | 最終給付単位数、地域単価 -> 整数円総費用額 | `claim.rounding.cost.floor-yen.v1` | `r6-calculation-note` p9、`r8-calculation-note` p9〜10、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.step.burden.ten-percent.multiply.v1` | 整数円総費用額、`10 / 100` -> 整数円1割相当額 | `claim.rounding.burden.floor-yen.v1` | `r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.step.burden.article31.resolve.v1` | 1割相当額、`Article31SpecialBurdenStatus`と検証済み入力 -> 特例適用後負担額 | —（呼出しなし） | `mhlw-disability-support-act-observed-4b8f2824` 31条、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.step.burden.cap.minimum.v1` | 特例適用後負担額、受給者証上限、制度上限 -> 暫定負担額 | —（呼出しなし） | ADR 0022、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197〜198 |
| `claim.step.burden.in-office-order.allocate.v1` | サービス種別別暫定負担額、証上限、公式順 -> 調整後負担額 | —（呼出しなし） | ADR 0022、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p198〜199 |
| `claim.step.burden.upper-limit-result.allocate.v1` | 検証済み管理結果額、管理前最終負担額 -> 決定利用者負担額 | —（呼出しなし） | ADR 0022、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p182〜186、p199 |
| `claim.step.benefit.cost-minus-decided-burden.v1` | 総費用額、決定利用者負担額 -> 請求額・給付費 | —（呼出しなし） | `mhlw-disability-support-act-observed-4b8f2824` 29条3項、`r8-grant-decision-administration-202606` / `r8-grant-decision-administration-202607` p197、p199 |

基準該当B型では、公式式と地方公共団体比較補正の各step直後にhalf-upし、整数同士のminimum選択では`roundingRuleId = null`として`RoundingPolicy`を呼ばない。minimum後のplan未作成、人員欠如及びサービス管理責任者欠如factorは既存`claim.step.units.per-service-code.percentage.v1`を配列順に逐次使用し、各factor直後に`claim.rounding.units.half-up.v1`を適用する。これらはmaster上のclosed step／rounding契約であり、保護施設事務費の実値seed、resolver又はruntime算定の実装完了を意味しない。

### 適用順

```text
ADR 0023の平均工賃計算
  -> PaymentBand / 実在service-code解決
  -> 公式source rowのpercentageBaseScope / applicationKind / selector / orderを検証
  -> PerServiceCodeUnitの各row
       整数基礎単位 × 割合 -> 都度四捨五入 -> Replace/Add/Subtract
       -> 端数処理済み整数service-code単位 × 当月算定回数
  -> 同一サービス種別の整数サービス単位を月次基礎給付単位数へ合算
  -> MonthlyTargetUnitSumの各row
       selector対象の「端数処理済み単位 × 回数」を月次整数合算
       -> 合計 × 割合 -> 四捨五入
       -> 整数の加算/減算単位をcalculationOrderどおり反映
  -> サービス種別ごとの最終月次給付単位数を確定
  -> 最終月次給付単位数 × decimal地域単価を円未満切捨て
  -> 総費用額 × 10 / 100を円未満切捨て
  -> Article31SpecialBurdenStatusを検証
       Unknown: 算定不能
       NotApplicable: 1割相当額を維持
       Applicable: min(1割相当額, 検証済み特例額)
  -> 証上限・制度上限のmin
  -> 同一事業所内の公式順調整
  -> 対象者だけ正式な上限額管理結果を公式順に転記
  -> 総費用額 - 決定利用者負担額 = B型の請求額・給付費
```

同一受給者に複数サービス種別がある場合、総費用額と1割相当額はサービス種別ごとに算出してから、公式順で利用者負担を配分する。全サービス種別の給付単位数を先に一括合算して単一の単価を乗じない。`MonthlyTargetUnitSum`の対象集合もsource rowの`targetSelector`を越えて広げない。

### 公式ケースの期待値

一次資料に数値が明記されたケースだけを公式ケースとして固定する。`scope / application`列は公式文言を本ADRの閉じた値へ分類したものであり、数値例の改変ではない。一次資料に具体値がない1割相当額、上限額管理及び給付費の境界テストは、上表の式から作る仕様テストとして公式数値例と区別する。

| case ID | scope / application | 公式入力 | 公式期待値 | 検証する禁止事項 | sourceDocumentId / 物理頁 |
| --- | --- | --- | --- | --- | --- |
| `official.units.sequential-01` | `PerServiceCodeUnit` / `Replace` | `587単位 × 0.70` | `410.9 -> 411単位` | 割合適用後の未丸め値を次段へ渡さない | `r6-calculation-note` p8〜9、`r8-calculation-note` p8〜9 |
| `official.units.sequential-02` | `PerServiceCodeUnit` / `Replace` | `411単位 × 1.5` | `616.5 -> 617単位` | `587 × 0.70 × 1.5 = 616.35`を最後に1回丸めない | 同上 p9 |
| `official.units.monthly-rate-01` | `MonthlyTargetUnitSum` / `Add` | `587単位 × 6回 = 3,522単位`、`3,522 × 0.15` | `528.3 -> 528単位` | 月次合算対象の割合を日次・明細ごとに分割しない | 同上 p9 |
| `official.cost.region-01` | 対象外 | `617単位 × 4回 = 2,468単位`、`2,468 × 11.20円` | `27,641.6 -> 27,641円` | 1円未満を四捨五入しない | `r6-calculation-note` p9、`r8-calculation-note` p9〜10 |
| `official.avg-wage.daily-users-01` | 対象外 | `14.679人` | `14.7人` | 訂正前の小数点第2位四捨五入を使わない | ADR 0023、`r6-qa-v2` p11、`r6-qa-corr-2` p4 |

### scope順序差の仕様境界例

次は公式資料に掲載された数値例ではなく、`percentageBaseScope`の取り違えを検出する仕様境界テストである。公式ケースの表へ混入させない。

| case ID | scope / application | 計算 | 期待する割合加算単位 | 基礎404単位へ加算後 |
| --- | --- | --- | ---: | ---: |
| `spec.scope.per-service-code-101x4x15` | `PerServiceCodeUnit` / `Add` | `roundHalfUp(101 × 0.15) = 15`、`15 × 4` | 60 | 464 |
| `spec.scope.monthly-target-101x4x15` | `MonthlyTargetUnitSum` / `Add` | `101 × 4 = 404`、`roundHalfUp(404 × 0.15) = 61` | 61 | 465 |

同じ`101単位 × 4回 × 15%`でもscopeにより1単位差が生じる。source rowが`MonthlyTargetUnitSum`ならper-service-codeの60単位を、`PerServiceCodeUnit`ならmonthly-targetの61単位を採用してはならない。

### 未確定時とフェイルクローズ

| 境界 | 算定不能条件 |
| --- | --- |
| 平均工賃 | ADR 0023の入力、適用版、完全性又は`roundingRuleId`が欠ける。既存`AverageWageMetric`又はADR 0012の工賃丸めへフォールバックしない |
| 単位数 | `percentageBaseScope`、`percentageApplicationKind`、基礎単位、`targetSelector`、`calculationOrder`又は公式service-code rowを一意にできない。per-service-codeとmonthly-targetの相互代用、複数割合の一括乗算をしない |
| 地域単価 | サービス月・地域・サービス種別に対する公式単価を一意に解決できない。10円又は直近値を既定値にしない |
| 総費用額 | サービス種別、月次給付単位数又は単価が不明、範囲超過、負値。明細別円換算又は全サービス種別一括換算へ切り替えない |
| 利用者負担 | `Article31SpecialBurdenStatus.Unknown`、法31条特例の金額・期間・原本参照・確認根拠の欠落又は矛盾、受給者証の個別上限・入力済み状態・適用期間を確認できない。null又は自由記述から不適用・特例割合・金額を推測しない |
| 上限額管理 | ADR 0022の対象状態、成人B型に適用できる当月版一次資料又は確定済み正式結果票が欠ける。特に2024-04〜2026-05のR6成人B型で対象者は算定不能 |
| 給付費 | 決定利用者負担額が未確定、負値、総費用額超過又はB型以外の減免が混入。90%乗算へ置換しない |
| 数値型 | `double` / `float`経由、非有限値、overflow、公式桁数超過、未登録`roundingRuleId`又は未登録`calculationStepId`。丸めモード又は計算stepを既定選択しない |

### 版境界

- `claim-master-r6-04`（2024-04〜05）及び`claim-master-r6-06`（2024-06〜2026-05）は、単位数・金額換算について`r6-calculation-note` p8〜9を使う。
- `claim-master-r8-06`（2026-06以降）は`r8-calculation-note` p8〜10を使う。R8改定は平均工賃区分を変更するが、本ADRの割合単位四捨五入と金額換算切捨てを変更していない。
- R8の請求集計、1割相当額、同一事業所内調整、上限額管理結果及び給付費は、ADR 0020登録済みの令和8年6月版と、現行liveの令和8年7月版のphysical pages 197〜199で式と順序が一致することを確認した。令和8年7月版を2026-05以前へ遡及しない。
- 平均工賃の令和6／令和8境界はADR 0023に従う。本ADRは区分境界、経過措置又は平均工賃の式を再定義しない。

## 出典と再検証

物理頁はPDF先頭を1頁とする。2026-07-10に下表のlive資料を2回以上取得し、同一SHA-256とサイズを確認した。令和8年6月事務処理要領は厚生労働省の旧原URLが404であるため、ADR 0020登録済みの北九州市公式サイト再配布PDFを2026-07-11に再取得し、同一SHA-256、サイズ、262 pagesを確認した。物理頁182〜186、197〜199の抽出本文は、現行liveの令和8年7月版の同頁と完全一致することも確認した。ADR 0020に登録済みのIDを優先し、現行liveで置換された事務処理要領だけADR 0024登録済みの令和8年7月IDを併記する。

| sourceDocumentId | URL | SHA-256 / size | 本ADRで使用する箇所 |
| --- | --- | --- | --- |
| `r6-qa-v2` | https://www.mhlw.go.jp/content/001250243.pdf | `bd773dfe577b8a83fd99b28fd10f8cdc1b55d47114a03fd64c128777e54e6b8d` / 368,262 bytes。ADR 0020登録値と一致 | p11。問24の平均工賃月額と公式例。丸め方向は次行の正誤を必ず適用 |
| `r6-qa-corr-2` | https://www.mhlw.go.jp/content/001250239.pdf | `ee37c492b6989ad874f4be0f8febddab2ff351e6fe5bb0afff04223a7e2df8df` / 121,736 bytes。ADR 0020登録値と一致 | p4。問24を小数点第2位切上げへ訂正し、最終円未満四捨五入を確認 |
| `r6-calculation-note` | https://www.mhlw.go.jp/content/001494356.pdf | `958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1` / 4,193,553 bytes。ADR 0020登録値と一致 | p8〜9。割合を適用するたび四捨五入、合成コードは端数処理済み、円換算切捨て、公式例 |
| `r8-calculation-note` | https://www.mhlw.go.jp/content/001705650.pdf | `0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99` / 2,677,112 bytes。ADR 0020登録値と一致 | p8〜10。同じ単位数・円換算規則と公式例 |
| `mhlw-unit-price-notice-observed-946c3d96` | https://www.mhlw.go.jp/web/t_doc?dataId=83aa8493&dataType=0&pageNo=1 | `946c3d969ffd4128db15106d25ce6d26ff108f5460a7618e3df96352e42c0c1b` / 52,785 bytes | HTML pageNo=1。基準額10円と地域別割合。具体値と版選択はADR 0020を参照 |
| `mhlw-disability-support-act-observed-4b8f2824` | https://www.mhlw.go.jp/web/t_doc?dataId=83aa7574&dataType=0&pageNo=1 | `4b8f2824d25351a7b97f37461eaa0825be5046bfc3ab4d87595e1ab86a9443dc` / 204,793 bytes | HTML 29条3項、31条。給付費を費用額から決定負担額の控除で求める法的順序と特例給付を照合 |
| `r6-disability-support-guide-202404` | https://www.mhlw.go.jp/content/12200000/001327493.pdf | `3ead1b2d235ff15e6d0c71a129e7ef880e119272a00acd043e635aee8c637469` / 3,124,049 bytes。ADR 0020登録値と一致 | p9。R6の制度上限4区分。証実値を上書きせず、ADR 0022の補助上限として使用 |
| `r8-grant-decision-administration-202606` | https://www.city.kitakyushu.lg.jp/files/001215921.pdf | `c5070de88b83528860e8dba6c4aa88ec4bd7418dea017fbbdb5cc80dc7014798` / 1,968,795 bytes。ADR 0020登録値と一致 | p182〜186、p197〜199。上限額管理、月次給付単位数、総費用額、1割相当額、決定負担額、給付費 |
| `r8-grant-decision-administration-202607` | https://www.mhlw.go.jp/content/12200000/001721666.pdf | `1a94220c99986f353e4c63c095c156448271ecad1d7bf0d9e197d3b8ca06de65` / 1,999,016 bytes | p182〜186、p197〜199。令和8年6月版の本ADR対象式・順序を現行liveで再検証 |

障害者総合支援法29条3項は、月次の給付額を、サービス種類ごとの基準費用額の合計から負担能力等を勘案した額を控除した額と定め、同項2号はその額を費用額の10%相当額以下とする。31条の特例は市町村が定める額を使う。`mhlw-disability-support-act-observed-4b8f2824`は2026-07-10に3回取得し、全bytesが一致したlive法令証拠である。これは給付費を決定利用者負担額の控除で求める法的順序の照合に使い、地域単価又は端数方向の値ソースには使わない。

## 影響

- Phase 3-1の`RoundingPolicy`は、上表の5つの`roundingRuleId`だけを閉じた集合として実装する。選択、整数乗算・合算、`min`、配分又は減算を追加しない。
- `ClaimCalculator`はsource rowの`percentageBaseScope`、`percentageApplicationKind`、`targetSelector`及び`calculationOrder`を検証し、per-service-codeとmonthly-targetを別の`calculationStepId`で実行する。仕様境界例と公式例を別のテーブル駆動テストにする。
- `ClaimCalculator`はサービスコード明細の円額を先に作らず、全monthly-target結果を反映したサービス種別ごとの最終月次給付単位数を構築してから地域単価を適用する。
- `BurdenCalculator`は1割相当額の切捨てだけを`RoundingPolicy`へ委譲し、法31条三値入力、証上限、制度上限、正式結果票及び優先順を`calculationStepId`で扱う。
- Phase 3-1で`Article31SpecialBurdenStatus`、特例額、`effectiveFrom` / `effectiveTo`、受給者証原本document reference及び確認証跡をモデル、migration、DTO、入力UIへ追加する。2026-07-12時点で入力UI以外は実装済み。既存null又は`SupplyNotes`は`NotApplicable`へ自動移行しない。
- 確定スナップショットは、`roundingRuleId`と`calculationStepId`を別フィールドで、丸め前値、丸め後値、scope、selector、適用順、masterVersion、sourceDocumentIds及びsource pageとともに保持する。合成サービスコードでは「公式表で端数処理済み」であることとsource rowを保持する。
- 2026-06-29版Phase 3-1計画の`MultiplyAndFloor`を明細ごとに呼ぶ設計例、`double`の`InlineData`及びR9仮データは現行設計の実装契約にしない。Phase 3-1再計画時に本ADRと2026-06-29設計の現行化注記へ合わせる。
- ADR 0012の工賃支払額用`RoundingPolicy`と、請求算定用`roundingRuleId` / `calculationStepId`のnamespace又は型を分け、同名の`HalfUp`を暗黙共有しない。
