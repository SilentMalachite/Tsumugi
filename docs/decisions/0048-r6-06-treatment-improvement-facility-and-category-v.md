# ADR 0048: R6-06世代 統一処遇改善加算の指定障害者支援施設variantと(Ⅴ)の実値投入

- 状態: 確定（2026-07-27）
- 関連: [ADR 0021](0021-office-capability-official-codes.md) / [ADR 0025](0025-claim-rounding-rules.md) /
  [ADR 0026](0026-claim-batch-snapshot.md) / [ADR 0029](0029-claim-snapshot-codec-v2.md) /
  [ADR 0045](0045-r8-treatment-improvement-addition-values.md) / [ADR 0047](0047-r8-designated-support-facility-variants.md) /
  [ADR 0049](0049-office-capability-master-coverage-check.md)

## 結論

R6-06世代（2024-06〜2026-05）の福祉・介護職員等処遇改善加算について、次の**26行**を出典付きで投入する。あわせて既存の通常3行へ非施設条件を付与する。

1. **指定障害者支援施設variant 3区分**（(Ⅰ)・(Ⅲ)・(Ⅳ)。適用期間 2024-06〜2026-05）
2. **処遇改善(Ⅴ) 14サブ区分の通常行**（適用期間 **2024-06〜2025-03**）
3. **処遇改善(Ⅴ) の施設variant 9区分**（同上）

**(Ⅱ)は対象外**（告示に施設別立ての括弧書きが無く、サービスコードも存在しない）。**(Ⅴ)は令和7年3月31日限りで失効し、R8-06世代（2026-06〜）には存在しない**。

これにより ADR 0047 がR8-06について塞いだ「指定障害者支援施設が無音で通常行へ解決し過少請求になる」欠陥が、R6-06世代についても塞がれる。

### ADR 0045 の前提の訂正（明示）

ADR 0045 は処遇改善(Ⅴ)を「確定できなかった区分」として持ち越し、`docs/open-questions.md` も「**R8処遇改善(Ⅴ)の実値投入**」という項目名で、`r8-capability-correction` と `r8-fee-notice` から14通りの率を一意に確定してから **R8-06へ** seedする、という解除条件を書いていた。

**この前提は誤りだった。** 就労継続支援B型の処遇改善(Ⅴ)は **R6-06世代の経過措置であり、2024-06〜2025-03 にしか存在しない**。R8-06へ投入すべき(Ⅴ)は存在しない（根拠は「(Ⅴ)の適用期間」節の3点）。本ADRは、当該open-questions項目を「解除条件を満たしたためクローズ」ではなく **「前提が誤りだったため、R6分の投入と R8 非存在の確定をもってクローズ」** として扱う。

## 背景

`docs/phase3-5-acceptance.md` §8-5 が最終レビューで発見した欠陥がそのまま残っていた。`b-addition.r6-06.treatment-improvement.unified.i`〜`.iv`（465120〜465123、適用期間2024-06〜2026-05）は `conditionSelectors` に施設区分条件を1つも持たず、**処理対象月が2026-05以前なら、指定障害者支援施設が体制届option 2等を届け出ても通常行へ無音で解決していた（過少請求。エラーは出ない）**。

同時に、処遇改善(Ⅴ)（体制届 選択番号6）は一切seedされておらず、option 6 を届け出た事業所は **警告なく加算が0円**になっていた（無音の未算定）。ADR 0045 はこれを未確定として保留していた。

Phase 3-5（ADR 0047）はDomain側の受け皿（`FacilityClassification` enum・`ClaimBillingConditionContext.FacilityClassification`・`ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved`）と `OfficeClaimProfile` の永続化・結線・入力UIを既に完成させているため、本ADRはマスタデータの投入だけでR6-06世代へ同じ解決を届けられる。

## 一次資料の同一性検証（2026-07-26実施）

本ADRで使用した文書をすべて再取得し、`sources.json` の登録値とSHA-256を照合した。不一致は0件。

