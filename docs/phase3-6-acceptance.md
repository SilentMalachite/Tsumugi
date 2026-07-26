# Phase 3-6（R6-06世代 処遇改善の施設variantと(Ⅴ)、体制届option存在検査）受け入れ証跡

- 対象: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` — Phase 3-5 最終レビューが持ち越したR6-06世代の施設区分欠落、ADR 0045 が未確定として保留した処遇改善(Ⅴ)、およびその一般形である「宣言された体制届optionに対応するマスタ行が当月に無いときの無音の0円」
- spec: `docs/superpowers/specs/2026-07-26-phase3-6-r6-06-treatment-improvement-design.md`
- 計画: `docs/superpowers/plans/2026-07-26-phase3-6-r6-06-treatment-improvement.md`
- 実装ブランチ: `feature/phase3-6-r6-06-treatment-improvement`
- 実行台帳（本証跡の一次情報源）: `.superpowers/sdd/2026-07-26-phase3-6-r6-06-treatment-improvement/progress.md`
- Task別詳細報告: `task-0-report.md` 〜 `task-6-report.md`（同ディレクトリ）
- ADR: [0048](decisions/0048-r6-06-treatment-improvement-facility-and-category-v.md) / [0049](decisions/0049-office-capability-master-coverage-check.md)

---

> **状態（2026-07-27）**: 一次資料の再照合（Task 0）→ 施設variant 3区分の投入（Task 1）→ 処遇改善(Ⅴ) 23行の投入（Task 2）→ 体制届選択肢のマスタ導出（Task 3）→ 公式体制届キーの入力面（Task 4）→ 体制届option存在検査（Task 5）の順に実装した。
> 指定障害者支援施設が **2024-06〜2026-05** についても施設コード・施設率へ正しく解決するようになり、処遇改善(Ⅴ)は **2024-06〜2025-03** に限って算定できるようになった。あわせて、**(Ⅴ)はR8-06世代（2026-06〜）には存在しない**ことを一次資料から確定し、ADR 0045 の「R8へ(Ⅴ)を投入する必要がある」という前提の誤りを訂正した。
> コードとseedデータは Task 1〜5 で完結しており、本タスク（Task 6）は文書のみを更新した。
> **重要な運用上の帰結**は §3-1 に記載する（2024-06以降の全月で施設区分の入力が必須になる。過去月の訂正にも及ぶ）。

---

## 1. 達成状況と証拠

| # | 達成内容 | 証拠（テスト名） |
|---|---|---|
| 1 | R6-06の施設variant 3区分（(Ⅰ)=465138・(Ⅲ)=465140・(Ⅳ)=465141）が施設区分ごとにちょうど1行へ解決する（多重一致なし） | `ClaimMasterR6FacilityTests.Facility_variants_resolve_to_exactly_one_row_per_classification`（Theory 3ケース） |
| 2 | 施設別立てが無い(Ⅱ)（option 3）は施設事業所でも通常行へ解決する | `ClaimMasterR6FacilityTests.Tier_two_has_no_facility_variant_and_resolves_for_both_classifications`（Theory 2ケース） |
| 3 | 施設区分未入力のまま施設variantを持つ区分を解決しようとすると `FacilityClassificationUnresolved` でfail-closeする | `ClaimMasterR6FacilityTests.An_unresolved_facility_classification_fails_closed` |
| 4 | R6-06の施設variant 3行が 2026-06 に到達しない（世代境界の上側） | `ClaimMasterR6FacilityTests.R6_facility_rows_do_not_reach_june_2026` |
| 5 | R8-06の処遇改善行が 2026-05 に到達しない（世代境界の下側。**行キー**で判定。§4-2参照） | `ClaimAdditionSeedScopeTests.R8_treatment_improvement_rows_apply_only_from_2026_06` |
| 6 | 施設variant 3コードの下限境界（2024-04 に現れない） | `ClaimAdditionSeedScopeTests.Unified_treatment_improvement_rows_apply_only_between_2024_06_and_2026_05` |
| 7 | (Ⅴ)14サブ区分が通常事業所で全件解決する | `ClaimMasterR6FacilityTests.Category_v_subdivisions_resolve_for_a_general_office`（Theory 14ケース） |
| 8 | (Ⅴ)のうち施設variantを持つ9区分が施設事業所で施設行へ解決する | `ClaimMasterR6FacilityTests.Category_v_facility_variants_resolve_for_a_facility_office`（Theory 9ケース） |
| 9 | (Ⅴ)のうち施設variantを持たない5区分（⑶⑷⑹⑼⑿）は両方の施設区分で通常行へ解決する | `ClaimMasterR6FacilityTests.Category_v_subdivisions_without_a_facility_variant_resolve_for_both`（Theory 5ケース。各ケースが本体で `general` / `designated-support-facility` の両方を回す） |
| 10 | (Ⅴ)は 2025-03 で失効し 2025-04 では解決しない | `ClaimMasterR6FacilityTests.Category_v_expires_at_the_end_of_march_2025` |
| 11 | R6-06世代の処遇改善サービスコード集合が公式表の30件と過不足なく一致する（上限も下限も固定） | `ClaimMasterR6FacilityTests.The_r6_treatment_improvement_codes_match_the_official_table_exactly` |
| 12 | 率が production seed から pin されている（1単位変えると検出） | `ClaimMasterR6FacilityTests.R6_treatment_improvement_percentages_match_the_notice`（Theory 7ケースのspot check。施設3区分の全件と、(Ⅴ)の上下端 ⑴・⒁ の通常/施設） |
| 13 | 体制届の選択肢が当月のマスタ条件定義から導出される（UIが語彙を持たない。ADR 0021） | `QueryClaimBillingTokenOptionsProductionWiringTests`（5件: R6の option 2〜6／(Ⅴ)の2025-03失効／R8の6区分に(Ⅴ)が無い／v-band が(Ⅴ)有効期間にだけ存在する／2系統が混ざらない） |
| 14 | 2系統（`treatment-improvement.*` と `treatment-improvement-v-band.*`）が接頭辞一致で混ざらない | `QueryClaimBillingTokenOptionsCapabilityTests.Synthetic_condition_definitions_keep_the_two_capability_families_isolated` |
| 15 | 公式体制届キー（処遇改善 対象区分・(Ⅴ)区分）が one-hot で保存される。語彙外は書かない | `OfficeCapabilityViewModelTests`（`SaveAsync_writes_the_official_treatment_improvement_key` / `..._writes_only_the_selected_option_as_one_hot` / `..._writes_the_selected_category_v_band` / `..._does_not_write_a_band_when_the_month_has_no_band_options` / `..._does_not_write_an_out_of_vocabulary_option_key`） |
| 16 | band 側の書き込みは選択番号を条件にしない（無害な向き。§6-2）。**有害な逆向き（option だけで band が無い）は行28〜30 のとおり最終レビューで塞いだ** | `OfficeCapabilityViewModelTests.SaveAsync_writes_the_band_key_regardless_of_the_selected_option_number` |
| 17 | 期間を変えると語彙が入れ替わり、旧語彙の選択は書かれない | `OfficeCapabilityViewModelTests.SaveAsync_does_not_write_the_option_key_after_the_period_changes_to_a_generation_lacking_it` |
| 18 | `DiscardCommand` が既定期間へ戻し、選択肢が再充填される（空のまま使用不能にならない） | `OfficeCapabilityViewModelTests.DiscardCommand_resets_state_to_the_default_period_without_throwing` |
| 19 | `ServiceMonth` の年範囲外を `PeriodStart` へ直接入力しても落ちない | `OfficeCapabilityViewModelTests.ReloadCapabilityOptions_does_not_throw_for_a_period_start_year_outside_the_service_month_range` |
| 20 | View に2つの入力が結線されている（`ViewInputWiringTests` の検査対象に追加） | `ViewInputWiringTests.OfficeCapabilityView_exposes_treatment_improvement_option_and_v_band_inputs` |
| 21 | 体制届option存在検査（2段構え。当月に無い ∧ 他の期間には有る） | `OfficeCapabilityCoveragePolicyTests`（9件: 判定4件＋`ExtractCapabilityValues` 5件） |
| 22 | 警告が `IsReady` を落とさない／当月に有るキーでは出ない／無関係な理由の not-ready でも運ばれる | `CalculateClaimUseCaseTests`（`Execute_warns_about_declared_capabilities_without_master_rows_this_month` / `..._does_not_warn_when_the_declared_capability_is_covered_this_month` / `..._still_surfaces_capability_coverage_warnings_when_not_ready_for_an_unrelated_reason`） |
| 22b | 請求に効かないキーでは警告が出ない（偽陽性の不在） | `OfficeCapabilityCoveragePolicyTests.A_key_never_used_by_any_condition_is_ignored`（Domain層。`allConditionValues` が非空の状態で判定するため主張が成立する）。**Application層の `CalculateClaimUseCaseTests..._does_not_warn_about_a_declared_capability_never_referenced_by_any_condition` はこの主張の証拠にはならない** —— §6-5 が開示するとおり `all` が空であるために通っており、判定関数が偽陽性を出さないことを示していない |
| 23 | 実seedに対して(Ⅴ)が 2025-04・2026-06 で「未カバーの体制届キー」になる | `ClaimMasterR6FacilityTests.Category_v_becomes_an_uncovered_capability_after_it_expires`（Theory 2ケース） |
| 24 | 警告がUIまで届き、`IsReady` を落とさない | `ClaimPreparationViewModelTests.PreviewAsync_surfaces_capability_coverage_warnings_without_blocking_readiness` |
| 25 | `office-capability` トークンの ADR 0021 形状（ちょうど5セグメント）が強制される | `ClaimMasterSchemaPhase31Tests.Load_rejects_a_short_form_office_capability_key` / `..._rejects_a_nested_office_capability_key` |
| 26 | 同一フィールドの条件衝突は引き続き拒否し、異なるフィールドの合成は許可する | `ClaimMasterSchemaPhase31Tests.Load_rejects_empty_office_capability_intersections_within_the_same_field` / `..._accepts_office_capability_conditions_composed_across_different_fields` |
| 27 | `calculationOrder` のスキャナ除外が `amount` 祖先の直下に限定されている | `ClaimSpecificationBoundaryTests`（3件: 除外が効く／同じ `amount` 内の他の数値は除外しない／`amount` 外の同名プロパティは除外しない） |
| 28 | 施設区分未入力の fail-close が**アプリを落とさず**、入力すべき欄を名指しする固定文言になる（C1。§3-1） | `ClaimPreparationViewModelTests.PreviewAsync_maps_an_unresolved_facility_classification_to_a_message_naming_the_field` / `..._CloseAsync_maps_an_unresolved_facility_classification_instead_of_terminating` |
| 29 | band を要求する選択番号を band 未選択で保存できない（無音0円の入口を塞ぐ。I1。§6-2） | `OfficeCapabilityViewModelTests.SaveAsync_rejects_an_option_that_requires_a_v_band_when_no_band_is_selected` / `..._accepts_an_option_that_requires_a_v_band_when_the_band_is_selected` / `..._accepts_an_option_that_does_not_require_a_v_band_without_a_band` |
| 30 | 「band を要求する選択番号」が実seedから導出される（R6 は option 6 のみ／失効後・R8 は該当なし） | `QueryClaimBillingTokenOptionsProductionWiringTests.Only_category_v_requires_a_band_in_the_r6_generation` / `..._No_option_requires_a_band_once_category_v_is_gone`（Theory 2ケース） |
| 31 | 体制届画面の既定期間が現在月から導かれ、当該世代の選択番号が選べる（I2） | `OfficeCapabilityViewModelTests.The_default_period_start_follows_the_current_month_and_exposes_that_generation` / `..._DiscardCommand_returns_to_the_current_month_not_a_fixed_date` |
| 32 | 施設区分未入力の fail-close の影響範囲が `conditionSelectors` の**配列順に依存しない**（I3。ADR 0048 決定5） | `ServiceCodeResolverTests.An_unresolved_facility_classification_does_not_throw_when_another_condition_fails`（Theory 2ケース） / `..._still_fails_closed_when_every_other_condition_matches`（Theory 2ケース） |
| 33 | 有効な `office-capability` 条件定義は、有効な service-code 行から必ず参照されている（spec §6.1 条件2 の前提。item 6。§6-4） | `ClaimMasterCapabilityCoverageTests.Every_effective_office_capability_condition_is_referenced_by_an_effective_service_code_row` / `..._The_check_detects_a_capability_condition_that_no_service_code_row_references`（判定関数の歯） |
| 34 | option 未選択のとき `treatment-improvement.*` を1件も書かない（one-hot の下限） | `OfficeCapabilityViewModelTests.SaveAsync_writes_no_option_key_when_no_option_is_selected` |
| 35 | 新規2 ComboBox が `AutomationProperties.Name` を持つ（ハード制約5） | `ViewInputWiringTests.OfficeCapabilityView_exposes_treatment_improvement_option_and_v_band_inputs` |

---

## 2. 投入した行数の実績

実測値は `git diff b8bada0..fdd6f58` と各JSONの `entries` / `conditionDefinitions` 配列長を本証跡作成時に再計測して確認した（`b8bada0` ＝本スライス着手直前のベースコミット）。

| ファイル | 投入前 | 投入後 | 差分 | 内訳 |
|---|---:|---:|---:|---|
| `conditionDefinitions`（`service-codes.json` 内） | 53 | 70 | **+17** | 施設区分2件（`facility-classification-{general,designated-support-facility}-r6-06`）＋(Ⅴ)本体1件（`capability-treatment-improvement-v`）＋(Ⅴ)サブ区分14件（`capability-treatment-improvement-v-band-{1..14}`） |
| `additions.json`（entries） | 26 | 52 | **+26** | 施設variant3行（0.104・0.086・0.069、`calculationOrder` 5〜7）＋(Ⅴ)通常14行（0.080〜0.031、8〜21）＋(Ⅴ)施設9行（0.091〜0.035、22〜30） |
| `service-codes.json`（entries） | 341 | 367 | **+26** | 465138・465140・465141／465124〜465137／465142・465143・465146・465148・465149・465151・465152・465154・465155 |
| 既存行の `conditionSelectors` 変更 | — | — | **3行** | `unified.i`／`unified.iii`／`unified.iv`（465120/465122/465123）の末尾へ `facility-classification-general-r6-06` を追加。**`unified.ii`（465121）は無変更** |

seed の完全性は Task 2 完了時にコーディネータが `difflib` で独立検証した（既存エントリの削除0・内容変更0・並び順保持、opcodes が `equal` ＋ `insert` のみ）。`git numstat` が示す 5,469 削除は git の差分アライメント由来の見かけであり、実際の削除は0件である。

コード側は次の要素を追加・変更した（本証跡作成時に実ファイルで再確認済み）。

- `OfficeCapabilityCoveragePolicy`（新規 Domain 純粋関数。`FindUncoveredKeys` / `ExtractCapabilityValues`）
- `IClaimMasterProvider.AllOfficeCapabilityConditionValues()`（1メソッド追加。実装8箇所＝production 1・テストfake 7）
- `ClaimBillingTokenOptionsDto` に `TreatmentImprovementOptions` / `TreatmentImprovementVBandOptions`（`IReadOnlyList<int>`）
- `ClaimPreviewDto.CapabilityCoverageWarnings`（末尾の省略可能パラメータ）／`ClaimPreparationViewModel.CapabilityCoverageWarnings`／`ClaimPreparationView.axaml` の表示ブロック
- `ClaimCalculationRequestBuilder.ResolveDeclaredOfficeCapabilityKeys`（request 構築の成否から独立した宣言キー解決）
- `OfficeCapabilityViewModel` に ComboBox 2つ分の状態（`TreatmentImprovementOption` / `TreatmentImprovementVBand` と各 `Options` コレクション）／`OfficeCapabilityView.axaml` の ComboBox 2個
- `ClaimMasterFileValidator` の2箇所（`ValidateConditionIntersection` の family 分割・`ValidateConditionToken` の5セグメント強制。ADR 0048「影響」§3）
- `ExternalSpecificationLiteralGuard` の `calculationOrder` scanner skip（ADR 0048「影響」§4）

**エンティティ・migration の変更は無い。** (Ⅴ)区分は体制届の1行そのものであり、`OfficeCapability.Flags`（`IReadOnlyDictionary<string, bool>`）へキーを増やすだけで成立する（ADR 0048 決定4／spec 決定1）。Phase 3-5 の `FacilityClassification` が `OfficeClaimProfile` へ列として追加されたのは、施設区分が体制届に**無い**項目だったためであり、一貫性の欠如ではない。

---

## 3. spec からの逸脱と、実装中に判明したこと

### 3-1. 【最重要】2024-06〜2026-05 の全月で施設区分の入力が必須になる（過去月の訂正に及ぶ）

spec §7-1 が予告したとおりの帰結だが、**影響範囲が Phase 3-5 の同種の変更より広い**ため、ここに明記する。

**本ブランチ以降、`OfficeClaimProfile.FacilityClassification` が NULL のまま処遇改善(Ⅰ)/(Ⅲ)/(Ⅳ)（体制届 option 2/4/5）を宣言している事業所は、2024-06 から 2026-05 までの *どの月も* preview も再確定もできない。** `ServiceCodeResolver.EvaluateFacilityClassification` が `ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved` を投げる。

**当初この帰結を「呼び出し元での未捕捉例外として表面化する」と記述したが、それは実際の挙動を過小に伝える表現だった。** `ServiceCodeResolutionException` は `src/Tsumugi.Application` にも `src/Tsumugi.App` にも捕捉箇所が無く、`ClaimPreparationViewModel` の `PreviewAsync` / `CloseAsync` の `when (IsHandledClaimException(ex))` フィルタも当時これを受け付けなかった。`AsyncRelayCommand` は `FlowExceptionsToTaskScheduler` 無しで生成され、`App.axaml.cs` にグローバルハンドラも無いため、**エラー表示ではなくアプリの終了**になっていた（施設区分が未入力であることは利用者に一切伝わらない）。

**最終レビューの修正（C1）でこれを是正した。** `IsHandledClaimException` に `ServiceCodeResolutionException` を加え、`MapError` が `FacilityClassificationUnresolved` を「施設区分が未入力です。事業所請求設定で施設区分を入力してから、もう一度実行してください。」という固定文言（氏名・受給者証番号を含まない）へ写像する。ADR 0047 が「施設区分を readiness の不足項目にしない」と決めているため、入力すべき欄を名指しできるのはこの境界だけである。証拠: `ClaimPreparationViewModelTests.PreviewAsync_maps_an_unresolved_facility_classification_to_a_message_naming_the_field` / `..._CloseAsync_maps_an_unresolved_facility_classification_instead_of_terminating`。

- **指定障害者支援施設に限らない。** `general`（非施設）も有効な解決可能値であり、fail-close するのは**未入力**のときだけである。
- **`docs/phase3-5-acceptance.md:90` に記録した Phase 3-5 の同種の帰結より広い。** ADR 0047 のfail-closeは 2026-06 以降＝これからの確定にしか及ばなかった。本スライスは **2024-06 まで遡る**ため、**旧挙動（無音の過少請求）で既に確定済みの過去月について訂正や再確定を開こうとした時点で例外に当たる**。
- **回避不可能である。** 通常行に非施設条件を付けずに施設行だけを足す「片側投入」は、施設事業所が通常行と施設行の**両方**に一致して処遇改善加算が**二重計上**になる（`ResolveAdditions` は複数一致をそのまま返す設計。ADR 0047／0048 選択肢A）。したがって選択肢は「無音の過少請求を続ける」か「未入力を fail-close する」の2つしかない。
- **意図した取引である。** Global Constraints「確定できない場合はfail-close側へ倒す」に従い後者を採った。**運用上は、2026-05以前の月を扱う可能性があるすべての事業所について、施設区分を先に入力しておく必要がある。**

確定済み請求そのものは変わらない（ADR 0026・0029 が確定時点の入力・規則・版・出典を不変に保持する）。影響を受けるのは未確定のプレビューと、これから行う再確定である。

### 3-2. Task 4 は spec §5 より広い変更になった（公式キーの入力経路が存在しなかった）

spec §5 は「(Ⅴ)区分の14択 ComboBox を1つ追加する」としていたが、実装着手時点で **`mhlw.b46.capability.treatment-improvement.{n}`（処遇改善の対象区分そのもの）を書く入力経路がどこにも無かった**ことが計画段階で判明していた。`OfficeCapabilityViewModel` は旧暫定キー `mealProvision` / `transportSupport` しか書いておらず、(Ⅴ)区分だけを追加しても option 6 が立たないため(Ⅴ)は永久に解決しない。したがって Task 4 は ComboBox を **2つ**（処遇改善 対象区分・(Ⅴ)区分）追加した。これは Task 4 の前提として brief に明記された既知の拡大である。

### 3-3. Task 5 は spec §6.1 の第2の副条件を実装していない

spec §6.1 の条件2は「処理対象年月に有効な条件定義が無い、**または**有効な条件定義はあるがそれを参照するサービスコード行が処理対象年月に無い」の2つを含むが、実装（`FindUncoveredKeys`）は**前者だけ**である。

現行seedでは差が出ない。全32件の `office-capability` 条件定義について、条件定義が有効でありながらそれを参照する行が1件も有効でない月は **0件**であることを本証跡作成時に機械的に確認した。ADR 0049「影響」節に限界として記録し、拡張方法（`monthConditionValues` の組み立てを狭める。判定関数は変更不要）も併記した。

**この「0件」は最終レビューの修正（item 6）で常設アサーション化した**（§6-4）。判定関数は依然として未拡張だが、前提が崩れた瞬間に CI が赤になる。

### 3-4. Task 2 は spec のファイル一覧に無い production コードを変更した（validator 2箇所）

(Ⅴ)の二重ゲート（option 6 ＋ band）は、既存の `ClaimMasterFileValidator` では **load 時に fail-close して成立しなかった**。いずれも本スライス以前は一度も行使されていなかった実装上のギャップであり、seed データの誤りではない。詳細は ADR 0048「影響」§3。

1. `ValidateConditionIntersection` が `ClaimConditionKind` だけでグループ化しており、`OfficeCapability`（`HashSet<string>` で複数の one-hot を同時に持つ。ADR 0021）で誤って「交差が空」と判定していた。`Kind + token-family` へ精密化した。
2. 1 が意味を持つには token 形状が固定されている必要があるため、`ValidateConditionToken` の `OfficeCapability` 分岐を接頭辞一致から **ちょうど5セグメント**の強制へ強化した。既存30トークンすべてが適合することを確認済み。

レビュー（Task 2 fix round）でこの2点それぞれに直接の回帰テストを追加した（§1 の #25・#26）。

### 3-5. Task 2 は literal guard の allowlist を誤用し、レビューで機構ごと差し替えた

`calculationOrder` の値域を 1〜7 から 1〜30 へ広げたことで、Domain/Application の無関係な数値リテラル **34件**が `ExternalSpecificationLiteralGuard` に誤検知された。初期実装は `KnownCoincidentalLiteralMatches` allowlist へ34件を追加したが、レビューが「同ファイルには目的に合う別機構（`officialOptionCode` の祖先スコープ付き property skip）が既にある」と根拠づけて指摘し、**34件の allowlist をすべて撤去して scanner 側の skip へ差し替えた**。

副次的に、レビューが疑っていた `ClaimCalculationMasters.cs:264`/`:267`（基準該当B型の法定除数 22/23）についても、scanner skip 後は**例外を1件も再追加せずに**緑になることを実測で確認した（`calculationOrder` をカタログから外したことで、衝突源そのものが消えた）。

**将来 `calculationOrder` を大量に増やすseed追加は同種の衝突を起こしうる。** 衝突面は seed 自身の整合性ではなくコードベース全体の数値リテラル語彙の大きさに比例する。

### 3-6. Task 3 は層跨ぎ参照を作りかけ、レビューで撤回した

`Application.Tests` から `Tsumugi.Infrastructure.csproj` への `ProjectReference` を追加して実seedベースのテストを書いた。`ArchitectureTests.Application_does_not_reference_outer_layers` は **production の `Tsumugi.Application.dll` の直接参照しか走査しない**ため緑のままだったが、これは「ガードが検出しない」ことを示すだけで「その参照が正しい」ことは示さない。レビュー指摘により参照を撤去し、実seed依存の5テストを既存規約どおり `tests/Tsumugi.Infrastructure.Tests/Claim/QueryClaimBillingTokenOptionsProductionWiringTests.cs` へ移設した（`Infrastructure.Tests` が唯一の正当な例外であり、既に `ClaimPreviewProductionWiringTests` 等の前例がある）。合成 fake ベースの1件は `Application.Tests` に残置した。

### 3-7. Task 4 の `try`/`catch` 存置（レビュー指摘への部分的な不同意を裁定で採用）

レビューは「`Discard()` の既定値を正しい月へ直せば例外経路そのものが消えるので `try`/`catch` は不要」と指摘した。実装者はこれを検証したうえで**部分的に反論し、裁定で存置が採用された**。

**根拠**: `OfficeCapabilityView.axaml` の `PeriodStart` は `TextBox` であり、`DateOnlyConverter.ConvertBack` は `yyyy-MM-dd` として解釈できる文字列を**年範囲を検査せずに**通す（`1899-01-01` や `2500-01-01` が通る。真に解釈不能な文字列だけを `BindingNotification` で拒否する）。Avalonia の `TextBox.TextProperty` は既定で TwoWay バインドするため、**範囲外の年がそのまま `PeriodStart` へコミットされ、`OnPeriodStartChanged` → `ReloadCapabilityOptions()` → `new ServiceMonth(...)` へ到達する**。この経路は `Discard()` とは**独立**であり、`Discard()` の既定値を直しても閉じない。再レビュアーが Avalonia 11.3.18 のメタデータをリフレクションで確認して裏づけた。

`Discard()` 側の根本原因（`PeriodStart = default;` ＝ 年1）は別途修正し、`DefaultPeriodStart` を単一の真実の源にした。存置した `try`/`catch` にはリグレッションテスト（§1 の #19）を付けた。

### 3-8. 【単体テストで検証できない欠陥】選択保持のロジックが実UIでは死んでいた

**現象**: `ReloadCapabilityOptions()` は `ObservableCollection` を `Clear()` してから再 `Add()` する。この `Clear()` は Reset 通知を上げ、バインドされた Avalonia の `SelectingItemsControl` は自分の `SelectedItem` をクリアする。`SelectedItem` は TwoWay バインドなので、その `null` が **ViewModel の `TreatmentImprovementOption` / `TreatmentImprovementVBand` へ書き戻される**。しかもこれは「現在値がまだ有効なら保持する」という旧コードの判定が走る**前**に起きるため、判定は既に null にされた値を読んでいた。**選択保持のロジックは実UIでは完全に死んでいた。**

**修正**: `Clear()` の**前**に現在値をローカル変数へ退避し、コレクション再充填の**後**に新語彙と突き合わせて明示的に再代入する。これによりAvaloniaのReset由来の null 書き戻しの後で正しい値が復元される。

**検証できないこと**: headless の ViewModel テストにはバインドされた View が無く `CollectionChanged` に反応する `SelectingItemsControl` も存在しないため、**この修正の効果は VM テストでは証明できない**（修正前後で headless の挙動は同一。レビュー自身がこれを明言している）。実機で ComboBox が `PeriodStart` 編集をまたいで選択を保持することの確認は、**§6-3 の GUI 手動貫通確認の対象項目として残す**。

### 3-9. 歯の確認は「自己整合した複数フィールドの違反」を組まないと機能しない

Task 1 の学びであり、Task 2 でも再現した。単一フィールドの意図的違反は `ClaimMasterFileValidator` の構造検査（`officialLabel` 整合・条件期間の被覆・`calculationOrder` の連続性・source authority の一意性）が**先に**弾いてしまい、狙ったテストのアサーションまで到達しない。

例（Task 2 teeth-check #2）: `v-7.facility` を1行削除するだけでは `calculationOrder` に欠番ができて `ValidateCalculationOrder` が `InvalidDataException` を投げる。5つの兄弟エントリの `calculationOrder` を 26→25／27→26／28→27／29→28／30→29 と繰り上げて初めてテストのアサーションへ到達する。

例（Task 1 fix round Important 1）: R8-06 施設行の `effectiveFrom` を1つ早めるだけでは、行によって `ValidateServiceIdentity`（同一コードに世代違いの `officialLabel`）または `ValidateConditions`（条件が行の期間を覆わない）が先に弾く。完全に自己整合した状態を作るには**5つのフィールドを協調して**変更する必要があった。

**副産物として本当の穴が1つ見つかった**（§4-2）。

---

## 4. 実装中に発見したこと

### 4-1. 465138・465140・465141 は世代をまたいで共有されるサービスコードになった

R6-06（2024-06〜2026-05、本スライス）と R8-06（2026-06〜、ADR 0047）が**同一のサービスコード**を使う。世代境界で率だけが変わる（(Ⅰ)施設で 0.104 → 0.116）。行キー（`b-addition.r6-06.…` / `b-addition.r8-06.…`）は世代ごとに別である。

### 4-2. 上記の帰結として、既存の世代境界テストが歯を失っていた

`ClaimAdditionSeedScopeTests.R8_treatment_improvement_rows_apply_only_from_2026_06` は「2026-05 のコード集合と 2026-06 のコード集合の**差分**」で世代境界を検査していた。465138/465140/465141 が世代をまたいで連続するようになったため、**R8-06 の行の `effectiveFrom` が誤って早まっても 2026-05 のコード集合が変わらず、この差分検査は何も検出しなくなった**。

修正: 差分の期待値を `{465174, 465175, 465176}` へ狭めたうえで、**行キー**（世代間で共有されない）による検査「2026-05 のマスタに `b-addition.r8-06.treatment-improvement.*` キーが1件も無いこと」を追加した。

歯の確認では、**完全に自己整合した違反を作ったうえで**「旧来の差分検査は緑のまま通過し、新しいキー検査だけが赤くなる」ことを実測した（レビュアーの予測どおり）。ただし実装者は、共有される3コードに限れば**この形の欠陥はランタイムに到達する前に3つの既存構造検査が既に捕まえる**ことも同時に確認しており、新しい検査が唯一の防御になるのは非共有コード（465176、(Ⅰ)ロ施設。R6に対応行が無い）の場合であると正直に報告している。

### 4-3. `r6-fee.pdf` 物理235〜236頁は新旧対照の2欄組であり、右欄を読むと別の値が並ぶ

Task 0 が発見し、後続タスクへの注意として台帳へ記録した。左欄＝改正後（R6-06。本スライスが投入する値）、右欄＝改正前（「令和６年５月31日までの間」の旧3加算構造。(Ⅰ)は54/64、(Ⅱ)は40/47 など**同じ区分ラベルに別の数値**）。機械的な正規表現一致だけで転記すると誤った欄を拾う。投入した26行の locator はすべて「左欄改正後」を明記している。

### 4-4. brief の C# コードは3件のコンパイル/ゲート不適合を含んでいた

いずれも「意味は変えずに実型・実ゲートへ合わせる」判断で修正し、報告済み。

- Task 1: `ServiceCodeResolutionException` のプロパティは `ErrorCode` ではなく `Code`（既存の全使用箇所で確認）。
- Task 3: 戻り値を `IReadOnlyList<int>` と宣言しつつ `.ToArray()` を返すと **CA1859**（`TreatWarningsAsErrors` によりエラー）。`int[]` 宣言へ変更。
- Task 3: `int.TryParse` ＋ `int.Parse` の二度読みが `CultureExplicitnessGuardTests`（ハード制約6）に抵触。`NumberStyles.None` ＋ `CultureInfo.InvariantCulture` の1行へ集約。

### 4-5. `ClaimBillingTokenOptionsDto` の構築箇所はリポジトリ全体で2つだけだった

いずれも `QueryClaimBillingTokenOptionsUseCase.cs` 内（catch 分岐と最終 return）。位置引数の取り違えリスクは実際には無かったが、grep で確認したうえで報告している。

### 4-6. 月フィルタは既存の Infrastructure 側で完結していた

`JsonClaimMasterProvider.ResolveCalculationMasters` が `ConditionDefinitions` を `ServiceMonth` で先にフィルタするため、「(Ⅴ)が2025-03で消える」「R8 に v-band が存在しない」という月依存の挙動は Task 3 の `CapabilityOptionCodes`（月非依存）に何も足さずに成立した。

---

## 5. 歯の確認の一覧

| Task | 手法 | 対象テスト | 結果 |
|---|---|---|---|
| Task 1 (Step 9-1) | `unified.i.facility` の率 `0.104`→`0.105`（2ファイル） | `ClaimMasterR6FacilityTests`（Task 1 時点の7件） | **GREENのまま**（Task 1 時点では率を pin するテストが無いことを実証。Task 2 が担保） |
| Task 1 (Step 9-2) | `unified.i` から `facility-classification-general-r6-06` を除去 | `Facility_variants_resolve_to_exactly_one_row_per_classification`（option 2） | RED（2行への多重一致＝二重計上を検出。実出力あり） |
| Task 1 (Step 9-3) | `unified.i.facility`（465138）のエントリを削除 | 同上 | RED（0行一致。実出力あり） |
| Task 1 (Fix I-1) | R8-06 `iii.facility` の `effectiveFrom` を 2026-05 へ（**5フィールド協調**で自己整合化） | 旧来の `Except` ベース差分検査 | **GREENのまま**（歯を失っていたことを実証。§4-2） |
| Task 1 (Fix I-1) | 同じ違反を維持 | 新設のキーベース検査 | RED（実出力あり） |
| Task 1 (Fix I-1) | 非共有コード 465176 の同種違反（協調不要） | 旧来の `Except` ベース差分検査 | RED（非共有コードには旧検査も効くことを実証） |
| Task 1 (Fix I-2) | `unified.i.facility` の `effectiveFrom` を 2024-04 へ（**4フィールド協調**） | 新設の下限境界検査 | RED。直前行の通常コード検査は**GREENのまま**（施設コードだけが漏れたことを実証） |
| Task 2 (Step 9-1) | `v-1` の率 `0.080`→`0.081`（2ファイル） | `R6_treatment_improvement_percentages_match_the_notice` | RED（実出力あり） |
| Task 2 (Step 9-2) | `v-7.facility` を削除＋兄弟5件の `calculationOrder` 繰り上げ | `Category_v_facility_variants_resolve_for_a_facility_office(7)` ／ 30コード集合一致（下限） | RED（両方。実出力あり） |
| Task 2 (Step 9-3) | 実在しない `v-3.facility`（465144）を捏造して追加 | 30コード集合一致（上限） | RED（実出力あり。`Category_v_subdivisions_without_a_facility_variant_resolve_for_both(3)` も正当な副作用でRED） |
| Task 2 (Step 9-4) | `v-1` の `effectiveTo` を 2026-05 へ（**共有条件定義2件も協調**） | `Category_v_expires_at_the_end_of_march_2025` | RED（1件のみ。43/44は緑。実出力あり） |
| Task 2 (Step 9-5) | `v-3` へ `facility-classification-general-r6-06` を誤付与（単一フィールド） | `Category_v_subdivisions_without_a_facility_variant_resolve_for_both(3)` | RED（実出力あり） |
| Task 2 (Fix I-3) | `ConditionIntersectionGroupKey` の本体を `return definition.Key;` へ（検査を無効化） | `Load_rejects_empty_office_capability_intersections_within_the_same_field` | RED（実出力あり。**再レビュアーが独立に再現し失敗文字列の完全一致を確認**） |
| Task 4 (Critical 1) | 修正前の `Discard()`（`PeriodStart = default;`）に対して新テストを実行 | `DiscardCommand_resets_state_to_the_default_period_without_throwing` | RED（例外ではなく状態不整合で。`Expected <2026-04-01> but found <0001-01-01>`） |
| Task 4 (Important 4) | band 書き込み条件へ `&& TreatmentImprovementOption == 6` を一時的に再導入 | `SaveAsync_writes_the_band_key_regardless_of_the_selected_option_number` | RED（実出力あり） |
| Task 4 (Important 5) | 修正前の無条件 option 書き込みに対して新テストを実行 | `SaveAsync_does_not_write_an_out_of_vocabulary_option_key` | RED（`…treatment-improvement.99` が書かれた。実出力あり） |
| Task 5 (Step 10-1) | `all.Contains(key) &&` を除去（偽陽性回避の半分を無効化） | `A_key_never_used_by_any_condition_is_ignored` | RED（1/4。実出力あり） |
| Task 5 (Step 10-2) | `!month.Contains(key)` を反転 | 3件（brief の予測は2件） | RED（3/4。**brief の表が不正確**だったことを報告。順序テストも正当に巻き込まれる） |
| Task 5 (Step 10-3) | `.OrderBy(...)` を除去 | `The_result_is_ordered_deterministically` | RED（1/4。実出力あり） |
| Task 5 (Step 10-4) | pipeline の警告組み立てを `[]` へ固定 | Application 層のテスト | RED。ただし Infrastructure の結線テストは**GREENのまま**（同テストは pipeline を通らないため。**brief の表が不正確**だったことを報告） |
| Task 5 (Fix I-1) | `ExtractCapabilityValues` の `Kind == OfficeCapability` を `== Staffing` へ | `Execute_does_not_warn_when_the_declared_capability_is_covered_this_month` | RED（1/20。**既存2件は緑のまま**＝それらが弱かったというレビュー指摘を実測で再現） |
| Task 5 (Fix I-2) | not-ready 分岐の警告引数を落とす（修正前の defect を再現） | `Execute_still_surfaces_capability_coverage_warnings_when_not_ready_for_an_unrelated_reason` | RED（実出力あり） |
| Task 5 (Item 4) | `PreviewAsync` から `Replace(CapabilityCoverageWarnings, ...)` を除去 | `PreviewAsync_surfaces_capability_coverage_warnings_without_blocking_readiness` | RED（実出力あり） |

いずれも確認後にバックアップ（`git checkout --` またはMD5照合済みの `cp`）で復元し、`git diff --stat` が空であることを確認済み（各 task report に記録）。

**Task 4 の Important 6（選択保持が実UIで死んでいた件）だけは歯の確認ができない**（§3-8）。headless の VM テストでは修正前後の挙動が同一であり、実機でしか観測できない。

---

## 6. 残課題

### 6-1. 旧暫定体制届キー（`mealProvision` / `transportSupport`）が公式キーへ移行していない

`OfficeCapabilityViewModel.SaveAsync` は `["mealProvision"]` / `["transportSupport"]` を書き続けており、`OfficeCapabilityView.axaml` にも CheckBox が残っている。しかし**どの条件定義からもこれらのキーは参照されておらず、算定に一切効かない**。ADR 0021 が「暫定キーを請求コードへ推測変換しない」と定めた状態のまま、公式キー（送迎体制・食事提供体制それぞれの `mhlw.b46.capability.*`）への移行が行われていない。

さらに Task 2 の `ValidateConditionToken` 強化（ちょうど5セグメント）により、**これらの旧キーは条件トークンとしては構造的に受け付けられなくなった**（`ClaimMasterFileValidator.cs:673` のコメントが明記）。移行は、送迎体制加算・食事提供体制加算それぞれのマスタ投入と**同時に**行う必要がある。`docs/open-questions.md` へ起票済み。

なお ADR 0049 の存在検査は、これらのキーを「どの期間の条件定義からも参照されない」ものとして**意図的に無視する**（偽陽性の回避。決定1）。移行が済むまで毎月の警告ノイズにはならない。

### 6-2. (Ⅴ)区分と処遇改善対象optionの組合せ — 2方向のうち有害な向きは最終レビューで塞いだ

**本節は当初「算定額には影響しない」と一括りに書いていたが、それは片方の向きにしか当てはまらなかった。**

**無害な向き（band だけがあって option 6 が無い）**: マスタ側の二重ゲート（ADR 0048 決定4）により、option 6 が立っていなければ(Ⅴ)行は一致しない。よって算定額に影響しない。2025-04 以降は band キー自体が ADR 0049 の警告対象になるため、失効後は存在検査が可視化する。**現在も弾かない**（spec 決定5・非スコープのまま）。

**有害な向き（option 6 だけがあって band が無い）**: (Ⅴ)行は option 6 と band の**両方**を条件に要求するため、band が無いと 2024-06〜2025-03 のどの(Ⅴ)行にも一致せず、**加算が無音で0円になる**。しかも ADR 0049 の存在検査は警告しない（`…treatment-improvement.6` は当該月に**有効**なので `!month.Contains(key)` が成立しない）。本ブランチが追加した入力画面（Task 4 の ComboBox 2つ）は、(Ⅴ)の ComboBox が表示される月ではまさにこの状態を保存可能にしていた。

**最終レビューの修正（I1）で塞いだ。** `QueryClaimBillingTokenOptionsUseCase` が当月の service-code 行を走査して「`treatment-improvement-v-band.*` 条件を同じ行で要求している選択番号」を `TreatmentImprovementOptionsRequiringVBand` として返し、`OfficeCapabilityViewModel.SaveAsync` はその集合に属する選択番号を band 未選択で保存しようとしたとき保存エラーを返して**1件も永続化しない**。どの選択番号が(Ⅴ)かはコードに書かず常にマスタ行から導出する（ハード制約3）。実seedでは R6-06 で option 6 のみが該当し、2025-04 以降・R8-06 では該当ゼロであることを production wiring テストで固定した。

証拠: `OfficeCapabilityViewModelTests.SaveAsync_rejects_an_option_that_requires_a_v_band_when_no_band_is_selected` / `..._accepts_an_option_that_requires_a_v_band_when_the_band_is_selected` / `..._accepts_an_option_that_does_not_require_a_v_band_without_a_band`、`QueryClaimBillingTokenOptionsProductionWiringTests.Only_category_v_requires_a_band_in_the_r6_generation` / `..._No_option_requires_a_band_once_category_v_is_gone`。

**`OfficeClaimProfile` 側の施設区分と体制届optionの組合せ検証**は依然として未実施であり（Phase 3-5 が一次資料の再確認を要するとして非スコープにしたもの）、`docs/phase3-5-acceptance.md` §8-2 の既存課題へ合流させる。

### 6-3. GUI 手動貫通確認が Phase 1 から未実施のまま

`docs/open-questions.md` の「Avalonia GUI 目視確認 (AC1-8 補完)」項目が示すとおり、実機起動でのフォント拡大追従・Reduce Motion・タブ順・フォーカス移動は手動QAでしか確認できない。Phase 3-6 は `OfficeCapabilityView` へ ComboBox 2個、`ClaimPreparationView` へ警告表示ブロック1個を追加した。

**本スライスで新たに手動確認の対象として明示的に追加されたもの**（§3-8）:

> `OfficeCapabilityView` の処遇改善区分 ComboBox・(Ⅴ)区分 ComboBox が、`PeriodStart` を編集して語彙が入れ替わっても**選択を保持する**こと。修正はコードに入っているが、headless の ViewModel テストでは原理的に検証できない（Avalonia の `SelectingItemsControl` が Reset 通知に反応して `SelectedItem` を null 化し、TwoWay バインドがそれを書き戻す挙動が再現しないため）。

### 6-4. spec §6.1 の第2の副条件 — 判定関数は未拡張だが、前提は機械判定になった

§3-3 に記載のとおり `FindUncoveredKeys` は第1の副条件だけを見る。現行seedで差が出ないことは機械的に確認済みだったが、**それが崩れたときに何もfail-closeしない**状態が残っていた（本ブランチが世代境界テストの歯の喪失を見つけた §4-2 と同じ類型）。

**最終レビューの修正（item 6）で常設アサーションへ格上げした**: `ClaimMasterCapabilityCoverageTests.Every_effective_office_capability_condition_is_referenced_by_an_effective_service_code_row` が 2024〜2030 の全月について production seed を走査し、有効な `office-capability` 条件定義がどの service-code 行からも参照されない月があれば赤にする。走査が空振りしていないこと（月数・条件件数が非ゼロ）も同テストが固定し、判定ロジックの歯は `..._The_check_detects_a_capability_condition_that_no_service_code_row_references` が合成データで実証する。判定関数の拡張自体は不要なまま（拡張が必要になった瞬間をこのテストが知らせる）。

### 6-5. 台帳の `minor (deferred)` 一覧

`progress.md` の `minor (deferred)` 行は**13件**（`grep -c "minor (deferred)"` で実測。台帳の21・22・23・24・42・43・44・61・62・63・74・75・82行目）。うち1件（75行目「ADR 0049 は未作成（Task 6 で作る。0048 も同様）」）は本タスクで解消したため除外し、**残る12件を下記に列挙する**（列挙数は12で、除外後の件数と一致する）。

**最終レビューの修正ウェーブで、このうち2件（Task 4 の「option が null のときのテストが無い」と、Task 4 の `ViewInputWiringTests` の `AutomationProperties` 未検査）を解消した**（打ち消し線・注記で下記に反映）。残りは実害が無いか、より広い課題へ合流するものとして繰り延べたままである。

- **Task 1**: `An_unresolved_facility_classification_fails_closed` が option 2 の1ケースのみ（R8側の同種テストは Theory 4件）。option 4・5 の未入力fail-closeは同一コードパスであり実害は無い。
- **Task 1**: locator の項番表記が `additions.json` と `service-codes.json` で不統一（一方は「第14の17 イ」、他方は「物理236頁 ハ」のように前置が異なる）。指す位置は同一。
- **Task 1**: 3つの新率を pin するテストが Task 1 コミット時点で存在しなかった（Task 2 が担保。歯の確認 Step 9-1 がこれを実証している）。
- **Task 1**: R6側の token provider 経由 end-to-end テストが無い（R8側の `Facility_variants_resolve_end_to_end_through_the_production_token_provider` が結線を担保しており、R6/R8で結線コードは共通）。
- **Task 2**: 歯の確認1件目（率）の失敗メッセージに `because` アンカーが無い（他4件は検証済み）。
- **Task 2**: `decimal.Equals` は scale を無視するため、率を `"0.08"` と書いても `0.080` のテストが通る（末尾ゼロの表記ゆれは未検査）。
- **Task 2**: `ConditionIntersectionGroupKey` が `Values[0]` を見るため、`in` 演算子の operand が来ると family の決定が恣意的になりうる。現行seedに該当なし。
- **Task 4**: 体制届キーの接頭辞（`mhlw.b46.capability.…`）が書き側（ViewModel）と読み側（use case）で重複しており、共有定数が無い。**リテラルは合計4箇所**（`OfficeCapabilityViewModel.cs` の option 側・band 側の2箇所と、`QueryClaimBillingTokenOptionsUseCase` の2つの接頭辞定数）。当初この証跡は「2箇所」と書いていたが、読み側の2件を数え落としていた。共有定数化には層跨ぎの定数置き場が要るため、繰延を維持する。
- ~~**Task 4**: option が null のとき `treatment-improvement.*` を1件も書かないことの直接テストが無い。~~ → **最終レビューで解消**（`OfficeCapabilityViewModelTests.SaveAsync_writes_no_option_key_when_no_option_is_selected`）。請求に効く one-hot 不変条件であり、同メソッドで実欠陥（語彙外optionの無条件書き込み）が過去に見つかっているため昇格した。
- **Task 4**: 新規2コントロール以外の同 View の既存の兄弟コントロールは `AutomationProperties.Name` 未設定（本スライスの追加分ではないため繰延）。**`ViewInputWiringTests` の未検査は最終レビューで解消**し、新規2 ComboBox の `AutomationProperties.Name` を検査対象に含めた（ハード制約5）。
- **Task 5**: `Application` 層の「無視する」テストは `all` が空であるために通っており、テスト名が主張するほど強くない（Domain 層の `OfficeCapabilityCoveragePolicyTests.A_key_never_used_by_any_condition_is_ignored` が本来の主張を担保している）。§1 の行22/22b をこの実態に合わせて訂正済み。
- **Task 5**: `QueryClaimBillingTokenOptionsUseCase.CapabilityOptionCodes` に4つ目の類似 operand 抽出が残る（下流の形が異なり——接頭辞フィルタ＋int パース——coverage 計算を汚染しないため、レビューが名指しした3箇所の統合からは意図的に外した。将来の整理候補）。

---

## 6-A. 最終レビュー（ブランチ全体）の修正ウェーブ

マージ前の最終レビューで7件を適用した。**seed JSON は一切変更していない**（コードと文書のみ）。

| 項目 | 重大度 | 内容 | 反映先 |
|---|---|---|---|
| C1 | Critical | 施設区分未入力の `ServiceCodeResolutionException` が `ClaimPreparationViewModel` の例外フィルタで受けられず、`AsyncRelayCommand` から**アプリの終了**になっていた。`IsHandledClaimException` に追加し、`FacilityClassificationUnresolved` を欄名入りの固定文言へ写像 | §3-1・ADR 0048「影響」1・§1 行28 |
| I1 | Important | 処遇改善(Ⅴ)を band 未選択で宣言でき、**無音で0円**になった（存在検査も警告しない）。「band を要求する選択番号」をマスタ行から導出し、保存時に差し戻す | §6-2・ADR 0049・§1 行29〜30 |
| I2 | Important | 体制届画面の既定期間が `2026-04-01` のハードコードで、現在月（2026-07）の登録で 2026-06 施行の選択番号が選べなかった。DI の `TimeProvider` から当月初日を導く | §1 行31 |
| I3 | Important | 施設区分 fail-close の影響範囲が `conditionSelectors` の**配列順**でしか抑えられていなかった。`MatchesAll` を順序非依存にし、他条件がすべて一致した行でだけ表面化させる | ADR 0048 決定5・§1 行32 |
| 5 | minor→昇格 | option 未選択のとき option キーを書かないことの直接テスト。併せて `ViewInputWiringTests` に `AutomationProperties.Name` の検査を追加 | §6-5・§1 行34〜35 |
| 6 | — | spec §6.1 条件2 の前提（「該当0件」）を常設アサーション化 | §6-4・ADR 0049・§1 行33 |
| 7 | — | 本証跡と ADR 0048/0049 の3件の記述の訂正（下記） | §1 行22/22b・§3-1・§6-2・§6-5 |

**訂正した3件の記述**:

1. 「(Ⅴ)区分/option の不一致は**算定額には影響しない**」（§6-2・ADR 0049）→ **無害な向き（band のみ）にしか当てはまらない**。有害な向き（option 6 のみ）は無音で0円になり、しかも ADR 0049 の存在検査は警告しない。I1 で塞いだ。
2. §1 行22 が「偽陽性の不在」の証拠として `CalculateClaimUseCaseTests..._does_not_warn_about_a_declared_capability_never_referenced_by_any_condition` を挙げていたが、**§6-5 が同時に「そのテストは `all` が空だから通っている」と開示していた**（自己矛盾）。行22b を立て、Domain 層の `OfficeCapabilityCoveragePolicyTests.A_key_never_used_by_any_condition_is_ignored` を証拠として明示し、Application 層のテストは証拠にならない旨を書いた。
3. 「呼び出し元での**未捕捉例外として表面化する**」（§3-1・ADR 0048「影響」1）→ 字義どおりではあるが「エラーが表示される」と読めた。実際の挙動は**アプリの終了**であり、C1 が変えた後の挙動へ文言を合わせた。

加えて §6-5 の繰延項目の件数を実測へ訂正した（体制届キー接頭辞のリテラルは **4箇所**。ViewModel の2箇所と use case の2つの接頭辞定数。当初「2箇所」と書いて読み側を数え落としていた）。

---

## 7. `docs/open-questions.md` の処理

本タスクで4項目をクローズし、1項目を新規に起票した。

| 項目 | 処理 |
|---|---|
| [Phase3-5 最終レビュー由来] R6-06世代の処遇改善に施設区分の別立てが無い | **クローズ**（ADR 0048。施設variant3区分を投入し通常3行へ非施設条件を付与した） |
| [Phase3-4/Task2 follow-up] R8処遇改善(Ⅴ)の実値投入 | **クローズ**。ただし**解除条件を満たしたのではなく、項目の前提が誤りだった**。(Ⅴ)はR6-06世代の経過措置（2024-06〜2025-03）であり、R8-06には存在しない。R6分を投入し、R8非存在を3つの根拠で確定してクローズした（ADR 0048） |
| [Phase3-4/Task2 follow-up] 体制届optionに対応するマスタ行が当月に存在しない場合のreadiness警告 | **クローズ**（ADR 0049） |
| [Phase3-1/Task 9] 利用定員・人員配置区分の実データ源 | **クローズ**。**Phase 3-1 で実装済みであり記述が陳腐化していた**（新規実装ではない）。§7-1 参照 |
| 旧暫定体制届キー（`mealProvision` / `transportSupport`）が公式キーへ移行していない | **新規起票**（§6-1） |

### 7-1. 「利用定員・人員配置区分の実データ源」の陳腐化を実コードで確認した

当該項目は「現行エンティティ・migration・入力UIに未実装」「`OfficeClaimBillingTokenProvider` は両者を null で返し」と記述していたが、**本証跡作成時に実コードで確認したところ、いずれも事実に反していた**。

- `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs:38` — `public int? CapacityHeadcount { get; init; }`（`:35` のコメントが「ADR 0021が定める基本報酬選択の構造化入力（定員条件）」と明記）
- 同 `:45` — `public string? StaffingKey { get; init; }`
- `src/Tsumugi.Infrastructure/Migrations/20260718220223_Phase31OfficeClaimBillingTokens.cs` — `CapacityHeadcount` / `StaffingKey` / `RegionKey` の3列を追加し、Cancel レコード（`Kind = 3`）でこれら3列が NULL であることをチェック制約で強制している
- `src/Tsumugi.Infrastructure/ClaimMasters/OfficeClaimBillingTokenProvider.cs:87-88` — `CapacityHeadcount: profile?.CapacityHeadcount,` / `StaffingKey: profile?.StaffingKey,`（**null 固定ではなく profile から取っている**）
- `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs:456-458` — `CapacityHeadcount` / `StaffingKey` / `RegionKey` を保存経路へ渡している

なお項目本文の `OfficeClaimProfile.StaffingClass` というプロパティ名も実際には `StaffingKey` であり、この点でも記述は現行コードと一致していない。**本スライスは当該機能を新たに実装していない。** 記述が Phase 3-1 の実装に追随していなかっただけである。

---

## 8. `./build/ci.sh` 実行証跡

最終レビューの修正ウェーブ（§6-A）完了時点に 2026-07-27 実行、**全ゲート緑**（exit 0）。

```
==> restore
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!   -失敗:     0、合格:   704、スキップ:     0、合計:   704 - Tsumugi.Domain.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   472、スキップ:     0、合計:   472 - Tsumugi.Application.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   313、スキップ:     0、合計:   313 - Tsumugi.Infrastructure.Csv.Tests.dll (net10.0)
成功!   -失敗:     0、合格:    30、スキップ:     0、合計:    30 - Tsumugi.Infrastructure.Reporting.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   279、スキップ:     0、合計:   279 - Tsumugi.App.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   784、スキップ:     0、合計:   784 - Tsumugi.Infrastructure.Tests.dll (net10.0)
==> coverage threshold gate (gate #3 enforcement — floor=Domain 95% / Application 70%, raise Application in Phase 3)
Tsumugi.Domain      Line  95.3% / Branch 88.23% / Method 93.45%  (floor 95%)
Tsumugi.Application Line 91.72% / Branch 83.46% / Method 85.14%  (floor 70%)
==> CI OK
```

合計 **2,582テスト**。`dotnet format --verify-no-changes` は `./build/ci.sh` の gate #2 として同一実行で緑。

**注意（環境フレーク）**: 同日の実行のうち1回だけ、`Tsumugi.App.Tests` のテストホストプロセスが 248/279 の地点でクラッシュした（`テストのホスト プロセスがクラッシュしました`）。同一コミットで `./build/ci.sh` を続けて3回、`Tsumugi.App.Tests` 単体（Release ＋ coverage）を3回実行していずれも 279/279 緑であり、**再現しない**。失敗したテスト名は記録されず、特定のテストへ帰属できない。macOS 上の並列テストホスト＋coverage collector の既知の不安定性と判断し、環境要因として記録する。

各 Task 完了時点の全件数（各 task report の `dotnet test` 実測値）:

| 時点 | 合計 | 増分 |
|---|---:|---:|
| Task 1 完了（`81e0f46`） | 2,489 | — |
| Task 2 完了（`2a629dc`） | 2,533 | +44 |
| Task 3 完了（`12a11b8`） | 2,539 | +6（内訳: 移設5件は増減なし、新規6件） |
| Task 4 完了（`11fc53b`） | 2,549 | +10 |
| Task 5 完了（`fdd6f58`） | 2,565 | +16 |
| Task 6 完了（`d21af54`） | 2,565 | ±0（文書のみ） |
| 最終レビュー修正ウェーブ | 2,582 | +17 |

Task 3 の増分には、`Application.Tests` から `Infrastructure.Tests` へ移設した5件（アセンブリ間の移動であり総数は不変。§3-6）が含まれる。

### Domain カバレッジ床への接触（Task 5 fix round）

`OfficeCapabilityCoveragePolicy.ExtractCapabilityValues` を Domain へ追加した時点で直接テストが無く、Domain 行カバレッジが **94.99%** へ落ちて `./build/ci.sh` が1度失敗した（`The total line coverage is below the specified 95`）。`ExtractCapabilityValues` の直接テスト5件（kind フィルタ・token operand・token set operand・非 token operand の無視・null 拒否）を追加して 95.29% へ回復した。これらのテストは床合わせのためだけでなく、新設 Domain ロジックの直接単体カバレッジとしてそれ自体が必要なものである。

---

## 9. 追補（体制届宣言の充足可能性検査。ADR 0049 の一般化）

- ブランチ: `feature/capability-declaration-satisfiability`
- ブリーフ: `.superpowers/sdd/capability-satisfiability/brief.md`

Task 5 の存在検査（`FindUncoveredKeys`）は「宣言キー K が当月のどの条件定義にも無い」（失効・未施行）だけを拾い、「K は当月に生きているが、K を含む行がすべて追加の体制届キーを要求していて宣言集合では1行も成立しない」場合は無音のまま残っていた（実例: 処遇改善(Ⅴ)＝option 6 のみ宣言し `-v-band.{n}` を宣言していない事業所。option 6 自体は 2024-06〜2025-03 に有効なため `FindUncoveredKeys` は沈黙するが、(Ⅴ)行23件はすべて option 6 と band の両方を要求するため0行一致し、加算は無音の¥0になる）。

本追補は `OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys` を新設してこの穴を塞いだ。判定は**capability種別の条件だけ**を見る（`ExtractCapabilityValueSets`）。行には facility-classification 等の条件も同居するため、それらを混ぜると偽陽性になる（処遇改善(Ⅰ)行 465120/465138 は capability条件と facility-classification 条件を同じ行に持つ）。既存の `FindUncoveredKeys` とは排反（前者は「K が当月に無い」、本検査は「K が当月にある」が前提）であり、判定関数自体は変更していない。

DTOは既存の `CapabilityCoverageWarnings` と**並列の別リスト**（`IncompleteCapabilityDeclarationWarnings`）にした。同一リストへ混ぜると、運用者が「失効した option」と「宣言が不完全（companion option の過不足）」のどちらか判別できず、対処（体制届の見直し vs companion option の追加・削除）を誤る。**不完全になる原因は「不足しているキーがある」場合と「不要なキーが残っている」場合の両方があり得るため（§9-A参照）、UI見出しはどちらか一方の対処だけを指示しない中立な表現にした。**

検出位置は `ClaimPreviewPipeline`（プレビュー時のマスタ照合）に置き、Phase 3-6 の I1（`OfficeCapabilityViewModel.SaveAsync` の入口ガード）とは役割が異なる。入口ガードは**新規保存時**にしか効かず、`RegisterOfficeCapabilityUseCase` 自体はフラグ辞書を検証しないため、将来の取込機能・別の入力面・テスト用シード・DB直接操作は入口ガードを素通りする。本検査は**永続化済みの任意のレコード**（書き手・作成時期を問わない）に対して、確認のたびに再評価される。また入口ガードは `PeriodStart` の月の語彙しか見ないため、世代境界をまたぐ体制届は開始月しか検査されないが、本検査は処理対象月ごとに独立して評価する。

`IsReady` は変えない（Phase 3-6 と同じ非ブロッキング契約。`ClaimPreviewDto.IncompleteCapabilityDeclarationWarnings` が非空でも確定できる）。算定不成立の早期リターン（readiness不成立・経過措置guard不一致）でも警告を運ぶ（`CalculateClaimUseCaseTests.Execute_still_surfaces_incomplete_capability_declaration_warnings_when_not_ready_for_an_unrelated_reason`）。

**残る限界**: 本検査が拾うのは capability 起因の不成立だけである。`docs/open-questions.md` の「体制届optionに対応するマスタ行が当月に存在しない場合のreadiness警告」項目に追補したとおり、average-wage-band や capacity 起因で行が成立しない場合（条件は参照されているが参照先の行が他の理由で成立しない「第3の形」の残り）は依然として拾わない。また警告であるため、運用者が見落とせば加算が¥0のまま確定できる点は ADR 0049 決定2・「残る限界」節と同じ（本検査もブロックしない）。

### 証拠（テスト名）

| # | 内容 | 証拠 |
|---|---|---|
| 1 | Domain: 充足可能／companion欠落で不充足／当月に無いキーは対象外／`in` operand の代替値で充足／決定論的順序／companion単独宣言も報告／複数行中1つでも充足可能なら報告しない（rule 3のピン止め） | `OfficeCapabilityCoveragePolicyTests`（`FindUnsatisfiableDeclaredKeys` 10件＋`ExtractCapabilityValueSets` 5件） |
| 2 | Application: 警告する／companion併宣言で警告しない／無関係な理由の not-ready でも運ばれる | `CalculateClaimUseCaseTests.Execute_warns_about_an_incomplete_capability_declaration_missing_a_companion_key` / `..._does_not_warn_about_an_incomplete_capability_declaration_when_the_companion_key_is_also_declared` / `..._still_surfaces_incomplete_capability_declaration_warnings_when_not_ready_for_an_unrelated_reason` |
| 3 | Infrastructure（実seed。2024-06）: option6のみ宣言→警告／option6+band3→警告なし／option2のみ宣言→facility-classification条件混在でも偽陽性なし／band単独宣言（orphan band）→警告 | `CapabilityDeclarationSatisfiabilityProductionWiringTests`（4件） |
| 4 | UI: 別枠のリストとして到達し `IsReady` を落とさない | `ClaimPreparationViewModelTests.PreviewAsync_surfaces_incomplete_capability_declaration_warnings_without_blocking_readiness` |

### 9-A. 追加レビュー対応（orphan band 等、2026-07-27）

独立レビューで3件の指摘を受け、いずれも本ブランチのまま解消した。

| 項目 | 重大度 | 内容 | 反映先 |
|---|---|---|---|
| 1 | Important | `docs/decisions/0049-…md` と本ファイルが `.superpowers/sdd/capability-satisfiability/report.md`（gitignore対象・harnessが書き込みを拒否したため実在しない）を4箇所引用しており、うち1箇所（ADR §(2)）はkindフィルタのRED検証という最も根拠性の高い主張の唯一の証拠になっていた。RED時の実際の失敗文字列をADR §(2)へ直接引用し、4引用すべてを除去した | ADR §(2)・末尾、本節（report.md参照を削除） |
| 2 | Important | ADR:128「無害な向き（band だけがあって option 6 が無い）」の「本チェックでも入力側でも弾かない」が、本タスクの追加により**誤りになっていた**（band単独宣言は2024-06で実際に警告される）。加えて `OfficeCapabilityView.axaml` は band を要求しない選択番号のままband選択を保存できる（保存後もエラーにならない）ため、(Ⅴ)から他区分へ切り替えた事業所がorphanなband宣言を持ったまま10ヶ月警告を受け続けるという、実運用で到達可能な経路だった。UI見出しの文言（旧: 「他に必要なoptionが未選択」）も「追加」だけを指示しており、正しい対処が「削除」であるケースで誤った操作へ誘導しかねなかった | ADR:128訂正・追補(5)、`OfficeCapabilityViewModel.SaveAsync`（orphan band書き込みガード）、`ClaimPreparationView.axaml`（中立な見出しへ変更）、`ClaimPreparationViewModel.cs`のdocコメント |
| 3 | Minor | Domain契約が rule 3（「Kを含む行が複数あり、1つでも充足可能なら報告しない」）を単一行のテストでしかピン止めしておらず、`OfficeCapabilityCoveragePolicy.cs`の`row.Any(IsSatisfiable)`を`row.All(IsSatisfiable)`へ変異させても既存22件が全緑のまま通過した（実seedのwiringテストでしか検出できなかった） | `OfficeCapabilityCoveragePolicyTests.A_declared_key_with_one_unsatisfiable_and_one_satisfiable_row_is_not_reported`（2行構成でこの変異を直接RED化） |

**項目2の修正詳細**: `OfficeCapabilityViewModel.SaveAsync` の band 書き込みを、既存の I1 ガード（option側がband併宣言を要求するのにband未選択なら保存エラー）の**逆向き**に改めた——選択中の選択番号が `_optionsRequiringVBand`（`QueryClaimBillingTokenOptionsUseCase.TreatmentImprovementOptionsRequiringVBand`、マスタ行から導出。I1と同じデータ）に含まれない場合、band を選択していても band キー自体を書き込まない。保存エラーにはせず黙って落とす（option側の不足をエラーにする非対称性は、band側の不要分を落とすだけなら再入力コストが不釣り合いに高くないため意図的）。証拠: `OfficeCapabilityViewModelTests.SaveAsync_does_not_write_an_orphan_band_when_the_selected_option_does_not_require_it`（option=2固定でband単独保存を試みる） / `..._does_not_write_an_orphan_band_after_switching_away_from_the_option_that_required_it`（option 6→2 の実運用切替経路を再現。`ReloadCapabilityOptions`はoption変更単体ではband選択をクリアしないことも合わせて固定）。既存テスト`SaveAsync_writes_the_band_key_regardless_of_the_selected_option_number`（前提が今回の修正で偽になった）は上記2件へ置き換えた。`CapabilityDeclarationSatisfiabilityProductionWiringTests.Declaring_only_the_band_without_the_v_option_is_reported_as_an_incomplete_capability_declaration`が、この書き込みガードを経由しない既存データに対しても検査自体が実seedで機能し続けることを固定する。

各RED確認の失敗文字列は `docs/decisions/0049-office-capability-master-coverage-check.md` §(2)（kindフィルタ）と、以下（本節限定の3項目）に引用する。

- 項目2（orphan band、修正前の`SaveAsync`へ一時的に戻して確認）:
  `Expected SavedFlags.Keys ... to not have any items matching k.StartsWith("mhlw.b46.capability.treatment-improvement-v-band.", Ordinal), but found {"mhlw.b46.capability.treatment-improvement-v-band.3"}.`（2件とも同型）
- 項目2（Infrastructure、`FindUnsatisfiableDeclaredKeys`を一時的に常に空へ差し替えて確認）:
  `Expected dto.IncompleteCapabilityDeclarationWarnings to contain a single item, but the collection is empty.`
- 項目3（rule 3、`row.Any(IsSatisfiable)`→`row.All(IsSatisfiable)`へ変異させて確認）:
  `Expected result to be empty, but found at least one item {"mhlw.b46.capability.treatment-improvement.6"}.`（新設テスト1件だけがFAILし、既存23件はすべてPASSのまま——レビュー指摘どおり既存テストがこの変異に対して盲目であることも同時に確認した）

`./build/ci.sh` は本追補（review round含む）の変更を含めて全ゲート緑（2026-07-27実行、exit 0）。

```
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
    0 個の警告 / 0 エラー
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!   -失敗:     0、合格:   719、スキップ:     0、合計:   719 - Tsumugi.Domain.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   475、スキップ:     0、合計:   475 - Tsumugi.Application.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   313、スキップ:     0、合計:   313 - Tsumugi.Infrastructure.Csv.Tests.dll (net10.0)
成功!   -失敗:     0、合格:    30、スキップ:     0、合計:    30 - Tsumugi.Infrastructure.Reporting.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   281、スキップ:     0、合計:   281 - Tsumugi.App.Tests.dll (net10.0)
成功!   -失敗:     0、合格:   788、スキップ:     0、合計:   788 - Tsumugi.Infrastructure.Tests.dll (net10.0)
==> coverage threshold gate (gate #3 enforcement — floor=Domain 95% / Application 70%, raise Application in Phase 3)
Tsumugi.Domain      Line 95.34% / Branch 88.27% / Method 93.47%  (floor 95%)
Tsumugi.Application Line 91.77% / Branch 83.46% / Method 85.16%  (floor 70%)
==> CI OK
```

合計 **2,606テスト**（review round前の2,602から純増4件: Domain +2、App +1、Infrastructure +1。Application層は今回の指摘に新規テスト追加が無く増減0）。
