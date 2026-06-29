# Tsumugi Phase 3-3 実装計画 — CSV 生成＋請求確定

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3-0 で骨組みを置いた `Tsumugi.Infrastructure.Csv` アセンブリに、ADR 0023 で確定した国保連請求 CSV インターフェース仕様に基づく `ClaimCsvWriter` を実装する。`Recipient` を CSV 必須項目（性別等）で拡張し migration を発行する。`CloseClaimUseCase` で確定スナップショット（`ClaimBatch.New`、再実行は `Correct`、取下げは `Cancel`）を追記する。App に CSV エクスポート / 請求確定 View を追加。最後に `build/ci.sh` の Application カバレッジ閾値を 70 → 90 に昇格する。

**Architecture:** `Tsumugi.Infrastructure.Csv` 内に CSV カラム名 literal を閉じ込め、Phase 3-0 で導入した (b) スキャナで境界を機械判定。CSV はバイト単位スナップショットで固定（文字コード・改行・桁・項目順）。`CloseClaimUseCase` は Phase 3-1 の `ClaimCalculator` で算定 → `ClaimBatch` + `ClaimDetail` を追記する。再生成は同一 UseCase の再実行で `Correct` が積まれる。`AppendOnlyGuard` 違反は赤。

**Tech Stack:** .NET 10 / Avalonia 11.3 / EF Core 10 / `System.Text.Encoding`（Shift_JIS or UTF-8、ADR 0023 確定値）/ xUnit / FluentAssertions

## Global Constraints

> 親文書 `CLAUDE.md` §ハード制約、`01 §6`、`06_Phase3指示書 §4.3`、設計仕様書を尊守。

- **着手条件**: Phase 3-0/3-1/3-2 完了、ADR 0023 確定、`Recipient` 拡張内容（性別等）が CSV 仕様から確定済
- **CSV カラム名 literal は `Tsumugi.Infrastructure.Csv` 限定**: Phase 3-0 の (b) スキャナで機械判定済
- **文字コード/改行**: ADR 0023 確定値（Shift_JIS + CRLF or UTF-8 + CRLF）を `Tsumugi.Infrastructure.Csv/Encoding/` に閉じる
- **バイトスナップショット**: 同入力で同バイト（文字コード・改行・桁・項目順）。差分が説明可能であること
- **`ClaimBatch` の append-only**: 確定後の下層訂正で自動再生成しない。再算定は `CloseClaimUseCase` を明示再実行 → `Correct` レコード追加。取下げは `Cancel` レコード追加
- **`AppendOnlyGuard` 違反で赤**（意図的違反で歯あり性確認）
- **オフライン**: `Tsumugi.Infrastructure.Csv` は `System.Net.*` 直接参照禁止（Phase 3-0 のオフライン検査で機械判定済、変わらず緑）
- **Application カバレッジ 90% 達成 + `build/ci.sh` 閾値昇格 70 → 90**（本フェーズ完了条件）
- **TDD**: Red → Green → Refactor。コミットメッセージに `phase3-3/AC3-N`

## ファイル構成

```
src/Tsumugi.Infrastructure.Csv/
  ClaimCsvWriter.cs                                       拡張 — ADR 0023 の正式仕様に基づく実装
  Records/                                                 新規
    BasicInformationRecord.cs                             新規 — レコード種別1: 基本情報
    DetailRecord.cs                                       新規 — レコード種別2: 明細情報
    SummaryRecord.cs                                      新規 — レコード種別3: 集計情報
    ClaimCsvColumns.cs                                    新規 — カラム名 literal を集約（(b) スキャナの境界対象）
  Encoding/
    CsvEncoder.cs                                         新規 — Shift_JIS or UTF-8 / CRLF 規律
  Formatting/
    YenFormatter.cs                                       新規 — 整数円のゼロ埋め桁数等

src/Tsumugi.Domain/Entities/
  Recipient.cs                                            拡張 — Gender / 連絡先必須項目（ADR 0023 に応じて）

src/Tsumugi.Domain/Enums/
  Gender.cs                                               既存（変更なし、Phase 0/1 で導入済の場合）or 新規

src/Tsumugi.Application/
  Abstractions/
    ICsvClaimWriter.cs                                    既存（Phase 3-0 で配置済、不変）
  Dtos/Claim/
    ClaimCsvInputDto.cs                                   新規 — CSV 書出しに必要なデータ集約
  UseCases/Claim/
    ExportClaimCsvUseCase.cs                              新規
    CloseClaimUseCase.cs                                  新規

src/Tsumugi.Infrastructure/
  Migrations/
    <yyyyMMddHHmmss>_ExpandRecipientForCsv.cs             新規
  Persistence/Configurations/RecipientConfiguration.cs    改修 — 新規列のマッピング

src/Tsumugi.App/
  ViewModels/Claim/
    ClaimCsvExportViewModel.cs                            新規
    ClaimCloseViewModel.cs                                新規
  Views/Claim/
    ClaimCsvExportView.axaml(.cs)                         新規
    ClaimCloseView.axaml(.cs)                             新規

build/ci.sh                                                改修 — Application 閾値 70 → 90

tests/
  Tsumugi.Infrastructure.Csv.Tests/                       新規アセンブリ
    ClaimCsvWriterTests.cs                                バイト単位スナップショット
    BasicInformationRecordTests.cs
    DetailRecordTests.cs
    SummaryRecordTests.cs
    CsvEncoderTests.cs                                    Shift_JIS/UTF-8/CRLF 規律
  Tsumugi.Infrastructure.Tests/
    Persistence/
      RecipientCsvFieldsMigrationTests.cs                 新規 — 拡張 migration の往復
      RecipientCertificateConsistencyTests.cs             新規 — Certificate.RecipientGender との整合
  Tsumugi.Application.Tests/UseCases/Claim/
    ExportClaimCsvUseCaseTests.cs                         新規
    CloseClaimUseCaseTests.cs                             新規
  Tsumugi.App.Tests/ViewModels/Claim/
    ClaimCsvExportViewModelTests.cs                       新規
    ClaimCloseViewModelTests.cs                           新規
```

