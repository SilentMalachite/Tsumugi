# Phase 3-2（snapshot v2 + 3帳票）受け入れ証跡

- 対象: `docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md`（正本spec）§15 成功基準5項目
- 実装計画: `docs/superpowers/plans/2026-07-20-phase3-2-reports.md`（Task 1〜14 完了・チェック済み。本書はTask 15の証跡）
- 記録日: 2026-07-20 / ブランチ: `feature/phase3-2-reports`
- 本書はTask 15ブリーフ（`.superpowers/sdd/task-15-brief.md`）Step 2「`docs/phase3-2-acceptance.md` を作成」の成果物。実装経緯の詳細ログは `.superpowers/sdd/progress.md`（2026-07-20 Phase 3-2 節）を参照。

## 1. §15 成功基準5項目の判定

| # | 成功基準 | 判定 | 証跡 |
|---|---|---|---|
| 1 | **AC3-5**: 3帳票（実績記録票・請求書・明細書）で CJK 抽出（Kangxi正規化後）に主要漢字が含まれ、決定論（同payload+同TimeProvider→同バイト）と合計値検証が緑 | **✔** | CJK抽出は3帳票とも `KangxiRadicalNormalizer.FoldKangxiRadicals` を経由する共通 `ExtractText` ヘルパ（`WageStatementPdfGeneratorTests` と同じ流儀）で行い、`GenerateServiceProvisionRecord_includes_office_recipient_month_in_extracted_text`（`tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimReportGeneratorServiceProvisionRecordTests.cs`）が「テスト事業所」「山田太郎」「2026年5月」「サービス提供実績記録票」を検出、`GenerateClaimInvoice_shows_office_contact_totals_and_spec_versions`（`ClaimReportGeneratorClaimInvoiceTests.cs`）が「介護給付費・訓練等給付費等請求書」、`GenerateClaimStatement_lists_recipient_with_lines_and_all_detail_fields`（`ClaimReportGeneratorClaimStatementTests.cs`）が「介護給付費・訓練等給付費等請求明細書」「山田太郎」を検出。決定論は `GenerateServiceProvisionRecord_is_deterministic_for_same_inputs_and_timeprovider` / `GenerateClaimInvoice_is_deterministic_for_same_inputs_and_timeprovider` / `GenerateClaimStatement_is_deterministic_for_same_inputs_and_timeprovider`（いずれも別インスタンスの同時刻`FakeTimeProvider`で`bytes.Should().Equal(bytes)`）で固定。合計値検証は `GenerateClaimStatement_totals_equal_sum_of_recipient_subtotals`（受給者2名フィクスチャで「明細合計=header合計」をフィクスチャ側でも事前アサートした上でPDF抽出テキストからも1,130/12,656/11,391/1,265を検出）で固定。 |
| 2 | **AC3-6**: `ClaimPreparation` 画面から3帳票を保存できる（`IFileSaveService`で保存ダイアログ→PDF書き出し） | **部分** | 自動化証跡は完備: `ClaimReportSection`（`src/Tsumugi.App/ViewModels/Claim/ClaimReportSection.cs`）の3 `SaveCommand` は `Commands_are_disabled_when_no_finalized_revision_exists` / `ServiceProvisionRecord_command_requires_selected_recipient_while_others_do_not` / `All_three_commands_are_enabled_once_a_recipient_is_selected`（`tests/Tsumugi.App.Tests/ViewModels/Claim/ClaimReportSectionTests.cs`）でCanExecute配線を検証、`SaveServiceProvisionRecordAsync_writes_generator_bytes_to_the_picked_path` / `SaveClaimInvoiceAsync_writes_generator_bytes_and_uses_office_name_in_the_file_name` / `SaveClaimStatementAsync_writes_generator_bytes_to_the_picked_path` がFake `IFileSaveService`経由で`GenerateClaimReportsUseCase`の返却バイト列が書き込みパスへ渡ることを検証、`User_cancelling_the_save_dialog_does_not_throw_and_leaves_no_saved_file_name` がキャンセル時に例外を投げないことを検証。UseCase側は `GenerateClaimReportsUseCaseTests.cs` 全8件（下記§2/§4参照）で緑。**ただし**Phase 3-1受け入れ証跡と同種の理由により、`dotnet run --project src/Tsumugi.App` での実機・人手による最終クリックスルー確認（プレビュー→確定→帳票保存×3）は本タスクでは未実施。既存open-question「Avalonia GUI目視確認」に統合して残課題として維持するため部分判定とする。 |
| 3 | `docs/phase3-claim-field-mapping.md` の21 `report:*` フィールド全てが snapshot v2 に凍結され、対応する PDF 描画テキストで抽出可能 | **✔** | §2の対応表を参照。21フィールド全てが `ClaimFinalizationSnapshot`（`src/Tsumugi.Application/Claim/ClaimFinalizationSnapshot.cs`）のフィールドとして存在し、`OperationLocalSnapshotReaderTests.ReadAsync_captures_all_21_report_fields_from_entities` が実体からの集約を21件個別アサートで固定、`ClaimFinalizationSnapshotReaderTests.Write_then_parse_roundtrips_all_21_report_fields` が canonical JSON write→parseのround-tripを固定。PDF描画テキストからの抽出可能性は§2右列の generator test 群（`comment`に fieldId を明記したもの含む）で個別に固定。 |
| 4 | codec v1 → v2 の破壊的置換完了、backwards-compat コード無し | **✔** | `grep -rn 'ClaimSnapshotValidationCodecV1\|claim-snapshot-v1\|claim-snapshot-codec-v1' src tests` の出力は §3 参照。クラス `ClaimSnapshotValidationCodecV1` への参照は0件（クラスファイル自体がTask 2で削除済み）。残る文字列一致は全て「v2 codecがv1 identityを拒否する」ことを検証するnegativeテストのリテラル文字列（`ClaimSnapshotValidationCodecV2Tests.CreateEnvelope_rejects_v1_schema_version` / `Registry_exposes_v2_with_write_support_and_rejects_v1` 等）とv1時代のfixture文字列（`ClaimFinalizationOperationV1Tests.cs`, `ValidatedClaimSnapshotEnvelopeTests.cs` 等、汎用envelope機構のテストでv1/v2いずれの識別子でも動作することを示す既存契約テスト）のみ。`ProductionClaimSnapshotValidationCodecRegistry` はv2のみ登録（`Registry_exposes_v2_with_write_support_and_rejects_v1`）。 |
| 5 | `./build/ci.sh` 緑、依存方向不変、オフライン検査緑（Reporting含む） | **✔** | §6の`./build/ci.sh`末尾出力で`==> CI OK`（2,050件成功・0失敗、警告0）。依存方向は`Reporting_does_not_reference_persistence_or_ui`（`tests/Tsumugi.Infrastructure.Tests/ArchitectureTests.cs`）で固定、オフライン検査は`Tsumugi_assemblies_do_not_reference_network_libraries`のTheoryに`Tsumugi.Infrastructure.Reporting`を含む5アセンブリ全件が対象（`tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`）。詳細は §4。 |

