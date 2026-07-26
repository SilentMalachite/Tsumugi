# Phase 3-4（令和8年6月施行分 R8-06 制度実値投入）受け入れ証跡

- 対象: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` — R8-06（令和8年6月施行分）への制度実値投入
- spec: `docs/superpowers/specs/2026-07-26-phase3-4-r8-06-master-values-design.md`
- 計画: `docs/superpowers/plans/2026-07-26-phase3-4-r8-06-master-values.md`
- 実装ブランチ: `feature/phase3-4-r8-06-master-values`
- 実行台帳（本証跡の一次情報源）: `.superpowers/sdd/2026-07-26-phase3-4-r8-06-master-values/progress.md`
- Task別詳細報告: `task-1-report.md` / `task-2-report.md` / `task-3-report.md`（Task 3+4+5 統合）/ `task-6-report.md`（同ディレクトリ）

---

> **状態（2026-07-26）**: AC3-4-1〜4 の4項目を達成した。地域区分単価・負担上限額（ADR 0044）、
> 福祉・介護職員等処遇改善加算のR8実値6区分（ADR 0045）、R8改定対象の新12区分（条件トークン・
> 基本報酬180行・サービスコード180行、ADR 0046）を出典付きで投入し、2026-06以降の請求が
> 改定対象・改定対象外を問わず成立するようになった。処遇改善(Ⅴ)・障害者支援施設variant・
> `r8-reform-status-exempt`・option 8（filed-transition）対応の4点は一次資料から一意に確定できず
> 未投入のまま `docs/open-questions.md` に残る。コードと seed データは Task 1〜6 で完結しており、
> 本タスク（Task 7）は文書のみを更新した。

---

## 1. AC3-4-1〜4 の判定

| # | 受け入れ基準 | 判定 | 証跡（テスト名） |
|---|---|---|---|
| AC3-4-1 | `region-unit-prices` と `burden-caps` の各 entry が、2026-06 を含む適用期間について「R8 出典に裏付けられた値」または「明示的に閉じられた適用期間」のいずれかになっている。出典なしで2026-06に到達するentryが0件 | ✅ | `ClaimMasterR8ContinuityTests.Every_entry_reaching_june_2026_is_backed_by_an_applied_source`（`region-unit-prices.json` / `burden-caps.json` の2ファイルをTheoryで検査）/ `ClaimMasterR8BoundaryTests.Region_unit_prices_and_burden_caps_resolve_in_june_2026`（2026-05と2026-06の値が`BeEquivalentTo`で完全一致することを固定） |
| AC3-4-2 | 2026-06の`ResolveCalculationMasters`が処遇改善加算行を返し、`ClaimCalculatorGoldenCaseTests`のR8 worked exampleが一次資料由来の期待値と一致する | ✅ | `ClaimCalculatorGoldenCaseTests.Matches_adr_0045_worked_example_reform_exempt_office_in_june_2026` / `ClaimMasterR8BoundaryTests.R8_treatment_improvement_rows_apply_only_from_2026_06` / `..._percentages_match_adr_0045` / `ClaimAdditionSeedScopeTests.R8_treatment_improvement_rows_apply_only_from_2026_06` |
| AC3-4-3 | `ReformTarget`×option 11〜22の全12組合せについて、2026-06の`ResolveBasicReward`が例外を投げずにservice codeと単位数を返す。R6数値option（1〜7・9）を宣言したReformTarget profileは従来どおり登録段階で拒否され続ける | ✅ | `ClaimMasterR8BoundaryTests.Reform_target_offices_resolve_every_r8_numeric_band`（`[Theory]` 12ケース）/ `..._still_fail_closed_on_r6_numeric_bands`（`[Theory]` 8ケース、`OfficeClaimProfilePolicy.ValidateHistory`層）/ `..._option_11_resolves_to_the_service_code_and_units_from_adr_0046` / `ClaimMasterSeedPhase31Tests.R8_reform_target_basic_rewards_form_a_complete_product` / `..._basic_reward_rows_pair_with_their_service_code_rows` / `..._seeds_twelve_average_wage_bands_and_one_reform_status_condition` |
| AC3-4-4 | `./build/ci.sh`が緑。Domainカバレッジ≧95%。golden caseの期待値がすべてADRの決定表を参照している | ✅ | `ClaimCalculatorGoldenCaseTests.Matches_adr_0045_worked_example_reform_exempt_office_in_june_2026` / `..._adr_0046_worked_example_reform_target_office_in_june_2026`（いずれもXMLdocでADR節を参照）。本タスク実施後の`./build/ci.sh`実行結果は §9 参照 |

---

## 2. Task 1 が採った分岐: (a) 継続を確定

計画は地域区分単価・負担上限額のR8-06適用について3分岐（a: 継続確定／b: 部分確定／c: 出典不足でfail-close）を用意していた。**Task 1は最終的に分岐(a)を採用し、地域区分単価8件・負担上限額4件のすべてを`effectiveFrom: "2024-04"` / `effectiveTo: null`のまま2026-06以降も適用継続と確定した**（ADR 0044）。

この経緯は単純ではなく、実装者は当初、検査基準を「R8-06報酬改定資料束7件に載っているか」とする分岐(c)（region-unit-prices 8件・burden-caps 4件を`effectiveTo: "2026-05"`で閉じる）を暫定的に実施し、既存`ClaimCsvExportProductionWiringTests`5テストが2026-06以降の月でFAILする状態を一度作った（実装者は指示どおり既存テストを弱めずにこの状態を報告した）。その後、地域単価・負担上限額は報酬改定パッケージとは別の法令・通知系統（地域単価は厚生労働省告示第539号系統、負担上限額は障害者総合支援法施行令・こども家庭庁通知系統）に属するため、報酬改定資料束の中を探しても原理的に表が存在しないことが判明した。利用者の裁定により、検査の問いを「R8-06改定資料束に載っているか」から「R8-06施行後に適用され続けることを直接確認できた出典があるか」へ改め、分岐(a)へ切り替えた。

**帰結**: 2026-06の請求は地域単価・負担上限額の両方が解決できる。分岐(c)を採らなかったため、**Task 6のStep 5では分岐(c)用の`..._fail_closed_...`テストを意図的に作成していない**（`Region_unit_prices_and_burden_caps_resolve_in_june_2026`のみを追加した）。この判断は`progress.md`の「Task 6 -> Task 7 carry-forward」に明記されており、本節がその記録の履行である。将来もし地域単価・負担上限額のいずれかが実際に改定される事態が生じた場合は、当該マスタを`effectiveTo`で閉じ、分岐(c)相当のfail-closeテストを新たに追加することになる。

---

## 3. 投入した行数の実績

| ファイル | 投入前 | 投入後 | 差分 | 内訳 |
|---|---:|---:|---:|---|
| `conditionDefinitions`（`service-codes.json`内） | 32 | 51 | **+19** | Task 2（ADR 0045）+6（`capability-treatment-improvement-r8-*`）／Task 3（ADR 0046）**+13**（`average-wage-band`12個＋`r8-reform-status-target`1個。当初計画の14個から`r8-reform-status-exempt`を除いた数） |
| `basic-rewards.json`（entries） | 135 | 315 | **+180** | Task 4（ADR 0046）。option 11〜22 × 定員5区分 × 人員配置3区分の完全直積180行 |
| `service-codes.json`（entries） | 151 | 337 | **+186** | Task 5（ADR 0046）R8基本報酬180行 ＋ Task 2（ADR 0045）R8処遇改善6行 |
| `additions.json`（entries） | 16 | 22 | **+6** | Task 2（ADR 0045）R8処遇改善加算(Ⅰ)イ/(Ⅰ)ロ/(Ⅱ)イ/(Ⅱ)ロ/(Ⅲ)/(Ⅳ) |

いずれも`git diff <base>..HEAD -- src/Tsumugi.Infrastructure/ClaimMasters/Seed/`とJSON中の`entries`/`conditionDefinitions`配列長の実測値で確認済み（本証跡作成時に再計測し、上表の数値と一致）。`region-unit-prices.json`・`burden-caps.json`は値を変更していない（`sourceRefs`の追記のみ、8件・4件のまま）。`sources.json`は新規documentId 2件（`mhlw-unit-price-notice-post-r8-observed-946c3d96` / `r8-burden-recognition-guide-202606`）を追加した。

---

## 4. 確定できず投入しなかった項目

| 項目 | 未投入の理由（要約） | 現在の挙動 |
|---|---|---|
| 福祉・介護職員等処遇改善加算(Ⅴ)（選択番号6・サブ区分⑴〜⒁） | 経過措置で率が14通りに枝分かれし、`r8-capability-correction`でのみ選択肢の存在が確認できる等、一意対応の確認に本タスクのスコープを超える追加調査を要する | 選択番号6を届け出た事業所は**警告なしに加算が0円になる**（無音の未算定） |
| 障害者支援施設variant（465138・465140・465141・465176） | 通常事業所と異なる率を持ち、`OfficeClaimProfile`側に「指定障害者支援施設か」の構造化入力が必要（ADR 0021が既に要求、未実装） | 施設が通常事業所向け条件に一致してしまい、**465120@0.105（本来は465138@0.116）で無音の過少請求が成立し得る** |
| `r8-reform-status-exempt`条件トークン | 参照先となるR6基本報酬135行は`conditionSelectors`にこの種別を一切持たず、後付けしようとすると「R6行を書き換えない」制約と`ClaimMasterFileValidator`の期間被覆検査の両方に抵触するため成立しない | 該当なし（R6の135行が制約なし一致として`reform-exempt`等の受け皿を正しく機能させている。`Exempt_offices_resolve_the_same_code_and_units_across_the_boundary`が固定） |
| option 8（filed-transition）に一致する`average-wage-band`条件 | `transition-rules.json`はoption 8を許可するが、対応する条件定義がR6・R8いずれのseedにも存在しない（一次資料で対応先が未確定） | `ServiceCodeResolver`が0行一致で`MasterUnavailable`によりfail-close（本コミット前後で挙動不変） |
| 体制届option 10（生産活動支援）と参加評価型（`band-participation`）の対応 | 本タスクのスコープ外（Phase3-1/Task 9で既に起票済みの既存未確定事項） | 変更なし（既存の未解決事項をそのまま参照） |

いずれも`docs/open-questions.md`へ個別の項目として起票済み（クローズ・起票の一覧は §7・`task-7-report.md`を参照）。

**追記（2026-07-26、Phase 3-5 / ADR 0047）**: 上表の障害者支援施設variant行はPhase 3-5で解消した。一次資料から一意に確定できた4区分（465138・465176・465140・465141）を投入し、施設区分を入力した事業所が正しい率・コードへ解決するようになった。詳細は[`docs/phase3-5-acceptance.md`](phase3-5-acceptance.md)。処遇改善(Ⅴ)・`r8-reform-status-exempt`・option 8は本追記時点でも未確定のまま。

---

## 5. spec / plan からの逸脱と理由

台帳（`progress.md`）に記録された5件を証跡として記す。

1. **Task 1 の許容出典 whitelist が誤っていた**: 計画は「R8-06報酬改定資料束に載っていること」を検査基準にする想定だったが、地域単価・負担上限額は報酬改定パッケージとは別の法令・通知系統に属するため、この基準では原理的に該当する出典を持ち得ない。利用者裁定により「R8施行後に適用され続けることを確認できた出典があるか」へ基準を改めた（ADR 0044）。
2. **Task 2 で計画のテスト雛形の前提が誤っていた**: 計画は「R8の新コードはR6と重複しない」ことを前提にテスト雛形（`NotIntersectWith`）を書いていたが、実際には(Ⅰ)イ・(Ⅱ)イ・(Ⅲ)・(Ⅳ)の4区分がR6と同一サービスコード（465120〜465123）を2026-06以降も継続使用し、新設コード（465174・465175）を持つのは(Ⅰ)ロ・(Ⅱ)ロの2区分だけだった。テスト雛形と既存`ClaimAdditionSeedScopeTests`の前提を両方訂正した。
3. **Task 3・4・5を1コミットへ統合した**: `ClaimMasterFileValidator.ValidateConditions`の「未参照のconditionDefinitionはfail-close」という既存不変条件により、条件トークンだけを先行してseedへ投入すると`JsonClaimMasterProvider.LoadEmbedded()`を呼ぶ既存テスト89件（Infrastructure 69 + App 20）が新たにRedになることが判明し、Task 3単独のコミットが不可能と判明した。コーディネーターの裁定によりTask 3・4・5を1タスク・1コミットへ統合した（commit `4c2f64b`）。
4. **Task 3の条件トークンが14個ではなく13個になった**: `r8-reform-status`の`value`には`reform-target`・`reform-exempt`の2値を投入する計画だったが、`reform-exempt`を参照する行が本フェーズのどこにも無いこと（R6基本報酬135行は`r8-reform-status`セレクタを一切持たず「制約なし一致」として機能する設計であり、後付けするとR6行書き換え禁止制約と`ClaimMasterFileValidator`の期間被覆検査の双方に抵触する）が判明し、`r8-reform-status-exempt`を投入見送りとした。トークン数は14→13へ縮小した（ADR 0046決定2）。
5. **Task 5のfail-closeテストの層を`ServiceCodeResolver`から`OfficeClaimProfilePolicy`へ移した**: ブリーフは「改定対象がR6区分option 1〜7・9で2026-06を請求しようとすると`ServiceCodeResolver.ResolveBasicReward`が例外を投げる」ことを直接テストするコードを示していたが、実装（`src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs`の`Evaluate`）を確認したところ、`ServiceCodeResolver`自体は`AverageWageBandOption`と`R8ReformStatus`の整合性を検査しないことが実測で確認された（例外が投げられなかった）。整合性チェックは1つ上の層である`OfficeClaimProfilePolicy.ValidateHistory`が担っていたため、テストをそちらへ書き直し、対象もoption 3の1点からoption 1〜7・9の全8点（`[Theory]`）へ拡大した。

---

## 6. 歯の確認一覧（意図的違反でREDになることを確認したテスト）

| Task | 手法 | 対象テスト | 結果 |
|---|---|---|---|
| Task 1 (Step 7) | `region-grade-1`の`effectiveTo`を`2026-05`→`null`へ一時変更 | `ClaimMasterR8ContinuityTests`（region側） | RED（burden側は無傷でPASS） |
| Task 1 (Fix Round 2, I-1) | 許容リストから`mhlw-unit-price-notice-post-r8-observed-946c3d96`を一時除去 | `Every_entry_reaching_june_2026_is_backed_by_an_applied_source`（region-unit-prices.json） | RED（burden-caps.jsonは無傷） |
| Task 1 (Fix Round 2, I-1) | 許容リストから`r8-burden-recognition-guide-202606`を一時除去 | 同上（burden-caps.json） | RED（region-unit-prices.jsonは無傷） |
| Task 1 (Fix Round 2, I-1・最重要) | region-grade-1エントリから今回追加した`sourceRefs`（cross-checkのみ）を直接削除。元の`authoritative` refは残置 | 同上 | RED（許容リストの仕組みだけでなく追加した証跡refそのものが合否を左右することを確認） |
| Task 2 (Step 10) | `addition.treatment-improvement.r8.i-i`をadditions.jsonから削除 | `ClaimMasterR8BoundaryTests.Treatment_improvement_additions_switch_generations_at_june_2026` | RED（`InvalidDataException`、seedロード自体が失敗） |
| Task 2 (Step 10) | additions.json・service-codes.json両方で`percentage`を`0.999`に統一変更 | `ClaimMaster`系270件 | 変化なし（270件緑のまま。golden case未実装のため率検証はTask 6以降が担う設計） |
| Task 2 (Step 10) | additions.jsonのみ`percentage`を`0.999`に変更 | `JsonClaimMasterProviderTests` / `ClaimMasterSeedPhase31Tests` | RED（`ValidateAdjustmentComponent`が構造不一致を検出） |
| Task 2 (Fix Round 1, a) | `percentage`を0.105→0.106へ変更 | `R8_treatment_improvement_percentages_match_adr_0045` | RED |
| Task 2 (Fix Round 1, b) | (Ⅳ)＝465123の行（addition・service-code・conditionDefinitionの3点）を削除 | `R8_treatment_improvement_rows_apply_only_from_2026_06` | RED（集合の下限側） |
| Task 2 (Fix Round 1, c) | 余分なコード465138（施設variant相当）を追加 | 同上 | RED（集合の上限側） |
| Task 3 (単体テスト) | `band-48000-plus`条件トークンを削除 | `R8_seeds_twelve_average_wage_bands_and_one_reform_status_condition` | RED（`HaveCount(12)`が11で失敗） |
| Task 3 (単体テスト) | `band-48000-plus`の`value`を11→99へ改変 | 同上 | RED（optionCodesの集合不一致） |
| Task 4/5 (最終状態, a) | `basic-rewards`から1行削除 | `R8_reform_target_basic_rewards_form_a_complete_product` | RED（180→179件） |
| Task 4/5 (最終状態, b) | `service-codes`行の`baseComponentKey`を存在しないキーへ書き換え | `R8_basic_reward_rows_pair_with_their_service_code_rows` | RED（参照整合性） |
| Task 4/5 (最終状態, c) | 条件トークンを1件削除 | Task 3の単体テスト | RED |
| Task 6 (Step 4) | golden case 1の期待金額を1円変更 | `Matches_adr_0045_worked_example_reform_exempt_office_in_june_2026` | RED |
| Task 6 (Step 4) | golden case 2の再掲マスタ行の単位数を837→838へ変更 | `Matches_adr_0046_worked_example_reform_target_office_in_june_2026` | RED（率丸めへ連鎖） |
| Task 6 (Step 5) | `RegionUnitPrices`/`BurdenCaps`のアサーションを一時的に`BeEmpty`へ反転 | `Region_unit_prices_and_burden_caps_resolve_in_june_2026` | RED（実データに対して主張が非自明であることを確認） |
| Task 6 (Fix Round 1, I-1) | `basic-rewards.json`の`baseUnits`を837→838へ変更 | `Reform_target_option_11_resolves_to_the_service_code_and_units_from_adr_0046` | RED |
| Task 6 (Fix Round 1, I-2) | `region-unit-prices.json`のregion-grade-1の`effectiveTo`を`null`→`"2026-05"`へ変更 | `Region_unit_prices_and_burden_caps_resolve_in_june_2026`（`BeEquivalentTo`へ強化後） | RED |

いずれも確認後にバックアップ・`git diff --stat`で復元前の状態と完全一致することを確認済み。

---

## 7. 残課題

1. **GUI 手動貫通確認が Phase 1 から未実施のまま**: `docs/open-questions.md`の「Avalonia GUI 目視確認 (AC1-8 補完)」項目が示すとおり、Phase 1着手前の予定だった実機QAはPhase 2・3-1・3-2・3-3を経て本Phase 3-4でも未実施である。本タスクはCSV生成そのものやUIに変更を加えていないため対象は増えていないが、継続課題として維持する。
2. **処遇改善(Ⅴ)・障害者支援施設variantの未投入と現在の挙動**: 処遇改善(Ⅴ)を届け出た事業所は**無音で加算0円**（fail-closeではない）、障害者支援施設は通常事業所向け条件に一致してしまい**無音の過少請求**（465120@0.105、本来は465138@0.116）が成立し得る。いずれもエラーは出ない。詳細と解除条件は`docs/open-questions.md`の該当項目・ADR 0045「確定できなかった区分」節を参照。**追記（2026-07-26）**: 障害者支援施設variantはPhase 3-5で解消した（詳細は`docs/phase3-5-acceptance.md`）。処遇改善(Ⅴ)は引き続き未投入のまま残る。
3. **`r8-reform-status-exempt`の不投入**: 現状は必要ない（R6の135行が制約なし一致で正しく機能している）が、将来`effectiveFrom: "2026-06"`以降の新規行でreform-exempt/targetを明示的に区別する必要が生じた場合に備え、`docs/open-questions.md`へ解除条件付きで起票済み。schema・validatorの変更は不要（既に対応済みの語彙）。
4. **option 8（filed-transition）に一致する条件が無い**: `transition-rules.json`はoption 8を`reform-target`に許可しているが、対応する`average-wage-band`条件定義がR6・R8いずれのseedにも存在しない。`ServiceCodeResolver`は0行一致で`MasterUnavailable`によりfail-closeする（本タスクの前後で挙動は変わらない、既存の未確定事項）。
5. **「宣言された体制届optionに対応する有効なマスタ行が当月に存在しない場合のreadiness警告」の不在**: 上記2の無音経路を将来的に検出するための恒久的なreadinessチェックは未実装（処遇改善に限らず`OfficeCapability`参照全般に共通する構造的課題）。`docs/open-questions.md`へ別項目として起票済み。
6. **体制届option 10（生産活動支援）と参加評価型（`band-participation`）の対応**: Phase3-1/Task 9由来の既存未確定事項がそのまま残る（本タスクのスコープ外）。

---

## 8. deferred minor 一覧（台帳の `minor (deferred)` 行）

- Task 1: 観測entryの`effectiveAt`が「文書の施行日」でなく「裏づける施行分」を意味する曖昧さ（スキーマ明文化の余地）
- Task 1: 継続性テストは`evidenceRole`を見ない（現状は`ClaimMasterFileValidator`が別途authoritative必須を強制。不変条件が2箇所に分散）
- Task 1: `Every_entry_reaching_june_2026_is_backed_by_an_applied_source`のアサーションメッセージが旧語「R8出典を持つか」のまま（表示のみ）
- Task 2: `AdditionRows`が`UnitRule`型で絞るため別種`UnitRule`での誤seedは集合一致をすり抜ける（現行は全て`unit-addition`で実害なし）
- Task 2: `R8_treatment_improvement_percentages_match_adr_0045`のKeys検査は上限を示さない（上限は別テスト側が担保。コメント追記の余地）
- Task 2: seed全体のlocator形式が`p.NNN`系と`物理NNN頁`系で混在（R6側の統一は別スライス）
- Task 3+4+5: 報告文中の例示「最初のグループはoption 17までが43頁」がオフバイワン（実データは16まで。JSONは正しい）
- Task 6: 条件種別2規約（`AverageWageBand`と`PaymentBand`）の混在（XMLdocに理由あり）
- Task 6: 合成sha256が0×61桁（既存`SourceRef`と同形なので規約整合）

---

## 9. `./build/ci.sh` 実行証跡

Task 7完了時点（文書のみの変更、コード・seedは無変更）に2026-07-26実行、**全ゲート緑**。

```
==> restore
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!  失敗: 0、合格:   687 - Tsumugi.Domain.Tests.dll
成功!  失敗: 0、合格:   460 - Tsumugi.Application.Tests.dll
成功!  失敗: 0、合格:   313 - Tsumugi.Infrastructure.Csv.Tests.dll
成功!  失敗: 0、合格:    30 - Tsumugi.Infrastructure.Reporting.Tests.dll
成功!  失敗: 0、合格:   259 - Tsumugi.App.Tests.dll
成功!  失敗: 0、合格:   682 - Tsumugi.Infrastructure.Tests.dll
==> coverage threshold gate
Tsumugi.Domain      Line 95.25% / Branch 88.11% / Method 93.40%  (floor 95%)
Tsumugi.Application Line 91.84% / Branch 83.66% / Method 85.00%  (floor 70%)
==> CI OK
```

合計 **2,431テスト**（Task 1〜6完了時点から変化なし。本タスクは文書のみで src/ tests/ に変更なし）。`dotnet format --verify-no-changes` も別途 exit 0 を確認済み。
