# Phase 3-4 実装計画 — 令和8年6月施行分（R8-06）の制度実値投入

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `claim-master-r8-06` の制度実値（地域単価・負担上限の適用判断、処遇改善加算、改定対象の新12区分）を production seed へ投入し、2026-06 以降のサービス提供月が改定対象・対象外を問わず請求プレビューから CSV 生成まで通る状態にする。

**Architecture:** 器（JSON schema・`JsonClaimMasterProvider`・`ServiceCodeResolver`・`transition-rules` の R8 許可宣言・readiness・CSV writer）はすべて完成済みで、本計画は **seed JSON へ値を追記するだけ**である。C# の型・schema・resolver は一切変更しない。R6 entry は書き換えず、R8 は `effectiveFrom: "2026-06"` の新 entry として追記し、R6 側は必要に応じて `effectiveTo` を設定して閉じる（ADR 0039 が CSV 仕様版で確立した「追記して並存、処理対象年月で選ぶ」規律と同型）。

**Tech Stack:** .NET 10 / C# 14、xUnit ＋ FluentAssertions、EF Core 10（本計画では migration なし）、`pdftotext`（poppler）と Python 3 による一次資料抽出。

**設計 spec（正本）:** `docs/superpowers/specs/2026-07-26-phase3-4-r8-06-master-values-design.md`

---

## Global Constraints

これらは全タスクの要件に暗黙に含まれる。

- **制度実値をC#へ直書きしない**（CLAUDE.md ハード制約3）。単位数・率・単価・上限額はすべて seed JSON ＋ `sourceRefs` 経由。`ClaimSpecificationBoundaryTests` と `ExternalSpecificationLiteralGuard` が Roslyn token 単位で検査する。
- **一次資料から (1) 値、(2) 算定単位、(3) 算定条件、(4) サービスコード の4点を一意に確定できた行だけを seed する。** 1点でも欠ける行は seed しない。
- **すべての抽出は2形式または2方式の独立抽出＋全行一致で行う**（ADR 0027/0028 の確立した方式）。xlsx と PDF、または `pdftotext -layout` と `pdftotext -raw`。一致しない行は seed せず `docs/open-questions.md` へ起票する。
- **使用前に必ず `shasum -a 256` が `sources.json` の登録値と一致することを確認する。** 不一致時は値を使わず停止する（ADR 0020）。
- **確定できない場合は fail-close 側へ倒す。** 適用期間を閉じて請求を止める。「エラーを出さずに古い値を使う」経路を作らない。
- **R6 entry を書き換えない。** 追記と `effectiveTo` の設定のみ。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。`dotnet build` は警告ゼロが前提。
- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。本計画は Infrastructure の seed と Domain/Infrastructure のテストのみ触る。
- 1コミット=1論理変更。コミットメッセージにフェーズ番号と受け入れ基準ID（AC3-4-1〜4）を記す。
- ADR 番号は着手時点の空き番号へ採番する（本計画執筆時点の最大は 0043 なので 0044〜0046 を想定）。

### 制度値を計画に書けないことについて（重要・意図的）

**本計画は単位数・率・単価の実値を一切含まない。** これは手抜きではなく設計上の境界である。制度値は一次資料からの抽出結果としてのみ存在してよく、計画文書に書けば「計画を出典とする値」が生まれてハード制約3の趣旨（出典の外部化）を壊す。

したがって各タスクは次の順で進む。

1. 抽出コマンドを実行し、2方式の出力が一致することを確認する
2. **ADR に決定表として値を記録する**（ここが値の唯一の出典になる）
3. ADR の決定表を seed JSON へ転記する
4. golden case は ADR の決定表を期待値として参照する

計画が与えるのは **抽出手順・JSON の正確な形・構造的な受け入れゲート（行数・直積の完全性）** であり、値そのものではない。JSON 例の値スロットは `<抽出値>` と書く。これは TODO ではなく「ここに抽出結果が入る」という契約である。

---

## File Structure

| ファイル | 責務 | タスク |
| --- | --- | --- |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json` | 一次資料の documentId・SHA-256・URL・release 束 | 1 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json` | 地域区分単価（8行）の適用期間 | 1 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json` | 負担上限額（4行）の適用期間 | 1 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json` | 加算の単位数・率 | 2 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json` | `conditionDefinitions` ＋ サービスコード行 | 2, 3, 5 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json` | 基本報酬の単位数 | 4 |
| `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8ContinuityTests.cs` | **新規** — 2026-06 に到達する全 entry が出典を持つか適用期間が閉じているかの網羅検査 | 1 |
| `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs` | 版境界の解決と fail-close の残存範囲 | 2, 5 |
| `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs` | seed の行数と schema 適合 | 2, 4, 5 |
| `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimAdditionSeedScopeTests.cs` | 加算のコード単位スコープ | 2 |
| `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs` | 手計算検証ケース | 6 |
| `docs/decisions/0044〜0046` | 値の唯一の出典（決定表） | 1, 2, 4 |
| `docs/phase3-4-acceptance.md` | 受け入れ証跡 | 7 |

---

## 既存 API リファレンス（実装者向け・これ以外を発明しない）

本計画のテストコードが使う型は既存のものだけである。

```csharp
// Tsumugi.Infrastructure.ClaimMasters
JsonClaimMasterProvider.LoadEmbedded()                    // → JsonClaimMasterProvider
provider.ResolveCalculationMasters(ServiceMonth month)    // → ClaimCalculationMasters
provider.Resolve(ClaimMasterVersion version)              // → OfficeClaimProfilePolicy

// Tsumugi.Domain.Logic.Claim.Models.ClaimCalculationMasters のメンバ
masters.BasicRewards      // IReadOnlyList<BasicRewardMasterRow>      … .Key
masters.ServiceCodes      // IReadOnlyList<ServiceCodeMasterRow>      … .Key
masters.UnitAdjustments   // IReadOnlyList<UnitAdjustmentMasterRow>   … .Key
masters.RegionUnitPrices  // IReadOnlyList<RegionUnitPriceMasterRow>
masters.BurdenCaps        // IReadOnlyList<BurdenCapMasterRow>
masters.TransitionRules   // IReadOnlyList<OfficeClaimProfileTransitionRuleMasterRow>

// Tsumugi.Domain.Logic.Claim
ServiceCodeResolver.ResolveBasicReward(masters, month, context)
    // → .ServiceCode (string) / .UnitsPerDay (int)
    // 解決不能時 ServiceCodeResolutionException（.Code == ServiceCodeResolutionErrorCode.MasterUnavailable）

// Tsumugi.Domain.Logic.Claim.Models
new ClaimBillingConditionContext(
    RewardSystem: string, PaymentBand: string, CapacityHeadcount: int,
    StaffingKey: string, AverageWageBandOption: AverageWageBandOption,
    R8ReformStatus: R8ReformStatus, OfficeCapabilityKeys: ISet<string>)
new AverageWageBandOption(AverageWageBandOptionKind.Numeric, int officialOptionCode)
// AverageWageBandOptionKind: Unknown / Numeric / FiledTransition / ProductionActivitySupport
// R8ReformStatus: Unknown / NotApplicableBeforeR8 / ReformTarget / ReformExempt / UnchangedBelow15000

// Tsumugi.Domain.ValueObjects
new ServiceMonth(int year, int month)
new ClaimMasterVersion("claim-master-r8-06")
```

**`transition-rules.json` の R8 区分割当ては投入済みで、既存テスト `ClaimMasterR8BoundaryTests.R8_band_edition_partitions_official_options_by_reform_status_from_june_2026` が固定している。**

| `R8ReformStatus` | 許可 option |
| --- | --- |
| `ReformTarget` | `FiledTransition`(8) ＋ Numeric 11〜22（**新12区分**） |
| `ReformExempt` | Numeric 1〜6（従前6区分を継続） |
| `UnchangedBelow15000` | Numeric 7・9（境界不変） |

したがって **新12区分は option 11〜22 に対応する 12 個の `average-wage-band` トークン**であり、この対応は既に確定している（spec §2.3 の「仮説」は本テストにより解消済み。残る未確定は各 option code がどの金額境界に対応するかだけ）。

---

## コマンド

```bash
dotnet build                                # 警告ゼロが前提
dotnet test                                 # 全緑が前提
./build/ci.sh                               # 品質ゲート一括（コミット前に必ず緑）
dotnet format --verify-no-changes           # 整形チェック

# 単一テスト実行
dotnet test --filter "FullyQualifiedName~ClaimMasterR8BoundaryTests.Reform_target"

# 一次資料の同一性検証
shasum -a 256 <file>

# PDF 抽出（2方式）
pdftotext -layout -f <first> -l <last> <file.pdf> -
pdftotext -raw    -f <first> -l <last> <file.pdf> -
```

---

## Task 1: 地域区分単価・負担上限額の R8 適用判断（AC3-4-1）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8ContinuityTests.cs`
- Create: `docs/decisions/0044-r8-region-unit-price-and-burden-cap-continuity.md`

**Interfaces:**
- Consumes: なし（本計画の最初のタスク）
- Produces: `region-unit-prices.json` / `burden-caps.json` の 2026-06 における確定した適用期間。Task 6 の golden case が使う `regionKey`（`region-grade-1` / `region-grade-2` / `region-other`）の単価が 2026-06 で解決可能か、閉じているかが確定する。

**背景:** `region-unit-prices.json`（8件）と `burden-caps.json`（4件）は `effectiveFrom: "2024-04"` / `effectiveTo: null` で 2026-06 以降も適用され続けるが、`sources.json` に R8 版の出典が登録されていない。他の2ギャップが fail-close するのに対し、**これだけがエラーを出さずに古い値で請求を生成しうる**。

- [ ] **Step 1: 網羅検査テストを書く（失敗させる）**

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8ContinuityTests.cs` を新規作成する。

```csharp
using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

/// <summary>
/// 2026-06（R8-06施行月）に適用される全master entryが、R8出典に裏付けられているか、
/// または明示的に適用期間を閉じられているかを網羅検査する（ADR 0044・AC3-4-1）。
/// 「出典なしでR8へ到達するentry」＝エラーを出さずに古い値で請求を生成しうる経路を0件に保つ。
/// </summary>
public sealed class ClaimMasterR8ContinuityTests
{
    private const string SeedDirectory = "src/Tsumugi.Infrastructure/ClaimMasters/Seed";

