# ADR 0044: 地域区分単価・負担上限額マスタの令和8年6月施行分（R8-06）における適用判断

- 状態: 確定（2026-07-26）
- 関連: [ADR 0020](0020-claim-master-sources-and-versioning.md) / [ADR 0022](0022-burden-cap-master.md) /
  [ADR 0027](0027-r6-basic-reward-service-code-region-price-values.md) / [ADR 0028](0028-r6-major-addition-values.md)

## 結論

`region-unit-prices.json`（地域区分単価8件）と `burden-caps.json`（負担上限額4件）は、**いずれもR8-06（2026-06-01施行）以降も値が変わっていないことを一次資料で直接確認した**。両ファイルの全12エントリは `effectiveFrom: "2024-04"` / `effectiveTo: null` のまま2026-06以降も適用され、各エントリの `sourceRefs` にR8-06時点の適用性を直接裏づける出典を追加で持つ。`values` はR6確定時から一切変更していない。

制度実値の確認方法は、地域単価と負担上限額で異なる2系統である。

1. **地域区分単価**（厚生労働省告示第539号系統）: 既存出典 `mhlw-unit-price-notice-observed-946c3d96`（2026-07-10取得、R8-06施行前）と**同一URL・同一文書を、R8-06施行後の2026-07-26に独立して再取得**し、`mhlw-unit-price-notice-post-r8-observed-946c3d96` として別documentIdで登録した。SHA-256が2026-07-10取得値と完全一致し（52,785 bytes）、ページの改正履歴表示も「令和5年」が最新で令和8年の改正注記は無い。**この再観測（retrievedAtが2026-07-26、R8-06施行後）自体が、地域区分単価がR8-06以降も適用されることの直接一次証拠である。**
2. **負担上限額**（障害者総合支援法施行令・こども家庭庁通知系統）: こども家庭庁が公開する「障害福祉サービス・障害児通所支援等の利用者負担認定の手引き【令和8年6月版】」（2026-06-05公開、`sources.json` へ新規登録。documentId `r8-burden-recognition-guide-202606`）の物理12頁（footerの印字ページ番号は11）の表が、現行seedの値（生活保護0円・低所得１／２0円・一般1 9,300円・一般2 37,200円）と完全一致する。**この文書自体が令和8年6月版であり、R8適用性は版そのものが立証する。**

## 背景

`region-unit-prices.json` と `burden-caps.json` は、`effectiveFrom: "2024-04"` / `effectiveTo: null` のまま2026-06以降も適用され続けるが、`sources.json` にはR8-06時点の適用を裏づける出典が登録されていなかった。CSV生成の仕組みは完成しているため、この状態では2026-06以降の請求が**エラーを出さずにR6の値で生成されうる**（他のマスタギャップはreadiness gateやfail-close guardを経由するため、この2ファイルだけが例外だった）。

このギャップを閉じるため、`ClaimMasterR8ContinuityTests`（AC3-4-1）を新設し、「2026-06に到達する全entryが、R8-06適用性を確認済みの出典を持つか、適用期間を明示的に閉じているか」を機械検査する。**この検査の設計で最も重要な論点は、「R8-06適用性を確認済みの出典」をどう定義するかである。**

### 地域単価・負担上限額は報酬改定パッケージの一部ではない

当初、この定義を「R8-06報酬改定パッケージ（`r8-fee-notice` / `r8-reward-structure` / `r8-service-codes-2-xlsx` / `r8-service-codes-2-pdf` / `r8-b-reward-band-guide` / `r8-calculation-note` / `r8-capability-202606` の7件）に属する出典」に限定して検討した。しかしこの7件をSHA-256照合の上で`pdftotext -layout`/`-raw`の2方式で全文検索したところ、**いずれの文書にも地域区分単価表・負担上限額表は掲載されていなかった**（詳細は次節）。

厚生労働省の公式ページ「報酬算定構造・サービスコード表等」（令和8年6月施行分）が列挙する改定パッケージは次の8項目に限られる。

