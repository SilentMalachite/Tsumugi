# Tsumugi Phase 3-1 実装計画 — 報酬算定エンジン＋マスタ実値

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3-0 で揃えたマスタ抽象（`IRateMaster`/`IAdditionMaster`/`IBurdenCapMaster`/`IRegionUnitMaster`）に実値を投入し、純粋関数の **B 型基本報酬算定エンジン**を Domain に実装する。基本報酬区分（平均工賃月額連動）・加算/減算・地域区分単価・利用者負担＋月額上限管理を網羅し、適用年月差し替え（令和 6 → 令和 9 仮データ）で無改修切替できることをテストで実証する。

**Architecture:** Domain `Logic/Claim/` 配下に純粋関数群を追加（`ClaimCalculator`、`BasicAllowanceClassifier`、`AdditionRules/*`、`BurdenCalculator`、`RoundingPolicy`）。マスタ実値は `Tsumugi.Infrastructure/Seed/Claim/*.json` の `entries` に投入（ADR 0018〜0022, 0025 の確定値）。Application に `CalculateClaimUseCase`（プレビュー）と `QueryClaimUseCase`（確定済参照／未確定再算定）を追加。

**Tech Stack:** .NET 10 / xUnit / FluentAssertions / `record struct` / `decimal` for unit prices / `int` for yen

## Global Constraints

> 親文書 `CLAUDE.md` §ハード制約、`01 §6`、`06_Phase3指示書 §4.1`、`docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md` を尊守。

- **着手条件**: Phase 3-0 完了、ADR 0018〜0023 確定、ADR 0019/0020 で `OfficeCapability` 正式コード集合確定済、`AverageWageMetric` の FIXME 解消準備済
- **金額は整数円**。最終額に浮動小数点を混入させない。地域区分単価は `decimal`、計算過程は `decimal`、最終円換算は `int` で確定
- **端数規則は ADR 0025**（本フェーズで確定）にマスタ駆動で詰める
- **Domain にハードコード禁止**: 単位数・加算・地域区分単価・負担上限額・閾値はすべてマスタ JSON 経由。Phase 3-0 のスキャナ (a)(c) 緑を維持
- **算定は分岐網羅 100% 目標**: テーブル駆動テストで既知ケース対応表を資産化
- **適用年月差し替え**: 令和 6 改定（現行）と令和 9 改定（仮データ）を同じ算定関数で切替えて結果が異なることをテストで実証
- **依存方向**: 算定関数は `Tsumugi.Domain.Logic.Claim` 内に閉じる。Infrastructure / EF / Avalonia を直接参照しない
- **TDD**: Red → Green → Refactor。1 コミット=1 論理変更。コミットメッセージに `phase3-1/AC3-N` を含める
- **Domain カバレッジ 95% 維持**、`Logic/Claim/` 配下は **100% 目標**

## ファイル構成

```
docs/decisions/
  0025-rounding-policy.md                                  新規 — 端数規則の確定

src/Tsumugi.Domain/Logic/Claim/
  RoundingPolicy.cs                                        新規 — 端数規則（マスタ駆動値を関数で適用）
  BasicAllowanceClassifier.cs                              新規 — B型基本報酬区分の解決（純粋関数）
  ClaimCalculator.cs                                       新規 — 算定本体（純粋関数）
  AdditionRules/
    AdditionRule.cs                                        新規 — 加算ルールの抽象（純粋関数）
    MealProvisionAdditionRule.cs                           新規
    TransportAdditionRule.cs                               新規
    StaffPlacementAdditionRule.cs                          新規
    AbsenceResponseAdditionRule.cs                         新規
    UpperLimitManagementAdditionRule.cs                    新規
    TargetWageAchievementAdditionRule.cs                   新規
    WelfareSpecialistAdditionRule.cs                       新規
  BurdenCalculator.cs                                      新規 — 利用者負担＋月額上限管理

src/Tsumugi.Domain/Logic/
  AverageWageMetric.cs                                     改修 — ADR 0022 確定後の正式定義を反映、FIXME 解消

src/Tsumugi.Domain/Entities/
  OfficeCapability.cs                                      コメント改修 — 正式コード集合に確定した旨を明記（型変更なし、暫定キー文書を更新）

src/Tsumugi.Infrastructure/
  Persistence/JsonClaimMasterLoader.cs                     拡張 — JSON スキーマ詳細を実装し空殻から実ロジックへ
  Seed/Claim/
    rates-v1.json                                          実値投入（令和 6 改定）
    additions-v1.json                                      実値投入
    burden-caps-v1.json                                    実値投入
    region-units-v1.json                                   実値投入
    meta.json                                              ADR バージョン・取得日を実値で埋める
    rates-v2-reiwa9-test.json                              新規 — 適用年月差し替えテスト用ダミー（令和 9 仮）

src/Tsumugi.Application/
  Dtos/Claim/
    ClaimResultDto.cs                                      新規
    ClaimLineDto.cs                                        新規
    ClaimBurdenDto.cs                                      新規
  UseCases/Claim/
    CalculateClaimUseCase.cs                               新規 — プレビュー（保存しない）
    QueryClaimUseCase.cs                                   新規 — 確定済参照／未確定再算定
  Validation/
    ClaimDateValidator.cs                                  新規（必要なら）

tests/
  Tsumugi.Domain.Tests/Logic/Claim/
    RoundingPolicyTests.cs                                 新規
    BasicAllowanceClassifierTests.cs                       新規 — 区分解決のテーブル駆動
    AdditionRules/                                         新規 — 各加算ルールのテスト
      MealProvisionAdditionRuleTests.cs
      TransportAdditionRuleTests.cs
      StaffPlacementAdditionRuleTests.cs
      AbsenceResponseAdditionRuleTests.cs
      UpperLimitManagementAdditionRuleTests.cs
      TargetWageAchievementAdditionRuleTests.cs
      WelfareSpecialistAdditionRuleTests.cs
    BurdenCalculatorTests.cs                               新規
    ClaimCalculatorTests.cs                                新規 — 算定本体（分岐網羅 100% 目標）
    EffectiveFromSwitchTests.cs                            新規 — 適用年月差し替え無改修切替（AC3-4）
    AverageWageMetricTests.cs                              改修 — ADR 0022 正式定義に合わせる
  Tsumugi.Application.Tests/UseCases/Claim/
    CalculateClaimUseCaseTests.cs                          新規
    QueryClaimUseCaseTests.cs                              新規
```

