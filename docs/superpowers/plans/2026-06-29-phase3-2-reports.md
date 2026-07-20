> **Status: SUPERSEDED（2026-07-20）** — 本計画は spec §8「確定時のスナップショットだけを入力」規約に反する設計だったため、`docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md` と `docs/superpowers/plans/2026-07-20-phase3-2-reports.md` に置き換え。以後編集しない。履歴として保存。

# Tsumugi Phase 3-2 実装計画 — 帳票（QuestPDF）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 埋込済みの Noto Sans JP を用いて、3 種類の請求関連 PDF 帳票（サービス提供実績記録票・介護給付費等請求書・介護給付費等請求明細書）を `Tsumugi.Infrastructure.Reporting` に追加し、Application UseCase と App UI を配線する。決定論（同入力＋同 TimeProvider → 同バイト）、CJK 抽出検証、合計値検証を CI で固定する。

**Architecture:** Phase 2 の `WageStatementPdfGenerator` と同じ流儀で `ClaimReportGenerator` を追加。`Application.Abstractions.IClaimReportGenerator` 経由で DI 注入し、`GenerateClaimReportsUseCase` から呼ぶ。App は `IFileSaveService` で保存先を取得し、`ClaimReportViewModel` が状態管理。

**Tech Stack:** .NET 10 / QuestPDF 2025.4.0 (Community License, Noto Sans JP 埋込済) / xUnit / FluentAssertions / Avalonia 11.3 / CommunityToolkit.Mvvm 8.4 / `TimeProvider`

## Global Constraints

> 親文書 `CLAUDE.md` §ハード制約、`01 §6`、`06_Phase3指示書 §4.2`、設計仕様書を尊守。

> **前提解消**: 日本語フォント埋込は Phase 4 / S1 で解消済 (`docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md`)。本プランでは前提として扱う。

- **着手条件**: Phase 3-0 完了、Phase 3-1 完了（`ClaimResult` DTO・UseCase が揃う）
- **決定論**: 同入力＋同 `TimeProvider` で同バイト列を返すこと（Phase 2 `WageStatementPdfGenerator` と同じ規律）
- **CJK 検証**: 抽出テキストに利用者氏名・事業所名・帳票タイトル等の漢字が含まれること（Linux/Windows CI で NUL 化しない）
- **依存方向**: `Tsumugi.Infrastructure.Reporting → Application/Domain`、`Tsumugi.App → Tsumugi.Application + Reporting`。Domain は Reporting を直接参照しない
- **オフライン**: `Tsumugi.Infrastructure.Reporting` は `System.Net.*` を直接参照しない（`OfflineComplianceTests` で機械判定済）
- **金額整形**: `InvariantCulture` で 3 桁区切り（カンマ）、整数円
- **TDD**: Red → Green → Refactor。1 コミット=1 論理変更。コミットメッセージに `phase3-2/AC3-N` を含める

## ファイル構成

```
src/Tsumugi.Application/
  Abstractions/
    IClaimReportGenerator.cs                                       新規
  Dtos/Claim/
    ServiceProvisionRecordDto.cs                                   新規 — 実績記録票用
    ClaimInvoiceDto.cs                                             新規 — 請求書用
    ClaimStatementDto.cs                                           新規 — 明細書用
  UseCases/Claim/
    GenerateClaimReportsUseCase.cs                                 新規

src/Tsumugi.Infrastructure.Reporting/
  ClaimReportGenerator.cs                                          新規 — IClaimReportGenerator 実装
  Components/                                                       新規 — QuestPDF 部品（再利用可能なヘッダ・フッタ等）
    JapaneseHeader.cs                                              新規
    JapaneseFooter.cs                                              新規

src/Tsumugi.App/
  ViewModels/Claim/
    ClaimReportViewModel.cs                                        新規
  Views/Claim/
    ClaimReportView.axaml                                          新規
    ClaimReportView.axaml.cs                                       新規

tests/
  Tsumugi.Application.Tests/UseCases/Claim/
    GenerateClaimReportsUseCaseTests.cs                            新規
  Tsumugi.Infrastructure.Reporting.Tests/
    ClaimReportGeneratorTests.cs                                   新規 — 3 種類の帳票・決定論・CJK 抽出
    ServiceProvisionRecordPdfTests.cs                              新規 — 実績記録票単体
    ClaimInvoicePdfTests.cs                                        新規 — 請求書単体
    ClaimStatementPdfTests.cs                                      新規 — 明細書単体
  Tsumugi.App.Tests/ViewModels/Claim/
    ClaimReportViewModelTests.cs                                   新規
```

---

### Task 1: 帳票 DTO 群

**Files:**
- Create: `src/Tsumugi.Application/Dtos/Claim/ServiceProvisionRecordDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/ClaimInvoiceDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/ClaimStatementDto.cs`

