# ADR 0020: 令和6／令和8請求マスタの出典と版管理

## 決定

就労継続支援B型の請求マスタは、サービス提供年月で次の3版を選択する。

| masterVersion | effectiveFrom | effectiveTo | 用途 |
| --- | --- | --- | --- |
| claim-master-r6-04 | 2024-04 | 2024-05 | 令和6年4月施行の基本報酬、加算・減算、体制、サービスコード |
| claim-master-r6-06 | 2024-06 | 2026-05 | 令和6年6月施行分を反映した令和6版。令和6年4月分と異なる体制・コードは6月版で置換 |
| claim-master-r8-06 | 2026-06 | null（終了月なし） | 令和8年6月施行の基本報酬区分、加算・減算、体制、サービスコード、経過措置 |

effectiveToのnullは、終了月が定められていないopen-endedな版を表す。将来の改定月を推測して設定しない。

マスタ群ごとの選択は次のとおりとする。

| masterKind | 2024-04 | 2024-06〜2026-05 | 2026-06以降 |
| --- | --- | --- | --- |
| basic-rewards | r6-fee-notice、r6-reward-structure | 同左 | r8-fee-notice、r8-calculation-note、r8-reward-structure |
| additions / deductions | r6-fee-notice、r6-calculation-note、r6-service-codes-2-pdf、r6-service-codes-2-xlsx | r6-fee-notice、r6-calculation-note、r6-service-codes-2-pdf、r6-service-codes-2-xlsxの令和6年6月施行分と正誤を反映 | r8-fee-notice、r8-calculation-note、r8-service-codes-2-pdf、r8-service-codes-2-xlsx |
| region-unit-prices | r6-revision-overview。告示のHTML観測バイトは別表のロケータ証拠とする | 同左 | 同左。令和8改定で単価表の置換なし |
| office-capabilities | r6-capability-202404 | r6-capability-202406 | r8-capability-202606 と修正箇所資料 |
| service-codes | r6-service-codes-2-pdf、r6-service-codes-2-xlsxの2025-01-31確定版から2024-04適用行を選択 | r6-service-codes-2-pdf、r6-service-codes-2-xlsxから2024-06適用行を選択 | r8-service-codes-2-pdf、r8-service-codes-2-xlsx |
| claim-to-decision-code mapping | r6-claim-decision-202404-pdf／xls | r6-claim-decision-202406-pdf／xls | r8-claim-decision-pdf／xls |

各版のSourceDocumentIdsは次の閉じたリストとし、再取得時に同一バイトを特定できる公式PDF、XLS、XLSXだけを含める。ここにない資料を暗黙に参照せず、全IDが後述の公式資料カタログに解決できることを登録時に検証する。

| masterVersion | SourceDocumentIds |
| --- | --- |
| claim-master-r6-04 | [r6-revision-overview,r6-fee-notice,r6-calculation-note,r6-employment-guidance-r6,r6-employment-guidance,r6-employment-guidance-corr-4,r6-qa-v1,r6-qa-v2,r6-qa-v3,r6-qa-v5,r6-qa-v6,r6-qa-v7,r6-qa-v8,r6-calculation-corr-1,r6-calculation-corr-3,r6-calculation-corr-5,r6-calculation-corr-6,r6-calculation-corr-7,r6-calculation-corr-8,r6-qa-corr-1,r6-qa-corr-2,r6-qa-corr-3,r6-capability-202404,r6-reward-structure,r6-service-codes-2-pdf,r6-service-codes-2-xlsx,r6-claim-decision-202404-pdf,r6-claim-decision-202404-xls] |
| claim-master-r6-06 | [r6-revision-overview,r6-fee-notice,r6-calculation-note,r6-employment-guidance-r6,r6-employment-guidance,r6-employment-guidance-corr-4,r6-qa-v1,r6-qa-v2,r6-qa-v3,r6-qa-v5,r6-qa-v6,r6-qa-v7,r6-qa-v8,r6-calculation-corr-1,r6-calculation-corr-3,r6-calculation-corr-5,r6-calculation-corr-6,r6-calculation-corr-7,r6-calculation-corr-8,r6-qa-corr-1,r6-qa-corr-2,r6-qa-corr-3,r6-capability-202406,r6-reward-structure,r6-service-codes-2-pdf,r6-service-codes-2-xlsx,r6-claim-decision-202406-pdf,r6-claim-decision-202406-xls] |
| claim-master-r8-06 | [r6-revision-overview,r8-revision-overview,r8-fee-notice,r8-calculation-note,r8-b-reward-band-guide,r8-employment-transition-guide,r8-qa-v1,r8-amendment-qa,r8-capability-202606,r8-capability-correction,r8-reward-structure,r8-service-codes-2-pdf,r8-service-codes-2-xlsx,r8-claim-decision-pdf,r8-claim-decision-xls] |

動的HTMLはマスタ値入力ではなく、公式ファイルの所在とページ上の判断根拠を示すロケータ証拠として次の別表で管理する。同一URLが複数バイトを返すため、単一の「現行SHA」は定義せず、観測できたバリアントだけを列挙する。このリストは全バリアントの網羅を保証しない。

