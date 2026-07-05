# Phase 4 / S0 実装計画 — KouchinModule v5 突合・手当モデル拡張・AC2-8 平均工賃

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** KouchinModule v5（.bas + .xlsm）の実挙動を Tsumugi の Domain モデルに突合し、ADR 0012 を「暫定」から「確定」へ書き換える。作業手当/職能手当を `WageSettings` 拡張、特別手当を `WageAdjustment` 新設、時給の期間変動を `RecipientHourlyRate` 新設で表現する。合わせて `AverageWageMetric` を差替え可能な構造に整理する。

**Architecture:** 4 層構成（Domain → Application → Infrastructure → App）と append-only 追記型を維持。手当は「事業所ルール（`WageSettings` 拡張）」と「利用者×月の任意支給（`WageAdjustment`）」の 2 種に分ける。端数は `RoundingRule.HalfUp` を新設し `RoundingPolicy` 純粋関数に集約。時給は利用者×期間の `RecipientHourlyRate` エンティティで持つ。`HourlyWageStrategy` は `WageInputs.DailyBreakdown`（新規オプション項目）を通じて日単位の時間・時給を受け取り、日ごとに ROUND(時間×時給) して合算する。

**Tech Stack:** .NET SDK 10 (`net10.0`) / C# 14 / Avalonia 11.x / EF Core 10.x + SQLite / CommunityToolkit.Mvvm / xUnit + FluentAssertions

## Global Constraints

- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` を維持。警告ゼロで通ること。
- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。Domain は Infrastructure/UI/EF/Avalonia を一切知らない。
- 全エンティティは `record` + append-only（訂正は元を壊さず `Kind=Correct`、取消は `Kind=Cancel` で履歴を残す）。主キーは `Guid`。`CreatedAt` / `CreatedBy` / `ConcurrencyToken` を持つ（`Entity` 基底継承）。
- 報酬算定・工賃計算は副作用のない純粋関数（日付/乱数/I/O に依存しない）。入出力を値で閉じる。
- 報酬告示・CSV 仕様の外部化ルールに触らない（Phase 3 スコープ）。本プランでは単位数・加算・地域区分単価などを一切扱わない。
- 全プロダクションアセンブリで通信 API（`System.Net.*` 等）への直接参照を追加しない。オフライン検査を維持。
- 保存先権限（Unix `0700`/`0600`、Windows DACL）と `AppendOnlyGuard` を新エンティティに適用。
- 1 コミット = 1 論理変更。メッセージは既存慣習に従う（`feat(phase4/s0):` / `test(phase4/s0):` / `docs(phase4/s0):` 等、末尾に `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>`）。
- TDD: Red → Green → Refactor。Domain カバレッジ ≥ 95%（`build/ci.sh` の閾値）を維持。
- `RecordKind` 列挙値は既存の `New = 1 / Correct = 2 / Cancel = 3` を使う（`Correction` ではない）。
- `.bas` / `.xlsm` はリポジトリに含めない。テスト値のコメントに「KouchinModule v5 突合（15日×26h×時給350円 → 16,600 円）」等の由来を残す。
- 全タスク完了後に `./build/ci.sh` が緑で通ること。

---

## ファイル構造マップ

**新規作成**:

```
src/Tsumugi.Domain/
  Enums/WageAdjustmentType.cs
  ValueObjects/SkillAllowanceTier.cs
  ValueObjects/DailyHourlyBasis.cs
  Entities/WageAdjustment.cs
  Entities/RecipientHourlyRate.cs
  Logic/RoundingPolicy.cs
  Logic/WageAdjustmentPolicy.cs
  Logic/RecipientHourlyRatePolicy.cs

src/Tsumugi.Application/
  Abstractions/IWageAdjustmentRepository.cs
  Abstractions/IRecipientHourlyRateRepository.cs
  Dtos/WageAdjustmentDto.cs
  Dtos/RecipientHourlyRateDto.cs
  Dtos/SkillAllowanceTierDto.cs
  UseCases/Wage/RecordWageAdjustmentUseCase.cs
  UseCases/Wage/QueryWageAdjustmentUseCase.cs
  UseCases/Wage/SetRecipientHourlyRateUseCase.cs
  UseCases/Wage/QueryRecipientHourlyRateUseCase.cs

src/Tsumugi.Infrastructure/
  Persistence/WageAdjustmentRepository.cs
  Persistence/RecipientHourlyRateRepository.cs
  Persistence/Configurations/WageAdjustmentConfiguration.cs
  Persistence/Configurations/RecipientHourlyRateConfiguration.cs
  Migrations/<ts>_AddWageAdjustment.cs
  Migrations/<ts>_AddRecipientHourlyRate.cs
  Migrations/<ts>_ExtendWageSettingsAllowances.cs

src/Tsumugi.App/
  ViewModels/RecipientHourlyRateViewModel.cs
  ViewModels/WageAdjustmentViewModel.cs
  Views/RecipientHourlyRateView.axaml (+ .axaml.cs)
  Views/WageAdjustmentView.axaml (+ .axaml.cs)

tests/
  Tsumugi.Domain.Tests/Logic/RoundingPolicyTests.cs
  Tsumugi.Domain.Tests/Entities/WageAdjustmentTests.cs
  Tsumugi.Domain.Tests/Entities/RecipientHourlyRateTests.cs
  Tsumugi.Domain.Tests/Logic/WageAdjustmentPolicyTests.cs
  Tsumugi.Domain.Tests/Logic/RecipientHourlyRatePolicyTests.cs
  Tsumugi.Domain.Tests/Logic/Wage/HourlyWageStrategyKouchinModuleTests.cs
  Tsumugi.Application.Tests/Wage/RecordWageAdjustmentUseCaseTests.cs
  Tsumugi.Application.Tests/Wage/QueryWageAdjustmentUseCaseTests.cs
  Tsumugi.Application.Tests/Wage/SetRecipientHourlyRateUseCaseTests.cs
  Tsumugi.Application.Tests/Wage/QueryRecipientHourlyRateUseCaseTests.cs
  Tsumugi.Infrastructure.Tests/Persistence/WageAdjustmentRepositoryTests.cs
  Tsumugi.Infrastructure.Tests/Persistence/RecipientHourlyRateRepositoryTests.cs
  Tsumugi.Infrastructure.Tests/Persistence/WageSettingsExtensionMigrationTests.cs
  Tsumugi.App.Tests/ViewModels/RecipientHourlyRateViewModelTests.cs
  Tsumugi.App.Tests/ViewModels/WageAdjustmentViewModelTests.cs

docs/decisions/
  0018-wage-adjustment-append-only.md
  0019-recipient-hourly-rate-periodic-master.md
```

**改修**:

```
src/Tsumugi.Domain/
  Enums/RoundingRule.cs                    +HalfUp / +Ceiling
  Entities/WageSettings.cs                 +WorkAllowancePerDayYen / +SkillAllowanceTiers / +HourUnitMinutes
  Logic/Wage/IWageMethodStrategy.cs        WageInputs に DailyBreakdown 追加
  Logic/Wage/HourlyWageStrategy.cs         15分単位検証・HalfUp 丸め・手当規則加算・日単位時給合算
  Logic/Wage/PieceWageStrategy.cs          手当規則加算
  Logic/Wage/FixedWageStrategy.cs          手当規則加算
  Logic/Wage/EqualWageStrategy.cs          手当規則加算
  Logic/AverageWageMetric.cs               コメント整理・拡張ポイント明示

src/Tsumugi.Application/
  Dtos/WageSettingsDto.cs                  +新3フィールド
  UseCases/Wage/ConfigureWageSettingsUseCase.cs  新フィールド受入
  UseCases/Wage/CalculateWagesUseCase.cs   手当合算 + RecipientHourlyRate 引き当て
  UseCases/Wage/CloseWagesUseCase.cs       BasisSummary に内訳保持

src/Tsumugi.Infrastructure/
  Persistence/Configurations/WageSettingsConfiguration.cs  新3列
  Persistence/AppendOnlyGuard.cs           WageAdjustment / RecipientHourlyRate 追加
  DependencyInjection.cs                   新Repo 登録
  Migrations/TsumugiDbContextModelSnapshot.cs  自動再生

src/Tsumugi.App/
  ViewModels/WageFundSettingsViewModel.cs  手当規則入力欄
  ViewModels/WageCalculationViewModel.cs   内訳表示
  Views/WageFundSettingsView.axaml         手当規則 UI
  Views/WageCalculationView.axaml          内訳表示
  Views/MainWindow.axaml                   新2タブ追加
  CompositionRoot.cs                       新 UseCase / Repository / VM 登録

docs/
  decisions/0012-wage-calculation-strategy.md   暫定 → 確定へ全面書換
  open-questions.md                             §11 の該当項目を更新
CHANGELOG.md                                    Unreleased 項目更新
```

---

## Task 1: `RoundingPolicy` と `RoundingRule.HalfUp` の追加

**Files:**
- Modify: `src/Tsumugi.Domain/Enums/RoundingRule.cs`
- Create: `src/Tsumugi.Domain/Logic/RoundingPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/RoundingPolicyTests.cs`

**Interfaces:**
- Consumes: 既存 `RoundingRule` 列挙（`FloorYen = 0`）
- Produces:
  - `RoundingRule.HalfUp = 1`、`RoundingRule.Ceiling = 2` を追加。既定値の並びは変更しない（`FloorYen = 0` は互換のため保持）。
  - `public static class Tsumugi.Domain.Logic.RoundingPolicy { public static int Round(decimal amount, RoundingRule rule); }`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// tests/Tsumugi.Domain.Tests/Logic/RoundingPolicyTests.cs
using FluentAssertions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic;

public class RoundingPolicyTests
{
    [Theory]
    [InlineData(100.4, RoundingRule.FloorYen, 100)]
    [InlineData(100.6, RoundingRule.FloorYen, 100)]
    [InlineData(-0.5, RoundingRule.FloorYen, -1)]
    [InlineData(100.4, RoundingRule.HalfUp, 100)]
    [InlineData(100.5, RoundingRule.HalfUp, 101)]
    [InlineData(100.6, RoundingRule.HalfUp, 101)]
    [InlineData(-100.5, RoundingRule.HalfUp, -101)] // AwayFromZero
    [InlineData(100.1, RoundingRule.Ceiling, 101)]
    [InlineData(100.0, RoundingRule.Ceiling, 100)]
    public void Round_returns_expected_integer(decimal amount, RoundingRule rule, int expected)
    {
        RoundingPolicy.Round(amount, rule).Should().Be(expected);
    }

    [Fact]
    public void Round_throws_on_unknown_rule()
    {
        var act = () => RoundingPolicy.Round(100m, (RoundingRule)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: テスト失敗を確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter FullyQualifiedName~RoundingPolicyTests`
Expected: FAIL（`RoundingPolicy` 未定義、`HalfUp`/`Ceiling` 未定義）

- [ ] **Step 3: `RoundingRule` を拡張**

