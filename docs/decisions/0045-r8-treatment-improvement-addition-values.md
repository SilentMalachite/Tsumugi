# ADR 0045: 福祉・介護職員等処遇改善加算の令和8年6月施行分（R8-06）実値投入

- 状態: 確定（2026-07-26）
- 関連: [ADR 0021](0021-office-capability-official-codes.md) / [ADR 0025](0025-claim-rounding-rules.md) /
  [ADR 0028](0028-r6-major-addition-values.md) / [ADR 0044](0044-r8-region-unit-price-and-burden-cap-continuity.md)

## 結論

`addition.treatment-improvement.unified.i`〜`.iv`（統一 福祉・介護職員等処遇改善加算(Ⅰ)〜(Ⅳ)）は `effectiveTo: "2026-05"` で失効する。ADR 0028 決定7が「R8-06の処遇改善は率・コード構成が変わるため本ADRのスコープ外」として明示的に繰り延べたR8実値を、本ADRで確定し投入する。

一次資料から一意に確定できたのは次の6区分である。

| 区分 | 率（1000分の） | サービスコード | 体制届選択番号 |
| --- | ---: | --- | --- |
| (Ⅰ)イ | 105 | `465120`（R6と同一コードを継続） | `2` |
| (Ⅰ)ロ（新設） | 109 | `465174`（新設） | `7` |
| (Ⅱ)イ | 103 | `465121`（R6と同一コードを継続） | `3` |
| (Ⅱ)ロ（新設） | 107 | `465175`（新設） | `8` |
| (Ⅲ) | 88 | `465122`（R6と同一コードを継続） | `4` |
| (Ⅳ) | 74 | `465123`（R6と同一コードを継続） | `5` |

**重要な発見**: `docs/open-questions.md` の未検証観測メモは「R8新コード構成（465174〜465176追加）」とだけ記していたが、これは「新コードが3件追加される」という意味であり、「(Ⅰ)〜(Ⅳ)全体が新コードに置き換わる」という意味ではなかった。一次資料を一意に確定した結果、**(Ⅰ)イ・(Ⅱ)イ・(Ⅲ)・(Ⅳ)の4区分はR6統一処遇改善と同一のサービスコードを2026-06以降も継続使用し**、(Ⅰ)ロ・(Ⅱ)ロという2つの新設区分だけが新コード（465174・465175）を持つ。465176は(Ⅰ)ロの指定障害者支援施設variant（本ADRのスコープ外）である。

処遇改善(Ⅴ)（14区分の経過措置。旧R6処遇改善(Ⅴ)の令和8年版相当）と障害者支援施設variant（465138・465140・465141・465176等、率が通常事業所と異なる）は、一次資料上は存在を確認したが、区分数・率対応が複雑で本タスクのスコープ外（ADR 0028決定8と同じ整理）として投入しない。詳細は「確定できなかった区分」節を参照。

## 背景

処遇改善加算は実務上ほぼ全事業所が算定するため、R6行が2026-05で失効したまま対応するR8行が無いと、**2026-06以降は改定対象外の事業所でも請求が成立しない**（全事業所に効く穴）。ADR 0028は令和6年度改定時点でこの4行の率・コードを確定したが、決定7で「R8-06の処遇改善は率・コード構成が変わるため本ADRのスコープ外」と明示的に繰り延べていた。本ADRはこの繰延事項を引き取り確定する。

`docs/open-questions.md` には「`current-fee-notice-html` で(Ⅰ)イ105・(Ⅰ)ロ109・(Ⅱ)イ103・(Ⅱ)ロ107・(Ⅲ)88・(Ⅳ)74（各1000分の）を観測」という**未検証の観測メモ**があったが、これは出典として使えない（`current-fee-notice-html` はcontinuity mapping用の現行統合本文であり、改正告示そのものではない）。本ADRは `r8-fee-notice`（改正告示そのもの）からの正式抽出を行い、この観測値と照合する。

## 一次資料の同一性検証（2026-07-26実施）

本タスクで使用した5文書すべてを自分で再取得し、`sources.json` の登録値とSHA-256を照合した。不一致は0件。

