# Phase 3-6 設計spec — R6-06世代 処遇改善加算の施設区分variantと(Ⅴ)の実値投入、体制届optionの存在検査

> **Status**: 設計合意済（2026-07-26）
> **正本**: 本書。実装計画は `docs/superpowers/plans/2026-07-26-phase3-6-r6-06-treatment-improvement.md`。
> **前提**: Phase 3-5（ADR 0047）完了。R8-06 については施設区分の解決が既に成立している。

---

## 1. 目的

3つを同時に解消する。

1. **R6-06世代（2024-06〜2026-05）の処遇改善加算に施設区分の別立てが無い**ため、指定障害者支援施設が通常行へ無音で解決し**過少請求**が成立する（`docs/open-questions.md` 起票済み、`docs/phase3-5-acceptance.md` §8-5）。Phase 3-5 がR8-06について塞いだのとまったく同じクラスの欠陥。
2. **処遇改善(Ⅴ)が一切seedされていない**ため、選択番号6を届け出た事業所は**警告なく加算が0円**になる（無音の未算定。ADR 0045が未確定として保留）。
3. 上記2の一般形として、**宣言された体制届optionに対応する有効なマスタ行が当月に存在しない場合に無音で0円になる**という構造的な穴（`OfficeCapability` を参照する全加算に共通）。

---

## 2. 一次資料調査の結果（2026-07-26 実施）

### 2.1 照合した文書

いずれも `sources.json` 登録済みの documentId で、**再取得して SHA-256 が登録値と完全一致すること**を確認した。

| documentId | SHA-256 | 用途 |
|---|---|---|
| `r6-fee-notice` | `5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54` | R6の率（authoritative） |
| `r6-service-codes-2-xlsx` | `4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82` | R6のサービスコード（authoritative） |
| `r6-service-codes-2-pdf` | `708270200599de9fb7d15d7270997286c3671d378e0a00e3b186a946e67b4465` | 同上の2形式独立照合（cross-check） |
| `r8-fee-notice` | `f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c` | 改正前欄によるR6値の独立確認、(Ⅴ)削除の確認 |
| `r8-service-codes-2-xlsx` | `307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049` | R8に(Ⅴ)コードが無いことの確認 |
| `r8-capability-correction` | `06414c8aad4c014f44fd211dac141d152f30135fb622cdd32874e1c6bccbd980` | (Ⅴ)区分の体制届項目の確認 |

### 2.2 R6-06 の率（`r6-fee-notice` 改正後欄・物理235〜238頁）

物理235頁に (Ⅰ)、236頁に (Ⅱ)(Ⅲ)(Ⅳ) と (Ⅴ)⑴〜⑶、237頁に (Ⅴ)⑷〜⒀、238頁に (Ⅴ)⒁。

`r8-fee-notice` 物理57頁の**改正前欄**が同じ値（通常 93・91・76・62、施設 104・86・69）を示し、**2つの独立した告示が一致**する。ADR 0045 の「抽出方式と2方式の一致確認結果」節の観測が正しかったことを確認した。

### 2.3 サービスコードの実在（2形式独立照合）

`r6-service-codes-2-xlsx` ワークブック順38「18就労継続支援(B・基本)」と `r6-service-codes-2-pdf` 物理259頁を独立に抽出し、**同一のコード集合・同一の欠番**を得た。PDF側は type-46 の処遇改善コード30件がすべて物理259頁の1頁に収まる。

**告示の括弧書き「指定障害者支援施設にあっては」の有無と、コード表の施設行の有無が完全に一致する。** (Ⅱ)・(Ⅴ)⑶⑷⑹⑼⑿ の6区分は告示に括弧書きが無く、コード表でも「指定障害者支援施設において行った場合」の行は存在するがサービスコード欄が空である。これは抽出漏れではなく制度上の欠番であることの独立した裏づけになる。

### 2.4 処遇改善(Ⅴ)の適用期間 — 2024-06〜2025-03 のみ

3つの独立した証拠が一致する。

