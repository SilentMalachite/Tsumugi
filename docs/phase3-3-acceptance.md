# Phase 3-3（国保連請求CSV生成）受け入れ証跡

- 対象: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §7 3-3 / AC3-7
- spec: `docs/superpowers/specs/2026-07-20-phase3-3-kokuhoren-csv-design.md`
- 計画: `docs/superpowers/plans/2026-07-20-phase3-3-kokuhoren-csv.md`
- 実装ブランチ: `feat/phase3-3-kokuhoren-csv`

---

> **状態（2026-07-25）**: AC3-7 の 7 項目を達成。Codex レビューの指摘を全件トリアージし、
> 明細書「契約情報」レコード（`provider:J121:05`）と開始年月日（`provider:J121:02:008`）は
> **受給者証「サービス事業者記入欄」の個別入力**を正本とする方式（ADR 0032）で解消した。
> 契約情報が未入力・または Phase 3-3 より前に確定した分は fail-close する（推測で埋めない）。

## 1. AC3-7 の判定

| # | 受け入れ基準 | 判定 | 証跡（テスト名） |
|---|---|---|---|
| 1 | 独立入力 `ProcessingMonth` がコントロールレコードへ入る | ✅ | `ClaimCsvGeneratorTests.Generate_writes_the_processing_month_into_the_control_record` / `ClaimCsvExportProductionWiringTests.Real_wiring_writes_the_processing_month_independently_from_the_service_month` |
| 2 | CP932 / CRLF | ✅ | `CsvCellEncoderTests.Cp932_is_registered_and_round_trips_japanese_text` / `GoldenCsvSnapshotTests.Golden_fixtures_are_valid_cp932_with_crlf_line_endings` |
| 3 | 外側3レコード（control=1 / data=2..n / end=3） | ✅ | `ClaimCsvGeneratorTests.Generate_writes_the_outer_three_record_frame` / `..._numbers_records_from_one_and_ends_at_data_count_plus_two` |
| 4 | 公式の内側レコード順 | ✅ | `ClaimCsvGeneratorTests.Generate_emits_inner_records_in_the_official_record_order` |
| 5 | バイトスナップショット一致 | ✅ | `GoldenCsvSnapshotTests.Generated_csv_matches_the_golden_fixture_byte_for_byte`（normal / correction / cjk / multi） |
| 6 | CSV は確定済み実効 `ClaimBatch` のみから | ✅ | `ClaimCsvExportProductionWiringTests.Real_wiring_fails_closed_when_no_finalized_batch_exists` / `..._refuses_to_export_when_the_head_revision_is_a_cancellation`。head は Cancel を含む最大 revision（`ClaimBatchPolicy.Head` と同じ規則）で解決し、head が Cancel なら拒否する |
| 7 | 決定論 | ✅ | `GoldenCsvSnapshotTests.Generated_csv_is_deterministic_for_the_same_input` / `ClaimCsvExportProductionWiringTests.Real_wiring_is_byte_deterministic_for_the_same_finalized_batch` |

---

## 2. 外側フレームと項目長の根拠（実装判断の出所）

spec JSON から機械的に導出した事実を実装の根拠にしている。推測ではない。

| 事実 | 導出元 |
|---|---|
| データレコードの「データ」項目には内側 provider レコード 1 件がそのまま入る | `provider:J611:01` の `sum(maxBytes) + 区切りカンマ数 = 822` が `common:outer:data:003`（データ, maxBytes=822）と完全一致 |
| `maxBytes` は「引用符を除いた内容のバイト数」の上限 | 同上（引用符を内容長に含める解釈では 822 の一致が成立しない） |
| 行終端は末尾の「ブランク」項目（`quoteRule: "crlf"`）である | 各外側レコードの最終項目だけが `crlf` 規則を持つ（`CsvCellEncoderTests.The_crlf_quote_rule_appears_only_on_the_last_field_of_a_record`） |
| レコード番号は control=1 / data=2..n+1 / end=n+2 | `common:outer:end:002` の `sequence(value=outerDataRecordCountPlus2)` |
| 内側レコードの並び | `provider-claim-r7-10.json` の `order`（1..9） |

---

## 3. 443 フィールドの解決方式（spec 駆動）

`field-mapping-r7-10.json` の宣言だけを根拠に値を決め、fieldId・制度実値を C# に持たない。