| documentId | SHA-256 | 用途 |
| --- | --- | --- |
| `r6-fee-notice` | `5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54` | R6の率（authoritative）・(Ⅴ)の適用期限 |
| `r6-service-codes-2-xlsx` | `4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82` | R6のサービスコード（authoritative） |
| `r6-service-codes-2-pdf` | `708270200599de9fb7d15d7270997286c3671d378e0a00e3b186a946e67b4465` | 同上の2形式独立照合（cross-check） |
| `r6-capability-202406` | `d1edf9715b8c41660d6e4278ebd886861d0758c75109e4efc594f5d70f197c50` | 体制届 選択番号6=(Ⅴ) の出典 |
| `r6-calculation-note` | `958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1` | 端数処理（ADR 0025と同一節） |
| `r8-fee-notice` | `f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c` | 改正前欄によるR6値の独立確認・(Ⅴ)削除の確認 |
| `r8-service-codes-2-xlsx` | `307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049` | R8に(Ⅴ)コードが無いことの確認 |
| `r8-capability-correction` | `06414c8aad4c014f44fd211dac141d152f30135fb622cdd32874e1c6bccbd980` | (Ⅴ)区分の選択番号（1〜14）の出典 |

### 読み取り上の重大な前提: `r6-fee-notice` 物理235〜236頁は新旧対照の2欄構成

同PDFの物理235〜236頁前半は**左右2欄の新旧対照表**である。

- **左欄＝改正後（令和6年6月1日以降。本ADRが投入するR6-06統一処遇改善）**
- **右欄＝改正前（「令和６年５月31日までの間」の限定文言つき。旧・処遇改善／特定処遇改善／ベースアップ等支援の3加算構造）**。同じ区分ラベルに**別の数値**が並ぶ（例: (Ⅰ)は54/64、(Ⅱ)は40/47）

**本ADRの率はすべて左欄（改正後）の値である。** 既存seedの locator 表記（`p.235（第2条表・左欄改正後 第14の17 率）`）もこの区別を明示しており、投入した26行の locator もすべて「左欄改正後」を明記した。(Ⅴ)⑴〜⒁の経過措置部分（236頁12行目以降）からは右欄が「（新設）」となり実質1カラムになる。

## 2形式独立照合の結果

### 告示の括弧書きの有無と、コード表の欠番が完全一致する

`r6-service-codes-2-xlsx`（ワークブック順38「18就労継続支援(B・基本)」）と `r6-service-codes-2-pdf`（物理259頁）を独立に抽出し、**同一のコード集合・同一の欠番**を得た。PDF側は type-46 の処遇改善コード30件がすべて物理259頁の1頁に収まる。

そのうえで、**告示（`r6-fee-notice` 左欄）の「指定障害者支援施設にあっては」という括弧書きの有無と、コード表の施設行の有無が18区分すべてで完全に一致した**。

| 区分 | 告示の括弧書き | 施設サービスコード |
| --- | --- | --- |
| (Ⅰ) | あり（104） | 465138 |
| (Ⅱ) | **なし** | **なし** |
| (Ⅲ) | あり（86） | 465140 |
| (Ⅳ) | あり（69） | 465141 |
| (Ⅴ)⑴⑵⑸⑺⑻⑽⑾⒀⒁ | あり | あり（9件） |
| (Ⅴ)⑶⑷⑹⑼⑿ | **なし** | **なし** |

施設別立てを持たない6区分（(Ⅱ)・(Ⅴ)⑶⑷⑹⑼⑿）については、xlsx の直後行に「指定障害者支援施設において行った場合」という**注記テキストのみ**が入り、列Aの `46` もコード番号も無い（例: 行1064は `['指定障害者支援施設において行った場合', '単位加算']` だけ）。すなわち**抽出漏れではなく制度上の欠番**であることが、率表とコード表の双方から独立に裏づけられる。これは ADR 0047 がR8-06の(Ⅱ)イ・(Ⅱ)ロについて確認したのと同じ構造である。

### 率の独立確認

`r8-fee-notice` 物理57頁の**改正前欄**が同じ値（通常 93・91・76・62、施設 104・86・69）を示し、**2つの独立した告示が一致する**。ADR 0045「抽出方式と2方式の一致確認結果」節の観測が正しかったことを、別の告示から確認した。

## (Ⅴ)の適用期間 — 2024-06〜2025-03 のみ

3つの独立した証拠が一致する。

