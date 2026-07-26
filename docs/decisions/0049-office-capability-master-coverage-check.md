# ADR 0049: 体制届optionに対応するマスタ行の存在検査（恒久readinessチェック）

- 状態: 確定（2026-07-27）
- 関連: [ADR 0021](0021-office-capability-official-codes.md) / [ADR 0026](0026-claim-batch-snapshot.md) /
  [ADR 0041](0041-version-aware-readiness.md) / [ADR 0045](0045-r8-treatment-improvement-addition-values.md) /
  [ADR 0048](0048-r6-06-treatment-improvement-facility-and-category-v.md)

## 結論

事業所が体制届（`OfficeCapability.Flags`）で宣言したキーのうち、**処理対象年月に有効な条件定義が存在せず、かつ他の期間には存在するもの**を検出し、**確定をブロックしない警告**として `ClaimPreviewDto.CapabilityCoverageWarnings` で運ぶ。

判定は Domain の純粋関数 `OfficeCapabilityCoveragePolicy.FindUncoveredKeys(declaredKeys, monthConditionValues, allConditionValues)` に閉じる。`IsReady` は変えない。

処遇改善に限らず、`OfficeCapability` を参照する**全加算**に効く恒久チェックである。

## 背景

`ClaimConditionKind.OfficeCapability` の条件は、事業所が届け出た one-hot キー（ADR 0021。例: `mhlw.b46.capability.treatment-improvement.6`）に一致するかどうかで加算行の適用を決める。

一致する行が当月に**1件も無い**場合、`ServiceCodeResolver.ResolveAdditions` は「その加算は算定されない」と解釈して**空リストを返す**。これは加算family として正しい設計である（複数一致も0件一致も例外にしない。ADR 0047）。しかし**「届け出たのに0円」という状態が、エラーにも警告にもならず無音で成立する**。

実害は2度観測されている。

1. **ADR 0045 Fix Round 1 I-4**: 処遇改善(Ⅴ)（option 6）が一切seedされていない状態で option 6 を届け出た事業所は、加算が黙って0円になっていた。`docs/open-questions.md` に「恒久的な readiness 警告を別途実装する」として起票されていた。
2. **ADR 0048**: (Ⅴ)は 2025-03 で失効する。2025-04以降も option 6 を届け出たままの事業所、および 2026-06以降に option 6 を届け出ている事業所（B型に(Ⅴ)は存在しない）は、同じく無音で0円になる。

1 は「まだ投入していない」、2 は「制度上失効した」という別の原因だが、**運用者から見える現象は同一（無音の未算定）**であり、同じ検査で拾える。ADR 0048 が(Ⅴ)を投入しても 2 は残るため、本ADRは ADR 0048 と同一フェーズで導入する。

## 決定

### 決定1: 2段構えで判定する

宣言キー `key` について、次の**両方**が成立するときだけ警告する。

1. `key` を `value` に持つ `kind: office-capability` の条件定義が、**登録済みマスタのいずれかの期間には存在する**。
2. `key` を `value` に持つ条件定義が、**処理対象年月には有効でない**。

```
警告 ⟺ allConditionValues.Contains(key) ∧ ¬monthConditionValues.Contains(key)
```

**1 が必要な理由（偽陽性の回避）**: 体制届には請求の算定に一切関与しないキーが混在する。本アプリ自身が、ADR 0021 以前の暫定キー `mealProvision` / `transportSupport` を `OfficeCapabilityViewModel` から書き続けている（どの条件定義からも参照されない。`docs/open-questions.md` に移行課題として起票済み）。1 を落として「当月に無い」だけで警告すると、**すべての事業所が毎月これらのキーで警告を受ける**。恒久的なノイズは警告そのものを無視させるため、意味のある警告（失効・未施行）だけを拾う設計にする。

条件1は「登録済みマスタの全期間」を見る必要があるため、`IClaimMasterProvider` に月フィルタを掛けない列挙を1メソッドだけ足した（決定4）。

### 決定2: 警告であり `IsReady` を変えない

確定をブロックしない。理由は3つ。