1. `r6-fee-notice` の (Ⅴ) 規定本文が「**令和７年３月31日までの間**」と期限を明記する
2. `r8-fee-notice` 物理57頁が同規定を「**（削る）**」として削除する。同notice の改正前欄も期限が「令和７年３月31日」のままであり、**中間の告示で延長されていない**
3. `r8-service-codes-2-xlsx` ワークブック順38 は Excel行2272（`465141`）で終わり、**(Ⅴ)のコード `465124`〜`465137` が存在しない**

**したがって就労継続支援B型の処遇改善(Ⅴ)は 2024-06〜2025-03 にのみ存在する。** ADR 0045 が「R8-06へ(Ⅴ)を投入する必要がある」としていた前提は誤りであり、本specで訂正する。

なお `r8-capability-correction` 物理9頁の体制届（別紙1-1）には `６．Ⅴ` と「福祉・介護職員等処遇改善加算（Ⅴ）区分」の行が残っている。別紙1-1は複数サービス種別で共用する様式であり、B型について(Ⅴ)が算定できることを意味しない。**B型については告示とコード表を正とする。**

### 2.5 (Ⅴ)区分は公式の体制届項目である

`r8-capability-correction` 物理9頁に独立した行「福祉・介護職員等処遇改善加算（Ⅴ）区分（※18 ※20）」があり、`１．Ｖ（１）`〜`１４．Ｖ（１４）` の14択が定義されている。**サブ区分は本アプリが発明する入力ではなく、選択番号を持つ公式の体制届項目である。**

---

## 3. 決定

### 決定1: (Ⅴ)区分は `OfficeCapability` で受ける

体制届の1行そのものであるため、ADR 0021 の「公式体制項目＋選択番号によるone-hotキー」の枠に載せる。`OfficeClaimProfile` 側に置くと公式選択番号を独自enumへ写す変換が1段増える。

`OfficeCapability.Flags` は `IReadOnlyDictionary<string, bool>` であり、`ClaimMasterFileValidator` は `mhlw.b46.capability.` 接頭辞のみを要求する（`ClaimMasterFileValidator.cs:674-678`）。よって**エンティティのmigrationは不要**で、キーを増やすだけで成立する。

Phase 3-5 の `FacilityClassification` が `OfficeClaimProfile` に置かれたのは、施設区分が体制届に**無い**項目だったためであり、本件とは事情が異なる。一貫性の欠如ではない。

### 決定2: 施設条件は通常行と施設行の両側へ付ける

ADR 0047 が確立した方式に従う。片側（施設行の追加）だけだと施設事業所が通常行と施設行の**両方に一致し二重計上**になる。

### 決定3: R6世代の施設区分条件を別途立てる

既存の `facility-classification-general` / `-designated-support-facility` は `effectiveFrom: "2026-06"` であり、`ClaimMasterFileValidator` の「conditionDefinitionの有効期間が参照元行の有効期間を覆っていること」検査を通らない。**既存条件の期間を書き換えず**、R6世代用に別キーを立てる。

### 決定4: 体制届optionの存在検査は「警告」とする

確定はブロックしない。`docs/open-questions.md` の起票文言が警告であること、ADR 0041 が「将来版との差分は警告とし `IsReady` を変えない」前例を作っていることによる。

### 決定5: (Ⅴ)区分と処遇改善対象optionの組合せ検証は行わない

option 6 以外を届け出た事業所が(Ⅴ)区分を入力できてしまうが弾かない。施設区分と体制届optionの組合せ検証を Phase 3-5 が非スコープとした理由（一次資料の再確認を要する）と同じであり、`docs/phase3-5-acceptance.md` §8-2 の既存課題へ合流させる。

---

## 4. 投入するデータ

### 4.1 施設variant 3行（`effectiveFrom: "2024-06"` / `effectiveTo: "2026-05"`）

