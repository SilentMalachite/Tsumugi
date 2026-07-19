# Phase 3-1（最小垂直スライス）受け入れ証跡

- 対象: `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`（正本spec）§8 成功基準5項目
- 実装計画: `docs/superpowers/plans/2026-07-19-phase3-1-minimal-vertical-slice.md`（Task 1〜13 完了・チェック済み。本書はTask 14の証跡）
- 記録日: 2026-07-19 / ブランチ: `feature/phase3-1-vertical-slice`
- 本書はTask 14ブリーフ（`.superpowers/sdd/task-14-brief.md`）Step 2「受け入れ証跡を記録」の成果物。詳細な実行ログは `.superpowers/sdd/task-14-report.md` を参照。

## 1. §8 成功基準5項目の判定

| # | 成功基準 | 判定 | 証跡 |
|---|---|---|---|
| 1 | 典型的なB型事業所の1ヶ月分の請求が、入力→算定プレビュー→確定までUIから通る | **部分** | 実際の本番DI配線（実`JsonClaimMasterProvider`・実requirement catalog・実`ClaimCalculationSnapshotReader`）を通してReadyプレビューに到達することを`tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs`の`Real_production_wiring_reaches_ready_preview_with_the_expected_r6_service_code`・`Real_production_wiring_bills_a_seeded_addition_when_the_capability_is_held`・`Real_embedded_requirement_provider_reaches_ready_preview_when_snapshot_is_fully_entered`で固定。`ClaimPreparationViewModel`（`src/Tsumugi.App/ViewModels/ClaimPreparationViewModel.cs`）はPreview/Close(確定)/Cancel(取下げ)コマンドを`CalculateClaimPreviewUseCase`/`CloseClaimUseCase`/`CancelClaimUseCase`へ実配線し、`tests/Tsumugi.App.Tests/ClaimPreparationViewModelTests.cs`（10件）で検証済み。**ただし**計画Task 10 Step 3は「自動起動確認＋VMテストで代替。GUI手動確認はユーザー実施推奨」と明記の通り、`dotnet run --project src/Tsumugi.App`での実機・人手による最終クリックスルー確認は本タスクでは未実施（既存open-question「Avalonia GUI目視確認」と同種の残課題）。自動証跡は完備だが人手のUI貫通確認が残るため部分判定とする。 |
| 2 | golden caseテストが公式計算例と一致し、全テスト緑・警告ゼロ・`./build/ci.sh`緑 | **✔** | `./build/ci.sh` 実行結果は末尾 `==> CI OK`。build: `0 個の警告 / 0 エラー`。test合計 1,960件成功・0失敗（Domain 666 / Application 377 / Infrastructure 613 / App 234 / Csv 58 / Reporting 12）。golden caseは`tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`の`Matches_adr_0027_worked_examples`（Theory、R6基本報酬）・`Matches_adr_0028_worked_example_a_fixed_unit_additions`・`_b_unified_treatment_improvement`・`_c_capacity_initial_and_same_premises`（ADR 0028加算例、初期加算300単位＋送迎同一敷地252単位＝月次給付単位数11,252単位・総費用額112,520円の手計算一致を含む）。R8-06境界月（2026-05/2026-06）は`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs`の`Basic_reward_rows_continue_unchanged_across_the_r8_boundary`（基本報酬135/135行が無変更で継続）で固定。 |
| 3 | seed 6ファイルのうち本スライス対象分に実値が入り、validator・出典メタデータ検証を通過している | **✔**（保護施設・基準該当B型は§3.2により凍結スコープ外のため空スタブのまま。これは欠落ではなく仕様上の意図） | 実値行数（直接JSON検査）: `basic-rewards.json`=135、`additions.json`=16、`region-unit-prices.json`=8、`burden-caps.json`=4、`transition-rules.json`=3、`service-codes.json`=151（135基本報酬対応＋16加算対応）。`JsonClaimMasterProvider.LoadEmbedded()`（内部で`ClaimMasterFileValidator.Prepare/ValidateAll`を通し、entry必須項目`key/effectiveFrom/effectiveTo/sourceRefs/values`とsourceRef必須項目`documentId/sha256/locator/evidenceRole/supports`を検証）が例外なく成功することを`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`の`LoadEmbedded_succeeds_with_populated_seeds`で確認、行数を`LoadEmbedded_embeds_the_adr0027_r6_seed_row_counts`（135/135/8）で固定。出典・値の根拠は ADR 0027（R6基本報酬・サービスコード・地域区分単価）、ADR 0028（主要加算16行）、ADR 0022（負担上限4区分）、ADR 0023（平均工賃正式式・R8遷移規則3件）。 |
| 4 | `docs/open-questions.md`に、スコープから外した未確定値が漏れなく起票されている | **✔** | §2「明示的に対象外・凍結」の一覧が全て`docs/open-questions.md`の該当行に対応（表を参照）。新規追加漏れなし。 |
| 5 | 進捗が新実装計画のチェックボックスだけで判定できる | **✔** | `docs/superpowers/plans/2026-07-19-phase3-1-minimal-vertical-slice.md`はTask 1〜13の全ステップが`- [x]`済み。Task 14（本タスク）のStep 1〜3のみ未チェックのまま（ブリーフの指示によりチェックは入れず、本受け入れ証跡と`CLAUDE.md`現在地更新をもって完了根拠とする）。旧Task 1-28計画・task12/13スピンオフ文書群への追記は本タスクを通じて発生していない（specの文書増殖禁止ルール§4.3を維持）。 |

