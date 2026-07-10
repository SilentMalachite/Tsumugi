# ADR 0023: 平均工賃月額の正式式と令和8年基本報酬区分の経過措置

**ステータス**: 確定（2026-07-10）

## 結論

就労継続支援B型サービス費(Ⅰ)〜(Ⅲ)の基本報酬選択に用いる平均工賃月額は、サービス提供月が属する年度の前年度について、次の正式式で算出する。

```text
年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12
```

中間値の「開所日1日当たりの平均利用者数」は小数点第1位までとし、小数点第2位以下があるときは小数点第2位で切り上げる。最後に平均工賃月額の円未満を四捨五入する。通常式に必要な前年度実績の期間・対象利用者・開所日・丸め順のいずれかを確認できない場合や、年間開所日数又は年間延べ利用者数が0の場合は、0円又は1万円未満の区分へ倒さず算定を停止する。

令和6通常区分は2024-04から2026-05まで、令和8区分は2026-06から使用する。`R8ReformStatus`（令和8見直しの対象状態）、届出済み`AverageWageBandOption`（体制届の公式選択番号）、請求に使う数値`PaymentBand`、最終的なservice-codeを別の値として保持する。体制届の公式`8=なし（経過措置対象）`はR6/R8共通の`FiledTransition` optionであり、数値区分でもservice-code上の区分9でもない。新規指定の公式要件を検証した後に、通常の初期期間は「1万円未満とみなす」規則から数値`PaymentBand` 8へ変換し、実在する基本報酬service-codeを解決する。

令和8は、改定対象の新しい12区分、改定対象外の従前6区分及び変更なしの2数値区分を`PaymentBand`マスタとして持つ。どの数値区分群を使うかは指定権者の有効な登録スナップショットと`R8ReformStatus`で決め、計算した平均工賃月額だけから改定対象・対象外、過去区分又は届出optionを推測しない。変更届の新規提出が不要な自動切替・変更なしの場合も、サービス月に有効な指定権者台帳の登録状態を検証する。

Phase 2の `Tsumugi.Domain.Logic.AverageWageMetric` は工賃支払明細の暫定集計指標として残し、破壊変更しない。Phase 3-1では請求専用の `Logic/Claim/AverageWageCalculator` を別実装し、正式入力、丸め、結果、出典版及び失敗理由を分離する。

## 背景

Phase 2の `AverageWageMetric` は `WageStatement` の金額合計を明細件数又は利用者の実人数で割る。空配列と分母0を0円として返し、整数除算で切り捨てる。この関数には年間開所日数、年間延べ利用者数、前年度実績の期間完全性、公式の中間丸め及び出典版がなく、請求区分の正式式とは意味が異なる。2026-07-10時点で、本番コードからの利用はなく、定義とDomainテストだけが存在する。

令和6改定で平均工賃月額の式と丸めが変更され、令和8年6月には1万5千円以上の基本報酬区分が細分化された。一方、一定の既存事業所は令和5年度と令和6年度等の届出区分の比較により従前区分を継続でき、1万5千円未満の区分は変更されない。同じ平均工賃月額でも、指定時期、過去の届出、経過措置の根拠書類によって令和8に使う区分群が異なるため、平均工賃計算と令和8改定状態を分離する必要がある。

公式の体制表では、R6-04の `r6-capability-202404` 273行、R6-06の `r6-capability-202406` 240行、R8-06の `r8-capability-202606` 242行で、平均工賃月額区分の選択番号`8`を一貫して「なし（経過措置対象）」、選択番号`9`を「平均工賃月額が1万円未満」としている。報酬告示の数値区分（一）〜（八）や補助資料の表示上の区分番号と、体制届の選択番号は同じ番号体系ではない。これらを1つのenumにすると、存在しない区分9 service-codeを生成する危険がある。

## 選択肢

### A: 正式計算、届出、経過措置を分離した版付き契約（採用）

- 正式な純粋計算と行政上の届出選択を別々に監査できる。
- 令和8の対象・対象外を平均工賃から誤って推測しない。
- 入力又は根拠書類が不足する請求は算定不能になる。

### B: `AverageWageMetric` を正式式へ置換する

- 既存のPhase 2集計指標の意味と戻り値を変え、空入力0や既存テストとの互換性を壊すため採用しない。

### C: 平均工賃月額だけから令和8区分を決める

- 同じ金額でも令和8改定対象・対象外が混在し、届出済み区分と経過措置の根拠を失うため採用しない。

## 決定

### 正式計算の入力

Phase 3-1の請求用計算は、少なくとも次の入力を構造化して受ける。自由記述、`Flags`、欠落月の暗黙の0では代用しない。