| 区分 | 率 | serviceCode | xlsx locator | pdf locator |
|---|---|---|---|---|
| (Ⅰ)施設 | `0.104` | `465138` | `workbook-order=38;row=1062` | `p.259` |
| (Ⅲ)施設 | `0.086` | `465140` | `workbook-order=38;row=1066` | `p.259` |
| (Ⅳ)施設 | `0.069` | `465141` | `workbook-order=38;row=1068` | `p.259` |

(Ⅱ) は告示に括弧書きが無くコードも無いため対象外。

キー: `addition.treatment-improvement.unified.{i,iii,iv}.facility` / `b-addition.r6-06.treatment-improvement.unified.{i,iii,iv}.facility`。

### 4.2 処遇改善(Ⅴ) 23行（`effectiveFrom: "2024-06"` / `effectiveTo: "2025-03"`）

| サブ区分 | 通常率 | 通常code | 通常row | 施設率 | 施設code | 施設row |
|---|---|---|---|---|---|---|
| ⑴ | `0.080` | `465124` | 1069 | `0.091` | `465142` | 1070 |
| ⑵ | `0.079` | `465125` | 1071 | `0.087` | `465143` | 1072 |
| ⑶ | `0.078` | `465126` | 1073 | — | — | — |
| ⑷ | `0.077` | `465127` | 1075 | — | — | — |
| ⑸ | `0.066` | `465128` | 1077 | `0.074` | `465146` | 1078 |
| ⑹ | `0.064` | `465129` | 1079 | — | — | — |
| ⑺ | `0.061` | `465130` | 1081 | `0.066` | `465148` | 1082 |
| ⑻ | `0.063` | `465131` | 1083 | `0.073` | `465149` | 1084 |
| ⑼ | `0.059` | `465132` | 1085 | — | — | — |
| ⑽ | `0.048` | `465133` | 1087 | `0.053` | `465151` | 1088 |
| ⑾ | `0.049` | `465134` | 1089 | `0.056` | `465152` | 1090 |
| ⑿ | `0.046` | `465135` | 1091 | — | — | — |
| ⒀ | `0.044` | `465136` | 1093 | `0.048` | `465154` | 1094 |
| ⒁ | `0.031` | `465137` | 1095 | `0.035` | `465155` | 1096 |

row は `r6-service-codes-2-xlsx` の1始まりExcel行番号（既存seedの locator 規約と同一。既存 `unified.i` の `row=1061` が `465120` を指すことで検証済み）。

キー: `addition.treatment-improvement.unified.v-{1..14}` および `...v-{n}.facility`。

### 4.3 完全性の機械的チェック

既存4行（`465120`〜`465123`）＋ 本specの26行 ＝ **30行**は、`r6-service-codes-2-pdf` 物理259頁に現れる type-46 処遇改善サービスコード30件と**過不足なく一致する**。この集合一致をテストで固定する。

### 4.4 追加する条件定義 17件

| key | kind | 期間 | value |
|---|---|---|---|
| `facility-classification-general-r6-06` | `facility-classification` | 2024-06〜2026-05 | `general` |
| `facility-classification-designated-support-facility-r6-06` | `facility-classification` | 2024-06〜2026-05 | `designated-support-facility` |
| `capability-treatment-improvement-v` | `office-capability` | 2024-06〜**2025-03** | `mhlw.b46.capability.treatment-improvement.6` |
| `capability-treatment-improvement-v-band-{1..14}` | `office-capability` | 2024-06〜**2025-03** | `mhlw.b46.capability.treatment-improvement-v-band.{1..14}` |

(Ⅴ)の各行は `capability-treatment-improvement-v`（option 6）と該当する `-v-band-{n}` の**両方**を `conditionSelectors` に持つ。

### 4.5 変更する既存行 3件

`b-addition.r6-06.treatment-improvement.unified.{i,iii,iv}` の `conditionSelectors` に `facility-classification-general-r6-06` を追加する。

**`unified.ii` は無変更**。施設variantが無いため施設区分に関わらず一致し続ける（R8の(Ⅱ)イ・(Ⅱ)ロと同じ設計）。