| status | 件数 | 解決 |
|---|---|---|
| `generated` | 375 | `generatorRule` DSL（17 種の head）を `CsvGeneratorRuleParser` で解析し `ClaimCsvFieldResolver` が評価 |
| `existing` | 28 | `modelPath` を finalization snapshot v2 上で解決 |
| `explicitInput` | 10 | `ProcessingMonth`（独立入力）/ `ServiceProvisionMonth` |
| `missing` | 30 | `targetModel.targetProperty` を snapshot v2 上で解決（対象プロパティは Phase 3-1 で追加済み。本スライスで migration は不要だった） |

DSL の語彙は `CsvGeneratorRuleParserTests.The_embedded_specification_uses_exactly_the_known_generator_rule_heads` で閉じており、未知の head が spec に入ると RED になる。

### コード値の決め方（ハード制約3 の遵守）

列挙型 → 公式コードの対応表を C# に置かない。次の規則だけで決める。

- spec の `allowedCodes` が 1 個 → その値（該当することの表明そのものがコード）
- `allowedCodes` が空 かつ `requiredWhen` が同一性判定（`modelIn` / `modelEquals`）→ 該当回数 `1`
- それ以外 → モデルの数値をそのまま出し、`allowedCodes` で encoder が検証

この結果、`MedicalCoordinationType.TypeV` は spec の `allowedCodes`（`1,2,3,4,6`）に無いため自動的に `UnknownCode` で fail-close する（§6 参照）。

---

## 4. 依存方向 / オフライン検査

| 検査 | テスト |
|---|---|
| `Tsumugi.Infrastructure.Csv` は永続化・帳票・UI を参照しない | `Tsumugi.Infrastructure.Csv.Tests.ArchitectureTests.Infrastructure_csv_does_not_reference_persistence_reporting_or_ui` |
| Application は `Tsumugi.Infrastructure.Csv` を参照しない | 既存 `Tsumugi.Application.Tests.ArchitectureTests.Application_does_not_reference_outer_layers`（`Tsumugi.Infrastructure` 接頭辞一致で `.Csv` も禁止） |
| 生成器は Application 抽象の実装である | `ArchitectureTests.Claim_csv_generator_implements_the_application_abstraction` |
| 通信 API 不参照 | 既存 `OfflineComplianceTests.Tsumugi_assemblies_do_not_reference_network_libraries`（`Tsumugi.Infrastructure.Csv` を含む） + `ArchitectureTests.Infrastructure_csv_does_not_reference_network_libraries` |
| 制度実値・CSV 仕様値の配置境界 | 既存 `ClaimSpecificationBoundaryTests.Production_source_keeps_external_specification_literals_in_their_catalogs`（実装中に 2 回 RED になり、Application 層から `employment-continuation-support` / `region-other` / `provider:J121:*` を `Tsumugi.Infrastructure/ClaimMasters/ClaimMasterCsvOfficeContextProvider` へ退避して解消） |
| culture 明示 | 既存 `CultureExplicitnessGuardTests` |

---

## 5. 個人情報の非露出（ハード制約4）

- `ClaimCsvExport`（出力履歴）は `ClaimBatchId` / `ProcessingMonth` / 版 / SHA-256 / バイト長 / 監査列のみを持ち、氏名・受給者証番号・保存先パスを持たない。
- `ClaimCsvExportFailedException` は fieldId・理由トークン・構造情報・内部参照コードだけを載せる。値そのものは載せない。
  - 証跡: `GoldenCsvSnapshotTests.A_kangxi_radical_in_a_recipient_name_fails_closed_with_the_field_id`（例外メッセージ・詳細に氏名が出ないことを検証）
  - 証跡: `ClaimCsvExportSectionTests.Generate_reports_the_field_id_and_reason_without_personal_data`

---

## 6. fail-close の経路