| masterVersion | LocatorEvidenceDocumentIds | evidenceScope |
| --- | --- | --- |
| claim-master-r6-04 | [mhlw-r6-revision-page-observed-c7b78655,mhlw-r6-revision-page-observed-fefe2d88,mhlw-r6-structure-page-observed-8a9858bf,mhlw-unit-price-notice-observed-946c3d96] | 令和6改定資料・構造表等の所在と一単位単価告示の現行条文を確認するロケータ証拠。マスタ値入力には使用しない |
| claim-master-r6-06 | [mhlw-r6-revision-page-observed-c7b78655,mhlw-r6-revision-page-observed-fefe2d88,mhlw-r6-structure-page-observed-8a9858bf,mhlw-unit-price-notice-observed-946c3d96] | 令和6年6月施行資料・確定サービスコードの所在と一単位単価告示の現行条文を確認するロケータ証拠。マスタ値入力には使用しない |
| claim-master-r8-06 | [mhlw-r8-revision-page-observed-2e8f1425,mhlw-r8-revision-page-observed-7c937a6a,mhlw-r8-structure-page-observed-9bc71ce6,mhlw-r8-structure-page-observed-13da3c44,mhlw-unit-price-notice-observed-946c3d96] | 令和8改定資料・構造表等の所在、5項目の「変更なし」、対応表の新規掲載、一単位単価告示の現行条文を確認するロケータ証拠。マスタ値入力には使用しない |

R8構造ページの2観測バリアントは、HTMLエンティティと空白を正規化すると本文と公式リンク集合が一致し、どちらも実績記録票、同記載例、請求書明細書、同記載例、決定サービスごとの設定内容の5項目を「変更なし」とし、請求サービスコードと決定サービスコード対応表を掲載している。

サービスコード分割1は就労継続支援B型のシートを含まないため、3版のSourceDocumentIdsには含めない。分割1の資料は非入力コンテキストとしてカタログにのみ残す。

令和6版は2025年1月31日確定のサービスコード表を正本とし、2024年11月29日以前の掲載ファイルへ戻さない。令和8版は2026年4月28日更新のサービスコード表と、修正箇所を反映した体制状況一覧表を正本とする。

地域区分単価は、就労継続支援について厚生労働省告示第539号の現行条文に記載された次の値を採用する。金額は基準額10円に告示の割合を乗じた結果であり、令和6年度改定概要が地域区分を「令和6〜8年度」の適用地域としているため、claim-master-r6-04、claim-master-r6-06、claim-master-r8-06で共通に使用する。

令和8年告示第5号は告示第539号を改正対象としておらず、令和8年度改定ページと令和8年6月施行分ページにも代替の一単位単価表は掲載されていない。このため、単価の継続は値の推測ではなく、現行告示の継続適用として扱う。

| 地域区分 | 告示上の割合 | 1単位の単価 |
| --- | ---: | ---: |
| 1級地 | 1,114 / 1,000 | 11.14円 |
| 2級地 | 1,091 / 1,000 | 10.91円 |
| 3級地 | 1,086 / 1,000 | 10.86円 |
| 4級地 | 1,068 / 1,000 | 10.68円 |
| 5級地 | 1,057 / 1,000 | 10.57円 |
| 6級地 | 1,034 / 1,000 | 10.34円 |
| 7級地 | 1,017 / 1,000 | 10.17円 |
| その他 | 1,000 / 1,000 | 10.00円 |

令和8年6月ページの2観測バリアント（mhlw-r8-structure-page-observed-9bc71ce6、mhlw-r8-structure-page-observed-13da3c44）で「変更なし」とされている実績記録票、実績記録票の記載例、請求書明細書、請求書明細書の記載例、決定サービスごとの設定内容には、空の令和8版や複製版を作らない。これらは既存の直近版を継続する。一方、両バリアントの「請求サービスコードと決定サービスコード対応表」は令和8年6月版が新規掲載されているため、両者を混同せず r8-claim-decision-pdf／xls を選択する。

各外部マスタは documentId、title、publisher、effectiveAt、publishedAt、retrievedAt、url、sha256、supersedes、corrects、supplements、applicabilityNote、correctionNote を保持する。安定したPDF、XLS、XLSXが同じURLで公式に差し替えられた場合は、既存documentIdを上書きせず、新しいdocumentIdと取得日で登録する。動的HTMLは差替え時系列を推測せず、取得バイトのSHA-256接頭辞を含む観測バリアントIDで登録し、バリアント間にsupersedesまたはcorrectsを設定しない。必要資料、適用版、訂正関係のいずれかが欠ける場合は算定を停止し、近い値や前版を推測適用しない。

制度値は本ADRと後続ADR・外部マスタにのみ記録し、C#定数へ転記しない。取得したPDF、Excel、HTMLは一時領域だけに置き、リポジトリへ保存しない。

## 背景

Tsumugiはオフラインで請求データを生成するため、実行時に厚生労働省サイトを参照できない。一方、基本報酬、加算・減算、地域区分単価、体制コード、サービスコードは改定・正誤・差替えの対象であり、値だけを保存すると、どの施行月と資料へ基づく請求かを再現できない。

令和6年度改定には2024年4月施行と6月施行が混在し、その後もQ&Aと正誤、サービスコードの確定版が公表された。令和8年度改定は2026年6月施行で、就労継続支援B型の基本報酬区分、処遇改善加算等、サービスコード、体制状況が更新された一方、帳票等は「変更なし」と明記された。このため「令和6版／令和8版」の二値だけでなく、令和6年4月と6月の境界、訂正済みファイル、変更なしの継続元を固定する必要がある。

## 選択肢

- **A: 公式資料のSHA-256と施行月を持つ不変の版束（採用）**: 取得した各資料をdocumentIdで識別し、サービス提供年月から版束を選ぶ。訂正・差替えは新しい資料として追記する。
- **B: 最新の公式URLだけを保持**: URL差替え後に過去請求の根拠バイトを特定できず、同じURLでも内容が変わり得る。
- **C: 制度値をC#へ直接記述**: 出典、施行月、訂正関係がコード差分に埋もれ、ハード制約の外部化・版管理に反する。

