# H-4: Domain カバレッジ 95% 到達と CI 閾値強化 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Domain アセンブリのテストカバレッジを ≥ 95% line に到達させ、`build/ci.sh` の閾値を 70 → 95 に引き上げて Phase 2 完了条件 (CLAUDE.md「カバレッジ: Domain ≥ 95% 目標」) を満たす。Codex H-4 を解消する。

**Architecture:**
現状 Domain は **85.83% line / 75.21% branch**。未到達の主犯は 4 ファイル:
1. `WageStatementPolicy.cs` (0% line) — テストファイル自体が存在しない
2. `FaceSheet.cs` (0% line) — Phase 1 で導入されたが未テスト
3. `WageStatement.cs` (59% line) — 既存 `WageStatementTests.cs` が validation 例外パスを未カバー
4. `YearMonth.cs` (69.6% line) — `Next`/`Previous` 年境界・`FromInt/ToInt`・比較演算子・`ToString` が未カバー

この 4 ファイルを 100% に近づけると、Domain 全体が ~95% を上回る見込み（4 ファイルが Domain LOC の概ね 14% を占めるため）。残る軽微ギャップ (FiscalYearPolicy / DisabilityCategories / 各 WageStrategy 等の数行) は本タスクの最終確認で対応要否を判定する。

最後に `build/ci.sh` の Domain 閾値を `70` → `95` に変更して CI ゲートとして固定する。

**Tech Stack:** .NET 10 / C# / xUnit / FluentAssertions / coverlet.msbuild

## Global Constraints

- 依存方向: 本タスクは Domain のテストのみ追加 + `build/ci.sh` 1 ファイルの編集。Production code (`src/`) は触らない。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — テストも警告ゼロ。
- 1 コミット=1 論理変更。コミットメッセージは `phase2` + `H-4` を含める。
- `dotnet format --verify-no-changes` 緑、`dotnet build -warnaserror` 緑、`dotnet test` 全緑を各 commit 前に確認。
- 既存ヘルパ (`new Guid(...)` シード値、`DateTimeOffset` リテラル) と命名規約に従う。
- ADR 不要 (純粋なテスト拡張)。報酬告示・CSV 仕様の数値は登場しない。

## File Structure

- **新規**: `tests/Tsumugi.Domain.Tests/WageStatementPolicyTests.cs`
  - `Effective` / `EffectiveByRecipient` の全分岐をテーブル駆動。`DailyRecordPolicyTests` と同形式。
- **新規**: `tests/Tsumugi.Domain.Tests/FaceSheetTests.cs`
  - `Create` 工場メソッドの最小引数・全引数・null 既定値のラウンドトリップ。
- **修正**: `tests/Tsumugi.Domain.Tests/WageStatementTests.cs`
  - `NewRecord` / `Correction` の `ArgumentOutOfRangeException`（負金額）と `ArgumentException`（空 summary）をカバー。
- **修正**: `tests/Tsumugi.Domain.Tests/YearMonthTests.cs`
  - `Next` 12→1 月、`Previous` 1→12 月、`FromInt/ToInt` 往復、比較演算子 (`<`/`<=`/`>`/`>=`)、`ToString` フォーマット。
- **修正**: `build/ci.sh:18-29`
  - Domain の `-p:Threshold=70` を `95` に変更。Application は本タスクのスコープ外（70 のまま）。

## 仕様根拠

- CLAUDE.md「カバレッジ: Domain ≧ 95% 目標、Phase3 報酬算定は 100% 目標」
- Codex H-4: "Domain threshold を 95 に上げ、未到達なら未達としてPhase2完了扱いにしない。"
- `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md` AC2-6（工賃確定スナップショット）の根拠コードに `WageStatementPolicy` / `WageStatement` が含まれる → これらを未テストで通すのは Phase 2 完了条件に反する。

## カバレッジ目標と検証

各タスクの最後に以下を実行し、Domain line-rate が目標を超えていることを確認する。

```bash
rm -rf TestResults && \
dotnet test tests/Tsumugi.Domain.Tests -c Release --no-build \
  --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

cobertura XML から `<package name="Tsumugi.Domain" line-rate="...">` の数値を取り出して比較する。Task 4 後に ≥ 0.95、Task 5 後に CI スクリプトが緑になることをもって完了とする。

---

### Task 1: `WageStatementPolicyTests.cs` 新規作成 — `WageStatementPolicy` 100% 到達

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/WageStatementPolicyTests.cs`

