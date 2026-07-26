# ADR 0047: 指定障害者支援施設variantの処遇改善4区分の実値投入

- 状態: 確定（2026-07-26）
- 関連: [ADR 0021](0021-office-capability-official-codes.md) / [ADR 0025](0025-claim-rounding-rules.md) /
  [ADR 0026](0026-claim-batch-snapshot.md) / [ADR 0029](0029-claim-snapshot-codec-v2.md) /
  [ADR 0045](0045-r8-treatment-improvement-addition-values.md)

## 結論

ADR 0045が「確定できなかった区分」として持ち越した指定障害者支援施設variantのうち、一次資料から一意に確定できた4区分を`additions.json`・`service-codes.json`へ出典付きで投入する。

| 区分 | 体制届option | 通常コード | 通常の率 | 施設コード | 施設の率 | xlsx行（通常/施設） |
| --- | --- | --- | ---: | --- | ---: | --- |
| (Ⅰ)イ | 2 | 465120 | 1000分の105 | 465138 | 1000分の116 | 2261 / 2262 |
| (Ⅰ)ロ | 7 | 465174 | 1000分の109 | 465176 | 1000分の120 | 2263 / 2264 |
| (Ⅲ) | 4 | 465122 | 1000分の88 | 465140 | 1000分の98 | 2269 / 2270 |
| (Ⅳ) | 5 | 465123 | 1000分の74 | 465141 | 1000分の81 | 2271 / 2272 |

率は**ADR 0045「抽出方式と2方式の一致確認結果」節の表（`r8-fee-notice`を`pdftotext -layout`/`-raw`の2方式で抽出した「指定障害者支援施設の別立て率（参考・本ADRでは未使用）」列）からの転記**である。本ADRで新たに`r8-fee-notice`から率を抽出し直してはいない。サービスコードの実在は`r8-service-codes-2-xlsx`と`r8-service-codes-2-pdf`の2形式で独立に照合した（後述）。

(Ⅱ)イ・(Ⅱ)ロ（体制届option 3・8）は公式に施設別立てが存在しないため、施設区分条件を一切付けない。福祉・介護職員等処遇改善(Ⅴ)（選択番号6・14区分の経過措置）は本ADRのスコープ外として引き続き未投入とする。

## 背景

Task 1（本フェーズの前段）が`FacilityClassification` enum・`ClaimBillingConditionContext.FacilityClassification`（nullable）・`ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved`というDomain側の受け皿を用意した。しかし実際の施設variant行・条件トークンはまだseedされておらず、この受け皿は空である。

この空白は実害を伴う。ADR 0045が投入した6区分の通常行（465120等）は施設区分を問わず無条件に一致する状態だったため、**指定障害者支援施設が体制届option 2を届け出ると、通常行465120＠105/1000で黙って請求が成立していた（正しくは465138＠116/1000）**。これはエラーを出さない過少請求であり、ADR 0045「確定できなかった区分」表・`docs/open-questions.md`の該当項目（2026-07-26追加分）に記録されていた。本ADRはこの穴を、一意に確定できた4区分の範囲で塞ぐ。

## 一次資料の同一性検証（2026-07-26実施）

本タスクで使用した4文書すべてを自分で再取得し、`sources.json`の登録値とSHA-256を照合した。不一致は0件。

| documentId | sha256（先頭12桁） | 照合 | 用途 |
| --- | --- | --- | --- |
| r8-fee-notice | f4b7a05e33b5 | 一致 | 施設別立て率の一次証拠（ADR 0045からの転記の裏付け・自己再検証） |
| r8-service-codes-2-xlsx | 307b631ed91a | 一致 | 施設variantサービスコードの一次証拠 |
| r8-service-codes-2-pdf | 0ff507138037 | 一致 | 上記の独立2形式照合 |
| r8-calculation-note | 0c4f357f4dfd | 一致 | 端数処理（四捨五入）の一次証拠（ADR 0025と同一節を継続参照） |