## 決定根拠

### 版境界と訂正の扱い

- claim-master-r6-04は2024年4月施行資料を使用する。6月施行専用の処遇改善・体制・コードを先取りしない。
- claim-master-r6-06は2024年6月施行資料を使用し、サービスコードは公式ページの「11月29日修正、1月31日確定分」を使用する。B型に関係する留意事項、Q&A VOL.5〜8、正誤その4／6を含む正誤資料も適用する。
- claim-master-r8-06は2026年6月施行資料を使用する。令和8年5月以前の請求へ遡及適用しない。
- r8-calculation-noteは2026年5月28日改正を含む公式の現行ファイルを使用する。
- r8-capability-202606は同ページ掲載の修正箇所資料と一組で扱う。Excelだけ、または修正箇所PDFだけを単独の正本にしない。
- 基本報酬の全区分・定員別単位、加算率、減算率、サービスコードは報酬算定構造とサービスコードExcelを外部マスタへ転記する。人手でC#へ再入力しない。

### 公式資料カタログ

retrievedAtは全て2026-07-10である。publisherは、別記がない限り厚生労働省である。effectiveAtは必ず単一のISO 8601日付（YYYY-MM-DD）とし、次の順で決定する。

1. 資料に法的・制度的な適用開始日が明記されている場合は、その開始日を記録する。
2. Q&A、正誤、通知などに別の適用開始日がない場合は、公式の発出日または公表日を記録する。
3. 複数の施行日・適用期間を含む資料は、最初の適用開始日だけをeffectiveAtに記録し、残りをapplicabilityNoteに記録する。
4. publishedAtは公式の発出日・公布日・掲載日を一意に確認できる場合だけISO 8601日付で記録し、確認できない場合はnullとする。

supersedes、corrects、supplementsはdocumentIdの配列またはnullだけを取り、自由記述を置かない。関係の意味は順に「旧資料を置換する」「対象資料を訂正する」「対象資料を補足する」であり、全参照先が本カタログに存在することを登録時に検証する。correctionNoteは、これらの関係列に値がある場合に対象documentId、関係種別、applicabilityNoteに記録した適用範囲を簡潔に説明し、いずれも該当しない場合はnullとする。

#### HTMLロケータ観測バリアント・地域区分単価

| documentId | title | publisher | effectiveAt | publishedAt | retrievedAt | url | sha256 | supersedes | corrects | supplements | applicabilityNote | correctionNote |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mhlw-r6-revision-page-observed-c7b78655 | 令和6年度障害福祉サービス等報酬改定（観測バイトc7b78655・69,626 bytes） | 厚生労働省 | 2024-04-01 | null | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00009.html | c7b78655086e6f5d9276c1a84dad5e20826d8bbbbd7e9bd44c15142facedf923 | null | null | null | 同一URLからfefe2d88バリアントも観測。両者はエンティティ・空白正規化後の本文と公式リンク集合が一致する。時系列関係と全バリアントの網羅を主張しないロケータ証拠 | null |
| mhlw-r6-revision-page-observed-fefe2d88 | 令和6年度障害福祉サービス等報酬改定（観測バイトfefe2d88・69,915 bytes） | 厚生労働省 | 2024-04-01 | null | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00009.html | fefe2d888f93acb90b7c24a08aeec617104b4bbe5e02489c854ecfcd4a502369 | null | null | null | 同一URLからc7b78655バリアントも観測。両者はエンティティ・空白正規化後の本文と公式リンク集合が一致する。時系列関係と全バリアントの網羅を主張しないロケータ証拠 | null |
| mhlw-r6-structure-page-observed-8a9858bf | 報酬算定構造・サービスコード表等 令和6年4月、6月施行分（観測バイト8a9858bf・57,638 bytes） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00018.html | 8a9858bf575af9bb57a3a5df61428e66e8d903bf984de9f6b46b3fd47631a0c7 | null | null | null | 2024-11-29修正後の2025-01-31確定ファイルを指す観測ロケータ。今回の反復取得ではこのバイトだけを観測したが、全バリアントの網羅を主張しない | null |
| mhlw-r8-revision-page-observed-2e8f1425 | 令和8年度障害福祉サービス等報酬改定（観測バイト2e8f1425・55,758 bytes） | 厚生労働省 | 2026-06-01 | null | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00013.html | 2e8f142511e9c6a5c9125c4dbaec6c05062cd5fb2e65ea2b0ba925f639b6c395 | null | null | null | 同一URLから7c937a6aバリアントも観測。両者はエンティティ・空白正規化後の本文と公式リンク集合が一致する。時系列関係と全バリアントの網羅を主張しないロケータ証拠 | null |
| mhlw-r8-revision-page-observed-7c937a6a | 令和8年度障害福祉サービス等報酬改定（観測バイト7c937a6a・55,469 bytes） | 厚生労働省 | 2026-06-01 | null | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00013.html | 7c937a6a51aa0bfb3974ef47b20119c929fa3b142f74d65e78526095a34ee70e | null | null | null | 同一URLから2e8f1425バリアントも観測。両者はエンティティ・空白正規化後の本文と公式リンク集合が一致する。時系列関係と全バリアントの網羅を主張しないロケータ証拠 | null |
| mhlw-r8-structure-page-observed-9bc71ce6 | 報酬算定構造・サービスコード表等 令和8年6月施行分（観測バイト9bc71ce6・51,599 bytes） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html | 9bc71ce6bfdfe2461a3068dd2ad924a215c12d3f071e2034a8ca51b51de49622 | null | null | null | 同一URLから13da3c44バリアントも観測。両者の正規化本文・公式リンク集合は一致し、5項目の「変更なし」と対応表の掲載を確認。時系列関係と全バリアントの網羅を主張しないロケータ証拠 | null |
| mhlw-r8-structure-page-observed-13da3c44 | 報酬算定構造・サービスコード表等 令和8年6月施行分（観測バイト13da3c44・51,888 bytes） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html | 13da3c44eb9e9485c38fda878bb1fe966407e7331609f87123ba2b2778a32083 | null | null | null | 同一URLから9bc71ce6バリアントも観測。両者の正規化本文・公式リンク集合は一致し、5項目の「変更なし」と対応表の掲載を確認。時系列関係と全バリアントの網羅を主張しないロケータ証拠 | null |
| mhlw-unit-price-notice-observed-946c3d96 | こども家庭庁長官及び厚生労働大臣が定める一単位の単価並びに厚生労働大臣が定める一単位の単価（厚生労働省告示第539号・観測バイト946c3d96・52,785 bytes） | こども家庭庁・厚生労働省 | 2006-10-01 | 2006-09-29 | 2026-07-10 | https://www.mhlw.go.jp/web/t_doc?dataId=83aa8493&dataType=0&pageNo=1 | 946c3d969ffd4128db15106d25ce6d26ff108f5460a7618e3df96352e42c0c1b | null | null | null | 令和6〜8年度に適用する現行条文の観測ロケータ。今回の反復取得ではこのバイトだけを観測したが、動的HTMLの全バリアントの網羅を主張しない | null |