---

### Task 1: ADR 0025 — 端数規則の確定

**Files:**
- Create: `docs/decisions/0025-rounding-policy.md`

**Interfaces:**
- Produces: ADR 0025 が確定（Task 2 以降の前提）

- [ ] **Step 1: 端数規則を一次情報から確定**

厚生労働省告示で「単位数 × 地域区分単価」の小数点以下処理（切り捨て / 四捨五入 / 切り上げ）が定められている。一般的には「小数点以下切り捨て」だが、加算によっては別規則が適用されることもある。一次情報で確定し、ADR に書く:
- 基本: `単位数 × 単価 = 円` の小数点以下切り捨て
- 月次合計の丸め: 各明細を切り捨てたあと合算（明細単位で丸め → 合計）
- 利用者負担計算: `算定金額 × 1割 = 円` の端数規則も告示で確認

- [ ] **Step 2: ADR 0025 を執筆**

「結論→背景→選択肢→決定→影響」で記述:
- 結論: 端数規則を「明細単位で切り捨て → 合算」「利用者負担も同様」と確定
- 出典 URL と取得日、版番号
- 影響: `RoundingPolicy` 関数で集約。マスタ JSON に `roundingMode` フィールドを置く

- [ ] **Step 3: コミット**

```bash
git add docs/decisions/0025-rounding-policy.md
git commit -m "docs(phase3-1/AC3-3): ADR 0025 端数規則の確定"
```

---

### Task 2: RoundingPolicy — 端数規則の純粋関数

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/RoundingPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/RoundingPolicyTests.cs`

**Interfaces:**
- Consumes: ADR 0025
- Produces: `RoundingPolicy.MultiplyAndFloor(int unit, decimal unitPrice) → int`（単位 × 単価 → 円切り捨て）
- Produces: `RoundingPolicy.BurdenAtRate(int amountYen, decimal rate) → int`（金額 × 利率 → 円切り捨て）

- [ ] **Step 1: 失敗テストを書く（テーブル駆動）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Domain.Tests.Logic.Claim;

public sealed class RoundingPolicyTests
{
    [Theory]
    [InlineData(100, 10.0, 1000)]
    [InlineData(100, 10.5, 1050)]
    [InlineData(100, 11.20, 1120)]
    [InlineData(123, 10.45, 1285)]  // 123 * 10.45 = 1285.35 → 1285
    [InlineData(1, 11.20, 11)]       // 1 * 11.20 = 11.20 → 11
    [InlineData(0, 11.20, 0)]
    public void MultiplyAndFloor_floors_to_integer_yen(int unit, double unitPrice, int expected)
    {
        RoundingPolicy.MultiplyAndFloor(unit, (decimal)unitPrice).Should().Be(expected);
    }

    [Theory]
    [InlineData(10000, 0.1, 1000)]
    [InlineData(10009, 0.1, 1000)]      // 1000.9 → 1000
    [InlineData(10010, 0.1, 1001)]
    [InlineData(0, 0.1, 0)]
    public void BurdenAtRate_floors_to_integer_yen(int amount, double rate, int expected)
    {
        RoundingPolicy.BurdenAtRate(amount, (decimal)rate).Should().Be(expected);
    }
}
```

- [ ] **Step 2: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "RoundingPolicyTests" -v normal`
Expected: FAIL

- [ ] **Step 3: RoundingPolicy を実装**

```csharp
namespace Tsumugi.Domain.Logic.Claim;

/// <summary>
/// 端数規則（ADR 0025）。明細単位で切り捨て → 合算。
/// 単位数 × 単価 → 円切り捨て、利用者負担 = 金額 × 利率 → 円切り捨て。
/// </summary>
public static class RoundingPolicy
{
    public static int MultiplyAndFloor(int unit, decimal unitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unit);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        return (int)Math.Floor((decimal)unit * unitPrice);
    }

    public static int BurdenAtRate(int amountYen, decimal rate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        ArgumentOutOfRangeException.ThrowIfNegative(rate);
        return (int)Math.Floor((decimal)amountYen * rate);
    }
}
```

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "RoundingPolicyTests" -v normal
git add src/Tsumugi.Domain/Logic/Claim/RoundingPolicy.cs tests/Tsumugi.Domain.Tests/Logic/Claim/RoundingPolicyTests.cs
git commit -m "feat(phase3-1/AC3-3): RoundingPolicy pure function"
```

---

### Task 3: AverageWageMetric — FIXME 解消（ADR 0022 反映）

**Files:**
- Modify: `src/Tsumugi.Domain/Logic/AverageWageMetric.cs`
- Modify: `tests/Tsumugi.Domain.Tests/Logic/AverageWageMetricTests.cs`