    /// <summary>
    /// R8-06施行分を裏づける一次資料のdocumentId。ここに載るdocumentIdをsourceRefsに
    /// 1件以上持つentryだけが、2026-06へ適用期間を開いたまま到達してよい。
    /// </summary>
    private static readonly string[] R8AuthoritativeDocumentIds =
    [
        "r8-fee-notice",
        "r8-reward-structure",
        "r8-service-codes-2-xlsx",
        "r8-service-codes-2-pdf",
        "r8-b-reward-band-guide",
        "r8-calculation-note",
        "r8-capability-202606",
    ];

    // spec §3.1 AC3-4-1 の対象はこの2ファイルに限る。basic-rewards / additions /
    // service-codes のR8継続はADR 0027決定6・ADR 0028決定1が照合済みとして記録し、
    // ClaimMasterR8BoundaryTests.Exempt_offices_resolve_the_same_code_and_units_across_the_boundary
    // がruntimeで固定しているため、本テストの対象に含めない。
    // 既存テスト（ClaimCalculatorGoldenCaseTests.GoldenCases）と同じ構築様式に揃える。
    public static TheoryData<string> ValueBearingSeedFiles()
    {
        var data = new TheoryData<string>();
        data.Add("region-unit-prices.json");
        data.Add("burden-caps.json");
        return data;
    }

    [Theory]
    [MemberData(nameof(ValueBearingSeedFiles))]
    public void Every_entry_reaching_june_2026_is_backed_by_an_r8_source(string fileName)
    {
        var r8Sources = R8AuthoritativeDocumentIds.ToHashSet(StringComparer.Ordinal);
        using var document = OpenSeed(fileName);

        var unbacked = new List<string>();
        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (!ReachesJune2026(entry))
                continue;

            var backed = entry.GetProperty("sourceRefs").EnumerateArray().Any(
                sourceRef => r8Sources.Contains(sourceRef.GetProperty("documentId").GetString()!));

            if (!backed)
                unbacked.Add(entry.GetProperty("key").GetString()!);
        }