## 2. 21 `report:*` フィールド → snapshot v2 payload path → PDF抽出テスト 対応表

spec §6.2 の対応表（正本）に、抽出を固定するgenerator testを追加した表。

| fieldId | payload path | 型 | 抽出テキスト検証テスト（`tests/Tsumugi.Infrastructure.Reporting.Tests/`） |
|---|---|---|---|
| report:benefit-claim-form:header:004 | office.postalCode | string | `ClaimReportGeneratorClaimInvoiceTests.GenerateClaimInvoice_shows_office_contact_totals_and_spec_versions`（コメントで fieldId 明記） |
| report:benefit-claim-form:header:005 | office.address | string | 同上 |
| report:benefit-claim-form:header:006 | office.phoneNumber | string | 同上 |
| report:benefit-claim-form:header:008 | office.representativeTitleAndName | string | 同上 |
| report:benefit-claim-detail:header:001 | certificate.municipalityNumber | string | `ClaimReportGeneratorClaimStatementTests.GenerateClaimStatement_lists_recipient_with_lines_and_all_detail_fields`（コメントで fieldId 明記） |
| report:benefit-claim-detail:header:003 | certificate.subsidyMunicipalityNumber | string? | 同上（"999999"検出 + `GenerateClaimStatement_omits_claim_input_summary_and_optional_certificate_lines_when_absent`でnull時に行自体が出ないことも固定） |
| report:benefit-claim-detail:upper-limit-management:001 | certificate.upperLimitManagementProviderNumber | string? | 同上（"1234567890"/"管理事業所A"検出 + omitsテストでnull時非表示を固定） |
| report:benefit-claim-detail:upper-limit-management:003 | claimInput.upperLimitManagementResult | string? | 同上（"Compliant"検出 + omitsテストでnull時summary行非表示を固定） |
| report:benefit-claim-detail:upper-limit-management:004 | claimInput.upperLimitManagedAmountYen | int? | 同上（"705"検出） |
| report:benefit-claim-detail:summary:015 | claimInput.municipalSubsidyAmountYen | int? | 同上（"500"検出） |
| report:service-performance:daily:004 | dailyRecords[].serviceStartTime | string?(HH:mm) | `ClaimReportGeneratorServiceProvisionRecordTests.GenerateServiceProvisionRecord_renders_all_10_daily_fields`（"09:00"検出） |
| report:service-performance:daily:005 | dailyRecords[].serviceEndTime | string?(HH:mm) | 同上（"16:00"検出） |
| report:service-performance:daily:008 | dailyRecords[].specialVisitSupportMinutes | int? | `GenerateServiceProvisionRecord_renders_all_10_daily_fields`（ヘッダ「特別訪問」）+ `GenerateServiceProvisionRecord_renders_nondefault_optional_daily_fields`（値"30"検出） |
| report:service-performance:daily:010 | dailyRecords[].medicalCoordinationType | string? | 同上（ヘッダ「医療連携」+ 値"typeA"検出） |
| report:service-performance:daily:011 | dailyRecords[].trialUseSupportType | string? | 同上（ヘッダ「体験利用」+ 値"trialB"検出） |
| report:service-performance:daily:012 | dailyRecords[].regionalCollaborationApplied | bool | 同上（ヘッダ「地域協働」+ nondefaultテストで○個数=5を検証） |
| report:service-performance:daily:013 | dailyRecords[].emergencyAdmissionApplied | bool | 同上（ヘッダ「緊急受入」） |
| report:service-performance:daily:014 | dailyRecords[].intensiveSupportApplied | bool | 同上（ヘッダ「集中的支援」） |
| report:service-performance:daily:015 | dailyRecords[].offsiteSupportApplied | bool | 同上（ヘッダ「敷地外」） |
| report:service-performance:daily:016 | dailyRecords[].recipientConfirmation | bool | 同上（ヘッダ「利用者確認」） |
| report:service-performance:intensive-support:001 | intensiveSupportEpisode.startDate | string?(yyyy-MM-dd) | `GenerateServiceProvisionRecord_includes_intensive_support_start_date`（"集中的支援エピソード開始日"検出）+ `GenerateServiceProvisionRecord_omits_intensive_support_line_when_absent`（null時に行自体が出ないことを固定） |

