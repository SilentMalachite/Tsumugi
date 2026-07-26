# Phase 3-6 実装計画 — R6-06処遇改善の施設variant・(Ⅴ)実値投入・体制届optionの入力と存在検査

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development`（推奨）または `superpowers:executing-plans` でタスク単位に実装すること。ステップは checkbox (`- [ ]`) で進捗を管理する。**進捗の正本はこのチェックボックス。**

**Goal:** R6-06世代（2024-06〜2026-05）の処遇改善加算に施設区分variantと(Ⅴ)14区分を実値投入し、公式体制届キーの入力面を作り、「届け出たのに当月マスタ行が無い」を警告として検出する。

**Architecture:** seed JSON（`additions.json` / `service-codes.json`）へ出典付きで行と条件定義を追加し、既存R6通常3行へ非施設条件を付ける。体制届の選択肢は語彙をUIへハードコードせず月の条件定義から導出する。存在検査はDomainの純粋関数として実装し、Applicationが非ブロッキング警告として合流させる。

**Tech Stack:** .NET 10 / C# 14、Avalonia 11、EF Core 10（本計画ではmigration不要）、xUnit ＋ FluentAssertions。

## Global Constraints

- **spec正本**: `docs/superpowers/specs/2026-07-26-phase3-6-r6-06-treatment-improvement-design.md`。逸脱は証跡へ理由付きで記録する。
- **制度実値をC#へ直書きしない**（CLAUDE.md ハード制約3）。値・選択番号・サービスコードは seed JSON にのみ置く。`ExternalSpecificationLiteralGuard` / `ClaimSpecificationBoundaryTests` が Roslyn token単位で拒否する。**テストコード内のサービスコード文字列は既存 `ClaimMasterR8BoundaryTests` と同じ扱い（テストは対象外）。**
- **依存方向**: `App → Application → Domain`、`Infrastructure → Application/Domain`。DomainはEF/Avalonia/Infrastructureを知らない。
- **条件定義・行・参照は必ず1コミットで揃える**。`ClaimMasterFileValidator` の「未参照のconditionDefinitionはfail-close」により、条件定義だけ先行コミットすると `JsonClaimMasterProvider.LoadEmbedded()` を呼ぶ既存テストが一斉に赤くなる（`docs/phase3-4-acceptance.md` §5-3 の前例）。
- **`conditionSelectors` は末尾へ追加する**。`MatchesAll` が `.All` の短絡評価のため、施設条件を先頭に置くと体制届未提出の事業所まで `FacilityClassificationUnresolved` で落ちる。既存R8行の順（`reward-system...` → `capability-...` → `facility-classification-...`）に揃える。
- **`percentage` は文字列**（`"0.104"`）。既存行と同じ表記で、末尾ゼロを削らない。
- **locator に率の数値そのものを埋めない**（`docs/phase3-5-acceptance.md` §9 の deferred minor を繰り返さない）。位置と括弧書きの引用に留める。
- 各タスクの最後に `dotnet build`（警告ゼロ）と関連テストの緑を確認してからコミットする。全タスク完了後に `./build/ci.sh` を通す。

---

## Task 0: 一次資料の再取得と SHA-256 照合

**Files:** なし（検証のみ）

**Interfaces:**
- Produces: 後続タスクが locator を書く根拠。ここで SHA が合わなければ**以降のタスクに進んではならない**。

- [ ] **Step 1: 4文書を取得してハッシュを照合する**

作業ディレクトリは任意の一時領域でよい（リポジトリへコミットしない）。

```bash
mkdir -p /tmp/phase36 && cd /tmp/phase36
curl -sS -L -o r6-sc2.xlsx "https://www.mhlw.go.jp/content/12200000/20241129010.xlsx"
curl -sS -L -o r6-sc2.pdf  "https://www.mhlw.go.jp/content/12200000/20241129007.pdf"
curl -sS -L -o r6-fee.pdf  "https://www.mhlw.go.jp/content/001239565.pdf"
curl -sS -L -o r8-fee.pdf  "https://www.mhlw.go.jp/content/001684450.pdf"
shasum -a 256 r6-sc2.xlsx r6-sc2.pdf r6-fee.pdf r8-fee.pdf
```

期待値（`sources.json` 登録値と完全一致すること）:

```
4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82  r6-sc2.xlsx
708270200599de9fb7d15d7270997286c3671d378e0a00e3b186a946e67b4465  r6-sc2.pdf
5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54  r6-fee.pdf
f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c  r8-fee.pdf
```

- [ ] **Step 2: 率をPDFから再確認する（2方式）**

```bash
cd /tmp/phase36
for m in -layout -raw; do
  echo "== $m =="
  pdftotext $m -f 235 -l 238 r6-fee.pdf - | grep -o "1000分の[0-9]*に相当" | sort -u | head -40
done
```

物理235頁に `93` と `104`、236頁に `91` `76` `86` `62` `69` `80` `91` `87` `79` `78`、237頁に `77` `66` `74` `64` `61` `66` `63` `73` `59` `48` `53` `49` `56` `46` `44` `48`、238頁に `31` `35` が現れること。

- [ ] **Step 3: サービスコードを2形式で照合する**

```bash
cd /tmp/phase36
pdftotext -layout -f 259 -l 259 r6-sc2.pdf - | grep -oE "^ *46 +51[2-5][0-9]" | awk '{print $2}' | sort -u | tr '\n' ' '
```

次の30件がちょうど現れること（過不足があれば止めて報告する）:

```
5120 5121 5122 5123 5124 5125 5126 5127 5128 5129 5130 5131 5132 5133 5134
5135 5136 5137 5138 5140 5141 5142 5143 5146 5148 5149 5151 5152 5154 5155
```

- [ ] **Step 4: コミットは無し**

検証のみのタスク。ファイル変更が無いことを `git status` で確認する。

---

## Task 1: R6-06 施設区分variant（Ⅰ・Ⅲ・Ⅳ）の投入

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs`

**Interfaces:**
- Produces: 条件定義キー `facility-classification-general-r6-06` / `facility-classification-designated-support-facility-r6-06`（Task 2 が(Ⅴ)行から参照する）。加算キー接頭辞 `addition.treatment-improvement.unified.`（Task 2 も同じ接頭辞を使う）。

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs` を新規作成する。

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// R6-06世代（2024-06〜2026-05）の処遇改善加算における指定障害者支援施設variantを
/// production seedで固定する（ADR 0048）。Phase 3-5がR8-06について塞いだ欠陥と
/// 同一クラスの欠陥がR6世代に残っていたものを解消する。
/// </summary>
public sealed class ClaimMasterR6FacilityTests
{
    private static readonly ServiceMonth June2024 = new(2024, 6);

    private static readonly JsonClaimMasterProvider Provider =
        JsonClaimMasterProvider.LoadEmbedded();

    /// <summary>
    /// R6世代の処遇改善加算family（通常・施設の両方）だけへ絞り込む。
    /// <c>ResolveAdditions</c>は条件に一致する加算行を<b>すべて</b>返すため、
    /// 欠席時対応加算のようなreward-system条件しか持たない行が常に混入する。
    /// </summary>
    private static ResolvedUnitAddition[] TreatmentImprovementRows(
        IReadOnlyList<ResolvedUnitAddition> rows) => rows
        .Where(row => row.AdjustmentComponentKey.StartsWith(
            "addition.treatment-improvement.unified.", StringComparison.Ordinal))
        .ToArray();

    private static ClaimBillingConditionContext Context(
        int officialOptionCode, string? facilityClassification) => new(
        RewardSystem: "employment-continuation-support-b",
        PaymentBand: "",
        CapacityHeadcount: 15,
        StaffingKey: "staff-6-1",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
        OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal)
        {
            $"mhlw.b46.capability.treatment-improvement.{officialOptionCode}",
        },
        FacilityClassification: facilityClassification);

    /// <summary>
    /// ADR 0048: 施設variantを持つ3区分（体制届option 2・4・5）は施設区分ごとに
    /// ちょうど1行へ解決する。通常行と施設行の両方が一致する状態は条件の付け方を
    /// 誤った証拠（＝二重計上）であり、ここで検出する。
    /// </summary>
    [Theory]
    [InlineData(2, "465120", "465138")]
    [InlineData(4, "465122", "465140")]
    [InlineData(5, "465123", "465141")]
    public void Facility_variants_resolve_to_exactly_one_row_per_classification(
        int officialOptionCode, string generalCode, string facilityCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var general = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            masters, June2024, Context(officialOptionCode, "general")));
        var facility = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            masters, June2024, Context(officialOptionCode, "designated-support-facility")));

        general.Should().ContainSingle(
            $"非施設 × option {officialOptionCode} は通常行だけに一致する")
            .Which.ServiceCode.Should().Be(generalCode);
        facility.Should().ContainSingle(
            $"施設 × option {officialOptionCode} は施設行だけに一致する")
            .Which.ServiceCode.Should().Be(facilityCode);
    }

    /// <summary>
    /// ADR 0048: (Ⅱ)（option 3）は告示に括弧書きが無くサービスコードも存在しないため
    /// 施設区分条件を付けない。施設事業所でも通常行へ解決しなければならない
    /// （条件を付けると施設事業所が算定できなくなる＝無音の未算定）。
    /// </summary>
    [Theory]
    [InlineData("general")]
    [InlineData("designated-support-facility")]
    public void Tier_two_has_no_facility_variant_and_resolves_for_both_classifications(
        string classification)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var rows = TreatmentImprovementRows(
            ServiceCodeResolver.ResolveAdditions(masters, June2024, Context(3, classification)));

        rows.Should().ContainSingle("(Ⅱ)は施設別立てが無く施設区分に依らず1行へ解決する")
            .Which.ServiceCode.Should().Be("465121");
    }

    /// <summary>
    /// 施設区分が未入力（null）のとき、施設variantを持つ区分は推測で通常行へ倒さず
    /// 専用コードでフェイルクローズする（ADR 0047の方針をR6世代へ適用）。
    /// </summary>
    [Fact]
    public void An_unresolved_facility_classification_fails_closed()
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var act = () => ServiceCodeResolver.ResolveAdditions(
            masters, June2024, Context(2, facilityClassification: null));

        act.Should().Throw<ServiceCodeResolutionException>()
            .Which.ErrorCode.Should().Be(
                ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved);
    }

    /// <summary>
    /// 施設variantはR6世代（2024-06〜2026-05）に閉じる。2026-06以降はADR 0047が
    /// 投入したR8世代の行が担うため、R6キーが漏れ出さないことを固定する。
    /// </summary>
    [Fact]
    public void R6_facility_rows_do_not_reach_june_2026()
    {
        var june2026 = Provider.ResolveCalculationMasters(new ServiceMonth(2026, 6));

        june2026.ServiceCodes
            .Select(row => row.Key)
            .Where(key => key.StartsWith("b-addition.r6-06.treatment-improvement.", StringComparison.Ordinal))
            .Should().BeEmpty("R6世代の処遇改善行は2026-05で終了する");
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~ClaimMasterR6FacilityTests"
```

