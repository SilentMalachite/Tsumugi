# ADR 0021: 加算コード集合と `OfficeCapability` の正式キー移行

**ステータス**: 確定（2026-07-10）

## 結論

就労継続支援B型の請求サービスコードと決定サービスコードを適用年月版マスタから解決し、`OfficeCapability.Flags` は請求コードとは別の公式体制選択one-hotキーとして保持する。暫定キー `mealProvision` / `transportSupport` は自動変換せず、AC3-8の入力UIで届出内容を明示的に再登録する。未知・旧・版外キーまたは必須入力不足はフェイルクローズする。

## 背景

ADR 0006はPhase 1で体制フラグの文字列マップだけを確定し、正式キーと単位数をPhase 3まで保留した。その結果、現行UIは食事提供と送迎を単純なboolで保持するが、公式の体制等状況一覧表では送迎体制I/II、明示的な「なし」、処遇改善区分等を選択番号で区別し、請求サービスコード表では体制届に加えて利用者・日・月の実績から個別の請求コードを選ぶ。

また、令和6年4月、令和6年6月、令和8年6月で体制項目、基本報酬区分、処遇改善コードが変わる。旧boolを名称だけで請求コードへ変換すると、届出区分、実施実績、適用月を推測することになるため、ADR 0020の版束と公式SourceDocumentIdに結び付いた閉じたコード集合と移行規律が必要である。

## 選択肢

### A: 公式体制項目と選択番号のone-hotキー（採用）

- 体制届、請求コード、決定コード、実績入力を分離できる。
- 「なし」を明示し、未知・重複・欠落を検出できる。
- 版ごとの許可field/optionを外部マスタで検証する必要がある。

### B: 請求サービスコードを `OfficeCapability.Flags` のキーにする

- 算定結果をそのままキーにできるが、事業所体制と利用者・日・月の実績を混同し、送迎の片道実績や欠席相談等を表現できないため却下する。

### C: 既存boolを名称の近い正式キーへ自動変換する

- 移行操作は少ないが、`mealProvision` は公式届出と当日提供を証明せず、`transportSupport` はI/IIを区別できない。監査可能な根拠を失うため却下する。

## 決定

就労継続支援B型（サービス種類コード `46`）の請求コードは、ADR 0020の版束と公式資料に従い、次の3期間で解決する。

| masterVersion | 適用開始月 | 廃止月 | 体制資料 | サービスコード正本 | 請求・決定対応正本 |
| --- | --- | --- | --- | --- | --- |
| `claim-master-r6-04` | 2024-04 | 2024-05 | `r6-capability-202404` | `r6-service-codes-2-xlsx` | `r6-claim-decision-202404-xls` |
| `claim-master-r6-06` | 2024-06 | 2026-05 | `r6-capability-202406` | `r6-service-codes-2-xlsx` | `r6-claim-decision-202406-xls` |
| `claim-master-r8-06` | 2026-06 | null | `r8-capability-202606` と `r8-capability-correction` | `r8-service-codes-2-xlsx` | `r8-claim-decision-xls` |

月はサービス提供月で判定する。廃止月がnullのコードは終了月未定であり、将来の改定月を推測しない。

請求サービスコードは公式表の全角英数字をASCII大文字へ字形正規化した6文字で保持する。この正規化は `４６Ｚ０３４` を `46Z034` とするような一対一の字形変換だけであり、範囲の穴埋め、近似コードへの置換、意味上の変換を行わない。決定サービスコードもASCII数字6文字で保持する。

通常のB型請求サービスコードの決定サービスコードは `461000` である。公式対応表が個別に示す在宅時生活支援サービス加算は `460932`、社会生活支援特別加算は `460929` を使用する。対応表でB型グループの決定コード欄がセル結合相当の空欄になっている行は、同じB型グループの `461000` を展開して外部マスタへ記録する。グループ外へ空欄を継承しない。

`OfficeCapability.Flags` は請求サービスコードや決定サービスコードを保持しない。正式キーは公式の体制等状況一覧表にある「項目」と「選択番号」を一対一に保持するone-hotキーとし、形式を次に固定する。

```text
mhlw.b46.capability.<field-id>.<official-option-code>
```

- `<field-id>` は後掲の閉じた一覧にあるASCII小文字kebab-caseだけを許可する。
- `<official-option-code>` は体制表の選択番号をASCII十進表記にした値である。ローマ数字の表示名ではなく、公式の選択番号を使う。
- 正式キーの値は `true` だけを許可する。`false`、同一項目の複数選択、必要項目の無選択は入力欠落として拒否する。
- `1.なし`、`1.非該当` も明示的な選択であり、キーの不在を「なし」と解釈しない。
- 適用版に存在しないfieldまたはoption、廃止後のoption、未知キー、暫定キーを1件でも検出した場合は算定不能とする。

### 出典locatorの表記

本ADRの「行」は先頭行を1とするExcel行番号、「頁」はPDFの物理頁である。表中の略記を次に固定する。

| sourceDocumentId | シート・頁 |
| --- | --- |
| `r6-capability-202404` | 「介護給付費等　体制等状況一覧」 |
| `r6-capability-202406` | 「介護給付費等　体制等状況一覧」 |
| `r8-capability-202606` | 「別紙１-１」 |
| `r8-capability-correction` | PDF物理頁。B型の修正後表は9頁、定員規則は12頁、新規指定時の区分規則は13頁 |
| `r6-service-codes-2-xlsx` | 「18就労継続支援(B・基本)」「18就労継続支援(B・定超)」「18就労継続支援(B・生活支援員等欠員)」「18就労継続支援(B・管理責任者欠員)」。表中の「基本」は第1シート名の略記 |
| `r8-service-codes-2-xlsx` | 同じ4シート名。表中の「基本」は第1シート名の略記 |
| `r6-claim-decision-202404-xls` | 「請求コードと支給決定コード（R6_4対応版）」 |
| `r6-claim-decision-202406-xls` | 「請求コードと支給決定コード（R6_6対応版）」 |
| `r8-claim-decision-xls` | 「請求コードと支給決定コード（R8.6対応版）」 |
| `r6-calculation-note` | PDF物理頁 |
| `r8-calculation-note` | PDF物理頁 |

### 入力の境界

体制届の選択、請求コード、実績入力を混同しない。次の所有境界を採用する。