1. `r6-fee-notice` の (Ⅴ) 規定本文（物理236〜238頁、第2条表・左欄改正後 第14の17 注2 ホ）が「**令和７年３月31日までの間**」と期限を明記する。
2. `r8-fee-notice` 物理57頁が同規定を「**（削る）**」として削除する。同notice の**改正前欄**も期限が「令和７年３月31日」のままであり、**中間の告示で延長されていない**ことがわかる。
3. `r8-service-codes-2-xlsx` ワークブック順38 は Excel行2272（`465141`）で終わり、**(Ⅴ)のコード `465124`〜`465137` が存在しない**。

したがって就労継続支援B型の処遇改善(Ⅴ)は 2024-06〜2025-03 にのみ存在する。seed の `effectiveTo` は `"2025-03"` とする。

**`r8-capability-correction` 物理9頁の体制届（別紙1-1）に `６．Ⅴ` と「福祉・介護職員等処遇改善加算（Ⅴ）区分」の行が残っていること**は、上記と矛盾しない。別紙1-1は複数サービス種別で共用する様式であり、B型について(Ⅴ)が算定できることを意味しない。**B型については告示とコード表を正とする。**

### 令和8年6月版の様式を、令和6年期の語彙の出典に使う理由

本ADRには証跡上の緊張関係がある。**(Ⅴ)のサブ区分選択番号（1〜14）を列挙している登録済みauthoritative文書は `r8-capability-correction`（令和8年6月版）だけ**である一方、本ADR自身が「(Ⅴ)はR8には存在しない」と結論している。矛盾に見えるが、次の役割分担により整合する。

- **`r8-capability-correction` が担うのは `supports: ["conditions"]` だけ** — すなわち「(Ⅴ)区分という体制届項目が存在し、その選択肢が `１．Ｖ（１）`〜`１４．Ｖ（１４）` の14択である」という**語彙の列挙**のみ。この文書は**期間について何も主張していない**。
- **期間（`supports: ["effective-period"]`）を担うのは `r6-fee-notice`** — 「令和７年３月31日までの間」という期限本文が、`effectiveFrom: 2024-06` / `effectiveTo: 2025-03` の唯一の根拠である。

言い換えると、R8版の様式は「(Ⅴ)⑴〜⒁という区分ラベルが公式に何と呼ばれ、何番で選択されるか」という**恒常的な語彙**を示しているにすぎない。区分ラベル自体は `r6-fee-notice` 左欄の ⑴〜⒁ と1対1で対応しており（`capability-treatment-improvement-v-band-{n}` の `r6-fee-notice` locator が各 ⑴〜⒁ を個別に指す）、R8版様式が与えるのは**そのラベルに対応する選択番号**だけである。**選択番号を推測で振らない**ためにこの文書を引いている。

なお **(Ⅴ)そのもの（体制届 選択番号6）の出典は R6期の `r6-capability-202406`**（基本情報シート258行、38列目の表示文字列「１．なし　２．Ⅰ　３．Ⅱ　４．Ⅲ　５．Ⅳ　６．Ⅴ」）であり、R8版様式には依存していない。R8版様式に依存しているのは**サブ区分14択の番号だけ**である。

この役割分担は `sourceRefs` の `supports` に機械可読な形で書かれており、`SourceAuthorityValidator` が `(key, support-token)` ごとに authoritative 文書がちょうど1つであることを強制する（両文書を同じ `supports` で authoritative にすると `has multiple authoritative maxima` で load 時に fail-close する。実際に初期実装がこれで落ちた）。

## 決定

### 決定1: 施設variant 3行（2024-06〜2026-05）

`additions.json` / `service-codes.json` に3行ずつ追加する。率は `r6-fee-notice` 左欄の括弧書きから、サービスコードは xlsx/pdf の2形式照合から確定した。

| 区分 | 体制届option | 通常コード | 通常率 | 施設コード | 施設率 | `calculationOrder` | xlsx行（施設） |
| --- | --- | --- | ---: | --- | ---: | ---: | --- |
| (Ⅰ) | 2 | 465120 | 0.093 | **465138** | **0.104** | 5 | `workbook-order=38;row=1062` |
| (Ⅲ) | 4 | 465122 | 0.076 | **465140** | **0.086** | 6 | `workbook-order=38;row=1066` |
| (Ⅳ) | 5 | 465123 | 0.062 | **465141** | **0.069** | 7 | `workbook-order=38;row=1068` |