1. **`docs/open-questions.md` の起票文言が「readiness 警告」であり、ブロックを求めていない。**
2. **ADR 0041 の前例**: 「将来の施行分で必須になる項目の不足は警告として示し、`IsReady` は変えない」という非ブロッキング経路が既に確立しており、`UpcomingSpecificationIssues` と同じ形で運べる。本チェックはその隣に並ぶ。
3. **期間境界をまたぐ正当な体制届が存在する**。事業所は制度が変わっても体制届の選択番号をすぐには更新しない（更新義務のタイミングと請求月がずれる）。「2025-03まで(Ⅴ)を算定していた事業所が 2025-04 の請求を出す」ことそれ自体は正常な業務であり、(Ⅴ)が付かないことは制度上正しい結果である。ここでブロックすると、**制度どおりの請求ができなくなる**。

`ClaimPreviewDto.IsReady == true` のまま `CapabilityCoverageWarnings` が非空になりうることをテストで固定した（`CalculateClaimUseCaseTests` / `ClaimPreparationViewModelTests.PreviewAsync_surfaces_capability_coverage_warnings_without_blocking_readiness`）。

### 決定3: 判定は Domain の純粋関数に置き、値の抽出も1箇所へ集約する

`src/Tsumugi.Domain/Logic/Claim/OfficeCapabilityCoveragePolicy.cs` に2つの `static` メソッドを置く。日付・乱数・I/O に依存しない。

- `FindUncoveredKeys(declaredKeys, monthConditionValues, allConditionValues)` — 決定1の判定。結果は `Distinct` ＋ `OrderBy`（`StringComparer.Ordinal`）で決定論的に整列する。
- `ExtractCapabilityValues(conditionDefinitions)` — `kind: office-capability` の条件定義から operand が運ぶ値文字列を列挙する（token operand は単一値、token set operand は複数値、その他の operand 型は対象外）。

`ExtractCapabilityValues` を分離したのは、同じ `Where(kind == OfficeCapability).SelectMany(operand switch {...})` が3箇所（`ClaimPreviewPipeline`・`JsonClaimMasterProvider`・テスト）へ複製され、片方だけ壊れても検出できない状態になっていたためである（レビュー指摘）。当月側・全期間側の**両方の入力を同じ関数で組み立てる**ことで、抽出ロジックの食い違いを構造的に防ぐ。

### 決定4: `IClaimMasterProvider` を1メソッドだけ広げる

```csharp
IReadOnlySet<string> AllOfficeCapabilityConditionValues();
```

既存の `ResolveCalculationMasters(ServiceMonth)` は必ず月でフィルタするため、決定1の条件1（全期間に存在するか）を答えられない。「全期間の条件定義バンドルを返す」ような広いAPIを足すと、月フィルタを迂回して算定に使う経路が生まれてしまう。**答えられる問いを1つに絞った専用メソッド**（`IReadOnlySet<string>` を返すだけで、条件定義そのものは出さない）にすることで、誤用の余地を残さない。

実装者は8つの `IClaimMasterProvider` 実装（production 1・テストfake 7）すべてを更新した。

### 決定5: 警告は not-ready 経路でも運ぶ

`ClaimPreviewPipeline` は readiness 不成立や request 構築失敗で早期 return する経路を持つ。当初実装は最終成功経路にしか警告を載せておらず、**受給者証未入力・プロファイル未登録など無関係な理由で not-ready の月では警告が消えていた**（レビュー指摘）。

無音の加算欠落は、まさに not-ready の月にも同時に起きうる。ここで落とすと運用者は不足に気付けないまま他の不備を直し、直った瞬間に0円で確定してしまう。したがって宣言キーの解決を request 構築の成否から切り離し（`ClaimCalculationRequestBuilder.ResolveDeclaredOfficeCapabilityKeys` を新設して `snapshot.OfficeCapabilities` から直接解決する）、**すべての return 経路へ警告を載せる**。

**ただし遅延評価は保つ**: 宣言キーが1件も無ければ算定マスタの解決自体を試みない。「無関係な理由で not-ready のときは算定マスタに触れない」という既存の不変条件（`CalculateClaimUseCaseTests.Execute_returns_issues_and_skips_calculation_when_not_ready` が `CalculationMasterResolutions == 0` で固定）を壊さないためである。

### 決定6: UI へ出す

