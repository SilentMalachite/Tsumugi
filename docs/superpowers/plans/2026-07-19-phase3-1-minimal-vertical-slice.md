# Phase 3-1 最小垂直スライス 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 典型的な就労継続支援B型事業所の1ヶ月分の請求を、入力 → 算定プレビュー → 確定までUIから通し、golden caseテストで公式計算例と一致させる。

**Architecture:** 既存の入力土台（ClaimInput系）と確定土台（ClaimFinalizationOperationV1/ClaimBatch）の間を、seed実値 → ServiceCodeResolver → ClaimCalculator（Domain純粋関数） → SnapshotReader → production codec → Calculate/Close/Cancel/Query UseCase → ClaimPreparation画面、の一本のパイプラインで接続する。新しい抽象は増やさず既存interfaceの実装を埋める。まずTask 1〜10で「基本報酬のみ」を貫通させ、Task 11〜13で加算 → 利用者負担 → R8切替を縦に積む。

**Tech Stack:** .NET 10 / C# 14, Avalonia 11 + CommunityToolkit.Mvvm, EF Core 10 + SQLite, xUnit + FluentAssertions

**正本spec:** `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`

## Global Constraints

- **進捗は本計画のチェックボックスのみで表す。** spin-off計画文書の新規作成禁止。計画文書の修正が3コミット連続したら停止しユーザーに相談（specの§4）
- オフライン専用。全プロダクションアセンブリで通信API禁止（`OfflineComplianceTests` / `AppOfflineComplianceTests` が機械判定）
- 制度実値をDomain/Applicationにハードコードしない（`ClaimSpecificationBoundaryTests` が機械判定。|値|≥10の数値リテラル等を走査）。制度値はseed JSON（埋め込みリソース）のみ
- **一次資料から一意に確定できない値は推測しない。** そのservice codeごとスコープ外にし `docs/open-questions.md` に1行起票して先へ進む
- seed実値のコード化はADRレビュー確定後のみ（正本仕様§2.3）。本計画のADR番号は0027・0028を使用（先行ADRが増えていたら次番号へずらす）
- 金額は整数円、単価等の小数は `decimal`（`double`/`float` 禁止）。端数規則はADR 0025に従いrounding rule IDで供給
- エンティティは `record`＋append-only。主キー `Guid`。`CreatedAt`/`CreatedBy`/`ConcurrencyToken` 必須（`Entity` 基底）
- `<Nullable>enable</Nullable>`＋`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。警告ゼロ・全テスト緑・`./build/ci.sh` 緑を維持
- カバレッジ: Domain≧95%（line）、Application≧70%（line）。`Logic.Claim` 分岐100%目標
- TDD必須（Red→Green→Refactor）。1コミット=1論理変更。コミットメッセージにタスク番号
- 依存方向: App → Application → Domain、Infrastructure → Application/Domain。UIから `DbContext` 直接参照禁止
- 時刻は `TimeProvider clock` を注入し `clock.GetUtcNow()`。純粋関数（Domain Logic）には時刻を渡さず値で受ける

## 既存コードの前提（2026-07-19調査で確定した契約）

- seedは**埋め込みリソース**。`JsonClaimMasterProvider.LoadEmbedded()` が `.ClaimMasters.Seed.{basic-rewards|additions|region-unit-prices|burden-caps|transition-rules|service-codes}.json` と `sources.json` を読む。検証は `ClaimMasterFileValidator.Prepare(...)` → `ValidateAll(...)`（例外ベース、`InvalidDataException` 等）
- seedヘッダは `{"schemaVersion":"2","masterKind":"...","entries":[]}`。service-codesのみ `conditionDefinitions` を持つ。entry必須: `key/effectiveFrom/effectiveTo/sourceRefs/values`。sourceRef必須: `documentId/sha256/locator/evidenceRole/supports`
- マスタ束は `Tsumugi.Domain.Logic.Claim.Models.ClaimCalculationMasterBundle`（BasicRewards / UnitAdjustments / RegionUnitPrices / BurdenCaps / TransitionRules / ServiceCodes / ConditionDefinitions）
- `IClaimMasterProvider`（Application.Abstractions）は現在 `ResolveVersion(ServiceMonth)` のみ。算定マスタを渡す口がない → Task 4で拡張
- `ValidatedClaimSnapshotEnvelope.CreateValidated` は **Application内部限定** → production codecは `Tsumugi.Application` 内に置く（Task 8）
- 確定土台: `IClaimFinalizationStore.CommitAsync(ClaimFinalizationDraft, ct)` 実装済み。`ClaimFinalizationStore` は `IDbContextFactory<TsumugiDbContext>` + `SqliteConnection.BeginTransaction(deferred: false)` の明示tx。読み取り専用パスは `RollbackAsync`
- UseCase規約: primary-constructor DI、`ExecuteAsync(request, actor, ct)` がDTOを返し、失敗は型付き例外（`ClaimInputSaveException` / `ClaimFinalizationException(ClaimErrorCode)` 等）。Result型モナドは無い
- UI: `AppSection.ClaimPreparation = 16` 宣言済み。`MainViewModel.DispatchAsync` に `NavigationTargetUnavailable` を返す短絡あり（除去対象）。TabItemは `src/Tsumugi.App/MainWindow.axaml`。DI登録は `src/Tsumugi.App/CompositionRoot.cs` と `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- テスト規約: `tests/<Project>/…` にsrcミラー配置、`<TypeUnderTest>Tests.cs`、xUnit `[Theory]`+`TheoryData`+FluentAssertions。SQLiteは `SqliteFixture`（一時ファイルDB + `Database.Migrate()`、`IClassFixture`）

---

### Task 1: ワークツリー浄化とPhase 3-0正式クローズ

**Files:**
- Modify: `.gitignore`（`graphify-out/` 追加）
- Modify: `CLAUDE.md`（「現在地」を本計画基準へ）
- Commit: `docs/superpowers/plans/2026-07-11-phase3-0-task16-acceptance-closeout.md`（未追跡）、`.serena/project.yml`（ツール再生成の無害diff）
- Discard: `docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md` の未コミットdiff

**Interfaces:**
- Consumes: なし（git操作と文書更新のみ）
- Produces: クリーンなワークツリー。以後の全タスクの前提

- [x] **Step 1: 旧マスター計画の未コミットdiff（Task 11分割追記）を破棄**

```bash
git checkout -- docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md
```

- [x] **Step 2: `.gitignore` 末尾に追記**

```
graphify-out/
```

- [x] **Step 3: Phase 3-0 closeout文書とserena設定をコミット**

```bash
git add docs/superpowers/plans/2026-07-11-phase3-0-task16-acceptance-closeout.md
git commit -m "docs(phase3-0): commit task16 acceptance closeout"
git add .serena/project.yml .gitignore
git commit -m "chore: sync serena config and ignore graphify output"
```

- [x] **Step 4: CLAUDE.md「現在地」を更新**

「ワークフロー」節の現在地の段落を次で置き換える:

```markdown
- **現在地**: フェーズ0・1・2とPhase 3-0は完了。Phase 3-1は再設計済み（spec: `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`、計画: `docs/superpowers/plans/2026-07-19-phase3-1-minimal-vertical-slice.md`）。進捗は計画のチェックボックスのみが正。旧Task 1-28計画とtask12/13スピンオフ文書群の未完了部分は失効（specの§9）。保護施設・基準該当B型は凍結スコープ外。
```

「仕様の所在」節の旧Phase 3-1計画への参照行を新spec/計画の2行に置き換える。

- [x] **Step 5: コミットとクリーン確認**

```bash
git add CLAUDE.md
git commit -m "docs: point CLAUDE.md to phase3-1 vertical slice plan"
git status --short
```

Expected: `git status --short` の出力なし

---

### Task 2: ADR 0027 — R6基本報酬・サービスコード・地域区分単価の実値抽出

**Files:**
- Create: `docs/decisions/0027-r6-basic-reward-service-code-region-price-values.md`
- Modify（必要時のみ）: `docs/open-questions.md`

