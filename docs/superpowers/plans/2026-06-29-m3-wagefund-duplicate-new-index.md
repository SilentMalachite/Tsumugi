# M-3: WageFund 重複 New を SQLite partial unique index で防止 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Codex M-3 を解消する。`WageFund` の同一 `(OfficeId, MonthKey)` に対する `Kind=New` 二重挿入を SQLite partial unique index で防止し、`WageFundPolicy.Effective` が依存する「最初の New を起点に履歴を辿る」前提をデータ層で保証する。

**Architecture:**
ADR 0015 (`DailyRecord` の partial unique index) と同形パターンを採用。設計判断は ADR 0017 に固定し、`(OfficeId, Month → MonthKey)` かつ `Kind = 1` のみに作用する partial unique index を `WageFundConfiguration` に追加、EF Core migration で SQLite に反映する。`WageFund.Correction`/`Cancellation` (Kind ≠ 1) は影響を受けない。

**Tech Stack:** .NET 10 / C# / EF Core 10.x / SQLite / xUnit / FluentAssertions / dotnet-ef CLI

## Global Constraints

- 依存方向: Infrastructure と Infrastructure.Tests のみ変更。Domain/Application/App は触らない。Domain ロジック (`WageFundPolicy`) は変更不要（既存挙動を維持）。
- 純粋関数: Domain 不変、Infrastructure 側で SQLite 制約として宣言的に強制。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — 0 warnings。
- TDD: RED → GREEN → Refactor。
- `dotnet format --verify-no-changes` 緑、`dotnet build -warnaserror` 緑、`dotnet test` 全緑を各 commit 前に確認。
- 既存 ADR 0015 のテキスト・コード様式と完全同形 (ADR フォーマット、partial unique index の filter 表記 `"\\"Kind\\" = 1"`)。
- マイグレーション生成は `dotnet ef migrations add WageFundDuplicateNewIndex --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App` で行う（CLAUDE.md の正式コマンド）。
- 既存マイグレーション (`20260628015004_DailyRecordDuplicateNewIndex`, `20260628045957_Phase2Wage`) には触らない。

## File Structure

| Task | Files |
|---|---|
| 1 (ADR) | `docs/decisions/0017-wagefund-duplicate-new-index.md` (新規) |
| 2 (RED) | `tests/Tsumugi.Infrastructure.Tests/WageFundDuplicateNewIndexTests.cs` (新規) |
| 3 (GREEN) | `src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs:6-29` (修正), `src/Tsumugi.Infrastructure/Migrations/*_WageFundDuplicateNewIndex.cs` (新規, EF CLI 生成), `src/Tsumugi.Infrastructure/Migrations/*_WageFundDuplicateNewIndex.Designer.cs` (新規, EF CLI 生成), `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs` (修正, EF CLI 自動更新) |

## 仕様根拠

- Codex M-3: "`WageFund` の月次New重複をDBで防げません。UseCaseの既存確認だけでは同時実行で二重Newが入り得て、`WageFundPolicy` は最初のNew起点で実効値を決めます。修正方針: `(OfficeId, MonthKey) where Kind=New` のpartial unique indexと重複Newテストを追加。"
- ADR 0015 が `DailyRecord` で同設計を採用済み（参照: `docs/decisions/0015-dailyrecord-duplicate-new-index.md`）。
- `WageFundPolicy.Effective` (`src/Tsumugi.Domain/Logic/WageFundPolicy.cs:5-27`) は `Kind == RecordKind.New` の最初のレコードを origin として履歴を辿る — 二重 New が許されると挙動が `CreatedAt` 順最古を選び続けるが、UseCase が「既存があれば Correction として記録する」ロジックなので、競合時に二重 New が物理的に格納されると将来の `Effective` 解釈が壊れる。
- `RecordKind.New = 1`（既存 ADR 0015 で同じ enum 値を確認済み）。partial filter は `"\\"Kind\\" = 1"` を使用。

---

### Task 1: ADR 0017 — WageFund 重複 New 防止

**Files:**
- Create: `docs/decisions/0017-wagefund-duplicate-new-index.md`

**Interfaces:**
- Consumes: 既存 `WageFund` entity、`WageFundConfiguration`、ADR 0015 のフォーマット
- Produces: Task 2/3 が参照する設計決定文書

- [ ] **Step 1.1: ADR ファイルを作成**

`docs/decisions/0017-wagefund-duplicate-new-index.md` を以下の内容で作成（ADR 0015 の完全な対称版、フォーマット・順序・トーンを揃える）:

```markdown
# ADR 0017: WageFund 重複 New を SQLite partial unique index で防止

## 結論

- `(OfficeId, Month)`（DB 列は `MonthKey`、`YYYYMM` 整数）かつ `Kind = 1`（`RecordKind.New`）の組み合わせを SQLite の partial unique index（`HasFilter("\"Kind\" = 1")`）で一意化する。
- `WageFundPolicy.Effective` および `SetWageFundUseCase` は変更しない（UseCase は依然 New/Correction を選択するが、最終的な一意性は DB 層で担保される）。

## 背景

- Codex Phase 2 レビュー M-3 指摘事項。`SetWageFundUseCase.ExecuteAsync` (`src/Tsumugi.Application/UseCases/Wage/SetWageFundUseCase.cs`) は「既存 effective を見つけたら Correction、無ければ New」のロジックだが、レース条件下で 2 つの並行リクエストがいずれも「既存なし」と判定し、二重 New を挿入してしまう。
- `WageFundPolicy.Effective` (`src/Tsumugi.Domain/Logic/WageFundPolicy.cs`) は `Kind == RecordKind.New` のレコードを `CreatedAt` 昇順で先頭から 1 つ origin に取る — 二重 New が物理的に格納されると、Correction 連鎖が分岐して履歴解釈が壊れる可能性がある。
- ADR 0015 が `DailyRecord` で完全に対称な問題に対して partial unique index を採用済み。本 ADR はそれを `WageFund` に適用する。
- `RecordKind.New = 1`（EF Core により `int` でストアされる）。partial filter は `"\"Kind\" = 1"` で記述する。

## 選択肢

1. **アプリケーション層の排他制御のみ**（現状）: レース条件を塞げない。
2. **DB ユニーク制約（全 Kind 対象）**: Correction/Cancel の同月複数挿入が不能になる。ドメイン要件（履歴追記）に合わない。
3. **Partial unique index（Kind=New のみ）**: New の二重挿入をアトミックに防止しつつ Correction/Cancel を許容する。

## 決定

選択肢 3 を採用。`WageFundConfiguration` で以下を宣言する。

```csharp
builder.HasIndex(r => new { r.OfficeId, r.Month })
    .HasFilter("\"Kind\" = 1")
    .IsUnique()
    .HasDatabaseName("UX_WageFunds_OfficeId_MonthKey_NewOnly");
```

`builder.HasIndex(r => r.OfficeId);` の単独 index は重複定義になるためそのままにせず、partial unique index で代替する想定であれば削除を検討する。ただし他のクエリ（事業所単独での検索）がこの単独 index を使う可能性があるため、本 ADR では `(OfficeId)` 単独 index は **温存** し、partial unique index を追加する形で並存させる（ADR 0015 では非 unique の `(RecipientId, ServiceDate)` 単独 index を削除したが、本件は `(OfficeId, Month)` 複合と `(OfficeId)` 単独で意味が異なる）。

## 影響

- Migration 1 件追加 (`{timestamp}_WageFundDuplicateNewIndex`)。アプリ起動時の `Database.Migrate()` が自動適用。
- `WageFund.Correction` / `Cancellation`（`Kind != 1`）には影響しない。
- 既存テストはすべて緑を維持。新規テスト 2 件追加（duplicate New 拒否 / Correction 許容）。
- `SetWageFundUseCase` は変更不要だが、レース条件下では `DbUpdateException` が発生する。これは想定挙動として呼び出し側（UI: `WageFundSettingsViewModel`）が `catch (DbUpdateException ex)` で再試行 or エラー表示することで対処する案がある（本 ADR スコープ外、open-questions に追記する案を report で検討）。
```

- [ ] **Step 1.2: ファイル整合チェックとコミット**

```bash
dotnet format --verify-no-changes
git add docs/decisions/0017-wagefund-duplicate-new-index.md
git commit -m "docs(phase2): ADR 0017 WageFund 重複 New を partial unique index で防止"
```

---