1. 事務連絡
2. 体制状況一覧表
3. 実績記録票（変更なし）
4. 請求書明細書（変更なし）
5. 決定サービスごとの設定内容（変更なし）
6. 障害福祉サービス費等の報酬算定構造
7. 介護給付費等単位数サービスコード
8. 請求サービスコードと決定サービスコード対応表

地域区分単価は、厚生労働省告示第539号（`mhlw-unit-price-notice-observed-946c3d96`。障害福祉サービス全体に共通する一単位の単価を定める独立した告示）で定められ、負担上限額は障害者総合支援法施行令および関連通知（こども家庭庁が発出する利用者負担認定の手引き等）で定められる。**両制度値は3年ごとの報酬改定パッケージの一部ではなく、別の法令・通知系統に属する。** ADR 0022も「r8-grant-decision-administration-202606 物理112頁は制度額を別資料参照としており、金額自体の根拠には使わない」と記録しており、本ADRの発見と整合する。

このため、「R8-06適用性を確認済みの出典」を報酬改定パッケージ限定で定義すると、地域単価・負担上限額は原理的に該当する出典を持ち得ない。改定パッケージの中に無いことは、値が変わったかもしれないことを何ら意味しない。検査の問いは「R8-06改定パッケージに載っているか」ではなく、**「地域単価・負担上限額それぞれが実際に属する法令・通知系統において、R8-06時点の適用を直接確認できたか」**でなければならない。本ADRはこの問いに沿って、告示第539号のR8-06施行後の再観測と、こども家庭庁の令和8年6月版手引きの2系統で確認した。

**運用規則（次の施行境界向け）**: 令和9年度以降の改定でも、地域単価・負担上限額は報酬改定パッケージを見るだけでは確認できない。**施行境界を迎えるたびに、告示第539号のURLとこども家庭庁の利用者負担認定の手引きのURLを個別に再取得し、SHA-256を比較する**手順を踏むこと（再検証手順を参照）。「R6・R8で改定パッケージに含まれなかった」のはこの2版で実測した事実であり、次の施行分でも同様であることを保証するものではない。

## 一次資料の同一性検証（2026-07-26実施）

R8-06改定資料束7件に加え、地域単価・負担上限額それぞれの系統で新たに確認した2件、計9件のSHA-256が `sources.json` の登録値と一致することを確認した。不一致は0件。

| documentId | sha256（先頭12桁） | 照合 | 本ADRでの用途 |
| --- | --- | --- | --- |
| r8-fee-notice | f4b7a05e33b5 | 一致 | 報酬改定パッケージに地域単価・負担上限の表が無いことの確認 |
| r8-reward-structure | 8c2638482364 | 一致 | 同上 |
| r8-service-codes-2-pdf | 0ff507138037 | 一致 | 同上 |
| r8-b-reward-band-guide | 96b002a6aecf | 一致 | 同上 |
| r8-calculation-note | 0c4f357f4dfd | 一致 | 同上（級地の例示計算1件のみ。対象サービス種類が異なり値の裏づけには使えない） |
| r8-capability-202606 | 84ff0b3b34c2 | 一致 | 同上（「地域区分」欄は事業所の自己申告フィールドで単価表ではない） |
| **mhlw-unit-price-notice-post-r8-observed-946c3d96**（新規登録） | 946c3d969ffd | 一致（原観測`mhlw-unit-price-notice-observed-946c3d96`の2026-07-10取得値と完全一致） | **地域区分単価の直接一次証拠。R8-06施行後（2026-07-26）の再観測** |
| **r8-burden-recognition-guide-202606**（新規登録） | ad24221afabc | 一致（2026-07-26に独立2回取得しSHA-256完全一致） | **負担上限額の直接一次証拠。令和8年6月版の表が現行seedと完全一致** |

## 抽出方式と2方式の一致確認結果

