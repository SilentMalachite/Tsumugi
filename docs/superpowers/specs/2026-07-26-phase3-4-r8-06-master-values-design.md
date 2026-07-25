# Phase 3-4 設計spec — 令和8年6月施行分（R8-06）の制度実値投入

> **Status**: 設計合意済（2026-07-26）／未着手
> **位置づけ**: Phase 3-3（国保連請求CSV生成）の完了後、Phase 4（リリース準備）に入る前に置く単一スライス。器（schema・resolver・transition rule・readiness・CSV writer）は完成しており、本スライスは **`claim-master-r8-06` の値だけを入れる**。
> **前提文書**: ADR 0020（出典・版）／ADR 0021（体制コード）／ADR 0022（負担上限）／ADR 0025（丸め）／ADR 0027（R6基本報酬・サービスコード・地域単価）／ADR 0028（R6主要加算）／ADR 0023（平均工賃・R8区分）

---

## 1. 結論

令和8年6月改定は **2026-06 に施行済み**であるが、production seed（`src/Tsumugi.Infrastructure/ClaimMasters/Seed/`）は `transition-rules.json` の 1 エントリを除き **`effectiveFrom >= 2026-06` の実値を 1 件も持たない**。本スライスは次の 3 つの穴を、既存の出典・schema・token 語彙の枠内で閉じる。

1. **地域区分単価・負担上限額の R8 妥当性**（出典未登録のまま `effectiveTo: null` で継続中 = 唯一の「エラーにならず古い値で請求が出る」経路）
2. **福祉・介護職員等処遇改善加算の R8 値**（R6 行が 2026-05 で失効済み。全事業所に影響）
3. **R8 改定対象（reform-target）の新12区分**（basic-rewards 180 行＋条件トークン 14 個。器は許可宣言済みで値のみ欠落）

到達点は **「2026-06 以降のサービス提供月について、改定対象・改定対象外を問わず請求プレビューから CSV 生成まで通る」**。加算の種類は増やさない（ADR 0028 が採った「候補家族に限定」の判断を維持する）。

---

## 2. 背景 — 現状の機械的な確認結果（2026-07-26 時点）

### 2.1 production seed の適用期間

| ファイル | entries | `effectiveFrom` → `effectiveTo` |
| --- | ---: | --- |
| `basic-rewards.json` | 135 | 2024-04 → null |
| `service-codes.json` | 151 | 147件 2024-04 → null ／ 4件 2024-06 → **2026-05** |
| `additions.json` | 16 | 12件 2024-04 → null ／ 処遇改善4件 2024-06 → **2026-05** |
| `region-unit-prices.json` | 8 | 2024-04 → null |
| `burden-caps.json` | 4 | 2024-04 → null |
| `transition-rules.json` | 3 | r6-04 / r6-06 / **r8-06（2026-06 → null）** |

`transition-rules.json` の `transition-rule-claim-master-r8-06` は既に `allowedAverageWageBandOptions` に option 1〜22 を、`allowedOptionsByR8ReformStatus["reform-target"]` に option 8・11〜22 を宣言している。**器は R8 を知っており、値だけが無い。**

### 2.2 source inventory から確定した R8 の形

`docs/spec-data/phase3/claim-master-source-row-manifest.json`（schemaVersion 2、44 documents / 14,726 rows）の `disposition: "seed"` 行を masterKind ごとに集約し、R6 側の集合と比較した結果:

| masterKind | R6 targets | R8 targets | 重なり | **R8 のみ** |
| --- | ---: | ---: | ---: | ---: |
| `basic-rewards` | 135 | 315 | 135 | **180** |
| `service-code-conditions` | 34 | 48 | 34 | **14** |
| `additions` | 158 | 278 | 125 | 153 |
| `region-unit-prices` | 8 | **0** | — | — |
| `burden-caps` | 4 | **0** | — | — |
| `transition-rules` | 2 | 1 | 0 | 1（**投入済み**） |