#### 令和6資料

| documentId | title | publisher | effectiveAt | publishedAt | retrievedAt | url | sha256 | supersedes | corrects | supplements | applicabilityNote | correctionNote |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| r6-revision-overview | 令和6年度障害福祉サービス等報酬改定の概要 | 厚生労働省 | 2024-04-01 | null | 2026-07-10 | https://www.mhlw.go.jp/content/001216035.pdf | 8b66f74bd44cceeb3a0556f8055d10d997199097ed2f9715f79df6cd6fc94f75 | null | null | null | 地域区分は令和6〜8年度の適用地域を示す | null |
| r6-fee-notice | 指定障害福祉サービス等及び基準該当障害福祉サービスに要する費用の額の算定に関する基準等の一部を改正する告示（令和6年こども家庭庁・厚生労働省告示第3号） | こども家庭庁・厚生労働省 | 2024-04-01 | 2024-03-15 | 2026-07-10 | https://www.mhlw.go.jp/content/001239565.pdf | 5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54 | null | null | null | 一部は2024-06-01施行。留意事項・正誤・構造表と併用 | null |
| r6-calculation-note | 指定障害福祉サービス等の費用算定基準等の制定に伴う実施上の留意事項について | 厚生労働省 | 2024-04-01 | 2025-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001494356.pdf | 958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1 | null | null | [r6-fee-notice] | 2024-06-01施行分と2025-03-31までの改正を含む現行ファイル | r6-fee-noticeを補足（2024-06-01施行分と2025-03-31までの改正を含む現行ファイル） |
| r6-employment-guidance-r6 | 就労移行支援事業、就労継続支援事業（A型、B型）における留意事項について（令和6年3月29日通知） | 厚生労働省 | 2024-04-01 | 2024-03-29 | 2026-07-10 | https://www.mhlw.go.jp/content/001261445.pdf | 58097cbd040de95fd26b65ee2177f762ed276e899214bbc692a3c58c2e3440f3 | null | null | [r6-calculation-note] | 令和6改定時の原本。正誤その4と組み合わせる | r6-calculation-noteを補足（令和6改定時の原本。正誤その4と組み合わせる） |
| r6-employment-guidance | 就労移行支援事業、就労継続支援事業（A型、B型）における留意事項について（現行通知） | 厚生労働省 | 2025-03-31 | 2025-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001473458.pdf | 185fa99281ea97b5724297041f8bcbb50c92f9d3882496d3d9a3227328b17481 | [r6-employment-guidance-r6] | null | [r6-calculation-note] | 2025-10-01開始事項を含む。各サービス月に有効な条項だけを使用 | r6-employment-guidance-r6を置換。r6-calculation-noteを補足（2025-10-01開始事項を含む。各サービス月に有効な条項だけを使用） |
| r6-employment-guidance-corr-4 | 令和6年度障害福祉サービス等関連通知の正誤について（その4） | 厚生労働省 | 2024-06-06 | 2024-06-06 | 2026-07-10 | https://www.mhlw.go.jp/content/001261442.pdf | ee76aa76e2e0f76b38d4282142291501f9982a818d1a9a6ac3e6a343d9de6626 | null | [r6-employment-guidance-r6] | null | 施設外支援等を訂正 | r6-employment-guidance-r6を訂正（施設外支援等を訂正） |
| r6-qa-v1 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.1 | 厚生労働省 | 2024-03-29 | 2024-03-29 | 2026-07-10 | https://www.mhlw.go.jp/content/001260473.pdf | 88309c362a23aac08fce7947bb4aadcc892531164ad7d56a37188ca01be1d95a | null | null | [r6-fee-notice,r6-calculation-note] | 別掲の正誤資料と組み合わせる | r6-fee-notice、r6-calculation-noteを補足（別掲の正誤資料と組み合わせる） |
| r6-qa-v2 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.2 | 厚生労働省 | 2024-04-05 | 2024-04-05 | 2026-07-10 | https://www.mhlw.go.jp/content/001250243.pdf | bd773dfe577b8a83fd99b28fd10f8cdc1b55d47114a03fd64c128777e54e6b8d | null | null | [r6-qa-v1] | 別掲の正誤資料と組み合わせる | r6-qa-v1を補足（別掲の正誤資料と組み合わせる） |
| r6-qa-v3 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.3 | 厚生労働省 | 2024-05-10 | 2024-05-10 | 2026-07-10 | https://www.mhlw.go.jp/content/001253308.pdf | 8968925cfae7a563d68fa2b7131e19c7746e30e3a65b91b37ff7c3812c90e2b4 | null | null | [r6-qa-v2] | 取得時点でB型項目の別掲正誤なし | r6-qa-v2を補足（取得時点でB型項目の別掲正誤なし） |
| r6-qa-v5 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.5 | 厚生労働省 | 2024-08-29 | 2024-08-29 | 2026-07-10 | https://www.mhlw.go.jp/content/001297221.pdf | 5a5c07db593c2d75a14fa12714ed9ed112d62a00feb3ffd7855cc7e1f6ee5eb7 | null | null | [r6-qa-v3] | B型に適用される食事提供体制加算等を確認 | r6-qa-v3を補足（B型に適用される食事提供体制加算等を確認） |
| r6-qa-v6 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.6 | 厚生労働省 | 2024-10-11 | 2024-10-11 | 2026-07-10 | https://www.mhlw.go.jp/content/001316215.pdf | 14346b90b13893e201bcad9f1db325becf5b109353b92b0ace362987ffd55802 | null | null | [r6-qa-v5] | B型に適用される送迎加算を確認 | r6-qa-v5を補足（B型に適用される送迎加算を確認） |
| r6-qa-v7 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.7 | 厚生労働省 | 2025-01-24 | 2025-01-24 | 2026-07-10 | https://www.mhlw.go.jp/content/001385141.pdf | 943ed022a46f706adc23e2a6c40724acf7c994c4549d0b1df709c335c46215f5 | null | null | [r6-qa-v6] | 就労移行支援体制加算を確認 | r6-qa-v6を補足（就労移行支援体制加算を確認） |
| r6-qa-v8 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.8 | 厚生労働省 | 2025-03-31 | 2025-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001471548.pdf | 09187ef8d8f88edf95e3454fec96eb39c1e455a7fd72f76eae871fab6c40b340 | null | null | [r6-qa-v7,r6-employment-guidance-r6] | 施設外就労と基本報酬を確認 | r6-qa-v7、r6-employment-guidance-r6を補足（施設外就労と基本報酬を確認） |
| r6-calculation-corr-1 | 令和6年度障害福祉サービス等報酬改定関連通知の正誤について | 厚生労働省 | 2024-04-04 | 2024-04-04 | 2026-07-10 | https://www.mhlw.go.jp/content/001241205.pdf | e17e55bd7698a0343bf7c688afb7ca1a8447aa27dbd8a20070e6c9e95a8cecdb | null | [r6-calculation-note] | null | B型に適用される共通・個別訂正を含む | r6-calculation-noteを訂正（B型に適用される共通・個別訂正を含む） |
| r6-calculation-corr-3 | 同正誤について（その3） | 厚生労働省 | 2024-06-04 | 2024-06-04 | 2026-07-10 | https://www.mhlw.go.jp/content/001260426.pdf | d179e42bc7001da63e2f720148e7ceeec0ad35086a6707acea394eb887880332 | null | [r6-calculation-note] | null | B型項目の訂正を含む | r6-calculation-noteを訂正（B型項目の訂正を含む） |
| r6-calculation-corr-5 | 同正誤について（その5） | 厚生労働省 | 2024-06-14 | 2024-06-14 | 2026-07-10 | https://www.mhlw.go.jp/content/001264340.pdf | 8d0bc5bdb3895eda2c6b127a87f54bed5f52a361836de0d2e4684a8bd10fb43b | null | [r6-calculation-note] | null | 情報公表未報告・業務継続計画未策定等の訂正を含む | r6-calculation-noteを訂正（情報公表未報告・業務継続計画未策定等の訂正を含む） |
| r6-calculation-corr-6 | 同正誤について（その6） | 厚生労働省 | 2024-08-29 | 2024-08-29 | 2026-07-10 | https://www.mhlw.go.jp/content/001297223.pdf | ae8d1a2ca574cad9870e628818e6a536b87ab4a4117e468b49e2004cc04e406c | null | [r6-calculation-note] | null | 目標工賃達成加算の年度・例示を訂正 | r6-calculation-noteを訂正（目標工賃達成加算の年度・例示を訂正） |
| r6-calculation-corr-7 | 同正誤について（その7） | 厚生労働省 | 2025-01-29 | 2025-01-29 | 2026-07-10 | https://www.mhlw.go.jp/content/001389280.pdf | 3ac7bf7826e25b41f06842916a24222a7d6af95037ba0ec5b50cf5346ee0fb98 | null | [r6-calculation-note] | null | B型項目の訂正を含む | r6-calculation-noteを訂正（B型項目の訂正を含む） |
| r6-calculation-corr-8 | 同正誤について（その8） | 厚生労働省 | 2025-05-23 | 2025-05-23 | 2026-07-10 | https://www.mhlw.go.jp/content/001494351.pdf | e82f8627753508f381fd1cd226830ce78379143b68c86e2ceeb5694236085433 | null | [r6-calculation-note] | null | B型の処遇改善加算に関する訂正を含む | r6-calculation-noteを訂正（B型の処遇改善加算に関する訂正を含む） |
| r6-qa-corr-1 | Q&A VOL.1の正誤について | 厚生労働省 | 2024-04-05 | 2024-04-05 | 2026-07-10 | https://www.mhlw.go.jp/content/001241668.pdf | 81db930576745d6b7b53e82e0105805ec182f9d294118a6f822f9b8b3a3ad246 | null | [r6-qa-v1] | null | 目標工賃達成加算の訂正 | r6-qa-v1を訂正（目標工賃達成加算の訂正） |
| r6-qa-corr-2 | Q&A VOL.1の正誤（その2）及びVOL.2の正誤について | 厚生労働省 | 2024-04-26 | 2024-04-26 | 2026-07-10 | https://www.mhlw.go.jp/content/001250239.pdf | ee37c492b6989ad874f4be0f8febddab2ff351e6fe5bb0afff04223a7e2df8df | null | [r6-qa-v1,r6-qa-v2] | null | 目標工賃達成加算・平均工賃算定の訂正を含む | r6-qa-v1、r6-qa-v2を訂正（目標工賃達成加算・平均工賃算定の訂正を含む） |
| r6-qa-corr-3 | Q&A VOL.1の正誤（その3）について | 厚生労働省 | 2024-06-04 | 2024-06-04 | 2026-07-10 | https://www.mhlw.go.jp/content/001260428.pdf | 0a5b18d0d1f413d1599861db90d4735bb8a39c99cb6d48e4e0e45fa090f7c57a | null | [r6-qa-v1] | null | 目標工賃達成加算の確定訂正 | r6-qa-v1を訂正（目標工賃達成加算の確定訂正） |
| r6-capability-202404 | 体制等状況一覧表（令和6年4月〜） | 厚生労働省 | 2024-04-01 | null | 2026-07-10 | https://www.mhlw.go.jp/content/001253260.xlsx | fa24cd44e81cf1f1118b4a4c8a0b28bce31ee5227f13b9f8baa260ea6f223531 | null | null | null | 2024-04、2024-05サービス月に使用 | null |
| r6-capability-202406 | 体制等状況一覧表（令和6年6月〜） | 厚生労働省 | 2024-06-01 | null | 2026-07-10 | https://www.mhlw.go.jp/content/001253309.xlsx | d1edf9715b8c41660d6e4278ebd886861d0758c75109e4efc594f5d70f197c50 | [r6-capability-202404] | null | null | 2025-01-31確定コードと併用 | r6-capability-202404を置換（2025-01-31確定コードと併用） |
| r6-reward-structure | 障害福祉サービス費等の報酬算定構造（令和6年4月、6月施行分） | 厚生労働省 | 2024-04-01 | 2024-03-29 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/2024040100006.pdf | 451a5013d0d0f8009d5021bafc259c37c6d156bd9e5eaccd38cbfc166a22f1f9 | null | null | [r6-fee-notice] | 2024-06-01施行分を含む。基本報酬・加算・減算を告示と相互照合 | r6-fee-noticeを補足（2024-06-01施行分を含む。基本報酬・加算・減算を告示と相互照合） |
| r6-service-codes-1-pdf | 介護給付費等単位数サービスコード（令和6年4月、6月施行分・分割1・確定版PDF） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/0131001.pdf | 59a7ef23aac84a325705943d3278ad0556471cd1725c51a1ece6b03e76b3b1bf | null | null | [r6-fee-notice,r6-reward-structure] | 2024-06-01施行分を含む。分割1にB型シートはなく、B型マスタの入力には使用しない | r6-fee-notice、r6-reward-structureを補足（2024-06-01施行分を含む。分割1にB型シートはなく、B型マスタの入力には使用しない） |
| r6-service-codes-1-xlsx | 介護給付費等単位数サービスコード（令和6年4月、6月施行分・分割1・確定版Excel） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/0131002.xlsx | 5518200dbf66c6f7dcda29ed6a451add32cf1ca6c2623ef3772179a097ffdd6a | null | null | [r6-service-codes-1-pdf] | 2024-06-01施行分を含む。分割1にB型シートはなく、B型マスタの入力には使用しない | r6-service-codes-1-pdfを補足（2024-06-01施行分を含む。分割1にB型シートはなく、B型マスタの入力には使用しない） |
| r6-service-codes-2-pdf | 介護給付費等単位数サービスコード（令和6年4月、6月施行分・分割2・確定版PDF） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129007.pdf | 708270200599de9fb7d15d7270997286c3671d378e0a00e3b186a946e67b4465 | null | null | [r6-fee-notice,r6-reward-structure] | 2024-06-01施行分を含む。就労継続支援B型サービスコード表を収載 | r6-fee-notice、r6-reward-structureを補足（2024-06-01施行分を含む。就労継続支援B型サービスコード表を収載） |
| r6-service-codes-2-xlsx | 介護給付費等単位数サービスコード（令和6年4月、6月施行分・分割2・確定版Excel） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129010.xlsx | 4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82 | null | null | [r6-service-codes-2-pdf] | 2024-06-01施行分を含む。ワークブック順38〜41がB型の基本、定員超過、生活支援員等欠員、サービス管理責任者欠員。マスタ転記の正本 | r6-service-codes-2-pdfを補足（2024-06-01施行分を含む。ワークブック順38〜41がB型の基本、定員超過、生活支援員等欠員、サービス管理責任者欠員。マスタ転記の正本） |
| r6-claim-decision-202404-pdf | 請求サービスコードと決定サービスコード対応表（令和6年4月版・確定版PDF） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129012.pdf | b2591a682b95cc3120fe2d24fdc8d6686fadadefc5ba1e2264310e41dadfb971 | null | null | null | Excelと同一版 | null |
| r6-claim-decision-202404-xls | 同対応表（令和6年4月版・確定版Excel） | 厚生労働省 | 2024-04-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129013.xls | 1853640e49de6c671c182460435431e24acbc3a57587ce0285b714386ae55797 | null | null | [r6-claim-decision-202404-pdf] | マスタ転記の正本 | r6-claim-decision-202404-pdfを補足（マスタ転記の正本） |
| r6-claim-decision-202406-pdf | 請求サービスコードと決定サービスコード対応表（令和6年6月版・確定版PDF） | 厚生労働省 | 2024-06-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129015.pdf | 6a9219573228846f496b11dbdd81bf557c039db381c4515c654c2c9bded45de5 | [r6-claim-decision-202404-pdf] | null | null | Excelと同一版 | r6-claim-decision-202404-pdfを置換（Excelと同一版） |
| r6-claim-decision-202406-xls | 同対応表（令和6年6月版・確定版Excel） | 厚生労働省 | 2024-06-01 | 2025-01-31 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129016.xls | f361e7a518439c825e84d5cc0165afab8d17410dddebc6afb4e00832f6345c98 | [r6-claim-decision-202404-xls] | null | [r6-claim-decision-202406-pdf] | マスタ転記の正本 | r6-claim-decision-202404-xlsを置換。r6-claim-decision-202406-pdfを補足（マスタ転記の正本） |

