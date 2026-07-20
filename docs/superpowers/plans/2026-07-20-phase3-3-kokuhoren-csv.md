# Phase 3-3 国保連請求 CSV 生成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 確定済み `ClaimBatch` と独立入力 `ProcessingMonth` から、国保連仕様準拠の CP932/CRLF CSV をバイト決定論的に生成し、AC3-7 を満たす。

**Architecture:** ハイブリッド方式 — recordType 9 種の `IProviderRecordBuilder<T>` が strong-typed に `CsvRow` を組み、`CsvCellEncoder` が spec JSON 駆動で byte 幅・引用・CP932 変換を担い、`ClaimCsvWriter` が外側 3 レコード（control=1 / data=2..n / end=3）を組み立てる。`ExportClaimCsvUseCase` が確定ガード・履歴追記（`ClaimCsvExport` append-only）・fail-close を統括する。

**Tech Stack:** .NET 10 / C# 14 / Avalonia 11.x / EF Core 10.x / xUnit / FluentAssertions / SQLite

## Global Constraints

- .NET SDK 10.0（`net10.0`）、C# 14（`<LangVersion>` 下げ禁止）
- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- 主キーは `Guid`、`作成日時` / `作成者` 必須、更新は楽観的同時実行トークン（append-only エンティティは token 不要）
- `record` + append-only。訂正は新レコードで表現
- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`、`Infrastructure.Csv → Application/Domain`
- オフライン: `Tsumugi.Infrastructure.Csv` を含む全 production アセンブリで通信 API 禁止
- Domain/Application に制度実値を置かない。CSV 仕様・サービスコードは `Tsumugi.Infrastructure.Csv` の seed/spec に限定
- ログに氏名・受給者証番号・保存先フルパスを出さない
- 金額は整数円。単位数単価は 10 進固定小数、`double`/`float` 禁止
- 決定論: 同一入力（ClaimBatch + ProcessingMonth + spec 版 + reward master 版 + service code 版）で**バイト列一致**
- `ITimeProvider`（`FakeTimeProvider` 差し替え可）から作成日時を取得
- 1 コミット = 1 論理変更。メッセージにフェーズ番号と受け入れ基準 ID を書く（例: `feat(phase3-3/task 4/AC3-7): ...`）
- CI ゲート: `./build/ci.sh` 緑

## File Structure

**新規（Domain / Application / Infrastructure）**
- `src/Tsumugi.Domain/Entities/ClaimCsvExport.cs` — append-only 出力履歴 record
- `src/Tsumugi.Application/Abstractions/IClaimCsvExportRepository.cs`
- `src/Tsumugi.Application/Claim/ExportClaimCsvUseCase.cs`
- `src/Tsumugi.Application/Claim/ClaimCsvExportFailedException.cs`
- `src/Tsumugi.Application/Claim/ClaimBatchNotFinalizedException.cs`
- `src/Tsumugi.Application/Claim/ClaimCsvReadinessException.cs`
- `src/Tsumugi.Application/Dtos/Claim/ClaimCsvExportResult.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimCsvExportConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/ClaimCsvExportRepository.cs`
- `src/Tsumugi.Infrastructure/Migrations/{ts}_Phase33ClaimCsvExport.cs`
- `src/Tsumugi.Infrastructure/Migrations/{ts}_Phase33ClaimInputProviderFields.cs`

**新規（Infrastructure.Csv writer 群）**
- `src/Tsumugi.Infrastructure.Csv/Writer/CsvCell.cs` — セル値の VO（`FieldId` + `Raw` string）
- `src/Tsumugi.Infrastructure.Csv/Writer/CsvRow.cs`
- `src/Tsumugi.Infrastructure.Csv/Writer/CsvCellEncoder.cs` — byte 幅・quote・CP932 fail-close
- `src/Tsumugi.Infrastructure.Csv/Writer/ClaimCsvWriter.cs` — 外側 3 レコード
- `src/Tsumugi.Infrastructure.Csv/Writer/RecordIdRouter.cs` — ClaimBatch → recordType 分配
- `src/Tsumugi.Infrastructure.Csv/Writer/IProviderRecordBuilder.cs`
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceHeaderBuilder.cs` — J111:01
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceTotalsBuilder.cs` — J111:02
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimContractBuilder.cs` — J121:01
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimStatementSummaryBuilder.cs` — J121:02
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimStatementLineBuilder.cs` — J121:03
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimStatementAggregateBuilder.cs` — J121:04
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimTransitionalBuilder.cs` — J121:05
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/UpperLimitManagementHeaderBuilder.cs` — J611:01
- `src/Tsumugi.Infrastructure.Csv/Writer/Builders/UpperLimitManagementLineBuilder.cs` — J611:02
- `src/Tsumugi.Infrastructure.Csv/Writer/Sources/*.cs` — 9 種 source DTO（例: `ClaimInvoiceHeaderSource`）
- `src/Tsumugi.Infrastructure.Csv/ServiceCode/IServiceCodeCatalog.cs`
- `src/Tsumugi.Infrastructure.Csv/ServiceCode/JsonServiceCodeCatalog.cs`
- `src/Tsumugi.Infrastructure.Csv/Specifications/service-code-r8-06.json` — R8.6 サービスコード表 seed

**新規（App）**
- `src/Tsumugi.App/ViewModels/CsvExportViewModel.cs`
- `src/Tsumugi.App/Views/CsvExportView.axaml`
- `src/Tsumugi.App/Views/CsvExportView.axaml.cs`

**修正（既存）**
- `src/Tsumugi.Domain/Entities/ClaimInput.cs` — provider:* 追加フィールド
- `src/Tsumugi.Application/Dtos/ClaimInputDtos.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimInputConfiguration.cs`
- `src/Tsumugi.Infrastructure.Csv/Specifications/field-mapping-r7-10.json` — provider:* の `status: "missing"` + entityPath 追記
- `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs` — 孤立 4 フィールド cross-field 条件パーサ拡張（既に Any-merge 対応済み、宣言のみ追加）
- `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs` — provider:* プロパティ
- `src/Tsumugi.App/Views/ClaimInputView.axaml` — provider:* セクション
- `src/Tsumugi.App/DependencyInjection/*` — DI 登録（`ExportClaimCsvUseCase` 等）

**新規（テスト）**
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/CsvCellEncoderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/ClaimCsvWriterTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/RecordIdRouterTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceHeaderBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceTotalsBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimContractBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimStatementSummaryBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimStatementLineBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimStatementAggregateBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimTransitionalBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/UpperLimitManagementHeaderBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/UpperLimitManagementLineBuilderTests.cs`
- `tests/Tsumugi.Infrastructure.Csv.Tests/ServiceCode/JsonServiceCodeCatalogTests.cs`
- `tests/Tsumugi.Application.Tests/Claim/ExportClaimCsvUseCaseTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportRepositoryTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportProductionWiringTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Claim/GoldenCsvSnapshotTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-normal.csv`
- `tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-correction.csv`
- `tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-cjk.csv`
- `tests/Tsumugi.App.Tests/CsvExportViewModelTests.cs`

**新規（ドキュメント）**
- `docs/decisions/0031-service-code-r8-06-service-code-table.md`
- `docs/phase3-3-acceptance.md`

**修正（ドキュメント）**
- `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`（ハイブリッド方式追記）
- `docs/decisions/0030-report-input-ui-responsibility.md`（provider:* も ClaimInputView 集約追記）
- `docs/open-questions.md`（孤立 4 フィールド解消、R8.6 サービスコード表解消）

---

## Task 1: `ClaimCsvExport` エンティティと append-only リポジトリ

**Files:**
- Create: `src/Tsumugi.Domain/Entities/ClaimCsvExport.cs`
- Create: `src/Tsumugi.Application/Abstractions/IClaimCsvExportRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimCsvExportConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/ClaimCsvExportRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/{ts}_Phase33ClaimCsvExport.cs`（`dotnet ef` 生成）
- Test: `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportRepositoryTests.cs`

**Interfaces:**
- Produces:
  - `Tsumugi.Domain.Entities.ClaimCsvExport`（record with: `Id`, `ClaimBatchId`, `ProcessingMonth`, `CsvSpecVersion`, `RewardMasterVersion`, `ServiceCodeVersion`, `Sha256`, `CreatedAtUtc`, `CreatedBy`）
  - `IClaimCsvExportRepository.AppendAsync(ClaimCsvExport, CancellationToken)`
  - `IClaimCsvExportRepository.ListByBatchAsync(Guid claimBatchId, CancellationToken)`

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportRepositoryTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests.Claim;

public sealed class ClaimCsvExportRepositoryTests
{
    [Fact]
    public async Task AppendAsync_persists_new_export_and_never_updates_existing_row()
    {
        await using var factory = TsumugiDbContextFactory.CreateInMemory();
        var ctx = factory.CreateContext();
        var repo = new ClaimCsvExportRepository(ctx);

        var export = new ClaimCsvExport
        {
            Id = Guid.NewGuid(),
            ClaimBatchId = Guid.NewGuid(),
            ProcessingMonth = new ProcessingMonth(2026, 8),
            CsvSpecVersion = "r7-10",
            RewardMasterVersion = "r6-2024",
            ServiceCodeVersion = "r8-06",
            Sha256 = "0000000000000000000000000000000000000000000000000000000000000000",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "tester",
        };

        await repo.AppendAsync(export, CancellationToken.None);

        var listed = await repo.ListByBatchAsync(export.ClaimBatchId, CancellationToken.None);
        listed.Should().ContainSingle().Which.Sha256.Should().Be(export.Sha256);
    }

    [Fact]
    public async Task AppendAsync_allows_multiple_exports_for_same_batch()
    {
        await using var factory = TsumugiDbContextFactory.CreateInMemory();
        var ctx = factory.CreateContext();
        var repo = new ClaimCsvExportRepository(ctx);

        var batchId = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            await repo.AppendAsync(new ClaimCsvExport
            {
                Id = Guid.NewGuid(),
                ClaimBatchId = batchId,
                ProcessingMonth = new ProcessingMonth(2026, 8),
                CsvSpecVersion = "r7-10",
                RewardMasterVersion = "r6-2024",
                ServiceCodeVersion = "r8-06",
                Sha256 = new string((char)('a' + i), 64),
                CreatedAtUtc = DateTime.UtcNow.AddSeconds(i),
                CreatedBy = "tester",
            }, CancellationToken.None);
        }

        (await repo.ListByBatchAsync(batchId, CancellationToken.None)).Should().HaveCount(3);
    }
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimCsvExportRepositoryTests
```
Expected: FAIL（`ClaimCsvExport` 未定義）

- [ ] **Step 3: Domain entity を書く**

`src/Tsumugi.Domain/Entities/ClaimCsvExport.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>国保連CSV出力履歴（append-only）。訂正・再出力は新規レコードで表現。</summary>
public sealed record ClaimCsvExport : Entity
{
    public required Guid ClaimBatchId { get; init; }
    public required ProcessingMonth ProcessingMonth { get; init; }
    public required string CsvSpecVersion { get; init; }
    public required string RewardMasterVersion { get; init; }
    public required string ServiceCodeVersion { get; init; }
    public required string Sha256 { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required string CreatedBy { get; init; }
}
```

- [ ] **Step 4: Application interface を書く**

`src/Tsumugi.Application/Abstractions/IClaimCsvExportRepository.cs`:

```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IClaimCsvExportRepository
{
    Task AppendAsync(ClaimCsvExport export, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClaimCsvExport>> ListByBatchAsync(Guid claimBatchId, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: EF Core Configuration を書く**

`src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimCsvExportConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

internal sealed class ClaimCsvExportConfiguration : IEntityTypeConfiguration<ClaimCsvExport>
{
    public void Configure(EntityTypeBuilder<ClaimCsvExport> builder)
    {
        builder.ToTable("ClaimCsvExports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClaimBatchId).IsRequired();
        builder.Property(x => x.ProcessingMonth)
            .HasConversion(v => v.ToInt(), v => ProcessingMonth.FromInt(v))
            .IsRequired();
        builder.Property(x => x.CsvSpecVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RewardMasterVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ServiceCodeVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.ClaimBatchId);
        builder.HasIndex(x => new { x.ClaimBatchId, x.CreatedAtUtc });

        builder.HasOne<ClaimBatch>()
            .WithMany()
            .HasForeignKey(x => x.ClaimBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 6: Repository を書く**

`src/Tsumugi.Infrastructure/Persistence/ClaimCsvExportRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class ClaimCsvExportRepository : IClaimCsvExportRepository
{
    private readonly TsumugiDbContext _dbContext;

    public ClaimCsvExportRepository(TsumugiDbContext dbContext) => _dbContext = dbContext;

    public async Task AppendAsync(ClaimCsvExport export, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(export);
        await _dbContext.ClaimCsvExports.AddAsync(export, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClaimCsvExport>> ListByBatchAsync(
        Guid claimBatchId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ClaimCsvExports
            .AsNoTracking()
            .Where(x => x.ClaimBatchId == claimBatchId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 7: `TsumugiDbContext` に `DbSet<ClaimCsvExport>` と Configuration 登録を追加**

`src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs` に：

```csharp
public DbSet<ClaimCsvExport> ClaimCsvExports => Set<ClaimCsvExport>();
```

`OnModelCreating` に：

```csharp
modelBuilder.ApplyConfiguration(new ClaimCsvExportConfiguration());
```

- [ ] **Step 8: マイグレーション生成**

```
dotnet ef migrations add Phase33ClaimCsvExport --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```
生成された `.cs` を確認し、`ClaimCsvExports` テーブル定義と FK 制約が入っていることを確認。

- [ ] **Step 9: テスト実行 → PASS を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~ClaimCsvExportRepositoryTests
```
Expected: PASS（2 テスト）

- [ ] **Step 10: コミット**

```
git add src/Tsumugi.Domain/Entities/ClaimCsvExport.cs \
        src/Tsumugi.Application/Abstractions/IClaimCsvExportRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimCsvExportConfiguration.cs \
        src/Tsumugi.Infrastructure/Persistence/ClaimCsvExportRepository.cs \
        src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs \
        src/Tsumugi.Infrastructure/Migrations/*Phase33ClaimCsvExport* \
        tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportRepositoryTests.cs
git commit -m "feat(phase3-3/task 1): add ClaimCsvExport append-only entity and repository"
```

---

## Task 2: `CsvCellEncoder`（byte 幅・quote・CP932 fail-close）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/CsvCell.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/CsvCellEncoder.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/CsvCellEncoderTests.cs`

**Interfaces:**
- Consumes: `CsvFieldSpecification`（既存 `Tsumugi.Infrastructure.Csv.Specifications.Models`）
- Produces:
  - `CsvCell(FieldId: string, Raw: string)` — record struct
  - `CsvCellEncoder.EncodeCell(CsvCell, CsvFieldSpecification) → ReadOnlyMemory<byte>`
  - `CsvCellEncoder.EncodeRow(IReadOnlyList<CsvCell>, IReadOnlyList<CsvFieldSpecification>) → ReadOnlyMemory<byte>`
  - `CsvEncodingException(FieldId, Reason, Detail)` — `Reason ∈ { OverByteWidth, NonRepresentableCharacter, NulCharacter, MissingRequired, UnknownCode }`

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/CsvCellEncoderTests.cs`:

```csharp
using System.Text;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications.Models;
using Tsumugi.Infrastructure.Csv.Writer;
using Xunit;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer;

public sealed class CsvCellEncoderTests
{
    static CsvCellEncoderTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static CsvFieldSpecification NumericField(string fieldId, int maxBytes) =>
        new(fieldId, "numeric", maxBytes, isRequired: false, allowedCodes: null, quotingRule: "unquoted");

    private static CsvFieldSpecification TextField(string fieldId, int maxBytes) =>
        new(fieldId, "text", maxBytes, isRequired: false, allowedCodes: null, quotingRule: "quoted");

    [Fact]
    public void EncodeCell_pads_numeric_within_byte_width()
    {
        var spec = NumericField("provider:J111:01:001", maxBytes: 4);
        var cell = new CsvCell(spec.FieldId, "12");

        var bytes = CsvCellEncoder.EncodeCell(cell, spec).ToArray();

        Encoding.GetEncoding(932).GetString(bytes).Should().Be("12");
    }

    [Fact]
    public void EncodeCell_quotes_text_and_escapes_embedded_quotes()
    {
        var spec = TextField("provider:J121:01:015", maxBytes: 40);
        var cell = new CsvCell(spec.FieldId, "山田\"太郎\"");

        var bytes = CsvCellEncoder.EncodeCell(cell, spec).ToArray();

        Encoding.GetEncoding(932).GetString(bytes)
            .Should().Be("\"山田\"\"太郎\"\"\"");
    }

    [Fact]
    public void EncodeCell_fails_when_over_byte_width()
    {
        var spec = TextField("provider:J121:01:015", maxBytes: 4);
        var cell = new CsvCell(spec.FieldId, "山田太郎");

        var act = () => CsvCellEncoder.EncodeCell(cell, spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.OverByteWidth);
    }

    [Fact]
    public void EncodeCell_fails_on_non_cp932_character()
    {
        var spec = TextField("provider:J121:01:015", maxBytes: 40);
        var cell = new CsvCell(spec.FieldId, "🍣");

        var act = () => CsvCellEncoder.EncodeCell(cell, spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.NonRepresentableCharacter);
    }

    [Fact]
    public void EncodeCell_fails_on_nul_character()
    {
        var spec = TextField("provider:J121:01:015", maxBytes: 40);
        var cell = new CsvCell(spec.FieldId, "山田\0太郎");

        var act = () => CsvCellEncoder.EncodeCell(cell, spec);

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.NulCharacter);
    }

    [Fact]
    public void EncodeRow_joins_cells_with_comma()
    {
        var specs = new[]
        {
            NumericField("provider:J111:01:001", 4),
            TextField("provider:J111:01:002", 20),
            NumericField("provider:J111:01:003", 6),
        };
        var cells = new[]
        {
            new CsvCell(specs[0].FieldId, "1"),
            new CsvCell(specs[1].FieldId, "テスト"),
            new CsvCell(specs[2].FieldId, "10"),
        };

        var bytes = CsvCellEncoder.EncodeRow(cells, specs).ToArray();

        Encoding.GetEncoding(932).GetString(bytes)
            .Should().Be("1,\"テスト\",10");
    }
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~CsvCellEncoderTests
```
Expected: FAIL（`CsvCell`, `CsvCellEncoder`, `CsvEncodingException` 未定義）

- [ ] **Step 3: `CsvCell` record を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/CsvCell.cs`:

```csharp
namespace Tsumugi.Infrastructure.Csv.Writer;

public readonly record struct CsvCell(string FieldId, string Raw);
```

- [ ] **Step 4: 例外を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/CsvEncodingException.cs`:

```csharp
namespace Tsumugi.Infrastructure.Csv.Writer;

public enum CsvEncodingReason
{
    OverByteWidth,
    NonRepresentableCharacter,
    NulCharacter,
    MissingRequired,
    UnknownCode,
    QuoteRuleViolation,
}

public sealed class CsvEncodingException : Exception
{
    public CsvEncodingException(string fieldId, CsvEncodingReason reason, string detail)
        : base($"CSV encoding failed: field={fieldId}, reason={reason}, detail={detail}")
    {
        FieldId = fieldId;
        Reason = reason;
        Detail = detail;
    }

    public string FieldId { get; }
    public CsvEncodingReason Reason { get; }
    public string Detail { get; }
}
```

- [ ] **Step 5: `CsvCellEncoder` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/CsvCellEncoder.cs`:

```csharp
using System.Text;
using Tsumugi.Infrastructure.Csv.Specifications.Models;

namespace Tsumugi.Infrastructure.Csv.Writer;

public static class CsvCellEncoder
{
    private const int Cp932 = 932;
    private static readonly Encoding Encoding;

    static CsvCellEncoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding = Encoding.GetEncoding(
            Cp932,
            new EncoderExceptionFallback(),
            new DecoderExceptionFallback());
    }

    public static ReadOnlyMemory<byte> EncodeCell(CsvCell cell, CsvFieldSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!string.Equals(cell.FieldId, spec.FieldId, StringComparison.Ordinal))
            throw new CsvEncodingException(cell.FieldId, CsvEncodingReason.QuoteRuleViolation,
                $"Cell fieldId '{cell.FieldId}' does not match spec '{spec.FieldId}'.");

        if (cell.Raw.Contains('\0'))
            throw new CsvEncodingException(cell.FieldId, CsvEncodingReason.NulCharacter, "raw contains NUL");

        var quoted = ApplyQuoting(cell.Raw, spec);
        byte[] bytes;
        try
        {
            bytes = Encoding.GetBytes(quoted);
        }
        catch (EncoderFallbackException ex)
        {
            throw new CsvEncodingException(cell.FieldId, CsvEncodingReason.NonRepresentableCharacter,
                $"character '{ex.CharUnknown}' is not representable in CP932");
        }

        if (bytes.Length > spec.MaxByteLength)
            throw new CsvEncodingException(cell.FieldId, CsvEncodingReason.OverByteWidth,
                $"encoded byte length {bytes.Length} exceeds max {spec.MaxByteLength}");

        return bytes;
    }

    public static ReadOnlyMemory<byte> EncodeRow(
        IReadOnlyList<CsvCell> cells,
        IReadOnlyList<CsvFieldSpecification> specs)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(specs);
        if (cells.Count != specs.Count)
            throw new ArgumentException(
                $"Cell count {cells.Count} does not match spec count {specs.Count}.", nameof(cells));

        using var buffer = new MemoryStream();
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0) buffer.WriteByte((byte)',');
            var cellBytes = EncodeCell(cells[i], specs[i]);
            buffer.Write(cellBytes.Span);
        }
        return buffer.ToArray();
    }

    private static string ApplyQuoting(string raw, CsvFieldSpecification spec)
    {
        return spec.QuotingRule switch
        {
            "quoted" => "\"" + raw.Replace("\"", "\"\"") + "\"",
            "unquoted" => raw,
            _ => throw new CsvEncodingException(spec.FieldId, CsvEncodingReason.QuoteRuleViolation,
                $"Unknown quoting rule '{spec.QuotingRule}'"),
        };
    }
}
```

- [ ] **Step 6: テスト実行 → PASS を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~CsvCellEncoderTests
```
Expected: PASS（6 テスト）

- [ ] **Step 7: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Writer/CsvCell.cs \
        src/Tsumugi.Infrastructure.Csv/Writer/CsvEncodingException.cs \
        src/Tsumugi.Infrastructure.Csv/Writer/CsvCellEncoder.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/CsvCellEncoderTests.cs
git commit -m "feat(phase3-3/task 2/AC3-7): add CsvCellEncoder with CP932 fail-close"
```

---

## Task 3: R8.6 サービスコード表 seed と `IServiceCodeCatalog`

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/ServiceCode/IServiceCodeCatalog.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/ServiceCode/JsonServiceCodeCatalog.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/ServiceCode/ServiceCodeEntry.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Specifications/service-code-r8-06.json`
- Create: `docs/decisions/0031-service-code-r8-06-service-code-table.md`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/ServiceCode/JsonServiceCodeCatalogTests.cs`

**Interfaces:**
- Produces:
  - `IServiceCodeCatalog.EffectiveFrom → ServiceMonth`
  - `IServiceCodeCatalog.Version → string`（例: "r8-06"）
  - `IServiceCodeCatalog.Get(string code) → ServiceCodeEntry`（未登録なら `CsvEncodingException(UnknownCode)`）
  - `ServiceCodeEntry(Code: string, Name: string, UnitCount: int)`
  - `JsonServiceCodeCatalog.LoadEmbedded() → IServiceCodeCatalog`

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/ServiceCode/JsonServiceCodeCatalogTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.ServiceCode;
using Tsumugi.Infrastructure.Csv.Writer;
using Xunit;

namespace Tsumugi.Infrastructure.Csv.Tests.ServiceCode;

public sealed class JsonServiceCodeCatalogTests
{
    [Fact]
    public void LoadEmbedded_exposes_r8_06_version_and_effectiveFrom_202606()
    {
        var catalog = JsonServiceCodeCatalog.LoadEmbedded();

        catalog.Version.Should().Be("r8-06");
        catalog.EffectiveFrom.Year.Should().Be(2026);
        catalog.EffectiveFrom.Month.Should().Be(6);
    }

    [Fact]
    public void Get_returns_entry_for_known_service_code()
    {
        var catalog = JsonServiceCodeCatalog.LoadEmbedded();

        var entry = catalog.Get("636211");

        entry.Code.Should().Be("636211");
        entry.Name.Should().NotBeNullOrEmpty();
        entry.UnitCount.Should().BePositive();
    }

    [Fact]
    public void Get_throws_CsvEncodingException_UnknownCode_for_unknown_service_code()
    {
        var catalog = JsonServiceCodeCatalog.LoadEmbedded();

        var act = () => catalog.Get("999999");

        act.Should().Throw<CsvEncodingException>()
            .Which.Reason.Should().Be(CsvEncodingReason.UnknownCode);
    }
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗**

Expected: FAIL（interface / class / JSON 未定義）

- [ ] **Step 3: `ServiceCodeEntry` と `IServiceCodeCatalog` を書く**

`src/Tsumugi.Infrastructure.Csv/ServiceCode/ServiceCodeEntry.cs`:

```csharp
namespace Tsumugi.Infrastructure.Csv.ServiceCode;

public sealed record ServiceCodeEntry(string Code, string Name, int UnitCount);
```

`src/Tsumugi.Infrastructure.Csv/ServiceCode/IServiceCodeCatalog.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Csv.ServiceCode;

public interface IServiceCodeCatalog
{
    string Version { get; }
    ServiceMonth EffectiveFrom { get; }
    ServiceCodeEntry Get(string code);
}
```

- [ ] **Step 4: seed JSON を書く**

`src/Tsumugi.Infrastructure.Csv/Specifications/service-code-r8-06.json`:

```json
{
  "schemaVersion": 1,
  "version": "r8-06",
  "effectiveFrom": "2026-06",
  "sourceDocumentId": "kokuho-service-code-r8-06",
  "sourceSha256": "PLACEHOLDER_SHA256_TO_BE_SET_BY_ADR_0031",
  "entries": [
    { "code": "636211", "name": "就労継続支援B型サービス費(I)1", "unitCount": 566 },
    { "code": "636212", "name": "就労継続支援B型サービス費(I)2", "unitCount": 517 }
  ]
}
```

（実装計画: ADR 0031 起票時に一次資料 URL・SHA256・完全な entries を確定する。placeholder は ADR 0031 で置換）

- [ ] **Step 5: `.csproj` に `EmbeddedResource` を追加**

`src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj` の既存 `<ItemGroup>` に：

```xml
<EmbeddedResource Include="Specifications/service-code-r8-06.json" />
```

- [ ] **Step 6: `JsonServiceCodeCatalog` を書く**

`src/Tsumugi.Infrastructure.Csv/ServiceCode/JsonServiceCodeCatalog.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Infrastructure.Csv.ServiceCode;

public sealed class JsonServiceCodeCatalog : IServiceCodeCatalog
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly ReadOnlyDictionary<string, ServiceCodeEntry> _entries;

    private JsonServiceCodeCatalog(
        string version,
        ServiceMonth effectiveFrom,
        IDictionary<string, ServiceCodeEntry> entries)
    {
        Version = version;
        EffectiveFrom = effectiveFrom;
        _entries = new ReadOnlyDictionary<string, ServiceCodeEntry>(entries);
    }

    public string Version { get; }
    public ServiceMonth EffectiveFrom { get; }

    public ServiceCodeEntry Get(string code)
    {
        if (!_entries.TryGetValue(code, out var entry))
            throw new CsvEncodingException(
                fieldId: "service-code",
                reason: CsvEncodingReason.UnknownCode,
                detail: $"service code '{code}' is not present in {Version}");
        return entry;
    }

    public static JsonServiceCodeCatalog LoadEmbedded()
    {
        var assembly = typeof(JsonServiceCodeCatalog).Assembly;
        using var stream = OpenEmbedded(assembly, "service-code-r8-06.json");
        var file = JsonSerializer.Deserialize<ServiceCodeFile>(stream, Options)
            ?? throw new InvalidDataException("service-code-r8-06.json is empty");

        var parts = file.EffectiveFrom.Split('-');
        var month = new ServiceMonth(int.Parse(parts[0]), int.Parse(parts[1]));
        var dict = file.Entries.ToDictionary(e => e.Code, e => new ServiceCodeEntry(e.Code, e.Name, e.UnitCount));
        return new JsonServiceCodeCatalog(file.Version, month, dict);
    }

    private static Stream OpenEmbedded(Assembly assembly, string name)
    {
        var resource = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith(name, StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(resource)!;
    }

    private sealed record ServiceCodeFile(
        int SchemaVersion,
        string Version,
        string EffectiveFrom,
        string SourceDocumentId,
        string SourceSha256,
        IReadOnlyList<ServiceCodeFileEntry> Entries);

    private sealed record ServiceCodeFileEntry(string Code, string Name, int UnitCount);
}
```

- [ ] **Step 7: ADR 0031 を書く**

`docs/decisions/0031-service-code-r8-06-service-code-table.md`:

```markdown
# 0031 R8.6 サービスコード表の取り込み

## 結論

令和8年6月のサービスコード表を `Tsumugi.Infrastructure.Csv` の seed JSON `service-code-r8-06.json` として同梱する。既存の R6 系 rewardmaster（ADR 0027/0028）とは独立に、`effectiveFrom = 2026-06` で管理する。

## 背景

`06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` §4.3 は令和8年6月資料で「請求書明細書・実績記録票は変更なし」とし、CSV 項目構造は令和7年10月事業所編を、サービスコード表は令和8年6月を組み合わせるよう規定している。

## 選択肢

1. rewardmaster テーブルに R8.6 サービスコード列を混在（統一）
2. 独立 seed（本ADR）
3. runtime 生成（DB を持たない）

## 決定

**選択肢2**。理由：
- rewardmaster と service code は寿命が異なる（サービスコード表は 3 年サイクル、報酬値は同じサイクルだがマスタ構造が異なる）
- CSV writer は `IServiceCodeCatalog` 越しに読み、Application/Domain から service code JSON を隠蔽できる
- 変更時の diff が独立し、`sourceSha256` の追跡が容易

## 影響

- 新規: `service-code-r8-06.json`（`schemaVersion`, `version`, `effectiveFrom`, `sourceDocumentId`, `sourceSha256`, `entries`）
- `JsonServiceCodeCatalog.LoadEmbedded()` が Csv アセンブリの埋め込みリソースを読む
- 一次資料 URL・取得日・SHA256 は本ADRに追記する（追記後の状態を "closed" とする）

## 一次資料

- URL: TBD（Phase 3-3 実装時に確定）
- 取得日: TBD
- SHA256: TBD
```

（TBD は Phase 3-3 実装中に埋める。空のままマージしない）

- [ ] **Step 8: テスト実行 → PASS を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~JsonServiceCodeCatalogTests
```
Expected: PASS（3 テスト）

- [ ] **Step 9: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/ServiceCode/*.cs \
        src/Tsumugi.Infrastructure.Csv/Specifications/service-code-r8-06.json \
        src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj \
        docs/decisions/0031-service-code-r8-06-service-code-table.md \
        tests/Tsumugi.Infrastructure.Csv.Tests/ServiceCode/JsonServiceCodeCatalogTests.cs
git commit -m "feat(phase3-3/task 3): add R8.6 service code catalog and ADR 0031"
```

---

## Task 4: `ClaimCsvWriter`（外側 3 レコード）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/CsvRow.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/ClaimCsvWriter.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/ClaimCsvWriterTests.cs`

**Interfaces:**
- Consumes: `CsvCellEncoder`, `CsvFieldSpecification`, `CsvSpecificationCatalog`
- Produces:
  - `CsvRow(RecordId: string, Cells: IReadOnlyList<CsvCell>)`
  - `ClaimCsvWriter.WriteAll(controlRow: CsvRow, dataRows: IReadOnlyList<CsvRow>, endRow: CsvRow, catalog: CsvSpecificationCatalog) → ReadOnlyMemory<byte>`
    - 各行の先頭に record type 番号（"1"/"2"/"3"）を付加
    - 行区切りは CRLF（`\r\n`）
    - 最終行末尾も CRLF（国保連仕様）

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/ClaimCsvWriterTests.cs`:

```csharp
using System.Text;
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;
using Xunit;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer;

public sealed class ClaimCsvWriterTests
{
    static ClaimCsvWriterTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void WriteAll_prefixes_records_with_type_number_and_terminates_with_crlf()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();
        var control = MakeMinimalControl(catalog);
        var data = new[] { MakeMinimalData(catalog) };
        var end = MakeMinimalEnd(catalog);

        var bytes = ClaimCsvWriter.WriteAll(control, data, end, catalog).ToArray();

        var text = Encoding.GetEncoding(932).GetString(bytes);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("1,");
        lines[1].Should().StartWith("2,");
        lines[2].Should().StartWith("3,");
        text.Should().EndWith("\r\n");
    }

    [Fact]
    public void WriteAll_requires_at_least_one_data_row()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();
        var control = MakeMinimalControl(catalog);
        var end = MakeMinimalEnd(catalog);

        var act = () => ClaimCsvWriter.WriteAll(control, Array.Empty<CsvRow>(), end, catalog);

        act.Should().Throw<InvalidOperationException>();
    }

    // MakeMinimalControl / MakeMinimalData / MakeMinimalEnd はテスト用に
    // catalog の record 仕様に合わせて最小限のセル値を組む helper。
    // Task 6 の Sources DTO が完成するまでは stub cell 値でよい。
    private static CsvRow MakeMinimalControl(CsvSpecificationCatalog catalog) { /* 実装省略 */ throw new NotImplementedException(); }
    private static CsvRow MakeMinimalData(CsvSpecificationCatalog catalog) { /* 実装省略 */ throw new NotImplementedException(); }
    private static CsvRow MakeMinimalEnd(CsvSpecificationCatalog catalog) { /* 実装省略 */ throw new NotImplementedException(); }
}
```

**注記**: `MakeMinimalControl` 系ヘルパーは、control/data/end それぞれの record spec が catalog に登録済みであることを前提に、各フィールドを `catalog.FieldByFieldId` から引いて必須項目に定数を入れる。テスト用の control record id は `"provider:control"`、end record id は `"provider:end"`、data record id は `"provider:J111:01"`。

- [ ] **Step 2: テスト実行 → 期待通り失敗**

Expected: FAIL

- [ ] **Step 3: `CsvRow` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/CsvRow.cs`:

```csharp
namespace Tsumugi.Infrastructure.Csv.Writer;

public sealed record CsvRow(string RecordId, IReadOnlyList<CsvCell> Cells);
```

- [ ] **Step 4: `ClaimCsvWriter` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/ClaimCsvWriter.cs`:

```csharp
using System.Text;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Writer;

public static class ClaimCsvWriter
{
    private static readonly byte[] Crlf = { (byte)'\r', (byte)'\n' };

    public static ReadOnlyMemory<byte> WriteAll(
        CsvRow controlRow,
        IReadOnlyList<CsvRow> dataRows,
        CsvRow endRow,
        CsvSpecificationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(controlRow);
        ArgumentNullException.ThrowIfNull(dataRows);
        ArgumentNullException.ThrowIfNull(endRow);
        ArgumentNullException.ThrowIfNull(catalog);
        if (dataRows.Count == 0)
            throw new InvalidOperationException("At least one data row is required.");

        using var buffer = new MemoryStream();
        WriteRow(buffer, controlRow, catalog, recordTypeNumber: 1);
        foreach (var row in dataRows) WriteRow(buffer, row, catalog, recordTypeNumber: 2);
        WriteRow(buffer, endRow, catalog, recordTypeNumber: 3);
        return buffer.ToArray();
    }