| documentId | sha256（先頭12桁） | 照合 | 用途 |
| --- | --- | --- | --- |
| r8-fee-notice | f4b7a05e33b5 | 一致 | 率の一次証拠（改正告示そのもの） |
| r8-service-codes-2-xlsx | 307b631ed91a | 一致 | サービスコード・公式名称の一次証拠 |
| r8-service-codes-2-pdf | 0ff507138037 | 一致 | 上記の独立2形式照合 |
| r8-calculation-note | 0c4f357f4dfd | 一致 | 端数処理（四捨五入）の一次証拠 |
| r8-capability-202606 | 84ff0b3b34c2 | 一致 | 体制届選択番号の一次証拠 |
| r8-capability-correction | 06414c8aad4c | 一致（新規使用） | 体制届選択番号の訂正版（ADR 0021が常に一組で検証するとする文書） |

`r8-capability-correction` は本タスクで新たに使用したが、`sources.json` には既に登録済み（`claim-master-r8-06` releaseの `sourceDocumentIds` にも既に含まれていた）ため、`sources.json` 自体への追記は不要だった。

## 抽出方式と2方式の一致確認結果

### 率（`r8-fee-notice`）: `pdftotext -layout` と `-raw` の2方式

`就労継続支援Ｂ型`（別表第14）の項目17「福祉・介護職員等処遇改善加算」を検索し、物理57頁（本PDFは印字頁番号を持たない。見出し「17 福祉・介護職員等処遇改善加算」自体は物理56頁）に到達した。`-layout`（左右2欄の新旧対照表として抽出）と `-raw`（改正後欄をまとめて読んでから改正前欄を読む順序で抽出）の両方式で、次の6値が完全一致した。

| 区分 | -layout 抽出 | -raw 抽出 | 指定障害者支援施設の別立て率（参考・本ADRでは未使用） |
| --- | --- | --- | --- |
| (Ⅰ)イ | 1000分の105 | 1000分の105 | 1000分の116 |
| (Ⅰ)ロ | 1000分の109 | 1000分の109 | 1000分の120 |
| (Ⅱ)イ | 1000分の103 | 1000分の103 | （施設別立てなし） |
| (Ⅱ)ロ | 1000分の107 | 1000分の107 | （施設別立てなし） |
| (Ⅲ) | 1000分の88 | 1000分の88 | 1000分の98 |
| (Ⅳ) | 1000分の74 | 1000分の74 | 1000分の81 |

同じページの「改正前」欄（=R6時点の値）は 93・91・76・62（施設: 104・86・69）であり、既存seedの `addition.treatment-improvement.unified.i`〜`.iv` の `percentage`（0.093・0.091・0.076・0.062）と完全一致した。これにより物理頁・項番の特定が正しいことを二重に確認した。

**open-questions.md の未検証観測値との照合**: (Ⅰ)イ105・(Ⅰ)ロ109・(Ⅱ)イ103・(Ⅱ)ロ107・(Ⅲ)88・(Ⅳ)74という観測値は、`r8-fee-notice` からの正式抽出と**完全に一致した**。食い違いはなかった。

### サービスコード（`r8-service-codes-2-xlsx` と `r8-service-codes-2-pdf`）: xlsx/PDFの2形式

xlsxはワークブック順38番目のシート「18就労継続支援(B・基本)」（R6と同じワークブック順）の該当行を `openpyxl` で読み、PDFは物理245頁（本PDFも印字頁番号を持たない）を `pdftotext -layout` で読んだ。両形式で次の対応が完全一致した。