### `./build/ci.sh` 実行結果（末尾）

```
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
...
成功!   -失敗:     0、合格:    58、スキップ:     0、合計:    58、期間: 177 ms - Tsumugi.Infrastructure.Csv.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   666、スキップ:     0、合計:   666、期間: 393 ms - Tsumugi.Domain.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   377、スキップ:     0、合計:   377、期間: 330 ms - Tsumugi.Application.Tests.dll (net10.0)
成功!   -失敗:     0、合格:    12、スキップ:     0、合計:    12、期間: 904 ms - Tsumugi.Infrastructure.Reporting.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   234、スキップ:     0、合計:   234、期間: 4 s - Tsumugi.App.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   613、スキップ:     0、合計:   613、期間: 25 s - Tsumugi.Infrastructure.Tests.dll (net10.0)
==> coverage threshold gate (gate #3 enforcement — floor=Domain 95% / Application 70%, raise Application in Phase 3)
...
| Tsumugi.Domain | 95.58% | 88.19% | 93.83% |     (Line / Branch / Method)
...
| Tsumugi.Application | 93.73% | 86.65% | 87.75% |  (Line / Branch / Method)
==> CI OK
```

## 2. `Logic.Claim` 分岐カバレッジ実測（正本AC3-3: 100%目標）

**計測方法**: `dotnet test tests/Tsumugi.Domain.Tests -c Release -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:Include="[Tsumugi.Domain]*"` でcobertura XMLを生成し、`Tsumugi.Domain.Logic.Claim`名前空間配下の全クラス（`Logic/Claim/*.cs` および `Logic/Claim/Models/*.cs`、62クラス）についてのみ`<line branch="True">`の`condition-coverage`属性（`covered/valid`）を集計した（ci.shのDomain全体しきい値ゲートとは別の的を絞った実測。ci.shはDomain**全体**95%/line基準のみを強制し、`Logic.Claim`単体の分岐は強制していない）。

**実測値**: **88.28%**（2,546 / 2,884 branches）。100%未達。

不足箇所（部分カバーの行がある12ファイル、多い順）:

| ファイル | 部分カバー行数 | 未到達branch数 |
|---|---|---|
| `Logic/Claim/ServiceCodeResolver.cs` | 18 | 68 |
| `Logic/Claim/OfficeClaimProfilePolicy.cs` | 38 | 56 |
| `Logic/Claim/UpperLimitManagementStatementPolicy.cs` | 26 | 50 |
| `Logic/Claim/ClaimCalculator.cs` | 20 | 24 |
| `Logic/Claim/CertificateClaimEvidencePolicy.cs` | 18 | 24 |
| `Logic/Claim/Models/ClaimMasterRelease.cs` | 8 | 24 |
| `Logic/Claim/AverageWageAnnualEvidencePolicy.cs` | 16 | 22 |
| `Logic/Claim/ClaimInputPolicy.cs` | 6 | 12 |
| `Logic/Claim/ClaimMasterCatalogPolicy.cs` | 4 | 12 |
| `Logic/Claim/IntensiveSupportEpisodePolicy.cs` | 6 | 12 |
| `Logic/Claim/Models/ClaimInputModels.cs` | 10 | 12 |
| `Logic/Claim/ClaimBatchPolicy.cs` | 6 | 10 |
| `Logic/Claim/AverageWageFormula.cs` | 4 | 8 |
| `Logic/Claim/CertificatePolicy.cs` | 4 | 4 |

理由（傾向、個別行の逐一調査は未実施）: 未到達分岐の大半は各Policyのガード節（複合条件`&&`/`??`のうち一方の枝、null分岐、defined-enum検証等）で、フェイルクローズ系の防御的分岐が正常系golden caseテストでは踏まれない構造。特に`ServiceCodeResolver.cs`の208/219/227/238行（それぞれ25%・62.5%・23.5%・0%）はTask 13で追加したR8遷移状態×体制届optionの組合せ検証で、一次資料未確定のため`docs/open-questions.md`へ起票してスコープ外にした分岐（改定対象新12区分・band-participation option10等）に対応する防御コードであり、意図的に「入力が来ないので通らない」経路である可能性が高い。100%到達には、これら防御的分岐ごとに意図的なnegativeテストを追加するか、スコープ外分岐を明示的に`[ExcludeFromCodeCoverage]`等で除外する方針決定が必要（本タスクでは修正せず実測記録のみ）。

## 3. 本スライスで提供したスコープ

- 基本報酬135行（令和6年度改定、ADR 0027。R8-06[2026-06]までの継続をTask 13で境界月テスト込みで確認済み）
- 主要加算16行（ADR 0028: 福祉専門職員配置等加算(Ⅰ)(Ⅱ)(Ⅲ)・目標工賃達成指導員配置加算[定員5区分]・食事提供体制加算・欠席時対応加算・送迎加算(Ⅰ)(Ⅱ)・統一 福祉・介護職員等処遇改善加算(Ⅰ)〜(Ⅳ)）
- 地域区分単価8区分
- 利用者負担上限4区分（ADR 0022）
- R8-06経過措置遷移規則3件（ADR 0023）
- 上記に対応するサービスコード151行、`ServiceCodeResolver`/`ClaimCalculator`（Domain純粋関数）、`SnapshotReader`、production snapshot codec、Calculate/Close/Cancel/QueryのUseCase、`ClaimPreparation`画面配線（Task 1〜13、全チェック済み）

## 4. 明示的に対象外・凍結（`docs/open-questions.md`起票済み）