`sources.json`への追記は不要だった（4文書とも既に`claim-master-r8-06` releaseの`sourceDocumentIds`に登録済み）。

## 2形式独立照合の結果

### サービスコードの実在（xlsx行2262/2264/2270/2272 と PDF物理245頁）

`r8-service-codes-2-xlsx`をワークブック順38番目のシート「18就労継続支援(B・基本)」で`openpyxl`により直接読み、行2261〜2272を確認した。

| 行 | サービスコード（A列×10000+B列） | C列（サービス内容略称） | V列（追加条件欄） |
| --- | --- | --- | --- |
| 2261 | 465120 | 就継Ｂ処遇改善加算Ⅰイ | （空） |
| 2262 | 465138 | 就継Ｂ障害者支援施設処遇改善加算Ⅰイ | 指定障害者支援施設において行った場合 |
| 2263 | 465174 | 就継Ｂ処遇改善加算Ⅰロ | （空） |
| 2264 | 465176 | 就継Ｂ障害者支援施設処遇改善加算Ⅰロ | 指定障害者支援施設において行った場合 |
| 2265 | 465121 | 就継Ｂ処遇改善加算Ⅱイ | （空） |
| 2266 | （A・B・C列とも空欄。コード未割当） | — | 指定障害者支援施設において行った場合 |
| 2267 | 465175 | 就継Ｂ処遇改善加算Ⅱロ | （空） |
| 2268 | （A・B・C列とも空欄。コード未割当） | — | 指定障害者支援施設において行った場合 |
| 2269 | 465122 | 就継Ｂ処遇改善加算Ⅲ | （空） |
| 2270 | 465140 | 就継Ｂ障害者支援施設処遇改善加算Ⅲ | 指定障害者支援施設において行った場合 |
| 2271 | 465123 | 就継Ｂ処遇改善加算Ⅳ | （空） |
| 2272 | 465141 | 就継Ｂ障害者支援施設処遇改善加算Ⅳ | 指定障害者支援施設において行った場合 |

`r8-service-codes-2-pdf`を`pdftotext -layout`で抽出し、物理245頁（`-f 245 -l 245`で該当頁のみ抽出して確認。本PDFは印字頁番号を持たない）で「46 5138 就継Ｂ障害者支援施設処遇改善加算Ⅰイ...指定障害者支援施設において行った場合...単位加算」「46 5176 就継Ｂ障害者支援施設処遇改善加算Ⅰロ...」「46 5140 就継Ｂ障害者支援施設処遇改善加算Ⅲ...」「46 5141 就継Ｂ障害者支援施設処遇改善加算Ⅳ...」の4行を確認した。コード（465138/465176/465140/465141）・略称・「指定障害者支援施設において行った場合」の対応がxlsxと完全一致した。

**行2266・2268（(Ⅱ)イ・(Ⅱ)ロの施設variant相当位置）について**: xlsx・PDFの両方で、この2行はV列（追加条件欄。PDF側は対応する行位置）に「指定障害者支援施設において行った場合」という書式上の見出しは存在するが、コード列（A・B・C列。PDF側は行頭の数字）が空欄であり、サービスコードが割り当てられていない。これはADR 0045が確認した「(Ⅱ)イ・(Ⅱ)ロにあっては施設別立てなし」という率表の記述と、xlsx・PDFの2形式それぞれから独立に一致する。すなわち(Ⅱ)イ・(Ⅱ)ロには「未投入のコードが存在する」のではなく「投入すべきコード自体が存在しない」ことを、率表だけでなくサービスコード表側からも確認した。

## 決定

### 決定表（seed実値。これが値の唯一の出典）

`additions.json`（`masterKind: additions`）に4行を追加する。`effectiveFrom: "2026-06"` / `effectiveTo: null`。率は上掲の「結論」節の表（ADR 0045からの転記）と同一。