#### 令和8資料

| documentId | title | publisher | effectiveAt | publishedAt | retrievedAt | url | sha256 | supersedes | corrects | supplements | applicabilityNote | correctionNote |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| r8-revision-overview | 令和8年度障害福祉サービス等報酬改定における改定事項について | 厚生労働省 | 2026-06-01 | 2026-02-18 | 2026-07-10 | https://www.mhlw.go.jp/content/001680064.pdf | 91bb6b34e19617ff7deac20406875fa977e3788cabfe2ed0ef7e95a79cd23900 | null | null | null | 告示・通知で確定値を照合 | null |
| r8-fee-notice | 指定障害福祉サービス等及び基準該当障害福祉サービスに要する費用の額の算定に関する基準等の一部を改正する告示（令和8年こども家庭庁・厚生労働省告示第5号） | こども家庭庁・厚生労働省 | 2026-06-01 | 2026-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001684450.pdf | f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c | [r6-fee-notice] | null | null | 通知・構造表と相互照合 | r6-fee-noticeを置換（通知・構造表と相互照合） |
| r8-calculation-note | 指定障害福祉サービス等の費用算定基準等の制定に伴う実施上の留意事項について | 厚生労働省 | 2026-06-01 | 2026-05-28 | 2026-07-10 | https://www.mhlw.go.jp/content/001705650.pdf | 0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99 | [r6-calculation-note] | null | [r8-fee-notice] | 2026-05-28改正を含む現行ファイル | r6-calculation-noteを置換。r8-fee-noticeを補足（2026-05-28改正を含む現行ファイル） |
| r8-b-reward-band-guide | 別添資料 就労継続支援B型の基本報酬区分の基準の見直しについて | 厚生労働省 | 2026-06-01 | 2026-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001683311.pdf | 96b002a6aecf76cbf2141fc53aee1c803e7cf78ba2dca52adbf755190e59ab5e | null | null | [r8-fee-notice,r8-calculation-note] | 報酬算定構造で確定値を照合 | r8-fee-notice、r8-calculation-noteを補足（報酬算定構造で確定値を照合） |
| r8-employment-transition-guide | 別紙6 就労移行支援体制加算の見直し | 厚生労働省 | 2026-06-01 | 2026-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001683303.pdf | 4bae0d014086f89d400c442ed7413142a6feb4c4bdbcbfd69d06832d90885047 | null | null | [r8-fee-notice] | 告示・サービスコードと併用する補助資料 | r8-fee-noticeを補足（告示・サービスコードと併用する補助資料） |
| r8-qa-v1 | 令和8年度障害福祉サービス等報酬改定等に関するQ&A VOL.1 | 厚生労働省 | 2026-03-31 | 2026-03-31 | 2026-07-10 | https://www.mhlw.go.jp/content/001683290.pdf | e2b95e451418c928e6e2ec7e05360d1810079fa81fa70acfe76fe91126276e78 | null | null | [r8-fee-notice,r8-calculation-note] | 取得時点で別掲正誤なし | r8-fee-notice、r8-calculation-noteを補足（取得時点で別掲正誤なし） |
| r8-amendment-qa | 実施上の留意事項通知等の発出に伴うQ&A | 厚生労働省 | 2026-05-28 | 2026-05-28 | 2026-07-10 | https://www.mhlw.go.jp/content/001705652.pdf | 68811f40164727ffa7165d53bd6a090363f23a7cf5aaacd2302ded41add2ad35 | null | null | [r8-calculation-note,r8-qa-v1] | 人員欠如の共通取扱い。B型にも適用 | r8-calculation-note、r8-qa-v1を補足（人員欠如の共通取扱い。B型にも適用） |
| r8-capability-202606 | 体制状況一覧表（令和8年6月版） | 厚生労働省 | 2026-06-01 | 2026-04-13 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001689932.xlsx | 84ff0b3b34c2ef857a1bcec221b8c276c177678b403ca6e171b2a08a6d8a150b | [r6-capability-202406] | null | null | 修正箇所資料と一組で使用 | r6-capability-202406を置換（修正箇所資料と一組で使用） |
| r8-capability-correction | 体制状況一覧表（令和8年6月版）（修正箇所） | 厚生労働省 | 2026-06-01 | 2026-04-13 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001689933.pdf | 06414c8aad4c014f44fd211dac141d152f30135fb622cdd32874e1c6bccbd980 | null | [r8-capability-202606] | null | 単独マスタにせず掲載Excelの検証根拠とする | r8-capability-202606を訂正（単独マスタにせず掲載Excelの検証根拠とする） |
| r8-reward-structure | 障害福祉サービス費等の報酬算定構造（令和8年6月施行分） | 厚生労働省 | 2026-06-01 | null | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001663319.pdf | 8c2638482364d57c7a42c87f5a4ff0dc9a58b0f32c1908ac3702e2a1c35a2b49 | [r6-reward-structure] | null | [r8-fee-notice] | B型のR8対象行だけを適用条件で選択 | r6-reward-structureを置換。r8-fee-noticeを補足（B型のR8対象行だけを適用条件で選択） |
| r8-service-codes-1-pdf | 介護給付費等単位数サービスコード（令和8年6月施行分・分割1・PDF） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696433.pdf | db9c20a55c40f831537b9d59eaa02d54906544432397e8f1914d0098b2578760 | [r6-service-codes-1-pdf] | null | [r8-reward-structure] | 分割1にB型シートはなく、B型マスタの入力には使用しない | r6-service-codes-1-pdfを置換。r8-reward-structureを補足（分割1にB型シートはなく、B型マスタの入力には使用しない） |
| r8-service-codes-1-xlsx | 介護給付費等単位数サービスコード（令和8年6月施行分・分割1・Excel） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696436.xlsx | b0d5495bdc684cd5c3e6e4f8c97dc247f169df7fd628660e085014564f266908 | [r6-service-codes-1-xlsx] | null | [r8-service-codes-1-pdf] | 分割1にB型シートはなく、B型マスタの入力には使用しない | r6-service-codes-1-xlsxを置換。r8-service-codes-1-pdfを補足（分割1にB型シートはなく、B型マスタの入力には使用しない） |
| r8-service-codes-2-pdf | 介護給付費等単位数サービスコード（令和8年6月施行分・分割2・PDF） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696434.pdf | 0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445 | [r6-service-codes-2-pdf] | null | [r8-reward-structure] | 就労継続支援B型サービスコード表を収載 | r6-service-codes-2-pdfを置換。r8-reward-structureを補足（就労継続支援B型サービスコード表を収載） |
| r8-service-codes-2-xlsx | 介護給付費等単位数サービスコード（令和8年6月施行分・分割2・Excel） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696437.xlsx | 307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049 | [r6-service-codes-2-xlsx] | null | [r8-service-codes-2-pdf] | ワークブック順38〜41がB型の基本、定員超過、生活支援員等欠員、サービス管理責任者欠員。マスタ転記の正本 | r6-service-codes-2-xlsxを置換。r8-service-codes-2-pdfを補足（ワークブック順38〜41がB型の基本、定員超過、生活支援員等欠員、サービス管理責任者欠員。マスタ転記の正本） |
| r8-claim-decision-pdf | 請求サービスコードと決定サービスコード対応表（令和8年6月版PDF） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696448.pdf | 26d10690efee3b26a21d219e32ffdc92892cad6044d4414864c853e09e15033e | [r6-claim-decision-202406-pdf] | null | null | 「決定サービスごとの設定内容は変更なし」とは別資料 | r6-claim-decision-202406-pdfを置換（「決定サービスごとの設定内容は変更なし」とは別資料） |
| r8-claim-decision-xls | 請求サービスコードと決定サービスコード対応表（令和8年6月版Excel） | 厚生労働省 | 2026-06-01 | 2026-04-28 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696450.xls | dd11bfc99831aab4d0ec6eaef1e97fc52cbaf7f15c036f6edd114fa36fd249b2 | [r6-claim-decision-202406-xls] | null | [r8-claim-decision-pdf] | マスタ転記の正本 | r6-claim-decision-202406-xlsを置換。r8-claim-decision-pdfを補足（マスタ転記の正本） |