| 区分 | サービスコード | 公式名称（xlsx「算定項目」欄） | xlsx行 |
| --- | --- | --- | --- |
| (Ⅰ)イ | 465120 | (1)　福祉・介護職員等処遇改善加算（Ⅰ）イ | 2261 |
| 　同・指定障害者支援施設variant（未投入） | 465138 | 指定障害者支援施設において行った場合 | 2262 |
| (Ⅰ)ロ | 465174 | (2)　福祉・介護職員等処遇改善加算（Ⅰ）ロ | 2263 |
| 　同・指定障害者支援施設variant（未投入） | 465176 | 指定障害者支援施設において行った場合 | 2264 |
| (Ⅱ)イ | 465121 | (3)　福祉・介護職員等処遇改善加算（Ⅱ）イ | 2265 |
| (Ⅱ)ロ | 465175 | (4)　福祉・介護職員等処遇改善加算（Ⅱ）ロ | 2267 |
| (Ⅲ) | 465122 | (5)　福祉・介護職員等処遇改善加算（Ⅲ） | 2269 |
| 　同・指定障害者支援施設variant（未投入） | 465140 | 指定障害者支援施設において行った場合 | 2270 |
| (Ⅳ) | 465123 | (6)　福祉・介護職員等処遇改善加算（Ⅳ） | 2271 |
| 　同・指定障害者支援施設variant（未投入） | 465141 | 指定障害者支援施設において行った場合 | 2272 |

この対応は [ADR 0021](0021-office-capability-official-codes.md) 「令和8年6月追加コード」節（`465120`; `465121`; `465122`; `465123`; `465138`; `465140`; `465141`; `465174`; `465175`; `465176`。`r8-claim-decision-xls` 5020〜5023行; `r8-service-codes-2-xlsx`基本2261〜2272行）とも独立に一致することを確認した。ADR 0021は請求サービスコード体系全体の設計時（Task 13付近）に既にこの対応を記録していたが、実際のseed投入とテスト固定は本ADRが初めて行う。

すべて算定単位「単位加算」・算定周期「1月につき」（xlsx上は先頭行にのみ表示されるグループ共通値）で、R6と同形。

### 端数処理（`r8-calculation-note`）

物理8〜9頁（印字頁番号も8・9で物理頁と一致）に「①単位数算定の際の端数処理」があり、「小数点以下の端数処理(四捨五入)を行っていく」と定める。R6の `r6-calculation-note` p.8〜9と同じ節構成・同じ内容であることを確認した（ADR 0025の `claim.rounding.units.half-up.v1` と整合）。

### 体制届選択番号（`r8-capability-202606` と `r8-capability-correction`）

`r8-capability-202606`「別紙１-１」260行（AL列。列位置38列目、R6の「38列目」形式と一致）の「福祉・介護職員等処遇改善加算対象」欄は次の選択肢を持つ。

```
１．なし　２．Ⅰ・イ　３．Ⅱ・イ　４．Ⅲ　５．Ⅳ
７．Ⅰ・ロ　８．Ⅱ・ロ
```

ADR 0021が要求するとおり `r8-capability-202606` と `r8-capability-correction` を一組で検証したところ、`r8-capability-correction` 物理9頁の訂正後表は同じ7選択肢に加えて「６．Ⅴ」を含む（＋処遇改善(Ⅴ)区分のサブフィールドが別途ある）。**base xlsx単体では選択肢「６．Ⅴ」が欠落しており、訂正版でのみ完全な選択肢集合が確認できた。** 本ADRが投入する6区分（選択番号2,3,4,5,7,8）はいずれの版でも共通に存在するため、この欠落は投入対象の判断には影響しない。選択番号6（Ⅴ）は「確定できなかった区分」として未投入とする。

選択番号とローマ数字区分の対応:

| 選択番号 | 区分 |
| --- | --- |
| 1 | なし |
| 2 | (Ⅰ)・イ |
| 3 | (Ⅱ)・イ |
| 4 | (Ⅲ) |
| 5 | (Ⅳ) |
| 6 | (Ⅴ)（未投入） |
| 7 | (Ⅰ)・ロ |
| 8 | (Ⅱ)・ロ |

field-idは `treatment-improvement`（ADR 0021の既存field-idを継続使用。option集合だけが版で変わる）。R6-06版のoption `2`＝「Ⅰ」（無分割）とR8-06版のoption `2`＝「Ⅰ・イ」は数値としては同じ`2`だが、両者は`effectiveFrom`/`effectiveTo`が重ならない別の`ClaimConditionDefinition`（キーが異なる）として登録するため、実行時の混同は起きない。

## 決定

### 決定表（seed実値。これが値の唯一の出典）

