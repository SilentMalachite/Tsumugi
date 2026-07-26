# Phase 3-5 実装計画 — 指定障害者支援施設区分の構造化入力と請求への結線

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 指定障害者支援施設か否かを `OfficeClaimProfile` の構造化入力として持たせ、入力 UI を用意し、処遇改善加算の施設 variant 4行を投入して、施設事業所が正しい加算行で請求できるようにする。

**Architecture:** 既存の構造化入力（`CapacityHeadcount` / `StaffingKey` / `RegionKey`）とまったく同じ経路に1本足す。新しい仕組みは作らない。`ClaimConditionKind.FacilityClassification` と schema の `facility-classification` は既に定義済みで、欠けているのは resolver の評価ケース1つ・enum・列1本・条件2件・行4組・ComboBox 1つだけである。値は ADR 0045 が抽出済みで、新たな一次資料の抽出は不要。

**Tech Stack:** .NET 10 / C# 14、xUnit ＋ FluentAssertions、EF Core 10（migration 1本）、Avalonia 11（ComboBox 1つ）。

**設計 spec（正本）:** `docs/superpowers/specs/2026-07-26-phase3-5-facility-classification-design.md`

---

## Global Constraints

- **制度実値（率・コード）を `src/` の C# へ直書きしない。** すべて seed JSON ＋ `sourceRefs` 経由。**テストの期待値は可**（既存 golden case と同じ扱い）。`ClaimSpecificationBoundaryTests` と `ExternalSpecificationLiteralGuard` が Roslyn token 単位で検査する。
- **一次資料は必ず `shasum -a 256` で `sources.json` の登録値と照合してから使う。** 不一致なら使わず停止する。
- **抽出は2形式または2方式の独立実施＋一致確認。** 本計画で新規抽出が必要なのはサービスコードの実在確認だけ（率は ADR 0045 が確定済み）。
- **`locator` は位置指定だけを書く。** 主張・考察は書かない。**物理頁と印字頁を区別**し、両方書く（印字頁が無い文書は「印字頁番号なし」と明記）。
- **確定できない行は seed しない。** 部分完了を許容し、投入しなかったものは `docs/open-questions.md` へ起票する。
- **R6 の行・条件定義を書き換えない。**
- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。**`Tsumugi.Domain.Tests` は Infrastructure を参照できない。**
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。`dotnet build` は警告ゼロ。
- テストは xUnit ＋ FluentAssertions。コメントとアサーションの `because` は日本語（既存コードの慣習）。
- 1コミット=1論理変更。コミットメッセージにフェーズ番号を記す。
- **Co-Authored-By 等の attribution 行を入れない**（このプロジェクトでは無効化されている）。
- `docs/spec-data/phase3/claim-master-source-row-manifest.json` は触らない。

### spec からの意図的な逸脱（1件・計画作成時に確定）

**spec §3.7 の「readiness で非ブロッキング警告を出す」は実装しない。**

理由: `ClaimCalculationRequestBuilder` は `issues.Count > 0` で `Request` を `null` にするため、そこへ issue を足すと必ずブロッキングになる。非ブロッキングの経路は `UpcomingSpecificationIssues`（ADR 0041、将来の施行分専用）しか存在せず、施設区分の告知に流用するのは意味論が合わない。汎用の警告チャネルを新設するには `ClaimPreparationIssue` に severity を足す必要があり、全消費者に波及する。

**代わりに `ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved` を新設**し、失敗を汎用の `ConditionUnresolved` と区別できるようにする（Task 1）。これにより「施設条件を持つ行を評価しようとしたときだけ止まる」という spec §3.2 の狙いは変わらず、失敗の原因が判別可能になる。`ClaimPreviewPipeline` は解決失敗を catch せず伝播させる既存挙動をそのまま使う（他のすべての解決失敗と同じ扱い）。

---

## File Structure

| ファイル | 責務 | タスク |
| --- | --- | --- |
| `src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs` | `FacilityClassification` enum | 1 |
| `src/Tsumugi.Domain/Logic/Claim/Models/ClaimBillingConditionContext.cs` | context の施設区分フィールド | 1 |
| `src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs` | エラーコード追加・評価ケース追加 | 1 |
| `docs/decisions/0047-r8-designated-support-facility-variants.md` | 施設 variant の決定表（値の唯一の出典） | 2 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json` | 条件2件・施設4行・通常4行への条件付与 | 2 |
| `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json` | 施設 variant の率4行 | 2 |
| `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs` | 施設区分プロパティ | 3 |
| `src/Tsumugi.Infrastructure/Migrations/*_Phase35OfficeFacilityClassification.cs` | 列追加 | 3 |
| `src/Tsumugi.Infrastructure/Persistence/Configurations/OfficeClaimProfileConfiguration.cs` | Cancel チェック制約 | 3 |
| `src/Tsumugi.Application/Abstractions/IClaimBillingTokenProvider.cs` | `ClaimBillingConditionTokens` の施設区分 | 4 |
| `src/Tsumugi.Infrastructure/ClaimMasters/OfficeClaimBillingTokenProvider.cs` | enum → トークン写像 | 4 |
| `src/Tsumugi.Application/Claim/ClaimCalculationRequestBuilder.cs` | context への受け渡し | 4 |
| `src/Tsumugi.Application/UseCases/Claim/SetClaimEvidenceUseCases.cs` | 保存経路 | 4 |
| `src/Tsumugi.Application/Dtos/ClaimInputDtos.cs` / `ClaimInputQueryDtos.cs` | DTO | 4 |
| `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs` / `Views/ClaimInputView.axaml` | 入力 UI | 5 |
| `docs/phase3-5-acceptance.md` ほか | 受け入れ証跡・文書同期 | 6 |

---

## 既存 API リファレンス（実装者向け・これ以外を発明しない）

```csharp
// Tsumugi.Domain.Logic.Claim.ServiceCodeResolver
public enum ServiceCodeResolutionErrorCode
{ MasterUnavailable = 1, AmbiguousMatch = 2, ConditionUnresolved = 3,
  ComponentMissing = 4, UnsupportedUnitRule = 5, ComponentMismatch = 6 }

private static bool Evaluate(ClaimConditionDefinition definition, ClaimBillingConditionContext context)
    => definition.Kind switch { /* 7ケース。既定は ConditionUnresolved で throw */ };