```csharp
// src/Tsumugi.Domain/Enums/RoundingRule.cs
namespace Tsumugi.Domain.Enums;

public enum RoundingRule
{
    FloorYen = 0,
    HalfUp = 1,
    Ceiling = 2,
}
```

- [ ] **Step 4: `RoundingPolicy` を実装**

```csharp
// src/Tsumugi.Domain/Logic/RoundingPolicy.cs
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>工賃計算の端数規則を集約する純粋関数（KouchinModule v5 は HalfUp）。</summary>
public static class RoundingPolicy
{
    public static int Round(decimal amount, RoundingRule rule) => rule switch
    {
        RoundingRule.FloorYen => (int)Math.Floor(amount),
        RoundingRule.HalfUp => (int)Math.Round(amount, 0, MidpointRounding.AwayFromZero),
        RoundingRule.Ceiling => (int)Math.Ceiling(amount),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "未知の端数規則です。"),
    };
}
```

- [ ] **Step 5: テストを通す**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter FullyQualifiedName~RoundingPolicyTests`
Expected: PASS

- [ ] **Step 6: コミット**

```bash
git add src/Tsumugi.Domain/Enums/RoundingRule.cs \
        src/Tsumugi.Domain/Logic/RoundingPolicy.cs \
        tests/Tsumugi.Domain.Tests/Logic/RoundingPolicyTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s0): add RoundingPolicy and RoundingRule.HalfUp

KouchinModule v5 の日次工賃時給は ROUND(x, 0)（四捨五入）で
計算されるため、既存の FloorYen に加えて HalfUp / Ceiling を
追加し、丸め本体を RoundingPolicy 純粋関数へ集約する。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: `SkillAllowanceTier` と `WageSettings` 拡張

**Files:**
- Create: `src/Tsumugi.Domain/ValueObjects/SkillAllowanceTier.cs`
- Modify: `src/Tsumugi.Domain/Entities/WageSettings.cs`
- Create: `tests/Tsumugi.Domain.Tests/ValueObjects/SkillAllowanceTierTests.cs`
- Modify: `tests/Tsumugi.Domain.Tests/Entities/WageSettingsTests.cs`（既存があれば追記、なければ新設）

**Interfaces:**
- Consumes: `Entity` 基底、`RoundingRule`（Task 1）
- Produces:
  - `public sealed record SkillAllowanceTier(int MinHours, int Yen)` — 妥当性は集合レベルで判定（下記 `WageSettings.Create` で強制）
  - `WageSettings.WorkAllowancePerDayYen : int?`
  - `WageSettings.SkillAllowanceTiers : IReadOnlyList<SkillAllowanceTier>` （既定は空配列）
  - `WageSettings.HourUnitMinutes : int` （既定 15）
  - `WageSettings.Create(...)` に 3 引数追加。順序は `..., int? fixedDailyYen, int? workAllowancePerDayYen, IReadOnlyList<SkillAllowanceTier>? skillAllowanceTiers, int hourUnitMinutes, string createdBy, DateTimeOffset createdAt)`

- [ ] **Step 1: `SkillAllowanceTier` の失敗テスト**

```csharp
// tests/Tsumugi.Domain.Tests/ValueObjects/SkillAllowanceTierTests.cs
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.ValueObjects;

public class SkillAllowanceTierTests
{
    [Fact]
    public void Tier_holds_min_hours_and_yen()
    {
        var t = new SkillAllowanceTier(55, 2000);
        t.MinHours.Should().Be(55);
        t.Yen.Should().Be(2000);
    }

    // 妥当性チェックはコレクション単位で WageSettings.Create が担うため、
    // ここでは負値も型的に許容する（プリミティブ record として）。
    [Fact]
    public void Tier_allows_zero_thresholds_and_zero_yen()
    {
        var t = new SkillAllowanceTier(0, 0);
        t.MinHours.Should().Be(0);
        t.Yen.Should().Be(0);
    }
}
```

- [ ] **Step 2: `WageSettings` 拡張の失敗テストを書く**

```csharp
// tests/Tsumugi.Domain.Tests/Entities/WageSettingsTests.cs（新規または追記）
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Entities;

public class WageSettingsAllowanceExtensionTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly DateRange Period =
        new(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));

    [Fact]
    public void Create_defaults_hour_unit_to_15_and_empty_tiers()
    {
        var s = WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: 500,
            skillAllowanceTiers: null,
            hourUnitMinutes: 15,
            createdBy: "tester", createdAt: DateTimeOffset.UtcNow);

        s.WorkAllowancePerDayYen.Should().Be(500);
        s.SkillAllowanceTiers.Should().BeEmpty();
        s.HourUnitMinutes.Should().Be(15);
    }

    [Fact]
    public void Create_accepts_sorted_tier_list()
    {
        var tiers = new[] { new SkillAllowanceTier(55, 2000), new SkillAllowanceTier(70, 4000) };
        var s = WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        s.SkillAllowanceTiers.Should().Equal(tiers);
    }

    [Fact]
    public void Create_rejects_unsorted_tiers()
    {
        var tiers = new[] { new SkillAllowanceTier(70, 4000), new SkillAllowanceTier(55, 2000) };
        var act = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*昇順*");
    }

    [Fact]
    public void Create_rejects_duplicate_thresholds()
    {
        var tiers = new[] { new SkillAllowanceTier(55, 2000), new SkillAllowanceTier(55, 3000) };
        var act = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*重複*");
    }

    [Fact]
    public void Create_rejects_negative_amounts()
    {
        var act1 = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, -1, null, 15, "tester", DateTimeOffset.UtcNow);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var tiers = new[] { new SkillAllowanceTier(55, -10) };
        var act2 = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, tiers, 15, "tester", DateTimeOffset.UtcNow);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]  // 60 の約数ではない
    [InlineData(61)]
    public void Create_rejects_invalid_hour_unit(int minutes)
    {
        var act = () => WageSettings.Create(
            Guid.NewGuid(), OfficeId, Period,
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            4, null, 500, null, minutes, "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("*分単位*");
    }
}
```

- [ ] **Step 3: テストが赤で失敗することを確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter "FullyQualifiedName~WageSettingsAllowanceExtensionTests|FullyQualifiedName~SkillAllowanceTierTests"`
Expected: FAIL（`SkillAllowanceTier` 未定義、`Create` シグネチャ不整合）

- [ ] **Step 4: `SkillAllowanceTier` を実装**

```csharp
// src/Tsumugi.Domain/ValueObjects/SkillAllowanceTier.cs
namespace Tsumugi.Domain.ValueObjects;

/// <summary>職能手当の 1 段（就労時間の閾値と支給額）。妥当性は WageSettings.Create が集合単位で判定する。</summary>
public sealed record SkillAllowanceTier(int MinHours, int Yen);
```

- [ ] **Step 5: `WageSettings` を拡張**

```csharp
// src/Tsumugi.Domain/Entities/WageSettings.cs（全体を差し替え）
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>工賃設定（期間マスタ、追記）。基準日時点の方式・端数規則・年度起点・手当規則を引く。</summary>
public sealed record WageSettings : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateRange Period { get; init; }
    public required WageMethod Method { get; init; }
    public required RoundingRule Rounding { get; init; }
    public required RemainderPolicy Remainder { get; init; }
    public required int FiscalYearStartMonth { get; init; }
    public int? FixedDailyYen { get; init; }

    // v2 追加
    public int? WorkAllowancePerDayYen { get; init; }
    public required IReadOnlyList<SkillAllowanceTier> SkillAllowanceTiers { get; init; }
    public required int HourUnitMinutes { get; init; }

    private static readonly int[] AllowedHourUnitMinutes =
        [1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60];

    public static WageSettings Create(
        Guid id, Guid officeId, DateRange period,
        WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
        int fiscalYearStartMonth, int? fixedDailyYen,
        int? workAllowancePerDayYen,
        IReadOnlyList<SkillAllowanceTier>? skillAllowanceTiers,
        int hourUnitMinutes,
        string createdBy, DateTimeOffset createdAt)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth),
                fiscalYearStartMonth, "年度起点月は1〜12の範囲で指定してください。");
        if (method == WageMethod.Fixed && fixedDailyYen is null)
            throw new ArgumentException("Fixed 方式では FixedDailyYen を指定してください。", nameof(fixedDailyYen));
        if (fixedDailyYen is { } y && y < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedDailyYen), y, "FixedDailyYen は0円以上で指定してください。");
        if (workAllowancePerDayYen is { } w && w < 0)
            throw new ArgumentOutOfRangeException(nameof(workAllowancePerDayYen), w,
                "作業手当日額は0円以上で指定してください。");

        var tiers = skillAllowanceTiers ?? Array.Empty<SkillAllowanceTier>();
        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].Yen < 0)
                throw new ArgumentOutOfRangeException(nameof(skillAllowanceTiers),
                    tiers[i].Yen, "職能手当の金額は0円以上で指定してください。");
            if (i > 0 && tiers[i].MinHours <= tiers[i - 1].MinHours)
            {
                var reason = tiers[i].MinHours == tiers[i - 1].MinHours ? "重複しています" : "昇順ではありません";
                throw new ArgumentException($"職能手当の閾値が{reason}。", nameof(skillAllowanceTiers));
            }
        }

        if (!AllowedHourUnitMinutes.Contains(hourUnitMinutes))
            throw new ArgumentException(
                "工賃時給の最小単位は 60 の約数（1〜60 分単位）で指定してください。",
                nameof(hourUnitMinutes));

        return new WageSettings
        {
            Id = id,
            OfficeId = officeId,
            Period = period,
            Method = method,
            Rounding = rounding,
            Remainder = remainder,
            FiscalYearStartMonth = fiscalYearStartMonth,
            FixedDailyYen = fixedDailyYen,
            WorkAllowancePerDayYen = workAllowancePerDayYen,
            SkillAllowanceTiers = tiers,
            HourUnitMinutes = hourUnitMinutes,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
```

- [ ] **Step 6: 既存の `WageSettings.Create` 呼出しを新シグネチャに追随**

Run: `dotnet build src/Tsumugi.Application` で失敗箇所を洗い出す。既存の呼出しは `ConfigureWageSettingsUseCase` にあるので、そこは Task 8 で改修する（本タスクでは build エラーを避けるため、暫定的にオーバーロードを追加）。

以下を `WageSettings` に追記（暫定・Task 8 で除去）:

```csharp
    // 暫定オーバーロード（Task 8 で除去）
    public static WageSettings Create(
        Guid id, Guid officeId, DateRange period,
        WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
        int fiscalYearStartMonth, int? fixedDailyYen,
        string createdBy, DateTimeOffset createdAt) =>
        Create(id, officeId, period, method, rounding, remainder,
            fiscalYearStartMonth, fixedDailyYen,
            workAllowancePerDayYen: null,
            skillAllowanceTiers: null,
            hourUnitMinutes: 15,
            createdBy, createdAt);
```

- [ ] **Step 7: テストと build を通す**

Run: `dotnet build && dotnet test tests/Tsumugi.Domain.Tests/`
Expected: PASS

- [ ] **Step 8: コミット**

```bash
git add src/Tsumugi.Domain/ValueObjects/SkillAllowanceTier.cs \
        src/Tsumugi.Domain/Entities/WageSettings.cs \
        tests/Tsumugi.Domain.Tests/ValueObjects/SkillAllowanceTierTests.cs \
        tests/Tsumugi.Domain.Tests/Entities/WageSettingsTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s0): extend WageSettings with allowance rules and hour unit

