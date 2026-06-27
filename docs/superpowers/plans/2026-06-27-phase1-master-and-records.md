# Tsumugi フェーズ1 実装計画 — マスタ＋記録

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 事業所・利用者・受給者証・契約・日次記録（実績＋出欠）を、データ性質に応じた**3つの永続化方針**（厳密追記／実効日付つき追記／同一性マスタ＋楽観ロック）で実装し、受給者証期限アラートと日次記録の訂正・取消履歴を純粋ロジックで固定する。

**Architecture:** 既存の4プロジェクト構成（`Tsumugi.Domain`/`.Application`/`.Infrastructure`/`.App`）を維持。Domainに6エンティティ（record）と純粋ロジック関数群を、Infrastructureに EF Core 設定＋マイグレーション＋追記不変ガード＋リポジトリ実装を、Applicationにユースケース＋日付検証を、Appに ViewModel ＋ Avalonia 画面を追加する。フェーズ0の `Office` 最小実装を拡張し、`OfficeCapability`/`Recipient`/`Certificate`/`Contract`/`DailyRecord` を新規追加する。

**Tech Stack:** .NET 10 / Avalonia 11.3 / EF Core 10 / SQLite / CommunityToolkit.Mvvm 8.4 / xUnit / FluentAssertions 7.x / `DateOnly`

## Global Constraints

> 親文書 `CLAUDE.md` §ハード制約、`01_ClaudeCode_実装指示書_Tsumugi.md` §6、`04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md` §6 を全タスクで尊守する。

- **依存方向**: `App → Application → Domain`、`Infrastructure → Application/Domain`。`Domain` は `Infrastructure`/`Avalonia`/`EntityFrameworkCore` を**直接参照しない**（`ArchitectureTests` が機械判定）。
- **オフライン**: 全プロダクションアセンブリで `System.Net.*` 等の通信 API への直接参照禁止（`OfflineComplianceTests` / `AppOfflineComplianceTests` が機械判定）。`Tsumugi.App` を含む。
- **警告ゼロ**: `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。
- **`dotnet format --verify-no-changes` 通過**。
- **クロスプラットフォーム**: macOS/Windows 両方で `dotnet build`/`dotnet test` 緑。
- **データ保護**: ログに氏名・受給者証番号・保存先フルパスを出さない。
- **報酬値の非確定**: 単位数・正式加算コードはここで確定しない。暫定値は実装コメントに *「要・報酬告示突合（暫定）」* と明記し、`docs/open-questions.md` に起票。
- **日付**: すべての期間は **両端含む `[開始日, 終了日]`**、**`DateOnly`（暦日）**で比較。`DateTime`/`TimeZone` を記録ロジックに混入させない。
- **TDD**: Red → Green → Refactor。1コミット=1論理変更。コミットメッセージに `phase1/AC1-N` の参照IDを含める。
- **エンティティ**: `record`、`Guid` 主キー、`CreatedAt`/`CreatedBy` を持つ（既存 `Entity` 基底を踏襲）。同一性マスタは更新トークン、期間マスタ／取引記録は新行追加で表現。
- **EF Core**: SQLite に `rowversion` なし → `Guid` 更新トークン方式（既存 `TsumugiDbContext.RotateConcurrencyTokens` を維持・拡張）。

## ファイル構成

```
src/Tsumugi.Domain/
  Entities/
    Entity.cs                                 既存（変更なし）
    Office.cs                                 拡張（ServiceCategory/RegionGrade を追加）
    OfficeCapability.cs                       新規（期間マスタ）
    Recipient.cs                              新規（同一性マスタ）
    Certificate.cs                            新規（期間マスタ）
    Contract.cs                               新規（期間マスタ）
    DailyRecord.cs                            新規（取引記録／追記専用）
  Enums/
    ServiceCategory.cs                        新規（事業所サービス種別）
    RegionGrade.cs                            新規（地域区分）
    RecordKind.cs                             新規（新規／訂正／取消）
    Attendance.cs                             新規（出席／欠席／欠席時対応／中止 等）
    TransportKind.cs                          新規（なし／往／復／往復）
  ValueObjects/
    DateRange.cs                              新規（両端含む [開始日, 終了日?]）
  Logic/
    CertificatePolicy.cs                      新規（期限アラート純粋関数）
    DailyRecordPolicy.cs                      新規（訂正・取消の実効状態導出）
    PeriodPolicy.cs                           新規（期間重複・空白検出）

src/Tsumugi.Application/
  Abstractions/
    IOfficeRepository.cs                      既存（CRUD メソッドを追加）
    IOfficeCapabilityRepository.cs            新規
    IRecipientRepository.cs                   新規
    ICertificateRepository.cs                 新規
    IContractRepository.cs                    新規
    IDailyRecordRepository.cs                 新規
  Dtos/
    OfficeDto.cs                              既存（拡張）
    OfficeCapabilityDto.cs                    新規
    RecipientDto.cs                           新規
    CertificateDto.cs                         新規
    ContractDto.cs                            新規
    DailyRecordDto.cs                         新規
    ExpiringCertificateDto.cs                 新規（残日数を含む）
  UseCases/
    Office/                                   (Update/List 追加)
    OfficeCapability/                         (新規)
    Recipient/                                (新規)
    Certificate/                              (新規 + ListExpiringCertificatesUseCase)
    Contract/                                 (新規)
    DailyRecord/                              (Record/Correct/Cancel/QueryMonth)
  Validation/
    DateValidator.cs                          新規（信頼境界の日付検証）

src/Tsumugi.Infrastructure/
  Persistence/
    TsumugiDbContext.cs                       拡張（5つの DbSet 追加 + 追記不変ガード）
    AppendOnlyGuard.cs                        新規（SaveChanges 介入）
    OfficeConfiguration.cs                    既存（拡張）
    OfficeCapabilityConfiguration.cs          新規
    RecipientConfiguration.cs                 新規
    CertificateConfiguration.cs               新規
    ContractConfiguration.cs                  新規
    DailyRecordConfiguration.cs               新規
    OfficeRepository.cs                       既存（CRUD メソッド追加）
    OfficeCapabilityRepository.cs             新規
    RecipientRepository.cs                    新規
    CertificateRepository.cs                  新規
    ContractRepository.cs                     新規
    DailyRecordRepository.cs                  新規
  Migrations/
    2026MMDDhhmmss_AddPhase1Entities.cs       新規

src/Tsumugi.App/
  ViewModels/
    MainViewModel.cs                          新規
    RecipientListViewModel.cs                 新規
    RecipientEditViewModel.cs                 新規
    CertificateViewModel.cs                   新規（一覧 + 期限アラート）
    ContractViewModel.cs                      新規
    OfficeViewModel.cs                        新規（事業所＋体制）
    DailyRecordViewModel.cs                   新規（月グリッド）
  Views/
    MainWindow.axaml                          差し替え（ナビゲーション）
    RecipientListView.axaml                   新規
    RecipientEditView.axaml                   新規
    CertificateView.axaml                     新規
    ContractView.axaml                        新規
    OfficeView.axaml                          新規
    DailyRecordView.axaml                     新規
  Settings/
    UiDefaults.cs                             新規（ダーク・低アニメ既定）

tests/
  Tsumugi.Domain.Tests/                       各エンティティ + 各 Logic のテーブル駆動テスト
  Tsumugi.Application.Tests/                  各ユースケース + DateValidator
  Tsumugi.Infrastructure.Tests/               追記不変違反テスト + 期間マスタの基準日クエリ
  Tsumugi.App.Tests/                          各 ViewModel のコマンド・状態テスト

docs/decisions/
  0004-attendance-integrated-into-daily-record.md    新規
  0005-certificate-period-overlap-policy.md          新規
  0006-office-capability-flag-set.md                 新規
docs/open-questions.md                               更新（報酬値・期間ポリシー詳細）
```

---

## Group A — Domain（純粋ロジック）

> Domain は外部依存ゼロ。`DateOnly`/`Guid` のみ。すべて record＋静的 Logic 関数。

### Task 1: `DateRange` 値オブジェクト＋`PeriodPolicy`

**Files:**
- Create: `src/Tsumugi.Domain/ValueObjects/DateRange.cs`
- Create: `src/Tsumugi.Domain/Logic/PeriodPolicy.cs`
- Test: `tests/Tsumugi.Domain.Tests/DateRangeTests.cs`
- Test: `tests/Tsumugi.Domain.Tests/PeriodPolicyTests.cs`

**Interfaces:**
- Produces:
  - `record DateRange(DateOnly Start, DateOnly? End)` with `Contains(DateOnly d) : bool` and `Overlaps(DateRange other) : bool`.
  - `static class PeriodPolicy` with `DetectOverlaps(IEnumerable<DateRange>) : IReadOnlyList<(int,int)>` and `DetectGaps(IEnumerable<DateRange>) : IReadOnlyList<DateRange>`.

- [ ] **Step 1: Write failing test for `DateRange.Contains` (両端含む)**

`tests/Tsumugi.Domain.Tests/DateRangeTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DateRangeTests
{
    [Theory]
    [InlineData("2026-04-01", "2026-04-30", "2026-04-01", true)]   // 開始日ちょうど
    [InlineData("2026-04-01", "2026-04-30", "2026-04-30", true)]   // 終了日ちょうど（両端含む）
    [InlineData("2026-04-01", "2026-04-30", "2026-04-15", true)]
    [InlineData("2026-04-01", "2026-04-30", "2026-03-31", false)]
    [InlineData("2026-04-01", "2026-04-30", "2026-05-01", false)]
    public void Contains_handles_both_ends_inclusive(string s, string e, string d, bool expected)
    {
        var range = new DateRange(DateOnly.Parse(s), DateOnly.Parse(e));
        range.Contains(DateOnly.Parse(d)).Should().Be(expected);
    }

    [Fact]
    public void Contains_with_open_end_is_unbounded()
    {
        var range = new DateRange(new DateOnly(2026, 4, 1), End: null);
        range.Contains(new DateOnly(2099, 12, 31)).Should().BeTrue();
        range.Contains(new DateOnly(2026, 3, 31)).Should().BeFalse();
    }

    [Fact]
    public void Construction_rejects_inverted_range()
    {
        Action act = () => new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 3, 31));
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~DateRangeTests
```
Expected: FAIL with `DateRange` not defined.

- [ ] **Step 3: Implement `DateRange`**

`src/Tsumugi.Domain/ValueObjects/DateRange.cs`:

```csharp
namespace Tsumugi.Domain.ValueObjects;

/// <summary>両端含む暦日範囲 [Start, End]。End=null は継続中（無期限）。</summary>
public readonly record struct DateRange
{
    public DateOnly Start { get; }
    public DateOnly? End { get; }

    public DateRange(DateOnly start, DateOnly? end)
    {
        if (end is { } e && e < start)
            throw new ArgumentException("終了日は開始日以降である必要があります。", nameof(end));
        Start = start;
        End = end;
    }

    public bool Contains(DateOnly d) => d >= Start && (End is null || d <= End);

    public bool Overlaps(DateRange other)
    {
        var aEnd = End ?? DateOnly.MaxValue;
        var bEnd = other.End ?? DateOnly.MaxValue;
        return Start <= bEnd && other.Start <= aEnd;
    }
}
```

- [ ] **Step 4: Re-run test — verify PASS**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~DateRangeTests
```
Expected: PASS.

- [ ] **Step 5: Write failing test for `PeriodPolicy`**

`tests/Tsumugi.Domain.Tests/PeriodPolicyTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class PeriodPolicyTests
{
    [Fact]
    public void DetectOverlaps_finds_pairs()
    {
        var ranges = new[]
        {
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)),
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31)),  // overlaps [0]
            new DateRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31)),
        };
        PeriodPolicy.DetectOverlaps(ranges).Should().ContainSingle().Which.Should().Be((0, 1));
    }

    [Fact]
    public void DetectGaps_finds_non_contiguous_ranges()
    {
        var ranges = new[]
        {
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30)),
            new DateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31)),  // gap 7月
        };
        var gaps = PeriodPolicy.DetectGaps(ranges);
        gaps.Should().ContainSingle();
        gaps[0].Start.Should().Be(new DateOnly(2026, 7, 1));
        gaps[0].End.Should().Be(new DateOnly(2026, 7, 31));
    }
}
```

- [ ] **Step 6: Implement `PeriodPolicy`**

`src/Tsumugi.Domain/Logic/PeriodPolicy.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

/// <summary>期間の重複・空白検出（純粋関数）。</summary>
public static class PeriodPolicy
{
    public static IReadOnlyList<(int LeftIndex, int RightIndex)> DetectOverlaps(IEnumerable<DateRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var arr = ranges.ToArray();
        var result = new List<(int, int)>();
        for (var i = 0; i < arr.Length; i++)
            for (var j = i + 1; j < arr.Length; j++)
                if (arr[i].Overlaps(arr[j])) result.Add((i, j));
        return result;
    }

    public static IReadOnlyList<DateRange> DetectGaps(IEnumerable<DateRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var ordered = ranges.OrderBy(r => r.Start).ToArray();
        var result = new List<DateRange>();
        for (var i = 0; i < ordered.Length - 1; i++)
        {
            if (ordered[i].End is not { } end) continue;
            var nextStart = ordered[i + 1].Start;
            var dayAfter = end.AddDays(1);
            if (dayAfter < nextStart)
                result.Add(new DateRange(dayAfter, nextStart.AddDays(-1)));
        }
        return result;
    }
}
```

- [ ] **Step 7: Re-run tests — verify PASS**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~PeriodPolicyTests
```
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Tsumugi.Domain/ValueObjects/DateRange.cs \
        src/Tsumugi.Domain/Logic/PeriodPolicy.cs \
        tests/Tsumugi.Domain.Tests/DateRangeTests.cs \
        tests/Tsumugi.Domain.Tests/PeriodPolicyTests.cs
git commit -m "feat(domain): add DateRange value object and PeriodPolicy (phase1)"
```

---

### Task 2: `Office` 拡張（`ServiceCategory` / `RegionGrade`）

**Files:**
- Create: `src/Tsumugi.Domain/Enums/ServiceCategory.cs`
- Create: `src/Tsumugi.Domain/Enums/RegionGrade.cs`
- Modify: `src/Tsumugi.Domain/Entities/Office.cs`
- Modify: `tests/Tsumugi.Domain.Tests/OfficeTests.cs`

**Interfaces:**
- Consumes: `Entity` 基底（既存）
- Produces:
  - `enum ServiceCategory { TypeB = 1 }`（拡張可、初期値はB型）
  - `enum RegionGrade { None = 0, Grade1 = 1, Grade2 = 2, Grade3 = 3, Grade4 = 4, Grade5 = 5, Grade6 = 6, Grade7 = 7, Other = 99 }`
  - `Office.Create(..., ServiceCategory category, RegionGrade region, ...)`

- [ ] **Step 1: Write failing test (extend `OfficeTests.Create_sets_all_fields`)**

Replace existing `OfficeTests.cs` Create test to assert new fields:

```csharp
[Fact]
public void Create_sets_all_fields()
{
    var id = Guid.NewGuid();
    var token = Guid.NewGuid();
    var at = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(9));

    var office = Office.Create(
        id, "1234567890", "つむぎ作業所",
        ServiceCategory.TypeB, RegionGrade.Grade4,
        "tester", at, token);

    office.Id.Should().Be(id);
    office.OfficeNumber.Should().Be("1234567890");
    office.Name.Should().Be("つむぎ作業所");
    office.ServiceCategory.Should().Be(ServiceCategory.TypeB);
    office.RegionGrade.Should().Be(RegionGrade.Grade4);
    office.CreatedBy.Should().Be("tester");
    office.CreatedAt.Should().Be(at);
    office.ConcurrencyToken.Should().Be(token);
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~OfficeTests
```
Expected: FAIL (`ServiceCategory` not defined / `Create` signature mismatch).

- [ ] **Step 3: Add enums**

`src/Tsumugi.Domain/Enums/ServiceCategory.cs`:

```csharp
namespace Tsumugi.Domain.Enums;

/// <summary>事業所のサービス種別。フェーズ1は B型 のみ。将来拡張可。</summary>
public enum ServiceCategory
{
    TypeB = 1,
}
```

`src/Tsumugi.Domain/Enums/RegionGrade.cs`:

```csharp
namespace Tsumugi.Domain.Enums;

/// <summary>地域区分（1〜7級地・その他）。報酬告示由来。フェーズ3で単価と突合する。</summary>
public enum RegionGrade
{
    None = 0,
    Grade1 = 1,
    Grade2 = 2,
    Grade3 = 3,
    Grade4 = 4,
    Grade5 = 5,
    Grade6 = 6,
    Grade7 = 7,
    Other = 99,
}
```