private static bool EvaluateToken(ClaimConditionDefinition definition, string value)
    => (definition.Operator, definition.Operand) switch
    {
        (ClaimConditionOperator.Equals, ClaimConditionTokenOperand token) => token.Value == value,
        (ClaimConditionOperator.In, ClaimConditionTokenSetOperand set) => set.Values.Contains(value),
        _ => throw new ServiceCodeResolutionException(ServiceCodeResolutionErrorCode.ConditionUnresolved),
    };

// Tsumugi.Domain.Logic.Claim.Models
public sealed record ClaimBillingConditionContext(
    string RewardSystem, string PaymentBand, int CapacityHeadcount, string StaffingKey,
    AverageWageBandOption AverageWageBandOption, R8ReformStatus R8ReformStatus,
    IReadOnlyCollection<string>? OfficeCapabilityKeys = null);

// Tsumugi.Application.Abstractions
public sealed record ClaimBillingConditionTokens(
    string? RewardSystem, string? RegionKey, string? RegionUnitPriceServiceKind,
    int? CapacityHeadcount, string? StaffingKey, bool RegionKeyConflict = false,
    IReadOnlyDictionary<string, ClaimCountMetric>? CountSelectorBindings = null,
    IReadOnlyDictionary<PaymentBurdenCategory, string>? BurdenCategoryTokens = null);

// Tsumugi.Infrastructure.ClaimMasters
JsonClaimMasterProvider.LoadEmbedded().ResolveCalculationMasters(ServiceMonth month)
ServiceCodeResolver.ResolveAdditions(masters, month, context)   // 加算行の解決
```

**体制届 option と区分の対応（seed から機械確認済み・2026-07-26）。option 番号は区分の並び順と一致しないので順序から推測しないこと。**

| 区分 | option | 通常コード | 通常の率 | 施設の率 | 施設コード | xlsx 行 |
| --- | ---: | --- | ---: | ---: | --- | ---: |
| (Ⅰ)イ | **2** | 465120 | 0.105 | **0.116** | **465138** | 2262 |
| (Ⅰ)ロ | **7** | 465174 | 0.109 | **0.120** | **465176** | 2264 |
| (Ⅱ)イ | **3** | 465121 | 0.103 | — | — | — |
| (Ⅱ)ロ | **8** | 465175 | 0.107 | — | — | — |
| (Ⅲ) | **4** | 465122 | 0.088 | **0.098** | **465140** | 2270 |
| (Ⅳ) | **5** | 465123 | 0.074 | **0.081** | **465141** | 2272 |

施設 variant を持つのは **option 2・7・4・5**、持たないのは **3・8**。

---

## コマンド

```bash
dotnet build                                # 警告ゼロが前提
dotnet test                                 # 全緑が前提
./build/ci.sh                               # 品質ゲート一括（コミット前に必ず緑）
dotnet format --verify-no-changes

dotnet test --filter "FullyQualifiedName~ServiceCodeResolverTests"

# migration（startup は合成ルートの App）
dotnet ef migrations add Phase35OfficeFacilityClassification \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

---

## Task 1: Domain の受け皿（enum・context・resolver）

**Files:**
- Modify: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs`
- Modify: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimBillingConditionContext.cs`
- Modify: `src/Tsumugi.Domain/Logic/Claim/ServiceCodeResolver.cs`
- Test: `tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs`

**Interfaces:**
- Consumes: なし（最初のタスク）
- Produces: `FacilityClassification` enum（`Unknown = 0` / `General = 1` / `DesignatedSupportFacility = 2`）、`ClaimBillingConditionContext.FacilityClassification`（`string?`、**最後の省略可能パラメータ**）、`ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved = 7`。Task 2 のテストと Task 4 の結線が使う。

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.Domain.Tests/Logic/Claim/ServiceCodeResolverTests.cs` に追加する。既存のテストが使っているマスタ構築ヘルパの名前はファイルを読んで合わせること。

```csharp
    /// <summary>
    /// ADR 0047: 施設区分条件は、context が施設区分を持たない（null）とき判定不能として
    /// フェイルクローズする。汎用の ConditionUnresolved ではなく専用コードで返し、
    /// 「施設区分が未入力である」ことを呼び出し側が判別できるようにする。
    /// </summary>
    [Fact]
    public void Facility_classification_condition_fails_closed_when_the_context_has_no_value()
    {
        var definition = new ClaimConditionDefinition(
            "facility-designated-support",
            ClaimConditionKind.FacilityClassification,
            ClaimConditionOperator.Equals,
            new ClaimConditionTokenOperand("designated-support-facility"));

        var context = ContextWithoutFacilityClassification();

        var action = () => ServiceCodeResolver.EvaluateForTest(definition, context);

        action.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(
                ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved,
                "施設区分未入力は汎用の判定不能と区別する（ADR 0047）");
    }

    /// <summary>ADR 0047: 施設区分が入っていれば通常のtoken比較として評価する。</summary>
    [Theory]
    [InlineData("designated-support-facility", "designated-support-facility", true)]
    [InlineData("general", "designated-support-facility", false)]
    public void Facility_classification_condition_compares_the_token(
        string contextValue, string conditionValue, bool expected)
    {
        var definition = new ClaimConditionDefinition(
            "facility-condition",
            ClaimConditionKind.FacilityClassification,
            ClaimConditionOperator.Equals,
            new ClaimConditionTokenOperand(conditionValue));

        var context = ContextWithFacilityClassification(contextValue);

        ServiceCodeResolver.EvaluateForTest(definition, context).Should().Be(expected);
    }
```

> **`EvaluateForTest` について**: `Evaluate` は `private static` である。テストから直接叩けるようにするため、`ServiceCodeResolver` に `internal static bool EvaluateForTest(ClaimConditionDefinition, ClaimBillingConditionContext) => Evaluate(definition, context);` を足し、`Tsumugi.Domain` の csproj に `InternalsVisibleTo` が既にあるかを確認する。**無い場合は追加せず**、代わりに公開 API（`ResolveAdditions`）経由で1行だけのマスタを組んで検証する形にテストを書き換えること（`internal` の露出を増やさない）。どちらを採ったかを報告に書く。
>
> `ContextWithoutFacilityClassification()` / `ContextWithFacilityClassification(string)` は同ファイル内に追加するヘルパで、既存テストが context を組んでいる方法に合わせる（`RewardSystem` 等の必須引数は既存テストの値を流用してよい）。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ServiceCodeResolverTests.Facility_classification"
```

期待: **コンパイルエラー**（`FacilityClassificationUnresolved` と context の新フィールドが未定義）。これが最初の RED である。

- [ ] **Step 3: enum を追加する**

`src/Tsumugi.Domain/Logic/Claim/Models/ClaimInputModels.cs` の `R8ReformStatus` の近くに追加する。