作業手当日額・職能手当閾値表（IReadOnlyList<SkillAllowanceTier>）・
工賃時給最小単位（分）を追加。閾値表は昇順・重複禁止・非負を Create
で強制する。既存呼出しの互換のため暫定オーバーロードを併存させる
（Task 8 で除去）。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: `RecipientHourlyRate` エンティティと `RecipientHourlyRatePolicy`

**Files:**
- Create: `src/Tsumugi.Domain/Entities/RecipientHourlyRate.cs`
- Create: `src/Tsumugi.Domain/Logic/RecipientHourlyRatePolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Entities/RecipientHourlyRateTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/RecipientHourlyRatePolicyTests.cs`

**Interfaces:**
- Consumes: `Entity` 基底、`RecordKind`、`DateRange`
- Produces:
  - `public sealed record RecipientHourlyRate : Entity` — フィールド `OfficeId, RecipientId, Period, HourlyYen, Kind, OriginId, Note`
  - ファクトリ: `NewRecord(...)`, `Correction(...)`, `Cancel(...)`
  - `public static class RecipientHourlyRatePolicy { public static int? EffectiveYen(IEnumerable<RecipientHourlyRate> records, Guid recipientId, DateOnly asOf); }`

- [ ] **Step 1: エンティティ失敗テスト**

```csharp
// tests/Tsumugi.Domain.Tests/Entities/RecipientHourlyRateTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Entities;

public class RecipientHourlyRateTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly DateRange Period =
        new(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));

    [Fact]
    public void NewRecord_sets_defaults()
    {
        var r = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, Period, 350,
            "tester", DateTimeOffset.UtcNow);
        r.Kind.Should().Be(RecordKind.New);
        r.OriginId.Should().BeNull();
        r.HourlyYen.Should().Be(350);
    }

    [Fact]
    public void NewRecord_rejects_negative_hourly()
    {
        var act = () => RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, Period, -1,
            "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Correction_requires_origin_id()
    {
        var origin = Guid.NewGuid();
        var r = RecipientHourlyRate.Correction(
            Guid.NewGuid(), Office, Recipient, Period, origin, 400,
            "tester", DateTimeOffset.UtcNow);
        r.Kind.Should().Be(RecordKind.Correct);
        r.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancel_requires_origin_id()
    {
        var origin = Guid.NewGuid();
        var r = RecipientHourlyRate.Cancel(
            Guid.NewGuid(), Office, Recipient, Period, origin,
            "tester", DateTimeOffset.UtcNow);
        r.Kind.Should().Be(RecordKind.Cancel);
        r.HourlyYen.Should().Be(0);
    }
}
```

- [ ] **Step 2: Policy 失敗テスト**

```csharp
// tests/Tsumugi.Domain.Tests/Logic/RecipientHourlyRatePolicyTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic;

public class RecipientHourlyRatePolicyTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static DateTimeOffset At(int day) => new(2026, 4, day, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EffectiveYen_returns_null_when_no_records()
    {
        RecipientHourlyRatePolicy.EffectiveYen(
            Array.Empty<RecipientHourlyRate>(), A, new DateOnly(2026, 5, 1))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_returns_yen_within_period()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var r = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r }, A, new DateOnly(2026, 4, 15))
            .Should().Be(350);
    }

    [Fact]
    public void EffectiveYen_uses_latest_correction()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        var corr = RecipientHourlyRate.Correction(Guid.NewGuid(), Office, A, p, newRec.Id, 400, "u", At(2));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, corr }, A, new DateOnly(2026, 4, 15))
            .Should().Be(400);
    }

    [Fact]
    public void EffectiveYen_returns_null_after_cancel()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var newRec = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        var cancel = RecipientHourlyRate.Cancel(Guid.NewGuid(), Office, A, p, newRec.Id, "u", At(3));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { newRec, cancel }, A, new DateOnly(2026, 4, 15))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_filters_by_recipient()
    {
        var p = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 31));
        var r = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p, 350, "u", At(1));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r }, B, new DateOnly(2026, 4, 15))
            .Should().BeNull();
    }

    [Fact]
    public void EffectiveYen_switches_across_multiple_periods()
    {
        var p1 = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 29));
        var p2 = new DateRange(new DateOnly(2026, 4, 30), new DateOnly(2026, 6, 30));
        var r1 = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p1, 350, "u", At(1));
        var r2 = RecipientHourlyRate.NewRecord(Guid.NewGuid(), Office, A, p2, 400, "u", At(2));
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r1, r2 }, A, new DateOnly(2026, 4, 29)).Should().Be(350);
        RecipientHourlyRatePolicy.EffectiveYen(new[] { r1, r2 }, A, new DateOnly(2026, 4, 30)).Should().Be(400);
    }
}
```