| key | percentage | calculationOrder | 転記元（ADR 0045） |
| --- | ---: | ---: | --- |
| `addition.treatment-improvement.r8.i-i.facility` | 0.116 | 7 | 「抽出方式と2方式の一致確認結果」節の表・(Ⅰ)イ行「指定障害者支援施設の別立て率」列 |
| `addition.treatment-improvement.r8.i-ro.facility` | 0.120 | 8 | 同・(Ⅰ)ロ行 |
| `addition.treatment-improvement.r8.iii.facility` | 0.098 | 9 | 同・(Ⅲ)行 |
| `addition.treatment-improvement.r8.iv.facility` | 0.081 | 10 | 同・(Ⅳ)行 |

`calculationOrder`は7〜10で確定（後述「calculationOrder 7〜10の根拠」節）。

`service-codes.json`（`masterKind: service-codes`）に対応する4行を追加する。

| key | serviceCode | officialLabel | conditionSelectors |
| --- | --- | --- | --- |
| `b-addition.r8-06.treatment-improvement.i-i.facility` | 465138 | 福祉・介護職員等処遇改善加算(Ⅰ)イ（指定障害者支援施設において行った場合） | reward-system-employment-continuation-support-b, capability-treatment-improvement-r8-i-i, facility-classification-designated-support-facility |
| `b-addition.r8-06.treatment-improvement.i-ro.facility` | 465176 | 福祉・介護職員等処遇改善加算(Ⅰ)ロ（指定障害者支援施設において行った場合） | 同上（`-r8-i-ro`） |
| `b-addition.r8-06.treatment-improvement.iii.facility` | 465140 | 福祉・介護職員等処遇改善加算(Ⅲ)（指定障害者支援施設において行った場合） | 同上（`-r8-iii`） |
| `b-addition.r8-06.treatment-improvement.iv.facility` | 465141 | 福祉・介護職員等処遇改善加算(Ⅳ)（指定障害者支援施設において行った場合） | 同上（`-r8-iv`） |

officialLabelは、既存行（例: `b-addition.r8-06.treatment-improvement.i-i`のofficialLabel「福祉・介護職員等処遇改善加算(Ⅰ)イ」）に、xlsx V列の追加条件欄の文言「指定障害者支援施設において行った場合」をそのまま括弧書きで付加したものである。

`service-codes.json`の`conditionDefinitions`に2件の`facility-classification`条件定義を追加する。`effectiveFrom: "2026-06"` / `effectiveTo: null`。

| key | value | 出典 |
| --- | --- | --- |
| `facility-classification-general` | `general` | `r8-service-codes-2-xlsx` workbook-order=38;row=2261（追加条件欄が空＝指定障害者支援施設以外） |
| `facility-classification-designated-support-facility` | `designated-support-facility` | `r8-service-codes-2-xlsx` workbook-order=38;row=2262（追加条件欄「指定障害者支援施設において行った場合」） |

**あわせて既存の通常4行（`i-i`/`i-ro`/`iii`/`iv`）の`conditionSelectors`へ`facility-classification-general`を追加する。**`ii-i`（465121）・`ii-ro`（465175）は変更しない。

### トークン命名規約（本ADRが`facility-classification` kindの最初の利用者）

Task 1は`kind: "facility-classification"`という文字列と`ClaimConditionKind.FacilityClassification`の対応づけを`ClaimMasterFileValidator`へ実装済みだったが、実際のトークン・キー命名は本ADRで初めて決める。既存の`r8-reform-status`条件（例: キー`r8-reform-status-target`、値`"reform-target"`。`ClaimConditionKind.R8ReformStatus`のトークン系条件）の命名規約`<kind接頭辞>-<値>`をそのまま踏襲し、次のように定める。

- 条件定義キー: `facility-classification-<値>`（`facility-classification-general` / `facility-classification-designated-support-facility`）
- トークン値（`ClaimBillingConditionContext.FacilityClassification`が持つ文字列、条件定義の`value`と一致）: `general` / `designated-support-facility`
- addition master key: 既存のr8世代キー（`addition.treatment-improvement.r8.<区分suffix>`）へ`.facility`を追加する（`addition.treatment-improvement.r8.i-i.facility`等）
- service-codes master key: 同様に`b-addition.r8-06.treatment-improvement.<区分suffix>.facility`