```csharp
/// <summary>
/// 施設区分（ADR 0021・ADR 0047）。処遇改善加算の一部区分は指定障害者支援施設で率が
/// 別立てになるため、体制届の `designated-management` から推測せず構造化入力で受ける。
/// </summary>
public enum FacilityClassification
{
    Unknown = 0,

    /// <summary>指定障害者支援施設以外の就労継続支援B型事業所。</summary>
    General = 1,

    /// <summary>指定障害者支援施設において行う場合。</summary>
    DesignatedSupportFacility = 2,
}
```

- [ ] **Step 4: エラーコードと context フィールドを追加する**

`ServiceCodeResolver.cs` の `ServiceCodeResolutionErrorCode` へ追加する。

```csharp
    /// <summary>
    /// 施設区分条件を評価しようとしたが、contextが施設区分を持たない（未入力）。
    /// 汎用の<see cref="ConditionUnresolved"/>と区別し、呼び出し側が
    /// 「施設区分を入力すれば解消する」と判別できるようにする（ADR 0047）。
    /// </summary>
    FacilityClassificationUnresolved = 7,
```

`ClaimBillingConditionContext.cs` に **最後の省略可能パラメータとして**追加する。既存の全呼び出し箇所を壊さないため、必ず末尾に置くこと。

```csharp
/// <param name="FacilityClassification">
/// 施設区分トークン（ADR 0047）。<c>null</c>は「施設区分が未入力（判定不能）」を表し、
/// 施設区分条件つき行の解決はフェイルクローズする。施設区分条件を持たない行（例: 処遇改善
/// (Ⅱ)イ・(Ⅱ)ロ。公式に施設別立てが存在しない）は<c>null</c>のままでも解決できる。
/// </param>
public sealed record ClaimBillingConditionContext(
    string RewardSystem,
    string PaymentBand,
    int CapacityHeadcount,
    string StaffingKey,
    AverageWageBandOption AverageWageBandOption,
    R8ReformStatus R8ReformStatus,
    IReadOnlyCollection<string>? OfficeCapabilityKeys = null,
    string? FacilityClassification = null);
```

- [ ] **Step 5: resolver の評価ケースを追加する**

`Evaluate` の switch へ1ケース追加する（`OfficeCapability` の次、既定の直前）。

```csharp
            ClaimConditionKind.FacilityClassification =>
                EvaluateFacilityClassification(definition, context),
```

評価関数を `EvaluateCapability` の隣に追加する。

```csharp
    /// <summary>
    /// 施設区分条件: contextの施設区分トークンとの一致。未入力（null）の場合は判定不能として
    /// 専用コードでフェイルクローズする（推測して通常事業所として扱わない。ADR 0047）。
    /// </summary>
    private static bool EvaluateFacilityClassification(
        ClaimConditionDefinition definition, ClaimBillingConditionContext context)
    {
        if (context.FacilityClassification is not { } value)
        {
            throw new ServiceCodeResolutionException(
                ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved);
        }

        return EvaluateToken(definition, value);
    }
```

- [ ] **Step 6: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ServiceCodeResolverTests"
dotnet test
```

期待: 全緑。**既存テストは1件も落ちないはず**である（context の新パラメータは省略可能で既定 `null`、施設区分条件を持つ seed 行はまだ存在しない）。落ちたテストがあれば、その事実とテスト名を報告すること。

- [ ] **Step 7: 歯の確認**

`EvaluateFacilityClassification` の `throw` を `return false;` に一時的に書き換え、Step 1 の fail-close テストが RED になることを確認する。確認後に戻す。実出力を報告に貼ること。

- [ ] **Step 8: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Domain/ tests/Tsumugi.Domain.Tests/
git commit -m "feat(phase3-5): 施設区分条件をDomainで評価できるようにする"
```

---

## Task 2: ADR 0047 と seed（条件2件・施設4行・通常4行への条件付与）

**Files:**
- Create: `docs/decisions/0047-r8-designated-support-facility-variants.md`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimAdditionSeedScopeTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs`
- Modify: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs`

**Interfaces:**
- Consumes: Task 1 の `ClaimBillingConditionContext.FacilityClassification` と `FacilityClassificationUnresolved`
- Produces: 施設／非施設の条件トークン2件（キー名は ADR 0047 で決定）、施設 variant の `additions` 4行と `service-codes` 4行。Task 4 の結線がこのトークン名を使う。

> **1コミットで行う。** Phase 3-4 で判明したとおり、`ClaimMasterFileValidator.ValidateConditions` は未参照の `conditionDefinition` があると例外を投げる（dead-code ガード）。条件だけを先に入れる中間状態は作れない。

- [ ] **Step 1: 曖昧性検査テストを書く（失敗させる）**

`tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterR8BoundaryTests.cs` に追加する。