**Interfaces:**
- Consumes: Phase 3-1 の `ClaimResultDto`、Domain の `Recipient`/`Office`/`DailyRecord`
- Produces: 帳票描画に必要なフィールドを集約した DTO

各帳票で必要なデータは異なるため、3 種類を別 DTO に。

- [ ] **Step 1: ServiceProvisionRecordDto を作る（実績記録票）**

```csharp
namespace Tsumugi.Application.Dtos.Claim;

/// <summary>
/// サービス提供実績記録票（A4・利用者×月次）の描画 DTO。
/// </summary>
public sealed record ServiceProvisionRecordDto(
    string OfficeName,
    string OfficeNumber,
    string RecipientKanjiName,
    string CertificateNumber,
    YearMonth YearMonth,
    IReadOnlyList<DailyServiceRecordDto> Days);

public sealed record DailyServiceRecordDto(
    DateOnly ServiceDate,
    Attendance Attendance,
    bool MealProvided,
    TransportKind Transport,
    string? AbsenceResponseNote);
```

- [ ] **Step 2: ClaimInvoiceDto を作る（請求書）**

```csharp
public sealed record ClaimInvoiceDto(
    string OfficeName,
    string OfficeNumber,
    YearMonth YearMonth,
    int TotalUnit,
    int TotalAmountYen,
    int InsuranceAmountYen,    // 公費請求額（90%相当）
    int UserBurdenYen,
    ClaimMasterVersion MasterVersionRates,
    ClaimMasterVersion MasterVersionAdditions,
    ClaimMasterVersion MasterVersionBurdenCaps,
    ClaimMasterVersion MasterVersionRegionUnits);
```

- [ ] **Step 3: ClaimStatementDto を作る（明細書）**

```csharp
public sealed record ClaimStatementDto(
    string OfficeName,
    string OfficeNumber,
    YearMonth YearMonth,
    IReadOnlyList<RecipientClaimDetailDto> Recipients,
    int TotalUnit, int TotalAmountYen, int TotalBurdenYen);

public sealed record RecipientClaimDetailDto(
    string RecipientKanjiName,
    string CertificateNumber,
    IReadOnlyList<ClaimLineDto> Lines,
    int SubtotalUnit,
    int SubtotalAmountYen,
    ClaimBurdenDto Burden);
```

- [ ] **Step 4: ビルド緑確認 → コミット**

```bash
dotnet build src/Tsumugi.Application/Tsumugi.Application.csproj -c Release
git add src/Tsumugi.Application/Dtos/Claim/
git commit -m "feat(phase3-2/AC3-6): Claim report DTOs (record, invoice, statement)"
```

---

### Task 2: IClaimReportGenerator 抽象

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimReportGenerator.cs`

**Interfaces:**
- Consumes: Task 1 の DTO 群
- Produces: 
  - `byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto)`
  - `byte[] GenerateClaimInvoice(ClaimInvoiceDto dto)`
  - `byte[] GenerateClaimStatement(ClaimStatementDto dto)`

- [ ] **Step 1: 抽象を作る**

```csharp
using Tsumugi.Application.Dtos.Claim;

namespace Tsumugi.Application.Abstractions;

/// <summary>
/// 請求関連 PDF 帳票の生成抽象。実装は <c>Tsumugi.Infrastructure.Reporting</c>。
/// 既存 <see cref="IWageReportGenerator"/> と同型。決定論（同入力＋同 TimeProvider → 同バイト）。
/// </summary>
public interface IClaimReportGenerator
{
    /// <summary>サービス提供実績記録票（A4、利用者×月次）。</summary>
    byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求書（事業所×月次の集計）。</summary>
    byte[] GenerateClaimInvoice(ClaimInvoiceDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求明細書（事業所×月次の明細一覧）。</summary>
    byte[] GenerateClaimStatement(ClaimStatementDto dto);
}
```

- [ ] **Step 2: ビルド緑確認 → コミット**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimReportGenerator.cs
git commit -m "feat(phase3-2/AC3-6): IClaimReportGenerator abstraction"
```

---

### Task 3: ClaimReportGenerator — サービス提供実績記録票

**Files:**
- Create: `src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/ServiceProvisionRecordPdfTests.cs`

**Interfaces:**
- Consumes: `IClaimReportGenerator`、`ServiceProvisionRecordDto`、`TimeProvider`、QuestPDF
- Produces: 実績記録票 PDF byte 列

- [ ] **Step 1: 失敗テストを書く（PDF テキスト抽出 + CJK + 決定論）**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Tsumugi.Application.Dtos.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Reporting;
// 既存 Reporting Tests と同じ PDF テキスト抽出ヘルパを再利用（UglyToad.PdfPig 等）

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class ServiceProvisionRecordPdfTests
{
    [Fact]
    public void GenerateServiceProvisionRecord_includes_office_recipient_name_and_month_in_extracted_text()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var gen = new ClaimReportGenerator(clock);
        var dto = new ServiceProvisionRecordDto(
            OfficeName: "テスト事業所",
            OfficeNumber: "0123456789",
            RecipientKanjiName: "山田太郎",
            CertificateNumber: "9876543210",
            YearMonth: new YearMonth(2026, 5),
            Days: Enumerable.Range(1, 31).Select(d => new DailyServiceRecordDto(
                new DateOnly(2026, 5, d),
                Attendance: d % 7 != 0 ? Attendance.Present : Attendance.Absent,
                MealProvided: d % 7 != 0,
                Transport: TransportKind.None,
                AbsenceResponseNote: null)).ToList());

        var bytes = gen.GenerateServiceProvisionRecord(dto);
        bytes.Should().NotBeEmpty();
        var text = PdfTextExtractor.Extract(bytes);   // 既存ヘルパ
        text.Should().Contain("テスト事業所");
        text.Should().Contain("山田太郎");
        text.Should().Contain("2026年5月");
    }