| 入力区分 | 保持する値 | 保持先 |
| --- | --- | --- |
| 基本報酬選択 | 届出済み報酬体系（I〜VIまたは基準該当）、利用定員、定員規模、多機能型等定員区分、人員配置区分、平均工賃月額区分、実額、経過措置コンテキスト | `OfficeClaimProfile` の構造化フィールド。`Flags`へ入れない |
| 数値の事業所入力 | 前年度の就労定着者数、定員数、指定日等 | `OfficeClaimProfile` の数値・日付フィールド。`Flags`へ入れない |
| 体制届の選択 | 定員超過、職員欠如、食事提供体制、送迎体制等の公式選択番号 | `OfficeCapability.Flags` の正式one-hotキー |
| 利用者・日・月の実績 | 食事提供、送迎の片道ごとの実施、欠席相談、医療連携、上限額管理、各加算の実施記録 | `DailyRecord`、`Certificate`または請求専用入力。事業所体制から実施を推測しない |
| 算定結果 | 請求サービスコード、決定サービスコード、単位数 | 適用年月版マスタから解決した `ClaimDetail`。`Flags`へ入れない |

基本報酬選択に必要な公式入力は、R6-04では `r6-capability-202404` の「介護給付費等　体制等状況一覧」5行目と273行目、R6-06では `r6-capability-202406` の同シート5行目と240行目、R8-06では `r8-capability-202606` の「別紙１-１」5行目と242行目、および `r8-capability-correction` 9頁で確認する。人員配置の公式選択番号は `1=II型(7.5:1)`、`2=III型(10:1)`、`3=I型(6:1)` である。番号とローマ数字の順序を並べ替えない。

前年度の就労定着者数は `Flags` では表現できない。R6-04は `r6-capability-202404` 同シート285行、R6-06は `r6-capability-202406` 同シート252行、R8-06は `r8-capability-202606` 「別紙１-１」254行に数値欄があるため、AC3-8で数値入力として追加する。多機能型の定員は、R6-04の同シート396行、R6-06の同シート352行、R8-06の「別紙１-１」353行と `r8-capability-correction` 12頁に従い、対象加算ではB型単独の利用定員を入力する。

### 基本報酬コード集合

#### 令和6から継続するコード

次のコードは2024-04から有効で、R8対応表にも残るため廃止月はnullとする。必要入力は、サービス提供月、届出済み報酬体系、利用定員・定員規模、人員配置区分、平均工賃月額区分、地方公共団体設置の別、計画未作成・短時間利用等の該当条件である。詳細条件は `r6-calculation-note` 286〜296頁および `r8-calculation-note` 287〜299頁から適用版を選ぶ。

| 表示名 | 請求サービスコード | 決定サービスコード | 適用開始月 | 廃止月 | コード根拠 |
| --- | --- | --- | --- | --- | --- |
| 就労継続支援B型サービス費（I） | `462980-463219`; `46H001-46H240`; `46H361-46H600`; `46H721-46H960`; `46L082-46L161`; `46L202-46L281` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`「請求コードと支給決定コード（R6_4対応版）」4938〜4943行; `r6-claim-decision-202406-xls`同R6_6版4853〜4858行; `r8-claim-decision-xls`同R8.6版4874〜4879行 |
| 就労継続支援B型サービス費（II） | `462001-462414`; `462853-462882`; `46B001-46B414`; `46B841-46B870`; `46C001-46C414`; `46C853-46C882`; `46D001-46D414`; `46D853-46D882`; `46E001-46E412`; `46E853-46E862`; `46F001-46F412`; `46F853-46F862` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`同シート4944〜4955行; `r6-claim-decision-202406-xls`同シート4859〜4870行; `r8-claim-decision-xls`同シート4899〜4910行 |
| 就労継続支援B型サービス費（III） | `462421-462834`; `462883-462912`; `46B421-46B834`; `46B871-46B900`; `46C421-46C834`; `46C883-46C912`; `46D421-46D834`; `46D883-46D912`; `46E421-46E832`; `46E863-46E872`; `46F421-46F832`; `46F863-46F872` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`同シート4956〜4967行; `r6-claim-decision-202406-xls`同シート4871〜4882行; `r8-claim-decision-xls`同シート4932〜4943行 |
| 就労継続支援B型サービス費（IV） | `463220-463279`; `46H241-46H300`; `46H601-46H660`; `46H961-46H999`; `46L001-46L021`; `46L162-46L181`; `46L282-46L301` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`同シート4968〜4974行; `r6-claim-decision-202406-xls`同シート4883〜4889行; `r8-claim-decision-xls`同シート4961〜4967行 |
| 就労継続支援B型サービス費（V） | `462913-462942`; `463280-463309`; `46B901-46B930`; `46C913-46C942`; `46D913-46D942`; `46E873-46E882`; `46F873-46F882`; `46H301-46H330`; `46H661-46H690`; `46L022-46L051`; `46L182-46L191`; `46L302-46L311` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`同シート4975〜4986行; `r6-claim-decision-202406-xls`同シート4890〜4901行; `r8-claim-decision-xls`同シート4968〜4979行 |
| 就労継続支援B型サービス費（VI） | `462943-462972`; `463310-463339`; `46B931-46B960`; `46C943-46C972`; `46D943-46D972`; `46E883-46E892`; `46F883-46F892`; `46H331-46H360`; `46H691-46H720`; `46L052-46L081`; `46L192-46L201`; `46L312-46L321` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`同シート4987〜4998行; `r6-claim-decision-202406-xls`同シート4902〜4913行; `r8-claim-decision-xls`同シート4980〜4991行 |
| 基準該当就労継続支援B型サービス費 | `462841-462846`; `46C841-46C846`; `46D841-46D846`; `46E841`; `46E844`; `46F841`; `46F844` | `461000` | 2024-04 | null | `r6-claim-decision-202404-xls`同シート4999〜5003行; `r6-claim-decision-202406-xls`同シート4914〜4918行; `r8-claim-decision-xls`同シート4992〜4996行 |

単位数と複合条件は `r6-service-codes-2-xlsx` のワークブック順38〜41（「18就労継続支援(B・基本)」7〜912行、「同(B・定超)」7〜906行、「同(B・生活支援員等欠員)」7〜1820行、「同(B・管理責任者欠員)」7〜612行）および `r8-service-codes-2-xlsx` の同38〜41（順に7〜1993行、7〜1986行、7〜3978行、7〜1330行）を正本とする。範囲内の未掲載コードを生成しない。