キー: `addition.treatment-improvement.unified.{i,iii,iv}.facility` / `b-addition.r6-06.treatment-improvement.unified.{i,iii,iv}.facility`。
`officialLabel` は既存通常行のラベルへ「（指定障害者支援施設において行った場合）」を付加した（ADR 0047 と同じ規約）。

**サービスコード 465138・465140・465141 は R6-06（2024-06〜2026-05）と R8-06（2026-06〜、ADR 0047）で共有される。** 世代境界で率だけが変わる（104→116 等）。行キー（`b-addition.r6-06.…` / `b-addition.r8-06.…`）は世代ごとに別であり、共有されない。

### 決定2: 処遇改善(Ⅴ) 23行（2024-06〜2025-03）

| サブ区分 | 通常code | 通常率 | 通常xlsx行 | 施設code | 施設率 | 施設xlsx行 | `calculationOrder`（通常/施設） |
| --- | --- | ---: | ---: | --- | ---: | ---: | --- |
| ⑴ | 465124 | 0.080 | 1069 | 465142 | 0.091 | 1070 | 8 / 22 |
| ⑵ | 465125 | 0.079 | 1071 | 465143 | 0.087 | 1072 | 9 / 23 |
| ⑶ | 465126 | 0.078 | 1073 | — | — | — | 10 / — |
| ⑷ | 465127 | 0.077 | 1075 | — | — | — | 11 / — |
| ⑸ | 465128 | 0.066 | 1077 | 465146 | 0.074 | 1078 | 12 / 24 |
| ⑹ | 465129 | 0.064 | 1079 | — | — | — | 13 / — |
| ⑺ | 465130 | 0.061 | 1081 | 465148 | 0.066 | 1082 | 14 / 25 |
| ⑻ | 465131 | 0.063 | 1083 | 465149 | 0.073 | 1084 | 15 / 26 |
| ⑼ | 465132 | 0.059 | 1085 | — | — | — | 16 / — |
| ⑽ | 465133 | 0.048 | 1087 | 465151 | 0.053 | 1088 | 17 / 27 |
| ⑾ | 465134 | 0.049 | 1089 | 465152 | 0.056 | 1090 | 18 / 28 |
| ⑿ | 465135 | 0.046 | 1091 | — | — | — | 19 / — |
| ⒀ | 465136 | 0.044 | 1093 | 465154 | 0.048 | 1094 | 20 / 29 |
| ⒁ | 465137 | 0.031 | 1095 | 465155 | 0.035 | 1096 | 21 / 30 |

`r6-fee-notice` の locator は物理頁で個別に指す（⑴〜⑶=236頁、⑷〜⒀=237頁、⒁=238頁）。**率の数値そのものは locator に埋めない**（`docs/phase3-5-acceptance.md` §9 が deferred minor として記録した「率が locator に埋まっている」を繰り返さない）。

キー: `addition.treatment-improvement.unified.v-{1..14}` / `…v-{n}.facility`、`b-addition.r6-06.treatment-improvement.unified.v-{n}` / `…v-{n}.facility`。

### 決定3: 条件定義17件を新設する（既存条件の期間は書き換えない）

| key | kind | 期間 | value |
| --- | --- | --- | --- |
| `facility-classification-general-r6-06` | `facility-classification` | 2024-06〜2026-05 | `general` |
| `facility-classification-designated-support-facility-r6-06` | `facility-classification` | 2024-06〜2026-05 | `designated-support-facility` |
| `capability-treatment-improvement-v` | `office-capability` | 2024-06〜**2025-03** | `mhlw.b46.capability.treatment-improvement.6` |
| `capability-treatment-improvement-v-band-{1..14}` | `office-capability` | 2024-06〜**2025-03** | `mhlw.b46.capability.treatment-improvement-v-band.{n}` |

ADR 0047 が定めた `facility-classification-general` / `-designated-support-facility` は `effectiveFrom: "2026-06"` であり、`ClaimMasterFileValidator` の「conditionDefinition の有効期間が参照元行の有効期間を覆っていること」検査を通らない。**既存条件の期間を書き換えず**、R6世代用に `-r6-06` 接尾辞つきの別キーを立てた（ハード制約「R6の条件定義を書き換えない」に従う）。

