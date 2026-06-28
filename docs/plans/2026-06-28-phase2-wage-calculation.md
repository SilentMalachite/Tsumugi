# Phase 2 工賃計算 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 就労継続支援B型の工賃（作業実績→原資/設定→月次集計→確定スナップショット→帳票）を、現リポジトリの追記機構・UseCase 作法・アクセシビリティ既定の上に純粋関数で実装する。

**Architecture:**
- Domain には append-only entity（WorkRecord/WageFund/WageStatement/AuditEntry）・期間マスタ（WageSettings）・値オブジェクト（YearMonth）・実効導出（WorkRecordPolicy）・方式戦略（IWageMethodStrategy）・端数/余り（AllocationPolicy）・年度（FiscalYearPolicy）・平均工賃メトリクスを純粋関数で配置する。
- Application は既存 UseCase 作法 `(repo, IUnitOfWork, TimeProvider)` と `DateValidator` を踏襲し、DTO/Map で Domain を露出させない。確定（Close）は Domain の純粋関数結果を WageStatement の追記スナップショットとして固定する。
- Infrastructure は EF Configuration + Migration + Repository + AppendOnlyGuard 登録の Phase 1 と同形を新型に適用する。DailyRecord の `(RecipientId, ServiceDate, Kind=New)` partial unique index を追加する。
- Reporting は新 csproj `Tsumugi.Infrastructure.Reporting`（QuestPDF）で工賃明細・支払一覧を生成する。
- App は ViewModel 先行で Avalonia 画面を作る。AccessibilityDefaults を踏襲し、整数円・桁区切り（InvariantCulture）。

**Tech Stack:** .NET 10.0 / Avalonia 11.x / EF Core 10.x (SQLite) / CommunityToolkit.Mvvm / xUnit + FluentAssertions / QuestPDF（採否は Task A2 で ADR 化）。

## Global Constraints

- 全プロダクションアセンブリで通信 API を持ち込まない（`AppOfflineComplianceTests` が CI で機械判定。所属 allowlist は理由付きで最小。**国保連の伝送・電子証明書処理は Phase 2 でも実装しない**）。
- 報酬告示・CSV 仕様・**平均工賃月額の正式定義**などをハードコードしない。未確定は `docs/open-questions.md` に起票し、設計判断は `docs/decisions/` に ADR。
- 依存方向: `Domain ← Application ← Infrastructure / App`。Domain は EF/Avalonia/HTTP を一切知らない。`ArchitectureTests` で機械判定。
- すべてのエンティティは `Tsumugi.Domain.Entities.Entity` を継承し `record` で `Guid Id` / `DateTimeOffset CreatedAt` / `string CreatedBy` / `Guid ConcurrencyToken` を持つ。
- 新規追記型は必ず `Infrastructure.Persistence.AppendOnlyGuard.AppendOnlyTypes` に追加し、違反テストで赤確認する。
- 日付は `DateOnly`、両端含む暦日、`InvariantGlobalization=true`。金額は **整数円**。Domain で `double`/`float` を最終金額計算に使わない。比率は `decimal` までで止め、`RoundingRule` で整数化。
- 配分系（Hourly/Equal）は **Σ配分 = WageFund.TotalYen** を不変条件として保証し、テーブル駆動テストで固定する。
- 追記訂正（Correction/Cancel）は `Kind` と `OriginId` で表現し、`DailyRecordPolicy` と同型の実効導出を持つ。確定済みスナップショット（WageStatement）への下層訂正は **自動再計算しない**。再確定は `Correction` で履歴を残す。
- 整数 literal は計算ロジックに直接埋めない。tunable な閾値・既定値は WageSettings（期間マスタ）経由。**令和6年改定の報酬数値はゼロ参照**を維持。
- macOS / Windows 双方で `dotnet build` / `dotnet test` / `./build/ci.sh` 緑。`dotnet format --verify-no-changes` 通過。警告ゼロ・`Nullable enable`・`TreatWarningsAsErrors=true`。
- 主要操作はキーボードで完結。ダーク・低アニメ既定。`AccessibilityDefaults.Apply` の踏襲。金額表示は `InvariantCulture` ＋ 桁区切り `"N0"`。

---

## ファイル構造（新規・変更）

### 新規ファイル — Domain
- `src/Tsumugi.Domain/Enums/RoundingRule.cs` — 端数規則（FloorYen など）
- `src/Tsumugi.Domain/Enums/RemainderPolicy.cs` — 余り処理（LargestRemainder / ReserveToOffice）
- `src/Tsumugi.Domain/Enums/WageMethod.cs` — Piece / Hourly / Fixed / Equal
- `src/Tsumugi.Domain/Enums/AuditAction.cs` — Update / Register / Archive 等の最小集合
- `src/Tsumugi.Domain/ValueObjects/YearMonth.cs` — 年月（int Year, int Month）— 比較・前後・契約期間
- `src/Tsumugi.Domain/Entities/WorkRecord.cs`
- `src/Tsumugi.Domain/Entities/WageFund.cs`
- `src/Tsumugi.Domain/Entities/WageSettings.cs`
- `src/Tsumugi.Domain/Entities/WageStatement.cs`
- `src/Tsumugi.Domain/Entities/AuditEntry.cs`
- `src/Tsumugi.Domain/Logic/WorkRecordPolicy.cs`
- `src/Tsumugi.Domain/Logic/WageFundPolicy.cs` — 月次原資の実効値導出
- `src/Tsumugi.Domain/Logic/WageSettingsPolicy.cs` — 期間マスタの基準日 lookup（PeriodPolicy 同型）
- `src/Tsumugi.Domain/Logic/FiscalYearPolicy.cs` — 年度導出（FiscalYearStartMonth）
- `src/Tsumugi.Domain/Logic/AllocationPolicy.cs` — 配分（端数・余り）
- `src/Tsumugi.Domain/Logic/Wage/IWageMethodStrategy.cs` — 方式戦略 IF
- `src/Tsumugi.Domain/Logic/Wage/PieceWageStrategy.cs`
- `src/Tsumugi.Domain/Logic/Wage/HourlyWageStrategy.cs`
- `src/Tsumugi.Domain/Logic/Wage/FixedWageStrategy.cs`
- `src/Tsumugi.Domain/Logic/Wage/EqualWageStrategy.cs`
- `src/Tsumugi.Domain/Logic/WageCalculator.cs` — 月次集計エントリポイント
- `src/Tsumugi.Domain/Logic/AverageWageMetric.cs` — 平均工賃月額（暫定式、要・通知突合）

### 新規ファイル — Application
- `src/Tsumugi.Application/Abstractions/IWorkRecordRepository.cs`
- `src/Tsumugi.Application/Abstractions/IWageFundRepository.cs`
- `src/Tsumugi.Application/Abstractions/IWageSettingsRepository.cs`
- `src/Tsumugi.Application/Abstractions/IWageStatementRepository.cs`
- `src/Tsumugi.Application/Abstractions/IAuditEntryRepository.cs`
- `src/Tsumugi.Application/Dtos/WorkRecordDto.cs`
- `src/Tsumugi.Application/Dtos/WageFundDto.cs`
- `src/Tsumugi.Application/Dtos/WageSettingsDto.cs`
- `src/Tsumugi.Application/Dtos/WageStatementDto.cs`
- `src/Tsumugi.Application/Dtos/WagePreviewDto.cs`
- `src/Tsumugi.Application/UseCases/WorkRecord/RecordWorkUseCase.cs`
- `src/Tsumugi.Application/UseCases/WorkRecord/CorrectWorkUseCase.cs`
- `src/Tsumugi.Application/UseCases/WorkRecord/CancelWorkUseCase.cs`
- `src/Tsumugi.Application/UseCases/WorkRecord/QueryMonthWorkUseCase.cs`
- `src/Tsumugi.Application/UseCases/Wage/SetWageFundUseCase.cs`
- `src/Tsumugi.Application/UseCases/Wage/ConfigureWageSettingsUseCase.cs`
- `src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs`
- `src/Tsumugi.Application/UseCases/Wage/CloseWagesUseCase.cs`
- `src/Tsumugi.Application/UseCases/Wage/QueryWageStatementUseCase.cs`
- `src/Tsumugi.Application/Audit/IAuditTrail.cs` — UpdateXxx UseCase から呼ぶ追記抽象

### 新規ファイル — Infrastructure
- `src/Tsumugi.Infrastructure/Persistence/Configurations/WorkRecordConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/WageSettingsConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/WageStatementConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/WorkRecordRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/WageFundRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/WageSettingsRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/WageStatementRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/AuditEntryRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/AuditTrail.cs` — IAuditTrail 実装（AuditEntryRepository ラッパ）
- `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase2Wage.cs` — 1 マイグレーションに集約（DailyRecord の partial unique index も同時）

### 新規ファイル — Reporting
- `src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj`
- `src/Tsumugi.Infrastructure.Reporting/IWageReportGenerator.cs`（Application 側 IF として再配置検討、Task E2 で確定）
- `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`
- `src/Tsumugi.Infrastructure.Reporting/WagePaymentListPdfGenerator.cs`
- `src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs`

### 新規ファイル — App / UI
- `src/Tsumugi.App/ViewModels/WorkRecordViewModel.cs`
- `src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs`
- `src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs`
- `src/Tsumugi.App/ViewModels/WageStatementViewModel.cs`
- `src/Tsumugi.App/Views/WorkRecordView.axaml` + `.cs`
- `src/Tsumugi.App/Views/WageView.axaml` + `.cs`
- `src/Tsumugi.App/Formatting/YenFormatter.cs` — 整数円 `"N0"` 桁区切り

### 新規ファイル — テスト
- `tests/Tsumugi.Domain.Tests/YearMonthTests.cs`
- `tests/Tsumugi.Domain.Tests/FiscalYearPolicyTests.cs`
- `tests/Tsumugi.Domain.Tests/WorkRecordPolicyTests.cs`
- `tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs`
- `tests/Tsumugi.Domain.Tests/Wage/PieceWageStrategyTests.cs`
- `tests/Tsumugi.Domain.Tests/Wage/HourlyWageStrategyTests.cs`
- `tests/Tsumugi.Domain.Tests/Wage/FixedWageStrategyTests.cs`
- `tests/Tsumugi.Domain.Tests/Wage/EqualWageStrategyTests.cs`
- `tests/Tsumugi.Domain.Tests/WageCalculatorTests.cs`
- `tests/Tsumugi.Domain.Tests/AverageWageMetricTests.cs`
- `tests/Tsumugi.Application.Tests/WorkRecordUseCaseTests.cs`
- `tests/Tsumugi.Application.Tests/WageUseCaseTests.cs`
- `tests/Tsumugi.Application.Tests/AuditTrailWiringTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/WorkRecordRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/WageFundRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/WageSettingsRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/WageStatementRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/AuditEntryRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase2Tests.cs`
- `tests/Tsumugi.Infrastructure.Tests/DailyRecordDuplicateNewIndexTests.cs`
- `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`（新 csproj）
- `tests/Tsumugi.App.Tests/WorkRecordViewModelTests.cs`
- `tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs`
- `tests/Tsumugi.App.Tests/YenFormatterTests.cs`

### 変更ファイル
- `Tsumugi.sln` — 新 csproj（Reporting と Reporting.Tests）追加
- `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs` — DbSet 追加
- `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs` — AppendOnlyTypes 追加
- `src/Tsumugi.Infrastructure/DependencyInjection.cs` — 新 repo / IAuditTrail 登録
- `src/Tsumugi.App/CompositionRoot.cs` — UseCase / ViewModel / Reporting 登録
- `src/Tsumugi.App/Views/MainWindow.axaml` — タブ追加（作業実績 / 工賃）
- `src/Tsumugi.App/ViewModels/MainViewModel.cs` — タブ用 VM 配線
- `src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs` — `IAuditTrail` 注入
- `src/Tsumugi.Application/UseCases/Recipient/UpdateRecipientUseCase.cs` — `IAuditTrail` 注入
- `docs/open-questions.md` — Phase 2 項目を埋める / 追加
- `docs/decisions/0012-wage-calculation-strategy.md` — 新規 ADR
- `docs/decisions/0013-pdf-engine-questpdf.md` — 新規 ADR
- `docs/decisions/0014-audit-trail-append-only.md` — 新規 ADR
- `docs/decisions/0015-dailyrecord-duplicate-new-index.md` — 新規 ADR

---

## Phase A: Pre-flight（運用突合・整地・ライセンス）

### Task A1: 工賃方式・締め・端数・年度を open-questions と ADR0012 に固定

**Files:**
- Modify: `docs/open-questions.md`（工賃計算の既定エントリを Phase 2 詳細にリファイン）
- Create: `docs/decisions/0012-wage-calculation-strategy.md`

**Interfaces:**
- Produces: ADR 0012 で「方式戦略を**並存実装**し、既定方式は WageSettings の `Method` で事業所運用に委ねる」「丸めは `RoundingRule.FloorYen`、余りは `RemainderPolicy.LargestRemainder` を暫定既定とする」「`FiscalYearStartMonth=4`」を明文化。

- [ ] **Step 1: open-questions.md を更新**

既存の「工賃計算の既定（出来高/時間/固定）・締め日・端数規則の確定（事業所運用に依存）」を、以下に置き換える（節タイトル「Phase 2 工賃計算（2026-06-28 追加）」を新設）：

```markdown
## Phase 2 工賃計算（2026-06-28 追加）

- [ ] **KouchinModule.bas v5 の実挙動突合**: 旧 Excel の方式（Piece/Hourly/Fixed/Equal のどれを採用しているか）・締め日・端数規則・年度起点を一次情報として確認し、ADR 0012 を**確定**へ書き換える。ファイルがリポジトリに無いため、運用者からのコピー入手が前提。突合完了までは ADR 0012 の暫定既定値で実装を進めるが、**事業所運用に投入する前に再確認必須**。
- [ ] **平均工賃月額の正式定義**: 厚労省告示/通知の定義（分母＝延べ利用者 or 実利用者、基準期間、控除項目）を一次情報で確認。確定までは `AverageWageMetric` の暫定式を `[Obsolete("要・通知突合（暫定）")]` 相当のコメントで明示し、テストで形を固定（分母切替に強い構造）する。
- [ ] **QuestPDF ライセンス**: Community License の収益閾値・帰属表示要件を確認し、ADR 0013 で採否を確定。社会福祉事業の収益閾値超過リスクが大きい場合は Avalonia 印刷経路（PrintDialog → 視覚 Print）にフォールバックする判断を ADR に書く。
- [ ] **工賃確定後の下層訂正方針**: 自動再計算しない（Correction で履歴に残す）方針を ADR 0012 に併記済。次月調整 or 再確定の手順は運用ガイドへ。
```

- [ ] **Step 2: ADR 0012 を新規作成**

```markdown
# ADR 0012: 工賃計算の方式戦略・端数・年度起点

## 結論
- 方式戦略を 4 種類（Piece/Hourly/Fixed/Equal）並存で実装し、既定は `WageSettings.Method`（期間マスタ）で事業所運用に委ねる。
- 端数規則: `RoundingRule.FloorYen`（円未満切り捨て）を暫定既定。
- 余り処理（按分時の Σ＝原資 維持）: `RemainderPolicy.LargestRemainder`（最大剰余法、同点時は `RecipientId` 昇順）を暫定既定。`ReserveToOffice` も実装（残余を事業所留保）。
- 年度起点: `FiscalYearStartMonth=4`（日本会計年度）。
- 確定後の下層訂正は自動再計算しない。再確定は `WageStatement` の `Correction` で履歴に残す。

## 背景
- B型は非雇用であり最低賃金法の対象外。よって最低賃金チェックは入れない。
- KouchinModule.bas v5 の実挙動が一次情報。本 ADR は突合完了までの**暫定**で、突合後に「確定」へ書き換える。

## 選択肢
1. 既定方式を 1 つに固定 → 事業所運用ごとに変えにくい。却下。
2. 期間マスタ（WageSettings）に委ねて 4 方式並存 → **採用**。
3. プラグイン拡張で追加方式を許す → YAGNI。

## 影響
- Domain は `IWageMethodStrategy` で 4 実装を持つ。
- 報酬告示由来の数値は本 ADR に**含めない**（ハードコード禁止）。
- 突合後に open-questions の該当チェックボックスを閉じる。
```

- [ ] **Step 3: コミット**

```bash
git add docs/open-questions.md docs/decisions/0012-wage-calculation-strategy.md
git commit -m "docs(phase2): wage calculation strategy and open questions (ADR 0012)"
```

---

### Task A2: QuestPDF ライセンス確認と ADR 0013

**Files:**
- Create: `docs/decisions/0013-pdf-engine-questpdf.md`

**Interfaces:**
- Produces: QuestPDF Community License の閾値要件と、超過時のフォールバック（Avalonia 印刷）を明文化。後続 Task E1 以降のパッケージ追加判断の根拠となる。

- [ ] **Step 1: ADR 0013 を起草**

```markdown
# ADR 0013: PDF 生成エンジン（QuestPDF）の採否

## 結論
- QuestPDF Community License を採用する（採用条件: 事業所の年商が QuestPDF の Community 閾値を下回ること。閾値超過時は Avalonia 印刷経路へフォールバック）。
- フォールバック手順: View に `Print` コマンドを置き、Avalonia の `PrintDialog` → ビジュアル Print に切り替える（Reporting 層の IF を切り替える）。
- ライセンス設定は `Tsumugi.Infrastructure.Reporting.QuestPdfLicenseConfigurator` に集約し、構成は `appsettings`/環境変数ではなく**コードでハード設定**（オフライン保証）。

## 背景
- 仕様 §4.4: 「QuestPDF を採用。ライセンス条件を確認し ADR 化。不可なら Avalonia 印刷経路にフォールバック」。
- 完全オフライン制約のため、PDF 生成は決定的・依存ファイル最小・日本語フォント対応必須。

## 選択肢
1. QuestPDF（Community License）→ **採用**。
2. iText7 → AGPL/商用の二重ライセンス、商用が高額。却下。
3. PdfSharpCore → 機能/レイアウト表現力が弱い。却下。
4. Avalonia 印刷のみ → フォールバックとして保持。

## 影響
- `Tsumugi.Infrastructure.Reporting` を新 csproj として分離（App 直リンクで合成）。
- テストは抽出テキスト + 合計金額で検証（バイト一致は環境差で揺れるため避ける）。
```