- R8-06改定資料束7件を `pdftotext -layout` と `pdftotext -raw` の2方式で独立抽出し、地域単価（`1114`/`1091`/`1086`/`1068`/`1057`/`1034`/`1017`/`級地`/`地域区分`）と負担上限額（`9,300`/`37,200`/`生活保護`/`低所得`/`負担上限`）のキーワードで検索した。両方式とも、実額表はいずれの文書にも存在しないという結論で一致した（「値が存在しない」という否定的な結論そのものが2方式で一致）。
- `mhlw-unit-price-notice-post-r8-observed-946c3d96` は、2026-07-10取得の原観測と2026-07-26取得（R8施行後）の**独立2回の取得**でSHA-256・バイト数（52,785 bytes）が完全一致した。ページ本文の「地域区分」表の「一級地 就労継続支援 千分の千百十四」（＝1114/1000＝11.14円）以下、seedの8行（1級地〜7級地・その他）と完全一致する記載を確認した。
- `r8-burden-recognition-guide-202606` は物理**12頁**（footerの印字ページ番号は「11」。物理頁と印字ページ番号は1つずれる）に負担上限額の表がある。`pdftotext -layout -f 12 -l 12` と `pdftotext -raw -f 12 -l 12` で当該ページのみを独立抽出し、両方式で次の4値が一致することを確認した（burden-caps.jsonの現行値とも完全一致）。

  | 所得区分 | -layout 抽出 | -raw 抽出 | seed値 |
  | --- | --- | --- | --- |
  | 生活保護 | ０円 | ０円 | 0 |
  | 低所得１・２ | ０円 | ０円 | 0 |
  | 一般１（居宅で生活する障害者並びに20歳未満の障害者施設入所者及び障害児施設入所者） | 9,300 円 | 9,300 円 | 9300 |
  | 一般２ | 37,200 円 | 37,200 円 | 37200 |

  （実際に発見時は「物理11頁」と誤記していたが、ページ範囲を指定した2方式再抽出で物理12頁が正しいことが判明し、`sources.json`・`burden-caps.json`のlocatorを訂正した。）

## 決定

### 1. 検査基準: 「R8-06に適用されることをプロジェクトとして確認済みの出典」

`ClaimMasterR8ContinuityTests` の許容出典リストは `R8AppliedDocumentIds`（「R8-06に適用されることをプロジェクトとして確認済みの出典」）である。各メンバーに、なぜR8適用性を立証するのかを1行コメントで付記する。

```csharp
private static readonly string[] R8AppliedDocumentIds =
[
    // --- R8-06 報酬改定資料束（basic-rewards / additions / service-codes向け） ---
    "r8-fee-notice",              // 令和8年6月1日施行の改正告示そのもの
    "r8-reward-structure",        // 令和8年6月施行分の報酬算定構造
    "r8-service-codes-2-xlsx",    // 令和8年6月施行分のサービスコード表
    "r8-service-codes-2-pdf",     // 同上のPDF版（独立2方式照合の一方）
    "r8-b-reward-band-guide",     // 令和8年6月施行分のB型基本報酬区分見直し資料
    "r8-calculation-note",        // 令和8年6月1日施行の留意事項通知
    "r8-capability-202606",       // 令和8年6月版の体制状況一覧表

    // --- 地域区分単価・負担上限額向け（報酬改定資料束とは別の法令・通知系統） ---
    "mhlw-unit-price-notice-post-r8-observed-946c3d96", // R8-06施行後（2026-07-26）に独立再取得した観測。retrievedAtがR8施行後である点が直接証拠になる
    "r8-burden-recognition-guide-202606",               // 版そのものが令和8年6月版。表の値がseedと完全一致
];
```

このリストは2種類のdocumentIdで構成される。「R8-06報酬改定資料束7件」（basic-rewards / additions / service-codesの継続照合にADR 0027決定6・ADR 0028決定1が使う）と、「地域単価・負担上限額のように別系統の制度値について、その系統でR8-06時点の適用を直接確認した出典」である。今後、新しい制度値カテゴリのR8継続を検査する場合も、この2分類（改定パッケージに属する値か、別系統の値か）を先に切り分けてから証跡基準を設計する。