- [ ] **Step 3: 失敗を確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter "FullyQualifiedName~RecipientHourlyRate"`
Expected: FAIL

- [ ] **Step 4: エンティティ実装**

```csharp
// src/Tsumugi.Domain/Entities/RecipientHourlyRate.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>利用者×期間の時給マスタ（追記型）。KouchinModule v5 は月中変動を扱うため期間マスタで持つ。</summary>
public sealed record RecipientHourlyRate : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required DateRange Period { get; init; }
    public required int HourlyYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static RecipientHourlyRate NewRecord(
        Guid id, Guid officeId, Guid recipientId, DateRange period, int hourlyYen,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hourlyYen);
        return new RecipientHourlyRate
        {
            Id = id, OfficeId = officeId, RecipientId = recipientId,
            Period = period, HourlyYen = hourlyYen,
            Kind = RecordKind.New, OriginId = null,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static RecipientHourlyRate Correction(
        Guid id, Guid officeId, Guid recipientId, DateRange period, Guid originId, int hourlyYen,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hourlyYen);
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new RecipientHourlyRate
        {
            Id = id, OfficeId = officeId, RecipientId = recipientId,
            Period = period, HourlyYen = hourlyYen,
            Kind = RecordKind.Correct, OriginId = originId,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static RecipientHourlyRate Cancel(
        Guid id, Guid officeId, Guid recipientId, DateRange period, Guid originId,
        string createdBy, DateTimeOffset createdAt)
    {
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new RecipientHourlyRate
        {
            Id = id, OfficeId = officeId, RecipientId = recipientId,
            Period = period, HourlyYen = 0,
            Kind = RecordKind.Cancel, OriginId = originId,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
```

- [ ] **Step 5: Policy 実装**

```csharp
// src/Tsumugi.Domain/Logic/RecipientHourlyRatePolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>RecipientHourlyRate 群から (recipientId, asOf) での実効時給を導出する純粋関数。</summary>
public static class RecipientHourlyRatePolicy
{
    public static int? EffectiveYen(
        IEnumerable<RecipientHourlyRate> records, Guid recipientId, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(records);
        var groups = records
            .Where(r => r.RecipientId == recipientId)
            .Where(r => r.Period.Includes(asOf))
            .GroupBy(r => r.Kind == RecordKind.New ? r.Id : r.OriginId ?? r.Id);

        int? latest = null;
        DateTimeOffset latestAt = DateTimeOffset.MinValue;
        foreach (var g in groups)
        {
            var ordered = g.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList();
            if (ordered.Count == 0) continue;
            var last = ordered[^1];
            if (last.Kind == RecordKind.Cancel) continue;
            if (last.CreatedAt >= latestAt)
            {
                latestAt = last.CreatedAt;
                latest = last.HourlyYen;
            }
        }
        return latest;
    }
}
```

（`DateRange.Includes(asOf)` が既存にあることを前提。もしなければ範囲判定を `r.Period.Start <= asOf && asOf <= r.Period.End` に置換する）

- [ ] **Step 6: テスト緑化**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter "FullyQualifiedName~RecipientHourlyRate"`
Expected: PASS

- [ ] **Step 7: コミット**

```bash
git add src/Tsumugi.Domain/Entities/RecipientHourlyRate.cs \
        src/Tsumugi.Domain/Logic/RecipientHourlyRatePolicy.cs \
        tests/Tsumugi.Domain.Tests/Entities/RecipientHourlyRateTests.cs \
        tests/Tsumugi.Domain.Tests/Logic/RecipientHourlyRatePolicyTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s0): add RecipientHourlyRate periodic master and policy

利用者×期間の時給を append-only レコード（New/Correct/Cancel）で
保持し、EffectiveYen(records, recipientId, asOf) 純粋関数で任意
日付の実効値を導出する。KouchinModule v5 の月中変動（AU12 検知）
を追記型で表現するための土台。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `WageAdjustment` エンティティと `WageAdjustmentPolicy`

**Files:**
- Create: `src/Tsumugi.Domain/Enums/WageAdjustmentType.cs`
- Create: `src/Tsumugi.Domain/Entities/WageAdjustment.cs`
- Create: `src/Tsumugi.Domain/Logic/WageAdjustmentPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Entities/WageAdjustmentTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/WageAdjustmentPolicyTests.cs`

**Interfaces:**
- Consumes: `Entity`, `RecordKind`, `YearMonth`
- Produces:
  - `public enum WageAdjustmentType { SpecialAllowance = 0 }`
  - `public sealed record WageAdjustment : Entity` — `OfficeId, RecipientId, YearMonth, Type, AmountYen, Kind, OriginId, Note`
  - `NewRecord / Correction / Cancel` ファクトリ
  - `public static class WageAdjustmentPolicy { EffectiveYen(records, recipientId, ym, type); SumEffective(records, recipientId, ym); }`

- [ ] **Step 1: 失敗テスト（エンティティと Policy を同時に）**

```csharp
// tests/Tsumugi.Domain.Tests/Entities/WageAdjustmentTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Entities;

public class WageAdjustmentTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly YearMonth Ym = YearMonth.FromInt32(202605);

    [Fact]
    public void NewRecord_sets_defaults()
    {
        var w = WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, note: null,
            "tester", DateTimeOffset.UtcNow);
        w.Kind.Should().Be(RecordKind.New);
        w.OriginId.Should().BeNull();
        w.AmountYen.Should().Be(1000);
    }

    [Fact]
    public void NewRecord_rejects_negative_amount()
    {
        var act = () => WageAdjustment.NewRecord(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, -1, null,
            "tester", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Correction_and_cancel_require_origin_id()
    {
        var act1 = () => WageAdjustment.Correction(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, Guid.Empty, 500, null,
            "tester", DateTimeOffset.UtcNow);
        act1.Should().Throw<ArgumentException>();

        var act2 = () => WageAdjustment.Cancel(
            Guid.NewGuid(), Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, Guid.Empty,
            "tester", DateTimeOffset.UtcNow);
        act2.Should().Throw<ArgumentException>();
    }
}
```

```csharp
// tests/Tsumugi.Domain.Tests/Logic/WageAdjustmentPolicyTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic;

public class WageAdjustmentPolicyTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly YearMonth Ym = YearMonth.FromInt32(202605);
    private static DateTimeOffset At(int day) => new(2026, 5, day, 0, 0, 0, TimeSpan.Zero);

    private static WageAdjustment New(int amount, DateTimeOffset at) =>
        WageAdjustment.NewRecord(Guid.NewGuid(), Office, A, Ym,
            WageAdjustmentType.SpecialAllowance, amount, null, "u", at);

    [Fact]
    public void Effective_returns_zero_when_empty()
    {
        WageAdjustmentPolicy.EffectiveYen(
            Array.Empty<WageAdjustment>(), A, Ym, WageAdjustmentType.SpecialAllowance)
            .Should().Be(0);
    }

    [Fact]
    public void Effective_returns_new_when_only_new()
    {
        var r = New(1000, At(1));
        WageAdjustmentPolicy.EffectiveYen(new[] { r }, A, Ym,
            WageAdjustmentType.SpecialAllowance).Should().Be(1000);
    }

    [Fact]
    public void Effective_applies_correction_over_new()
    {
        var n = New(1000, At(1));
        var c = WageAdjustment.Correction(Guid.NewGuid(), Office, A, Ym,
            WageAdjustmentType.SpecialAllowance, n.Id, 1500, null, "u", At(2));
        WageAdjustmentPolicy.EffectiveYen(new[] { n, c }, A, Ym,
            WageAdjustmentType.SpecialAllowance).Should().Be(1500);
    }

    [Fact]
    public void Effective_returns_zero_after_cancel()
    {
        var n = New(1000, At(1));
        var x = WageAdjustment.Cancel(Guid.NewGuid(), Office, A, Ym,
            WageAdjustmentType.SpecialAllowance, n.Id, "u", At(3));
        WageAdjustmentPolicy.EffectiveYen(new[] { n, x }, A, Ym,
            WageAdjustmentType.SpecialAllowance).Should().Be(0);
    }

    [Fact]
    public void SumEffective_sums_types()
    {
        var n = New(1000, At(1));
        WageAdjustmentPolicy.SumEffective(new[] { n }, A, Ym).Should().Be(1000);
    }
}
```

- [ ] **Step 2: テスト失敗を確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter "FullyQualifiedName~WageAdjustment"`
Expected: FAIL

- [ ] **Step 3: 実装**

```csharp
// src/Tsumugi.Domain/Enums/WageAdjustmentType.cs
namespace Tsumugi.Domain.Enums;

public enum WageAdjustmentType
{
    SpecialAllowance = 0,
}
```

```csharp
// src/Tsumugi.Domain/Entities/WageAdjustment.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>利用者×月の任意手当（追記型）。KouchinModule 工賃集計 G 列（特別手当）等を受ける。</summary>
public sealed record WageAdjustment : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required YearMonth YearMonth { get; init; }
    public required WageAdjustmentType Type { get; init; }
    public required int AmountYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static WageAdjustment NewRecord(
        Guid id, Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, int amountYen, string? note,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        return new WageAdjustment
        {
            Id = id, OfficeId = officeId, RecipientId = recipientId,
            YearMonth = yearMonth, Type = type, AmountYen = amountYen,
            Kind = RecordKind.New, OriginId = null, Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static WageAdjustment Correction(
        Guid id, Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, Guid originId, int amountYen, string? note,
        string createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountYen);
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new WageAdjustment
        {
            Id = id, OfficeId = officeId, RecipientId = recipientId,
            YearMonth = yearMonth, Type = type, AmountYen = amountYen,
            Kind = RecordKind.Correct, OriginId = originId, Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

    public static WageAdjustment Cancel(
        Guid id, Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, Guid originId,
        string createdBy, DateTimeOffset createdAt)
    {
        if (originId == Guid.Empty) throw new ArgumentException("OriginId が空です。", nameof(originId));
        return new WageAdjustment
        {
            Id = id, OfficeId = officeId, RecipientId = recipientId,
            YearMonth = yearMonth, Type = type, AmountYen = 0,
            Kind = RecordKind.Cancel, OriginId = originId, Note = null,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
```

```csharp
// src/Tsumugi.Domain/Logic/WageAdjustmentPolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

public static class WageAdjustmentPolicy
{
    public static int EffectiveYen(
        IEnumerable<WageAdjustment> records,
        Guid recipientId, YearMonth ym, WageAdjustmentType type)
    {
        ArgumentNullException.ThrowIfNull(records);
        var chain = records
            .Where(r => r.RecipientId == recipientId && r.YearMonth == ym && r.Type == type)
            .GroupBy(r => r.Kind == RecordKind.New ? r.Id : r.OriginId ?? r.Id)
            .Select(g => g.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToList())
            .Where(g => g.Count > 0 && g[^1].Kind != RecordKind.Cancel)
            .OrderByDescending(g => g[^1].CreatedAt)
            .FirstOrDefault();
        return chain is null ? 0 : chain[^1].AmountYen;
    }

    public static int SumEffective(
        IEnumerable<WageAdjustment> records, Guid recipientId, YearMonth ym)
    {
        var materialized = records as IReadOnlyCollection<WageAdjustment> ?? records.ToList();
        return Enum.GetValues<WageAdjustmentType>()
            .Sum(t => EffectiveYen(materialized, recipientId, ym, t));
    }
}
```

- [ ] **Step 4: テスト緑化**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter "FullyQualifiedName~WageAdjustment"`
Expected: PASS

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.Domain/Enums/WageAdjustmentType.cs \
        src/Tsumugi.Domain/Entities/WageAdjustment.cs \
        src/Tsumugi.Domain/Logic/WageAdjustmentPolicy.cs \
        tests/Tsumugi.Domain.Tests/Entities/WageAdjustmentTests.cs \
        tests/Tsumugi.Domain.Tests/Logic/WageAdjustmentPolicyTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s0): add WageAdjustment append-only entity and policy

利用者×月×手当種別の任意支給を append-only レコード
（New/Correct/Cancel）で受ける。EffectiveYen / SumEffective を
純粋関数で導出。KouchinModule 工賃集計 G 列（特別手当・手入力）
を Tsumugi で表現するための土台。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: `WageInputs.DailyBreakdown` と `HourlyWageStrategy` の突合実装

**Files:**
- Create: `src/Tsumugi.Domain/ValueObjects/DailyHourlyBasis.cs`
- Modify: `src/Tsumugi.Domain/Logic/Wage/IWageMethodStrategy.cs`
- Modify: `src/Tsumugi.Domain/Logic/Wage/HourlyWageStrategy.cs`
- Modify: `src/Tsumugi.Domain/Logic/Wage/PieceWageStrategy.cs`
- Modify: `src/Tsumugi.Domain/Logic/Wage/FixedWageStrategy.cs`
- Modify: `src/Tsumugi.Domain/Logic/Wage/EqualWageStrategy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Logic/Wage/HourlyWageStrategyKouchinModuleTests.cs`

**Interfaces:**
- Consumes: `WageSettings`（Task 2）、`RoundingPolicy`（Task 1）、`WageInputs`
- Produces:
  - `public sealed record DailyHourlyBasis(DateOnly Date, int Minutes, int HourlyYen)`
  - `WageInputs` に `IReadOnlyList<DailyHourlyBasis>? DailyBreakdown` を追加（既定 null）
  - 全 Strategy が `WageSettings.WorkAllowancePerDayYen` と `SkillAllowanceTiers` を反映
  - `HourlyWageStrategy` は `DailyBreakdown` があれば日単位で `RoundingPolicy.Round(minutes/60 * yen, rule)` を合算、なければ従来通り `TotalWorkedMinutes` を使用
  - **KouchinModule 突合ケース**: 就労15日・就労時間 26h・時給 350 円 → 期待 16,600 円

- [ ] **Step 1: `DailyHourlyBasis` 値オブジェクト作成**

```csharp
// src/Tsumugi.Domain/ValueObjects/DailyHourlyBasis.cs
namespace Tsumugi.Domain.ValueObjects;

/// <summary>日単位の時給・時間の基礎値。RecipientHourlyRate の実効値と WorkRecord から Application 層で組み立てる。</summary>
public sealed record DailyHourlyBasis(DateOnly Date, int Minutes, int HourlyYen);
```

- [ ] **Step 2: `WageInputs` に `DailyBreakdown` を追加（コンストラクタ引数の後方互換）**

```csharp
// src/Tsumugi.Domain/Logic/Wage/IWageMethodStrategy.cs（該当箇所を差替え）
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Wage;

public sealed record WageInputs(
    Guid RecipientId,
    int PresentDays,
    int TotalWorkedMinutes,
    int TotalPieceAmountYen,
    int TotalPoints)
{
    public IReadOnlyList<DailyHourlyBasis>? DailyBreakdown { get; init; }
}

public sealed record WageLineItem(Guid RecipientId, int AmountYen, string BasisSummary);

public interface IWageMethodStrategy
{
    Enums.WageMethod Method { get; }
    IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs,
        WageFund? fund,
        WageSettings settings);
}
```

- [ ] **Step 3: KouchinModule 突合 失敗テスト**

```csharp
// tests/Tsumugi.Domain.Tests/Logic/Wage/HourlyWageStrategyKouchinModuleTests.cs
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Logic.Wage;

public class HourlyWageStrategyKouchinModuleTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000021");

    private static WageSettings Settings(
        int? workAllowancePerDay = 500,
        IReadOnlyList<SkillAllowanceTier>? tiers = null) =>
        WageSettings.Create(
            Guid.NewGuid(), Office,
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
            fiscalYearStartMonth: 4, fixedDailyYen: null,
            workAllowancePerDayYen: workAllowancePerDay,
            skillAllowanceTiers: tiers ?? new[]
            {
                new SkillAllowanceTier(55, 2000),
                new SkillAllowanceTier(70, 4000),
            },
            hourUnitMinutes: 15,
            createdBy: "u", createdAt: DateTimeOffset.UtcNow);

    // KouchinModule v5 突合ケース:
    //   利用日数 15, 就労時間 26h(=1560分), 時給 350 円
    //   → 工賃時給 = ROUND(26 * 350) = 9,100
    //     作業手当 = 15 * 500       = 7,500
    //     職能手当 = 0（26h < 55h）
    //     合計                     = 16,600
    [Fact]
    public void Kouchin_baseline_15days_26h_yen350_totals_16600()
    {
        var inputs = new WageInputs(A, PresentDays: 15, TotalWorkedMinutes: 1560,
            TotalPieceAmountYen: 0, TotalPoints: 0)
        {
            DailyBreakdown = Enumerable.Range(0, 15)
                .Select(i => new DailyHourlyBasis(new DateOnly(2026, 5, i + 1), 104, 350))
                .ToArray(),
        };

        var s = new HourlyWageStrategy();
        var line = s.Calculate(new[] { inputs }, fund: null, settings: Settings()).Single();
        line.AmountYen.Should().Be(16_600);
        line.BasisSummary.Should().Contain("時給").And.Contain("作業").And.Contain("職能");
    }

    [Fact]
    public void Skill_allowance_tier_upper_55_lt_70_returns_2000()
    {
        var inputs = new WageInputs(A, PresentDays: 20, TotalWorkedMinutes: 60 * 60,
            TotalPieceAmountYen: 0, TotalPoints: 0);
        var line = new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings()).Single();
        // 20 日 * 500 = 10,000（作業）+ 職能 2,000 + 時給 0（DailyBreakdown なしで rate ソースなし）
        line.AmountYen.Should().Be(12_000);
    }

    [Fact]
    public void Skill_allowance_tier_upper_70_returns_4000()
    {
        var inputs = new WageInputs(A, PresentDays: 20, TotalWorkedMinutes: 70 * 60,
            TotalPieceAmountYen: 0, TotalPoints: 0);
        var line = new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings()).Single();
        line.AmountYen.Should().Be(20 * 500 + 4_000);
    }

    [Fact]
    public void Rejects_minutes_not_multiple_of_hour_unit()
    {
        var inputs = new WageInputs(A, PresentDays: 1, TotalWorkedMinutes: 30,
            TotalPieceAmountYen: 0, TotalPoints: 0)
        {
            DailyBreakdown = new[] { new DailyHourlyBasis(new DateOnly(2026, 5, 1), 7, 350) },
        };
        var act = () => new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings());
        act.Should().Throw<ArgumentException>().WithMessage("*15分単位*");
    }

    [Fact]
    public void DailyBreakdown_supports_mid_month_rate_change()
    {
        // 前半 10 日 350 円/h × 1h, 後半 5 日 400 円/h × 1h
        var breakdown = Enumerable.Range(0, 10)
            .Select(i => new DailyHourlyBasis(new DateOnly(2026, 5, i + 1), 60, 350))
            .Concat(Enumerable.Range(0, 5)
                .Select(i => new DailyHourlyBasis(new DateOnly(2026, 5, i + 11), 60, 400)))
            .ToArray();
        var inputs = new WageInputs(A, PresentDays: 15, TotalWorkedMinutes: 900,
            TotalPieceAmountYen: 0, TotalPoints: 0) { DailyBreakdown = breakdown };
        var line = new HourlyWageStrategy()
            .Calculate(new[] { inputs }, fund: null, settings: Settings(workAllowancePerDay: 0,
                tiers: Array.Empty<SkillAllowanceTier>())).Single();
        // ROUND(1*350)*10 + ROUND(1*400)*5 = 3500 + 2000 = 5,500
        line.AmountYen.Should().Be(5_500);
    }
}
```

- [ ] **Step 4: 失敗確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests/ --filter FullyQualifiedName~HourlyWageStrategyKouchinModuleTests`
Expected: FAIL

