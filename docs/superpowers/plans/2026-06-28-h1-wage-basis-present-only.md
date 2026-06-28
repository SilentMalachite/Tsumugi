# H-1: WageBasisExtractor を実効Presentのみで集計する 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `WageBasisExtractor.Build` が `WorkRecord` を集計する際、recipient × date が「実効 Attendance=Present」である日に限定するよう修正し、仕様 AC2-5「工賃基礎は実効`Present`のみ」を満たす。

**Architecture:**
現状 `WageBasisExtractor` は recipient ごとに、実効 `DailyRecord` から `PresentDays` を数えつつ、`WorkRecord` 集計は **全実効 `WorkRecord` を月内合算**している（出席日条件が抜けている）。修正は **recipient×date の "Present セット"** を導出し、実効 `WorkRecord` を `presentDates.Contains(w.WorkDate)` でフィルタしてから合算する純粋関数変更。Domain 層内・副作用なし・呼び出し側のシグネチャ変更なし。

**Tech Stack:** .NET 10 / C# / xUnit / FluentAssertions

## Global Constraints

- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。Domain は外部依存なし（本タスクは Domain 内のみ）。
- `record` ＋ append-only、純粋関数（日付/乱数/I/O 非依存）。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。警告ゼロ。
- TDD: Red → Green → Refactor。1 コミット=1 論理変更。コミットメッセージにフェーズ番号(`phase2`) と受け入れ基準ID (`AC2-5`) を含める。
- カバレッジ: Domain ≥ 95%（H-4 で CI ゲート強化予定、本タスクは到達確認のみ）。
- `dotnet build` 警告ゼロ・`dotnet test` 全緑・`dotnet format --verify-no-changes` 緑。

## File Structure

- **修正**: `src/Tsumugi.Domain/Logic/WageBasisExtractor.cs:12-56`
  - 1 ファイル / 1 静的クラス / 1 メソッド `Build(...)` の内部のみ変更。
  - 公開シグネチャ、戻り値型 (`IReadOnlyList<WageInputs>`)、副作用なしの保証は維持。
- **修正**: `tests/Tsumugi.Domain.Tests/WageBasisExtractorTests.cs:9-57`
  - 既存テスト `Aggregates_worked_minutes_and_piece_amounts_from_effective_work_records` の期待値を修正（誤った期待値で固定されているのを正す）。
  - 新規 RED テストを 4 件追加。

呼び出し側 (`src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs:60`) は `WageBasisExtractor.Build(allDaily, allWork, ym)` を変更不要。インプットの自然な絞り込み強化のみ。

## 不変条件・仕様根拠

- 仕様: `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md:149` AC2-5「月次集計が `FiscalYearStartMonth=4` の年度・締めを正しく扱う。**工賃基礎は実効`Present`のみ**。」
- 「実効 Present」 = `DailyRecordPolicy.EffectiveByDate(...)` で当該日のレコードが残り、その `Attendance == Attendance.Present` であること。
- 影響対象集計値: `TotalWorkedMinutes`, `TotalPieceAmountYen`, `TotalPoints`。`PresentDays` は現状でも正しい (既存実装維持)。
- 除外されるべき入力:
  1. 実効 `Attendance=Absent` 日の `WorkRecord`
  2. 実効 `Attendance=AbsenceSupport` 日の `WorkRecord`（欠席時対応加算は記録系の話で、工賃基礎ではない）
  3. 実効 `Attendance=Discontinued` 日（取消で実効化された日）の `WorkRecord`
  4. `DailyRecord` が一切ない日の `WorkRecord`（"出席が記録されていない"＝出席でない）
- 取消・訂正は `WorkRecordPolicy.EffectiveByDate` / `DailyRecordPolicy.EffectiveByDate` が既に処理済み。本変更は **それらの実効結果同士のクロスフィルタ** を追加するのみ。

---