計算されるだけで運用者に届かなければ意味がない。`ClaimPreparationViewModel.CapabilityCoverageWarnings`（`ObservableCollection<string>`）へ流し、`ClaimPreparationView.axaml` に見出し「体制届optionに対応するマスタ行が見つかりません（今月の確定には影響しません）」と一覧を出す。隣接する `UpcomingSpecificationWarnings` ブロックと同じ構造・同じ `CountGreaterThanZeroConverter` による表示制御にした。既存の `Clear()` 呼び出し3箇所（`CloseAsync` / `CancelAsync` / `ClearPreviewAndHistory`）すべてに対応する `Clear()` を追加した。

## 選択肢

### A: 検査しない（不採用）

現状維持。無音の未算定が残る。ADR 0045 の時点で「別途実装する」と決めており、ADR 0048 が(Ⅴ)の失効という**新しい無音経路を意図的に作る**以上、同時に導入しなければ状況は悪化する。不採用。

### B: `IsReady` を落としてブロックする（不採用）

決定2の理由3により、期間境界をまたぐ正当な体制届で請求ができなくなる。不採用。

### C: 「当月に無い」だけで警告する（不採用）

実装は最も単純だが、算定に関与しない暫定キー（`mealProvision` / `transportSupport`）で全事業所が毎月警告を受ける。警告が無視されるようになり、決定1の目的が失われる。不採用。

### D: 2段構えの警告（採用）

決定1。偽陽性を構造的に排除しつつ、失効・未施行という本当の穴だけを拾う。

## 影響

### 残る限界（重要）

**警告であるため、運用者が見落とせば加算が0円のまま確定できる。** 本ADRは「無音の未算定」を「有音の未算定」へ変えるだけであり、誤請求（過少請求）そのものを機械的に防ぐものではない。ブロックしない理由は決定2のとおりだが、**この限界は残る**。

確定をブロックせず、確定後の請求内容にも影響しないため、**警告を見落として確定した請求は、後から警告だけを根拠に自動訂正されることはない**（ADR 0026 の不変性）。訂正が必要な場合は通常の訂正フローによる。

### spec §6.1 の第2の副条件は未実装

設計spec §6.1 の条件2は「処理対象年月に有効な条件定義が無い、**または**有効な条件定義はあるがそれを参照するサービスコード行が処理対象年月に無い」の2つを含む。**実装は前者だけである**（`FindUncoveredKeys` は条件定義の集合しか見ない）。

現行seedでは差が出ない。全32件の `office-capability` 条件定義について、条件定義が有効でありながらそれを参照する行が1件も有効でない月は**0件**であることを確認した（`ClaimMasterFileValidator.ValidateConditions` が「どの行からも参照されない条件定義」を load 時に拒否し、`condition does not cover the service period` 検査が条件期間 ⊇ 行期間を強制するため、条件定義が行より広い期間を持つ場合にのみ理論上の隙間が生じる）。

**この「0件」は当初は人手確認だけで、崩れてもfail-closeするものが何も無かった。** 本ブランチ最終レビューの修正（item 6）で常設アサーションへ格上げした:
`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterCapabilityCoverageTests.Every_effective_office_capability_condition_is_referenced_by_an_effective_service_code_row` が 2024〜2030 の全月について production seed を走査し、有効な `office-capability` 条件定義がどの service-code 行からも参照されない月を検出したら赤にする。走査が空振りしていないこと（月数・条件件数が非ゼロ）も同テスト内で固定し、判定関数自体の歯は `..._The_check_detects_a_capability_condition_that_no_service_code_row_references` が合成データで実証する。

**判定関数（`FindUncoveredKeys`）自体は依然として前者だけを見る。** 将来この形の隙間が生じうるseedを実際に追加する場合は、`monthConditionValues` の組み立てを「当月有効な行から参照されている条件定義の値」へ狭める形で拡張できる（判定関数は変更不要）。上記アサーションは、その拡張が必要になった瞬間を機械的に知らせるためのものである。

### (Ⅴ)区分と処遇改善対象optionの組合せ — 2方向を区別する

当初この節は「本チェックは弾かないが**算定額には影響しない**」と一括りに書いていた。**それは片方の向きにしか当てはまらない。**