    private static void WriteRow(
        Stream sink,
        CsvRow row,
        CsvSpecificationCatalog catalog,
        int recordTypeNumber)
    {
        // 先頭に record type number + カンマを付ける
        sink.WriteByte((byte)('0' + recordTypeNumber));
        sink.WriteByte((byte)',');

        var specs = row.Cells
            .Select(cell => catalog.FieldByFieldId[cell.FieldId])
            .ToArray();
        var rowBytes = CsvCellEncoder.EncodeRow(row.Cells, specs);
        sink.Write(rowBytes.Span);
        sink.Write(Crlf);
    }
}
```

- [ ] **Step 5: テストの `MakeMinimalXxx` ヘルパーを埋める**

catalog から control/data/end の record spec を引き、各 field の最小定数値（`"0"` for numeric, `""` for text）で cells を組む。テストが実行可能な状態にする。

- [ ] **Step 6: テスト実行 → PASS を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~ClaimCsvWriterTests
```
Expected: PASS（2 テスト）

- [ ] **Step 7: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Writer/CsvRow.cs \
        src/Tsumugi.Infrastructure.Csv/Writer/ClaimCsvWriter.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/ClaimCsvWriterTests.cs
git commit -m "feat(phase3-3/task 4/AC3-7): add ClaimCsvWriter with outer 3-record frame"
```

---

## Task 5: `RecordIdRouter`（ClaimBatch → recordType 9 種の分配）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/RecordIdRouter.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/Sources/ClaimCsvSourceBundle.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/RecordIdRouterTests.cs`

**Interfaces:**
- Consumes: `ClaimBatch`（Domain）、確定 snapshot の payload path
- Produces:
  - `ClaimCsvSourceBundle` — 9 種 source DTO をまとめる bag（各 optional / list）
  - `RecordIdRouter.Route(ClaimBatchAggregate batch, ProcessingMonth processingMonth) → ClaimCsvSourceBundle`
  - 出力順は仕様: J111:01 → J111:02 → J121:01 → J121:02 → J121:03 → J121:04 → J121:05 → J611:01 → J611:02

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/RecordIdRouterTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.Writer;
using Xunit;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer;

public sealed class RecordIdRouterTests
{
    [Fact]
    public void Route_returns_bundle_with_all_expected_record_types_in_official_order()
    {
        var batch = ClaimBatchFakes.MinimalFinalized();
        var processingMonth = new ProcessingMonth(2026, 8);

        var bundle = RecordIdRouter.Route(batch, processingMonth);

        bundle.OrderedRecordIds.Should().ContainInOrder(
            "provider:J111:01",
            "provider:J111:02",
            "provider:J121:01",
            "provider:J121:02",
            "provider:J121:03",
            "provider:J121:04",
            "provider:J121:05",
            "provider:J611:01",
            "provider:J611:02");
    }

    [Fact]
    public void Route_sorts_J121_03_service_lines_by_service_date_and_service_code()
    {
        var batch = ClaimBatchFakes.WithMultipleServiceLines();
        var processingMonth = new ProcessingMonth(2026, 8);

        var bundle = RecordIdRouter.Route(batch, processingMonth);

        bundle.StatementLines.Select(x => (x.ServiceDate, x.ServiceCode))
            .Should().BeInAscendingOrder();
    }
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗**

Expected: FAIL

- [ ] **Step 3: `ClaimCsvSourceBundle` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/Sources/ClaimCsvSourceBundle.cs`:

```csharp
namespace Tsumugi.Infrastructure.Csv.Writer.Sources;

public sealed record ClaimCsvSourceBundle(
    ClaimInvoiceHeaderSource InvoiceHeader,
    ClaimInvoiceTotalsSource InvoiceTotals,
    IReadOnlyList<ClaimContractSource> Contracts,
    IReadOnlyList<ClaimStatementSummarySource> StatementSummaries,
    IReadOnlyList<ClaimStatementLineSource> StatementLines,
    IReadOnlyList<ClaimStatementAggregateSource> StatementAggregates,
    IReadOnlyList<ClaimTransitionalSource> Transitionals,
    IReadOnlyList<UpperLimitManagementHeaderSource> UpperLimitHeaders,
    IReadOnlyList<UpperLimitManagementLineSource> UpperLimitLines)
{
    public IReadOnlyList<string> OrderedRecordIds { get; } = new[]
    {
        "provider:J111:01",
        "provider:J111:02",
        "provider:J121:01",
        "provider:J121:02",
        "provider:J121:03",
        "provider:J121:04",
        "provider:J121:05",
        "provider:J611:01",
        "provider:J611:02",
    };
}
```

- [ ] **Step 4: 9 種 Source DTO を書く（stub でよい）**

`src/Tsumugi.Infrastructure.Csv/Writer/Sources/ClaimInvoiceHeaderSource.cs`:

```csharp
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Csv.Writer.Sources;

public sealed record ClaimInvoiceHeaderSource(
    ProcessingMonth ProcessingMonth,
    string OfficeCode,
    string PrefectureCode,
    string InsurerNumber,
    string OfficeName,
    int TotalRecipients,
    string ClaimBatchIdReference);
```

（残り 8 種も同様の空 record として作成。各の詳細プロパティは Task 7-9 で埋める）

- [ ] **Step 5: `RecordIdRouter` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/RecordIdRouter.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.Writer.Sources;

namespace Tsumugi.Infrastructure.Csv.Writer;

public static class RecordIdRouter
{
    public static ClaimCsvSourceBundle Route(
        ClaimBatchAggregate batch,
        ProcessingMonth processingMonth)
    {
        ArgumentNullException.ThrowIfNull(batch);
        // Task 7-9 が埋めるまで stub。ここでは shape のみ保証。
        var invoiceHeader = new ClaimInvoiceHeaderSource(
            processingMonth,
            batch.OfficeCode,
            batch.PrefectureCode,
            batch.InsurerNumber,
            batch.OfficeName,
            batch.RecipientCount,
            batch.Id.ToString("N"));

        var invoiceTotals = new ClaimInvoiceTotalsSource(
            batch.TotalUnits, batch.TotalCostYen, batch.TotalBenefitYen, batch.TotalBurdenYen);

        var contracts = batch.Details
            .OrderBy(d => d.RecipientCode, StringComparer.Ordinal)
            .Select(d => new ClaimContractSource(/* ... 各プロパティ ... */))
            .ToArray();

        var statementSummaries = batch.Details
            .OrderBy(d => d.RecipientCode, StringComparer.Ordinal)
            .Select(d => new ClaimStatementSummarySource(/* ... */))
            .ToArray();

        var statementLines = batch.Details
            .SelectMany(d => d.ServiceLines)
            .OrderBy(l => l.ServiceDate)
            .ThenBy(l => l.ServiceCode, StringComparer.Ordinal)
            .Select(l => new ClaimStatementLineSource(/* ... */))
            .ToArray();

        // 以下同様に 4 種
        // ...

        return new ClaimCsvSourceBundle(
            invoiceHeader, invoiceTotals, contracts, statementSummaries,
            statementLines, /* aggregates */, /* transitionals */,
            /* upperLimitHeaders */, /* upperLimitLines */);
    }
}
```

**注記**: `ClaimBatchAggregate` の実プロパティ名は `src/Tsumugi.Application/Abstractions/ClaimBatchAggregate.cs` に従う。上記コード内 `batch.OfficeCode` 等は既存プロパティに対応させる（存在しないものは Snapshot v2 payload から解決）。

- [ ] **Step 6: テスト fake helper を書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/ClaimBatchFakes.cs`:

```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer;

internal static class ClaimBatchFakes
{
    public static ClaimBatchAggregate MinimalFinalized() =>
        new(/* ClaimBatch record with New kind, 1 recipient, 1 service line */);