基本報酬へ組み込まれる減算・補正は独立した請求サービスコードを持たず、上表の基本報酬コード集合から条件に一致する実在行を選ぶ。対象と入力、根拠を次に固定する。

| 表示名 | 請求サービスコード | 決定サービスコード | 適用開始月 | 廃止月 | 必要入力・根拠 |
| --- | --- | --- | --- | --- | --- |
| 定員超過利用減算 | 上表の基本報酬コード集合のうち定員超過シートに実在するcode | `461000` | 2024-04 | null | `capacity-exceeded`、定員数・利用者数。`r6-service-codes-2-xlsx`「18就労継続支援(B・定超)」7〜906行、`r8-service-codes-2-xlsx`同シート7〜1986行 |
| 生活支援員等欠員減算 | 上表の基本報酬コード集合のうち生活支援員等欠員シートに実在するcode | `461000` | 2024-04 | null | `staff-shortage`、欠員期間・人員配置。`r6-service-codes-2-xlsx`「18就労継続支援(B・生活支援員等欠員)」7〜1820行、`r8-service-codes-2-xlsx`同シート7〜3978行 |
| サービス管理責任者欠員減算 | 上表の基本報酬コード集合のうち管理責任者欠員シートに実在するcode | `461000` | 2024-04 | null | `service-manager-shortage`、欠員継続月数。`r6-service-codes-2-xlsx`「18就労継続支援(B・管理責任者欠員)」7〜612行、`r8-service-codes-2-xlsx`同シート7〜1330行 |
| 就労継続支援B型計画未作成減算 | 上表の各基本報酬シートに実在する「未計画1・2」code | `461000` | 2024-04 | null | 計画作成状態、減算継続月数。R6各B型シート7行以降、R8各B型シート7行以降の「未計画」行 |
| 短時間利用減算 | 上表の基本報酬コード集合のうち報酬体系IV/V/VIの「短時間」条件を持つ実在code | `461000` | 2024-04 | null | 届出済み報酬体系、算定対象期間、対象利用者ID、利用者ごとの利用時間、分母となる事業所利用者全体、割合算定から除外する利用者IDと理由。`r6-service-codes-2-xlsx`「18就労継続支援(B・基本)」733〜906行、`r8-service-codes-2-xlsx`同シート1813〜1986行; `r6-calculation-note` 292頁; `r8-calculation-note` 293頁 |
| 地方公共団体設置の補正 | 上表の各基本報酬シートに実在する「地公体」code | `461000` | 2024-04 | null | `designated-management.2`。R6/R8各B型シート7行以降の「地公体」行 |

ここで「実在するcode」としたものはワイルドカードではない。適用版Excelの各行に記録された個別codeを外部マスタへ転記し、上表の範囲から組合せコードを生成しない。

短時間利用減算の割合は、算定対象期間の事業所利用者全体から、公式に割合算定から除く利用者を除いた集合を分母とし、その集合のうち利用時間が4時間未満の利用者を分子とする。利用時間には送迎のみを実施する時間を含めない。除外理由は `long-transport`（送迎に長時間を要する）、`planned-time-extension-support`（個別支援計画に一般就労等へ向けた利用時間延長の支援が位置付けられ、実際に実施した）、`unavoidable-reason`（やむを得ない理由）の閉集合とし、理由の根拠記録を必須にする。除外者を分母だけまたは分子だけから除く、送迎時間を利用時間に足す、除外理由を自由なboolで推測する処理は作らない。割合が50%以上の場合、各種加算前の所定単位数の70%となる実在codeを選ぶ。

#### 令和8年6月追加コード

次のコードだけを2026-06から追加する。既存コードを置換せず、R8の届出区分・平均工賃月額区分・経過措置コンテキストで選択する。

| 表示名 | 追加される請求サービスコード | 決定サービスコード | 適用開始月 | 廃止月 | コード根拠 |
| --- | --- | --- | --- | --- | --- |
| 就労継続支援B型サービス費（I）R8区分 | `463340-463699`; `46AA01-46AA99`; `46AB01-46AB99`; `46AC01-46AC99`; `46AD01-46AD63`; `46AL91-46AL99`; `46AM01-46AM99`; `46AN01-46AN99`; `46AP01-46AP99`; `46AR01-46AR54`; `46AY82-46AY99`; `46AZ01-46AZ99`; `46BA01-46BA99`; `46BB01-46BB99`; `46BC01-46BC45`; `46BK73-46BK99`; `46BL01-46BL93`; `46BP37-46BP99`; `46BR01-46BR57` | `461000` | 2026-06 | null | `r8-claim-decision-xls`「請求コードと支給決定コード（R8.6対応版）」4880〜4898行; `r8-service-codes-2-xlsx`「18就労継続支援(B・基本)」7〜1993行 |
| 就労継続支援B型サービス費（II）R8区分 | `463700-464059`; `46AD64-46AD99`; `46AE01-46AE99`; `46AF01-46AF99`; `46AG01-46AG99`; `46AH01-46AH27`; `46AR55-46AR99`; `46AS01-46AS99`; `46AT01-46AT99`; `46AU01-46AU99`; `46AV01-46AV18`; `46BC46-46BC99`; `46BD01-46BD99`; `46BE01-46BE99`; `46BF01-46BF99`; `46BG01-46BG09`; `46BL94-46BL99`; `46BM01-46BM99`; `46BN01-46BN15`; `46BR58-46BR99`; `46BS01-46BS78` | `461000` | 2026-06 | null | `r8-claim-decision-xls`同シート4911〜4931行; `r8-service-codes-2-xlsx`「18就労継続支援(B・基本)」7〜1993行 |
| 就労継続支援B型サービス費（III）R8区分 | `464060-464419`; `46AH28-46AH99`; `46AJ01-46AJ99`; `46AK01-46AK99`; `46AL01-46AL90`; `46AV19-46AV99`; `46AW01-46AW99`; `46AX01-46AX99`; `46AY01-46AY81`; `46BG10-46BG99`; `46BH01-46BH99`; `46BJ01-46BJ99`; `46BK01-46BK72`; `46BN16-46BN99`; `46BP01-46BP36`; `46BS79-46BS99`; `46BT01-46BT99` | `461000` | 2026-06 | null | `r8-claim-decision-xls`同シート4944〜4960行; `r8-service-codes-2-xlsx`「18就労継続支援(B・基本)」7〜1993行 |
| 令和8年6月1日以降新規指定事業所特例分 | `46Z006` | `461000` | 2026-06 | null | `r8-claim-decision-xls`同シート4997行; `r8-service-codes-2-xlsx`「18就労継続支援(B・基本)」1993行; `r8-calculation-note` 293〜295頁 |