### 決定4: (Ⅴ)行は「option 6」と「サブ区分」の二重ゲートで判定する

(Ⅴ)の各行は `capability-treatment-improvement-v`（option 6）と該当する `-v-band-{n}` の**両方**を `conditionSelectors` に持つ。片方（band だけ）にすると、`OfficeCapabilityKeys` が「band は立っているが option 6 は立っていない」という中途半端な状態でも(Ⅴ)加算が解決してしまう。

この二重ゲートは、`ClaimMasterFileValidator.ValidateConditionIntersection` の既存実装では成立しなかった（後述「影響: validator の2箇所の改修」）。

### 決定5: 施設variantを持つ通常行にだけ非施設条件を付ける

ADR 0047 の決定をそのまま踏襲する。

- 既存3行 `b-addition.r6-06.treatment-improvement.unified.{i,iii,iv}` の `conditionSelectors` **末尾**へ `facility-classification-general-r6-06` を追加する（`serviceCode`・`officialLabel`・`unitRule`・`percentage` は一切変更しない）。
- **`unified.ii`（465121）は無変更**。施設別立てが無いため、施設事業所も通常行で算定するのが正しい（施設条件を付けると一致0行になり無音の未算定になる。ADR 0047「(Ⅱ)を対象外にする理由」と同じ）。
- (Ⅴ)についても同様に、施設variantを**持つ**9サブ区分の通常行にだけ `facility-classification-general-r6-06` を付け、**持たない**5サブ区分（⑶⑷⑹⑼⑿）には施設区分条件を一切付けない。

**`conditionSelectors` の配列順**: `MatchesAll` は `.All` の短絡評価であるためフェイルクローズの発生順が配列順に依存する（`docs/phase3-5-acceptance.md` §9 の deferred minor）。施設条件は**既存要素の末尾**へ追加した（R8行が `reward-system…` → `capability-…` → `facility-classification-…` の順を採っているのと揃えた）。

### 決定6: `calculationOrder` は 1〜30 の連続集合になる

`ClaimMasterFileValidator.ValidateCalculationOrder` は、同一 `targetSelector`・同一期間で有効な割合加算行の `calculationOrder` が「1から連続かつ一意」であることを要求する。処遇改善30行（通常4＋施設3＋(Ⅴ)通常14＋(Ⅴ)施設9）はすべて `target.b46.items-1-to-16-4.v1` を共有し2024-06〜2025-03で同時に有効なので、集合は1〜30でなければならない。既存4行が1〜4を使用しているため、施設variantを5〜7、(Ⅴ)通常を8〜21、(Ⅴ)施設を22〜30とした。施設行と通常行は施設区分条件で排他なので、値そのものは算定結果に影響しない（validator の帳簿上の要求）。

## 選択肢

### A: 施設行にだけ条件を付ける（不採用）

施設variant行にのみ `facility-classification-designated-support-facility-r6-06` を付け、通常行を変更しない。実装は最小だが、施設事業所が option 2 を届け出ると通常行（office-capability 条件のみ）と施設行（office-capability ＋ facility-classification）の**両方**が一致する。`ServiceCodeResolver.ResolveAdditions` は加算family特有の設計として複数一致をそのまま複数要素で返す（`AmbiguousMatch` 例外は投げない）ため、**処遇改善加算が二重計上される**。不採用。

**これが「片側投入では済まない」ことの根拠であり、後述する fail-close の帰結を回避する手段が存在しないことの根拠でもある。**

### B: (Ⅴ)をR8-06にも投入する（不採用・前提が誤り）

ADR 0045 が想定していた選択肢。「(Ⅴ)の適用期間」節の3点により、R8-06に投入すべき(Ⅴ)は存在しないことが確定したため、この選択肢自体が成立しない。

### C: (Ⅴ)を band 条件だけで判定する（不採用）

`capability-treatment-improvement-v-band-{n}` だけを `conditionSelectors` に置く。validator を改修せずに済むが、「band は立っているが option 6 は立っていない」という部分的な体制届状態で(Ⅴ)が解決してしまう。算定精度に直結するため不採用（決定4）。

### D: 通常行・施設行の両方に排他的な条件を付ける（採用）

ADR 0047 の選択肢Cと同じ。resolver の意味論を変えず、マスタデータの条件付けだけで多重一致を防ぐ。