    public static ClaimBatchAggregate WithMultipleServiceLines() =>
        new(/* 2 recipients × 3 service lines, dates: 2026-07-01/05/10, codes: 636211/636212 */);
}
```

- [ ] **Step 7: テスト実行 → PASS を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter FullyQualifiedName~RecordIdRouterTests
```
Expected: PASS（2 テスト）

- [ ] **Step 8: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Writer/RecordIdRouter.cs \
        src/Tsumugi.Infrastructure.Csv/Writer/Sources/*.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/RecordIdRouterTests.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/ClaimBatchFakes.cs
git commit -m "feat(phase3-3/task 5/AC3-7): route ClaimBatch to 9 recordTypes in official order"
```

---

## Task 6: `IProviderRecordBuilder<T>` 抽象と共通基底

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/IProviderRecordBuilder.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ProviderRecordBuilderBase.cs`

**Interfaces:**
- Produces:
  - `IProviderRecordBuilder<T>.RecordId → string`
  - `IProviderRecordBuilder<T>.Build(T source, CsvSpecificationCatalog catalog) → IReadOnlyList<CsvRow>`
  - `ProviderRecordBuilderBase<T>` — 共通ヘルパー（フィールド順の解決、`CsvCell` 生成、numeric/text 変換）

- [ ] **Step 1: `IProviderRecordBuilder<T>` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/IProviderRecordBuilder.cs`:

```csharp
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Writer;

public interface IProviderRecordBuilder<in T>
{
    string RecordId { get; }
    IReadOnlyList<CsvRow> Build(T source, CsvSpecificationCatalog catalog);
}
```

- [ ] **Step 2: `ProviderRecordBuilderBase<T>` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/Builders/ProviderRecordBuilderBase.cs`:

```csharp
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Specifications.Models;

namespace Tsumugi.Infrastructure.Csv.Writer.Builders;

public abstract class ProviderRecordBuilderBase<T> : IProviderRecordBuilder<T>
{
    public abstract string RecordId { get; }

    public abstract IReadOnlyList<CsvRow> Build(T source, CsvSpecificationCatalog catalog);

    protected CsvRow BuildRow(
        CsvSpecificationCatalog catalog,
        params (string FieldId, string Raw)[] cells)
    {
        var record = catalog.RecordByRecordId[RecordId];
        if (cells.Length != record.Fields.Count)
            throw new InvalidOperationException(
                $"Builder {RecordId} produced {cells.Length} cells but spec expects {record.Fields.Count}.");

        var ordered = record.Fields
            .Select(spec =>
            {
                var match = cells.FirstOrDefault(c => c.FieldId == spec.FieldId);
                if (match.FieldId is null)
                    throw new InvalidOperationException(
                        $"Builder {RecordId} missing cell for field {spec.FieldId}");
                return new CsvCell(match.FieldId, match.Raw);
            })
            .ToArray();

        return new CsvRow(RecordId, ordered);
    }

    protected static string FormatYyyymm(Tsumugi.Domain.ValueObjects.ServiceMonth month)
        => $"{month.Year:D4}{month.Month:D2}";

    protected static string FormatYyyymm(Tsumugi.Domain.ValueObjects.ProcessingMonth month)
        => $"{month.Year:D4}{month.Month:D2}";

    protected static string FormatYen(int amount) => amount.ToString();
    protected static string FormatUnits(int units) => units.ToString();
    protected static string FormatOptionalYen(int? amount) => amount?.ToString() ?? string.Empty;
}
```

- [ ] **Step 3: コンパイル確認**

```
dotnet build src/Tsumugi.Infrastructure.Csv
```
Expected: 警告ゼロで成功

- [ ] **Step 4: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Writer/IProviderRecordBuilder.cs \
        src/Tsumugi.Infrastructure.Csv/Writer/Builders/ProviderRecordBuilderBase.cs
git commit -m "feat(phase3-3/task 6): add IProviderRecordBuilder abstraction and base"
```

---

## Task 7: J111 builders（請求書 総括・集計）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceHeaderBuilder.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceTotalsBuilder.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceHeaderBuilderTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceTotalsBuilderTests.cs`

**Interfaces:**
- Consumes: `ClaimInvoiceHeaderSource`, `ClaimInvoiceTotalsSource`, `CsvSpecificationCatalog`
- Produces:
  - `ClaimInvoiceHeaderBuilder.RecordId = "provider:J111:01"`
  - `ClaimInvoiceTotalsBuilder.RecordId = "provider:J111:02"`

- [ ] **Step 1: J111:01 の失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceHeaderBuilderTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer.Builders;
using Tsumugi.Infrastructure.Csv.Writer.Sources;
using Xunit;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer.Builders;

public sealed class ClaimInvoiceHeaderBuilderTests
{
    [Fact]
    public void Build_maps_processing_month_and_office_metadata_to_J111_01_fields()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();
        var source = new ClaimInvoiceHeaderSource(
            ProcessingMonth: new ProcessingMonth(2026, 8),
            OfficeCode: "1112223333",
            PrefectureCode: "13",
            InsurerNumber: "13100",
            OfficeName: "つむぎ事業所",
            TotalRecipients: 3,
            ClaimBatchIdReference: "abc");

        var rows = new ClaimInvoiceHeaderBuilder().Build(source, catalog);

        rows.Should().ContainSingle();
        var row = rows[0];
        row.RecordId.Should().Be("provider:J111:01");
        row.Cells.Should().Contain(c => c.FieldId == "provider:J111:01:002" && c.Raw == "202608");
        row.Cells.Should().Contain(c => c.FieldId == "provider:J111:01:003" && c.Raw == "1112223333");
        row.Cells.Should().Contain(c => c.FieldId == "provider:J111:01:005" && c.Raw == "つむぎ事業所");
    }
}
```

**注記**: `provider:J111:01:002` = 処理対象年月、`:003` = 事業所番号、`:005` = 事業所名（記法は provider-claim-r7-10.json の fieldId 順に対応、実装時に JSON を参照して正確な fieldId に置き換える）。

- [ ] **Step 2: テスト実行 → 期待通り失敗**

- [ ] **Step 3: `ClaimInvoiceHeaderBuilder` を書く**

`src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceHeaderBuilder.cs`:

```csharp
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer.Sources;

namespace Tsumugi.Infrastructure.Csv.Writer.Builders;

public sealed class ClaimInvoiceHeaderBuilder : ProviderRecordBuilderBase<ClaimInvoiceHeaderSource>
{
    public override string RecordId => "provider:J111:01";

    public override IReadOnlyList<CsvRow> Build(
        ClaimInvoiceHeaderSource source, CsvSpecificationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(catalog);

        var row = BuildRow(catalog,
            ("provider:J111:01:001", "1"), // レコード種別コード
            ("provider:J111:01:002", FormatYyyymm(source.ProcessingMonth)),
            ("provider:J111:01:003", source.OfficeCode),
            ("provider:J111:01:004", source.PrefectureCode),
            ("provider:J111:01:005", source.OfficeName),
            ("provider:J111:01:006", source.InsurerNumber),
            ("provider:J111:01:007", source.TotalRecipients.ToString()));
        // 追加フィールドは field-mapping-r7-10.json を参照して埋める

        return new[] { row };
    }
}
```

- [ ] **Step 4: テスト実行 → PASS を確認**

- [ ] **Step 5: J111:02 の失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceTotalsBuilderTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer.Builders;
using Tsumugi.Infrastructure.Csv.Writer.Sources;
using Xunit;

namespace Tsumugi.Infrastructure.Csv.Tests.Writer.Builders;

public sealed class ClaimInvoiceTotalsBuilderTests
{
    [Fact]
    public void Build_maps_total_units_and_amounts_to_J111_02()
    {
        var catalog = CsvSpecificationLoader.LoadEmbedded();
        var source = new ClaimInvoiceTotalsSource(
            TotalUnits: 12345,
            TotalCostYen: 145678,
            TotalBenefitYen: 130000,
            TotalBurdenYen: 15678);

        var rows = new ClaimInvoiceTotalsBuilder().Build(source, catalog);

        rows.Should().ContainSingle();
        var cells = rows[0].Cells.ToDictionary(c => c.FieldId, c => c.Raw);
        cells.Should().ContainKey("provider:J111:02:012").WhoseValue.Should().Be("12345");
        cells.Should().ContainKey("provider:J111:02:013").WhoseValue.Should().Be("145678");
    }
}
```

- [ ] **Step 6: `ClaimInvoiceTotalsBuilder` を書く**

```csharp
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer.Sources;

namespace Tsumugi.Infrastructure.Csv.Writer.Builders;

public sealed class ClaimInvoiceTotalsBuilder : ProviderRecordBuilderBase<ClaimInvoiceTotalsSource>
{
    public override string RecordId => "provider:J111:02";

    public override IReadOnlyList<CsvRow> Build(
        ClaimInvoiceTotalsSource source, CsvSpecificationCatalog catalog)
    {
        var row = BuildRow(catalog,
            ("provider:J111:02:001", "2"),
            ("provider:J111:02:012", FormatUnits(source.TotalUnits)),
            ("provider:J111:02:013", FormatYen(source.TotalCostYen)),
            ("provider:J111:02:014", FormatYen(source.TotalBenefitYen)),
            ("provider:J111:02:015", FormatYen(source.TotalBurdenYen)));
        // 追加フィールドは JSON に従い埋める

        return new[] { row };
    }
}
```

- [ ] **Step 7: 全 J111 builder テスト実行 → PASS を確認**

- [ ] **Step 8: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceHeaderBuilder.cs \
        src/Tsumugi.Infrastructure.Csv/Writer/Builders/ClaimInvoiceTotalsBuilder.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceHeaderBuilderTests.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/ClaimInvoiceTotalsBuilderTests.cs
git commit -m "feat(phase3-3/task 7/AC3-7): add J111 (invoice header/totals) builders"
```

---

## Task 8: J121 builders（明細書 5 レコード種別）

**Files:**
- Create: 5 個の builder（`ClaimContractBuilder`, `ClaimStatementSummaryBuilder`, `ClaimStatementLineBuilder`, `ClaimStatementAggregateBuilder`, `ClaimTransitionalBuilder`）
- Test: 各 builder に対応する `*Tests.cs` 5 個

**Interfaces:**
- Consumes: J121 系 5 種 Source DTO
- Produces:
  - `ClaimContractBuilder.RecordId = "provider:J121:01"` — 契約情報
  - `ClaimStatementSummaryBuilder.RecordId = "provider:J121:02"` — 明細書サマリ
  - `ClaimStatementLineBuilder.RecordId = "provider:J121:03"` — サービス単位数明細（複数行を返す）
  - `ClaimStatementAggregateBuilder.RecordId = "provider:J121:04"` — 集計・例外利用日
  - `ClaimTransitionalBuilder.RecordId = "provider:J121:05"` — 経過措置

- [ ] **Step 1: J121:01 失敗テスト → 実装 → PASS → コミット**

Task 7 と同じ TDD 手順で `ClaimContractBuilder` を実装。field-mapping-r7-10.json の `provider:J121:01:*` を全て埋める。テストは主要フィールド（`受給者証番号` `契約支給量` `契約開始日` `契約事業所番号`）の値を assert。

- [ ] **Step 2: J121:02 失敗テスト → 実装 → PASS → コミット**

`ClaimStatementSummaryBuilder` を実装。fields は `合計単位数` `保護者負担額` `合計費用額` `合計給付率` 等。テストは Task 7 と同構造。

- [ ] **Step 3: J121:03 失敗テスト → 実装 → PASS → コミット**

`ClaimStatementLineBuilder` — 唯一 **複数行を返す** builder。`IReadOnlyList<ClaimStatementLineSource>` を入力に取り、行ごとに CsvRow を生成。テストは 3 明細行の生成順（`ServiceDate` → `ServiceCode` の昇順）と unit_count が spec の numeric width 内に収まることを確認。

```csharp
public override IReadOnlyList<CsvRow> Build(
    IReadOnlyList<ClaimStatementLineSource> sources, CsvSpecificationCatalog catalog)
{
    return sources
        .OrderBy(s => s.ServiceDate)
        .ThenBy(s => s.ServiceCode, StringComparer.Ordinal)
        .Select(s => BuildRow(catalog,
            ("provider:J121:03:001", "2"),
            ("provider:J121:03:xxx", s.ServiceCode),
            ("provider:J121:03:yyy", $"{s.ServiceDate:yyyyMMdd}"),
            ("provider:J121:03:zzz", s.UnitCount.ToString()),
            ("provider:J121:03:www", s.TotalUnits.ToString())))
        .ToArray();
}
```

（fieldId `xxx/yyy/zzz/www` は field-mapping-r7-10.json 参照）

- [ ] **Step 4: J121:04 失敗テスト → 実装 → PASS → コミット**

`ClaimStatementAggregateBuilder` — 集計 + 例外利用日 4 フィールド（`provider:J121:04:030-033`）を扱う。孤立 4 フィールドの cross-field 条件は Task 11 で `ClaimInputRequirementProvider` に登録済みの前提。builder は値をそのまま出力する。テストで 4 フィールドの optional 埋め方（値なしは空文字）を確認。

- [ ] **Step 5: J121:05 失敗テスト → 実装 → PASS → コミット**

`ClaimTransitionalBuilder` — 経過措置種別コード（`provider:J121:05:*`）を扱う。source が空リストなら 0 行を返す（optional record type）。

- [ ] **Step 6: 全 J121 テスト実行 → PASS を確認**

```
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests --filter "FullyQualifiedName~ClaimContractBuilder|FullyQualifiedName~ClaimStatementSummaryBuilder|FullyQualifiedName~ClaimStatementLineBuilder|FullyQualifiedName~ClaimStatementAggregateBuilder|FullyQualifiedName~ClaimTransitionalBuilder"
```

- [ ] **Step 7: まとめコミット（各 builder は Step 1-5 で個別にコミット済み）**

（追加のまとめコミットは不要。上記 5 コミットで完了）

---

## Task 9: J611 builders（上限管理結果票）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/Builders/UpperLimitManagementHeaderBuilder.cs`
- Create: `src/Tsumugi.Infrastructure.Csv/Writer/Builders/UpperLimitManagementLineBuilder.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/UpperLimitManagementHeaderBuilderTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/UpperLimitManagementLineBuilderTests.cs`

**Interfaces:**
- Consumes: `UpperLimitManagementHeaderSource`, `UpperLimitManagementLineSource`
- Produces:
  - `UpperLimitManagementHeaderBuilder.RecordId = "provider:J611:01"`
  - `UpperLimitManagementLineBuilder.RecordId = "provider:J611:02"`

- [ ] **Step 1: J611:01 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/UpperLimitManagementHeaderBuilderTests.cs`:

```csharp
[Fact]
public void Build_maps_upper_limit_management_office_and_month_to_J611_01()
{
    var catalog = CsvSpecificationLoader.LoadEmbedded();
    var source = new UpperLimitManagementHeaderSource(
        ProcessingMonth: new ProcessingMonth(2026, 8),
        OfficeCode: "1112223333",
        RecipientCode: "1234567890",
        ManagementResult: "1"); // 1=管理事業所自ら管理

    var rows = new UpperLimitManagementHeaderBuilder().Build(source, catalog);

    rows.Should().ContainSingle();
    rows[0].RecordId.Should().Be("provider:J611:01");
    rows[0].Cells.Should().Contain(c => c.FieldId.EndsWith(":002") && c.Raw == "202608");
}
```

- [ ] **Step 2: 実装 → PASS**

`UpperLimitManagementHeaderBuilder` を実装（Task 7 と同構造）。`provider:J611:01:*` の全フィールドを field-mapping-r7-10.json に従い埋める。

- [ ] **Step 3: J611:02 失敗テスト → 実装 → PASS**

`UpperLimitManagementLineBuilder` — 複数行を返す（管理下事業所の内訳が複数）。ソート順は `事業所番号` 昇順。

- [ ] **Step 4: 全 J611 テスト実行 → PASS を確認**

- [ ] **Step 5: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Writer/Builders/UpperLimitManagement*.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/Writer/Builders/UpperLimitManagement*Tests.cs
git commit -m "feat(phase3-3/task 9/AC3-7): add J611 upper limit management builders"
```

---

## Task 10: `ClaimInput` に provider:* 追加フィールド

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/ClaimInput.cs`
- Modify: `src/Tsumugi.Application/Dtos/ClaimInputDtos.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimInputConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/{ts}_Phase33ClaimInputProviderFields.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryProviderFieldsTests.cs`

**Interfaces:**
- Produces（`ClaimInput` に追加、全て nullable init）:
  - `ContractStartDate: DateOnly?`
  - `ContractInitialDate: DateOnly?`
  - `ContractSupplyQuantity: int?`（契約支給量）
  - `ProviderRemarks: string?`（事業者記入欄）
  - `TransitionalMeasureCategory: string?`
  - `ParentBurdenAmountYen: int?`
  - `MunicipalReviewNote: string?`
  - その他 provider:* 由来のフィールド（field-mapping-r7-10.json の "missing" ステータスから抽出）

**注記**: 追加すべきプロパティ集合は、Phase 3-3 実装時に `field-mapping-r7-10.json` の `status: "missing"` かつ `targetModel = "ClaimInput"` のエントリを列挙して確定する。以下は代表例。

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryProviderFieldsTests.cs`:

```csharp
[Fact]
public async Task Repository_persists_all_provider_star_fields_including_optionals()
{
    await using var factory = TsumugiDbContextFactory.CreateInMemory();
    var ctx = factory.CreateContext();
    var repo = new ClaimInputRepository(ctx);

    var input = ClaimInputBuilder.Minimal() with
    {
        ContractStartDate = new DateOnly(2026, 4, 1),
        ContractSupplyQuantity = 22,
        ProviderRemarks = "テスト",
        TransitionalMeasureCategory = "01",
        ParentBurdenAmountYen = 5000,
    };
    await repo.SaveAsync(input, CancellationToken.None);

    var loaded = await repo.GetLatestAsync(input.OfficeId, input.RecipientId, input.ServiceMonth, CancellationToken.None);
    loaded.Should().NotBeNull();
    loaded!.ContractSupplyQuantity.Should().Be(22);
    loaded.TransitionalMeasureCategory.Should().Be("01");
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗**

- [ ] **Step 3: `ClaimInput` 拡張**

`src/Tsumugi.Domain/Entities/ClaimInput.cs` に追加（既存 provider:* 対応の init プロパティに続けて）:

```csharp
public DateOnly? ContractStartDate { get; init; }
public DateOnly? ContractInitialDate { get; init; }
public int? ContractSupplyQuantity { get; init; }
public string? ProviderRemarks { get; init; }
public string? TransitionalMeasureCategory { get; init; }
public int? ParentBurdenAmountYen { get; init; }
public string? MunicipalReviewNote { get; init; }
// field-mapping-r7-10.json の missing 全件を列挙する
```

- [ ] **Step 4: DTO を追加**

`src/Tsumugi.Application/Dtos/ClaimInputDtos.cs` の DTO record に対応プロパティを追加。

- [ ] **Step 5: EF Core configuration に列を追加**

`src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimInputConfiguration.cs`:

```csharp
builder.Property(x => x.ContractStartDate);
builder.Property(x => x.ContractInitialDate);
builder.Property(x => x.ContractSupplyQuantity);
builder.Property(x => x.ProviderRemarks).HasMaxLength(200);
builder.Property(x => x.TransitionalMeasureCategory).HasMaxLength(4);
builder.Property(x => x.ParentBurdenAmountYen);
builder.Property(x => x.MunicipalReviewNote).HasMaxLength(200);
```

- [ ] **Step 6: マイグレーション生成**

```
dotnet ef migrations add Phase33ClaimInputProviderFields \
  --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
```
生成された migration を確認し、既存 row に対して null デフォルトが入ることを確認。

- [ ] **Step 7: テスト実行 → PASS**

- [ ] **Step 8: コミット**

```
git add src/Tsumugi.Domain/Entities/ClaimInput.cs \
        src/Tsumugi.Application/Dtos/ClaimInputDtos.cs \
        src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimInputConfiguration.cs \
        src/Tsumugi.Infrastructure/Migrations/*Phase33ClaimInputProviderFields* \
        tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryProviderFieldsTests.cs
git commit -m "feat(phase3-3/task 10): extend ClaimInput with provider:* fields"
```

---

## Task 11: `field-mapping-r7-10.json` + `ClaimInputRequirementProvider` に provider:* 宣言追加

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Csv/Specifications/field-mapping-r7-10.json`
- Modify: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`（cross-field 条件パーサが未対応な場合のみ）
- Modify: `tests/Tsumugi.Infrastructure.Csv.Tests/ClaimInputRequirementProviderTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs` に provider:* 追加

**Interfaces:**
- Produces:
  - `ClaimInputRequirementProvider.LoadEmbedded()` が provider:* 全 fieldId に対して requirement を返す
  - 孤立 4 フィールド `provider:J121:04:030-033` に cross-field Any-merge 条件が登録される

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Csv.Tests/ClaimInputRequirementProviderTests.cs` に追加:

```csharp
[Fact]
public void Provider_combines_exceptional_usage_cross_field_condition_via_any()
{
    var provider = ClaimInputRequirementProvider.LoadEmbedded();

    var requirements = provider.GetRequirements()
        .Where(r => r.FieldIds.Any(id => id.StartsWith("provider:J121:04:03", StringComparison.Ordinal)))
        .ToArray();

    requirements.Should().HaveCountGreaterOrEqualTo(4);
    foreach (var req in requirements)
    {
        req.Condition.Should().BeAssignableTo<ClaimRequirementCondition.Any>(
            "exceptional usage fields must use Any-merge cross-field condition");
    }
}

[Fact]
public void Provider_registers_all_provider_star_fields_from_field_mapping_json()
{
    var provider = ClaimInputRequirementProvider.LoadEmbedded();
    var catalog = CsvSpecificationLoader.LoadEmbedded();

    var missingProviderFieldIds = catalog.MappingByFieldId.Values
        .Where(m => m.FieldId.StartsWith("provider:", StringComparison.Ordinal))
        .Where(m => string.Equals(m.Status, "missing", StringComparison.Ordinal))
        .Select(m => m.FieldId)
        .ToArray();

    var registeredFieldIds = provider.GetRequirements()
        .SelectMany(r => r.FieldIds)
        .ToHashSet(StringComparer.Ordinal);

    foreach (var expected in missingProviderFieldIds)
    {
        registeredFieldIds.Should().Contain(expected,
            $"provider requirement for {expected} must be declared");
    }
}
```

`tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs` に追加:

```csharp
[Fact]
public void Real_embedded_requirement_provider_requires_all_exceptional_usage_fields_when_any_is_entered()
{
    var provider = ClaimInputRequirementProvider.LoadEmbedded();
    var input = ClaimInputBuilder.Minimal() with { ExceptionalUsageDays = 3 };

    var issues = ClaimReadinessEvaluator.Evaluate(provider.GetRequirements(), input, /* others */);

    issues.Should().Contain(i => i.FieldId == "provider:J121:04:030"); // ExceptionalUsageStartMonth
    issues.Should().Contain(i => i.FieldId == "provider:J121:04:031"); // ExceptionalUsageEndMonth
    issues.Should().Contain(i => i.FieldId == "provider:J121:04:033"); // StandardUsageDayTotal
}