`46Z006` は、基準該当を除き、2026-06-01以降に指定を受けて新たに事業を開始した事業所について、各種加算前の所定単位数の1000分の984を算定する月次条件である。コード選択の構造化入力を、指定日、新規事業開始日、基準該当か、主たる事業所の所在地が平成21年厚生労働省告示第176号の地域か、都道府県知事による地域上の特別必要性の認定とその根拠（サービス不足地域への公募設置または新規開設時の自治体による経済的支援）、法人の合併・分割・事業譲渡等に伴う指定か、その前後で実質的に継続運営されるとの都道府県知事認定、対象サービス月に視覚・聴覚言語障害者支援体制加算または高次脳機能障害者支援体制加算を1日でも算定したか、利用者ごとに同月の医療連携体制加算を1日でも算定したか、とする。

告示第176号地域、地域上の特別必要性認定、または法人再編前後の実質的継続認定に該当する事業所には `46Z006` を適用しない。視覚・聴覚言語障害者支援体制加算または高次脳機能障害者支援体制加算を当月1日でも算定した事業所は当月全体を所定単位数とし、医療連携体制加算を当月1日でも算定した利用者はその利用者だけを当月の所定単位数とする。事業所単位の除外と利用者単位の除外を混同しない。

### 加算・減算コード集合

#### 令和6から継続するコード

次の26コードグループは2024-04から有効で、R8資料にも同じコードが掲載されるため廃止月はnullとする。必要入力欄はコードを選ぶための最小入力であり、体制選択だけから実施実績を作らない。