- [ ] **Step 2: コミット**

```bash
git add docs/decisions/0013-pdf-engine-questpdf.md
git commit -m "docs(phase2): adopt QuestPDF with fallback policy (ADR 0013)"
```

---

### Task A3: DailyRecord 重複 New を SQLite partial unique index で防止

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/DailyRecordDuplicateNewIndexTests.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/DailyRecordConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_DailyRecordDuplicateNewIndex.cs`（`dotnet ef migrations add` で生成）
- Create: `docs/decisions/0015-dailyrecord-duplicate-new-index.md`

**Interfaces:**
- Produces: 同一 `(RecipientId, ServiceDate)` かつ `Kind=0`（`RecordKind.New`）の 2 件目挿入が SQLite 例外で失敗する。`DailyRecordPolicy.Effective` は変更しない（多重 New が物理的に発生しなくなるため、既存仕様の決定論は無関係）。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class DailyRecordDuplicateNewIndexTests
{
    [Fact]
    public async Task Duplicate_new_record_for_same_recipient_and_date_is_rejected_by_index()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var recipientId = await harness.SeedRecipientAsync();
        var date = new DateOnly(2026, 7, 1);

        var first = DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, date,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        harness.Db.DailyRecords.Add(first);
        await harness.Db.SaveChangesAsync();

        var duplicate = DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, date,
            Attendance.Absent, TransportKind.None, mealProvided: false,
            note: null, createdBy: "tester", createdAt: DateTimeOffset.UtcNow);
        harness.Db.DailyRecords.Add(duplicate);

        var act = async () => await harness.Db.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.WithInnerException<SqliteException>()
          .Which.SqliteErrorCode.Should().Be(19, "UNIQUE constraint failed");
    }

    [Fact]
    public async Task Correction_record_for_same_date_is_allowed()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var recipientId = await harness.SeedRecipientAsync();
        var date = new DateOnly(2026, 7, 1);

        var newRec = DailyRecord.NewRecord(
            Guid.NewGuid(), recipientId, date,
            Attendance.Present, TransportKind.None, false, null,
            "tester", DateTimeOffset.UtcNow);
        harness.Db.DailyRecords.Add(newRec);
        await harness.Db.SaveChangesAsync();

        var correction = DailyRecord.Correction(
            Guid.NewGuid(), recipientId, date, newRec.Id,
            Attendance.Absent, TransportKind.None, false, "訂正",
            "tester", DateTimeOffset.UtcNow);
        harness.Db.DailyRecords.Add(correction);

        var act = async () => await harness.Db.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }
}
```

`SqliteTestHarness` は既存 Phase 1 テスト群と同形（in-memory SQLite + `EnsureCreated` ではなく `Database.Migrate`）。既存ヘルパが無ければ既存リポジトリテストから流用する。

- [ ] **Step 2: テストを赤確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~DailyRecordDuplicateNewIndexTests" -v normal
```

期待: 2 ケース失敗（インデックス未追加のため重複 New が通る／対象テーブルがマイグレートされていない）。

- [ ] **Step 3: DailyRecordConfiguration に partial unique index を追加**

```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/DailyRecordConfiguration.cs に追記
builder.HasIndex(r => new { r.RecipientId, r.ServiceDate })
    .HasFilter("\"Kind\" = 0")  // RecordKind.New（0）に限定する partial unique index
    .IsUnique()
    .HasDatabaseName("UX_DailyRecords_RecipientId_ServiceDate_NewOnly");

// 既存の非 unique index（行頭の HasIndex(r => new { r.RecipientId, r.ServiceDate });）は
// この unique index と重複するため削除する。
```

- [ ] **Step 4: マイグレーションを発行**

```bash
dotnet ef migrations add DailyRecordDuplicateNewIndex \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

生成ファイルを確認し、`migrationBuilder.CreateIndex(... filter: "\"Kind\" = 0", ...)` が含まれることを確認。

- [ ] **Step 5: テストを緑確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~DailyRecordDuplicateNewIndexTests" -v normal
```

期待: 2 ケース緑。

- [ ] **Step 6: ADR 0015 を作成**

```markdown
# ADR 0015: DailyRecord 重複 New を SQLite partial unique index で防止

## 結論
- `(RecipientId, ServiceDate)` かつ `Kind = 0`（`RecordKind.New`）の組み合わせを SQLite の partial unique index（`HasFilter("\"Kind\" = 0")`）で一意化する。
- `DailyRecordPolicy.Effective` は変更しない。

## 背景
- Phase 1 → Phase 2 引継ぎの open-questions 項目。`RecordDailyRecordUseCase` のチェックはレース条件下で多重 New を許してしまう。
- 工賃計算（Phase 2）は実効レコードを合算する前提のため、データ層で一意性を担保する必要がある。

## 影響
- Migration 1 件追加。アプリ起動時の `Database.Migrate()` が自動適用。
- Correction/Cancel（`Kind != 0`）には影響しない。
```

- [ ] **Step 7: コミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/DailyRecordDuplicateNewIndexTests.cs \
        src/Tsumugi.Infrastructure/Persistence/Configurations/DailyRecordConfiguration.cs \
        src/Tsumugi.Infrastructure/Migrations \
        docs/decisions/0015-dailyrecord-duplicate-new-index.md
git commit -m "feat(phase2): partial unique index for DailyRecord new records (ADR 0015)"
```

---

## Phase B: Domain（TDD 中心）

### Task B1: Enums と YearMonth 値オブジェクト

**Files:**
- Create: `src/Tsumugi.Domain/Enums/WageMethod.cs`, `RoundingRule.cs`, `RemainderPolicy.cs`, `AuditAction.cs`
- Create: `src/Tsumugi.Domain/ValueObjects/YearMonth.cs`
- Create: `tests/Tsumugi.Domain.Tests/YearMonthTests.cs`

**Interfaces:**
- Produces: `WageMethod { Piece, Hourly, Fixed, Equal }` / `RoundingRule { FloorYen, RoundHalfAwayFromZeroYen, CeilYen }` / `RemainderPolicy { LargestRemainder, ReserveToOffice }` / `AuditAction { Update, Register, Archive }`。
- Produces: `readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>` — コンストラクタは 1900-01〜2200-12 を強制。`FirstDay()`/`LastDay()` を `DateOnly` で返す。`Next()`/`Previous()`/`FromDate(DateOnly)` 静的ヘルパを持つ。

- [ ] **Step 1: YearMonth テストを書く（赤）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class YearMonthTests
{
    [Theory]
    [InlineData(2026, 7, 2026, 7, 1, 2026, 7, 31)]
    [InlineData(2026, 2, 2026, 2, 1, 2026, 2, 28)]
    [InlineData(2024, 2, 2024, 2, 1, 2024, 2, 29)]  // 閏年
    [InlineData(2026, 12, 2026, 12, 1, 2026, 12, 31)]
    public void First_and_last_day(int y, int m, int fy, int fm, int fd, int ly, int lm, int ld)
    {
        var ym = new YearMonth(y, m);
        ym.FirstDay().Should().Be(new DateOnly(fy, fm, fd));
        ym.LastDay().Should().Be(new DateOnly(ly, lm, ld));
    }

    [Theory]
    [InlineData(2026, 12, 2027, 1)]
    [InlineData(2026, 1, 2026, 2)]
    public void Next_wraps_year(int y, int m, int ey, int em)
        => new YearMonth(y, m).Next().Should().Be(new YearMonth(ey, em));

    [Theory]
    [InlineData(2026, 1, 2025, 12)]
    public void Previous_wraps_year(int y, int m, int ey, int em)
        => new YearMonth(y, m).Previous().Should().Be(new YearMonth(ey, em));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1899, 12)]
    [InlineData(2201, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void Out_of_range_throws(int y, int m)
        => FluentActions.Invoking(() => new YearMonth(y, m))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void FromDate_returns_year_and_month()
        => YearMonth.FromDate(new DateOnly(2026, 7, 15))
            .Should().Be(new YearMonth(2026, 7));

    [Fact]
    public void Comparable_ordering()
    {
        var a = new YearMonth(2026, 6);
        var b = new YearMonth(2026, 7);
        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);
    }
}
```

- [ ] **Step 2: テストを赤確認**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj \
  --filter "FullyQualifiedName~YearMonthTests"
```

期待: コンパイル失敗（YearMonth 未定義）。

- [ ] **Step 3: YearMonth と Enums を実装**

```csharp
// src/Tsumugi.Domain/Enums/WageMethod.cs
namespace Tsumugi.Domain.Enums;
public enum WageMethod { Piece, Hourly, Fixed, Equal }
```

```csharp
// src/Tsumugi.Domain/Enums/RoundingRule.cs
namespace Tsumugi.Domain.Enums;
public enum RoundingRule { FloorYen, RoundHalfAwayFromZeroYen, CeilYen }
```

```csharp
// src/Tsumugi.Domain/Enums/RemainderPolicy.cs
namespace Tsumugi.Domain.Enums;
public enum RemainderPolicy { LargestRemainder, ReserveToOffice }
```

```csharp
// src/Tsumugi.Domain/Enums/AuditAction.cs
namespace Tsumugi.Domain.Enums;
public enum AuditAction { Register, Update, Archive, Restore }
```

```csharp
// src/Tsumugi.Domain/ValueObjects/YearMonth.cs
namespace Tsumugi.Domain.ValueObjects;

/// <summary>年月。両端含む暦日範囲 [FirstDay, LastDay] に対応する不可変値オブジェクト。</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public int Year { get; }
    public int Month { get; }

    public YearMonth(int year, int month)
    {
        if (year < 1900 || year > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), year, "年は1900〜2200の範囲で指定してください。");
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "月は1〜12の範囲で指定してください。");
        Year = year;
        Month = month;
    }

    public DateOnly FirstDay() => new(Year, Month, 1);
    public DateOnly LastDay() => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public YearMonth Next() => Month == 12 ? new YearMonth(Year + 1, 1) : new YearMonth(Year, Month + 1);
    public YearMonth Previous() => Month == 1 ? new YearMonth(Year - 1, 12) : new YearMonth(Year, Month - 1);

    public static YearMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    public int CompareTo(YearMonth other)
    {
        var byYear = Year.CompareTo(other.Year);
        return byYear != 0 ? byYear : Month.CompareTo(other.Month);
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
```

- [ ] **Step 4: テスト緑確認**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj \
  --filter "FullyQualifiedName~YearMonthTests"
```

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.Domain/Enums src/Tsumugi.Domain/ValueObjects/YearMonth.cs \
        tests/Tsumugi.Domain.Tests/YearMonthTests.cs
git commit -m "feat(domain): YearMonth value object and wage/rounding/audit enums"
```

---

### Task B2: WorkRecord エンティティとファクトリ

**Files:**
- Create: `src/Tsumugi.Domain/Entities/WorkRecord.cs`
- Create: `tests/Tsumugi.Domain.Tests/WorkRecordTests.cs`

**Interfaces:**
- Produces: `sealed record WorkRecord : Entity` — `RecipientId / WorkDate(DateOnly) / Kind(RecordKind) / OriginId(Guid?) / WorkedMinutes(int?) / PieceCount(int?) / PieceUnitYen(int?) / Points(int?) / Note(string?)`。
- Produces: 静的ファクトリ `NewRecord(...)` / `Correction(originId, ...)` / `Cancellation(originId, ...)` — `DailyRecord` と同型シグネチャ。

- [ ] **Step 1: テストを書く（赤）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WorkRecordTests
{
    private static readonly DateTimeOffset Clock = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewRecord_sets_kind_and_clears_origin()
    {
        var id = Guid.NewGuid();
        var rec = WorkRecord.NewRecord(
            id, recipientId: Guid.NewGuid(), workDate: new DateOnly(2026, 7, 1),
            workedMinutes: 240, pieceCount: null, pieceUnitYen: null, points: null,
            note: null, createdBy: "tester", createdAt: Clock);

        rec.Id.Should().Be(id);
        rec.Kind.Should().Be(RecordKind.New);
        rec.OriginId.Should().BeNull();
        rec.WorkedMinutes.Should().Be(240);
        rec.PieceCount.Should().BeNull();
    }

    [Fact]
    public void Correction_carries_origin()
    {
        var origin = Guid.NewGuid();
        var rec = WorkRecord.Correction(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 1), originId: origin,
            workedMinutes: 200, pieceCount: null, pieceUnitYen: null, points: null,
            note: "訂正", createdBy: "tester", createdAt: Clock);
        rec.Kind.Should().Be(RecordKind.Correct);
        rec.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancellation_zeroes_measurements()
    {
        var origin = Guid.NewGuid();
        var rec = WorkRecord.Cancellation(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 1), origin,
            createdBy: "tester", createdAt: Clock);
        rec.Kind.Should().Be(RecordKind.Cancel);
        rec.OriginId.Should().Be(origin);
        rec.WorkedMinutes.Should().BeNull();
        rec.PieceCount.Should().BeNull();
        rec.PieceUnitYen.Should().BeNull();
        rec.Points.Should().BeNull();
    }
}
```

- [ ] **Step 2: テストを赤確認**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WorkRecordTests"
```

- [ ] **Step 3: WorkRecord を実装**

```csharp
// src/Tsumugi.Domain/Entities/WorkRecord.cs
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 作業実績（取引記録・厳密追記）。DailyRecord と同型の追記機構。
/// 計測値は方式に応じて null 可（方式ごとに使う列が変わる）。
/// </summary>
public sealed record WorkRecord : Entity
{
    public required Guid RecipientId { get; init; }
    public required DateOnly WorkDate { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public int? WorkedMinutes { get; init; }
    public int? PieceCount { get; init; }
    public int? PieceUnitYen { get; init; }
    public int? Points { get; init; }
    public string? Note { get; init; }

    public static WorkRecord NewRecord(
        Guid id, Guid recipientId, DateOnly workDate,
        int? workedMinutes, int? pieceCount, int? pieceUnitYen, int? points,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id, RecipientId = recipientId, WorkDate = workDate,
            Kind = RecordKind.New, OriginId = null,
            WorkedMinutes = workedMinutes, PieceCount = pieceCount,
            PieceUnitYen = pieceUnitYen, Points = points,
            Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    public static WorkRecord Correction(
        Guid id, Guid recipientId, DateOnly workDate, Guid originId,
        int? workedMinutes, int? pieceCount, int? pieceUnitYen, int? points,
        string? note, string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id, RecipientId = recipientId, WorkDate = workDate,
            Kind = RecordKind.Correct, OriginId = originId,
            WorkedMinutes = workedMinutes, PieceCount = pieceCount,
            PieceUnitYen = pieceUnitYen, Points = points,
            Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    public static WorkRecord Cancellation(
        Guid id, Guid recipientId, DateOnly workDate, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id, RecipientId = recipientId, WorkDate = workDate,
            Kind = RecordKind.Cancel, OriginId = originId,
            WorkedMinutes = null, PieceCount = null, PieceUnitYen = null, Points = null,
            Note = null,
            CreatedBy = createdBy, CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
}
```

- [ ] **Step 4: テスト緑確認**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WorkRecordTests"
```

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.Domain/Entities/WorkRecord.cs tests/Tsumugi.Domain.Tests/WorkRecordTests.cs
git commit -m "feat(domain): WorkRecord append-only entity with New/Correction/Cancellation factories"
```

---

### Task B3: WorkRecordPolicy（実効レコード導出）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/WorkRecordPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/WorkRecordPolicyTests.cs`

**Interfaces:**
- Produces: `static class WorkRecordPolicy { WorkRecord? Effective(IEnumerable<WorkRecord>); IReadOnlyDictionary<DateOnly, WorkRecord> EffectiveByDate(IEnumerable<WorkRecord>); }` — `DailyRecordPolicy` と完全同型。同一 OriginId の兄弟は `CreatedAt` 最新優先、`Cancel` で実効 null。