この命名は`ClaimMasterR8BoundaryTests.Treatment_improvement_additions_switch_generations_at_june_2026`（ADR 0045が新設、本ADRでは変更なし）が要求する「addition行のkeyの`addition.treatment-improvement.r8.`接頭辞を除いた残りが、service-codes行のkeyの`b-addition.r8-06.treatment-improvement.`接頭辞を除いた残りと一致する」という既存の一般規則にも自動的に適合し、施設variant4行についても同テストが1:1対応を機械的に検証する。

### calculationOrder 7〜10の根拠

`ClaimMasterFileValidator.ValidateCalculationOrder`（`ClaimMasterFileValidator.cs:2893-2921`）は、同一`targetSelector`・同一期間で有効な割合加算行の`calculationOrder`が「1から連続かつ一意」であることを要求する（`actual.SequenceEqual(Enumerable.Range(1, actual.Length))`、違反時`must be unique and contiguous from one`）。処遇改善10行（通常6行＋施設4行）はすべて`target.b46.items-1-to-16-4.v1`を共有し2026-06で同時に有効なので、集合は1〜10でなければならない。既存6行（ADR 0045決定表）が1〜6を使用しているため、施設variantは7〜10とした。割当順は体制届option順（2・7・4・5）に対応する区分順（i-i・i-ro・iii・iv）に合わせた。施設行と通常行は`facility-classification`条件により排他なので、`calculationOrder`の値そのものは算定結果に影響しない。これはvalidatorの帳簿上の一意性・連続性の要求である。

### 通常行にも非施設条件を付ける理由

施設variantは通常行と**同じ体制届option**を共有する（例: (Ⅰ)イも施設(Ⅰ)イもoption 2）。施設行にだけ`facility-classification-designated-support-facility`条件を付け、通常行の`conditionSelectors`を変更しなかった場合、指定障害者支援施設がoption 2を届け出ると、通常行（office-capability条件だけで判定）と施設行（office-capability＋facility-classification条件で判定）の**両方**が一致してしまう。`ServiceCodeResolver.ResolveAdditions`は複数一致をそのまま複数要素のリストとして返す設計（加算は複数同時算定が正しい挙動であるため、`ResolveBasicReward`のような`AmbiguousMatch`例外は投げない）ため、この状態は例外にならず**処遇改善加算が二重に計算される**。これを防ぐため、通常4行にも`facility-classification-general`を追加し、通常行と施設行を施設区分で完全に排他にした。

### (Ⅱ)を対象外にする理由

(Ⅱ)イ・(Ⅱ)ロには公式に施設別立てが存在しない。根拠は独立した2点。

1. ADR 0045の率表（`r8-fee-notice`物理57頁の2方式抽出）で、(Ⅱ)イ・(Ⅱ)ロの行だけ「指定障害者支援施設にあっては...」という括弧書きの施設別建て率が存在しない。
2. 本ADRが確認したサービスコード表（xlsx・PDF）で、(Ⅱ)イ・(Ⅱ)ロの施設variant相当位置（xlsx行2266・2268）はコード未割当のプレースホルダである（前掲「2形式独立照合の結果」節）。

(Ⅱ)イ・(Ⅱ)ロの通常行（465121・465175）に施設区分条件を付けると、指定障害者支援施設がoption 3・8を届け出た場合に**一致する行が0件**になり、処遇改善加算そのものが算定できなくなる（無音の未算定）。施設別立てが無い区分では、施設事業所も通常行で算定するのが正しい請求挙動であるため、`ii-i`（465121）・`ii-ro`（465175）の`conditionSelectors`は一切変更しない。

### 既存行のconditionSelectors変更と遡及しないことの根拠