| 事象 | 例外 / 理由 | テスト |
|---|---|---|
| 確定済み revision なし | `ClaimBatchNotFinalizedException` | `Real_wiring_fails_closed_when_no_finalized_batch_exists` |
| CP932 変換不能（絵文字・康熙部首など） | `NonRepresentableCharacter` | `Real_wiring_does_not_append_history_when_generation_fails` / `A_kangxi_radical_in_a_recipient_name_fails_closed_with_the_field_id` |
| バイト幅超過 | `OverByteWidth` | `CsvCellEncoderTests.EncodeCell_fails_when_content_exceeds_the_byte_width` |
| 必須項目が空 | `MissingRequired` | `CsvCellEncoderTests.EncodeCell_fails_when_an_always_required_field_is_empty` |
| 許容コード外 | `UnknownCode` | `CsvCellEncoderTests.EncodeCell_fails_when_the_value_is_not_an_allowed_code` |
| NUL / 改行混入 | `NulCharacter` / `LineBreakInValue` | `CsvCellEncoderTests.EncodeCell_fails_on_a_nul_character` / `..._fails_on_an_embedded_line_break` |
| 未知の引用規則 / generatorRule | `UnknownQuoteRule` / `CsvGeneratorRuleException` | `CsvCellEncoderTests.EncodeCell_fails_on_an_unknown_quote_rule` / `CsvGeneratorRuleParserTests.Parse_fails_closed_on_malformed_or_unknown_rules` |
| 地域区分コード未確定（`RegionGrade.Other` / `None`） | `UnknownRegionClassification` | 実装: `ClaimMasterCsvOfficeContextProvider`（§8 の未確定事項） |

**失敗時は `ClaimCsvExport` を追記しない**（`Real_wiring_does_not_append_history_when_generation_fails` で履歴が空のままであることを検証）。

---

## 7. 例外利用日 4 項目（Phase 3-2 の「孤立4フィールド」）のクローズ

`provider:J121:04:030`〜`033` は Phase 3-2 まで自己参照条件のみで恒常 fail-open だった。

- `field-mapping-r7-10.json` に **新キー `crossFieldGroup: "exceptional-usage"`** を追加し、公式出典に紐づく `requiredWhen`（単項条件）は書き換えていない。
- `ClaimInputRequirementProvider` が同一 group の条件を Any-merge して各要件へ配る。
- `uiSurface` を `ClaimPreparationView` → `ClaimInputView` へ変更（`provider:J121:04:025` で確立済みの前例に合わせ、受給者単位で編集可能にするため）。
- 入力 UI: `ClaimInputView` の「例外利用日」Expander（`ClaimInputViewModel` の 6 プロパティ）。

証跡: `ExceptionalUsageCrossFieldTests`（5 テスト）/ `ViewInputWiringTests.ClaimInputView_exposes_only_owned_fields_histories_and_keyboard_commands`（Phase 3-2 の `NotContain` を `Contain` へ反転）。

---

## 8. 既知の限界（Known limitations）

いずれも「推測で埋めない」方針に従い、値を捏造せず限界として記録する。詳細は `docs/open-questions.md`。

1. **R8.6 サービスコード表の独立 seed は見送り**: CSV writer はサービスコードを確定済み snapshot の `ClaimLines[].ServiceCode` からコピーするだけで、独立カタログを必要としない。一次資料（URL / SHA256 / 取得日）が未入手のため、`service-code-r8-06.json` は作らない（ADR 0031）。
2. **地域区分コード**: 共通編 1.4 から確定（ADR 0033）。`11`〜`17` / その他 `23`。`RegionGrade.None` のみ fail-close する。
3. **`provider:J121:02:008`（開始年月日）と `provider:J121:05`（契約情報）**: 受給者証「サービス事業者記入欄」の個別入力を正本とし（ADR 0032）、確定時に snapshot へ焼き込む。**Phase 3-3 より前に確定した請求は snapshot に契約情報を持たないため、CSV 化には再確定が必要**（該当分は fail-close する）。自事業所の行が受給者証に無い場合も fail-close する。
4. **`provider:J121:02:009`（終了年月日）**: 契約終了は snapshot v2 に含まれないため常に空欄。任意項目のため spec 上の不整合は生じない。
5. **訪問支援特別加算の算定回数・算定時間数（`provider:J611:01:052` / `provider:J611:02:028`）と施設外支援の年度累計（`:054`）**: 意味論は登録済み一次資料から確定した（ADR 0033 追記）。算定回数・算定時間数は計画に基づく別概念で日次実績から導出できず、施設外支援の累計は**年度（4/1〜3/31）累計**であり当月分しか持たない snapshot から算出できない。**いずれも ADR 0032 と同じ個別入力が必要で、それまで該当加算を算定する事業所では fail-close する。**
6. **`provider:J611:01:070`〜`072`（初期加算）/ `ClaimServiceLine.SummaryNote` / `DailyRecord.Note` / `ContractedProvider.*`**: finalization snapshot v2 に含まれない。いずれも条件付き項目のため空欄が正しい出力になる。`provider:J121:05`（経過措置）は 0 レコードで出力する。
7. **引用規則**: 共通編 1.2.2(4) から確定（ADR 0033）。「漢字」= 2 バイトコードのため全角カナ・全角記号も引用する。残る未確定は属性区分（英数/漢字）に紐づく文字種検証で、`dataType` が公式の属性区分と 1 対 1 でないため未実装。
8. **データ種別（`common:outer:control:005`）**: 共通編 1.6 から確定（ADR 0033）。混在時も最初のデータレコードの交換情報識別番号の上3桁で、実装は正しい。ただし同節の例外対応表（物理44頁）がテキスト抽出できず、B型請求が例外に該当するかは目視確認が残る。
9. **GUI 手動貫通確認 未実施**: `ClaimInputView`（例外利用日セクション）と `ClaimPreparationView`（国保連CSV出力セクション）の実機確認は未実施。Phase 1 からの継続課題として `docs/open-questions.md` に残す。
10. **golden CSV は自前生成**: 公式サンプル CSV・取込テストデータは公開されておらず、実データ突合には電子請求受付システムのテスト環境（電子証明書・伝送を伴う＝責務境界外）が必要と確定した（ADR 0033 追記）。golden の用途は「意図しないバイト変化の検出」に限定し、**仕様適合は登録済み一次資料の条文に対して固定する**（ADR 0033）。運用開始時に事業所側で公式システムへの取込テストを一度実施することを前提とする。