    [Fact]
    public void GenerateServiceProvisionRecord_is_deterministic_for_same_inputs_and_same_timeprovider()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock1 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var clock2 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var dto = /* 同じ DTO */;
        var bytes1 = new ClaimReportGenerator(clock1).GenerateServiceProvisionRecord(dto);
        var bytes2 = new ClaimReportGenerator(clock2).GenerateServiceProvisionRecord(dto);
        bytes1.Should().Equal(bytes2);
    }
}
```

- [ ] **Step 2: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "ServiceProvisionRecord" -v normal`
Expected: FAIL（`ClaimReportGenerator` 未定義）

- [ ] **Step 3: ClaimReportGenerator の骨組みと GenerateServiceProvisionRecord を実装**

```csharp
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos.Claim;

namespace Tsumugi.Infrastructure.Reporting;

public sealed class ClaimReportGenerator(TimeProvider clock) : IClaimReportGenerator
{
    private const string FontFamily = QuestPdfLicenseConfigurator.NotoSansJpFamilyName;

    public byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var pdf = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(20, Unit.Millimetre);
                p.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(10));

                p.Header().Row(h =>
                {
                    h.RelativeItem().Text(t =>
                    {
                        t.Span($"サービス提供実績記録票").FontSize(16).Bold();
                        t.EmptyLine();
                        t.Span($"対象月: {dto.YearMonth.Year}年{dto.YearMonth.Month}月");
                        t.EmptyLine();
                        t.Span($"事業所: {dto.OfficeName} ({dto.OfficeNumber})");
                        t.EmptyLine();
                        t.Span($"利用者: {dto.RecipientKanjiName} ({dto.CertificateNumber})");
                    });
                });

                p.Content().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(1);  // 日付
                        cd.RelativeColumn(1);  // 出欠
                        cd.RelativeColumn(1);  // 食事
                        cd.RelativeColumn(1);  // 送迎
                        cd.RelativeColumn(2);  // 欠席時対応
                    });
                    table.Header(h =>
                    {
                        h.Cell().Border(1).Padding(2).Text("日付").Bold();
                        h.Cell().Border(1).Padding(2).Text("出欠").Bold();
                        h.Cell().Border(1).Padding(2).Text("食事").Bold();
                        h.Cell().Border(1).Padding(2).Text("送迎").Bold();
                        h.Cell().Border(1).Padding(2).Text("欠席時対応").Bold();
                    });
                    foreach (var d in dto.Days)
                    {
                        table.Cell().Border(1).Padding(2).Text($"{d.ServiceDate:M/d}");
                        table.Cell().Border(1).Padding(2).Text(d.Attendance.ToString());
                        table.Cell().Border(1).Padding(2).Text(d.MealProvided ? "○" : "");
                        table.Cell().Border(1).Padding(2).Text(d.Transport.ToString());
                        table.Cell().Border(1).Padding(2).Text(d.AbsenceResponseNote ?? "");
                    }
                });

                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"出力日時: {clock.GetUtcNow().UtcDateTime:yyyy/MM/dd HH:mm:ss} UTC")
                     .FontSize(8);
                });
            });
        });
        return pdf.GeneratePdf();
    }

    public byte[] GenerateClaimInvoice(ClaimInvoiceDto dto) =>
        throw new NotImplementedException("Task 4");

    public byte[] GenerateClaimStatement(ClaimStatementDto dto) =>
        throw new NotImplementedException("Task 5");
}
```

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "ServiceProvisionRecord" -v normal
git add src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs tests/Tsumugi.Infrastructure.Reporting.Tests/ServiceProvisionRecordPdfTests.cs
git commit -m "feat(phase3-2/AC3-6): ClaimReportGenerator.GenerateServiceProvisionRecord"
```

---

### Task 4: ClaimReportGenerator — 介護給付費等 請求書

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimInvoicePdfTests.cs`