---

### Task 1: テスト用アセンブリ Tsumugi.Infrastructure.Csv.Tests 新設

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj`
- Modify: `Tsumugi.sln`

**Interfaces:**
- Produces: 以降のタスクで使うテストプロジェクト

- [ ] **Step 1: csproj を作る**

既存テストプロジェクト（`Tsumugi.Infrastructure.Reporting.Tests`）を雛形にコピー:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../../src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj" />
    <ProjectReference Include="../../src/Tsumugi.Application/Tsumugi.Application.csproj" />
    <ProjectReference Include="../../src/Tsumugi.Domain/Tsumugi.Domain.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Tsumugi.sln に追加**

```bash
dotnet sln add tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj
```

- [ ] **Step 3: ビルド緑確認 → コミット**

```bash
dotnet build -c Release
git add tests/Tsumugi.Infrastructure.Csv.Tests/ Tsumugi.sln
git commit -m "feat(phase3-3/AC3-7): scaffold Tsumugi.Infrastructure.Csv.Tests project"
```

---

### Task 2: ClaimCsvColumns — カラム名 literal の集約

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Records/ClaimCsvColumns.cs`

**Interfaces:**
- Produces: 国保連 CSV の全カラム名 literal を1ファイルに集約

ADR 0023 で確定した CSV 各カラム名を `static class` の `const string` として集約。Phase 3-0 の (b) スキャナはこのファイルが置かれているアセンブリ `Tsumugi.Infrastructure.Csv` 限定で literal 出現を許可する。

- [ ] **Step 1: ClaimCsvColumns を作る**

```csharp
namespace Tsumugi.Infrastructure.Csv.Records;

/// <summary>
/// 国保連 請求 CSV のカラム名 literal を集約。(b) スキャナで namespace 境界を機械判定。
/// 値は ADR 0023 で確定した仕様書版に従う。
/// </summary>
internal static class ClaimCsvColumns
{
    public const string RecordType        = "レコード種別";
    public const string ProvisionYearMonth = "サービス提供月";
    public const string OfficeNumber       = "事業所番号";
    public const string OfficeName         = "事業所名";
    public const string CertificateNumber  = "受給者証番号";
    public const string RecipientName      = "氏名";
    public const string RecipientGender    = "性別";
    public const string RecipientDateOfBirth = "生年月日";
    public const string ServiceCode        = "サービスコード";
    public const string UnitCount          = "単位数";
    public const string AmountYen          = "金額";
    public const string BurdenYen          = "利用者負担額";
    // ... ADR 0023 のカラム一覧をすべて列挙
}
```

> `internal` にすることでアセンブリ外からの参照を遮断（境界をより硬く）。

- [ ] **Step 2: ビルド緑確認**

```bash
dotnet build src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj -c Release
```

- [ ] **Step 3: (b) スキャナが緑のままであることを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "CsvLiteralNamespace" -v normal
```

Expected: PASS

- [ ] **Step 4: コミット**

```bash
git commit -m "feat(phase3-3/AC3-7): ClaimCsvColumns literal集約 in Tsumugi.Infrastructure.Csv"
```

---

### Task 3: CsvEncoder — 文字コード・改行規律

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Encoding/CsvEncoder.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/CsvEncoderTests.cs`

**Interfaces:**
- Produces: `CsvEncoder.WriteRow(Stream, IReadOnlyList<string>) → void`（CRLF + ADR 0023 確定の文字コードで 1 行書出し）
- Produces: `CsvEncoder.Encoding` プロパティ（実値は ADR 0023 で確定）

仕様で Shift_JIS が要求される場合: .NET 10 では `Encoding.GetEncoding("shift_jis")` のために `System.Text.Encoding.CodePages` パッケージが必要。csproj に追加。

- [ ] **Step 1: 失敗テストを書く（Shift_JIS / CRLF 仮定）**