期待: `Facility_variants_resolve_to_exactly_one_row_per_classification` と `An_unresolved_facility_classification_fails_closed` が FAIL（施設行と施設条件がまだ無いため）。`Tier_two_...` と `R6_facility_rows_do_not_reach_june_2026` は PASS してよい。

- [ ] **Step 3: 条件定義2件を `service-codes.json` の `conditionDefinitions` へ追加する**

配列の末尾へ追加する。

```json
{
  "key": "facility-classification-general-r6-06",
  "effectiveFrom": "2024-06",
  "effectiveTo": "2026-05",
  "kind": "facility-classification",
  "operator": "equals",
  "value": "general",
  "sourceRefs": [
    {
      "documentId": "r6-service-codes-2-xlsx",
      "sha256": "4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82",
      "locator": "workbook-order=38;row=1061（追加条件欄が空＝指定障害者支援施設以外）",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    },
    {
      "documentId": "r6-fee-notice",
      "sha256": "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54",
      "locator": "物理235頁（第2条表・左欄改正後 第14の17 イ 本文の率。括弧書きの外側）",
      "evidenceRole": "cross-check",
      "supports": ["conditions"]
    }
  ]
},
{
  "key": "facility-classification-designated-support-facility-r6-06",
  "effectiveFrom": "2024-06",
  "effectiveTo": "2026-05",
  "kind": "facility-classification",
  "operator": "equals",
  "value": "designated-support-facility",
  "sourceRefs": [
    {
      "documentId": "r6-service-codes-2-xlsx",
      "sha256": "4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82",
      "locator": "workbook-order=38;row=1062（追加条件欄「指定障害者支援施設において行った場合」）",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    },
    {
      "documentId": "r6-fee-notice",
      "sha256": "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54",
      "locator": "物理235頁（第2条表・左欄改正後 第14の17 イ 括弧書き「指定障害者支援施設にあっては、…に相当する単位数」）",
      "evidenceRole": "cross-check",
      "supports": ["conditions"]
    }
  ]
}
```

- [ ] **Step 4: 施設variant 3件を `additions.json` の `entries` へ追加する**

以下は (Ⅰ)施設 の完全形。`entries` 配列の末尾へ追加する。

```json
{
  "key": "addition.treatment-improvement.unified.i.facility",
  "effectiveFrom": "2024-06",
  "effectiveTo": "2026-05",
  "sourceRefs": [
    {
      "documentId": "r6-fee-notice",
      "sha256": "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54",
      "locator": "物理235頁（第2条表・左欄改正後 第14の17 イ 括弧書き「指定障害者支援施設にあっては、…に相当する単位数」）",
      "evidenceRole": "authoritative",
      "supports": ["master-values", "effective-period"]
    },
    {
      "documentId": "r6-service-codes-2-xlsx",
      "sha256": "4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82",
      "locator": "workbook-order=38;row=1062",
      "evidenceRole": "cross-check",
      "supports": ["master-values", "effective-period"]
    },
    {
      "documentId": "r6-service-codes-2-pdf",
      "sha256": "708270200599de9fb7d15d7270997286c3671d378e0a00e3b186a946e67b4465",
      "locator": "p.259",
      "evidenceRole": "cross-check",
      "supports": ["master-values", "effective-period"]
    },
    {
      "documentId": "r6-calculation-note",
      "sha256": "958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1",
      "locator": "p.8〜9 単位数の端数処理（割合加算の四捨五入・ADR 0025）",
      "evidenceRole": "authoritative",
      "supports": ["unit-rule-rounding"]
    }
  ],
  "values": {
    "amount": {
      "kind": "percentage-of-target",
      "percentage": "0.104",
      "applicationKind": "add",
      "percentageBaseScope": "monthly-target-unit-sum",
      "targetSelector": "target.b46.items-1-to-16-4.v1",
      "calculationOrder": 5
    },
    "calculationStepId": "claim.step.units.monthly-target.percentage.v1",
    "roundingRuleId": "claim.rounding.units.half-up.v1",
    "billingUnit": "per-month"
  }
}
```

(Ⅲ)施設・(Ⅳ)施設 は上の完全形の以下4箇所だけを差し替えた同形のエントリを作る。他のフィールドは1文字も変えない。

| key | `percentage` | `calculationOrder` | xlsx `locator` | `r6-fee-notice` locator の項番 |
|---|---|---|---|---|
| `addition.treatment-improvement.unified.iii.facility` | `"0.086"` | `6` | `workbook-order=38;row=1066` | 物理236頁 ハ |
| `addition.treatment-improvement.unified.iv.facility` | `"0.069"` | `7` | `workbook-order=38;row=1068` | 物理236頁 二 |

- [ ] **Step 5: 施設variantのサービスコード3件を `service-codes.json` の `entries` へ追加する**

以下は (Ⅰ)施設 の完全形。`entries` 配列の末尾へ追加する。

```json
{
  "key": "b-addition.r6-06.treatment-improvement.unified.i.facility",
  "effectiveFrom": "2024-06",
  "effectiveTo": "2026-05",
  "sourceRefs": [
    {
      "documentId": "r6-service-codes-2-xlsx",
      "sha256": "4a191158daa7dbe8b29c10e966a01960ca8267a747546220aad666dfa5ef5a82",
      "locator": "workbook-order=38;row=1062（注記: 令和6年6月1日から算定可能）",
      "evidenceRole": "authoritative",
      "supports": ["service-identity", "selectors", "unit-rule-kind", "unit-rule-step", "unit-rule-target", "effective-period", "conditions"]
    },
    {
      "documentId": "r6-fee-notice",
      "sha256": "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54",
      "locator": "物理235頁（第2条表・左欄改正後 第14の17 イ 括弧書き）",
      "evidenceRole": "authoritative",
      "supports": ["unit-rule-value"]
    },
    {
      "documentId": "r6-service-codes-2-pdf",
      "sha256": "708270200599de9fb7d15d7270997286c3671d378e0a00e3b186a946e67b4465",
      "locator": "p.259",
      "evidenceRole": "cross-check",
      "supports": ["service-identity", "selectors", "unit-rule-kind", "unit-rule-step", "unit-rule-target", "effective-period", "conditions"]
    },
    {
      "documentId": "r6-calculation-note",
      "sha256": "958f9868e4527c27fd050676879b8e6c88b477dbf71c01d5721b7af0bc4f35e1",
      "locator": "p.8〜9 単位数の端数処理（割合加算の四捨五入・ADR 0025）",
      "evidenceRole": "authoritative",
      "supports": ["unit-rule-rounding"]
    }
  ],
  "values": {
    "serviceCode": "465138",
    "officialLabel": "福祉・介護職員等処遇改善加算(Ⅰ)（指定障害者支援施設において行った場合）",
    "serviceKind": "employment-continuation-support-b",
    "selectors": ["selector:b-addition.r6-06.treatment-improvement.unified.i.facility"],
    "conditionSelectors": [
      "reward-system-employment-continuation-support-b",
      "capability-treatment-improvement-i",
      "facility-classification-designated-support-facility-r6-06"
    ],
    "unitRule": {
      "kind": "unit-addition",
      "adjustmentComponentKey": "addition.treatment-improvement.unified.i.facility",
      "amount": {
        "kind": "percentage-of-target",
        "percentage": "0.104",
        "applicationKind": "add",
        "percentageBaseScope": "monthly-target-unit-sum",
        "targetSelector": "target.b46.items-1-to-16-4.v1",
        "calculationOrder": 5
      },
      "calculationStepId": "claim.step.units.monthly-target.percentage.v1",
      "roundingRuleId": "claim.rounding.units.half-up.v1",
      "billingUnit": "per-month"
    },
    "componentRefs": [
      { "masterKind": "additions", "key": "addition.treatment-improvement.unified.i.facility", "role": "adjustment" }
    ]
  }
}
```

(Ⅲ)施設・(Ⅳ)施設 は上の完全形の以下だけを差し替える。

| 項目 | (Ⅲ)施設 | (Ⅳ)施設 |
|---|---|---|
| `key` / `selectors` / `adjustmentComponentKey` / `componentRefs[0].key` の語幹 | `...unified.iii.facility` | `...unified.iv.facility` |
| `serviceCode` | `"465140"` | `"465141"` |
| `officialLabel` | `福祉・介護職員等処遇改善加算(Ⅲ)（指定障害者支援施設において行った場合）` | `福祉・介護職員等処遇改善加算(Ⅳ)（指定障害者支援施設において行った場合）` |
| `conditionSelectors[1]` | `capability-treatment-improvement-iii` | `capability-treatment-improvement-iv` |
| `percentage`（2箇所） | `"0.086"` | `"0.069"` |
| `calculationOrder`（2箇所） | `6` | `7` |
| xlsx `locator` | `workbook-order=38;row=1066（注記: 令和6年6月1日から算定可能）` | `workbook-order=38;row=1068（注記: 令和6年6月1日から算定可能）` |
| `r6-fee-notice` locator | `物理236頁（第2条表・左欄改正後 第14の17 ハ 括弧書き）` | `物理236頁（第2条表・左欄改正後 第14の17 二 括弧書き）` |