```csharp
    private static ClaimBillingConditionContext FacilityContext(
        int officialOptionCode, string? facilityClassification) => new(
        RewardSystem: "employment-continuation-support-b",
        PaymentBand: "",
        CapacityHeadcount: 15,
        StaffingKey: "staff-6-1",
        AverageWageBandOption: Numeric(3),
        R8ReformStatus: R8ReformStatus.ReformExempt,
        OfficeCapabilityKeys: new HashSet<string>(StringComparer.Ordinal)
        {
            $"mhlw.b46.capability.treatment-improvement.{officialOptionCode}",
        },
        FacilityClassification: facilityClassification);

    /// <summary>
    /// ADR 0047: 施設variantを持つ4区分（体制届option 2・7・4・5）は、施設区分ごとに
    /// ちょうど1行へ解決する。通常行と施設行の両方が一致する（AmbiguousMatch）状態は
    /// 条件の付け方を誤った証拠であり、ここで検出する。
    /// </summary>
    [Theory]
    [InlineData(2, "465120", "465138")]
    [InlineData(7, "465174", "465176")]
    [InlineData(4, "465122", "465140")]
    [InlineData(5, "465123", "465141")]
    public void Facility_variants_resolve_to_exactly_one_row_per_classification(
        int officialOptionCode, string generalCode, string facilityCode)
    {
        var june = Provider.ResolveCalculationMasters(June2026);

        var general = ServiceCodeResolver.ResolveAdditions(
            june, June2026, FacilityContext(officialOptionCode, "general"));
        var facility = ServiceCodeResolver.ResolveAdditions(
            june, June2026, FacilityContext(officialOptionCode, "designated-support-facility"));

        general.Should().ContainSingle(
            $"非施設 × option {officialOptionCode} は通常行だけに一致する")
            .Which.ServiceCode.Should().Be(generalCode);
        facility.Should().ContainSingle(
            $"施設 × option {officialOptionCode} は施設行だけに一致する")
            .Which.ServiceCode.Should().Be(facilityCode);
    }

    /// <summary>
    /// ADR 0047: (Ⅱ)イ・(Ⅱ)ロ（option 3・8）は公式に施設別立てが存在しないため
    /// 施設区分条件を付けない。施設事業所でも通常行へ解決しなければならない
    /// （条件を付けると施設事業所が算定できなくなる＝無音の未算定）。
    /// </summary>
    [Theory]
    [InlineData(3, "465121")]
    [InlineData(8, "465175")]
    public void Tiers_without_a_facility_variant_resolve_for_both_classifications(
        int officialOptionCode, string expectedCode)
    {
        var june = Provider.ResolveCalculationMasters(June2026);

        foreach (var classification in new[] { "general", "designated-support-facility" })
        {
            var rows = ServiceCodeResolver.ResolveAdditions(
                june, June2026, FacilityContext(officialOptionCode, classification));

            rows.Should().ContainSingle(
                $"施設区分 {classification} でも option {officialOptionCode} は通常行へ解決する")
                .Which.ServiceCode.Should().Be(expectedCode);
        }
    }

    /// <summary>
    /// ADR 0047: 施設区分が未入力のまま施設variantを持つ区分を算定しようとすると、
    /// 専用コードでフェイルクローズする（推測して通常行を選ばない）。
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(4)]
    [InlineData(5)]
    public void Facility_variant_tiers_fail_closed_without_a_facility_classification(
        int officialOptionCode)
    {
        var june = Provider.ResolveCalculationMasters(June2026);

        var action = () => ServiceCodeResolver.ResolveAdditions(
            june, June2026, FacilityContext(officialOptionCode, null));

        action.Should().Throw<ServiceCodeResolutionException>()
            .Which.Code.Should().Be(
                ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved);
    }
```

> **`ResolveAdditions` の戻り値の要素型**を実装前にコードで確認し、`.ServiceCode` の呼び出しが正しいことを確かめること。異なる場合はプロパティ名を実物に合わせる。
> **トークン文字列 `"general"` / `"designated-support-facility"`** は ADR 0047 で決める命名の仮値である。Step 3 で命名を確定したら、テストと seed の両方を確定した値へ揃えること。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMasterR8BoundaryTests.Facility"
dotnet test --filter "FullyQualifiedName~ClaimMasterR8BoundaryTests.Tiers_without"
```

期待: **FAIL**。施設行がまだ無いため `Facility_variants_resolve_to_exactly_one_row_per_classification` の施設側が0件で落ちる。`Tiers_without_a_facility_variant_resolve_for_both_classifications` は**この時点では PASS する**（(Ⅱ) には元から条件が無い）ので、Step 5 の後にも PASS のままであることを確認するのが本来の役割である。

- [ ] **Step 3: サービスコードを2形式独立で照合し、ADR 0047 を書く**

```bash
# SHA-256 照合（不一致なら停止）
shasum -a 256 <r8-service-codes-2.xlsx>   # 307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049
shasum -a 256 <r8-service-codes-2.pdf>    # 0ff507138037c0b58bdada54d35677c27cf54e708635706d41004638626a7445

# xlsx 側: 行 2262 / 2264 / 2270 / 2272
python3 - <<'PY'
import openpyxl
wb = openpyxl.load_workbook("<r8-service-codes-2.xlsx>", data_only=True)
ws = wb["18就労継続支援(B・基本)"]
for r in (2261, 2262, 2263, 2264, 2269, 2270, 2271, 2272):
    print(r, [c.value for c in ws[r]][:6], "|", [c.value for c in ws[r]][21:22])
PY