| 入力 | 正式な意味 | 検証 |
| --- | --- | --- |
| `serviceMonth` | 請求するサービス提供月 | 対応する請求マスタ版が1件だけ存在すること |
| `sourceFiscalYear` | `serviceMonth`が属する年度の前年度（4月1日〜翌年3月31日） | 自動導出した年度と入力年度が一致すること |
| `annualWagePaidYen` | 対象年度に利用者へ支払った工賃、給与、手当、賞与その他名称を問わず利用者へ支払う全ての額の合計 | 非負、通常式が対象とする前年度の全期間について支払記録又は支払なしを確認できること |
| `annualExtendedUsers` | 対象年度の開所日に就労継続支援B型を利用した対象利用者の人日合計 | 正の整数、日別実績へ逆引きできること。外部事業所に雇用され一時的な復職・労働時間延長支援を受ける者を除く |
| `annualOpeningDays` | 対象年度に工賃支払いが生じる生産活動を実施した日数 | 正の整数。生産活動を目的としないレクリエーション・行事日は除き、利用者の生産品等を販売する地域バザー等は含めてよい |
| `fiscalYearCompleteness` | 通常式が対象とする前年度について支払、利用、人日、開所の必要実績を照合済みであること | `Complete`だけ通常式を計算可能。未取込、未照合、月欠落、短期実績又は休止期間を0で補完して`Complete`にしない |
| `sourceDocumentVersion` | ADR 0020の版束と本ADRの計算規則版 | `claim-master-r6-04`、`claim-master-r6-06`又は`claim-master-r8-06`に解決できること |

工賃の範囲と正式式は `r6-employment-guidance-r6` 物理22〜23頁、対象利用者の除外と式は `r6-calculation-note` 物理289頁及び `r8-calculation-note` 物理289〜290頁を根拠とする。請求用集計は、訂正・取消を含む追記型実績から対象年度時点の実効値を作ってから行い、同じ支払を重複計上しない。

### 計算順と丸め

分母は正の値、工賃額は非負の値を対象に、次の順序を変更しない。

1. `rawDailyAverageUsers = annualExtendedUsers / annualOpeningDays` を十進数で求める。
2. `rawDailyAverageUsers` を小数点第1位までとし、小数点第2位以下が1つでもあれば正方向へ切り上げ、`roundedDailyAverageUsers` とする。例は14.679人から14.7人である。
3. `rawAverageWageMonth = annualWagePaidYen / roundedDailyAverageUsers / 12` を求める。12は対象年度中の実開所月数へ置換しない。
4. `rawAverageWageMonth` の円未満を四捨五入し、非負整数円の `baseAverageWageMonthYen` とする。

手順2の確定規則は、`r6-qa-v2` 物理11頁の問24を `r6-qa-corr-2` 物理4頁が「小数点第2位を四捨五入」から「小数点第2位を切り上げ」へ訂正した結果である。手順4は同じ問24の円未満四捨五入による。訂正前の規則を選択可能な旧版として残さない。

`annualOpeningDays = 0`、`annualExtendedUsers = 0`、丸め後の平均利用者数が0、整数円の範囲超過、負値又は日別・月別合計との不一致は計算失敗とする。0除算を0円に変換せず、区分8を選ばない。

### 12か月未満、新規指定、休止等

公式資料が定める規則と、資料不足時に誤請求を生成しないための製品安全判断を区別する。

| 状況 | 公式規則 | 製品安全判断 | 根拠 |
| --- | --- | --- | --- |
| 通常の既存事業所 | 前年度の工賃支払総額、延べ利用者数、年間開所日数を用い、最後に12月で除する | 通常式に必要な前年度の期間と実績を全て照合できる場合だけ計算する | `r6-calculation-note`物理289頁、`r8-calculation-note`物理289〜290頁 |
| 開所日の範囲 | 原則として工賃の支払いが生じる生産活動の実施日を含め、生産活動を目的としないレクリエーション・行事日は含めない。利用者の生産品等を販売した地域バザー等は含めてよい | 公式の範囲で日別実績を特定できない日は、開所又は非開所を推測しない | `r6-qa-v2`物理11頁 |
| 12か月未満、休止その他の短期実績 | 登録済み一次資料には、通常式へ休止期間を0で補完する規則も、実開所月数で除す正式な短期再計算式もない | 通常式で0埋め、年換算、実開所月数除算又は近接年度補完をしない。適用できる新規指定の例外について、指定権者確認済みの届出option、対象期間及び根拠書類が全て揃う場合だけ専用分岐で扱う。6月実績の数値optionを使う場合は届出平均工賃値も必須とし、それ以外は算定不能とする | 製品安全判断。公式に明記された例外期間は `r6-calculation-note`物理291頁、`r8-calculation-note`物理292〜293頁 |
| 対象年度の開所日又は延べ利用者が0 | 通常式では0除算となる | 0円、区分8又は届出区分を生成せず算定不能とする | 製品安全判断 |
| 年度途中を含む新規指定 | 初年度の1年間は平均工賃月額1万円未満とみなす。年度途中指定のこの1年間は初年度から2年度目にまたがり得る。支援開始から6月経過した月から当該年度3月までは、開始後6月間の平均工賃月額に応じる数値optionを届け出ることができる | 指定日、支援開始日、対象サービス月が指定後1年未満の公式期間内であること、届出optionと根拠書類を検証する。6月値を通常式の12から推測しない | `r6-calculation-note`物理291頁、`r8-calculation-note`物理292〜293頁 |
| 廃止、再開、事業譲渡等で同一事業所・対象年度を一意にできない | 登録済み一次資料から一律の短期再計算規則は確認できない | 指定権者が確認した指定履歴、届出値、届出区分、対象期間及び根拠書類が揃うまで算定不能とする | 製品安全判断 |