**無害な向き（band だけがあって option 6 が無い）**: マスタ側の二重ゲート（ADR 0048 決定4）により、option 6 が立っていなければ(Ⅴ)行は一致しない。よって算定額には影響しない。また 2025-04 以降は band キー自体が本チェックの警告対象になるため、失効後は可視化される。

> **訂正（`feature/capability-declaration-satisfiability`、2026-07-27）**: 直前の「本チェックでも入力側でも弾かない」は、下記「追補」節（`FindUnsatisfiableDeclaredKeys`）導入前の記述であり、**現在は誤り**。band だけを宣言すると、band キー自体は当月に条件定義を持つため決定1の判定（本節冒頭の2段構え）は依然沈黙するが、band を要求する(Ⅴ)行はどれも同じ行で option 6 も要求するため、追補の判定はこの band を「宣言はあるが1行も成立しないキー」として報告する（`IncompleteCapabilityDeclarationWarnings`。実例: 2024-06 に band.3 のみ宣言すると band.3 が報告される）。**本チェック（追補を含む）は現在この向きも弾く。** 入力側（`OfficeCapabilityViewModel.SaveAsync`）も、保存エラーにはしないが、**band を要求しない選択番号が選ばれている間は band キー自体を書き込まない**という形でこの向きを塞ぐよう改めた（詳細は追補 (5)）。

**有害な向き（option 6 だけがあって band が無い）**: seed の(Ⅴ)行は `capability-treatment-improvement-v`（option 6）と `-v-band-{n}` の**両方**を要求するため、band が無いと 2024-06〜2025-03 のどの(Ⅴ)行にも一致せず、**加算が無音で0円になる**（本ADRが排除しようとしている無音の過少請求そのもの）。しかも本チェックは警告しない —— `…treatment-improvement.6` は当該月に**有効**なので `!month.Contains(key)` が成立しないからである。

**本ブランチ最終レビューの修正（I1）で、この向きを入力側で塞いだ。** `QueryClaimBillingTokenOptionsUseCase` が当月の service-code 行を走査し、`treatment-improvement-v-band.*` 条件を**同じ行で**要求している `treatment-improvement.{n}` の選択番号を `TreatmentImprovementOptionsRequiringVBand` として返す。`OfficeCapabilityViewModel.SaveAsync` は、その集合に属する選択番号を band 未選択で保存しようとした場合に保存エラーを返し、**不完全な宣言を1件も永続化しない**。どの選択番号が(Ⅴ)かはコードに書かず、常にマスタ行から導出する（CLAUDE.md ハード制約3）。

証拠: `OfficeCapabilityViewModelTests.SaveAsync_rejects_an_option_that_requires_a_v_band_when_no_band_is_selected` / `..._accepts_an_option_that_requires_a_v_band_when_the_band_is_selected` / `..._accepts_an_option_that_does_not_require_a_v_band_without_a_band`、`QueryClaimBillingTokenOptionsProductionWiringTests.Only_category_v_requires_a_band_in_the_r6_generation`（実seedで option 6 のみ） / `..._No_option_requires_a_band_once_category_v_is_gone`。

なお **`OfficeClaimProfile` 側の施設区分と体制届optionの組合せ検証**（Phase 3-5 が一次資料の再確認を要するとして非スコープにしたもの）は依然として未実施であり、`docs/phase3-5-acceptance.md` §8-2 の既存課題へ合流させる。

### テスト

- `tests/Tsumugi.Domain.Tests/Logic/Claim/OfficeCapabilityCoveragePolicyTests.cs` — 9件（判定4件＋`ExtractCapabilityValues` 5件）。
- `CalculateClaimUseCaseTests` — 4件（警告する／未参照キーは警告しない／当月に有る場合は警告しない／無関係な理由の not-ready でも警告を運ぶ）。
- `ClaimMasterR6FacilityTests.Category_v_becomes_an_uncovered_capability_after_it_expires` — 実seedに対する結線検査（2025-04・2026-06 の2ケース）。
- `ClaimPreparationViewModelTests.PreviewAsync_surfaces_capability_coverage_warnings_without_blocking_readiness` — UI への到達と `IsReady` 非干渉。

## 追補（隣接する別の穴の一般化。`feature/capability-declaration-satisfiability`、2026-07-27）