`additions.json`（`masterKind: additions`）に6行を追加する。`effectiveFrom: "2026-06"` / `effectiveTo: null`。

| key | percentage | calculationOrder |
| --- | ---: | ---: |
| `addition.treatment-improvement.r8.i-i` | 0.105 | 1 |
| `addition.treatment-improvement.r8.i-ro` | 0.109 | 2 |
| `addition.treatment-improvement.r8.ii-i` | 0.103 | 3 |
| `addition.treatment-improvement.r8.ii-ro` | 0.107 | 4 |
| `addition.treatment-improvement.r8.iii` | 0.088 | 5 |
| `addition.treatment-improvement.r8.iv` | 0.074 | 6 |

`calculationOrder`は告示の項番順（イ〜ヘ）を採番したもので、6区分は「いずれか1つだけを算定する」相互排他の家族（ADR 0028と同じ契約）のため、実際の算定では常にどれか1行だけがOfficeCapability一致で選ばれる。

`service-codes.json`（`masterKind: service-codes`）に対応する6行を追加する。

| key | serviceCode | officialLabel | 体制届キー |
| --- | --- | --- | --- |
| `b-addition.r8-06.treatment-improvement.i-i` | 465120 | 福祉・介護職員等処遇改善加算(Ⅰ)イ | `capability-treatment-improvement-r8-i-i` |
| `b-addition.r8-06.treatment-improvement.i-ro` | 465174 | 福祉・介護職員等処遇改善加算(Ⅰ)ロ | `capability-treatment-improvement-r8-i-ro` |
| `b-addition.r8-06.treatment-improvement.ii-i` | 465121 | 福祉・介護職員等処遇改善加算(Ⅱ)イ | `capability-treatment-improvement-r8-ii-i` |
| `b-addition.r8-06.treatment-improvement.ii-ro` | 465175 | 福祉・介護職員等処遇改善加算(Ⅱ)ロ | `capability-treatment-improvement-r8-ii-ro` |
| `b-addition.r8-06.treatment-improvement.iii` | 465122 | 福祉・介護職員等処遇改善加算(Ⅲ) | `capability-treatment-improvement-r8-iii` |
| `b-addition.r8-06.treatment-improvement.iv` | 465123 | 福祉・介護職員等処遇改善加算(Ⅳ) | `capability-treatment-improvement-r8-iv` |

`service-codes.json` の `conditionDefinitions` に6件の `office-capability` 条件定義を追加する（R6の `capability-treatment-improvement-i` と同形。`effectiveFrom: "2026-06"` / `effectiveTo: null`）。

| key | value | 出典 |
| --- | --- | --- |
| `capability-treatment-improvement-r8-i-i` | `mhlw.b46.capability.treatment-improvement.2` | r8-capability-202606別紙１-１260行 ＋ r8-capability-correction物理9頁 |
| `capability-treatment-improvement-r8-i-ro` | `mhlw.b46.capability.treatment-improvement.7` | 同上 |
| `capability-treatment-improvement-r8-ii-i` | `mhlw.b46.capability.treatment-improvement.3` | 同上 |
| `capability-treatment-improvement-r8-ii-ro` | `mhlw.b46.capability.treatment-improvement.8` | 同上 |
| `capability-treatment-improvement-r8-iii` | `mhlw.b46.capability.treatment-improvement.4` | 同上 |
| `capability-treatment-improvement-r8-iv` | `mhlw.b46.capability.treatment-improvement.5` | 同上 |

命名は「区分→率→サービスコード→体制届キー」の対応を`<区分suffix>`で貫通させ（`addition.treatment-improvement.r8.<suffix>` / `b-addition.r8-06.treatment-improvement.<suffix>` / `capability-treatment-improvement-r8-<suffix>`）、機械的に追跡できるようにした。R6の `capability-treatment-improvement-i` 等はキーに世代を含まないが、R8では同名衝突を避けるため `-r8-` を挿入した。

### ADR 0025の割合加算契約との整合