| 表示名 | 請求サービスコード | 決定 | 必要入力 | 公式根拠（対応表行 / サービスコード行） |
| --- | --- | --- | --- | --- |
| 身体拘束廃止未実施減算 | `46Z034`; `46Z037` | `461000` | `physical-restraint-noncompliance`の選択、障害者支援施設か否か、サービス月 | `r6-claim-decision-202404-xls` 5004行 / `r6-service-codes-2-xlsx`基本913〜915行; `r6-claim-decision-202406-xls` 4919行; `r8-claim-decision-xls` 4998行 / `r8-service-codes-2-xlsx`基本1994〜1995行 |
| 虐待防止措置未実施減算 | `46Z051` | `461000` | `abuse-prevention-noncompliance`の選択、サービス月 | `r6-claim-decision-202404-xls` 5005行 / `r6-service-codes-2-xlsx`基本916行; `r6-claim-decision-202406-xls` 4920行; `r8-claim-decision-xls` 4999行 / `r8-service-codes-2-xlsx`基本1996行 |
| 業務継続計画未策定減算 | `46Z064`; `46Z067` | `461000` | `business-continuity-plan-missing`の選択、障害者支援施設か否か、サービス月 | `r6-claim-decision-202404-xls` 5006行 / `r6-service-codes-2-xlsx`基本917〜918行; `r6-claim-decision-202406-xls` 4921行; `r8-claim-decision-xls` 5000行 / `r8-service-codes-2-xlsx`基本1997〜1998行 |
| 情報公表未報告減算 | `46Z084`; `46Z087` | `461000` | `information-disclosure-missing`の選択、障害者支援施設か否か、サービス月 | `r6-claim-decision-202404-xls` 5007行 / `r6-service-codes-2-xlsx`基本919〜920行; `r6-claim-decision-202406-xls` 4922行; `r8-claim-decision-xls` 5001行 / `r8-service-codes-2-xlsx`基本1999〜2000行 |
| 利用者負担上限額管理加算 | `465010` | `461000` | 上限額管理の実施結果、対象月、当月算定回数 | `r6-claim-decision-202404-xls` 5008行 / `r6-service-codes-2-xlsx`基本1046行; `r6-claim-decision-202406-xls` 4923行; `r8-claim-decision-xls` 5002行 / `r8-service-codes-2-xlsx`基本2246行; `r6-calculation-note` 297頁; `r8-calculation-note` 300頁 |
| 初期加算 | `465050` | `461000` | 利用開始日、サービス提供日、初期期間内かの判定 | `r6-claim-decision-202404-xls` 5009行 / `r6-service-codes-2-xlsx`基本937行; `r6-claim-decision-202406-xls` 4924行; `r8-claim-decision-xls` 5003行 / `r8-service-codes-2-xlsx`基本2017行; `r6-calculation-note` 296頁; `r8-calculation-note` 299頁 |
| 視覚・聴覚言語支援体制加算 | `465060`; `465061` | `461000` | `visual-hearing-support`のI/II選択、サービス提供日 | `r6-claim-decision-202404-xls` 5010行 / `r6-service-codes-2-xlsx`基本924〜925行; `r6-claim-decision-202406-xls` 4925行; `r8-claim-decision-xls` 5004行 / `r8-service-codes-2-xlsx`基本2004〜2005行 |
| 食事提供体制加算 | `465070` | `461000` | `meal-provision-system.2`、受給者証上の食事提供体制加算対象、当日の食事提供実績 | `r6-claim-decision-202404-xls` 5011行 / `r6-service-codes-2-xlsx`基本1047行; `r6-claim-decision-202406-xls` 4926行; `r8-claim-decision-xls` 5005行 / `r8-service-codes-2-xlsx`基本2247行; `r6-calculation-note` 297頁; `r8-calculation-note` 300頁 |
| 就労移行支援体制加算 | `465240-465249`; `466775-466854` | `461000` | `employment-transition-support.2`、前年度就労定着者数、対象報酬体系、平均工賃月額区分、B型単独定員 | `r6-claim-decision-202404-xls` 5012〜5013行 / `r6-service-codes-2-xlsx`基本941〜1030行; `r6-claim-decision-202406-xls` 4927〜4928行; `r8-claim-decision-xls` 5006〜5007行 / `r8-service-codes-2-xlsx`基本2021〜2110行; `r6-calculation-note` 293〜295頁; `r8-calculation-note` 296〜298頁 |
| 目標工賃達成指導員配置加算 | `465255-465259` | `461000` | `target-wage-instructor.2`、B型単独定員、対象報酬体系、サービス提供日 | `r6-claim-decision-202404-xls` 5014行 / `r6-service-codes-2-xlsx`基本1032〜1036行; `r6-claim-decision-202406-xls` 4929行; `r8-claim-decision-xls` 5010行 / `r8-service-codes-2-xlsx`基本2232〜2236行; `r6-calculation-note` 300頁; `r8-calculation-note` 303頁 |
| 就労移行連携加算 | `465270` | `461000` | 就労移行支援事業所との連携実績、支給決定・利用履歴、算定回数 | `r6-claim-decision-202404-xls` 5015行 / `r6-service-codes-2-xlsx`基本1031行; `r6-claim-decision-202406-xls` 4930行; `r8-claim-decision-xls` 5011行 / `r8-service-codes-2-xlsx`基本2231行; `r6-calculation-note` 295〜296頁; `r8-calculation-note` 298〜299頁 |
| 訪問支援特別加算 | `465600`; `465601` | `461000` | 訪問支援日、所要時間区分、当月回数、支援記録 | `r6-claim-decision-202404-xls` 5016行 / `r6-service-codes-2-xlsx`基本938〜939行; `r6-claim-decision-202406-xls` 4931行; `r8-claim-decision-xls` 5012行 / `r8-service-codes-2-xlsx`基本2018〜2019行 |
| 重度者支援体制加算 | `465810-465819` | `461000` | `severe-support`のI/II選択、B型単独定員、サービス提供日 | `r6-claim-decision-202404-xls` 5017行 / `r6-service-codes-2-xlsx`基本927〜936行; `r6-claim-decision-202406-xls` 4932行; `r8-claim-decision-xls` 5013行 / `r8-service-codes-2-xlsx`基本2007〜2016行; `r6-calculation-note` 300頁; `r8-calculation-note` 303頁 |
| 福祉専門職員配置等加算 | `466035-466037` | `461000` | `welfare-professional-staffing`のI/II/III選択、サービス提供日 | `r6-claim-decision-202404-xls` 5018行 / `r6-service-codes-2-xlsx`基本921〜923行; `r6-claim-decision-202406-xls` 4933行; `r8-claim-decision-xls` 5014行 / `r8-service-codes-2-xlsx`基本2001〜2003行 |
| 欠席時対応加算 | `466040` | `461000` | 欠席日、連絡日、相談援助内容、当月算定回数 | `r6-claim-decision-202404-xls` 5019行 / `r6-service-codes-2-xlsx`基本940行; `r6-claim-decision-202406-xls` 4934行; `r8-claim-decision-xls` 5015行 / `r8-service-codes-2-xlsx`基本2020行; `r6-calculation-note` 297頁; `r8-calculation-note` 300頁 |
| 医療連携体制加算 | `466065-466068`; `466080-466082`; `469992` | `461000` | B型公式読替え後の区分I〜VI、看護等の実施日・内容、対象人数、時間、同日回数 | `r6-claim-decision-202404-xls` 5020〜5022行 / `r6-service-codes-2-xlsx`基本1038〜1045行; `r6-claim-decision-202406-xls` 4935〜4937行; `r8-claim-decision-xls` 5016〜5018行 / `r8-service-codes-2-xlsx`基本2238〜2245行; `r6-calculation-note` 297〜298頁; `r8-calculation-note` 300〜301頁 |
| 送迎加算 | `466590-466593` | `461000` | `transport-system`のI/II選択、往路・復路ごとの送迎実績、同一敷地内か | `r6-claim-decision-202404-xls` 5023行 / `r6-service-codes-2-xlsx`基本1048〜1051行; `r6-claim-decision-202406-xls` 4938行; `r8-claim-decision-xls` 5019行 / `r8-service-codes-2-xlsx`基本2248〜2251行 |
| 在宅時生活支援サービス加算 | `466875` | `460932` | 在宅支援対象、在宅支援日、生活支援の実施記録 | `r6-claim-decision-202404-xls` 5032行 / `r6-service-codes-2-xlsx`基本1055行; `r6-claim-decision-202406-xls` 4940行; `r8-claim-decision-xls` 5024行 / `r8-service-codes-2-xlsx`基本2255行; `r8-calculation-note` 286頁 |
| 社会生活支援特別加算 | `466880` | `460929` | `social-life-support.2`、対象者区分、サービス提供日 | `r6-claim-decision-202404-xls` 5033行 / `r6-service-codes-2-xlsx`基本1056行; `r6-claim-decision-202406-xls` 4941行; `r8-claim-decision-xls` 5025行 / `r8-service-codes-2-xlsx`基本2256行 |
| 地域協働加算 | `466885` | `461000` | 対象報酬体系IV/V/VI、地域協働の実施日・取組内容・公表記録 | `r6-claim-decision-202404-xls` 5034行 / `r6-service-codes-2-xlsx`基本1057行; `r6-claim-decision-202406-xls` 4942行; `r8-claim-decision-xls` 5026行 / `r8-service-codes-2-xlsx`基本2257行; IV/V/VI限定は`r6-calculation-note` 298頁、`r8-calculation-note` 301頁 |
| ピアサポート実施加算 | `466890` | `461000` | 対象報酬体系IV/V/VI、`peer-support.2`、ピアサポート実施日・実施記録 | `r6-claim-decision-202404-xls` 5035行 / `r6-service-codes-2-xlsx`基本1058行; `r6-claim-decision-202406-xls` 4943行; `r8-claim-decision-xls` 5027行 / `r8-service-codes-2-xlsx`基本2258行; IV/V/VI限定は`r6-calculation-note` 297頁、`r8-calculation-note` 300頁 |
| 体験利用時支援加算 | `467590-467592` | `461000` | 体験利用区分I/II、実施日、`community-life-support-base`の該当 | `r6-claim-decision-202404-xls` 5036行 / `r6-service-codes-2-xlsx`基本1052〜1054行; `r6-claim-decision-202406-xls` 4944行; `r8-claim-decision-xls` 5028行 / `r8-service-codes-2-xlsx`基本2252〜2254行 |
| 集中的支援加算 | `465041` | `461000` | 集中的支援の区分、実施日・実施記録、当月回数 | `r6-claim-decision-202404-xls` 5037行 / `r6-service-codes-2-xlsx`基本1060行; `r6-claim-decision-202406-xls` 4945行; `r8-claim-decision-xls` 5029行 / `r8-service-codes-2-xlsx`基本2260行 |
| 高次脳機能障害者支援体制加算 | `465078` | `461000` | `higher-brain-dysfunction-support.2`、対象利用者、サービス提供日 | `r6-claim-decision-202404-xls` 5038行 / `r6-service-codes-2-xlsx`基本926行; `r6-claim-decision-202406-xls` 4946行; `r8-claim-decision-xls` 5030行 / `r8-service-codes-2-xlsx`基本2006行 |
| 緊急時受入加算 | `465086` | `461000` | 緊急受入日、受入理由・支援記録、当月回数 | `r6-claim-decision-202404-xls` 5039行 / `r6-service-codes-2-xlsx`基本1059行; `r6-claim-decision-202406-xls` 4947行; `r8-claim-decision-xls` 5031行 / `r8-service-codes-2-xlsx`基本2259行 |
| 目標工賃達成加算 | `465087` | `461000` | `target-wage-achievement.2`、対象報酬体系、事業所工賃向上計画、目標額、実績平均工賃月額 | `r6-claim-decision-202404-xls` 5040行 / `r6-service-codes-2-xlsx`基本1037行; `r6-claim-decision-202406-xls` 4948行; `r8-claim-decision-xls` 5032行 / `r8-service-codes-2-xlsx`基本2237行; `r6-calculation-note` 300〜302頁; `r8-calculation-note` 303〜305頁 |