21/21フィールド全件が抽出テキストから検証可能。上流の snapshot v2 集約は `OperationLocalSnapshotReaderTests.ReadAsync_captures_all_21_report_fields_from_entities` で21件個別アサート済み、readiness gate（確定前 fail-closed）は §4「fail-closed の証跡」を参照。

### Task 7 fix（`MunicipalSubsidyAmountYen` UI 移動）について

`ClaimInput.MunicipalSubsidyAmountYen`（fieldId `report:benefit-claim-detail:summary:015`）は当初 `ClaimPreparationView` 所属UIとして実装されたが、Task 7で「`Certificate.SubsidyMunicipalityNumber` が入力済でも `MunicipalSubsidyAmountYen` を入力するUIが存在しない」という恒久 NotReady 状態をレビューアが自テストで検出したため、`ClaimInputView`（`UpperLimitManagedAmountYen` の姉妹欄）へ移動した。詳細な責務境界・移動根拠は ADR 0030（`docs/decisions/0030-report-input-ui-responsibility.md`）に記録済み。fail-closed 修正後の状態は `Real_embedded_requirement_provider_requires_municipal_subsidy_amount_when_subsidy_municipality_number_is_entered` / `Real_embedded_requirement_provider_does_not_require_municipal_subsidy_amount_when_both_legs_are_absent`（`tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs`）で固定。

## 3. codec v1 削除確認