- [ ] **Step 1: テストを書く（DailyRecordPolicyTests を参照してケースを並べる、赤）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WorkRecordPolicyTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 7, 1);
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private static WorkRecord N(int minutes, DateTimeOffset at, Guid? id = null) =>
        WorkRecord.NewRecord(id ?? Guid.NewGuid(), Recipient, Date,
            minutes, null, null, null, null, "tester", at);

    private static WorkRecord C(Guid origin, int minutes, DateTimeOffset at) =>
        WorkRecord.Correction(Guid.NewGuid(), Recipient, Date, origin,
            minutes, null, null, null, "訂正", "tester", at);

    private static WorkRecord X(Guid origin, DateTimeOffset at) =>
        WorkRecord.Cancellation(Guid.NewGuid(), Recipient, Date, origin, "tester", at);

    [Fact]
    public void Empty_returns_null()
        => WorkRecordPolicy.Effective(Array.Empty<WorkRecord>()).Should().BeNull();

    [Fact]
    public void Single_new_is_effective()
    {
        var n = N(240, T0);
        WorkRecordPolicy.Effective(new[] { n }).Should().Be(n);
    }

    [Fact]
    public void Latest_correction_wins_among_siblings()
    {
        var n = N(240, T0);
        var c1 = C(n.Id, 200, T0.AddMinutes(1));
        var c2 = C(n.Id, 180, T0.AddMinutes(2));  // 最新
        WorkRecordPolicy.Effective(new[] { n, c1, c2 }).Should().Be(c2);
    }

    [Fact]
    public void Cancellation_makes_effective_null()
    {
        var n = N(240, T0);
        var x = X(n.Id, T0.AddMinutes(1));
        WorkRecordPolicy.Effective(new[] { n, x }).Should().BeNull();
    }

    [Fact]
    public void EffectiveByDate_groups_per_day()
    {
        var d1 = new DateOnly(2026, 7, 1);
        var d2 = new DateOnly(2026, 7, 2);
        var n1 = WorkRecord.NewRecord(Guid.NewGuid(), Recipient, d1, 240, null, null, null, null, "t", T0);
        var n2 = WorkRecord.NewRecord(Guid.NewGuid(), Recipient, d2, 360, null, null, null, null, "t", T0);

        var byDate = WorkRecordPolicy.EffectiveByDate(new[] { n1, n2 });
        byDate.Should().HaveCount(2);
        byDate[d1].Should().Be(n1);
        byDate[d2].Should().Be(n2);
    }
}
```

- [ ] **Step 2: テストを赤確認 → 実装**

```csharp
// src/Tsumugi.Domain/Logic/WorkRecordPolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>作業実績の訂正・取消の実効状態を導出する純粋関数。DailyRecordPolicy と同型。</summary>
public static class WorkRecordPolicy
{
    public static WorkRecord? Effective(IEnumerable<WorkRecord> records)
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
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null) return current;
            if (next.Kind == RecordKind.Cancel) return null;
            current = next;
        }
    }

    public static IReadOnlyDictionary<DateOnly, WorkRecord> EffectiveByDate(
        IEnumerable<WorkRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var result = new Dictionary<DateOnly, WorkRecord>();
        foreach (var group in records.GroupBy(r => r.WorkDate))
            if (Effective(group) is { } eff) result[group.Key] = eff;
        return result;
    }
}
```

- [ ] **Step 3: テスト緑確認**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WorkRecordPolicyTests"
```

- [ ] **Step 4: コミット**

```bash
git add src/Tsumugi.Domain/Logic/WorkRecordPolicy.cs tests/Tsumugi.Domain.Tests/WorkRecordPolicyTests.cs
git commit -m "feat(domain): WorkRecordPolicy effective derivation (mirror of DailyRecordPolicy)"
```

---

### Task B4: WageFund エンティティ（月次原資・追記訂正）

**Files:**
- Create: `src/Tsumugi.Domain/Entities/WageFund.cs`
- Create: `src/Tsumugi.Domain/Logic/WageFundPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/WageFundTests.cs`

**Interfaces:**
- Produces: `sealed record WageFund : Entity` — `OfficeId(Guid) / Month(YearMonth) / TotalYen(int) / Kind(RecordKind) / OriginId(Guid?) / Note(string?)`。ファクトリ `NewRecord(...)`/`Correction(originId, ...)`/`Cancellation(originId, ...)`。
- Produces: `static class WageFundPolicy { WageFund? Effective(IEnumerable<WageFund>); }` — `(OfficeId, Month)` ごとに 1 件の実効値を返す。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageFundTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewRecord_is_new_kind()
    {
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, Month, totalYen: 300000, note: null, "tester", T);
        fund.TotalYen.Should().Be(300000);
        fund.Kind.Should().Be(Domain.Enums.RecordKind.New);
        fund.OriginId.Should().BeNull();
    }

    [Fact]
    public void NewRecord_negative_total_throws()
        => FluentActions.Invoking(() => WageFund.NewRecord(
                Guid.NewGuid(), Office, Month, totalYen: -1, note: null, "t", T))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact]
    public void Effective_picks_latest_correction()
    {
        var n = WageFund.NewRecord(Guid.NewGuid(), Office, Month, 300000, null, "t", T);
        var c = WageFund.Correction(Guid.NewGuid(), Office, Month, n.Id, 280000, null, "t", T.AddMinutes(1));
        WageFundPolicy.Effective(new[] { n, c })!.TotalYen.Should().Be(280000);
    }

    [Fact]
    public void Cancellation_yields_null_effective()
    {
        var n = WageFund.NewRecord(Guid.NewGuid(), Office, Month, 300000, null, "t", T);
        var x = WageFund.Cancellation(Guid.NewGuid(), Office, Month, n.Id, "t", T.AddMinutes(1));
        WageFundPolicy.Effective(new[] { n, x }).Should().BeNull();
    }
}
```

- [ ] **Step 2: 赤確認 → WageFund と WageFundPolicy を実装**

```csharp
// src/Tsumugi.Domain/Entities/WageFund.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>月次の工賃原資（追記訂正）。OfficeId × Month で一意の実効値を持つ。</summary>
public sealed record WageFund : Entity
{
    public required Guid OfficeId { get; init; }
    public required YearMonth Month { get; init; }
    public required int TotalYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static WageFund NewRecord(
        Guid id, Guid officeId, YearMonth month, int totalYen,
        string? note, string createdBy, DateTimeOffset createdAt)
    {
        if (totalYen < 0)
            throw new ArgumentOutOfRangeException(nameof(totalYen), totalYen, "工賃原資は0円以上で指定してください。");
        return new WageFund
        {
            Id = id, OfficeId = officeId, Month = month, TotalYen = totalYen,
            Kind = RecordKind.New, OriginId = null, Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.Empty,
        };
    }

    public static WageFund Correction(
        Guid id, Guid officeId, YearMonth month, Guid originId, int totalYen,
        string? note, string createdBy, DateTimeOffset createdAt)
    {
        if (totalYen < 0)
            throw new ArgumentOutOfRangeException(nameof(totalYen), totalYen, "工賃原資は0円以上で指定してください。");
        return new WageFund
        {
            Id = id, OfficeId = officeId, Month = month, TotalYen = totalYen,
            Kind = RecordKind.Correct, OriginId = originId, Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.Empty,
        };
    }

    public static WageFund Cancellation(
        Guid id, Guid officeId, YearMonth month, Guid originId,
        string createdBy, DateTimeOffset createdAt) => new()
        {
            Id = id, OfficeId = officeId, Month = month, TotalYen = 0,
            Kind = RecordKind.Cancel, OriginId = originId, Note = null,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.Empty,
        };
}
```

```csharp
// src/Tsumugi.Domain/Logic/WageFundPolicy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

public static class WageFundPolicy
{
    public static WageFund? Effective(IEnumerable<WageFund> records)
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
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            if (next is null) return current;
            if (next.Kind == RecordKind.Cancel) return null;
            current = next;
        }
    }
}
```

- [ ] **Step 3: 緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WageFundTests"
git add src/Tsumugi.Domain/Entities/WageFund.cs src/Tsumugi.Domain/Logic/WageFundPolicy.cs \
        tests/Tsumugi.Domain.Tests/WageFundTests.cs
git commit -m "feat(domain): WageFund append-only entity and effective derivation"
```

---

### Task B5: WageSettings 期間マスタ

**Files:**
- Create: `src/Tsumugi.Domain/Entities/WageSettings.cs`
- Create: `src/Tsumugi.Domain/Logic/WageSettingsPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/WageSettingsTests.cs`

**Interfaces:**
- Produces: `sealed record WageSettings : Entity` — `OfficeId(Guid) / Period(DateRange) / Method(WageMethod) / Rounding(RoundingRule) / Remainder(RemainderPolicy) / FiscalYearStartMonth(int=4) / FixedDailyYen(int?)`。期間マスタとして `Period` を持つため append-only。
- Produces: `static class WageSettingsPolicy { WageSettings? Effective(IEnumerable<WageSettings>, DateOnly asOf); }` — `Period.Contains(asOf)` の最新（`CreatedAt` 降順）を返す。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageSettingsTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WageSettings Settings(DateRange period, WageMethod method, int? fixedYen = null) =>
        new()
        {
            Id = Guid.NewGuid(), OfficeId = Office, Period = period, Method = method,
            Rounding = RoundingRule.FloorYen, Remainder = RemainderPolicy.LargestRemainder,
            FiscalYearStartMonth = 4, FixedDailyYen = fixedYen,
            CreatedAt = T, CreatedBy = "tester", ConcurrencyToken = Guid.NewGuid(),
        };

    [Fact]
    public void Effective_returns_settings_whose_period_contains_asOf()
    {
        var s1 = Settings(new DateRange(new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)), WageMethod.Equal);
        var s2 = Settings(new DateRange(new DateOnly(2026, 4, 1), null), WageMethod.Hourly);
        WageSettingsPolicy.Effective(new[] { s1, s2 }, new DateOnly(2026, 7, 1)).Should().Be(s2);
        WageSettingsPolicy.Effective(new[] { s1, s2 }, new DateOnly(2025, 5, 1)).Should().Be(s1);
    }

    [Fact]
    public void Effective_returns_null_when_no_period_contains_asOf()
    {
        var s1 = Settings(new DateRange(new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)), WageMethod.Equal);
        WageSettingsPolicy.Effective(new[] { s1 }, new DateOnly(2027, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void Fixed_method_without_fixed_daily_yen_throws()
    {
        var s = new
        {
            Id = Guid.NewGuid(), OfficeId = Office,
            Period = new DateRange(new DateOnly(2026, 4, 1), null),
            Method = WageMethod.Fixed, Rounding = RoundingRule.FloorYen,
            Remainder = RemainderPolicy.LargestRemainder, FiscalYearStartMonth = 4,
            FixedDailyYen = (int?)null,
        };
        FluentActions.Invoking(() => WageSettings.Create(
            s.Id, s.OfficeId, s.Period, s.Method, s.Rounding, s.Remainder,
            s.FiscalYearStartMonth, s.FixedDailyYen, "tester", T))
            .Should().Throw<ArgumentException>()
            .WithMessage("*FixedDailyYen*");
    }
}
```

- [ ] **Step 2: 赤確認 → 実装**

```csharp
// src/Tsumugi.Domain/Entities/WageSettings.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>工賃設定（期間マスタ、追記）。基準日時点の方式・端数規則・年度起点を引く。</summary>
public sealed record WageSettings : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateRange Period { get; init; }
    public required WageMethod Method { get; init; }
    public required RoundingRule Rounding { get; init; }
    public required RemainderPolicy Remainder { get; init; }
    public required int FiscalYearStartMonth { get; init; }
    public int? FixedDailyYen { get; init; }

    public static WageSettings Create(
        Guid id, Guid officeId, DateRange period,
        WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
        int fiscalYearStartMonth, int? fixedDailyYen,
        string createdBy, DateTimeOffset createdAt)
    {
        if (fiscalYearStartMonth < 1 || fiscalYearStartMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth),
                fiscalYearStartMonth, "年度起点月は1〜12の範囲で指定してください。");
        if (method == WageMethod.Fixed && fixedDailyYen is null)
            throw new ArgumentException("Fixed 方式では FixedDailyYen を指定してください。", nameof(fixedDailyYen));
        if (fixedDailyYen is { } y && y < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedDailyYen), y, "FixedDailyYen は0円以上で指定してください。");
        return new WageSettings
        {
            Id = id, OfficeId = officeId, Period = period,
            Method = method, Rounding = rounding, Remainder = remainder,
            FiscalYearStartMonth = fiscalYearStartMonth, FixedDailyYen = fixedDailyYen,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.NewGuid(),
        };
    }
}
```

```csharp
// src/Tsumugi.Domain/Logic/WageSettingsPolicy.cs
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public static class WageSettingsPolicy
{
    public static WageSettings? Effective(IEnumerable<WageSettings> records, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records
            .Where(s => s.Period.Contains(asOf))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 3: 緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WageSettingsTests"
git add src/Tsumugi.Domain/Entities/WageSettings.cs src/Tsumugi.Domain/Logic/WageSettingsPolicy.cs \
        tests/Tsumugi.Domain.Tests/WageSettingsTests.cs
git commit -m "feat(domain): WageSettings period master with effective lookup"
```

---

### Task B6: FiscalYearPolicy（年度導出）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/FiscalYearPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/FiscalYearPolicyTests.cs`

**Interfaces:**
- Produces: `static class FiscalYearPolicy { int Year(DateOnly date, int startMonth); YearMonth FiscalYearStart(int fiscalYear, int startMonth); YearMonth FiscalYearEnd(int fiscalYear, int startMonth); }`。
- 例: `startMonth=4` の場合、`2026-04-01〜2027-03-31` は会計年度 2026。

- [ ] **Step 1: テストを書く**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class FiscalYearPolicyTests
{
    [Theory]
    [InlineData(2026, 3, 31, 4, 2025)]
    [InlineData(2026, 4, 1, 4, 2026)]
    [InlineData(2027, 1, 31, 4, 2026)]
    [InlineData(2027, 3, 31, 4, 2026)]
    [InlineData(2027, 4, 1, 4, 2027)]
    [InlineData(2026, 12, 31, 1, 2026)]  // 暦年運用
    public void Fiscal_year_for_calendar_dates(int y, int m, int d, int startMonth, int expected)
        => FiscalYearPolicy.Year(new DateOnly(y, m, d), startMonth).Should().Be(expected);

    [Theory]
    [InlineData(2026, 4, 2026, 4, 2027, 3)]
    public void Fiscal_year_start_and_end(int fy, int startMonth, int sy, int sm, int ey, int em)
    {
        FiscalYearPolicy.FiscalYearStart(fy, startMonth).Should().Be(new YearMonth(sy, sm));
        FiscalYearPolicy.FiscalYearEnd(fy, startMonth).Should().Be(new YearMonth(ey, em));
    }
}
```

- [ ] **Step 2: 赤 → 実装**

```csharp
// src/Tsumugi.Domain/Logic/FiscalYearPolicy.cs
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

public static class FiscalYearPolicy
{
    public static int Year(DateOnly date, int startMonth)
    {
        if (startMonth < 1 || startMonth > 12)
            throw new ArgumentOutOfRangeException(nameof(startMonth), startMonth, "年度起点月は1〜12の範囲で指定してください。");
        return date.Month >= startMonth ? date.Year : date.Year - 1;
    }

    public static YearMonth FiscalYearStart(int fiscalYear, int startMonth)
        => new(fiscalYear, startMonth);

    public static YearMonth FiscalYearEnd(int fiscalYear, int startMonth)
    {
        var startNextYear = startMonth == 1 ? fiscalYear + 1 : fiscalYear + 1;
        var endMonth = startMonth == 1 ? 12 : startMonth - 1;
        var endYear = startMonth == 1 ? fiscalYear : startNextYear;
        return new YearMonth(endYear, endMonth);
    }
}
```

- [ ] **Step 3: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~FiscalYearPolicyTests"
git add src/Tsumugi.Domain/Logic/FiscalYearPolicy.cs tests/Tsumugi.Domain.Tests/FiscalYearPolicyTests.cs
git commit -m "feat(domain): fiscal year derivation policy"
```

---

### Task B7: AllocationPolicy（端数・余り）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/AllocationPolicy.cs`
- Create: `tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs`

**Interfaces:**
- Produces: `static class AllocationPolicy { IReadOnlyList<(Guid Key, int AmountYen)> Allocate(IReadOnlyList<(Guid Key, decimal Weight)> shares, int totalYen, RoundingRule rounding, RemainderPolicy remainder, Guid? officeReserveKey = null); }`。
- 不変条件: `Σ AmountYen == totalYen`（`ReserveToOffice` の場合は `officeReserveKey` への配分を含めて等しい）。
- `Weight` が全て 0 または `shares.Count == 0` の場合は全員 0 円。`ReserveToOffice` のときに余りが `officeReserveKey` に上乗せされる。`LargestRemainder` は剰余降順 → 同点時は `Key.CompareTo` 昇順で 1 円ずつ。

- [ ] **Step 1: テストを書く（テーブル駆動 + 不変条件プロパティ）**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class AllocationPolicyTests
{
    private static (Guid Key, decimal Weight) S(int idx, decimal w) =>
        (new Guid($"00000000-0000-0000-0000-{idx:D12}"), w);

    private static int Sum(IReadOnlyList<(Guid, int)> allocs) => allocs.Sum(a => a.Item2);

    [Fact]
    public void Empty_shares_returns_empty()
    {
        var r = AllocationPolicy.Allocate(
            Array.Empty<(Guid, decimal)>(), 1000, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        r.Should().BeEmpty();
    }

    [Fact]
    public void All_zero_weights_yield_zero_amounts()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 0m), S(2, 0m) }, 1000,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        r.Should().AllSatisfy(t => t.AmountYen.Should().Be(0));
    }

    [Fact]
    public void Even_split_when_weights_equal()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 1m), S(2, 1m), S(3, 1m) }, 300,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        Sum(r).Should().Be(300);
        r.Should().AllSatisfy(t => t.AmountYen.Should().Be(100));
    }

    [Fact]
    public void Largest_remainder_distributes_leftover_yen()
    {
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 1m), S(2, 1m), S(3, 1m) }, 100,
            RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        Sum(r).Should().Be(100);
        // floor 33 + 余り 1 を Key 昇順 1人へ → 34, 33, 33
        r[0].AmountYen.Should().Be(34);
        r[1].AmountYen.Should().Be(33);
        r[2].AmountYen.Should().Be(33);
    }

    [Fact]
    public void Reserve_to_office_dumps_remainder_to_office_key()
    {
        var officeKey = new Guid("00000000-0000-0000-0000-000099999999");
        var r = AllocationPolicy.Allocate(
            new[] { S(1, 1m), S(2, 1m), S(3, 1m) }, 100,
            RoundingRule.FloorYen, RemainderPolicy.ReserveToOffice, officeKey);
        Sum(r).Should().Be(100);
        r.Should().Contain(t => t.Key == officeKey && t.AmountYen == 1);
        r.Where(t => t.Key != officeKey).Sum(t => t.AmountYen).Should().Be(99);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(7, 100)]
    [InlineData(13, 100000)]
    [InlineData(17, 99991)]
    public void Sum_invariant_holds_for_random_weights(int count, int total)
    {
        var rng = new Random(seed: count * 31 + total);
        var shares = Enumerable.Range(1, count)
            .Select(i => S(i, (decimal)rng.NextDouble() * 100))
            .ToArray();
        var r = AllocationPolicy.Allocate(
            shares, total, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder);
        Sum(r).Should().Be(total, "Σ AmountYen == totalYen 不変条件");
        r.Should().AllSatisfy(t => t.AmountYen.Should().BeGreaterThanOrEqualTo(0));
    }
}
```

- [ ] **Step 2: 赤 → 実装**

```csharp
// src/Tsumugi.Domain/Logic/AllocationPolicy.cs
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>配分の端数・余りを決定的に処理する純粋関数。Σ AmountYen == totalYen を保証する。</summary>
public static class AllocationPolicy
{
    public static IReadOnlyList<(Guid Key, int AmountYen)> Allocate(
        IReadOnlyList<(Guid Key, decimal Weight)> shares,
        int totalYen,
        RoundingRule rounding,
        RemainderPolicy remainder,
        Guid? officeReserveKey = null)
    {
        ArgumentNullException.ThrowIfNull(shares);
        if (totalYen < 0) throw new ArgumentOutOfRangeException(nameof(totalYen));
        if (remainder == RemainderPolicy.ReserveToOffice && officeReserveKey is null)
            throw new ArgumentException("ReserveToOffice では officeReserveKey が必要です。", nameof(officeReserveKey));
        if (shares.Count == 0) return Array.Empty<(Guid, int)>();

        var totalWeight = shares.Sum(s => s.Weight);
        if (totalWeight <= 0m)
            return shares.Select(s => (s.Key, 0)).ToArray();

        // 比率と整数化後の差分を保持
        var raw = shares
            .Select(s => (s.Key, Exact: (decimal)totalYen * s.Weight / totalWeight))
            .ToArray();

        int[] floored = raw.Select(r => Round(r.Exact, rounding)).ToArray();
        int allocated = floored.Sum();
        int leftover = totalYen - allocated;  // 切り捨て分。CeilYen の場合は負になり得る

        if (leftover == 0)
            return raw.Zip(floored, (r, f) => (r.Key, f)).ToList();

        if (remainder == RemainderPolicy.ReserveToOffice)
        {
            var result = raw.Zip(floored, (r, f) => (Key: r.Key, Amount: f)).ToList();
            var officeIndex = result.FindIndex(t => t.Key == officeReserveKey!.Value);
            if (officeIndex < 0)
            {
                result.Add((officeReserveKey!.Value, leftover));
            }
            else
            {
                result[officeIndex] = (result[officeIndex].Key, result[officeIndex].Amount + leftover);
            }
            return result.Select(t => (t.Key, t.Amount)).ToList();
        }

        // LargestRemainder: 剰余降順 → Key 昇順
        var ordered = raw
            .Select((r, i) => (Index: i, r.Key, Fraction: r.Exact - floored[i]))
            .OrderByDescending(t => t.Fraction)
            .ThenBy(t => t.Key)
            .ToArray();

        int step = leftover > 0 ? 1 : -1;
        int remaining = Math.Abs(leftover);
        for (int i = 0; i < remaining; i++)
        {
            floored[ordered[i % ordered.Length].Index] += step;
        }

        return raw.Zip(floored, (r, f) => (r.Key, f)).ToList();
    }