**Interfaces:**
- Consumes: `ClaimInvoiceDto`、`TimeProvider`
- Produces: 請求書 PDF byte 列

- [ ] **Step 1: 失敗テストを書く**

```csharp
public sealed class ClaimInvoicePdfTests
{
    [Fact]
    public void GenerateClaimInvoice_shows_office_name_and_total_amounts_and_master_versions()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var gen = new ClaimReportGenerator(clock);
        var v = new ClaimMasterVersion("v2024.04", new DateOnly(2026, 6, 29), "ADR-0018");
        var dto = new ClaimInvoiceDto(
            "テスト事業所", "0123456789", new YearMonth(2026, 5),
            TotalUnit: 12000, TotalAmountYen: 1_344_000,
            InsuranceAmountYen: 1_209_600,  // 90%相当
            UserBurdenYen: 134_400,
            v, v, v, v);
        var bytes = gen.GenerateClaimInvoice(dto);
        var text = PdfTextExtractor.Extract(bytes);
        text.Should().Contain("テスト事業所");
        text.Should().Contain("介護給付費・訓練等給付費等請求書");
        text.Should().Contain("1,344,000");
        text.Should().Contain("ADR-0018");
    }

    [Fact]
    public void GenerateClaimInvoice_is_deterministic() { /* ... */ }
}
```

- [ ] **Step 2: テスト赤確認 → 実装**

```csharp
public byte[] GenerateClaimInvoice(ClaimInvoiceDto dto)
{
    ArgumentNullException.ThrowIfNull(dto);
    var pdf = Document.Create(c =>
    {
        c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(20, Unit.Millimetre);
            p.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(11));

            p.Header().Column(col =>
            {
                col.Item().AlignCenter().Text(t =>
                    t.Span("介護給付費・訓練等給付費等請求書").FontSize(18).Bold());
                col.Item().PaddingTop(8).Text($"請求対象月: {dto.YearMonth.Year}年{dto.YearMonth.Month}月");
                col.Item().Text($"事業所: {dto.OfficeName} ({dto.OfficeNumber})");
            });

            p.Content().PaddingVertical(10).Table(table =>
            {
                table.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(1); });
                table.Cell().Padding(4).Text("総単位数").Bold();
                table.Cell().Padding(4).AlignRight().Text($"{dto.TotalUnit:N0}");
                table.Cell().Padding(4).Text("総額（円）").Bold();
                table.Cell().Padding(4).AlignRight().Text($"{dto.TotalAmountYen:N0}");
                table.Cell().Padding(4).Text("公費請求額（円）").Bold();
                table.Cell().Padding(4).AlignRight().Text($"{dto.InsuranceAmountYen:N0}");
                table.Cell().Padding(4).Text("利用者負担合計（円）").Bold();
                table.Cell().Padding(4).AlignRight().Text($"{dto.UserBurdenYen:N0}");
            });

            p.Footer().Column(col =>
            {
                col.Item().Text($"報酬マスタ版: {dto.MasterVersionRates.Adr} {dto.MasterVersionRates.Version}").FontSize(8);
                col.Item().Text($"加算マスタ版: {dto.MasterVersionAdditions.Adr} {dto.MasterVersionAdditions.Version}").FontSize(8);
                col.Item().Text($"負担上限マスタ版: {dto.MasterVersionBurdenCaps.Adr} {dto.MasterVersionBurdenCaps.Version}").FontSize(8);
                col.Item().Text($"地域単価マスタ版: {dto.MasterVersionRegionUnits.Adr} {dto.MasterVersionRegionUnits.Version}").FontSize(8);
                col.Item().AlignCenter().Text($"出力日時: {clock.GetUtcNow().UtcDateTime:yyyy/MM/dd HH:mm:ss} UTC").FontSize(8);
            });
        });
    });
    return pdf.GeneratePdf();
}
```

- [ ] **Step 3: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "ClaimInvoice" -v normal
git add src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimInvoicePdfTests.cs
git commit -m "feat(phase3-2/AC3-6): ClaimReportGenerator.GenerateClaimInvoice"
```

---

### Task 5: ClaimReportGenerator — 介護給付費等 請求明細書

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimStatementPdfTests.cs`

**Interfaces:**
- Consumes: `ClaimStatementDto`、`TimeProvider`
- Produces: 明細書 PDF byte 列

- [ ] **Step 1: 失敗テストを書く**