#### 版で変わるコード

| 表示名 | 請求サービスコード | 決定 | 適用開始月 | 廃止月 | 必要入力 | 公式根拠 |
| --- | --- | --- | --- | --- | --- | --- |
| 福祉・介護職員処遇改善加算I | `466715`; `466716` | `461000` | 2024-04 | 2024-05 | `legacy-treatment-improvement-eligible.2`、`legacy-career-path`、障害者支援施設か | `r6-claim-decision-202404-xls` 5024行; `r6-service-codes-2-xlsx`基本1097〜1098行 |
| 福祉・介護職員処遇改善加算II | `466710`; `466711` | `461000` | 2024-04 | 2024-05 | 同上 | `r6-claim-decision-202404-xls` 5025行; `r6-service-codes-2-xlsx`基本1099〜1100行 |
| 福祉・介護職員処遇改善加算III | `466665`; `466666` | `461000` | 2024-04 | 2024-05 | 同上 | `r6-claim-decision-202404-xls` 5026行; `r6-service-codes-2-xlsx`基本1101〜1102行 |
| 福祉・介護職員等特定処遇改善加算I | `466772` | `461000` | 2024-04 | 2024-05 | `legacy-specific-treatment-improvement-eligible.2`、同区分`1` | `r6-claim-decision-202404-xls` 5027行; `r6-service-codes-2-xlsx`基本1103行 |
| 福祉・介護職員等特定処遇改善加算II | `466773` | `461000` | 2024-04 | 2024-05 | 同体制、同区分`2` | `r6-claim-decision-202404-xls` 5028行; `r6-service-codes-2-xlsx`基本1104行 |
| 福祉・介護職員等特定処遇改善加算（指定障害者支援施設） | `466774` | `461000` | 2024-04 | 2024-05 | 同体制、障害者支援施設 | `r6-claim-decision-202404-xls` 5029〜5030行; `r6-service-codes-2-xlsx`基本1105行 |
| 福祉・介護職員等ベースアップ等支援加算 | `466766` | `461000` | 2024-04 | 2024-05 | `legacy-baseup-support-eligible.2` | `r6-claim-decision-202404-xls` 5031行; `r6-service-codes-2-xlsx`基本1106行 |
| 福祉・介護職員等処遇改善加算I〜IV | `465120`; `465121`; `465122`; `465123`; `465138`; `465140`; `465141` | `461000` | 2024-06 | 2026-05 | `treatment-improvement`のI〜IV、障害者支援施設か、月次算定基礎 | `r6-claim-decision-202406-xls` 4939行; `r6-service-codes-2-xlsx`基本1061〜1068行 |
| 福祉・介護職員等処遇改善加算V(1)〜V(14) | `465124-465137`; `465142`; `465143`; `465146`; `465148`; `465149`; `465151`; `465152`; `465154`; `465155` | `461000` | 2024-06 | 2025-03 | `treatment-improvement.6`、`treatment-improvement-v-class`、障害者支援施設か、月次算定基礎 | `r6-claim-decision-202406-xls` 4939行; `r6-service-codes-2-xlsx`基本1069〜1096行（1061行注記でVは2025-03-31まで） |
| 福祉・介護職員等処遇改善加算Iイ・Iロ・IIイ・IIロ・III・IV | `465120`; `465121`; `465122`; `465123`; `465138`; `465140`; `465141`; `465174`; `465175`; `465176` | `461000` | 2026-06 | null | `treatment-improvement`の公式選択`2,3,4,5,7,8`、障害者支援施設か、月次算定基礎 | `r8-claim-decision-xls` 5020〜5023行; `r8-service-codes-2-xlsx`基本2261〜2272行 |
| 就労移行支援体制加算 R8追加区分 | `466A01-466A99`; `466B01-466B21` | `461000` | 2026-06 | null | 共通コードと同じ入力にR8平均工賃月額区分を追加 | `r8-claim-decision-xls` 5008〜5009行; `r8-service-codes-2-xlsx`基本2111〜2230行; `r8-calculation-note` 296〜298頁 |

### `OfficeCapability.Flags` の正式キー集合

次表のfieldだけを許可する。`{1,2}`はキー末尾にできる公式選択番号の閉集合を表す。根拠セルの表示名と選択番号をそのまま使用し、表示名から請求サービスコードを逆算しない。