**Interfaces:**
- Consumes: ADR 0022（平均工賃月額の正式定義）
- Produces: `AverageWageMetric.Calculate(IReadOnlyList<WageStatement>, AverageWageDenominator) → int` の正式定義版（FIXME コメント削除）

- [ ] **Step 1: AverageWageMetricTests を改修（失敗テスト）**

ADR 0022 の正式定義に従って、分母 / 基準期間 / 控除の動作をテーブル駆動でテストし直す。例:

```csharp
[Fact]
public void Calculate_uses_official_denominator_definition_per_ADR_0022()
{
    // ADR 0022 で「分母は実利用者数（同月内に 1 回でも工賃を受取った人）」と確定したケース
    var statements = new[]
    {
        WageStatement.NewRecord(/* recipientA, 月X, 10000円 */),
        WageStatement.NewRecord(/* recipientA, 月X, 5000円 */),  // 訂正
        WageStatement.NewRecord(/* recipientB, 月X, 8000円 */),
    };
    var actual = AverageWageMetric.Calculate(statements, AverageWageDenominator.ActiveRecipients);
    // 期待値は ADR 0022 で確定した計算ロジックに合わせる
    actual.Should().Be(/* 期待値 */);
}
```

- [ ] **Step 2: テスト赤確認 → 実装更新**

`src/Tsumugi.Domain/Logic/AverageWageMetric.cs` の `FIXME` コメントを削除し、ADR 0022 の正式定義に従って実装更新（分母切替の構造は維持）。

- [ ] **Step 3: テスト緑確認 → Domain カバレッジ確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "AverageWageMetric" -v normal
dotnet test tests/Tsumugi.Domain.Tests -p:CollectCoverage=true -p:Include="[Tsumugi.Domain]*" -p:Threshold=95
git add src/Tsumugi.Domain/Logic/AverageWageMetric.cs tests/Tsumugi.Domain.Tests/Logic/AverageWageMetricTests.cs
git commit -m "fix(phase3-1/AC3-2): resolve AverageWageMetric FIXME per ADR 0022"
```

---

### Task 4: マスタ JSON 実値投入（令和 6 改定）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Seed/Claim/rates-v1.json`
- Modify: `src/Tsumugi.Infrastructure/Seed/Claim/additions-v1.json`
- Modify: `src/Tsumugi.Infrastructure/Seed/Claim/burden-caps-v1.json`
- Modify: `src/Tsumugi.Infrastructure/Seed/Claim/region-units-v1.json`
- Modify: `src/Tsumugi.Infrastructure/Seed/Claim/meta.json`

**Interfaces:**
- Consumes: ADR 0018/0019/0021
- Produces: マスタ JSON が令和 6 改定の実値で埋まる

- [ ] **Step 1: rates-v1.json に B 型基本報酬の単位数を投入**

ADR 0018 から、B 型基本報酬の各区分（平均工賃月額区分 × 定員規模 × 人員配置）の単位数を引いて投入。例（実値は ADR 0018 確定値）:

```json
{
  "version": "v1",
  "effectiveFrom": "2024-04",
  "entries": [
    {
      "serviceCode": "B_BASE_W1_C20_S1",
      "comment": "平均工賃月額 区分1 / 定員 20以下 / 人員配置 1",
      "from": "2024-04",
      "capacityClass": 20,
      "unit": 0
    },
    // 各区分の組合せ
  ]
}
```

**注**: `comment` フィールドに日本語の説明を入れて良い（スキャナの語彙は文字列 literal `"単位数"` を見ているので JSON ファイルは対象外。ただし対象拡張時は除外設定を更新）。

- [ ] **Step 2: additions-v1.json に加算実値を投入**

ADR 0019 の加算正式コード集合に基づき:

```json
{
  "version": "v1",
  "entries": [
    { "additionCode": "MEAL_PROVISION_I", "from": "2024-04", "unit": 0 },
    { "additionCode": "MEAL_PROVISION_II", "from": "2024-04", "unit": 0 },
    { "additionCode": "TRANSPORT_I", "from": "2024-04", "unit": 0 },
    // ...
  ]
}
```

- [ ] **Step 3: burden-caps-v1.json に月額上限を投入**

ADR 0021 から:

```json
{
  "version": "v1",
  "entries": [
    { "category": "Livelihood", "from": "2024-04", "capYen": 0 },
    { "category": "LowIncome", "from": "2024-04", "capYen": 0 },
    { "category": "General1", "from": "2024-04", "capYen": 9300 },
    { "category": "General2", "from": "2024-04", "capYen": 37200 },
  ]
}
```

> 値は ADR 0021 で確定したものに置き換える（上記は構造例）。

- [ ] **Step 4: region-units-v1.json に地域区分単価を投入**

ADR 0018 から、`RegionGrade.Grade1`〜`Grade7`/`Other` × `B型基本報酬` 等の組合せ:

```json
{
  "version": "v1",
  "entries": [
    { "grade": "Grade1", "serviceCategory": "B_BASE", "from": "2024-04", "unitPrice": 11.20 },
    { "grade": "Grade2", "serviceCategory": "B_BASE", "from": "2024-04", "unitPrice": 10.90 },
    // ...
    { "grade": "Other",  "serviceCategory": "B_BASE", "from": "2024-04", "unitPrice": 10.00 }
  ]
}
```

- [ ] **Step 5: meta.json を実値で埋める**

```json
{
  "rates": { "adr": "0018", "version": "2024.04", "retrievedOn": "2026-06-29" },
  "additions": { "adr": "0019", "version": "2024.04", "retrievedOn": "2026-06-29" },
  "burdenCaps": { "adr": "0021", "version": "2024.04", "retrievedOn": "2026-06-29" },
  "regionUnits": { "adr": "0018", "version": "2024.04", "retrievedOn": "2026-06-29" }
}
```