登録済み一次資料は、新規指定の「6月間における平均工賃月額」を使用できる期間は示すが、その6月値について年間式とは別の分子、月除数及び丸め順を明示していない。したがって `AverageWageCalculator` は年間式の12を6へ置換して6月値を自動生成しない。通常の新規指定期間は、有効な`AverageWageBandOption.FiledTransition`を保持したまま、公式の「1万円未満とみなす」規則から当月版の数値`PaymentBand` 8へ解決する。支援開始後6月実績を使う場合は、指定権者確認済みの `sixMonthFiledAverageWageYen`、数値`AverageWageBandOption`、対象6月、算定開始月及び根拠書類IDを必須にし、その数値optionから`PaymentBand`を解決する。値又はoptionが欠ける、両者が矛盾する、6月を一意にできない場合は算定不能とする。

通常式の前年度に代えて前々年度を使えるのは、同一都道府県内の8割のB型事業所で工賃実績が低下し都道府県がやむを得ないと認めた場合、激甚災害・災害救助法適用地域で工賃支払額の減少が見込まれる場合、又は大規模災害の間接影響を指定権者が認めた場合だけである。自動選択せず、指定権者の決定、対象年度、根拠文書を必須入力とし、選択した前々年度にも同じ正式式と完全性検証を適用する（`r8-calculation-note`物理290〜291頁）。

### 改定状態、届出option、支払区分、コードの責務分離

次の4軸を別フィールド・別型にする。相互変換は後述の閉じた表と選択手順だけで行う。

| 型 / 出力 | 責務 | 閉じた値 |
| --- | --- | --- |
| `R8ReformStatus` | 令和8年6月の区分見直しが事業所へどう適用されるか | `NotApplicableBeforeR8`、`ReformTarget`、`ReformExempt`、`UnchangedBelow15000`、`Unknown` |
| `AverageWageBandOption` | サービス月に有効な指定権者台帳・登録スナップショット上の体制届「平均工賃月額区分」 | `Numeric(officialOptionCode)`、`FiledTransition`、`ProductionActivitySupport`。`masterVersion`と公式選択番号を必ず併記 |
| `PaymentBand` | 基本報酬の金額境界又は新規指定のみなし規則を適用した、請求用の数値区分 | 後掲28行の数値keyだけ。`FiledTransition`を含めない |
| `ServiceCodeSelection` | 報酬体系、人員配置、定員、`PaymentBand`、減算条件から解決した実在service-code・単位数 | 適用版サービスコードExcelの基本報酬行に実在する組合せだけ |

`FiledTransition`は`R8ReformStatus`の値ではない。例えばR8改定対象の新規指定事業所は、`R8ReformStatus.ReformTarget`を保持したまま、一時的に`AverageWageBandOption.FiledTransition`を持ち、公式のみなし規則で`PaymentBand` 8を使える。初期期間終了後に数値optionへ移行しても`R8ReformStatus`は失われず、将来の区分群選択に使う。

### 版付き`AverageWageBandOption`と`PaymentBand`対応

`AverageWageBandOption`は体制表の公式選択番号をそのまま保持する。次表のedition表記`R6-04`、`R6-06`、`R8-06`はそれぞれ永続化時の正規`masterVersion`である`claim-master-r6-04`、`claim-master-r6-06`、`claim-master-r8-06`の表示略記であり、略記を保存しない。`R6-BAND`は `r6-capability-202404`「介護給付費等　体制等状況一覧」273行（2024-04〜05）及び `r6-capability-202406`同240行（2024-06〜2026-05）、`R8-BAND`は `r8-capability-202606`「別紙1-1」242行と `r8-capability-correction`物理9頁（2026-06以降）である。