| field-id | R6-04 option | R6-06 option | R8-06 option | 公式根拠行 |
| --- | --- | --- | --- | --- |
| `capacity-exceeded` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404`基本274行; `r6-capability-202406`基本241行; `r8-capability-202606`別紙1-1 243行 + `r8-capability-correction` 9頁 |
| `staff-shortage` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 275行; `r6-capability-202406` 242行; `r8-capability-202606` 244行 + `r8-capability-correction` 9頁 |
| `service-manager-shortage` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 276行; `r6-capability-202406` 243行; `r8-capability-202606` 245行 + `r8-capability-correction` 9頁 |
| `physical-restraint-noncompliance` | `{1,2,3}` | `{1,2,3}` | `{1,2,3}` | `r6-capability-202404` 277行; `r6-capability-202406` 244行; `r8-capability-202606` 246行 + `r8-capability-correction` 9頁 |
| `abuse-prevention-noncompliance` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 278行; `r6-capability-202406` 245行; `r8-capability-202606` 247行 + `r8-capability-correction` 9頁 |
| `business-continuity-plan-missing` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 279行; `r6-capability-202406` 246行; `r8-capability-202606` 248行 + `r8-capability-correction` 9頁 |
| `information-disclosure-missing` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 280行; `r6-capability-202406` 247行; `r8-capability-202606` 249行 + `r8-capability-correction` 9頁 |
| `welfare-professional-staffing` | `{1,3,4,5}` | `{1,3,4,5}` | `{1,3,4,5}` | `r6-capability-202404` 281行; `r6-capability-202406` 248行; `r8-capability-202606` 250行 + `r8-capability-correction` 9頁 |
| `visual-hearing-support` | `{1,2,3}` | `{1,2,3}` | `{1,2,3}` | `r6-capability-202404` 282行; `r6-capability-202406` 249行; `r8-capability-202606` 251行 + `r8-capability-correction` 9頁 |
| `severe-support` | `{1,2,3}` | `{1,2,3}` | `{1,2,3}` | `r6-capability-202404` 283行; `r6-capability-202406` 250行; `r8-capability-202606` 252行 + `r8-capability-correction` 9頁 |
| `employment-transition-support` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 284行; `r6-capability-202406` 251行; `r8-capability-202606` 253行 + `r8-capability-correction` 9頁 |
| `target-wage-instructor` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 286行; `r6-capability-202406` 253行; `r8-capability-202606` 255行 + `r8-capability-correction` 9頁 |
| `target-wage-achievement` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 287行; `r6-capability-202406` 254行; `r8-capability-202606` 256行 + `r8-capability-correction` 9頁 |
| `transport-system` | `{1,3,4}` | `{1,3,4}` | `{1,3,4}` | `r6-capability-202404` 288行; `r6-capability-202406` 255行; `r8-capability-202606` 257行 + `r8-capability-correction` 9頁。`3=I`、`4=II` |
| `meal-provision-system` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 289行; `r6-capability-202406` 256行; `r8-capability-202606` 258行 + `r8-capability-correction` 9頁 |
| `social-life-support` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 290行; `r6-capability-202406` 257行; `r8-capability-202606` 259行 + `r8-capability-correction` 9頁 |
| `designated-management` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 296行; `r6-capability-202406` 260行; `r8-capability-202606` 261行 + `r8-capability-correction` 9頁 |
| `peer-support` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 297行; `r6-capability-202406` 261行; `r8-capability-202606` 262行 + `r8-capability-correction` 9頁 |
| `community-life-support-base` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 298行; `r6-capability-202406` 262行; `r8-capability-202606` 263行 + `r8-capability-correction` 9頁 |
| `higher-brain-dysfunction-support` | `{1,2}` | `{1,2}` | `{1,2}` | `r6-capability-202404` 299行; `r6-capability-202406` 263行; `r8-capability-202606` 264行 + `r8-capability-correction` 9頁 |
| `legacy-treatment-improvement-eligible` | `{1,2}` | 使用不可 | 使用不可 | `r6-capability-202404`基本291行 |
| `legacy-specific-treatment-improvement-eligible` | `{1,2}` | 使用不可 | 使用不可 | `r6-capability-202404`基本292行 |
| `legacy-baseup-support-eligible` | `{1,2}` | 使用不可 | 使用不可 | `r6-capability-202404`基本293行 |
| `legacy-career-path` | `{1,5,6}` | 使用不可 | 使用不可 | `r6-capability-202404`基本294行 |
| `legacy-specific-treatment-improvement-class` | `{1,2}` | 使用不可 | 使用不可 | `r6-capability-202404`基本295行 |
| `treatment-improvement` | 使用不可 | `{1,2,3,4,5,6}` | `{1,2,3,4,5,7,8}` | `r6-capability-202406`基本258行; `r8-capability-202606`別紙1-1 260行 + 訂正9頁 |
| `treatment-improvement-v-class` | 使用不可 | `{1,2,3,4,5,6,7,8,9,10,11,12,13,14}` | 使用不可 | `r6-capability-202406`基本259行 |

requiredWhenを満たすfieldは、適用版の許可optionからexactly-oneを要求する。forbiddenWhenを満たすfieldは0件を要求し、1件でも存在すれば版外入力として拒否する。条件付きfieldを含む閉じた依存規則を次に固定する。

| 対象版・field | requiredWhen | forbiddenWhen | 公式根拠 |
| --- | --- | --- | --- |
| 全版の共通20field（`capacity-exceeded`〜`higher-brain-dysfunction-support`） | R6-04、R6-06、R8-06で常に | なし | 上表の各根拠行 |
| R6-04 `legacy-treatment-improvement-eligible` | R6-04で常に | R6-06、R8-06 | `r6-capability-202404` 291行 |
| R6-04 `legacy-career-path` | `legacy-treatment-improvement-eligible.2`を選択 | `legacy-treatment-improvement-eligible.1`を選択、またはR6-04以外 | `r6-capability-202404` 294行、399行 |
| R6-04 `legacy-specific-treatment-improvement-eligible` | R6-04で常に | R6-06、R8-06 | `r6-capability-202404` 292行 |
| R6-04 `legacy-specific-treatment-improvement-class` | `legacy-specific-treatment-improvement-eligible.2`を選択 | `legacy-specific-treatment-improvement-eligible.1`を選択、またはR6-04以外 | `r6-capability-202404` 295行、400行 |
| R6-04 `legacy-baseup-support-eligible` | R6-04で常に | R6-06、R8-06 | `r6-capability-202404` 293行 |
| R6-06/R8-06 `treatment-improvement` | R6-06またはR8-06で常に | R6-04 | `r6-capability-202406` 258行; `r8-capability-202606` 260行 + `r8-capability-correction` 9頁 |
| R6-06 `treatment-improvement-v-class` | サービス月が2024-06〜2025-03で、`treatment-improvement.6`を選択 | `treatment-improvement.1`〜`.5`を選択、サービス月が2025-04以降、またはR6-06以外 | `r6-capability-202406` 259行、372〜373行 |

処遇改善を算定しない場合は、常時必須の親fieldに公式の「1.なし」を明示登録し、条件付きの子fieldは登録しない。R6-06の `treatment-improvement.6` は2025-03までだけ許可し、2025-04以降は親fieldの許可optionを `{1,2,3,4,5}` に絞る。指定障害者支援施設では、R6-06の `treatment-improvement` は `{1,2,4,5,6}`、そのV区分は `{1,2,5,7,8,10,11,13,14}`、R8-06の `treatment-improvement` は `{1,2,4,5,7}` へさらに絞る。障害者支援施設か否かは `designated-management` から推測せず、構造化入力を使う。根拠は `r6-capability-202406` 372〜375行と `r8-capability-202606` 372行である。

基本報酬の `capacityClass`、`multifunctionCapacityClass`、`staffingClass`、`averageWageBand` はこのkey集合に含めない。R6-04は `r6-capability-202404` 基本273行、R6-06は `r6-capability-202406` 基本240行、R8-06は `r8-capability-202606` 別紙1-1 242行と `r8-capability-correction` 9頁にある構造化入力であり、`OfficeClaimProfile`で型を分ける。

### 暫定キーの移行

リポジトリで使用実績のある旧キーは次の2件だけである。

| 旧キー | 自動変換 | 明示的な再登録候補 | 自動変換しない理由 |
| --- | --- | --- | --- |
| `mealProvision` | 禁止 | `mhlw.b46.capability.meal-provision-system.1` または `mhlw.b46.capability.meal-provision-system.2` | 旧boolは公式の届出選択を証明せず、利用者の受給者証上の対象・当日提供実績も含まない |
| `transportSupport` | 禁止 | `mhlw.b46.capability.transport-system.1`、`mhlw.b46.capability.transport-system.3`、`mhlw.b46.capability.transport-system.4` のいずれか | 旧boolは送迎体制IとIIを区別せず、片道実績・同一敷地内かも表さない |

既存レコードを書き換えない。AC3-8の入力UIで公式資料の表示名、選択番号、適用開始日を表示し、利用者が事業所届出内容を確認して新しい追記型 `OfficeCapability` を登録する。旧レコードは監査履歴として保持し、新レコードの期間開始日は届出の実効日と一致させる。`true`だった旧キーだけを移す、`false`を公式の「なし」にする、名称の似た請求コードへ置換する処理は作らない。

Phase 3-1は、対象サービス提供日 `asOf` を含むサービス月からmasterVersionを先に一意に選び、同一事業所で `Period.Contains(asOf)` を満たす `OfficeCapability` を候補にする。`DateRange` は両端を含み、`End=null` は無期限として扱う。候補が重複した場合は `Period.Start` の降順、次に `CreatedAt` の降順で優先し、先頭1件を実効レコードとする。後の開始日を持つレコードは前の期間レコードを、開始日が同じなら後に追記されたレコードは先のレコードを暗黙にsupersedeする。異なるレコードの `Period.Start` と `CreatedAt` がともに同値で一意な先頭を決められない場合は、ID順等へフォールバックせず曖昧として算定不能にする。

正式キーとrequiredWhen/forbiddenWhenは、この実効レコード1件だけをサービス月のmasterVersionで検証する。監査履歴として残る非選択レコードの旧キー・旧版キーは算定を失敗させない。実効レコードの `Period` が2024-06-01または2026-06-01の版境界をまたいでもmasterVersionを期間開始日から推測せず、対象サービス月の版で同じFlagsを再検証する。境界後の版で使用不可のfield/optionを持つ実効レコードは算定不能とし、旧版へ戻さない。

再登録UIは少なくとも、正式one-hotキー全field、利用定員・定員規模・多機能型等定員区分、人員配置区分、届出済み報酬体系、平均工賃月額区分、前年度就労定着者数を入力できなければならない。食事提供は受給者証上の対象と日別提供実績、送迎は往路・復路と同一敷地内、欠席時対応は連絡・相談援助内容と月回数、上限額管理は月次の実施結果を別入力にする。これらを暫定キーや自由記述から復元しない。

### フェイルクローズ

Phase 3-1のマスタ登録時と算定時に、次のいずれかを検出した場合は請求明細を作らず、事業所、サービス月、fieldまたはcode、期待したmasterVersionを含むエラーを返す。

- 実効レコードに `mealProvision`、`transportSupport`またはその他の未登録キーが存在する。
- 実効レコードの正式キーがfalse、形式不正、版外field、版外option、requiredWhenの無選択・複数選択、またはforbiddenWhenのfield保持である。
- `OfficeCapability` の候補がない、または同順位で実効レコードが一意に決まらない。
- サービス月からmasterVersionを一意に選べない、または版境界後のmasterVersionで実効レコードを検証できない。
- 基本報酬の構造化入力、前年度就労定着者数、実績入力のいずれかが不足している。
- 短時間利用減算の利用時間・分母利用者・除外利用者/理由、または `46Z006` の事業所単位・利用者単位の除外入力が不足している。
- 請求サービスコードが適用版の `r6/r8-service-codes-2-xlsx` に一意に存在しない。
- 請求サービスコードから決定サービスコードへの対応が適用版対応表で一意に解決できない。
- 必要なSourceDocumentId、SHA-256、訂正資料または適用版が欠けている。

前版、近いコード、同じ表示名、同じ単位数、既存bool、空文字、ゼロ値へフォールバックしない。エラーを警告へ降格して算定を継続しない。

## 影響

- Phase 3-1の加算マスタは請求サービスコードを主キー相当の版付きcodeとして持ち、`OfficeCapability.Flags`のkeyとは別フィールドにする。
- Phase 3-1の請求解決はサービス月でmasterVersionを選び、重複する `OfficeCapability` から開始日・作成日時の優先規則で実効レコード1件だけを検証する。
- AC3-8は正式キーの再登録UIと、boolで表せない事業所・日次・月次入力を追加する。現在の2チェックボックスUIだけでは算定を開始できない。
- `OfficeCapability`は追記型を維持し、移行のための既存行UPDATEや推測変換を行わない。
- R8体制は `r8-capability-202606` と `r8-capability-correction` を常に一組で検証する。
- 本ADRのcode、期間、sourceDocumentId、sheet/row/page locatorを外部マスタへ転記し、C#定数へ複製しない。
