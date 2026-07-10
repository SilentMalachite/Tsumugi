# ADR 0024: 国保連CSVと請求帳票の全項目契約

## 決定

就労継続支援B型の国保連提出CSVは、令和7年10月版インタフェース仕様書の共通編と事業所編を正本とする。CSVはCP932（共通編の表記はシフトJIS）、CRLF、カンマ区切りで生成する。文字列、数値、コード値、漢字は、カンマ、ダブルコーテーション、空白または漢字を含む場合にダブルコーテーションで囲み、値中のダブルコーテーションは二重化する。

ファイルは外側のコントロールレコード、データレコード、エンドレコードの3種類から構成する。コントロールレコードは1件、データレコードはJ111、J121、J611の内側レコード件数分、エンドレコードは1件とする。外側19項目の定義表は共通編physical page 6であり、physical page 5はファイル形式と3レコード構成の説明である。外側19項目の定義表は共通編physical page 6であり、physical page 5はファイル形式と3レコード構成の説明である。外側19項目の定義表は共通編physical page 6であり、physical page 5はファイル形式と3レコード構成の説明である。外側19項目の定義表は共通編physical page 6であり、physical page 5はファイル形式と3レコード構成の説明である。外側19項目の定義表は共通編physical page 6であり、physical page 5はファイル形式と3レコード構成の説明である。外側データレコードの「データ」の`maxBytes`は、対象9レコードの公式最大バイト数と区切りカンマを合計した最大値822 bytesである。引用符と改行はこの値に含めない。

`ProcessingMonth`は外側コントロールレコードの「処理対象年月」専用の明示入力とする。内側レコードの「サービス提供年月」は別の`ServiceProvisionMonth`明示入力とし、翌月であることを推測して自動設定しない。過月請求、再請求、取消では両者が一致も連続もしないためである。

対象となる内側レコードは次の9種類である。

| exchangeInformationId | innerRecordType | 内容 | field数 | 事業所編physical page |
| --- | --- | --- | ---: | ---: |
| J111 | 01 | 介護給付費等請求書 基本情報 | 23 | 17 |
| J111 | 02 | 介護給付費等請求書 明細情報 | 14 | 18 |
| J121 | 01 | 介護給付費等明細書 基本情報 | 35 | 20 |
| J121 | 02 | 介護給付費等明細書 日数情報 | 12 | 22 |
| J121 | 03 | 介護給付費等明細書 明細情報 | 11 | 31 |
| J121 | 04 | 介護給付費等明細書 集計情報 | 33 | 32 |
| J121 | 05 | 介護給付費等明細書 契約情報 | 11 | 38 |
| J611 | 01 | サービス提供実績記録票 基本情報 | 172 | 60 |
| J611 | 02 | サービス提供実績記録票 明細情報 | 113 | 72 |

表のpageは各record定義の開始pageである。field単位ではJ121-01の項番1〜22をphysical page 20、23〜35をpage 21、J121-04の項番1〜17をpage 32、18〜33をpage 33として保持する。J611-01の定義はphysical pages 60〜69、様式1701必須表はpages 83〜86、J611-02の定義はpages 72〜78、様式1701必須表はpages 87〜89である。`sourcePage`は定義page、`requiredWhenSource`は必須表pageを分離して保持する。

`dataType`は項目説明中の年月条件ではなく、公式表の属性列から転記する。`provider:J121:01:030`、`provider:J121:04:012`、`:014`、`:015`、`:024`は順に数値10、数値3、数値10、数値6、数値10であり、いずれも`numeric`とする。J121-04項番15はphysical page 32の定義どおり項番14の1割相当額をコピーし、独立入力やmigrationを追加しない。

共通編19項目と事業所編424項目の合計443項目を`field-mapping-r7-10.json`で全域写像にする。各項目の入力源は`existing`、`missing`、`explicitInput`、`generated`のいずれか一つである。J611の使用可否は令和7年10月以降の様式1701必須項目表に従い、使用しない列は空項目を決定論的に生成する。自由記述からの推定、複数候補、空のモデルパスは認めない。

請求帳票はCSV列から生成しない。実績記録票41項目、介護給付費・訓練等給付費等請求書24項目、同明細書48項目を公式様式・記載例から独立して棚卸しし、合計113項目を`report-fields-r8-06.json`に固定する。同義のCSV項目は`sameMeaningAsCsvFieldId`で参照するだけとする。

実績記録票41項目には、令和6年4月・6月施行分Excelの`就労継続支援!C36:W37`にある静的な「合計」ラベルを1項目として含める。このslotは数値ではなく`const(合計)`であり、サービス提供日数やCSV fieldとの同義参照を持たない。公式layoutの静的文字もinventory対象とする既存方針を維持するため、項目を削除せず41件を維持する。

