# ADR 0044: 地域区分単価・負担上限額マスタの令和8年6月施行分（R8-06）における適用判断

- 状態: 確定（2026-07-26）
- 関連: [ADR 0020](0020-claim-master-sources-and-versioning.md) / [ADR 0022](0022-burden-cap-master.md) /
  [ADR 0027](0027-r6-basic-reward-service-code-region-price-values.md) / [ADR 0028](0028-r6-major-addition-values.md)

## 結論

`region-unit-prices.json`（地域区分単価8件）と `burden-caps.json`（負担上限額4件）は、いずれも**令和8年6月施行分（R8-06）を裏づける一次資料をAC3-4-1が要求する証跡集合から一意に確定できなかった**。両ファイルの全12エントリについて、`effectiveFrom: "2024-04"` を変更せず、`effectiveTo` を `null` から `"2026-05"` へ設定した（値・`sourceRefs` は無変更）。R8エントリは追加しない。

この結果、`claim-master-r8-06`（2026年6月以降）の請求は、地域区分単価・負担上限額の両方が**未解決でfail-closeする**。これは「値が変わった」ではなく「AC3-4-1の証跡基準でR8を確認できなかった」ことによる意図的な停止である。R6の値をR8へ推測で延長することは行わない。

一方で、本ADRの調査により、**R8-06の証跡集合に厳密には含まれない一次資料からは、両マスタとも値が変わっていない強い状況証拠が得られている**（後述「決定4」）。これは将来のTask（Task 6等）でR8向け証跡を正式登録すれば、fail-closeを解除できる具体的な道筋があることを意味する。本ADRはこの道筋を再現可能な形で記録する。

## 背景

`region-unit-prices.json` と `burden-caps.json` は、`effectiveFrom: "2024-04"` / `effectiveTo: null` のまま2026-06以降も適用され続ける。ADR 0020・0022・0027が確定した既存の出典は次のとおりで、いずれもR8プレフィックスのdocumentIdを持たない。

- 地域区分単価: `mhlw-unit-price-notice-observed-946c3d96`（厚生労働省告示第539号「こども家庭庁長官及び厚生労働大臣が定める一単位の単価並びに厚生労働大臣が定める一単位の単価」の現行条文観測）＋ `r6-revision-overview`（令和6年度改定概要）
- 負担上限額: `r6-disability-support-guide-202404`（障害者総合支援法 地域社会における共生の実現に向けて 2024年4月版）

