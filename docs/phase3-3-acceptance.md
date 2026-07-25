# Phase 3-3（国保連請求CSV生成）受け入れ証跡

- 対象: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §7 3-3 / AC3-7
- spec: `docs/superpowers/specs/2026-07-20-phase3-3-kokuhoren-csv-design.md`
- 計画: `docs/superpowers/plans/2026-07-20-phase3-3-kokuhoren-csv.md`
- 実装ブランチ: `feat/phase3-3-kokuhoren-csv`

---

## 1. AC3-7 の判定

| # | 受け入れ基準 | 判定 | 証跡（テスト名） |
|---|---|---|---|
| 1 | 独立入力 `ProcessingMonth` がコントロールレコードへ入る | ✅ | `ClaimCsvGeneratorTests.Generate_writes_the_processing_month_into_the_control_record` / `ClaimCsvExportProductionWiringTests.Real_wiring_writes_the_processing_month_independently_from_the_service_month` |
| 2 | CP932 / CRLF | ✅ | `CsvCellEncoderTests.Cp932_is_registered_and_round_trips_japanese_text` / `GoldenCsvSnapshotTests.Golden_fixtures_are_valid_cp932_with_crlf_line_endings` |
| 3 | 外側3レコード（control=1 / data=2..n / end=3） | ✅ | `ClaimCsvGeneratorTests.Generate_writes_the_outer_three_record_frame` / `..._numbers_records_from_one_and_ends_at_data_count_plus_two` |
| 4 | 公式の内側レコード順 | ✅ | `ClaimCsvGeneratorTests.Generate_emits_inner_records_in_the_official_record_order` |
| 5 | バイトスナップショット一致 | ✅ | `GoldenCsvSnapshotTests.Generated_csv_matches_the_golden_fixture_byte_for_byte`（normal / correction / cjk） |
| 6 | CSV は確定済み実効 `ClaimBatch` のみから | ✅ | `ClaimCsvExportProductionWiringTests.Real_wiring_fails_closed_when_no_finalized_batch_exists`（`ClaimBatchNotFinalizedException`）。`ExportClaimCsvUseCase` は `IClaimBatchRepository` の履歴から `Kind != Cancel` の最大 revision だけを採る |
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
2. **地域区分コードの「その他」**: `RegionGrade.Grade1..Grade7` は級地番号のゼロ詰め（`01`..`07`）とした。`Other` / `None` の公式コードはリポジトリ内の一次資料から確定できないため fail-close する。
3. **`provider:J121:02:008`（開始年月日）**: spec の selector は `DailyRecord.ServiceDate` だが、filter「有効な継続契約における最初のサービス提供日」の解釈のうち、前月以前へまたがる継続契約の扱いが確定していない。確定 snapshot は当月分の日次記録しか持たないため、当月内の最小サービス提供日を採る。
4. **`provider:J121:02:009`（終了年月日）**: 契約終了は snapshot v2 に含まれないため常に空欄。任意項目のため spec 上の不整合は生じない。
5. **`provider:J611:01:052`（`measure=billableOccurrences`）/ `:054`（`window=official180DayWindow`）**: 算定回数・180日窓の意味論が確定できないため、該当データが存在するときだけ fail-close する。該当なしの場合は条件が偽になり空欄で通る。
6. **`provider:J611:01:070`〜`072`（初期加算）/ `ClaimServiceLine.SummaryNote` / `DailyRecord.Note` / `ContractedProvider.*`**: finalization snapshot v2 に含まれない。いずれも条件付き項目のため空欄が正しい出力になる。`provider:J121:05`（経過措置）は 0 レコードで出力する。
7. **引用規則の解釈**: 公式文言「comma, double quote, space, or kanji」を literal に実装した。全角カナ・全角記号のみの値を引用するかは確定できないため引用しない側に寄せている。
8. **データ種別（`common:outer:control:005`）**: 先頭の内側レコードの交換情報識別番号の先頭3文字（本スライスでは常に `J11`）。1ファイルに J111 / J121 / J611 が混在するときの公式解釈は未確定。
9. **GUI 手動貫通確認 未実施**: `ClaimInputView`（例外利用日セクション）と `ClaimPreparationView`（国保連CSV出力セクション）の実機確認は未実施。Phase 1 からの継続課題として `docs/open-questions.md` に残す。
10. **golden CSV は自前生成**: 公式サンプルとの突合ではないため「仕様に対する正しさ」は担保しない。意図しないバイト変化の検出が役割で、仕様適合は `CsvCellEncoderTests` / `ClaimCsvGeneratorTests` が担う。

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

## 10. `./build/ci.sh` 実行証跡

2026-07-25 実行、**全ゲート緑**。

```
==> restore
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!  失敗: 0、合格:   677 - Tsumugi.Domain.Tests.dll
成功!  失敗: 0、合格:   411 - Tsumugi.Application.Tests.dll
成功!  失敗: 0、合格:   153 - Tsumugi.Infrastructure.Csv.Tests.dll
成功!  失敗: 0、合格:    30 - Tsumugi.Infrastructure.Reporting.Tests.dll
成功!  失敗: 0、合格:   252 - Tsumugi.App.Tests.dll
成功!  失敗: 0、合格:   637 - Tsumugi.Infrastructure.Tests.dll
==> coverage threshold gate
Tsumugi.Domain      Line 95.63% / Branch 88.29% / Method 93.89%  (floor 95%)
Tsumugi.Application Line 90.57% / Branch 84.26% / Method 84.28%  (floor 70%)
==> CI OK
```

合計 2,160 テスト（Phase 3-3 で追加した主なテストクラス: `ClaimCsvExportTests` 11 /
`ClaimCsvExportRepositoryTests` 6 / `CsvCellEncoderTests` 29 / `CsvGeneratorRuleParserTests` 14 /
`ClaimCsvGeneratorTests` 10 / `GoldenCsvSnapshotTests` 10 / `ExceptionalUsageCrossFieldTests` 5 /
`Tsumugi.Infrastructure.Csv.Tests.ArchitectureTests` 6 / `ClaimCsvExportProductionWiringTests` 5 /
`ClaimCsvExportSectionTests` 7）。

---

## 11. 本スライスで提供したスコープ

- 確定済み実効 `ClaimBatch` + 独立入力 `ProcessingMonth` から、CP932 / CRLF の請求 CSV をバイト決定論的に生成する。
- 出力履歴 `ClaimCsvExport`（追記型・SHA-256・バイト長・版）を残す。失敗時は残さない。
- 例外利用日 4 項目の入力 UI と cross-field readiness。
- CSV 仕様値・制度実値を C# に持ち込まない spec 駆動の生成器と、その境界を守るアーキテクチャテスト。

**提供しないもの**: 伝送・電子証明書・電子請求受付システム連携（CLAUDE.md §責務境界）、CSV の再取込、公式サンプルとの突合。