**Interfaces:**
- Consumes: `WageStatementPolicy.Effective(IEnumerable<WageStatement>) → WageStatement?`、`EffectiveByRecipient(...) → IReadOnlyDictionary<Guid, WageStatement>`、`WageStatement.NewRecord(...)`、`WageStatement.Correction(...)`、`WageStatement.Cancellation` は存在しないので注意（`RecordKind.Cancel` は `Correction` の origin で表現される? → コード読解必要）
- Produces: 新規テスト 8 件相当。`WageStatementPolicy.cs` を line 100% / branch 100% に到達。

**事前メモ:**
`WageStatementPolicy.Effective` のアルゴリズムは以下:
1. `CreatedAt` 昇順に整列
2. `Kind == New` の最初のエントリを origin として取得（無ければ null）
3. origin から「次の派生（OriginId が現 current.Id で Kind != New のもの）」を辿る
4. 派生が `Cancel` なら null を返す、`Correct` なら current を更新して継続
5. 派生が無くなったら current を返す

`WageStatement` には現状 `Cancel` ファクトリは無い（`WageStatementTests.cs` 読解で確認）。`Cancel` を test 用に作るには `new WageStatement { ..., Kind = RecordKind.Cancel, ... }` の record initializer を直接使う必要がある。

- [ ] **Step 1.1: 既存 `WageStatementTests.cs` のヘルパパターンを確認**

Serena `find_symbol` で `WageStatementTests` (相対パス `tests/Tsumugi.Domain.Tests/WageStatementTests.cs`) を body 取得し、`NewRecord` / `Correction` 呼び出しの引数順とヘルパフィクスチャ（`Office`, `Month`, `T`, `R1` 等）を確認する。同じシード値スタイルを再利用する。

- [ ] **Step 1.2: `WageStatementPolicyTests.cs` を新規作成**

ヘッダ部:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Tests;

public sealed class WageStatementPolicyTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);

    private static WageStatement New(Guid id, Guid rid, int yen, DateTimeOffset createdAt) =>
        WageStatement.NewRecord(id, Office, Month, rid, yen, "基礎要約", "tester", createdAt);

    private static WageStatement Correct(Guid id, Guid rid, Guid originId, int yen, DateTimeOffset createdAt) =>
        WageStatement.Correction(id, Office, Month, rid, originId, yen, "訂正要約", "tester", createdAt);

    private static WageStatement Cancel(Guid id, Guid rid, Guid originId, DateTimeOffset createdAt) =>
        new()
        {
            Id = id,
            OfficeId = Office,
            Month = Month,
            RecipientId = rid,
            AmountYen = 0,
            BasisSummary = "取消",
            Kind = RecordKind.Cancel,
            OriginId = originId,
            CreatedBy = "tester",
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    [Fact]
    public void Effective_returns_null_for_empty_input()
    {
        WageStatementPolicy.Effective(Array.Empty<WageStatement>()).Should().BeNull();
    }

    [Fact]
    public void Effective_returns_null_when_no_new_origin_present()
    {
        var orphan = Correct(Guid.NewGuid(), R1, originId: Guid.NewGuid(), 1000, T);
        WageStatementPolicy.Effective(new[] { orphan }).Should().BeNull();
    }

    [Fact]
    public void Effective_returns_origin_when_no_correction_or_cancel_follows()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var result = WageStatementPolicy.Effective(new[] { origin });
        result.Should().NotBeNull();
        result!.Id.Should().Be(origin.Id);
        result.AmountYen.Should().Be(10_000);
    }

    [Fact]
    public void Effective_returns_latest_correction_chain()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var c1 = Correct(Guid.NewGuid(), R1, origin.Id, 11_000, T.AddMinutes(1));
        var c2 = Correct(Guid.NewGuid(), R1, c1.Id, 12_000, T.AddMinutes(2));
        var result = WageStatementPolicy.Effective(new[] { origin, c1, c2 });
        result.Should().NotBeNull();
        result!.Id.Should().Be(c2.Id);
        result.AmountYen.Should().Be(12_000);
    }

    [Fact]
    public void Effective_returns_null_when_chain_ends_with_cancel()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var cancel = Cancel(Guid.NewGuid(), R1, origin.Id, T.AddMinutes(1));
        WageStatementPolicy.Effective(new[] { origin, cancel }).Should().BeNull();
    }

    [Fact]
    public void Effective_picks_latest_correction_when_multiple_children_same_origin()
    {
        var origin = New(Guid.NewGuid(), R1, 10_000, T);
        var c_old = Correct(Guid.NewGuid(), R1, origin.Id, 11_000, T.AddMinutes(1));
        var c_new = Correct(Guid.NewGuid(), R1, origin.Id, 13_000, T.AddMinutes(2));
        var result = WageStatementPolicy.Effective(new[] { origin, c_old, c_new });
        result.Should().NotBeNull();
        result!.Id.Should().Be(c_new.Id);
        result.AmountYen.Should().Be(13_000);
    }

    [Fact]
    public void EffectiveByRecipient_groups_per_recipient_and_skips_cancelled_chains()
    {
        var o1 = New(Guid.NewGuid(), R1, 10_000, T);
        var o2 = New(Guid.NewGuid(), R2, 20_000, T);
        var cancel2 = Cancel(Guid.NewGuid(), R2, o2.Id, T.AddMinutes(1));
        var result = WageStatementPolicy.EffectiveByRecipient(new[] { o1, o2, cancel2 });
        result.Should().HaveCount(1);
        result.Should().ContainKey(R1);
        result[R1].AmountYen.Should().Be(10_000);
        result.Should().NotContainKey(R2);
    }

    [Fact]
    public void Effective_and_EffectiveByRecipient_throw_on_null_input()
    {
        FluentActions.Invoking(() => WageStatementPolicy.Effective(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => WageStatementPolicy.EffectiveByRecipient(null!))
            .Should().Throw<ArgumentNullException>();
    }
}
```

- [ ] **Step 1.3: ビルド / format / focused test**

Run:
```bash
dotnet build tests/Tsumugi.Domain.Tests
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~WageStatementPolicyTests"
```

Expected: ビルド成功、format clean、8/8 PASS（既存実装と整合する設計のためすべて GREEN）。

- [ ] **Step 1.4: Domain 全体テストとカバレッジを確認**

Run:
```bash
rm -rf TestResults && \
dotnet test tests/Tsumugi.Domain.Tests -c Release \
  --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