CSV生成の仕組み（schema・resolver・readiness・CSV writer）は完成しているため、2026-06以降の請求は**エラーを出さずにこれらR6値で生成されうる**。他のマスタギャップ（例: R8-06改定対象の新12区分基本報酬、R8-06の処遇改善加算率）はreadiness gateや経過措置guardでfail-closeするが、この2ファイルだけはそのガードを経由しない。

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8ContinuityTests.cs`（本ADRの実装）は、2026-06に到達する全entryが次のいずれかを満たすことを機械検査する。

1. `sourceRefs` に、R8-06施行分の改定資料束（`r8-fee-notice` / `r8-reward-structure` / `r8-service-codes-2-xlsx` / `r8-service-codes-2-pdf` / `r8-b-reward-band-guide` / `r8-calculation-note` / `r8-capability-202606` の7件、以下「R8改定資料束」）のいずれかを1件以上持つ。
2. `effectiveTo` で2026-06より前に適用期間を閉じている。

この7件は、`sources.json` に既に登録済みの、令和8年6月施行分の改定パッケージを構成する文書群である（`mhlw-r8-structure-page-observed-*` が示す厚生労働省の公式ページ「報酬算定構造・サービスコード表等」の掲載物と一致する。決定3参照）。

## 一次資料の同一性検証（2026-07-26実施）

R8改定資料束7件のうち、AC3-4-1の対象になりうる6件（`r8-service-codes-2-xlsx` は分割2 PDF/Excelの一方を確認すれば十分と判断し、PDF側で代表）と、追加で確認した2件（`r8-revision-overview`・`r8-qa-v1`・`r8-amendment-qa` は範囲外だが念のため確認）を含む計8件を再取得し、SHA-256が `sources.json` の登録値と一致することを確認した。不一致は0件。

| documentId | sha256（先頭12桁） | 照合 | 本ADRでの用途 |
| --- | --- | --- | --- |
| r8-fee-notice | f4b7a05e33b5 | 一致 | 地域単価・負担上限の値/表の非存在を確認（改正告示だが対象外） |
| r8-reward-structure | 8c2638482364 | 一致 | 地域単価・負担上限の値/表の非存在を確認 |
| r8-service-codes-2-pdf | 0ff507138037 | 一致 | 地域単価・負担上限の値/表の非存在を確認（コード表に地域単価表なし） |
| r8-b-reward-band-guide | 96b002a6aecf | 一致 | 地域単価・負担上限の値/表の非存在を確認 |
| r8-calculation-note | 0c4f357f4dfd | 一致 | 地域単価・負担上限の値/表の非存在を確認（級地の例示計算1件のみ、対象サービス種類が異なり照合に使えない） |
| r8-capability-202606 | 84ff0b3b34c2 | 一致 | 「地域区分」欄が事業所の**自己申告フィールド**として存在することのみ確認（単価表ではない） |
| r8-revision-overview | 91bb6b34e196 | 一致 | 範囲外（AC3-4-1のR8改定資料束に含まれない）。参考確認のみ |
| r8-qa-v1 / r8-amendment-qa | e2b95e451418 / 68811f401647 | 一致 | 範囲外。参考確認のみ |

## 抽出方式と2方式の一致確認結果

上記8件を `pdftotext -layout` と `pdftotext -raw` の2方式で独立抽出し、地域単価（`1114`/`1091`/`1086`/`1068`/`1057`/`1034`/`1017`/`級地`/`地域区分`）と負担上限額（`9,300`/`37,200`/`生活保護`/`低所得`/`負担上限`）のキーワードで両方式を検索した。

- 両方式とも、**負担上限額の実額表（0円/0円/9,300円/37,200円）はいずれの文書にも存在しない**。
- 地域単価について、`r8-calculation-note` のみ「地域区分は１級地」という端数処理の例示（11.20円/単位、居宅介護＝訪問系の例）を含むが、これは就労継続支援の基準額（10円）ではなく訪問系の基準額（8.5円）に基づく別区分の例示であり、本マスタが対象とする `serviceKind: employment-continuation-support` の値（1級地=11.14円）を裏づけない。両方式で同一の該当箇所を確認し、不一致はなかった（＝「値が存在しない」という結論そのものが2方式で一致した）。
- `r8-capability-202606`（Excelの体制状況一覧表）には「地域区分」という**入力欄ラベル**（事業所が1〜7級地・その他を申告する欄）はあるが、級地ごとの単価表は無い。

この結果、R8改定資料束のいずれにも、地域区分単価表・負担上限額表そのものは掲載されていないことを確認した。これは転記ミスではなく、**これら2つの制度値がR8-06の改定パッケージ（報酬算定構造・サービスコード表等）とは別の法令系統で定められているため**である（決定2参照）。

## 決定

### 1. AC3-4-1の証跡基準では両マスタとも「確定不能」と判定する

`ClaimMasterR8ContinuityTests.R8AuthoritativeDocumentIds`（7件）は、令和8年6月施行分の改定パッケージを構成する文書に限定している。上記の検証により、この7件のいずれにも地域区分単価表・負担上限額表が含まれないことが確定したため、AC3-4-1の証跡基準では**両マスタとも分岐(c)（確定不能）**を採る。

- `region-unit-prices.json` の8エントリ（`b-region.r6.region-grade-1`〜`region-grade-7`・`region-other`）: `effectiveTo` を `null` → `"2026-05"`
- `burden-caps.json` の4エントリ（`burden-cap.r6.welfare`・`low-income`・`general-1`・`general-2`）: `effectiveTo` を `null` → `"2026-05"`
- `sourceRefs`・`values` は無変更（R6 entryを書き換えないという制約に従う）。R8 entryは追加しない。

この結果、2026-06以降の請求生成は、`ClaimMasterCsvOfficeContextProvider.UnitPriceMilliYen` が地域区分単価を解決できず `UnitPriceUnresolved` でfail-closeする。同様に負担上限額の解決も2026-06以降は失敗する。

### 2. なぜR8改定資料束に地域単価・負担上限の表が無いのか

厚生労働省の公式ページ「報酬算定構造・サービスコード表等」（令和8年6月施行分。`mhlw-r8-structure-page-observed-9bc71ce6`/`-13da3c44` が観測、2026-07-26に同一URLを再取得しても内容は同じ8項目構成）が列挙する改定パッケージは次の8項目のみである。

1. 事務連絡
2. 体制状況一覧表
3. 実績記録票（変更なし）
4. 請求書明細書（変更なし）
5. 決定サービスごとの設定内容（変更なし）
6. 障害福祉サービス費等の報酬算定構造（`r8-reward-structure`）
7. 介護給付費等単位数サービスコード（`r8-service-codes-*`）
8. 請求サービスコードと決定サービスコード対応表（`r8-claim-decision-*`）

地域区分単価は、厚生労働省告示第539号（`mhlw-unit-price-notice-observed-946c3d96`。障害福祉サービス全体に共通する一単位の単価を定める独立した告示）で定められ、負担上限額は障害者総合支援法施行令および関連通知（こども家庭庁・厚生労働省が別途発出する利用者負担認定の手引き等）で定められる。**いずれも3年ごとの報酬改定パッケージの一部ではなく、別の法令・通知系統に属する**。ADR 0022も「r8-grant-decision-administration-202606 物理112頁は制度額を別資料参照としており、金額自体の根拠には使わない」と記録しており、本ADRの発見と整合する。

### 3. 参考: R8改定資料束の外で得られた継続の状況証拠（fail-closeの判断は変えない）

AC3-4-1の証跡基準を満たさないため上記の決定1・fail-closeは変えないが、次の2件は将来の解除判断に資する具体的な手がかりである。**いずれもsources.jsonへの正式登録は行っていない**（branch (c)ではR6 entryの`effectiveTo`設定のみを行い、新規sourceRefsを追加しないため）。

**(a) 地域区分単価**: `mhlw-unit-price-notice-observed-946c3d96` のURL（`https://www.mhlw.go.jp/web/t_doc?dataId=83aa8493&dataType=0&pageNo=1`）を2026-07-26に再取得したところ、sha256は `946c3d969ffd4128db15106d25ce6d26ff108f5460a7618e3df96352e42c0c1b`（52,785 bytes）で、**2026-07-10に登録済みの値とバイト完全一致**した。両取得日とも令和8年6月1日（R8-06施行日）より後である。ページ本文の改正履歴表示は「(令五厚労告一六七・改称)」（令和5年）が最新であり、令和8年の改正注記は無い。ADR 0020は既に「令和8年告示第5号は告示第539号を改正対象としておらず、令和8年度改定ページと令和8年6月施行分ページにも代替の一単位単価表は掲載されていない」と記録しており、本ADRの再取得はこの結論を追認する。ただし、この文書のdocumentId（`mhlw-unit-price-notice-observed-946c3d96`）はAC3-4-1のR8改定資料束7件に含まれないため、機械検査上は「確認済み」と扱えない。