    private static int Round(decimal exact, RoundingRule rule) => rule switch
    {
        RoundingRule.FloorYen => (int)Math.Floor(exact),
        RoundingRule.CeilYen => (int)Math.Ceiling(exact),
        RoundingRule.RoundHalfAwayFromZeroYen => (int)Math.Round(exact, MidpointRounding.AwayFromZero),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "未対応の RoundingRule"),
    };
}
```

- [ ] **Step 3: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~AllocationPolicyTests"
git add src/Tsumugi.Domain/Logic/AllocationPolicy.cs tests/Tsumugi.Domain.Tests/AllocationPolicyTests.cs
git commit -m "feat(domain): allocation with deterministic rounding and remainder policy"
```

---

### Task B8: 方式戦略 IF と 4 実装（Piece/Hourly/Fixed/Equal）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/Wage/IWageMethodStrategy.cs`
- Create: `src/Tsumugi.Domain/Logic/Wage/WageInputs.cs`（共通入力レコード）
- Create: `src/Tsumugi.Domain/Logic/Wage/PieceWageStrategy.cs`
- Create: `src/Tsumugi.Domain/Logic/Wage/HourlyWageStrategy.cs`
- Create: `src/Tsumugi.Domain/Logic/Wage/FixedWageStrategy.cs`
- Create: `src/Tsumugi.Domain/Logic/Wage/EqualWageStrategy.cs`
- Create: `tests/Tsumugi.Domain.Tests/Wage/PieceWageStrategyTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Wage/HourlyWageStrategyTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Wage/FixedWageStrategyTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/Wage/EqualWageStrategyTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record WageInputs(
      Guid RecipientId,
      int PresentDays,                 // 実効 DailyRecord で Attendance.Present の日数
      int TotalWorkedMinutes,          // 実効 WorkRecord の合計作業時間
      int TotalPieceAmountYen,         // 実効 WorkRecord の Σ(PieceCount × PieceUnitYen)
      int TotalPoints);                // 評価点合計（将来用、未使用なら0）

  public sealed record WageLineItem(Guid RecipientId, int AmountYen, string BasisSummary);

  public interface IWageMethodStrategy
  {
      WageMethod Method { get; }
      IReadOnlyList<WageLineItem> Calculate(
          IReadOnlyList<WageInputs> inputs,
          WageFund? fund,                 // Fund を使わない方式（Piece/Fixed）では null 可
          WageSettings settings);
  }
  ```
- Strategy ごとの素計算:
  - **Piece**: `inputs[i].TotalPieceAmountYen` をそのまま採用。`BasisSummary = $"出来高: {pieceCount}点×{unit}円"` 相当の集計を返す（合計のみで可）。
  - **Hourly**: `WageFund.TotalYen` を `inputs[i].TotalWorkedMinutes` 比で配分。`AllocationPolicy` を呼ぶ。
  - **Fixed**: `inputs[i].PresentDays * settings.FixedDailyYen` をそのまま採用（按分なし）。
  - **Equal**: `WageFund.TotalYen` を `inputs[i].PresentDays > 0` の対象者で均等割（重み = `PresentDays > 0 ? 1 : 0`）。`AllocationPolicy` を呼ぶ。

- [ ] **Step 1: Piece テスト → 実装 → 緑 → コミット**

テスト:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class PieceWageStrategyTests
{
    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Guid.NewGuid(),
        new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Piece, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder,
        fiscalYearStartMonth: 4, fixedDailyYen: null,
        "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Calculates_per_recipient_from_piece_amount()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), PresentDays: 10, TotalWorkedMinutes: 1200, TotalPieceAmountYen: 5_400, TotalPoints: 0),
            new WageInputs(Guid.NewGuid(), PresentDays: 8, TotalWorkedMinutes: 960, TotalPieceAmountYen: 3_120, TotalPoints: 0),
        };
        var lines = new PieceWageStrategy().Calculate(inputs, fund: null, Settings());
        lines.Should().HaveCount(2);
        lines[0].AmountYen.Should().Be(5_400);
        lines[1].AmountYen.Should().Be(3_120);
        lines.Sum(l => l.AmountYen).Should().Be(8_520);
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Logic/Wage/IWageMethodStrategy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

public sealed record WageInputs(
    Guid RecipientId,
    int PresentDays,
    int TotalWorkedMinutes,
    int TotalPieceAmountYen,
    int TotalPoints);

public sealed record WageLineItem(Guid RecipientId, int AmountYen, string BasisSummary);

public interface IWageMethodStrategy
{
    WageMethod Method { get; }
    IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs,
        WageFund? fund,
        WageSettings settings);
}
```

```csharp
// src/Tsumugi.Domain/Logic/Wage/PieceWageStrategy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

public sealed class PieceWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Piece;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId, i.TotalPieceAmountYen,
                $"出来高方式: 合計{i.TotalPieceAmountYen:N0}円"))
            .ToArray();
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~PieceWageStrategyTests"
git add src/Tsumugi.Domain/Logic/Wage/IWageMethodStrategy.cs \
        src/Tsumugi.Domain/Logic/Wage/PieceWageStrategy.cs \
        tests/Tsumugi.Domain.Tests/Wage/PieceWageStrategyTests.cs
git commit -m "feat(domain): Piece wage strategy"
```

- [ ] **Step 2: Hourly テスト → 実装 → 緑 → コミット**

テスト（按分・Σ＝原資 不変条件含む）:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class HourlyWageStrategyTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "t", T);

    private static WageFund Fund(int yen) =>
        WageFund.NewRecord(Guid.NewGuid(), Office, Month, yen, null, "t", T);

    [Fact]
    public void Distributes_proportional_to_worked_minutes_and_invariant_holds()
    {
        var a = new WageInputs(Guid.NewGuid(), 10, 600, 0, 0);
        var b = new WageInputs(Guid.NewGuid(), 10, 400, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(100_000), Settings());
        lines.Sum(l => l.AmountYen).Should().Be(100_000);
        lines.First(l => l.RecipientId == a.RecipientId).AmountYen.Should().Be(60_000);
        lines.First(l => l.RecipientId == b.RecipientId).AmountYen.Should().Be(40_000);
    }

    [Fact]
    public void Single_recipient_takes_full_fund()
    {
        var only = new WageInputs(Guid.NewGuid(), 10, 600, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { only }, Fund(99_991), Settings());
        lines.Single().AmountYen.Should().Be(99_991);
    }

    [Fact]
    public void All_zero_minutes_yields_all_zero()
    {
        var a = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
        var b = new WageInputs(Guid.NewGuid(), 0, 0, 0, 0);
        var lines = new HourlyWageStrategy().Calculate(new[] { a, b }, Fund(100_000), Settings());
        lines.Should().AllSatisfy(l => l.AmountYen.Should().Be(0));
    }

    [Fact]
    public void Fund_required_for_hourly()
    {
        var only = new WageInputs(Guid.NewGuid(), 10, 600, 0, 0);
        FluentActions.Invoking(() => new HourlyWageStrategy().Calculate(new[] { only }, fund: null, Settings()))
            .Should().Throw<ArgumentNullException>().WithParameterName("fund");
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Logic/Wage/HourlyWageStrategy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

public sealed class HourlyWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Hourly;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fund);

        var shares = inputs
            .Select(i => (i.RecipientId, (decimal)i.TotalWorkedMinutes))
            .ToArray();

        var alloc = AllocationPolicy.Allocate(
            shares, fund.TotalYen, settings.Rounding, settings.Remainder,
            officeReserveKey: settings.Remainder == RemainderPolicy.ReserveToOffice ? settings.OfficeId : null);

        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId,
                alloc.First(a => a.Key == i.RecipientId).AmountYen,
                $"時間割方式: {i.TotalWorkedMinutes}分 / 原資{fund.TotalYen:N0}円"))
            .ToArray();
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~HourlyWageStrategyTests"
git add src/Tsumugi.Domain/Logic/Wage/HourlyWageStrategy.cs tests/Tsumugi.Domain.Tests/Wage/HourlyWageStrategyTests.cs
git commit -m "feat(domain): Hourly wage strategy with proportional allocation"
```

- [ ] **Step 3: Fixed テスト → 実装 → 緑 → コミット**

テスト:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class FixedWageStrategyTests
{
    private static WageSettings Settings(int fixedYen) => WageSettings.Create(
        Guid.NewGuid(), Guid.NewGuid(),
        new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Fixed, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, fixedYen,
        "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Multiplies_present_days_by_fixed_daily_yen()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), 10, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),
        };
        var lines = new FixedWageStrategy().Calculate(inputs, fund: null, Settings(500));
        lines[0].AmountYen.Should().Be(5000);
        lines[1].AmountYen.Should().Be(0);
    }

    [Fact]
    public void Throws_if_fixed_daily_yen_missing()
    {
        var s = WageSettings.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateRange(new DateOnly(2026, 4, 1), null),
            WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null,
            "tester", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));
        var only = new WageInputs(Guid.NewGuid(), 10, 0, 0, 0);
        FluentActions.Invoking(() => new FixedWageStrategy().Calculate(new[] { only }, null, s))
            .Should().Throw<InvalidOperationException>().WithMessage("*Fixed*");
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Logic/Wage/FixedWageStrategy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

public sealed class FixedWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Fixed;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.FixedDailyYen is not { } daily)
            throw new InvalidOperationException("Fixed 方式では WageSettings.FixedDailyYen が必要です。");
        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId, i.PresentDays * daily,
                $"固定方式: {i.PresentDays}日×{daily:N0}円"))
            .ToArray();
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~FixedWageStrategyTests"
git add src/Tsumugi.Domain/Logic/Wage/FixedWageStrategy.cs tests/Tsumugi.Domain.Tests/Wage/FixedWageStrategyTests.cs
git commit -m "feat(domain): Fixed wage strategy"
```

- [ ] **Step 4: Equal テスト → 実装 → 緑 → コミット**

テスト:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests.Wage;

public sealed class EqualWageStrategyTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Equal, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "t", T);

    private static WageFund Fund(int yen) =>
        WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), yen, null, "t", T);

    [Fact]
    public void Splits_equally_among_present_recipients()
    {
        var inputs = new[]
        {
            new WageInputs(Guid.NewGuid(), 10, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 5, 0, 0, 0),
            new WageInputs(Guid.NewGuid(), 0, 0, 0, 0),  // 出席日数 0 は対象外
        };
        var lines = new EqualWageStrategy().Calculate(inputs, Fund(100), Settings());
        lines.Sum(l => l.AmountYen).Should().Be(100);
        lines.Where(l => l.AmountYen > 0).Should().HaveCount(2);
        // 50:50, 余り 0
        lines.Where(l => l.AmountYen > 0).Select(l => l.AmountYen).Should().AllBeEquivalentTo(50);
        lines.First(l => l.RecipientId == inputs[2].RecipientId).AmountYen.Should().Be(0);
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Logic/Wage/EqualWageStrategy.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

public sealed class EqualWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Equal;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fund);

        var shares = inputs
            .Select(i => (i.RecipientId, Weight: i.PresentDays > 0 ? 1m : 0m))
            .ToArray();

        var alloc = AllocationPolicy.Allocate(
            shares, fund.TotalYen, settings.Rounding, settings.Remainder,
            officeReserveKey: settings.Remainder == RemainderPolicy.ReserveToOffice ? settings.OfficeId : null);

        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId,
                alloc.First(a => a.Key == i.RecipientId).AmountYen,
                $"均等方式: 出席{i.PresentDays}日 / 対象者で均等割"))
            .ToArray();
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~EqualWageStrategyTests"
git add src/Tsumugi.Domain/Logic/Wage/EqualWageStrategy.cs tests/Tsumugi.Domain.Tests/Wage/EqualWageStrategyTests.cs
git commit -m "feat(domain): Equal wage strategy"
```

---

### Task B9: WageCalculator（月次集計エントリポイント）と WageStatement / AuditEntry

**Files:**
- Create: `src/Tsumugi.Domain/Entities/WageStatement.cs`
- Create: `src/Tsumugi.Domain/Entities/AuditEntry.cs`
- Create: `src/Tsumugi.Domain/Logic/WageBasisExtractor.cs`
- Create: `src/Tsumugi.Domain/Logic/WageCalculator.cs`
- Create: `tests/Tsumugi.Domain.Tests/WageStatementTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/AuditEntryTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/WageBasisExtractorTests.cs`
- Create: `tests/Tsumugi.Domain.Tests/WageCalculatorTests.cs`

**Interfaces:**
- Produces: `sealed record WageStatement : Entity` — `OfficeId / Month / RecipientId / AmountYen(int) / BasisSummary(string) / Kind(RecordKind) / OriginId(Guid?)`。`NewRecord` / `Correction` ファクトリ。
- Produces: `sealed record AuditEntry : Entity` — `Actor(string) / Action(AuditAction) / TargetType(string) / TargetId(Guid) / OccurredAt(DateTimeOffset) / Summary(string?)`。`Create(...)` ファクトリ。
- Produces: `static class WageBasisExtractor { IReadOnlyList<WageInputs> Build(IEnumerable<DailyRecord> dailyRecords, IEnumerable<WorkRecord> workRecords, YearMonth month); }` — 実効 DailyRecord の `Attendance.Present` 日数と実効 WorkRecord を `RecipientId` で集計。
- Produces: `static class WageCalculator { IReadOnlyList<WageLineItem> Calculate(IReadOnlyList<IWageMethodStrategy> strategies, WageMethod method, IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings); }`。

- [ ] **Step 1: WageStatement と AuditEntry のテスト → 実装 → コミット**

