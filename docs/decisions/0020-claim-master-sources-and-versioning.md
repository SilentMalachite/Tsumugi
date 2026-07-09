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
| additions / deductions | r6-fee-notice、r6-calculation-note、r6-service-codes-pdf／xlsx | 令和6年6月施行分と正誤を反映 | r8-fee-notice、r8-calculation-note、r8-service-codes-pdf／xlsx |
| region-unit-prices | mhlw-unit-price-notice と r6-revision-overview | 同左 | 同左。令和8改定で単価表の置換なし |
| office-capabilities | r6-capability-202404 | r6-capability-202406 | r8-capability-202606 と修正箇所資料 |
| service-codes | r6-service-codes-pdf／xlsxの2025-01-31確定版から2024-04適用行を選択 | 同確定版から2024-06適用行を選択 | r8-service-codes-pdf／xlsx |
| claim-to-decision-code mapping | r6-claim-decision-202404-pdf／xls | r6-claim-decision-202406-pdf／xls | r8-claim-decision-pdf／xls |

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

令和8年6月の公式ページで「変更なし」とされている実績記録票、実績記録票の記載例、請求書明細書、請求書明細書の記載例、決定サービスごとの設定内容には、空の令和8版や複製版を作らない。これらは既存の直近版を継続する。一方、同じページの「請求サービスコードと決定サービスコード対応表」は令和8年6月版が新規掲載されているため、両者を混同せず r8-claim-decision を選択する。

各外部マスタは documentId、title、publisher、effectiveAt、retrievedAt、url、sha256、supersedes、correctionNote を保持する。同じURLの内容が後日差し替わりSHA-256が変わった場合は、既存documentIdの値を上書きせず、新しいdocumentIdと取得日で登録する。必要資料、適用版、訂正関係のいずれかが欠ける場合は算定を停止し、近い値や前版を推測適用しない。

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
- r8-calculation-noteは2026年5月28日改正版であり、2026年3月31日版を置換する。
- r8-capability-202606は同ページ掲載の修正箇所資料と一組で扱う。Excelだけ、または修正箇所PDFだけを単独の正本にしない。
- 基本報酬の全区分・定員別単位、加算率、減算率、サービスコードは報酬算定構造とサービスコードExcelを外部マスタへ転記する。人手でC#へ再入力しない。

### 公式資料カタログ

retrievedAtは全て2026-07-10である。publisherは、別記がない限り厚生労働省である。

#### 入口・地域区分単価

| documentId | title | publisher | effectiveAt | retrievedAt | url | sha256 | supersedes | correctionNote |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| mhlw-r6-revision-page | 令和6年度障害福祉サービス等報酬改定 | 厚生労働省 | 2024-04-01／2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00009.html | c7b78655086e6f5d9276c1a84dad5e20826d8bbbbd7e9bd44c15142facedf923 | 令和3年度等の旧改定ページ | 正誤その8までと現行差替え先を確認する入口 |
| mhlw-r6-structure-page | 報酬算定構造・サービスコード表等 令和6年4月、6月施行分 | 厚生労働省 | 2024-04-01／2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00018.html | 8a9858bf575af9bb57a3a5df61428e66e8d903bf984de9f6b46b3fd47631a0c7 | 初回掲載状態 | 2024-11-29修正、2025-01-31確定分を同ページで確認 |
| mhlw-r8-revision-page | 令和8年度障害福祉サービス等報酬改定 | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00013.html | 2e8f142511e9c6a5c9125c4dbaec6c05062cd5fb2e65ea2b0ba925f639b6c395 | mhlw-r6-revision-pageの対象規定 | 取得時点で正誤資料の別掲なし。2026-05-28改正通知を反映 |
| mhlw-r8-structure-page | 報酬算定構造・サービスコード表等 令和8年6月施行分 | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html | 9bc71ce6bfdfe2461a3068dd2ad924a215c12d3f071e2034a8ca51b51de49622 | mhlw-r6-structure-pageのB型対象資料 | 2026-04-28更新。「変更なし」と新規掲載項目の根拠 |
| mhlw-unit-price-notice | こども家庭庁長官及び厚生労働大臣が定める一単位の単価並びに厚生労働大臣が定める一単位の単価（厚生労働省告示第539号・現行条文） | こども家庭庁・厚生労働省 | 2006-10-01。R6〜R8は現行条文を適用 | 2026-07-10 | https://www.mhlw.go.jp/web/t_doc?dataId=83aa8493&dataType=0&pageNo=1 | 946c3d969ffd4128db15106d25ce6d26ff108f5460a7618e3df96352e42c0c1b | 同告示の旧条文 | 動的現行条文のため、変更時は再取得して新documentIdとする |