**(b) 負担上限額**: こども家庭庁の事務処理要領ページ（`https://www.cfa.go.jp/policies/shougaijishien/shisaku/jimushori_yoryo`）から、r6-disability-support-guide-202404の後継とみられる「障害福祉サービス・障害児通所支援等の利用者負担認定の手引き【令和8年6月版】」（公開日2026-06-05）を発見した。URL: `https://www.cfa.go.jp/assets/contents/node/basic_page/field_ref_resources/b27810b0-7802-444c-a95d-22841bdf3eca/f823f8c1/20260605_policies_shougaijishien_shisaku_jimushori_yoryo_55.pdf`（2026-07-26取得、sha256 `ad24221afabc3b07e5f28602662d68dd7a44ea905de066375c209be0f101d522`、2,351,497 bytes）。本文11頁「（４）【負担上限月額について】」の表は、生活保護＝０円、低所得１・２＝０円、一般１（居宅で生活する障害者並びに20歳未満の障害者施設入所者及び障害児施設入所者）＝9,300円、一般２＝37,200円と、**現行seedと完全一致する値**を掲載する。ただしこの文書もAC3-4-1のR8改定資料束7件に含まれず、こども家庭庁発行でありADR 0020のsources.jsonカタログにも未登録のため、機械検査上は「確認済み」と扱えない。

### 4. 「値の確定不能」と「値の変更」は区別する

本ADRの結論は「R8で値が変わった」ではない。(a)(b)の状況証拠はむしろ「変わっていない」ことを強く示唆する。それでもfail-closeを選ぶのは、AC3-4-1が要求する証跡基準（R8-06改定パッケージの一次資料からの一意確定）を、この2つの制度値について満たせないためである。CLAUDE.mdハード制約3は「公式資料から一意に確定できない値は推測で埋めず docs/open-questions.md へ」と定めており、状況証拠だけで機械検査の基準を緩めることは、この制約の精神に反する。

## 選択肢

### A: 出典なしで継続する（不採用）

現状（`effectiveTo: null` のまま）を維持する。実装コストはゼロだが、これは本タスクが解消すべき問題そのものである。R8で改定されていないことを確認しないまま値を使い続けると、万一R8で改定されていた場合に**エラーを出さずに誤った金額で請求が生成される**（サイレント誤請求）。事業所はCSVが出せないことには気付けるが、単価が古いことには気付けない。誤った金額を静かに生成するより生成を止める方が回復可能であるため、不採用とする。

### B: ADR 0020のprose reasoningおよび決定3の状況証拠だけで継続する（不採用）