テスト（最小）:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageStatementTests
{
    [Fact]
    public void NewRecord_is_new_kind()
    {
        var s = WageStatement.NewRecord(
            Guid.NewGuid(), Guid.NewGuid(), new YearMonth(2026, 7), Guid.NewGuid(),
            amountYen: 12_345, basisSummary: "時間割: 600分", createdBy: "tester",
            createdAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        s.Kind.Should().Be(RecordKind.New);
        s.OriginId.Should().BeNull();
        s.AmountYen.Should().Be(12_345);
    }

    [Fact]
    public void Negative_amount_throws()
    {
        FluentActions.Invoking(() => WageStatement.NewRecord(
            Guid.NewGuid(), Guid.NewGuid(), new YearMonth(2026, 7), Guid.NewGuid(),
            -1, "x", "t", DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}

public sealed class AuditEntryTests
{
    [Fact]
    public void Create_records_all_fields()
    {
        var t = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var target = Guid.NewGuid();
        var e = AuditEntry.Create(
            Guid.NewGuid(), actor: "alice", action: AuditAction.Update,
            targetType: "Office", targetId: target,
            occurredAt: t, summary: "officeName changed", createdAt: t, createdBy: "alice");
        e.Actor.Should().Be("alice");
        e.Action.Should().Be(AuditAction.Update);
        e.TargetType.Should().Be("Office");
        e.TargetId.Should().Be(target);
        e.OccurredAt.Should().Be(t);
        e.Summary.Should().Be("officeName changed");
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Entities/WageStatement.cs
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>工賃確定スナップショット（取引記録・厳密追記）。再確定は Correction で履歴に残す。</summary>
public sealed record WageStatement : Entity
{
    public required Guid OfficeId { get; init; }
    public required YearMonth Month { get; init; }
    public required Guid RecipientId { get; init; }
    public required int AmountYen { get; init; }
    public required string BasisSummary { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }

    public static WageStatement NewRecord(
        Guid id, Guid officeId, YearMonth month, Guid recipientId,
        int amountYen, string basisSummary, string createdBy, DateTimeOffset createdAt)
    {
        if (amountYen < 0)
            throw new ArgumentOutOfRangeException(nameof(amountYen), amountYen, "工賃額は0円以上で指定してください。");
        ArgumentException.ThrowIfNullOrEmpty(basisSummary);
        return new WageStatement
        {
            Id = id, OfficeId = officeId, Month = month, RecipientId = recipientId,
            AmountYen = amountYen, BasisSummary = basisSummary,
            Kind = RecordKind.New, OriginId = null,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.Empty,
        };
    }

    public static WageStatement Correction(
        Guid id, Guid officeId, YearMonth month, Guid recipientId, Guid originId,
        int amountYen, string basisSummary, string createdBy, DateTimeOffset createdAt)
    {
        if (amountYen < 0)
            throw new ArgumentOutOfRangeException(nameof(amountYen), amountYen, "工賃額は0円以上で指定してください。");
        ArgumentException.ThrowIfNullOrEmpty(basisSummary);
        return new WageStatement
        {
            Id = id, OfficeId = officeId, Month = month, RecipientId = recipientId,
            AmountYen = amountYen, BasisSummary = basisSummary,
            Kind = RecordKind.Correct, OriginId = originId,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.Empty,
        };
    }
}
```

```csharp
// src/Tsumugi.Domain/Entities/AuditEntry.cs
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>同一性マスタ更新時の監査追記。誰が・いつ・何を・概要を残す。</summary>
public sealed record AuditEntry : Entity
{
    public required string Actor { get; init; }
    public required AuditAction Action { get; init; }
    public required string TargetType { get; init; }
    public required Guid TargetId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Summary { get; init; }

    public static AuditEntry Create(
        Guid id, string actor, AuditAction action,
        string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary,
        DateTimeOffset createdAt, string createdBy)
    {
        ArgumentException.ThrowIfNullOrEmpty(actor);
        ArgumentException.ThrowIfNullOrEmpty(targetType);
        return new AuditEntry
        {
            Id = id, Actor = actor, Action = action,
            TargetType = targetType, TargetId = targetId,
            OccurredAt = occurredAt, Summary = summary,
            CreatedAt = createdAt, CreatedBy = createdBy, ConcurrencyToken = Guid.Empty,
        };
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj \
  --filter "FullyQualifiedName~WageStatementTests|FullyQualifiedName~AuditEntryTests"
git add src/Tsumugi.Domain/Entities/WageStatement.cs src/Tsumugi.Domain/Entities/AuditEntry.cs \
        tests/Tsumugi.Domain.Tests/WageStatementTests.cs tests/Tsumugi.Domain.Tests/AuditEntryTests.cs
git commit -m "feat(domain): WageStatement snapshot and AuditEntry append-only"
```

- [ ] **Step 2: WageBasisExtractor のテスト → 実装 → 緑 → コミット**

テスト:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageBasisExtractorTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();
    private static readonly YearMonth Month = new(2026, 7);
    private static readonly DateTimeOffset T = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Counts_only_present_days_per_recipient()
    {
        var d = new[]
        {
            DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T),
            DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), Attendance.Absent, TransportKind.None, false, null, "t", T),
            DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 3), Attendance.AbsenceSupport, TransportKind.None, false, null, "t", T),
            DailyRecord.NewRecord(Guid.NewGuid(), R2, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T),
        };
        var w = Array.Empty<WorkRecord>();
        var inputs = WageBasisExtractor.Build(d, w, Month);

        inputs.Should().HaveCount(2);
        inputs.First(i => i.RecipientId == R1).PresentDays.Should().Be(1);
        inputs.First(i => i.RecipientId == R2).PresentDays.Should().Be(1);
    }

    [Fact]
    public void Aggregates_worked_minutes_and_piece_amounts_from_effective_work_records()
    {
        var d1 = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var w1 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 1), workedMinutes: 240, pieceCount: 5, pieceUnitYen: 100, points: 0, note: null, "t", T);
        var w2 = WorkRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 7, 2), workedMinutes: 180, pieceCount: 3, pieceUnitYen: 100, points: 0, note: null, "t", T);

        var inputs = WageBasisExtractor.Build(new[] { d1 }, new[] { w1, w2 }, Month);

        inputs.Should().HaveCount(1);
        var i = inputs[0];
        i.TotalWorkedMinutes.Should().Be(420);
        i.TotalPieceAmountYen.Should().Be(800);
        i.PresentDays.Should().Be(1);  // DailyRecord 由来
    }

    [Fact]
    public void Filters_out_months_outside_target()
    {
        var d = DailyRecord.NewRecord(Guid.NewGuid(), R1, new DateOnly(2026, 8, 1), Attendance.Present, TransportKind.None, false, null, "t", T);
        var inputs = WageBasisExtractor.Build(new[] { d }, Array.Empty<WorkRecord>(), Month);
        inputs.Should().BeEmpty();
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Logic/WageBasisExtractor.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

/// <summary>
/// 月次の工賃基礎を抽出する純粋関数。
/// 出席日数は実効 DailyRecord で Attendance.Present のもののみ、
/// 作業実績量は実効 WorkRecord から合算する。
/// </summary>
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
                var presentDays = effDaily.Count(r => r.Attendance == Attendance.Present);

                var effWork = workByRecipient.TryGetValue(rid, out var ww)
                    ? WorkRecordPolicy.EffectiveByDate(ww).Values
                    : Enumerable.Empty<WorkRecord>();
                var totalMinutes = effWork.Sum(w => w.WorkedMinutes ?? 0);
                var totalPiece = effWork.Sum(w => (w.PieceCount ?? 0) * (w.PieceUnitYen ?? 0));
                var totalPoints = effWork.Sum(w => w.Points ?? 0);

                return new WageInputs(rid, presentDays, totalMinutes, totalPiece, totalPoints);
            })
            .ToArray();
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WageBasisExtractorTests"
git add src/Tsumugi.Domain/Logic/WageBasisExtractor.cs tests/Tsumugi.Domain.Tests/WageBasisExtractorTests.cs
git commit -m "feat(domain): wage basis extractor from effective DailyRecord + WorkRecord"
```

- [ ] **Step 3: WageCalculator テスト → 実装 → 緑 → コミット**

テスト:
```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class WageCalculatorTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    private static WageSettings Settings(WageMethod m, int? fixedYen = null) => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        m, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, fixedYen, "t", T);

    [Fact]
    public void Selects_strategy_matching_settings_method()
    {
        var inputs = new[] { new WageInputs(Guid.NewGuid(), 10, 600, 0, 0) };
        var fund = WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), 50_000, null, "t", T);
        var lines = WageCalculator.Calculate(AllStrategies, WageMethod.Hourly, inputs, fund, Settings(WageMethod.Hourly));
        lines.Should().HaveCount(1);
        lines[0].AmountYen.Should().Be(50_000);
    }

    [Fact]
    public void Throws_if_strategy_for_method_not_registered()
    {
        var inputs = new[] { new WageInputs(Guid.NewGuid(), 10, 600, 0, 0) };
        var onlyPiece = new IWageMethodStrategy[] { new PieceWageStrategy() };
        FluentActions.Invoking(() => WageCalculator.Calculate(
                onlyPiece, WageMethod.Hourly, inputs, null, Settings(WageMethod.Hourly)))
            .Should().Throw<InvalidOperationException>().WithMessage("*Hourly*");
    }
}
```

実装:
```csharp
// src/Tsumugi.Domain/Logic/WageCalculator.cs
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;

namespace Tsumugi.Domain.Logic;

/// <summary>方式戦略の選択と委譲のみを行う純粋関数（数値計算は各戦略が持つ）。</summary>
public static class WageCalculator
{
    public static IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<IWageMethodStrategy> strategies,
        WageMethod method,
        IReadOnlyList<WageInputs> inputs,
        WageFund? fund,
        WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);

        var strategy = strategies.FirstOrDefault(s => s.Method == method)
            ?? throw new InvalidOperationException(
                $"工賃方式 {method} に対応する戦略が登録されていません。");
        return strategy.Calculate(inputs, fund, settings);
    }
}
```

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~WageCalculatorTests"
git add src/Tsumugi.Domain/Logic/WageCalculator.cs tests/Tsumugi.Domain.Tests/WageCalculatorTests.cs
git commit -m "feat(domain): WageCalculator dispatching to method strategy"
```

---

### Task B10: AverageWageMetric（暫定式、要・通知突合）

**Files:**
- Create: `src/Tsumugi.Domain/Logic/AverageWageMetric.cs`
- Create: `tests/Tsumugi.Domain.Tests/AverageWageMetricTests.cs`

**Interfaces:**
- Produces: `static class AverageWageMetric { int Calculate(IReadOnlyList<WageStatement> statements, AverageWageDenominator denominator); }`。
  - `AverageWageDenominator { TotalRecipients, ActiveRecipients }` の暫定 enum。
  - `TotalRecipients`: 月数 × 対象者数（延べ）
  - `ActiveRecipients`: ユニーク利用者数
- `Calculate` は内部に `// FIXME(open-questions): 正式定義は厚労省告示/通知突合で確定。本暫定式は分母の差し替えに強い構造で実装。` のコメントを必ず残す。

- [ ] **Step 1: テストを書く**

```csharp
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class AverageWageMetricTests
{
    private static readonly Guid Office = Guid.NewGuid();
    private static readonly DateTimeOffset T = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();

    private static WageStatement S(Guid recipient, YearMonth month, int yen) =>
        WageStatement.NewRecord(Guid.NewGuid(), Office, month, recipient, yen, "test", "t", T);

    [Fact]
    public void TotalRecipients_uses_sum_count_pairs()
    {
        var stmts = new[]
        {
            S(R1, new YearMonth(2026, 4), 10_000),
            S(R1, new YearMonth(2026, 5), 12_000),
            S(R2, new YearMonth(2026, 4), 8_000),
        };
        // 合計30,000 / 3 = 10,000
        AverageWageMetric.Calculate(stmts, AverageWageDenominator.TotalRecipients).Should().Be(10_000);
    }

    [Fact]
    public void ActiveRecipients_uses_distinct_count()
    {
        var stmts = new[]
        {
            S(R1, new YearMonth(2026, 4), 10_000),
            S(R1, new YearMonth(2026, 5), 12_000),
            S(R2, new YearMonth(2026, 4), 8_000),
        };
        // 合計30,000 / 2人 = 15,000
        AverageWageMetric.Calculate(stmts, AverageWageDenominator.ActiveRecipients).Should().Be(15_000);
    }

    [Fact]
    public void Empty_returns_zero()
        => AverageWageMetric.Calculate(Array.Empty<WageStatement>(), AverageWageDenominator.ActiveRecipients).Should().Be(0);
}
```

- [ ] **Step 2: 実装**

```csharp
// src/Tsumugi.Domain/Logic/AverageWageMetric.cs
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic;

public enum AverageWageDenominator { TotalRecipients, ActiveRecipients }

/// <summary>
/// 平均工賃月額（暫定式）。
/// FIXME(open-questions): 正式定義（分母＝延べ/実利用者、対象期間、控除項目）は厚労省告示/通知突合で確定する。
/// 本暫定式は分母の差し替えに強い構造で実装してあり、確定後は AverageWageDenominator を増減して
/// テストで固定し直すこと。
/// </summary>
public static class AverageWageMetric
{
    public static int Calculate(IReadOnlyList<WageStatement> statements, AverageWageDenominator denominator)
    {
        ArgumentNullException.ThrowIfNull(statements);
        if (statements.Count == 0) return 0;

        var totalYen = statements.Sum(s => (long)s.AmountYen);
        long divisor = denominator switch
        {
            AverageWageDenominator.TotalRecipients => statements.Count,
            AverageWageDenominator.ActiveRecipients => statements.Select(s => s.RecipientId).Distinct().Count(),
            _ => throw new ArgumentOutOfRangeException(nameof(denominator)),
        };
        if (divisor == 0) return 0;
        return (int)(totalYen / divisor);
    }
}
```

- [ ] **Step 3: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~AverageWageMetricTests"
git add src/Tsumugi.Domain/Logic/AverageWageMetric.cs tests/Tsumugi.Domain.Tests/AverageWageMetricTests.cs
git commit -m "feat(domain): provisional average wage metric (formula pending official guidance)"
```

---

## Phase C: Infrastructure（EF + マイグレーション + Repository）

### Task C1: AppendOnlyGuard 拡張と違反テスト（赤確認）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase2Tests.cs`

**Interfaces:**
- Produces: `AppendOnlyGuard.AppendOnlyTypes` に `typeof(WorkRecord) / typeof(WageFund) / typeof(WageStatement) / typeof(AuditEntry)` を追加（`WageSettings` は期間マスタとして既存追記型と同様に追加）。

- [ ] **Step 1: 違反テストを書く**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class AppendOnlyGuardPhase2Tests
{
    [Theory]
    [InlineData(typeof(WorkRecord))]
    [InlineData(typeof(WageFund))]
    [InlineData(typeof(WageSettings))]
    [InlineData(typeof(WageStatement))]
    [InlineData(typeof(AuditEntry))]
    public void Append_only_types_include_phase2_entities(Type t)
    {
        // テスト容易性のため AppendOnlyGuard.AppendOnlyTypes は internal アクセサを提供する（次ステップで追加）
        AppendOnlyGuard.GetAppendOnlyTypesForTests().Should().Contain(t);
    }

    [Fact]
    public async Task Updating_existing_WorkRecord_throws_AppendOnlyViolation()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var rid = await harness.SeedRecipientAsync();
        var rec = WorkRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
            240, null, null, null, null, "t", DateTimeOffset.UtcNow);
        harness.Db.WorkRecords.Add(rec);
        await harness.Db.SaveChangesAsync();

        var loaded = await harness.Db.WorkRecords.FirstAsync();
        loaded = loaded with { Note = "更新トライ" };
        harness.Db.WorkRecords.Update(loaded);

        var act = async () => await harness.Db.SaveChangesAsync();
        await act.Should().ThrowAsync<AppendOnlyViolationException>();
    }
}
```

- [ ] **Step 2: 赤確認 → AppendOnlyGuard 拡張**

```csharp
// src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs を編集
private static readonly HashSet<Type> AppendOnlyTypes = new()
{
    typeof(DailyRecord),
    typeof(Certificate),
    typeof(Contract),
    typeof(OfficeCapability),
    typeof(DisabilityCertificate),
    typeof(FaceSheet),
    typeof(WorkRecord),
    typeof(WageFund),
    typeof(WageSettings),
    typeof(WageStatement),
    typeof(AuditEntry),
};

internal static IReadOnlySet<Type> GetAppendOnlyTypesForTests() => AppendOnlyTypes;
```

`AssemblyInfo.cs` に `[assembly: InternalsVisibleTo("Tsumugi.Infrastructure.Tests")]` が既にあることを確認（無ければ追加）。

- [ ] **Step 3: テストは Task C2 以降で DbContext 拡張後に通る。本タスクではコンパイル緑のみ確認。コミット。**

```bash
dotnet build src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj
git add src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase2Tests.cs
git commit -m "feat(infra): register Phase 2 entities in AppendOnlyGuard"
```

---

### Task C2: EF Configurations（5 新型）と DbContext 拡張

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/WorkRecordConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/WageSettingsConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/WageStatementConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`

**Interfaces:**
- Produces: 5 つの `IEntityTypeConfiguration<T>` 実装。`ApplyConfigurationsFromAssembly` で自動取り込み。`YearMonth` は `(int Year, int Month)` 2 列に展開する Conversion を持つ。`DateRange` は既存 `DateRangeJson` Conversion を流用。

- [ ] **Step 1: WorkRecord Configuration（DailyRecordConfiguration のパターン踏襲）**

```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/WorkRecordConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WorkRecordConfiguration : IEntityTypeConfiguration<WorkRecord>
{
    public void Configure(EntityTypeBuilder<WorkRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WorkRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RecipientId).IsRequired();
        builder.Property(r => r.WorkDate).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        builder.Property(r => r.OriginId);
        builder.Property(r => r.WorkedMinutes);
        builder.Property(r => r.PieceCount);
        builder.Property(r => r.PieceUnitYen);
        builder.Property(r => r.Points);
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ConcurrencyToken);
        builder.HasIndex(r => new { r.RecipientId, r.WorkDate });
        builder.HasIndex(r => r.OriginId);
        // 多重 New 防止（DailyRecord と同じ partial unique index 戦略）
        builder.HasIndex(r => new { r.RecipientId, r.WorkDate })
            .HasFilter("\"Kind\" = 0")
            .IsUnique()
            .HasDatabaseName("UX_WorkRecords_RecipientId_WorkDate_NewOnly");
    }
}
```

- [ ] **Step 2: WageFund Configuration**

```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/WageFundConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageFundConfiguration : IEntityTypeConfiguration<WageFund>
{
    public void Configure(EntityTypeBuilder<WageFund> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageFunds");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.OfficeId).IsRequired();
        // YearMonth を Year/Month の 2 列に展開
        builder.OwnsOne(r => r.Month, mb =>
        {
            mb.Property(m => m.Year).HasColumnName("Year").IsRequired();
            mb.Property(m => m.Month).HasColumnName("Month").IsRequired();
        });
        builder.Property(r => r.TotalYen).IsRequired();
        builder.Property(r => r.Kind).HasConversion<int>().IsRequired();
        builder.Property(r => r.OriginId);
        builder.Property(r => r.Note).HasMaxLength(512);
        builder.Property(r => r.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ConcurrencyToken);
        builder.HasIndex(r => new { r.OfficeId });
        builder.HasIndex(r => r.OriginId);
    }
}
```

ただし `YearMonth` は `readonly record struct` のため EF Core の `OwnsOne` には不向き。代替案として `Year` / `Month` を `int` 列に直接展開し、`YearMonth` を `[NotMapped]` プロパティ にする方が単純。実装方針確定のため、**この場合は WageFund 等の Domain entity に `int Year { get; init; } / int Month { get; init; }` + `YearMonth Month => new(Year, Month)` の薄いラッパを追加する**。本タスクは:
1. Domain `WageFund` / `WageStatement` を `int Year, int Month` 列ベースに変更（B4/B9 のテストは `YearMonth` プロパティ経由のままなので影響最小）。
2. 該当テストを更新（`Month` プロパティが計算ベースになっても等価チェックが通ること）。
3. Configuration 側は 2 列を `IsRequired()` する。

```csharp
// 変更後の WageFund 抜粋
public sealed record WageFund : Entity
{
    public required Guid OfficeId { get; init; }
    public required int Year { get; init; }
    public required int Month { get; init; }
    public YearMonth YearMonth => new(Year, Month);
    public required int TotalYen { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public string? Note { get; init; }

    public static WageFund NewRecord(
        Guid id, Guid officeId, YearMonth month, int totalYen, string? note,
        string createdBy, DateTimeOffset createdAt)
    {
        if (totalYen < 0) throw new ArgumentOutOfRangeException(nameof(totalYen));
        return new()
        {
            Id = id, OfficeId = officeId, Year = month.Year, Month = month.Month,
            TotalYen = totalYen, Kind = RecordKind.New, OriginId = null, Note = note,
            CreatedBy = createdBy, CreatedAt = createdAt, ConcurrencyToken = Guid.Empty,
        };
    }
    // Correction/Cancellation も同様
}
```

`WageStatement` も同じ書き換えを適用する。**Domain テストは `YearMonth` プロパティで等価判定するため、必要に応じてテストの作成方法を `WageFund.NewRecord(..., new YearMonth(2026, 7), ...)` のままで動かす**。

- [ ] **Step 3: WageSettings Configuration（DateRangeJson 流用）**

```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/WageSettingsConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Infrastructure.Persistence.Conversions;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageSettingsConfiguration : IEntityTypeConfiguration<WageSettings>
{
    public void Configure(EntityTypeBuilder<WageSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.OfficeId).IsRequired();
        builder.Property(s => s.Period).HasConversion(DateRangeJson.Converter).IsRequired();
        builder.Property(s => s.Method).HasConversion<int>().IsRequired();
        builder.Property(s => s.Rounding).HasConversion<int>().IsRequired();
        builder.Property(s => s.Remainder).HasConversion<int>().IsRequired();
        builder.Property(s => s.FiscalYearStartMonth).IsRequired();
        builder.Property(s => s.FixedDailyYen);
        builder.Property(s => s.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(s => s.OfficeId);
    }
}
```

- [ ] **Step 4: WageStatement Configuration**

```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/WageStatementConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class WageStatementConfiguration : IEntityTypeConfiguration<WageStatement>
{
    public void Configure(EntityTypeBuilder<WageStatement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("WageStatements");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.OfficeId).IsRequired();
        builder.Property(s => s.Year).IsRequired();
        builder.Property(s => s.Month).IsRequired();
        builder.Property(s => s.RecipientId).IsRequired();
        builder.Property(s => s.AmountYen).IsRequired();
        builder.Property(s => s.BasisSummary).IsRequired().HasMaxLength(512);
        builder.Property(s => s.Kind).HasConversion<int>().IsRequired();
        builder.Property(s => s.OriginId);
        builder.Property(s => s.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ConcurrencyToken);
        builder.HasIndex(s => new { s.OfficeId, s.Year, s.Month });
        builder.HasIndex(s => s.OriginId);
        builder.HasIndex(s => new { s.OfficeId, s.Year, s.Month, s.RecipientId })
            .HasFilter("\"Kind\" = 0")
            .IsUnique()
            .HasDatabaseName("UX_WageStatements_Office_YM_Recipient_NewOnly");
    }
}
```

- [ ] **Step 5: AuditEntry Configuration**

```csharp
// src/Tsumugi.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("AuditEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Actor).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Action).HasConversion<int>().IsRequired();
        builder.Property(e => e.TargetType).IsRequired().HasMaxLength(64);
        builder.Property(e => e.TargetId).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(512);
        builder.Property(e => e.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ConcurrencyToken);
        builder.HasIndex(e => new { e.TargetType, e.TargetId });
        builder.HasIndex(e => e.OccurredAt);
    }
}
```

- [ ] **Step 6: TsumugiDbContext に DbSet 追加**

```csharp
public DbSet<WorkRecord> WorkRecords => Set<WorkRecord>();
public DbSet<WageFund> WageFunds => Set<WageFund>();
public DbSet<WageSettings> WageSettings => Set<WageSettings>();
public DbSet<WageStatement> WageStatements => Set<WageStatement>();
public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
```

- [ ] **Step 7: コンパイル確認 → コミット**

```bash
dotnet build src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj
git add src/Tsumugi.Infrastructure/Persistence/Configurations src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs \
        src/Tsumugi.Domain/Entities/WageFund.cs src/Tsumugi.Domain/Entities/WageStatement.cs
git commit -m "feat(infra): EF configurations for Phase 2 entities and DbContext sets"
```

---

### Task C3: マイグレーション発行（Phase2Wage）

**Files:**
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase2Wage.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase2Wage.Designer.cs`

- [ ] **Step 1: マイグレーションを生成**

```bash
dotnet ef migrations add Phase2Wage \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```

生成された migration を読み、以下が含まれることを確認:
- 5 テーブルの CreateTable（WorkRecords, WageFunds, WageSettings, WageStatements, AuditEntries）
- partial unique index（WorkRecords / WageStatements）
- インデックス（OfficeId / OriginId / TargetType+TargetId）

- [ ] **Step 2: マイグレーションを適用してテスト**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~AppendOnlyGuardPhase2Tests" -v normal
```

期待: 全テスト緑（DbContext のテーブルが生成され、AppendOnlyGuard が動作する）。

- [ ] **Step 3: コミット**

```bash
git add src/Tsumugi.Infrastructure/Migrations
git commit -m "feat(infra): EF migration for Phase 2 wage tables"
```

---

### Task C4: Repository 5 種

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IWorkRecordRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IWageFundRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IWageSettingsRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IWageStatementRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IAuditEntryRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/WorkRecordRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/WageFundRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/WageSettingsRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/WageStatementRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/AuditEntryRepository.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/WorkRecordRepositoryTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/WageFundRepositoryTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/WageSettingsRepositoryTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/WageStatementRepositoryTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/AuditEntryRepositoryTests.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface IWorkRecordRepository
  {
      Task AddAsync(WorkRecord r, CancellationToken ct);
      Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct);
      Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(Guid recipientId, int year, int month, CancellationToken ct);
      Task<IReadOnlyList<WorkRecord>> ListByOfficeAndMonthAsync(Guid officeId, int year, int month, CancellationToken ct);
  }
  public interface IWageFundRepository
  {
      Task AddAsync(WageFund f, CancellationToken ct);
      Task<IReadOnlyList<WageFund>> ListByOfficeAndMonthAsync(Guid officeId, int year, int month, CancellationToken ct);
  }
  public interface IWageSettingsRepository
  {
      Task AddAsync(WageSettings s, CancellationToken ct);
      Task<IReadOnlyList<WageSettings>> ListByOfficeAsync(Guid officeId, CancellationToken ct);
  }
  public interface IWageStatementRepository
  {
      Task AddAsync(WageStatement s, CancellationToken ct);
      Task<IReadOnlyList<WageStatement>> ListByOfficeAndMonthAsync(Guid officeId, int year, int month, CancellationToken ct);
      Task<IReadOnlyList<WageStatement>> ListByOfficeFiscalYearAsync(Guid officeId, int fiscalYear, int startMonth, CancellationToken ct);
  }
  public interface IAuditEntryRepository
  {
      Task AddAsync(AuditEntry e, CancellationToken ct);
      Task<IReadOnlyList<AuditEntry>> ListByTargetAsync(string targetType, Guid targetId, CancellationToken ct);
  }
  ```

- [ ] **Step 1: 5 IF を Application に追加**（コード省略可、上記シグネチャ通り）

- [ ] **Step 2: 5 Repository 実装を Infrastructure に追加**（既存 `DailyRecordRepository` を雛形にする）

```csharp
// 例: src/Tsumugi.Infrastructure/Persistence/WorkRecordRepository.cs
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class WorkRecordRepository(TsumugiDbContext db) : IWorkRecordRepository
{
    public Task AddAsync(WorkRecord r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        db.WorkRecords.Add(r);
        return Task.CompletedTask;
    }

    public Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.WorkRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(
        Guid recipientId, int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return await db.WorkRecords.AsNoTracking()
            .Where(r => r.RecipientId == recipientId
                     && r.WorkDate >= first && r.WorkDate <= last)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkRecord>> ListByOfficeAndMonthAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return await db.WorkRecords.AsNoTracking()
            .Join(db.Recipients.AsNoTracking(), w => w.RecipientId, r => r.Id, (w, r) => new { w, r.OfficeId })
            .Where(x => x.OfficeId == officeId
                     && x.w.WorkDate >= first && x.w.WorkDate <= last)
            .Select(x => x.w)
            .ToListAsync(ct);
    }
}
```

他 4 種も同じパターン。`WageFundRepository`/`WageSettingsRepository`/`WageStatementRepository`/`AuditEntryRepository` を作る。

- [ ] **Step 3: 各 Repository のラウンドトリップテストを追加**

例:
```csharp
public sealed class WorkRecordRepositoryTests
{
    [Fact]
    public async Task Add_and_list_by_recipient_month_round_trip()
    {
        await using var h = await SqliteTestHarness.CreateAsync();
        var rid = await h.SeedRecipientAsync();
        var repo = new WorkRecordRepository(h.Db);
        var r = WorkRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 7, 1),
            240, null, null, null, null, "t", DateTimeOffset.UtcNow);
        await repo.AddAsync(r, default);
        await h.Db.SaveChangesAsync();

        var list = await repo.ListByRecipientAndMonthAsync(rid, 2026, 7, default);
        list.Should().HaveCount(1);
        list[0].WorkedMinutes.Should().Be(240);
    }
}
```

5 種すべてで同様のテストを書く。

- [ ] **Step 4: DependencyInjection.cs に登録**

```csharp
// src/Tsumugi.Infrastructure/DependencyInjection.cs に追記
services.AddScoped<IWorkRecordRepository, WorkRecordRepository>();
services.AddScoped<IWageFundRepository, WageFundRepository>();
services.AddScoped<IWageSettingsRepository, WageSettingsRepository>();
services.AddScoped<IWageStatementRepository, WageStatementRepository>();
services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
```

- [ ] **Step 5: 全テスト緑 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj
git add src/Tsumugi.Application/Abstractions src/Tsumugi.Infrastructure/Persistence \
        src/Tsumugi.Infrastructure/DependencyInjection.cs tests/Tsumugi.Infrastructure.Tests
git commit -m "feat(infra): repositories for Phase 2 wage and audit entities"
```

---

## Phase D: Application（UseCase + DTO + 監査配線）

### Task D1: WorkRecord 4 UseCase + DTO

**Files:**
- Create: `src/Tsumugi.Application/Dtos/WorkRecordDto.cs`
- Create: `src/Tsumugi.Application/UseCases/WorkRecord/RecordWorkUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/WorkRecord/CorrectWorkUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/WorkRecord/CancelWorkUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/WorkRecord/QueryMonthWorkUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/WorkRecordUseCaseTests.cs`

**Interfaces:**
- Produces: 4 UseCase（`(repo, IUnitOfWork, TimeProvider)` 構成、`DateValidator.EnsureValid` 使用、DTO `Map`）。`RecordWorkUseCase` は `(repo.ListByRecipientAndMonthAsync` ＋ 当日抽出ではなく `WorkRecordPolicy` の `Effective` で「既に有効な New があれば拒否」ロジック）。`CorrectWorkUseCase` は existing `originId` を Find→ Effective を確認し、stale な origin の訂正は ADR 0009 同型でリジェクト（`DailyRecord` の `Logic` を参考）。

- [ ] **Step 1: WorkRecordDto を定義**

```csharp
// src/Tsumugi.Application/Dtos/WorkRecordDto.cs
using Tsumugi.Domain.Enums;
namespace Tsumugi.Application.Dtos;

public sealed record WorkRecordDto(
    Guid Id, Guid RecipientId, DateOnly WorkDate, RecordKind Kind, Guid? OriginId,
    int? WorkedMinutes, int? PieceCount, int? PieceUnitYen, int? Points, string? Note);
```

- [ ] **Step 2: UseCase 4 種をモックリポジトリでテスト（DailyRecord と同型）**

テスト例 1 つ:
```csharp
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.WorkRecord;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class WorkRecordUseCaseTests
{
    private sealed class InMemoryRepo : IWorkRecordRepository
    {
        public List<WorkRecord> Items { get; } = new();
        public Task AddAsync(WorkRecord r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
        public Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(
            Guid recipientId, int year, int month, CancellationToken ct)
        {
            var first = new DateOnly(year, month, 1);
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            IReadOnlyList<WorkRecord> r = Items
                .Where(x => x.RecipientId == recipientId && x.WorkDate >= first && x.WorkDate <= last)
                .ToList();
            return Task.FromResult(r);
        }
        public Task<IReadOnlyList<WorkRecord>> ListByOfficeAndMonthAsync(Guid o, int y, int m, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<WorkRecord>>(Array.Empty<WorkRecord>());
    }

    private sealed class FakeUow : IUnitOfWork { public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1); }

    [Fact]
    public async Task Record_new_work_creates_entity()
    {
        var repo = new InMemoryRepo();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));
        var u = new RecordWorkUseCase(repo, new FakeUow(), clock);
        var dto = await u.ExecuteAsync(
            Guid.NewGuid(), new DateOnly(2026, 7, 1),
            workedMinutes: 240, pieceCount: null, pieceUnitYen: null, points: null,
            note: null, actor: "t", CancellationToken.None);
        dto.Kind.Should().Be(RecordKind.New);
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Duplicate_new_record_is_rejected()
    {
        var repo = new InMemoryRepo();
        var recipient = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 1);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));
        var u = new RecordWorkUseCase(repo, new FakeUow(), clock);
        await u.ExecuteAsync(recipient, date, 240, null, null, null, null, "t", default);
        var act = async () => await u.ExecuteAsync(recipient, date, 200, null, null, null, null, "t", default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*New record exists*");
    }
}
```

実装は `RecordDailyRecordUseCase` と同型。`CorrectWorkUseCase` は ADR 0009（stale-origin rejection）に揃え、`FindByIdAsync(originId)` → `OriginId is null || Kind == New` の origin のみ受理し、その他はリジェクト。

- [ ] **Step 3: 4 UseCase を実装 → 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~WorkRecord"
git add src/Tsumugi.Application/Dtos/WorkRecordDto.cs src/Tsumugi.Application/UseCases/WorkRecord \
        tests/Tsumugi.Application.Tests/WorkRecordUseCaseTests.cs
git commit -m "feat(application): WorkRecord use cases (Record/Correct/Cancel/QueryMonth)"
```

---

### Task D2: SetWageFundUseCase + ConfigureWageSettingsUseCase

**Files:**
- Create: `src/Tsumugi.Application/Dtos/WageFundDto.cs`, `WageSettingsDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/SetWageFundUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/ConfigureWageSettingsUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/WageMasterUseCaseTests.cs`

**Interfaces:**
- Produces: `SetWageFundUseCase.ExecuteAsync(officeId, year, month, totalYen, note?, actor, ct) -> WageFundDto`。既存 Fund があれば `Correction` で追記、無ければ `NewRecord`。
- Produces: `ConfigureWageSettingsUseCase.ExecuteAsync(officeId, period, method, rounding, remainder, fiscalYearStartMonth, fixedDailyYen?, actor, ct) -> WageSettingsDto`。期間マスタとして追記。

- [ ] **Step 1: テストを書く（モック）**

```csharp
// WageMasterUseCaseTests.cs（抜粋）
[Fact]
public async Task First_call_creates_new_fund()
{
    var repo = new InMemoryWageFundRepo();
    var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
    var u = new SetWageFundUseCase(repo, new FakeUow(), clock);
    var dto = await u.ExecuteAsync(Guid.NewGuid(), 2026, 7, 300_000, null, "alice", default);
    dto.TotalYen.Should().Be(300_000);
    repo.Items.Should().ContainSingle(f => f.Kind == RecordKind.New);
}

[Fact]
public async Task Second_call_appends_correction_referring_origin()
{
    var repo = new InMemoryWageFundRepo();
    var officeId = Guid.NewGuid();
    var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
    var u = new SetWageFundUseCase(repo, new FakeUow(), clock);
    var first = await u.ExecuteAsync(officeId, 2026, 7, 300_000, null, "alice", default);
    var second = await u.ExecuteAsync(officeId, 2026, 7, 280_000, "下方修正", "alice", default);
    second.TotalYen.Should().Be(280_000);
    repo.Items.Should().HaveCount(2);
    var correction = repo.Items.Single(f => f.Kind == RecordKind.Correct);
    correction.OriginId.Should().Be(first.Id);
}
```

- [ ] **Step 2: 実装**

```csharp
// src/Tsumugi.Application/UseCases/Wage/SetWageFundUseCase.cs
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class SetWageFundUseCase(
    IWageFundRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<WageFundDto> ExecuteAsync(
        Guid officeId, int year, int month, int totalYen, string? note,
        string actor, CancellationToken ct)
    {
        if (officeId == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(officeId));
        DateValidator.EnsureYearMonth(year, month);
        if (totalYen < 0)
            throw new ArgumentOutOfRangeException(nameof(totalYen), "工賃原資は0円以上で指定してください。");

        var existing = await repo.ListByOfficeAndMonthAsync(officeId, year, month, ct);
        var effective = WageFundPolicy.Effective(existing);
        var now = clock.GetUtcNow();
        var ym = new YearMonth(year, month);

        WageFund entity = effective is null
            ? WageFund.NewRecord(Guid.NewGuid(), officeId, ym, totalYen, note, actor, now)
            : WageFund.Correction(Guid.NewGuid(), officeId, ym, effective.Id, totalYen, note, actor, now);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return new WageFundDto(entity.Id, officeId, year, month, totalYen, entity.Kind, entity.OriginId, note);
    }
}
```

`ConfigureWageSettingsUseCase` は WageSettings を `Create` ファクトリで作成して追記するシンプル UseCase。

- [ ] **Step 3: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~WageMasterUseCaseTests"
git add src/Tsumugi.Application/Dtos/WageFundDto.cs src/Tsumugi.Application/Dtos/WageSettingsDto.cs \
        src/Tsumugi.Application/UseCases/Wage tests/Tsumugi.Application.Tests/WageMasterUseCaseTests.cs
git commit -m "feat(application): SetWageFund and ConfigureWageSettings use cases"
```

---

### Task D3: CalculateWagesUseCase（プレビュー）

**Files:**
- Create: `src/Tsumugi.Application/Dtos/WagePreviewDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/CalculateWagesUseCaseTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record WagePreviewLineDto(Guid RecipientId, int AmountYen, string BasisSummary);
  public sealed record WagePreviewDto(
      Guid OfficeId, int Year, int Month, WageMethod Method,
      int TotalFundYen, int TotalAllocatedYen,
      IReadOnlyList<WagePreviewLineDto> Lines);
  ```
- Produces: UseCase は `(IDailyRecordRepository, IWorkRecordRepository, IWageFundRepository, IWageSettingsRepository, IReadOnlyList<IWageMethodStrategy> strategies)` を受け、純粋関数で計算結果を返す。永続化なし。

- [ ] **Step 1: 失敗テストを書く**

主要ケース:
1. WageSettings 未設定で例外。
2. Hourly のとき WageFund 未設定で例外。
3. Piece 方式は WageFund 不要・配分なしで実額。
4. Hourly 方式は Σ＝原資 を満たす。

- [ ] **Step 2: 実装**

```csharp
// src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class CalculateWagesUseCase(
    IDailyRecordRepository dailyRepo,
    IWorkRecordRepository workRepo,
    IWageFundRepository fundRepo,
    IWageSettingsRepository settingsRepo,
    IContractRepository contractRepo,
    IRecipientRepository recipientRepo,
    IReadOnlyList<IWageMethodStrategy> strategies)
{
    public async Task<WagePreviewDto> ExecuteAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        DateValidator.EnsureYearMonth(year, month);
        var ym = new YearMonth(year, month);

        var settings = WageSettingsPolicy.Effective(
            await settingsRepo.ListByOfficeAsync(officeId, ct),
            ym.FirstDay());
        if (settings is null)
            throw new InvalidOperationException(
                $"事業所 {officeId} の {year}-{month:D2} 時点の工賃設定が見つかりません。");

        var fund = WageFundPolicy.Effective(
            await fundRepo.ListByOfficeAndMonthAsync(officeId, year, month, ct));
        if (settings.Method is WageMethod.Hourly or WageMethod.Equal && fund is null)
            throw new InvalidOperationException(
                "Hourly / Equal 方式では当月の工賃原資（WageFund）が必須です。");

        // 注: Recipient / Contract はまだ OfficeId を持たない（仕様 §3.4・ContractedProvider/Contract 整理は別パス）。
        // 単一事業所運用の前提で「当月 1 日時点で有効な契約を持つ利用者」を対象とする。
        // 複数事業所対応は ContractedProvider/Contract 整理時に再実装する（open-questions 既出）。
        var allRecipients = await recipientRepo.ListAsync(includeArchived: false, ct);
        var anchor = ym.FirstDay();
        var targetRecipients = new List<Guid>();
        foreach (var r in allRecipients)
        {
            var contract = await contractRepo.FindEffectiveAsync(r.Id, anchor, ct);
            if (contract is not null) targetRecipients.Add(r.Id);
        }

        var allDaily = new List<DailyRecord>();
        var allWork = new List<WorkRecord>();
        foreach (var rid in targetRecipients)
        {
            allDaily.AddRange(await dailyRepo.ListByRecipientAndMonthAsync(rid, year, month, ct));
            allWork.AddRange(await workRepo.ListByRecipientAndMonthAsync(rid, year, month, ct));
        }

        var inputs = WageBasisExtractor.Build(allDaily, allWork, ym);
        var lines = WageCalculator.Calculate(strategies, settings.Method, inputs, fund, settings);

        return new WagePreviewDto(
            officeId, year, month, settings.Method,
            TotalFundYen: fund?.TotalYen ?? 0,
            TotalAllocatedYen: lines.Sum(l => l.AmountYen),
            Lines: lines.Select(l => new WagePreviewLineDto(l.RecipientId, l.AmountYen, l.BasisSummary)).ToArray());
    }
}
```

- [ ] **Step 3: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~CalculateWagesUseCaseTests"
git add src/Tsumugi.Application/Dtos/WagePreviewDto.cs src/Tsumugi.Application/UseCases/Wage/CalculateWagesUseCase.cs \
        tests/Tsumugi.Application.Tests/CalculateWagesUseCaseTests.cs
git commit -m "feat(application): CalculateWagesUseCase preview pipeline"
```

---

### Task D4: CloseWagesUseCase（確定） + QueryWageStatementUseCase

**Files:**
- Create: `src/Tsumugi.Application/Dtos/WageStatementDto.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/CloseWagesUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Wage/QueryWageStatementUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/CloseWagesUseCaseTests.cs`

**Interfaces:**
- Produces: `CloseWagesUseCase.ExecuteAsync(officeId, year, month, actor, ct) -> IReadOnlyList<WageStatementDto>`。内部で `CalculateWagesUseCase` を呼び、結果を `WageStatement.NewRecord` として追記（既存 New があれば全件 `Correction` で再確定）。

- [ ] **Step 1: テストを書く**

ケース:
1. 初回確定で WageStatement の `Kind=New` が利用者数ぶん追加される。
2. 確定後の下層 WorkRecord 訂正では既存 WageStatement が**変わらない**。
3. 再確定で全行が `Correction` として追加され、`OriginId` が初回 `New` の Id を指す。

- [ ] **Step 2: 実装**

```csharp
// src/Tsumugi.Application/UseCases/Wage/CloseWagesUseCase.cs
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class CloseWagesUseCase(
    CalculateWagesUseCase preview,
    IWageStatementRepository statementRepo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<WageStatementDto>> ExecuteAsync(
        Guid officeId, int year, int month, string actor, CancellationToken ct)
    {
        DateValidator.EnsureYearMonth(year, month);
        var calc = await preview.ExecuteAsync(officeId, year, month, ct);
        var existing = await statementRepo.ListByOfficeAndMonthAsync(officeId, year, month, ct);
        var existingNewByRecipient = existing
            .Where(s => s.Kind == RecordKind.New)
            .GroupBy(s => s.RecipientId)
            .ToDictionary(g => g.Key, g => g.First());
        var now = clock.GetUtcNow();
        var ym = new YearMonth(year, month);
        var results = new List<WageStatementDto>();
        foreach (var line in calc.Lines)
        {
            WageStatement stmt = existingNewByRecipient.TryGetValue(line.RecipientId, out var origin)
                ? WageStatement.Correction(Guid.NewGuid(), officeId, ym, line.RecipientId, origin.Id,
                    line.AmountYen, line.BasisSummary, actor, now)
                : WageStatement.NewRecord(Guid.NewGuid(), officeId, ym, line.RecipientId,
                    line.AmountYen, line.BasisSummary, actor, now);
            await statementRepo.AddAsync(stmt, ct);
            results.Add(new WageStatementDto(
                stmt.Id, officeId, year, month, line.RecipientId,
                stmt.AmountYen, stmt.BasisSummary, stmt.Kind, stmt.OriginId));
        }
        await uow.SaveChangesAsync(ct);
        return results;
    }
}
```

`QueryWageStatementUseCase` は `ListByOfficeAndMonthAsync` から実効レコードを返す（`WageStatement` 用の Effective 導出は新規 `WageStatementPolicy` として Domain に追加 — Task B9 の小規模追加で済むのでコミットに含める。**抜けがあれば B9 に戻して `WageStatementPolicy.Effective` を実装**）。

- [ ] **Step 3: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~CloseWagesUseCaseTests"
git add src/Tsumugi.Application/Dtos/WageStatementDto.cs src/Tsumugi.Application/UseCases/Wage/CloseWagesUseCase.cs \
        src/Tsumugi.Application/UseCases/Wage/QueryWageStatementUseCase.cs \
        tests/Tsumugi.Application.Tests/CloseWagesUseCaseTests.cs
git commit -m "feat(application): CloseWages and QueryWageStatement use cases"
```

---

### Task D5: IAuditTrail と AuditEntry 配線（UpdateOffice/UpdateRecipient）

**Files:**
- Create: `src/Tsumugi.Application/Audit/IAuditTrail.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/AuditTrail.cs`
- Modify: `src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/Recipient/UpdateRecipientUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/Recipient/ArchiveRecipientUseCase.cs`
- Modify: `src/Tsumugi.Application/UseCases/Recipient/RestoreRecipientUseCase.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- Create: `tests/Tsumugi.Application.Tests/AuditTrailWiringTests.cs`
- Create: `docs/decisions/0014-audit-trail-append-only.md`

**Interfaces:**
- Produces:
  ```csharp
  public interface IAuditTrail
  {
      Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
                       DateTimeOffset occurredAt, string? summary, CancellationToken ct);
  }
  ```
- Produces: `Infrastructure` 実装はリポジトリ呼び出しで `AuditEntry.Create` を追記する（保存は呼び出し側 UseCase の `IUnitOfWork.SaveChangesAsync` で commit される）。

- [ ] **Step 1: IAuditTrail を Application に追加**

```csharp
// src/Tsumugi.Application/Audit/IAuditTrail.cs
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Audit;

public interface IAuditTrail
{
    Task RecordAsync(
        string actor, AuditAction action,
        string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary,
        CancellationToken ct);
}
```

- [ ] **Step 2: 既存 UpdateXxx UseCase を改修**

```csharp
// 例: UpdateOfficeUseCase
public sealed class UpdateOfficeUseCase(
    IOfficeRepository repo, IUnitOfWork uow, TimeProvider clock, IAuditTrail audit)
{
    public async Task ExecuteAsync(Guid officeId, /* 既存パラメータ */, string actor, CancellationToken ct)
    {
        // 既存処理
        var now = clock.GetUtcNow();
        await audit.RecordAsync(actor, AuditAction.Update,
            "Office", officeId, now,
            summary: $"officeName={office.Name}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
```

`Recipient` の `Update/Archive/Restore` も同様。

- [ ] **Step 3: 配線テスト**

```csharp
[Fact]
public async Task UpdateOffice_emits_audit_entry()
{
    var officeRepo = new InMemoryOfficeRepo(/* seed */);
    var audit = new RecordingAuditTrail();
    var uow = new FakeUow();
    var clock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero));
    var u = new UpdateOfficeUseCase(officeRepo, uow, clock, audit);
    await u.ExecuteAsync(/* args */, actor: "alice", default);
    audit.Entries.Should().ContainSingle().Which.Should().Match<RecordingAuditTrail.Entry>(
        e => e.Actor == "alice" && e.Action == AuditAction.Update && e.TargetType == "Office");
}
```

- [ ] **Step 4: Infrastructure 実装と DI**

```csharp
// src/Tsumugi.Infrastructure/Persistence/AuditTrail.cs
public sealed class AuditTrail(IAuditEntryRepository repo, TimeProvider clock) : IAuditTrail
{
    public async Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
        DateTimeOffset occurredAt, string? summary, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var entry = AuditEntry.Create(
            Guid.NewGuid(), actor, action, targetType, targetId, occurredAt, summary, now, actor);
        await repo.AddAsync(entry, ct);
    }
}

// DependencyInjection
services.AddScoped<IAuditTrail, AuditTrail>();
```

- [ ] **Step 5: ADR 0014 作成**

```markdown
# ADR 0014: 監査ログ（AuditEntry）を append-only で導入

## 結論
- 同一性マスタ更新 UseCase（UpdateOffice/UpdateRecipient/Archive/Restore）から `IAuditTrail.RecordAsync` を呼び、`AuditEntry` を append-only で追記する。
- `AuditEntry` は `AppendOnlyGuard` に登録、Update/Delete は実行時例外で落ちる。

## 背景
- Phase 1 引継ぎ open-questions の項目。`actor` を受けながら使用していなかった。
- 工賃確定（Phase 2）と並行して、操作の事後検証を可能にする。

## 選択肢
1. EF Core 監査拡張（EFCore.Audit 系）→ 依存追加が大きい。却下。
2. ChangeTracker フック → 何を残すか暗黙的になりがち。却下。
3. UseCase 内で明示的に `IAuditTrail` を呼ぶ → **採用**。意図的に何を残すか制御できる。

## 影響
- すべての UpdateXxx UseCase に `IAuditTrail` の依存追加。
- DI 設定追加。
```

- [ ] **Step 6: 緑 → コミット**

```bash
dotnet test
git add src/Tsumugi.Application/Audit src/Tsumugi.Infrastructure/Persistence/AuditTrail.cs \
        src/Tsumugi.Application/UseCases/Office/UpdateOfficeUseCase.cs \
        src/Tsumugi.Application/UseCases/Recipient/UpdateRecipientUseCase.cs \
        src/Tsumugi.Application/UseCases/Recipient/ArchiveRecipientUseCase.cs \
        src/Tsumugi.Application/UseCases/Recipient/RestoreRecipientUseCase.cs \
        src/Tsumugi.Infrastructure/DependencyInjection.cs \
        tests/Tsumugi.Application.Tests/AuditTrailWiringTests.cs \
        docs/decisions/0014-audit-trail-append-only.md
git commit -m "feat(application): wire IAuditTrail into master update use cases (ADR 0014)"
```

---

## Phase E: Reporting（QuestPDF）

### Task E1: 新 csproj `Tsumugi.Infrastructure.Reporting` を追加

**Files:**
- Create: `src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj`
- Create: `src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj`
- Modify: `Tsumugi.sln`
- Modify: `src/Tsumugi.App/Tsumugi.App.csproj`（参照追加）
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests` の `csproj`

**Interfaces:**
- Produces: 新規プロジェクトは Application 層への依存のみ。`Tsumugi.App` から参照する（合成ルートは App）。
- Produces: `static class QuestPdfLicenseConfigurator { void ApplyCommunityLicense(); }` をアプリ起動時 1 度だけ呼ぶ。

- [ ] **Step 1: csproj を作成**

```xml
<!-- src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="QuestPDF" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Tsumugi.Application\Tsumugi.Application.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: `Tsumugi.sln` に追加 → `Tsumugi.App` に参照追加**

```bash
dotnet sln Tsumugi.sln add src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj
dotnet add src/Tsumugi.App/Tsumugi.App.csproj reference \
  src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj
```

- [ ] **Step 3: ライセンス Configurator**

```csharp
// src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs
using QuestPDF.Infrastructure;
namespace Tsumugi.Infrastructure.Reporting;

public static class QuestPdfLicenseConfigurator
{
    /// <summary>
    /// 起動時に1度だけ呼ぶ。ADR 0013 の判断によりライセンスを Community に固定する。
    /// 閾値超過時は Avalonia 印刷経路にフォールバックする実装に差し替える（ADR 0013 参照）。
    /// </summary>
    public static void ApplyCommunityLicense() => QuestPDF.Settings.License = LicenseType.Community;
}
```

`Tsumugi.App/App.axaml.cs` の `OnFrameworkInitializationCompleted` 冒頭で `QuestPdfLicenseConfigurator.ApplyCommunityLicense();` を呼ぶ。

- [ ] **Step 4: オフライン確認テスト追加**

`AppOfflineComplianceTests` の対象に `Tsumugi.Infrastructure.Reporting.dll` を追加し、QuestPDF が通信 API を内包していないか機械判定する（既存 allowlist を空のまま維持できることを確認）。

- [ ] **Step 5: コミット**

```bash
dotnet build
git add Tsumugi.sln src/Tsumugi.Infrastructure.Reporting src/Tsumugi.App/Tsumugi.App.csproj \
        tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs
git commit -m "feat(reporting): add Infrastructure.Reporting project with QuestPDF license configurator"
```

---

### Task E2: 工賃明細 PDF（利用者ごと）

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IWageReportGenerator.cs`（Application 側に IF）
- Create: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface IWageReportGenerator
  {
      byte[] GenerateStatement(WageStatementDto statement, RecipientDto recipient, OfficeDto office);
      byte[] GeneratePaymentList(IReadOnlyList<WageStatementDto> statements, OfficeDto office, int year, int month);
  }
  ```
- Produces: 出力 PDF のレイアウト要件:
  - A4 縦
  - 見出し: 事業所名、対象年月、利用者氏名、受給者証番号
  - 明細: 算定根拠サマリ（`BasisSummary`）、金額（整数円・桁区切り `N0`）
  - フッタ: 発行日時、発行者
- 文字コード: `NotoSansCJKjp-Regular.ttf` 等を埋め込み（リポジトリに `assets/fonts/` を追加。ライセンス確認）→ `open-questions` に「日本語フォントの埋込ライセンス」を追加。

- [ ] **Step 1: IF を Application に追加**（コード省略可）

- [ ] **Step 2: 抽出テキスト＋合計金額のテストを書く**

```csharp
[Fact]
public void Statement_pdf_contains_recipient_name_and_amount()
{
    var stmt = new WageStatementDto(
        Guid.NewGuid(), Guid.NewGuid(), 2026, 7, Guid.NewGuid(),
        AmountYen: 12_345, BasisSummary: "時間割: 600分 / 原資100,000円",
        Kind: RecordKind.New, OriginId: null);
    var recipient = new RecipientDto(stmt.RecipientId, "山田 太郎", "ヤマダ タロウ", new DateOnly(1970, 1, 1));
    var office = new OfficeDto(stmt.OfficeId, "1234567890", "テスト事業所");
    var bytes = new WageStatementPdfGenerator().GenerateStatement(stmt, recipient, office);

    // PDF からテキスト抽出（PdfPig 等の軽量ライブラリ。テスト専用依存）
    var text = PdfTextExtractor.Extract(bytes);
    text.Should().Contain("山田 太郎").And.Contain("テスト事業所").And.Contain("12,345");
}
```

テスト依存に `PdfPig` を加える（テスト csproj のみ）。

- [ ] **Step 3: 実装**

```csharp
// src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Infrastructure.Reporting;

public sealed class WageStatementPdfGenerator : IWageReportGenerator
{
    public byte[] GenerateStatement(WageStatementDto s, RecipientDto r, OfficeDto o)
    {
        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.Header().Text($"{o.Name}　{s.Year}年{s.Month}月分 工賃明細").FontSize(14).Bold();
                p.Content().Column(col =>
                {
                    col.Item().Text($"利用者: {r.NameKanji}（{r.NameKana}）");
                    col.Item().Text($"金額: {s.AmountYen:N0} 円");
                    col.Item().Text($"算定根拠: {s.BasisSummary}");
                });
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("発行日: ");
                    t.Span(System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
                });
            });
        });
        return doc.GeneratePdf();
    }

    public byte[] GeneratePaymentList(IReadOnlyList<WageStatementDto> statements, OfficeDto office, int year, int month)
    {
        // Task E3 で実装
        throw new NotImplementedException();
    }
}
```

- [ ] **Step 4: 緑 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj
git add src/Tsumugi.Application/Abstractions/IWageReportGenerator.cs \
        src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs \
        tests/Tsumugi.Infrastructure.Reporting.Tests
git commit -m "feat(reporting): per-recipient wage statement PDF"
```