J121-04項番9のサービス利用日数は`DailyRecord`の架空propertyから数えない。Phase 3-1の`ClaimDetailSnapshot.CalculatedClaimLines`契約をsourceとし、本体報酬、加算、上限額管理加算、欠席時対応加算の算定済みlineに含まれる`ServiceDate`のdistinct集合を数える。同日の複数lineは1日、加算のみの日も1日とする。

## 版選択

- CSV仕様は、公式索引に掲載された令和7年10月版共通編・事業所編を使用する。
- 請求書・明細書は平成31年10月施行分の障害福祉命令様式Excelと記載例PDFを正本にする。令和3年4月、令和4年10月、令和6年4月・6月、令和8年6月の各公式ページが「変更なし」としているため、令和8年6月にも継続適用する。
- 実績記録票は令和6年4月・6月施行分の障害福祉様式Excelと2025年1月31日確定記載例を正本にする。令和8年6月ページが「変更なし」としているため、様式17を令和8年6月にも継続適用する。
- 現行の令和8年7月支給決定事務要領（`001721666.pdf`）physical pages 233〜235で、様式17のB型固有欄と記載条件を照合する。保持済み令和8年6月版の同pagesと正規化テキストSHA-256が3pageすべて一致し、様式17の記載規則に変更はない。
- `000535461.xls`は障害児命令様式であり、成人の就労継続支援B型帳票inventoryへ混入させない。


## 条件DSLと生成式

`requiredWhen`は次の閉じた文法だけを許可する。引数区切りは`;`とし、`all`、`any`、`not`は再帰的に同じconditionを取る。

```text
condition := always | optional | never
           | recordPresent(recordId) | rowPresent(reportSection)
           | fieldPresent(fieldId) | fieldNonZero(fieldId) | fieldEquals(fieldId;value)
           | modelPresent(modelPath) | modelTrue(boolModelPath) | modelNonZero(numericModelPath)
           | modelEquals(enumModelPath;enumValue) | modelIn(enumModelPath;enumValue...)
           | inputPresent(inputContract)
           | serviceProvisionMonthBefore(YYYYMM) | processingMonthBefore(YYYYMM)
           | all(condition;condition...) | any(condition;condition...) | not(condition)
```

`always`は公式表の`◎`、`optional`は`△`、条件式は`○`を具体的な入力・算定結果へ展開したもの、`never`はJ121または様式1701列が空白の項目である。各fieldは`requiredWhenSource`に必須表のphysical page、項番、様式1701列を保持する。注記だけ、または「該当時」という自然文はconditionとして扱わない。

`modelTrue`はboolだけ、`modelPresent`はnullable値型または参照型だけに使用する。enumは`modelEquals`または`modelIn`を使用し、reflectionで実在するenum値を検証する。`TransportKind`は`None / Outbound / Inbound / Round`、往は`Outbound / Round`、復は`Inbound / Round`、`Attendance`の欠席時対応は`AbsenceSupport`に固定する。

`generatorRule`は`operation(target=fieldId;具体入力;source=sourceDocumentId:pN:itemN)`の形式とする。operationは`aggregate`、`calendarDay`、`conditional`、`const`、`constEmpty`、`copy`、`count`、`difference`、`format`、`lookup`、`max`、`min`、`multiply`、`payload`、`recordCount`、`render`、`roundDown`、`sequence`、`sum`の閉集合である。全generated項目は一意のtarget、公式source、具体的なfield/model/selectorと演算を持ち、共通の自由記述規則へフォールバックしない。

## 出典

取得日は全て2026-07-10である。各URLは2回連続取得し、同一SHA-256とサイズを確認した。動的HTMLは観測バイトのIDとして扱い、別の観測バイトを同じ版へ暗黙に置換しない。