**Interfaces:**
- Consumes: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json` の登録済み一次資料（documentId / url / sha256）
- Produces: Task 3のseed値とTask 6のgolden case期待値の**唯一の出典**。ADRに (a) B型基本報酬表（定員規模×平均工賃月額区分×人員配置、R6値）、(b) 対応サービスコード（R8-6月サービスコード表基準）、(c) 地域区分単価（就労継続支援B型のサービス種別単価、全地域区分）、(d) 手計算検証ケース2件以上（入力と期待値: 単位数・総費用額・給付額）

- [x] **Step 1: 一次資料の同一性検証**

`sources.json` から基本報酬・サービスコード表・地域区分単価に対応する `documentId` / `url` / `sha256` を特定し、資料を取得してハッシュ照合する:

```bash
curl -sL "<sources.jsonのurl>" -o /tmp/source-check.bin && shasum -a 256 /tmp/source-check.bin
```

Expected: `sources.json` の `sha256` と一致。**不一致の場合は停止**し、`docs/open-questions.md` に「一次資料のバイト変化（documentId、旧/新sha256）」を1行起票してユーザーに報告する。

- [x] **Step 2: ADR 0027を作成**

結論→背景→選択肢→決定→影響の既存ADR形式。決定セクションに次を**表で**記載する:

1. B型基本報酬（R6）: `paymentBand`（平均工賃月額区分）× `capacityKey`（定員規模）× `staffingKey`（人員配置 7.5:1 / 10:1 等）→ 単位数。各行に出典locator（資料名・ページ・表番号）
2. 各行に対応するサービスコード（6桁等の公式コード）とofficialLabel
3. 地域区分単価: 地域区分（1級地〜7級地・その他）→ 就労継続支援B型の1単位単価（円、小数）
4. 手計算検証ケース: 「定員20以下・平均工賃月額2万円以上2.5万円未満・7.5:1・22日利用・地域区分X」のような具体入力と、期待される 単位数/月・総費用額・給付額（9割）・利用者負担
5. **確定できなかった行の一覧**（あれば）→ 同じ内容を `docs/open-questions.md` に1行ずつ起票し、スコープ外を明記

- [x] **Step 3: コミット**

```bash
git add docs/decisions/0027-r6-basic-reward-service-code-region-price-values.md docs/open-questions.md
git commit -m "docs(adr): record r6 basic reward and region price values (task 2)"
```

---

### Task 3: seed実値投入（service-codes / basic-rewards / region-unit-prices）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`（新規）

**Interfaces:**
- Consumes: ADR 0027の確定値。`claim-master-file.schema.json` の `$defs`（unitRuleのkind判別子文字列は**必ずschemaの `$defs` を読んで確認**する — 本計画には転記しない）
- Produces: `JsonClaimMasterProvider.LoadEmbedded()` が返すbundleに `BasicRewards` / `ServiceCodes` / `RegionUnitPrices` の実値行。Task 4以降が消費

- [x] **Step 1: 失敗するテストを書く**

```csharp
// tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class ClaimMasterSeedPhase31Tests
{
    [Fact]
    public void LoadEmbedded_provides_r6_basic_reward_rows()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        var masters = provider.ResolveCalculationMasters(new ServiceMonth(2025, 4));
        // 件数はADR 0027の確定行数を転記する（例: 定員3区分×工賃区分×人員配置区分）
        masters.BasicRewards.Should().NotBeEmpty();
        masters.ServiceCodes.Should().NotBeEmpty();
        masters.RegionUnitPrices.Should().NotBeEmpty();
        masters.BasicRewards.Should().OnlyContain(row => row.BaseUnits > 0);
        masters.ServiceCodes.Should().OnlyContain(row =>
            row.ComponentRefs.Count > 0 || row.UnitRule is Tsumugi.Domain.Logic.Claim.Models.FixedCompositeUnitRule);
    }
}
```

注: `ResolveCalculationMasters` はTask 4で追加する。Task 3の時点では `JsonClaimMasterProvider.LoadEmbedded()` 自体の成功（validator通過）を検証する `[Fact] LoadEmbedded_succeeds_with_populated_seeds()` のみ先に書き、上のテストはTask 4で有効化してもよい。

- [x] **Step 2: テスト実行（失敗確認）**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter ClaimMasterSeedPhase31 -v minimal`
Expected: FAIL（seedが空のため件数0、または `ResolveCalculationMasters` 未定義のコンパイルエラー）

- [x] **Step 3: seed JSONへADR 0027の値を投入**

各entryの形（basic-rewardsの例。実値・locator・sha256はADR 0027から転記）:

```json
{
  "schemaVersion": "2",
  "masterKind": "basic-rewards",
  "entries": [
    {
      "key": "b-basic.r6.cap20.band-20000-25000.staff-7.5-1",
      "effectiveFrom": "2024-04",
      "effectiveTo": null,
      "sourceRefs": [
        {
          "documentId": "<ADR 0027のdocumentId>",
          "sha256": "<sources.jsonの64hex>",
          "locator": "<資料名 p.NN 表N 行名>",
          "evidenceRole": "authoritative",
          "supports": ["master-values", "effective-period"]
        }
      ],
      "values": {
        "paymentBand": "band-20000-25000",
        "staffingKey": "staff-7.5-1",
        "capacityKey": "cap-20-or-less",
        "serviceCode": "<ADR 0027のコード>",
        "baseUnits": 0
      }
    }
  ]
}
```

`service-codes.json` は `conditionDefinitions`（reward-system / payment-band / capacity / staffing の各条件）と、基本報酬行を指す `componentRefs`（`masterKind: "basic-rewards"`, `role: "base"`）+ `unitRule`（基本報酬をper-dayで通す形。**kind文字列はschemaの `$defs/serviceCodeUnitRule` を読んで正確に**）を持つ。`region-unit-prices.json` は `regionKey` / `serviceKind` / `unitPriceYen`（10進文字列）。

- [x] **Step 4: validator通過をテストで確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "ClaimMasterSeedPhase31|JsonClaimMasterProvider" -v minimal`
Expected: PASS（既存の `JsonClaimMasterProviderTests` の埋め込みカタログ検証も緑のまま）

- [x] **Step 5: コミット**

```bash
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/ tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "feat(phase3-1): seed r6 basic rewards, service codes, region prices (task 3)"
```

---

### Task 4: `IClaimMasterProvider.ResolveCalculationMasters` の追加

**Files:**
- Modify: `src/Tsumugi.Application/Abstractions/IClaimMasterProvider.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderCalculationMastersTests.cs`（新規）

**Interfaces:**
- Consumes: Task 3のseed実値。`ClaimCalculationMasterBundle`（Domain既存）
- Produces: `ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth)` — 指定月に有効な行だけへフィルタしたbundle。Task 6/7/9が消費

- [x] **Step 1: 失敗するテストを書く**

```csharp
// tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderCalculationMastersTests.cs
using FluentAssertions;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class JsonClaimMasterProviderCalculationMastersTests
{
    [Fact]
    public void ResolveCalculationMasters_filters_rows_by_effective_month()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        var masters = provider.ResolveCalculationMasters(new ServiceMonth(2025, 4));
        masters.BasicRewards.Should().OnlyContain(row =>
            row.EffectiveFrom <= new ServiceMonth(2025, 4)
            && (row.EffectiveTo == null || new ServiceMonth(2025, 4) <= row.EffectiveTo));
    }

    [Fact]
    public void ResolveCalculationMasters_throws_for_month_before_any_release()
    {
        var provider = JsonClaimMasterProvider.LoadEmbedded();
        FluentActions.Invoking(() => provider.ResolveCalculationMasters(new ServiceMonth(2000, 1)))
            .Should().Throw<Tsumugi.Application.Abstractions.ClaimMasterPolicyUnavailableException>();
    }
}
```

- [x] **Step 2: テスト実行（失敗確認）**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter JsonClaimMasterProviderCalculationMasters -v minimal`
Expected: FAIL（コンパイルエラー: `ResolveCalculationMasters` 未定義）

- [x] **Step 3: interfaceと実装を追加**

```csharp
// IClaimMasterProvider.cs へ追加
ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth);
```

```csharp
// JsonClaimMasterProvider.cs へ追加（ResolveVersionで版の存在を検証してからフィルタ）
public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth)
{
    _ = ResolveVersion(serviceMonth); // 版が無ければClaimMasterPolicyUnavailableException
    return new ClaimCalculationMasterBundle(
        FilterByMonth(_calculationMasters.BasicRewards, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo),
        FilterByMonth(_calculationMasters.UnitAdjustments, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo),
        FilterByMonth(_calculationMasters.RegionUnitPrices, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo),
        FilterByMonth(_calculationMasters.BurdenCaps, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo),
        FilterByMonth(_calculationMasters.TransitionRules, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo),
        FilterByMonth(_calculationMasters.ServiceCodes, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo),
        FilterByMonth(_calculationMasters.ConditionDefinitions, serviceMonth, r => r.EffectiveFrom, r => r.EffectiveTo));
}