**注意（`conditionSelectors` の配列順）**: `docs/phase3-5-acceptance.md` §9 が deferred minor として記録したとおり、`MatchesAll` は `.All` の短絡評価であるためフェイルクローズの発生順が配列順に依存する。施設条件を先頭へ置くと、体制届を提出していない事業所まで `FacilityClassificationUnresolved` で落ちる可能性がある。**施設条件は既存要素の末尾へ追加する**（R8行が `reward-system...` → `capability-...` → `facility-classification-...` の順を採っているのと揃える）。

### 4.6 出典の付け方

全行で既存R6行と同じ4点構成にする。

- `r6-fee-notice` — `unit-rule-value`（率）: authoritative。locator は物理頁（(Ⅰ)=235、(Ⅱ)(Ⅲ)(Ⅳ)と(Ⅴ)⑴〜⑶=236、(Ⅴ)⑷〜⒀=237、(Ⅴ)⒁=238）
- `r6-service-codes-2-xlsx` — `service-identity` ほか: authoritative
- `r6-service-codes-2-pdf` — 同上: cross-check
- `r6-calculation-note` — `unit-rule-rounding`: authoritative

施設行の `r6-fee-notice` locator には、ADR 0047 の R8 施設行と同じく括弧書きを引用する。ただし**率の数値そのものを locator に埋めない**（`docs/phase3-5-acceptance.md` §9 の deferred minor「率の値が locator に埋まっている」を繰り返さない）。

---

## 5. (Ⅴ)区分の入力

`OfficeCapabilityView` に14択の ComboBox を1つ追加し、`OfficeCapabilityViewModel` が `Flags` へ one-hot キーを書く。選択肢の表示文字列は体制届の表記（`Ｖ（１）`〜`Ｖ（１４）`）に合わせる。

エンティティ・migration の変更は不要（決定1）。

`ViewInputWiringTests` の検査対象に含める。既存の施設区分・人員配置区分と同じ扱い。

---

## 6. 体制届optionの存在検査（恒久readinessチェック）

### 6.1 判定

処理対象年月と事業所の宣言済み `OfficeCapability.Flags`（値が `true` のキー）を入力に、キーごとに次を判定する。

1. そのキーを `value` に持つ条件定義が**登録済みマスタのどの期間にも存在しない** → **無視**。請求に効かない体制届項目（算定に関与しないキー）であり、警告すると偽陽性になる。
2. そのキーを `value` に持つ条件定義が**他の期間には存在するが、処理対象年月に有効なものが無い**、または有効な条件定義はあるが**それを参照するサービスコード行が処理対象年月に無い** → **警告**。

2段構えにすることで「失効した」「まだ施行されていない」という本当の穴だけを拾う。

### 6.2 対象となる既知の経路

- R6で option 6（Ⅴ）を届け出たまま **2025-04〜2026-05** を請求する事業所（(Ⅴ)は2025-03で失効）
- **2026-06以降**に option 6 を届け出ている事業所（B型に(Ⅴ)が存在しない）
- 将来、同種の期間境界が生じた任意の加算

### 6.3 位置づけ

警告であり `IsReady` を変えない（決定4）。処遇改善に限らず `OfficeCapability` を参照する全加算に効く。`docs/open-questions.md` の該当項目をクローズする。

---

## 7. この変更が生む帰結

1. **2024-06〜2026-05 の請求は、`OfficeClaimProfile.FacilityClassification` が未入力だとフェイルクローズする。** `ServiceCodeResolver.EvaluateFacilityClassification`（`ServiceCodeResolver.cs:234-238`）が `FacilityClassificationUnresolved` を投げる。通常行に施設条件を付ける以上避けられず、片側だけの投入は二重計上になるため成立しない（決定2）。Phase 3-5 がR8-06で受け入れたのと同じトレードであり、**無音の過少請求よりフェイルクローズを選ぶ**という一貫した判断である。
2. (Ⅴ)は 2024-06〜2025-03 のみ有効。2025-04以降に option 6 のままの事業所は §6 の警告対象になる。
3. ADR 0045 の「R8-06へ(Ⅴ)を投入する必要がある」という前提が誤りだったことを ADR で明示的に訂正する。