- [ ] **Step 6: ビルド緑確認**

```bash
dotnet build src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj -c Release
```

JSON ファイルが EmbeddedResource として参照されるので、JSON シンタックスエラーがあればビルドは通るがロード時にエラー。手動で `JsonSerializer.Deserialize` を呼んで通過することを確認するスポットテストを追加してもよい。

- [ ] **Step 7: コミット**

```bash
git add src/Tsumugi.Infrastructure/Seed/Claim/
git commit -m "feat(phase3-1/AC3-1): seed master JSON with R6 official values"
```

---

### Task 5: JsonClaimMasterLoader 実装（スキーマ詳細）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/JsonClaimMasterLoader.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/Persistence/JsonClaimMasterLoaderTests.cs`

**Interfaces:**
- Consumes: Task 4 の JSON、Task 10（Phase 3-0）の interface
- Produces: `JsonRateMaster` / `JsonAdditionMaster` / `JsonBurdenCapMaster` / `JsonRegionUnitMaster` の内部実装が実値で動作する

Phase 3-0 で空殻だった内部クラスを実装。各 `Lookup*` メソッドが JSON entries から正しく解決するよう書き、Phase 3-0 で書いた InMemory 系テストと同等の境界条件テストを JSON ローダ側でも追加。

- [ ] **Step 1〜N**: 各内部クラス（`JsonRateMaster` 等）に entries の List を持たせ、`LookupBasic` 等で `effectiveFrom` 降順ソートして最新を返す実装。Phase 3-0 の `InMemoryRateMaster` の実装パターンを移植。

- [ ] **テスト追加**: 実値投入後の典型ケース（例: 区分1 / 定員20 / 2026-05 → 期待単位数）をいくつか固定。実値は ADR 0018 確定値に合わせる。

- [ ] **コミット**:

```bash
git add src/Tsumugi.Infrastructure/Persistence/JsonClaimMasterLoader.cs tests/Tsumugi.Infrastructure.Tests/Persistence/JsonClaimMasterLoaderTests.cs
git commit -m "feat(phase3-1/AC3-1): implement JsonClaimMasterLoader internals with real entries"
```

---

### Task 6: BasicAllowanceClassifier — B 型基本報酬区分の解決

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/BasicAllowanceClassifier.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/BasicAllowanceClassifierTests.cs`

**Interfaces:**
- Consumes: `IRateMaster`、`AverageWageMetric`
- Produces: `BasicAllowanceClassifier.ResolveServiceCode(int capacityClass, int staffingClass, AverageWageBracket bracket) → string`（サービスコード）
- Produces: `enum AverageWageBracket` ＝ ADR 0018 の区分（仮: `Bracket1`〜`Bracket6` 等、ADR 確定値に合わせる）
- Produces: `BasicAllowanceClassifier.ClassifyBracket(int averageWageYen, YearMonth ym, IRateMaster master) → AverageWageBracket`（閾値はマスタ経由）

> **重要**: 閾値はコードに焼かない。マスタ JSON の `bracketThresholds-v1.json`（必要なら）または `rates-v1.json` の補助テーブルから引く。Phase 3-1 着手前に ADR 0018 で確定した閾値テーブルがどこに収まるかを決定。

- [ ] **Step 1〜4**: TDD で `ClassifyBracket` と `ResolveServiceCode` を実装。テーブル駆動テストで各区分の境界（閾値ぴったり / 直下 / 直上）と、定員 × 人員配置の組合せを網羅。

- [ ] **Step 5: コミット**

```bash
git commit -m "feat(phase3-1/AC3-2): BasicAllowanceClassifier (B-type basic allowance bracket resolution)"
```

---

### Task 7: 加算ルール群（AdditionRules/）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/AdditionRule.cs`（共通インタフェース or static 関数）
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/MealProvisionAdditionRule.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/TransportAdditionRule.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/StaffPlacementAdditionRule.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/AbsenceResponseAdditionRule.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/UpperLimitManagementAdditionRule.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/TargetWageAchievementAdditionRule.cs`
- Create: `src/Tsumugi.Domain/Logic/Claim/AdditionRules/WelfareSpecialistAdditionRule.cs`
- Create: 各テストファイル

**Interfaces:**
- 各 `AdditionRule` は純粋関数で、引数は `(OfficeCapability.Flags, IReadOnlyList<DailyRecord> effective, Certificate, YearMonth, IAdditionMaster) → AdditionResult?`（適用可なら明細、不適用なら null）
- `AdditionResult = (string AdditionCode, int Unit)`（地域単価適用前の単位数）

各加算は 1 ファイル 1 関数で、TDD で適用条件と単位数取得をテスト。

- [ ] **Step 1: AdditionRule 共通インタフェース**

```csharp
namespace Tsumugi.Domain.Logic.Claim.AdditionRules;

public readonly record struct AdditionResult(string AdditionCode, int Unit);