### 採用理由

- URLだけでなく取得バイトのSHA-256を固定するため、公式サイトで同じURLの差替えがあっても過去請求の根拠を識別できる。
- 2024-04、2024-06、2026-06を別版にするため、境界月でコード改修なしに正しい資料束を選択できる。
- 告示、留意事項、構造表、サービスコード、体制表、Q&A、正誤を相互参照するため、一資料だけの誤読や未反映を防げる。
- 「変更なし」を明示的な版選択根拠にするため、存在しない令和8帳票版を推測作成しない。
- 制度値を外部マスタに限定するため、オフライン要件を守りつつ、改定時にC#の算定ロジックを変更せず差し替えられる。

## 影響

- Phase 3-1以降の外部マスタと出典カタログは、本ADRのdocumentId、適用月、SHA-256を使用する。
- claim-master-r6-04、claim-master-r6-06、claim-master-r8-06の境界と、2026-05／2026-06切替をテスト対象にする。
- B型の基本報酬区分、加算・減算、サービスコード、体制コードを実装する際は、PDFの目視転記ではなく、同版Excelと告示・構造表の照合結果を外部JSONへ格納する。
- 公式資料の新しい正誤、差替え、または施行日が公表された場合は、既存版を上書きせず、新documentId・新SHA-256・新しい適用期間を追記する。
- 取得物はリポジトリに含めないため、アプリの配布物・本番アセンブリは引き続きオフラインで完結する。