private static IReadOnlyList<T> FilterByMonth<T>(
    IReadOnlyList<T> rows, ServiceMonth month,
    Func<T, ServiceMonth> from, Func<T, ServiceMonth?> to)
    => [.. rows.Where(r => from(r) <= month && (to(r) is not { } end || month <= end))];
```

- [x] **Step 4: テスト実行（成功確認）**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter JsonClaimMasterProviderCalculationMasters -v minimal`
Expected: PASS。続けて `dotnet build`（警告ゼロ）

- [x] **Step 5: コミット**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimMasterProvider.cs src/Tsumugi.Infrastructure/ClaimMasters/JsonClaimMasterProvider.cs tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderCalculationMastersTests.cs
git commit -m "feat(phase3-1): expose calculation master bundle by service month (task 4)"
```

---

### Task 5: ServiceCodeResolver（Domain純粋関数・基本報酬解決）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimBillingConditionContext.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs`（新規）

**Interfaces:**
- Consumes: `ClaimCalculationMasterBundle` / `ClaimConditionDefinition` / `ServiceCodeMasterRow` / `BasicRewardMasterRow`（Domain既存）
- Produces:
  - `ClaimBillingConditionContext(string RewardSystem, string PaymentBand, string CapacityKey, string StaffingKey, AverageWageBandOption AverageWageBandOption, R8ReformStatus R8ReformStatus)`
  - `ServiceCodeResolver.ResolveBasicReward(ClaimCalculationMasterBundle masters, ServiceMonth month, ClaimBillingConditionContext context)` → `ResolvedBasicReward(string ServiceCode, string OfficialLabel, int UnitsPerDay, BillingUnit BillingUnit)`
  - 失敗は `ServiceCodeResolutionException(ServiceCodeResolutionErrorCode)`、`ServiceCodeResolutionErrorCode { MasterUnavailable=1, AmbiguousMatch=2, ConditionUnresolved=3, ComponentMissing=4, UnsupportedUnitRule=5 }`

- [x] **Step 1: 失敗するテストを書く（合成マスタで網羅）**

```csharp
// tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs
using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class ServiceCodeResolverTests
{
    private static readonly ServiceMonth Month = new(2025, 4);

    // 合成マスタ組み立てヘルパ（テスト内privateメソッド）:
    //  - BasicRewardMasterRow("base-a", "band-a", "staff-a", "cap-a", "610000", 700, 2024-04, null, [srcRef])
    //  - ClaimConditionDefinition("cond-band-a", …, Kind=PaymentBand, Operator=Equals, TokenOperand("band-a"), [srcRef])
    //    同様に cond-cap-a(Capacity), cond-staff-a(Staffing), cond-system-b(RewardSystem)
    //  - ServiceCodeMasterRow("sc-a", "610000", "B型基本(合成)", "b-type",
    //      Selectors: [], ConditionSelectors: ["cond-system-b","cond-band-a","cond-cap-a","cond-staff-a"],
    //      UnitRule: BaseComponentPassThroughRule("base-a", "step-base", null, BillingUnit.PerDay),
    //      ComponentRefs: [ClaimComponentRef(BasicRewards, "base-a", Base)], …)

    [Fact]
    public void Resolves_the_single_matching_service_code_to_base_units()
    {
        var masters = SyntheticMasters();
        var context = new ClaimBillingConditionContext(
            "b-type", "band-a", "cap-a", "staff-a",
            new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 3), R8ReformStatus.NotApplicableBeforeR8);

        var resolved = ServiceCodeResolver.ResolveBasicReward(masters, Month, context);

        resolved.ServiceCode.Should().Be("610000");
        resolved.UnitsPerDay.Should().Be(700);
        resolved.BillingUnit.Should().Be(BillingUnit.PerDay);
    }

    [Fact]
    public void Throws_when_no_service_code_matches()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMasters(), Month, ContextWith(paymentBand: "band-unknown")))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.MasterUnavailable);

    [Fact]
    public void Throws_ambiguous_when_two_service_codes_match()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithDuplicateMatch(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.AmbiguousMatch);

    [Fact]
    public void Throws_condition_unresolved_for_frozen_condition_kinds()
        // FacilityClassification（保護施設系）等、本スライス対象外のkindを含む行はConditionUnresolved
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithFacilityClassificationCondition(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ConditionUnresolved);

    [Fact]
    public void Throws_component_missing_when_base_component_ref_is_broken()
        => FluentActions.Invoking(() => ServiceCodeResolver.ResolveBasicReward(
                SyntheticMastersWithBrokenComponentRef(), Month, DefaultContext()))
            .Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(ServiceCodeResolutionErrorCode.ComponentMissing);
}
```

- [x] **Step 2: テスト実行（失敗確認）**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter ServiceCodeResolver -v minimal`
Expected: FAIL（コンパイルエラー: 型未定義）

- [x] **Step 3: 実装**

```csharp
// src/Tsumugi.Domain/Logic/Claim/Models/ClaimBillingConditionContext.cs
namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>算定条件の入力。値はすべて呼び出し側で閉じる（I/O・時刻に依存しない）。</summary>
public sealed record ClaimBillingConditionContext(
    string RewardSystem,
    string PaymentBand,
    string CapacityKey,
    string StaffingKey,
    AverageWageBandOption AverageWageBandOption,
    R8ReformStatus R8ReformStatus);
```

```csharp
// src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

public enum ServiceCodeResolutionErrorCode
{
    MasterUnavailable = 1, AmbiguousMatch = 2, ConditionUnresolved = 3,
    ComponentMissing = 4, UnsupportedUnitRule = 5,
}

public sealed class ServiceCodeResolutionException(ServiceCodeResolutionErrorCode code)
    : Exception($"Service code resolution failed: {code}.")
{
    public ServiceCodeResolutionErrorCode Code { get; } = code;
}

public sealed record ResolvedBasicReward(
    string ServiceCode, string OfficialLabel, int UnitsPerDay, BillingUnit BillingUnit);