- [ ] **Step 6: 既存の通常3行へ非施設条件を追加する**

`service-codes.json` の既存エントリ `b-addition.r6-06.treatment-improvement.unified.i` / `.iii` / `.iv` の `values.conditionSelectors` の**末尾**へ `"facility-classification-general-r6-06"` を追加する。

変更後（`.i` の例）:

```json
"conditionSelectors": [
  "reward-system-employment-continuation-support-b",
  "capability-treatment-improvement-i",
  "facility-classification-general-r6-06"
]
```

**`b-addition.r6-06.treatment-improvement.unified.ii` は変更しない。**

- [ ] **Step 7: テストが通ることを確認する**

```bash
dotnet build
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~ClaimMaster"
```

期待: `ClaimMasterR6FacilityTests` 全件 PASS。既存の `ClaimMasterR8BoundaryTests` / `ClaimMasterSeedPhase31Tests` / `JsonClaimMasterProviderTests` / `ClaimAdditionSeedScopeTests` も PASS。

既存テストが `unified.{i,iii,iv}` の `conditionSelectors` を直接assertしていて赤くなった場合は、**テスト側の期待値へ `facility-classification-general-r6-06` を追加して直す**（実装を戻さない。条件追加は仕様どおりの変更である）。

- [ ] **Step 8: 全体テストを流す**

```bash
dotnet test
```

期待: 全緑。赤があれば原因を切り分けてから次へ進む。

- [ ] **Step 9: 歯の確認**

以下を一時的に加えて RED を確認し、確認後に元へ戻す。戻したあと `git diff --stat` が空であることを確認する。

| 変更 | 対象 | 期待 |
|---|---|---|
| `additions.json` の `unified.i.facility` の `percentage` を `"0.104"`→`"0.105"` | `Facility_variants_resolve_to_exactly_one_row_per_classification` は率を見ないので**PASSのまま**。`service-codes.json` 側も同時に `"0.105"` にすると構造検証も通る | 率だけを守るテストが無いことをここで確認し、Task 2 Step 12 の率固定テストで担保する |
| `service-codes.json` の `unified.i` から `facility-classification-general-r6-06` を削除 | `Facility_variants_resolve_to_exactly_one_row_per_classification`(option 2) | RED（施設contextで通常行と施設行の2行に一致し `ContainSingle` が失敗） |
| `service-codes.json` の `unified.i.facility` エントリを削除 | 同上 | RED（施設contextで0行） |

- [ ] **Step 10: コミット**

```bash
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
        src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs
git commit -m "feat(phase3-6): R6-06処遇改善の指定障害者支援施設variant3区分を投入する

告示の括弧書き(物理235-236頁)とサービスコード表(xlsx行1062/1066/1068、
pdf物理259頁)の2形式独立照合により、(Ⅰ)104/465138・(Ⅲ)86/465140・
(Ⅳ)69/465141を確定した。(Ⅱ)は括弧書きもコードも存在しないため対象外。

通常3行へ非施設条件を同時に付与する。片側だけの投入は施設事業所が
2行に一致して二重計上になるため成立しない(ADR 0047と同じ方式)。

この変更により2024-06〜2026-05は施設区分の入力が必須になり、
未入力はFacilityClassificationUnresolvedでフェイルクローズする。"
```

---

## Task 2: 処遇改善(Ⅴ) 14区分と施設variant 9区分の投入

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs`

**Interfaces:**
- Consumes: Task 1 の `facility-classification-{general,designated-support-facility}-r6-06`。
- Produces: 体制届キー `mhlw.b46.capability.treatment-improvement.6` と `mhlw.b46.capability.treatment-improvement-v-band.{1..14}`（Task 3 が選択肢として導出し、Task 5 が存在検査の対象にする）。

- [ ] **Step 1: 失敗するテストを書く**

`ClaimMasterR6FacilityTests.cs` へ以下を追記する。

```csharp
    private static ClaimBillingConditionContext VContext(
        int subdivision, string? facilityClassification) => new(
        RewardSystem: "employment-continuation-support-b",
        PaymentBand: "",
        CapacityHeadcount: 15,
        StaffingKey: "staff-6-1",
        AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3),
        R8ReformStatus: R8ReformStatus.NotApplicableBeforeR8,
        OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal)
        {
            "mhlw.b46.capability.treatment-improvement.6",
            $"mhlw.b46.capability.treatment-improvement-v-band.{subdivision}",
        },
        FacilityClassification: facilityClassification);

    /// <summary>
    /// ADR 0048: (Ⅴ)の14サブ区分は通常事業所で全件解決する。
    /// </summary>
    [Theory]
    [InlineData(1, "465124")]
    [InlineData(2, "465125")]
    [InlineData(3, "465126")]
    [InlineData(4, "465127")]
    [InlineData(5, "465128")]
    [InlineData(6, "465129")]
    [InlineData(7, "465130")]
    [InlineData(8, "465131")]
    [InlineData(9, "465132")]
    [InlineData(10, "465133")]
    [InlineData(11, "465134")]
    [InlineData(12, "465135")]
    [InlineData(13, "465136")]
    [InlineData(14, "465137")]
    public void Category_v_subdivisions_resolve_for_a_general_office(
        int subdivision, string expectedCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var rows = TreatmentImprovementRows(
            ServiceCodeResolver.ResolveAdditions(masters, June2024, VContext(subdivision, "general")));

        rows.Should().ContainSingle($"(Ⅴ)⑵{subdivision}は通常行だけに一致する")
            .Which.ServiceCode.Should().Be(expectedCode);
    }

    /// <summary>
    /// ADR 0048: (Ⅴ)のうち施設variantを持つ9サブ区分は施設事業所で施設行へ解決する。
    /// 告示の括弧書きの有無とサービスコード表の欠番が一致することを根拠とする。
    /// </summary>
    [Theory]
    [InlineData(1, "465142")]
    [InlineData(2, "465143")]
    [InlineData(5, "465146")]
    [InlineData(7, "465148")]
    [InlineData(8, "465149")]
    [InlineData(10, "465151")]
    [InlineData(11, "465152")]
    [InlineData(13, "465154")]
    [InlineData(14, "465155")]
    public void Category_v_facility_variants_resolve_for_a_facility_office(
        int subdivision, string expectedFacilityCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        var rows = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            masters, June2024, VContext(subdivision, "designated-support-facility")));

        rows.Should().ContainSingle($"(Ⅴ)⑵{subdivision}施設は施設行だけに一致する")
            .Which.ServiceCode.Should().Be(expectedFacilityCode);
    }

    /// <summary>
    /// ADR 0048: 施設variantを持たない5サブ区分（⑶⑷⑹⑼⑿）は施設事業所でも
    /// 通常行へ解決する。条件を付けると施設事業所が算定できなくなる。
    /// </summary>
    [Theory]
    [InlineData(3, "465126")]
    [InlineData(4, "465127")]
    [InlineData(6, "465129")]
    [InlineData(9, "465132")]
    [InlineData(12, "465135")]
    public void Category_v_subdivisions_without_a_facility_variant_resolve_for_both(
        int subdivision, string expectedCode)
    {
        var masters = Provider.ResolveCalculationMasters(June2024);

        foreach (var classification in new[] { "general", "designated-support-facility" })
        {
            var rows = TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
                masters, June2024, VContext(subdivision, classification)));

            rows.Should().ContainSingle(
                $"(Ⅴ)⑵{subdivision}は施設別立てが無く{classification}でも通常行へ解決する")
                .Which.ServiceCode.Should().Be(expectedCode);
        }
    }

    /// <summary>
    /// ADR 0048: (Ⅴ)は令和7年3月31日限りで失効する。r6-fee-noticeの規定本文が
    /// 期限を明記し、r8-fee-noticeが当該規定を「（削る）」として削除し、
    /// R8サービスコード表にも465124〜465137が存在しないことによる。
    /// </summary>
    [Fact]
    public void Category_v_expires_at_the_end_of_march_2025()
    {
        var march = Provider.ResolveCalculationMasters(new ServiceMonth(2025, 3));
        var april = Provider.ResolveCalculationMasters(new ServiceMonth(2025, 4));

        TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            march, new ServiceMonth(2025, 3), VContext(1, "general")))
            .Should().ContainSingle("2025-03は(Ⅴ)が有効な最終月");

        TreatmentImprovementRows(ServiceCodeResolver.ResolveAdditions(
            april, new ServiceMonth(2025, 4), VContext(1, "general")))
            .Should().BeEmpty("2025-04以降に(Ⅴ)は存在しない");
    }

    /// <summary>
    /// ADR 0048 完全性: R6-06世代の処遇改善サービスコードは、r6-service-codes-2-pdf
    /// 物理259頁に現れる type-46 処遇改善コード30件と過不足なく一致する。
    /// 集合の上限（余分な行の混入）と下限（投入漏れ）の両方をここで固定する。
    /// </summary>
    [Fact]
    public void The_r6_treatment_improvement_codes_match_the_official_table_exactly()
    {
        string[] expected =
        [
            "465120", "465121", "465122", "465123", "465124", "465125", "465126",
            "465127", "465128", "465129", "465130", "465131", "465132", "465133",
            "465134", "465135", "465136", "465137", "465138", "465140", "465141",
            "465142", "465143", "465146", "465148", "465149", "465151", "465152",
            "465154", "465155",
        ];

        var actual = Provider.ResolveCalculationMasters(June2024).ServiceCodes
            .Where(row => row.Key.StartsWith(
                "b-addition.r6-06.treatment-improvement.", StringComparison.Ordinal))
            .Select(row => row.ServiceCode)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        actual.Should().BeEquivalentTo(expected,
            "r6-service-codes-2-pdf 物理259頁の type-46 処遇改善コードは30件ちょうど");
    }

    /// <summary>
    /// ADR 0048: 率は告示（r6-fee-notice 物理235〜238頁）の値そのものでなければならない。
    /// </summary>
    [Theory]
    [InlineData("addition.treatment-improvement.unified.i.facility", "0.104")]
    [InlineData("addition.treatment-improvement.unified.iii.facility", "0.086")]
    [InlineData("addition.treatment-improvement.unified.iv.facility", "0.069")]
    [InlineData("addition.treatment-improvement.unified.v-1", "0.080")]
    [InlineData("addition.treatment-improvement.unified.v-1.facility", "0.091")]
    [InlineData("addition.treatment-improvement.unified.v-14", "0.031")]
    [InlineData("addition.treatment-improvement.unified.v-14.facility", "0.035")]
    public void R6_treatment_improvement_percentages_match_the_notice(
        string additionKey, string expectedPercentage)
    {
        var row = Provider.ResolveCalculationMasters(June2024).UnitAdjustments
            .Should().ContainSingle(r => r.Key == additionKey).Subject;

        row.Amount.Should().BeOfType<PercentageOfTargetAmount>()
            .Which.Percentage.Should().Be(decimal.Parse(
                expectedPercentage, System.Globalization.CultureInfo.InvariantCulture));
    }