`i-i`/`i-ro`/`iii`/`iv`の4行は、ADR 0045が2026-06から投入済みの既存行である。本ADRはこの4行の`conditionSelectors`へ`facility-classification-general`を追加する（`serviceCode`・`officialLabel`・`unitRule`・`percentage`等の値は一切変更しない）。

この変更が確定済み請求へ遡及しないことは、ADR 0026・ADR 0029の設計により保証されている。ADR 0026は「請求確定後にOffice・Recipient・受給者証・日次記録又は報酬マスタが訂正されても、確定請求は自動で変わってはならない」と定め、確定時点の入力・規則・版・出典を`InputSnapshotJson`・`CalculationSnapshotJson`として不変に保持する（ADR 0026 §1・§5）。ADR 0029のsnapshot codec v2はこの不変性を版付きで再現する。したがって、本ADRによる`conditionSelectors`の追加はマスタの新規解決（未確定の請求・これから確定する請求）にのみ影響し、既存の確定済み`ClaimBatch`のsnapshotには一切影響しない。

### 未入力時の挙動

`ClaimBillingConditionContext.FacilityClassification`が`null`のまま施設variantを持つ区分（option 2・7・4・5）を解決しようとすると、`ServiceCodeResolver`は`ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved`でフェイルクローズする（Task 1実装。本ADRでは変更なし）。(Ⅱ)イ・(Ⅱ)ロ（option 3・8）は施設区分条件を持たないため、`FacilityClassification`が`null`のままでも解決できる。

### 影響（本ADR単独では未解消の運用上のギャップ）

本タスク（Phase 3-5 Task 2）はDomainのマスタ解決層までを対象とし、`OfficeClaimProfile`への施設区分の永続化（Task 3）と、`ClaimCalculationRequestBuilder`が`ClaimBillingConditionContext.FacilityClassification`へ実際の値を渡す結線（Task 4）はまだ実装していない。`ClaimCalculationRequestBuilder.Build`（`src/Tsumugi.Application/Claim/ClaimCalculationRequestBuilder.cs:79-86`）は現状`FacilityClassification`を省略しており（既定値`null`）、`ClaimPreviewPipeline`は解決失敗を捕捉せず伝播させる。したがって、**Task 3・Task 4が完了するまでの間**、処遇改善(Ⅰ)イ・(Ⅰ)ロ・(Ⅲ)・(Ⅳ)（option 2・7・4・5）のいずれかを体制届で宣言している事業所は、施設・非施設を問わず`ClaimPreviewPipeline`経由の請求計算が`FacilityClassificationUnresolved`でフェイルクローズする。これは「指定障害者支援施設が黙って過少請求になる」という本ADR以前の状態から、「該当区分を宣言した全事業所が結線完了まで計算できない」という状態への意図的な変化である。Global Constraints「確定できない場合はfail-close側へ倒す」に沿った判断であり、Task 3・Task 4は同一フェーズ内で継続して実装する前提とする。

## worked example（golden case 3の算出過程）

施設 × (Ⅰ)イ × 2026-06。基本報酬はADR 0045 golden case（cap-20-or-less×band-20000-25000×staff-7.5-1×22日×region-grade-2、462049＝637単位/日、reform-exempt）を再利用し、処遇改善加算だけを通常率(105/1000・465120)から施設率(116/1000・465138)へ差し替える。

```
基本 637×22=14,014単位 ＋ 処遇改善(施設) 14,014×0.116=1,625.624→1,626単位 ＝ 15,640単位
総費用額 15,640×10.91円=170,632.40円→170,632円
1割相当額 170,632×10/100=17,063.2円→17,063円
給付費 170,632−17,063=153,569円
```

`tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`の`Matches_adr_0047_worked_example_designated_support_facility_office_in_june_2026`が上記期待値を固定する。

## 選択肢

### A: 施設行にだけ条件を付ける（不採用）

施設variant行にのみ`facility-classification-designated-support-facility`を付け、通常行の`conditionSelectors`は変更しない。実装が最小で済むが、施設事業所が既存option（例: option 2）を届け出た場合に通常行・施設行の両方が一致し、`ResolveAdditions`が2行を返す（**多重一致**。処遇改善加算の二重計上）。不採用。