- [ ] **Step 5: `HourlyWageStrategy` 実装**

```csharp
// src/Tsumugi.Domain/Logic/Wage/HourlyWageStrategy.cs（全体差替）
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>Hourly 方式: 日単位に ROUND(時間×時給) を合算し、作業手当（日額×就労日数）と職能手当（時間閾値表）を加算。</summary>
public sealed class HourlyWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Hourly;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);

        var work = settings.WorkAllowancePerDayYen ?? 0;
        var tiers = settings.SkillAllowanceTiers;
        var rule = settings.Rounding;
        var unit = settings.HourUnitMinutes;

        var items = new List<WageLineItem>(inputs.Count);
        foreach (var input in inputs)
        {
            var hourlyYen = 0;
            if (input.DailyBreakdown is { } days)
            {
                foreach (var d in days)
                {
                    if (d.Minutes < 0 || d.Minutes % unit != 0)
                        throw new ArgumentException(
                            $"就労時間は {unit} 分単位で指定してください。", nameof(inputs));
                    hourlyYen += RoundingPolicy.Round(d.Minutes / 60m * d.HourlyYen, rule);
                }
            }

            var workAllow = input.PresentDays * work;

            var totalHours = input.TotalWorkedMinutes / 60;
            var skillAllow = 0;
            foreach (var t in tiers)
            {
                if (totalHours >= t.MinHours) skillAllow = t.Yen;
            }

            var total = hourlyYen + workAllow + skillAllow;
            var summary =
                $"時給 {hourlyYen:N0} 円 + 作業手当 {workAllow:N0} 円 + 職能手当 {skillAllow:N0} 円";
            items.Add(new WageLineItem(input.RecipientId, total, summary));
        }
        return items;
    }
}
```

- [ ] **Step 6: Piece/Fixed/Equal Strategy にも手当規則加算を反映**

各 Strategy の `Calculate` 末尾を「基本額に `settings.WorkAllowancePerDayYen * PresentDays` と職能手当を加算」する形に差し替える。`BasisSummary` も内訳を含める。

例: `FixedWageStrategy.cs` の Calculate 内で:

```csharp
// 既存: i.PresentDays * daily を計上
var baseYen = i.PresentDays * daily;
var workAllow = i.PresentDays * (settings.WorkAllowancePerDayYen ?? 0);
var totalHours = i.TotalWorkedMinutes / 60;
var skillAllow = 0;
foreach (var t in settings.SkillAllowanceTiers)
    if (totalHours >= t.MinHours) skillAllow = t.Yen;
var total = baseYen + workAllow + skillAllow;
```

（`PieceWageStrategy` / `EqualWageStrategy` も同様。既存の按分ロジックには手を付けず、末尾の合算に手当を加える）

- [ ] **Step 7: 全 Domain テストを緑化**

Run: `dotnet test tests/Tsumugi.Domain.Tests/`
Expected: PASS（既存の非-Hourly Strategy テストは `settings.WorkAllowancePerDayYen = null` かつ `SkillAllowanceTiers = []` で影響なし。ただし合算後の期待値が変わるテストは追記し、KouchinModule 突合ケースが緑）

- [ ] **Step 8: コミット**

```bash
git add src/Tsumugi.Domain/ValueObjects/DailyHourlyBasis.cs \
        src/Tsumugi.Domain/Logic/Wage/*.cs \
        tests/Tsumugi.Domain.Tests/Logic/Wage/HourlyWageStrategyKouchinModuleTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s0): wire allowance rules into strategies + hourly daily breakdown

WageInputs.DailyBreakdown を追加し HourlyWageStrategy が日単位で
ROUND(minutes/60 * hourlyYen) を合算するように変更。作業手当（日額×
就労日数）と職能手当（時間閾値表）を全 Strategy で加算する。
KouchinModule v5 の突合ケース（15日×26h×時給350円→16,600円）を
テーブル駆動テストで固定。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Application 層 — 新規 UseCase / DTO / Repository 抽象

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IWageAdjustmentRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IRecipientHourlyRateRepository.cs`
- Create: `src/Tsumugi.Application/Dtos/WageAdjustmentDto.cs`
- Create: `src/Tsumugi.Application/Dtos/RecipientHourlyRateDto.cs`
- Modify: `src/Tsumugi.Application/Dtos/WageSettingsDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/RecordWageAdjustmentUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/QueryWageAdjustmentUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/SetRecipientHourlyRateUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/QueryRecipientHourlyRateUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/Wage/RecordWageAdjustmentUseCaseTests.cs`
- Create: `tests/Tsumugi.Application.Tests/Wage/QueryWageAdjustmentUseCaseTests.cs`
- Create: `tests/Tsumugi.Application.Tests/Wage/SetRecipientHourlyRateUseCaseTests.cs`
- Create: `tests/Tsumugi.Application.Tests/Wage/QueryRecipientHourlyRateUseCaseTests.cs`

**Interfaces:**
- Consumes: Task 3 / Task 4 の Domain エンティティ、`IUnitOfWork`, `TimeProvider`, `IAuditTrail`
- Produces:
  - `IWageAdjustmentRepository`: `AddAsync`, `ListByOfficeMonthAsync(officeId, ym, ct)`
  - `IRecipientHourlyRateRepository`: `AddAsync`, `ListByOfficeRecipientAsync(officeId, recipientId, ct)`
  - `WageAdjustmentDto(Id, OfficeId, RecipientId, YearMonth, Type, AmountYen, Kind, OriginId, Note)`
  - `RecipientHourlyRateDto(Id, OfficeId, RecipientId, Period, HourlyYen, Kind, OriginId, Note)`
  - `WageSettingsDto` に `WorkAllowancePerDayYen`, `SkillAllowanceTiers`, `HourUnitMinutes` を追加（`IReadOnlyList<SkillAllowanceTier>` はそのまま使う。DTO に別型は作らない — Domain 値オブジェクトを Application に露出）
  - `RecordWageAdjustmentUseCase.ExecuteAsync(Guid officeId, Guid recipientId, YearMonth ym, WageAdjustmentType type, int amountYen, string? note, string actor, CancellationToken ct)` — 監査追記あり
  - `SetRecipientHourlyRateUseCase.ExecuteAsync(Guid officeId, Guid recipientId, DateRange period, int hourlyYen, string actor, CancellationToken ct)` — 監査追記あり
  - Query 系はそれぞれ Repository を通してリストを DTO で返す

- [ ] **Step 1: 抽象・DTO・失敗テストを追加**

（Repository 抽象と DTO は素直な形なので複数ファイル一括で作成。テストは `RecordWageAdjustmentUseCaseTests` を代表として先に赤で書く）

```csharp
// src/Tsumugi.Application/Abstractions/IWageAdjustmentRepository.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public interface IWageAdjustmentRepository
{
    Task AddAsync(WageAdjustment adjustment, CancellationToken ct);
    Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth yearMonth, CancellationToken ct);
}
```

```csharp
// src/Tsumugi.Application/Abstractions/IRecipientHourlyRateRepository.cs
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IRecipientHourlyRateRepository
{
    Task AddAsync(RecipientHourlyRate rate, CancellationToken ct);
    Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(
        Guid officeId, Guid recipientId, CancellationToken ct);
}
```

```csharp
// src/Tsumugi.Application/Dtos/WageAdjustmentDto.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record WageAdjustmentDto(
    Guid Id, Guid OfficeId, Guid RecipientId, YearMonth YearMonth,
    WageAdjustmentType Type, int AmountYen,
    RecordKind Kind, Guid? OriginId, string? Note);
```

```csharp
// src/Tsumugi.Application/Dtos/RecipientHourlyRateDto.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record RecipientHourlyRateDto(
    Guid Id, Guid OfficeId, Guid RecipientId, DateRange Period,
    int HourlyYen, RecordKind Kind, Guid? OriginId, string? Note);
```