> **注意（実装時に必ず踏むこと）**: manifest の `productionTargets[].seedKey` は `basic-reward-463340` のようなコード由来の命名であり、production seed の実キー（`b-basic.r6.cap-20-or-less.band-45000-plus.staff-6-1`）とは **命名規約が異なる**。上表の「重なり」は同一コードが両版の一次資料に存在することを示すものであって、seed キーの一致ではない。seed キーは ADR 0027 決定1 の token 語彙に従って新規に決める。

### 2.3 構造の検算

- R6: `basic-rewards` 135 = **9 区分 × 15**（capacity 5 × staffing 3）。9 区分は `average-wage-band` 8 トークン（公式 option code 1〜7・9）＋ `payment-band` の `band-participation` 1 トークン。
- R8: 新規 180 = **12 区分 × 15**。R8 のみの条件トークン 14 個の内訳は、新 12 区分の境界 12 個＋ `r8-reform-status` 2 個。

この分解は `basic-rewards.json` に対して機械的に検証済みである（2026-07-26）。

| 次元 | 値 |
| --- | --- |
| `paymentBand` | 9 個 — `band-45000-plus` / `band-35000-45000` / `band-30000-35000` / `band-25000-30000` / `band-20000-25000` / `band-15000-20000` / `band-10000-15000` / `band-under-10000` / `band-participation` |
| `capacityKey` | 5 個 — `cap-20-or-less` / `cap-21-40` / `cap-41-60` / `cap-61-80` / `cap-81-plus` |
| `staffingKey` | 3 個 — `staff-6-1` / `staff-7.5-1` / `staff-10-1` |

135 行は完全な直積であり、**各 `paymentBand` がちょうど 15 行、各（`capacityKey`, `staffingKey`）組合せがちょうど 9 行**を持つ（欠けも重複もない）。R8 の 180 行にも同じ完全性を要求する — 各新区分がちょうど 15 行、各組合せがちょうど 12 行。

| 新区分トークン（manifest 由来の意味） | 想定 option code |
| --- | ---: |
| 平均工賃月額 4万8千円以上 | 11 |
| 4万5千円以上4万8千円未満 | 12 |
| 3万8千円以上4万5千円未満 | 13 |
| 3万5千円以上3万8千円未満 | 14 |
| 3万3千円以上3万5千円未満 | 15 |
| 3万円以上3万3千円未満 | 16 |
| 2万8千円以上3万円未満 | 17 |
| 2万5千円以上2万8千円未満 | 18 |
| 2万3千円以上2万5千円未満 | 19 |
| 2万円以上2万3千円未満 | 20 |
| 1万8千円以上2万円未満 | 21 |
| 1万5千円以上1万8千円未満 | 22 |

**option code の割当ては上表を仮説として扱い、`r8-capability-202606`（体制状況一覧表・令和8年6月版、workbook-order=1;row=242）の選択番号と `r8-b-reward-band-guide` で一意に確定してから seed する。**確定できない割当ては推測で埋めず、当該区分を投入対象から外して `docs/open-questions.md` に起票する。

### 2.4 schema 変更の要否

`src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json` は既に次を持つため、**schema 変更は不要**である。

- `conditionDefinition.kind` の enum に `average-wage-band` と `r8-reform-status` が定義済み（`r8-reform-status` は seed 行がまだ 0 件）
- `basicRewardValues` = `{ paymentBand, staffingKey, capacityKey, serviceCode, baseUnits }`
- `percentageOfTargetAmount` = `{ kind, percentage, applicationKind, percentageBaseScope, targetSelector, calculationOrder }`（処遇改善はこの形）

schema 変更が必要になった場合は、それ自体が「本スライスの想定を外れた」というシグナルであり、実装を止めて設計へ差し戻す。