public interface IAdditionRule
{
    AdditionResult? Apply(
        IReadOnlyDictionary<string, bool> officeCapabilityFlags,
        IReadOnlyList<DailyRecord> effectiveRecords,
        Certificate certificate,
        YearMonth yearMonth,
        IAdditionMaster master);
}
```

> 注: ステートレス純粋関数のため `static` メソッド集合でも可。Codex レビューで決定。

- [ ] **Step 2: MealProvisionAdditionRule（食事提供体制加算）**

```csharp
// 食事提供体制加算: OfficeCapability.Flags["meal_provision_i" or "meal_provision_ii"] = true
// かつ Certificate.MealProvisionApplicable = true
// かつ DailyRecord で食事提供のあった日数だけ単位数を計上
public sealed class MealProvisionAdditionRule : IAdditionRule
{
    public AdditionResult? Apply(/* ... */)
    {
        var i  = officeCapabilityFlags.GetValueOrDefault("meal_provision_i");
        var ii = officeCapabilityFlags.GetValueOrDefault("meal_provision_ii");
        if (!i && !ii) return null;
        if (!certificate.MealProvisionApplicable) return null;

        var mealDays = effectiveRecords.Count(r => r.MealProvided);
        if (mealDays == 0) return null;

        var code = i ? "MEAL_PROVISION_I" : "MEAL_PROVISION_II";
        var unit = master.LookupAddition(code, yearMonth) * mealDays;
        return new AdditionResult(code, unit);
    }
}
```

- [ ] **Step 3: テスト（食事提供体制加算）**

```csharp
[Theory]
[InlineData(true, false, true, 10, "MEAL_PROVISION_I")]   // I のみ
[InlineData(false, true, true, 10, "MEAL_PROVISION_II")]  // II のみ
[InlineData(false, false, true, 10, null)]                // 不適用
[InlineData(true, false, false, 10, null)]                // Certificate 不適用
[InlineData(true, false, true, 0, null)]                  // 食事 0 日
public void Apply_returns_expected(bool i, bool ii, bool certApplicable, int mealDays, string? expectedCode) { /* ... */ }
```

- [ ] **Step 4〜N: 残りの加算ルール（Transport / StaffPlacement / AbsenceResponse / UpperLimitManagement / TargetWageAchievement / WelfareSpecialist）も同パターン**

各加算の適用条件は ADR 0019 と ADR 0018 の付帯通知から確定。

- [ ] **Step N+1: 各加算ごとにコミット**

加算 1 個ずつコミットする（レビュー粒度を小さくする）:

```bash
git commit -m "feat(phase3-1/AC3-3): MealProvisionAdditionRule"
git commit -m "feat(phase3-1/AC3-3): TransportAdditionRule"
# ...
```

---

### Task 8: BurdenCalculator — 利用者負担＋月額上限管理

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/BurdenCalculator.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/BurdenCalculatorTests.cs`

**Interfaces:**
- Consumes: `IBurdenCapMaster`、`RoundingPolicy`、`Certificate`
- Produces: `BurdenCalculator.Calculate(int totalAmountYen, Certificate certificate, YearMonth ym, IBurdenCapMaster master) → BurdenResult`

```csharp
public readonly record struct BurdenResult(
    int RawBurdenYen,        // 算定金額 × 1割（切り捨て）
    int CapYen,              // 月額上限額
    int EffectiveBurdenYen); // min(RawBurden, Cap, Certificate.MonthlyCostCap)
```

- [ ] **Step 1〜4**: TDD で実装。テスト:
  - 各 `PaymentBurdenCategory` で上限額が正しく引かれること
  - 算定金額 × 1割 が上限を下回るケース／上回るケース／一致するケース
  - `Certificate.MonthlyCostCap` が補助上限として効くケース（受給者証側の個別上限）
  - 上限額管理事業所が指定されている場合の挙動（本フェーズではフラグ参照のみ、複数事業所合算は別途）

- [ ] **Step 5: コミット**

```bash
git commit -m "feat(phase3-1/AC3-3): BurdenCalculator (user copay + monthly cap)"
```

---

### Task 9: ClaimCalculator — 算定本体（純粋関数）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Claim/ClaimCalculator.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculatorTests.cs`

**Interfaces:**
- Consumes: `BasicAllowanceClassifier`、すべての加算ルール、`BurdenCalculator`、`RoundingPolicy`、`AverageWageMetric`、各マスタ interface
- Produces: 
  ```csharp
  public sealed record ClaimResult(
      Guid OfficeId,
      YearMonth YearMonth,
      IReadOnlyList<RecipientClaimResult> Recipients,
      int TotalUnit,
      int TotalAmountYen,
      int TotalBurdenYen);

  public sealed record RecipientClaimResult(
      Guid RecipientId,
      IReadOnlyList<ClaimLine> Lines,   // 基本報酬 + 各加算
      int SubtotalUnit,
      int SubtotalAmountYen,
      int BurdenYen);

  public sealed record ClaimLine(
      ClaimDetailLineKind LineKind,
      string Code,
      int Unit,
      int AmountYen);
  ```
- Produces: `ClaimCalculator.Calculate(YearMonth ym, Office office, OfficeCapability capability, IReadOnlyList<Recipient> recipients, IReadOnlyDictionary<Guid, Certificate> certificates, IReadOnlyDictionary<Guid, IReadOnlyList<DailyRecord>> effectiveDailyRecords, IReadOnlyList<WageStatement> wageStatements, IRateMaster rateMaster, IAdditionMaster additionMaster, IBurdenCapMaster burdenCapMaster, IRegionUnitMaster regionMaster) → ClaimResult`

- [ ] **Step 1: ClaimCalculator のシグネチャと出力型を定義**

`ClaimResult` / `RecipientClaimResult` / `ClaimLine` を `src/Tsumugi.Domain/Logic/Claim/` 内に定義。

- [ ] **Step 2: 失敗テストを書く（テーブル駆動 + 既知ケース対応表）**

ADR 0018〜0022 の確定値で「典型ケース」「境界ケース」「複数加算ケース」「ゼロ実績ケース」「欠席時対応のみケース」等を網羅。テストは大きくなるので `MemberData` で対応表データを外出し:

```csharp
public sealed class ClaimCalculatorTests
{
    public static IEnumerable<object[]> KnownCases => new[]
    {
        // (caseName, scenario data..., expected ClaimResult)
        new object[] { "minimum_b_type_no_addition", /* ... */ },
        new object[] { "with_meal_provision_i_for_full_month", /* ... */ },
        new object[] { "with_transport_to_all_recipients", /* ... */ },
        // ...
    };

    [Theory]
    [MemberData(nameof(KnownCases))]
    public void Calculate_matches_expected(string caseName, /* args */, ClaimResult expected)
    {
        var actual = ClaimCalculator.Calculate(/* args */);
        actual.Should().BeEquivalentTo(expected);
    }
}
```

> **分岐網羅 100% 目標**: テストケース対応表で全分岐が走るよう設計。`dotnet test --collect:"XPlat Code Coverage"` でカバレッジを確認し、`Tsumugi.Domain.Logic.Claim` の line coverage が 100% になるまでケース追加。

- [ ] **Step 3〜N: ClaimCalculator を実装**

実装は以下の順序:
1. 受給者ごとに `effectiveDailyRecords` を確認し、当月に 1 日以上の `Attendance=Present` がある受給者のみ対象
2. `AverageWageMetric.Calculate(wageStatements, denominator)` で平均工賃月額を算出
3. `BasicAllowanceClassifier.ClassifyBracket(...)` で区分解決、`ResolveServiceCode(...)` でサービスコード取得
4. 基本報酬: `unit = rateMaster.LookupBasic(serviceCode, ym, office.CapacityClass)` × `dailyAttendanceCount`
5. 各加算ルールを順に適用し、`AdditionResult` を集める
6. 各明細を `regionMaster.LookupUnitPrice(office.RegionGrade, "B_BASE", ym)` で円換算 → `RoundingPolicy.MultiplyAndFloor`
7. 受給者ごとに `BurdenCalculator.Calculate(subtotal, certificate, ym, burdenCapMaster)` で利用者負担
8. 全受給者を合算して `ClaimResult` を構築

- [ ] **Step N+1: テスト緑確認 + カバレッジ 100% 確認**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "ClaimCalculator" -v normal
dotnet test tests/Tsumugi.Domain.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Domain]Tsumugi.Domain.Logic.Claim.*" \
  -p:Threshold=100 \
  -p:ThresholdType=line
```

- [ ] **Step N+2: コミット**

```bash
git commit -m "feat(phase3-1/AC3-2,3): ClaimCalculator main calculation pipeline"
```

---

### Task 10: 適用年月差し替えテスト（AC3-4）

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/Logic/Claim/EffectiveFromSwitchTests.cs`
- Create: `src/Tsumugi.Infrastructure/Seed/Claim/rates-v2-reiwa9-test.json`（テスト用ダミー）

**Interfaces:**
- Consumes: `ClaimCalculator`、`JsonClaimMasterLoader`、テスト専用 `rates-v2-reiwa9-test.json`

このタスクは「マスタの `effectiveFrom` を変えるだけで算定結果が切り替わる」ことを実証する。

- [ ] **Step 1: rates-v2-reiwa9-test.json を作る**

令和 9 改定（仮データ）。実際の改定値は未公表のため、テスト用ダミー値を使う:

```json
{
  "version": "v2-reiwa9-test",
  "effectiveFrom": "2027-04",
  "entries": [
    {
      "serviceCode": "B_BASE_W1_C20_S1",
      "from": "2027-04",
      "capacityClass": 20,
      "unit": 999
    }
  ]
}
```

注意: このファイルはテスト専用なので、本番マスタとは別の経路（テスト時の `JsonClaimMasterLoader` ファクトリで切替）で読み込む。または、`rates-v1.json` の `entries` 配列に両方の `from` を持つ entry を追加するアプローチでも良い（後者の方が「無改修切替」を示しやすい）。

- [ ] **Step 2: rates-v1.json に令和 9 ダミー entry を追加**

実は、本来 `rates-v1.json` 内に `effectiveFrom` が異なる複数 entry を並べることが「無改修切替」の正しい示し方。テスト用ダミー値を追加:

```json
{
  "entries": [
    { "serviceCode": "B_BASE_W1_C20_S1", "from": "2024-04", "capacityClass": 20, "unit": <現行値> },
    { "serviceCode": "B_BASE_W1_C20_S1", "from": "2027-04", "capacityClass": 20, "unit": <ダミー値> }
  ]
}
```

これにより、同じ JSON ローダ・同じコードで `YearMonth(2027, 5)` を渡すと新しい単位数で算定される。

- [ ] **Step 3: EffectiveFromSwitchTests を書く**

```csharp
public sealed class EffectiveFromSwitchTests
{
    [Fact]
    public void Same_office_same_recipients_different_yearmonth_produces_different_results()
    {
        // 同じ事業所・受給者・実績で、対象月だけ 2026-05 と 2027-05 で算定
        var loader = new JsonClaimMasterLoader();
        var rateMaster = loader.LoadRates();
        // ...

        var resultR6 = ClaimCalculator.Calculate(new YearMonth(2026, 5), /* args */, rateMaster, /* ... */);
        var resultR9 = ClaimCalculator.Calculate(new YearMonth(2027, 5), /* args */, rateMaster, /* ... */);

        resultR6.TotalAmountYen.Should().NotBe(resultR9.TotalAmountYen,
            "適用年月差し替えで算定結果が変わるべき（AC3-4）");
    }
}
```

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "EffectiveFromSwitch" -v normal
git add src/Tsumugi.Infrastructure/Seed/Claim/rates-v1.json tests/Tsumugi.Domain.Tests/Logic/Claim/EffectiveFromSwitchTests.cs
git commit -m "test(phase3-1/AC3-4): effectiveFrom switch (R6 → R9 dummy) without code change"
```

---

### Task 11: OfficeCapability コメント更新（暫定キー→正式コード）

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/OfficeCapability.cs`（XML ドキュメントのみ）
- Modify: `docs/decisions/0006-office-capability-flag-set.md`（追記）