```

> **注意**: `UnitAdjustmentMasterRow` の率へのアクセス経路（`row.Amount` の型名・プロパティ名）は実装で確認すること。`src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs` に定義がある。型名が `PercentageOfTargetAmount` でない場合は実型に合わせ、**アサーションの意味（率が期待値と一致する）は変えない**。

- [ ] **Step 2: テストが失敗することを確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~ClaimMasterR6FacilityTests"
```

期待: (Ⅴ)関連の全テストと `The_r6_treatment_improvement_codes_match_the_official_table_exactly` が FAIL。

- [ ] **Step 3: (Ⅴ)の体制届条件定義15件を `service-codes.json` の `conditionDefinitions` へ追加する**

まず option 6（(Ⅴ)そのもの）。

```json
{
  "key": "capability-treatment-improvement-v",
  "effectiveFrom": "2024-06",
  "effectiveTo": "2025-03",
  "kind": "office-capability",
  "operator": "equals",
  "value": "mhlw.b46.capability.treatment-improvement.6",
  "sourceRefs": [
    {
      "documentId": "r6-capability-202406",
      "sha256": "d1edf9715b8c41660d6e4278ebd886861d0758c75109e4efc594f5d70f197c50",
      "locator": "基本情報シート258行 福祉・介護職員等処遇改善加算（38列目の表示文字列「１．なし　２．Ⅰ　３．Ⅱ　４．Ⅲ　５．Ⅳ　６．Ⅴ」の 6=(Ⅴ)）",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    },
    {
      "documentId": "r6-fee-notice",
      "sha256": "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54",
      "locator": "物理236〜238頁（第2条表・左欄改正後 第14の17 注2 ホ 区分。本文が「令和７年３月31日までの間」と期限を定める）",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    }
  ]
}
```

続いてサブ区分14件。`{n}` は `1`〜`14`、`{sub}` は `⑴`〜`⒁` の対応する記号（1=⑴, 2=⑵, 3=⑶, 4=⑷, 5=⑸, 6=⑹, 7=⑺, 8=⑻, 9=⑼, 10=⑽, 11=⑾, 12=⑿, 13=⒀, 14=⒁）。14件すべてを展開して書く。

```json
{
  "key": "capability-treatment-improvement-v-band-{n}",
  "effectiveFrom": "2024-06",
  "effectiveTo": "2025-03",
  "kind": "office-capability",
  "operator": "equals",
  "value": "mhlw.b46.capability.treatment-improvement-v-band.{n}",
  "sourceRefs": [
    {
      "documentId": "r8-capability-correction",
      "sha256": "06414c8aad4c014f44fd211dac141d152f30135fb622cdd32874e1c6bccbd980",
      "locator": "物理9頁（印字頁番号なし） 就労継続支援Ｂ型 福祉・介護職員等処遇改善加算（Ⅴ）区分（選択肢一覧「{n}．Ｖ（{n}）」を含む）",
      "evidenceRole": "authoritative",
      "supports": ["conditions"]
    },
    {
      "documentId": "r6-fee-notice",
      "sha256": "5d32a1fa54d928be5c91861ecf68490e820768a93b9923a5d8b342c267351d54",
      "locator": "物理236〜238頁（第2条表・左欄改正後 第14の17 注2 ホ {sub} 区分）",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    }
  ]
}
```

> **なぜ選択肢の出典が `r8-capability-correction` なのか**: (Ⅴ)区分の選択肢を選択番号つきで列挙している登録済み資料はこれのみである。同文書は令和8年6月版だが、列挙している(Ⅴ)区分の選択番号体系（1〜14 が ⑴〜⒁ に対応）は R6 の告示の区分と1対1で一致する。期間の根拠は `r6-fee-notice` 側が担う（`supports` を出典ごとに分けているのはこのため）。

- [ ] **Step 4: (Ⅴ)通常14件を `additions.json` へ追加する**

Task 1 Step 4 の完全形と同形で、以下だけを差し替える。`effectiveTo` は **`"2025-03"`**（Task 1 の `"2026-05"` ではない）。`r6-fee-notice` の locator は「物理{頁}頁（第2条表・左欄改正後 第14の17 注2 ホ {sub} 率）」。

| key | `percentage` | `calculationOrder` | xlsx `locator` の row | fee-notice 物理頁 |
|---|---|---|---|---|
| `addition.treatment-improvement.unified.v-1` | `"0.080"` | `8` | `1069` | 236 |
| `addition.treatment-improvement.unified.v-2` | `"0.079"` | `9` | `1071` | 236 |
| `addition.treatment-improvement.unified.v-3` | `"0.078"` | `10` | `1073` | 236 |
| `addition.treatment-improvement.unified.v-4` | `"0.077"` | `11` | `1075` | 237 |
| `addition.treatment-improvement.unified.v-5` | `"0.066"` | `12` | `1077` | 237 |
| `addition.treatment-improvement.unified.v-6` | `"0.064"` | `13` | `1079` | 237 |
| `addition.treatment-improvement.unified.v-7` | `"0.061"` | `14` | `1081` | 237 |
| `addition.treatment-improvement.unified.v-8` | `"0.063"` | `15` | `1083` | 237 |
| `addition.treatment-improvement.unified.v-9` | `"0.059"` | `16` | `1085` | 237 |
| `addition.treatment-improvement.unified.v-10` | `"0.048"` | `17` | `1087` | 237 |
| `addition.treatment-improvement.unified.v-11` | `"0.049"` | `18` | `1089` | 237 |
| `addition.treatment-improvement.unified.v-12` | `"0.046"` | `19` | `1091` | 237 |
| `addition.treatment-improvement.unified.v-13` | `"0.044"` | `20` | `1093` | 237 |
| `addition.treatment-improvement.unified.v-14` | `"0.031"` | `21` | `1095` | 238 |

- [ ] **Step 5: (Ⅴ)施設9件を `additions.json` へ追加する**

同形。`r6-fee-notice` の locator は「物理{頁}頁（第2条表・左欄改正後 第14の17 注2 ホ {sub} 括弧書き「指定障害者支援施設にあっては、…に相当する単位数」）」。

| key | `percentage` | `calculationOrder` | xlsx row | fee-notice 物理頁 |
|---|---|---|---|---|
| `addition.treatment-improvement.unified.v-1.facility` | `"0.091"` | `22` | `1070` | 236 |
| `addition.treatment-improvement.unified.v-2.facility` | `"0.087"` | `23` | `1072` | 236 |
| `addition.treatment-improvement.unified.v-5.facility` | `"0.074"` | `24` | `1078` | 237 |
| `addition.treatment-improvement.unified.v-7.facility` | `"0.066"` | `25` | `1082` | 237 |
| `addition.treatment-improvement.unified.v-8.facility` | `"0.073"` | `26` | `1084` | 237 |
| `addition.treatment-improvement.unified.v-10.facility` | `"0.053"` | `27` | `1088` | 237 |
| `addition.treatment-improvement.unified.v-11.facility` | `"0.056"` | `28` | `1090` | 237 |
| `addition.treatment-improvement.unified.v-13.facility` | `"0.048"` | `29` | `1094` | 237 |
| `addition.treatment-improvement.unified.v-14.facility` | `"0.035"` | `30` | `1096` | 238 |

- [ ] **Step 6: (Ⅴ)のサービスコード23件を `service-codes.json` へ追加する**

Task 1 Step 5 の完全形と同形。`effectiveTo` は `"2025-03"`。`conditionSelectors` は次のとおり。

通常14件（施設variantを**持つ**9区分 ⑴⑵⑸⑺⑻⑽⑾⒀⒁）:

```json
"conditionSelectors": [
  "reward-system-employment-continuation-support-b",
  "capability-treatment-improvement-v",
  "capability-treatment-improvement-v-band-{n}",
  "facility-classification-general-r6-06"
]
```

通常のうち施設variantを**持たない**5区分（⑶⑷⑹⑼⑿）は施設条件を付けない:

```json
"conditionSelectors": [
  "reward-system-employment-continuation-support-b",
  "capability-treatment-improvement-v",
  "capability-treatment-improvement-v-band-{n}"
]
```

施設9件:

```json
"conditionSelectors": [
  "reward-system-employment-continuation-support-b",
  "capability-treatment-improvement-v",
  "capability-treatment-improvement-v-band-{n}",
  "facility-classification-designated-support-facility-r6-06"
]
```

`serviceCode` / `percentage` / `calculationOrder` / locator は Step 4・5 の表と同じ値を使う。`officialLabel` は通常が `福祉・介護職員等処遇改善加算(Ⅴ){sub}`、施設が `福祉・介護職員等処遇改善加算(Ⅴ){sub}（指定障害者支援施設において行った場合）`。`key` は `b-addition.r6-06.treatment-improvement.unified.v-{n}` および `...v-{n}.facility`。

- [ ] **Step 7: テストが通ることを確認する**

```bash
dotnet build
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~ClaimMaster"
```

期待: `ClaimMasterR6FacilityTests` 全件 PASS。

- [ ] **Step 8: 全体テストを流す**

```bash
dotnet test
```

期待: 全緑。

- [ ] **Step 9: 歯の確認**

| 変更 | 期待 |
|---|---|
| `v-1` の `percentage` を `"0.080"`→`"0.081"`（additions/service-codes 両方） | `R6_treatment_improvement_percentages_match_the_notice` が RED |
| `v-7.facility` のエントリを additions/service-codes から削除 | `The_r6_treatment_improvement_codes_match_the_official_table_exactly`（下限）と `Category_v_facility_variants_resolve_for_a_facility_office`(7) が RED |
| 存在しない施設variant（例 `v-3.facility` / コード `465144`）を追加 | `The_r6_treatment_improvement_codes_match_the_official_table_exactly`（上限）が RED |
| `v-1` の `effectiveTo` を `"2025-03"`→`"2026-05"` | `Category_v_expires_at_the_end_of_march_2025` が RED |
| `v-3` に `facility-classification-general-r6-06` を追加 | `Category_v_subdivisions_without_a_facility_variant_resolve_for_both`(3) が RED（施設contextで0行） |

確認後は元へ戻し、`git diff --stat` が空であることを確認する。

- [ ] **Step 10: コミット**

```bash
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
        src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs
git commit -m "feat(phase3-6): 処遇改善(Ⅴ)14区分と施設variant9区分を投入する

適用期間は2024-06〜2025-03。r6-fee-noticeの規定本文が令和7年3月31日限りと
定め、r8-fee-noticeが当該規定を「（削る）」として削除し、R8サービスコード表にも
465124〜465137が存在しないことの3点で確定した。

施設variantを持つのは⑴⑵⑸⑺⑻⑽⑾⒀⒁の9区分のみ。告示の括弧書きの有無と
サービスコード表の欠番が完全に一致することを根拠とする。

既存4行と本コミットの26行で、r6-service-codes-2-pdf物理259頁のtype-46
処遇改善コード30件と過不足なく一致する（集合一致テストで固定）。"
```

---

## Task 3: 体制届の選択肢をマスタから導出する

**Files:**
- Modify: `src/Tsumugi.Application/UseCases/Claim/QueryClaimBillingTokenOptionsUseCase.cs`
- Modify: `tests/Tsumugi.Application.Tests/UseCases/Claim/QueryClaimBillingTokenOptionsUseCaseTests.cs`（無い場合は新規作成）

**Interfaces:**
- Consumes: Task 2 が投入した `capability-treatment-improvement*` 条件定義。
- Produces: `ClaimBillingTokenOptionsDto` に `TreatmentImprovementOptions` と `TreatmentImprovementVBandOptions`（いずれも `IReadOnlyList<int>`、昇順・重複なし）。Task 4 の ViewModel がこれを消費する。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using FluentAssertions;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Application.Tests.UseCases.Claim;

public sealed class QueryClaimBillingTokenOptionsCapabilityTests
{
    private static readonly QueryClaimBillingTokenOptionsUseCase UseCase =
        new(JsonClaimMasterProvider.LoadEmbedded());

    /// <summary>
    /// 体制届の選択番号はseedの条件定義にのみ存在し、UI/Applicationへハードコードしない
    /// （CLAUDE.md ハード制約3）。R6世代は(Ⅰ)〜(Ⅴ)＝option 2〜6。
    /// </summary>
    [Fact]
    public void R6_generation_exposes_options_two_through_six()
    {
        var dto = UseCase.Execute(new ServiceMonth(2024, 6));

        dto.TreatmentImprovementOptions.Should().Equal(2, 3, 4, 5, 6);
    }

    /// <summary>
    /// (Ⅴ)は2025-03限りで失効するため、2025-04以降のoption 6は選択肢から消える。
    /// </summary>
    [Fact]
    public void Category_v_disappears_after_march_2025()
    {
        UseCase.Execute(new ServiceMonth(2025, 3))
            .TreatmentImprovementOptions.Should().Contain(6);
        UseCase.Execute(new ServiceMonth(2025, 4))
            .TreatmentImprovementOptions.Should().NotContain(6);
    }

    /// <summary>
    /// R8世代は(Ⅰ)イ=2・(Ⅱ)イ=3・(Ⅲ)=4・(Ⅳ)=5・(Ⅰ)ロ=7・(Ⅱ)ロ=8。
    /// B型に(Ⅴ)は存在しないためoption 6は出ない（ADR 0048）。
    /// </summary>
    [Fact]
    public void R8_generation_exposes_the_six_reformed_options_without_category_v()
    {
        var dto = UseCase.Execute(new ServiceMonth(2026, 6));

        dto.TreatmentImprovementOptions.Should().Equal(2, 3, 4, 5, 7, 8);
    }