- [ ] **Step 4: Extend `Office`**

`src/Tsumugi.Domain/Entities/Office.cs`:

```csharp
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>事業所マスタ（同一性マスタ・楽観ロック）。</summary>
public sealed record Office : Entity
{
    public required string OfficeNumber { get; init; }
    public required string Name { get; init; }
    public required ServiceCategory ServiceCategory { get; init; }
    public required RegionGrade RegionGrade { get; init; }

    public static Office Create(
        Guid id,
        string officeNumber,
        string name,
        ServiceCategory serviceCategory,
        RegionGrade regionGrade,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken) => new()
        {
            Id = id,
            OfficeNumber = officeNumber,
            Name = name,
            ServiceCategory = serviceCategory,
            RegionGrade = regionGrade,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
}
```

- [ ] **Step 5: Update existing test for record equality (signature changed)**

```csharp
[Fact]
public void Records_with_same_values_are_equal()
{
    var id = Guid.NewGuid();
    var token = Guid.NewGuid();
    var at = DateTimeOffset.UnixEpoch;

    var a = Office.Create(id, "1", "x", ServiceCategory.TypeB, RegionGrade.Grade4, "u", at, token);
    var b = Office.Create(id, "1", "x", ServiceCategory.TypeB, RegionGrade.Grade4, "u", at, token);

    a.Should().Be(b);
}
```

- [ ] **Step 6: Run tests — verify PASS**

```bash
dotnet test tests/Tsumugi.Domain.Tests
```
Expected: PASS. **注意**: `RegisterOfficeUseCase` の呼び出しが古い `Create` シグネチャを使うので Application 層も同時にコンパイルエラーになる。次ステップでまとめて解消。

- [ ] **Step 7: Update `RegisterOfficeUseCase` to use new signature**

`src/Tsumugi.Application/UseCases/RegisterOfficeUseCase.cs` の `Office.Create` 呼び出しを：

```csharp
var office = Office.Create(
    id: Guid.NewGuid(),
    officeNumber: officeNumber,
    name: name,
    serviceCategory: ServiceCategory.TypeB,
    regionGrade: RegionGrade.None,        // 旧シグネチャ互換：明示登録は Task 16 で追加
    createdBy: actor,
    createdAt: timeProvider.GetUtcNow(),
    concurrencyToken: Guid.NewGuid());
```

（`using Tsumugi.Domain.Enums;` を追加）

- [ ] **Step 8: Run `dotnet build` — verify zero warnings**

```bash
dotnet build
```
Expected: build succeeds, zero warnings.

- [ ] **Step 9: Commit**

```bash
git add src/Tsumugi.Domain/Enums/ServiceCategory.cs \
        src/Tsumugi.Domain/Enums/RegionGrade.cs \
        src/Tsumugi.Domain/Entities/Office.cs \
        src/Tsumugi.Application/UseCases/RegisterOfficeUseCase.cs \
        tests/Tsumugi.Domain.Tests/OfficeTests.cs
git commit -m "feat(domain): extend Office with ServiceCategory and RegionGrade (phase1/AC1-1)"
```

---

### Task 3: `Recipient`（利用者・同一性マスタ）

**Files:**
- Create: `src/Tsumugi.Domain/Entities/Recipient.cs`
- Test: `tests/Tsumugi.Domain.Tests/RecipientTests.cs`

**Interfaces:**
- Produces: `Recipient.Create(id, kanjiName, kanaName, dateOfBirth, createdBy, createdAt, concurrencyToken)`

- [ ] **Step 1: Write failing test**

`tests/Tsumugi.Domain.Tests/RecipientTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class RecipientTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var id = Guid.NewGuid();
        var token = Guid.NewGuid();
        var at = DateTimeOffset.UnixEpoch;
        var dob = new DateOnly(1990, 1, 1);

        var r = Recipient.Create(id, "山田太郎", "ヤマダタロウ", dob, "u", at, token);

        r.Id.Should().Be(id);
        r.KanjiName.Should().Be("山田太郎");
        r.KanaName.Should().Be("ヤマダタロウ");
        r.DateOfBirth.Should().Be(dob);
        r.CreatedBy.Should().Be("u");
        r.CreatedAt.Should().Be(at);
        r.ConcurrencyToken.Should().Be(token);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~RecipientTests
```
Expected: FAIL.

- [ ] **Step 3: Implement `Recipient`**

`src/Tsumugi.Domain/Entities/Recipient.cs`:

```csharp
namespace Tsumugi.Domain.Entities;

/// <summary>利用者（同一性マスタ・楽観ロック）。氏名は漢字とカナを分けて保持。</summary>
public sealed record Recipient : Entity
{
    public required string KanjiName { get; init; }
    public required string KanaName { get; init; }
    public required DateOnly DateOfBirth { get; init; }

    public static Recipient Create(
        Guid id,
        string kanjiName,
        string kanaName,
        DateOnly dateOfBirth,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken) => new()
        {
            Id = id,
            KanjiName = kanjiName,
            KanaName = kanaName,
            DateOfBirth = dateOfBirth,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
}
```

- [ ] **Step 4: Re-run test — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Domain/Entities/Recipient.cs \
        tests/Tsumugi.Domain.Tests/RecipientTests.cs
git commit -m "feat(domain): add Recipient entity (phase1/AC1-1)"
```

---

### Task 4: `Certificate`（受給者証・期間マスタ）

**Files:**
- Create: `src/Tsumugi.Domain/Entities/Certificate.cs`
- Test: `tests/Tsumugi.Domain.Tests/CertificateTests.cs`

**Interfaces:**
- Produces: `Certificate.Create(id, recipientId, certificateNumber, validity:DateRange, supplyDays:int, monthlyCostCap:int, municipality:string, createdBy, createdAt, concurrencyToken)`

> **設計判断**: 期間マスタは「実効日付つき追記」。`ConcurrencyToken` は基底から継承しているが、更新ではなく**新行追加**で訂正する運用にする。トークンは将来訂正フラグ用途で残す。

- [ ] **Step 1: Write failing test**

`tests/Tsumugi.Domain.Tests/CertificateTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class CertificateTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        var c = Certificate.Create(
            id: Guid.NewGuid(),
            recipientId: Guid.NewGuid(),
            certificateNumber: "1234567890",
            validity: validity,
            supplyDays: 22,
            monthlyCostCap: 9300,
            municipality: "杉並区",
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid());

        c.Validity.Should().Be(validity);
        c.SupplyDays.Should().Be(22);
        c.MonthlyCostCap.Should().Be(9300);
        c.Municipality.Should().Be("杉並区");
    }

    [Fact]
    public void Create_rejects_negative_supply_days()
    {
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        Action act = () => Certificate.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1", validity,
            supplyDays: -1, monthlyCostCap: 0, municipality: "x",
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `Certificate`**

`src/Tsumugi.Domain/Entities/Certificate.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>受給者証（期間マスタ・実効日付つき追記）。</summary>
public sealed record Certificate : Entity
{
    public required Guid RecipientId { get; init; }
    public required string CertificateNumber { get; init; }
    public required DateRange Validity { get; init; }
    public required int SupplyDays { get; init; }
    public required int MonthlyCostCap { get; init; }
    public required string Municipality { get; init; }

    public static Certificate Create(
        Guid id,
        Guid recipientId,
        string certificateNumber,
        DateRange validity,
        int supplyDays,
        int monthlyCostCap,
        string municipality,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken)
    {
        if (supplyDays < 0) throw new ArgumentOutOfRangeException(nameof(supplyDays));
        if (monthlyCostCap < 0) throw new ArgumentOutOfRangeException(nameof(monthlyCostCap));

        return new()
        {
            Id = id,
            RecipientId = recipientId,
            CertificateNumber = certificateNumber,
            Validity = validity,
            SupplyDays = supplyDays,
            MonthlyCostCap = monthlyCostCap,
            Municipality = municipality,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
```

- [ ] **Step 4: Re-run test — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Domain/Entities/Certificate.cs \
        tests/Tsumugi.Domain.Tests/CertificateTests.cs
git commit -m "feat(domain): add Certificate entity (phase1/AC1-1)"
```

---

### Task 5: `CertificatePolicy`（期限アラート純粋関数）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/CertificatePolicy.cs`
- Test: `tests/Tsumugi.Domain.Tests/CertificatePolicyTests.cs`

**Interfaces:**
- Consumes: `Certificate`（Task 4）
- Produces:
  - `record CertificateExpiry(Certificate Certificate, int RemainingDays)`
  - `CertificatePolicy.FindExpiring(IEnumerable<Certificate>, DateOnly asOf, int thresholdDays) : IReadOnlyList<CertificateExpiry>`
  - 失効（残日数<0）は対象外。`残日数 = End − asOf`。`End is null`（無期限）は対象外。

- [ ] **Step 1: Write failing tests (table-driven, boundary coverage)**

`tests/Tsumugi.Domain.Tests/CertificatePolicyTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class CertificatePolicyTests
{
    private static Certificate Cert(DateOnly end) => Certificate.Create(
        Guid.NewGuid(), Guid.NewGuid(), "n",
        new DateRange(new DateOnly(2026, 1, 1), end),
        supplyDays: 0, monthlyCostCap: 0, municipality: "x",
        "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());

    [Theory]
    [InlineData("2026-06-27", "2026-07-27", 30, true)]   // 残日数 = しきい値ちょうど
    [InlineData("2026-06-27", "2026-06-27", 30, true)]   // 残日数 = 0
    [InlineData("2026-06-27", "2026-06-26", 30, false)]  // 失効（負）
    [InlineData("2026-06-27", "2026-07-28", 30, false)]  // しきい値より遠い
    public void Single_certificate_matches_threshold(string asOf, string end, int threshold, bool isHit)
    {
        var result = CertificatePolicy.FindExpiring(
            new[] { Cert(DateOnly.Parse(end)) },
            DateOnly.Parse(asOf),
            threshold);
        result.Should().HaveCount(isHit ? 1 : 0);
    }

    [Fact]
    public void Empty_list_returns_empty()
    {
        CertificatePolicy.FindExpiring(Array.Empty<Certificate>(), new DateOnly(2026, 6, 27), 30)
            .Should().BeEmpty();
    }

    [Fact]
    public void Multiple_certificates_returned_ordered_by_remaining_ascending()
    {
        var asOf = new DateOnly(2026, 6, 27);
        var near = Cert(new DateOnly(2026, 6, 30));   // 残3日
        var far = Cert(new DateOnly(2026, 7, 25));    // 残28日
        var result = CertificatePolicy.FindExpiring(new[] { far, near }, asOf, thresholdDays: 30);
        result.Should().HaveCount(2);
        result[0].RemainingDays.Should().Be(3);
        result[1].RemainingDays.Should().Be(28);
    }

    [Fact]
    public void Open_ended_certificate_is_skipped()
    {
        var open = Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "n",
            new DateRange(new DateOnly(2026, 1, 1), End: null),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        CertificatePolicy.FindExpiring(new[] { open }, new DateOnly(2026, 6, 27), 30)
            .Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `CertificatePolicy`**

`src/Tsumugi.Domain/Logic/CertificatePolicy.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public sealed record CertificateExpiry(Certificate Certificate, int RemainingDays);

/// <summary>受給者証の期限アラート抽出（純粋関数。日付/I/Oに依存しない）。</summary>
public static class CertificatePolicy
{
    /// <summary>
    /// 基準日 <paramref name="asOf"/> 時点で、残日数（終了日 − 基準日）が
    /// 0 以上 <paramref name="thresholdDays"/> 以下の受給者証を、残日数昇順で返す。
    /// 終了日 null（無期限）と既に失効（残日数 &lt; 0）は対象外。
    /// </summary>
    public static IReadOnlyList<CertificateExpiry> FindExpiring(
        IEnumerable<Certificate> certificates,
        DateOnly asOf,
        int thresholdDays)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        if (thresholdDays < 0) throw new ArgumentOutOfRangeException(nameof(thresholdDays));

        var result = new List<CertificateExpiry>();
        foreach (var c in certificates)
        {
            if (c.Validity.End is not { } end) continue;
            var remaining = end.DayNumber - asOf.DayNumber;
            if (remaining >= 0 && remaining <= thresholdDays)
                result.Add(new CertificateExpiry(c, remaining));
        }
        return result.OrderBy(e => e.RemainingDays).ToArray();
    }
}
```

- [ ] **Step 4: Re-run tests — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Domain/Logic/CertificatePolicy.cs \
        tests/Tsumugi.Domain.Tests/CertificatePolicyTests.cs
git commit -m "feat(domain): add CertificatePolicy expiry alert (phase1/AC1-5)"
```

---

### Task 6: `Contract`（契約・期間マスタ）

**Files:**
- Create: `src/Tsumugi.Domain/Entities/Contract.cs`
- Test: `tests/Tsumugi.Domain.Tests/ContractTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class ContractTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var period = new DateRange(new DateOnly(2026, 4, 1), End: null);
        var c = Contract.Create(
            id: Guid.NewGuid(), recipientId: Guid.NewGuid(),
            period: period, contractedSupplyDays: 22,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid());

        c.Period.Should().Be(period);
        c.ContractedSupplyDays.Should().Be(22);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `Contract`**

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>契約（期間マスタ・実効日付つき追記）。終了日 null は継続中。</summary>
public sealed record Contract : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateRange Period { get; init; }
    public required int ContractedSupplyDays { get; init; }

    public static Contract Create(
        Guid id, Guid recipientId, DateRange period, int contractedSupplyDays,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken)
    {
        if (contractedSupplyDays < 0) throw new ArgumentOutOfRangeException(nameof(contractedSupplyDays));
        return new()
        {
            Id = id,
            RecipientId = recipientId,
            Period = period,
            ContractedSupplyDays = contractedSupplyDays,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
```

- [ ] **Step 4: Re-run test — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Domain/Entities/Contract.cs tests/Tsumugi.Domain.Tests/ContractTests.cs
git commit -m "feat(domain): add Contract entity (phase1/AC1-1)"
```

---

### Task 7: `OfficeCapability`（事業所体制・期間マスタ・拡張可能なコード集合）

**Files:**
- Create: `src/Tsumugi.Domain/Entities/OfficeCapability.cs`
- Test: `tests/Tsumugi.Domain.Tests/OfficeCapabilityTests.cs`

**Interfaces:**
- Produces: `OfficeCapability.Create(id, officeId, period:DateRange, flags:IReadOnlyDictionary<string,bool>, ...)`
- 加算コードは**文字列キー＋有効フラグ**の辞書として保持。具体コードは `docs/open-questions.md` で未確定。

- [ ] **Step 1: Write failing test**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class OfficeCapabilityTests
{
    [Fact]
    public void Create_holds_flag_map_as_extensible_set()
    {
        var period = new DateRange(new DateOnly(2026, 4, 1), End: null);
        var flags = new Dictionary<string, bool>
        {
            // ★要・報酬告示突合（暫定）: フラグキーはフェーズ3で正式コードに置換
            ["mealProvision"] = true,
            ["transportSupport"] = false,
        };

        var cap = OfficeCapability.Create(
            id: Guid.NewGuid(), officeId: Guid.NewGuid(),
            period: period, flags: flags,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid());

        cap.Period.Should().Be(period);
        cap.Flags["mealProvision"].Should().BeTrue();
        cap.Flags["transportSupport"].Should().BeFalse();
    }

    [Fact]
    public void Flags_are_defensively_copied()
    {
        var dict = new Dictionary<string, bool> { ["a"] = true };
        var cap = OfficeCapability.Create(Guid.NewGuid(), Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null), dict,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        dict["a"] = false;
        cap.Flags["a"].Should().BeTrue("外部の Dictionary 変更が record の状態を壊してはいけない");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `OfficeCapability`**

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 事業所体制（期間マスタ・実効日付つき追記）。加算フラグは拡張可能なコード集合（文字列キー）。
/// ★要・報酬告示突合（暫定）: フラグキーの正式値・単位数はフェーズ3で確定。
/// </summary>
public sealed record OfficeCapability : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateRange Period { get; init; }
    public required IReadOnlyDictionary<string, bool> Flags { get; init; }

    public static OfficeCapability Create(
        Guid id, Guid officeId, DateRange period,
        IReadOnlyDictionary<string, bool> flags,
        string createdBy, DateTimeOffset createdAt, Guid concurrencyToken)
    {
        ArgumentNullException.ThrowIfNull(flags);
        var copy = new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>(flags));
        return new()
        {
            Id = id,
            OfficeId = officeId,
            Period = period,
            Flags = copy,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
```

- [ ] **Step 4: Re-run test — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Domain/Entities/OfficeCapability.cs \
        tests/Tsumugi.Domain.Tests/OfficeCapabilityTests.cs