public static class ServiceCodeResolver
{
    public static ResolvedBasicReward ResolveBasicReward(
        ClaimCalculationMasterBundle masters, ServiceMonth month, ClaimBillingConditionContext context)
    {
        ArgumentNullException.ThrowIfNull(masters);
        ArgumentNullException.ThrowIfNull(context);

        var candidates = masters.ServiceCodes
            .Where(row => row.ComponentRefs.Any(c => c.MasterKind == ClaimComponentMasterKind.BasicRewards && c.Role == ClaimComponentRole.Base))
            .Where(row => MatchesAll(row, masters, context))
            .ToArray();

        if (candidates.Length == 0)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.MasterUnavailable);
        if (candidates.Length > 1)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.AmbiguousMatch);

        var row = candidates[0];
        if (row.UnitRule is not BaseComponentPassThroughRule passThrough)
            throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.UnsupportedUnitRule);

        var baseRow = masters.BasicRewards.SingleOrDefault(b => b.Key == passThrough.BaseComponentKey)
            ?? throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ComponentMissing);

        return new ResolvedBasicReward(row.ServiceCode, row.OfficialLabel, baseRow.BaseUnits, row.UnitRule.BillingUnit);
    }

    private static bool MatchesAll(
        ServiceCodeMasterRow row, ClaimCalculationMasterBundle masters, ClaimBillingConditionContext context)
        => row.ConditionSelectors.All(selector =>
        {
            var definition = masters.ConditionDefinitions.SingleOrDefault(d => d.Key == selector)
                ?? throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved);
            return Evaluate(definition, context);
        });

    private static bool Evaluate(ClaimConditionDefinition definition, ClaimBillingConditionContext context)
        => definition.Kind switch
        {
            ClaimConditionKind.RewardSystem => EvaluateToken(definition, context.RewardSystem),
            ClaimConditionKind.PaymentBand => EvaluateToken(definition, context.PaymentBand),
            ClaimConditionKind.Capacity => EvaluateToken(definition, context.CapacityKey),
            ClaimConditionKind.Staffing => EvaluateToken(definition, context.StaffingKey),
            ClaimConditionKind.AverageWageBand => EvaluateInteger(definition, context.AverageWageBandOption.OfficialOptionCode),
            ClaimConditionKind.R8ReformStatus => EvaluateToken(definition, TokenFor(context.R8ReformStatus)),
            // 凍結スコープ（保護施設・基準該当等）のkindはフェイルクローズ
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

    private static bool EvaluateToken(ClaimConditionDefinition definition, string value)
        => (definition.Operator, definition.Operand) switch
        {
            (ClaimConditionOperator.Equals, ClaimConditionTokenOperand token) => token.Value == value,
            (ClaimConditionOperator.In, ClaimConditionTokenSetOperand set) => set.Values.Contains(value),
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

    private static bool EvaluateInteger(ClaimConditionDefinition definition, int value)
        => (definition.Operator, definition.Operand) switch
        {
            (ClaimConditionOperator.Equals, ClaimConditionIntegerOperand i) => value == i.Value,
            (ClaimConditionOperator.LessThan, ClaimConditionIntegerOperand i) => value < i.Value,
            (ClaimConditionOperator.LessThanOrEqual, ClaimConditionIntegerOperand i) => value <= i.Value,
            (ClaimConditionOperator.GreaterThan, ClaimConditionIntegerOperand i) => value > i.Value,
            (ClaimConditionOperator.GreaterThanOrEqual, ClaimConditionIntegerOperand i) => value >= i.Value,
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };

    private static string TokenFor(R8ReformStatus status)
        => status switch
        {
            R8ReformStatus.NotApplicableBeforeR8 => "not-applicable-before-r8",
            R8ReformStatus.ReformTarget => "reform-target",
            R8ReformStatus.ReformExempt => "reform-exempt",
            R8ReformStatus.UnchangedBelow15000 => "unchanged-below-15000",
            _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
        };
}
```

注: `month` 引数はTask 4で有効月フィルタ済みのbundleを受ける前提の検証用に残す（行の有効期間再チェックが必要ならここで行う）。

- [x] **Step 4: テスト実行（成功確認）**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter ServiceCodeResolver -v minimal`
Expected: PASS

- [x] **Step 5: コミット**

```bash
git add src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs src/Tsumugi.Domain/Logic/Claim/Models/ClaimBillingConditionContext.cs tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs
git commit -m "feat(phase3-1): resolve basic reward service code from masters (task 5)"
```

---

### Task 6: ClaimCalculator最小（基本報酬×日数 → 地域単価 → 負担0）+ golden case

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimRoundingRules.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorTests.cs`（合成マスタ）
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`（ADR 0027検証ケース）

**Interfaces:**
- Consumes: `ServiceCodeResolver`（Task 5）、`ClaimCalculationMasterBundle`、ADR 0025（端数規則）、ADR 0027（golden case期待値）
- Produces:
  - `RecipientClaimSource(Guid RecipientId, int BilledDays, int BenefitRatePercent, int? CertificateMonthlyCapYen)`
  - `ClaimCalculationRequest(ServiceMonth Month, ClaimBillingConditionContext Conditions, string RegionKey, string ServiceKind, IReadOnlyList<RecipientClaimSource> Recipients)`
  - `ClaimCalculator.Calculate(ClaimCalculationMasterBundle masters, ClaimCalculationRequest request)` → `ClaimCalculationResult(IReadOnlyList<RecipientClaimResult> Details, int TotalUnits, int TotalCostYen, int TotalBenefitYen, int TotalBurdenYen)`、`RecipientClaimResult(Guid RecipientId, string ServiceCode, int BilledDays, int TotalUnits, int TotalCostYen, int BenefitYen, int BurdenYen)`
  - `ClaimCalculationException(ClaimCalculationErrorCode)`、`ClaimCalculationErrorCode { RegionUnitPriceUnavailable=1, InvalidInput=2, RoundingRuleUnavailable=3 }`
  - `ClaimRoundingRules.Apply(string roundingRuleId, decimal value)` → `int`（ADR 0025のID→規則。IDが未知なら `RoundingRuleUnavailable`）

- [x] **Step 1: ADR 0025を読み、端数規則IDと規則（単位数・金額それぞれ）を確認する**

Run: `cat docs/decisions/0025-claim-rounding-rules.md`
確認事項: 単位数の丸めID（例 `claim.rounding.units.half-up.v1`）と金額（円）の丸め規則。**ADR 0025に無い規則が必要になったら停止してopen-questionsへ**。

- [x] **Step 2: 失敗するテストを書く**

```csharp
// tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorTests.cs
// 合成マスタ（ServiceCodeResolverTestsと同じヘルパ形式: baseUnits=700, unitPriceYen=10.00m）で:
[Fact]
public void Calculates_basic_reward_only_recipient()
{
    var result = ClaimCalculator.Calculate(SyntheticMasters(), new ClaimCalculationRequest(
        new ServiceMonth(2025, 4), DefaultContext(), "region-a", "b-type",
        [new RecipientClaimSource(RecipientA, BilledDays: 20, BenefitRatePercent: 90, CertificateMonthlyCapYen: 0)]));

    var detail = result.Details.Should().ContainSingle().Subject;
    detail.TotalUnits.Should().Be(700 * 20);
    detail.TotalCostYen.Should().Be(/* 700*20*10.00 → ADR 0025の金額丸めを適用した値 */ 140000);
    detail.BurdenYen.Should().Be(0);          // cap=0（生活保護等）→ 負担0
    detail.BenefitYen.Should().Be(140000);    // 総費用 − 負担
    result.TotalUnits.Should().Be(detail.TotalUnits);
}

[Fact]
public void Caps_burden_at_certificate_monthly_cap()
{
    // cap=4600, 1割相当がcapを超える入力 → BurdenYen==4600, Benefit=Cost-4600
}

[Fact]
public void Throws_when_region_unit_price_is_missing()
{
    // RegionKey不一致 → ClaimCalculationErrorCode.RegionUnitPriceUnavailable
}

[Theory]
[InlineData(0)] [InlineData(-1)] [InlineData(32)]
public void Rejects_invalid_billed_days(int days)
{
    // ClaimCalculationErrorCode.InvalidInput
}
```

```csharp
// tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs
// ADR 0027 §手計算検証ケースの入力・期待値を転記し、実seedマスタで検証する。
// マスタはInfrastructureに依存できないため、ADR 0027の該当行だけをテスト内で
// BasicRewardMasterRow等として再掲する（値の出典コメントにADR 0027の行番号を書く）。
[Theory]
[MemberData(nameof(GoldenCases))] // TheoryData<入力, 期待単位数, 期待総費用, 期待給付, 期待負担>
public void Matches_adr_0027_worked_examples(...)
```

- [x] **Step 3: テスト実行（失敗確認）**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter ClaimCalculator -v minimal`
Expected: FAIL（型未定義）

- [x] **Step 4: 実装**

```csharp
// src/Tsumugi.Domain/Logic/Claim/ClaimRoundingRules.cs
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>ADR 0025の端数規則。IDで引く（アルゴリズムのみ。制度値は持たない）。</summary>
public static class ClaimRoundingRules
{
    public const string UnitsHalfUpV1 = "claim.rounding.units.half-up.v1";
    // 金額の規則IDはADR 0025の記載に合わせて追加する

    public static int Apply(string roundingRuleId, decimal value)
        => roundingRuleId switch
        {
            UnitsHalfUpV1 => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero),
            // ADR 0025の金額規則（例: 円未満切り捨てなら decimal.Floor）をここに追加
            _ => throw new ClaimCalculationException(ClaimCalculationErrorCode.RoundingRuleUnavailable),
        };
}
```

```csharp
// src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim;

public enum ClaimCalculationErrorCode { RegionUnitPriceUnavailable = 1, InvalidInput = 2, RoundingRuleUnavailable = 3 }

public sealed class ClaimCalculationException(ClaimCalculationErrorCode code)
    : Exception($"Claim calculation failed: {code}.")
{
    public ClaimCalculationErrorCode Code { get; } = code;
}

public sealed record RecipientClaimSource(
    Guid RecipientId, int BilledDays, int BenefitRatePercent, int? CertificateMonthlyCapYen);

public sealed record ClaimCalculationRequest(
    ServiceMonth Month, ClaimBillingConditionContext Conditions, string RegionKey, string ServiceKind,
    IReadOnlyList<RecipientClaimSource> Recipients);