#### 令和6資料

| documentId | title | publisher | effectiveAt | retrievedAt | url | sha256 | supersedes | correctionNote |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| r6-revision-overview | 令和6年度障害福祉サービス等報酬改定の概要 | 厚生労働省 | 2024-04-01。地域区分は令和6〜8年度 | 2026-07-10 | https://www.mhlw.go.jp/content/001216035.pdf | 8b66f74bd44cceeb3a0556f8055d10d997199097ed2f9715f79df6cd6fc94f75 | 令和3年度改定概要 | 地域区分の年度別適用地域を確認 |
| r6-fee-notice | 指定障害福祉サービス等及び基準該当障害福祉サービスに要する費用の額の算定に関する基準等の一部を改正する告示（令和6年こども家庭庁・厚生労働省告示第3号） | こども家庭庁・厚生労働省 | 2024-04-01。一部2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001239565.pdf | 5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54 | 改正前の報酬告示 | 留意事項・正誤・構造表と併用 |
| r6-calculation-note | 指定障害福祉サービス等の費用算定基準等の制定に伴う実施上の留意事項について | 厚生労働省 | R6改定箇所は2024-04-01／2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001494356.pdf | 958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1 | 2024-03-29改正版 | 公式ページの現行ファイル。2025-03-31改正まで含み、r6-calculation-corr-1／3／5／6／7／8を適用 |
| r6-employment-guidance | 就労移行支援事業、就労継続支援事業（A型、B型）における留意事項について | 厚生労働省 | 施行日なし。2025-03-31通知。2025-10-01開始事項を含む | 2026-07-10 | https://www.mhlw.go.jp/content/001473458.pdf | 185fa99281ea97b5724297041f8bcbb50c92f9d3882496d3d9a3227328b17481 | 令和6改定時の同通知 | 公式ページの現行差分。2024-03-29版にはr6-employment-guidance-corr-4を適用。R6サービス月にはR6当時の適用条項だけを使用 |
| r6-employment-guidance-corr-4 | 令和6年度障害福祉サービス等関連通知の正誤について（その4） | 厚生労働省 | 施行日なし。2024-06-06公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001261442.pdf | ee76aa76e2e0f76b38d4282142291501f9982a818d1a9a6ac3e6a343d9de6626 | r6-employment-guidanceの2024-03-29版の一部 | 就労移行支援事業、就労継続支援（A型、B型）の施設外支援等を訂正 |
| r6-qa-v1 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.1 | 厚生労働省 | 施行日なし。2024-03-29公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001260473.pdf | 88309c362a23aac08fce7947bb4aadcc892531164ad7d56a37188ca01be1d95a | 旧改定Q&Aの対象回答 | r6-qa-corr-1／2／3を適用 |
| r6-qa-v2 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.2 | 厚生労働省 | 施行日なし。2024-04-05公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001250243.pdf | bd773dfe577b8a83fd99b28fd10f8cdc1b55d47114a03fd64c128777e54e6b8d | r6-qa-v1の補足 | r6-qa-corr-2を適用 |
| r6-qa-v3 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.3 | 厚生労働省 | 施行日なし。2024-05-10公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001253308.pdf | 8968925cfae7a563d68fa2b7131e19c7746e30e3a65b91b37ff7c3812c90e2b4 | r6-qa-v1／2の補足 | 取得時点でB型項目の別掲正誤なし |
| r6-qa-v5 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.5 | 厚生労働省 | 施行日なし。2024-08-29公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001297221.pdf | 5a5c07db593c2d75a14fa12714ed9ed112d62a00feb3ffd7855cc7e1f6ee5eb7 | r6-qa-v1／2／3の補足 | B型に適用される食事提供体制加算と、B型事業所を実施主体とする就労定着支援を確認 |
| r6-qa-v6 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.6 | 厚生労働省 | 施行日なし。2024-10-11公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001316215.pdf | 14346b90b13893e201bcad9f1db325becf5b109353b92b0ace362987ffd55802 | r6-qa-v5の補足 | B型に適用される送迎加算の委託・共同送迎を確認 |
| r6-qa-v7 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.7 | 厚生労働省 | 施行日なし。2025-01-24公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001385141.pdf | 943ed022a46f706adc23e2a6c40724acf7c994c4549d0b1df709c335c46215f5 | r6-qa-v6の補足 | 就労継続支援の就労移行支援体制加算を確認 |
| r6-qa-v8 | 令和6年度障害福祉サービス等報酬改定等に関するQ&A VOL.8 | 厚生労働省 | 施行日なし。2025-03-31公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001471548.pdf | 09187ef8d8f88edf95e3454fec96eb39c1e455a7fd72f76eae871fab6c40b340 | r6-qa-v7の補足 | 就労継続支援（A型、B型）の施設外就労と基本報酬を確認 |
| r6-calculation-corr-1 | 令和6年度障害福祉サービス等報酬改定関連通知の正誤について | 厚生労働省 | 施行日なし。2024-04-04公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001241205.pdf | e17e55bd7698a0343bf7c688afb7ca1a8447aa27dbd8a20070e6c9e95a8cecdb | r6-calculation-noteの一部 | B型に適用される共通・個別訂正を含む |
| r6-calculation-corr-3 | 同正誤について（その3） | 厚生労働省 | 施行日なし。2024-06-04公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001260426.pdf | d179e42bc7001da63e2f720148e7ceeec0ad35086a6707acea394eb887880332 | r6-calculation-noteの一部 | B型項目の訂正を含む |
| r6-calculation-corr-5 | 同正誤について（その5） | 厚生労働省 | 施行日なし。2024-06-14公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001264340.pdf | 8d0bc5bdb3895eda2c6b127a87f54bed5f52a361836de0d2e4684a8bd10fb43b | r6-calculation-noteの一部 | 情報公表未報告・業務継続計画未策定等、B型に適用される訂正を含む |
| r6-calculation-corr-6 | 同正誤について（その6） | 厚生労働省 | 施行日なし。2024-08-29公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001297223.pdf | ae8d1a2ca574cad9870e628818e6a536b87ab4a4117e468b49e2004cc04e406c | r6-calculation-noteの一部 | B型の目標工賃達成加算に関する年度・例示を訂正 |
| r6-calculation-corr-7 | 同正誤について（その7） | 厚生労働省 | 施行日なし。2025-01-29公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001389280.pdf | 3ac7bf7826e25b41f06842916a24222a7d6af95037ba0ec5b50cf5346ee0fb98 | r6-calculation-noteの一部 | B型項目の訂正を含む |
| r6-calculation-corr-8 | 同正誤について（その8） | 厚生労働省 | 施行日なし。2025-05-23公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001494351.pdf | e82f8627753508f381fd1cd226830ce78379143b68c86e2ceeb5694236085433 | r6-calculation-noteの一部 | B型の処遇改善加算に関する訂正を含む |
| r6-qa-corr-1 | Q&A VOL.1の正誤について | 厚生労働省 | 施行日なし。2024-04-05公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001241668.pdf | 81db930576745d6b7b53e82e0105805ec182f9d294118a6f822f9b8b3a3ad246 | r6-qa-v1の一部 | 目標工賃達成加算の訂正 |
| r6-qa-corr-2 | Q&A VOL.1の正誤（その2）及びVOL.2の正誤について | 厚生労働省 | 施行日なし。2024-04-26公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001250239.pdf | ee37c492b6989ad874f4be0f8febddab2ff351e6fe5bb0afff04223a7e2df8df | r6-qa-v1／2の一部 | 目標工賃達成加算・平均工賃算定の訂正を含む |
| r6-qa-corr-3 | Q&A VOL.1の正誤（その3）について | 厚生労働省 | 施行日なし。2024-06-04公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001260428.pdf | 0a5b18d0d1f413d1599861db90d4735bb8a39c99cb6d48e4e0e45fa090f7c57a | r6-qa-v1の一部 | 目標工賃達成加算の確定訂正 |
| r6-capability-202404 | 体制等状況一覧表（令和6年4月〜） | 厚生労働省 | 2024-04-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001253260.xlsx | fa24cd44e81cf1f1118b4a4c8a0b28bce31ee5227f13b9f8baa260ea6f223531 | 令和3年版体制表 | 令和6年6月分はr6-capability-202406を使用 |
| r6-capability-202406 | 体制等状況一覧表（令和6年6月〜） | 厚生労働省 | 2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001253309.xlsx | d1edf9715b8c41660d6e4278ebd886861d0758c75109e4efc594f5d70f197c50 | r6-capability-202404 | mhlw-r6-structure-pageの2025-01-31確定コードと併用 |
| r6-reward-structure | 障害福祉サービス費等の報酬算定構造（令和6年4月、6月施行分） | 厚生労働省 | 2024-04-01／2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/2024040100006.pdf | 451a5013d0d0f8009d5021bafc259c37c6d156bd9e5eaccd38cbfc166a22f1f9 | 令和3／4年構造表 | B型の基本報酬・加算・減算を告示と相互照合 |
| r6-service-codes-pdf | 介護給付費等単位数サービスコード（令和6年4月、6月施行分・確定版PDF） | 厚生労働省 | 2024-04-01／2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/0131001.pdf | 59a7ef23aac84a325705943d3278ad0556471cd1725c51a1ece6b03e76b3b1bf | 2024-04-24掲載版 | 2025-01-31確定。Excelと同一版として扱う |
| r6-service-codes-xlsx | 介護給付費等単位数サービスコード（令和6年4月、6月施行分・確定版Excel） | 厚生労働省 | 2024-04-01／2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/0131002.xlsx | 5518200dbf66c6f7dcda29ed6a451add32cf1ca6c2623ef3772179a097ffdd6a | 2024-04-24掲載版 | 2025-01-31確定。マスタ転記の正本 |
| r6-claim-decision-202404-pdf | 請求サービスコードと決定サービスコード対応表（令和6年4月版・確定版PDF） | 厚生労働省 | 2024-04-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129012.pdf | b2591a682b95cc3120fe2d24fdc8d6686fadadefc5ba1e2264310e41dadfb971 | 初回掲載版 | 2025-01-31確定。Excelと同一版 |
| r6-claim-decision-202404-xls | 同対応表（令和6年4月版・確定版Excel） | 厚生労働省 | 2024-04-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129013.xls | 1853640e49de6c671c182460435431e24acbc3a57587ce0285b714386ae55797 | 初回掲載版 | 2025-01-31確定。マスタ転記の正本 |
| r6-claim-decision-202406-pdf | 請求サービスコードと決定サービスコード対応表（令和6年6月版・確定版PDF） | 厚生労働省 | 2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129015.pdf | 6a9219573228846f496b11dbdd81bf557c039db381c4515c654c2c9bded45de5 | r6-claim-decision-202404-pdf | 2025-01-31確定。Excelと同一版 |
| r6-claim-decision-202406-xls | 同対応表（令和6年6月版・確定版Excel） | 厚生労働省 | 2024-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129016.xls | f361e7a518439c825e84d5cc0165afab8d17410dddebc6afb4e00832f6345c98 | r6-claim-decision-202404-xls | 2025-01-31確定。マスタ転記の正本 |