---

## 9. spec / plan からの主な逸脱と理由

| 計画 | 実装 | 理由 |
|---|---|---|
| `ExportClaimCsvUseCase`(Application) が `ClaimCsvWriter` 等を直接使用 | Application 抽象 `IClaimCsvGenerator` を新設し `Tsumugi.Infrastructure.Csv` が実装 | `Infrastructure.Csv → Application` の既存参照があるため、計画どおりでは循環参照になり compile 不能 |
| 外側レコードは `provider:control` / `provider:end` | `common:outer:control` / `data` / `end` | spec JSON の実体に合わせた |
| `quoteRule` は `"quoted"` / `"unquoted"` | 条件付き引用（散文の規則をそのまま実装） | spec JSON の実体に合わせた |
| 9 個の `IProviderRecordBuilder<T>` に fieldId を手書き列挙 | `generatorRule` DSL 解釈器 + 薄い spec 駆動生成 | 424 fieldId と定数を C# に書くとハード制約3（仕様値の直書き禁止）に抵触する |
| `ClaimInput` に provider:* を新規追加 + migration | 追加なし | 30 件の対象プロパティは Phase 3-1 で追加済みだった |
| 新規 `CsvExportView` + ナビゲーション追加 | `ClaimPreparationView` の子セクション `ClaimCsvExportSection` | 既存 `ClaimReportSection` と同じ「親から状態を押し込む子セクション」パターン。責務（確定済み選択・`ProcessingMonth` 入力・エラー表示・保存）は spec §4.1 のまま |
| `service-code-r8-06.json` + `IServiceCodeCatalog` | 作らない | §8-1 のとおり（利用者判断でスコープ外に決定） |

---

## 9-2. 実装後コードレビューで見つけた欠陥と修正（2026-07-25）