public sealed record RecipientClaimResult(
    Guid RecipientId, string ServiceCode, int BilledDays, int TotalUnits,
    int TotalCostYen, int BenefitYen, int BurdenYen);

public sealed record ClaimCalculationResult(
    IReadOnlyList<RecipientClaimResult> Details,
    int TotalUnits, int TotalCostYen, int TotalBenefitYen, int TotalBurdenYen);

public static class ClaimCalculator
{
    public static ClaimCalculationResult Calculate(
        ClaimCalculationMasterBundle masters, ClaimCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(masters);
        ArgumentNullException.ThrowIfNull(request);

        var resolved = ServiceCodeResolver.ResolveBasicReward(masters, request.Month, request.Conditions);
        var unitPrice = masters.RegionUnitPrices.SingleOrDefault(
                p => p.RegionKey == request.RegionKey && p.ServiceKind == request.ServiceKind)
            ?? throw new ClaimCalculationException(ClaimCalculationErrorCode.RegionUnitPriceUnavailable);

        var details = request.Recipients.Select(source =>
        {
            // 月の日数上限は暦月で31。0日以下・32日以上は入力エラー（フェイルクローズ）
            if (source.BilledDays is <= 0 or > 31 || source.BenefitRatePercent is < 0 or > 100)
                throw new ClaimCalculationException(ClaimCalculationErrorCode.InvalidInput);

            var totalUnits = resolved.UnitsPerDay * source.BilledDays;
            // 金額丸めはADR 0025の規則IDを適用（Step 1で確認したIDに差し替える）
            var totalCostYen = ClaimRoundingRules.Apply(ClaimRoundingRules.UnitsHalfUpV1, totalUnits * unitPrice.UnitPriceYen);
            var statutoryBurden = totalCostYen - ClaimRoundingRules.Apply(
                ClaimRoundingRules.UnitsHalfUpV1, totalCostYen * source.BenefitRatePercent / 100m);
            var burdenYen = source.CertificateMonthlyCapYen is { } cap
                ? Math.Min(statutoryBurden, cap)
                : statutoryBurden;

            return new RecipientClaimResult(
                source.RecipientId, resolved.ServiceCode, source.BilledDays,
                totalUnits, totalCostYen, totalCostYen - burdenYen, burdenYen);
        }).ToArray();

        return new ClaimCalculationResult(
            details,
            details.Sum(d => d.TotalUnits), details.Sum(d => d.TotalCostYen),
            details.Sum(d => d.BenefitYen), details.Sum(d => d.BurdenYen));
    }
}
```

注: 丸めIDの適用箇所（単位数→金額の変換、給付額計算）はStep 1で確認したADR 0025の規則に**必ず**合わせる。上のコードの `UnitsHalfUpV1` 使用は仮置きではなくADR 0025確認後に正しいIDへ差し替えてからコミットする。

- [x] **Step 5: テスト実行（成功確認）**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter ClaimCalculator -v minimal`
Expected: PASS（golden case含む）

- [x] **Step 6: コミット**

```bash
git add src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs src/Tsumugi.Domain/Logic/Claim/ClaimRoundingRules.cs tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorTests.cs tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs
git commit -m "feat(phase3-1): calculate basic reward claim with golden cases (task 6)"
```

---

### Task 7: SnapshotReader（operation-local read transaction）

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimCalculationSnapshotReader.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ClaimCalculationSnapshotReader.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimCalculationSnapshotReaderTests.cs`（新規、`SqliteFixture` 使用）

**Interfaces:**
- Consumes: `TsumugiDbContext` の各DbSet、各 `*Policy.Effective`（Domain既存）、`ClaimFinalizationStore` の明示txパターン（既存コードを参照実装とする）
- Produces:
  - `IClaimCalculationSnapshotReader { Task<ClaimCalculationSnapshot> ReadAsync(Guid officeId, ServiceMonth serviceMonth, CancellationToken ct); }`
  - `ClaimCalculationSnapshot`（値のみのrecord）: `OfficeClaimProfile? Profile`、`IReadOnlyList<ClaimInput> EffectiveClaimInputs`、`IReadOnlyList<CertificateClaimEvidence> EffectiveCertificateEvidences`、`IReadOnlyList<AverageWageAnnualEvidence> EffectiveAverageWageEvidences`、`IReadOnlyDictionary<Guid, int> BilledDaysByRecipient`（recipientId→提供実績日数）
  - 全readを**1本のdeferred read transaction**で行い、終了時 `RollbackAsync`

- [x] **Step 1: Phase 1の `DailyRecord` エンティティと効力判定Policyを読む**

Run: `grep -n "record DailyRecord" -r src/Tsumugi.Domain/Entities/ && grep -rn "class DailyRecordPolicy" src/Tsumugi.Domain/Logic/`
確認事項: 出席・サービス提供実績を表すフィールド名、訂正履歴の効力判定メソッド名。**「提供実績日数」の判定基準（どの出席区分を数えるか）が既存仕様で一意でなければ停止してopen-questionsへ**（推測で数えない）。

- [x] **Step 2: 失敗するテストを書く**

```csharp
// tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimCalculationSnapshotReaderTests.cs
public sealed class ClaimCalculationSnapshotReaderTests : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Reads_effective_inputs_and_billed_days_in_one_snapshot()
    {
        // Arrange: fixtureのDBに Office/Recipient/Certificate と
        //   ClaimInput(New→Correct 2世代), OfficeClaimProfile, CertificateClaimEvidence,
        //   DailyRecord（対象月に提供実績n日 + 対象外月）を保存
        // Act: reader.ReadAsync(officeId, month, ct)
        // Assert:
        //   EffectiveClaimInputs は各RootIdの最新Revisionのみ
        //   BilledDaysByRecipient[recipient] == n（対象月のみ）
        //   Profile は EffectiveFrom<=月末 の最新効力版
    }

    [Fact]
    public async Task Returns_empty_snapshot_for_month_without_records() { /* 空コレクション・0件辞書 */ }
}
```

- [x] **Step 3: テスト実行（失敗確認）**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter ClaimCalculationSnapshotReader -v minimal`
Expected: FAIL（型未定義）

- [x] **Step 4: 実装**

```csharp
// src/Tsumugi.Application/Abstractions/IClaimCalculationSnapshotReader.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public sealed record ClaimCalculationSnapshot(
    OfficeClaimProfile? Profile,
    IReadOnlyList<ClaimInput> EffectiveClaimInputs,
    IReadOnlyList<CertificateClaimEvidence> EffectiveCertificateEvidences,
    IReadOnlyList<AverageWageAnnualEvidence> EffectiveAverageWageEvidences,
    IReadOnlyDictionary<Guid, int> BilledDaysByRecipient);

public interface IClaimCalculationSnapshotReader
{
    Task<ClaimCalculationSnapshot> ReadAsync(Guid officeId, ServiceMonth serviceMonth, CancellationToken ct);
}
```

```csharp
// src/Tsumugi.Infrastructure/Persistence/ClaimCalculationSnapshotReader.cs
// ClaimFinalizationStore と同じ明示txパターン:
//   await using var db = await contextFactory.CreateDbContextAsync(ct);
//   await db.Database.OpenConnectionAsync(ct);
//   var connection = (SqliteConnection)db.Database.GetDbConnection();
//   await using var transaction = connection.BeginTransaction(deferred: true); // 読み取り専用
//   await db.Database.UseTransactionAsync(transaction, ct);
//   try { …AsNoTrackingで各履歴をロード→ *Policy.ValidateHistory→Effective/headへ縮約→
//         DailyRecordから対象月の提供実績日数を集計 …}
//   finally { await transaction.RollbackAsync(ct); }
public sealed class ClaimCalculationSnapshotReader(
    IDbContextFactory<TsumugiDbContext> contextFactory,
    IOfficeClaimProfilePolicyProvider profilePolicyProvider) : IClaimCalculationSnapshotReader
{
    public async Task<ClaimCalculationSnapshot> ReadAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    { /* 上記コメントの構造。履歴縮約は ClaimInputPolicy.Effective /
         CertificateClaimEvidencePolicy.Effective / AverageWageAnnualEvidencePolicy.Effective /
         profilePolicyProvider.Resolve(version).Effective を使用（履歴不正はそのまま例外を伝播） */ }
}
```

DI登録（`src/Tsumugi.Infrastructure/DependencyInjection.cs` に追加）:

```csharp
services.AddSingleton<IClaimCalculationSnapshotReader, ClaimCalculationSnapshotReader>();
```