| sourceDocumentId | version | retrievedAt | url | sha256 | sizeBytes |
| --- | --- | --- | --- | --- | ---: |
| interface-index-r7-10 | 令和7年10月掲載索引 | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000045136.html | 921c9eaba31b0a005b442154f0a04362bbff84481cddf3e10245a02c1397151a | 15202 |
| common-r7-10 | 令和7年10月 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001565560.pdf | f6932c52a4fd1044cbdd5aa752baf52e1c2e1eda106d9fa54991423fbd04a9a9 | 482854 |
| provider-r7-10 | 令和7年10月 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001565561.pdf | 44e94a7817bde2b9c324701e40ca703f4d4b372164460e7ebba6d906c840def1 | 1725930 |
| r1-structure-page-observed-a814e583 | 2019-10 observed bytes | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00006.html | a814e5834ada2944d74c860f81cdca7da887a9b084bd57560f6ad9ce82ffcae4 | 9794 |
| claim-forms-r1-10-xls | 平成31年10月施行分 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/000535460.xls | bb10270da47e2f45d1527286e0c962fbe496e84513df64468dead363872b65da | 238592 |
| claim-examples-r1-10-pdf | 平成31年10月施行分 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/000535462.pdf | 8d6dd7dd6458ccd2b6a0ce4050eeeb8da2b9eb5849deecc91c1095779a6bd6aa | 280226 |
| r3-structure-page-observed-ec199786 | 2021-04 observed bytes | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00016.html | ec1997867f087eeba77cd2c07b69696d7df84b0745f65f16bc2c5a6a7225dab6 | 14453 |
| r4-structure-page-observed-e6a9105b | 2022-10 observed bytes | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00017.html | e6a9105b786cb649bc858714bd1f8fa20ecd553e287bb1fd9b3c3994b8216c31 | 11414 |
| r6-structure-page-observed-8a9858bf | 2025-01-31 observed bytes | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00018.html | 8a9858bf575af9bb57a3a5df61428e66e8d903bf984de9f6b46b3fd47631a0c7 | 57638 |
| service-performance-r6-04-xls | 令和6年4月・6月施行分 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/0329006.xls | 0c3e74eab4cef91021da4a973f667e4858374a6376c3cc7d8b4137e20e99bf49 | 863744 |
| service-performance-examples-r6-04-pdf | 2025-01-31確定分 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/20241129005.pdf | 2afb44f72d04fe6c376d115641cc542e7d82545284dd9eb40f5b66e14dbbc1d9 | 1401207 |
| r8-structure-page-observed-13da3c44 | 2026-06 observed bytes | 2026-07-10 | https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html | 13da3c44eb9e9485c38fda878bb1fe966407e7331609f87123ba2b2778a32083 | 51888 |
| r8-grant-decision-administration-202606 | 最終改正令和8年6月 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001470632.pdf | d6e1672245370d2d7bb9a4258622ae3e631d0a6144c8e0c9ea51e2018a146f1e | 1998305 |
| r8-grant-decision-administration-202607 | 最終改正令和8年7月 | 2026-07-10 | https://www.mhlw.go.jp/content/12200000/001721666.pdf | 1a94220c99986f353e4c63c095c156448271ecad1d7bf0d9e197d3b8ca06de65 | 1999016 |

令和8年6月版の取得済みPDF bytesは削除せず、歴史sourceとして保持する。旧URLは2026-07-10のlive確認でHTTP 404となり、404 responseは48,524 bytes、SHA-256 `62487501d53438999737baba39208b6f83de89280b31efcd804a99d193108ed8`だったため、旧sourceをlive reproducibleとは呼ばない。令和8年7月版は3回取得して全bytesが一致し、令和8年6月版をsupersedeする。帳票inventoryの直接sourceは各Excel・記載例PDFのままとし、支給決定事務要領は様式17記載規則の照合sourceとして版境界を分離する。

## 失敗時の扱い

必要なsourceDocumentId、版、適用月、SHA-256、レコード、field、mapping、入力値のいずれかが欠ける場合は生成を停止する。近い列、前版、自由記述、既定のゼロ、空文字から値を推測しない。未登録コード、非連続position、重複ID、マッピング集合の差、最大バイト超過、CP932へ変換できない文字も停止条件とする。

`missing`項目はPhase 3-1でモデル、migration、入力画面を追加するまで生成可能と扱わない。特に市町村番号、上限額管理結果、サービス開始・終了時間、実績記録票の加算入力を、既存の備考や真偽フラグから推定しない。

## 背景

国保連CSVは同じ帳票概念を持つが、外側レコードと複数の内側レコードを別々の位置・桁数・必須条件で表現する。帳票は法定様式として独自の欄と繰返し構造を持ち、CSVの列集合と一致しない。短縮した3レコード仕様やCSVからの帳票inventory生成では、日数、契約、上限額管理、様式17固有欄が欠落するため、一次資料から別々に全件固定する必要がある。

## 選択肢

- **A: 一次資料からCSVと帳票を独立棚卸しし、JSONで全域写像を検証する（採用）**
- **B: 帳票をCSV fieldから派生する**: 帳票固有欄と繰返し構造が失われる。
- **C: 実装時に必要列だけ追加する**: 欠落を検出できず、改定差分と入力不足を追跡できない。

## 影響

- Phase 3-1は`missing`一覧だけをモデル・migration・UI実装の入力契約として使う。
- CSV loader、writer、validatorは443項目のID、position、最大バイト数、requiredWhenを変更せず読む。
- 帳票生成は113項目の独立inventoryを使い、同義CSV項目は相互参照に限定する。
- 公式資料の差替え時は既存sourceDocumentIdを上書きせず、新しいID、版、取得日、SHA-256を追加する。