**Interfaces:**
- Consumes: ADR 0019/0020
- Produces: `OfficeCapability` の XML ドキュメントが正式コード集合を参照（型は変更なし）

- [ ] **Step 1: XML ドキュメント更新**

`OfficeCapability.cs` の XML コメントを「★要・報酬告示突合（暫定）」から「ADR 0019 で確定」に書き換え。例:

```csharp
/// <summary>
/// 事業所体制（期間マスタ・実効日付つき追記）。加算フラグは ADR 0019 で確定した正式コード集合（文字列キー）。
/// 例: "meal_provision_i", "meal_provision_ii", "transport_i", "transport_ii", "staff_placement_*", ...
/// 暫定キー（"mealProvision"/"transportSupport"）は ADR 0020 に従い 3-1 完了時点で正式コードに完全置換。
/// </summary>
```

- [ ] **Step 2: ADR 0006 を追記**

「2026-06-29 追記: ADR 0019/0020 で正式コード集合確定、Phase 3-1 で移行完了」。

- [ ] **Step 3: コミット**

```bash
git add src/Tsumugi.Domain/Entities/OfficeCapability.cs docs/decisions/0006-office-capability-flag-set.md
git commit -m "docs(phase3-1/AC3-0-8): finalize OfficeCapability flag key set per ADR 0019/0020"
```

---

### Task 12: Application — Claim DTO 群

**Files:**
- Create: `src/Tsumugi.Application/Dtos/Claim/ClaimResultDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/ClaimLineDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/ClaimBurdenDto.cs`

**Interfaces:**
- Consumes: Domain の `ClaimResult` / `ClaimLine` / `BurdenResult`
- Produces: UseCase / UI で受け渡す DTO

- [ ] **Step 1〜3: DTO を作成**

Domain の `record` を直接 UI に渡すと依存方向が崩れるので、Application 側に DTO を作る。フィールドは Domain の `ClaimResult` 等とほぼ同じだが、Domain 型（`YearMonth` 等）を Application でも参照しているのでそのまま使ってよい。

```csharp
public sealed record ClaimResultDto(
    Guid OfficeId,
    YearMonth YearMonth,
    IReadOnlyList<RecipientClaimDto> Recipients,
    int TotalUnit, int TotalAmountYen, int TotalBurdenYen,
    ClaimMasterVersion MasterVersionRates,
    ClaimMasterVersion MasterVersionAdditions,
    ClaimMasterVersion MasterVersionBurdenCaps,
    ClaimMasterVersion MasterVersionRegionUnits);

public sealed record RecipientClaimDto(
    Guid RecipientId,
    IReadOnlyList<ClaimLineDto> Lines,
    int SubtotalUnit, int SubtotalAmountYen,
    ClaimBurdenDto Burden);

public sealed record ClaimLineDto(ClaimDetailLineKind LineKind, string Code, int Unit, int AmountYen);

public sealed record ClaimBurdenDto(int RawBurdenYen, int CapYen, int EffectiveBurdenYen);
```

- [ ] **Step 4: コミット**

```bash
git commit -m "feat(phase3-1/AC3-1): Application Claim DTOs"
```

---

### Task 13: CalculateClaimUseCase — プレビュー算定

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/CalculateClaimUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/UseCases/Claim/CalculateClaimUseCaseTests.cs`

**Interfaces:**
- Consumes: `ClaimCalculator`、各リポジトリ抽象、`IRateMaster` 等
- Produces: `CalculateClaimUseCase.ExecuteAsync(Guid officeId, YearMonth ym, CancellationToken) → Task<ClaimResultDto>`

UseCase は **保存しない**。プレビュー専用。

- [ ] **Step 1〜N: TDD で実装**

```csharp
public sealed class CalculateClaimUseCase(
    IOfficeRepository officeRepo,
    IOfficeCapabilityRepository capabilityRepo,
    IRecipientRepository recipientRepo,
    ICertificateRepository certRepo,
    IDailyRecordRepository dailyRepo,
    IWageStatementRepository wageRepo,
    IRateMaster rateMaster,
    IAdditionMaster additionMaster,
    IBurdenCapMaster burdenCapMaster,
    IRegionUnitMaster regionMaster)
{
    public async Task<ClaimResultDto> ExecuteAsync(Guid officeId, YearMonth ym, CancellationToken ct)
    {
        // 1. データ取得
        var office = await officeRepo.GetByIdAsync(officeId, ct) ?? throw new InvalidOperationException(...);
        var capability = await capabilityRepo.GetEffectiveAsync(officeId, ym.LastDay, ct) ?? throw ...;
        var recipients = await recipientRepo.ListActiveAsync(ct);
        var certs = await certRepo.GetEffectiveForMonthAsync(ym, ct);
        var dailyByRecipient = await dailyRepo.GetEffectiveMonthAsync(ym, ct);
        var wages = await wageRepo.GetByOfficeAndMonthAsync(officeId, ym, ct);

        // 2. ClaimCalculator を呼ぶ（純粋）
        var domainResult = ClaimCalculator.Calculate(
            ym, office, capability, recipients, certs, dailyByRecipient, wages,
            rateMaster, additionMaster, burdenCapMaster, regionMaster);

        // 3. DTO に変換して返す
        return MapToDto(domainResult, rateMaster, additionMaster, burdenCapMaster, regionMaster);
    }

    private static ClaimResultDto MapToDto(ClaimResult r, IRateMaster rm, IAdditionMaster am, IBurdenCapMaster bcm, IRegionUnitMaster rum) => /* ... */;
}
```

テストは Mock リポジトリ + InMemory マスタ（Phase 3-0 のテストヘルパ再利用）。

- [ ] **Step N+1: コミット**

```bash
git commit -m "feat(phase3-1/AC3-1): CalculateClaimUseCase (preview)"
```

---

### Task 14: QueryClaimUseCase — 確定済参照／未確定再算定

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/QueryClaimUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/UseCases/Claim/QueryClaimUseCaseTests.cs`