## 影響

### 1. 2024-06〜2026-05 の請求は施設区分の入力が必須になる（過去月へ遡る）

**本ADR以降、`OfficeClaimProfile.FacilityClassification` が未入力（NULL）のまま処遇改善(Ⅰ)/(Ⅲ)/(Ⅳ)（体制届 option 2/4/5）を宣言している事業所は、2024-06〜2026-05 の全月について preview も再確定もできない。** `ServiceCodeResolver.EvaluateFacilityClassification` が `ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved` で例外を投げ、`ServiceCodeResolutionException` は `Tsumugi.Application` にも `Tsumugi.App` にも捕捉箇所が無いため、そのまま伝播する。

**これは指定障害者支援施設に限らない。** `general`（非施設）も有効な解決可能値であり、fail-close するのは**未入力**のときだけである。施設区分を入力すれば施設・非施設のどちらも正しく解決する。

**Phase 3-5（ADR 0047）の同種の帰結より影響が広い。** ADR 0047 のfail-closeは 2026-06 以降＝これからの確定にしか及ばなかったが、本ADRは **2024-06 まで遡る**。すなわち **既に旧挙動（無音の過少請求）で確定済みの過去月について、訂正や再確定を開こうとした時点で例外に当たる**。UI層に上流ガードは無く、`ClaimPreviewPipeline` 等の呼び出し元で未捕捉例外として表面化する。

**それでもこれを選ぶ**: 決定2の理由により、通常行に非施設条件を付けずに施設行だけを足すことは二重計上になるため成立せず、片側投入という逃げ道が存在しない。したがって選択肢は「無音の過少請求を続ける」か「未入力を fail-close する」の2つしかない。Global Constraints「確定できない場合はfail-close側へ倒す」に従い後者を採る。**運用上は、2026-05以前の月を扱う可能性があるすべての事業所について、施設区分を先に入力しておく必要がある。**

確定済み請求そのものは変わらない（ADR 0026・0029 が確定時点の入力・規則・版・出典を `InputSnapshotJson` / `CalculationSnapshotJson` として不変に保持する）。影響を受けるのは**未確定のプレビューと、これから行う再確定**である。

### 2. (Ⅴ)の失効が可視化される

(Ⅴ)は 2025-03 で失効する。2025-04以降も option 6 を届け出たままの事業所は、加算が0円になる。本ADRだけではこれは無音のままなので、[ADR 0049](0049-office-capability-master-coverage-check.md) の体制届option存在検査が同時に導入され、**警告として可視化される**（確定はブロックしない）。2026-06以降に option 6 を届け出ている事業所も同じ警告の対象になる。

### 3. validator の2箇所の改修が必要だった

(Ⅴ)の二重ゲート（決定4）は、既存の `ClaimMasterFileValidator` では load 時に fail-close して成立しなかった。いずれも**本ADR以前は一度も行使されていなかった実装上のギャップ**であり、seed データの誤りではない。

1. **`ValidateConditionIntersection` の grouping**: 同一 `ClaimConditionKind` の条件はトークン値が交差することを要求していた。`FacilityClassification` や `RewardSystem` のような真に単一値の次元では正しいが、`OfficeCapability` では誤りである。`ClaimBillingConditionContext.OfficeCapabilityKeys` は `HashSet<string>` であり、独立した one-hot フラグを同時に複数保持しうる（ADR 0021）。「option 6 が選択されている」と「band 1 が選択されている」は**別のフィールド**であり、両方を要求するのは正常な AND である。`OfficeCapability` に限り、グループキーを `Kind + token-family`（トークンの最後の `.` までの部分文字列。`mhlw.b46.capability.treatment-improvement` と `mhlw.b46.capability.treatment-improvement-v-band`）へ精密化した。同一フィールドの真の衝突（例: (Ⅰ)と(Ⅳ)の誤併記）は引き続き検出する。
2. **`ValidateConditionToken` の `OfficeCapability` トークン形状**: 1 の family 分割が意味を持つには、トークン形状が固定されている必要がある。`StartsWith("mhlw.b46.capability.")` という接頭辞検査から、`mhlw` / `b46` / `capability` / `<field>` / `<option>` の**ちょうど5セグメント**を要求する検査へ強化した。短形（`mhlw.b46.capability.peer-support`）は family が `mhlw.b46.capability` へ潰れて過剰grouping を起こし、過剰修飾形（`…treatment-improvement.6.a`）は別 family へ分かれて真の衝突を見逃す。既存の30個の `office-capability` トークン値すべてが5セグメント形であることを確認したうえで強制した。