| edition（正規masterVersionの表示略記） | effectiveFrom | effectiveTo | officialOptionCode | `AverageWageBandOption` | 対応する数値`PaymentBand` | sourceLocator |
| --- | --- | --- | ---: | --- | --- | --- |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 1 | `Numeric(1)` | `r6.standard.1` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 2 | `Numeric(2)` | `r6.standard.2` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 3 | `Numeric(3)` | `r6.standard.3` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 4 | `Numeric(4)` | `r6.standard.4` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 5 | `Numeric(5)` | `r6.standard.5` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 6 | `Numeric(6)` | `r6.standard.6` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 7 | `Numeric(7)` | `r6.standard.7` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 8 | `FiledTransition` | 直接対応なし。新規指定要件の検証後に`r6.standard.8`へ解決 | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 9 | `Numeric(9)` | `r6.standard.8` | R6-BAND |
| R6-04 / R6-06 | 2024-04 | 2026-05 | 10 | `ProductionActivitySupport` | 平均工賃`PaymentBand`の対象外 | R6-BAND |
| R8-06 | 2026-06 | null | 1 | `Numeric(1)` | `r8.reform-exempt.1` | R8-BAND |
| R8-06 | 2026-06 | null | 2 | `Numeric(2)` | `r8.reform-exempt.2` | R8-BAND |
| R8-06 | 2026-06 | null | 3 | `Numeric(3)` | `r8.reform-exempt.3` | R8-BAND |
| R8-06 | 2026-06 | null | 4 | `Numeric(4)` | `r8.reform-exempt.4` | R8-BAND |
| R8-06 | 2026-06 | null | 5 | `Numeric(5)` | `r8.reform-exempt.5` | R8-BAND |
| R8-06 | 2026-06 | null | 6 | `Numeric(6)` | `r8.reform-exempt.6` | R8-BAND |
| R8-06 | 2026-06 | null | 7 | `Numeric(7)` | `r8.unchanged.7` | R8-BAND |
| R8-06 | 2026-06 | null | 8 | `FiledTransition` | 直接対応なし。新規指定要件の検証後に`r8.unchanged.8`へ解決 | R8-BAND |
| R8-06 | 2026-06 | null | 9 | `Numeric(9)` | `r8.unchanged.8` | R8-BAND |
| R8-06 | 2026-06 | null | 10 | `ProductionActivitySupport` | 平均工賃`PaymentBand`の対象外 | R8-BAND |
| R8-06 | 2026-06 | null | 11 | `Numeric(11)` | `r8.reform-target.1` | R8-BAND |
| R8-06 | 2026-06 | null | 12 | `Numeric(12)` | `r8.reform-target.A` | R8-BAND |
| R8-06 | 2026-06 | null | 13 | `Numeric(13)` | `r8.reform-target.2` | R8-BAND |
| R8-06 | 2026-06 | null | 14 | `Numeric(14)` | `r8.reform-target.B` | R8-BAND |
| R8-06 | 2026-06 | null | 15 | `Numeric(15)` | `r8.reform-target.3` | R8-BAND |
| R8-06 | 2026-06 | null | 16 | `Numeric(16)` | `r8.reform-target.C` | R8-BAND |
| R8-06 | 2026-06 | null | 17 | `Numeric(17)` | `r8.reform-target.4` | R8-BAND |
| R8-06 | 2026-06 | null | 18 | `Numeric(18)` | `r8.reform-target.D` | R8-BAND |
| R8-06 | 2026-06 | null | 19 | `Numeric(19)` | `r8.reform-target.5` | R8-BAND |
| R8-06 | 2026-06 | null | 20 | `Numeric(20)` | `r8.reform-target.E` | R8-BAND |
| R8-06 | 2026-06 | null | 21 | `Numeric(21)` | `r8.reform-target.6` | R8-BAND |
| R8-06 | 2026-06 | null | 22 | `Numeric(22)` | `r8.reform-target.F` | R8-BAND |

R8の`Numeric(7)`、`FiledTransition`（公式option 8）、`Numeric(9)`は、令和8資料上「変更なし」である。補助資料の表示区分（七）、（八）、（九）と体制表の公式option番号は並びが異なるため、名称又は数字だけで相互変換しない（`r8-b-reward-band-guide`物理5〜6頁、`r8-qa-v1`物理14〜15頁、`r8-capability-202606` 242行）。

### 基本報酬区分マスタ

`lowerInclusiveYen` は下端を含み、`upperExclusiveYen` は上端を含まない。`null` の上端は上限なしである。このマスタは28行全てが数値`PaymentBand`であり、`FiledTransition`を含めない。従前の29行から1行減った理由は、非数値の「なし（経過措置対象）」を数値bandから削除し、欠落させず版付き`AverageWageBandOption.FiledTransition`へ移したためである。負の平均工賃月額は全ての行に該当しない。

#### 令和6通常区分

| key | displayName | lowerInclusiveYen | upperExclusiveYen | effectiveFrom | effectiveTo | sourceDocumentId | 物理頁 |
| --- | --- | ---: | ---: | --- | --- | --- | ---: |
| `r6.standard.1` | （一）4万5千円以上 | 45,000 | null | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.2` | （二）3万5千円以上4万5千円未満 | 35,000 | 45,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.3` | （三）3万円以上3万5千円未満 | 30,000 | 35,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.4` | （四）2万5千円以上3万円未満 | 25,000 | 30,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.5` | （五）2万円以上2万5千円未満 | 20,000 | 25,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.6` | （六）1万5千円以上2万円未満 | 15,000 | 20,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.7` | （七）1万円以上1万5千円未満 | 10,000 | 15,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |
| `r6.standard.8` | （八）1万円未満 | 0 | 10,000 | 2024-04 | 2026-05 | `r6-fee-notice` | 129〜136 |

`r6-service-codes-2-xlsx` のワークブック順38「18就労継続支援(B・基本)」7〜912行でも、定員・人員配置別の基本報酬行が同じ8境界を持つことを照合した。913行以降は独立した減算・加算行であり、`PaymentBand`から基本報酬service-codeを選ぶ範囲へ含めない。

#### 令和8改定対象区分