```
$ grep -rn 'ClaimSnapshotValidationCodecV1\|claim-snapshot-v1\|claim-snapshot-codec-v1' src tests
src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV2.cs:9:/// v1（<c>claim-snapshot-v1</c>）からの破壊的置換（ADR 0029）。canonical bytesは非空JSON objectで、
tests/Tsumugi.Domain.Tests/Entities/ClaimDetailTests.cs:24,183: SnapshotSchemaVersion = "claim-snapshot-v1" （汎用envelope契約の識別子非依存性を示す既存フィクスチャ、Domain層はcodec実装を知らない）
tests/Tsumugi.Application.Tests/Claim/ClaimFinalizationOperationV1Tests.cs: "claim-snapshot-v1" 文字列リテラル（"V1"はfinalization operation payloadのスキーマ世代名であり codec v1 とは別物。ファイル名の"V1"はoperation payload自体のバージョン）
tests/Tsumugi.Application.Tests/UseCases/Claim/ClaimPreparationTestKit.cs:388: SnapshotSchemaVersion = "claim-snapshot-v1" （汎用テストfixtureのデフォルト値、codec実装への参照ではない）
tests/Tsumugi.Application.Tests/Claim/ValidatedClaimSnapshotEnvelopeTests.cs: "claim-snapshot-v1" 文字列リテラル（envelope機構自体はスキーマ非依存であることを示す契約テスト）
tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotValidationCodecV2Tests.cs:34,172: "claim-snapshot-v1"/"claim-snapshot-codec-v1" 文字列リテラル（v2 codecがv1 identityを拒否するnegativeテスト: CreateEnvelope_rejects_v1_schema_version, Registry_exposes_v2_with_write_support_and_rejects_v1）
tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimBatchRepositoryTests.cs, ClaimFinalizationStoreTests.cs: "claim-snapshot-v1" 文字列リテラル（永続化層がスキーマ文字列を検証しないことを示す既存契約テスト）
```

**`ClaimSnapshotValidationCodecV1` というクラス／型への参照は0件**（Task 2で `src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs` を削除済み）。残存する文字列一致は全て、(a) v2がv1 identityを拒否することを示すnegativeテストのリテラル、(b) envelope/永続化機構がスキーマ文字列に依存しないことを示す既存の汎用契約テストのフィクスチャ値、のいずれかであり、backwards-compatコードではない。この分類はTask 2レビューで確認済み（`.superpowers/sdd/progress.md` Task 2節）。

## 4. 依存方向 / オフライン検査

| テスト | 対象 | 結果 |
|---|---|---|
| `Domain_does_not_reference_outer_layers`（`tests/Tsumugi.Domain.Tests/ArchitectureTests.cs`） | Domain → 外層参照禁止 | 緑 |
| `Application_does_not_reference_outer_layers`（`tests/Tsumugi.Application.Tests/ArchitectureTests.cs`） | Application → Infrastructure/UI参照禁止 | 緑 |
| `Infrastructure_does_not_reference_ui_layers` / `Domain_does_not_reference_outer_layers` / `Application_does_not_reference_infrastructure_or_ui` / **`Reporting_does_not_reference_persistence_or_ui`** / `Csv_does_not_reference_persistence_reporting_or_ui`（`tests/Tsumugi.Infrastructure.Tests/ArchitectureTests.cs`） | `Tsumugi.Infrastructure.Reporting → Application/Domain` のみであることを含む全層 | 緑 |
| `Tsumugi_assemblies_do_not_reference_network_libraries`（Theory: Domain/Application/Infrastructure/**Infrastructure.Reporting**/Infrastructure.Csv の5アセンブリ）（`tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`） | `System.Net.*`/`Microsoft.AspNetCore`/`Grpc` 等への直接参照ゼロ、allowlist既定空 | 緑（5/5アセンブリ） |
| `TsumugiApp_dll_does_not_directly_reference_network_apis` / `Tsumugi_assemblies_do_not_pinvoke_into_network_native_libraries` / `Tsumugi_assemblies_do_not_embed_external_url_literals`（`tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`） | App層含む全プロダクションアセンブリのP/Invoke・URLリテラル検査 | 緑 |

`GenerateClaimReportsUseCase`（`src/Tsumugi.Application/UseCases/Claim/GenerateClaimReportsUseCase.cs`）が `IClaimBatchRepository` のみに依存し `IOfficeRepository`/`IRecipientRepository`/`ICertificateRepository`/`IDailyRecordRepository` を一切参照しないことは `Constructor_depends_only_on_IClaimBatchRepository_and_IClaimReportGenerator`（`tests/Tsumugi.Application.Tests/UseCases/Claim/GenerateClaimReportsUseCaseTests.cs`）で固定（コンストラクタが実際に2引数のみであることをリフレクション等で確認する契約テスト）。

### fail-closed の証跡（readiness gate が genuinely fail-closed であることの実配線テスト）

Task 4/5/7/8 で pinned された、実 `JsonClaimMasterProvider`・実requirement catalog・実 `ClaimCalculationSnapshotReader` を通した production wiring の fail-closed テスト（`tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs`）:

- `Real_embedded_requirement_provider_reports_missing_certificate_municipality_number`（Certificate.MunicipalityNumber必須）
- `Real_embedded_requirement_provider_does_not_require_subsidy_municipality_number_when_absent`（SubsidyMunicipalityNumber optional）
- `Real_embedded_requirement_provider_reports_missing_daily_record_field_on_present_day`（Theory: `DailyRecord.ServiceStartTime`/`ServiceEndTime`/`RecipientConfirmation` — Present日に欠落でreadiness issue）
- `Real_embedded_requirement_provider_does_not_require_daily_record_fields_without_a_present_day`（Present日が無ければ必須にならない）
- `Real_embedded_requirement_provider_requires_municipal_subsidy_amount_when_subsidy_municipality_number_is_entered` / `_does_not_require_municipal_subsidy_amount_when_both_legs_are_absent`（Task 7 fix: cross-field Any-merge、恒久NotReady化していないことを固定）
- `Real_embedded_requirement_provider_does_not_require_upper_limit_management_fields_when_provider_number_is_absent` / `_requires_upper_limit_management_result_when_provider_number_is_entered` / `_requires_upper_limit_managed_amount_when_provider_number_is_entered`（Certificate.UpperLimitManagementProviderNumber有無に連動）
- `Real_embedded_requirement_provider_requires_intensive_support_start_date_when_applied_this_month` / `_does_not_require_intensive_support_start_date_when_not_applied_this_month`（Task 8: IntensiveSupportEpisode.StartDate）

`ClaimInputRequirementProviderTests.cs`（`tests/Tsumugi.Infrastructure.Csv.Tests/`）は上記の宣言側（`ClaimInputRequirementProvider`が正しい条件式を登録していること）をユニットレベルで固定: `Provider_registers_daily_record_fields_required_on_present_days`（Theory: daily:004/005/016）、`Provider_combines_intensive_support_start_date_condition_via_any`、`Provider_registers_certificate_report_fields`（Theory）、`Provider_keeps_municipality_number_always_required`、`Provider_keeps_optional_certificate_fields_self_referential`（Theory）、`Provider_combines_upper_limit_management_cross_field_condition_via_any`（Theory）、`Provider_combines_municipal_subsidy_cross_field_condition_via_any`、`Provider_registers_office_report_fields`（Theory）、`Provider_keeps_office_report_fields_always_required`（Theory）。

### 決定論（Task 10/11/12 の3帳票決定論テスト名）

- `GenerateServiceProvisionRecord_is_deterministic_for_same_inputs_and_timeprovider`
- `GenerateClaimInvoice_is_deterministic_for_same_inputs_and_timeprovider`
- `GenerateClaimStatement_is_deterministic_for_same_inputs_and_timeprovider`

いずれも別インスタンスの同時刻`FakeTimeProvider`で生成した2つのバイト列を`Should().Equal`で完全一致検証。

### CJK 検証（Task 10/11/12、`KangxiRadicalNormalizer` 経由）

3帳票テストは共通の`ExtractText`ヘルパ（各テストファイル末尾）で `PdfPig` の `PdfDocument.Open` → 全ページ `.Text` 連結 → `KangxiRadicalNormalizer.FoldKangxiRadicals(...)` を経由してから `Should().Contain` するため、QuestPDF/Skiaが漢字の一部を康熙部首コードポイントとして誤エンコードする既知事象（`docs/open-questions.md`「PDFテキスト抽出時の康熙部首誤置換」、Phase 4 S1由来）を畳み込んだ上で日本語アサーションが成立する。

## 5. 既知の限界（Known limitations）