| 重大度 | 欠陥 | 修正 |
|---|---|---|
| **Critical** | **受給者が2人以上だとCSV生成が失敗**。請求書の集計項目（`provider:J111:02:012` / `:014`）の `fieldNonZero(provider:J121:01:0xx)` は、ファイルスコープの行から受給者スコープの項目を参照する。受給者が1人のときだけ「候補行が1つなら採用」というフォールバックで偶然通っており、2人以上で `UnresolvableFieldReference` になっていた。golden が全て1人だったため未検出 | 条件式の項目参照を行スコープ対応にし、対象行のスコープ内にある全行を「いずれかが満たすか」で判定（`ClaimCsvFieldResolver.ReferenceValues`）。受給者2人の golden `csv-golden-multi.csv` と `ClaimCsvRowScopeTests` 4件で恒久固定 |
| Important | `ClaimCsvWriter` が引数の catalog ではなく、埋め込み spec を裏で再ロードした別インスタンスからマッピングを引いていた（差し替え不能・二重parse） | 引数の `catalog.MappingByFieldId` を渡す。`ClaimCsvGeneratorTests.Generate_resolves_outer_frame_values_from_the_supplied_catalog` で固定 |
| Important | 例外利用日 4 項目を一度入力すると画面から解除できない（保存時に空なら旧値へフォールバックしていた）。cross-field readiness が永久に外れなくなる | フォールバックを削除。`ClaimInputViewModelTests.Correcting_claim_input_can_clear_the_exceptional_usage_fields` で固定 |
| Important | `sum(field=...)` が未知の `filter` / `groupBy` を黙って無視し、請求金額が静かに誤りうる | 既知値の閉じた集合を用意し、外れたら fail-close |
| Important | `roundDown` の除算が `double` を経由していた（金額計算） | 整数の切り捨て除算（`FloorDivide`）へ |
| Important | 行スコープキーが区切り文字で終わらず、受給者 1000 のキーが受給者 10000 の接頭辞になりうる（集約の混線） | 受給者キーを区切り文字終端に。`ClaimCsvRowScopeTests` で固定 |
| Medium | `count` の日次走査が日次記録を線形探索し直しており、ファイルスコープでは `MissingRow` になる潜在バグがあった | 行スコープごと列挙する（`EnumerateDailyRecordScopes`） |
| Medium | `min(fields=...)` が欠損を 0 とみなし、負担上限が静かに 0 円になりうる | 欠損があれば fail-close |
| Minor | 二重引用符のエスケープで内容が伸びた後の幅を検査していなかった / 生成器のテストが `Throw<Exception>` と緩かった / データ項目の判定が接頭辞・接尾辞ヒューリスティックだった | それぞれ修正 |

**未修正（意図的）**: `ClaimCsvExportSection` の `ProcessingMonth` プロパティ（`int`）が `ProcessingMonth` 型と同名。C# の color-color 規則で曖昧さは生じず全経路テスト済みのため、XAML とテストへの波及を避けて据え置く。

---

## 9-3. Codex レビューの指摘と対応（2026-07-25）

Codex（読み取り専用レビュー）から CRITICAL 1 / HIGH 9 / MEDIUM 1。全件を実コードで検証した。

### 修正した（8件）

| 重大度 | 指摘 | 検証結果 | 対応 |
|---|---|---|---|
| **CRITICAL** | `provider:J121:05` を 0 件にしており必須レコードが欠落 | **再現**。J121:05 は「経過措置」ではなく**契約情報**レコード（`契約支給量`/`契約開始年月日`/`事業者記入欄番号` が `always` 必須）。設計spec §3.4 の表のラベルを鵜呑みにしたのが原因 | 受給者ごとに 1 行出力するよう変更。契約情報は DTO（`ClaimCsvContractDto`）で受け、snapshot v2 に無いため実データでは **fail-close**（黙って空欄で出さない） |
| HIGH | Cancel を除外してから最大 Revision を採るため取消済み請求が復活する | **再現**。Domain の `ClaimBatchPolicy.Head` は Cancel を含む最大 Revision を返す | head を Cancel 込みで解決し、head が Cancel なら `ClaimBatchNotFinalizedException` |
| HIGH | 確定時の仕様版を無視して実行時 spec で再生成する | **再現** | `IClaimCsvGenerator.SpecificationVersion` を追加し、確定時の版と不一致なら fail-close |
| HIGH | 数値 0 と「出力対象外」を同一視し、負担 0 円の請求が生成できない | **再現**。`016=0 → 019（fieldNonZero(self)）が空 → 021（always必須）が空 → MissingRequired`。生活保護受給者などで常に落ちる | 自己参照条件を**表示規則**として扱い、参照式には算出値を渡す。0 は `"0"` として出力 |
| HIGH | 開始年月日を当月の日次記録の最小日で代用している | **再現**。前月以前から継続する契約で誤値 | 確定済み契約の初回サービス提供日だけを正本にし、無ければ fail-close。当月最小日への推測フォールバックを削除 |
| HIGH | 往復を片道 1 回として数えている | **再現**。ADR 0028 決定5 / `ClaimCalculator.TransportOneWayCount` は `Outbound/Inbound=1、Round=2` | 片道換算の重み付き合計へ。月次値が日次の往+復合計と一致する不変条件をテスト化 |
| HIGH | 制御文字・dataType の文字種を検証していない | **再現**（NUL と CR/LF しか弾いていない） | 制御文字を拒否し、`numeric`/`yearMonth`/`date` は ASCII 数字のみに限定 |
| MEDIUM | 保存が非アトミックで失敗時に部分ファイルが残る | **再現**（`File.WriteAllBytesAsync` 直書き） | 同一ディレクトリの一時ファイルへ書き切ってから `File.Replace`/`Move`。失敗・取消時は一時ファイルだけ削除 |