`TestResults/.../coverage.cobertura.xml` を確認し、`WageStatementPolicy` のクラス `line-rate` が `1` (100%) に達していることを確認。Domain 全体の `<package name="Tsumugi.Domain" line-rate="...">` が 0.85 以上に上昇していることを記録（Task 5 で 0.95 が目標）。

- [ ] **Step 1.5: コミット**

```bash
git add tests/Tsumugi.Domain.Tests/WageStatementPolicyTests.cs
git commit -m "test(phase2): H-4 cover WageStatementPolicy Effective/EffectiveByRecipient (8 tests)"
```

---

### Task 2: `FaceSheetTests.cs` 新規作成 — `FaceSheet` 100% 到達

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/FaceSheetTests.cs`

**Interfaces:**
- Consumes: `FaceSheet.Create(Guid id, Guid recipientId, string createdBy, DateTimeOffset createdAt, Guid concurrencyToken, ...23 optional nullable strings/bools)` → `FaceSheet`
- Produces: 新規テスト 3 件。`FaceSheet.cs` を line 100% に到達（branch は元から 100%）。

- [ ] **Step 2.1: `FaceSheetTests.cs` を新規作成**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Tests;

public sealed class FaceSheetTests
{
    private static readonly DateTimeOffset T = new(2026, 6, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_with_minimal_args_initializes_defaults_to_null_or_false()
    {
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var token = Guid.NewGuid();

        var f = FaceSheet.Create(id, rid, createdBy: "t", createdAt: T, concurrencyToken: token);

        f.Id.Should().Be(id);
        f.RecipientId.Should().Be(rid);
        f.CreatedBy.Should().Be("t");
        f.CreatedAt.Should().Be(T);
        f.ConcurrencyToken.Should().Be(token);

        // 連絡先・家族・医療・受給状況・生活歴の string プロパティはすべて null 既定
        f.PostalCode.Should().BeNull();
        f.Address.Should().BeNull();
        f.PhoneNumber.Should().BeNull();
        f.EmailAddress.Should().BeNull();
        f.EmergencyContactName.Should().BeNull();
        f.EmergencyContactRelationship.Should().BeNull();
        f.EmergencyContactPhone.Should().BeNull();
        f.FamilyComposition.Should().BeNull();
        f.Cohabitants.Should().BeNull();
        f.PrimaryDoctorName.Should().BeNull();
        f.PrimaryDoctorHospital.Should().BeNull();
        f.PrimaryDoctorPhone.Should().BeNull();
        f.MedicalHistory.Should().BeNull();
        f.CurrentConditions.Should().BeNull();
        f.Medications.Should().BeNull();
        f.Allergies.Should().BeNull();
        f.PensionDetails.Should().BeNull();
        f.LifeHistory.Should().BeNull();
        f.PersonalWishes.Should().BeNull();
        f.SupportNeeds.Should().BeNull();
        f.AssessmentSummary.Should().BeNull();

        // bool は false 既定
        f.ReceivesNursingInsurance.Should().BeFalse();
        f.ReceivesDisabilityPension.Should().BeFalse();
    }

    [Fact]
    public void Create_with_all_optional_args_roundtrips_every_field()
    {
        var f = FaceSheet.Create(
            id: Guid.NewGuid(),
            recipientId: Guid.NewGuid(),
            createdBy: "tester",
            createdAt: T,
            concurrencyToken: Guid.NewGuid(),
            postalCode: "100-0001",
            address: "東京都千代田区千代田1-1",
            phoneNumber: "03-0000-0001",
            emailAddress: "user@example.test",
            emergencyContactName: "親族 太郎",
            emergencyContactRelationship: "父",
            emergencyContactPhone: "090-0000-0002",
            familyComposition: "三世代同居",
            cohabitants: "本人・配偶者・子",
            primaryDoctorName: "主治医 花子",
            primaryDoctorHospital: "千代田総合病院",
            primaryDoctorPhone: "03-0000-0003",
            medicalHistory: "高血圧 2010〜",
            currentConditions: "経過良好",
            medications: "降圧剤",
            allergies: "卵",
            receivesNursingInsurance: true,
            receivesDisabilityPension: true,
            pensionDetails: "障害基礎年金 1級",
            lifeHistory: "本人記録",
            personalWishes: "自立した生活",
            supportNeeds: "通所継続",
            assessmentSummary: "総合所見");

        f.PostalCode.Should().Be("100-0001");
        f.Address.Should().Be("東京都千代田区千代田1-1");
        f.PhoneNumber.Should().Be("03-0000-0001");
        f.EmailAddress.Should().Be("user@example.test");
        f.EmergencyContactName.Should().Be("親族 太郎");
        f.EmergencyContactRelationship.Should().Be("父");
        f.EmergencyContactPhone.Should().Be("090-0000-0002");
        f.FamilyComposition.Should().Be("三世代同居");
        f.Cohabitants.Should().Be("本人・配偶者・子");
        f.PrimaryDoctorName.Should().Be("主治医 花子");
        f.PrimaryDoctorHospital.Should().Be("千代田総合病院");
        f.PrimaryDoctorPhone.Should().Be("03-0000-0003");
        f.MedicalHistory.Should().Be("高血圧 2010〜");
        f.CurrentConditions.Should().Be("経過良好");
        f.Medications.Should().Be("降圧剤");
        f.Allergies.Should().Be("卵");
        f.ReceivesNursingInsurance.Should().BeTrue();
        f.ReceivesDisabilityPension.Should().BeTrue();
        f.PensionDetails.Should().Be("障害基礎年金 1級");
        f.LifeHistory.Should().Be("本人記録");
        f.PersonalWishes.Should().Be("自立した生活");
        f.SupportNeeds.Should().Be("通所継続");
        f.AssessmentSummary.Should().Be("総合所見");
    }

    [Fact]
    public void Created_record_is_value_equal_when_all_fields_match()
    {
        var id = Guid.NewGuid();
        var rid = Guid.NewGuid();
        var token = Guid.NewGuid();
        var a = FaceSheet.Create(id, rid, "t", T, token, address: "東京");
        var b = FaceSheet.Create(id, rid, "t", T, token, address: "東京");
        a.Should().Be(b);
    }
}
```