決定1の2段構えは「K が当月のどの条件定義にも無い」（失効・未施行）だけを拾う。**K が当月に生きているのに、K を含む行がすべて追加の体制届キー（companion）も要求していて、宣言集合では1行も成立しない**場合は対象外のまま残っていた（§「(Ⅴ)区分と処遇改善対象optionの組合せ」で扱った「有害な向き」がまさにこれで、当時は入口ガード I1 だけで塞いでいた）。

### (1) 塞いだ穴

処遇改善(Ⅴ)の実例が典型: `mhlw.b46.capability.treatment-improvement.6`（option 6）だけを宣言し `mhlw.b46.capability.treatment-improvement-v-band.{n}`（band）を宣言していない事業所は、option 6 自体が当月（2024-06〜2025-03）に有効なため決定1の判定は沈黙するが、実seedの(Ⅴ)行23件は**すべて** option 6 と band の両方を `conditionSelectors` に要求するため0行一致し、加算は無音の¥0になる。`OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys` を新設し、宣言キー K について「K を含む行が当月に存在するが、そのどれも（宣言集合との対応で）充足可能でない」場合を検出する。行ごとの要件は条件のリスト（各条件は受理可能な値の集合。`equals` なら1要素、`in` なら複数要素）としてモデル化し、行が充足可能 ⇔ すべての条件の値集合が宣言集合と重なる、と定義した。既存の `FindUncoveredKeys` は変更していない（両者は排反 — 前者は「K が当月に無い」、後者は「K が当月にある」が前提）。

### (2) capability条件だけを見る理由と偽陽性回避

行には capability 種別以外（facility-classification・reward-system・average-wage-band・capacity・staffing 等）の条件も同居する。これらを行の「充足可否」判定に混ぜると、宣言集合（capabilityキーのみ）とは決して重ならない値（例: `general` / `designated-support-facility`）を持つ条件のせいで、本来成立する行まで「不成立」と誤判定する。実seedの処遇改善(Ⅰ)行（465120＝一般事業所向け、465138＝指定障害者支援施設向け）は、どちらも `capability-treatment-improvement-i`（option 2/Ⅰ）と `facility-classification-*` の両方を同じ行に要求する構造になっており、これが実際に偽陽性を起こしうる最小の実例になっている。そのため判定関数は `ExtractCapabilityValueSets` で `kind: office-capability` の条件だけを抽出し（`ExtractCapabilityValues` と同じ kind フィルタ契約。ただし平坦化はしない — 条件ごとの値集合を保つ）、それ以外の kind は行の要件から構造的に除外する。この除外を外すと、option 2/Ⅰ を宣言しただけの一般事業所が「宣言不完全」と誤警告される。実装時に `ExtractCapabilityValueSets` から kind フィルタを一時的に外し、`CapabilityDeclarationSatisfiabilityProductionWiringTests`（実seed・2024-06）を実行して確認済み: `Declaring_the_i_option_alone_is_not_reported_even_though_its_rows_also_carry_a_facility_classification_condition` が
`Expected dto.IncompleteCapabilityDeclarationWarnings to be empty because facility-classification条件はcapability種別ではないため、行の充足可否に含めてはならない, but found at least one item {"mhlw.b46.capability.treatment-improvement.2"}.`
で失敗し、同時に `Declaring_the_v_option_together_with_its_band_is_not_reported` も
`Expected dto.IncompleteCapabilityDeclarationWarnings to be empty, but found at least one item {"mhlw.b46.capability.treatment-improvement-v-band.3"}.`
で失敗した（kind フィルタを外すと `reward-system` 種別の値まで混入し、実質すべての行が「不成立」に見えるため）。kind フィルタを戻すと同クラスの4件とも green に戻る。

### (3) 入口ガード（`OfficeCapabilityViewModel.SaveAsync` の I1）との役割分担

I1 は `OfficeCapabilityViewModel.SaveAsync` に置いた**新規保存時**の入口ガードで、band を要求する選択番号を band 未選択のまま保存しようとする操作を1件も永続化しない（保存エラーで拒否する）。しかし `RegisterOfficeCapabilityUseCase` 自体はフラグ辞書を検証せず永続化するため、I1 は「この ViewModel から新規保存する経路」にしか効かない。将来の取込機能・別の入力面・テスト用シード・DB直接操作は I1 を素通りする。加えて I1 は `PeriodStart` の月の語彙しか見ないため、世代境界をまたぐ体制届（例: 期間開始が2025-01で処理対象月が2025-06）は開始月の語彙でしか検査されない。