**Interfaces:**
- Consumes: `IClaimBatchRepository`、`CalculateClaimUseCase`
- Produces: `QueryClaimUseCase.ExecuteAsync(Guid officeId, YearMonth ym, CancellationToken) → Task<ClaimQueryResult>`

```csharp
public sealed record ClaimQueryResult(ClaimResultDto Result, bool IsFromBatch, Guid? ClaimBatchId);
```

- [ ] **Step 1〜N**: 確定済 `ClaimBatch` があれば `ClaimBatchRepository.GetEffectiveBatchAsync` から復元、無ければ `CalculateClaimUseCase` を呼んで再算定。

```csharp
public async Task<ClaimQueryResult> ExecuteAsync(Guid officeId, YearMonth ym, CancellationToken ct)
{
    var batch = await batchRepo.GetEffectiveBatchAsync(officeId, ym, ct);
    if (batch is not null)
    {
        var details = await batchRepo.GetDetailsAsync(batch.Id, ct);
        var dto = MapFromBatch(batch, details);
        return new(dto, IsFromBatch: true, batch.Id);
    }
    var calculated = await calculate.ExecuteAsync(officeId, ym, ct);
    return new(calculated, IsFromBatch: false, ClaimBatchId: null);
}
```

- [ ] **Step N+1: コミット**

```bash
git commit -m "feat(phase3-1/AC3-1): QueryClaimUseCase (read snapshot or recalc)"
```

---

### Task 15: Application カバレッジ 90% への向上

**Files:**
- 各 UseCase テストの追加（既存も含めて 90% 達成）

**Interfaces:**
- 全 Application UseCase の line coverage が 90% を超える

- [ ] **Step 1: 現在のカバレッジ計測**

```bash
dotnet test tests/Tsumugi.Application.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Application]*" \
  -p:CoverletOutputFormat=lcov \
  -p:CoverletOutput=./TestResults/coverage.application.lcov.info
```

- [ ] **Step 2: 未カバー箇所を特定し、テストを追加**

主に Phase 3-1 で追加した `CalculateClaimUseCase` / `QueryClaimUseCase`、および既存 UseCase の未カバー分岐。

- [ ] **Step 3: 90% 到達確認**

```bash
dotnet test tests/Tsumugi.Application.Tests -c Release -p:CollectCoverage=true -p:Include="[Tsumugi.Application]*" -p:Threshold=90 -p:ThresholdType=line
```

Expected: PASS

- [ ] **Step 4: コミット**

```bash
git commit -m "test(phase3-1/AC3-3-add): raise Application coverage to 90%"
```

> **注**: `build/ci.sh` の閾値昇格（70→90）は Phase 3-3 で行うが、3-1 完了時点で実質 90% を達成しておく。

---

### Task 16: open-questions.md / CHANGELOG / 受け入れ確認

**Files:**
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Create: `docs/phase3-1-acceptance.md`

- [ ] **Step 1: open-questions.md 更新**

`AverageWageMetric` の FIXME 解消、ADR 0025 端数規則の確定をチェック。

- [ ] **Step 2: docs/phase3-1-acceptance.md 作成**

`phase3-0-acceptance.md` のフォーマットで AC3-1〜4 と AC3-1-add（Domain Logic.Claim 100% カバレッジ）を列挙。

- [ ] **Step 3: CHANGELOG.md 更新**

Phase 3-1 完了を追記。

- [ ] **Step 4: `./build/ci.sh` 緑確認**

```bash
./build/ci.sh
```

- [ ] **Step 5: コミット**

```bash
git commit -m "docs(phase3-1): Phase 3-1 acceptance complete + open-questions sync + CHANGELOG"
```

---

## Phase 3-1 全体受け入れ基準

- [ ] AC3-1 単価/加算/負担上限が seed JSON（適用年月版）供給、Domain literal 無し（(a)(c) 緑）
- [ ] AC3-2 B 型基本報酬区分が `AverageWageMetric`（ADR 0022）連動でマスタ駆動解決
- [ ] AC3-3 加算/減算・地域区分単価・利用者負担上限がマスタ駆動算定、既知ケース対応表で分岐網羅
- [ ] AC3-4 適用年月差し替え（R6 → R9）で無改修切替実証
- [ ] AC3-1-add Domain カバレッジ 95% 維持、`Tsumugi.Domain.Logic.Claim` は 100% 目標
- [ ] AC3-3-add Application カバレッジ実質 90% 達成（ci.sh 閾値昇格は 3-3）
- [ ] AC3-10（横断）`./build/ci.sh` 緑、依存方向不変、オフライン検査緑

## 参考

- 設計仕様書: `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`
- Phase 3 指示書 §4.1
- Phase 3-0 計画: `docs/superpowers/plans/2026-06-29-phase3-0-foundation.md`