### Task 1: RED — Present-only 仕様の失敗テスト追加と誤期待値の修正

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/WageBasisExtractorTests.cs:9-57`

**Interfaces:**
- Consumes: `WageBasisExtractor.Build(IEnumerable<DailyRecord>, IEnumerable<WorkRecord>, YearMonth) → IReadOnlyList<WageInputs>` （既存）
- Produces: 失敗する RED テスト 4 件 + 修正された既存テスト 1 件。Task 2 でこれらを GREEN にする。

- [ ] **Step 1.1: 既存テスト `Aggregates_worked_minutes_and_piece_amounts_from_effective_work_records` の期待値を Present 仕様に合わせて修正**

`tests/Tsumugi.Domain.Tests/WageBasisExtractorTests.cs` の該当テストを以下で **置換**（既存は 7/1 だけ Present なのに 7/2 の work 180分も合算する誤期待）：

```csharp
[Fact]
public void Aggregates_worked_minutes_and_piece_amounts_only_from_present_dates()
{
    var d1 = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
    // 7/2 は出席記録なし → 7/2 の work は除外されるべき
    var w1 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), workedMinutes: 240, pieceCount: 5, pieceUnitYen: 100, points: 0, note: null, "t", T);
    var w2 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), workedMinutes: 180, pieceCount: 3, pieceUnitYen: 100, points: 0, note: null, "t", T);

    var inputs = WageBasisExtractor.Build(new[] { d1 }, new[] { w1, w2 }, Month);

    inputs.Should().HaveCount(1);
    var i = inputs[0];
    i.TotalWorkedMinutes.Should().Be(240);        // 7/1 のみ
    i.TotalPieceAmountYen.Should().Be(500);       // 5 * 100 のみ
    i.PresentDays.Should().Be(1);
}
```

- [ ] **Step 1.2: 新規 RED テスト追加 — Absent 日の WorkRecord は除外**

同ファイル末尾の `Filters_out_months_outside_target` の直後（クラス閉じ `}` の直前）に挿入：

```csharp
[Fact]
public void Excludes_work_records_on_absent_days()
{
    var dPresent = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
    var dAbsent  = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), Attendance.Absent,  TransportKind.None, false, null, "t", T);
    var wPresent = WorkRecord.NewRecord(Guid.NewGuid(),  R1, new DateOnly(2026, 7, 1), workedMinutes: 120, pieceCount: 0, pieceUnitYen: 0, points: 10, note: null, "t", T);
    var wAbsent  = WorkRecord.NewRecord(Guid.NewGuid(),  R1, new DateOnly(2026, 7, 2), workedMinutes: 999, pieceCount: 9, pieceUnitYen: 99, points: 99, note: null, "t", T);

    var inputs = WageBasisExtractor.Build(new[] { dPresent, dAbsent }, new[] { wPresent, wAbsent }, Month);

    inputs.Should().HaveCount(1);
    var i = inputs[0];
    i.PresentDays.Should().Be(1);
    i.TotalWorkedMinutes.Should().Be(120);
    i.TotalPieceAmountYen.Should().Be(0);
    i.TotalPoints.Should().Be(10);
}
```

- [ ] **Step 1.3: 新規 RED テスト追加 — AbsenceSupport 日の WorkRecord は除外**

直前テストの直後に追加：

```csharp
[Fact]
public void Excludes_work_records_on_absence_support_days()
{
    var dSupport = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.AbsenceSupport, TransportKind.None, false, null, "t", T);
    var w        = WorkRecord.NewRecord(Guid.NewGuid(),  R1, new DateOnly(2026, 7, 1), workedMinutes: 60, pieceCount: 1, pieceUnitYen: 50, points: 5, note: null, "t", T);

    var inputs = WageBasisExtractor.Build(new[] { dSupport }, new[] { w }, Month);

    inputs.Should().HaveCount(1);
    var i = inputs[0];
    i.PresentDays.Should().Be(0);
    i.TotalWorkedMinutes.Should().Be(0);
    i.TotalPieceAmountYen.Should().Be(0);
    i.TotalPoints.Should().Be(0);
}
```

- [ ] **Step 1.4: 新規 RED テスト追加 — DailyRecord が無い日の WorkRecord は除外**

直前テストの直後に追加：

```csharp
[Fact]
public void Excludes_work_records_on_dates_without_any_daily_record()
{
    var w = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 5), workedMinutes: 300, pieceCount: 0, pieceUnitYen: 0, points: 0, note: null, "t", T);

    var inputs = WageBasisExtractor.Build(Array.Empty<DailyRecord>(), new[] { w }, Month);

    inputs.Should().HaveCount(1);
    var i = inputs[0];
    i.RecipientId.Should().Be(R1);
    i.PresentDays.Should().Be(0);
    i.TotalWorkedMinutes.Should().Be(0);
}
```

- [ ] **Step 1.5: 新規 RED テスト追加 — DailyRecord の取消で Discontinued 化された日の WorkRecord は除外**

直前テストの直後に追加：

```csharp
[Fact]
public void Excludes_work_records_on_days_whose_daily_record_was_cancelled()
{
    var originalDailyId = Guid.NewGuid();
    var dNew    = DailyRecord.NewRecord(originalDailyId, R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
    var dCancel = DailyRecord.Cancellation(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), originalDailyId, "t", T.AddMinutes(1));
    var w       = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), workedMinutes: 200, pieceCount: 0, pieceUnitYen: 0, points: 0, note: null, "t", T);

    var inputs = WageBasisExtractor.Build(new[] { dNew, dCancel }, new[] { w }, Month);

    inputs.Should().HaveCount(1);
    var i = inputs[0];
    i.PresentDays.Should().Be(0);
    i.TotalWorkedMinutes.Should().Be(0);
}
```

- [ ] **Step 1.6: ビルドが通り、対象テストが期待通り赤になることを確認**

Run:
```bash
dotnet build tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj
dotnet test  tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WageBasisExtractorTests"
```

Expected:
- ビルド成功（警告ゼロ）。
- 5 件のうち 4 件 FAIL、1 件 PASS（`Filters_out_months_outside_target` は無関係なので PASS のまま）。
- 失敗内訳:
  - `Aggregates_worked_minutes_and_piece_amounts_only_from_present_dates`: `TotalWorkedMinutes` 期待 240, 実測 420 で失敗
  - `Excludes_work_records_on_absent_days`: `TotalWorkedMinutes` 期待 120, 実測 1119 で失敗
  - `Excludes_work_records_on_absence_support_days`: `TotalWorkedMinutes` 期待 0, 実測 60 で失敗
  - `Excludes_work_records_on_dates_without_any_daily_record`: `TotalWorkedMinutes` 期待 0, 実測 300 で失敗
  - `Excludes_work_records_on_days_whose_daily_record_was_cancelled`: `TotalWorkedMinutes` 期待 0, 実測 200 で失敗
- `Counts_only_present_days_per_recipient` は元から PASS のまま。

- [ ] **Step 1.7: RED 状態でコミット**

```bash
git add tests/Tsumugi.Domain.Tests/WageBasisExtractorTests.cs
git commit -m "test(phase2): H-1 RED - AC2-5 Present-only filter for WageBasisExtractor"
```

---

### Task 2: GREEN — WageBasisExtractor を Present-date セットでフィルタ

**Files:**
- Modify: `src/Tsumugi.Domain/Logic/WageBasisExtractor.cs:12-56`

**Interfaces:**
- Consumes: `DailyRecordPolicy.EffectiveByDate(IEnumerable<DailyRecord>) → IReadOnlyDictionary<DateOnly, DailyRecord>` (既存), `WorkRecordPolicy.EffectiveByDate(IEnumerable<WorkRecord>) → IReadOnlyDictionary<DateOnly, WorkRecord>` (既存)
- Produces: 同シグネチャ `Build(...)`。挙動として「実効 `Attendance=Present` の日に限定して work を集計」を保証。

- [ ] **Step 2.1: `WageBasisExtractor.Build` を Present-date セットでフィルタする実装に置換**

`src/Tsumugi.Domain/Logic/WageBasisExtractor.cs` の `WageBasisExtractor` クラス本体を、symbolic edit (`replace_symbol_body` 等) または `Edit` で以下に置換：

```csharp
public static class WageBasisExtractor
{
    public static IReadOnlyList<WageInputs> Build(
        IEnumerable<DailyRecord> dailyRecords,
        IEnumerable<WorkRecord> workRecords,
        YearMonth month)
    {
        ArgumentNullException.ThrowIfNull(dailyRecords);
        ArgumentNullException.ThrowIfNull(workRecords);

        var firstDay = month.FirstDay();
        var lastDay = month.LastDay();

        var dailyByRecipient = dailyRecords
            .Where(r => r.ServiceDate >= firstDay && r.ServiceDate <= lastDay)
            .GroupBy(r => r.RecipientId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var workByRecipient = workRecords
            .Where(r => r.WorkDate >= firstDay && r.WorkDate <= lastDay)
            .GroupBy(r => r.RecipientId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var allRecipients = dailyByRecipient.Keys.Union(workByRecipient.Keys);

        return allRecipients
            .Select(rid =>
            {
                var effDaily = dailyByRecipient.TryGetValue(rid, out var dd)
                    ? DailyRecordPolicy.EffectiveByDate(dd).Values
                    : Enumerable.Empty<DailyRecord>();
                var presentDates = effDaily
                    .Where(r => r.Attendance == Attendance.Present)
                    .Select(r => r.ServiceDate)
                    .ToHashSet();

                var effWork = workByRecipient.TryGetValue(rid, out var ww)
                    ? WorkRecordPolicy.EffectiveByDate(ww).Values
                    : Enumerable.Empty<WorkRecord>();
                var presentWork = effWork.Where(w => presentDates.Contains(w.WorkDate)).ToArray();

                var totalMinutes = presentWork.Sum(w => w.WorkedMinutes ?? 0);
                var totalPiece = presentWork.Sum(w => (w.PieceCount ?? 0) * (w.PieceUnitYen ?? 0));
                var totalPoints = presentWork.Sum(w => w.Points ?? 0);

                return new WageInputs(rid, presentDates.Count, totalMinutes, totalPiece, totalPoints);
            })
            .ToArray();
    }
}
```

変更点:
1. `presentDays` を `effDaily.Count(...)` から `presentDates.Count` に変更（同値、ただし dates Set の再利用のため）。
2. `presentDates` (HashSet) を導出。
3. `effWork` を `presentDates.Contains(w.WorkDate)` でフィルタした `presentWork` に置換。
4. 集計 3 種をすべて `presentWork` から行う。

- [ ] **Step 2.2: 新規 5 件 + 既存 2 件のテストが全てパスすることを確認**

Run:
```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WageBasisExtractorTests"
```

Expected: 7 件すべて PASS（Step 1 の RED 5 件 + 既存の `Counts_only_present_days_per_recipient` + `Filters_out_months_outside_target`）。

- [ ] **Step 2.3: Domain テスト全体が緑であることを確認（リグレッション無し）**

Run:
```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj
```

Expected: 全 PASS（既存テスト数 + 4 新規）。

- [ ] **Step 2.4: ソリューション全体が緑であることを確認（Application 層の利用に影響なし）**

Run:
```bash
dotnet test
```

Expected: 全 PASS。`CalculateWagesUseCaseTests` 5 件含めて緑。もし `CalculateWagesUseCase` 系で誤期待値による失敗が出た場合は **Task 2.5 として失敗テストを別タスクで切り出し**、本タスクはここで停止して報告する（仕様逸脱を修正したことの自然な波及 vs リグレッション、を区別するため）。

- [ ] **Step 2.5: ビルド警告ゼロ、format 緑を確認**

Run:
```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
```

Expected: 両方 0 エラー / 0 警告 / 差分なし。

- [ ] **Step 2.6: Domain カバレッジが ≥ 95% に到達していることを確認**

Run:
```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --collect:"XPlat Code Coverage"
```

Expected: 生成された coverage.cobertura.xml で Domain アセンブリの line/branch カバレッジが ≥ 95%。
未達の場合 → **本タスクは Task 2.7 にスキップせず**、未達ファイル名と現値を報告して停止する（H-4 で扱う閾値強化と切り分けるため）。

- [ ] **Step 2.7: GREEN コミット**

```bash
git add src/Tsumugi.Domain/Logic/WageBasisExtractor.cs
git commit -m "fix(phase2): H-1 GREEN - aggregate WorkRecord only on effective Present dates (AC2-5)"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- AC2-5「工賃基礎は実効`Present`のみ」→ Task 1/2 で完全に検証・実装。
- 取消・訂正（AC2-1）→ 既存 `WorkRecordPolicy.EffectiveByDate` / `DailyRecordPolicy.EffectiveByDate` がそのまま機能。Step 1.5 で Daily 取消経由の除外を回帰防御。
- 月境界（既存 `Filters_out_months_outside_target`）はそのまま緑。

**2. Placeholder scan:** TBD/TODO/「適切なエラー処理」等なし。すべて具体コード提示。

**3. Type consistency:**
- `WageInputs(Guid RecipientId, int PresentDays, int TotalWorkedMinutes, int TotalPieceAmountYen, int TotalPoints)` — Task 1 アサート ↔ Task 2 実装で一致。
- `DailyRecord.NewRecord`/`Cancellation`/`Attendance`/`TransportKind`/`WorkRecord.NewRecord` のシグネチャは現状コードから引用（実コード `src/Tsumugi.Domain/Entities/DailyRecord.cs:12-30, 60-74`, `src/Tsumugi.Domain/Entities/WorkRecord.cs:20-38`, `src/Tsumugi.Domain/Enums/Attendance.cs:2-8`）。

**4. 影響範囲:**
- 公開 API 不変、呼び出し側 (`CalculateWagesUseCase`) 改修不要。
- Step 2.4 で Application 層リグレッションを検出する設計（誤期待値が他にあれば顕在化）。