```csharp
public sealed class ClaimStatementPdfTests
{
    [Fact]
    public void GenerateClaimStatement_lists_each_recipient_with_basic_and_additions()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var gen = new ClaimReportGenerator(clock);
        var dto = new ClaimStatementDto(
            "テスト事業所", "0123456789", new YearMonth(2026, 5),
            Recipients: new[]
            {
                new RecipientClaimDetailDto(
                    "山田太郎", "9876543210",
                    Lines: new[]
                    {
                        new ClaimLineDto(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 6720),
                        new ClaimLineDto(ClaimDetailLineKind.Addition, "MEAL_PROVISION_I", 30, 336),
                    },
                    SubtotalUnit: 630, SubtotalAmountYen: 7056,
                    Burden: new ClaimBurdenDto(705, 9300, 705)),
            },
            TotalUnit: 630, TotalAmountYen: 7056, TotalBurdenYen: 705);
        var bytes = gen.GenerateClaimStatement(dto);
        var text = PdfTextExtractor.Extract(bytes);
        text.Should().Contain("テスト事業所");
        text.Should().Contain("山田太郎");
        text.Should().Contain("B_BASE_W1_C20_S1");
        text.Should().Contain("MEAL_PROVISION_I");
    }

    [Fact]
    public void GenerateClaimStatement_is_deterministic() { /* ... */ }
}
```

- [ ] **Step 2: テスト赤確認 → 実装**

```csharp
public byte[] GenerateClaimStatement(ClaimStatementDto dto)
{
    ArgumentNullException.ThrowIfNull(dto);
    var pdf = Document.Create(c =>
    {
        c.Page(p =>
        {
            p.Size(PageSizes.A4);
            p.Margin(15, Unit.Millimetre);
            p.DefaultTextStyle(t => t.FontFamily(FontFamily).FontSize(9));

            p.Header().Column(col =>
            {
                col.Item().AlignCenter().Text(t => t.Span("介護給付費・訓練等給付費等請求明細書").FontSize(14).Bold());
                col.Item().PaddingTop(6).Text($"対象月: {dto.YearMonth.Year}年{dto.YearMonth.Month}月");
                col.Item().Text($"事業所: {dto.OfficeName} ({dto.OfficeNumber})");
            });

            p.Content().Column(col =>
            {
                foreach (var r in dto.Recipients)
                {
                    col.Item().PaddingTop(8).Text(t =>
                        t.Span($"{r.RecipientKanjiName} ({r.CertificateNumber})").Bold());
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1);
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(1);
                            cd.RelativeColumn(1);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Border(1).Padding(2).Text("種別").Bold();
                            h.Cell().Border(1).Padding(2).Text("コード").Bold();
                            h.Cell().Border(1).Padding(2).Text("単位数").Bold();
                            h.Cell().Border(1).Padding(2).Text("金額(円)").Bold();
                        });
                        foreach (var line in r.Lines)
                        {
                            t.Cell().Border(1).Padding(2).Text(line.LineKind.ToString());
                            t.Cell().Border(1).Padding(2).Text(line.Code);
                            t.Cell().Border(1).Padding(2).AlignRight().Text($"{line.Unit:N0}");
                            t.Cell().Border(1).Padding(2).AlignRight().Text($"{line.AmountYen:N0}");
                        }
                    });
                    col.Item().Text($"小計 単位数: {r.SubtotalUnit:N0} / 金額: {r.SubtotalAmountYen:N0}円 / 利用者負担: {r.Burden.EffectiveBurdenYen:N0}円");
                }
            });

            p.Footer().AlignCenter().Text($"合計 単位数: {dto.TotalUnit:N0} / 金額: {dto.TotalAmountYen:N0}円 / 利用者負担合計: {dto.TotalBurdenYen:N0}円  ({clock.GetUtcNow().UtcDateTime:yyyy/MM/dd HH:mm:ss} UTC)").FontSize(8);
        });
    });
    return pdf.GeneratePdf();
}
```

- [ ] **Step 3: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "ClaimStatement" -v normal
git add src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimStatementPdfTests.cs
git commit -m "feat(phase3-2/AC3-6): ClaimReportGenerator.GenerateClaimStatement"
```

---

### Task 6: GenerateClaimReportsUseCase

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/GenerateClaimReportsUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/UseCases/Claim/GenerateClaimReportsUseCaseTests.cs`

**Interfaces:**
- Consumes: `IClaimReportGenerator`、`QueryClaimUseCase`（Phase 3-1）、各リポジトリ
- Produces: 
  - `GenerateClaimReportsUseCase.GenerateServiceProvisionRecordAsync(Guid officeId, Guid recipientId, YearMonth ym, CancellationToken) → Task<byte[]>`
  - `GenerateClaimReportsUseCase.GenerateClaimInvoiceAsync(Guid officeId, YearMonth ym, CancellationToken) → Task<byte[]>`
  - `GenerateClaimReportsUseCase.GenerateClaimStatementAsync(Guid officeId, YearMonth ym, CancellationToken) → Task<byte[]>`