- `percentageBaseScope: "monthly-target-unit-sum"` — R6と同じ月次対象単位合計。
- `targetSelector: "target.b46.items-1-to-16-4.v1"` — 告示本文が6区分すべてで「１から16の４まで により算定した単位数」と定めており、R6と対象範囲が変わっていないことを確認した（項目1〜16の4の間に新規項目の挿入は無い）。
- `calculationOrder` — 相互排他家族内の順序。1〜6を告示の項番順（イ〜ヘ）で採番。
- `roundingRuleId: "claim.rounding.units.half-up.v1"` — `r8-calculation-note` 物理8〜9頁の四捨五入規則と整合。

### 手計算検証ケース（golden case期待値）

端数規則はADR 0025に従う: 月次給付単位数は基本報酬＋加算の整数合算（%行は`claim.rounding.units.half-up.v1`で丸めてから加算）、総費用額＝給付単位数×地域単価の円未満切捨て、1割相当額＝総費用額×10/100の円未満切捨て、給付費＝総費用額−決定利用者負担額。改定対象外事業所（`R8ReformStatus.ReformExempt`）× 2026年6月のケースで、法31条特例不適用・上限は1割相当額以上・上限額管理対象外を前提とする（ADR 0027決定4・ADR 0028決定6と同一前提）。

#### ケース: cap-20-or-less × band-20000-25000 × staff-7.5-1 × 22日 × region-grade-2（reform-exempt、2026-06）

- 基本: 462049（就継ＢⅡ１５、ADR 0027決定6により2026-06以降も継続。r8-reform-status条件を一切持たないため`R8ReformStatus.ReformExempt`でも無条件に一致する）637単位×22日 = 14,014単位
- 福祉・介護職員等処遇改善加算(Ⅰ)イ: 465120（本ADR決定表、体制届選択番号2＝`capability-treatment-improvement-r8-i-i`）。月次対象単位合計（`target.b46.items-1-to-16-4.v1`）は本ケースの基本報酬行のみが対象で14,014単位。14,014 × 105/1000 = 1,471.47 → `claim.rounding.units.half-up.v1` → **1,471単位**
- 月次給付単位数: 14,014 + 1,471 = **15,485単位**
- 総費用額: 15,485 × 10.91円（region-grade-2。ADR 0044により2026-06も改定なしで継続）= 168,941.35円 → 円未満切捨て → **168,941円**
- 1割相当額: 168,941 × 10/100 = 16,894.1円 → 円未満切捨て → 16,894円
- 給付費: 168,941 − 16,894 = **152,047円**

`tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`の`Matches_adr_0045_worked_example_reform_exempt_office_in_june_2026`が上記期待値を固定する（マスタ行はDomainテストの依存方向規律によりテストファイル内に再掲。`R8Masters()`ヘルパ）。

### 確定できなかった区分

| 区分 | 未投入の理由 | 現在の挙動（Fix Round 1 I-4） |
| --- | --- | --- |
| 処遇改善(Ⅴ)（14区分。選択番号6・サブ区分⑴〜⒁） | 経過措置（旧制度からの移行者向け）で、率が14通りに枝分かれし、`r8-capability-correction`でのみ選択肢自体の存在が確認できる（base xlsxには欠落）など、対応表の一意性確認に本タスクのスコープを超える追加調査を要する。ADR 0028決定8と同じ整理でスコープ外とする。 | 選択番号6（Ⅴ）に対応するマスタ行が存在しないため、体制届で option 6 を選択した事業所は**警告なしに処遇改善加算が0円になる**（fail-closeではなく無音の未算定）。 |
| 障害者支援施設variant（465138・465140・465141・465176） | 通常事業所と異なる率（施設向け別建て。例: (Ⅰ)イは1000分の116、通常事業所の105とは異なる）を持ち、`OfficeClaimProfile`側に「指定障害者支援施設か」の構造化入力が必要（ADR 0021が既に要求）。本タスクは通常の就労継続支援B型事業所向け6区分に限定し、施設variantは別スライスとする。 | 「指定障害者支援施設か」を区別する構造化入力自体が存在しないため、指定障害者支援施設が本ADRの通常事業所向け条件（例: option 2＝`capability-treatment-improvement-r8-i-i`）に一致してしまい、**465120@0.105（本来は465138@0.116）で請求が成立し得る**。これは誤った値での**過少請求**であり、エラーは出ない。 |

