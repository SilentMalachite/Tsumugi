# Phase 3-5（指定障害者支援施設variantの実値投入）受け入れ証跡

- 対象: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` — ADR 0045が「確定できなかった区分」として持ち越した指定障害者支援施設variant（465138・465140・465141・465176）の実値投入
- spec: `docs/superpowers/specs/2026-07-26-phase3-5-facility-classification-design.md`
- 計画: `docs/superpowers/plans/2026-07-26-phase3-5-facility-classification.md`
- 実装ブランチ: `feature/phase3-5-facility-classification`
- 実行台帳（本証跡の一次情報源）: `.superpowers/sdd/2026-07-26-phase3-5-facility-classification/progress.md`
- Task別詳細報告: `task-1-report.md` 〜 `task-5-report.md`（同ディレクトリ）
- ADR: [0047](decisions/0047-r8-designated-support-facility-variants.md)

---

> **状態（2026-07-26）**: 指定障害者支援施設variantのうち一次資料から一意に確定できた4区分
> （(Ⅰ)イ=465138・(Ⅰ)ロ=465176・(Ⅲ)=465140・(Ⅳ)=465141）を、Domainの受け皿（Task 1）→
> ADR 0047とseed投入（Task 2）→永続化（Task 3）→結線（Task 4）→入力UI（Task 5）の順に実装し、
> 施設事業所が該当区分を届け出ると施設コード・施設率で正しく解決するようになった。
> 処遇改善(Ⅴ)（14区分の経過措置）は本スライスのスコープ外のまま未投入。
> コードとseedデータはTask 1〜5で完結しており、本タスク（Task 6）は文書のみを更新した。

---

## 1. 達成状況と証拠

| # | 達成内容 | 証拠（テスト名） |
|---|---|---|
| 1 | 施設区分条件（`FacilityClassification`）をDomainで評価できる。未入力は専用エラーコードでfail-close | `ServiceCodeResolverTests.Facility_classification_condition_fails_closed_when_the_context_has_no_value` / `..._condition_compares_the_token`（Theory 2ケース） |
| 2 | 施設variant4区分の`additions.json`・`service-codes.json`投入。施設区分ごとにちょうど1行へ解決（多重一致なし） | `ClaimMasterR8BoundaryTests.Facility_variants_resolve_to_exactly_one_row_per_classification`（Theory 4ケース） |
| 3 | 施設別立てが無い(Ⅱ)イ・(Ⅱ)ロ（option 3・8）は施設事業所でも通常行へ解決する | `ClaimMasterR8BoundaryTests.Tiers_without_a_facility_variant_resolve_for_both_classifications`（Theory 2ケース） |
| 4 | 施設区分未入力のまま施設variantを持つ区分を解決しようとすると専用コードでfail-close | `ClaimMasterR8BoundaryTests.Facility_variant_tiers_fail_closed_without_a_facility_classification`（Theory 4ケース） |
| 5 | 施設variant4区分の率がproduction seedからpinされている（1桁変えると検出） | `ClaimMasterR8BoundaryTests.R8_treatment_improvement_facility_percentages_match_adr_0047` |
| 6 | 2026-06の処遇改善コード集合が10件（通常6＋施設4）で上限も固定 | `ClaimAdditionSeedScopeTests.R8_treatment_improvement_rows_apply_only_from_2026_06` |
| 7 | 施設×(Ⅰ)イ×2026-06のworked example（ADR 0047決定表由来）が一致 | `ClaimCalculatorGoldenCaseTests.Matches_adr_0047_worked_example_designated_support_facility_office_in_june_2026` |
| 8 | `OfficeClaimProfile`へ`FacilityClassification`列を追加。Cancelレコードは施設区分を持てない。閉集合制約で列挙外の値を拒否 | `Phase35OfficeFacilityClassificationMigrationTests`（4 Fact: ラウンドトリップ／Cancel制約／Down・reup決定性／閉集合制約） |
| 9 | token provider・request builderが施設区分をprofile→contextへ結線し、end-to-endで解決する | `OfficeClaimBillingTokenProviderTests.Resolve_maps_the_facility_classification_to_its_token`（Theory）/ `ClaimCalculationRequestBuilderTests.Build_threads_the_facility_classification_token_into_the_context_without_an_issue`（Theory）/ `ClaimMasterR8BoundaryTests.Facility_variants_resolve_end_to_end_through_the_production_token_provider`（Theory 4ケース） |
| 10 | 入力UI（`ClaimInputView`のComboBox）が施設区分を保存・読込・クリアの3経路で往復する | `ClaimInputViewModelTests.Office_profile_round_trips_facility_classification_through_reload_and_clears_on_reenter` / `ViewInputWiringTests.ClaimInputView_exposes_only_owned_fields_histories_and_keyboard_commands` |

---

## 2. 投入した行数の実績

実測値は`git diff d0e0792..a47e765`と各JSONの`entries`/`conditionDefinitions`配列長を本証跡作成時に再計測して確認した（`d0e0792`＝本スライス着手直前のベースコミット）。

| ファイル | 投入前 | 投入後 | 差分 | 内訳 |
|---|---:|---:|---:|---|
| `conditionDefinitions`（`service-codes.json`内） | 51 | 53 | **+2** | `facility-classification-general` / `facility-classification-designated-support-facility`（Task 2、ADR 0047） |
| `additions.json`（entries） | 22 | 26 | **+4** | 施設variant4行（(Ⅰ)イ=0.116・(Ⅰ)ロ=0.120・(Ⅲ)=0.098・(Ⅳ)=0.081、calculationOrder 7〜10） |
| `service-codes.json`（entries） | 337 | 341 | **+4** | 施設variant4行（465138・465176・465140・465141） |
| 既存4行の`conditionSelectors`変更 | — | — | **4行** | `i-i`/`i-ro`/`iii`/`iv`（465120/465174/465122/465123）へ`facility-classification-general`を追加。`ii-i`（465121）・`ii-ro`（465175）は無変更 |

コード側は次の要素を追加した（本証跡作成時に実ファイルで再確認済み）。

- `FacilityClassification` enum（`Unknown=0`/`General=1`/`DesignatedSupportFacility=2`。`src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs:161`）
- `ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved = 7`（`src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs:26`）
- `OfficeClaimProfile.FacilityClassification`列（`src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs:61`）＋migration `20260726052445_Phase35OfficeFacilityClassification`
- `ClaimInputView.axaml`のComboBox1個（`ItemsSource="{Binding FacilityClassificationOptions}" SelectedItem="{Binding FacilityClassification}"`、`:160`）

---

## 3. spec からの逸脱（1件）

**spec §3.7は「readinessの非ブロッキング警告」を要求していたが、実装しなかった。** 代わりに`ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved`（Domain resolver層のfail-close）で代替した。

spec原文（§3.7）: 「`ClaimCalculationRequestBuilder` に、施設区分が未入力のときの通知を追加する。**`IsReady` を落とさない警告**とする（ADR 0041 が確立した『確定は止めないが不足を知らせる』形）」。

**不実装の理由**: `ClaimCalculationRequestBuilder`は`ValidateTokens`が返す`Issues`に1件でも要素があると`IsReady`を必ず落とす設計であり（既存の`Require`ヘルパはすべてブロッキング）、「`IsReady`を落とさない警告」という非ブロッキング経路は`ClaimCalculationRequestBuilder`に存在しない。存在する唯一の非ブロッキング経路は`UpcomingSpecificationIssues`（ADR 0041が確立した仕組み）だが、これは**「現行版には無いが将来の施行分で必須になる項目」を確定前に知らせる**ためのものであり、意味論が異なる（施設区分は現行版=R8-06で既に必須たりうる入力であり、将来施行分の先触れではない）。この経路を転用すると「将来必須になる」という誤った文言をユーザーに見せることになり、ADR 0041の設計意図と矛盾する。

したがって、施設区分が未入力のまま施設variantを持つ区分（option 2・7・4・5）を解決しようとした場合は、Domain層の`ServiceCodeResolver`が`FacilityClassificationUnresolved`で例外を投げ、`ClaimCalculationRequestBuilder`はこれを捕捉せずそのまま伝播させる（既存の他の`ServiceCodeResolutionException`と同じ扱い）。この方針は計画作成時点でGlobal Constraintsに明記され（`.superpowers/sdd/2026-07-26-phase3-5-facility-classification/progress.md`冒頭）、Task 1〜5すべてがこの前提で実装された。

**未解決とは異なる**: 施設区分を入力すれば正しく解決し（Task 4完了により結線済み、§5参照）、未入力でも(Ⅱ)イ・(Ⅱ)ロ（option 3・8。施設別立てなし）は問題なく算定できる。fail-closeが発生するのは「施設variantを持つ区分を宣言し、かつ施設区分を未入力のまま計算しようとした場合」のみである。

---

## 4. 既存4行の`conditionSelectors`変更と遡及しない根拠

ADR 0045が2026-06から投入済みの通常4行（`i-i`=465120・`i-ro`=465174・`iii`=465122・`iv`=465123）へ、`facility-classification-general`条件を追加した（`serviceCode`・`officialLabel`・`unitRule`・`percentage`等の値は一切変更していない。§2で実測確認済み）。

**変更理由**: 施設variantは通常行と同じ体制届optionを共有する（例: (Ⅰ)イも施設(Ⅰ)イもoption 2）。施設行にだけ`facility-classification-designated-support-facility`条件を付け通常行を変更しない場合、指定障害者支援施設がoption 2を届け出ると通常行（office-capability条件のみ）と施設行（office-capability＋facility-classification条件）の**両方**が一致してしまう。`ServiceCodeResolver.ResolveAdditions`は加算family特有の設計として複数一致をそのまま複数要素で返す（`ResolveBasicReward`のような`AmbiguousMatch`例外は投げない）ため、この状態は例外にならず**処遇改善加算が二重に計算される**。これを防ぐため通常4行にも`facility-classification-general`を追加し、通常行・施設行を施設区分で完全に排他化した（ADR 0047「通常行にも非施設条件を付ける理由」節）。

**確定済み請求への遡及なし**: ADR 0026は「請求確定後にOffice・Recipient・受給者証・日次記録又は報酬マスタが訂正されても、確定請求は自動で変わってはならない」と定め、確定時点の入力・規則・版・出典を`InputSnapshotJson`・`CalculationSnapshotJson`として不変に保持する。ADR 0029のsnapshot codec v2はこの不変性を版付きで再現する。したがって本変更（`conditionSelectors`の追加）はマスタの新規解決（未確定の請求・これから確定する請求）にのみ影響し、既存の確定済み`ClaimBatch`のsnapshotには一切影響しない（ADR 0047「既存行のconditionSelectors変更と遡及しないことの根拠」節）。

**2026-06以降を既に確定済みの環境がある場合の注意**: 上記のとおり確定済みの請求そのものは変わらないが、**未確定のプレビュー**（`ClaimPreviewPipeline`が返す試算結果）は本変更の影響を受ける。具体的には、2026-06以降の月についてoption 2/7/4/5（施設variantを持つ4区分）を宣言している事業所のプレビューは、Task 1〜4完了前は（施設区分入力欄自体が無かったため）通常行のみが一致していたが、完了後は施設区分の入力状態に応じて通常行／施設行のいずれかへ解決が変わる。指定障害者支援施設が施設区分を未入力のまま2026-06以降のプレビューを再実行すると、本スライス以前は通常行で試算が成立していたのに対し、本スライス以降は`FacilityClassificationUnresolved`でプレビューが失敗するようになる（§5参照。中間状態ではなく本スライス完了後の恒久的な挙動）。

---

## 5. フェーズ内の中間状態（Task 2完了時点〜Task 4完了時点。現在は解消済み）

Task 2完了時点（seed投入完了、Task 3・4未着手）からTask 4完了時点までの間、`ClaimCalculationRequestBuilder`は`FacilityClassification`を一切渡さず既定`null`のままだった。この間、体制届option 2・7・4・5（施設variantを持つ4区分）を宣言した事業所は**施設・非施設を問わず**`FacilityClassificationUnresolved`でフェイルクローズしていた（ADR 0047「影響（本ADR単独では未解消の運用上のギャップ）」節に記録済み）。

これは「指定障害者支援施設が黙って過少請求になる」という本スライス以前の状態（465120@0.105、本来は465138@0.116。ADR 0045「確定できなかった区分」表）から、「該当区分を宣言した全事業所が結線完了までfail-closeする」という状態への意図的な変化であり、Global Constraints「確定できない場合はfail-close側へ倒す」に沿った正しい向きの変化だった。

**現在は解消している**: Task 4（コミット`23126e9`）で`OfficeClaimBillingTokenProvider.TokenFor`と`ClaimCalculationRequestBuilder`が施設区分を実際に結線し、`ClaimMasterR8BoundaryTests.Facility_variants_resolve_end_to_end_through_the_production_token_provider`（4ケース）が実測で示すとおり、`OfficeClaimProfile.FacilityClassification`を保存した事業所は施設・非施設それぞれが正しい行（例: option 2→施設なら465138、非施設なら465120）へ一意に解決するようになった。未入力（`null`/`Unknown`）のままだと引き続きfail-closeするが、これは「施設区分を入力すれば通る」という正しい設計どおりの挙動であり、中間状態ではない。

---

## 6. 実装者が発見した3件

1. **Task 3: SQLiteではチェック制約の実行時SQL源が`migration`の`Up()`ではなく`*.Designer.cs`の`BuildTargetModel()`である。**
   `dotnet ef migrations add`が生成する`Up()`の`migrationBuilder.AddCheckConstraint(..., sql: "...")`文字列は、SQLite providerが実行時に使うSQLの直接のソースではない。SQLiteは`ALTER TABLE ADD/DROP CONSTRAINT`をネイティブサポートしないため、`SqliteMigrationsSqlGenerator`はテーブル再構築（rebuild）方式を取り、その際に参照するチェック制約の定義文字列は`*.Designer.cs`の`BuildTargetModel()`内の`t.HasCheckConstraint(...)`呼び出し（そのマイグレーション時点のモデルスナップショット）から来る。実装者は最初`Up()`側の文字列だけを一時的に書き換えて歯の確認を試みたが green のままで、原因を切り分けた上で`.Designer.cs`側を編集し直して初めてREDを再現できた（`Phase35OfficeFacilityClassificationMigrationTests`）。Fix Round 1の閉集合制約追加でも同じ教訓を踏まえ、`.Designer.cs`と`TsumugiDbContextModelSnapshot.cs`の両方を狙って歯を確認した。

2. **Task 4: コーディネータが指示した歯の確認（Task 2のテストがRED化するはず）は前提が誤っていた。**
   コーディネータは「`TokenFor`の写像を壊せばTask 2の`Facility_variants_resolve_to_exactly_one_row_per_classification`がREDになる」と予測していたが、実装者が実測したところ**GREENのまま**だった。原因は同テストが`ClaimBillingConditionContext`をヘルパ内でリテラル文字列（`"general"` / `"designated-support-facility"`）から直接手組みしており、`OfficeClaimBillingTokenProvider`（Task 4の結線対象）を一切経由しないため。実装者は「REDにならなかった」という事実をそのまま報告した上で、`OfficeClaimBillingTokenProvider.Resolve`を実際に呼び出してtokenを得てから`ServiceCodeResolver.ResolveAdditions`へそのまま渡す本物のend-to-endテスト`Facility_variants_resolve_end_to_end_through_the_production_token_provider`を新規追加し、同じ改変で4/4ケースがREDになることを実証した（結線がload-bearingであることの直接証拠）。

3. **Task 5: `ViewInputWiringTests`は assertion list に足さなければ検出しない。**
   `ViewInputWiringTests.ClaimInputView_exposes_only_owned_fields_histories_and_keyboard_commands`は、ViewModelの主要プロパティごとに`{Binding <Name>`が画面側に存在することを固定するトリップワイヤだが、これはテスト内の固定文字列リスト（assertion list）に名前を足さない限り機能しない。実装者はComboBoxを追加した後、あえて`FacilityClassification`をこのリストから一時的に外して歯を確認したところGREENのままであることを実測し（追加しなければComboBoxを消しても検査は何も検出しない）、同時に既存の`CapacityHeadcount`/`StaffingKey`/`RegionKey`（Phase 3-5 Task 3で追加された構造化入力）もこのリストに含まれておらず同じ「無検査」状態にあることを発見した。この既存の検査漏れは本タスクのスコープ外として一旦報告のみに留めたが、レビューのFix Round 1で3項目とも assertion list へ追加し、それぞれ個別にComboBox/NumericUpDownを一時削除してREDになることを確認した（コミット`a47e765`）。

---

## 7. 歯の確認の一覧

| Task | 手法 | 対象テスト | 結果 |
|---|---|---|---|
| Task 1 (Step 7) | `EvaluateFacilityClassification`内の`throw`を一時的に`return false`へ書き換え | `ServiceCodeResolverTests.Facility_classification_condition_fails_closed_when_the_context_has_no_value` | RED（実出力あり） |
| Task 2 | `percentage`を1桁変更 | `ClaimCalculatorGoldenCaseTests`のgolden case ／ seed側pinテスト | RED（両方） |
| Task 2 | 通常行から`facility-classification-general`条件を除去 | 施設側2行一致の検証（option 2のみで確認） | RED（多重一致を検出） |
| Task 2 | `ii-i`（465121）へ施設条件を誤付与 | 施設×option3の解決 | RED（0行一致） |
| Task 3 (Step 7) | `Up()`のSQL文字列から`AND "FacilityClassification" IS NULL`を除去 | `Phase35OfficeFacilityClassificationMigrationTests` | GREENのまま（実行時SQL源ではないことを実証） |
| Task 3 (Step 7) | `.Designer.cs`の`BuildTargetModel()`から同句を除去 | `Target_extends_cancel_payload_check_to_the_facility_classification_column` | RED（実出力あり） |
| Task 3 (Fix Round 1) | `.Designer.cs`と`TsumugiDbContextModelSnapshot.cs`から`CK_OfficeClaimProfiles_FacilityClassification_ClosedSet`を除去 | `Target_rejects_a_facility_classification_value_outside_the_closed_set` | RED（実出力あり） |
| Task 4 (Step 8a) | `TokenFor`の`General`マッピングを施設トークンへ入れ替え | `OfficeClaimBillingTokenProviderTests.Resolve_maps_the_facility_classification_to_its_token` | RED（実出力あり） |
| Task 4 (Step 8b) | 同じ改変を維持したまま | `Facility_variants_resolve_to_exactly_one_row_per_classification`（Task 2のテスト） | GREENのまま（直交していることを実証。§6の2参照） |
| Task 4 (Step 8b) | 同じ改変を維持したまま | `Facility_variants_resolve_end_to_end_through_the_production_token_provider`（新規追加） | RED（4/4ケース、実出力あり） |
| Task 5 (Step 6) | ComboBoxをView から一時削除 | `ViewInputWiringTests` | RED（実出力あり） |
| Task 5 (Step 6追加確認) | ComboBox削除＋`FacilityClassification`をassertion listから一時除去 | 同上 | GREENのまま（無検査だったことを実証。§6の3参照） |
| Task 5 (Fix Round 1) | `CapacityHeadcount`のNumericUpDownを一時削除 | `ViewInputWiringTests` | RED（実出力あり） |
| Task 5 (Fix Round 1) | `StaffingKey`のComboBoxを一時削除 | 同上 | RED（実出力あり） |
| Task 5 (Fix Round 1) | `RegionKey`のComboBoxを一時削除 | 同上 | RED（実出力あり） |

いずれも確認後にバックアップ・`git diff`で復元前の状態と完全一致することを確認済み（各task reportに記録）。

---

## 8. 残課題

1. **福祉・介護職員等処遇改善(Ⅴ) 14区分の未投入**: 選択番号6・サブ区分⑴〜⒁の経過措置。ADR 0045が未投入とし、ADR 0047・本スライスでも投入していない。`r8-capability-correction`物理9頁の⑴〜⒁選択肢と対応する14通りの率の一意対応確認に、本スライスのスコープを超える追加調査を要する。選択番号6を届け出た事業所は引き続き**警告なしに加算が0円になる**（無音の未算定。fail-closeではない）。詳細は`docs/open-questions.md`の該当項目・ADR 0045「確定できなかった区分」節を参照。
2. **施設での体制届option集合の絞り込み（ADR 0021: R8-06の`treatment-improvement`は`{1,2,4,5,7}`）**: 率とは別の入力バリデーションの話であり、一次資料の再確認と体制届側の変更を伴うため本スライスの非スコープとした（spec §4）。現状、`OfficeClaimProfile.FacilityClassification`と体制届の`treatment-improvement`選択番号は独立して入力可能であり、組合せの妥当性検証は行っていない。
3. **GUI手動貫通確認がPhase 1から未実施のまま**: `docs/open-questions.md`の「Avalonia GUI 目視確認 (AC1-8 補完)」項目が示すとおり、実機起動でのフォント拡大追従・Reduce Motionのタブ順・フォーカス移動は手動QAでしか確認できない。本タスクは`ClaimInputView`へComboBoxを1つ追加したが（Task 5）、新規View・新規タブは作っておらず既存の「事業所請求設定」構造化入力群と同一パターンのため対象範囲は実質的に増えていないが、継続課題として維持する。
4. **`FacilityClassification`が体制届の`treatment-improvement`選択番号と独立入力である点の未検証**: 施設区分を入力しても体制届optionと矛盾する組合せ（例: option 3・8のみ届け出ているのに施設区分を入力する等）を弾くバリデーションは無い。実害は無い（(Ⅱ)は施設区分条件を持たないため単に無視される）が、上記2の絞り込みが実装されればあわせて解消されうる。

---

## 9. deferred minor 一覧（台帳の `minor (deferred)` 行）

台帳（`progress.md`）の`minor (deferred)`行は8件あり、うち1件（Task 2の open-questions carry-forward）は本タスク（Task 6, §Step 2）で解消済みのため下記からは除外し、別途§10で扱う。残る7件を列挙する。

- Task 1: 同一namespaceに`ClaimConditionKind.FacilityClassification`（値10。条件kind）と新設enum`FacilityClassification`（施設区分そのものの型）が同名で共存する（曖昧性なし・ブリーフ指示どおりの命名）
- Task 2: fail-closeの影響範囲が`conditionSelectors`の配列順に暗黙依存する（`MatchesAll`の`.All`短絡。施設条件を先頭へ動かすと体制届未提出の事業所まで落ちる可能性がある）。ADRかテストで順序を固定すると安価
- Task 2: `additions.json`施設4行の`locator`に率の値そのものが埋まっている（位置指定だけの規律から外れる）
- Task 2: ADR 0047が遡及しない根拠としてADR 0032・0034を引用していない（0026・0029のみを引用。論旨自体は成立）
- Task 3: プロパティ名`FacilityClassification`が型名と同一（`ReformStatus`は型が`R8ReformStatus`で分離されているのと対照的）。ビルドは通り実害なし
- Task 4: `OfficeClaimProfileQueryRevisionDto`の新パラメータだけ`<summary>`直付けで、同ファイル内の`<param>`スタイルと不統一（ビルド0警告・実害なし）
- Task 5: 接頭辞一致方式（`xml.Should().Contain($"{{Binding {binding}")`）のため、`FacilityClassification`の`SelectedItem`側だけ消しても`ItemsSource="{Binding FacilityClassificationOptions}"`が残れば検査をすり抜ける余地がある（`ReformStatus`等、既存の同型項目すべてに共通する既存仕様。Task 5固有の劣化ではない）

---

## 10. `docs/open-questions.md` の carry-forward 処理

Task 2レビューで発見された`minor (deferred)`（progress.md 98行目）: 「open-questionsの中間状態の記述2箇所がTask 3/4完了後に陳腐化する」を、本タスク（Task 6）で解消した。具体的には以下2箇所を削除した。

1. `[x]`項目（「[Phase3-4/Task2 follow-up] 障害者支援施設variantの実値投入」の旧記述）の「残作業」文（`OfficeClaimProfile`への施設区分の永続化とrequest builderの結線が未実装、という中間状態の記述）
2. 恒久`[ ]`readiness項目（「体制届optionに対応するマスタ行が当月に存在しない場合のreadiness警告」）末尾の「fail-close側の新しいギャップ」に関する記述（Task 3・4完了により解消済みの中間状態）

いずれも中間状態は本証跡§5に記録済みのため、`open-questions.md`側からは削除して恒久項目のみを残した。

---

## 11. `./build/ci.sh` 実行証跡

Task 6完了時点（文書のみの変更、コード・seedは無変更）に2026-07-26実行、**全ゲート緑**。

```
==> restore
==> format verify (gate #2)
==> build warnings-as-errors (gate #1)
==> test + coverage (gate #3, arch=gate#4, offline=gate#5)
成功!  失敗: 0、合格:   691 - Tsumugi.Domain.Tests.dll
成功!  失敗: 0、合格:   463 - Tsumugi.Application.Tests.dll
成功!  失敗: 0、合格:   313 - Tsumugi.Infrastructure.Csv.Tests.dll
成功!  失敗: 0、合格:    30 - Tsumugi.Infrastructure.Reporting.Tests.dll
成功!  失敗: 0、合格:   260 - Tsumugi.App.Tests.dll
成功!  失敗: 0、合格:   721 - Tsumugi.Infrastructure.Tests.dll
==> coverage threshold gate
Tsumugi.Domain      Line 95.27% / Branch 88.12% / Method 93.42%  (floor 95%)
Tsumugi.Application Line 91.85% / Branch 83.66% / Method 85.02%  (floor 70%)
==> CI OK
```

合計 **2,478テスト**（Task 1〜5完了時点から変化なし。本タスクは文書のみでsrc/ tests/に変更なし）。`dotnet format --verify-no-changes`も別途exit 0を確認済み。