### 修正せず記録した（3件）

| 重大度 | 指摘 | 判断 |
|---|---|---|
| HIGH | 未検証 raw aggregate を直接読み、snapshot の hash・canonical 性を検証していない | **妥当だがスコープ外**。Phase 3-2 の `GenerateClaimReportsUseCase` が同じ経路であり、検証済み aggregate を返す Application port の新設は Phase 3-2 領域の変更。open-questions へ起票し、3帳票と共通の課題として扱う |
| HIGH | `provider:J121:04:009` を `BilledDays` で代替し、加算のみ算定した日を落とす | **2026-07-25 修正済み（ADR 0034）**。事業所編の項目説明で指摘が確定し、あわせて `provider:J121:02:010`「利用日数」は逆に「欠席時対応加算は除く」と判明（2項目は別定義）。算定器が cap 後の欠席時対応加算算定日数を返し、snapshot の `ServiceUsageDays` を `J121:04:009` に用いる |
| HIGH | 既定ファイル名が国保連の命名規則（英字開始・8文字以内・`.CSV`）に不適合 | **リポジトリ内の登録済み一次資料で確認できない**。ハード制約3（推測で埋めない）に従い、Codex が引用した外部PDFの記述だけを根拠に実装しない。open-questions へ HIGH 優先で起票し、一次資料を `sources.json` へ登録してから対応する |

---

## 9-4. 契約情報の個別入力（ADR 0032・2026-07-25）

Codex レビューの CRITICAL（契約情報レコードの欠落）と HIGH（開始年月日の推測）は、いずれも
「契約ごとに実情が異なる値を導出しようとしていた」ことが原因だった。導出をやめ、個別入力を正本にした。

| CSV 項目 | 正本 | 入力面 |
|---|---|---|
| `provider:J121:05:008` 契約支給量 | `ContractedProvider.ContractedSupplyDays` | `CertificateView`（既存） |
| `provider:J121:05:009` 契約開始年月日 | `ContractedProvider.ContractDate` | `CertificateView`（既存） |
| `provider:J121:05:010` 契約終了年月日 | `ContractedProvider.TerminationDate` | `CertificateView`（既存） |
| `provider:J121:05:011` 事業者記入欄番号 | `ContractedProvider.CertificateEntryNumber` | `CertificateView`（既存） |
| `provider:J121:02:008` 開始年月日 | `ContractedProvider.FirstServiceDate`（**新規追加**） | `CertificateView` |

- `field-mapping-r7-10.json` の `provider:J121:02:008` を `generated`（min 導出）から
  `missing`（`ContractedProvider.FirstServiceDate`）へ変更。これで readiness gate が確定前に未入力を検出する。
  generatorRule 375→374 件、missing 30→31 件、readiness target 26→27 path。
- 確定時に `OperationLocalSnapshotReader` が「サービス事業者記入欄」から**自事業所（事業所番号一致）の行**を
  選び、`ClaimFinalizationContractedProviderSnapshot` として焼き込む。該当行が無ければ null のまま確定を通し、
  CSV 側で fail-close する（確定操作は妨げない）。
- `min(selector=...)` の評価器は撤去した。導出への逆戻りは fail-close する。

証跡: `ClaimCsvExportProductionWiringTests.Real_wiring_generates_cp932_csv_and_appends_the_export_history`
（契約情報あり → 実データから生成成功・J121:05 レコードを検証）/
`..._fails_closed_when_the_finalized_snapshot_has_no_contract_information`（未入力 → fail-close）/
`ViewInputWiringTests`（`CertificateView` の入力欄）。

---

## 9-5. 登録済み一次資料からの仕様適合確定（ADR 0033・2026-07-25）

`sources.json` 登録済みの共通編・事業所編を再取得し（登録 SHA-256 と一致・`liveCheck` 記録済み）、
推測に頼っていた 5 点を条文から確定した。**うち 3 点は実装が公式仕様に違反していた。**