**原観測を許容リストに含めない理由**: `mhlw-unit-price-notice-observed-946c3d96`（2026-07-10取得、R8-06施行前）はregion-unit-pricesの全8エントリが既に`authoritative`で引いている。これを許容リストに含めると、entry側に新たな出典を1件も追加しなくても検査が無条件で通ってしまい、検査が実質的に「2006年の告示を引いているか」しか主張しなくなる（歯が立たない）。許容リストには、R8-06施行**後**に行われた独立した観測（`mhlw-unit-price-notice-post-r8-observed-946c3d96`、retrievedAt 2026-07-26）だけを載せる。

**同一文書を2つのentryとして登録した理由**: `mhlw-unit-price-notice-observed-946c3d96`と`mhlw-unit-price-notice-post-r8-observed-946c3d96`は同一URL・同一バイト列（同一SHA-256）だが、**観測イベントとして別**（取得日が異なり、片方はR8-06施行前、もう片方は施行後）である。本プロジェクトには同一URLの異なる観測を別documentIdにする前例がある（`mhlw-r8-revision-page-observed-2e8f1425`と`-7c937a6a`）。`entry.sourceRefs`では、原観測を`evidenceRole: authoritative`（値そのものの根拠）、再観測を`evidenceRole: cross-check`（独立した別の観測イベントによる裏付け）として区別する。**同一documentIdを2回引いて片方を`cross-check`と呼ぶと、文書が自分自身を裏付けることになり証跡語彙が濁る**（役割欄だけを見る監査者が「2つの独立した出典が一致した」と誤読する）。documentIdを分けることで、`cross-check`という語が本来意味する「独立した情報源による裏付け」を保つ。

**runtime validatorとの整合**: `ClaimMasterFileValidator.SourceAuthorityValidator`は、1つのentryに`evidenceRole: authoritative`の出典が複数あり、かつそれらの間に`corrects`関係が無い場合、「有効な最新版が一意に定まらない」として`has multiple authoritative maxima`で例外を投げる。原観測・再観測をどちらも`authoritative`にすると、両者の間に`corrects`関係が無いためこの例外に触れる。再観測は原観測の**誤りを訂正するものではなく**（`corrects`は不適切）、単に独立した後続の観測であるため、`cross-check`が正しい選択になる。また`sources.json`側では、`supplements`等の関係を持つ出典に`correctionNote`が必須（`JsonClaimMasterProvider.ValidateCatalog`）であるため、再観測entryには関係の説明を`correctionNote`に記載している。

歯の確認: 次の3通りをそれぞれ実施した。

1. `R8AppliedDocumentIds`から`mhlw-unit-price-notice-post-r8-observed-946c3d96`を一時的に除去 → `region-unit-prices.json`のみRED（`burden-caps.json`は無傷でPASS）。
2. `R8AppliedDocumentIds`から`r8-burden-recognition-guide-202606`を一時的に除去 → `burden-caps.json`のみRED（`region-unit-prices.json`は無傷でPASS）。
3. **（最重要）** `region-unit-prices.json`の1エントリから、今回追加した`sourceRefs`（`mhlw-unit-price-notice-post-r8-observed-946c3d96`への`cross-check`ref）だけを直接削除し、元の`authoritative`ref（原観測）はそのまま残した状態で実行 → RED。`burden-caps.json`の1エントリでも同様に新規ref（`r8-burden-recognition-guide-202606`への`cross-check`ref）だけを削除 → RED。これにより、許容リストの仕組みだけでなく、**今回追加した証跡refそのものが各entryの合否を左右すること**（歯が実データに対して機能すること）を確認した。

いずれも復元後は2件ともPASSに戻ることを確認した。

### 2. 決定表（seed実値。これが値の唯一の出典）