### 2.5 既に「穴」を名指しして fail-close しているテスト

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs` に次の 3 本がある。本スライスはこれらを起点にする。

| テスト | 現在の主張 | 本スライス後 |
| --- | --- | --- |
| `Reform_target_r8_numeric_options_fail_explicitly_until_their_rows_land` | option 12 × ReformTarget で `ServiceCodeResolutionException(MasterUnavailable)` | **反転** — 正しい service code と単位数を返す |
| `Treatment_improvement_additions_lapse_at_june_2026_until_their_r8_values_land` | 2026-06 に処遇改善 4 行が消える | **反転** — R8 の行が解決する |
| `Basic_reward_rows_continue_unchanged_across_the_r8_boundary` | 135 行が境界を跨いで不変 | **緑のまま維持**（Task A が根拠を付ける） |

---

## 3. スコープ

### 3.1 Task A — 地域区分単価・負担上限額の R8 妥当性を確定する（最優先）

**問題**: `region-unit-prices.json`（8 件）と `burden-caps.json`（4 件）は `effectiveFrom: 2024-04` / `effectiveTo: null` で 2026-06 以降も適用され続ける。しかし `sources.json` に登録されている単価告示は `mhlw-unit-price-notice-observed-946c3d96`（厚生労働省告示第539号）**1 本のみ**、負担上限の出典は `r6-disability-support-guide-202404`（2024年4月版）**1 本のみ**で、**R8 版の出典が登録されていない**。source inventory にも R8 行が 0 件である。

したがって現状は「R8 で改定されていないことを確認した上での継続」ではなく、**「確認しないまま継続している」**。他の 2 ギャップが fail-close するのに対し、これだけが **エラーを出さずに古い値で請求を生成しうる**唯一の経路であり、最優先で答えを出す。

> ADR 0022 が述べる「r6-04 / r6-06 / r7-01 / r7-09 / r8-06 の 5-release source chain」は負担上限の *出典連鎖* を指しており、seed に投入された実値は 2024-04 の 4 件だけである。この 2 つを混同しない。

**手順**:

1. R8 版の一単位単価告示および負担上限額の一次資料を取得し、URL・SHA-256・取得日・publisher を `sources.json` へ登録する（ADR 0020 の登録規律に従う）。
2. `pdftotext -layout` と `-raw` の 2 方式で独立抽出し、就労継続支援の各級地単価および負担区分ごとの上限額を突合する。
3. 結果に応じて分岐する。

| 照合結果 | 対応 |
| --- | --- |
| R6 と同値 | R6 entry の `effectiveTo: null` を維持し、`sourceRefs` に R8 出典を `evidenceRole: "cross-check"` で追記する（継続に根拠を付ける） |
| R6 と異なる | R6 entry を `effectiveTo: "2026-05"` で閉じ、R8 entry を新規追加する |
| **一次資料を取得できない／値を一意に確定できない** | **R6 entry を `effectiveTo: "2026-05"` で閉じて fail-close 側に倒す。** 未確定を `docs/open-questions.md` へ起票する |

3 番目の分岐が本スライスの中核的な設計判断である。**誤った金額の請求を静かに生成するより、生成を止める方が回復可能である。** 事業所は請求を出せないことには気付けるが、単価が古いことには気付けない。

**受け入れ基準（AC3-4-1）**: `region-unit-prices` と `burden-caps` の各 entry が、2026-06 を含む適用期間について「R8 出典に裏付けられた値」または「明示的に閉じられた適用期間」のいずれかになっている。出典なしで 2026-06 に到達する entry が 0 件であることをテストで固定する。

### 3.2 Task B — 福祉・介護職員等処遇改善加算の R8 値

**問題**: `addition.treatment-improvement.unified.i`〜`.iv` の 4 行が `effectiveTo: "2026-05"` で閉じており、対応するサービスコード 4 行も同時に失効する。処遇改善加算は実務上ほぼ全事業所が算定するため、**2026-06 以降は改定対象外の事業所でも請求が成立しない**。ADR 0028 決定7 が「R8-06 の処遇改善は率・コード構成が変わるため本ADRのスコープ外」として明示的に繰り延べた項目である。

**現在判明していること**（`docs/open-questions.md`）: `current-fee-notice-html` で (Ⅰ)イ 105 / (Ⅰ)ロ 109 / (Ⅱ)イ 103 / (Ⅱ)ロ 107 / (Ⅲ) 88 / (Ⅳ) 74（各 1000 分の）を観測済み。R8 新コード構成（465174〜465176 の追加）との対応と `r8-fee-notice` からの正式抽出は未実施。

**手順**:

1. `r8-fee-notice`（令和8年こども家庭庁・厚生労働省告示第5号、SHA `f4b7a05e…`）の就労継続支援B型該当節から率を抽出する。ADR 0028 と同じく `pdftotext -layout` / `-raw` の 2 方式で独立抽出し、全率一致を確認する。
2. `r8-service-codes-2-xlsx`（SHA `307b631e…`）と `r8-service-codes-2-pdf`（SHA `0ff50713…`）の 2 形式独立抽出でサービスコードと合成単位数を突合する。
3. 率と (Ⅰ)イ〜(Ⅳ) の区分の対応、および新コードとの対応が一次資料から一意に確定した行だけを seed する。
4. `additions.json` へ R8 行を `effectiveFrom: "2026-06"` で追加し、`service-codes.json` へ対応する code 行を追加する。`percentageBaseScope` / `targetSelector` / `calculationOrder` / `roundingRuleId` は ADR 0025 の契約に従い、R6 の統一処遇改善行と同じ構造を保つ。

**投入対象を限定する**: source inventory の R8-only `additions` は 153 targets あるが、本スライスは **処遇改善系に限定**する。他の R8-only 加算（就労移行支援体制加算の見直し等）は ADR 0028 が採った「候補家族に限定」の判断を踏襲して非スコープとする（§4 参照）。

**受け入れ基準（AC3-4-2）**: 2026-06 の `ResolveCalculationMasters` が処遇改善加算行を返し、`ClaimCalculatorGoldenCaseTests` の R8 worked example が一次資料由来の期待値と一致する。

### 3.3 Task C — R8 改定対象の新12区分

**問題**: ADR 0027 決定6 が「R8 改定対象の新しい平均工賃月額区分（例:（一）4万8千円以上、（Ａ）4万5千円以上4万8千円未満）の行（サービス費（Ⅰ）で項目3340〜3406等）は本ADRでは抽出しておらず、`claim-master-r8-06` の R8 改定対象行を seed する前に別ADRで確定する」とスコープ外宣言している。`OfficeClaimProfilePolicy` は option 11〜22 の profile 登録を既に許可するが、`ServiceCodeResolver` が対応行を見つけられず `MasterUnavailable` で停止する。

**手順（この順で行う。後段は前段の token に依存する）**:

1. **条件トークン 14 個**を `service-codes.json` の `conditionDefinitions` へ追加する。
   - `average-wage-band` 12 個: `kind: "average-wage-band"` / `operator: "equals"` / `value: <公式 option code>` / `effectiveFrom: "2026-06"`。R6 の 8 トークンと同じ形式（R6 は option code 1〜7・9 を `value` に持つ）。
   - `r8-reform-status` 2 個: `r8-reform-status-in-scope` / `-out-of-scope` 相当。schema の `conditionDefinition.kind` に enum 値は定義済みだが seed 行は 0 件であり、本スライスが最初の利用者になる。
   - キー名は ADR 0027 決定1 の token 語彙に揃える（`band-48000-plus` / `band-45000-48000` 等）。manifest の `average-wage-48000-or-more` 形式をそのまま持ち込まない。
2. **`basic-rewards.json` へ 180 行**を `effectiveFrom: "2026-06"` で追加する。`r8-service-codes-2-xlsx` シート `18就労継続支援(B・基本)` と `r8-service-codes-2-pdf` の 2 形式独立抽出で、サービスコード・合成単位数・算定単位を全行突合する。
3. **`service-codes.json` へ対応する 180 行**を追加する（R6 が basic-rewards 135＋additions 16 = service-codes 151 という 1:1 対応を保っているのと同じ構造）。
4. 区分境界そのものは `r8-b-reward-band-guide`（別添資料 就労継続支援B型の基本報酬区分の基準の見直しについて、SHA `96b002a6…`）で裏を取る。

**検算**: 180 = 12 区分 × 15（capacity 5 × staffing 3）。R6 の 135 = 9 区分 × 15 と同じ 15 組合せ（§2.3 で機械検証済み）。抽出行数がこの積に一致しない場合、または直積が完全でない（ある区分の行数が 15 でない／ある組合せの行数が 12 でない）場合は、抽出漏れか過剰であり seed せず原因を特定する。

**受け入れ基準（AC3-4-3）**: `ReformTarget` × option 11〜22 の全 12 組合せについて、2026-06 の `ResolveBasicReward` が例外を投げずに service code と単位数を返す。R6 数値 option（1〜7・9）を宣言した ReformTarget profile は従来どおり登録段階で拒否され続ける。

### 3.4 Task D — golden case と回帰

1. `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs` に R8 worked example を追加する。ADR 0027/0028 と同じ作法で、ADR に決定表として書いた値を期待値の唯一の出典とする。最低 3 ケース:
   - 改定対象外事業所 × 2026-06（R6 基本報酬継続＋R8 処遇改善）
   - 改定対象事業所 × 2026-06 × 新区分（新 basic reward ＋ R8 処遇改善）
   - 経過措置（option 8 `filed-transition`）× 2026-06
2. `ClaimMasterR8BoundaryTests` の 2 本を反転する（§2.5）。反転は「fail-close の主張を消す」のではなく「fail-close する条件を、値が入っていない別の組合せへ移す」形にし、歯を残す。
3. 版境界の回帰: 2026-05 と 2026-06 の解決結果が意図どおり異なる（または同じ）ことを、Task A の結論に沿って固定する。
4. `ClaimSpecificationBoundaryTests` / `ExternalSpecificationLiteralGuard` が緑であること（制度実値の C# 直書きがないこと）を確認する。ハード制約3 の機械判定。

**受け入れ基準（AC3-4-4）**: `./build/ci.sh` が緑。Domain カバレッジ ≧95%。golden case の期待値がすべて ADR の決定表を参照している。

---

## 4. 非スコープ

本スライスに含めないものと、その理由。

| 項目 | 理由 |
| --- | --- |
| 加算の種類拡張（就労移行支援体制・医療連携体制・重度者支援体制など ADR 0021 の未採録グループ） | ADR 0028 選択肢 C として一度検討し不採用とした判断を維持する。値確定と golden 検証の負荷だけが増える |
| 保護施設事務費（基準該当B型） | ADR 0027 決定6 が明示的に別扱いとし、実値・resolver・runtime 算定がまとめて未実装。独立したスライス |
| PaymentBand 数値境界マスタ（平均工賃→区分の自動導出） | 届出済み option が入力の正であり、導出は矛盾検知のための付加機能。schema 拡張を伴うため分離する |
| 参加評価型（`band-participation`）と体制届 option 10 の R8 状態対応 | 一次資料から確定できず open-questions 継続中。報酬体系Ⅳ〜Ⅵは請求対象外のまま |
| 報酬表の機械抽出パイプライン化 | ADR 0037 が CSV 項目表で採った方式を報酬マスタへ広げる案。有用だが初期コストが大きく、本スライスの「絞る」判断と逆方向。令和9年改定の準備として別途検討する |
| R7-10 より前の CSV 仕様版、共通編 1.6 データ種別対応表の目視確認、証跡台帳 knownGaps | CSV 仕様側の残件であり制度実値とは独立。Phase 4 以降で扱う |
| GUI 手動貫通確認、NuGet suppression のセキュリティ再レビュー | Phase 4（リリース準備）の範囲 |

---

## 5. 全体を貫く規律

### 5.1 出典と抽出

- ハード制約3 に従い、単位数・率・単価・上限額を C# へ直書きしない。すべて seed JSON ＋ `sourceRefs` 経由とする。`ExternalSpecificationLiteralGuard` が Roslyn token 単位で検査する。
- すべての抽出は **2 形式または 2 方式の独立抽出＋全行一致**で行う（ADR 0027/0028 の確立した方式）。xlsx と PDF、または `pdftotext -layout` と `-raw`。一致しない行は seed せず起票する。
- 使用する一次資料は事前に `shasum -a 256` が `sources.json` の登録値と一致することを確認する。不一致時は値を使わず停止する（ADR 0020）。
- 各 entry の `sourceRefs` は `documentId` ＋ `sha256` ＋ `locator` ＋ `evidenceRole` ＋ `supports` を持つ。`locator` は再現可能な粒度（`workbook-order=38;row=1061` / `p.235（第2条表・左欄改正後 第14の17）`）で書く。

### 5.2 エラー処理の方針 — 迷ったら fail-close

本スライスの全タスクに共通する判断規律:

- 一次資料から **(1) 値、(2) 算定単位、(3) 算定条件、(4) サービスコード** の 4 点を一意に確定できた行だけを seed する。1 点でも欠ける行は seed しない。
- 確定できない行の適用期間は **閉じる**（`effectiveTo` を設定する）。開いたまま残して古い値が適用されるのを防ぐ。
- 「入っていない」ことは `MasterUnavailable` 等の明示的な例外として現れ、UI の readiness で利用者に伝わる。**沈黙して古い値を使う経路を作らない。**
- 未確定は必ず `docs/open-questions.md` へ、何が分からないか・どの資料を見れば分かるか・現在の fail-close 挙動を書いて起票する。

### 5.3 追記であって差し替えではない

R6 entry を書き換えない。R8 は新しい `effectiveFrom: "2026-06"` の entry として**追記**し、R6 側は必要に応じて `effectiveTo` を設定して閉じるだけにする。これは ADR 0039 が CSV 仕様版について確立した「適用期間つきで追記して並存させ、処理対象年月で選ぶ」規律と同じ形であり、過去月の再確定・再出力を壊さないために必要である。

---

## 6. テスト戦略

TDD（Red → Green → Refactor）で進める。各タスクは「まず失敗するテストを書く」から始める。

| 層 | テスト | 内容 |
| --- | --- | --- |
| Infrastructure | `ClaimMasterR8BoundaryTests`（既存・拡張） | 版境界の解決、option × reform-status の分割、fail-close の残存範囲 |
| Infrastructure | `JsonClaimMasterProviderCalculationMastersTests`（既存・拡張） | 2026-06 の解決結果に R8 行が含まれること |
| Infrastructure | `ClaimMasterSeedPhase31Tests`（既存・拡張） | seed の schema 適合、キー一意性、`sourceRefs` の完全性 |
| Infrastructure | 新規（Task A） | 2026-06 に到達する全 entry が R8 出典を持つか、適用期間が閉じているかの網羅検査 |
| Domain | `ClaimCalculatorGoldenCaseTests`（既存・拡張） | R8 worked example 3 ケース |
| Infrastructure | `ClaimSpecificationBoundaryTests`（既存・維持） | 制度実値の直書きが増えていないこと |

**歯の確認**: 実装後、意図的に 1 行の単位数を改変して golden case が RED になること、`effectiveTo` を意図的に開いて Task A の網羅検査が RED になることを確認する（CLAUDE.md 運用メモの規律）。

---

## 7. ADR 計画

1 件 1 ファイル、結論 → 背景 → 選択肢 → 決定 → 影響。「暫定 → 確定」ではなく**初手から確定**として書く（確定できない項目だけ open-questions へ）。

| 番号（暫定） | 対象 | 主内容 |
| --- | --- | --- |
| **0044** | Task A | 地域区分単価・負担上限額の R8 適用判断。出典登録、継続か改定かの結論、確定不能時に適用期間を閉じる方針 |
| **0045** | Task B | R8 福祉・介護職員等処遇改善加算の率・サービスコード実値。抽出方式と決定表 |
| **0046** | Task C | R8 改定対象の新12区分。option code 割当て、条件トークン 14 個、basic reward 180 行の決定表 |

> ADR 番号は着手時点の空き番号へ再採番する（現在の最大は 0043）。ADR 0027 決定6・ADR 0028 決定7 の「別ADRで確定する」という繰り延べを、それぞれ 0046・0045 が引き取る形になるため、両 ADR の該当節から新 ADR への参照を追記する。

---

## 8. 成果物

- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json` — R8 単価告示・負担上限資料の登録（Task A）
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json` / `burden-caps.json` — 適用期間の確定（Task A）
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json` / `service-codes.json` — R8 処遇改善（Task B）
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`（`conditionDefinitions` 14 個）/ `basic-rewards.json`（180 行）/ `service-codes.json`（180 行）— 新12区分（Task C）
- `docs/decisions/0044`〜`0046`（番号は着手時に再採番）
- テスト: 上記 §6 の各ファイル
- `docs/open-questions.md` — 該当項目のクローズと、新たな未確定の起票
- `docs/phase3-4-acceptance.md` — 受け入れ証跡（spec/plan からの逸脱と理由もここに書く）
- `CHANGELOG.md` — 本スライスの節
- `CLAUDE.md` — 「現在地」の更新

commit 粒度は TDD の Red → Green → Refactor 単位に割る。末尾コミットで ADR・CHANGELOG・open-questions・acceptance を同期する。

---

## 9. リスクと対応

| リスク | 影響 | 対応 |
| --- | --- | --- |
| **Task A の R8 一次資料が取得できない** | 地域単価・負担上限が確定しない | 適用期間を `2026-05` で閉じて fail-close する。請求は止まるが誤請求は出ない。open-questions へ起票し、資料入手後に別スライスで開ける |
| **新12区分の option code 割当てが一次資料から一意に確定しない** | Task C が部分的にしか閉じない | 確定した区分だけ seed し、残りは `MasterUnavailable` のまま残す。区分ごとに独立して価値が出る構造なので部分完了が成立する |
| **処遇改善の率と新コードの対応が確定しない** | 全事業所で 2026-06 以降が止まったまま | Task B が本スライス最大の価値。ここが閉じない場合は他タスクの完了に関わらずスライス全体を「未達」として扱い、原因を明示して報告する |
| **抽出行数が 12 × 15 = 180 に一致しない** | 抽出漏れまたは過剰 | seed せず原因を特定する。数が合うまで進めない |
| **R8 で 15 組合せ（capacity 5 × staffing 3）自体が変わっている** | §2.3 の検算が前提から崩れる | 検算が合わない時点で設計へ差し戻す。定員区分・人員配置区分の変更は本スライスの想定外であり、スコープの再判断が要る |
| Phase 3-3 より前に確定した請求は snapshot に契約情報等を持たない | 過去分の再出力に再確定が必要 | 既知の制約（ADR 0032・0034）。本スライスでは変えない |

---

## 10. 未確定事項（着手時に決めること）

- Task A の R8 一次資料の所在（単位数単価告示の令和8年6月版、負担上限額の最新版資料）。`sources.json` に既登録の R8 文書群（`r8-fee-notice` / `r8-reward-structure` 等）に含まれている可能性があり、まず既登録文書内を探す。
- 新12区分の option code 割当て（§2.3 の表は仮説）。`r8-capability-202606` の workbook-order=1;row=242 が第一候補の典拠。
- R8 の `r8-reform-status` 条件トークンのキー命名。ADR 0027 決定1 の語彙規約に従うが、`r8-reform-status` kind の seed 前例が無いため本スライスが規約を作る。

---

## 11. 参照

- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` — Phase 4 残余（S2〜S5）。本スライスの完了後に着手する
- `docs/decisions/0027-r6-basic-reward-service-code-region-price-values.md` 決定6 — Task C が引き取る繰り延べ
- `docs/decisions/0028-r6-major-addition-values.md` 決定7 — Task B が引き取る繰り延べ
- `docs/decisions/0020-claim-master-sources-and-versioning.md` — 出典登録と再検証の規律
- `docs/decisions/0025-claim-rounding-rules.md` — 割合加算の source row 契約
- `docs/spec-data/phase3/claim-master-source-row-manifest.json` — source inventory
- `docs/open-questions.md` — クローズ対象と新規起票先