    /// <summary>
    /// (Ⅴ)区分の14択はR6の(Ⅴ)有効期間にのみ現れる。
    /// </summary>
    [Fact]
    public void The_category_v_band_options_exist_only_while_category_v_is_effective()
    {
        UseCase.Execute(new ServiceMonth(2024, 6))
            .TreatmentImprovementVBandOptions
            .Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

        UseCase.Execute(new ServiceMonth(2026, 6))
            .TreatmentImprovementVBandOptions.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

```bash
dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~QueryClaimBillingTokenOptionsCapabilityTests"
```

期待: コンパイルエラー（`TreatmentImprovementOptions` が未定義）。

- [ ] **Step 3: DTOへ2プロパティを追加する**

`QueryClaimBillingTokenOptionsUseCase.cs` の DTO を差し替える。

```csharp
/// <summary>
/// <c>OfficeClaimProfile</c>のStaffingKey/RegionKey選択肢と、<c>OfficeCapability</c>の
/// 公式体制届キーの選択番号。UIはこの値だけを選択肢として提示し、語彙を自前で
/// ハードコードしない（CLAUDE.md ハード制約3）。
/// </summary>
public sealed record ClaimBillingTokenOptionsDto(
    IReadOnlyList<string> StaffingKeyOptions,
    IReadOnlyList<string> RegionKeyOptions,
    IReadOnlyList<int> TreatmentImprovementOptions,
    IReadOnlyList<int> TreatmentImprovementVBandOptions);
```

- [ ] **Step 4: 導出ロジックを実装する**

`Execute` の `catch` を `return new ClaimBillingTokenOptionsDto([], [], [], []);` に変え、`return` 直前へ以下を挿入する。

```csharp
        var treatmentImprovementOptions = CapabilityOptionCodes(
            masters.ConditionDefinitions, "mhlw.b46.capability.treatment-improvement.");
        var treatmentImprovementVBandOptions = CapabilityOptionCodes(
            masters.ConditionDefinitions, "mhlw.b46.capability.treatment-improvement-v-band.");

        return new ClaimBillingTokenOptionsDto(
            staffingKeyOptions,
            regionKeyOptions,
            treatmentImprovementOptions,
            treatmentImprovementVBandOptions);
```

同クラスへ private helper を追加する。

```csharp
    /// <summary>
    /// <c>kind: office-capability</c>の条件定義のうち、指定した接頭辞を持つキーの
    /// 選択番号部分を昇順で列挙する。接頭辞が完全一致で終端することを要求するため、
    /// <c>treatment-improvement.</c>は<c>treatment-improvement-v-band.</c>を拾わない
    /// （前者の接頭辞の直後は必ず数字であり、ハイフンで始まる後者は候補にならない）。
    /// </summary>
    private static IReadOnlyList<int> CapabilityOptionCodes(
        IReadOnlyList<ClaimConditionDefinition> definitions, string prefix)
        => definitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .SelectMany(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token => new[] { token.Value },
                ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                _ => [],
            })
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value[prefix.Length..])
            .Where(suffix => int.TryParse(suffix, out _))
            .Select(int.Parse)
            .Distinct()
            .OrderBy(code => code)
            .ToArray();
```

- [ ] **Step 5: テストが通ることを確認する**

```bash
dotnet build
dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~QueryClaimBillingTokenOptions"
```

期待: PASS。既存の `ClaimBillingTokenOptionsDto` を組み立てている箇所（テスト含む）がコンパイルエラーになる場合は、新引数へ `[]` を渡して直す。

- [ ] **Step 6: 全体テストを流してコミット**

```bash
dotnet test
git add src/Tsumugi.Application/UseCases/Claim/QueryClaimBillingTokenOptionsUseCase.cs \
        tests/Tsumugi.Application.Tests/UseCases/Claim/QueryClaimBillingTokenOptionsCapabilityTests.cs
git commit -m "feat(phase3-6): 体制届の選択番号を月のマスタ条件定義から導出する

処遇改善対象と(Ⅴ)区分の選択肢を、seedの条件定義から月ごとに導出する。
R6は2〜6、R8は2〜5・7・8が自動的に出る。(Ⅴ)区分は(Ⅴ)の有効期間
(〜2025-03)にのみ現れる。語彙をUI/Applicationへハードコードしない。"
```

---

## Task 4: 公式体制届キーの入力面

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/OfficeCapabilityViewModel.cs`
- Modify: `src/Tsumugi.App/Views/OfficeCapabilityView.axaml`
- Modify: `tests/Tsumugi.App.Tests/OfficeCapabilityViewModelTests.cs`
- Modify: `tests/Tsumugi.App.Tests/ViewInputWiringTests.cs`

**Interfaces:**
- Consumes: Task 3 の `ClaimBillingTokenOptionsDto.TreatmentImprovementOptions` / `.TreatmentImprovementVBandOptions`。
- Produces: `OfficeCapability.Flags` に `mhlw.b46.capability.treatment-improvement.{n}` と `mhlw.b46.capability.treatment-improvement-v-band.{n}` を書き込む経路。Task 5 の存在検査が読む。

> **前提の確認**: 現行の `SaveAsync` は `mealProvision` / `transportSupport` の2キーしか書いていない。これは ADR 0021 が非推奨とした旧暫定キーであり、公式キーを書く経路が `src/` に存在しない（＝実運用では処遇改善加算がどの区分も算定されない）。本タスクはその欠落を埋める。**旧2キーは本タスクでは削除も変更もしない**（送迎体制・食事提供体制の公式キーへの移行は別の未確定事項に依存する）。

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.App.Tests/OfficeCapabilityViewModelTests.cs` へ追記する。既存のテストが使っている fake の組み立て方に合わせること（同ファイル冒頭を読んでから書く）。

```csharp
    /// <summary>
    /// ADR 0021の公式one-hotキーを書き込む。旧暫定キー（mealProvision等）だけを
    /// 書いていると、マスタ側の条件に一致せず処遇改善加算が無音で0円になる。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_the_official_treatment_improvement_key()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 2;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement.2")
            .WhoseValue.Should().BeTrue();
    }

    /// <summary>
    /// 選択されていない選択番号のキーは書かない（one-hot）。書くと複数区分が
    /// 同時に一致し、AmbiguousMatchまたは二重計上になる。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_only_the_selected_option_as_one_hot()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 4;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Keys
            .Where(k => k.StartsWith("mhlw.b46.capability.treatment-improvement.", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.4");
    }

    /// <summary>
    /// (Ⅴ)区分は処遇改善対象がoption 6のときだけ書く。他の区分を選んでいるのに
    /// (Ⅴ)区分のキーが残ると、失効後の月で不要なキーが宣言されたままになる。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_the_category_v_band_only_for_option_six()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 6;
        vm.TreatmentImprovementVBand = 3;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement-v-band.3");

        var other = CreateViewModel();
        await other.InitializeAsync();
        other.OfficeId = Guid.NewGuid();
        other.TreatmentImprovementOption = 2;
        other.TreatmentImprovementVBand = 3;

        await other.SaveCommand.ExecuteAsync(null);

        SavedFlags.Keys.Should().NotContain(
            k => k.StartsWith("mhlw.b46.capability.treatment-improvement-v-band.", StringComparison.Ordinal));
    }
```

`CreateViewModel()` と `SavedFlags` は同ファイルの既存 fake に合わせて用意する。`RegisterOfficeCapabilityUseCase` の fake が受け取った `flags` を保持する形にすること。

- [ ] **Step 2: テストが失敗することを確認する**

```bash
dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~OfficeCapabilityViewModelTests"
```

期待: コンパイルエラー（`TreatmentImprovementOption` 未定義）。

- [ ] **Step 3: ViewModel へプロパティと選択肢を追加する**

コンストラクタへ `QueryClaimBillingTokenOptionsUseCase` を注入する（DI登録は `CompositionRoot` にあるはずなので、無ければ追加する）。

```csharp
    [ObservableProperty] private int? _treatmentImprovementOption;
    [ObservableProperty] private int? _treatmentImprovementVBand;

    public ObservableCollection<int> TreatmentImprovementOptions { get; } = new();
    public ObservableCollection<int> TreatmentImprovementVBandOptions { get; } = new();
```

`InitializeAsync` / `LoadOfficesAsync` の後段で選択肢を読み込む。対象月は `PeriodStart` から作る。

```csharp
    /// <summary>
    /// 体制届の選択肢は適用期間の開始月に有効なマスタから引く。世代（R6/R8）で
    /// 選択番号の集合が変わるため、UIへ語彙を持たせない（ADR 0021・0048）。
    /// </summary>
    public void ReloadCapabilityOptions()
    {
        var month = new ServiceMonth(PeriodStart.Year, PeriodStart.Month);
        var options = tokenOptionsUseCase.Execute(month);

        TreatmentImprovementOptions.Clear();
        foreach (var code in options.TreatmentImprovementOptions) TreatmentImprovementOptions.Add(code);

        TreatmentImprovementVBandOptions.Clear();
        foreach (var code in options.TreatmentImprovementVBandOptions) TreatmentImprovementVBandOptions.Add(code);

        if (TreatmentImprovementOption is { } selected && !TreatmentImprovementOptions.Contains(selected))
        {
            TreatmentImprovementOption = null;
        }

        if (TreatmentImprovementVBand is { } band && !TreatmentImprovementVBandOptions.Contains(band))
        {
            TreatmentImprovementVBand = null;
        }
    }

    partial void OnPeriodStartChanged(DateOnly value) => ReloadCapabilityOptions();
```

- [ ] **Step 4: `SaveAsync` を one-hot 書き込みへ変更する**

```csharp
            var flags = new Dictionary<string, bool>
            {
                ["mealProvision"] = MealProvision,
                ["transportSupport"] = TransportSupport,
            };

            // ADR 0021: 公式体制届キーはone-hot。選択された選択番号のキーだけをtrueで置く。
            // 未選択なら1件も置かない（「なし」＝option 1 は加算行を持たないため宣言しない）。
            if (TreatmentImprovementOption is { } option)
            {
                flags[$"mhlw.b46.capability.treatment-improvement.{option}"] = true;

                // (Ⅴ)区分は処遇改善対象が(Ⅴ)のときにのみ意味を持つ。
                if (TreatmentImprovementVBandOptions.Count > 0
                    && TreatmentImprovementVBand is { } band
                    && TreatmentImprovementVBandOptions.Contains(band)
                    && option == CategoryVOptionCode)
                {
                    flags[$"mhlw.b46.capability.treatment-improvement-v-band.{band}"] = true;
                }
            }
```

`CategoryVOptionCode` は「(Ⅴ)区分の選択肢が存在するとき、それに対応する処遇改善対象の選択番号」である。**この値をハードコードしない**ため、次のように導出する。

```csharp
    /// <summary>
    /// (Ⅴ)区分が有効な世代では、(Ⅴ)区分の選択肢と処遇改善対象の選択肢が同時に存在する。
    /// (Ⅴ)に対応する処遇改善対象の選択番号は、(Ⅴ)区分が存在する月にのみ現れる選択番号
    /// として一意に定まる（R6: {2,3,4,5,6} のうち6、R8: (Ⅴ)区分が空なので該当なし）。
    /// この導出はseedの条件定義の有効期間が一致していることに依存する（ADR 0048）。
    /// </summary>
    private int? CategoryVOptionCode => TreatmentImprovementVBandOptions.Count == 0
        ? null
        : TreatmentImprovementOptions.Count == 0 ? null : TreatmentImprovementOptions.Max();
```

> **判断の記録**: `Max()` を使うのは、R6世代で(Ⅴ)が最後の選択番号（6）であり、(Ⅴ)区分が存在しない世代では `null` になるため。この導出が将来の版で崩れる可能性はあるので、Task 6 の証跡へ「導出の前提」として明記し、Step 5 のテストで固定する。

`Discard` へも `TreatmentImprovementOption = null; TreatmentImprovementVBand = null;` を追加する。

- [ ] **Step 5: View へ ComboBox 2つを追加する**

`OfficeCapabilityView.axaml` の既存のチェックボックス群の下へ追加する。

```xml
<StackPanel Orientation="Vertical" Spacing="4">
  <TextBlock Text="福祉・介護職員等処遇改善加算 対象区分" />
  <ComboBox ItemsSource="{Binding TreatmentImprovementOptions}"
            SelectedItem="{Binding TreatmentImprovementOption}"
            AutomationProperties.Name="福祉・介護職員等処遇改善加算 対象区分" />
</StackPanel>
<StackPanel Orientation="Vertical" Spacing="4"
            IsVisible="{Binding TreatmentImprovementVBandOptions.Count}">
  <TextBlock Text="福祉・介護職員等処遇改善加算(Ⅴ) 区分" />
  <ComboBox ItemsSource="{Binding TreatmentImprovementVBandOptions}"
            SelectedItem="{Binding TreatmentImprovementVBand}"
            AutomationProperties.Name="福祉・介護職員等処遇改善加算(Ⅴ) 区分" />
</StackPanel>
```

`ViewInputWiringTests` の検査対象へ `TreatmentImprovementOption` と `TreatmentImprovementVBand` を追加する（同ファイルの既存の記法に合わせる）。

- [ ] **Step 6: テストが通ることを確認してコミット**

```bash
dotnet build
dotnet test
git add src/Tsumugi.App/ViewModels/OfficeCapabilityViewModel.cs \
        src/Tsumugi.App/Views/OfficeCapabilityView.axaml \
        tests/Tsumugi.App.Tests/OfficeCapabilityViewModelTests.cs \
        tests/Tsumugi.App.Tests/ViewInputWiringTests.cs
git commit -m "feat(phase3-6): 公式体制届キー(処遇改善対象・(Ⅴ)区分)の入力面を追加する

従来SaveAsyncはmealProvision/transportSupportの旧暫定キーしか書いておらず、
ADR 0021の公式one-hotキーを書く経路がsrc/に存在しなかった。このため実運用では
処遇改善加算がどの区分も算定されない状態だった。

選択肢は月のマスタ条件定義から導出し、UIへ語彙を持たせない。
(Ⅴ)区分は処遇改善対象が(Ⅴ)のときにのみ書き込む。"
```

---

## Task 5: 体制届optionの存在検査（恒久readinessチェック）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/OfficeCapabilityCoveragePolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/OfficeCapabilityCoveragePolicyTests.cs`
- Modify: `src/Tsumugi.Application/Abstractions/IClaimMasterProvider.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs`
- Modify: `src/Tsumugi.Application/Dtos/ClaimPreparationDtos.cs`
- Modify: `src/Tsumugi.Application/UseCases/Claim/ClaimPreviewPipeline.cs`
- Modify: `src/Tsumugi.Application/UseCases/Claim/CalculateClaimUseCase.cs`
- Modify: `tests/Tsumugi.Application.Tests/UseCases/Claim/CalculateClaimUseCaseTests.cs`

**Interfaces:**
- Consumes: `ClaimCalculationMasterBundle.ConditionDefinitions`（月フィルタ済み）と、新設の `IClaimMasterProvider.AllOfficeCapabilityConditionValues()`（全期間）。
- Produces: `OfficeCapabilityCoveragePolicy.FindUncoveredKeys(declaredKeys, monthValues, allValues)` → `IReadOnlyList<string>`。`ClaimPreviewDto.CapabilityCoverageWarnings`。

- [ ] **Step 1: Domain の純粋関数のテストを書く**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Domain.Tests.Logic.Claim;

/// <summary>
/// ADR 0049: 宣言された体制届optionに対応する有効なマスタ行が当月に存在しない場合を
/// 検出する。無音で加算0円になる経路を可視化するための警告であり、確定は止めない。
/// </summary>
public sealed class OfficeCapabilityCoveragePolicyTests
{
    /// <summary>
    /// 当月に有効な条件定義が宣言キーを覆っていれば警告しない。
    /// </summary>
    [Fact]
    public void A_declared_key_covered_by_the_month_is_not_reported()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.2"],
            monthConditionValues: ["mhlw.b46.capability.treatment-improvement.2"],
            allConditionValues: ["mhlw.b46.capability.treatment-improvement.2"]);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// 他の期間では使われているのに当月に無いキーは「失効した／まだ施行されていない」
    /// 本当の穴であり、警告する。処遇改善(Ⅴ)を2025-04以降も届け出たままの事業所がこれ。
    /// </summary>
    [Fact]
    public void A_key_used_in_other_periods_but_not_this_month_is_reported()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.6"],
            monthConditionValues: ["mhlw.b46.capability.treatment-improvement.2"],
            allConditionValues:
            [
                "mhlw.b46.capability.treatment-improvement.2",
                "mhlw.b46.capability.treatment-improvement.6",
            ]);