- [x] **Step 5: テスト実行（成功確認）→ コミット**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter ClaimCalculationSnapshotReader -v minimal`
Expected: PASS

```bash
git add src/Tsumugi.Application/Abstractions/IClaimCalculationSnapshotReader.cs src/Tsumugi.Infrastructure/Persistence/ClaimCalculationSnapshotReader.cs src/Tsumugi.Infrastructure/DependencyInjection.cs tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimCalculationSnapshotReaderTests.cs
git commit -m "feat(phase3-1): read claim calculation snapshot in one transaction (task 7)"
```

---

### Task 8: production snapshot codec v1（Unavailableスタブ置換）

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs`
- Create: `src/Tsumugi.Application/Claim/ProductionClaimSnapshotValidationCodecRegistry.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`（registry差し替え）
- Delete: `src/Tsumugi.Infrastructure/Persistence/UnavailableClaimSnapshotValidationCodecRegistry.cs`（テストが参照していれば置換）
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotValidationCodecV1Tests.cs`（新規）

**Interfaces:**
- Consumes: `IClaimSnapshotValidationCodec` / `IClaimSnapshotValidationCodecRegistry` / `ValidatedClaimSnapshotEnvelope.CreateValidated`（Application内部factory — このためcodecはApplicationに置く）
- Produces:
  - `ClaimSnapshotValidationCodecV1`: `SchemaVersion = "claim-snapshot-v1"`、`ValidationCodecId = "claim-snapshot-codec-v1"`、`CanWrite = true`。`ReadValidated(canonicalUtf8)`: UTF-8 JSONとしてparse可能・重複キーなし・sha256一致を検証して `CreateValidated`。`Validate(envelope)`: sha256再計算一致
  - `ProductionClaimSnapshotValidationCodecRegistry : IClaimSnapshotValidationCodecRegistry`（`HasWriteSupport => true`、v1のみ登録）
  - 書き込み用ヘルパ: `ClaimSnapshotValidationCodecV1.CreateEnvelope(ReadOnlySpan<byte> canonicalUtf8)` → `ValidatedClaimSnapshotEnvelope`（Task 9のClose UseCaseが使用）

- [x] **Step 1: 失敗するテストを書く**

```csharp
[Fact] public void ReadValidated_roundtrips_canonical_json() { /* CreateEnvelope→GetCanonicalUtf8Bytes→ReadValidated→Sha256一致 */ }
[Fact] public void ReadValidated_rejects_non_json_bytes() { /* ClaimFinalizationException(InvalidSnapshotEnvelope) */ }
[Fact] public void Registry_exposes_v1_with_write_support() { /* HasWriteSupport==true, Find("claim-snapshot-v1","claim-snapshot-codec-v1")!=null, 未知IDはnull */ }
```

- [x] **Step 2: 失敗確認 → 実装 → 成功確認**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter ClaimSnapshotValidationCodecV1 -v minimal`（FAIL→実装→PASS）

DI差し替え（`DependencyInjection.cs`）:

```csharp
services.AddSingleton<IClaimSnapshotValidationCodecRegistry, ProductionClaimSnapshotValidationCodecRegistry>();
```

`UnavailableClaimSnapshotValidationCodecRegistry` を参照する既存テスト（`ClaimFinalizationStoreTests` 等）はproduction registryへ移行し、「codec無しでは確定できない」検証は未知schemaVersionを使う形に書き換える。

- [x] **Step 3: 全体テストで回帰確認 → コミット**

Run: `dotnet test -v minimal`
Expected: 全緑

```bash
git add src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs src/Tsumugi.Application/Claim/ProductionClaimSnapshotValidationCodecRegistry.cs src/Tsumugi.Infrastructure/ tests/
git commit -m "feat(phase3-1): production snapshot validation codec v1 (task 8)"
```

---

### Task 9: Calculate / Close / Cancel / Query UseCase

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/CalculateClaimUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Claim/CancelClaimUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Claim/QueryClaimUseCase.cs`
- Create: `src/Tsumugi.Application/Dtos/ClaimPreparationDtos.cs`
- Test: `tests/Tsumugi.Application.Tests/UseCases/Claim/CalculateClaimUseCaseTests.cs` ほか各UseCase 1ファイル（リポジトリ/リーダはテスト用フェイクで注入）

**Interfaces:**
- Consumes: `IClaimCalculationSnapshotReader`（Task 7）、`IClaimMasterProvider.ResolveCalculationMasters`（Task 4）、`ClaimCalculator`（Task 6）、`ClaimPreparationReadiness`（既存）、`ClaimSnapshotValidationCodecV1.CreateEnvelope`（Task 8）、`IClaimFinalizationStore.CommitAsync` / `ClaimFinalizationDraft` / `ClaimBatchPolicy`（既存）
- Produces（`ClaimPreparationDtos.cs`）:
  - `ClaimPreviewDto(ServiceMonth ServiceMonth, string ClaimMasterVersion, string PreviewHash, IReadOnlyList<ClaimPreviewDetailDto> Details, int TotalUnits, int TotalCostYen, int TotalBenefitYen, int TotalBurdenYen, IReadOnlyList<ClaimPreparationIssue> Issues, bool IsReady)`
  - `ClaimPreviewDetailDto(Guid RecipientId, string ServiceCode, int BilledDays, int TotalUnits, int TotalCostYen, int BenefitYen, int BurdenYen)`
  - `CloseClaimRequest(Guid OfficeId, ServiceMonth ServiceMonth, string ExpectedPreviewHash)` / `CancelClaimRequest(Guid OfficeId, ServiceMonth ServiceMonth)` / `ClaimBatchRevisionDto(Guid BatchId, int Revision, bool IsReplay)`
  - `CalculateClaimUseCase.ExecuteAsync(CalculateClaimRequest, ct)` → `ClaimPreviewDto`（readinessが不成立なら `IsReady=false`+Issues、算定はスキップ）
  - `CloseClaimUseCase.ExecuteAsync(CloseClaimRequest, actor, ct)` → 再算定してPreviewHash照合（不一致は `ClaimFinalizationException(ClaimErrorCode.InvalidOperationPayload)`）→ 入力/算定snapshotをcanonical JSON化→envelope→`ClaimFinalizationDraft`（`RecordKind.New` または head存在時 `Correct`）→ `store.CommitAsync`
  - `CancelClaimUseCase.ExecuteAsync(CancelClaimRequest, actor, ct)` → head必須で `RecordKind.Cancel` のdraft→Commit
  - `QueryClaimUseCase.ExecuteAsync(QueryClaimRequest, ct)` → `IClaimBatchRepository.ListHistoryAggregatesAsync` をDTO化
- PreviewHash = 確定時と同じ `ClaimFinalizationOperationV1.Canonicalize(draft).Sha256` をプレビュー用draft（FinalizationOperationId空Guid固定・CreatedBy `"preview"` 固定）で計算した値。**プレビューと確定で同一入力→同一hashになることをテストで固定**

- [x] **Step 1: 失敗するテストを書く（フェイクで境界を固定）**

主要ケース: (a) readiness不成立→Issues返却・算定スキップ、(b) 成立→ClaimCalculator結果がDTO化、(c) 同一入力でPreviewHashが安定、(d) Close時hash不一致→例外、(e) Close成功→store.CommitAsyncへ渡るdraftのKind/Totals/Details件数、(f) Cancelはhead無しで `ClaimFinalizationException(InvalidHistory)`、(g) Queryが履歴を返す。

- [x] **Step 2: 失敗確認 → 実装 → 成功確認**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter "CalculateClaim|CloseClaim|CancelClaim|QueryClaim" -v minimal`（FAIL→実装→PASS）

実装の要点（UseCase規約に従いprimary-constructor DI・型付き例外）:

```csharp
public sealed class CalculateClaimUseCase(
    IClaimCalculationSnapshotReader snapshotReader,
    IClaimMasterProvider masterProvider,
    ClaimPreparationReadiness readiness)
{
    public async Task<ClaimPreviewDto> ExecuteAsync(CalculateClaimRequest request, CancellationToken ct)
    {
        var snapshot = await snapshotReader.ReadAsync(request.OfficeId, request.ServiceMonth, ct);
        var context = ClaimPreparationContextBuilder.Build(snapshot);   // snapshot→ClaimPreparationContext の純粋写像
        var readinessResult = readiness.Evaluate(context);
        if (!readinessResult.IsReady)
            return ClaimPreviewDto.NotReady(request.ServiceMonth, readinessResult.Issues);

        var release = masterProvider.ResolveVersion(request.ServiceMonth);
        var masters = masterProvider.ResolveCalculationMasters(request.ServiceMonth);
        var calcRequest = ClaimCalculationRequestBuilder.Build(snapshot, request.ServiceMonth); // profile→条件context、証→cap/給付率、日数辞書→Recipients
        var result = ClaimCalculator.Calculate(masters, calcRequest);
        return ClaimPreviewDto.From(request.ServiceMonth, release.Version.Value, PreviewHash(result, snapshot), result, readinessResult);
    }
}
```

`ClaimPreparationContextBuilder` / `ClaimCalculationRequestBuilder` は `src/Tsumugi.Application/Claim/` に置く純粋staticクラス（テストは合成snapshotで直接検証）。profile側の `PaymentBand`/`CapacityKey`/`StaffingKey` トークンは `OfficeClaimProfile` と `Office` 既存項目から写像し、写像できない場合は `ClaimPreparationIssue` として返す（推測しない）。

- [x] **Step 3: DI登録（`CompositionRoot.cs`）→ 全体テスト → コミット**

```csharp
services.AddScoped<ClaimPreparationReadiness>();
services.AddScoped<CalculateClaimUseCase>();
services.AddScoped<CloseClaimUseCase>();
services.AddScoped<CancelClaimUseCase>();
services.AddScoped<QueryClaimUseCase>();
```

Run: `dotnet test -v minimal` → 全緑

```bash
git add src/Tsumugi.Application/ src/Tsumugi.App/CompositionRoot.cs tests/Tsumugi.Application.Tests/
git commit -m "feat(phase3-1): calculate, close, cancel, query claim use cases (task 9)"
```

---

### Task 9b: OfficeClaimProfile請求トークン拡張と明示的evidence対応付け（Task 9実装中の発見への追補）

> Task 9で判明した3点を閉じる: (1) 定員・人員配置・地域区分の実データ保存先が存在せず本番プレビューが恒久NotReady、(2) snapshotのevidence⇄利用者対応が位置依存で生産者契約に順序不変条件が未記載、(3) 実績0日の契約中利用者が月全体のreadinessをブロックする過剰厳格性。

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs`（`int? CapacityHeadcount` / `string? StaffingKey` / `string? RegionKey` を追加）と `OfficeClaimProfilePolicy`（検証）
- Migration: `dotnet ef migrations add Phase31OfficeClaimBillingTokens`
- Modify: `SetOfficeClaimProfileUseCase` / request DTO / `ClaimInputViewModel` のプロファイル節（入力欄3つ。StaffingKey/RegionKey の選択肢はマスタbundle（staffing条件トークン・region-unit-prices行）から列挙し、ハードコードしない）
- Modify: `OfficeClaimBillingTokenProvider`（有効プロファイルから実値を返す本番配線。未入力はnullのまま=readiness issue）
- Modify: `ClaimCalculationSnapshot` — `EffectiveCertificateEvidences` リストを `IReadOnlyDictionary<Guid, CertificateClaimEvidence> EffectiveCertificateEvidenceByRecipient` に置換（位置依存を排除）。reader・builder・関連テスト追随
- Modify: readiness規則 — `billedDays == 0` かつ有効ClaimInputなしの利用者は請求明細を生成しないため、証・入力系の必須判定から除外（一覧には残す）。テストで固定
- Test: 各変更に対応する既存テストファイルへ追加

**Interfaces:**
- Consumes: Task 9の `IClaimBillingTokenProvider` / builders / readiness、Task 7のreader
- Produces: 本番構成で「プロファイル入力済みならプレビューがReadyになる」状態。Task 10のUIが前提とする

- [x] **Step 1: Domain拡張＋policy＋migrationをTDDで実装**
- [x] **Step 2: snapshotのevidence辞書化とbuilder追随（位置依存排除）**
- [x] **Step 3: readinessの0日非ブロック化**
- [x] **Step 4: UseCase/VM入力欄＋token provider本番配線**
- [x] **Step 5: 全体テスト・ci.sh緑 → コミット（論理単位で分割可）**

---

### Task 9c: readiness requirement catalog の全写像（本番Ready到達の最終ブロッカー）

> Task 9b修正ラウンドで確定した事実: 本番の `ClaimInputRequirementProvider.LoadEmbedded()` には、context builder が未写像の **14 target paths**（Certificate.* / ContractedProvider.* / DailyRecord.* / IntensiveSupportEpisode.StartDate）があり、本番構成のプレビューは恒久NotReady（`ClaimPreviewProductionWiringTests` にpinnedテストあり）。本タスクでこの14 pathsをsnapshot実データから写像し、本番でReadyに到達可能にする。

**Files:**
- Modify: `src/Tsumugi.Application/Abstractions/IClaimCalculationSnapshotReader.cs`（snapshotへ必要エンティティ値を追加: 有効Certificate、ContractedProvider、有効DailyRecord由来値、IntensiveSupportEpisode）
- Modify: `src/Tsumugi.Infrastructure/Persistence/ClaimCalculationSnapshotReader.cs`（同read tx内で取得）
- Modify: `src/Tsumugi.Application/Claim/ClaimPreparationContextBuilder.cs`（14 pathsを `ClaimPreparationValue` / row scope へ写像）
- Modify: `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs`（pinnedテストを「実requirement catalogでReady到達」テストへ書き換え）
- Test: builder/reader の各追加に対応する既存テストファイルへ追加

**Interfaces:**
- Consumes: requirement catalog の targetPath/条件定義（`ClaimInputRequirementProvider.Create` を読む）、Task 7/9b の snapshot契約
- Produces: 本番DI構成そのままで、全入力が揃えば `IsReady == true`。Task 10 のUIが前提とする

- [ ] **Step 1: 14 pathsの条件・row scope仕様を調査し、写像表（path→snapshot源）を報告に記録**
- [ ] **Step 2: reader拡張（同一tx内で不足エンティティ取得）をTDDで実装**
- [ ] **Step 3: builder写像をTDDで実装（未確定・入力不足はissueのまま=フェイルクローズ維持）**
- [ ] **Step 4: pinnedテストを本番Ready到達テストへ置換（negative: 1入力欠落→該当issue）**
- [ ] **Step 5: 全体テスト・ci.sh緑 → コミット**

---

### Task 10: ClaimPreparation画面（プレビュー→確定→取下げ）とUI配線

**Files:**
- Create: `src/Tsumugi.App/ViewModels/ClaimPreparationViewModel.cs`
- Create: `src/Tsumugi.App/Views/ClaimPreparationView.axaml` / `.axaml.cs`
- Modify: `src/Tsumugi.App/ViewModels/MainViewModel.cs`（短絡除去・VMプロパティ・コンテキスト適用）
- Modify: `src/Tsumugi.App/MainWindow.axaml`（TabItem追加）
- Modify: `src/Tsumugi.App/CompositionRoot.cs`（`services.AddTransient<ClaimPreparationViewModel>();`）
- Test: `tests/Tsumugi.App.Tests/ViewModels/ClaimPreparationViewModelTests.cs`（新規）

**Interfaces:**
- Consumes: `CalculateClaimUseCase` / `CloseClaimUseCase` / `CancelClaimUseCase` / `QueryClaimUseCase`（Task 9）、`ListOfficesUseCase`（既存）、`IAppNavigationService` / `NavigationRequest`（既存）
- Produces: `ClaimPreparationViewModel`（`ObservableProperty`: 選択Office・対象月・`ClaimPreviewDto? Preview`・Issues・履歴、`RelayCommand`: `PreviewAsync` / `CloseAsync` / `CancelAsync`）。確定は成功時に履歴を再読込。エラーは型付き例外をcatchしてユーザー向けメッセージへ（個人情報・フルパスを含めない）

- [ ] **Step 1: 失敗するViewModelテストを書く**

主要ケース: (a) PreviewAsyncがUseCaseを呼びPreview/Issuesを公開、(b) IsReady=falseでCloseコマンドが実行不可、(c) CloseAsync成功でQueryが再実行される、(d) 例外時にErrorMessageが設定されPreviewは保持。UseCaseはコンストラクタ注入のフェイク（既存 `ClaimInputViewModel` テストの流儀に合わせる）。

- [ ] **Step 2: 失敗確認 → 実装（VM/View/配線）→ 成功確認**

- `MainViewModel.DispatchAsync` の `AppSection.ClaimPreparation` 短絡（`NavigationTargetUnavailable` 返却）を削除し、他セクションと同じVM委譲へ
- `MainWindow.axaml` の請求入力タブの直後に追加:

```xml
<TabItem Header="請求確定(_H)">
    <views:ClaimPreparationView DataContext="{Binding ClaimPreparation}" />