- [ ] **Step 1: 失敗テストを書く（モックリポジトリ + モック generator）**

```csharp
public sealed class GenerateClaimReportsUseCaseTests
{
    [Fact]
    public async Task GenerateServiceProvisionRecordAsync_assembles_dto_and_invokes_generator()
    {
        var genMock = new Mock<IClaimReportGenerator>();
        genMock.Setup(g => g.GenerateServiceProvisionRecord(It.IsAny<ServiceProvisionRecordDto>()))
               .Returns(new byte[] { 1, 2, 3 });

        var uc = new GenerateClaimReportsUseCase(
            queryUseCase: /* mock */, dailyRepo: /* mock */, officeRepo: /* mock */,
            recipientRepo: /* mock */, certRepo: /* mock */, generator: genMock.Object);

        var bytes = await uc.GenerateServiceProvisionRecordAsync(officeId, recipientId, new YearMonth(2026, 5), default);
        bytes.Should().Equal(1, 2, 3);
        genMock.Verify(g => g.GenerateServiceProvisionRecord(It.IsAny<ServiceProvisionRecordDto>()), Times.Once);
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_assembles_dto_from_QueryClaimUseCase() { /* ... */ }

    [Fact]
    public async Task GenerateClaimStatementAsync_assembles_dto_from_QueryClaimUseCase() { /* ... */ }
}
```

- [ ] **Step 2: テスト赤確認 → 実装**

```csharp
public sealed class GenerateClaimReportsUseCase(
    QueryClaimUseCase queryUseCase,
    IDailyRecordRepository dailyRepo,
    IOfficeRepository officeRepo,
    IRecipientRepository recipientRepo,
    ICertificateRepository certRepo,
    IClaimReportGenerator generator)
{
    public async Task<byte[]> GenerateServiceProvisionRecordAsync(
        Guid officeId, Guid recipientId, YearMonth ym, CancellationToken ct)
    {
        var office = await officeRepo.GetByIdAsync(officeId, ct) ?? throw new InvalidOperationException(...);
        var recipient = await recipientRepo.GetByIdAsync(recipientId, ct) ?? throw ...;
        var cert = await certRepo.GetEffectiveAsync(recipientId, ym.LastDay, ct) ?? throw ...;
        var days = await dailyRepo.GetEffectiveByRecipientAndMonthAsync(recipientId, ym, ct);

        var dto = new ServiceProvisionRecordDto(
            office.OfficeName, office.OfficeNumber,
            recipient.KanjiName, cert.CertificateNumber, ym,
            Days: BuildDailyList(days, ym));
        return generator.GenerateServiceProvisionRecord(dto);
    }

    public async Task<byte[]> GenerateClaimInvoiceAsync(Guid officeId, YearMonth ym, CancellationToken ct)
    {
        var office = await officeRepo.GetByIdAsync(officeId, ct) ?? throw ...;
        var query = await queryUseCase.ExecuteAsync(officeId, ym, ct);
        var result = query.Result;

        var dto = new ClaimInvoiceDto(
            office.OfficeName, office.OfficeNumber, ym,
            TotalUnit: result.TotalUnit,
            TotalAmountYen: result.TotalAmountYen,
            InsuranceAmountYen: result.TotalAmountYen - result.TotalBurdenYen,
            UserBurdenYen: result.TotalBurdenYen,
            result.MasterVersionRates, result.MasterVersionAdditions,
            result.MasterVersionBurdenCaps, result.MasterVersionRegionUnits);
        return generator.GenerateClaimInvoice(dto);
    }

    public async Task<byte[]> GenerateClaimStatementAsync(Guid officeId, YearMonth ym, CancellationToken ct)
    {
        var office = await officeRepo.GetByIdAsync(officeId, ct) ?? throw ...;
        var query = await queryUseCase.ExecuteAsync(officeId, ym, ct);
        var result = query.Result;
        // RecipientId → 名前/受給者証番号の辞書を作って差し込み
        var dto = await MapToStatementDtoAsync(office, ym, result, recipientRepo, certRepo, ct);
        return generator.GenerateClaimStatement(dto);
    }
}
```

- [ ] **Step 3: テスト緑確認 → コミット**

```bash
git commit -m "feat(phase3-2/AC3-6): GenerateClaimReportsUseCase"
```

---

### Task 7: App DI 登録と Reporting アセンブリ拡張

**Files:**
- Modify: `src/Tsumugi.App/Program.cs` or `App.axaml.cs`（DI 登録）

- [ ] **Step 1: ClaimReportGenerator を DI に登録**