```csharp
using FluentAssertions;
using System.Text;
using Tsumugi.Infrastructure.Csv.Encoding;

namespace Tsumugi.Infrastructure.Csv.Tests;

public sealed class CsvEncoderTests
{
    [Fact]
    public void WriteRow_emits_shift_jis_bytes_terminated_by_CRLF()
    {
        // 仮定: ADR 0023 で Shift_JIS + CRLF が確定したケース
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var sjis = Encoding.GetEncoding("shift_jis");

        using var ms = new MemoryStream();
        CsvEncoder.WriteRow(ms, new[] { "山田太郎", "1234567890" });
        var bytes = ms.ToArray();

        var expected = sjis.GetBytes("山田太郎,1234567890").Concat(new byte[] { 0x0D, 0x0A }).ToArray();
        bytes.Should().Equal(expected);
    }

    [Fact]
    public void WriteRow_escapes_double_quotes_per_RFC4180()
    {
        // CSV 仕様で文字列中の `"` をエスケープする要件があれば
        using var ms = new MemoryStream();
        CsvEncoder.WriteRow(ms, new[] { "テス\"ト" });
        // 期待: "テス""ト" の Shift_JIS バイト + CRLF
    }
}
```

- [ ] **Step 2: テスト赤確認 → 実装**

`src/Tsumugi.Infrastructure.Csv/Tsumugi.Infrastructure.Csv.csproj` に依存追加:

```xml
<ItemGroup>
  <PackageReference Include="System.Text.Encoding.CodePages" Version="*" />
</ItemGroup>
```

`src/Tsumugi.Infrastructure.Csv/Encoding/CsvEncoder.cs`:

```csharp
using System.Text;

namespace Tsumugi.Infrastructure.Csv.Encoding;