- **GUI手動貫通確認 未実施**: `dotnet run --project src/Tsumugi.App` での実機起動によるプレビュー→確定→帳票保存×3のクリックスルーは、Phase 3-1と同様に本タスクでも未実施。自動化証跡（ViewModelテスト・production wiring テスト）は完備だが、実際のAvalonia UI操作での目視確認は open-questions の既存項目「Avalonia GUI 目視確認 (AC1-8 補完)」に統合して残課題として維持する。
- **Task 7 review Important #3 — 4フィールドの孤立した自己参照readiness条件**: `ClaimInput.ExceptionalUsageStartMonth`/`ExceptionalUsageEndMonth`/`ExceptionalUsageDays`/`StandardUsageDayTotal`（`provider:J121:04:030-033`）は自己参照のみの`modelPresent`/`modelNonZero`条件で、`MunicipalSubsidyAmountYen`等が持つcross-field Any-mergeを持たないため恒常的にfail-open（値の有無に関わらずreadiness issueにならない）。spec §3.1の21 `report:*`フィールド一覧には含まれず、CSV `provider:*`スコープ（Phase 3-3）に属するためPhase 3-2の対象外。`docs/open-questions.md`に新規起票済み（§6参照）。
- **production DB migration前提**: v1→v2の破壊的置換は「production DBに確定revisionが存在しない」（spec §4.2）という前提に立つ。Phase 3-1マージ直後でGUI貫通も未実施のため、この前提は本受け入れ時点でも引き続き成立している（確定revisionを書き込むUI操作パス自体が人手で実行されていないため）。この前提が崩れた場合は別途data migration ADRの起票が必要。
- **`AbsenceResponseNote`の汎用フィールド流用**（Phase 3-2 Task 3由来、継続）: `DailyRecord.Note`汎用フィールドから`absenceResponseNote`へマップしており、専用カラムではない。将来帳票側で区別が必要になった場合に見直す。
- **`ContractedProvider`の証訂正後staleness**（Phase 3-1継承）: `docs/open-questions.md`「ContractedProviderとContractの整理」に未クローズのまま残存。

## 6. `./build/ci.sh` 実行証跡

実行日: 2026-07-20（Task 15、docsのみの変更後にsafety再実行）

```
==> restore
  復元対象のプロジェクトを決定しています...
  復元対象のすべてのプロジェクトは最新です。
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
  （Domain / Application / Infrastructure.Reporting / Infrastructure / Infrastructure.Csv / App 全プロジェクトビルド成功）

ビルドに成功しました。
    0 個の警告
    0 エラー

==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!   -失敗:     0、合格:    79、スキップ:     0、合計:    79、期間: 224 ms - Tsumugi.Infrastructure.Csv.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   666、スキップ:     0、合計:   666、期間: 386 ms - Tsumugi.Domain.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   411、スキップ:     0、合計:   411、期間: 335 ms - Tsumugi.Application.Tests.dll (net10.0)
成功!   -失敗:     0、合格:    27、スキップ:     0、合計:    27、期間: 1 s - Tsumugi.Infrastructure.Reporting.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   242、スキップ:     0、合計:   242、期間: 4 s - Tsumugi.App.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   625、スキップ:     0、合計:   625、期間: 24 s - Tsumugi.Infrastructure.Tests.dll (net10.0)

==> coverage threshold gate (gate #3 enforcement — floor=Domain 95% / Application 70%, raise Application in Phase 3)
...
| Tsumugi.Domain | 95.58% | 88.19% | 93.83% |          (Line / Branch / Method)
...
| Tsumugi.Application | 94.47% | 85.3% | 89.76% |       (Line / Branch / Method)

==> CI OK
```

合計 2,050 件成功・0失敗（79+666+411+27+242+625）。警告ゼロ。Domain line coverage 95.58%（≧95%floor）、Application line coverage 94.47%（≧70%floor）。Task 14終了時点（`.superpowers/sdd/progress.md`記載の2,050件）と一致し、Task 15はdocsのみの変更でテスト件数に影響していないことを確認した。

## 7. 本スライスで提供したスコープ

- Claim snapshot codec v2（`claim-snapshot-v2` / `claim-snapshot-codec-v2`）による office/recipient/certificate/dailyRecord[]/claimInput/intensiveSupportEpisode/claimLines[] の確定時凍結（Task 2）
- `OperationLocalSnapshotReader` による確定時の entities → v2 payload 集約、`CloseClaimUseCase` への配線（Task 3）
- 21 `report:*` フィールドの実UI露出（DailyRecordView 10 + Certificate View 3 + OfficeView 4 + ClaimInputView 3 + IntensiveSupport 1）と readiness gate 拡張（Task 4-8、ADR 0030）
- 帳票DTO群 + `IClaimReportGenerator` 抽象（Task 9）
- 3帳票QuestPDF実装: サービス提供実績記録票 / 介護給付費・訓練等給付費等請求書 / 介護給付費・訓練等給付費等請求明細書（Task 10-12）
- `GenerateClaimReportsUseCase`（`IClaimBatchRepository`のみ依存）+ DI配線（Task 13）
- `ClaimPreparationView`「帳票出力」セクション（`ClaimReportSection` VM + 3保存コマンド）（Task 14）

対象外（凍結・Phase 3-1から継続）: 保護施設事務費・基準該当B型、R8-06改定対象12区分の新単価、R8-06処遇改善加算の率・新コード対応、band-participation option 10、上限管理statement-graph解決。詳細は spec §3.2 と `docs/open-questions.md`。