- [ ] **Step 2.2: ビルド / format / focused test**

Run:
```bash
dotnet build tests/Tsumugi.Domain.Tests
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~FaceSheetTests"
```

Expected: ビルド成功、format clean、3/3 PASS。

- [ ] **Step 2.3: コミット**

```bash
git add tests/Tsumugi.Domain.Tests/FaceSheetTests.cs
git commit -m "test(phase2): H-4 cover FaceSheet.Create defaults and full roundtrip (3 tests)"
```

---

### Task 3: `WageStatementTests.cs` 拡張 — `WageStatement` 100% 到達

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/WageStatementTests.cs`

**Interfaces:**
- Consumes: 既存 `WageStatementTests` のヘルパ (`Office`, `Month`, `T` 等)
- Produces: 既存 + 4 件の validation テスト。`WageStatement.cs` を line ≥ 95% に到達（記述したコードパスはすべて exercise）。

**事前メモ:**
未カバー対象:
- `NewRecord`: `ArgumentOutOfRangeException.ThrowIfNegative(amountYen)` (line ~21)、`ArgumentException.ThrowIfNullOrEmpty(basisSummary)` (line ~22)
- `Correction`: 同じ 2 つの guard (line ~38, 39)

- [ ] **Step 3.1: 既存テストファイルのヘルパを確認**

`tests/Tsumugi.Domain.Tests/WageStatementTests.cs` を読み、既存メソッド名・ヘルパスタイルを把握する。

- [ ] **Step 3.2: 4 件の validation テストを追加**

ファイル末尾（クラス閉じ `}` の直前）に挿入。クラス内に既存のヘルパ（例: `Office`, `Month`, `R1`, `T`）がある前提でそれらを再利用する。**もし既存ヘルパ名が異なる場合は Step 3.1 で把握した名前に合わせて以下を調整する** — その変更は本ステップの正当な範囲内。

```csharp
[Fact]
public void NewRecord_throws_when_amount_is_negative()
{
    var act = () => WageStatement.NewRecord(
        Guid.NewGuid(), Office, Month, R1, amountYen: -1, basisSummary: "x", "t", T);
    act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("amountYen");
}