        unbacked.Should().BeEmpty(
            "2026-06へ到達するentryはR8出典を持つか、effectiveToで適用期間を閉じなければならない"
            + "（出典なしの継続は、エラーを出さずに古い値で請求を生成する唯一の経路）");
    }

    /// <summary>effectiveFrom ≦ 2026-06 かつ（effectiveTo が null または ≧ 2026-06）。</summary>
    private static bool ReachesJune2026(JsonElement entry)
    {
        const string June2026 = "2026-06";
        var from = entry.GetProperty("effectiveFrom").GetString()!;
        if (string.CompareOrdinal(from, June2026) > 0)
            return false;

        var to = entry.GetProperty("effectiveTo");
        return to.ValueKind == JsonValueKind.Null
            || string.CompareOrdinal(to.GetString()!, June2026) >= 0;
    }

    private static JsonDocument OpenSeed(string fileName)
    {
        var path = Path.Combine(RepositoryRoot(), SeedDirectory, fileName);
        File.Exists(path).Should().BeTrue($"seedファイル {fileName} が存在しなければならない");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
            directory = directory.Parent;

        directory.Should().NotBeNull("リポジトリルート（Tsumugi.slnのある階層）が見つからなければならない");
        return directory!.FullName;
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterR8ContinuityTests"
```

期待: **FAIL**。`region-unit-prices.json` の 8 件と `burden-caps.json` の 4 件が `unbacked` に挙がる（合計12件）。

> **対象を2ファイルに限る理由**（2026-07-26 の着手前スキャンで確定）: spec §3.1 AC3-4-1 の対象は `region-unit-prices` と `burden-caps` である。`basic-rewards` の 135 行・`service-codes` の 147 行・`additions` の固定単位 12 行の R8 継続は、ADR 0027 決定6 と ADR 0028 決定1 が「R8-6月表で同一コード・同一単位数を照合済み」として記録し、`ClaimMasterR8BoundaryTests.Exempt_offices_resolve_the_same_code_and_units_across_the_boundary` が runtime で固定している。これら 294 行へ手書きの locator を追記し直す作業は、既存の証跡を高い転記ミスリスクで複製するだけなので行わない。

- [ ] **Step 3: R8 版の単価告示・負担上限資料の所在を確定する**

まず既登録の R8 文書内を探す。地域区分単価は報酬算定構造資料に含まれることがある。

```bash
cd /Users/hiro/Projetct/GitHub/Tsumugi
python3 - <<'PY'
import json
d=json.load(open("src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json",encoding="utf-8"))
for s in d["sources"]:
    print(s["documentId"], "|", s.get("title","")[:70])
PY
```

次に、既存の R6 単価出典 `mhlw-unit-price-notice-observed-946c3d96`（厚生労働省告示第539号）の URL を起点に R8 版の有無を確認する。負担上限は `r6-disability-support-guide-202404` の後継版を探す。

**分岐**:
- 既登録文書に含まれる → Step 4 へ（新規登録不要、`sourceRefs` の追記のみ）
- 新規取得が必要 → Step 4 で `sources.json` へ登録
- **取得できない** → Step 5 の fail-close 分岐へ直行する

- [ ] **Step 4: 一次資料を検証して値を抽出する**

```bash
# 1. SHA-256 が sources.json の登録値と一致することを確認（不一致なら停止）
shasum -a 256 <取得したファイル>

# 2. 2方式で独立抽出し、diff が空であることを確認
pdftotext -layout -f <first> -l <last> <file.pdf> /tmp/layout.txt
pdftotext -raw    -f <first> -l <last> <file.pdf> /tmp/raw.txt
# 就労継続支援の各級地単価行／負担区分ごとの上限額行を両出力から拾い、目視で突合する
```

新規文書を `sources.json` の `sources` 配列へ追加する場合、既存 entry と同じ形にする。

```json
{
  "documentId": "<新規ID>",
  "title": "<公式表題>",
  "publisher": "<発出元>",
  "retrievedAt": "2026-07-26",
  "sha256": "<抽出値>",
  "url": "<公式URL>"
}
```

**あわせて `releases` 配列の `2026-06 → null` の束の `sourceDocumentIds` へ新 documentId を追加する。** `ClaimMasterSeedPhase31Tests.Source_manifest_documents_match_the_catalog_and_release_bundles` は manifest⊆catalog の向きしか検査しないため、`sources.json` への追加だけで manifest 側のテストは壊れない。**`docs/spec-data/phase3/claim-master-source-row-manifest.json` は触らない**（触ると `ExpectedDocumentIds` と `ExpectedFinalOrderedIdentityDigest` の更新が連鎖する）。

- [ ] **Step 5: 照合結果に応じて seed を更新する**

3分岐のいずれかを取る。**どの分岐を取ったかを ADR 0044 に必ず記録する。**

**(a) R6 と同値だった場合** — `effectiveTo: null` を維持し、`sourceRefs` へ R8 出典を `cross-check` で追記する。

```json
{
  "key": "b-region.r6.region-grade-1",
  "effectiveFrom": "2024-04",
  "effectiveTo": null,
  "sourceRefs": [
    {
      "documentId": "mhlw-unit-price-notice-observed-946c3d96",
      "sha256": "946c3d969ffd4128db15106d25ce6d26ff108f5460a7618e3df96352e42c0c1b",
      "locator": "mhlw-unit-price-notice 本文一 表 就労継続支援 一級地（1114/1000）",
      "evidenceRole": "authoritative",
      "supports": ["master-values", "effective-period"]
    },
    {
      "documentId": "<R8出典のdocumentId>",
      "sha256": "<抽出値>",
      "locator": "<再現可能な位置指定>",
      "evidenceRole": "cross-check",
      "supports": ["master-values", "effective-period"]
    }
  ],
  "values": { "regionKey": "region-grade-1", "serviceKind": "employment-continuation-support", "unitPriceYen": "11.14" }
}
```

**(b) R6 と異なった場合** — R6 entry に `"effectiveTo": "2026-05"` を設定し、R8 entry を新規追加する（キーは `b-region.r8.region-grade-1` のように版を含める）。

**(c) 一次資料を取得できない／値を一意に確定できない場合** — **R6 entry に `"effectiveTo": "2026-05"` を設定するだけ**にする。R8 entry は作らない。これにより 2026-06 の請求は地域単価未解決で fail-close する。`docs/open-questions.md` へ次を起票する: 何が確定できなかったか、どの資料を見れば確定するか、現在の fail-close 挙動、解除条件。

> **この分岐が本タスクの中核判断である。** 事業所は「請求が出せない」ことには気付けるが「単価が古い」ことには気付けない。誤った金額を静かに生成するより、生成を止める方が回復可能である。

- [ ] **Step 6: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterR8ContinuityTests"
```

期待: **PASS**。`region-unit-prices.json` の 8 件と `burden-caps.json` の 4 件が、R8 出典を持つ（分岐 a/b）か `effectiveTo: "2026-05"` で閉じている（分岐 c）かのいずれかになっている。

```bash
dotnet test
```

期待: 全緑。特に次を確認する。
- `ClaimMasterR8BoundaryTests.Basic_reward_rows_continue_unchanged_across_the_r8_boundary` — 本タスクは `basic-rewards.json` を触らないため無変更で緑のまま
- 分岐 (b)/(c) を取った場合、既存の golden case（2025-04・2026-05 以前）は影響を受けない。2026-06 を使う既存テストが無いことを確認する

- [ ] **Step 7: 歯の確認（意図的違反で赤になること）**

`region-unit-prices.json` の1件から R8 出典の `sourceRef` を一時的に削除し、Step 1 のテストが RED になることを確認する。確認後に戻す。

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterR8ContinuityTests"   # → FAIL を確認
git checkout src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json  # 戻す
dotnet test --filter "FullyQualifiedName~ClaimMasterR8ContinuityTests"   # → PASS を確認
```

- [ ] **Step 8: ADR 0044 を書く**

`docs/decisions/0044-r8-region-unit-price-and-burden-cap-continuity.md` を作成する。構成は既存 ADR に合わせ、**結論 → 背景 → 選択肢 → 決定 → 影響**。「暫定→確定」ではなく初手から確定として書く。

必須の記載事項:
- 結論: 地域単価・負担上限が R8 で継続か改定か、または確定不能で閉じたか
- 一次資料の同一性検証結果（documentId・SHA-256 先頭12桁・照合の可否の表。ADR 0028 の様式に倣う）
- 抽出方式と2方式の一致確認結果
- 決定表（該当する場合の実値。**これが値の唯一の出典になる**）
- 選択肢: 「出典なしで継続する」を明示的に不採用として記録し、その理由（サイレント誤請求）を書く
- 影響: ADR 0022 が述べる「5-release source chain」は出典連鎖であって seed 実値ではないことの明確化
- 再検証手順（sources.json の URL 取得 → shasum 照合 → 該当頁の突合）

- [ ] **Step 9: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/ \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8ContinuityTests.cs \
        docs/decisions/0044-r8-region-unit-price-and-burden-cap-continuity.md \
        docs/open-questions.md
git commit -m "feat(phase3-4/AC3-4-1): R8での地域単価・負担上限の適用を出典付きで確定する"
```

---

## Task 2: 福祉・介護職員等処遇改善加算の R8 実値（AC3-4-2）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs:212-231`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimAdditionSeedScopeTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs:34-41`
- Create: `docs/decisions/0045-r8-treatment-improvement-addition-values.md`

**Interfaces:**
- Consumes: Task 1 が確定した `sources.json` の release 束
- Produces: `additions.json` の R8 処遇改善行（キー命名 `addition.treatment-improvement.r8.<区分>`）と `service-codes.json` の対応行（キー命名 `b-addition.r8-06.treatment-improvement.<区分>`）。Task 6 の golden case が `OfficeCapabilityKeys` にこれらの体制キーを入れて参照する。

**背景:** `addition.treatment-improvement.unified.i`〜`.iv` の4行が `effectiveTo: "2026-05"` で閉じており、対応するサービスコード4行も同時に失効する。処遇改善加算は実務上ほぼ全事業所が算定するため、**2026-06 以降は改定対象外の事業所でも請求が成立しない**。ADR 0028 決定7 が明示的に繰り延べた項目。

**既知の観測値**（`docs/open-questions.md` より。**未検証の観測であり、これを出典として seed してはならない**）: `current-fee-notice-html` で (Ⅰ)イ 105 / (Ⅰ)ロ 109 / (Ⅱ)イ 103 / (Ⅱ)ロ 107 / (Ⅲ) 88 / (Ⅳ) 74（各1000分の）を観測。R8 新コード構成（465174〜465176 の追加）との対応と `r8-fee-notice` からの正式抽出が本タスクの作業である。

- [ ] **Step 1: 失効テストを反転させる（失敗させる）**

`ClaimMasterR8BoundaryTests.cs` の `Treatment_improvement_additions_lapse_at_june_2026_until_their_r8_values_land`（212〜231行）を次で置き換える。**fail-close の主張を消すのではなく、R6行の失効は保ったまま R8行の存在を要求する**形にして歯を残す。

```csharp
    [Fact]
    public void Treatment_improvement_additions_switch_generations_at_june_2026()
    {
        // ADR 0045: R6統一処遇改善(Ⅰ)〜(Ⅳ)は2026-05で失効し、2026-06からR8の区分へ入れ替わる。
        // 「R6行が消える」ことと「R8行が現れる」ことの両方を固定する（片方だけでは
        // 沈黙して加算が消える退行を検出できない）。
        var may = Provider.ResolveCalculationMasters(May2026);
        var june = Provider.ResolveCalculationMasters(June2026);

        var mayKeys = may.UnitAdjustments.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        var juneKeys = june.UnitAdjustments.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);

        // R6世代は2026-06に存在しない。
        var r6TreatmentImprovement = mayKeys
            .Where(key => key.Contains("treatment-improvement.unified", StringComparison.Ordinal))
            .ToArray();
        r6TreatmentImprovement.Should().NotBeEmpty("2026-05にはR6統一処遇改善行が存在する");
        r6TreatmentImprovement.Should().OnlyContain(
            key => !juneKeys.Contains(key), "R6統一処遇改善は2026-05で失効する");

        // R8世代が2026-06に存在する。
        var juneTreatmentImprovement = juneKeys
            .Where(key => key.Contains("treatment-improvement.r8", StringComparison.Ordinal))
            .ToArray();
        juneTreatmentImprovement.Should().NotBeEmpty(
            "R8の処遇改善行が入っていなければ、2026-06以降は全事業所で当該加算を算定できない");

        // 対応するサービスコード行も同じ世代交代をする。
        var juneServiceCodeKeys = june.ServiceCodes
            .Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var additionKey in juneTreatmentImprovement)
            juneServiceCodeKeys.Should().Contain(
                key => key.Contains("treatment-improvement.r8", StringComparison.Ordinal),
                $"加算行 {additionKey} に対応するサービスコード行が必要");
    }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterR8BoundaryTests.Treatment_improvement"
```

期待: **FAIL** — `juneTreatmentImprovement.Should().NotBeEmpty()` で「R8の処遇改善行が入っていなければ…」と表示される。

- [ ] **Step 3: `r8-fee-notice` から率を抽出する**

```bash
cd /Users/hiro/Projetct/GitHub/Tsumugi
# 1. SHA-256 照合（f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c）
shasum -a 256 <r8-fee-notice.pdf>

# 2. 就労継続支援B型（第14）の処遇改善節を特定
pdftotext -layout <r8-fee-notice.pdf> - | grep -n "就労継続支援" | head -40

# 3. 該当頁を2方式で独立抽出して突合
pdftotext -layout -f <first> -l <last> <r8-fee-notice.pdf> /tmp/ti-layout.txt
pdftotext -raw    -f <first> -l <last> <r8-fee-notice.pdf> /tmp/ti-raw.txt
```

抽出した率が (Ⅰ)イ／(Ⅰ)ロ／(Ⅱ)イ／(Ⅱ)ロ／(Ⅲ)／(Ⅳ) のどの区分に対応するかを、告示の左欄（改正後）から一意に読み取る。**2方式の出力が一致しない率、または区分との対応が一意に読めない率は採らない。**

- [ ] **Step 4: サービスコードを2形式独立抽出で突合する**

```bash
# xlsx 側（SHA 307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049）
shasum -a 256 <r8-service-codes-2.xlsx>
python3 - <<'PY'
import openpyxl, sys
wb = openpyxl.load_workbook(sys.argv[1] if len(sys.argv)>1 else "<r8-service-codes-2.xlsx>", data_only=True)
ws = wb["18就労継続支援(B・基本)"]
for row in ws.iter_rows(min_row=1, values_only=False):
    values = [c.value for c in row]
    text = " ".join(str(v) for v in values if v is not None)
    if "処遇改善" in text:
        print(row[0].row, "|", text[:160])
PY

# PDF 側（SHA 0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445）
shasum -a 256 <r8-service-codes-2.pdf>
pdftotext -layout <r8-service-codes-2.pdf> - | grep -n "処遇改善" | head -40
```

両出力でサービスコード・公式名称・算定単位が全行一致することを確認する。open-questions が挙げた 465174〜465176 が実際に処遇改善のどの区分に対応するかを、ここで確定する。

- [ ] **Step 5: ADR 0045 に決定表を書く**

`docs/decisions/0045-r8-treatment-improvement-addition-values.md` を作成する。**seed より先に ADR を書く。ADR の決定表が値の唯一の出典になる。**

必須の記載事項:
- 一次資料の同一性検証表（documentId・SHA-256 先頭12桁・照合結果・用途）
- 抽出方式（`-layout` / `-raw` の2方式、xlsx / PDF の2形式）と一致確認の結果
- 決定表: 区分 → サービスコード → 公式名称 → 率 → 算定単位 → 体制届キー
- ADR 0025 の割合加算契約との整合（`percentageBaseScope` / `targetSelector` / `calculationOrder` / `roundingRuleId`）
- 確定できなかった区分があればその一覧と理由
- ADR 0028 決定7 からの引き取りであることの明記

- [ ] **Step 6: `additions.json` へ R8 行を追記する**

R6 の統一処遇改善行と同じ構造を保つ。`effectiveFrom: "2026-06"` / `effectiveTo: null`。

```json
{
  "key": "addition.treatment-improvement.r8.i-i",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [
    {
      "documentId": "r8-fee-notice",
      "sha256": "f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c",
      "locator": "<物理頁と節（例: p.XX 第14の17 左欄改正後 率）>",
      "evidenceRole": "authoritative",
      "supports": ["master-values", "unit-rule-value"]
    },
    {
      "documentId": "r8-service-codes-2-xlsx",
      "sha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
      "locator": "workbook-order=38;row=<行番号>",
      "evidenceRole": "cross-check",
      "supports": ["service-identity", "effective-period"]
    },
    {
      "documentId": "r8-service-codes-2-pdf",
      "sha256": "0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445",
      "locator": "p.<頁>",
      "evidenceRole": "cross-check",
      "supports": ["service-identity", "effective-period"]
    },
    {
      "documentId": "r8-calculation-note",
      "sha256": "0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99",
      "locator": "<単位数の端数処理の頁>（割合加算の四捨五入・ADR 0025）",
      "evidenceRole": "authoritative",
      "supports": ["unit-rule-rounding"]
    }
  ],
  "values": {
    "amount": {
      "kind": "percentage-of-target",
      "percentage": "<抽出値>",
      "applicationKind": "add",
      "percentageBaseScope": "monthly-target-unit-sum",
      "targetSelector": "target.b46.items-1-to-16-4.v1",
      "calculationOrder": 1
    },
    "calculationStepId": "claim.step.units.monthly-target.percentage.v1",
    "roundingRuleId": "claim.rounding.units.half-up.v1",
    "billingUnit": "per-month"
  }
}
```

`percentage` は canonical decimal 文字列（R6 の `"0.093"` と同じ形式。1000分の93 → `"0.093"`）。ADR 0045 の決定表の率を転記する。

- [ ] **Step 7: `service-codes.json` へ対応行を追記する**

R6 の `b-addition.r6-06.treatment-improvement.unified.i` と同形。

```json
{
  "key": "b-addition.r8-06.treatment-improvement.i-i",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [
    {
      "documentId": "r8-service-codes-2-xlsx",
      "sha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
      "locator": "workbook-order=38;row=<行番号>",
      "evidenceRole": "authoritative",
      "supports": [
        "service-identity", "selectors", "unit-rule-kind",
        "unit-rule-step", "unit-rule-target", "effective-period", "conditions"
      ]
    },
    {
      "documentId": "r8-fee-notice",
      "sha256": "f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c",
      "locator": "<率の頁と節>",
      "evidenceRole": "authoritative",
      "supports": ["unit-rule-value"]
    },
    {
      "documentId": "r8-service-codes-2-pdf",
      "sha256": "0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445",
      "locator": "p.<頁>",
      "evidenceRole": "cross-check",
      "supports": [
        "service-identity", "selectors", "unit-rule-kind",
        "unit-rule-step", "unit-rule-target", "effective-period", "conditions"
      ]
    },
    {
      "documentId": "r8-calculation-note",
      "sha256": "0c4f357f4dfd20c71ec0ab8b688db4323d3a4f52c1545fdf46a996cb15485d99",
      "locator": "<端数処理の頁>（ADR 0025）",
      "evidenceRole": "authoritative",
      "supports": ["unit-rule-rounding"]
    }
  ],
  "values": {
    "serviceCode": "<抽出値>",
    "officialLabel": "<公式名称>",
    "serviceKind": "employment-continuation-support-b",
    "selectors": ["selector:b-addition.r8-06.treatment-improvement.i-i"],
    "conditionSelectors": [
      "reward-system-employment-continuation-support-b",
      "<体制届キー>"
    ],
    "unitRule": {
      "kind": "unit-addition",
      "adjustmentComponentKey": "addition.treatment-improvement.r8.i-i",
      "amount": {
        "kind": "percentage-of-target",
        "percentage": "<抽出値>",
        "applicationKind": "add",
        "percentageBaseScope": "monthly-target-unit-sum",
        "targetSelector": "target.b46.items-1-to-16-4.v1",
        "calculationOrder": 1
      },
      "calculationStepId": "claim.step.units.monthly-target.percentage.v1",
      "roundingRuleId": "claim.rounding.units.half-up.v1",
      "billingUnit": "per-month"
    },
    "componentRefs": [
      { "masterKind": "additions", "key": "addition.treatment-improvement.r8.i-i", "role": "adjustment" }
    ]
  }
}
```

**`conditionSelectors` の体制届キーが `conditionDefinitions` に未定義なら、`kind: "office-capability"` の定義を同時に追加する**（R6 の `capability-treatment-improvement-i` と同形。出典は `r8-capability-202606`）。

- [ ] **Step 8: 行数テストを更新する**

`ClaimMasterSeedPhase31Tests.LoadEmbedded_embeds_the_adr0027_r6_seed_row_counts`（34〜41行）は `service-codes = 135 + additions` の関係を検査している。R8 加算行を追加してもこの不変条件は保たれるため**式は変えずに済むはずである**。実行して確認し、崩れる場合のみコメントと期待値を更新する。

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterSeedPhase31Tests.LoadEmbedded_embeds"
```

`ClaimAdditionSeedScopeTests` は R6 のコード集合を固定しているため、R8 のコード集合を検査する `[Fact]` を追加する。

```csharp
    /// <summary>
    /// ADR 0045: R8処遇改善行は2026-06以降のみ有効で、R6統一処遇改善のコードとは重複しない。
    /// </summary>
    [Fact]
    public void R8_treatment_improvement_rows_apply_only_from_2026_06()
    {
        var r8Rows = ServiceCodeEntries()
            .Where(entry => entry.GetProperty("key").GetString()!
                .Contains("treatment-improvement.r8", StringComparison.Ordinal))
            .ToArray();

        r8Rows.Should().NotBeEmpty("R8処遇改善行がseedされていなければならない");

        foreach (var row in r8Rows)
        {
            row.GetProperty("effectiveFrom").GetString().Should().Be("2026-06");
            row.GetProperty("effectiveTo").ValueKind.Should().Be(JsonValueKind.Null);
        }

        var r8Codes = r8Rows
            .Select(row => row.GetProperty("values").GetProperty("serviceCode").GetString()!)
            .ToArray();
        r8Codes.Should().OnlyHaveUniqueItems();
        r8Codes.Should().NotIntersectWith(UnifiedTreatmentImprovementCodes,
            "R8の新コードはR6統一処遇改善のコードと重複しない");
    }
```

> `ServiceCodeEntries()` は既存の同ファイル内ヘルパを使う。存在しない場合は `ClaimMasterR8ContinuityTests` の `OpenSeed` と同じ方式（リポジトリルート探索＋`JsonDocument.Parse`）で `service-codes.json` の `entries` を返す `private static IEnumerable<JsonElement> ServiceCodeEntries()` を同ファイルへ追加する。

- [ ] **Step 9: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMaster"
dotnet test
```

期待: 全緑。Task 1 で作った `ClaimMasterR8ContinuityTests` も、R8 加算行が R8 出典を持つため緑を維持する。

- [ ] **Step 10: 歯の確認**

`additions.json` の R8 処遇改善行を1件コメントアウト（削除）して Step 1 のテストが RED になること、`percentage` を1桁変えて Task 6 の golden case（実装後）が RED になることを確認する。確認後に戻す。

- [ ] **Step 11: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/ \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ \
        docs/decisions/0045-r8-treatment-improvement-addition-values.md \
        docs/open-questions.md
git commit -m "feat(phase3-4/AC3-4-2): R8の福祉・介護職員等処遇改善加算を出典付きで投入する"
```

---

## Task 3: 新12区分の条件トークン14個（AC3-4-3 の前段）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`（`conditionDefinitions` のみ）
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

**Interfaces:**
- Consumes: Task 1 の `sources.json`
- Produces: **12個の `average-wage-band` トークン**（キー命名 `band-48000-plus` / `band-45000-48000` / … 、`value` は公式 option code 11〜22）と **2個の `r8-reform-status` トークン**。Task 4 の `basic-rewards` の `paymentBand` と Task 5 の `conditionSelectors` がこれらのキーを参照する。

**背景:** 新12区分の option code 11〜22 は `transition-rules.json` と既存テスト `R8_band_edition_partitions_official_options_by_reform_status_from_june_2026` により確定済み。残る未確定は**各 option code がどの金額境界に対応するか**だけであり、`r8-b-reward-band-guide` と `r8-fee-notice` で確定する。

- [ ] **Step 1: トークン網羅テストを書く（失敗させる）**

`ClaimMasterSeedPhase31Tests.cs` へ追加する。

```csharp
    /// <summary>
    /// ADR 0046: R8改定対象の新12区分は、transition-rulesが許可するoption 11〜22と
    /// 1対1で対応するaverage-wage-bandトークンを持つ。R8改定状態の2トークンも同時に入る。
    /// </summary>
    [Fact]
    public void R8_seeds_twelve_average_wage_bands_and_two_reform_status_conditions()
    {
        using var document = OpenRepositoryJson(
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json");

        var r8Conditions = document.RootElement.GetProperty("conditionDefinitions")
            .EnumerateArray()
            .Where(condition => condition.GetProperty("effectiveFrom").GetString() == "2026-06")
            .ToArray();

        var bands = r8Conditions
            .Where(condition => condition.GetProperty("kind").GetString() == "average-wage-band")
            .ToArray();
        bands.Should().HaveCount(12, "改定対象の新12区分（option 11〜22）に対応する");

        var optionCodes = bands
            .Select(band => band.GetProperty("value").GetInt32())
            .OrderBy(code => code)
            .ToArray();
        optionCodes.Should().Equal([11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22]);

        foreach (var band in bands)
        {
            band.GetProperty("operator").GetString().Should().Be("equals");
            band.GetProperty("effectiveTo").ValueKind.Should().Be(JsonValueKind.Null);
            band.GetProperty("sourceRefs").GetArrayLength().Should().BeGreaterThan(0);
        }

        var reformStatus = r8Conditions
            .Where(condition => condition.GetProperty("kind").GetString() == "r8-reform-status")
            .ToArray();
        reformStatus.Should().HaveCount(2, "改定対象・改定対象外の2トークン");
        reformStatus.Select(condition => condition.GetProperty("key").GetString())
            .Should().OnlyHaveUniqueItems();
    }
```

> `OpenRepositoryJson` は同ファイルに既存のヘルパ（`Source_manifest_documents_match_the_catalog_and_release_bundles` が使用）。そのまま使う。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterSeedPhase31Tests.R8_seeds_twelve"
```

期待: **FAIL** — `bands.Should().HaveCount(12)` が 0 で落ちる。

- [ ] **Step 3: 区分境界を一次資料から確定する**

```bash
# r8-b-reward-band-guide（SHA 96b002a6aecf76cbf2141fc53aee1c803e7cf78ba2dca52adbf755190e59ab5e）
shasum -a 256 <r8-b-reward-band-guide.pdf>
pdftotext -layout <r8-b-reward-band-guide.pdf> /tmp/band-layout.txt
pdftotext -raw    <r8-b-reward-band-guide.pdf> /tmp/band-raw.txt
grep -n "平均工賃月額" /tmp/band-layout.txt | head -30

# r8-fee-notice の第14 就労継続支援B型 基本報酬の区分見出しでも突合
pdftotext -layout -f <first> -l <last> <r8-fee-notice.pdf> - | grep -n "平均工賃月額" | head -30
```

各区分の（一）（二）…の順序と option code 11〜22 の対応を、`r8-capability-202606`（体制状況一覧表・workbook-order=1;row=242）の選択番号で確定する。**順序の推測をしない。** 番号と金額境界の対応が一意に読めない区分があれば、その区分を投入対象から外し `docs/open-questions.md` へ起票する（Task 4・5 でも該当区分の行を作らない）。

- [ ] **Step 4: ADR 0046 に決定表を書く（前半）**

`docs/decisions/0046-r8-reform-target-payment-bands.md` を作成し、まず**区分の決定表**を書く。

| option code | 公式表記（告示の行見出し） | seed token キー |
| --- | --- | --- |
| 11 | `<抽出値>` | `<決定したキー>` |
| … | … | … |

キー命名は ADR 0027 決定1 の語彙規約に従う（R6 が `band-45000-plus` / `band-35000-45000` / `band-under-10000` としているのと同じ形。`band-<下限>-<上限>` の半開区間、上限なしは `-plus`）。**manifest の `average-wage-48000-or-more` 形式をそのまま持ち込まない。**

`r8-reform-status` の2トークンのキー命名も本 ADR で決める（`kind: "r8-reform-status"` の seed 前例が無いため、本タスクが規約を作る）。

- [ ] **Step 5: `conditionDefinitions` へ14個を追記する**

`service-codes.json` の `conditionDefinitions` 配列へ追加する。R6 の `band-45000-plus` と同形。

```json
{
  "key": "band-48000-plus",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "kind": "average-wage-band",
  "operator": "equals",
  "value": 11,
  "sourceRefs": [
    {
      "documentId": "r8-fee-notice",
      "sha256": "f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c",
      "locator": "<物理頁> 第14の1 （一）平均工賃月額が４万８千円以上の場合",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    },
    {
      "documentId": "r8-b-reward-band-guide",
      "sha256": "96b002a6aecf76cbf2141fc53aee1c803e7cf78ba2dca52adbf755190e59ab5e",
      "locator": "<物理頁> 区分表",
      "evidenceRole": "cross-check",
      "supports": ["conditions"]
    },
    {
      "documentId": "r8-capability-202606",
      "sha256": "84ff0b3b34c2ef857a1bcec221b8c276c177678b403ca6e171b2a08a6d8a150b",
      "locator": "workbook-order=1;row=242（選択番号11）",
      "evidenceRole": "cross-check",
      "supports": ["conditions"]
    }
  ]
}
```

`r8-reform-status` の2件は次の形。

```json
{
  "key": "<ADR 0046で決定したキー>",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "kind": "r8-reform-status",
  "operator": "equals",
  "value": "<改定状態を表す値>",
  "sourceRefs": [
    {
      "documentId": "r8-capability-202606",
      "sha256": "84ff0b3b34c2ef857a1bcec221b8c276c177678b403ca6e171b2a08a6d8a150b",
      "locator": "workbook-order=1;row=242",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    }
  ]
}
```

- [ ] **Step 6: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterSeedPhase31Tests"
dotnet test
```

期待: 全緑。この時点では `basic-rewards` にまだ R8 行が無いため、`ClaimMasterR8BoundaryTests.Reform_target_r8_numeric_options_fail_explicitly_until_their_rows_land` は**まだ緑**（fail-close のまま）である。これは正しい中間状態。

- [ ] **Step 7: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs \
        docs/decisions/0046-r8-reform-target-payment-bands.md docs/open-questions.md
git commit -m "feat(phase3-4/AC3-4-3): R8新12区分の条件トークン14個を出典付きで追加する"
```

---

## Task 4: 新12区分の基本報酬180行（AC3-4-3 の本体）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs:145-154`
- Modify: `docs/decisions/0046-r8-reform-target-payment-bands.md`（決定表の後半）

**Interfaces:**
- Consumes: Task 3 の12個の `average-wage-band` トークンキー
- Produces: `basic-rewards.json` の180行（キー命名 `b-basic.r8.<capacityKey>.<bandKey>.<staffingKey>`）。Task 5 の `service-codes` が `baseComponentKey` / `componentRefs.key` でこれらを参照する。

**背景:** R6 の135行は `paymentBand` 9 × `capacityKey` 5 × `staffingKey` 3 の**完全直積**である（機械検証済み）。R8 の新12区分も同じ 15 組合せを取るため 12 × 15 = 180 行になる。

| 次元 | R6 の値（そのまま R8 でも使う） |
| --- | --- |
| `capacityKey` | `cap-20-or-less` / `cap-21-40` / `cap-41-60` / `cap-61-80` / `cap-81-plus` |
| `staffingKey` | `staff-6-1` / `staff-7.5-1` / `staff-10-1` |

- [ ] **Step 1: 完全直積テストを書く（失敗させる）**

`ClaimMasterSeedPhase31Tests.cs` へ追加する。

```csharp
    /// <summary>
    /// ADR 0046: R8改定対象の新12区分は 12区分 × 定員5 × 人員配置3 = 180行の完全直積を成す。
    /// R6の135行（9区分 × 15）と同じ構造で、欠けも重複も許さない。
    /// 抽出漏れ・過剰転記を行数と直積の完全性で機械検出する。
    /// </summary>
    [Fact]
    public void R8_reform_target_basic_rewards_form_a_complete_product()
    {
        string[] expectedCapacities =
            ["cap-20-or-less", "cap-21-40", "cap-41-60", "cap-61-80", "cap-81-plus"];
        string[] expectedStaffings = ["staff-6-1", "staff-7.5-1", "staff-10-1"];

        using var document = OpenRepositoryJson(
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json");

        var r8Entries = document.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Where(entry => entry.GetProperty("effectiveFrom").GetString() == "2026-06")
            .Select(entry => entry.GetProperty("values"))
            .ToArray();

        r8Entries.Should().HaveCount(180, "12区分 × 定員5 × 人員配置3");

        var bands = r8Entries
            .Select(values => values.GetProperty("paymentBand").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        bands.Should().HaveCount(12);

        r8Entries.Select(values => values.GetProperty("capacityKey").GetString()!)
            .Distinct(StringComparer.Ordinal).Should().BeEquivalentTo(expectedCapacities);
        r8Entries.Select(values => values.GetProperty("staffingKey").GetString()!)
            .Distinct(StringComparer.Ordinal).Should().BeEquivalentTo(expectedStaffings);

        // 完全直積: 各区分がちょうど15行、各(定員,人員配置)組合せがちょうど12行。
        foreach (var band in bands)
            r8Entries.Count(values => values.GetProperty("paymentBand").GetString() == band)
                .Should().Be(15, $"区分 {band} は15組合せすべてを持つ");

        foreach (var capacity in expectedCapacities)
            foreach (var staffing in expectedStaffings)
                r8Entries.Count(values =>
                    values.GetProperty("capacityKey").GetString() == capacity
                    && values.GetProperty("staffingKey").GetString() == staffing)
                    .Should().Be(12, $"組合せ {capacity}/{staffing} は12区分すべてを持つ");

        // サービスコードは全行で一意。
        r8Entries.Select(values => values.GetProperty("serviceCode").GetString()!)
            .Should().OnlyHaveUniqueItems();

        // 単位数は正の整数。
        foreach (var values in r8Entries)
            values.GetProperty("baseUnits").GetInt32().Should().BePositive();
    }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterSeedPhase31Tests.R8_reform_target_basic_rewards"
```

期待: **FAIL** — `r8Entries.Should().HaveCount(180)` が 0 で落ちる。

- [ ] **Step 3: 2形式独立抽出で180行を取り出す**

```bash
# xlsx 側
shasum -a 256 <r8-service-codes-2.xlsx>   # 307b631ed91a...
python3 - <<'PY' > /tmp/r8-basic-xlsx.tsv
import openpyxl
wb = openpyxl.load_workbook("<r8-service-codes-2.xlsx>", data_only=True)
ws = wb["18就労継続支援(B・基本)"]
for row in ws.iter_rows(min_row=1):
    cells = [c.value for c in row]
    # 48列目=合成単位数、49列目=算定単位（ADR 0028 の背景節が記載する列位置）
    code = cells[0]
    if code is None:
        continue
    print("\t".join("" if c is None else str(c) for c in cells))
PY

# PDF 側
shasum -a 256 <r8-service-codes-2.pdf>    # 0ff5071380...
pdftotext -layout <r8-service-codes-2.pdf> /tmp/r8-basic-pdf.txt
```

ADR 0027 決定6 が名指しした**項目3340〜3406等**の範囲から、改定対象向けの基本報酬行を拾う。両形式で **サービスコード・合成単位数・算定単位・区分／定員／人員配置の3次元** が全行一致することを確認する。

**受け入れゲート**: 抽出行数が 180 でない、または直積が完全でない場合は **seed せず原因を特定する**。数が合うまで進めない。抽出漏れ・過剰・区分の取り違えのいずれかである。

**R8 で 15 組合せ（定員5 × 人員配置3）自体が変わっている場合**は本計画の前提が崩れる。その場合は seed せず設計へ差し戻す（spec §9 のリスク）。

- [ ] **Step 4: ADR 0046 に決定表（後半）を書く**

区分×定員×人員配置 → サービスコード → 単位数 の180行を決定表として記録する。ADR 0027 §2.1〜2.3 の様式に倣う。あわせて次を書く。

- 一次資料の同一性検証表（SHA-256 照合結果）
- 2形式独立抽出の一致確認結果（不一致 0 件であること）
- 完全直積であることの確認（各区分15行・各組合せ12行）
- ADR 0027 決定6 からの引き取りであることの明記
- 確定できず投入しなかった区分があればその一覧と理由

- [ ] **Step 5: `basic-rewards.json` へ180行を追記する**

R6 と同形。キーに版 `r8` を含める。

```json
{
  "key": "b-basic.r8.cap-20-or-less.band-48000-plus.staff-6-1",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [
    {
      "documentId": "r8-service-codes-2-xlsx",
      "sha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
      "locator": "workbook-order=38;row=<行番号>",
      "evidenceRole": "authoritative",
      "supports": ["master-values", "effective-period"]
    },
    {
      "documentId": "r8-service-codes-2-pdf",
      "sha256": "0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445",
      "locator": "p.<頁>",
      "evidenceRole": "cross-check",
      "supports": ["master-values", "effective-period"]
    },
    {
      "documentId": "r8-fee-notice",
      "sha256": "f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c",
      "locator": "<物理頁> 第14の1 <区分見出し>",
      "evidenceRole": "cross-check",
      "supports": ["master-values"]
    }
  ],
  "values": {
    "paymentBand": "band-48000-plus",
    "staffingKey": "staff-6-1",
    "capacityKey": "cap-20-or-less",
    "serviceCode": "<抽出値>",
    "baseUnits": <抽出値>
  }
}
```

- [ ] **Step 6: R6継続テストの主張を明確化する**

`ClaimMasterR8BoundaryTests.Basic_reward_rows_continue_unchanged_across_the_r8_boundary`（145〜154行）は `june.Should().BeEquivalentTo(may)` と書いており、R8行を180行足すと **RED になる**。R6行の継続は保ったまま R8行の追加を許す形へ書き換える。

```csharp
    [Fact]
    public void Basic_reward_rows_continue_unchanged_across_the_r8_boundary()
    {
        // ADR 0027 決定6: 改定対象外向けR6基本報酬行（135行）はR8-06でも無変更で継続する。
        // ADR 0046: R8改定対象向けの新12区分180行が2026-06から加わる（R6行は変えない）。
        var may = Provider.ResolveCalculationMasters(May2026).BasicRewards;
        var june = Provider.ResolveCalculationMasters(June2026).BasicRewards;

        may.Should().HaveCount(135);
        june.Should().HaveCount(135 + 180, "R6の135行を保ったままR8改定対象の180行が加わる");

        // R6の135行は1行も変わらず、1行も消えていない。
        june.Should().Contain(may, "R6基本報酬行は135/135が検証済みの継続対象");
    }
```

- [ ] **Step 7: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMaster"
dotnet test
```

期待: 全緑。この時点で `Reform_target_r8_numeric_options_fail_explicitly_until_their_rows_land` は**まだ緑**（`service-codes` 行が無いため resolver はまだ `MasterUnavailable`）。Task 5 で反転する。

- [ ] **Step 8: 歯の確認**

180行のうち1行を削除して Step 1 のテストが RED（`HaveCount(180)` と各区分15行の両方）になることを確認する。1行の `baseUnits` を1増やして ADR 0046 の決定表との齟齬が Task 6 の golden case で検出されることを、Task 6 完了後に確認する。

- [ ] **Step 9: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ \
        docs/decisions/0046-r8-reform-target-payment-bands.md
git commit -m "feat(phase3-4/AC3-4-3): R8改定対象の新12区分 基本報酬180行を投入する"
```

---

## Task 5: 新12区分のサービスコード180行と fail-close の反転（AC3-4-3 の完了）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs:182-196`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs:34-41`

**Interfaces:**
- Consumes: Task 3 のトークンキー、Task 4 の `basic-rewards` キー（`b-basic.r8.<capacity>.<band>.<staffing>`）
- Produces: `ServiceCodeResolver.ResolveBasicReward` が `ReformTarget` × option 11〜22 で解決できる状態。Task 6 の golden case が依存する。

- [ ] **Step 1: fail-close テストを反転させる（失敗させる）**

`ClaimMasterR8BoundaryTests.cs` の `Reform_target_r8_numeric_options_fail_explicitly_until_their_rows_land`（182〜196行）を次で置き換える。**fail-close の歯は残す** — 全12区分が解決できることと、依然として解決できない組合せ（改定対象外の option を改定対象が使う等）が例外になることの両方を主張する。

```csharp
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    public void Reform_target_offices_resolve_every_r8_numeric_band(int officialOptionCode)
    {
        // ADR 0046: 改定対象の新12区分（option 11〜22）はseed投入済みで、2026-06に解決できる。
        var juneMasters = Provider.ResolveCalculationMasters(June2026);

        var resolved = ServiceCodeResolver.ResolveBasicReward(
            juneMasters, June2026,
            Context(Numeric(officialOptionCode), R8ReformStatus.ReformTarget));

        resolved.ServiceCode.Should().NotBeNullOrWhiteSpace();
        resolved.UnitsPerDay.Should().BePositive();

        // 経過措置ruleでも許可されている（runtime guardと整合）。
        SingleTransitionRule(June2026)
            .AllowedOptionsByR8ReformStatus[R8ReformStatus.ReformTarget]
            .Should().Contain(Numeric(officialOptionCode));
    }

    [Fact]
    public void Reform_target_offices_still_fail_closed_on_r6_numeric_bands()
    {
        // 新12区分を投入しても、改定対象がR6数値区分で請求する経路は開かない。
        var juneMasters = Provider.ResolveCalculationMasters(June2026);

        foreach (var r6NumericCode in new[] { 1, 2, 3, 4, 5, 6, 7, 9 })
        {
            var action = () => ServiceCodeResolver.ResolveBasicReward(
                juneMasters, June2026, Context(Numeric(r6NumericCode), R8ReformStatus.ReformTarget));

            action.Should().Throw<ServiceCodeResolutionException>(
                $"改定対象がR6区分option {r6NumericCode}で2026-06を請求することはフェイルクローズする");
        }
    }
```

> `Reform_target_offices_resolve_every_r8_numeric_band` は各 option が固有のサービスコードを返すことまでは主張しない（区分と定員・人員配置の組合せで決まるため、`Context` が固定する `cap-20-or-less`／`staff-6-1` の下で12個の異なるコードが返る想定だが、コードの実値は ADR 0046 の決定表が唯一の出典であり、テストにハードコードしない）。実値の検証は Task 6 の golden case が担う。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterR8BoundaryTests.Reform_target"
```

期待: `Reform_target_offices_resolve_every_r8_numeric_band` が12ケースすべて **FAIL**（`ServiceCodeResolutionException` / `MasterUnavailable`）。`Reform_target_offices_still_fail_closed_on_r6_numeric_bands` は **PASS**。

- [ ] **Step 3: `service-codes.json` へ180行を追記する**

Task 4 の各 `basic-rewards` 行に1対1で対応させる。R6 の基本報酬サービスコード行と同形。

```json
{
  "key": "b-service.r8.cap-20-or-less.band-48000-plus.staff-6-1",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [
    {
      "documentId": "r8-service-codes-2-xlsx",
      "sha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
      "locator": "workbook-order=38;row=<行番号>",
      "evidenceRole": "authoritative",
      "supports": [
        "service-identity", "selectors", "unit-rule-kind",
        "effective-period", "unit-rule-target", "unit-rule-step", "conditions"
      ]
    },
    {
      "documentId": "r8-service-codes-2-pdf",
      "sha256": "0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445",
      "locator": "p.<頁>",
      "evidenceRole": "cross-check",
      "supports": [
        "service-identity", "selectors", "unit-rule-kind",
        "effective-period", "unit-rule-target", "unit-rule-step", "conditions"
      ]
    }
  ],
  "values": {
    "serviceCode": "<抽出値・basic-rewardsの同一行と一致すること>",
    "officialLabel": "<公式略称>",
    "serviceKind": "employment-continuation-support-b",
    "selectors": [
      "selector:b-service.r8.cap-20-or-less.band-48000-plus.staff-6-1",
      "target.b46.items-1-to-16-4.v1"
    ],
    "conditionSelectors": [
      "reward-system-employment-continuation-support-b",
      "band-48000-plus",
      "cap-20-or-less",
      "staff-6-1",
      "<Task 3で決めたr8-reform-status改定対象トークン>"
    ],
    "unitRule": {
      "kind": "formula",
      "mode": "base-component-pass-through",
      "baseComponentKey": "b-basic.r8.cap-20-or-less.band-48000-plus.staff-6-1",
      "calculationStepId": "claim.step.units.service-code.base-component-pass-through.v1",
      "roundingRuleId": null,
      "billingUnit": "per-day"
    },
    "componentRefs": [
      {
        "masterKind": "basic-rewards",
        "key": "b-basic.r8.cap-20-or-less.band-48000-plus.staff-6-1",
        "role": "base"
      }
    ]
  }
}
```

**`baseComponentKey` と `componentRefs[0].key` は Task 4 の `basic-rewards` キーと文字列一致していなければならない。** 不一致は resolver が `MasterUnavailable` を返す原因になる。

- [ ] **Step 4: 対応の完全性テストを追加する**

`ClaimMasterSeedPhase31Tests.cs` へ追加する。

```csharp
    /// <summary>
    /// ADR 0046: R8基本報酬180行とR8基本報酬サービスコード180行は1対1で対応し、
    /// componentRefsのキーとserviceCodeが両ファイルで一致する。
    /// </summary>
    [Fact]
    public void R8_basic_reward_rows_pair_with_their_service_code_rows()
    {
        using var basicRewards = OpenRepositoryJson(
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json");
        using var serviceCodes = OpenRepositoryJson(
            "src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json");

        var basicByKey = basicRewards.RootElement.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.GetProperty("effectiveFrom").GetString() == "2026-06")
            .ToDictionary(
                entry => entry.GetProperty("key").GetString()!,
                entry => entry.GetProperty("values").GetProperty("serviceCode").GetString()!,
                StringComparer.Ordinal);

        basicByKey.Should().HaveCount(180);

        var pairedBasicKeys = new List<string>();
        foreach (var entry in serviceCodes.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (entry.GetProperty("effectiveFrom").GetString() != "2026-06")
                continue;

            var values = entry.GetProperty("values");
            var componentRefs = values.GetProperty("componentRefs").EnumerateArray()
                .Where(reference => reference.GetProperty("masterKind").GetString() == "basic-rewards")
                .ToArray();
            if (componentRefs.Length == 0)
                continue;   // 加算のサービスコード行はここでは対象外

            var basicKey = componentRefs.Single().GetProperty("key").GetString()!;
            basicByKey.Should().ContainKey(basicKey,
                "サービスコード行が参照するbasic-rewardsキーは実在しなければならない");
            values.GetProperty("serviceCode").GetString().Should().Be(basicByKey[basicKey],
                $"{basicKey} のサービスコードは両ファイルで一致しなければならない");
            values.GetProperty("unitRule").GetProperty("baseComponentKey").GetString()
                .Should().Be(basicKey);

            pairedBasicKeys.Add(basicKey);
        }

        pairedBasicKeys.Should().OnlyHaveUniqueItems();
        pairedBasicKeys.Should().BeEquivalentTo(basicByKey.Keys,
            "R8基本報酬180行はすべて対応するサービスコード行を持つ");
    }
```

- [ ] **Step 5: 行数テストを更新する**

`LoadEmbedded_embeds_the_adr0027_r6_seed_row_counts`（34〜41行）の `basic-rewards = 135` と `service-codes = 135 + additions` は R8 行の追加で崩れる。名称と主張を更新する。

```csharp
    // ADR 0027 fixes 135 basic-reward rows (service fee I-III: 3 staffing x 5 capacity x
    // 8 wage bands = 120, plus participation-evaluation IV-VI: 15) and 8 region unit prices.
    // ADR 0046 adds 180 R8 reform-target rows (12 new bands x 5 capacity x 3 staffing).
    // service-codes carries one row per basic-reward row plus one per seeded addition row
    // (see ClaimAdditionSeedScopeTests for the per-code scope). This asserts the seed JSON
    // itself (not the not-yet-wired ResolveCalculationMasters) carries those counts.
    [Fact]
    public void LoadEmbedded_embeds_the_seeded_basic_reward_and_service_code_counts()
    {
        const int R6BasicRewardRows = 135;
        const int R8ReformTargetBasicRewardRows = 180;
        var basicRewardEntries = CountEmbeddedEntries(".ClaimMasters.Seed.basic-rewards.json");
        basicRewardEntries.Should().Be(R6BasicRewardRows + R8ReformTargetBasicRewardRows);

        var additionEntries = CountEmbeddedEntries(".ClaimMasters.Seed.additions.json");
        CountEmbeddedEntries(".ClaimMasters.Seed.service-codes.json")
            .Should().Be(basicRewardEntries + additionEntries);
        CountEmbeddedEntries(".ClaimMasters.Seed.region-unit-prices.json").Should().Be(8);
    }
```

- [ ] **Step 6: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMaster"
dotnet test
```

期待: 全緑。特に次を確認する。
- `Reform_target_offices_resolve_every_r8_numeric_band` の12ケースすべて PASS
- `Reform_target_offices_still_fail_closed_on_r6_numeric_bands` PASS
- `Basic_reward_rows_continue_unchanged_across_the_r8_boundary` PASS（Task 4 で更新済み）
- `ClaimMasterR8ContinuityTests` PASS（R8行はすべて R8 出典を持つ）

- [ ] **Step 7: 歯の確認**

`service-codes.json` の R8 基本報酬行1件の `baseComponentKey` を存在しないキーに書き換え、Step 4 のテストが RED になることを確認する。1件を削除して Step 1 の12ケースのうち1つが RED になることを確認する。確認後に戻す。

- [ ] **Step 8: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/
git commit -m "feat(phase3-4/AC3-4-3): R8新12区分のサービスコード180行を投入しfail-closeを解除する"
```

---

## Task 6: golden case と回帰（AC3-4-4）

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`
- Modify: `docs/decisions/0045-r8-treatment-improvement-addition-values.md`（worked example 節を追記）
- Modify: `docs/decisions/0046-r8-reform-target-payment-bands.md`（worked example 節を追記）

**Interfaces:**
- Consumes: Task 2 の R8 処遇改善率、Task 4 の R8 基本報酬単位数、Task 1 が確定した地域単価
- Produces: なし（本計画の最終検証タスク）

**背景:** `ClaimCalculatorGoldenCaseTests` は Domain テストであり Infrastructure の seed へ依存できない。したがってマスタ行の値を**テストファイル内に再掲**する（既存の `Masters()` ヘルパが R6 行を再掲しているのと同じ方式。ファイル冒頭の XML doc コメント参照）。値の出典は ADR 0045 / 0046 の決定表である。

**前提:** Task 1 の分岐 (c)（地域単価が確定できず `effectiveTo: "2026-05"` で閉じた）を取った場合、2026-06 の請求は地域単価未解決で成立しない。その場合は **Step 1〜4 をスキップし、代わりに Step 5 の「閉じた場合の回帰」だけを実施する**。どちらを実施したかを `docs/phase3-4-acceptance.md` に記録する。

- [ ] **Step 1: 改定対象外 × 2026-06 の golden case を書く（失敗させる）**

`ClaimCalculatorGoldenCaseTests.cs` へ追加する。既存の `Matches_adr_0028_worked_example_b_unified_treatment_improvement` を雛形にする（同ファイル190行付近）。

```csharp
    /// <summary>
    /// ADR 0045 worked example: 改定対象外事業所 × 2026-06。
    /// R6基本報酬行（ADR 0027決定6により継続）＋ R8処遇改善（ADR 0045決定表）。
    /// 期待値の算出過程はADR 0045のworked example節に記載する。
    /// </summary>
    [Fact]
    public void Matches_adr_0045_worked_example_reform_exempt_office_in_june_2026()
    {
        var context = new ClaimBillingConditionContext(
            RewardSystem: "b-type",
            PaymentBand: "band-20000-25000",
            CapacityHeadcount: 20,
            StaffingKey: "staff-7.5-1",
            AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 5),
            R8ReformStatus: R8ReformStatus.ReformExempt,
            OfficeCapabilityKeys: ["<Task 2で決めたR8処遇改善の体制届キー>"]);

        var result = ClaimCalculator.Calculate(R8Masters(), new ClaimCalculationRequest(
            new ServiceMonth(2026, 6), context, "region-grade-2", "b-type",
            [new RecipientClaimSource(
                RecipientA, billedDays: 22, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: UnboundedSyntheticCapYen,
                BurdenCategory: SyntheticBurdenCategory)],
            CountSelectorBindings));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalUnits.Should().Be(<ADR 0045決定表からの算出値>);
        detail.TotalCostYen.Should().Be(<ADR 0045決定表からの算出値>);
        detail.BurdenYen.Should().Be(<ADR 0045決定表からの算出値>);
        detail.BenefitYen.Should().Be(<ADR 0045決定表からの算出値>);
    }
```

`R8Masters()` は既存の `Masters()` と同じ方式で、R8 の行（Task 2 の処遇改善率、Task 4 の該当基本報酬行、Task 1 で確定した地域単価）を再掲する `private static ClaimCalculationMasters R8Masters()` を同ファイルへ追加する。

- [ ] **Step 2: 改定対象 × 新区分 × 2026-06 の golden case を書く**

```csharp
    /// <summary>
    /// ADR 0046 worked example: 改定対象事業所 × 新12区分 × 2026-06。
    /// 新区分の基本報酬（ADR 0046決定表）＋ R8処遇改善（ADR 0045決定表）。
    /// </summary>
    [Fact]
    public void Matches_adr_0046_worked_example_reform_target_office_in_june_2026()
    {
        var context = new ClaimBillingConditionContext(
            RewardSystem: "b-type",
            PaymentBand: "<Task 3で決めた新区分トークン>",
            CapacityHeadcount: 20,
            StaffingKey: "staff-6-1",
            AverageWageBandOption: new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 11),
            R8ReformStatus: R8ReformStatus.ReformTarget,
            OfficeCapabilityKeys: ["<Task 2で決めたR8処遇改善の体制届キー>"]);

        var result = ClaimCalculator.Calculate(R8Masters(), new ClaimCalculationRequest(
            new ServiceMonth(2026, 6), context, "region-grade-1", "b-type",
            [new RecipientClaimSource(
                RecipientA, billedDays: 23, BenefitRatePercent: 90,
                CertificateMonthlyCapYen: UnboundedSyntheticCapYen,
                BurdenCategory: SyntheticBurdenCategory)],
            CountSelectorBindings));

        var detail = result.Details.Should().ContainSingle().Subject;
        detail.TotalUnits.Should().Be(<ADR 0046決定表からの算出値>);
        detail.TotalCostYen.Should().Be(<ADR 0046決定表からの算出値>);
        detail.BurdenYen.Should().Be(<ADR 0046決定表からの算出値>);
        detail.BenefitYen.Should().Be(<ADR 0046決定表からの算出値>);
    }
```

- [ ] **Step 3: ADR に worked example 節を追記する**

各 ADR に「手計算検証ケース（golden case 期待値）」節を追加し、**算出過程を1行ずつ書く**。ADR 0027 §4 / ADR 0028 決定6 と同じ様式。

```
基本 <単位数>×<日数>=<小計>単位 ＋ 処遇改善 <小計>×<率>=<加算単位（四捨五入）> ＝ <合計>単位
総費用額 <合計単位>×<単価>円=<円（小数）>→<円（切捨て）>円
1割相当額 <総費用額>×10/100=<円（小数）>→<円（切捨て）>円
給付費 <総費用額>−<1割相当額>=<円>円
```

丸めは ADR 0025 の契約（割合加算は四捨五入、金額は切捨て）に従う。テストの期待値はこの算出過程の結果と一致しなければならない。

- [ ] **Step 4: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimCalculatorGoldenCaseTests"
```

期待: 全 PASS。既存の ADR 0027 3ケース・ADR 0028 3ケースも緑のままであること（R8 行の追加は 2025-04 の解決に影響しない）。

- [ ] **Step 5: 版境界の回帰を固定する**

`ClaimMasterR8BoundaryTests.cs` へ追加する。Task 1 でどの分岐を取ったかで主張が変わる。

**Task 1 が分岐 (a)/(b) の場合**（地域単価が 2026-06 で解決できる）:

```csharp
    [Fact]
    public void Region_unit_prices_and_burden_caps_resolve_in_june_2026()
    {
        // ADR 0044: 地域単価・負担上限はR8出典に裏付けられて2026-06でも解決する。
        var june = Provider.ResolveCalculationMasters(June2026);

        june.RegionUnitPrices.Should().NotBeEmpty(
            "地域単価が解決できなければ総費用額を算出できない");
        june.BurdenCaps.Should().NotBeEmpty(
            "負担上限が解決できなければ利用者負担を確定できない");
    }
```

**Task 1 が分岐 (c) の場合**（確定できず閉じた）:

```csharp
    [Fact]
    public void Region_unit_prices_fail_closed_in_june_2026_until_the_r8_source_lands()
    {
        // ADR 0044: R8版の単価出典を確定できなかったため、R6行を2026-05で閉じた。
        // 2026-06の請求は地域単価未解決で成立しない（古い単価での静かな誤請求を防ぐ）。
        // docs/open-questions.md の解除条件を満たしたら本テストを反転する。
        var may = Provider.ResolveCalculationMasters(May2026);
        var june = Provider.ResolveCalculationMasters(June2026);

        may.RegionUnitPrices.Should().NotBeEmpty();
        june.RegionUnitPrices.Should().BeEmpty(
            "確定できない単価で請求を生成するより、生成を止める方が回復可能である");
    }
```

- [ ] **Step 6: ハード制約の機械判定を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimSpecificationBoundaryTests"
dotnet test --filter "FullyQualifiedName~OfflineComplianceTests"
dotnet test --filter "FullyQualifiedName~ArchitectureTests"
```

期待: 全緑。制度実値の C# 直書きが増えていないこと（golden case のテスト内再掲は既存の許容パターンであり、`ClaimSpecificationBoundaryTests` の対象は `src/` である）。

- [ ] **Step 7: 全体品質ゲート**

```bash
./build/ci.sh
```

期待: 全緑。Domain カバレッジ ≧95%。

- [ ] **Step 8: コミット**

```bash
git add tests/ docs/decisions/
git commit -m "test(phase3-4/AC3-4-4): R8のgolden caseと版境界回帰を固定する"
```

---

## Task 7: 文書同期と受け入れ証跡

**Files:**
- Create: `docs/phase3-4-acceptance.md`
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Modify: `CLAUDE.md`（「現在地」節）
- Modify: `docs/decisions/0027-r6-basic-reward-service-code-region-price-values.md`（決定6 に引き取り先を追記）
- Modify: `docs/decisions/0028-r6-major-addition-values.md`（決定7 に引き取り先を追記）
- Modify: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md`（前提解消の追記）

**Interfaces:**
- Consumes: Task 1〜6 の全成果
- Produces: なし（最終タスク）

- [ ] **Step 1: `docs/phase3-4-acceptance.md` を作成する**

Phase 3-3 の証跡（`docs/phase3-3-acceptance.md`）と同じ構成にする。必須の記載事項:

- AC3-4-1〜4 それぞれの達成状況と証拠（テスト名を名指しする）
- **Task 1 でどの分岐（a/b/c）を取ったか**と、その帰結（2026-06 の請求が成立するか否か）
- 投入した行数の実績（`basic-rewards` +180、`service-codes` +180+N、`additions` +N、`conditionDefinitions` +14）
- 確定できず投入しなかった項目の一覧
- spec / plan からの逸脱と理由
- 歯の確認（意図的違反で RED になることを確認したテストの一覧）
- 残課題（GUI 手動貫通確認が未実施であることを含む）

- [ ] **Step 2: `docs/open-questions.md` を更新する**

クローズする項目に `[x]` とクローズ日・ADR 番号を付け、**何をどう確定したか**を書く（既存項目の書式に倣う）。

- 「R8.6 サービスコード表の実値投入」（17行目）— Task 4・5 の範囲でクローズ。ただし本計画は B型基本報酬と処遇改善に限定したため、**その旨を明記して部分クローズとする**か、範囲を絞った新項目へ置き換える
- 「R8-06改定対象（reform-target）の新12区分基本報酬行と option 10 の R8 状態対応」（13行目）— **新12区分のみクローズ**。option 10 の R8 状態対応は一次資料未確定のまま残す
- 「R8-06 の福祉・介護職員等処遇改善加算の率・新コード対応」（11行目）— Task 2 でクローズ

新規に起票する項目（該当する場合）:
- Task 1 の分岐 (c) を取った場合の地域単価・負担上限の未確定
- 一意に確定できず投入しなかった区分・加算区分

- [ ] **Step 3: `CHANGELOG.md` に節を追加する**

`## [Unreleased]` の下へ、Phase 3-3 完了節と同じ様式で追加する。

```markdown
## Phase 3-4 完了 (2026-XX-XX)

- 令和8年6月施行分（`claim-master-r8-06`）の制度実値を投入し、2026-06 以降の請求が
  改定対象・改定対象外を問わず成立するようにした
- 地域区分単価・負担上限額の R8 適用を出典付きで確定（ADR 0044）
- 福祉・介護職員等処遇改善加算の R8 率・サービスコードを投入（ADR 0045）
- R8 改定対象の新12区分（条件トークン14個・基本報酬180行・サービスコード180行）を投入（ADR 0046）
- `ClaimMasterR8ContinuityTests` 追加 — 2026-06 へ到達する全 entry が R8 出典を持つか
  適用期間が閉じているかを網羅検査する
- ADR 0027 決定6 / ADR 0028 決定7 の繰り延べを引き取ってクローズ
```

あわせて「### 計画」節の Phase 4 の行から、本スライスで解消した前提を削る。

- [ ] **Step 4: `CLAUDE.md` の「現在地」を更新する**

現在の記述は「Phase 3-3 は完了…次は Phase 4 準備」である。Phase 3-4 の完了と、次が Phase 4（`docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` の S2〜S5）であることへ書き換える。**§ハード制約3 に、制度実値の版が R6／R8 の2世代並存であることを1行で追記する。**

- [ ] **Step 5: ADR 0027 / 0028 へ引き取り先を追記する**

- `0027-...md` 決定6 の「別ADRで確定する」に、ADR 0046 で確定した旨と日付を追記する
- `0028-...md` 決定7 の「本ADRのスコープ外」に、ADR 0045 で確定した旨と日付を追記する

既存の決定文を書き換えず、**追記**の形にする（ADR は決定時点の記録であるため）。

- [ ] **Step 6: Phase 4 ロードマップへ前提解消を追記する**

`docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` の冒頭 Status 行に、Phase 3-4 完了により Phase 4 S2 へ着手可能であることを追記する。

- [ ] **Step 7: 最終確認**

```bash
./build/ci.sh
dotnet format --verify-no-changes
git status --short          # 意図しない差分がないこと
```

- [ ] **Step 8: コミット**

```bash
git add docs/ CHANGELOG.md CLAUDE.md
git commit -m "docs(phase3-4): 受け入れ証跡とADR 0044-0046の同期、open-questionsのクローズ"
```

---

## Self-Review（計画作成時に実施済み）

**1. Spec coverage** — spec の各節に対応するタスクを確認した。

| spec | タスク |
| --- | --- |
| §3.1 Task A 地域単価・負担上限 | Task 1（AC3-4-1） |
| §3.2 Task B 処遇改善 | Task 2（AC3-4-2） |
| §3.3 Task C 新12区分（トークン→基本報酬→サービスコード） | Task 3・4・5（AC3-4-3） |
| §3.4 Task D golden・回帰 | Task 6（AC3-4-4） |
| §4 非スコープ | 各タスクで対象を限定。Task 2 Step 6 で処遇改善系に限定を明記 |
| §5.1 出典と抽出 | Global Constraints ＋ 各タスクの抽出 Step |
| §5.2 fail-close | Global Constraints ＋ Task 1 Step 5 分岐(c) ＋ Task 6 Step 5 |
| §5.3 追記であって差し替えではない | Global Constraints ＋ Task 1 Step 5・6（値を変えない） |
| §6 テスト戦略 | 各タスクの Step 1（Red）と「歯の確認」Step |
| §7 ADR 計画 | Task 1 Step 9、Task 2 Step 5、Task 3 Step 4、Task 4 Step 4 |
| §8 成果物 | File Structure ＋ Task 7 |
| §9 リスク | Task 3 Step 3（区分未確定）、Task 4 Step 3（行数不一致・15組合せ変更）、Task 1 Step 5(c) |
| §10 未確定事項 | Task 1 Step 3（出典の所在）、Task 3 Step 3（option code 対応）、Task 3 Step 4（命名規約） |

**2. Placeholder scan** — `<抽出値>` `<行番号>` `<物理頁>` は制度値・出典位置の**意図的な契約スロット**であり、その理由を「制度値を計画に書けないことについて」節で明示した。それ以外の TODO・TBD・「適切に処理する」類の記述は無い。

**3. Type consistency** — 全テストコードで使う型を「既存 API リファレンス」節に実物から転記した。キー命名は `b-basic.r8.<capacity>.<band>.<staffing>` と `b-service.r8.<capacity>.<band>.<staffing>` で Task 4・5 間を貫通させ、Task 5 Step 4 のテストが両者の一致を機械検証する。

**修正した齟齬（計画作成時）**: spec §2.3 は新12区分の option code 対応を「仮説」としていたが、既存テスト `R8_band_edition_partitions_official_options_by_reform_status_from_june_2026` が option 11〜22 = ReformTarget を既に固定していることを確認した。本計画の「既存 API リファレンス」節でこれを明記し、Task 3 の未確定を「各 option code がどの金額境界に対応するか」だけに絞った。

**修正した齟齬（2026-07-26 着手前スキャン）**: Task 1 の `ClaimMasterR8ContinuityTests` の対象を5ファイルにしていたが、**spec §3.1 AC3-4-1 の対象は `region-unit-prices` と `burden-caps` の2ファイル**であり、計画側の逸脱だった。広げた対象を緑にするため Task 1 に「`basic-rewards` 135行・`service-codes` 147行・`additions` 12行の計294行へ手書き locator で cross-check 出典を追記する」Step を置いており、これが (a) Task 1 を本来の責務から肥大させ、(b) 本来 Task 4 の依存である `r8-service-codes-2-xlsx` への依存を Task 1 に持ち込んでいた。

利用者の裁定により **spec に合わせて2ファイルへ絞り、294行追記の Step を削除**した（Step 6 を削除し、旧 Step 7〜10 を 6〜9 へ繰り上げ）。294行の R8 継続根拠は ADR 0027 決定6・ADR 0028 決定1 の記録と、runtime で固定している `ClaimMasterR8BoundaryTests.Exempt_offices_resolve_the_same_code_and_units_across_the_boundary` に委ねる。既存の証跡を高い転記ミスリスクで複製しないという判断である。

---

## 参照

- 設計 spec: `docs/superpowers/specs/2026-07-26-phase3-4-r8-06-master-values-design.md`
- `docs/decisions/0020-claim-master-sources-and-versioning.md` — 出典登録と再検証の規律
- `docs/decisions/0025-claim-rounding-rules.md` — 割合加算の source row 契約と丸め
- `docs/decisions/0027-r6-basic-reward-service-code-region-price-values.md` — 決定1（token 語彙）・決定6（繰り延べ）
- `docs/decisions/0028-r6-major-addition-values.md` — 抽出方式の様式・決定7（繰り延べ）
- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` — 本スライス完了後に着手する Phase 4 S2〜S5