`region-unit-prices.json`（`masterKind: region-unit-prices`。`serviceKind: employment-continuation-support`）:

| regionKey | unitPriceYen | 告示上の割合 | R8-06適用の直接証拠 |
| --- | ---: | --- | --- |
| region-grade-1 | 11.14円 | 1,114 / 1,000 | mhlw-unit-price-notice-post-r8-observed-946c3d96 |
| region-grade-2 | 10.91円 | 1,091 / 1,000 | 同上 |
| region-grade-3 | 10.86円 | 1,086 / 1,000 | 同上 |
| region-grade-4 | 10.68円 | 1,068 / 1,000 | 同上 |
| region-grade-5 | 10.57円 | 1,057 / 1,000 | 同上 |
| region-grade-6 | 10.34円 | 1,034 / 1,000 | 同上 |
| region-grade-7 | 10.17円 | 1,017 / 1,000 | 同上 |
| region-other | 10.00円 | 1,000 / 1,000 | 同上 |

`burden-caps.json`（`masterKind: burden-caps`）:

| burdenCategory | capYen | R8-06適用の直接証拠 |
| --- | ---: | --- |
| welfare（生活保護） | 0 | r8-burden-recognition-guide-202606 物理12頁 |
| low-income（低所得１・２） | 0 | 同上 |
| general-1（一般1） | 9,300 | 同上 |
| general-2（一般2） | 37,200 | 同上 |

いずれもR6（2024-04〜）から2026-06以降まで**同一値で継続**する。`effectiveFrom: "2024-04"` / `effectiveTo: null` を維持する。

### 3. ADR 0020のprose reasoningとの関係: 今回それを機械証跡化した

ADR 0020は既に「令和8年告示第5号は告示第539号を改正対象としておらず、令和8年度改定ページと令和8年6月施行分ページにも代替の一単位単価表は掲載されていない。このため、単価の継続は値の推測ではなく、現行告示の継続適用として扱う」とprose（自然文）で記録していた。本ADRはこの結論を否定するものではなく、**同じ結論を`ClaimMasterR8ContinuityTests`という機械検査で再現可能にした**。ADR 0020の根拠だった「R8施行後に現行条文を観測してSHA-256が変化していないこと」を、`mhlw-unit-price-notice-post-r8-observed-946c3d96`として独立したdocumentIdで登録し、entryの`sourceRefs`で機械的に参照できるようにした。今後、この文書のURLが変化した場合（＝告示が改正された場合）は、`sources.json`の`sha256`を更新する際にADR 0038と同様の証跡管理（差し替え検出・fail-close）が働く。

## 選択肢

### A: 出典なしで継続する（不採用）

`effectiveTo: null`のまま、R8-06向け出典を確認しない。R8で改定されていないことを確認しないまま値を使い続けると、万一改定されていた場合に**エラーを出さずに誤った金額で請求が生成される**（サイレント誤請求）。不採用。

### B: 「R8-06改定資料束に無いことをもってfail-closeする」（検討したが不採用）

検査基準を「R8-06報酬改定資料束7件のいずれかを出典に持つこと」に限定し、地域単価・負担上限額は改定資料束にその表が無いため、両マスタを2026-05で閉じ、2026-06以降の請求生成を停止する。

**検討した上で不採用とした理由**: 地域単価・負担上限額は、背景節で述べたとおり報酬改定パッケージとは別の法令・通知系統に属する制度値であり、改定パッケージの中を探しても見つからない。「改定資料束に無い」という事実は「値が変わったかもしれない」ことを何ら示唆しない。この基準でfail-closeし続けることは、**誤請求を防ぐのではなく、正しく確認できる値を持つ事業所からも不要に請求生成の手段を奪うだけ**になる。事業所は「請求が出せない」ことに気付けるが、この場合は気付いたところで「本来は出せるはずなのに出せない」という別の実害が生じる。

### C: 地域単価・負担上限額それぞれの実際の法令・通知系統でR8-06時点の適用を直接確認する（採用）