[Fact]
public void NewRecord_throws_when_basis_summary_is_empty()
{
    var act = () => WageStatement.NewRecord(
        Guid.NewGuid(), Office, Month, R1, amountYen: 0, basisSummary: "", "t", T);
    act.Should().Throw<ArgumentException>().WithParameterName("basisSummary");
}

[Fact]
public void Correction_throws_when_amount_is_negative()
{
    var act = () => WageStatement.Correction(
        Guid.NewGuid(), Office, Month, R1, originId: Guid.NewGuid(),
        amountYen: -1, basisSummary: "x", "t", T);
    act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("amountYen");
}

[Fact]
public void Correction_throws_when_basis_summary_is_empty()
{
    var act = () => WageStatement.Correction(
        Guid.NewGuid(), Office, Month, R1, originId: Guid.NewGuid(),
        amountYen: 100, basisSummary: "", "t", T);
    act.Should().Throw<ArgumentException>().WithParameterName("basisSummary");
}
```

注意: `ArgumentException.ThrowIfNullOrEmpty(value, parameterName)` は `value == null` でも例外を投げるため、`""` でテスト可能。`null!` を渡すと nullable diagnostic で警告 → `""` を使う。

- [ ] **Step 3.3: ビルド / format / focused test**

Run:
```bash
dotnet build tests/Tsumugi.Domain.Tests
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~WageStatementTests"
```

Expected: ビルド成功、format clean、既存 + 4 件すべて PASS。

- [ ] **Step 3.4: コミット**

```bash
git add tests/Tsumugi.Domain.Tests/WageStatementTests.cs
git commit -m "test(phase2): H-4 cover WageStatement.NewRecord/Correction validation paths"
```

---

### Task 4: `YearMonthTests.cs` 拡張 — `YearMonth` 100% 到達

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/YearMonthTests.cs`

**Interfaces:**
- Consumes: `YearMonth(int year, int month)`, `Next()`, `Previous()`, `FromDate`, `ToInt()`, `FromInt(int)`, `CompareTo`, operators `<`/`<=`/`>`/`>=`, `ToString()`, `FirstDay()`, `LastDay()`
- Produces: 既存 + 約 8 件の追加テスト。`YearMonth.cs` を line ≥ 95% / branch ≥ 90% に。

- [ ] **Step 4.1: 既存テストファイルの構造を確認**