---

### Task E3: 工賃支払一覧 PDF（事業所・月次）

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`（`GeneratePaymentList` の実体化）
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs`

- [ ] **Step 1: 抽出テキストテストを書く**

```csharp
[Fact]
public void PaymentList_contains_each_recipient_row_and_total()
{
    var office = new OfficeDto(Guid.NewGuid(), "1234567890", "テスト事業所");
    var statements = new[]
    {
        new WageStatementDto(Guid.NewGuid(), office.Id, 2026, 7, Guid.NewGuid(), 12_000, "x", RecordKind.New, null),
        new WageStatementDto(Guid.NewGuid(), office.Id, 2026, 7, Guid.NewGuid(),  8_000, "y", RecordKind.New, null),
    };
    var bytes = new WageStatementPdfGenerator().GeneratePaymentList(statements, office, 2026, 7);
    var text = PdfTextExtractor.Extract(bytes);
    text.Should().Contain("テスト事業所").And.Contain("12,000").And.Contain("8,000").And.Contain("20,000");
}
```

- [ ] **Step 2: テーブル形式で実装。合計金額を最終行に表示。緑 → コミット**

```bash
git commit -am "feat(reporting): office monthly wage payment list PDF"
```

---

## Phase F: App / UI（ViewModel 先行）

### Task F1: YenFormatter ヘルパとテスト