/// <summary>
/// 国保連 CSV の文字コード・改行規律（ADR 0023）。
/// </summary>
internal static class CsvEncoder
{
    static CsvEncoder()
    {
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static System.Text.Encoding Encoding { get; } = System.Text.Encoding.GetEncoding("shift_jis");

    public static void WriteRow(Stream output, IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var quoted = fields.Select(EscapeField);
        var line = string.Join(",", quoted);
        var bytes = Encoding.GetBytes(line);
        output.Write(bytes, 0, bytes.Length);
        output.WriteByte(0x0D);
        output.WriteByte(0x0A);
    }

    private static string EscapeField(string s)
    {
        if (s.Contains('"') || s.Contains(',') || s.Contains('\r') || s.Contains('\n'))
        {
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        return s;
    }
}
```

- [ ] **Step 3: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj --filter "CsvEncoderTests" -v normal
git add src/Tsumugi.Infrastructure.Csv/ tests/Tsumugi.Infrastructure.Csv.Tests/CsvEncoderTests.cs
git commit -m "feat(phase3-3/AC3-7): CsvEncoder (Shift_JIS + CRLF per ADR 0023)"
```

---

### Task 4: BasicInformationRecord（レコード種別 1）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Records/BasicInformationRecord.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/BasicInformationRecordTests.cs`

**Interfaces:**
- Produces: `BasicInformationRecord.WriteTo(Stream, ClaimBatch batch, OfficeInfo office) → void`

ADR 0023 確定値に従い「基本情報レコード」（事業所情報・対象月など）を書出し。

- [ ] **Step 1〜N: TDD で実装 + バイトスナップショットテスト**

```csharp
[Fact]
public void WriteTo_produces_exact_byte_sequence_for_basic_information()
{
    var batch = ClaimBatch.CreateNew(/* 固定値 */);
    var office = new OfficeInfo("テスト事業所", "0123456789");

    using var ms = new MemoryStream();
    BasicInformationRecord.WriteTo(ms, batch, office);

    var expected = /* Shift_JIS で "1,202605,0123456789,テスト事業所" + CRLF を期待 */;
    ms.ToArray().Should().Equal(expected);
}
```

固定の期待値バイト列はテスト内に直接書く（テストデータ管理を分散させない）。

- [ ] **Step N+1: コミット**

```bash
git commit -m "feat(phase3-3/AC3-7): BasicInformationRecord (record type 1)"
```

---

### Task 5: DetailRecord（レコード種別 2）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Records/DetailRecord.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/DetailRecordTests.cs`

**Interfaces:**
- Produces: `DetailRecord.WriteTo(Stream, ClaimBatch, ClaimDetail, RecipientInfo) → void`

明細レコード（受給者×明細行）。

- [ ] **Step 1〜N: TDD で実装 + バイトスナップショットテスト**

```bash
git commit -m "feat(phase3-3/AC3-7): DetailRecord (record type 2)"
```

---

### Task 6: SummaryRecord（レコード種別 3）

**Files:**
- Create: `src/Tsumugi.Infrastructure.Csv/Records/SummaryRecord.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/SummaryRecordTests.cs`

集計レコード（事業所×月の合計）。

- [ ] **Step 1〜N: TDD で実装 + バイトスナップショットテスト**

```bash
git commit -m "feat(phase3-3/AC3-7): SummaryRecord (record type 3)"
```

---

### Task 7: ClaimCsvWriter — レコード組み立て＋全体スナップショット

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Csv/ClaimCsvWriter.cs`
- Create: `tests/Tsumugi.Infrastructure.Csv.Tests/ClaimCsvWriterTests.cs`

**Interfaces:**
- 実装: `ICsvClaimWriter.Write(Stream output, ClaimBatch batch, IReadOnlyList<ClaimDetail> details, IReadOnlyDictionary<Guid, RecipientInfo> recipients, OfficeInfo office) → void`

  実装上は `ICsvClaimWriter.Write` のシグネチャに `recipients`/`office` 引数を追加する必要があるため、Phase 3-0 の interface を**拡張**する（破壊変更だが利用者は内部のみ、UseCase 側で対応）。

- [ ] **Step 1: ICsvClaimWriter のシグネチャ拡張**

`src/Tsumugi.Application/Abstractions/ICsvClaimWriter.cs`:

```csharp
public interface ICsvClaimWriter
{
    void Write(
        Stream output,
        ClaimBatch batch,
        IReadOnlyList<ClaimDetail> details,
        IReadOnlyDictionary<Guid, RecipientInfo> recipients,
        OfficeInfo office);
}

public sealed record RecipientInfo(
    string KanjiName, string CertificateNumber, Gender Gender, DateOnly DateOfBirth);

public sealed record OfficeInfo(string OfficeName, string OfficeNumber);
```

- [ ] **Step 2: ClaimCsvWriter を実装**

```csharp
public sealed class ClaimCsvWriter : ICsvClaimWriter
{
    public void Write(
        Stream output, ClaimBatch batch, IReadOnlyList<ClaimDetail> details,
        IReadOnlyDictionary<Guid, RecipientInfo> recipients, OfficeInfo office)
    {
        BasicInformationRecord.WriteTo(output, batch, office);
        foreach (var d in details)
            DetailRecord.WriteTo(output, batch, d, recipients[d.RecipientId]);
        SummaryRecord.WriteTo(output, batch);
    }
}
```

- [ ] **Step 3: 全体スナップショットテストを書く**

```csharp
public sealed class ClaimCsvWriterTests
{
    [Fact]
    public void Write_emits_record1_then_records2_then_record3_with_exact_bytes()
    {
        var w = new ClaimCsvWriter();
        var batch = /* 固定 ClaimBatch */;
        var details = new[] { /* 固定 ClaimDetail x 2 */ };
        var recipients = new Dictionary<Guid, RecipientInfo> { /* ... */ };
        var office = new OfficeInfo("テスト事業所", "0123456789");

        using var ms = new MemoryStream();
        w.Write(ms, batch, details, recipients, office);
        var actual = ms.ToArray();

        // 期待バイト列を直接書く（Shift_JIS で計算した bytes をテスト内で構築）
        var expected = new List<byte>();
        // BasicInformationRecord
        expected.AddRange(/* ... */);
        // DetailRecord x 2
        expected.AddRange(/* ... */);
        // SummaryRecord
        expected.AddRange(/* ... */);
        actual.Should().Equal(expected);
    }
}
```

- [ ] **Step 4: テスト緑確認 → コミット**

```bash
dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj --filter "ClaimCsvWriter" -v normal
git add src/Tsumugi.Application/Abstractions/ICsvClaimWriter.cs src/Tsumugi.Infrastructure.Csv/ tests/Tsumugi.Infrastructure.Csv.Tests/ClaimCsvWriterTests.cs
git commit -m "feat(phase3-3/AC3-7): ClaimCsvWriter assembles records with byte snapshot"
```

---

### Task 8: Recipient 拡張 — 性別・必須項目追加

**Files:**
- Modify: `src/Tsumugi.Domain/Entities/Recipient.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/Configurations/RecipientConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<auto>_ExpandRecipientForCsv.cs`
- Modify: `tests/Tsumugi.Domain.Tests/Entities/RecipientTests.cs`（既存。新フィールドのテスト追加）
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/RecipientCsvFieldsMigrationTests.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/RecipientCertificateConsistencyTests.cs`

**Interfaces:**
- Consumes: ADR 0023 の Recipient 必須項目
- Produces: `Recipient.Gender` プロパティ（と ADR 0023 で確定したその他必須項目）

- [ ] **Step 1: Recipient.Gender などのフィールドを追加（テスト先行）**

`tests/Tsumugi.Domain.Tests/Entities/RecipientTests.cs` に新フィールドのテスト追加:

```csharp
[Fact]
public void Create_holds_gender_per_ADR_0023()
{
    var r = Recipient.Create(
        id: Guid.NewGuid(),
        kanjiName: "山田太郎", kanaName: "ヤマダタロウ",
        dateOfBirth: new DateOnly(1990, 1, 1),
        createdBy: "u1", createdAt: DateTimeOffset.UtcNow,
        concurrencyToken: Guid.NewGuid(),
        gender: Gender.Male);
    r.Gender.Should().Be(Gender.Male);
}
```

- [ ] **Step 2: Recipient に Gender プロパティを追加**

```csharp
public sealed record Recipient : Entity
{
    // 既存
    public required string KanjiName { get; init; }
    public required string KanaName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    // ...

    // 新規
    public Gender Gender { get; init; } = Gender.Unspecified;

    public static Recipient Create(
        // ... 既存 args
        Gender gender = Gender.Unspecified) =>
        new()
        {
            // ... 既存
            Gender = gender,
        };
}
```

- [ ] **Step 3: RecipientConfiguration を更新**

```csharp
public void Configure(EntityTypeBuilder<Recipient> b)
{
    // ... 既存
    b.Property(x => x.Gender).HasConversion<int>().IsRequired();
}
```

- [ ] **Step 4: Migration 生成**

```bash
dotnet ef migrations add ExpandRecipientForCsv \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

migration ファイルで `AddColumn` が `Gender` 列を追加することを目視確認。既存データへの default 値 `Gender.Unspecified = 0` を `defaultValue: 0` で指定。

- [ ] **Step 5: Migration 往復テスト**

```csharp
public sealed class RecipientCsvFieldsMigrationTests
{
    [Fact]
    public async Task ExpandRecipientForCsv_migration_round_trips()
    {
        // Up → SQLite ファイル作成 → Recipient テーブルに Gender 列存在 → Down → 列削除
    }
}
```

- [ ] **Step 6: Certificate との整合テスト**

```csharp
public sealed class RecipientCertificateConsistencyTests
{
    [Fact]
    public void Recipient_Gender_aligns_with_Certificate_RecipientGender_when_both_set()
    {
        // 同一受給者で Recipient.Gender と Certificate.RecipientGender が一致すべきという業務ルール
        // 不一致を検出する純粋関数 ValidateRecipientCertificateConsistency を Domain に追加
    }
}
```

> 整合性チェックは Domain `Logic/` 配下に純粋関数として追加（例: `RecipientCertificateConsistencyValidator`）。

- [ ] **Step 7: テスト緑確認 → コミット**

```bash
git add src/Tsumugi.Domain/ src/Tsumugi.Infrastructure/ tests/
git commit -m "feat(phase3-3/AC3-8): expand Recipient with Gender per ADR 0023 + migration + consistency tests"
```

---

### Task 9: CloseClaimUseCase — 請求確定スナップショット追記

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/UseCases/Claim/CloseClaimUseCaseTests.cs`

**Interfaces:**
- Consumes: `ClaimCalculator`（Phase 3-1）、`IClaimBatchRepository`、`IUnitOfWork`、各リポジトリ、`IAuditTrail`、`TimeProvider`
- Produces: 
  - `CloseClaimUseCase.ExecuteAsync(Guid officeId, YearMonth ym, string actor, CancellationToken) → Task<CloseClaimResult>`
  - `CloseClaimUseCase.CancelAsync(Guid officeId, YearMonth ym, string actor, CancellationToken) → Task<CloseClaimResult>`（取下げ＝Cancel）
  - `CloseClaimResult = (Guid BatchId, RecordKind Kind, int RecordsBefore)`

ロジック:
1. `ClaimBatchRepository.GetByOfficeAndMonthAsync` で既存履歴取得
2. 既存 `New` が無い → `ClaimBatch.CreateNew(...)` を追記 → partial unique index で重複拒否（並行確定の防止）
3. 既存 `New` あり → `ClaimBatch.CreateCorrect(originId: 初代 New, ...)` を追記
4. `CancelAsync` は `ClaimBatch.CreateCancel(originId: 直近 New/Correct, ...)` を追記
5. すべて `IAuditTrail.RecordAsync` で監査ログ追記

- [ ] **Step 1: 失敗テストを書く**

```csharp
public sealed class CloseClaimUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_creates_New_when_no_prior_batch_exists()
    {
        // モック リポジトリ + InMemory マスタ + FakeTimeProvider
        var result = await uc.ExecuteAsync(officeId, ym, "u1", default);
        result.Kind.Should().Be(RecordKind.New);
        result.RecordsBefore.Should().Be(0);
        // ClaimBatch が AddBatchAsync で追記されたことを Verify
    }

    [Fact]
    public async Task ExecuteAsync_creates_Correct_when_New_already_exists()
    {
        // 事前に New を 1 件返すリポジトリモック
        var result = await uc.ExecuteAsync(officeId, ym, "u1", default);
        result.Kind.Should().Be(RecordKind.Correct);
        result.RecordsBefore.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_records_audit_with_actor()
    {
        // IAuditTrail モックの呼出を検証
    }

    [Fact]
    public async Task CancelAsync_creates_Cancel_linked_to_latest_New_or_Correct()
    {
        var result = await uc.CancelAsync(officeId, ym, "u1", default);
        result.Kind.Should().Be(RecordKind.Cancel);
    }
}
```

- [ ] **Step 2: テスト赤確認 → 実装**

```csharp
public sealed class CloseClaimUseCase(
    CalculateClaimUseCase calc,
    IClaimBatchRepository batchRepo,
    IUnitOfWork uow,
    IAuditTrail audit,
    TimeProvider clock,
    IRateMaster rateMaster,
    IAdditionMaster additionMaster,
    IBurdenCapMaster burdenCapMaster,
    IRegionUnitMaster regionMaster)
{
    public async Task<CloseClaimResult> ExecuteAsync(Guid officeId, YearMonth ym, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor required", nameof(actor));

        var prior = await batchRepo.GetByOfficeAndMonthAsync(officeId, ym, ct);
        var hasNew = prior.Any(b => b.Kind == RecordKind.New);
        var originId = hasNew ? prior.First(b => b.Kind == RecordKind.New).Id : (Guid?)null;

        var calculated = await calc.ExecuteAsync(officeId, ym, ct);
        var batchId = Guid.NewGuid();

        var batch = hasNew
            ? ClaimBatch.CreateCorrect(
                id: batchId, originId: originId!.Value,
                officeId: officeId, yearMonth: ym,
                totalUnit: calculated.TotalUnit,
                totalAmountYen: calculated.TotalAmountYen,
                totalBurdenYen: calculated.TotalBurdenYen,
                ratesVersion: rateMaster.Version, additionsVersion: additionMaster.Version,
                burdenCapsVersion: burdenCapMaster.Version, regionUnitsVersion: regionMaster.Version,
                createdBy: actor, createdAt: clock.GetUtcNow(), concurrencyToken: Guid.NewGuid())
            : ClaimBatch.CreateNew(
                id: batchId, officeId: officeId, yearMonth: ym,
                totalUnit: calculated.TotalUnit,
                totalAmountYen: calculated.TotalAmountYen,
                totalBurdenYen: calculated.TotalBurdenYen,
                ratesVersion: rateMaster.Version, additionsVersion: additionMaster.Version,
                burdenCapsVersion: burdenCapMaster.Version, regionUnitsVersion: regionMaster.Version,
                createdBy: actor, createdAt: clock.GetUtcNow(), concurrencyToken: Guid.NewGuid());

        var details = MapDetailsToEntities(batchId, calculated, actor, clock);
        await batchRepo.AddBatchAsync(batch, details, ct);
        await audit.RecordAsync(actor, AuditAction.Update, "ClaimBatch", batchId.ToString(), $"{batch.Kind} for office={officeId} month={ym.ToInt()}", ct);
        await uow.SaveChangesAsync(ct);

        return new CloseClaimResult(batchId, batch.Kind, prior.Count);
    }

    public async Task<CloseClaimResult> CancelAsync(Guid officeId, YearMonth ym, string actor, CancellationToken ct)
    {
        var prior = await batchRepo.GetByOfficeAndMonthAsync(officeId, ym, ct);
        if (prior.Count == 0)
            throw new InvalidOperationException("Cancel 対象の確定がありません");
        var origin = prior.First(b => b.Kind == RecordKind.New);
        var batchId = Guid.NewGuid();
        var cancel = ClaimBatch.CreateCancel(
            id: batchId, originId: origin.Id, officeId: officeId, yearMonth: ym,
            ratesVersion: rateMaster.Version, additionsVersion: additionMaster.Version,
            burdenCapsVersion: burdenCapMaster.Version, regionUnitsVersion: regionMaster.Version,
            createdBy: actor, createdAt: clock.GetUtcNow(), concurrencyToken: Guid.NewGuid());
        await batchRepo.AddBatchAsync(cancel, Array.Empty<ClaimDetail>(), ct);
        await audit.RecordAsync(actor, AuditAction.Update, "ClaimBatch", batchId.ToString(), $"Cancel for office={officeId} month={ym.ToInt()}", ct);
        await uow.SaveChangesAsync(ct);
        return new CloseClaimResult(batchId, RecordKind.Cancel, prior.Count);
    }

    private static IReadOnlyList<ClaimDetail> MapDetailsToEntities(Guid batchId, ClaimResultDto r, string actor, TimeProvider clock) =>
        r.Recipients.SelectMany(rc => rc.Lines.Select(l =>
            ClaimDetail.Create(
                id: Guid.NewGuid(),
                claimBatchId: batchId,
                recipientId: rc.RecipientId,
                lineKind: l.LineKind,
                code: l.Code,
                unit: l.Unit,
                amountYen: l.AmountYen,
                createdBy: actor, createdAt: clock.GetUtcNow(), concurrencyToken: Guid.NewGuid()))).ToList();
}

public sealed record CloseClaimResult(Guid BatchId, RecordKind Kind, int RecordsBefore);
```

- [ ] **Step 3: テスト緑確認 → コミット**

```bash
git commit -m "feat(phase3-3/AC3-9): CloseClaimUseCase (New / Correct / Cancel)"
```

---

### Task 10: ExportClaimCsvUseCase

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/ExportClaimCsvUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/UseCases/Claim/ExportClaimCsvUseCaseTests.cs`

**Interfaces:**
- Consumes: `IClaimBatchRepository`、`ICsvClaimWriter`、`IRecipientRepository`、`IOfficeRepository`
- Produces: `ExportClaimCsvUseCase.ExecuteAsync(Guid officeId, YearMonth ym, Stream output, CancellationToken) → Task`

確定済 `ClaimBatch` から CSV を生成。未確定の場合はエラー（推測しない）。

- [ ] **Step 1〜N: TDD で実装**

```csharp
public async Task ExecuteAsync(Guid officeId, YearMonth ym, Stream output, CancellationToken ct)
{
    var batch = await batchRepo.GetEffectiveBatchAsync(officeId, ym, ct)
        ?? throw new InvalidOperationException("当該月の確定済請求がありません。先に CloseClaimUseCase を実行してください。");
    var details = await batchRepo.GetDetailsAsync(batch.Id, ct);
    var office = await officeRepo.GetByIdAsync(officeId, ct) ?? throw ...;
    var recipientIds = details.Select(d => d.RecipientId).Distinct().ToList();
    var recipients = await recipientRepo.GetMapAsync(recipientIds, ct);
    var certs = await certRepo.GetEffectiveMapForMonthAsync(ym, ct);

    var recipientInfo = recipients.ToDictionary(
        kv => kv.Key,
        kv => new RecipientInfo(
            kv.Value.KanjiName,
            certs[kv.Key].CertificateNumber,
            kv.Value.Gender,
            kv.Value.DateOfBirth));

    writer.Write(output, batch, details, recipientInfo, new OfficeInfo(office.OfficeName, office.OfficeNumber));
}
```

- [ ] **Step N+1: コミット**

```bash
git commit -m "feat(phase3-3/AC3-7): ExportClaimCsvUseCase"
```

---

### Task 11: App ViewModel + View — CSV エクスポート

**Files:**
- Create: `src/Tsumugi.App/ViewModels/Claim/ClaimCsvExportViewModel.cs`
- Create: `src/Tsumugi.App/Views/Claim/ClaimCsvExportView.axaml(.cs)`
- Create: `tests/Tsumugi.App.Tests/ViewModels/Claim/ClaimCsvExportViewModelTests.cs`

**Interfaces:**
- Consumes: `ExportClaimCsvUseCase`、`IFileSaveService`、`IClaimBatchRepository`、`ListOfficesUseCase`

- [ ] **Step 1〜N: TDD で ViewModel 実装**

確定済 ClaimBatch がある (Office, YearMonth) 一覧を取得 → 選択 → 保存先指定 → エクスポート。出典版（マスタ版 ADR 番号）を画面に表示。

```csharp
public sealed partial class ClaimCsvExportViewModel : ObservableObject
{
    [ObservableProperty] private YearMonth selectedYearMonth = new(2026, 5);
    [ObservableProperty] private OfficeDto? selectedOffice;
    [ObservableProperty] private string masterVersionDisplay = "";
    public ObservableCollection<OfficeDto> Offices { get; } = new();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct) { /* ... */ }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(CancellationToken ct)
    {
        var defaultName = $"claim-{SelectedOffice!.OfficeNumber}-{SelectedYearMonth.ToInt()}.csv";
        var path = await fileSave.PickSaveFileAsync(defaultName, ".csv", ct);
        if (path is null) return;
        await using var fs = File.Create(path);
        await export.ExecuteAsync(SelectedOffice.Id, SelectedYearMonth, fs, ct);
    }

    private bool CanExport() => SelectedOffice is not null;
}
```

- [ ] **Step N+1: ClaimCsvExportView.axaml**

Phase 2 既存 View を参考に組み立て。

- [ ] **Step N+2: コミット**

```bash
git commit -m "feat(phase3-3/AC3-7): ClaimCsvExportView + ViewModel"
```

---

### Task 12: App ViewModel + View — 請求確定

**Files:**
- Create: `src/Tsumugi.App/ViewModels/Claim/ClaimCloseViewModel.cs`
- Create: `src/Tsumugi.App/Views/Claim/ClaimCloseView.axaml(.cs)`
- Create: `tests/Tsumugi.App.Tests/ViewModels/Claim/ClaimCloseViewModelTests.cs`

**Interfaces:**
- Consumes: `CloseClaimUseCase`、`QueryClaimUseCase`、`ListOfficesUseCase`

- [ ] **Step 1〜N: ViewModel 実装**

```csharp
public sealed partial class ClaimCloseViewModel : ObservableObject
{
    [ObservableProperty] private YearMonth selectedYearMonth;
    [ObservableProperty] private OfficeDto? selectedOffice;
    [ObservableProperty] private ClaimResultDto? preview;
    [ObservableProperty] private bool hasPriorClose;
    public ObservableCollection<OfficeDto> Offices { get; } = new();