`tests/Tsumugi.Domain.Tests/YearMonthTests.cs` を読み、ヘルパ命名と既にカバー済みの観点を把握する。本ステップで追加する観点と既存の重複を避ける。

- [ ] **Step 4.2: 不足観点を追加**

ファイル末尾に挿入（既存テストとの重複を Step 4.1 の調査結果に基づいて除外する）。**もし既存テストに同等のものがあれば、その項目はスキップする**:

```csharp
[Fact]
public void Constructor_throws_when_year_below_lower_bound()
{
    FluentActions.Invoking(() => new YearMonth(1899, 1))
        .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("year");
}

[Fact]
public void Constructor_throws_when_year_above_upper_bound()
{
    FluentActions.Invoking(() => new YearMonth(2201, 1))
        .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("year");
}

[Fact]
public void Constructor_throws_when_month_out_of_range()
{
    FluentActions.Invoking(() => new YearMonth(2026, 0))
        .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("month");
    FluentActions.Invoking(() => new YearMonth(2026, 13))
        .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("month");
}

[Fact]
public void Next_crosses_year_boundary_from_december()
{
    var dec = new YearMonth(2026, 12);
    var jan = dec.Next();
    jan.Year.Should().Be(2027);
    jan.Month.Should().Be(1);
}

[Fact]
public void Previous_crosses_year_boundary_from_january()
{
    var jan = new YearMonth(2026, 1);
    var dec = jan.Previous();
    dec.Year.Should().Be(2025);
    dec.Month.Should().Be(12);
}

[Fact]
public void ToInt_FromInt_roundtrip_preserves_value()
{
    var ym = new YearMonth(2026, 7);
    var i = ym.ToInt();
    i.Should().Be(202607);
    YearMonth.FromInt(i).Should().Be(ym);
}

[Fact]
public void FromDate_extracts_year_and_month()
{
    var ym = YearMonth.FromDate(new DateOnly(2026, 7, 15));
    ym.Should().Be(new YearMonth(2026, 7));
}

[Fact]
public void Comparison_operators_match_chronological_order()
{
    var a = new YearMonth(2026, 7);
    var b = new YearMonth(2026, 8);
    var c = new YearMonth(2027, 1);

    (a < b).Should().BeTrue();
    (b < c).Should().BeTrue();
    (a <= a).Should().BeTrue();
    (c > a).Should().BeTrue();
    (b >= b).Should().BeTrue();
    a.CompareTo(b).Should().BeNegative();
    b.CompareTo(a).Should().BePositive();
    a.CompareTo(a).Should().Be(0);
}

[Fact]
public void ToString_returns_zero_padded_yyyy_mm()
{
    new YearMonth(2026, 7).ToString().Should().Be("2026-07");
    new YearMonth(2026, 12).ToString().Should().Be("2026-12");
}

[Fact]
public void FirstDay_and_LastDay_return_month_bounds()
{
    var ym = new YearMonth(2026, 7);
    ym.FirstDay().Should().Be(new DateOnly(2026, 7, 1));
    ym.LastDay().Should().Be(new DateOnly(2026, 7, 31));

    new YearMonth(2024, 2).LastDay().Should().Be(new DateOnly(2024, 2, 29));  // 閏年
    new YearMonth(2026, 2).LastDay().Should().Be(new DateOnly(2026, 2, 28));  // 平年
}
```

- [ ] **Step 4.3: ビルド / format / focused test**

Run:
```bash
dotnet build tests/Tsumugi.Domain.Tests
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Domain.Tests --filter "FullyQualifiedName~YearMonthTests"
```

Expected: ビルド成功、format clean、既存 + 新規すべて PASS。

- [ ] **Step 4.4: 全体カバレッジを再計測し 95% 到達を確認**

```bash
rm -rf TestResults && \
dotnet test tests/Tsumugi.Domain.Tests -c Release \
  --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

cobertura XML を解析し、Domain package の line-rate を確認:
- 目標: ≥ 0.95
- 未到達なら、報告して **Step 4.5 へ進まず停止する**（Task 4 拡張または追加 micro task で対応するかをコントローラに判断仰ぐ）。

到達していれば次のステップへ。

- [ ] **Step 4.5: コミット**

```bash
git add tests/Tsumugi.Domain.Tests/YearMonthTests.cs
git commit -m "test(phase2): H-4 cover YearMonth boundaries, operators, and roundtrip"
```

---

### Task 5: `build/ci.sh` 閾値 70 → 95 へ引き上げ

**Files:**
- Modify: `build/ci.sh:18-29` (Domain の `-p:Threshold=70` を `95` に変更)

**Interfaces:**
- Consumes: `dotnet test ... -p:CollectCoverage=true -p:Threshold=N` (coverlet.msbuild)
- Produces: CI が Domain line ≥ 95% を機械的に強制するゲートに昇格。

**事前メモ:**
現状の関連箇所:
```bash
dotnet test tests/Tsumugi.Domain.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Domain]*" \
  -p:Threshold=70 \
  -p:ThresholdType=line \
  -p:ThresholdStat=total