        result.Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.6");
    }

    /// <summary>
    /// どの期間の条件定義からも参照されていないキーは請求に効かない体制届項目であり、
    /// 警告しない。ここを警告にすると、算定に関与しない項目で毎月ノイズが出る。
    /// </summary>
    [Fact]
    public void A_key_never_used_by_any_condition_is_ignored()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mealProvision"],
            monthConditionValues: ["mhlw.b46.capability.treatment-improvement.2"],
            allConditionValues: ["mhlw.b46.capability.treatment-improvement.2"]);

        result.Should().BeEmpty();
    }

    /// <summary>
    /// 結果は決定論（順序が安定）でなければならない。警告の並びが呼び出しごとに
    /// 変わると、確定snapshotやUI表示の差分が無意味に揺れる。
    /// </summary>
    [Fact]
    public void The_result_is_ordered_deterministically()
    {
        var result = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["b.key", "a.key"],
            monthConditionValues: [],
            allConditionValues: ["a.key", "b.key"]);

        result.Should().Equal("a.key", "b.key");
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~OfficeCapabilityCoveragePolicyTests"
```

期待: コンパイルエラー。

- [ ] **Step 3: 純粋関数を実装する**

```csharp
namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// 事業所が体制届で宣言したキーのうち、処理対象月に有効なマスタ行へ結び付かないものを
/// 検出する（ADR 0049）。日付・乱数・I/Oに依存しない純粋関数。
/// </summary>
/// <remarks>
/// 2段構えにしているのは偽陽性を避けるため。体制届には算定に関与しない項目もあり、
/// 「当月に無い」だけで警告すると毎月ノイズが出る。「他の期間では使われている」ことを
/// 条件に加えることで、失効・未施行という本当の穴だけを拾う。
/// </remarks>
public static class OfficeCapabilityCoveragePolicy
{
    public static IReadOnlyList<string> FindUncoveredKeys(
        IReadOnlyCollection<string> declaredKeys,
        IReadOnlyCollection<string> monthConditionValues,
        IReadOnlyCollection<string> allConditionValues)
    {
        ArgumentNullException.ThrowIfNull(declaredKeys);
        ArgumentNullException.ThrowIfNull(monthConditionValues);
        ArgumentNullException.ThrowIfNull(allConditionValues);

        var month = new HashSet<string>(monthConditionValues, StringComparer.Ordinal);
        var all = new HashSet<string>(allConditionValues, StringComparer.Ordinal);

        return declaredKeys
            .Where(key => all.Contains(key) && !month.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }
}
```

- [ ] **Step 4: テストが通ることを確認する**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~OfficeCapabilityCoveragePolicyTests"
```

期待: PASS。

- [ ] **Step 5: provider へ全期間アクセサを追加する**

`IClaimMasterProvider` へ追加する。

```csharp
    /// <summary>
    /// 登録済みマスタの<b>全期間</b>にわたる<c>kind: office-capability</c>条件定義の値集合。
    /// 「当月に無い」と「そもそも請求に効かないキー」を区別するために使う（ADR 0049）。
    /// </summary>
    IReadOnlySet<string> AllOfficeCapabilityConditionValues();
```

`JsonClaimMasterProvider` へ実装する。`_calculationMasters.ConditionDefinitions` は月フィルタ前の全件である。

```csharp
    public IReadOnlySet<string> AllOfficeCapabilityConditionValues()
        => _calculationMasters.ConditionDefinitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .SelectMany(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token => new[] { token.Value },
                ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                _ => [],
            })
            .ToHashSet(StringComparer.Ordinal);
```

テスト用の fake provider が `IClaimMasterProvider` を実装している箇所すべてに、空集合を返す実装を足してコンパイルを通す。

- [ ] **Step 6: DTO とパイプラインへ警告を通す**

`ClaimPreviewDto` へ末尾のオプション引数を追加する。

```csharp
    IReadOnlyList<ClaimUpcomingSpecificationIssue>? UpcomingSpecificationIssues = null,
    // 体制届で宣言されたが当月に有効なマスタ行が無いキー。**確定は止めない**
    // （IsReadyには影響させない）。無音で加算0円になる経路を可視化する警告（ADR 0049）。
    IReadOnlyList<string>? CapabilityCoverageWarnings = null)
```

`NotReady` ファクトリにも同じ引数を足して素通しする。`ClaimPreviewPipeline` の計算結果レコードへ `IReadOnlyList<string> CapabilityCoverageWarnings` を足し、`masters` を解決した直後で値を組み立てる。

```csharp
        var capabilityCoverageWarnings = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: tokens.OfficeCapabilityKeys,
            monthConditionValues: masters.ConditionDefinitions
                .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
                .SelectMany(condition => condition.Operand switch
                {
                    ClaimConditionTokenOperand token => new[] { token.Value },
                    ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                    _ => [],
                })
                .ToArray(),
            allConditionValues: masterProvider.AllOfficeCapabilityConditionValues());
```

`tokens.OfficeCapabilityKeys` の実際のプロパティ名は `ClaimBillingConditionContext` / token provider の戻り値で確認すること。`CalculateClaimUseCase` の2箇所（`NotReady` と成功経路）で `computation.CapabilityCoverageWarnings` を渡す。

- [ ] **Step 7: Application 層のテストを書く**

`CalculateClaimUseCaseTests.cs` へ追記する。既存の `Kit` fake の作法に合わせること。

```csharp
    // NOTE(teeth): 届け出たoptionに当月の行が無いのは「無音で0円」になる経路である。
    // IsReady を落とす実装にすると、期間境界をまたぐ正当な体制届まで確定できなくなる（ADR 0049）。
    [Fact]
    public async Task Execute_warns_about_declared_capabilities_without_master_rows_this_month()
    {
        // Kit.Tokens() が宣言する体制届キーに、当月の条件定義に無いキーを1件混ぜる。
        // Kit.SyntheticMasters() 側の条件定義には含めず、AllOfficeCapabilityConditionValues
        // には含める（＝他の期間では使われているキー）。
        var dto = await CreateUseCaseWithExpiredCapability()
            .ExecuteAsync(new CalculateClaimRequest(Kit.OfficeId, Kit.Month), CancellationToken.None);

        dto.IsReady.Should().BeTrue("体制届optionの不一致で確定は止めない");
        dto.CapabilityCoverageWarnings.Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.6");
    }
```

`CreateUseCaseWithExpiredCapability()` は同ファイルのヘルパー作法に合わせて用意し、fake provider の `AllOfficeCapabilityConditionValues()` が当該キーを含む集合を返すようにする。

- [ ] **Step 8: 実マスタでの疎通テストを書く**

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR6FacilityTests.cs` へ追記する。

```csharp
    /// <summary>
    /// ADR 0049: 実seedに対して、(Ⅴ)は2025-04以降で「他の期間には有るが当月に無い」
    /// 状態になる。R8世代（2026-06以降）でも同様。
    /// </summary>
    [Theory]
    [InlineData(2025, 4)]
    [InlineData(2026, 6)]
    public void Category_v_becomes_an_uncovered_capability_after_it_expires(int year, int month)
    {
        var provider = Provider;
        var target = new ServiceMonth(year, month);
        var monthValues = provider.ResolveCalculationMasters(target).ConditionDefinitions
            .Where(condition => condition.Kind == ClaimConditionKind.OfficeCapability)
            .SelectMany(condition => condition.Operand switch
            {
                ClaimConditionTokenOperand token => new[] { token.Value },
                ClaimConditionTokenSetOperand set => set.Values.ToArray(),
                _ => [],
            })
            .ToArray();

        var uncovered = OfficeCapabilityCoveragePolicy.FindUncoveredKeys(
            declaredKeys: ["mhlw.b46.capability.treatment-improvement.6"],
            monthConditionValues: monthValues,
            allConditionValues: provider.AllOfficeCapabilityConditionValues());

        uncovered.Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.6");
    }
```

- [ ] **Step 9: 全体テストを流す**

```bash
dotnet build
dotnet test
```

期待: 全緑。

- [ ] **Step 10: 歯の確認**

| 変更 | 期待 |
|---|---|
| `FindUncoveredKeys` の `all.Contains(key) &&` を削除 | `A_key_never_used_by_any_condition_is_ignored` が RED |
| `FindUncoveredKeys` の `!month.Contains(key)` を `month.Contains(key)` へ反転 | `A_key_used_in_other_periods_but_not_this_month_is_reported` と `A_declared_key_covered_by_the_month_is_not_reported` が RED |
| `OrderBy` を削除 | `The_result_is_ordered_deterministically` が RED（順序が入力順のまま） |
| `ClaimPreviewPipeline` の警告組み立てを `[]` 固定へ | Step 7・8 のテストが RED |

確認後は元へ戻し、`git diff --stat` が空であることを確認する。

- [ ] **Step 11: コミット**

```bash
git add src/Tsumugi.Domain/Logic/Claim/OfficeCapabilityCoveragePolicy.cs \
        src/Tsumugi.Application src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs \
        tests/Tsumugi.Domain.Tests tests/Tsumugi.Application.Tests tests/Tsumugi.Infrastructure.Tests
git commit -m "feat(phase3-6): 体制届optionに対応するマスタ行の存在検査を追加する

宣言されたキーが「他の期間には有るが当月に無い」場合だけを警告する2段構えに
することで、算定に関与しない体制届項目での偽陽性を避ける。

確定は止めない(IsReadyに影響させない)。期間境界をまたぐ正当な体制届まで
確定できなくなるため(ADR 0041の前例に倣う)。

処遇改善(Ⅴ)を2025-04以降も届け出たままの事業所と、2026-06以降にoption 6を
届け出ている事業所の2経路が、これで無音ではなくなる。"
```

---

## Task 6: ADR・open-questions・CHANGELOG・受け入れ証跡

**Files:**
- Create: `docs/decisions/0048-r6-06-treatment-improvement-facility-and-category-v.md`
- Create: `docs/decisions/0049-office-capability-master-coverage-check.md`
- Create: `docs/phase3-6-acceptance.md`
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: ADR 0048 を書く**

結論→背景→選択肢→決定→影響の順。必ず含める内容:

- 確定した実値（施設variant 3区分・(Ⅴ) 14区分＋施設9区分）と全出典（documentId・SHA-256・物理頁/行）
- (Ⅴ)の適用期間が 2024-06〜2025-03 である3つの根拠（`r6-fee-notice` の期限本文、`r8-fee-notice` の「（削る）」、R8コード表に 465124〜465137 が無いこと）
- **ADR 0045 の「R8-06へ(Ⅴ)を投入する必要がある」という前提が誤りだったことの明示的な訂正**
- 告示の括弧書きの有無とコード表の欠番が完全一致することを、施設variantの集合の根拠として記録
- 通常行への非施設条件の付与が遡及に与える影響（2024-06〜2026-05 は施設区分の入力が必須になる）と、片側投入が二重計上になるため回避不可であること
- `r8-capability-correction`（令和8年6月版）を(Ⅴ)区分の選択肢の出典に使った理由と、期間の根拠は `r6-fee-notice` が担うという役割分担

- [ ] **Step 2: ADR 0049 を書く**

- 2段構えの判定（当月に無い ∧ 他の期間には有る）とその理由（偽陽性の回避）
- 警告にしてブロックしない理由（ADR 0041 の前例、期間境界をまたぐ正当な体制届）
- 残る限界: 警告を見落とせば 0円のまま確定できること
- `IClaimMasterProvider` を1メソッド広げた理由

- [ ] **Step 3: `docs/open-questions.md` を更新する**

以下を `[x]` へ変えて、クローズ日・ADR番号・確定内容を追記する。

- 「[Phase3-5 最終レビュー由来] R6-06世代の処遇改善に施設区分の別立てが無い」→ クローズ（ADR 0048）
- 「[Phase3-4/Task2 follow-up] R8処遇改善(Ⅴ)の実値投入」→ クローズ（R6分を投入し、**R8には存在しないことを確定**したため。解除条件を満たしたのではなく前提が誤りだったことを明記する）
- 「[Phase3-4/Task2 follow-up] 体制届optionに対応するマスタ行が当月に存在しない場合のreadiness警告」→ クローズ（ADR 0049）
- 「[Phase3-1/Task 9] 利用定員・人員配置区分の実データ源」→ クローズ（**Phase 3-1 で実装済みであり記述が陳腐化していた**。`OfficeClaimProfile` の列・migration `Phase31OfficeClaimBillingTokens`・`OfficeClaimBillingTokenProvider.cs:87`・`ClaimInputViewModel.cs:456` で確認。新規実装ではない）

新規に起票する項目:

- **旧暫定体制届キー（`mealProvision` / `transportSupport`）が公式キーへ移行していない**: `OfficeCapabilityViewModel` は両キーを書き続けるが、どの条件定義からも参照されないため算定に効かない。送迎体制・食事提供体制の公式キーへの移行は、それぞれの加算のマスタ投入と合わせて行う。

- [ ] **Step 4: `CHANGELOG.md` を更新する**

「Phase 3-6 完了」節を追加する。あわせて「本番投入前に必須の deferred」節が Phase 3-1 未受け入れ時点の記述のまま陳腐化している（3帳票とCSV生成UIは Phase 3-2 / 3-3 で実装済み）ので、現況へ直す。

- [ ] **Step 5: `CLAUDE.md` の「現在地」を更新する**

Phase 3-6 完了を追記し、ハード制約3の記述へ「R6-06世代の処遇改善は施設区分条件を持つ」旨と「(Ⅴ)は2024-06〜2025-03のみ」を反映する。`docs/phase3-6-acceptance.md` を「仕様の所在」へ追加する。

- [ ] **Step 6: `docs/phase3-6-acceptance.md` を書く**

既存の `docs/phase3-5-acceptance.md` の構成に合わせる。必ず含める節:

1. 達成状況と証拠（テスト名を挙げる）
2. 投入した行数の実績（26行・条件定義17件・変更3行）
3. spec からの逸脱と理由
4. 実装者が発見したこと（`Task 4` の「公式キーの入力経路が存在しなかった」は spec 段階で判明済みだが、実装中に追加で判明したことがあれば記録）
5. 歯の確認の一覧（Task 1 Step 9・Task 2 Step 9・Task 5 Step 10 の結果）
6. **残課題**: (a) 旧暫定キーの移行（Step 3 で起票した新項目）、(b) (Ⅴ)区分と処遇改善対象optionの組合せ検証が無いこと、(c) `CategoryVOptionCode` の `Max()` 導出が将来の版で崩れうること、(d) GUI手動貫通確認が Phase 1 から未実施のままであること
7. `./build/ci.sh` 実行証跡

- [ ] **Step 7: 品質ゲートを通す**

```bash
dotnet format --verify-no-changes
./build/ci.sh
```

期待: 全ゲート緑。

- [ ] **Step 8: コミット**

```bash
git add docs/ CHANGELOG.md CLAUDE.md
git commit -m "docs(phase3-6): ADR 0048/0049・受け入れ証跡・open-questionsを同期する

ADR 0048でR6-06の施設variantと(Ⅴ)の実値・適用期間を確定し、ADR 0045の
「R8へ(Ⅴ)を投入する必要がある」という前提の誤りを訂正する。
ADR 0049で体制届optionの存在検査を確定する。

open-questionsは4項目をクローズし、旧暫定体制届キーの未移行を新規に起票する。"
```

---

## 自己レビュー結果

**spec カバレッジ**: spec §4.1→Task 1、§4.2→Task 2、§4.3→Task 2 Step 1 の集合一致テスト、§4.4→Task 1 Step 3 と Task 2 Step 3、§4.5→Task 1 Step 6、§4.6→各 Step の sourceRefs、§5→Task 4、§6→Task 5、§7→ADR 0048/0049 と証跡、§8→各 Step 9/10、§9→Task 6、§11→Task 6 Step 3。**spec §5 が想定していなかった「公式キーの入力経路が存在しない」問題は Task 4 で吸収し、spec より広い変更になることを Task 4 の前提として明記した。**

**型の整合**: `ClaimBillingTokenOptionsDto` は Task 3 で4引数へ拡張し、Task 4 が `TreatmentImprovementOptions` / `TreatmentImprovementVBandOptions` を消費する。`OfficeCapabilityCoveragePolicy.FindUncoveredKeys` は Task 5 Step 3 で定義し、Step 6・8 が同じ引数名で呼ぶ。`IClaimMasterProvider.AllOfficeCapabilityConditionValues()` は Step 5 で定義し Step 6・8 が呼ぶ。

**実装時に実型の確認が要る箇所**（計画で断定せず確認を指示した箇所）: `UnitAdjustmentMasterRow` の率アクセス経路（Task 2 Step 1）、token provider の戻り値の体制届キーのプロパティ名（Task 5 Step 6）、`OfficeCapabilityViewModelTests` の既存 fake の作法（Task 4 Step 1）、`ViewInputWiringTests` の記法（Task 4 Step 5）。いずれも「意味は変えずに実型へ合わせる」ことを明記した。