本検査（`FindUnsatisfiableDeclaredKeys`）は**プレビュー時のマスタ照合**（`ClaimPreviewPipeline`）に置くことで、書き手が誰であれ・いつ書かれたレコードであれ、**処理対象月ごとに独立して**再評価する。I1 が「不完全な宣言を新規に作らせない」予防であるのに対し、本検査は「既に存在する任意の宣言を、確認するたびに検査する」検出であり、互いに補完する（I1 だけでは救えない経路を本検査が拾う）。

### (4) 残る限界

判定は capability 種別の条件同士の競合だけを見る。**average-wage-band や capacity 起因で行が成立しない場合**（条件は参照されているが参照先の行が他の理由で成立しない、という同じ形の別バリエーション）は依然として拾わない。`docs/open-questions.md` の該当項目に追補済み。また本検査も警告であり `IsReady` を変えないため、決定2・「残る限界」節と同じく、運用者が見落とせば加算が0円のまま確定できる点は変わらない。

DTO は既存の `ClaimPreviewDto.CapabilityCoverageWarnings` と**並列の別リスト**（`ClaimPreviewDto.IncompleteCapabilityDeclarationWarnings`）にした。同一リストへ混ぜると、運用者が「失効した option」と「宣言が不完全（companion option の過不足）」のどちらか判別できず、対処（体制届の見直し vs companion option の追加・削除）を誤る。UI も `ClaimPreparationView.axaml` に隣接する別ブロックとして表示する。**宣言が不完全になる原因は「不足しているキーがある」場合と「不要なキーが残っている」場合の両方があり得るため（(5)参照）、見出し文言はどちらか一方の対処だけを指示しない中立な表現にした。**

### (5) orphan band の書き込みガード（レビュー追補、2026-07-27）

本節冒頭で訂正したとおり、「無害な向き（band だけがあって option 6 が無い）」は追補の判定自体が拾うようになったが、**発生源を断つ方が望ましい**。`OfficeCapabilityViewModel.SaveAsync` は元々、選択中の処遇改善対象の選択番号に関わらず band を無条件で書いていた（(Ⅴ)以外から見た band は「無害だから弾かない」という、当初の誤った前提に基づく設計判断だった）。加えて `ReloadCapabilityOptions` は選択番号の変更単体では band 選択をクリアしないため、(Ⅴ)（option 6）から他区分へ切り替えた後も band 選択がそのまま残り、次の保存操作で orphan な band キーが永続化される経路が実運用で到達可能だった（レビュー指摘）。

`SaveAsync` の band 書き込みを、既存の I1 ガード（「option 側が band を要求するのに band 未選択なら保存エラー」）の**逆向き**（「選択中の option が band を要求しない場合は band キー自体を書かない」）に改めた。使うデータは I1 と同じ `_optionsRequiringVBand`（`QueryClaimBillingTokenOptionsUseCase.TreatmentImprovementOptionsRequiringVBand`、マスタ行から導出）であり、UI へ「どの選択番号が(Ⅴ)か」という語彙を新たに持たせてはいない。band を要求しない選択番号のまま band を選んで保存しても、エラーにはならず単に band キーが書かれないだけになる（I1 とは非対称: option 側の不足はエラーで拒否するが、band 側の不要分は黙って落とす。band だけを外させる再入力を要求するコストと、単に書かないコストが不釣り合いなため）。

証拠: `OfficeCapabilityViewModelTests.SaveAsync_does_not_write_an_orphan_band_when_the_selected_option_does_not_require_it` / `..._does_not_write_an_orphan_band_after_switching_away_from_the_option_that_required_it`、`CapabilityDeclarationSatisfiabilityProductionWiringTests.Declaring_only_the_band_without_the_v_option_is_reported_as_an_incomplete_capability_declaration`（この書き込みガードを経由しない既存データ・別経路に対しても、検査自体は実seedで引き続き機能することの確認）。

証拠・詳細は `docs/phase3-6-acceptance.md` §9。