# PDF 側
pdftotext -layout <r8-service-codes-2.pdf> - | grep -n "指定障害者支援施設" | head -20
```

両形式で **465138 / 465176 / 465140 / 465141** と「指定障害者支援施設において行った場合」の対応が一致することを確認する。**一致しない行は seed しない。**

`docs/decisions/0047-r8-designated-support-facility-variants.md` を作成する。構成は `結論 → 背景 → 選択肢 → 決定 → 影響`、**初手から確定**として書く。ADR 0045 の様式に倣うこと。必須の記載事項:

- 一次資料の同一性検証表（documentId・SHA-256 先頭12桁・照合結果・用途）
- 2形式独立照合の結果
- **決定表**: 区分・体制届 option・通常コード・通常の率・施設コード・施設の率・xlsx 行。率は **ADR 0045 の抽出結果からの転記**であることを明示し、転記元の節を名指しする
- (Ⅱ)イ・(Ⅱ)ロ に施設別立てが無いことと、その根拠（ADR 0045 の率表と、行2266・2268 がコードを持たないプレースホルダであることの確認）
- **施設／非施設トークンの命名規約**（本 ADR が `facility-classification` kind の最初の利用者である）
- **通常行にも非施設条件を付ける理由**: 施設 variant は通常行と**同じ体制届 option を共有する**ため、片側だけに条件を付けると施設事業所で2行一致＝`AmbiguousMatch` になる
- **(Ⅱ) を対象外にする理由**: 施設別立てが無いため、条件を付けると施設事業所が算定できなくなる（無音の未算定）
- **既存行の `conditionSelectors` を変更する判断**と、確定済み請求が snapshot から読むため遡及しないという根拠（ADR 0026・0029・0032・0034）
- 選択肢: 「施設行にだけ条件を付ける」を**多重一致になるため不採用**、「resolver に優先順位を導入する」を**十分にテストされた解決器の意味論を変えるため不採用**として記録する
- 未入力時の挙動（`FacilityClassificationUnresolved` でフェイルクローズ）
- ADR 0045「確定できなかった区分」表の施設 variant 行を本 ADR が引き取ったこと

- [ ] **Step 4: 条件トークン2件を追加する**

`service-codes.json` の `conditionDefinitions` へ追加する。R6 の `capability-*` と同形。

```json
{
  "key": "<ADR 0047で決めた非施設のキー>",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "kind": "facility-classification",
  "operator": "equals",
  "value": "<ADR 0047で決めた非施設のトークン>",
  "sourceRefs": [
    {
      "documentId": "r8-service-codes-2-xlsx",
      "sha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
      "locator": "workbook-order=38;row=2261（追加条件欄が空＝指定障害者支援施設以外）",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    },
    {
      "documentId": "r8-fee-notice",
      "sha256": "f4b7a05e33b5dd91bf745ef078fd5563930ea3a2a7b84463e7bd092c36ff036c",
      "locator": "物理57頁（印字頁番号なし。第2条表・左欄改正後 第14の17 通常事業所の率）",
      "evidenceRole": "cross-check",
      "supports": ["conditions"]
    }
  ]
}
```

施設側は `locator` を `workbook-order=38;row=2262（追加条件欄「指定障害者支援施設において行った場合」）` とし、`r8-fee-notice` の cross-check は施設別立て率の位置を指す。

- [ ] **Step 5: 施設 variant 8行（additions 4 ＋ service-codes 4）を追加し、通常4行へ条件を付ける**

`additions.json` へ4行。既存の `addition.treatment-improvement.r8.i-i` と同形で、`percentage` だけが施設の率になる。

```json
{
  "key": "<ADR 0047で決めた施設variantのキー>",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [ /* r8-fee-notice(authoritative, 率) / r8-service-codes-2-xlsx(cross-check, row=2262) /
                     r8-service-codes-2-pdf(cross-check) / r8-calculation-note(authoritative, 丸め) */ ],
  "values": {
    "amount": {
      "kind": "percentage-of-target",
      "percentage": "0.116",
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

> **`calculationOrder` は 7・8・9・10 を使う（確定）。** `ClaimMasterFileValidator.ValidateCalculationOrder`（`:2893-2921`）は、**同一 `targetSelector`・同一期間で有効な割合加算行の `calculationOrder` が「1から連続かつ一意」であること**を要求する（`actual.SequenceEqual(Enumerable.Range(1, actual.Length))`、違反時は `must be unique and contiguous from one`）。処遇改善10行はすべて `target.b46.items-1-to-16-4.v1` を共有し 2026-06 で同時に有効なので、集合は 1〜10 でなければならない。既存6行が 1〜6 を使っているため、施設 variant は **7〜10** になる。
>
> 施設行と通常行は施設区分条件により排他なので、`calculationOrder` の値そのものは算定結果に影響しない。これは validator の帳簿上の一意性・連続性の要求である。

`service-codes.json` へ4行。既存の `b-addition.r8-06.treatment-improvement.i-i` と同形で、`serviceCode` を施設コードに、`conditionSelectors` に**施設条件を追加**する。

```json
  "conditionSelectors": [
    "reward-system-employment-continuation-support-b",
    "capability-treatment-improvement-r8-i-i",
    "<ADR 0047で決めた施設のキー>"
  ],
```

**あわせて既存の通常4行（`i-i` / `i-ro` / `iii` / `iv`）の `conditionSelectors` へ非施設条件を追加する。**

```json
  "conditionSelectors": [
    "reward-system-employment-continuation-support-b",
    "capability-treatment-improvement-r8-i-i",
    "<ADR 0047で決めた非施設のキー>"
  ],
```

**`ii-i`（465121）と `ii-ro`（465175）は変更しない。** これが本タスクで最も間違えやすい点である。

- [ ] **Step 6: コード集合テストを更新する**

`ClaimAdditionSeedScopeTests` の `R8TreatmentImprovementCodes` を6件から10件へ拡張し、`R8_treatment_improvement_rows_apply_only_from_2026_06` の集合一致（上限固定）が10件で成立するようにする。**`Should().Equal` を `Contain` へ緩めないこと。**

- [ ] **Step 7: golden case を追加する**

`tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs` に、**施設 × (Ⅰ)イ × 2026-06** の worked example を追加する。既存の `Matches_adr_0045_worked_example_reform_exempt_office_in_june_2026` を雛形にし、率を施設の値へ、context に施設区分を渡す形にする。

**ADR 0047 に worked example 節を先に書き、その算出過程をテストへ写すこと**（逆順にしない）。算出過程は1行ずつ書く。

```
基本 <単位数>×<日数>=<小計>単位 ＋ 処遇改善(施設) <小計>×<率>=<加算単位（四捨五入）> ＝ <合計>単位
総費用額 <合計単位>×<単価>円=<円（小数）>→<円（切捨て）>円
1割相当額 <総費用額>×10/100=<円（小数）>→<円（切捨て）>円
給付費 <総費用額>−<1割相当額>=<円>円
```

- [ ] **Step 8: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimMaster"
dotnet test --filter "FullyQualifiedName~ClaimCalculatorGoldenCaseTests"
dotnet test
```

期待: 全緑。特に `Tiers_without_a_facility_variant_resolve_for_both_classifications` が **Step 5 の後も PASS** であることを確認する（(Ⅱ) に誤って条件を付けていないことの検証）。

- [ ] **Step 9: 歯の確認（3種・必須）**

1. 施設 variant の `percentage` を1桁変える → golden case が RED
2. 通常行（例: `i-i`）から**非施設条件を消す** → `Facility_variants_resolve_to_exactly_one_row_per_classification` の施設側が **2行一致**で RED
3. `ii-i`（465121）に**誤って施設条件を付ける** → `Tiers_without_a_facility_variant_resolve_for_both_classifications` が RED

**3つとも実出力を報告に貼ること。** 確認後は必ず元へ戻し、`git diff` が意図した差分だけであることを確かめる。

- [ ] **Step 10: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Infrastructure/ClaimMasters/Seed/ \
        tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ \
        tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorGoldenCaseTests.cs \
        docs/decisions/0047-r8-designated-support-facility-variants.md
git commit -m "feat(phase3-5): 指定障害者支援施設variantの処遇改善4区分を出典付きで投入する"
```

---

## Task 3: 永続化（`OfficeClaimProfile` の施設区分列）

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/OfficeClaimProfileConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/*_Phase35OfficeFacilityClassification.cs`（`dotnet ef` が生成）
- Create: `tests/Tsumugi.Infrastructure.Tests/Phase35OfficeFacilityClassificationMigrationTests.cs`

**Interfaces:**
- Consumes: Task 1 の `FacilityClassification` enum
- Produces: `OfficeClaimProfile.FacilityClassification`（`FacilityClassification?`）。Task 4 の token provider が読む。

- [ ] **Step 1: ラウンドトリップテストを書く（失敗させる）**

`tests/Tsumugi.Infrastructure.Tests/` の既存 migration テスト（`Phase33GroupBExplicitAdditionInputsMigrationTests.cs`）を雛形にして新規作成する。**そのファイルを読んで、DbContext の組み方・ヘルパ・命名をそのまま真似ること。**

検証すること: (a) 新列に値を入れて保存し読み戻すと一致する、(b) `null` のまま保存できる、(c) **Cancel レコードでは `null` でなければならない**（`CK_OfficeClaimProfiles_CancelPayload` に違反すると `DbUpdateException`）。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~Phase35OfficeFacilityClassification"
```

期待: **コンパイルエラー**（プロパティ未定義）。

- [ ] **Step 3: エンティティにプロパティを追加する**

`src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs` の `RegionKey` の近く（構造化入力群）へ追加する。既存プロパティの XML doc の書き方に合わせること。

```csharp
    /// <summary>
    /// 施設区分（ADR 0021・ADR 0047）。処遇改善加算の一部区分は指定障害者支援施設で率が
    /// 別立てになる。体制届の `designated-management` から推測せず構造化入力で受ける。
    /// <c>null</c>／<see cref="FacilityClassification.Unknown"/>は未入力で、施設variantを持つ
    /// 区分の算定はフェイルクローズする。
    /// </summary>
    public FacilityClassification? FacilityClassification { get; init; }
```

- [ ] **Step 4: Configuration の Cancel チェック制約へ新列を追加する**

`OfficeClaimProfileConfiguration.cs` の `CK_OfficeClaimProfiles_CancelPayload` の SQL 文字列へ `AND "FacilityClassification" IS NULL` を追加する。**既存の列挙順に合わせて末尾へ追記**すること。

- [ ] **Step 5: migration を生成する**

```bash
dotnet ef migrations add Phase35OfficeFacilityClassification \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

生成された migration を**必ず目視で確認**する。列追加とチェック制約の再作成だけであること、既存データを壊す操作（列削除・型変更）が入っていないことを確かめる。

- [ ] **Step 6: テストを実行して緑を確認する**

```bash
dotnet test --filter "FullyQualifiedName~Phase35OfficeFacilityClassification"
dotnet test
```

期待: 全緑。既存の migration テストと `AppendOnlyGuard` 系が落ちていないことを確認する。

- [ ] **Step 7: 歯の確認**

Cancel レコードに施設区分を入れて保存を試み、`DbUpdateException` になることを Step 1 の (c) が捕まえることを確認する（既に (c) がそれを検証しているなら、一時的に Configuration から新列を外して (c) が RED になることを確認する）。実出力を報告に貼る。

- [ ] **Step 8: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Domain/Entities/OfficeClaimProfile.cs \
        src/Tsumugi.Infrastructure/Persistence/Configurations/ \
        src/Tsumugi.Infrastructure/Migrations/ \
        tests/Tsumugi.Infrastructure.Tests/
git commit -m "feat(phase3-5): OfficeClaimProfileへ施設区分の構造化入力を追加する"
```

---

## Task 4: 結線（token provider・request builder・保存ユースケース）

**Files:**
- Modify: `src/Tsumugi.Application/Abstractions/IClaimBillingTokenProvider.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/OfficeClaimBillingTokenProvider.cs`
- Modify: `src/Tsumugi.Application/Claim/ClaimCalculationRequestBuilder.cs`
- Modify: `src/Tsumugi.Application/UseCases/Claim/SetClaimEvidenceUseCases.cs`
- Modify: `src/Tsumugi.Application/Dtos/ClaimInputDtos.cs` / `ClaimInputQueryDtos.cs`
- Modify: `src/Tsumugi.Application/UseCases/Claim/QueryClaimInputWorkspaceUseCase.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/OfficeClaimBillingTokenProviderTests.cs`、`tests/Tsumugi.Application.Tests/`（既存の request builder テスト）

**Interfaces:**
- Consumes: Task 1 の context フィールド、Task 2 のトークン文字列、Task 3 の profile プロパティ
- Produces: `ClaimBillingConditionTokens.FacilityClassification`（`string?`、**最後の省略可能パラメータ**）。Task 5 の UI が DTO 経由で読み書きする。

- [ ] **Step 1: token provider のテストを書く（失敗させる）**

`OfficeClaimBillingTokenProviderTests.cs` に追加する。**enum → トークンの写像が ADR 0047 の命名と一致すること**を検証する。

```csharp
    [Theory]
    [InlineData(FacilityClassification.General, "<ADR 0047の非施設トークン>")]
    [InlineData(FacilityClassification.DesignatedSupportFacility, "<ADR 0047の施設トークン>")]
    public void Resolve_maps_the_facility_classification_to_its_token(
        FacilityClassification classification, string expected)
    {
        var tokens = Provider.Resolve(Office(), ProfileWith(classification), June2026);

        tokens.FacilityClassification.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(FacilityClassification.Unknown)]
    public void Resolve_returns_no_token_when_the_facility_classification_is_unset(
        FacilityClassification? classification)
    {
        var tokens = Provider.Resolve(Office(), ProfileWith(classification), June2026);

        tokens.FacilityClassification.Should().BeNull(
            "未入力は推測せずnullのまま運び、施設条件つき行の解決でフェイルクローズさせる");
    }
```

> `Provider` / `Office()` / `ProfileWith(...)` は同ファイルの既存ヘルパに合わせること。無ければ既存テストの組み方を真似て追加する。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~OfficeClaimBillingTokenProviderTests"
```

期待: **コンパイルエラー**（`ClaimBillingConditionTokens.FacilityClassification` 未定義）。

- [ ] **Step 3: `ClaimBillingConditionTokens` へフィールドを追加する**

`IClaimBillingTokenProvider.cs` の record へ**末尾の省略可能パラメータとして**追加する。既存呼び出しを壊さないため必ず末尾に置く。XML doc に「`null` は未入力。施設条件つき行の解決でフェイルクローズする（推測しない）」旨を書く。

```csharp
    IReadOnlyDictionary<PaymentBurdenCategory, string>? BurdenCategoryTokens = null,
    string? FacilityClassification = null);
```

- [ ] **Step 4: token provider に写像を実装する**

`OfficeClaimBillingTokenProvider.Resolve` の `new ClaimBillingConditionTokens(...)` へ追加する。

```csharp
            FacilityClassification: TokenFor(profile?.FacilityClassification),
```

写像関数を同クラスへ追加する。**トークン文字列は ADR 0047 の決定に従う。**

```csharp
    /// <summary>
    /// 施設区分enum→seedのfacility-classificationトークン（ADR 0047）。未入力（null／Unknown）は
    /// nullを返し、推測しない。
    /// </summary>
    private static string? TokenFor(FacilityClassification? classification)
        => classification switch
        {
            Domain.Logic.Claim.Models.FacilityClassification.General => "<ADR 0047の非施設トークン>",
            Domain.Logic.Claim.Models.FacilityClassification.DesignatedSupportFacility =>
                "<ADR 0047の施設トークン>",
            _ => null,
        };
```

- [ ] **Step 5: request builder で context へ渡す**

`ClaimCalculationRequestBuilder` の `new ClaimBillingConditionContext(...)` へ**名前付き引数で**追加する（位置引数が増えて読みにくくなるのを避ける）。

```csharp
                capabilityKeys,
                FacilityClassification: tokens.FacilityClassification),
```

**readiness issue は追加しない。**（Global Constraints の「spec からの意図的な逸脱」参照。未入力の検出は resolver の `FacilityClassificationUnresolved` が担う。）

- [ ] **Step 6: 保存・照会経路を配線する**

`SetClaimEvidenceUseCases.cs` の profile 保存部（既存の `CapacityHeadcount = request.CapacityHeadcount,` の並び）、`ClaimInputDtos.cs` / `ClaimInputQueryDtos.cs` の DTO、`QueryClaimInputWorkspaceUseCase.cs` の写像に、それぞれ既存の `CapacityHeadcount` と同じ位置・同じ作法で追加する。**DTO の record パラメータは末尾へ追加**して既存呼び出しを壊さないこと。

- [ ] **Step 7: テストを実行して緑を確認する**

```bash
dotnet test
```

期待: 全緑。**既存の request builder テストが落ちないこと**を特に確認する（施設区分は省略可能で既定 `null`）。

- [ ] **Step 8: 歯の確認**

`TokenFor` の `General` の戻り値を施設トークンへ入れ替え、Step 1 のテストが RED になることを確認する。あわせて Task 2 の `Facility_variants_resolve_to_exactly_one_row_per_classification` が RED になるかも確認する（結線が実際に効いていることの証明）。実出力を報告に貼る。

- [ ] **Step 9: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.Application/ src/Tsumugi.Infrastructure/ClaimMasters/ tests/
git commit -m "feat(phase3-5): 施設区分をprofileから算定条件へ結線する"
```

---

## Task 5: 入力 UI

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs`
- Modify: `src/Tsumugi.App/Views/ClaimInputView.axaml`
- Test: `tests/Tsumugi.App.Tests/`（既存の `ClaimInputViewModel` テストと `ViewInputWiringTests`）

**Interfaces:**
- Consumes: Task 3 の profile プロパティ、Task 4 の DTO
- Produces: なし（UI が終端）

- [ ] **Step 1: ViewModel のテストを書く（失敗させる）**

既存の `ClaimInputViewModel` テストに、施設区分の保存・読込・クリアの往復を検証するケースを追加する。既存の `ReformStatus` を扱うテストがあればそれを雛形にすること。

- [ ] **Step 2: テストを実行して失敗を確認する**

```bash
dotnet test --filter "FullyQualifiedName~ClaimInputViewModel"
```

期待: **コンパイルエラー**（`FacilityClassification` プロパティ未定義）。

- [ ] **Step 3: ViewModel にプロパティと選択肢を追加する**

`ClaimInputViewModel.cs` に、既存の `ReformStatus`（`:124`）と `ReformStatusOptions`（`:232`）とまったく同じ作法で追加する。

```csharp
    [ObservableProperty] private FacilityClassification? _facilityClassification;
```

```csharp
    public IReadOnlyList<FacilityClassification> FacilityClassificationOptions { get; } =
        Enum.GetValues<FacilityClassification>();
```

**3か所の配線を忘れないこと**（既存の `ReformStatus` を grep して同じ行に足す）。

- 保存（`:437` 付近 `ReformStatus = ReformStatus,` の並び）
- 読込（`:802` 付近 `ReformStatus = value?.ReformStatus;` の並び）
- クリア（`:1021` 付近 `ReformStatus = null;` の並び）

- [ ] **Step 4: View に ComboBox を追加する**

`ClaimInputView.axaml` の構造化入力群（`:153-159` の「利用定員（実頭数）」「人員配置区分」「地域区分」の並び）へ、同じ形で追加する。

```xml
              <TextBlock Text="施設区分" />
              <ComboBox ItemsSource="{Binding FacilityClassificationOptions}" SelectedItem="{Binding FacilityClassification}" />
```

**新しい View は作らない。** 施設区分は既存の請求プロファイル入力の1項目である。

- [ ] **Step 5: テストを実行して緑を確認する**

```bash
dotnet test
```

期待: 全緑。`ViewInputWiringTests` が View と ViewModel の対応を検査している場合、施設区分がその検査に含まれるかを確認し、含まれるべきなら期待値を更新する（**主張を弱めないこと**）。

- [ ] **Step 6: 歯の確認**

View から ComboBox を一時的に削除し、`ViewInputWiringTests`（または Step 1 のテスト）が RED になることを確認する。RED にならない場合は**配線が検査されていない**ということなので、その事実を報告し、検査するテストを追加する。実出力を報告に貼る。

- [ ] **Step 7: コミット**

```bash
./build/ci.sh
git add src/Tsumugi.App/ tests/Tsumugi.App.Tests/
git commit -m "feat(phase3-5): 請求プロファイル入力へ施設区分のComboBoxを追加する"
```

---

## Task 6: 文書同期と受け入れ証跡

**Files:**
- Create: `docs/phase3-5-acceptance.md`
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Modify: `CLAUDE.md`（「現在地」・「仕様の所在」）
- Modify: `docs/decisions/0045-r8-treatment-improvement-addition-values.md`（引き取り先の追記）
- Modify: `docs/phase3-4-acceptance.md`（残課題の消し込み）

**Interfaces:**
- Consumes: Task 1〜5 の全成果
- Produces: なし（最終タスク）

- [ ] **Step 1: `docs/phase3-5-acceptance.md` を作成する**

`docs/phase3-4-acceptance.md` の構成を参考にする。必須の記載事項:

- 達成状況と証拠（**テスト名を名指しで**）
- 投入した行数の実績（`conditionDefinitions` +2、`additions` +4、`service-codes` +4、既存4行の `conditionSelectors` 変更）
- **spec からの逸脱**: 「readiness の非ブロッキング警告を実装せず、専用エラーコード `FacilityClassificationUnresolved` で代替した」理由（`ClaimCalculationRequestBuilder` は issue があると必ずブロックし、非ブロッキング経路は `UpcomingSpecificationIssues` しか無く意味論が合わない）
- **既存4行の `conditionSelectors` を変更したこと**と、確定済み請求が snapshot から読むため遡及しないという根拠
- **2026-06 以降を既に確定済みの環境があれば、その月のプレビューが変わりうる**という注意
- 歯の確認の一覧（各タスクで実施したもの）
- 残課題: **処遇改善(Ⅴ) 14区分**、**施設での体制届 option 集合の絞り込み**（ADR 0021: R8-06 は `{1,2,4,5,7}`）、**GUI 手動貫通確認が Phase 1 から未実施**

- [ ] **Step 2: `docs/open-questions.md` を更新する**

**書く前に必ず現状を読むこと。** Phase 3-4 で複数の項目が新規起票・更新されている。

- 施設 variant 未投入の項目を**クローズ**（`[x]` ＋ クローズ日 ＋ ADR 0047、何をどう確定したか）
- **処遇改善(Ⅴ) と体制届 option 絞り込みは未解決のまま残す。** 施設 variant だけをクローズし、残りを誤って消さないこと
- **「現在の挙動」を書くときは実データ・実コードと照合すること。** このプロジェクトでは「現在の挙動」を事実と逆に書く欠陥が2回発生し、2回とも差し戻された

- [ ] **Step 3: `CHANGELOG.md` に節を追加する**

`## [Unreleased]` の下へ、Phase 3-4 完了節と同じ様式で追加する。

- [ ] **Step 4: `CLAUDE.md` を更新する**

- 「現在地」: Phase 3-5 の完了を追記し、**Phase 3-4 の残課題として書かれている「施設variant」の記述を消し込む**（処遇改善(Ⅴ)・option 8 等は残す）
- 「仕様の所在」: Phase 3-5 の spec・plan・受け入れ証跡を追加

**§ハード制約3 は変更不要**（制度実値の版管理の記述は Phase 3-4 で追記済み）。

- [ ] **Step 5: ADR 0045 へ引き取り先を追記する**

`docs/decisions/0045-...md` の「確定できなかった区分」表の**施設 variant 行**に、ADR 0047 で確定した旨と日付を追記する。**既存の記述を書き換えず追記**すること（ADR は決定時点の記録）。処遇改善(Ⅴ) の行は未確定のまま残す。

- [ ] **Step 6: `docs/phase3-4-acceptance.md` の残課題を消し込む**

施設 variant に関する残課題の記述に、Phase 3-5 で解消した旨と日付を追記する。**書き換えず追記**する。

- [ ] **Step 7: 最終確認**

```bash
./build/ci.sh
dotnet format --verify-no-changes
git status --short
```

- [ ] **Step 8: コミット**

```bash
git add docs/ CHANGELOG.md CLAUDE.md
git commit -m "docs(phase3-5): 受け入れ証跡とADR 0047の同期、open-questionsのクローズ"
```

---

## Self-Review（計画作成時に実施済み）

**1. Spec coverage**

| spec | タスク |
| --- | --- |
| §3.1 データモデル（enum・profile・migration・Cancel制約） | Task 1（enum）・Task 3（profile・migration・制約） |
| §3.2 トークンと context（nullable の理由） | Task 1（context）・Task 4（tokens・provider） |
| §3.3 resolver | Task 1 Step 5 |
| §3.4 seed（条件2・施設4・通常4への付与・(Ⅱ)は対象外） | Task 2 Step 4・5 |
| §3.5 既存行変更の正当化 | Task 2 Step 3（ADR）・Task 6 Step 1（受け入れ証跡） |
| §3.6 入力UI | Task 5 |
| §3.7 readiness 警告 | **意図的に不実装**。Global Constraints で理由を明示し、`FacilityClassificationUnresolved` で代替 |
| §4 非スコープ | 各タスクで対象を限定。Task 6 Step 2 で残課題を維持 |
| §5 エラー処理 | Task 1（fail-close）・Task 2 Step 9（多重一致の検出） |
| §6 テスト戦略 | 各タスクの Step 1（Red）と「歯の確認」Step |
| §7 一次資料 | Task 2 Step 3 |
| §8 ADR 0047 | Task 2 Step 3 |
| §9 成果物 | File Structure ＋ Task 6 |
| §10 リスク | Task 2 Step 9 の歯3種が「通常行への条件付け忘れ」「(Ⅱ)への誤付与」を直接検出 |
| §11 未確定事項 | Task 2 Step 3（トークン命名・option 対応の確認） |

**2. Placeholder scan**

`<ADR 0047で決めた…>` は**制度値・命名規約の意図的な契約スロット**である。ADR 0047 が値の唯一の出典であり、計画に書くと出典の外部化を壊すため、Phase 3-4 と同じ方針を採る。それ以外の TODO・TBD・「適切に処理する」類の記述は無い。

**3. Type consistency**

- `FacilityClassification` enum は Task 1 で定義し、Task 3（profile）・Task 4（provider）・Task 5（UI）が同名で参照する
- context・tokens の新フィールドはどちらも `string?` で名前は `FacilityClassification`。**どちらも record の末尾の省略可能パラメータ**とすることで既存呼び出しを壊さない
- `ServiceCodeResolutionErrorCode.FacilityClassificationUnresolved = 7` は Task 1 で定義し、Task 2 のテストが参照する
- Task 2 のテストが仮値として使う `"general"` / `"designated-support-factory"` 系のトークン文字列は、Step 3 で ADR 0047 が確定した値へ**テストと seed の両方**を揃える旨を明記した

**修正した齟齬（2件）**

1. Task 2 Step 1 のテストで施設トークンの仮値を `"designated-support-facility"` に統一した（一箇所 `factory` と書き誤っていた）。
2. `calculationOrder` を当初「実装を確認して決める」という未確定のまま残していたが、**計画作成中に `ValidateCalculationOrder`（`ClaimMasterFileValidator.cs:2893-2921`）を読んで確定した**。同一 `targetSelector`・同一期間で有効な割合加算行の `calculationOrder` は「1から連続かつ一意」でなければならず、処遇改善10行はすべて同じ `targetSelector` を共有して 2026-06 で同時に有効なので、施設 variant は **7〜10** で確定する。Task 2 Step 5 を確定形へ書き換えた。

---

## 参照

- 設計 spec: `docs/superpowers/specs/2026-07-26-phase3-5-facility-classification-design.md`
- `docs/decisions/0021-office-capability-official-codes.md` — 構造化入力の要求（239行）、R8 追加コード一覧
- `docs/decisions/0045-r8-treatment-improvement-addition-values.md` — 施設別立て率の抽出結果、コードと xlsx 行、「確定できなかった区分」表
- `docs/decisions/0025-claim-rounding-rules.md` — 割合加算の丸め
- `docs/phase3-4-acceptance.md` — 本スライスが引き取る残課題