既存 `WageStatementPdfGenerator` の登録と同じ場所:

```csharp
services.AddSingleton<IClaimReportGenerator, ClaimReportGenerator>();
services.AddScoped<GenerateClaimReportsUseCase>();
```

- [ ] **Step 2: ビルド緑確認 → コミット**

```bash
git commit -m "feat(phase3-2/AC3-6): wire ClaimReportGenerator into DI"
```

---

### Task 8: ClaimReportViewModel

**Files:**
- Create: `src/Tsumugi.App/ViewModels/Claim/ClaimReportViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/ViewModels/Claim/ClaimReportViewModelTests.cs`

**Interfaces:**
- Consumes: `GenerateClaimReportsUseCase`、`IFileSaveService`、`ListOfficesUseCase`、`ListRecipientsUseCase`
- Produces: 月選択・事業所選択・受給者選択（実績記録票のみ）・レポート種別選択・出力コマンド

`enum ClaimReportKind { ServiceProvisionRecord, Invoice, Statement }`

- [ ] **Step 1〜N: TDD で実装**

CommunityToolkit.Mvvm の `[ObservableProperty]` / `[RelayCommand]` を使う。Phase 2 の `WageStatementViewModel` と同じ流儀。

```csharp
public sealed partial class ClaimReportViewModel : ObservableObject
{
    [ObservableProperty] private YearMonth selectedYearMonth = new(2026, 5);
    [ObservableProperty] private OfficeDto? selectedOffice;
    [ObservableProperty] private RecipientDto? selectedRecipient;
    [ObservableProperty] private ClaimReportKind kind = ClaimReportKind.ServiceProvisionRecord;
    public ObservableCollection<OfficeDto> Offices { get; } = new();
    public ObservableCollection<RecipientDto> Recipients { get; } = new();

    private readonly GenerateClaimReportsUseCase generate;
    private readonly IFileSaveService fileSave;
    private readonly ListOfficesUseCase listOffices;
    private readonly ListRecipientsUseCase listRecipients;

    public ClaimReportViewModel(/* DI */) { /* ... */ }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        Offices.Clear();
        foreach (var o in await listOffices.ExecuteAsync(ct)) Offices.Add(o);
        Recipients.Clear();
        foreach (var r in await listRecipients.ExecuteAsync(ct)) Recipients.Add(r);
    }

    [RelayCommand(CanExecute = nameof(CanSaveAsync))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (SelectedOffice is null) return;
        byte[] bytes = Kind switch
        {
            ClaimReportKind.ServiceProvisionRecord =>
                await generate.GenerateServiceProvisionRecordAsync(SelectedOffice.Id, SelectedRecipient!.Id, SelectedYearMonth, ct),
            ClaimReportKind.Invoice =>
                await generate.GenerateClaimInvoiceAsync(SelectedOffice.Id, SelectedYearMonth, ct),
            ClaimReportKind.Statement =>
                await generate.GenerateClaimStatementAsync(SelectedOffice.Id, SelectedYearMonth, ct),
            _ => throw new InvalidOperationException()
        };
        var defaultName = $"{Kind}-{SelectedYearMonth.Year}-{SelectedYearMonth.Month:D2}.pdf";
        var path = await fileSave.PickSaveFileAsync(defaultName, ".pdf", ct);
        if (path is null) return;
        await File.WriteAllBytesAsync(path, bytes, ct);
    }

    private bool CanSaveAsync() => SelectedOffice is not null
        && (Kind != ClaimReportKind.ServiceProvisionRecord || SelectedRecipient is not null);
}
```

- [ ] **Step N+1: テスト（モック UseCase / IFileSaveService）**

```csharp
public sealed class ClaimReportViewModelTests
{
    [Fact]
    public async Task SaveAsync_invokes_generator_and_writes_to_picked_path()
    {
        var gen = new Mock<GenerateClaimReportsUseCase>(/* ... */);
        gen.Setup(g => g.GenerateClaimInvoiceAsync(It.IsAny<Guid>(), It.IsAny<YearMonth>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new byte[] { 9, 9 });

        var save = new Mock<IFileSaveService>();
        save.Setup(s => s.PickSaveFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/test.pdf");

        var vm = new ClaimReportViewModel(/* ... */);
        vm.SelectedOffice = new OfficeDto { /* ... */ };
        vm.Kind = ClaimReportKind.Invoice;
        await vm.SaveCommand.ExecuteAsync(null);
        // file contents
    }
}
```

- [ ] **Step N+2: コミット**

```bash
git commit -m "feat(phase3-2/AC3-6): ClaimReportViewModel"
```

---

### Task 9: ClaimReportView (XAML)