    public string ActionLabel => HasPriorClose ? "再確定（Correct を追記）" : "確定";

    [RelayCommand]
    private async Task LoadPreviewAsync(CancellationToken ct)
    {
        if (SelectedOffice is null) return;
        var q = await queryUseCase.ExecuteAsync(SelectedOffice.Id, SelectedYearMonth, ct);
        Preview = q.Result;
        HasPriorClose = q.IsFromBatch;
    }

    [RelayCommand(CanExecute = nameof(CanClose))]
    private async Task CloseAsync(CancellationToken ct)
    {
        if (SelectedOffice is null) return;
        if (HasPriorClose)
        {
            // 確認ダイアログ：Correct を追記する旨を明示
            // 実装簡素化のため YesNo ダイアログを既存 IDialogService 経由で表示。
            // ユーザーが No なら return。
        }
        var actor = Environment.UserName;
        var result = await closeUseCase.ExecuteAsync(SelectedOffice.Id, SelectedYearMonth, actor, ct);
        // 結果通知
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelAsync(CancellationToken ct) { /* ... */ }

    private bool CanClose() => SelectedOffice is not null && Preview is not null;
    private bool CanCancel() => SelectedOffice is not null && HasPriorClose;
}
```

- [ ] **Step N+1: View (axaml)**

プレビュー表示（明細・利用者負担・マスタ版 ADR 番号）+ 確定ボタン + 取下げボタン + ステータスバー。

- [ ] **Step N+2: コミット**

```bash
git commit -m "feat(phase3-3/AC3-9): ClaimCloseView + ViewModel"
```

---

### Task 13: build/ci.sh Application 閾値昇格 70 → 90

**Files:**
- Modify: `build/ci.sh`

**Interfaces:**
- Produces: CI で Application カバレッジが 90% 未満なら赤

- [ ] **Step 1: 現状カバレッジ計測**

```bash
dotnet test tests/Tsumugi.Application.Tests -c Release \
  -p:CollectCoverage=true \
  -p:Include="[Tsumugi.Application]*" \
  -p:Threshold=90 \
  -p:ThresholdType=line
```

Expected: PASS（Phase 3-1 Task 15 で実質 90% に到達済の前提）。未達なら個別 UseCase テストを追加して埋める。

- [ ] **Step 2: ci.sh の閾値を変更**

```diff
- dotnet test tests/Tsumugi.Application.Tests -c Release \
-   -p:CollectCoverage=true \
-   -p:Include="[Tsumugi.Application]*" \
-   -p:Threshold=70 \
+ dotnet test tests/Tsumugi.Application.Tests -c Release \
+   -p:CollectCoverage=true \
+   -p:Include="[Tsumugi.Application]*" \
+   -p:Threshold=90 \
    -p:ThresholdType=line \
    -p:ThresholdStat=total
```

- [ ] **Step 3: ci.sh 緑確認**

```bash
./build/ci.sh
```

Expected: PASS

- [ ] **Step 4: コミット**

```bash
git add build/ci.sh
git commit -m "ci(phase3-3/AC3-3-add): raise Application coverage threshold 70 → 90"
```

---

### Task 14: open-questions.md / CHANGELOG / 受け入れ確認

**Files:**
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Modify: `README.md`
- Create: `docs/phase3-3-acceptance.md`
- Create: `docs/phase3-acceptance.md`（Phase 3 全体サマリ）

- [ ] **Step 1: open-questions.md 最終化**

Phase 3 で解消したすべての項目を `[x]`、新規未確定（運用ガイド・自動返戻処理等）を整理。

- [ ] **Step 2: docs/phase3-3-acceptance.md 作成 + Phase 3 全体サマリ**

AC3-7 / AC3-8 / AC3-9 / AC3-3-add / AC3-10 の達成根拠を列挙。Phase 3 全体（3-0/3-1/3-2/3-3）の総合サマリを `docs/phase3-acceptance.md` に。

- [ ] **Step 3: CHANGELOG.md / README.md 更新**

Phase 3 完了を追記。新規アセンブリ `Tsumugi.Infrastructure.Csv`、`ClaimBatch` の追記型、新規 ADR 0018〜0026 へのリンク、Application カバレッジ 90% 達成、Domain Logic.Claim 100% 目標達成を記録。

- [ ] **Step 4: 歯あり性最終確認**

(a)(b)(c) スキャナ・`AppendOnlyGuard` 違反・partial unique index 重複拒否のそれぞれを意図的違反で赤になることを目視確認（または CI ログ確認）。

- [ ] **Step 5: macOS/Windows 両方で手動 QA**

- 月選択 → プレビュー → 確定 → 再実行で Correct 追記
- 取下げで Cancel 追記
- CSV エクスポート → 保存したファイルを開いて Shift_JIS デコードで内容確認
- 帳票 3 種出力 → CJK 化けなし
- 6 級地等の地域区分単価で算定結果が変わる

- [ ] **Step 6: コミット**

```bash
git commit -m "docs(phase3-3): Phase 3-3 + Phase 3 overall acceptance complete"
```

---

## Phase 3-3 全体受け入れ基準

- [ ] AC3-7 請求 CSV が ADR 0023 準拠でバイトスナップショット緑、カラム名 literal が `Tsumugi.Infrastructure.Csv` 限定、オフライン検査対象
- [ ] AC3-8 Recipient CSV 必須項目拡張＋migration、Certificate.RecipientGender と整合
- [ ] AC3-9 請求確定が append-only スナップショット、確定後の下層訂正で自動再生成しない、再生成は Correct、取下げは Cancel。`AppendOnlyGuard` 登録、partial unique index、歯あり性
- [ ] AC3-3-add `build/ci.sh` Application 閾値 70 → 90
- [ ] AC3-10（横断）`./build/ci.sh` 緑、依存方向不変、オフライン検査緑（App/Reporting/Csv）、Domain 95% / Application 90% / Domain.Logic.Claim 100% 目標

## 参考

- 設計仕様書: `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`
- Phase 3 指示書 §4.3
- Phase 3-0 計画（土台）/ 3-1 計画（算定）/ 3-2 計画（帳票）
- Phase 1/2 の append-only 前例: ADR 0015/0017、`docs/superpowers/plans/2026-06-29-m3-wagefund-duplicate-new-index.md`