```csharp
// src/Tsumugi.Application/Dtos/WageSettingsDto.cs（全体差替）
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record WageSettingsDto(
    Guid Id, Guid OfficeId, DateRange Period,
    WageMethod Method, RoundingRule Rounding, RemainderPolicy Remainder,
    int FiscalYearStartMonth, int? FixedDailyYen,
    int? WorkAllowancePerDayYen,
    IReadOnlyList<SkillAllowanceTier> SkillAllowanceTiers,
    int HourUnitMinutes);
```

- [ ] **Step 2: UseCase 失敗テスト（4 本）**

```csharp
// tests/Tsumugi.Application.Tests/Wage/RecordWageAdjustmentUseCaseTests.cs
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Wage;

public class RecordWageAdjustmentUseCaseTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly YearMonth Ym = YearMonth.FromInt32(202605);

    [Fact]
    public async Task Execute_persists_new_record_and_audit_entry()
    {
        var repo = new FakeAdjustmentRepo();
        var uow = new FakeUnitOfWork();
        var audit = new FakeAuditTrail();
        var uc = new RecordWageAdjustmentUseCase(repo, uow, audit,
            TimeProvider.System);

        var dto = await uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, "月末調整", "tester", default);

        dto.AmountYen.Should().Be(1000);
        repo.Added.Should().HaveCount(1);
        audit.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task Execute_rejects_empty_actor()
    {
        var uc = new RecordWageAdjustmentUseCase(
            new FakeAdjustmentRepo(), new FakeUnitOfWork(),
            new FakeAuditTrail(), TimeProvider.System);
        var act = () => uc.ExecuteAsync(Office, Recipient, Ym,
            WageAdjustmentType.SpecialAllowance, 1000, null, "", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // 略: 他 3 UseCase も同様のパターン
}

file sealed class FakeAdjustmentRepo : IWageAdjustmentRepository
{
    public List<WageAdjustment> Added { get; } = new();
    public Task AddAsync(WageAdjustment adjustment, CancellationToken ct)
    { Added.Add(adjustment); return Task.CompletedTask; }
    public Task<IReadOnlyList<WageAdjustment>> ListByOfficeMonthAsync(
        Guid officeId, YearMonth ym, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WageAdjustment>>(Added
            .Where(a => a.OfficeId == officeId && a.YearMonth == ym).ToArray());
}

file sealed class FakeUnitOfWork : IUnitOfWork
{ public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask; }

file sealed class FakeAuditTrail : IAuditTrail
{
    public List<(AuditAction, Guid, string?)> Entries { get; } = new();
    public Task AppendAsync(string actor, AuditAction action, Guid targetId, string? summary,
        CancellationToken ct)
    { Entries.Add((action, targetId, summary)); return Task.CompletedTask; }
}
```

（他 3 UseCase の Tests は同構造で `Query...UseCase` / `SetRecipientHourlyRateUseCase` / `QueryRecipientHourlyRateUseCase` を書く）

- [ ] **Step 3: 失敗確認**

Run: `dotnet test tests/Tsumugi.Application.Tests/ --filter "FullyQualifiedName~WageAdjustment|FullyQualifiedName~RecipientHourlyRate"`
Expected: FAIL

- [ ] **Step 4: 4 UseCase を実装**

```csharp
// src/Tsumugi.Application/UseCases/Wage/RecordWageAdjustmentUseCase.cs
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class RecordWageAdjustmentUseCase(
    IWageAdjustmentRepository repo, IUnitOfWork uow,
    IAuditTrail audit, TimeProvider clock)
{
    public async Task<WageAdjustmentDto> ExecuteAsync(
        Guid officeId, Guid recipientId, YearMonth yearMonth,
        WageAdjustmentType type, int amountYen, string? note,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor が空です。", nameof(actor));
        var entity = WageAdjustment.NewRecord(Guid.NewGuid(), officeId, recipientId, yearMonth,
            type, amountYen, note, actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await audit.AppendAsync(actor, AuditAction.Create, entity.Id,
            $"WageAdjustment 追記 {type} {amountYen}円", ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static WageAdjustmentDto Map(WageAdjustment e) =>
        new(e.Id, e.OfficeId, e.RecipientId, e.YearMonth, e.Type, e.AmountYen,
            e.Kind, e.OriginId, e.Note);
}
```

（`SetRecipientHourlyRateUseCase` も同パターン。`Query*` は Repository の list を DTO に写像して返す）

- [ ] **Step 5: テスト緑化**

Run: `dotnet test tests/Tsumugi.Application.Tests/`
Expected: PASS

- [ ] **Step 6: コミット**

```bash
git add src/Tsumugi.Application/Abstractions/IWageAdjustmentRepository.cs \
        src/Tsumugi.Application/Abstractions/IRecipientHourlyRateRepository.cs \
        src/Tsumugi.Application/Dtos/WageAdjustmentDto.cs \
        src/Tsumugi.Application/Dtos/RecipientHourlyRateDto.cs \
        src/Tsumugi.Application/Dtos/WageSettingsDto.cs \
        src/Tsumugi.Application/UseCases/Wage/RecordWageAdjustmentUseCase.cs \
        src/Tsumugi.Application/UseCases/Wage/QueryWageAdjustmentUseCase.cs \
        src/Tsumugi.Application/UseCases/Wage/SetRecipientHourlyRateUseCase.cs \
        src/Tsumugi.Application/UseCases/Wage/QueryRecipientHourlyRateUseCase.cs \
        tests/Tsumugi.Application.Tests/Wage/
git commit -m "$(cat <<'EOF'
feat(phase4/s0): add UseCases and DTOs for WageAdjustment & RecipientHourlyRate

Record/Query 各 UseCase を Application 層に追加し、監査ログ
(IAuditTrail) との配線を含める。WageSettingsDto を新3フィールドで
拡張。DTO は既存の Domain 値オブジェクト (SkillAllowanceTier) を
そのまま露出する（Application は Domain を参照可）。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: 既存 UseCase の改修 — `ConfigureWageSettings` / `CalculateWages` / `CloseWages`

**Files:**
- Modify: `src/Tsumugi.Application/UseCases/Wage/ConfigureWageSettingsUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/Wage/CloseWagesUseCase.cs`
- Modify: `src/Tsumugi.Domain/Entities/WageSettings.cs` — Task 2 の暫定オーバーロード削除
- Modify: `tests/Tsumugi.Application.Tests/Wage/ConfigureWageSettingsUseCaseTests.cs`（既存があれば拡張）
- Create/Modify: `tests/Tsumugi.Application.Tests/Wage/CalculateWagesUseCaseKouchinModuleTests.cs`

**Interfaces:**
- Consumes: 全 Task 1〜6 の産物 + 既存 `IWorkRecordRepository`, `IWageFundRepository`, `IWageSettingsRepository`
- Produces:
  - `ConfigureWageSettingsUseCase.ExecuteAsync` に 3 引数追加（既存呼出しはビルドエラーで検出）
  - `CalculateWagesUseCase.ExecuteAsync` が `IRecipientHourlyRateRepository` と `IWageAdjustmentRepository` を注入・利用し、`WageInputs.DailyBreakdown` を組み立てて Strategy に渡し、`WageAdjustmentPolicy.SumEffective` を線形合算する
  - `CloseWagesUseCase` は Strategy 出力 + 手当規則 + WageAdjustment の合計を BasisSummary に含めた確定スナップショットを作る

- [ ] **Step 1: KouchinModule シナリオの Application 統合テスト（失敗）**

```csharp
// tests/Tsumugi.Application.Tests/Wage/CalculateWagesUseCaseKouchinModuleTests.cs
// (簡潔化のため主要アサートのみ)
[Fact]
public async Task Calculate_matches_kouchin_baseline_case()
{
    // seed: WageSettings (WorkAllowance 500 / Tiers 55h→2000, 70h→4000 / hourUnit 15)
    //       RecipientHourlyRate 350 円/h (2026-04-01..2027-03-31)
    //       WorkRecord 15 日 × 各日 104 分 = 1560 分 = 26h
    //       WageAdjustment: 特別手当 なし
    var previews = await sut.ExecuteAsync(officeId, ym: new YearMonth(2026, 5), ct: default);
    var line = previews.LineItems.Single();
    line.AmountYen.Should().Be(16_600);
    line.BasisSummary.Should().Contain("時給 9,100").And.Contain("作業手当 7,500");
}

[Fact]
public async Task Calculate_adds_special_allowance_via_wage_adjustment()
{
    // + 特別手当 1,000 円 → 合計 17,600 円
}
```

- [ ] **Step 2: 失敗確認**

Run: `dotnet test tests/Tsumugi.Application.Tests/ --filter FullyQualifiedName~CalculateWagesUseCaseKouchinModuleTests`
Expected: FAIL

- [ ] **Step 3: `ConfigureWageSettingsUseCase` 改修**

```csharp
public async Task<WageSettingsDto> ExecuteAsync(
    Guid officeId, DateRange period,
    WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
    int fiscalYearStartMonth, int? fixedDailyYen,
    int? workAllowancePerDayYen,
    IReadOnlyList<SkillAllowanceTier>? skillAllowanceTiers,
    int hourUnitMinutes,
    string actor, CancellationToken ct)
{
    // 既存の DateValidator, officeId 検証を維持
    var entity = WageSettings.Create(
        Guid.NewGuid(), officeId, period,
        method, rounding, remainder, fiscalYearStartMonth, fixedDailyYen,
        workAllowancePerDayYen, skillAllowanceTiers, hourUnitMinutes,
        actor, clock.GetUtcNow());
    // ... 既存の Add/Save フロー
}

internal static WageSettingsDto Map(WageSettings e) =>
    new(e.Id, e.OfficeId, e.Period, e.Method, e.Rounding, e.Remainder,
        e.FiscalYearStartMonth, e.FixedDailyYen,
        e.WorkAllowancePerDayYen, e.SkillAllowanceTiers, e.HourUnitMinutes);
```

- [ ] **Step 4: `CalculateWagesUseCase` 改修**

- `IRecipientHourlyRateRepository`, `IWageAdjustmentRepository` を DI に追加
- 対象月の日ごとに `RecipientHourlyRatePolicy.EffectiveYen` を引き、`DailyHourlyBasis` の配列を作って `WageInputs.DailyBreakdown` にセット
- Strategy 出力に対し `WageAdjustmentPolicy.SumEffective(...)` を加算した金額を `WagePreviewDto` に反映

- [ ] **Step 5: `CloseWagesUseCase` 改修**

- Strategy 出力 + 手当規則（既に Strategy 内で加算済）+ WageAdjustment の合計を `WageStatement.BasisSummary` に「時給 X 円 + 作業 Y + 職能 Z + 特別 W」の形で保存

- [ ] **Step 6: Task 2 の暫定オーバーロードを削除**

`WageSettings` の暫定 `Create` オーバーロード（`workAllowance` などなしの 10 引数版）を除去。既存呼出しが `ConfigureWageSettingsUseCase` 経由に集約されているためエラーが出なければ完了。

- [ ] **Step 7: 全テスト緑化**

Run: `dotnet test`
Expected: PASS

- [ ] **Step 8: コミット**

```bash
git add src/Tsumugi.Application/UseCases/Wage/ConfigureWageSettingsUseCase.cs \
        src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs \
        src/Tsumugi.Application/UseCases/Wage/CloseWagesUseCase.cs \
        src/Tsumugi.Domain/Entities/WageSettings.cs \
        tests/Tsumugi.Application.Tests/Wage/