**Files:**
- Create: `src/Tsumugi.App/Views/Claim/ClaimReportView.axaml`
- Create: `src/Tsumugi.App/Views/Claim/ClaimReportView.axaml.cs`
- Modify: `src/Tsumugi.App/Views/MainWindow.axaml` or 既存ナビゲーションへ追加

- [ ] **Step 1: ClaimReportView.axaml を作る**

Phase 2 の `WageStatementView.axaml` を参考に、Phase 1 で固定した `AccessibilityDefaults`（ダークテーマ・低アニメ・広い余白・キーボード操作）を踏襲。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:vm="clr-namespace:Tsumugi.App.ViewModels.Claim"
             x:DataType="vm:ClaimReportViewModel"
             x:Class="Tsumugi.App.Views.Claim.ClaimReportView">
  <StackPanel Margin="16" Spacing="12">
    <TextBlock Text="請求帳票出力" FontSize="20" FontWeight="Bold" />

    <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,Auto,Auto,Auto" RowSpacing="8" ColumnSpacing="8">
      <TextBlock Text="対象月:" />
      <DatePicker Grid.Column="1" SelectedDate="{Binding SelectedYearMonth.FirstDay}" />

      <TextBlock Grid.Row="1" Text="事業所:" />
      <ComboBox Grid.Row="1" Grid.Column="1" ItemsSource="{Binding Offices}" SelectedItem="{Binding SelectedOffice}" />

      <TextBlock Grid.Row="2" Text="種別:" />
      <ComboBox Grid.Row="2" Grid.Column="1" SelectedItem="{Binding Kind}">
        <ComboBoxItem Content="サービス提供実績記録票" />
        <ComboBoxItem Content="請求書" />
        <ComboBoxItem Content="請求明細書" />
      </ComboBox>

      <TextBlock Grid.Row="3" Text="利用者:" IsVisible="{Binding Kind, Converter={StaticResource ServiceProvisionRecordSelected}}" />
      <ComboBox Grid.Row="3" Grid.Column="1" ItemsSource="{Binding Recipients}" SelectedItem="{Binding SelectedRecipient}"
                IsVisible="{Binding Kind, Converter={StaticResource ServiceProvisionRecordSelected}}" />

      <Button Grid.Row="4" Grid.Column="1" Content="保存" Command="{Binding SaveCommand}" />
    </Grid>
  </StackPanel>
</UserControl>
```

> `ServiceProvisionRecordSelected` Converter は新規作成（既存 Converter 配置に倣う）。

- [ ] **Step 2: 既存ナビゲーションに追加**

`MainWindow.axaml` の `TabControl` か Sidebar に「請求帳票」タブを追加（既存「工賃」と同じ配置）。

- [ ] **Step 3: ビルド緑確認 + 起動確認**

```bash
dotnet build -c Release
dotnet run --project src/Tsumugi.App
```

App が起動し、「請求帳票」タブが表示され、月選択・事業所選択・種別切替が動作することを目視確認（DataBind 確認）。

- [ ] **Step 4: コミット**

```bash
git commit -m "feat(phase3-2/AC3-6): ClaimReportView (XAML wiring)"
```

---

### Task 10: open-questions.md / CHANGELOG / 受け入れ確認

**Files:**
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Create: `docs/phase3-2-acceptance.md`

- [ ] **Step 1: docs/phase3-2-acceptance.md 作成**

AC3-5 / AC3-6 / AC3-10（横断）の達成根拠を列挙。

- [ ] **Step 2: CHANGELOG 更新**

Phase 3-2 完了を追記。新 PDF 3 種類と DI 配線、Avalonia View 追加を記録。

- [ ] **Step 3: `./build/ci.sh` 緑確認**

```bash
./build/ci.sh
```

- [ ] **Step 4: macOS / Windows 両方で起動確認（手動 QA）**

`dotnet run --project src/Tsumugi.App` で起動し、「請求帳票」タブから 3 種類の帳票出力を試す（保存→ファイル開いて目視確認）。

- [ ] **Step 5: コミット**

```bash
git commit -m "docs(phase3-2): Phase 3-2 acceptance complete + CHANGELOG"
```

---

## Phase 3-2 全体受け入れ基準

- [ ] AC3-5 日本語フォント埋込で CI で CJK 化けなし（前提解消済 — 上記 Global Constraints 参照）
- [ ] AC3-6 3 種類の帳票（実績記録票・請求書・明細書）出力、抽出テキスト＋合計＋CJK 検証、決定論
- [ ] AC3-10（横断）`./build/ci.sh` 緑、依存方向不変、オフライン検査緑（Reporting 含む）

## 参考

- 設計仕様書: `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`
- Phase 3 指示書 §4.2
- Phase 3-0 計画 / 3-1 計画
- Phase 2 帳票実装: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`、`tests/Tsumugi.Infrastructure.Reporting.Tests/`