### Task 2: RED — `WageFundDuplicateNewIndexTests.cs` 新規作成

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/WageFundDuplicateNewIndexTests.cs`

**Interfaces:**
- Consumes: 既存 `SqliteFixture` (`tests/Tsumugi.Infrastructure.Tests/SqliteFixture.cs`)、`WageFund.NewRecord`/`Correction`、`TsumugiDbContext.WageFunds`、`Microsoft.Data.Sqlite.SqliteException`、`Microsoft.EntityFrameworkCore.DbUpdateException`
- Produces: 失敗する RED テスト 1 件（duplicate New 試行 → 現状は通ってしまう）+ 既存挙動を確認する GREEN テスト 1 件（Correction は許容）。Task 3 で前者を GREEN にする。

**事前メモ:**
- 既存 `DailyRecordDuplicateNewIndexTests.cs` をモデルとして同形で書く。
- `SqliteFixture.NewContext()` で新しい DbContext を取得（migration 自動適用済み）。
- `_fixture.NewContext()` は migration を呼ぶため、Task 2 時点では本テストの duplicate ケースは **GREEN にも RED にもならず、duplicate が成功して 2 件保存される** → アサーション `await act.Should().ThrowAsync<DbUpdateException>()` で **RED**（throw されない）になる。

- [ ] **Step 2.1: テストファイル新規作成**

`tests/Tsumugi.Infrastructure.Tests/WageFundDuplicateNewIndexTests.cs` を以下で作成:

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class WageFundDuplicateNewIndexTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;

    public WageFundDuplicateNewIndexTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Duplicate_new_record_for_same_office_and_month_is_rejected_by_index()
    {
        var officeId = Guid.NewGuid();
        var month = new YearMonth(2026, 7);
        await using var ctx = _fixture.NewContext();

        var first = WageFund.NewRecord(
            Guid.NewGuid(), officeId, month, totalYen: 100_000,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(first);
        await ctx.SaveChangesAsync();

        var duplicate = WageFund.NewRecord(
            Guid.NewGuid(), officeId, month, totalYen: 50_000,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(duplicate);

        var act = async () => await ctx.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }

    [Fact]
    public async Task Correction_record_for_same_office_and_month_is_allowed()
    {
        var officeId = Guid.NewGuid();
        var month = new YearMonth(2026, 7);
        await using var ctx = _fixture.NewContext();

        var newRec = WageFund.NewRecord(
            Guid.NewGuid(), officeId, month, totalYen: 100_000,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(newRec);
        await ctx.SaveChangesAsync();

        var correction = WageFund.Correction(
            Guid.NewGuid(), officeId, month, originId: newRec.Id, totalYen: 120_000,
            note: "訂正", createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        ctx.WageFunds.Add(correction);

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}
```

- [ ] **Step 2.2: ビルドと RED 確認**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~WageFundDuplicateNewIndexTests"
```

Expected:
- ビルド 0 warnings、format clean。
- `Duplicate_new_record_for_same_office_and_month_is_rejected_by_index`: **RED** (期待 throw、実際は 2 件挿入が成功して assertion 失敗)
- `Correction_record_for_same_office_and_month_is_allowed`: **GREEN** (現状でも通る)

つまり 1 RED / 1 GREEN（2 件中 1 件失敗）。

- [ ] **Step 2.3: RED 状態でコミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/WageFundDuplicateNewIndexTests.cs
git commit -m "test(phase2): M-3 RED - WageFund duplicate New index tests"
```

---

### Task 3: GREEN — `WageFundConfiguration` に partial unique index 追加 + migration 生成

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs:6-29`
- Create (via EF CLI): `src/Tsumugi.Infrastructure/Migrations/{timestamp}_WageFundDuplicateNewIndex.cs`
- Create (via EF CLI): `src/Tsumugi.Infrastructure/Migrations/{timestamp}_WageFundDuplicateNewIndex.Designer.cs`
- Modify (auto by EF CLI): `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: 既存 `WageFundConfiguration.Configure(EntityTypeBuilder<WageFund> builder)`、`dotnet ef migrations add` コマンド
- Produces: `(OfficeId, MonthKey) WHERE Kind=1` の partial unique index がデータ層に存在。Task 2 の RED テストが GREEN になる。

- [ ] **Step 3.1: `WageFundConfiguration.Configure` に partial unique index を追加**

`src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs` の `Configure` メソッドを以下に修正。**既存の `builder.HasIndex(r => r.OfficeId);` は温存**（ADR 0017 の決定）。partial unique index は **新規追加** する。

差し替え対象（現状）:
```csharp
        builder.HasIndex(r => r.OfficeId);
        builder.HasIndex(r => r.OriginId);
    }
}
```

新しい末尾:
```csharp
        builder.HasIndex(r => r.OfficeId);
        // partial unique index: 同一 (OfficeId, Month) の Kind=New（=1）を DB レベルで一意化する（ADR 0017）
        builder.HasIndex(r => new { r.OfficeId, r.Month })
            .HasFilter("\"Kind\" = 1")
            .IsUnique()
            .HasDatabaseName("UX_WageFunds_OfficeId_MonthKey_NewOnly");
        builder.HasIndex(r => r.OriginId);
    }
}
```