いずれも部分完了として扱い、投入した6区分だけで「2026-06以降は改定対象外の事業所でも請求が成立しない」という本タスクの主目的（全事業所に効く穴を塞ぐこと）は達成される。一方、上表の「現在の挙動」列が示す2つの経路（無音の未算定・無音の過少請求）はいずれもR6期から続く構造的な既存ギャップであり、本タスクが新設したものではないが、R8では施設との率差が0.105対0.116（R6は0.093対0.104）へ広がった。「宣言された体制届optionに対応する有効なマスタ行が当月に存在しない場合にreadiness警告を出す」という恒久対応と、処遇改善(Ⅴ)・施設variant自体の投入は、いずれも`docs/open-questions.md`へ別項目として起票した（実装はいずれも本タスクの範囲外）。

### ADR 0028決定7からの引き取り

ADR 0028決定7は「R8-06の処遇改善は率・コード構成が変わるため本ADRのスコープ外」と明示し、決定7の2で「`claim-master-r8-06` の処遇改善をseedする前に別ADRで確定する」としていた。本ADR 0045がその別ADRであり、決定7を引き取って確定する。

## 選択肢

### A: 投入せず現状維持（不採用）

R6行のみ2026-05で失効させたまま、R8行を投入しない。**2026-06以降、処遇改善加算を算定するほぼ全事業所で請求が成立しなくなる**（全事業所に効く穴を放置する）。不採用。

### B: 6区分すべてに加え、処遇改善(Ⅴ)・障害者支援施設variantも含めて全区分投入する（検討したが不採用）

R8のR8-only additions source inventory（153 targets）を全て一括投入する。**検討した上で不採用とした理由**: 処遇改善(Ⅴ)は率が14通りに枝分かれし対応表の一意性確認に追加調査を要し、障害者支援施設variantは`OfficeClaimProfile`側の構造化入力（施設か否か）が未実装である。一次資料から一意に確定できない区分を推測で埋めることは Global Constraints で禁止されている。不採用。

### C: 一意に確定できた6区分（(Ⅰ)イ・(Ⅰ)ロ・(Ⅱ)イ・(Ⅱ)ロ・(Ⅲ)・(Ⅳ)）だけを投入する（採用）

処遇改善加算は実務上ほぼ全事業所が算定する主要区分であり、この6区分の投入だけで「全事業所に効く穴」を塞げる。処遇改善(Ⅴ)と施設variantは対象事業所が限定的（経過措置対象・施設併設型のみ）であり、確定できるまで待っても「穴」の深刻度は相対的に小さい。部分完了として6区分を投入し、残りは`docs/open-questions.md`へ起票する。

## 影響