**Files:**
- Create: `src/Tsumugi.App/Formatting/YenFormatter.cs`
- Create: `tests/Tsumugi.App.Tests/YenFormatterTests.cs`

**Interfaces:**
- Produces: `static class YenFormatter { string Format(int yen); }` — `InvariantCulture` + `"N0"` を使用し、`"12,345 円"` を返す。`int` 以外は受け取らない（金額は整数円ハード制約）。

- [ ] **Step 1: テスト**

```csharp
[Theory]
[InlineData(0, "0 円")]
[InlineData(1, "1 円")]
[InlineData(1000, "1,000 円")]
[InlineData(1234567, "1,234,567 円")]
public void Format_integer_yen_with_separator(int yen, string expected)
    => YenFormatter.Format(yen).Should().Be(expected);
```

- [ ] **Step 2: 実装 → 緑 → コミット**

```csharp
using System.Globalization;
namespace Tsumugi.App.Formatting;

public static class YenFormatter
{
    public static string Format(int yen) => $"{yen.ToString("N0", CultureInfo.InvariantCulture)} 円";
}
```

```bash
git add src/Tsumugi.App/Formatting/YenFormatter.cs tests/Tsumugi.App.Tests/YenFormatterTests.cs
git commit -m "feat(app): YenFormatter helper for invariant integer yen display"
```