### B: resolverに優先順位を導入する（不採用）

`ServiceCodeResolver`へ「施設区分条件を持つ行を優先し、一致した場合は通常行を除外する」という優先順位ロジックを追加する。多重一致自体は防げるが、**十分にテストされた解決器（`ServiceCodeResolver.ResolveAdditions`）の意味論を変える**ことになり、他のすべての加算family（送迎・食事提供体制等）の暗黙の振る舞いにも影響し得る。条件の付け方だけで排他性を表現できる（選択肢C）以上、resolver自体を変更する理由がない。不採用。

### C: 通常行・施設行の両方に排他的な条件を付ける（採用）

通常行に`facility-classification-general`、施設行に`facility-classification-designated-support-facility`を付け、両者を施設区分で完全に排他にする。resolverの意味論は変更せず、マスタデータの条件付けだけで多重一致を防げる。`ClaimMasterR8BoundaryTests.Facility_variants_resolve_to_exactly_one_row_per_classification`が排他性を機械的に検証する。採用。

## 影響（テストへの影響）

- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs`に3件のTheoryを追加した（施設variant4区分の排他一致・(Ⅱ)の非対象確認・施設区分未入力時のfail-close確認）。`ResolveAdditions`は宣言条件に一致する加算行を**すべて**返す設計のため、テストのアサーションは`b-addition.r6.absence-response`（466040・欠席時対応加算。reward-system条件しか持たず常に一致する）等の無関係な加算行を`AdjustmentComponentKey`で除外してから一意性を検証する（`TreatmentImprovementRows`ヘルパ）。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimAdditionSeedScopeTests.cs`の`R8TreatmentImprovementCodes`を6件から10件（施設variant4件を追加）へ拡張し、`R8_treatment_improvement_rows_apply_only_from_2026_06`の新設コード差分主張を`["465174", "465175"]`から6件（施設4件を含む）へ更新した。
- `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`に`Matches_adr_0047_worked_example_designated_support_facility_office_in_june_2026`を追加した。既存の2件（ADR 0045・ADR 0046 worked example）が共有する`R8Masters()`ヘルパは変更せず、独立した`R8FacilityMasters()`ヘルパを新設した（`R8Masters()`へ施設区分条件を追加すると、それを使う既存2ケースが`FacilityClassification`未指定でfail-closeし、無関係な既存golden caseを壊すため）。
- `docs/open-questions.md`の該当項目（2026-07-26追加分）を、施設variant4区分の投入完了を反映して更新し、残る未解決事項（処遇改善(Ⅴ)、およびTask 3・Task 4完了までのランタイム結線ギャップ）を明記した。

## 再検証手順

1. `sources.json`の該当4件（`r8-fee-notice`・`r8-service-codes-2-xlsx`・`r8-service-codes-2-pdf`・`r8-calculation-note`）のURLを取得し、`shasum -a 256`が登録値と一致することを確認する。
2. `r8-service-codes-2-xlsx`のワークブック順38番目のシートで行2261〜2272を`openpyxl`で読み、465138/465176/465140/465141のコードとV列「指定障害者支援施設において行った場合」の対応、および行2266・2268がコード未割当であることを確認する。
3. `r8-service-codes-2-pdf`の物理245頁（`pdftotext -layout -f 245 -l 245`）で同じ4コードと「指定障害者支援施設において行った場合」の対応を確認する。
4. ADR 0045「抽出方式と2方式の一致確認結果」節の表から、(Ⅰ)イ116・(Ⅰ)ロ120・(Ⅲ)98・(Ⅳ)81という施設別立て率を転記していることを確認する（新規抽出ではなく転記であることの確認）。
5. 処遇改善(Ⅴ)を投入する場合は、`r8-capability-correction`の選択肢⑴〜⒁の率対応を新たなADRで確定してから着手する。