### 4. `calculationOrder` の値域拡大が literal guard と衝突した

処遇改善familyの `calculationOrder` を 1〜7 から 1〜30 へ広げたことで、11〜23 という値が Domain/Application 中の無関係な数値リテラル（暦月境界・enum判別値・郵便番号長・基準該当B型の法定除数など）と多数衝突し、`ExternalSpecificationLiteralGuard` の `Production_source_keeps_external_specification_literals_in_their_catalogs` が誤検知した。

`calculationOrder` は**公式文書から来る制度値ではなく、validator のための内部帳簿値**である。したがって allowlist（個別の `(path, line, literal)` 例外）ではなく、`officialOptionCode` が既に採っている**スキャナ側の property skip**（`amount` 祖先の直下にある `calculationOrder` はカタログに載せない）で解決した。`amount` 祖先の外にある同名プロパティ、および `amount` 内の他の数値フィールドは引き続きカタログ対象である（3件の回帰テストで固定）。

**将来 `calculationOrder` を大量に増やすseed追加は、同種の衝突を起こしうる**ことを記録しておく。衝突面はseed自身の整合性ではなくコードベース全体の数値リテラル語彙の大きさに比例する。

### 5. テストへの影響

- 新規 `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs`（Task 1 で7件、Task 2 で6件、Task 5 で1件Theory追加）。
- `ClaimAdditionSeedScopeTests` の2件が正当に赤くなり更新した。`R6_fixed_addition_rows_cover_exactly_the_implemented_scope` は月ごとのコード集合を固定しており、施設3コードが 2025-04 にも現れるようになった。`R8_treatment_improvement_rows_apply_only_from_2026_06` は「2026-05→2026-06 の差分」で世代境界を検査していたが、**465138/465140/465141 が世代をまたいで連続するようになったため、この差分は世代越境の欠陥を検出できなくなった**。差分の期待値を `{465174, 465175, 465176}` へ狭めたうえで、**行キー**（世代間で共有されない）で「2026-05 に `b-addition.r8-06.treatment-improvement.*` が存在しないこと」を主張する検査を追加した。
- `ClaimMasterSchemaPhase31Tests` に4件（トークン形状の拒否2件・intersection grouping 2件）、`ClaimSpecificationBoundaryTests` に3件（scanner skip の回帰）を追加した。

## 再検証手順

1. `sources.json` の8文書のURLを取得し、`shasum -a 256` が本ADRの表の値と一致することを確認する。
2. `r6-fee-notice` を `pdftotext -layout` で物理235〜238頁抽出し、**左欄（改正後）**の率が決定1・決定2の表と一致することを確認する。右欄（改正前・「令和６年５月31日までの間」）を読まないこと。
3. `r6-service-codes-2-xlsx` のワークブック順38「18就労継続支援(B・基本)」を `openpyxl` で読み、決定1・決定2 の xlsx 行番号にそれぞれのコードが存在すること、および施設別立てを持たない6区分の直後行がコード未割当の注記のみであることを確認する。
4. `r6-service-codes-2-pdf` 物理259頁を `pdftotext -layout` で抽出し、type-46 の処遇改善コードがちょうど30件で xlsx と同一集合であることを確認する（`ClaimMasterR6FacilityTests.The_r6_treatment_improvement_codes_match_the_official_table_exactly` が同じ主張をテストで固定している）。
5. (Ⅴ)の期間については、`r6-fee-notice` の「令和７年３月31日までの間」・`r8-fee-notice` 物理57頁の「（削る）」・`r8-service-codes-2-xlsx` に 465124〜465137 が無いことの3点をそれぞれ確認する。
6. `r8-capability-correction` 物理9頁の「福祉・介護職員等処遇改善加算（Ⅴ）区分」行に `１．Ｖ（１）`〜`１４．Ｖ（１４）` の14択があることを確認する。**この文書から期間を読まない**（期間の出典は 5 の `r6-fee-notice`）。