| 対象 | 起票状況 |
|---|---|
| 保護施設事務費・基準該当B型（実値seed・入力UI・runtime算定） | `docs/open-questions.md`「[Phase3-1/Task13 follow-up] 保護施設事務費実値・production seed・runtime算定」に未クローズで起票。schema v2契約・validator・契約テストは資産として維持（削除せず）、runtime実装は将来スライスへ凍結 |
| R8改定対象12区分（reform-target service-code、option 11〜22）の新単価 | `docs/open-questions.md`「[Phase3-1/Task13 follow-up] R8-06改定対象（reform-target）の新12区分基本報酬行とoption 10のR8状態対応」。`ReformTarget`事業所の2026-06以降はtransition-rulesの状態別許可option集合と経過措置guardでフェイルクローズ |
| R8-06処遇改善加算の率・新コード対応 | `docs/open-questions.md`「[Phase3-1/Task11] R8-06の福祉・介護職員等処遇改善加算の率・新コード対応」。`claim-master-r8-06`の処遇改善seedは別ADR確定まで投入しない |
| band-participation option 10（生産活動支援）の対応 | `docs/open-questions.md`「[Phase3-1/Task 9] 参加評価型（band-participation）と体制届option 10の対応」。pinned test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`の`Load_rejects_average_wage_band_token_operand`等（commit `73c1dcc`） |
| 平均工賃区分の自動導出（PaymentBand閾値マスタ未定義） | `docs/open-questions.md`「[Phase3-1/Task13] 平均工賃月額からの区分（band）自動導出」。`ComputedAverageWageMonthYen`算出額提示までを実装し、band導出との矛盾検知readinessは閾値マスタ投入後に実装 |
| 上限管理statement-graph解決（`UpperLimitManagementStatement`全体グラフ） | Task 12は`ClaimInput.UpperLimitManagementResult`/`UpperLimitManagedAmountYen`（既存の事業所ローカル値）の適用のみをスコープとし、複数事業所間の`UpperLimitManagementStatement`エンティティグラフ解決は対象外（`.superpowers/sdd/task-12-report.md`に明記。CLAUDE.md現在地の既存注記「resolver・runtime請求計算…は未実装」と整合） |
| ContractedProviderの証訂正後staleness | 既存open-question「ContractedProviderとContractの整理」（`docs/open-questions.md`、社内呼称open-question #36）が未クローズのまま残存。`src/Tsumugi.Infrastructure/Persistence/ClaimCalculationSnapshotReader.cs`の`ReadEffectiveContractedProvidersByRecipientAsync`のXML doc-commentにstaleness限界を明記済み（`.superpowers/sdd/task-9c-report.md`） |
| 送迎同一敷地内判別・初期加算の専用ストレージ・欠席時対応の一部（465050/466592/466593） | `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimAdditionSeedScopeTests.cs`の`Storage_gap_rows_and_legacy_treatment_improvement_rows_stay_unseeded`が465050（初期加算: 利用開始日の専用ストレージなし）・466592/466593（送迎同一敷地内判別フィールドがDailyRecord/Certificate/OfficeCapabilityのいずれにも存在しない）をseed除外として固定。ADR 0028補足表に「gap」として明記 |

## 5. 既知の限界（Known limitations）

- 記録された全Minor findings（各TaskのCodexレビュー・自己レビューで挙がった軽微な指摘）は、最終ブランチレビュー（`feature/phase3-1-vertical-slice` → `main`）でトリアージ予定。本書はその前段の受け入れ判定であり、Minor findingsの解消状況を個別追跡しない。
- `Logic.Claim`分岐カバレッジは88.28%（§2参照）であり、正本AC3-3の100%目標を満たしていない。原因は防御的フェイルクローズ分岐の一部が正常系テストでは踏まれない構造によるもので、本タスクでは実測記録に留め修正しない。
- Task 10 Step 3のUI貫通確認は自動起動確認＋VMテストで代替しており、人手によるGUIクリックスルーは未実施（既存open-question「Avalonia GUI目視確認」と同種）。

## 6. Task 9由来のTask 12設計文書との乖離（記録のみ・凍結文書は編集しない）

`docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`（closed contract、Task 12完了記録として引き続き有効）は、`average-wage-band`条件のoperandを「token string / token string array」（同spec416-420行目付近）と定義している。しかしTask 9（commit `581f4a8` "encode payment bands as official option code conditions"）で、ADR 0023「平均工賃月額区分は体制届の公式選択番号（正の整数`officialOptionCode`）で判定する」という決定に基づき、`ServiceCodeResolver`が`AverageWageBandOption.OfficialOptionCode`（整数）と比較する実装へ変更された。これに伴い`ClaimMasterFileValidator`（`src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs` 585〜604行目）は`average-wage-band`条件を**整数equals（正の値のみ）限定**で受理し、token登録・非equals演算子・ゼロ値を読み込み時点で拒否するよう変更されている。pinned negative testsは`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`の`Load_rejects_average_wage_band_token_operand`・`Load_rejects_average_wage_band_non_equals_operator`・`Load_rejects_average_wage_band_zero_option_code`（commit `73c1dcc`）。

Task 12設計文書は本spec §9の「引き続き有効（完了記録）」区分に属し、ガバナンス（specの文書増殖禁止ルール§4.3・凍結領域の契約リファイン禁止）により編集しない。本節がこの実装済みの乖離を記録する唯一の場所である。