- [ ] **Step 3.2: EF Core migration を生成**

```bash
dotnet tool restore
dotnet ef migrations add WageFundDuplicateNewIndex \
    --project src/Tsumugi.Infrastructure \
    --startup-project src/Tsumugi.App
```

期待:
- `src/Tsumugi.Infrastructure/Migrations/{YYYYMMDDHHMMSS}_WageFundDuplicateNewIndex.cs` および同 `.Designer.cs` が新規作成される。
- `TsumugiDbContextModelSnapshot.cs` が自動更新される。
- 生成された `.cs` の `Up()` には `CreateIndex(name: "UX_WageFunds_OfficeId_MonthKey_NewOnly", table: "WageFunds", columns: new[] { "OfficeId", "MonthKey" }, unique: true, filter: "\"Kind\" = 1")` 相当が含まれる。`Down()` には `DropIndex` が含まれる。

**重要**: 生成物の内容を確認し、ADR 0015 の対応マイグレーションと同形であることを確認する。もし EF Core が `Kind = 1` ではなく `Kind = 0` 等の filter を生成したら STOP して報告（enum 値の不一致は重大）。

- [ ] **Step 3.3: ビルドと GREEN 確認**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~WageFundDuplicateNewIndexTests"
```

Expected:
- ビルド 0 warnings、format clean。
- 両テスト PASS（`Duplicate_new_record_for_same_office_and_month_is_rejected_by_index` が GREEN になる）。

- [ ] **Step 3.4: ソリューション全体テスト**

```bash
dotnet test
```

Expected: 全 PASS（baseline 464 + 2 新規 = 466 件、または相当数）。

特に `WageFundTests` / `SetWageFundUseCaseTests` / `WageFundSettingsViewModelTests` などの既存 WageFund 系テストが、新 index で破壊されていないことを確認。**もし「テスト内で 1 つのインメモリ DB に複数回 New を入れる」既存テストがあれば、それは新 index 違反で赤になる**。その場合は STOP して報告（既存テストの分離を見直す必要あり）。

- [ ] **Step 3.5: GREEN コミット**

```bash
git add src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs \
        src/Tsumugi.Infrastructure/Migrations/
git commit -m "fix(phase2): M-3 GREEN - WageFund duplicate New blocked by partial unique index (ADR 0017)"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- Codex M-3 修正方針「`(OfficeId, MonthKey) where Kind=New` のpartial unique indexと重複Newテストを追加」→ Task 2/3 で完全実装。
- ADR 0017 が ADR 0015 の対称設計として `docs/decisions/` に追加される。

**2. Placeholder scan:** TBD/TODO なし。EF migration は CLI 生成（timestamp は実行時決定）以外は具体提示。

**3. Type consistency:**
- `WageFund.NewRecord(Guid id, Guid officeId, YearMonth month, int totalYen, string? note, string createdBy, DateTimeOffset createdAt)` — Task 2 のテストと entity (`src/Tsumugi.Domain/Entities/WageFund.cs:15-29`) で一致。
- `WageFund.Correction(Guid id, Guid officeId, YearMonth month, Guid originId, int totalYen, string? note, string createdBy, DateTimeOffset createdAt)` — 同上 (`:35-47`)。
- EF Core `HasIndex(r => new { r.OfficeId, r.Month }).HasConversion` 経由で `Month` (`YearMonth`) は `MonthKey` (`int`) 列に変換済み（既存 configuration line 13-16）。複合 index 内で converter が機能することは EF Core 6+ 標準動作。
- `SqliteFixture.NewContext()` は migration を毎回 apply（既存 `DailyRecordDuplicateNewIndexTests` で実証済み）。

**4. 影響範囲:**
- Production code 1 ファイル (`WageFundConfiguration.cs`) + EF generated 3 ファイル + テスト 1 ファイル + ADR 1 ファイル。Domain/Application/App 不変。
- DI 不変。
- `WageFundPolicy` 不変。

**5. リスク:**
- 既存 `WageFundTests` 等で「同じ DbContext に複数 New をテスト目的で挿入」している箇所があれば赤化する。Step 3.4 で検出 → 報告 → 個別対処。
- `dotnet ef` ツールが `dotnet tool restore` で取得可能（CLAUDE.md のコマンド集に従う）。