- `claim-master-r8-06`（2026年6月以降）の福祉・介護職員等処遇改善加算(Ⅰ)イ・(Ⅰ)ロ・(Ⅱ)イ・(Ⅱ)ロ・(Ⅲ)・(Ⅳ)の6区分が解決できるようになる。
- `465120`・`465121`・`465122`・`465123`はR6・R8の両方で有効期間が重ならない別の`ClaimConditionDefinition`・`UnitAdjustmentMasterRow`・`ServiceCodeMasterRow`から参照される（同一サービスコード値を異なる世代のマスタ行が指す）。これはADR 0021が既に示していた設計（同一コードの版またぎ再利用）であり、本ADRが初めて実データで固定する。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimAdditionSeedScopeTests.cs`の`Unified_treatment_improvement_rows_apply_only_between_2024_06_and_2026_05`は、2026-06の`465120`等除外主張を削除した（コード再利用が判明したため、削除しないと実データに対して恒久的に失敗する）。新設した`R8_treatment_improvement_rows_apply_only_from_2026_06`が、正しい継続/新設の組合せ（新設は465174・465175だけ）を固定する。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs`の`Treatment_improvement_additions_lapse_at_june_2026_until_their_r8_values_land`を`Treatment_improvement_additions_switch_generations_at_june_2026`へ置き換えた。「R6行が消える」ことと「R8行が現れる」ことの両方を機械的に固定する。
- 処遇改善(Ⅴ)・障害者支援施設variantの投入は`docs/open-questions.md`へ**未チェック項目として**起票し、別スライスとして扱う（Fix Round 1 I-3。当初は既存項目を部分クローズしただけで新規`[ ]`項目が無く、未解決作業が一覧から消えていた）。
- 「宣言された体制届optionに対応する有効なマスタ行が当月に存在しない場合にreadiness警告を出す」という恒久対応も、別の未チェック項目として`docs/open-questions.md`へ起票した（Fix Round 1 I-4。実装は本タスクの範囲外）。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs`へ`R8_treatment_improvement_percentages_match_adr_0045`を追加し、本ADRが投入した6区分の率をproduction seedから解決した実データでpinした（Fix Round 1 I-2。追加前は`percentage`を1桁変えてもClaimMaster全テストが緑のままだった）。

## Fix Round 1（コーディネーターレビュー対応・2026-07-26）

opusレビューでImportant 4件・Minor 2件の指摘を受けた。**コード再利用（465120等の継続使用）の判断自体とページ特定の統制は正当と評価された**（ADR 0021の既存記録との独立一致、「改正前」欄と既存seedの一致という2点が確証バイアス対策として機能した）。指摘は主にその周辺の検証強度の不足だった。

- **I-1**: `R8_treatment_improvement_rows_apply_only_from_2026_06`が同一ファイル内のリテラル配列同士（`R8TreatmentImprovementCodes.Except(UnifiedTreatmentImprovementCodes)`）を比較しており、seedを一切読まない恒真式になっていた。production seedから解決した実データ（`AdditionRows`）どうしの比較へ差し替え、2026-06のコード集合を期待6コードとの完全一致（上限も固定）で検証するようにした。
- **I-2**: 投入した6区分の率に機械検証が1つも無かった（`percentage`を1桁変えてもClaimMaster全テストが緑のまま）。`ClaimMasterR8BoundaryTests.R8_treatment_improvement_percentages_match_adr_0045`を新設し、production seedから解決した率を本ADRの決定表とpinした。
- **I-3**: `docs/open-questions.md`の既存項目を部分クローズしただけで、処遇改善(Ⅴ)・施設variant用の新規`[ ]`項目が無かった。2件の未チェック項目を新規に起票した（対象・確定に必要な資料・現在の挙動・解除条件を明記）。
- **I-4**: 「確定できなかった区分」表が未投入の理由だけを書き、その場合の実際の実行時挙動（無音の未算定・無音の過少請求）を書いていなかった。「現在の挙動」列を追加した。
- **M-5**: `ClaimMasterR8BoundaryTests`のサービスコード行対応確認ループが`additionKey`を参照せず、実質的に「r8-06のservice-code行が1本でもあれば通る」チェックを6回繰り返すだけだった。suffixを切り出して個別のキー一致を確認する形へ修正した。
- **M-6**: `r8-calculation-note`のlocatorだけ物理／印字頁の区別が無かった。他のR8 locatorと同じ形式（`物理8〜9頁（印字頁番号も8・9で物理頁と一致）`）へ揃えた。

## 再検証手順

1. `sources.json`の該当5件（`r8-fee-notice`・`r8-service-codes-2-xlsx`・`r8-service-codes-2-pdf`・`r8-calculation-note`・`r8-capability-202606`。加えて`r8-capability-correction`）のURLを取得し、`shasum -a 256`が登録値と一致することを確認する。
2. `r8-fee-notice`の物理57頁を`pdftotext -layout`と`-raw`の両方式で抽出し、6区分の率が一致することを確認する。
3. `r8-service-codes-2-xlsx`のワークブック順38番目のシートと`r8-service-codes-2-pdf`の物理245頁で、サービスコード・公式名称が一致することを確認する。
4. 処遇改善(Ⅴ)・障害者支援施設variantを投入する場合は、`r8-capability-correction`の選択肢⑴〜⒁の率対応と、`OfficeClaimProfile`への施設フラグ追加を新たなADRで確定してから着手する。