決定3(a)(b)の状況証拠は強いが、いずれもAC3-4-1の証跡基準（R8-06改定パッケージ内の一次資料）を満たさない。`ClaimMasterR8ContinuityTests` はこの基準を機械的に検査するテストであり、基準外の証拠で `unbacked` 判定を回避することは、テストの意図（宣言された証跡集合に基づく機械検証）を損なう。将来この2文書を正式に証跡台帳へ登録し、AC3-4-1のR8改定資料束（またはそれに準ずる検証済み文書リスト）へ加えるという形でなら採用しうるが、それは本タスクの範囲を超える別タスクの決定である。

### C: R6エントリの適用期間を2026-05で閉じ、R8エントリを追加しない（採用）

2026-06以降の請求は地域区分単価・負担上限額が未解決でfail-closeする。事業所は請求を生成できないことで問題に気付ける。決定3の手がかりにより、将来の解除作業は「新規文書の発見」からではなく「既発見の2文書を正式に証跡登録する」ところから始められる。

## 決定表

該当なし。本ADRはR8向けの制度実値を確定していない（決定1参照）。

## 影響

- `claim-master-r8-06`（2026年6月以降）の請求生成は、地域区分単価・負担上限額の解決不能により`ExportClaimCsvUseCase`がfail-closeする。実装済みの`ClaimMasterCsvOfficeContextProvider.UnitPriceMilliYen`（`src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterCsvOfficeContextProvider.cs`）は変更していない。既存のfail-close経路（`ClaimCsvGenerationReason.UnitPriceUnresolved`）がそのまま機能する。
- **既存テストへの影響**: `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportProductionWiringTests.cs` の5テスト（`ServiceMonth(2026, 6)`〜`(2026, 10)` を使う`Real_wiring_generates_cp932_csv_and_appends_the_export_history`・`Real_wiring_writes_the_processing_month_independently_from_the_service_month`・`Real_wiring_is_byte_deterministic_for_the_same_finalized_batch`・`Real_wiring_does_not_append_history_when_generation_fails`・`Real_wiring_fails_closed_when_the_finalized_snapshot_has_no_contract_information`）は、地域区分単価が2026-06以降に未解決となったことで失敗するようになった。これは意図した挙動変化であり、本タスクの範囲では**これらのテストを弱めない**（テスト日付を変更しない、期待値を`UnitPriceUnresolved`へ書き換えない）。2026-06以降のproduction wiring挙動をテストで再確認する作業は、R8向け証跡登録を行う後続タスク（Task 6想定）で扱う。
- ADR 0022が述べる「5-release source chain」（`claim-master-r6-04`〜`claim-master-r8-06`の各版が参照するdocumentId束）は、**出典カタログの連続性**（どの文書を参照しうるかの記録）であって、**seed実値がR8-06向けに機械検証済みであることを意味しない**。本ADR以降、地域区分単価・負担上限額に関しては、release chainへの文書列挙とは独立に、`ClaimMasterR8ContinuityTests`が実際のseed値の適用期間を機械検査する。
- ADR 0020・0022の既存の prose reasoning（決定3(a)(b)に対応する箇所）は誤りではないが、AC3-4-1の機械検証基準を満たす形には未整備であることが判明した。今後、他の版境界（例: 令和9年度改定）でも同様の「出典連鎖はあるが機械検証済み証跡が無い」状態が再発しうるため、新しい施行分を迎えるたびに`ClaimMasterR8ContinuityTests`相当の網羅検査を先に書く運用を推奨する。

## 再検証手順

1. `sources.json` のR8改定資料束7件（`r8-fee-notice`・`r8-reward-structure`・`r8-service-codes-2-xlsx`・`r8-service-codes-2-pdf`・`r8-b-reward-band-guide`・`r8-calculation-note`・`r8-capability-202606`）のURLを取得し、`shasum -a 256` が登録値と一致することを確認する。
2. `pdftotext -layout` と `pdftotext -raw` で全文抽出し、地域区分単価（`級地`/`地域区分`/`1114`等の分子）と負担上限額（`負担上限`/`9,300`/`37,200`/`生活保護`/`低所得`）のキーワードを検索する。本ADR時点ではいずれも実額表は見つからない。
3. 決定3(a)の`mhlw-unit-price-notice-observed-946c3d96`のURLを再取得し、sha256が変化していないか（＝告示539号が改正されていないか）を確認する。変化していれば、地域単価は分岐(b)（改定あり）に切り替える可能性がある。
4. 決定3(b)のこども家庭庁「利用者負担認定の手引き」の最新版URLを確認し、令和8年6月版以降の版で負担上限額表の値を確認する。値が変わっていなければ、この文書を正式に`sources.json`へ登録し、`ClaimMasterR8ContinuityTests.R8AuthoritativeDocumentIds`（またはAC3-4-1の後継AC）へ追加する形で分岐(a)へ切り替えられる。
5. ハッシュ不一致または値の不一致を検出した場合は、値を使わず`docs/open-questions.md`に起票して停止する（ADR 0020と同じ運用）。