git commit -m "feat(domain): add OfficeCapability with extensible flag set (phase1/AC1-1,AC1-9)"
```

---

### Task 8: `DailyRecord` エンティティ＋関連列挙

**Files:**
- Create: `src/Tsumugi.Domain/Enums/RecordKind.cs`
- Create: `src/Tsumugi.Domain/Enums/Attendance.cs`
- Create: `src/Tsumugi.Domain/Enums/TransportKind.cs`
- Create: `src/Tsumugi.Domain/Entities/DailyRecord.cs`
- Test: `tests/Tsumugi.Domain.Tests/DailyRecordTests.cs`

**Interfaces:**
- Produces:
  - `enum RecordKind { New = 1, Correct = 2, Cancel = 3 }`
  - `enum Attendance { Present = 1, Absent = 2, AbsenceSupport = 3, Discontinued = 4 }`
  - `enum TransportKind { None = 0, Outbound = 1, Inbound = 2, Round = 3 }`
  - `DailyRecord.NewRecord(...)` / `DailyRecord.Correction(originId, ...)` / `DailyRecord.Cancellation(originId, ...)` ファクトリ3種

- [ ] **Step 1: Write failing tests for factories and invariants**

`tests/Tsumugi.Domain.Tests/DailyRecordTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DailyRecordTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 6, 1);

    [Fact]
    public void NewRecord_has_no_originId()
    {
        var r = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.Round, mealProvided: true,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.New);
        r.OriginId.Should().BeNull();
    }

    [Fact]
    public void Correction_carries_originId()
    {
        var origin = Guid.NewGuid();
        var r = DailyRecord.Correction(Guid.NewGuid(), Recipient, Day, origin,
            Attendance.Absent, TransportKind.None, mealProvided: false,
            note: "病気のため", createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.Correct);
        r.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancellation_carries_originId()
    {
        var origin = Guid.NewGuid();
        var r = DailyRecord.Cancellation(Guid.NewGuid(), Recipient, Day, origin,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.Cancel);
        r.OriginId.Should().Be(origin);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement enums**

`src/Tsumugi.Domain/Enums/RecordKind.cs`:

```csharp
namespace Tsumugi.Domain.Enums;

public enum RecordKind
{
    New = 1,
    Correct = 2,
    Cancel = 3,
}
```

`src/Tsumugi.Domain/Enums/Attendance.cs`:

```csharp
namespace Tsumugi.Domain.Enums;

public enum Attendance
{
    Present = 1,
    Absent = 2,
    AbsenceSupport = 3,
    Discontinued = 4,
}
```

`src/Tsumugi.Domain/Enums/TransportKind.cs`:

```csharp
namespace Tsumugi.Domain.Enums;

public enum TransportKind
{
    None = 0,
    Outbound = 1,
    Inbound = 2,
    Round = 3,
}
```

- [ ] **Step 4: Implement `DailyRecord`**

`src/Tsumugi.Domain/Entities/DailyRecord.cs`:

```csharp
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 日次記録（取引記録・厳密追記）。決して更新・削除しない。訂正・取消は新レコードで表現する。
/// 更新トークンは持たず、基底の <see cref="Entity.ConcurrencyToken"/> は無視する。
/// </summary>
public sealed record DailyRecord : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateOnly ServiceDate { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public Attendance Attendance { get; init; }
    public TransportKind Transport { get; init; }
    public bool MealProvided { get; init; }
    public string? Note { get; init; }

    public static DailyRecord NewRecord(
        Guid id, Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.New,
            OriginId = null,
            Attendance = attendance,
            Transport = transport,
            MealProvided = mealProvided,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,  // 取引記録は更新しないため未使用
        };

    public static DailyRecord Correction(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.Correct,
            OriginId = originId,
            Attendance = attendance,
            Transport = transport,
            MealProvided = mealProvided,
            Note = note,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    public static DailyRecord Cancellation(
        Guid id, Guid recipientId, DateOnly serviceDate, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id,
            RecipientId = recipientId,
            ServiceDate = serviceDate,
            Kind = RecordKind.Cancel,
            OriginId = originId,
            Attendance = Attendance.Discontinued,
            Transport = TransportKind.None,
            MealProvided = false,
            Note = null,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
}
```

- [ ] **Step 5: Re-run tests — verify PASS**

- [ ] **Step 6: Commit**

```bash
git add src/Tsumugi.Domain/Enums/RecordKind.cs \
        src/Tsumugi.Domain/Enums/Attendance.cs \
        src/Tsumugi.Domain/Enums/TransportKind.cs \
        src/Tsumugi.Domain/Entities/DailyRecord.cs \
        tests/Tsumugi.Domain.Tests/DailyRecordTests.cs
git commit -m "feat(domain): add DailyRecord with correction/cancellation factories (phase1/AC1-1,AC1-3)"
```

---

### Task 9: `DailyRecordPolicy`（訂正・取消の実効状態導出）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/DailyRecordPolicy.cs`
- Test: `tests/Tsumugi.Domain.Tests/DailyRecordPolicyTests.cs`

**Interfaces:**
- Consumes: `DailyRecord`
- Produces:
  - `DailyRecordPolicy.Effective(IEnumerable<DailyRecord>) : DailyRecord?` — 単一の (Recipient, ServiceDate) に対する実効レコード
  - `DailyRecordPolicy.EffectiveByDate(IEnumerable<DailyRecord>) : IReadOnlyDictionary<DateOnly, DailyRecord>` — 月次ビュー用

- [ ] **Step 1: Write failing tests covering all branches**

`tests/Tsumugi.Domain.Tests/DailyRecordPolicyTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DailyRecordPolicyTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 6, 1);
    private static DateTimeOffset T(int hour) => new(2026, 6, 2, hour, 0, 0, TimeSpan.Zero);

    private static DailyRecord New(Guid id, Attendance att, int t) =>
        DailyRecord.NewRecord(id, Recipient, Day, att, TransportKind.None, false, null, "u", T(t));
    private static DailyRecord Corr(Guid id, Guid origin, Attendance att, int t) =>
        DailyRecord.Correction(id, Recipient, Day, origin, att, TransportKind.None, false, null, "u", T(t));
    private static DailyRecord Cancel(Guid id, Guid origin, int t) =>
        DailyRecord.Cancellation(id, Recipient, Day, origin, "u", T(t));

    [Fact]
    public void Empty_returns_null()
    {
        DailyRecordPolicy.Effective(Array.Empty<DailyRecord>()).Should().BeNull();
    }

    [Fact]
    public void Single_new_record_is_effective()
    {
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        DailyRecordPolicy.Effective(new[] { n }).Should().Be(n);
    }

    [Fact]
    public void Latest_correction_wins()
    {
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        var c1 = Corr(Guid.NewGuid(), n.Id, Attendance.Absent, 10);
        var c2 = Corr(Guid.NewGuid(), c1.Id, Attendance.AbsenceSupport, 11);
        DailyRecordPolicy.Effective(new[] { c1, n, c2 }).Should().Be(c2);
    }

    [Fact]
    public void Cancellation_makes_effective_null()
    {
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        var x = Cancel(Guid.NewGuid(), n.Id, 10);
        DailyRecordPolicy.Effective(new[] { n, x }).Should().BeNull();
    }

    [Fact]
    public void Re_correction_after_cancellation_is_ignored()
    {
        // 取消後にさらに「取消Idを訂正元」とする訂正が来ても、取消は最終状態として残る
        var n = New(Guid.NewGuid(), Attendance.Present, 9);
        var x = Cancel(Guid.NewGuid(), n.Id, 10);
        var c = Corr(Guid.NewGuid(), x.Id, Attendance.Present, 11);
        DailyRecordPolicy.Effective(new[] { n, x, c }).Should().BeNull();
    }

    [Fact]
    public void EffectiveByDate_groups_by_service_date()
    {
        var a = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", T(9));
        var b = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, new DateOnly(2026, 6, 2),
            Attendance.Absent, TransportKind.None, false, null, "u", T(9));
        var map = DailyRecordPolicy.EffectiveByDate(new[] { a, b });
        map.Should().HaveCount(2);
        map[new DateOnly(2026, 6, 1)].Should().Be(a);
        map[new DateOnly(2026, 6, 2)].Should().Be(b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement `DailyRecordPolicy`**

`src/Tsumugi.Domain/Logic/DailyRecordPolicy.cs`:

```csharp
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>日次記録の訂正・取消の実効状態を導出する純粋関数。</summary>
public static class DailyRecordPolicy
{
    /// <summary>
    /// (Recipient, ServiceDate) ごとの全レコードから実効レコードを返す。
    /// アルゴリズム: 新規レコードを起点に、自分を OriginId とする「次の訂正/取消」を辿る。
    /// 取消に当たったらその時点で実効は null。
    /// </summary>
    public static DailyRecord? Effective(IEnumerable<DailyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var list = records.OrderBy(r => r.CreatedAt).ToArray();
        if (list.Length == 0) return null;

        var origin = list.FirstOrDefault(r => r.Kind == RecordKind.New);
        if (origin is null) return null;

        var current = origin;
        while (true)
        {
            var next = list
                .Where(r => r.OriginId == current.Id && r.Kind != RecordKind.New)
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null) return current;
            if (next.Kind == RecordKind.Cancel) return null;
            current = next;  // Correct
        }
    }

    /// <summary>月次ビュー用：日付ごとの実効レコードのマップを返す。</summary>
    public static IReadOnlyDictionary<DateOnly, DailyRecord> EffectiveByDate(
        IEnumerable<DailyRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var result = new Dictionary<DateOnly, DailyRecord>();
        foreach (var group in records.GroupBy(r => r.ServiceDate))
        {
            if (Effective(group) is { } eff)
                result[group.Key] = eff;
        }
        return result;
    }
}
```

- [ ] **Step 4: Re-run tests — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Domain/Logic/DailyRecordPolicy.cs \
        tests/Tsumugi.Domain.Tests/DailyRecordPolicyTests.cs
git commit -m "feat(domain): add DailyRecordPolicy effective-state derivation (phase1/AC1-3)"
```

---

## Group B — Infrastructure（永続化＋追記不変ガード）

### Task 10: 新規エンティティの EF Core 設定（Configuration ファイル5本）

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/RecipientConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/CertificateConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ContractConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/OfficeCapabilityConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/DailyRecordConfiguration.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/OfficeConfiguration.cs`（`ServiceCategory`/`RegionGrade` を追加）
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`（5つの DbSet と OfficeCapability の Flags JSON 変換を追加）

**Interfaces:**
- Consumes: Domain エンティティ群（Task 2〜8）
- Produces: 各エンティティに対応する `IEntityTypeConfiguration<T>` 実装と、`TsumugiDbContext` の `DbSet<T>` プロパティ

**設計判断:**
- `DateRange` 値オブジェクトは `Start`/`End` の2列として展開（`OwnsOne` ではなく shadow conversion）。
- `OfficeCapability.Flags` は `Dictionary<string,bool>` → JSON テキスト列に値変換で保存（SQLite に JSON 関数あり）。
- `DailyRecord` の `ConcurrencyToken` 列は不要だが基底に存在するため、SQLite 列としては作成し EF 上は `IsConcurrencyToken()` を**つけない**（追記専用なので競合不要）。

- [ ] **Step 1: Modify `OfficeConfiguration` to map new enum columns**

`src/Tsumugi.Infrastructure/Persistence/OfficeConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Offices");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OfficeNumber).IsRequired().HasMaxLength(32);
        builder.HasIndex(o => o.OfficeNumber).IsUnique();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(128);
        builder.Property(o => o.ServiceCategory).HasConversion<int>().IsRequired();
        builder.Property(o => o.RegionGrade).HasConversion<int>().IsRequired();
        builder.Property(o => o.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.ConcurrencyToken).IsConcurrencyToken();
    }
}
```

- [ ] **Step 2: Create `RecipientConfiguration`**

`src/Tsumugi.Infrastructure/Persistence/RecipientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class RecipientConfiguration : IEntityTypeConfiguration<Recipient>
{
    public void Configure(EntityTypeBuilder<Recipient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Recipients");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.KanjiName).IsRequired().HasMaxLength(128);
        builder.Property(r => r.KanaName).IsRequired().HasMaxLength(128);
        builder.Property(r => r.DateOfBirth).IsRequired();
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ConcurrencyToken).IsConcurrencyToken();
    }
}
```

- [ ] **Step 3: Create `CertificateConfiguration`** (DateRange 展開)

`src/Tsumugi.Infrastructure/Persistence/CertificateConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Certificates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RecipientId).IsRequired();
        builder.HasIndex(c => c.RecipientId);
        builder.Property(c => c.CertificateNumber).IsRequired().HasMaxLength(32);
        builder.Property(c => c.SupplyDays).IsRequired();
        builder.Property(c => c.MonthlyCostCap).IsRequired();
        builder.Property(c => c.Municipality).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ConcurrencyToken).IsConcurrencyToken();

        // DateRange は Start / End の2列に展開（EF Core は record struct を直接マップできないため値変換）
        builder.Property(c => c.Validity)
            .HasConversion(
                v => DateRangeJson.Serialize(v),
                s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("Validity");
    }
}
```

`src/Tsumugi.Infrastructure/Persistence/DateRangeJson.cs`（共有ヘルパ）:

```csharp
using System.Text.Json;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>DateRange の SQLite 列への単一文字列シリアライズ。</summary>
internal static class DateRangeJson
{
    public static string Serialize(DateRange v) =>
        JsonSerializer.Serialize(new Dto(v.Start.ToString("O"), v.End?.ToString("O")));

    public static DateRange Deserialize(string s)
    {
        var dto = JsonSerializer.Deserialize<Dto>(s)
            ?? throw new InvalidOperationException("DateRange のデシリアライズに失敗");
        return new DateRange(
            DateOnly.Parse(dto.Start),
            dto.End is null ? null : DateOnly.Parse(dto.End));
    }

    private sealed record Dto(string Start, string? End);
}
```

- [ ] **Step 4: Create `ContractConfiguration`**

`src/Tsumugi.Infrastructure/Persistence/ContractConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Contracts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.RecipientId).IsRequired();
        builder.HasIndex(c => c.RecipientId);
        builder.Property(c => c.ContractedSupplyDays).IsRequired();
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ConcurrencyToken).IsConcurrencyToken();
        builder.Property(c => c.Period)
            .HasConversion(v => DateRangeJson.Serialize(v), s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("Period");
    }
}
```

- [ ] **Step 5: Create `OfficeCapabilityConfiguration`** (Flags JSON)

`src/Tsumugi.Infrastructure/Persistence/OfficeCapabilityConfiguration.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeCapabilityConfiguration : IEntityTypeConfiguration<OfficeCapability>
{
    public void Configure(EntityTypeBuilder<OfficeCapability> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OfficeCapabilities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.OfficeId).IsRequired();
        builder.HasIndex(c => c.OfficeId);
        builder.Property(c => c.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.ConcurrencyToken).IsConcurrencyToken();
        builder.Property(c => c.Period)
            .HasConversion(v => DateRangeJson.Serialize(v), s => DateRangeJson.Deserialize(s))
            .IsRequired()
            .HasColumnName("Period");
        builder.Property(c => c.Flags)
            .HasConversion(
                f => JsonSerializer.Serialize(f, (JsonSerializerOptions?)null),
                s => new ReadOnlyDictionary<string, bool>(
                    JsonSerializer.Deserialize<Dictionary<string, bool>>(s, (JsonSerializerOptions?)null)
                    ?? new Dictionary<string, bool>()))
            .IsRequired()
            .HasColumnName("FlagsJson");
    }
}
```

- [ ] **Step 6: Create `DailyRecordConfiguration`**

`src/Tsumugi.Infrastructure/Persistence/DailyRecordConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class DailyRecordConfiguration : IEntityTypeConfiguration<DailyRecord>
{
    public void Configure(EntityTypeBuilder<DailyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DailyRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RecipientId).IsRequired();
        builder.HasIndex(r => new { r.RecipientId, r.ServiceDate });
        builder.Property(r => r.ServiceDate).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        builder.Property(r => r.OriginId);
        builder.HasIndex(r => r.OriginId);
        builder.Property(r => r.Attendance).HasConversion<int>().IsRequired();
        builder.Property(r => r.Transport).HasConversion<int>().IsRequired();
        builder.Property(r => r.MealProvided).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        // 取引記録は更新しないため ConcurrencyToken は IsConcurrencyToken() しない。列としては存在。
    }
}
```

- [ ] **Step 7: Extend `TsumugiDbContext` with new DbSets**

`src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`（DbSet を追加）:

```csharp
public DbSet<Recipient> Recipients => Set<Recipient>();
public DbSet<Certificate> Certificates => Set<Certificate>();
public DbSet<Contract> Contracts => Set<Contract>();
public DbSet<OfficeCapability> OfficeCapabilities => Set<OfficeCapability>();
public DbSet<DailyRecord> DailyRecords => Set<DailyRecord>();
```

- [ ] **Step 8: Build — verify zero warnings**

```bash
dotnet build
```
Expected: PASS. マイグレーション未追加なのでこの段階では実DBは更新されない。

- [ ] **Step 9: Commit**

```bash
git add src/Tsumugi.Infrastructure/Persistence/OfficeConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/RecipientConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/CertificateConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/ContractConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/OfficeCapabilityConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/DailyRecordConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/DateRangeJson.cs \
        src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs
git commit -m "feat(infra): add EF configurations for phase1 entities (phase1/AC1-1)"
```

---

### Task 11: マイグレーション `AddPhase1Entities`

**Files:**
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddPhase1Entities.cs`（`dotnet ef` で自動生成）
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddPhase1Entities.Designer.cs`
- Modify: `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate migration**

```bash
dotnet tool restore
dotnet ef migrations add AddPhase1Entities \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```
Expected: 新しいマイグレーションファイル3本生成。

- [ ] **Step 2: Inspect generated migration**

`src/Tsumugi.Infrastructure/Migrations/<timestamp>_AddPhase1Entities.cs` を Read で開き、以下を確認：
- `Offices` の `ALTER TABLE`（`ServiceCategory`/`RegionGrade` 列の追加）
- `Recipients`/`Certificates`/`Contracts`/`OfficeCapabilities`/`DailyRecords` の `CreateTable`
- 期待されるインデックス（`RecipientId`、`(RecipientId, ServiceDate)`、`OriginId`、`OfficeId`、`OfficeNumber unique`）

不適切なら一度ロールバック（`dotnet ef migrations remove`）して Configuration を直してから再生成。

- [ ] **Step 3: Apply migration locally (smoke)**

```bash
dotnet ef database update --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```
Expected: ローカル DB が更新される（権限は SqliteLocationService 経由なので 0700/0600 維持）。

- [ ] **Step 4: Add fixture-based round-trip test for new tables**

`tests/Tsumugi.Infrastructure.Tests/Phase1EntitiesRoundTripTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class Phase1EntitiesRoundTripTests : IClassFixture<TempDbFixture>
{
    private readonly TempDbFixture _fixture;
    public Phase1EntitiesRoundTripTests(TempDbFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Recipient_round_trips()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Recipients.Add(Recipient.Create(id, "山田", "ヤマダ",
                new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _fixture.NewContext())
        {
            var r = await ctx.Recipients.SingleAsync(x => x.Id == id);
            r.KanjiName.Should().Be("山田");
            r.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        }
    }

    [Fact]
    public async Task Certificate_with_date_range_round_trips()
    {
        var id = Guid.NewGuid();
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Certificates.Add(Certificate.Create(
                id, Guid.NewGuid(), "12345", validity, 22, 9300, "杉並区",
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _fixture.NewContext())
        {
            var c = await ctx.Certificates.SingleAsync(x => x.Id == id);
            c.Validity.Should().Be(validity);
        }
    }

    [Fact]
    public async Task OfficeCapability_flags_round_trip_as_json()
    {
        var id = Guid.NewGuid();
        var flags = new Dictionary<string, bool> { ["mealProvision"] = true, ["transportSupport"] = false };
        await using (var ctx = _fixture.NewContext())
        {
            ctx.OfficeCapabilities.Add(OfficeCapability.Create(
                id, Guid.NewGuid(),
                new DateRange(new DateOnly(2026, 4, 1), null), flags,
                "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _fixture.NewContext())
        {
            var cap = await ctx.OfficeCapabilities.SingleAsync(x => x.Id == id);
            cap.Flags["mealProvision"].Should().BeTrue();
            cap.Flags["transportSupport"].Should().BeFalse();
        }
    }

    [Fact]
    public async Task DailyRecord_appends_and_round_trips()
    {
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 1);
        var newId = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.DailyRecords.Add(DailyRecord.NewRecord(newId, rid, day,
                Attendance.Present, TransportKind.Round, true, "通常", "u", DateTimeOffset.UnixEpoch));
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _fixture.NewContext())
        {
            var r = await ctx.DailyRecords.SingleAsync(x => x.Id == newId);
            r.Attendance.Should().Be(Attendance.Present);
            r.Transport.Should().Be(TransportKind.Round);
            r.MealProvided.Should().BeTrue();
        }
    }
}
```

> **Note:** `TempDbFixture` は既存（`OfficeRepositoryTests`/`SqliteBackupTests` が使用しているもの）。マイグレーション適用済みの一時 SQLite を返す前提。

- [ ] **Step 5: Run tests — verify PASS**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~Phase1EntitiesRoundTripTests
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Tsumugi.Infrastructure/Migrations/ \
        tests/Tsumugi.Infrastructure.Tests/Phase1EntitiesRoundTripTests.cs
git commit -m "feat(infra): add migration AddPhase1Entities + round-trip tests (phase1/AC1-1,AC1-4)"
```

---

### Task 12: 追記不変ガード（`AppendOnlyGuard` + `TsumugiDbContext` 介入）

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardTests.cs`

**Interfaces:**
- Produces:
  - `static class AppendOnlyGuard.Inspect(ChangeTracker) : void` — `Modified`/`Deleted` の `DailyRecord`/`Certificate`/`Contract`/`OfficeCapability` を検出して例外を投げる
  - 例外型: `AppendOnlyViolationException(string entityName, EntityState state)`

**設計判断:**
- `DailyRecord`（取引記録）と期間マスタ群（`Certificate`/`Contract`/`OfficeCapability`）は **`Added` のみ許可**。
- 同一性マスタ（`Office`/`Recipient`）は `Added`/`Modified` を許可（楽観ロックで競合検知）。
- 違反は `SaveChanges` 前に検出し例外で確実に落とす。

- [ ] **Step 1: Write failing test for append-only violation**

`tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardTests : IClassFixture<TempDbFixture>
{
    private readonly TempDbFixture _fixture;
    public AppendOnlyGuardTests(TempDbFixture f) => _fixture = f;

    [Fact]
    public async Task Modifying_DailyRecord_throws()
    {
        var rid = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var rec = DailyRecord.NewRecord(id, rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        ctx.DailyRecords.Add(rec);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.DailyRecords.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(DailyRecord.Note)).CurrentValue = "after the fact";
        ctx.Entry(loaded).Property(nameof(DailyRecord.Note)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(e => e.EntityName == nameof(DailyRecord));
    }

    [Fact]
    public async Task Deleting_DailyRecord_throws()
    {
        var rid = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.DailyRecords.Add(DailyRecord.NewRecord(id, rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch));
        await ctx.SaveChangesAsync();

        ctx.DailyRecords.Remove(await ctx.DailyRecords.SingleAsync(x => x.Id == id));

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>();
    }

    [Fact]
    public async Task Modifying_Certificate_throws_period_master_is_append_only()
    {
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.Certificates.Add(Certificate.Create(id, Guid.NewGuid(), "1",
            new Domain.ValueObjects.DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Certificates.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(Certificate.Municipality)).CurrentValue = "別市";
        ctx.Entry(loaded).Property(nameof(Certificate.Municipality)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>()
            .Where(e => e.EntityName == nameof(Certificate));
    }

    [Fact]
    public async Task Modifying_Office_is_allowed_identity_master_uses_token()
    {
        var id = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        ctx.Offices.Add(Office.Create(id, "1", "x",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Offices.SingleAsync(x => x.Id == id);
        ctx.Entry(loaded).Property(nameof(Office.Name)).CurrentValue = "y";
        ctx.Entry(loaded).Property(nameof(Office.Name)).IsModified = true;

        Func<Task> act = () => ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~AppendOnlyGuardTests
```
Expected: FAIL (`AppendOnlyViolationException` not defined).

- [ ] **Step 3: Implement `AppendOnlyGuard`**

`src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// 追記不変ガード。取引記録（DailyRecord）と期間マスタ（Certificate/Contract/OfficeCapability）の
/// Modified/Deleted を SaveChanges 前に検出し例外で落とす。同一性マスタは対象外。
/// </summary>
public static class AppendOnlyGuard
{
    private static readonly HashSet<Type> AppendOnlyTypes = new()
    {
        typeof(DailyRecord),
        typeof(Certificate),
        typeof(Contract),
        typeof(OfficeCapability),
    };

    public static void Inspect(ChangeTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        foreach (var entry in tracker.Entries())
        {
            if (!AppendOnlyTypes.Contains(entry.Entity.GetType())) continue;
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new AppendOnlyViolationException(entry.Entity.GetType().Name, entry.State);
        }
    }
}

/// <summary>追記不変違反。コードレビュー時に「破壊更新を意図したコード」が混入したと判断する。</summary>
public sealed class AppendOnlyViolationException(string entityName, EntityState state)
    : InvalidOperationException(
        $"{entityName} は追記専用エンティティのため、{state} 状態での保存はできません。" +
        " 訂正・取消は新レコードとして追加してください。")
{
    public string EntityName { get; } = entityName;
    public EntityState State { get; } = state;
}
```

- [ ] **Step 4: Wire guard into `TsumugiDbContext.SaveChanges`**

`src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs` の `SaveChanges`/`SaveChangesAsync` を以下に差し替える：

```csharp
public override int SaveChanges(bool acceptAllChangesOnSuccess)
{
    AppendOnlyGuard.Inspect(ChangeTracker);
    RotateConcurrencyTokens();
    return base.SaveChanges(acceptAllChangesOnSuccess);
}

public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    AppendOnlyGuard.Inspect(ChangeTracker);
    RotateConcurrencyTokens();
    return base.SaveChangesAsync(cancellationToken);
}
```

> **Note:** `Inspect` を `Rotate` より先に呼ぶ。違反があった場合に楽観トークンを書き換えない。

- [ ] **Step 5: Re-run tests — verify PASS**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~AppendOnlyGuardTests
```
Expected: PASS.

- [ ] **Step 6: 歯があることの追加確認（意図的違反テスト）**

`AppendOnlyGuardTests` に **意図的違反を一時的に許可しても赤になる** ことを示すコメントを残す（実装変更でガードを外したら検証テストが緑のままにならない）。

具体的には `Modifying_DailyRecord_throws` が**ガードを外すと失敗する**ことを README/コメントで明示。コード変更不要。

- [ ] **Step 7: Commit**

```bash
git add src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs \
        src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs \
        tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardTests.cs
git commit -m "feat(infra): enforce append-only invariant in DbContext (phase1/AC1-2)"
```

---

### Task 13: リポジトリ抽象と実装（Recipient/Certificate/Contract/OfficeCapability/DailyRecord）

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IRecipientRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/ICertificateRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IContractRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IOfficeCapabilityRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IDailyRecordRepository.cs`
- Modify: `src/Tsumugi.Application/Abstractions/IOfficeRepository.cs`（GetById/Update/List 追加）
- Create: `src/Tsumugi.Infrastructure/Persistence/RecipientRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/CertificateRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ContractRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/OfficeCapabilityRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/DailyRecordRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/OfficeRepository.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`（5本の新リポジトリを登録）

**Interfaces:** 各リポジトリは下記契約。

- `IOfficeRepository`: `AddAsync(Office)`, `FindByNumberAsync(string)`, `FindByIdAsync(Guid)`, `UpdateAsync(Office)`, `ListAsync()`
- `IRecipientRepository`: `AddAsync(Recipient)`, `FindByIdAsync(Guid)`, `UpdateAsync(Recipient)`, `ListAsync()`
- `ICertificateRepository`: `AddAsync(Certificate)`, `ListByRecipientAsync(Guid recipientId)`, `ListAllAsync()` *(期限アラート用に全件取得)*, `FindEffectiveAsync(Guid recipientId, DateOnly asOf)`
- `IContractRepository`: `AddAsync(Contract)`, `ListByRecipientAsync(Guid)`, `FindEffectiveAsync(Guid recipientId, DateOnly asOf)`
- `IOfficeCapabilityRepository`: `AddAsync(OfficeCapability)`, `ListByOfficeAsync(Guid)`, `FindEffectiveAsync(Guid officeId, DateOnly asOf)`
- `IDailyRecordRepository`: `AddAsync(DailyRecord)`, `FindByIdAsync(Guid)`, `ListByRecipientAndDateAsync(Guid, DateOnly)`, `ListByRecipientAndMonthAsync(Guid, int year, int month)`

> **「基準日時点」クエリ**: `FindEffectiveAsync` は `Period.Start ≤ asOf AND (Period.End IS NULL OR asOf ≤ Period.End)` を SQL レベルで実装。`DateRange` は JSON 列なので、永続化側では `PeriodStart`/`PeriodEnd` の shadow プロパティを追加してインデックス可能にする選択肢があるが、まずは**メモリでフィルタ**で実装し、性能ボトルネックが顕在化したらシャドウ列追加に移行する（テスト規模では十分）。

- [ ] **Step 1: Define repository interfaces**

`src/Tsumugi.Application/Abstractions/IOfficeRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeRepository
{
    Task AddAsync(Office office, CancellationToken ct);
    Task<Office?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct);
    Task UpdateAsync(Office office, CancellationToken ct);
    Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct);
}
```

`src/Tsumugi.Application/Abstractions/IRecipientRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IRecipientRepository
{
    Task AddAsync(Recipient recipient, CancellationToken ct);
    Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct);
    Task UpdateAsync(Recipient recipient, CancellationToken ct);
    Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct);
}
```

`src/Tsumugi.Application/Abstractions/ICertificateRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface ICertificateRepository
{
    Task AddAsync(Certificate certificate, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct);
    Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct);
    Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct);
}
```

`src/Tsumugi.Application/Abstractions/IContractRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IContractRepository
{
    Task AddAsync(Contract contract, CancellationToken ct);
    Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid recipientId, CancellationToken ct);
    Task<Contract?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct);
}
```

`src/Tsumugi.Application/Abstractions/IOfficeCapabilityRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeCapabilityRepository
{
    Task AddAsync(OfficeCapability capability, CancellationToken ct);
    Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct);
    Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct);
}
```

`src/Tsumugi.Application/Abstractions/IDailyRecordRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IDailyRecordRepository
{
    Task AddAsync(DailyRecord record, CancellationToken ct);
    Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(
        Guid recipientId, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct);
}
```

- [ ] **Step 2: Extend `OfficeRepository` impl**

`src/Tsumugi.Infrastructure/Persistence/OfficeRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeRepository(TsumugiDbContext db) : IOfficeRepository
{
    public async Task AddAsync(Office office, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(office);
        await db.Offices.AddAsync(office, ct);
    }

    public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
        db.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.OfficeNumber == officeNumber, ct);

    public Task UpdateAsync(Office office, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(office);
        db.Offices.Update(office);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
        await db.Offices.AsNoTracking().OrderBy(o => o.OfficeNumber).ToListAsync(ct);
}
```

- [ ] **Step 3: Implement `RecipientRepository`**

`src/Tsumugi.Infrastructure/Persistence/RecipientRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class RecipientRepository(TsumugiDbContext db) : IRecipientRepository
{
    public async Task AddAsync(Recipient r, CancellationToken ct) => await db.Recipients.AddAsync(r, ct);

    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Recipients.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task UpdateAsync(Recipient r, CancellationToken ct)
    {
        db.Recipients.Update(r);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        await db.Recipients.AsNoTracking().OrderBy(r => r.KanaName).ToListAsync(ct);
}
```

- [ ] **Step 4: Implement `CertificateRepository`**

`src/Tsumugi.Infrastructure/Persistence/CertificateRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class CertificateRepository(TsumugiDbContext db) : ICertificateRepository
{
    public async Task AddAsync(Certificate c, CancellationToken ct) => await db.Certificates.AddAsync(c, ct);

    public async Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        await db.Certificates.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        await db.Certificates.AsNoTracking().ToListAsync(ct);

    public async Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct)
    {
        // DateRange は JSON 列のため SQL レベルでフィルタできない。
        // インデックスのある RecipientId で絞り込んだあとメモリで実効判定。
        var candidates = await db.Certificates.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);
        // 同期日内に複数あれば「最新の CreatedAt」が実効
        return candidates
            .Where(c => c.Validity.Contains(asOf))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 5: Implement `ContractRepository`** (`CertificateRepository` と同形)

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ContractRepository(TsumugiDbContext db) : IContractRepository
{
    public async Task AddAsync(Contract c, CancellationToken ct) => await db.Contracts.AddAsync(c, ct);

    public async Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        await db.Contracts.AsNoTracking()
            .Where(c => c.RecipientId == recipientId)
            .ToListAsync(ct);

    public async Task<Contract?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct)
    {
        var candidates = await db.Contracts.AsNoTracking()
            .Where(c => c.RecipientId == recipientId).ToListAsync(ct);
        return candidates
            .Where(c => c.Period.Contains(asOf))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 6: Implement `OfficeCapabilityRepository`**

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeCapabilityRepository(TsumugiDbContext db) : IOfficeCapabilityRepository
{
    public async Task AddAsync(OfficeCapability c, CancellationToken ct) => await db.OfficeCapabilities.AddAsync(c, ct);

    public async Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct) =>
        await db.OfficeCapabilities.AsNoTracking()
            .Where(c => c.OfficeId == officeId)
            .ToListAsync(ct);

    public async Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct)
    {
        var candidates = await db.OfficeCapabilities.AsNoTracking()
            .Where(c => c.OfficeId == officeId).ToListAsync(ct);
        return candidates
            .Where(c => c.Period.Contains(asOf))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 7: Implement `DailyRecordRepository`**

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class DailyRecordRepository(TsumugiDbContext db) : IDailyRecordRepository
{
    public async Task AddAsync(DailyRecord r, CancellationToken ct) => await db.DailyRecords.AddAsync(r, ct);

    public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.DailyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(
        Guid recipientId, DateOnly date, CancellationToken ct) =>
        await db.DailyRecords.AsNoTracking()
            .Where(r => r.RecipientId == recipientId && r.ServiceDate == date)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return await db.DailyRecords.AsNoTracking()
            .Where(r => r.RecipientId == recipientId && r.ServiceDate >= from && r.ServiceDate <= to)
            .OrderBy(r => r.ServiceDate).ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}
```

- [ ] **Step 8: Register new repositories in DI**

`src/Tsumugi.Infrastructure/DependencyInjection.cs` の `AddTsumugiInfrastructure` 内に追加：

```csharp
services.AddScoped<IRecipientRepository, RecipientRepository>();
services.AddScoped<ICertificateRepository, CertificateRepository>();
services.AddScoped<IContractRepository, ContractRepository>();
services.AddScoped<IOfficeCapabilityRepository, OfficeCapabilityRepository>();
services.AddScoped<IDailyRecordRepository, DailyRecordRepository>();
```

- [ ] **Step 9: Smoke test — repository round-trip**

`tests/Tsumugi.Infrastructure.Tests/Phase1RepositoriesTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class Phase1RepositoriesTests : IClassFixture<TempDbFixture>
{
    private readonly TempDbFixture _fixture;
    public Phase1RepositoriesTests(TempDbFixture f) => _fixture = f;

    [Fact]
    public async Task CertificateRepository_FindEffective_returns_in_range()
    {
        var rid = Guid.NewGuid();
        await using var ctx = _fixture.NewContext();
        var repo = new Persistence.CertificateRepository(ctx);

        await repo.AddAsync(Certificate.Create(Guid.NewGuid(), rid, "1",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()), default);
        await repo.AddAsync(Certificate.Create(Guid.NewGuid(), rid, "2",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch.AddSeconds(1), Guid.NewGuid()), default);
        await ctx.SaveChangesAsync();

        var hit = await repo.FindEffectiveAsync(rid, new DateOnly(2026, 5, 1), default);
        hit.Should().NotBeNull();
        hit!.CertificateNumber.Should().Be("2");

        var miss = await repo.FindEffectiveAsync(rid, new DateOnly(2025, 12, 31), default);
        miss.Should().BeNull();
    }
}
```

- [ ] **Step 10: Run tests — verify PASS**

```bash
dotnet build
dotnet test tests/Tsumugi.Infrastructure.Tests
```

- [ ] **Step 11: Commit**

```bash
git add src/Tsumugi.Application/Abstractions/ \
        src/Tsumugi.Infrastructure/Persistence/RecipientRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/CertificateRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/ContractRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/OfficeCapabilityRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/DailyRecordRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/OfficeRepository.cs \
        src/Tsumugi.Infrastructure/DependencyInjection.cs \
        tests/Tsumugi.Infrastructure.Tests/Phase1RepositoriesTests.cs
git commit -m "feat(infra): add phase1 repositories with as-of-date queries (phase1/AC1-1,AC1-4)"
```

---

## Group C — Application（ユースケース＋信頼境界の日付検証）

### Task 14: `DateValidator`（信頼境界の日付検証）

**Files:**
- Create: `src/Tsumugi.Application/Validation/DateValidator.cs`
- Create: `src/Tsumugi.Application/Validation/DateValidationException.cs`
- Test: `tests/Tsumugi.Application.Tests/DateValidatorTests.cs`

**Interfaces:**
- Produces:
  - `DateValidator.EnsureValid(DateOnly value, string fieldName)` — `DateOnly.MinValue`/`MaxValue` などの異常値を弾く
  - `DateValidator.EnsureRange(DateOnly start, DateOnly? end, string fieldName)` — `end < start` を弾く
  - `DateValidator.EnsureYearMonth(int year, int month)` — 月レンジ検証
  - `DateValidationException(string message, string fieldName)`

> **Reason**: UI の入力制限だけに依存しない。Application 層が信頼境界。

- [ ] **Step 1: Write failing tests**

`tests/Tsumugi.Application.Tests/DateValidatorTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Application.Validation;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class DateValidatorTests
{
    [Fact]
    public void EnsureValid_passes_for_realistic_date()
    {
        var act = () => DateValidator.EnsureValid(new DateOnly(2026, 6, 27), "x");
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_rejects_min_value()
    {
        var act = () => DateValidator.EnsureValid(DateOnly.MinValue, "誕生日");
        act.Should().Throw<DateValidationException>().Where(e => e.FieldName == "誕生日");
    }

    [Fact]
    public void EnsureRange_rejects_inverted_range()
    {
        var act = () => DateValidator.EnsureRange(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 5, 31), "有効期間");
        act.Should().Throw<DateValidationException>();
    }

    [Fact]
    public void EnsureRange_allows_open_end()
    {
        var act = () => DateValidator.EnsureRange(new DateOnly(2026, 4, 1), null, "契約期間");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1899, 6)]
    public void EnsureYearMonth_rejects_out_of_range(int y, int m)
    {
        var act = () => DateValidator.EnsureYearMonth(y, m);
        act.Should().Throw<DateValidationException>();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement validator**

`src/Tsumugi.Application/Validation/DateValidationException.cs`:

```csharp
namespace Tsumugi.Application.Validation;

public sealed class DateValidationException(string message, string fieldName)
    : ArgumentException(message, fieldName)
{
    public string FieldName { get; } = fieldName;
}
```

`src/Tsumugi.Application/Validation/DateValidator.cs`:

```csharp
namespace Tsumugi.Application.Validation;

/// <summary>信頼境界の日付検証。UI の入力制限に依存しない。</summary>
public static class DateValidator
{
    private static readonly DateOnly Floor = new(1900, 1, 1);
    private static readonly DateOnly Ceiling = new(2200, 12, 31);

    public static void EnsureValid(DateOnly value, string fieldName)
    {
        if (value < Floor || value > Ceiling)
            throw new DateValidationException(
                $"日付が想定範囲外です（{Floor:yyyy-MM-dd}〜{Ceiling:yyyy-MM-dd}）。", fieldName);
    }

    public static void EnsureRange(DateOnly start, DateOnly? end, string fieldName)
    {
        EnsureValid(start, fieldName);
        if (end is not { } e) return;
        EnsureValid(e, fieldName);
        if (e < start)
            throw new DateValidationException("終了日は開始日以降である必要があります。", fieldName);
    }

    public static void EnsureYearMonth(int year, int month)
    {
        if (year < 1900 || year > 2200)
            throw new DateValidationException($"年が想定範囲外です（1900〜2200）。", "year");
        if (month < 1 || month > 12)
            throw new DateValidationException("月は1〜12の範囲で指定してください。", "month");
    }
}
```

- [ ] **Step 4: Re-run tests — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Application/Validation/ \
        tests/Tsumugi.Application.Tests/DateValidatorTests.cs
git commit -m "feat(application): add DateValidator for trust boundary (phase1/AC1-7)"
```

---

### Task 15: Recipient ユースケース（CRUD）

**Files:**
- Create: `src/Tsumugi.Application/Dtos/RecipientDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Recipient/RegisterRecipientUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Recipient/UpdateRecipientUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Recipient/ListRecipientsUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/RegisterRecipientUseCaseTests.cs`
- Test: `tests/Tsumugi.Application.Tests/UpdateRecipientUseCaseTests.cs`

- [ ] **Step 1: Write failing tests (with mock repository)**

`tests/Tsumugi.Application.Tests/RegisterRecipientUseCaseTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterRecipientUseCaseTests
{
    [Fact]
    public async Task Adds_recipient_with_generated_id_and_token()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var tp = new FixedTimeProvider(new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero));
        var sut = new RegisterRecipientUseCase(repo, uow, tp);

        var dto = await sut.ExecuteAsync(
            kanjiName: "山田太郎", kanaName: "ヤマダタロウ",
            dateOfBirth: new DateOnly(1990, 1, 1), actor: "tester", default);

        repo.Added.Should().ContainSingle();
        dto.KanjiName.Should().Be("山田太郎");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Rejects_blank_kanji_name()
    {
        var sut = new RegisterRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        Func<Task> act = () => sut.ExecuteAsync(" ", "x", new DateOnly(1990, 1, 1), "u", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Rejects_unrealistic_date_of_birth_via_validator()
    {
        var sut = new RegisterRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        Func<Task> act = () => sut.ExecuteAsync("山田", "ヤマダ", DateOnly.MinValue, "u", default);
        await act.Should().ThrowAsync<DateValidationException>();
    }
}

internal sealed class FakeRecipientRepository : IRecipientRepository
{
    public List<Recipient> Added { get; } = new();
    public Task AddAsync(Recipient r, CancellationToken ct) { Added.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Added.SingleOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct)
    {
        Added[Added.FindIndex(x => x.Id == r.Id)] = r;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Recipient>>(Added);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.FromResult(SaveCount); }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement DTO and use cases**

`src/Tsumugi.Application/Dtos/RecipientDto.cs`:

```csharp
namespace Tsumugi.Application.Dtos;

public sealed record RecipientDto(Guid Id, string KanjiName, string KanaName, DateOnly DateOfBirth);
```

`src/Tsumugi.Application/UseCases/Recipient/RegisterRecipientUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class RegisterRecipientUseCase(
    IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<RecipientDto> ExecuteAsync(
        string kanjiName, string kanaName, DateOnly dateOfBirth,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(kanjiName))
            throw new ArgumentException("氏名（漢字）は必須です。", nameof(kanjiName));
        if (string.IsNullOrWhiteSpace(kanaName))
            throw new ArgumentException("氏名（カナ）は必須です。", nameof(kanaName));
        DateValidator.EnsureValid(dateOfBirth, nameof(dateOfBirth));

        var entity = Domain.Entities.Recipient.Create(
            Guid.NewGuid(), kanjiName, kanaName, dateOfBirth,
            actor, clock.GetUtcNow(), Guid.NewGuid());

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return new RecipientDto(entity.Id, entity.KanjiName, entity.KanaName, entity.DateOfBirth);
    }
}
```

`src/Tsumugi.Application/UseCases/Recipient/UpdateRecipientUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Validation;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class UpdateRecipientUseCase(IRecipientRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task ExecuteAsync(
        Guid id, string kanjiName, string kanaName, DateOnly dateOfBirth,
        string actor, CancellationToken ct)
    {
        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("利用者が見つかりません。");
        if (string.IsNullOrWhiteSpace(kanjiName))
            throw new ArgumentException("氏名（漢字）は必須です。", nameof(kanjiName));
        DateValidator.EnsureValid(dateOfBirth, nameof(dateOfBirth));

        var updated = existing with { KanjiName = kanjiName, KanaName = kanaName, DateOfBirth = dateOfBirth };
        await repo.UpdateAsync(updated, ct);
        await uow.SaveChangesAsync(ct);
        _ = actor; _ = clock;  // 監査ログ拡張用フック（フェーズ1では使用しない）
    }
}
```

`src/Tsumugi.Application/UseCases/Recipient/ListRecipientsUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Recipient;

public sealed class ListRecipientsUseCase(IRecipientRepository repo)
{
    public async Task<IReadOnlyList<RecipientDto>> ExecuteAsync(CancellationToken ct)
    {
        var list = await repo.ListAsync(ct);
        return list.Select(r => new RecipientDto(r.Id, r.KanjiName, r.KanaName, r.DateOfBirth)).ToArray();
    }
}
```

- [ ] **Step 4: Re-run tests — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Application/Dtos/RecipientDto.cs \
        src/Tsumugi.Application/UseCases/Recipient/ \
        tests/Tsumugi.Application.Tests/RegisterRecipientUseCaseTests.cs
git commit -m "feat(application): add Recipient CRUD use cases (phase1/AC1-1,AC1-7)"
```

---

### Task 16: Certificate ユースケース（CRUD + 期間重複警告 + 期限アラート）

**Files:**
- Create: `src/Tsumugi.Application/Dtos/CertificateDto.cs`
- Create: `src/Tsumugi.Application/Dtos/ExpiringCertificateDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Certificate/RegisterCertificateUseCase.cs`（期間重複を**警告として返す**）
- Create: `src/Tsumugi.Application/UseCases/Certificate/ListExpiringCertificatesUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/RegisterCertificateUseCaseTests.cs`
- Test: `tests/Tsumugi.Application.Tests/ListExpiringCertificatesUseCaseTests.cs`

**Interfaces:**
- `RegisterCertificateUseCase.ExecuteAsync(...)` は `(CertificateDto, IReadOnlyList<string> warnings)` を返す。重複は警告（ブロックしない）、`docs/decisions/0005` に記載。
- `ListExpiringCertificatesUseCase.ExecuteAsync(asOf, thresholdDays)` は `Certificate` 全件を取得し `CertificatePolicy.FindExpiring` で抽出。

- [ ] **Step 1: Write failing test for register + warning**

```csharp
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterCertificateUseCaseTests
{
    [Fact]
    public async Task Adds_certificate_when_no_overlap()
    {
        var repo = new FakeCertificateRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterCertificateUseCase(repo, uow,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        var (dto, warnings) = await sut.ExecuteAsync(
            recipientId: rid, certificateNumber: "1234567890",
            validity: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            supplyDays: 22, monthlyCostCap: 9300, municipality: "杉並区",
            actor: "u", ct: default);

        warnings.Should().BeEmpty();
        repo.Added.Should().ContainSingle();
        dto.CertificateNumber.Should().Be("1234567890");
    }

    [Fact]
    public async Task Surfaces_warning_when_period_overlaps_existing()
    {
        var repo = new FakeCertificateRepository();
        var rid = Guid.NewGuid();
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), rid, "old",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new RegisterCertificateUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var (_, warnings) = await sut.ExecuteAsync(
            rid, "new",
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2027, 3, 31)),  // overlaps
            0, 0, "x", "u", default);

        warnings.Should().NotBeEmpty();
        warnings.Should().ContainMatch("*重複*");
    }
}

internal sealed class FakeCertificateRepository : ICertificateRepository
{
    public List<Certificate> Added { get; } = new();
    public Task AddAsync(Certificate c, CancellationToken ct) { Added.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(Added.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(Added);
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(Added.FirstOrDefault(c => c.RecipientId == rid && c.Validity.Contains(asOf)));
}
```

- [ ] **Step 2: Run test to verify fails**

- [ ] **Step 3: Implement DTOs and use cases**

`src/Tsumugi.Application/Dtos/CertificateDto.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record CertificateDto(
    Guid Id, Guid RecipientId, string CertificateNumber, DateRange Validity,
    int SupplyDays, int MonthlyCostCap, string Municipality);
```

`src/Tsumugi.Application/Dtos/ExpiringCertificateDto.cs`:

```csharp
namespace Tsumugi.Application.Dtos;

public sealed record ExpiringCertificateDto(
    Guid CertificateId, Guid RecipientId, string CertificateNumber,
    DateOnly ExpiryDate, int RemainingDays);
```

`src/Tsumugi.Application/UseCases/Certificate/RegisterCertificateUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed class RegisterCertificateUseCase(
    ICertificateRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(CertificateDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        Guid recipientId, string certificateNumber, DateRange validity,
        int supplyDays, int monthlyCostCap, string municipality,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber))
            throw new ArgumentException("受給者証番号は必須です。", nameof(certificateNumber));
        DateValidator.EnsureRange(validity.Start, validity.End, nameof(validity));

        var existing = await repo.ListByRecipientAsync(recipientId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Validity).Append(validity).ToArray();
        var overlaps = PeriodPolicy.DetectOverlaps(ranges);
        if (overlaps.Count > 0)
            warnings.Add("同一利用者の受給者証期間が重複しています。意図的か確認してください。");
        var gaps = PeriodPolicy.DetectGaps(ranges);
        if (gaps.Count > 0)
            warnings.Add("受給者証期間に空白があります。連続性を確認してください。");

        var entity = Domain.Entities.Certificate.Create(
            Guid.NewGuid(), recipientId, certificateNumber, validity,
            supplyDays, monthlyCostCap, municipality,
            actor, clock.GetUtcNow(), Guid.NewGuid());

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);

        return (new CertificateDto(entity.Id, entity.RecipientId, entity.CertificateNumber,
            entity.Validity, entity.SupplyDays, entity.MonthlyCostCap, entity.Municipality),
            warnings);
    }
}
```

`src/Tsumugi.Application/UseCases/Certificate/ListExpiringCertificatesUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed class ListExpiringCertificatesUseCase(ICertificateRepository repo)
{
    public async Task<IReadOnlyList<ExpiringCertificateDto>> ExecuteAsync(
        DateOnly asOf, int thresholdDays, CancellationToken ct)
    {
        var all = await repo.ListAllAsync(ct);
        var hits = CertificatePolicy.FindExpiring(all, asOf, thresholdDays);
        return hits.Select(h => new ExpiringCertificateDto(
            h.Certificate.Id, h.Certificate.RecipientId, h.Certificate.CertificateNumber,
            h.Certificate.Validity.End!.Value, h.RemainingDays)).ToArray();
    }
}
```

- [ ] **Step 4: Write test for `ListExpiringCertificatesUseCase`**

```csharp
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class ListExpiringCertificatesUseCaseTests
{
    [Fact]
    public async Task Returns_expiring_within_threshold()
    {
        var repo = new FakeCertificateRepository();
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "near",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "far",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new ListExpiringCertificatesUseCase(repo);
        var dtos = await sut.ExecuteAsync(new DateOnly(2026, 6, 27), 30, default);

        dtos.Should().ContainSingle();
        dtos[0].CertificateNumber.Should().Be("near");
        dtos[0].RemainingDays.Should().Be(4);
    }
}
```

- [ ] **Step 5: Re-run tests — verify PASS**

- [ ] **Step 6: Commit**

```bash
git add src/Tsumugi.Application/Dtos/CertificateDto.cs \
        src/Tsumugi.Application/Dtos/ExpiringCertificateDto.cs \
        src/Tsumugi.Application/UseCases/Certificate/ \
        tests/Tsumugi.Application.Tests/RegisterCertificateUseCaseTests.cs \
        tests/Tsumugi.Application.Tests/ListExpiringCertificatesUseCaseTests.cs
git commit -m "feat(application): add Certificate use cases with overlap warning and expiry alert (phase1/AC1-5)"
```

---

### Task 17: Contract / OfficeCapability ユースケース

**Files:**
- Create: `src/Tsumugi.Application/Dtos/ContractDto.cs`
- Create: `src/Tsumugi.Application/Dtos/OfficeCapabilityDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Contract/RegisterContractUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Contract/ListContractsByRecipientUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/OfficeCapability/RegisterOfficeCapabilityUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/OfficeCapability/ListOfficeCapabilitiesUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/RegisterContractUseCaseTests.cs`
- Test: `tests/Tsumugi.Application.Tests/RegisterOfficeCapabilityUseCaseTests.cs`

> 設計は Task 16 と同じ「期間マスタは追記、`Register` のみ、`Update` なし」。

- [ ] **Step 1: Write failing tests** (parallel to Task 16: register success, register with overlap warning)

各 `Register*UseCaseTests.cs` を Task 16 のテンプレートに従い、`Contract` 用と `OfficeCapability` 用に作成。Fakeリポジトリも各テストファイル内に置く。

- [ ] **Step 2: Implement DTOs and use cases**

`src/Tsumugi.Application/Dtos/ContractDto.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record ContractDto(Guid Id, Guid RecipientId, DateRange Period, int ContractedSupplyDays);
```

`src/Tsumugi.Application/Dtos/OfficeCapabilityDto.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record OfficeCapabilityDto(
    Guid Id, Guid OfficeId, DateRange Period, IReadOnlyDictionary<string, bool> Flags);
```

`src/Tsumugi.Application/UseCases/Contract/RegisterContractUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Contract;

public sealed class RegisterContractUseCase(
    IContractRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(ContractDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        Guid recipientId, DateRange period, int contractedSupplyDays,
        string actor, CancellationToken ct)
    {
        DateValidator.EnsureRange(period.Start, period.End, nameof(period));

        var existing = await repo.ListByRecipientAsync(recipientId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Period).Append(period).ToArray();
        if (PeriodPolicy.DetectOverlaps(ranges).Count > 0)
            warnings.Add("同一利用者の契約期間が重複しています。");

        var entity = Domain.Entities.Contract.Create(
            Guid.NewGuid(), recipientId, period, contractedSupplyDays,
            actor, clock.GetUtcNow(), Guid.NewGuid());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return (new ContractDto(entity.Id, entity.RecipientId, entity.Period, entity.ContractedSupplyDays),
                warnings);
    }
}
```

`src/Tsumugi.Application/UseCases/Contract/ListContractsByRecipientUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Contract;

public sealed class ListContractsByRecipientUseCase(IContractRepository repo)
{
    public async Task<IReadOnlyList<ContractDto>> ExecuteAsync(Guid recipientId, CancellationToken ct)
    {
        var list = await repo.ListByRecipientAsync(recipientId, ct);
        return list.Select(c => new ContractDto(c.Id, c.RecipientId, c.Period, c.ContractedSupplyDays)).ToArray();
    }
}
```

`src/Tsumugi.Application/UseCases/OfficeCapability/RegisterOfficeCapabilityUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.OfficeCapability;

public sealed class RegisterOfficeCapabilityUseCase(
    IOfficeCapabilityRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(OfficeCapabilityDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        Guid officeId, DateRange period, IReadOnlyDictionary<string, bool> flags,
        string actor, CancellationToken ct)
    {
        DateValidator.EnsureRange(period.Start, period.End, nameof(period));
        ArgumentNullException.ThrowIfNull(flags);

        var existing = await repo.ListByOfficeAsync(officeId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Period).Append(period).ToArray();
        if (PeriodPolicy.DetectOverlaps(ranges).Count > 0)
            warnings.Add("同一事業所の体制期間が重複しています。");

        var entity = Domain.Entities.OfficeCapability.Create(
            Guid.NewGuid(), officeId, period, flags,
            actor, clock.GetUtcNow(), Guid.NewGuid());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return (new OfficeCapabilityDto(entity.Id, entity.OfficeId, entity.Period, entity.Flags),
                warnings);
    }
}
```

`src/Tsumugi.Application/UseCases/OfficeCapability/ListOfficeCapabilitiesUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.OfficeCapability;

public sealed class ListOfficeCapabilitiesUseCase(IOfficeCapabilityRepository repo)
{
    public async Task<IReadOnlyList<OfficeCapabilityDto>> ExecuteAsync(Guid officeId, CancellationToken ct)
    {
        var list = await repo.ListByOfficeAsync(officeId, ct);
        return list.Select(c => new OfficeCapabilityDto(c.Id, c.OfficeId, c.Period, c.Flags)).ToArray();
    }
}
```

- [ ] **Step 3: Run tests — verify PASS**

- [ ] **Step 4: Commit**

```bash
git add src/Tsumugi.Application/Dtos/ContractDto.cs \
        src/Tsumugi.Application/Dtos/OfficeCapabilityDto.cs \
        src/Tsumugi.Application/UseCases/Contract/ \
        src/Tsumugi.Application/UseCases/OfficeCapability/ \
        tests/Tsumugi.Application.Tests/RegisterContractUseCaseTests.cs \
        tests/Tsumugi.Application.Tests/RegisterOfficeCapabilityUseCaseTests.cs
git commit -m "feat(application): add Contract and OfficeCapability use cases (phase1/AC1-1,AC1-9)"
```

---

### Task 18: DailyRecord ユースケース（Record / Correct / Cancel / QueryMonth）

**Files:**
- Create: `src/Tsumugi.Application/Dtos/DailyRecordDto.cs`
- Create: `src/Tsumugi.Application/UseCases/DailyRecord/RecordDailyRecordUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/DailyRecord/CorrectDailyRecordUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/DailyRecord/CancelDailyRecordUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/DailyRecord/QueryMonthDailyRecordsUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/DailyRecordUseCaseTests.cs`

**Interfaces:**
- `Record(recipientId, serviceDate, attendance, transport, mealProvided, note, actor)` — 新規追加（既存があれば例外）
- `Correct(originId, attendance, transport, mealProvided, note, actor)` — 訂正（originIdから読んで新行追加）
- `Cancel(originId, actor)` — 取消
- `QueryMonth(recipientId, year, month)` — `EffectiveByDate` を返す

- [ ] **Step 1: Write failing tests**

`tests/Tsumugi.Application.Tests/DailyRecordUseCaseTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class DailyRecordUseCaseTests
{
    private readonly FakeDailyRecordRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Record_appends_new()
    {
        var sut = new RecordDailyRecordUseCase(_repo, _uow, _clock);
        var rid = Guid.NewGuid();
        var dto = await sut.ExecuteAsync(rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.Round, true, "通常", "u", default);
        _repo.Added.Should().ContainSingle();
        dto.Kind.Should().Be(RecordKind.New);
    }

    [Fact]
    public async Task Correct_appends_correction_with_origin()
    {
        var rid = Guid.NewGuid();
        var origin = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        _repo.Added.Add(origin);

        var sut = new CorrectDailyRecordUseCase(_repo, _uow, _clock);
        var dto = await sut.ExecuteAsync(origin.Id, Attendance.Absent, TransportKind.None, false, "病欠", "u", default);

        dto.Kind.Should().Be(RecordKind.Correct);
        dto.OriginId.Should().Be(origin.Id);
        _repo.Added.Count.Should().Be(2);
    }

    [Fact]
    public async Task Cancel_appends_cancellation()
    {
        var rid = Guid.NewGuid();
        var origin = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        _repo.Added.Add(origin);

        var sut = new CancelDailyRecordUseCase(_repo, _uow, _clock);
        var dto = await sut.ExecuteAsync(origin.Id, "u", default);
        dto.Kind.Should().Be(RecordKind.Cancel);
    }

    [Fact]
    public async Task QueryMonth_returns_effective_records()
    {
        var rid = Guid.NewGuid();
        var n = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var c = DailyRecord.Correction(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1), n.Id,
            Attendance.Absent, TransportKind.None, false, null, "u",
            DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { n, c });

        var sut = new QueryMonthDailyRecordsUseCase(_repo);
        var dtos = await sut.ExecuteAsync(rid, 2026, 6, default);
        dtos.Should().ContainSingle();
        dtos[new DateOnly(2026, 6, 1)].Attendance.Should().Be(Attendance.Absent);
    }
}

internal sealed class FakeDailyRecordRepository : IDailyRecordRepository
{
    public List<DailyRecord> Added { get; } = new();
    public Task AddAsync(DailyRecord r, CancellationToken ct) { Added.Add(r); return Task.CompletedTask; }
    public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Added.SingleOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(Guid rid, DateOnly d, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyRecord>>(
            Added.Where(r => r.RecipientId == rid && r.ServiceDate == d).ToArray());
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(Guid rid, int y, int m, CancellationToken ct)
    {
        var from = new DateOnly(y, m, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return Task.FromResult<IReadOnlyList<DailyRecord>>(
            Added.Where(r => r.RecipientId == rid && r.ServiceDate >= from && r.ServiceDate <= to).ToArray());
    }
}
```

- [ ] **Step 2: Implement DTO and use cases**

`src/Tsumugi.Application/Dtos/DailyRecordDto.cs`:

```csharp
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record DailyRecordDto(
    Guid Id, Guid RecipientId, DateOnly ServiceDate,
    RecordKind Kind, Guid? OriginId,
    Attendance Attendance, TransportKind Transport, bool MealProvided, string? Note);
```

`src/Tsumugi.Application/UseCases/DailyRecord/RecordDailyRecordUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class RecordDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(
        Guid recipientId, DateOnly serviceDate,
        Attendance attendance, TransportKind transport, bool mealProvided, string? note,
        string actor, CancellationToken ct)
    {
        DateValidator.EnsureValid(serviceDate, nameof(serviceDate));

        var existing = await repo.ListByRecipientAndDateAsync(recipientId, serviceDate, ct);
        if (existing.Any(r => r.Kind == Domain.Enums.RecordKind.New))
            throw new InvalidOperationException("同一日に新規記録が既に存在します。訂正または取消を使用してください。");

        var entity = Domain.Entities.DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, serviceDate,
            attendance, transport, mealProvided, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    internal static DailyRecordDto Map(Domain.Entities.DailyRecord e) =>
        new(e.Id, e.RecipientId, e.ServiceDate, e.Kind, e.OriginId,
            e.Attendance, e.Transport, e.MealProvided, e.Note);
}
```

`src/Tsumugi.Application/UseCases/DailyRecord/CorrectDailyRecordUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class CorrectDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(
        Guid originId, Attendance attendance, TransportKind transport, bool mealProvided,
        string? note, string actor, CancellationToken ct)
    {
        var origin = await repo.FindByIdAsync(originId, ct)
            ?? throw new InvalidOperationException("訂正元レコードが見つかりません。");

        var entity = Domain.Entities.DailyRecord.Correction(
            Guid.NewGuid(), origin.RecipientId, origin.ServiceDate, originId,
            attendance, transport, mealProvided, note,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordDailyRecordUseCase.Map(entity);
    }
}
```

`src/Tsumugi.Application/UseCases/DailyRecord/CancelDailyRecordUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class CancelDailyRecordUseCase(
    IDailyRecordRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DailyRecordDto> ExecuteAsync(Guid originId, string actor, CancellationToken ct)
    {
        var origin = await repo.FindByIdAsync(originId, ct)
            ?? throw new InvalidOperationException("取消元レコードが見つかりません。");

        var entity = Domain.Entities.DailyRecord.Cancellation(
            Guid.NewGuid(), origin.RecipientId, origin.ServiceDate, originId,
            actor, clock.GetUtcNow());
        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return RecordDailyRecordUseCase.Map(entity);
    }
}
```

`src/Tsumugi.Application/UseCases/DailyRecord/QueryMonthDailyRecordsUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Logic;

namespace Tsumugi.Application.UseCases.DailyRecord;

public sealed class QueryMonthDailyRecordsUseCase(IDailyRecordRepository repo)
{
    public async Task<IReadOnlyDictionary<DateOnly, DailyRecordDto>> ExecuteAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        DateValidator.EnsureYearMonth(year, month);
        var raw = await repo.ListByRecipientAndMonthAsync(recipientId, year, month, ct);
        var effective = DailyRecordPolicy.EffectiveByDate(raw);
        return effective.ToDictionary(
            kv => kv.Key,
            kv => RecordDailyRecordUseCase.Map(kv.Value));
    }
}
```

- [ ] **Step 3: Re-run tests — verify PASS**

- [ ] **Step 4: Commit**

```bash
git add src/Tsumugi.Application/Dtos/DailyRecordDto.cs \
        src/Tsumugi.Application/UseCases/DailyRecord/ \
        tests/Tsumugi.Application.Tests/DailyRecordUseCaseTests.cs
git commit -m "feat(application): add DailyRecord use cases (record/correct/cancel/query) (phase1/AC1-3)"
```

---

### Task 19: Office 拡張ユースケース（UpdateOffice / ListOffices）＋ 楽観ロック競合テスト

**Files:**
- Modify: `src/Tsumugi.Application/UseCases/RegisterOfficeUseCase.cs`（`ServiceCategory`/`RegionGrade` 引数を追加。Task 2 で `RegionGrade.None` を仮置きしたものを引数化）
- Create: `src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Office/ListOfficesUseCase.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/OptimisticConcurrencyTests.cs`

- [ ] **Step 1: Add UpdateOffice + ListOffices use cases**

`src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Office;

public sealed class UpdateOfficeUseCase(IOfficeRepository repo, IUnitOfWork uow)
{
    public async Task ExecuteAsync(
        Guid id, string name, ServiceCategory category, RegionGrade region,
        CancellationToken ct)
    {
        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("事業所が見つかりません。");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));

        var updated = existing with { Name = name, ServiceCategory = category, RegionGrade = region };
        await repo.UpdateAsync(updated, ct);
        await uow.SaveChangesAsync(ct);
    }
}
```

`src/Tsumugi.Application/UseCases/Office/ListOfficesUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Office;