告示第539号はそのURLをR8-06施行後に再取得し、負担上限額はこども家庭庁の利用者負担認定の手引きの令和8年版を探して確認する。両系統とも一次資料からR8-06時点での値の同一性を直接確認できたため、`sourceRefs`へ独立した観測・独立した文書として追記し、`effectiveTo: null`を維持する。

## 影響

- `claim-master-r8-06`（2026年6月以降）の請求生成は、地域区分単価・負担上限額が解決できる（`ClaimMasterCsvOfficeContextProvider.UnitPriceMilliYen`は無変更のまま、seedの出典追加により正常に解決する）。
- `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportProductionWiringTests.cs`の5テスト（`ServiceMonth(2026, 6)`〜`(2026, 10)`を使うテスト）は、テスト自体を変更せずに元の主張どおり成功する。
- `sources.json`へ新規documentId `mhlw-unit-price-notice-post-r8-observed-946c3d96`・`r8-burden-recognition-guide-202606` を登録し、`releases`の`claim-master-r8-06`束（`2026-06 → null`）の`sourceDocumentIds`へ両方を追加した。`source-catalog.schema.json`は変更していない（`applicabilityNote`・`correctionNote`とも既存フィールドの自由文字列で表現できたため。同一sha256を持つ複数documentIdの登録もschema上・`ClaimMasterFileValidator`上ともに禁止されていないことを確認済み）。
- ADR 0022が述べる「5-release source chain」（`claim-master-r6-04`〜`claim-master-r8-06`の各版が参照するdocumentId束）は、**出典カタログの連続性**（どの文書を参照しうるかの記録）であり、これに加えて本ADR以降は、地域区分単価・負担上限額の各entryが**実際にどの出典で機械検証されているか**を`ClaimMasterR8ContinuityTests`が個別に固定する。両者は補完関係にあり、リリース束への文書列挙だけでは機械検証済みを意味しない点は変わらない。
- **教訓（次の版境界向け）**: 新しい施行分を迎えるたびに「この制度値はどの法令・通知系統に属するか」を先に切り分けてから証跡基準を設計する。報酬改定パッケージに属さない制度値を改定パッケージの文書だけで検査すると、改定パッケージの中を探しても見つからないという結果になり、fail-closeにするか出典基準を見直すかの判断を誤りやすい。また、同一文書を「自分自身のcross-check」として再利用しない。R8-06施行後の再観測が必要な場合は、独立したdocumentIdとして登録し、`evidenceRole`はentry内の他の出典との関係（同一文書の後続観測なら`cross-check`、実際に内容を訂正するものなら`corrects`関係を張った上で`correction`）に応じて選ぶ。

## 再検証手順

1. `sources.json`の該当9件（R8-06改定資料束7件＋`mhlw-unit-price-notice-post-r8-observed-946c3d96`＋`r8-burden-recognition-guide-202606`）のURLを取得し、`shasum -a 256`が登録値と一致することを確認する。
2. `mhlw-unit-price-notice-observed-946c3d96`（原観測）のURL（`https://www.mhlw.go.jp/web/t_doc?dataId=83aa8493&dataType=0&pageNo=1`）を再取得し、SHA-256が変化していないか確認する。変化していれば、地域単価は改定の可能性があるため、新しい条文を読み値を再確認し、変化を検出した時点の観測を新しいdocumentIdとして登録する。
3. こども家庭庁の事務処理要領ページ（`https://www.cfa.go.jp/policies/shougaijishien/shisaku/jimushori_yoryo`）から「利用者負担認定の手引き」の最新版を確認し、負担上限額表の値を確認する。新しい版が出ていれば、`sources.json`へ新規documentIdとして登録し直し、`sourceRefs`を差し替える（旧版の扱いは`corrects`または`supersedes`で明示する）。
4. ハッシュ不一致または値の不一致を検出した場合は、値を使わず`docs/open-questions.md`に起票して停止する（ADR 0020と同じ運用）。