```
Application 側は本タスクで触らない（70 のまま）。

- [ ] **Step 5.1: `build/ci.sh` の Domain ブロックを編集**

`build/ci.sh` 内、Domain の `dotnet test` 呼び出しブロックで `-p:Threshold=70` を `-p:Threshold=95` に変更。Application ブロックは触らない。コメント行 `# coverage threshold gate (gate #3 enforcement — floor=70%, raise over time; spec targets 100% in Phase 3)` も実態に合わせて更新する（例: `floor=Domain 95% / Application 70%, raise Application in Phase 3`）。

- [ ] **Step 5.2: `build/ci.sh` をローカル実行して緑を確認**

```bash
./build/ci.sh
```

Expected: 全ステップ通過。特に Domain coverage threshold gate で「Threshold of 95 has been reached」相当のメッセージで PASS する（厳密な文言は coverlet 出力に依存）。

未到達 (例: 94.x%) の場合は **コミットせず停止し、コントローラに報告**。Task 4 で再度ギャップ充填するかの判断を仰ぐ。

- [ ] **Step 5.3: `dotnet format --verify-no-changes` を確認**

```bash
dotnet format --verify-no-changes
```

Expected: 差分なし。

- [ ] **Step 5.4: コミット**

```bash
git add build/ci.sh
git commit -m "ci(phase2): H-4 raise Domain coverage threshold from 70 to 95"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- Codex H-4「Domain threshold を 95 に上げ、未到達なら未達としてPhase2完了扱いにしない」→ Task 5 で機械的に強制。Task 1-4 で必要な追加テストを供給。
- CLAUDE.md「カバレッジ: Domain ≥ 95% 目標」→ Task 4 末で計測確認。

**2. Placeholder scan:** TBD/TODO/「適切なエラー処理」等なし。各テストは具体コード提示済み。

**3. Type consistency:**
- `WageStatement.NewRecord(Guid id, Guid officeId, YearMonth month, Guid recipientId, int amountYen, string basisSummary, string createdBy, DateTimeOffset createdAt)` — Task 1 と Task 3 の用例で完全一致。
- `WageStatement.Correction(Guid id, Guid officeId, YearMonth month, Guid recipientId, Guid originId, int amountYen, string basisSummary, string createdBy, DateTimeOffset createdAt)` — 同上。
- `WageStatementPolicy.Effective(IEnumerable<WageStatement>) → WageStatement?` — Task 1 の Effective 呼び出しと整合。
- `FaceSheet.Create(...)` のシグネチャは仕様コード (`src/Tsumugi.Domain/Entities/FaceSheet.cs:44-65`) と Task 2 のテスト両方で完全一致（25 個の named arguments）。
- `YearMonth` の `Next/Previous/FromInt/ToInt/CompareTo/FirstDay/LastDay/ToString` は実装 (`src/Tsumugi.Domain/ValueObjects/YearMonth.cs:18-43`) と Task 4 のテストで型一致。
- `build/ci.sh` の `-p:Threshold=N` 引数は coverlet.msbuild の公式仕様。

**4. 影響範囲:**
- Production code 不変。ADR 不要。
- Application 層の閾値は 70 のまま（Phase 3 で再検討）。
- 既存テストへの破壊的変更なし（Task 3 のみ既存ファイルに追加するが、既存メソッドは触らない）。
- もし Task 4 の Step 4.4 で 95% 未達なら、軽微ギャップファイル（FiscalYearPolicy / DisabilityCategories / AllocationPolicy 残ガード / WageSettings / FixedWageStrategy / AverageWageMetric / EqualWageStrategy）に 1-3 件ずつ追加して再計測する。本計画では「コントローラ判断仰ぐ」と明記し、必要時に Task 4.5 として展開する。