#### 令和8資料

| documentId | title | publisher | effectiveAt | retrievedAt | url | sha256 | supersedes | correctionNote |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| r8-revision-overview | 令和8年度障害福祉サービス等報酬改定における改定事項について | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001680064.pdf | 91bb6b34e19617ff7deac20406875fa977e3788cabfe2ed0ef7e95a79cd23900 | r6-revision-overviewの対象事項 | 2026-02-18公表。告示・通知で確定値を照合 |
| r8-fee-notice | 指定障害福祉サービス等及び基準該当障害福祉サービスに要する費用の額の算定に関する基準等の一部を改正する告示（令和8年こども家庭庁・厚生労働省告示第5号） | こども家庭庁・厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001684450.pdf | f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c | r6-fee-noticeの対象規定 | 2026-03-31公布。通知・構造表と相互照合 |
| r8-calculation-note | 指定障害福祉サービス等の費用算定基準等の制定に伴う実施上の留意事項について | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001705650.pdf | 0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99 | r6-calculation-noteの対象規定 | 2026-05-28改正を含む現行ファイル |
| r8-b-reward-band-guide | 別添資料 就労継続支援B型の基本報酬区分の基準の見直しについて | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001683311.pdf | 96b002a6aecf76cbf2141fc53aee1c803e7cf78ba2dca52adbf755190e59ab5e | r6-reward-structureの区分説明 | r8-fee-notice／r8-calculation-note／r8-reward-structureで確定値を照合 |
| r8-employment-transition-guide | 別紙6 就労移行支援体制加算の見直し | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/001683303.pdf | 4bae0d014086f89d400c442ed7413142a6feb4c4bdbcbfd69d06832d90885047 | r6同加算の運用 | 告示・サービスコードと併用する補助資料 |
| r8-qa-v1 | 令和8年度障害福祉サービス等報酬改定等に関するQ&A VOL.1 | 厚生労働省 | 施行日なし。2026-03-31公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001683290.pdf | e2b95e451418c928e6e2ec7e05360d1810079fa81fa70acfe76fe91126276e78 | R6 Q&Aの対象回答 | 取得時点で別掲正誤なし |
| r8-amendment-qa | 実施上の留意事項通知等の発出に伴うQ&A | 厚生労働省 | 施行日なし。2026-05-28公表 | 2026-07-10 | https://www.mhlw.go.jp/content/001705652.pdf | 68811f40164727ffa7165d53bd6a090363f23a7cf5aaacd2302ded41add2ad35 | r8-qa-v1の補足 | 人員欠如の共通取扱い。B型にも適用 |
| r8-capability-202606 | 体制状況一覧表（令和8年6月版） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001689932.xlsx | 84ff0b3b34c2ef857a1bcec221b8c276c177678b403ca6e171b2a08a6d8a150b | r6-capability-202406 | r8-capability-correctionを反映した掲載Excelを正本とする |
| r8-capability-correction | 体制状況一覧表（令和8年6月版）（修正箇所） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001689933.pdf | 06414c8aad4c014f44fd211dac141d152f30135fb622cdd32874e1c6bccbd980 | r8-capability-202606の修正前状態 | 単独マスタにせず掲載Excelの検証根拠とする |
| r8-reward-structure | 障害福祉サービス費等の報酬算定構造（令和8年6月施行分） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001663319.pdf | 8c2638482364d57c7a42c87f5a4ff0dc9a58b0f32c1908ac3702e2a1c35a2b49 | r6-reward-structureの対象規定 | B型のR8対象・対象外行を混在させず適用条件で選択 |
| r8-service-codes-pdf | 介護給付費等単位数サービスコード（令和8年6月施行分PDF） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696433.pdf | db9c20a55c40f831537b9d59eaa02d54906544432397e8f1914d0098b2578760 | r6-service-codes-pdf | 2026-04-28更新。Excelと同一版として扱う |
| r8-service-codes-xlsx | 介護給付費等単位数サービスコード（令和8年6月施行分Excel） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696436.xlsx | b0d5495bdc684cd5c3e6e4f8c97dc247f169df7fd628660e085014564f266908 | r6-service-codes-xlsx | 2026-04-28更新。マスタ転記の正本 |
| r8-claim-decision-pdf | 請求サービスコードと決定サービスコード対応表（令和8年6月版PDF） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696448.pdf | 26d10690efee3b26a21d219e32ffdc92892cad6044d4414864c853e09e15033e | r6-claim-decision-202406-pdf | 2026-04-28掲載。ページの「決定サービスごとの設定内容は変更なし」とは別資料 |
| r8-claim-decision-xls | 請求サービスコードと決定サービスコード対応表（令和8年6月版Excel） | 厚生労働省 | 2026-06-01 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001696450.xls | dd11bfc99831aab4d0ec6eaef1e97fc52cbaf7f15c036f6edd114fa36fd249b2 | r6-claim-decision-202406-xls | 2026-04-28掲載。マスタ転記の正本 |

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