public sealed class ListOfficesUseCase(IOfficeRepository repo)
{
    public async Task<IReadOnlyList<OfficeDto>> ExecuteAsync(CancellationToken ct)
    {
        var list = await repo.ListAsync(ct);
        return list.Select(o => new OfficeDto(o.Id, o.OfficeNumber, o.Name)).ToArray();
    }
}
```

- [ ] **Step 2: Extend `RegisterOfficeUseCase` signature**

引数に `ServiceCategory category, RegionGrade region` を追加し、デフォルトは付けない（既存呼び出しはなし）。Task 2 で `RegionGrade.None` 仮置きしていた箇所も同時に書き換える。

- [ ] **Step 3: Write optimistic concurrency test against real SQLite**

`tests/Tsumugi.Infrastructure.Tests/OptimisticConcurrencyTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OptimisticConcurrencyTests : IClassFixture<TempDbFixture>
{
    private readonly TempDbFixture _fixture;
    public OptimisticConcurrencyTests(TempDbFixture f) => _fixture = f;

    [Fact]
    public async Task Concurrent_Office_update_throws_DbUpdateConcurrencyException()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Offices.Add(Office.Create(id, "1", "x",
                ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync();
        }

        await using var ctxA = _fixture.NewContext();
        await using var ctxB = _fixture.NewContext();
        var a = await ctxA.Offices.SingleAsync(o => o.Id == id);
        var b = await ctxB.Offices.SingleAsync(o => o.Id == id);

        ctxA.Entry(a).Property(nameof(Office.Name)).CurrentValue = "A";
        ctxA.Entry(a).Property(nameof(Office.Name)).IsModified = true;
        await ctxA.SaveChangesAsync();

        ctxB.Entry(b).Property(nameof(Office.Name)).CurrentValue = "B";
        ctxB.Entry(b).Property(nameof(Office.Name)).IsModified = true;

        Func<Task> act = () => ctxB.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
```

- [ ] **Step 4: Run tests — verify PASS**

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.Application/UseCases/Office/ \
        src/Tsumugi.Application/UseCases/RegisterOfficeUseCase.cs \
        tests/Tsumugi.Infrastructure.Tests/OptimisticConcurrencyTests.cs
git commit -m "feat(application): add Office update/list use cases and optimistic concurrency test (phase1/AC1-6)"
```

---

## Group D — App（Avalonia MVVM）

> ViewModel テスト先行。View は ViewModel が緑になってから組む（仕様 §8）。
> ダークテーマ・低アニメーション既定・キーボード操作完結はテーマレベルで実装する。

### Task 20: UI 既定設定（ダーク・低アニメ）と CompositionRoot 拡張

**Files:**
- Create: `src/Tsumugi.App/Settings/UiDefaults.cs`（テーマ/アニメ既定）
- Modify: `src/Tsumugi.App/App.axaml`（ダークを既定）
- Modify: `src/Tsumugi.App/App.axaml.cs`（CompositionRoot 経由でユースケースを登録）
- Modify: `src/Tsumugi.App/CompositionRoot.cs`（既存。Application 層のユースケースを登録）
- Test: `tests/Tsumugi.App.Tests/UiDefaultsTests.cs`

- [ ] **Step 1: Write failing test for `UiDefaults`**

`tests/Tsumugi.App.Tests/UiDefaultsTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.App.Settings;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class UiDefaultsTests
{
    [Fact]
    public void Default_theme_is_dark()
    {
        UiDefaults.DefaultTheme.Should().Be(UiTheme.Dark);
    }

    [Fact]
    public void Reduced_motion_is_on_by_default()
    {
        UiDefaults.ReducedMotion.Should().BeTrue();
    }

    [Fact]
    public void Default_font_size_is_accessible_minimum_14()
    {
        UiDefaults.MinimumFontSize.Should().BeGreaterThanOrEqualTo(14);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `UiDefaults`**

`src/Tsumugi.App/Settings/UiDefaults.cs`:

```csharp
namespace Tsumugi.App.Settings;

public enum UiTheme { Dark, Light }

/// <summary>
/// アクセシビリティ既定（仕様 §4.6）: ダークテーマ・低アニメーション・フォント拡大追従。
/// 利用者設定で上書き可能だが既定値は常にここに固定する。
/// </summary>
public static class UiDefaults
{
    public const UiTheme DefaultTheme = UiTheme.Dark;
    public const bool ReducedMotion = true;
    public const int MinimumFontSize = 14;
}
```

- [ ] **Step 4: Update `App.axaml` to use dark theme by default**

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tsumugi.App.App"
             RequestedThemeVariant="Dark">
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

- [ ] **Step 5: Run test — verify PASS**

- [ ] **Step 6: Commit**

```bash
git add src/Tsumugi.App/Settings/UiDefaults.cs src/Tsumugi.App/App.axaml \
        tests/Tsumugi.App.Tests/UiDefaultsTests.cs
git commit -m "feat(app): default to dark theme and reduced motion (phase1/AC1-8)"
```

---

### Task 21: `RecipientListViewModel` / `RecipientEditViewModel`

**Files:**
- Create: `src/Tsumugi.App/ViewModels/ViewModelBase.cs`（`ObservableObject` ベース）
- Create: `src/Tsumugi.App/ViewModels/RecipientListViewModel.cs`
- Create: `src/Tsumugi.App/ViewModels/RecipientEditViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/RecipientListViewModelTests.cs`
- Create: `tests/Tsumugi.App.Tests/RecipientEditViewModelTests.cs`

**Interfaces:**
- `RecipientListViewModel(ListRecipientsUseCase)` — `LoadAsync()` を呼ぶと `Items` がリストで埋まる
- `RecipientEditViewModel(RegisterRecipientUseCase)` — `SaveCommand` が新規登録、`SaveErrorMessage` にバリデーションエラー文言

- [ ] **Step 1: Write failing test for `RecipientListViewModel`**

```csharp
using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class RecipientListViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_items_from_use_case()
    {
        var repo = new InMemoryRecipientRepo();
        repo.Add(Recipient.Create(Guid.NewGuid(), "山田", "ヤマダ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var vm = new RecipientListViewModel(new ListRecipientsUseCase(repo));
        await vm.LoadAsync();
        vm.Items.Should().ContainSingle(r => r.KanjiName == "山田");
    }
}

internal sealed class InMemoryRecipientRepo : IRecipientRepository
{
    private readonly List<Recipient> _list = new();
    public void Add(Recipient r) => _list.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { _list.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_list.SingleOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Recipient>>(_list);
}
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement `ViewModelBase` and `RecipientListViewModel`**

`src/Tsumugi.App/ViewModels/ViewModelBase.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tsumugi.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject { }
```

`src/Tsumugi.App/ViewModels/RecipientListViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientListViewModel(ListRecipientsUseCase listUseCase) : ViewModelBase
{
    public ObservableCollection<RecipientDto> Items { get; } = new();

    [ObservableProperty]
    private RecipientDto? _selected;

    public async Task LoadAsync()
    {
        Items.Clear();
        var list = await listUseCase.ExecuteAsync(default);
        foreach (var r in list) Items.Add(r);
    }
}
```

- [ ] **Step 4: Re-run test — verify PASS**

- [ ] **Step 5: Write failing test for `RecipientEditViewModel`**

```csharp
using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Recipient;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class RecipientEditViewModelTests
{
    [Fact]
    public async Task SaveCommand_with_valid_input_registers_and_clears_error()
    {
        var repo = new InMemoryRecipientRepo();
        var uow = new InMemoryUow();
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(repo, uow,
                new FixedClock(DateTimeOffset.UnixEpoch)))
        {
            KanjiName = "山田太郎",
            KanaName = "ヤマダタロウ",
            DateOfBirth = new DateOnly(1990, 1, 1),
        };

        await sut.SaveCommand.ExecuteAsync(null);

        sut.SaveErrorMessage.Should().BeNull();
        sut.IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task SaveCommand_with_blank_kanji_sets_error_message_in_friendly_text()
    {
        var sut = new RecipientEditViewModel(
            new RegisterRecipientUseCase(new InMemoryRecipientRepo(), new InMemoryUow(),
                new FixedClock(DateTimeOffset.UnixEpoch)))
        {
            KanjiName = "",
            KanaName = "ヤマダ",
            DateOfBirth = new DateOnly(1990, 1, 1),
        };

        await sut.SaveCommand.ExecuteAsync(null);

        sut.SaveErrorMessage.Should().Contain("氏名");  // 利用者を責めない平易な文言
        sut.IsSaved.Should().BeFalse();
    }
}

internal sealed class InMemoryUow : Tsumugi.Application.Abstractions.IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
}
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
```

- [ ] **Step 6: Implement `RecipientEditViewModel`**

`src/Tsumugi.App/ViewModels/RecipientEditViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.UseCases.Recipient;

namespace Tsumugi.App.ViewModels;

public sealed partial class RecipientEditViewModel(RegisterRecipientUseCase registerUseCase) : ViewModelBase
{
    [ObservableProperty] private string _kanjiName = string.Empty;
    [ObservableProperty] private string _kanaName = string.Empty;
    [ObservableProperty] private DateOnly _dateOfBirth = new(1990, 1, 1);
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private bool _isSaved;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await registerUseCase.ExecuteAsync(
                KanjiName, KanaName, DateOfBirth, actor: Environment.UserName, default);
            SaveErrorMessage = null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }
}
```

- [ ] **Step 7: Re-run tests — verify PASS**

- [ ] **Step 8: Commit**

```bash
git add src/Tsumugi.App/ViewModels/ \
        tests/Tsumugi.App.Tests/RecipientListViewModelTests.cs \
        tests/Tsumugi.App.Tests/RecipientEditViewModelTests.cs
git commit -m "feat(app): add Recipient ViewModels (phase1/AC1-8)"
```

---

### Task 22: `CertificateViewModel`（一覧 + 期限アラート）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/CertificateViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/CertificateViewModelTests.cs`

**Interfaces:**
- 受給者証一覧 `Items: ObservableCollection<CertificateDto>`
- 期限アラート一覧 `ExpiringItems: ObservableCollection<ExpiringCertificateDto>`
- `LoadAsync(asOf, thresholdDays)` 一発で両方更新

- [ ] **Step 1: Write failing test**

```csharp
using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CertificateViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_expiring_items()
    {
        var repo = new InMemoryCertRepo();
        repo.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "near",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new CertificateViewModel(new ListExpiringCertificatesUseCase(repo));
        await sut.LoadAsync(new DateOnly(2026, 6, 27), thresholdDays: 30);

        sut.ExpiringItems.Should().ContainSingle();
        sut.ExpiringItems[0].RemainingDays.Should().Be(4);
    }
}

internal sealed class InMemoryCertRepo : Tsumugi.Application.Abstractions.ICertificateRepository
{
    private readonly List<Certificate> _list = new();
    public void Add(Certificate c) => _list.Add(c);
    public Task AddAsync(Certificate c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list);
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult<Certificate?>(null);
}
```

- [ ] **Step 2: Implement `CertificateViewModel`**

```csharp
using System.Collections.ObjectModel;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Certificate;

namespace Tsumugi.App.ViewModels;

public sealed partial class CertificateViewModel(
    ListExpiringCertificatesUseCase listExpiring) : ViewModelBase
{
    public ObservableCollection<ExpiringCertificateDto> ExpiringItems { get; } = new();

    public async Task LoadAsync(DateOnly asOf, int thresholdDays)
    {
        ExpiringItems.Clear();
        var hits = await listExpiring.ExecuteAsync(asOf, thresholdDays, default);
        foreach (var h in hits) ExpiringItems.Add(h);
    }
}
```

- [ ] **Step 3: Run tests — verify PASS**

- [ ] **Step 4: Commit**

```bash
git add src/Tsumugi.App/ViewModels/CertificateViewModel.cs \
        tests/Tsumugi.App.Tests/CertificateViewModelTests.cs
git commit -m "feat(app): add Certificate ViewModel with expiry alerts (phase1/AC1-5,AC1-8)"
```

---

### Task 23: `DailyRecordViewModel`（月グリッド + 訂正・取消コマンド）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/DailyRecordViewModel.cs`
- Create: `src/Tsumugi.App/ViewModels/DailyCellViewModel.cs`（日セル）
- Create: `tests/Tsumugi.App.Tests/DailyRecordViewModelTests.cs`

**Interfaces:**
- `DailyRecordViewModel(RecordDailyRecordUseCase, CorrectDailyRecordUseCase, CancelDailyRecordUseCase, QueryMonthDailyRecordsUseCase)`
- `SetRecipient(Guid)`, `SetMonth(int year, int month)`, `LoadAsync()`
- `Cells: ObservableCollection<DailyCellViewModel>` — 月の日数ぶん
- 各 `DailyCellViewModel` に `RecordCommand` / `CorrectCommand` / `CancelCommand`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class DailyRecordViewModelTests
{
    private readonly FakeDailyRecordRepo _repo = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private DailyRecordViewModel NewVm() => new(
        new RecordDailyRecordUseCase(_repo, _uow, _clock),
        new CorrectDailyRecordUseCase(_repo, _uow, _clock),
        new CancelDailyRecordUseCase(_repo, _uow, _clock),
        new QueryMonthDailyRecordsUseCase(_repo));

    [Fact]
    public async Task LoadAsync_creates_cell_per_day_of_month()
    {
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();
        vm.Cells.Should().HaveCount(30);
    }

    [Fact]
    public async Task Record_then_query_shows_effective_attendance()
    {
        var vm = NewVm();
        var rid = Guid.NewGuid();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        var cell = vm.Cells[0];  // 6/1
        await cell.RecordCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();
        vm.Cells[0].EffectiveAttendance.Should().Be(Attendance.Present);
    }

    [Fact]
    public async Task Cancel_makes_effective_attendance_null_no_destructive_update()
    {
        var vm = NewVm();
        var rid = Guid.NewGuid();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        await vm.Cells[0].RecordCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();
        await vm.Cells[0].CancelCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        vm.Cells[0].EffectiveAttendance.Should().BeNull();
        _repo.Added.Count.Should().Be(2);  // 元レコードは残り、追記で取消行が追加
    }
}
```

- [ ] **Step 2: Implement ViewModels**

`src/Tsumugi.App/ViewModels/DailyCellViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Domain.Enums;

namespace Tsumugi.App.ViewModels;

public sealed partial class DailyCellViewModel(
    Guid recipientId,
    DateOnly date,
    RecordDailyRecordUseCase record,
    CorrectDailyRecordUseCase correct,
    CancelDailyRecordUseCase cancel,
    Func<Task> reload) : ViewModelBase
{
    public DateOnly Date { get; } = date;

    [ObservableProperty] private Guid? _effectiveId;
    [ObservableProperty] private Attendance? _effectiveAttendance;
    [ObservableProperty] private TransportKind _effectiveTransport;
    [ObservableProperty] private bool _effectiveMealProvided;

    [RelayCommand]
    private async Task RecordAsync(Attendance attendance)
    {
        await record.ExecuteAsync(recipientId, Date,
            attendance, TransportKind.None, mealProvided: false, note: null,
            actor: Environment.UserName, default);
        await reload();
    }

    [RelayCommand]
    private async Task CorrectAsync(Attendance attendance)
    {
        if (EffectiveId is null) return;
        await correct.ExecuteAsync(EffectiveId.Value,
            attendance, EffectiveTransport, EffectiveMealProvided, note: null,
            actor: Environment.UserName, default);
        await reload();
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (EffectiveId is null) return;
        await cancel.ExecuteAsync(EffectiveId.Value, Environment.UserName, default);
        await reload();
    }
}
```

`src/Tsumugi.App/ViewModels/DailyRecordViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using Tsumugi.Application.UseCases.DailyRecord;

namespace Tsumugi.App.ViewModels;

public sealed partial class DailyRecordViewModel(
    RecordDailyRecordUseCase record,
    CorrectDailyRecordUseCase correct,
    CancelDailyRecordUseCase cancel,
    QueryMonthDailyRecordsUseCase query) : ViewModelBase
{
    private Guid _recipientId;
    private int _year;
    private int _month;

    public ObservableCollection<DailyCellViewModel> Cells { get; } = new();

    public void SetRecipient(Guid id) => _recipientId = id;
    public void SetMonth(int year, int month) { _year = year; _month = month; }

    public async Task LoadAsync()
    {
        Cells.Clear();
        var daysInMonth = DateTime.DaysInMonth(_year, _month);
        var effective = await query.ExecuteAsync(_recipientId, _year, _month, default);
        for (var d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(_year, _month, d);
            var cell = new DailyCellViewModel(_recipientId, date, record, correct, cancel, LoadAsync);
            if (effective.TryGetValue(date, out var dto))
            {
                cell.EffectiveId = dto.Id;
                cell.EffectiveAttendance = dto.Attendance;
                cell.EffectiveTransport = dto.Transport;
                cell.EffectiveMealProvided = dto.MealProvided;
            }
            Cells.Add(cell);
        }
    }
}
```

- [ ] **Step 3: Run tests — verify PASS**

- [ ] **Step 4: Commit**

```bash
git add src/Tsumugi.App/ViewModels/DailyCellViewModel.cs \
        src/Tsumugi.App/ViewModels/DailyRecordViewModel.cs \
        tests/Tsumugi.App.Tests/DailyRecordViewModelTests.cs
git commit -m "feat(app): add DailyRecord ViewModel with record/correct/cancel commands (phase1/AC1-3,AC1-8)"
```

---

### Task 24: 残りの ViewModel（Contract / Office・Capability）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/ContractViewModel.cs`
- Create: `src/Tsumugi.App/ViewModels/OfficeViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/ContractViewModelTests.cs`
- Create: `tests/Tsumugi.App.Tests/OfficeViewModelTests.cs`

> Task 21〜22 と同形のパターン（`LoadAsync` + 入力プロパティ + `SaveCommand`）。各 ViewModel ごとに「ロード」「登録時の警告」「バリデーションエラーの平易な文言」をテスト。

- [ ] **Step 1: Implement Contract VM + tests** (Task 17 と同形)
- [ ] **Step 2: Implement Office VM + tests** (`RegisterOfficeUseCase` / `UpdateOfficeUseCase` / `ListOfficesUseCase` を呼ぶ)
- [ ] **Step 3: Run tests — verify PASS**
- [ ] **Step 4: Commit**

```bash
git add src/Tsumugi.App/ViewModels/ContractViewModel.cs \
        src/Tsumugi.App/ViewModels/OfficeViewModel.cs \
        tests/Tsumugi.App.Tests/ContractViewModelTests.cs \
        tests/Tsumugi.App.Tests/OfficeViewModelTests.cs
git commit -m "feat(app): add Contract and Office ViewModels (phase1/AC1-1,AC1-8)"
```

---

### Task 25: View（AXAML）とナビゲーション・キーボード操作

**Files:**
- Create: `src/Tsumugi.App/Views/RecipientListView.axaml(.cs)`
- Create: `src/Tsumugi.App/Views/RecipientEditView.axaml(.cs)`
- Create: `src/Tsumugi.App/Views/CertificateView.axaml(.cs)`
- Create: `src/Tsumugi.App/Views/ContractView.axaml(.cs)`
- Create: `src/Tsumugi.App/Views/OfficeView.axaml(.cs)`
- Create: `src/Tsumugi.App/Views/DailyRecordView.axaml(.cs)`
- Modify: `src/Tsumugi.App/MainWindow.axaml(.cs)` — `TabControl` でナビゲーション、各タブにアクセスキー `Ctrl+1〜6`
- Modify: `src/Tsumugi.App/CompositionRoot.cs` — ViewModel を DI に登録、`MainViewModel` を介して注入

**画面要件**（仕様 §4.6 と CLAUDE.md §ハード制約5）:
- 利用者一覧: `DataGrid` で `KanjiName`/`KanaName`/`DateOfBirth`。`Enter` で編集。
- 利用者編集: `TextBox` + 日付ピッカー、`Ctrl+S` で保存、エラー表示は赤系コントラスト確保。
- 受給者証: 一覧 + 「期限アラート（しきい値=30日）」ペイン。日付セルに残日数。
- 契約 / 事業所 / 体制: フォーム入力。
- 日次記録: 月グリッド（横軸＝1〜31日、縦＝項目）。各セルで `Space` 出席切替、`Backspace` 取消、`F2` 訂正。

- [ ] **Step 1: Implement Views (AXAML + code-behind)**

各 `.axaml` は ViewModel をバインディング、キーバインディング (`KeyBinding`) で頻用操作を完結させる。

`src/Tsumugi.App/Views/RecipientListView.axaml` 例:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tsumugi.App.ViewModels"
             x:Class="Tsumugi.App.Views.RecipientListView"
             x:DataType="vm:RecipientListViewModel">
    <UserControl.KeyBindings>
        <KeyBinding Gesture="F5" Command="{Binding LoadCommand}" />
    </UserControl.KeyBindings>
    <DataGrid ItemsSource="{Binding Items}" SelectedItem="{Binding Selected}"
              AutoGenerateColumns="False" CanUserResizeColumns="True">
        <DataGrid.Columns>
            <DataGridTextColumn Header="氏名" Binding="{Binding KanjiName}" />
            <DataGridTextColumn Header="氏名（カナ）" Binding="{Binding KanaName}" />
            <DataGridTextColumn Header="生年月日" Binding="{Binding DateOfBirth}" />
        </DataGrid.Columns>
    </DataGrid>
</UserControl>
```

> 残りのビューも同パターン。`F5` で再読込、`Ctrl+S` で保存、`Esc` で破棄、を全画面で統一。

- [ ] **Step 2: Modify `MainWindow.axaml` with TabControl navigation**

```xml
<TabControl>
    <TabItem Header="利用者(_1)"><views:RecipientListView /></TabItem>
    <TabItem Header="受給者証(_2)"><views:CertificateView /></TabItem>
    <TabItem Header="契約(_3)"><views:ContractView /></TabItem>
    <TabItem Header="日次記録(_4)"><views:DailyRecordView /></TabItem>
    <TabItem Header="事業所(_5)"><views:OfficeView /></TabItem>
</TabControl>
```

`Alt+1〜5` でタブ切り替え（`_1` はアクセスキー）。

- [ ] **Step 3: Modify `CompositionRoot` to register ViewModels**

```csharp
services.AddTransient<RecipientListViewModel>();
services.AddTransient<RecipientEditViewModel>();
services.AddTransient<CertificateViewModel>();
services.AddTransient<ContractViewModel>();
services.AddTransient<OfficeViewModel>();
services.AddTransient<DailyRecordViewModel>();
services.AddTransient<MainViewModel>();
```

- [ ] **Step 4: Manual smoke (cross-platform check)**

> UI verification: 開発機（macOS）で `dotnet run --project src/Tsumugi.App` を起動し、各タブにキーで遷移できる／登録→一覧反映／期限アラートが表示される／日次記録の訂正後に表示が変わる、を目視確認。CI では Off-screen テストできないため、ViewModel テストで担保。

- [ ] **Step 5: Commit**

```bash
git add src/Tsumugi.App/Views/ src/Tsumugi.App/MainWindow.axaml \
        src/Tsumugi.App/MainWindow.axaml.cs src/Tsumugi.App/CompositionRoot.cs \
        src/Tsumugi.App/ViewModels/MainViewModel.cs
git commit -m "feat(app): add views and tabbed navigation with keyboard shortcuts (phase1/AC1-8)"
```

---

## Group E — 設計判断（ADR）＋ open-questions 更新 ＋ 受け入れ確認

### Task 26: ADR 0004 — 出欠を日次記録に統合

**Files:**
- Create: `docs/decisions/0004-attendance-integrated-into-daily-record.md`

- [ ] **Step 1: Write ADR**

```markdown
# ADR 0004: 出欠を日次記録の属性として統合する（独立集約にしない）

- 結論: 出欠（出席/欠席/欠席時対応/中止 等）は `DailyRecord` の `Attendance` 列として持つ。独立集約は作らない。
- 背景: 親文書 §4.3 では出欠を別エンティティ候補としていたが、フェーズ1指示書 §3.3 で日次記録に統合する方針に変更。
- 選択肢:
  - (a) 出欠を `DailyRecord` の属性として保持（採用）。
  - (b) `AttendanceRecord` を独立集約とし、`DailyRecord` から参照。
- 決定: (a)。理由: 同一日に「実績」と「出欠」が独立して変化しない／集約境界を減らし状態追跡を簡素化（CLAUDE.md §2.4 認知特性方針）／訂正・取消が両者に対し同形となるため追記不変ガードを一元化できる。
- 影響:
  - `Attendance` 列挙は Domain に置く（`Tsumugi.Domain.Enums`）。
  - 月次ビューの実効状態導出は `DailyRecordPolicy.EffectiveByDate` 1本でカバー。
  - 将来「欠席連絡の到着時刻」など独立属性が必要になれば再評価。
```

- [ ] **Step 2: Commit**

```bash
git add docs/decisions/0004-attendance-integrated-into-daily-record.md
git commit -m "docs(adr): 0004 attendance integrated into DailyRecord"
```

---

### Task 27: ADR 0005 — 受給者証期間重複は警告（ブロックしない）

**Files:**
- Create: `docs/decisions/0005-certificate-period-overlap-policy.md`

- [ ] **Step 1: Write ADR**

```markdown
# ADR 0005: 受給者証・契約・体制の期間重複は「警告」とする（ハードブロックしない）

- 結論: 同一利用者／同一事業所内で期間が重複・空白を持つ登録は、ユースケースから警告として surfacing するが**保存はブロックしない**。
- 背景: 現場運用では受給者証の更新タイミングで一時的に期間が重なることがある／市町村変更で旧証・新証が並ぶケースがある／空白も短期で許容される場面がある。フェーズ1で機械的にブロックすると業務継続を妨げる。
- 選択肢:
  - (a) 警告（採用）。`PeriodPolicy.DetectOverlaps`/`DetectGaps` の結果をユースケース戻り値の `Warnings` として返す。
  - (b) ハードブロック。重複・空白を保存時に例外で落とす。
  - (c) 完全に無検査。
- 決定: (a)。UI は警告メッセージを目立たせ、利用者が意図的に承認できる導線を持つ。
- 影響:
  - 各 `Register*UseCase` が `(Dto, IReadOnlyList<string> Warnings)` を返す。
  - 将来「ブロックすべき」業務ルールが明確化されれば本 ADR を上書きする ADR を発行する。
```

- [ ] **Step 2: Commit**

```bash
git add docs/decisions/0005-certificate-period-overlap-policy.md
git commit -m "docs(adr): 0005 certificate period overlap as warning"
```

---

### Task 28: ADR 0006 — 事業所体制フラグは拡張可能なコード集合

**Files:**
- Create: `docs/decisions/0006-office-capability-flag-set.md`

- [ ] **Step 1: Write ADR**

```markdown
# ADR 0006: 事業所体制の加算フラグは「拡張可能な文字列キーのマップ」として保持する

- 結論: `OfficeCapability.Flags` を `IReadOnlyDictionary<string,bool>` として保持し、SQLite には JSON テキスト列で永続化する。キーの正式コード体系・単位数は**フェーズ1では未確定**とし、`docs/open-questions.md` で管理する。
- 背景: 報酬告示の加算コードはフェーズ3で公式仕様と突合して確定する必要がある。フェーズ1で列挙体に固定すると、フェーズ3で破壊的変更が避けられない。
- 選択肢:
  - (a) `Dictionary<string,bool>`＋JSON保存（採用）。
  - (b) 列挙体 `AdditionFlag : long` でビットフラグ。
  - (c) `OfficeCapabilityFlag` テーブルを正規化。
- 決定: (a)。フェーズ3で正式コードが揃った時点で、(c) への移行を Application 層の DTO で吸収する。
- 影響:
  - フラグ追加はデータ駆動（マイグレーション不要）。
  - クエリ性能はフェーズ1の規模では問題にならない（事業所体制は少行）。
  - 既知キーのドキュメント化は `docs/open-questions.md` で行う。
```

- [ ] **Step 2: Update `docs/open-questions.md`**

Add bullet:

```markdown
- [ ] **事業所体制の加算フラグキー一覧**: フェーズ1では文字列キーで保持。代表例（暫定）: `mealProvision`（食事提供体制加算）、`transportSupport`（送迎加算）等。フェーズ3で報酬告示と突合して正式コード・単位数を確定する。
```

- [ ] **Step 3: Commit**

```bash
git add docs/decisions/0006-office-capability-flag-set.md docs/open-questions.md
git commit -m "docs(adr): 0006 office capability flags as extensible code set"
```

---

### Task 29: フェーズ1 受け入れ基準の機械検証

**Files:**
- Modify: `04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md` の受け入れチェックボックスを更新
- Modify: `01_ClaudeCode_実装指示書_Tsumugi.md` の §7 フェーズ1 セクション

- [ ] **Step 1: Run full quality gate**

```bash
./build/ci.sh
```
Expected: 緑（dotnet build 警告ゼロ、dotnet test 全緑、dotnet format --verify-no-changes 通過、依存方向テスト緑、オフライン検査緑）。

- [ ] **Step 2: Smoke-run the app and verify AC1-8**

```bash
dotnet run --project src/Tsumugi.App
```
- [ ] タブ間を `Alt+1〜5` で移動できる
- [ ] 受給者証タブで期限アラート一覧が表示される
- [ ] ダークテーマが既定で適用されている
- [ ] 日次記録セルで `Space`/`F2`/`Backspace` が機能する

> UI verification: 上の項目を目視確認した上で本ステップを完了とする。type-checker / test suite では feature correctness は検証できないため、明示する。

- [ ] **Step 3: Update phase 1 acceptance checkboxes**

`04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md` の §7 で `[ ]` → `[x]` に更新：

- (AC1-1) CRUD
- (AC1-2) DailyRecord 厳密追記
- (AC1-3) 訂正・取消の純粋関数
- (AC1-4) 期間マスタの基準日クエリ
- (AC1-5) 期限アラート
- (AC1-6) 同一性マスタの楽観ロック
- (AC1-7) Application 層の日付検証
- (AC1-8) UI のキーボード操作・ダークテーマ・期限アラート可視化
- (AC1-9) 加算フラグの拡張可能性

`01_ClaudeCode_実装指示書_Tsumugi.md` §7 フェーズ1 も同様に更新。

- [ ] **Step 4: Final commit**

```bash
git add 01_ClaudeCode_実装指示書_Tsumugi.md \
        04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md
git commit -m "docs: mark phase1 acceptance criteria AC1-1..AC1-9 as met"
```

---

## Self-Review チェックリスト（プラン作成者向け）

- [x] **Spec coverage**:
  - AC1-1 CRUD: Task 2〜8（Domain）＋ Task 10〜13（Infrastructure）＋ Task 15〜19（Application）＋ Task 21〜25（App）
  - AC1-2 追記不変: Task 12
  - AC1-3 訂正・取消: Task 9 + Task 18 + Task 23
  - AC1-4 期間マスタ基準日: Task 13（`FindEffectiveAsync`）+ Phase1RoundTripTests
  - AC1-5 期限アラート: Task 5 + Task 16（ListExpiring）+ Task 22 (CertificateVM)
  - AC1-6 楽観ロック: 既存 `TsumugiDbContext.RotateConcurrencyTokens` + Task 19 の `OptimisticConcurrencyTests`
  - AC1-7 日付検証信頼境界: Task 14
  - AC1-8 キーボード・ダーク・低アニメ・期限可視化: Task 20 + Task 25
  - AC1-9 加算フラグ拡張可能性: Task 7 + Task 17 + Task 28（ADR 0006）

- [x] **Placeholder scan**: 「TBD」「あとで」等は使用していない。各ステップにコードを実体で記述。

- [x] **Type consistency**:
  - `Recipient.Create` シグネチャは Task 3/15/21 で一致（`kanjiName, kanaName, dateOfBirth`）
  - `Certificate.Create` シグネチャは Task 4/13/16/22 で一致
  - `CertificatePolicy.FindExpiring` の戻り値は Task 5/16 で `IReadOnlyList<CertificateExpiry>` 一致
  - `DailyRecordPolicy.Effective` / `EffectiveByDate` は Task 9/18 で一致
  - `(Dto, IReadOnlyList<string> Warnings)` の戻り値タプルは Task 16/17 で統一

- [x] **既存資産の保持**: Phase 0 の `Office`/`Entity`/`TsumugiDbContext`/`SqliteLocationService`/`OfflineComplianceTests`/`AppOfflineComplianceTests`/ADR 0001-0003 を破壊しない。`RegisterOfficeUseCase` 拡張のみ。

---

## 実行方針

このプランの実行モードを選択してください：

1. **Subagent-Driven（推奨）** — 各タスクごとに subagent をディスパッチし、タスク間でレビュー。Domain → Infrastructure → Application → App の順で、Group 内では Task 番号順。各 Task 完了時に build/test 緑を保つ。
2. **Inline Execution** — 本セッション内で `superpowers:executing-plans` を使ってチェックポイント付きで連続実行。

> いずれの場合も、`./build/ci.sh` 緑をタスク間の不変条件として保つ。違反テスト（追記不変ガードを意図的に外したら赤になるか）は Task 12 完了時点で 1 度だけ手で確認し、確認後にガードを戻すこと。