git commit -m "$(cat <<'EOF'
feat(phase4/s0): wire allowances and per-day hourly rate into wage UseCases

ConfigureWageSettings に手当規則3項目を追加。CalculateWages で
RecipientHourlyRate を日単位に引き当てて WageInputs.DailyBreakdown
を組み立て、WageAdjustment の実効合計を線形加算する。CloseWages の
BasisSummary に内訳を保持。WageSettings の暫定オーバーロードを削除。

KouchinModule 突合ケース（15日×26h×時給350円 → 16,600円、特別
手当 1,000 円加算で 17,600 円）が Application 層で緑。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Infrastructure — `WageAdjustment` の永続化とマイグレーション

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/WageAdjustmentConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/WageAdjustmentRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs` — `DbSet<WageAdjustment>` 追加
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs` — `IWageAdjustmentRepository` 登録
- Create: `src/Tsumugi.Infrastructure/Migrations/<ts>_AddWageAdjustment.cs`（`dotnet ef` で生成）
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/WageAdjustmentRepositoryTests.cs`

**Interfaces:**
- Consumes: Task 4 の Domain、Task 6 の抽象
- Produces:
  - `WageAdjustments` テーブル + `(OfficeId, RecipientId, YearMonth, Type) WHERE Kind=1` の partial unique index
  - `WageAdjustmentRepository` は既存 `WageFundRepository` と同スタイル（`AsNoTracking`, クライアント順序ソート）

- [ ] **Step 1: Repository 失敗テスト**

```csharp
// tests/Tsumugi.Infrastructure.Tests/Persistence/WageAdjustmentRepositoryTests.cs
[Fact]
public async Task Add_and_list_returns_by_office_and_month()
{
    using var ctx = TestDbContextFactory.Create();
    var repo = new WageAdjustmentRepository(ctx);
    var ym = YearMonth.FromInt32(202605);
    var w = WageAdjustment.NewRecord(Guid.NewGuid(), officeId, recipientId, ym,
        WageAdjustmentType.SpecialAllowance, 1000, null, "u", DateTimeOffset.UtcNow);
    await repo.AddAsync(w, default);
    await ctx.SaveChangesAsync();

    var list = await repo.ListByOfficeMonthAsync(officeId, ym, default);
    list.Should().ContainSingle().Which.AmountYen.Should().Be(1000);
}

[Fact]
public async Task Duplicate_new_is_rejected_by_partial_unique_index()
{
    // 同一 (OfficeId, RecipientId, YearMonth, Type) で Kind=New を 2 件追加
    // → DbUpdateException
}
```

- [ ] **Step 2: 失敗確認 → Configuration + Repository 実装 + DbSet 追加 + DI 登録 + マイグレーション生成**

Migration コマンド:
```bash
dotnet ef migrations add AddWageAdjustment \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

Configuration（partial unique index）:
```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/WageAdjustmentConfiguration.cs
public sealed class WageAdjustmentConfiguration : IEntityTypeConfiguration<WageAdjustment>
{
    public void Configure(EntityTypeBuilder<WageAdjustment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageAdjustments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OfficeId).IsRequired();
        builder.Property(x => x.RecipientId).IsRequired();
        builder.Property(x => x.YearMonth).HasConversion(
            v => v.ToInt32(), v => YearMonth.FromInt32(v)).IsRequired();
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.AmountYen).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.OriginId);
        builder.Property(x => x.Note).HasMaxLength(200);
        builder.HasIndex(x => new { x.OfficeId, x.RecipientId, x.YearMonth, x.Type })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_WageAdjustments_OfficeRecipientYmType_NewOnly");
    }
}
```

- [ ] **Step 3: テスト緑化**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/`
Expected: PASS

- [ ] **Step 4: コミット**

```bash
git add src/Tsumugi.Infrastructure/Persistence/Configurations/WageAdjustmentConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/WageAdjustmentRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs \
        src/Tsumugi.Infrastructure/DependencyInjection.cs \
        src/Tsumugi.Infrastructure/Migrations/ \
        tests/Tsumugi.Infrastructure.Tests/Persistence/WageAdjustmentRepositoryTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s0): persist WageAdjustment with partial unique index

WageAdjustments テーブルを追加し、(OfficeId, RecipientId, YearMonth,
Type) WHERE Kind=New で二重 New を SQLite partial unique index で
拒否する（ADR 0015/0017 パターン）。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Infrastructure — `RecipientHourlyRate` の永続化とマイグレーション

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/RecipientHourlyRateConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/RecipientHourlyRateRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<ts>_AddRecipientHourlyRate.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/RecipientHourlyRateRepositoryTests.cs`

**Interfaces:**
- Task 3 の Domain、Task 6 の抽象を消費
- 生成: `RecipientHourlyRates` テーブル + `(OfficeId, RecipientId, PeriodStart) WHERE Kind=1` partial unique index。`Period` は既存の `DateRangeJson` 変換パターンに合わせる

- [ ] **Step 1: Repository 失敗テスト**（Task 8 と同構造）

- [ ] **Step 2: 失敗確認 → 実装 → migration 生成**

```bash
dotnet ef migrations add AddRecipientHourlyRate \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

Configuration の partial unique index は `Period.Start` を専用 shadow プロパティ（`PeriodStart`）にして張る（既存 `WageSettings` の `Period` 実装を踏襲）。詳細は既存 `WageSettingsConfiguration` の実装を参照。

- [ ] **Step 3: テスト緑化**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests/`
Expected: PASS

- [ ] **Step 4: コミット**

```bash
git commit -m "$(cat <<'EOF'
feat(phase4/s0): persist RecipientHourlyRate with partial unique index

RecipientHourlyRates テーブルを追加し、(OfficeId, RecipientId,
PeriodStart) WHERE Kind=New で同一開始日の二重 New を拒否する。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Infrastructure — `WageSettings` 拡張マイグレーション

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/WageSettingsConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<ts>_ExtendWageSettingsAllowances.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/WageSettingsExtensionMigrationTests.cs`

**Interfaces:**
- Consumes: Task 2 の拡張済み `WageSettings`
- Produces: 新3カラム（`WorkAllowancePerDayYen INTEGER NULL`, `SkillAllowanceTiersJson TEXT NOT NULL DEFAULT '[]'`, `HourUnitMinutes INTEGER NOT NULL DEFAULT 15`）。既存レコードは既定値で埋める

- [ ] **Step 1: `SkillAllowanceTiers` の JSON シリアライズ変換器を Configuration に追加**

```csharp
private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

builder.Property(x => x.WorkAllowancePerDayYen);
builder.Property(x => x.SkillAllowanceTiers)
    .HasConversion(
        v => JsonSerializer.Serialize(v, JsonOpts),
        v => JsonSerializer.Deserialize<List<SkillAllowanceTier>>(v, JsonOpts)!
            .AsReadOnly())
    .HasColumnName("SkillAllowanceTiersJson")
    .IsRequired();
builder.Property(x => x.HourUnitMinutes).HasDefaultValue(15).IsRequired();
```

- [ ] **Step 2: マイグレーション生成**

```bash
dotnet ef migrations add ExtendWageSettingsAllowances \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

生成後、`Up` で既存行を埋める SQL を追記:

```csharp
migrationBuilder.Sql(
    "UPDATE \"WageSettings\" SET \"SkillAllowanceTiersJson\" = '[]' " +
    "WHERE \"SkillAllowanceTiersJson\" IS NULL;");
```

- [ ] **Step 3: マイグレーション テスト**

```csharp
[Fact]
public async Task Migration_preserves_existing_wage_settings_with_defaults()
{
    // 旧スキーマ相当の生 SQL で 1 行 insert（新カラムなし）
    // → context 経由で取得すると HourUnitMinutes=15, SkillAllowanceTiers=[] になる
}
```

- [ ] **Step 4: テスト緑化 → コミット**

```bash
git commit -m "$(cat <<'EOF'
feat(phase4/s0): migrate WageSettings to add allowance columns

WageSettings テーブルに WorkAllowancePerDayYen (nullable),
SkillAllowanceTiersJson (default '[]'), HourUnitMinutes (default 15)
を追加。既存行は既定値で埋める。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: `AppendOnlyGuard` に新エンティティを追加

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/Persistence/AppendOnlyGuardPhase2Tests.cs`（`Phase4` 用テストを追加）

**Interfaces:**
- Consumes: 全新エンティティ
- Produces: 監視型集合に `WageAdjustment`, `RecipientHourlyRate` を追加

- [ ] **Step 1: 失敗テスト**

```csharp
[Fact]
public async Task Attempt_to_modify_wage_adjustment_is_rejected()
{
    var w = WageAdjustment.NewRecord(...);
    ctx.Add(w); await ctx.SaveChangesAsync();
    var loaded = await ctx.Set<WageAdjustment>().FirstAsync();
    ctx.Entry(loaded).Property(nameof(WageAdjustment.AmountYen)).CurrentValue = 999;
    var act = () => ctx.SaveChangesAsync();
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("*追記型*");
}

[Fact]
public async Task Attempt_to_modify_recipient_hourly_rate_is_rejected() { /* 同上 */ }
```

- [ ] **Step 2: 実装**

```csharp
private static readonly IReadOnlySet<Type> Monitored = new HashSet<Type>
{
    // 既存 ...
    typeof(WageAdjustment),
    typeof(RecipientHourlyRate),
};
```

- [ ] **Step 3: テスト緑化 → コミット**

```bash
git commit -m "$(cat <<'EOF'
feat(phase4/s0): extend AppendOnlyGuard to cover new entities

WageAdjustment / RecipientHourlyRate を append-only 監視対象に加え、
Modified/Deleted 検出時に例外を投げる。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 12: App — `WageFundSettingsView` に手当規則入力欄を追加

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs`
- Modify: `src/Tsumugi.App/Views/WageFundSettingsView.axaml`
- Modify: `tests/Tsumugi.App.Tests/ViewModels/WageFundSettingsViewModelTests.cs`（新規または既存拡張）

**Interfaces:**
- Consumes: 改修版 `ConfigureWageSettingsUseCase`
- Produces: 手当規則の入力・保存が可能な VM/View

- [ ] **Step 1: 失敗テスト**