[Fact]
public void Real_embedded_requirement_provider_does_not_require_exceptional_usage_fields_when_all_absent()
{
    var provider = ClaimInputRequirementProvider.LoadEmbedded();
    var input = ClaimInputBuilder.Minimal(); // 4 フィールド全て null

    var issues = ClaimReadinessEvaluator.Evaluate(provider.GetRequirements(), input, /* others */);

    issues.Should().NotContain(i => i.FieldId.StartsWith("provider:J121:04:03", StringComparison.Ordinal));
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗**

- [ ] **Step 3: `field-mapping-r7-10.json` に provider:* エントリを追記**

`field-mapping-r7-10.json` に、Task 10 で追加した ClaimInput プロパティに対応する mapping を全て追加。孤立 4 フィールドについては、`requiredCondition` を Any-merge 表現に：

```json
{
  "fieldId": "provider:J121:04:030",
  "status": "missing",
  "targetModel": "ClaimInput",
  "targetProperty": "ExceptionalUsageStartMonth",
  "uiSurface": "ClaimInputView",
  "requiredCondition": "any(modelPresent(ClaimInput.ExceptionalUsageStartMonth);modelPresent(ClaimInput.ExceptionalUsageEndMonth);modelNonZero(ClaimInput.ExceptionalUsageDays);modelNonZero(ClaimInput.StandardUsageDayTotal))"
},
{
  "fieldId": "provider:J121:04:031",
  "status": "missing",
  "targetModel": "ClaimInput",
  "targetProperty": "ExceptionalUsageEndMonth",
  "uiSurface": "ClaimInputView",
  "requiredCondition": "any(modelPresent(ClaimInput.ExceptionalUsageStartMonth);modelPresent(ClaimInput.ExceptionalUsageEndMonth);modelNonZero(ClaimInput.ExceptionalUsageDays);modelNonZero(ClaimInput.StandardUsageDayTotal))"
},
{
  "fieldId": "provider:J121:04:032",
  "status": "missing",
  "targetModel": "ClaimInput",
  "targetProperty": "ExceptionalUsageDays",
  "uiSurface": "ClaimInputView",
  "requiredCondition": "any(modelPresent(ClaimInput.ExceptionalUsageStartMonth);modelPresent(ClaimInput.ExceptionalUsageEndMonth);modelNonZero(ClaimInput.ExceptionalUsageDays);modelNonZero(ClaimInput.StandardUsageDayTotal))"
},
{
  "fieldId": "provider:J121:04:033",
  "status": "missing",
  "targetModel": "ClaimInput",
  "targetProperty": "StandardUsageDayTotal",
  "uiSurface": "ClaimInputView",
  "requiredCondition": "any(modelPresent(ClaimInput.ExceptionalUsageStartMonth);modelPresent(ClaimInput.ExceptionalUsageEndMonth);modelNonZero(ClaimInput.ExceptionalUsageDays);modelNonZero(ClaimInput.StandardUsageDayTotal))"
}
```

その他の provider:* missing エントリも同様に追加。

- [ ] **Step 4: `ClaimInputRequirementProvider` パーサ拡張（必要なら）**

既存 provider は `Any-merge` を報告書 field 用に対応済み。CSV mapping からも同じ Any-merge を作れるか確認。作れなければ `ToSource → GroupBy(TargetPath) → CreateRequirement` の parse ロジックに provider:J121:04 の 4 フィールド分の Any-merge 対応を追加。

- [ ] **Step 5: テスト実行 → PASS**

- [ ] **Step 6: コミット**

```
git add src/Tsumugi.Infrastructure.Csv/Specifications/field-mapping-r7-10.json \
        src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs \
        tests/Tsumugi.Infrastructure.Csv.Tests/ClaimInputRequirementProviderTests.cs \
        tests/Tsumugi.Infrastructure.Tests/Claim/ClaimPreviewProductionWiringTests.cs
git commit -m "feat(phase3-3/task 11): declare provider:* readiness with cross-field Any-merge for orphan 4"
```

---

## Task 12: `ClaimInputView` に provider:* セクションを追加

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs`
- Modify: `src/Tsumugi.App/Views/ClaimInputView.axaml`
- Modify: `src/Tsumugi.App/Views/ClaimInputView.axaml.cs`（必要なら）
- Test: `tests/Tsumugi.App.Tests/ClaimInputViewModelProviderFieldsTests.cs`

**Interfaces:**
- Consumes: Task 10 で追加した `ClaimInputDto` プロパティ
- Produces: 各プロパティに対応する `[ObservableProperty]` と、`ClaimInputView` に折り畳み可能な `Expander` セクション

- [ ] **Step 1: 失敗 ViewModel テストを書く**

`tests/Tsumugi.App.Tests/ClaimInputViewModelProviderFieldsTests.cs`:

```csharp
[Fact]
public void ClaimInputViewModel_exposes_all_provider_star_fields_as_observable_properties()
{
    var vm = ClaimInputViewModelFactory.Create();

    vm.ContractStartDate = new DateOnly(2026, 4, 1);
    vm.ContractSupplyQuantity = 22;
    vm.ProviderRemarks = "テスト";
    vm.ExceptionalUsageStartMonth = new ServiceMonth(2026, 7);
    vm.ExceptionalUsageDays = 3;

    var dto = vm.BuildDto();
    dto.ContractStartDate.Should().Be(new DateOnly(2026, 4, 1));
    dto.ContractSupplyQuantity.Should().Be(22);
    dto.ExceptionalUsageDays.Should().Be(3);
}

[Fact]
public void ClaimInputViewModel_reports_cross_field_readiness_issue_when_any_exceptional_usage_field_is_set()
{
    var vm = ClaimInputViewModelFactory.Create();
    vm.ExceptionalUsageDays = 3;

    vm.RefreshReadinessAsync().GetAwaiter().GetResult();

    vm.ReadinessIssues.Should().Contain(i => i.FieldId == "provider:J121:04:030");
}
```

- [ ] **Step 2: `ClaimInputViewModel` を拡張**

Task 10 の全プロパティに対応する `[ObservableProperty]` を追加。`BuildDto()` メソッドで DTO へマッピング。既存の readiness 通知経路（`ClaimInputRequirementProvider`）が provider:* 分も含めて自動判定するので、追加ロジックは不要。

- [ ] **Step 3: `ClaimInputView.axaml` にセクションを追加**

既存 View に `<Expander Header="国保連CSV固有入力">` を追加し、以下のグループに整理：

```xml
<Expander Header="契約情報" IsExpanded="False">
  <StackPanel>
    <CalendarDatePicker SelectedDate="{Binding ContractStartDate}" />
    <TextBox Text="{Binding ContractSupplyQuantity}" />
    <TextBox Text="{Binding ProviderRemarks}" />
  </StackPanel>
</Expander>
<Expander Header="経過措置・例外利用日" IsExpanded="False">
  <StackPanel>
    <TextBox Text="{Binding ExceptionalUsageStartMonth}" />
    <TextBox Text="{Binding ExceptionalUsageEndMonth}" />
    <TextBox Text="{Binding ExceptionalUsageDays}" />
    <TextBox Text="{Binding StandardUsageDayTotal}" />
    <ComboBox SelectedItem="{Binding TransitionalMeasureCategory}" />
  </StackPanel>
</Expander>
```

（Phase 3-2 の Expander pattern に従う。アクセシビリティ既定を継承）

- [ ] **Step 4: テスト実行 → PASS**

- [ ] **Step 5: コミット**

```
git add src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs \
        src/Tsumugi.App/Views/ClaimInputView.axaml \
        src/Tsumugi.App/Views/ClaimInputView.axaml.cs \
        tests/Tsumugi.App.Tests/ClaimInputViewModelProviderFieldsTests.cs
git commit -m "feat(phase3-3/task 12): add provider:* input sections to ClaimInputView"
```

---

## Task 13: `ExportClaimCsvUseCase` と例外群

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimBatchNotFinalizedException.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimCsvReadinessException.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimCsvExportFailedException.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/ClaimCsvExportResult.cs`
- Create: `src/Tsumugi.Application/Claim/ExportClaimCsvUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ExportClaimCsvUseCaseTests.cs`

**Interfaces:**
- Consumes: `IClaimBatchRepository`, `IClaimCsvExportRepository`, `IClaimInputRequirementProvider`, `RecordIdRouter`, `ClaimCsvWriter`, 9 種 `IProviderRecordBuilder<T>`, `ITimeProvider`
- Produces:
  - `ClaimCsvExportResult(Bytes: ReadOnlyMemory<byte>, SuggestedFileName: string, Sha256: string)`
  - `ExportClaimCsvUseCase.Execute(Guid claimBatchId, ProcessingMonth processingMonth, CancellationToken)`

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Application.Tests/Claim/ExportClaimCsvUseCaseTests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ExportClaimCsvUseCaseTests
{
    [Fact]
    public async Task Execute_throws_when_claim_batch_is_not_finalized()
    {
        var fixture = new ExportClaimCsvUseCaseFixture()
            .WithClaimBatch(finalized: false);

        var act = async () => await fixture.UseCase.Execute(
            fixture.ClaimBatchId, new ProcessingMonth(2026, 8), CancellationToken.None);

        (await act.Should().ThrowAsync<ClaimBatchNotFinalizedException>())
            .Which.ClaimBatchId.Should().Be(fixture.ClaimBatchId);
    }

    [Fact]
    public async Task Execute_throws_readiness_exception_when_provider_fields_missing()
    {
        var fixture = new ExportClaimCsvUseCaseFixture()
            .WithClaimBatch(finalized: true)
            .WithMissingProviderField("provider:J121:01:015");

        var act = async () => await fixture.UseCase.Execute(
            fixture.ClaimBatchId, new ProcessingMonth(2026, 8), CancellationToken.None);

        (await act.Should().ThrowAsync<ClaimCsvReadinessException>())
            .Which.MissingFieldIds.Should().Contain("provider:J121:01:015");
    }

    [Fact]
    public async Task Execute_returns_result_and_appends_history_on_success()
    {
        var fixture = new ExportClaimCsvUseCaseFixture()
            .WithClaimBatch(finalized: true)
            .WithAllProviderFieldsFilled()
            .WithTimeProvider(new FakeTimeProvider(new DateTime(2026, 8, 10, 12, 34, 56, DateTimeKind.Utc)));

        var result = await fixture.UseCase.Execute(
            fixture.ClaimBatchId, new ProcessingMonth(2026, 8), CancellationToken.None);

        result.Bytes.Length.Should().BeGreaterThan(0);
        result.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
        result.SuggestedFileName.Should().Match("*_202608_*.csv");

        var history = await fixture.CsvExportRepository.ListByBatchAsync(fixture.ClaimBatchId, CancellationToken.None);
        history.Should().ContainSingle().Which.Sha256.Should().Be(result.Sha256);
    }

    [Fact]
    public async Task Execute_does_not_append_history_when_encoder_fails()
    {
        var fixture = new ExportClaimCsvUseCaseFixture()
            .WithClaimBatch(finalized: true)
            .WithAllProviderFieldsFilled()
            .WithOfficeName("🍣"); // CP932 変換不能

        var act = async () => await fixture.UseCase.Execute(
            fixture.ClaimBatchId, new ProcessingMonth(2026, 8), CancellationToken.None);

        await act.Should().ThrowAsync<ClaimCsvExportFailedException>();

        var history = await fixture.CsvExportRepository.ListByBatchAsync(fixture.ClaimBatchId, CancellationToken.None);
        history.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: テスト実行 → 期待通り失敗**

- [ ] **Step 3: 例外群を書く**

`src/Tsumugi.Application/Claim/ClaimBatchNotFinalizedException.cs`:

```csharp
namespace Tsumugi.Application.Claim;

public sealed class ClaimBatchNotFinalizedException : Exception
{
    public ClaimBatchNotFinalizedException(Guid claimBatchId)
        : base($"ClaimBatch {claimBatchId:N} is not finalized; CSV export requires a finalized batch.")
    {
        ClaimBatchId = claimBatchId;
    }
    public Guid ClaimBatchId { get; }
}
```

`src/Tsumugi.Application/Claim/ClaimCsvReadinessException.cs`:

```csharp
namespace Tsumugi.Application.Claim;

public sealed class ClaimCsvReadinessException : Exception
{
    public ClaimCsvReadinessException(IReadOnlyList<string> missingFieldIds)
        : base($"CSV export blocked by {missingFieldIds.Count} missing provider fields.")
    {
        MissingFieldIds = missingFieldIds;
    }
    public IReadOnlyList<string> MissingFieldIds { get; }
}
```

`src/Tsumugi.Application/Claim/ClaimCsvExportFailedException.cs`:

```csharp
using Tsumugi.Infrastructure.Csv.Writer;

namespace Tsumugi.Application.Claim;

public sealed class ClaimCsvExportFailedException : Exception
{
    public ClaimCsvExportFailedException(
        string recordId, string fieldId, CsvEncodingReason reason, string? recipientReferenceCode = null)
        : base($"CSV export failed at record={recordId} field={fieldId} reason={reason}.")
    {
        RecordId = recordId;
        FieldId = fieldId;
        Reason = reason;
        RecipientReferenceCode = recipientReferenceCode;
    }
    public string RecordId { get; }
    public string FieldId { get; }
    public CsvEncodingReason Reason { get; }
    public string? RecipientReferenceCode { get; }
}
```

**注記**: `RecipientReferenceCode` は内部 code（受給者証番号でなく system-internal のハッシュ code）。氏名・受給者証番号は含めない（CLAUDE.md §制約4）。

- [ ] **Step 4: `ClaimCsvExportResult` を書く**

`src/Tsumugi.Application/Dtos/Claim/ClaimCsvExportResult.cs`:

```csharp
namespace Tsumugi.Application.Dtos.Claim;

public sealed record ClaimCsvExportResult(
    ReadOnlyMemory<byte> Bytes,
    string SuggestedFileName,
    string Sha256);
```

- [ ] **Step 5: `ExportClaimCsvUseCase` を書く**

`src/Tsumugi.Application/Claim/ExportClaimCsvUseCase.cs`:

```csharp
using System.Security.Cryptography;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Csv.ServiceCode;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;
using Tsumugi.Infrastructure.Csv.Writer.Builders;
using Tsumugi.Infrastructure.Csv.Writer.Sources;

namespace Tsumugi.Application.Claim;

public sealed class ExportClaimCsvUseCase
{
    private readonly IClaimBatchRepository _batches;
    private readonly IClaimCsvExportRepository _history;
    private readonly IClaimInputRequirementProvider _requirements;
    private readonly IServiceCodeCatalog _serviceCodes;
    private readonly CsvSpecificationCatalog _spec;
    private readonly ITimeProvider _time;
    private readonly IPrincipalProvider _principal;

    public ExportClaimCsvUseCase(
        IClaimBatchRepository batches,
        IClaimCsvExportRepository history,
        IClaimInputRequirementProvider requirements,
        IServiceCodeCatalog serviceCodes,
        CsvSpecificationCatalog spec,
        ITimeProvider time,
        IPrincipalProvider principal)
    {
        _batches = batches;
        _history = history;
        _requirements = requirements;
        _serviceCodes = serviceCodes;
        _spec = spec;
        _time = time;
        _principal = principal;
    }

    public async Task<ClaimCsvExportResult> Execute(
        Guid claimBatchId,
        ProcessingMonth processingMonth,
        CancellationToken cancellationToken)
    {
        var aggregate = await _batches.GetByIdAsync(claimBatchId, cancellationToken)
            ?? throw new InvalidOperationException($"ClaimBatch {claimBatchId:N} not found.");

        if (!aggregate.IsFinalized)
            throw new ClaimBatchNotFinalizedException(claimBatchId);

        // Readiness
        var issues = ClaimReadinessEvaluator.EvaluateForExport(
            _requirements.GetRequirements(), aggregate);
        if (issues.Count > 0)
            throw new ClaimCsvReadinessException(
                issues.Select(i => i.FieldId).ToArray());

        // Route to bundle
        var bundle = RecordIdRouter.Route(aggregate, processingMonth);

        // Build rows
        var controlRow = new ClaimControlBuilder().Build(
            new ClaimControlSource(processingMonth, aggregate), _spec).Single();
        var dataRows = new List<CsvRow>();
        dataRows.AddRange(new ClaimInvoiceHeaderBuilder().Build(bundle.InvoiceHeader, _spec));
        dataRows.AddRange(new ClaimInvoiceTotalsBuilder().Build(bundle.InvoiceTotals, _spec));
        foreach (var c in bundle.Contracts) dataRows.AddRange(new ClaimContractBuilder().Build(c, _spec));
        foreach (var s in bundle.StatementSummaries) dataRows.AddRange(new ClaimStatementSummaryBuilder().Build(s, _spec));
        dataRows.AddRange(new ClaimStatementLineBuilder().Build(bundle.StatementLines, _spec));
        foreach (var a in bundle.StatementAggregates) dataRows.AddRange(new ClaimStatementAggregateBuilder().Build(a, _spec));
        foreach (var t in bundle.Transitionals) dataRows.AddRange(new ClaimTransitionalBuilder().Build(t, _spec));
        foreach (var h in bundle.UpperLimitHeaders) dataRows.AddRange(new UpperLimitManagementHeaderBuilder().Build(h, _spec));
        foreach (var l in bundle.UpperLimitLines) dataRows.AddRange(new UpperLimitManagementLineBuilder().Build(l, _spec));
        var endRow = new ClaimEndBuilder().Build(
            new ClaimEndSource(dataRows.Count), _spec).Single();

        // Write
        ReadOnlyMemory<byte> bytes;
        try
        {
            bytes = ClaimCsvWriter.WriteAll(controlRow, dataRows, endRow, _spec);
        }
        catch (CsvEncodingException ex)
        {
            throw new ClaimCsvExportFailedException(
                recordId: FindRecordIdFor(ex.FieldId),
                fieldId: ex.FieldId,
                reason: ex.Reason,
                recipientReferenceCode: null);
        }

        // Sha256 + history
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
        var export = new ClaimCsvExport
        {
            Id = Guid.NewGuid(),
            ClaimBatchId = claimBatchId,
            ProcessingMonth = processingMonth,
            CsvSpecVersion = _spec.Version,
            RewardMasterVersion = aggregate.ClaimMasterVersion,
            ServiceCodeVersion = _serviceCodes.Version,
            Sha256 = sha256,
            CreatedAtUtc = _time.UtcNow,
            CreatedBy = _principal.CurrentUser,
        };
        await _history.AppendAsync(export, cancellationToken);

        var fileName = BuildFileName(aggregate.OfficeCode, processingMonth, sha256);
        return new ClaimCsvExportResult(bytes, fileName, sha256);
    }

    private string FindRecordIdFor(string fieldId)
    {
        return _spec.RecordByFieldId.TryGetValue(fieldId, out var record)
            ? record.RecordId : "unknown";
    }

    private static string BuildFileName(string officeCode, ProcessingMonth month, string sha256)
        => $"kokuho_{officeCode}_{month.Year:D4}{month.Month:D2}_{sha256[..8]}.csv";
}
```

**注記**:
- `ClaimControlBuilder` / `ClaimEndBuilder` は Task 4 の frame writer が record-type-number を付けるので、field 側は「レコード種別コード」だけを扱う軽量 builder。実装は Task 7 と同じパターン。
- `ITimeProvider` / `IPrincipalProvider` は既存 abstraction を使用。存在しなければ Phase 3-2 で確立済みの `FakeTimeProvider` パターンに従う。

- [ ] **Step 6: テスト fixture を書く**

`tests/Tsumugi.Application.Tests/Claim/ExportClaimCsvUseCaseFixture.cs`:

```csharp
internal sealed class ExportClaimCsvUseCaseFixture
{
    public Guid ClaimBatchId { get; } = Guid.NewGuid();
    public FakeClaimCsvExportRepository CsvExportRepository { get; } = new();
    public ExportClaimCsvUseCase UseCase { get; }

    private readonly FakeClaimBatchRepository _batches = new();
    private readonly FakeRequirementProvider _requirements = new();
    private FakeTimeProvider _time = new(DateTime.UtcNow);

    public ExportClaimCsvUseCaseFixture()
    {
        UseCase = new ExportClaimCsvUseCase(
            _batches, CsvExportRepository, _requirements,
            JsonServiceCodeCatalog.LoadEmbedded(),
            CsvSpecificationLoader.LoadEmbedded(),
            _time,
            new FakePrincipalProvider("tester"));
    }

    public ExportClaimCsvUseCaseFixture WithClaimBatch(bool finalized) { /* ... */ return this; }
    public ExportClaimCsvUseCaseFixture WithMissingProviderField(string fieldId) { /* ... */ return this; }
    public ExportClaimCsvUseCaseFixture WithAllProviderFieldsFilled() { /* ... */ return this; }
    public ExportClaimCsvUseCaseFixture WithTimeProvider(FakeTimeProvider tp) { _time = tp; return this; }
    public ExportClaimCsvUseCaseFixture WithOfficeName(string name) { /* ... */ return this; }
}
```

（詳細は既存の Phase 3-2 fixture パターンを踏襲）

- [ ] **Step 7: テスト実行 → PASS**

- [ ] **Step 8: コミット**

```
git add src/Tsumugi.Application/Claim/*.cs \
        src/Tsumugi.Application/Dtos/Claim/ClaimCsvExportResult.cs \
        tests/Tsumugi.Application.Tests/Claim/ExportClaimCsvUseCase*.cs
git commit -m "feat(phase3-3/task 13/AC3-7): add ExportClaimCsvUseCase with fail-close and history"
```

---

## Task 14: `ClaimCsvExportRepository` production wiring

**Files:**
- Test: `tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportProductionWiringTests.cs`

**Interfaces:**
- Consumes: `ExportClaimCsvUseCase`（Task 13）、`ClaimCsvExportRepository`（Task 1）
- Produces: DI 登録の確認と production 環境での through-test

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportProductionWiringTests.cs`:

```csharp
[Fact]
public async Task Real_wiring_persists_export_and_returns_bytes_with_matching_sha256()
{
    var services = new ServiceCollection();
    services.AddTsumugiInfrastructure(sqliteConnectionString: ":memory:");
    services.AddTsumugiInfrastructureCsv();
    services.AddTsumugiApplication();
    var sp = services.BuildServiceProvider();

    await using var scope = sp.CreateAsyncScope();
    var ctx = scope.ServiceProvider.GetRequiredService<TsumugiDbContext>();
    await ctx.Database.EnsureCreatedAsync();

    // 確定済み ClaimBatch を作る
    var batchId = await ClaimBatchSeeder.SeedFinalizedMinimalAsync(scope.ServiceProvider);

    var useCase = scope.ServiceProvider.GetRequiredService<ExportClaimCsvUseCase>();
    var result = await useCase.Execute(batchId, new ProcessingMonth(2026, 8), CancellationToken.None);

    var actualSha = Convert.ToHexString(SHA256.HashData(result.Bytes.Span)).ToLowerInvariant();
    actualSha.Should().Be(result.Sha256);

    var repo = scope.ServiceProvider.GetRequiredService<IClaimCsvExportRepository>();
    var history = await repo.ListByBatchAsync(batchId, CancellationToken.None);
    history.Should().ContainSingle();
}
```

- [ ] **Step 2: DI 登録の追加**

`src/Tsumugi.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` に：

```csharp
services.AddScoped<IClaimCsvExportRepository, ClaimCsvExportRepository>();
```

`src/Tsumugi.Infrastructure.Csv/DependencyInjection/CsvServiceCollectionExtensions.cs`（新規または既存の Csv DI へ）:

```csharp
public static IServiceCollection AddTsumugiInfrastructureCsv(this IServiceCollection services)
{
    services.AddSingleton<CsvSpecificationCatalog>(_ => CsvSpecificationLoader.LoadEmbedded());
    services.AddSingleton<IServiceCodeCatalog>(_ => JsonServiceCodeCatalog.LoadEmbedded());
    services.AddSingleton<IClaimInputRequirementProvider>(_ => ClaimInputRequirementProvider.LoadEmbedded());
    services.AddScoped<ExportClaimCsvUseCase>();
    return services;
}
```

- [ ] **Step 3: テスト実行 → PASS**

- [ ] **Step 4: コミット**

```
git add tests/Tsumugi.Infrastructure.Tests/Claim/ClaimCsvExportProductionWiringTests.cs \
        src/Tsumugi.Infrastructure/DependencyInjection/*.cs \
        src/Tsumugi.Infrastructure.Csv/DependencyInjection/*.cs
git commit -m "test(phase3-3/task 14): production wiring for CSV export use case"
```

---

## Task 15: `CsvExportView` / `CsvExportViewModel`

**Files:**
- Create: `src/Tsumugi.App/ViewModels/CsvExportViewModel.cs`
- Create: `src/Tsumugi.App/Views/CsvExportView.axaml`
- Create: `src/Tsumugi.App/Views/CsvExportView.axaml.cs`
- Modify: `src/Tsumugi.App/App.axaml.cs`（ナビゲーション登録）
- Test: `tests/Tsumugi.App.Tests/CsvExportViewModelTests.cs`

**Interfaces:**
- Consumes: `ExportClaimCsvUseCase`, `IClaimBatchRepository`（確定済み一覧）, `IFileSaveService`
- Produces: `CsvExportViewModel` with `[ObservableProperty]`:
  - `SelectableBatches: IReadOnlyList<ClaimBatchListItemDto>`
  - `SelectedBatchId: Guid?`
  - `ProcessingMonthYear: int` / `ProcessingMonthMonth: int`
  - `ErrorMessage: string?`
  - Commands: `LoadCommand`, `GenerateCsvCommand`

- [ ] **Step 1: 失敗テストを書く**

`tests/Tsumugi.App.Tests/CsvExportViewModelTests.cs`:

```csharp
[Fact]
public async Task GenerateCsvCommand_saves_bytes_via_file_save_service_when_export_succeeds()
{
    var vm = new CsvExportViewModel(FakeUseCase.Success(), FakeBatches, FakeFileSave);
    vm.SelectedBatchId = Guid.NewGuid();
    vm.ProcessingMonthYear = 2026;
    vm.ProcessingMonthMonth = 8;

    await vm.GenerateCsvCommand.ExecuteAsync(null);

    FakeFileSave.LastSavedBytes.Length.Should().BeGreaterThan(0);
    vm.ErrorMessage.Should().BeNull();
}

[Fact]
public async Task GenerateCsvCommand_shows_field_ids_when_readiness_fails()
{
    var vm = new CsvExportViewModel(
        FakeUseCase.Failing(new ClaimCsvReadinessException(new[] { "provider:J121:01:015" })),
        FakeBatches, FakeFileSave);
    vm.SelectedBatchId = Guid.NewGuid();
    vm.ProcessingMonthYear = 2026;
    vm.ProcessingMonthMonth = 8;

    await vm.GenerateCsvCommand.ExecuteAsync(null);

    vm.ErrorMessage.Should().Contain("provider:J121:01:015");
    FakeFileSave.LastSavedBytes.Should().BeNull();
}

[Fact]
public async Task GenerateCsvCommand_shows_encoding_error_without_recipient_name()
{
    var vm = new CsvExportViewModel(
        FakeUseCase.Failing(new ClaimCsvExportFailedException(
            "provider:J121:02", "provider:J121:02:007", CsvEncodingReason.NonRepresentableCharacter,
            recipientReferenceCode: "REC-001")),
        FakeBatches, FakeFileSave);
    vm.SelectedBatchId = Guid.NewGuid();
    vm.ProcessingMonthYear = 2026;
    vm.ProcessingMonthMonth = 8;

    await vm.GenerateCsvCommand.ExecuteAsync(null);

    vm.ErrorMessage.Should().Contain("provider:J121:02:007");
    vm.ErrorMessage.Should().NotContain("氏名");
    vm.ErrorMessage.Should().Contain("REC-001");
}
```

- [ ] **Step 2: `CsvExportViewModel` を書く**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

public sealed partial class CsvExportViewModel : ObservableObject
{
    private readonly ExportClaimCsvUseCase _useCase;
    private readonly IClaimBatchRepository _batches;
    private readonly IFileSaveService _fileSave;

    [ObservableProperty] private IReadOnlyList<ClaimBatchListItemDto> _selectableBatches = Array.Empty<ClaimBatchListItemDto>();
    [ObservableProperty] private Guid? _selectedBatchId;
    [ObservableProperty] private int _processingMonthYear = DateTime.UtcNow.Year;
    [ObservableProperty] private int _processingMonthMonth = DateTime.UtcNow.Month;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public CsvExportViewModel(
        ExportClaimCsvUseCase useCase,
        IClaimBatchRepository batches,
        IFileSaveService fileSave)
    {
        _useCase = useCase;
        _batches = batches;
        _fileSave = fileSave;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        SelectableBatches = await _batches.ListFinalizedAsync(ct);
    }

    [RelayCommand]
    private async Task GenerateCsvAsync(CancellationToken ct)
    {
        if (SelectedBatchId is not Guid batchId) return;
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var month = new ProcessingMonth(ProcessingMonthYear, ProcessingMonthMonth);
            var result = await _useCase.Execute(batchId, month, ct);
            await _fileSave.SaveBytesAsync(result.SuggestedFileName, result.Bytes, ct);
        }
        catch (ClaimBatchNotFinalizedException)
        {
            ErrorMessage = "選択された請求バッチは確定されていません。";
        }
        catch (ClaimCsvReadinessException ex)
        {
            ErrorMessage = "以下の項目が未入力のため出力できません: "
                + string.Join(", ", ex.MissingFieldIds);
        }
        catch (ClaimCsvExportFailedException ex)
        {
            var refPart = ex.RecipientReferenceCode is null ? "" : $" [{ex.RecipientReferenceCode}]";
            ErrorMessage = $"CSV出力失敗: record={ex.RecordId}, field={ex.FieldId}, 理由={ex.Reason}{refPart}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

- [ ] **Step 3: `CsvExportView.axaml` を書く**

```xml
<UserControl xmlns="https://github.com/avaloniaui" ...>
  <StackPanel>
    <TextBlock Text="国保連請求 CSV 出力" Classes="h1" />
    <ComboBox ItemsSource="{Binding SelectableBatches}"
              SelectedValue="{Binding SelectedBatchId}"
              SelectedValueBinding="{Binding Id}" />
    <StackPanel Orientation="Horizontal">
      <NumericUpDown Value="{Binding ProcessingMonthYear}" Minimum="2020" Maximum="2100" />
      <TextBlock Text="年" />
      <NumericUpDown Value="{Binding ProcessingMonthMonth}" Minimum="1" Maximum="12" />
      <TextBlock Text="月（処理対象年月）" />
    </StackPanel>
    <Button Content="CSV生成" Command="{Binding GenerateCsvCommand}" />
    <TextBlock Text="{Binding ErrorMessage}" Foreground="Red" IsVisible="{Binding ErrorMessage, Converter={x:Static ObjectConverters.IsNotNull}}" />
  </StackPanel>
</UserControl>
```

- [ ] **Step 4: ナビゲーション登録**

既存のメインメニューまたはナビゲーション経路に `CsvExportView` を追加。DI で `CsvExportViewModel` を Scoped 登録。

- [ ] **Step 5: テスト実行 → PASS**

- [ ] **Step 6: コミット**

```
git add src/Tsumugi.App/ViewModels/CsvExportViewModel.cs \
        src/Tsumugi.App/Views/CsvExportView.axaml \
        src/Tsumugi.App/Views/CsvExportView.axaml.cs \
        src/Tsumugi.App/App.axaml.cs \
        tests/Tsumugi.App.Tests/CsvExportViewModelTests.cs
git commit -m "feat(phase3-3/task 15): add CsvExportView with fail-close error messaging"
```

---

## Task 16: Golden CSV fixture 3 種とバイトスナップショット決定論テスト（AC3-7）

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-normal.csv`
- Create: `tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-correction.csv`
- Create: `tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-cjk.csv`
- Create: `tests/Tsumugi.Infrastructure.Tests/Claim/GoldenCsvSnapshotTests.cs`

**Interfaces:**
- Consumes: `ExportClaimCsvUseCase`（Task 13 完成後）、`FakeTimeProvider`, `ClaimBatchSeeder`
- Produces: AC3-7 のバイトスナップショット一致検証

- [ ] **Step 1: fixture 生成の失敗テストを書く**

`tests/Tsumugi.Infrastructure.Tests/Claim/GoldenCsvSnapshotTests.cs`:

```csharp
using System.Text;
using FluentAssertions;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Infrastructure.Tests.Claim;

public sealed class GoldenCsvSnapshotTests
{
    static GoldenCsvSnapshotTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Theory]
    [InlineData("csv-golden-normal.csv", "Normal")]
    [InlineData("csv-golden-correction.csv", "Correction")]
    [InlineData("csv-golden-cjk.csv", "CjkNormalization")]
    public async Task Generated_csv_matches_golden_fixture_byte_for_byte(
        string fixtureName, string scenario)
    {
        var expected = await File.ReadAllBytesAsync(Path.Combine("Fixtures", fixtureName));

        var actual = await GoldenScenarioRunner.RunAsync(scenario);

        actual.ToArray().Should().Equal(expected,
            $"scenario '{scenario}' must produce byte-identical CSV");
    }

    [Fact]
    public async Task Generated_csv_is_deterministic_for_same_inputs_and_timeprovider()
    {
        var first = await GoldenScenarioRunner.RunAsync("Normal");
        var second = await GoldenScenarioRunner.RunAsync("Normal");

        first.ToArray().Should().Equal(second.ToArray());
    }

    [Fact]
    public async Task Generated_csv_control_record_contains_processing_month_at_expected_position()
    {
        var actual = await GoldenScenarioRunner.RunAsync("Normal");
        var text = Encoding.GetEncoding(932).GetString(actual.ToArray());
        var firstLine = text.Split("\r\n")[0];

        firstLine.Should().StartWith("1,");
        firstLine.Should().Contain("202608"); // ProcessingMonth
    }
}
```

- [ ] **Step 2: `GoldenScenarioRunner` を書く**

`tests/Tsumugi.Infrastructure.Tests/Claim/GoldenScenarioRunner.cs`:

```csharp
internal static class GoldenScenarioRunner
{
    public static async Task<ReadOnlyMemory<byte>> RunAsync(string scenario)
    {
        var services = new ServiceCollection();
        // 決定論のため、FakeTimeProvider を固定日時で登録
        services.AddSingleton<ITimeProvider>(new FakeTimeProvider(
            new DateTime(2026, 8, 10, 12, 34, 56, DateTimeKind.Utc)));
        services.AddTsumugiInfrastructure(":memory:");
        services.AddTsumugiInfrastructureCsv();
        services.AddTsumugiApplication();
        var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TsumugiDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var batchId = scenario switch
        {
            "Normal" => await ClaimBatchSeeder.SeedNormalAsync(scope.ServiceProvider),
            "Correction" => await ClaimBatchSeeder.SeedCorrectionAsync(scope.ServiceProvider),
            "CjkNormalization" => await ClaimBatchSeeder.SeedCjkAsync(scope.ServiceProvider),
            _ => throw new ArgumentException($"Unknown scenario {scenario}"),
        };

        var useCase = scope.ServiceProvider.GetRequiredService<ExportClaimCsvUseCase>();
        var result = await useCase.Execute(
            batchId, new ProcessingMonth(2026, 8), CancellationToken.None);
        return result.Bytes;
    }
}
```

- [ ] **Step 3: `ClaimBatchSeeder` の 3 シナリオを実装**

固定 UUID・固定日時・固定 recipient code / office code / service line で決定論的な ClaimBatch を作る。
- `SeedNormalAsync`: 通常請求 1 人 5 明細
- `SeedCorrectionAsync`: 訂正レコード（`Kind = Correction`、`OriginId` 設定）
- `SeedCjkAsync`: Kangxi radical 混入の氏名（`KangxiRadicalNormalizer` 経路のバイト一致検証）

- [ ] **Step 4: golden fixture を初回生成**

初回はテストが FAIL する（golden がない）。実装が正しいと信じるなら、`RunAsync` の出力を fixture に書き出すヘルパー（xUnit のマニュアル手順として README に書く）：

```csharp
// テスト時 environment variable UPDATE_GOLDEN=1 を設定すると fixture 更新
```

**注意**: golden の初回生成は Codex レビュー前に**手動で内容を目視確認**。バイト列の shape が仕様と合っていることを ADR 0024・field-mapping-r7-10.json と照合してから commit する。

- [ ] **Step 5: テスト実行 → PASS**

```
dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~GoldenCsvSnapshotTests
```

- [ ] **Step 6: コミット**

```
git add tests/Tsumugi.Infrastructure.Tests/Fixtures/csv-golden-*.csv \
        tests/Tsumugi.Infrastructure.Tests/Claim/GoldenCsvSnapshotTests.cs \
        tests/Tsumugi.Infrastructure.Tests/Claim/GoldenScenarioRunner.cs \
        tests/Tsumugi.Infrastructure.Tests/Claim/ClaimBatchSeeder.cs
git commit -m "test(phase3-3/task 16/AC3-7): add 3 golden CSV snapshot tests (Normal/Correction/CJK)"
```

---

## Task 17: 依存方向・オフライン検査の拡張

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/Architecture/DependencyDirectionTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/Architecture/OfflineComplianceTests.cs`

**Interfaces:**
- 追加: `Tsumugi.Infrastructure.Csv` の新規シンボル群を対象に依存方向とオフライン検査を実施

- [ ] **Step 1: 依存方向テストを追記**

```csharp
[Fact]
public void Tsumugi_Infrastructure_Csv_Writer_does_not_reference_Infrastructure_or_App()
{
    var writerAssembly = typeof(ClaimCsvWriter).Assembly;
    var references = writerAssembly.GetReferencedAssemblies();
    references.Should().NotContain(r => r.Name == "Tsumugi.Infrastructure");
    references.Should().NotContain(r => r.Name == "Tsumugi.App");
    references.Should().NotContain(r => r.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
}

[Fact]
public void ExportClaimCsvUseCase_does_not_depend_on_EF_Core()
{
    var appAssembly = typeof(ExportClaimCsvUseCase).Assembly;
    var references = appAssembly.GetReferencedAssemblies();
    references.Should().NotContain(r => r.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
}
```

- [ ] **Step 2: オフラインテストを追記**

```csharp
[Theory]
[InlineData("Tsumugi.Infrastructure.Csv")]
[InlineData("Tsumugi.Application")]
[InlineData("Tsumugi.Domain")]
[InlineData("Tsumugi.Infrastructure")]
[InlineData("Tsumugi.App")]
public void Assembly_does_not_reference_network_libraries(string assemblyName)
{
    var assembly = Assembly.Load(assemblyName);
    var references = assembly.GetReferencedAssemblies()
        .Select(r => r.Name!).ToArray();

    var forbidden = new[] {
        "System.Net.Http", "System.Net.WebSockets", "System.Net.Sockets",
        "Microsoft.AspNetCore", "Grpc.Net.Client", "MailKit", "Npgsql",
    };
    references.Should().NotIntersectWith(forbidden);
}
```

（既存の同種テストがあれば、`Tsumugi.Infrastructure.Csv` を InlineData に追加するだけでよい）

- [ ] **Step 3: テスト実行 → PASS**

- [ ] **Step 4: コミット**

```
git add tests/Tsumugi.Domain.Tests/Architecture/DependencyDirectionTests.cs \
        tests/Tsumugi.Infrastructure.Tests/Architecture/OfflineComplianceTests.cs
git commit -m "test(phase3-3/task 17): extend dependency-direction and offline checks for Csv writer"
```

---

## Task 18: `docs/phase3-3-acceptance.md` と open-questions 更新、ADR 補足

**Files:**
- Create: `docs/phase3-3-acceptance.md`
- Modify: `docs/open-questions.md`
- Modify: `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`
- Modify: `docs/decisions/0030-report-input-ui-responsibility.md`
- Modify: `01_ClaudeCode_実装指示書_Tsumugi.md`（AC3-7 のチェック）
- Modify: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`（AC3-7 のチェック）
- Modify: `docs/superpowers/plans/2026-07-20-phase3-3-kokuhoren-csv.md`（本ファイル: 全 Task のチェックボックスを完了に更新）
- Modify: `CLAUDE.md`（現在地更新）

- [ ] **Step 1: `docs/phase3-3-acceptance.md` を書く**

Phase 3-2 受け入れ証跡（`docs/phase3-2-acceptance.md`）のフォーマットを踏襲：
- §1: AC3-7 の 5 項目セルフチェック（各項目に対応するテスト名を列挙）
- §2: 30 provider:* フィールド → 入力 UI → readiness 宣言 → CSV フィールド 対応表
- §3: 依存方向 / オフライン検査 の証跡（テスト名）
- §4: 決定論（Normal/Correction/CJK の 3 テスト名）
- §5: CJK 検証（Kangxi normalizer 経由のバイト一致テスト名）
- §6: 既知の限界（GUI 手動貫通確認未実施 等）
- §7: `./build/ci.sh` 実行証跡
- §8: 本スライスで提供したスコープ

- [ ] **Step 2: `docs/open-questions.md` の更新**

- 孤立 4 フィールド項目 → 「Phase 3-3 で cross-field readiness 化して解消」を追記して close
- R8.6 サービスコード表 → 「Phase 3-3 で ADR 0031 起票して解消（一次資料 URL・SHA256 は ADR 内で確定）」を追記して close
- GUI 手動貫通確認 → 「Phase 3-3 でも未実施のため継続」

- [ ] **Step 3: ADR 補足**

- 0024 に「Phase 3-3 で writer をハイブリッド方式（strong-typed IProviderRecordBuilder<T> + spec JSON 駆動 encoder）で実装」を追記
- 0030 に「provider:* 追加フィールドも ClaimInputView に集約する（Task 12 実装）」を追記

- [ ] **Step 4: `01_...` と `06_...` の AC3-7 チェックを埋める**

- 06_...Phase3 §7 3-3 の AC3-7 チェックボックスを [x] に
- 01_... の Phase 3-3 該当箇所を [x] に

- [ ] **Step 5: `CLAUDE.md` 「現在地」を更新**

```markdown
- **現在地**: フェーズ0・1・2、Phase 3-0、Phase 3-1、Phase 3-2、Phase 3-3 は完了（Phase 3-3 は CSV writer + 30 provider:* UI + R8.6 service code、spec: `docs/superpowers/specs/2026-07-20-phase3-3-kokuhoren-csv-design.md`、計画: `docs/superpowers/plans/2026-07-20-phase3-3-kokuhoren-csv.md`、受け入れ証跡: `docs/phase3-3-acceptance.md`）。次は Phase 4 準備。GUI 手動貫通確認は残課題として open-questions に維持。
```

- [ ] **Step 6: `./build/ci.sh` を全緑で回す**

```
./build/ci.sh
```
Expected: 全緑

- [ ] **Step 7: 最終コミット**

```
git add docs/phase3-3-acceptance.md \
        docs/open-questions.md \
        docs/decisions/0024-kokuhoren-csv-and-field-mapping.md \
        docs/decisions/0030-report-input-ui-responsibility.md \
        01_ClaudeCode_実装指示書_Tsumugi.md \
        06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md \
        docs/superpowers/plans/2026-07-20-phase3-3-kokuhoren-csv.md \
        CLAUDE.md
git commit -m "docs(phase3-3/task 18/AC3-7): record acceptance and close open-questions"
```

---

## Self-Review

**1. Spec coverage:** spec §1-§13 の要件を各 Task にマップした。

- §3.1 レイヤ責務: Task 1 (Domain/App), Task 13 (Application), Task 2/4/5/6/7/8/9 (Csv writer)
- §3.2 データフロー: Task 13
- §3.3 コンポーネント表: 全 Task が該当
- §3.4 9 種 builder マッピング: Task 5 (Router), Task 7 (J111), Task 8 (J121), Task 9 (J611)
- §4 provider:* UI: Task 10 (schema), Task 11 (readiness), Task 12 (UI)
- §4.2 孤立 4 フィールド cross-field: Task 11
- §5 R8.6 サービスコード表: Task 3
- §6 fail-close: Task 2 (encoder), Task 13 (UseCase 例外 + 履歴非追記), Task 15 (UI メッセージ)
- §7 決定論: Task 16
- §8 テスト戦略: 全 Task の "Test" 節
- §9 マイグレーション: Task 1, Task 10
- §10 ADR / open-questions: Task 3, Task 18
- §11 受け入れ基準マッピング: Task 16 (AC3-7 バイト一致)
- §12 リスク緩和: Task 3 (R8.6 一次資料 TBD 明示), Task 12 (Expander), Task 13 (ログ)
- §13 着手順: Task 1-18 の順序に反映

**2. Placeholder scan:** 3 箇所の TBD を確認：
- Task 3 ADR 0031 の一次資料 URL・SHA256 は Task 3 実行時に埋める（`docs/decisions/0031-...` に "TBD" として残す方針が明示、Task 18 の open-questions クローズと連動）
- Task 5 RecordIdRouter の内部 stub コメント "// ... 各プロパティ ..." は builder 実装（Task 7-9）で埋める前提を明記
- Task 8 の J121:03 fieldId 表記 `provider:J121:03:xxx` は field-mapping-r7-10.json 参照とした

これらはすべて **他 Task で確定するもの** を指しており、単一 Task 内での placeholder ではない。plan failure ではない。

**3. Type consistency:** 全体を通じて：
- `ClaimCsvExport` — Domain record（Task 1）
- `IClaimCsvExportRepository` — Application interface（Task 1）
- `ClaimCsvExportRepository` — Infrastructure impl（Task 1）
- `CsvCell` / `CsvRow` — Writer VO（Task 2/4）
- `CsvEncodingException(FieldId, Reason, Detail)` — Writer 層（Task 2）
- `CsvEncodingReason` enum — Writer 層（Task 2）
- `IServiceCodeCatalog` / `JsonServiceCodeCatalog` / `ServiceCodeEntry` — Csv/ServiceCode（Task 3）
- `IProviderRecordBuilder<T>` / `ProviderRecordBuilderBase<T>` — Writer 抽象（Task 6）
- 9 種 Source DTO — Writer/Sources（Task 5 で shape、Task 7-9 で使用）
- `ExportClaimCsvUseCase.Execute(Guid, ProcessingMonth, CancellationToken)` — Application（Task 13）
- `ClaimCsvExportResult(Bytes, SuggestedFileName, Sha256)` — Application DTO（Task 13）
- `ClaimBatchNotFinalizedException(ClaimBatchId)` / `ClaimCsvReadinessException(MissingFieldIds)` / `ClaimCsvExportFailedException(RecordId, FieldId, Reason, RecipientReferenceCode)` — Application（Task 13）
- `CsvExportViewModel` — App（Task 15）
- `GoldenScenarioRunner` / `ClaimBatchSeeder` — テストヘルパー（Task 16）

すべて Task 1-15 で定義された型名・シグネチャが Task 16-18 の参照と一致する。

---

## Plan complete

Plan saved to `docs/superpowers/plans/2026-07-20-phase3-3-kokuhoren-csv.md`.

**Execution options:**

1. **Subagent-Driven（推奨）** — I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