| key | displayName | lowerInclusiveYen | upperExclusiveYen | effectiveFrom | effectiveTo | sourceDocumentId | 物理頁 |
| --- | --- | ---: | ---: | --- | --- | --- | ---: |
| `r8.reform-target.1` | （R8改定対象）（一）4万8千円以上 | 48,000 | null | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.A` | （R8改定対象）（A）4万5千円以上4万8千円未満 | 45,000 | 48,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.2` | （R8改定対象）（二）3万8千円以上4万5千円未満 | 38,000 | 45,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.B` | （R8改定対象）（B）3万5千円以上3万8千円未満 | 35,000 | 38,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.3` | （R8改定対象）（三）3万3千円以上3万5千円未満 | 33,000 | 35,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.C` | （R8改定対象）（C）3万円以上3万3千円未満 | 30,000 | 33,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.4` | （R8改定対象）（四）2万8千円以上3万円未満 | 28,000 | 30,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.D` | （R8改定対象）（D）2万5千円以上2万8千円未満 | 25,000 | 28,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.5` | （R8改定対象）（五）2万3千円以上2万5千円未満 | 23,000 | 25,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.E` | （R8改定対象）（E）2万円以上2万3千円未満 | 20,000 | 23,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.6` | （R8改定対象）（六）1万8千円以上2万円未満 | 18,000 | 20,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-target.F` | （R8改定対象）（F）1万5千円以上1万8千円未満 | 15,000 | 18,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |

#### 令和8改定対象外の従前区分と変更なし区分

| key | displayName | lowerInclusiveYen | upperExclusiveYen | effectiveFrom | effectiveTo | sourceDocumentId | 物理頁 |
| --- | --- | ---: | ---: | --- | --- | --- | ---: |
| `r8.reform-exempt.1` | （R8改定対象外）（一）4万5千円以上 | 45,000 | null | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-exempt.2` | （R8改定対象外）（二）3万5千円以上4万5千円未満 | 35,000 | 45,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-exempt.3` | （R8改定対象外）（三）3万円以上3万5千円未満 | 30,000 | 35,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-exempt.4` | （R8改定対象外）（四）2万5千円以上3万円未満 | 25,000 | 30,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-exempt.5` | （R8改定対象外）（五）2万円以上2万5千円未満 | 20,000 | 25,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.reform-exempt.6` | （R8改定対象外）（六）1万5千円以上2万円未満 | 15,000 | 20,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.unchanged.7` | （七）1万円以上1万5千円未満 | 10,000 | 15,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |
| `r8.unchanged.8` | （八）1万円未満 | 0 | 10,000 | 2026-06 | null | `r8-b-reward-band-guide` | 5 |

`r8-fee-notice`物理43〜55頁と `r8-service-codes-2-xlsx` のワークブック順38「18就労継続支援(B・基本)」7〜1993行でも、改定対象外、変更なし及び改定対象の数値境界を定員・人員配置別に照合した。1993行の`46Z006`は令和8年6月以降の新規指定事業所に対する独立した特例であり、`FiledTransition`又は数値`PaymentBand`のservice-codeではない。1994行以降は別の独立減算・加算行である。公式の「なし（経過措置対象）」は前節の`AverageWageBandOption.FiledTransition`だけに保持し、`PaymentBand`行又はservice-codeを作らない。

### 令和8経過措置の入力と判定

令和8の見直しは2026-06サービス月から施行する。2026-04・05は令和6通常区分、2026-06以降は指定権者が確認した`R8ReformStatus`に対応する区分群を使う。平均工賃の参照年度はどちらも原則として2025年度であり、同じ前年度計算値に対して6月から区分境界だけが切り替わる。

`OfficeClaimProfile` は平均工賃計算結果とは別に、少なくとも次を保持する。

| 入力 | 内容 |
| --- | --- |
| 指定履歴 | 当初指定日、支援開始日、廃止・再開・事業承継、同一事業所と指定権者が確認できる証拠 |
| `R8ReformStatus` | `NotApplicableBeforeR8`、`ReformTarget`、`ReformExempt`、`UnchangedBelow15000`又は`Unknown`、指定権者の確認日と根拠書類 |
| 既存事業所の過去区分比較 | R8改定対象外判定に使う令和5年度・令和6年度等の公式登録区分、比較対象月、指定権者の判定。新規指定`FiledTransition`の必須入力にはしない |
| 有効登録スナップショット | サービス月に有効な報酬体系、版付き`AverageWageBandOption`、有効期間、指定権者台帳の登録・確認状態、登録根拠、原本参照ID |
| 変更届提出 | 提出がある場合の提出日、受理日、変更内容、原本参照ID。新規提出不要の場合はnullを許すが、有効登録スナップショットは必須 |

#### 既存事業所のR8改定判定

原則として、令和6年度区分が令和5年度区分と同じ又は低下している既存事業所は`ReformExempt`として従前の数値区分群を使うことができる。令和6年度に1万円未満とみなした事業所は令和7年度区分、令和6年4月以降に指定され6月間平均で算定した事業所は算定開始月の公式登録区分を比較に使う。この比較は既存事業所のR8改定対象外判定だけに使い、新規指定の`FiledTransition`適格性へ流用しない（`r8-calculation-note`物理292頁）。平均工賃から過去の登録区分を再構成しない。

運用上の確認は次のとおりである。

- 令和5年4月以前に指定された事業所は、令和6年3月と令和6年4月の基本報酬区分が分かる書類等を根拠とする。両区分が変わらない又は下がっている場合は見直し対象外となる。
- 令和5年5月から令和6年3月までに指定された事業所は、新規指定時の区分8の終了月、終了翌月の届出区分及び指定月に応じた比較対象を `r8-b-reward-band-guide` 物理2〜3頁の図で特定し、指定権者が確認した比較結果を入力する。
- 令和6年4月以降に指定された事業所は見直し対象となる。平均工賃だけを理由に対象外へ変更しない。
- 令和7年度平均工賃月額が1万5千円未満で有効な体制optionが`Numeric(7)`又は`Numeric(9)`の事業所は区分境界が変わらず、`UnchangedBelow15000`とする。

`ReformExempt`について、登録済み一次資料には固定の終了月が示されていないため、`effectiveTo`を推測しない。指定権者が適用を終了・変更した登録の有効月、適用根拠を失った月、又は将来の公式改定が終了月を定めた月に期間行を追加する。現在の平均工賃が変動したことだけで`R8ReformStatus`を反転させない。

#### 新規指定の`FiledTransition`

`AverageWageBandOption.FiledTransition`は、R8改定対象外判定とは別の新規指定分岐である。R6/R8のどの版でも公式option codeは`8`であり、次を全て検証する。

- 新規指定事業所であること、指定日と支援開始日を原本へ逆引きできること。
- 対象サービス日が指定日から1年未満の公式初期期間内であること。年度途中指定ではこの1年間が初年度から2年度目にまたがり得る。
- サービス月に有効な指定権者台帳の登録スナップショットが`FiledTransition`であり、その有効期間と根拠書類が指定履歴に一致すること。
- 過去区分比較は要求しない。比較可能な過去区分がないことを理由に拒否しない。

適格な通常初期期間は平均工賃を計算せず、公式の「1万円未満とみなす」規則から、R6では`r6.standard.8`、R8では`r8.unchanged.8`を`PaymentBand`として出力する。`AverageWageBandOption.FiledTransition`は監査用に保持するが、service-code resolverへ渡さない。支援開始から6月経過後に指定権者確認済みの数値optionを登録した場合だけ、その版付き数値optionと確認済み6月平均工賃値から数値`PaymentBand`へ移行する。

指定後1年の公式期間が終了した、有効登録が数値optionへ切り替わった、又は指定権者が`FiledTransition`を終了した場合、`FiledTransition`分岐を終了する。R8サービス月では`R8ReformStatus`を保持したまま数値optionへ移行する。期間終了後も`FiledTransition`しかない場合は、区分8へ継続せず算定不能とする（`r6-calculation-note`物理291頁、`r8-calculation-note`物理292〜293頁）。

#### 変更届提出と有効登録状態

変更届を新たに提出した事実と、サービス月に有効な指定権者台帳・登録スナップショットを分離する。`r8-b-reward-band-guide`物理4〜6頁及び`r8-qa-v1`物理14〜15頁に従い、R8改定対象は原則として変更届を受ける一方、`ReformExempt`は公式の自動切替、1万5千円未満は変更なしとなり、新規の変更届が不要である。いずれも請求時には当月有効な版付き`AverageWageBandOption`と登録根拠を検証し、変更届nullだけを未届扱いしない。

### 区分とコード選択の優先順

次の順序で選択し、途中で不明又は矛盾があれば停止する。

1. `serviceMonth`から請求マスタ版と平均工賃の参照年度を1件に決める。
2. 指定権者台帳からサービス月に有効な登録スナップショットを1件選び、`masterVersion`、報酬体系、`AverageWageBandOption`、有効期間、登録根拠を検証する。新規変更届がなくても、自動切替又は変更なしの登録根拠があれば有効とする。
3. 2026-06以降は`R8ReformStatus`を検証する。`ReformExempt`だけ既存事業所の過去区分比較を要求し、`FiledTransition`の新規指定分岐には要求しない。`Unknown`は拒否する。
4. optionが`FiledTransition`なら、指定日、支援開始日、指定後1年未満の対象期間、有効登録及び根拠書類を検証する。適格なら当月版の`PaymentBand` 8へ解決し、手順8へ進む。option自体又は「区分9」service-codeを渡さない。
5. optionが`ProductionActivitySupport`なら平均工賃連動の報酬体系I〜IIIの本手順では拒否し、報酬体系IV〜VIの別resolverへ渡す。数値optionだけ後続する。
6. 通常の既存事業所は`AverageWageCalculator`で `baseAverageWageMonthYen` を算出する。新規指定の6月実績を選んだ場合だけ、指定権者確認済みの数値optionと届出平均工賃値を使う。重度者支援体制加算(Ⅰ)の2,000円調整を選ぶ正式根拠がある場合は基礎値を上書きせず別値にする（`r6-calculation-note`物理289頁、`r8-calculation-note`物理290頁）。
7. サービス月と`R8ReformStatus`から開いた数値区分群の境界で期待`PaymentBand`を1件求め、版付き数値`AverageWageBandOption`の閉じた対応先と一致することを検証する。
8. 検証済みの届出済み報酬体系、人員配置、定員規模及び`PaymentBand`から、R6は基本シート7〜912行、R8は7〜1993行の公式scopeにある実在service-code・単位数を解決する。R8の1993行`46Z006`は新規指定の独立条件で別評価し、`FiledTransition`のコードとして選ばない。`AverageWageBandOption`は監査スナップショットへ保持するがservice-code resolverの区分値にしない。
9. 出力は`R8ReformStatus`、入力`AverageWageBandOption`、解決済み`PaymentBand`及び`ServiceCodeSelection`を別フィールドで請求確定スナップショットへ保存する。

令和8の改定対象判定、届出option及び過去区分は `OfficeClaimProfile` の行政入力であり、`AverageWageCalculationResult` 又は `Flags` へ格納しない。逆に、年間工賃、延べ利用者、開所日、丸め前後の値は計算監査情報であり、OfficeCapabilityのone-hotキーにしない。

### Phase 2 `AverageWageMetric`との責務分離

`AverageWageMetric` と `AverageWageDenominator` は変更しない。既存の `Calculate(IReadOnlyList<WageStatement>, AverageWageDenominator)` はPhase 2内の暫定集計に限定し、請求マスタの区分選択、R8経過措置又は正式な平均工賃実績報告へ使わない。

Phase 3-1では `src/Tsumugi.Domain/Logic/Claim/AverageWageCalculator.cs` に別の純粋計算を追加し、概念上、次を分離する。

| 構造 | 保持するもの |
| --- | --- |
| `AverageWageCalculationInput` | 対象年度、年間工賃支払総額、年間延べ利用者数、年間開所日数、完全性、版ID |
| `AverageWageCalculationResult` | 基礎平均工賃月額、丸め前・丸め後の日平均利用者数、対象年度、丸め規則ID、sourceDocumentIds |
| `AverageWageCalculationFailure` | 欠落月、0除算、負値、合計不一致、版不明、範囲超過等の失敗理由 |
| `AverageWageBandRegistrationSnapshot` | masterVersion、サービス月に有効な`AverageWageBandOption`、有効期間、登録由来、指定権者確認状態、根拠文書。変更届提出とは別 |
| `R8ReformContext` | `R8ReformStatus`、既存事業所だけに必要な過去区分比較、確認日、根拠文書 |
| `NewOfficeFiledTransitionContext` | 指定日、支援開始日、指定後1年未満の対象期間、option 8の有効登録、根拠文書。過去区分を含めない |
| `PaymentBandSelectionResult` | 入力optionを保持したまま解決した数値`PaymentBand`、解決理由（正式式、6月届出値又は1万円未満みなし）、masterVersion |
| `ServiceCodeSelection` | 数値`PaymentBand`、報酬体系、人員配置、定員から解決した実在service-code・単位数・source row |

計算・区分・コードの各結果に `masterVersion`、`sourceDocumentIds`、source row及び丸め規則版を保存し、請求確定時のスナップショットから再現できるようにする。外部マスタのoption、閾値、2,000円調整及び期間はC#定数へ転記せず、ADR 0020の版束から読み込む。

### フェイルクローズ条件

次は全て算定不能とし、0円、数値`PaymentBand`、`FiledTransition`、R8改定対象又は対象外へ自動補正しない。

| 境界 | 拒否条件 |
| --- | --- |
| 年度 | `serviceMonth`なし、前年度不一致、通常式の必要期間・実績が未確認、月欠落・短期実績・休止期間を0で補完している |
| 金額・実績 | 負額、訂正重複、月合計と年合計の不一致、対象外利用者の混入 |
| 分母 | 開所日0、延べ利用者0、丸め後0、日別実績に逆引き不能 |
| 丸め | 訂正前の四捨五入、整数除算、途中切捨て、最終円未満の切捨てを使用 |
| 新規指定 | 指定日・支援開始日不明、サービス日が指定後1年未満の公式期間外、option 8の有効登録・根拠欠落、6月数値optionを使う場合の対象6月・届出値・根拠の欠落又は矛盾。過去区分がないこと単独では拒否しない |
| R8判定 | `Unknown`、指定権者未確認、`ReformExempt`で必要な比較対象区分又は根拠書類欠落、平均工賃又は`FiledTransition`から対象・対象外を推測 |
| 有効登録 | サービス月の登録スナップショットなし・複数、masterVersion・有効期間・公式option codeの不一致。新規変更届nullだけでは拒否しない |
| `FiledTransition` | 公式option codeが8でない、既存事業所のR8改定比較と混同、公式期間終了後も継続、optionをservice-codeへ渡す、存在しない区分9 codeを生成する |
| 数値option | 版外option、`R8ReformStatus`と許可option群の不一致、算出平均又は確認済み6月値から求めた`PaymentBand`と対応表の不一致 |
| `PaymentBand` / code | 版なし、期間重複、境界の穴・重複、未知key、基本報酬範囲外の加算・減算行混入、service-codeが一意に実在しない、未知sourceDocumentId |

### 出典locatorとライブ検証

本ADRの「物理頁」はPDF先頭を1頁とする。使用するIDは全てADR 0020のSourceDocumentIdsへ登録済みである。

| sourceDocumentId | 物理頁 / sheet・row | 本ADRでの用途 |
| --- | --- | --- |
| `r6-employment-guidance-r6` | 22〜23頁 | 工賃の範囲、前年度総額、延べ利用者、開所日、正式式 |
| `r6-calculation-note` | 289〜291頁 | 対象利用者除外、正式式、2,000円調整、前々年度例外、新規指定 |
| `r6-qa-v2` | 11頁 | 開所日の範囲、丸めの問24。丸め本文は正誤適用前なので単独使用しない |
| `r6-qa-corr-2` | 4頁 | 問24の中間丸めを小数点第2位切上げへ訂正、最終円未満四捨五入 |
| `r6-fee-notice` | 129〜136頁 | 令和6の8境界 |
| `r6-capability-202404` | 「介護給付費等　体制等状況一覧」273行 | R6-04の平均工賃月額区分option 1〜10。8=なし（経過措置対象）、9=1万円未満 |
| `r6-capability-202406` | 「介護給付費等　体制等状況一覧」240行 | R6-06の平均工賃月額区分option 1〜10。8=なし（経過措置対象）、9=1万円未満 |
| `r6-service-codes-2-xlsx` | ワークブック順38、7〜912行 | 令和6基本報酬の数値境界・実在service-code。913行以降の独立減算・加算を除外 |
| `r8-calculation-note` | 6、287〜293頁 | 令和8届出、正式式、R8経過措置、新規指定 |
| `r8-b-reward-band-guide` | 1〜7頁 | 指定時期別の対象外条件、届出フロー、R8新・従前・変更なし区分 |
| `r8-qa-v1` | 14〜15頁 | 2026-04・05と6月以降の届出、3区分群、対象外根拠書類 |
| `r8-fee-notice` | 43〜55頁 | 令和8の新境界と従前境界の告示照合 |
| `r8-capability-202606` | 「別紙1-1」242行 | R8-06の平均工賃月額区分option 1〜22。8=なし（経過措置対象）、9=1万円未満、11〜22=改定対象 |
| `r8-capability-correction` | 9頁 | R8体制表242行の修正確認。Excelと一組で使用 |
| `r8-service-codes-2-xlsx` | ワークブック順38、7〜1993行 | 令和8基本報酬の数値境界・実在service-codeと1993行の独立新規指定特例。1994行以降の別減算・加算を除外 |

2026-07-10に公式URLから再取得し、次のSHA-256がADR 0020の登録値と一致することを確認した。PDFは上記物理頁を再抽出し、XLSXは上記sheet・rowの表示名と境界を再走査した。

| sourceDocumentId | 検証SHA-256 |
| --- | --- |
| `r6-employment-guidance-r6` | `58097cbd040de95fd26b65ee2177f762ed276e899214bbc692a3c58c2e3440f3` |
| `r6-calculation-note` | `958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1` |
| `r6-qa-v2` | `bd773dfe577b8a83fd99b28fd10f8cdc1b55d47114a03fd64c128777e54e6b8d` |
| `r6-qa-corr-2` | `ee37c492b6989ad874f4be0f8febddab2ff351e6fe5bb0afff04223a7e2df8df` |
| `r6-fee-notice` | `5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54` |
| `r6-capability-202404` | `fa24cd44e81cf1f1118b4a4c8a0b28bce31ee5227f13b9f8baa260ea6f223531` |
| `r6-capability-202406` | `d1edf9715b8c41660d6e4278ebd886861d0758c75109e4efc594f5d70f197c50` |
| `r6-service-codes-2-xlsx` | `4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82` |
| `r8-calculation-note` | `0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99` |
| `r8-b-reward-band-guide` | `96b002a6aecf76cbf2141fc53aee1c803e7cf78ba2dca52adbf755190e59ab5e` |
| `r8-qa-v1` | `e2b95e451418c928e6e2ec7e05360d1810079fa81fa70acfe76fe91126276e78` |
| `r8-fee-notice` | `f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c` |
| `r8-capability-202606` | `84ff0b3b34c2ef857a1bcec221b8c276c177678b403ca6e171b2a08a6d8a150b` |
| `r8-capability-correction` | `06414c8aad4c014f44fd211dac141d152f30135fb622cdd32874e1c6bccbd980` |
| `r8-service-codes-2-xlsx` | `307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049` |

## 影響

- AC3-0で、平均工賃の正式式、丸め、版付き体制option、28行の数値`PaymentBand`、R8改定状態及びフェイルクローズ条件を外部マスタへ実装できる。
- Phase 3-1で `AverageWageCalculator`、計算入力・結果・失敗、有効登録スナップショット、`R8ReformContext`、新規指定`FiledTransition`コンテキスト、`PaymentBandSelectionResult`を新設する必要がある。
- 既存の `AverageWageMetric` とテストは変更せず、Phase 2の暫定集計として互換保持する。
- 2026-06以降も、`R8ReformStatus`、有効な版付きoption又は必要な根拠が不明な事業所は請求算定できない。変更届の新規提出が不要な場合は、有効登録スナップショットで検証できる。
- 新規指定の通常初期期間は`FiledTransition`を保持して数値`PaymentBand` 8へ解決する。6月平均は自動算出せず、指定権者確認済みの届出値と数値optionだけを使う。
- `FiledTransition`終了後も`R8ReformStatus`を保持し、数値optionへ移行する。将来の公式資料が終了条件を定めたとき、既存期間を上書きせず新しい期間行と出典を追加する。
- `AverageWageBandOption`をservice-codeへ直接渡さず、数値`PaymentBand`から基本報酬範囲内の実在行だけを選ぶため、存在しない区分9 codeを生成しない。