| 項目 | 出典 | 結果 |
|---|---|---|
| 地域区分コード | 共通編 1.4（物理21頁） | **修正**: `11:一級地`〜`17:七級地` / `23:その他`。実装は `01`〜`07` を出力していた（`06` は公式コードに存在しない） |
| 引用規則 | 共通編 1.2.2(4) | **修正**: 「漢字」は**2 バイトコード**の意。全角カナ・全角記号も引用対象。実装は表意文字のみ判定で全角カナ氏名を引用していなかった |
| ファイル名 | 共通編 1.2.1 | **修正**: 英字始まり・半角英数字 8 桁以内 ＋ `.CSV`。Codex 指摘どおり不適合だった |
| 使用不可能文字 | 共通編 1.2.2(3)① | **追加**: シングルコーテーション（0x27）を拒否 |
| 開始年月日の意味 | 事業所編 開始年月日の設定方法 | ADR 0032 の個別入力が正しいと確認。「平成18年4月1日以降の最初のサービス提供日」で契約変更に影響されないため、**誤っていた「契約日以降」検証を下限 2006-04-01 へ修正** |

実装が既に正しかったことも確認できた: データ種別（混在時も先頭データレコードの上3桁）／単位数単価
（整数部2桁・小数部3桁＝1/1000円尺度）／数値ゼロは `"0"`／制御文字 0x00〜0x1F 禁止／
年月日 `YYYYMMDD`・年月 `YYYYMM`／レコード構成・連番・件数・CRLF。

証跡: `RegionClassificationCodeCatalogTests`（8+3 テスト）/
`CsvCellEncoderTests.EncodeCell_quotes_every_two_byte_value` /
`..._does_not_quote_single_byte_values` / `..._fails_on_a_single_quotation_mark` /
`ClaimCsvExportProductionWiringTests`（ファイル名の正規表現）/ golden CSV 4 種を再生成。

---

## 10. `./build/ci.sh` 実行証跡

2026-07-25 実行、**全ゲート緑**。

```
==> restore
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!  失敗: 0、合格:   677 - Tsumugi.Domain.Tests.dll
成功!  失敗: 0、合格:   411 - Tsumugi.Application.Tests.dll
成功!  失敗: 0、合格:   199 - Tsumugi.Infrastructure.Csv.Tests.dll
成功!  失敗: 0、合格:    30 - Tsumugi.Infrastructure.Reporting.Tests.dll
成功!  失敗: 0、合格:   254 - Tsumugi.App.Tests.dll
成功!  失敗: 0、合格:   640 - Tsumugi.Infrastructure.Tests.dll
==> coverage threshold gate
Tsumugi.Domain      Line 95.63% / Branch 88.29% / Method 93.89%  (floor 95%)
Tsumugi.Application Line 90.57% / Branch 84.26% / Method 84.28%  (floor 70%)
==> CI OK
```

合計 2,211 テスト（Codex レビュー対応・契約情報の個別入力・公式仕様適合で 41 追加）（Phase 3-3 で追加した主なテストクラス: `ClaimCsvExportTests` 11 /
`ClaimCsvExportRepositoryTests` 6 / `CsvCellEncoderTests` 29 / `CsvGeneratorRuleParserTests` 14 /
`ClaimCsvGeneratorTests` 10 / `GoldenCsvSnapshotTests` 13 / `ClaimCsvRowScopeTests` 4 / `ExceptionalUsageCrossFieldTests` 5 /
`Tsumugi.Infrastructure.Csv.Tests.ArchitectureTests` 6 / `ClaimCsvExportProductionWiringTests` 5 /
`ClaimCsvExportSectionTests` 7）。

---

## 11. 本スライスで提供したスコープ

- 確定済み実効 `ClaimBatch` + 独立入力 `ProcessingMonth` から、CP932 / CRLF の請求 CSV をバイト決定論的に生成する。
- 出力履歴 `ClaimCsvExport`（追記型・SHA-256・バイト長・版）を残す。失敗時は残さない。
- 例外利用日 4 項目の入力 UI と cross-field readiness。
- CSV 仕様値・制度実値を C# に持ち込まない spec 駆動の生成器と、その境界を守るアーキテクチャテスト。

**提供しないもの**: 伝送・電子証明書・電子請求受付システム連携（CLAUDE.md §責務境界）、CSV の再取込、公式サンプルとの突合。