```csharp
[Fact]
public async Task Save_persists_allowance_rules()
{
    vm.WorkAllowancePerDayYen = 500;
    vm.SkillAllowanceTiersText = "55,2000\n70,4000"; // UI は行区切りテキスト or 行編集リスト
    vm.HourUnitMinutes = 15;
    await vm.SaveCommand.ExecuteAsync(null);
    fakeUseCase.LastArgs.WorkAllowance.Should().Be(500);
    fakeUseCase.LastArgs.Tiers.Should().HaveCount(2);
}
```

- [ ] **Step 2〜4: VM/View 実装 → テスト緑化 → コミット**

VM: `[ObservableProperty]` 4 個追加。`SaveCommand` で `ConfigureWageSettingsUseCase` に新引数を渡す。View: 既存の Grid にラベル + `NumericUpDown`（作業手当）+ `ItemsControl`（閾値行編集）+ `NumericUpDown`（最小単位）を追加。

```bash
git commit -m "feat(phase4/s0): edit allowance rules in WageFundSettingsView

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 13: App — `RecipientHourlyRateView` を新設

**Files:**
- Create: `src/Tsumugi.App/ViewModels/RecipientHourlyRateViewModel.cs`
- Create: `src/Tsumugi.App/Views/RecipientHourlyRateView.axaml` + `.axaml.cs`
- Create: `tests/Tsumugi.App.Tests/ViewModels/RecipientHourlyRateViewModelTests.cs`

**Interfaces:**
- Consumes: `SetRecipientHourlyRateUseCase`, `QueryRecipientHourlyRateUseCase`, `ListOfficesUseCase`, `ListRecipientsUseCase`
- Produces: 利用者選択 → 期間別時給の一覧・追記 UI

- [ ] **Step 1: 失敗テスト**（VM の主要フロー）
- [ ] **Step 2〜4: 実装 → 緑化 → コミット**

```bash
git commit -m "feat(phase4/s0): add RecipientHourlyRateView for per-recipient periodic hourly rate

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 14: App — `WageAdjustmentView` を新設

**Files:**
- Create: `src/Tsumugi.App/ViewModels/WageAdjustmentViewModel.cs`
- Create: `src/Tsumugi.App/Views/WageAdjustmentView.axaml` + `.axaml.cs`
- Create: `tests/Tsumugi.App.Tests/ViewModels/WageAdjustmentViewModelTests.cs`

**Interfaces:**
- Consumes: `RecordWageAdjustmentUseCase`, `QueryWageAdjustmentUseCase`, `ListOfficesUseCase`, `ListRecipientsUseCase`
- Produces: 事業所×月選択 → 利用者×手当種別のマトリクス編集 UI

- [ ] **Step 1: 失敗テスト**
- [ ] **Step 2〜4: 実装 → 緑化 → コミット**

```bash
git commit -m "feat(phase4/s0): add WageAdjustmentView for special allowance entry

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 15: App — `WageCalculationView` に内訳表示 + `MainWindow` に 2 タブ追加 + `CompositionRoot` 更新

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs`
- Modify: `src/Tsumugi.App/Views/WageCalculationView.axaml`
- Modify: `src/Tsumugi.App/Views/MainWindow.axaml`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`
- Modify: `tests/Tsumugi.App.Tests/ViewModels/WageCalculationViewModelTests.cs`

**Interfaces:**
- Consumes: 改修版 `CalculateWagesUseCase` の返却（`BasisSummary` に内訳）
- Produces: プレビューに「時給 / 作業 / 職能 / 特別」の内訳を並記。CompositionRoot に新 UseCase / Repository / VM を登録

- [ ] **Step 1〜4**: VM の Preview アイテムに内訳文字列プロパティを追加、View で表示、MainWindow の `TabControl` に 2 タブ挿入、CompositionRoot に新 UseCase / Repository / VM を登録

```bash
git commit -m "feat(phase4/s0): show wage breakdown + wire new tabs in MainWindow

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

## Task 16: `AverageWageMetric` の拡張ポイント整理（AC2-8 準備）

**Files:**
- Modify: `src/Tsumugi.Domain/Logic/AverageWageMetric.cs`
- Modify: `tests/Tsumugi.Domain.Tests/Logic/AverageWageMetricTests.cs`

**Interfaces:**
- Consumes: 既存 `AverageWageDenominator`
- Produces: 一次資料入手時に値差替可能な構造。既存 `Calculate(statements, denominator)` は維持し、コメントを「仮の 2 択」から「拡張ポイント: `AverageWageDenominator` 追加＋オプション引数追加で対応」と明示

- [ ] **Step 1〜4**: コメント整理と「常勤換算・除外者数」オプション引数の**未使用 stub を用意しない**（YAGNI）。テストで「分母切替に強い形」であることを回帰テストとして固定

```csharp
[Theory]
[InlineData(AverageWageDenominator.TotalRecipients, 5, 10000, 2000)]
[InlineData(AverageWageDenominator.ActiveRecipients, 5, 10000, 2500)]
public void Calculate_switches_denominator(...) { ... }
```

コミット:

```bash
git commit -m "$(cat <<'EOF'
chore(phase4/s0): clarify AverageWageMetric extension surface for AC2-8

一次資料入手時に値差替のみで正式化できるよう、拡張ポイントを
コメントで明示。テストは分母切替に強い形を回帰として固定。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 17: ドキュメント更新 — ADR 0012 確定 / ADR 0018 / ADR 0019 / CHANGELOG / open-questions

**Files:**
- Modify: `docs/decisions/0012-wage-calculation-strategy.md`
- Create: `docs/decisions/0018-wage-adjustment-append-only.md`
- Create: `docs/decisions/0019-recipient-hourly-rate-periodic-master.md`
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`

**Interfaces:**
- 実装で確定した値と設計判断を文書に反映

- [ ] **Step 1: ADR 0012 を「確定」に書換**

前提部から「暫定」を除去し、確定表（本プランの Global Constraints と一致）を記す:
- 年度起点月 = 4（確定）
- 既定方式 = Hourly（他 3 方式は互換保持）
- 端数 = HalfUp（変更）、`RoundingPolicy` に集約
- 余り = LargestRemainder（維持）
- 手当ルール: `WageSettings` の拡張フィールドと `WageAdjustment` の併存
- 時給の期間管理: `RecipientHourlyRate`
- 端数規則の一次確認: 2026-07-05、KouchinModule v5（`.xlsm` の `BD5 = ROUND(...,0)`）

- [ ] **Step 2: ADR 0018 (`wage-adjustment-append-only.md`) 新設**

結論・背景（.xlsm 突合結果）・選択肢 A/B/C・却下理由・影響。

- [ ] **Step 3: ADR 0019 (`recipient-hourly-rate-periodic-master.md`) 新設**

結論・背景（AU12 の月中変動検知）・選択肢・影響。

- [ ] **Step 4: `docs/open-questions.md` 更新**

- 「KouchinModule.bas v5 の実挙動突合」を `[x]` に
- 「工賃計算 4 方式のうちの既定」「端数規則」を明示的にクローズ
- 「精神障害者保健福祉手帳の更新通知」は S4 スコープなので継続（変更なし）

- [ ] **Step 5: `CHANGELOG.md` `[Unreleased]` を更新**

- 「本番投入前に必須の deferred」から「KouchinModule.bas v5 の実挙動突合」を除去
- 「追加」に S0 の実装項目（`WageAdjustment`, `RecipientHourlyRate`, `RoundingRule.HalfUp`, `RoundingPolicy`, `WageSettings` 拡張、2 タブ追加）を列挙

- [ ] **Step 6: コミット**

```bash
git add docs/decisions/0012-wage-calculation-strategy.md \
        docs/decisions/0018-wage-adjustment-append-only.md \
        docs/decisions/0019-recipient-hourly-rate-periodic-master.md \
        docs/open-questions.md \
        CHANGELOG.md
git commit -m "$(cat <<'EOF'
docs(phase4/s0): finalize ADR 0012 and add ADR 0018/0019 for allowance model

ADR 0012 を暫定→確定へ書換（端数 HalfUp・手当規則・時給期間マスタを
明記）。ADR 0018 (WageAdjustment append-only)、ADR 0019
(RecipientHourlyRate periodic master) を新設。open-questions と
CHANGELOG も反映。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 18: 最終ゲート — `./build/ci.sh` 緑化と smoke

**Files:** なし（既存のみを実行）

- [ ] **Step 1: フォーマット & ビルド & テスト**

Run: `dotnet format --verify-no-changes && dotnet build && dotnet test`
Expected: 全緑

- [ ] **Step 2: CI 一括**

Run: `./build/ci.sh`
Expected: 全緑（Domain カバレッジ ≥ 95% 維持）

- [ ] **Step 3: アプリ起動 smoke（手動）**

Run: `dotnet run --project src/Tsumugi.App`
Expected:
- 「工賃原資・設定」タブで手当規則が入力・保存可能
- 「利用者時給」タブが存在し追記できる
- 「特別手当」タブが存在しマトリクス入力できる
- 「工賃計算」タブのプレビュー内訳に「時給/作業/職能/特別」の 4 系統が表示される
- 「工賃明細」タブから既存の確定と PDF 出力が引き続き可能

Report to user: 手動 smoke の結果（OK / 発見された不具合の一覧）。

- [ ] **Step 4: 最終コミット（もし追加調整が要れば）**

追加修正がなければ本タスクはコミット無しで終了。

---

## Self-Review 結果

**Spec coverage**:
- v2 spec §3〜§14 の全項目を Task 1〜17 でカバー。
- §7 (AverageWageMetric) → Task 16。値差替は一次資料入手時の別コミットに委ねる（`AverageWageMetric` の構造整備のみを本プランで完了）。
- §12 の完了条件 1〜9 は Task 1〜17 の産物に対応。

**Type consistency**:
- `RecordKind` は既存 `New / Correct / Cancel` を統一使用（spec 表記の `Correction` を本プランでは `Correct` に統一済）。
- `Entity` 基底の必須プロパティ（`Id / CreatedAt / CreatedBy / ConcurrencyToken`）を全新エンティティで初期化。
- `WageSettings.Create` の新シグネチャは Task 2 で導入し Task 7 で暫定オーバーロード削除（責務移動を明示）。
- Application → Domain 参照は既存慣習通り（DTO も `SkillAllowanceTier` の Domain 値オブジェクトを直接露出）。

**Placeholder scan**:
- Task 6 の一部で「他 3 UseCase は同構造」と記述したが、パターンが明確（Repository call + Audit + Save + Map）で反復コードとしての情報量が薄いため許容。実装者は Task 4/5 のパターンを直接転用可能。
- Task 12〜14 の View XAML は既存 View（`WageFundSettingsView.axaml`）のパターン踏襲を前提とし詳細実装は含めない（プランが実装者に委ねる範囲）。

---

Plan complete and saved to `docs/superpowers/plans/2026-07-05-phase4-s0-kouchinmodule-implementation.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