---

### Task F2: WorkRecordViewModel（月グリッド・キーボード操作）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/WorkRecordViewModel.cs`
- Create: `src/Tsumugi.App/Views/WorkRecordView.axaml` + `.cs`
- Create: `tests/Tsumugi.App.Tests/WorkRecordViewModelTests.cs`

**Interfaces:**
- Produces: `WorkRecordViewModel(QueryMonthWorkUseCase, RecordWorkUseCase, CorrectWorkUseCase, CancelWorkUseCase)`。月の利用者×日のグリッドを `ObservableCollection<WorkRecordCellViewModel>` で持つ。`LoadAsync(year, month)` / `SaveCellAsync(cell)` / `CancelCellAsync(cell)`。
- Keyboard: Enter/方向キー/F2 編集モード。

- [ ] **Step 1: ViewModel テストを書く**

```csharp
[Fact]
public async Task Load_populates_cells_for_each_recipient_day()
{
    var query = new FakeQueryMonthWorkUseCase(seed: …);
    var vm = new WorkRecordViewModel(query, new FakeRecordWork(), new FakeCorrectWork(), new FakeCancelWork());
    await vm.LoadAsync(2026, 7);
    vm.Cells.Should().NotBeEmpty();
    vm.TotalCells.Should().Be(/* expected */);
}
```

- [ ] **Step 2: ViewModel を最小実装 → 緑**

`CommunityToolkit.Mvvm` の `ObservableObject` / `RelayCommand` を使う。

- [ ] **Step 3: axaml を作成（DataGrid 利用、`DynamicResource BaseFontSize` 等 AccessibilityDefaults リソースを参照）**

- [ ] **Step 4: コミット**

```bash
git add src/Tsumugi.App/ViewModels/WorkRecordViewModel.cs src/Tsumugi.App/Views/WorkRecordView.axaml \
        src/Tsumugi.App/Views/WorkRecordView.axaml.cs tests/Tsumugi.App.Tests/WorkRecordViewModelTests.cs
git commit -m "feat(app): WorkRecordViewModel with month grid and keyboard editing"
```

---

### Task F3: WageFundSettingsViewModel（原資 + 設定タブ）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/WageFundSettingsViewModelTests.cs`

- [ ] **Step 1: テスト → 実装 → コミット**

主要操作:
- 年月を選んで原資を表示／編集／保存（SetWageFundUseCase 呼び出し）
- WageSettings の方式/端数/余り/年度起点/固定額の編集 → ConfigureWageSettingsUseCase
- 入力金額は `int.Parse(InvariantCulture)` + 負値拒否。表示は `YenFormatter`。

```bash
git commit -m "feat(app): WageFundSettingsViewModel for fund/settings management"
```

---

### Task F4: WageCalculationViewModel（プレビュー）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs`

- [ ] **Step 1: テスト → 実装 → コミット**

- `LoadPreviewAsync(officeId, year, month)` → `WagePreviewDto`
- `Lines` / `TotalFundYen` / `TotalAllocatedYen` を表示
- `TotalAllocatedYen != TotalFundYen` の場合は警告バナーを表示（Hourly/Equal のみ）

```bash
git commit -m "feat(app): WageCalculationViewModel preview pipeline"
```

---

### Task F5: WageStatementViewModel（確定 + 帳票）

**Files:**
- Create: `src/Tsumugi.App/ViewModels/WageStatementViewModel.cs`
- Create: `src/Tsumugi.App/Views/WageView.axaml` + `.cs`
- Create: `tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs`

- [ ] **Step 1: テスト → 実装 → コミット**

- `CloseAsync` で `CloseWagesUseCase` 呼び出し、結果一覧を表示
- `ExportStatementPdfAsync(recipientId)` / `ExportPaymentListPdfAsync` で `IWageReportGenerator` 呼び出し → `SaveFileDialog`（Avalonia）に渡す
- 確定済み一覧の表示と「再確定」コマンド

```bash
git commit -m "feat(app): WageStatementViewModel with finalize and PDF export"
```

---

### Task F6: MainViewModel にタブ配線 + CompositionRoot 更新

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/MainViewModel.cs`
- Modify: `src/Tsumugi.App/Views/MainWindow.axaml`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`

- [ ] **Step 1: CompositionRoot に新規 UseCase/VM/Strategy を登録**

```csharp
services.AddSingleton<IReadOnlyList<IWageMethodStrategy>>(_ => new IWageMethodStrategy[]
{
    new PieceWageStrategy(), new HourlyWageStrategy(),
    new FixedWageStrategy(), new EqualWageStrategy(),
});
services.AddScoped<RecordWorkUseCase>();
services.AddScoped<CorrectWorkUseCase>();
services.AddScoped<CancelWorkUseCase>();
services.AddScoped<QueryMonthWorkUseCase>();
services.AddScoped<SetWageFundUseCase>();
services.AddScoped<ConfigureWageSettingsUseCase>();
services.AddScoped<CalculateWagesUseCase>();
services.AddScoped<CloseWagesUseCase>();
services.AddScoped<QueryWageStatementUseCase>();
services.AddScoped<IWageReportGenerator, WageStatementPdfGenerator>();
services.AddTransient<WorkRecordViewModel>();
services.AddTransient<WageFundSettingsViewModel>();
services.AddTransient<WageCalculationViewModel>();
services.AddTransient<WageStatementViewModel>();
```

- [ ] **Step 2: MainViewModel に新タブを追加して画面遷移**

- [ ] **Step 3: 起動確認 → コミット**

```bash
dotnet run --project src/Tsumugi.App  # 手動確認: macOS / Windows 双方で起動 + 各タブ表示
git commit -am "feat(app): wire Phase 2 view models and tabs into composition root"
```

---

## Phase G: 品質ゲート・受け入れ確認

### Task G1: ArchitectureTests を Phase 2 にも適用

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/ArchitectureTests.cs`（必要なら）
- Create: `tests/Tsumugi.Domain.Tests/Wage/WageDomainPurityTests.cs`

**Interfaces:**
- Produces: Domain の `Logic/Wage/**.cs` と `Logic/AllocationPolicy.cs` が `double`/`float` 型を一切持たないことを源コードスキャンで判定する（最終金額計算に浮動小数点を混入させない制約の機械判定）。

- [ ] **Step 1: テスト**

```csharp
[Fact]
public void Wage_logic_files_do_not_use_floating_point_types()
{
    var roots = new[] { "Logic/Wage", "Logic/AllocationPolicy.cs" };
    foreach (var rel in roots)
    {
        var files = Directory.EnumerateFiles(
            Path.Combine(SolutionRoot, "src/Tsumugi.Domain/", rel),
            "*.cs", SearchOption.AllDirectories);
        foreach (var f in files)
        {
            var src = File.ReadAllText(f);
            src.Should().NotMatchRegex(@"\b(double|float)\b",
                "工賃計算ロジックに浮動小数点型を持ち込まない。比率は decimal を使う。");
        }
    }
}
```

- [ ] **Step 2: 緑 → コミット**

```bash
git commit -am "test(domain): forbid floating-point types in wage calculation logic"
```

---

### Task G2: AppOfflineComplianceTests に新規アセンブリを追加

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`

- [ ] **Step 1: 対象配列に `"Tsumugi.Infrastructure.Reporting"` を追加**

- [ ] **Step 2: テスト緑 → コミット**

```bash
git commit -am "test(infra): enforce offline compliance on Reporting assembly"
```

---

### Task G3: 受け入れ基準 AC2-1〜AC2-10 セルフチェック

**Files:** なし（チェックリストの実施のみ）

- [ ] **AC2-1**: WorkRecord 厳密追記 + WorkRecordPolicy + AppendOnlyGuard 違反テスト（Task B2/B3/C1 で済）
- [ ] **AC2-2**: WageSettings 期間マスタ + 実効日付 lookup（Task B5 で済）
- [ ] **AC2-3**: 4 方式の素計算（Task B8 で済）
- [ ] **AC2-4**: Σ配分＝原資 不変条件（Task B7 で済）
- [ ] **AC2-5**: 月次集計と FiscalYearStartMonth=4（Task B6/B9/D3 で済）
- [ ] **AC2-6**: WageStatement 確定 + 自動再計算しない（Task D4 で済）
- [ ] **AC2-7**: 工賃明細・支払一覧 PDF（Task E2/E3 で済）+ ADR 0013（Task A2）
- [ ] **AC2-8**: 平均工賃月額メトリクス暫定式 + open-questions（Task B10 / Task A1 で済）
- [ ] **AC2-9**: DailyRecord 二重 New partial unique index（Task A3 で済）+ AuditEntry 監査追記（Task D5 で済）
- [ ] **AC2-10**: キーボード完結 + ダーク低アニメ + 整数円桁区切り（Task F1〜F6 で済）

各受け入れ基準につき、対応する単体/結合テストの ID をリスト化して `docs/phase2-acceptance.md` に残す（**任意**、ただし Codex レビュー受領時に便利）。

---

### Task G4: 最終 CI とフォーマット確認

- [ ] **Step 1: 全テスト・format・依存方向不変を確認**

```bash
./build/ci.sh
dotnet format --verify-no-changes
```

- [ ] **Step 2: 手動 QA**: macOS / Windows 双方でアプリ起動し、作業実績入力 → 原資設定 → 工賃計算プレビュー → 確定 → PDF 出力 を golden path で 1 度ずつ実施。

- [ ] **Step 3: open-questions.md に残る Phase 2 項目を点検し、未完項目（KouchinModule 突合・平均工賃月額正式定義）を Phase 3 起点に再注記する**

- [ ] **Step 4: 完了コミット**

```bash
git commit -m "chore(phase2): finalize Phase 2 wage calculation (AC2-1..AC2-10 met)"
```

---

## 注記

### 計画外への余白
- Phase 1 引継ぎの「精神障害者保健福祉手帳の更新通知ビュー」「フェースシート差分表示」「障害種別整合の警告」「ContractedProvider/Contract 整理」は本フェーズに**意図的に含めない**（仕様 §3.4）。
- QuestPDF 採否が ADR 0013 で却下された場合、Task E1〜E3 は Avalonia 印刷経路に差し替える。`IWageReportGenerator` IF は変えず、実装クラスのみ差し替える設計にしてあるため、UseCase/ViewModel 側の改変は不要。

### Open Questions（実装中に踏むであろう論点）
- `RecipientDto`/`OfficeDto` を Reporting で使う場合の参照方向（Application 経由で Reporting に渡す）と、Domain Entity を Reporting に直接見せない原則。
- `YearMonth` を EF Core で扱う際の `int Year / int Month` 列展開（Task C2 で対応）と、Domain で公開する `YearMonth` プロパティのテスト等価性。
- WorkRecord の partial unique index は **DailyRecord と同方針**で適用したが、出来高方式で同一日に複数行の作業（複数の `PieceCount`/`PieceUnitYen` 組）を許す要件が出た場合は要再設計（その場合は `(RecipientId, WorkDate, OperationKind)` のような複合キーに変更）。