</TabItem>
```

- Viewはダークテーマ既定・広い余白・キーボード完結（プレビュー=Enter、確定はボタンフォーカス）というハード制約5の既存スタイルに従う

Run: `dotnet test tests/Tsumugi.App.Tests --filter ClaimPreparationViewModel -v minimal` → PASS

- [ ] **Step 3: 手動貫通確認 → コミット**

Run: `dotnet run --project src/Tsumugi.App`
確認: 記録済みデータのある月でプレビュー→明細表示→確定→履歴に revision 1 が出る→取下げ→履歴に Cancel が出る。

```bash
git add src/Tsumugi.App/ tests/Tsumugi.App.Tests/
git commit -m "feat(phase3-1): claim preparation screen with preview and close (task 10)"
```

**⇒ ここまでで「基本報酬のみ」の垂直貫通が完成。以後は縦積み。**

---

### Task 11: ADR 0028 + 主要加算seed + Calculator拡張

**Files:**
- Create: `docs/decisions/0028-r6-major-addition-values.md`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json` / `service-codes.json`
- Modify: `src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs` / `ServiceCodeResolver.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorAdditionTests.cs`（新規）、golden case追加

**Interfaces:**
- Consumes: ADR 0027の様式・Task 3のseed形式・`UnitAdjustmentMasterRow` / `FixedUnitsAmount` / `UnitsPerCountAmount` / `PercentageOfTargetAmount`（Domain既存）
- Produces: `RecipientClaimSource` へ加算算定に必要な実績値を追加（例: `int TransportCount`（送迎回数）、`int AbsenceResponseCount`（欠席時対応回数）、`bool MealProvided…` 等 — **確定フィールドはADR 0028の加算リスト確定後に決める**が、値はすべてsnapshot経由の入力実績で、推測しない）。`ClaimCalculator` が加算明細行（serviceCodeごと）を `RecipientClaimResult` に追加

- [ ] **Step 1: ADR 0028を作成（Task 2 Step 1〜3と同じ手順）**

候補（specの§3.1-3）: 送迎加算、欠席時対応加算、食事提供体制加算（ADR 0020済）、目標工賃達成指導員配置加算、福祉専門職員配置等加算、初期加算、処遇改善加算系。**一次資料から単位数・算定条件を一意に確定できたものだけ**をADRの決定表に載せ、確定できないものは open-questions に起票してスコープ外を明記。処遇改善加算系（%ベース）は `PercentageOfTargetAmount` の適用順（`CalculationOrder`）と丸めをADRで確定できた場合のみ含める。

- [ ] **Step 2: 加算ごとにTDDで縦に足す**

加算1つずつ: 合成マスタテスト（Red）→ seed追記＋Calculator拡張（Green）→ golden case追加 → コミット（`feat(phase3-1): add <加算名> to claim calculation (task 11)`）。全加算の後に `dotnet test -v minimal` 全緑を確認。

---

### Task 12: 利用者負担の完成（burden-caps seed + 上限管理結果）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`（ADR 0022の区分・上限額）
- Modify: `src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorBurdenTests.cs`（新規）

**Interfaces:**
- Consumes: ADR 0022（`PaymentBurdenCategory` と月額上限）、`BurdenCapMasterRow`、`ClaimInput.UpperLimitManagementResult` / `UpperLimitManagedAmountYen`、`CertificateClaimEvidence.MonthlyCostCap`（既存）
- Produces: `RecipientClaimSource` に `string BurdenCategory` と `UpperLimitManagementResult? UpperLimitResult` / `int? UpperLimitManagedAmountYen` を追加。負担額 = min(法定負担, 区分上限capYen, 証記載上限, 上限管理結果額)。**不変条件テスト: 負担 ≦ 証記載上限 ≦ 法定上限**、Σ明細=合計

- [ ] **Step 1〜4: TDD（Red→Green→golden case→コミット）**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter ClaimCalculatorBurden -v minimal`

```bash
git commit -m "feat(phase3-1): apply burden caps and upper limit results (task 12)"
```

---

### Task 13: R8-6月切替・経過措置・平均工賃正式式・境界月テスト

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json` / `basic-rewards.json` / `service-codes.json`（R8行: `effectiveFrom: "2026-06"`、R6行に `effectiveTo: "2026-05"`）
- Create: `src/Tsumugi.Domain/Logic/Claim/AverageWageFormula.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/AverageWageFormulaTests.cs`、`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs`（新規）

**Interfaces:**
- Consumes: ADR 0023（平均工賃正式式・R8区分見直し・経過措置）、`AverageWageAnnualEvidence`、`OfficeClaimProfileTransitionRuleMasterRow`（既存）
- Produces:
  - `AverageWageFormula.Calculate(int annualWagePaidYen, int annualExtendedUsers, int annualOpeningDays)` → 平均工賃月額（`年間工賃支払総額 ÷ (年間延べ利用者数 ÷ 年間開所日数) ÷ 12`、丸めはADR 0023の規定。0除算・負値は `ClaimCalculationException(InvalidInput)`）
  - 境界月テスト: 同一入力で 2026-05 はR6行・2026-06 はR8行のservice code/単位数が解決されること（`ResolveCalculationMasters` 経由）
  - 経過措置: `OfficeClaimProfile` の届出情報から `R8ReformStatus` / band optionを検証する既存 `OfficeClaimProfilePolicy` 経路をsnapshot→条件contextの写像に接続。**判定に必要な入力が不足なら算定不能（推測しない）**

- [ ] **Step 1〜5: TDD（式→境界月→経過措置の順に Red→Green→コミット）**

Run: `dotnet test --filter "AverageWageFormula|ClaimMasterR8Boundary" -v minimal`

```bash
git commit -m "feat(phase3-1): r8 boundary switch and official average wage formula (task 13)"
```

---

### Task 14: 受け入れ証跡とクローズ

**Files:**
- Modify: `docs/phase3-0-acceptance.md` もしくは新規 `docs/phase3-1-acceptance.md`（受け入れ証跡）
- Modify: `CLAUDE.md`（現在地更新）

**Interfaces:**
- Consumes: 全タスクの成果
- Produces: Phase 3-1（本スライス）の受け入れ判定。Phase 3-2（帳票）へ進む前提

- [ ] **Step 1: 品質ゲート一括実行**

Run: `./build/ci.sh`
Expected: `==> CI OK`（format / build 警告ゼロ / 全テスト / Domain≧95% / Application≧70%）

- [ ] **Step 2: 受け入れ証跡を記録**

`docs/phase3-1-acceptance.md` に、specの§8成功基準5項目それぞれについて 判定（✔/✘）・証跡（テスト名 or 実行コマンドと結果）を表で記録。`Logic.Claim` の分岐カバレッジ実測値も記載（100%未達なら不足箇所と理由）。

- [ ] **Step 3: CLAUDE.md現在地を「Phase 3-1垂直スライス受け入れ済み、次はPhase 3-2帳票」へ更新しコミット**

```bash
git add docs/ CLAUDE.md
git commit -m "docs(phase3-1): record vertical slice acceptance evidence (task 14)"
```

---

## 自己レビュー記録（計画作成時）

- spec §3.1（基本報酬全区分）→ Task 2/3、（R6+R8境界）→ Task 13、（主要加算）→ Task 11、（地域単価・負担）→ Task 3/12、（確定・再確定・取下げ）→ Task 9、（平均工賃正式式）→ Task 13
- spec §3.2（凍結）→ Task 5のConditionUnresolvedフェイルクローズ + Global Constraints
- spec §4（ガバナンス）→ Task 1 + Global Constraints、§5（パイプライン）→ Task 4〜10、§6（エラー処理）→ 各タスクの型付き例外、§7（テスト）→ 各タスクTDD + Task 14
- 既知の限界: seed JSONのunitRule kind判別子とADR実値は計画に転記できない（一次資料ゲートのため）。該当タスクに「schemaを読む」「ADRから転記」の明示ステップを置いた