---

## 8. テスト方針

### 8.1 主な主張

- R6-06 の施設事業所が (Ⅰ)(Ⅲ)(Ⅳ) で施設コード・施設率へ**一意に**解決する（production token provider 経由の end-to-end）
- 通常事業所が通常コード・通常率へ解決し、施設行に一致しない
- 施設区分未入力は `FacilityClassificationUnresolved` でフェイルクローズする
- (Ⅴ)14サブ区分が `[Theory]` で全件解決する（通常）／施設variantを持つ9区分が施設コードへ解決する
- (Ⅴ)は 2025-03 で失効し、2025-04 では解決しない
- 2026-06以降の option 6 は解決しない
- §4.3 の集合一致（30コード）
- §6 の警告が2経路で出ること、および請求に効かないキーでは出ないこと（偽陽性の不在）

### 8.2 歯の確認（意図的違反でREDになることを確認する）

| 変更 | 期待 |
|---|---|
| 施設行を1行削除 | 集合一致テストがRED |
| 率を1単位変更（例 `0.104`→`0.105`） | 率固定テストがRED |
| 通常行から `facility-classification-general-r6-06` を外す | 施設事業所が2行に多重一致してRED |
| (Ⅴ)行の `effectiveTo` を `2025-03`→`2026-05` へ伸ばす | 2025-04の解決が変わってRED |
| §6 の検査を無効化 | 警告テストがRED |

確認後はバックアップと `git diff --stat` で復元前の状態と完全一致することを確認する。

### 8.3 既存テストへの波及（着手時に実測する）

`unified.{i,iii,iv}` の `conditionSelectors` を変更するため、これらの行の条件集合を直接assertしている既存テストが赤くなりうる。また `JsonClaimMasterProvider.LoadEmbedded()` を呼ぶ全テストは条件定義の追加に反応する（Phase 3-4 Task 3 で89件が一斉に赤くなった前例がある。`docs/phase3-4-acceptance.md` §5-3）。**条件定義だけを先行コミットすると「未参照のconditionDefinitionはfail-close」で必ず赤くなるため、条件定義・行・参照は1コミットで揃える。**

---

## 9. ADR

| 番号 | 内容 |
|---|---|
| `0048` | R6-06世代 処遇改善加算の施設区分variantと(Ⅴ)の実値投入、(Ⅴ)の適用期間（2024-06〜2025-03）、ADR 0045の前提の訂正 |
| `0049` | 体制届optionに対応するマスタ行の存在検査（恒久readinessチェック） |

いずれも「暫定→確定」ではなく**初手から確定**として書く。

---

## 10. 非スコープ

- **(Ⅴ)区分と処遇改善対象optionの組合せ検証**（決定5）。`docs/phase3-5-acceptance.md` §8-2 の既存課題へ合流。
- **R8-06向けの定員超過・生活支援員等欠員・サービス管理責任者欠員3シートの実値投入**。`docs/open-questions.md` の別項目のまま。
- **体制届option 10（生産活動支援）と参加評価型の対応**、**option 8（filed-transition）**。いずれも一次資料未確定の既存項目。
- **Phase 3-3 より前に確定した請求の再確定**。ADR 0032・0034 の既知の制約。

---

## 11. `docs/open-questions.md` への反映

- 「R6-06世代の処遇改善に施設区分の別立てが無い」 → **クローズ**
- 「R8処遇改善(Ⅴ)の実値投入」 → **クローズ**（R6分を投入し、R8には存在しないことを確定したため）
- 「体制届optionに対応するマスタ行が当月に存在しない場合のreadiness警告」 → **クローズ**
- 「利用定員・人員配置区分の実データ源」 → **クローズ**（Phase 3-1 で実装済みであり記述が陳腐化していた。`OfficeClaimProfile` の列・migration `Phase31OfficeClaimBillingTokens`・`OfficeClaimBillingTokenProvider.cs:87`・`ClaimInputViewModel.cs:456` で確認）
