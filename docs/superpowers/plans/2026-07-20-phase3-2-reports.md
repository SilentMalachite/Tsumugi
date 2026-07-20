# Phase 3-2 実装計画 — snapshot v2 + 3帳票

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ClaimPreparation` 画面から「入力補完 → プレビュー → 確定 → 帳票出力」を1画面で完結させ、`ClaimSnapshotValidationCodecV2` が凍結した snapshot だけを入力に3帳票 PDF（実績記録票／請求書／請求明細書）を決定論的に出力する。`docs/phase3-claim-field-mapping.md` の21 `report:*` フィールドを全て snapshot v2 に凍結し、DB上のモデルが空なら readiness gate で fail-closed。

**Architecture:** Phase 3-1 vertical slice で作った `ClaimBatch`/`ClaimDetail`/`IClaimBatchRepository`/`CloseClaimUseCase` はそのまま活用し、既存 `ClaimSnapshotValidationCodecV1` を破壊的置換で v2 に更新。確定時に新規 `OperationLocalSnapshotReader` が Office/Recipient/Certificate/DailyRecord/ClaimInput/IntensiveSupportEpisode を集約して v2 payload に凍結。帳票 UseCase は `IClaimBatchRepository` だけを参照し、`IClaimReportGenerator` (QuestPDF) は payload の parse 結果だけを描画する。UI は既存 View に 21 フィールドの入力欄を露出し、`ClaimPreparationView` に「帳票出力」セクションを追加。

**Tech Stack:** .NET 10 / C# 14, Avalonia 11 + CommunityToolkit.Mvvm, EF Core 10 + SQLite, xUnit + FluentAssertions, QuestPDF 2025.4.0 (Community License, Noto Sans JP 埋込済), UglyToad.PdfPig

**正本spec:** `docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md`

## Global Constraints

> 親文書 `CLAUDE.md` §ハード制約、`01 §6`、`06_Phase3指示書 §4.2`、正本spec §3〜§14 を遵守。

- **依存方向**: `App → Application → Domain`、`Infrastructure → Application/Domain`。Domain は Infrastructure/UI/EF/Avalonia を一切知らない。`Tsumugi.Infrastructure.Reporting → Application/Domain` のみ
- **オフライン**: 全プロダクションアセンブリで `System.Net.*` 使用を検出・禁止（`OfflineComplianceTests` で機械判定）。`Tsumugi.Infrastructure.Reporting` も対象
- **snapshot 入力単一化**: 帳票 generator と UseCase は `IClaimBatchRepository` だけを参照し、`IOfficeRepository`/`IRecipientRepository`/`ICertificateRepository`/`IDailyRecordRepository` を触らない
- **決定論**: 同 v2 payload + 同 `TimeProvider` → 同バイト（`bytes1.Should().Equal(bytes2)`）
- **CJK 検証**: 抽出テキストは `KangxiRadicalNormalizer.FoldKangxiRadicals` 経由で assert
- **codec v1 破壊的置換**: v1 実装は削除、backwards-compat hack 禁止。production DB に確定 revision が存在しない前提で進める
- **報酬告示・CSV仕様の外部化継続**: 単位数/加算/地域単価/CSVフィールドをハードコードしない（Phase 3-1 で確立済）
- **金額整形**: `InvariantCulture` で3桁区切り、整数円。`double`/`float` を使わない
- **記録は追記型**: 訂正 revision は独立 payload を持ち、過去 revision の帳票は不変
- **アクセシビリティ**: 既存 Phase 1 `AccessibilityDefaults`（ダークテーマ・低アニ・広い余白/行間・キーボード操作）踏襲
- **TDD**: Red → Green → Refactor。1コミット=1論理変更。コミットメッセージに `phase3-2/AC3-N` または `phase3-2/task N` を含める
- **ログ**: 氏名・受給者証番号・保存先フルパスを出さない
- **`./build/ci.sh` 緑**: format / build 警告ゼロ / 全テスト / Domain ≧ 95% / Application ≧ 70%（Phase 3-1 到達値を維持）
- **凍結領域（Phase 3-1継承）**: 保護施設・R8-06 新12区分・R8-06 処遇改善加算率・band-participation option 10 は snapshot v2 スキーマに含めない、fail-closed 継続

## 既存コードの前提（2026-07-20調査で確定した契約）

- `Office`, `DailyRecord`, `Certificate` は既に spec §3.1 の21 report フィールド全てを持つ（migration 不要）
- `ClaimInput.UpperLimitManagementResult` / `UpperLimitManagedAmountYen` / `MunicipalSubsidyAmountYen` / `ExceptionalUsage*` / `StandardUsageDayTotal` は Phase 3-1 Task 12 で実装済（model + migration 完了）
- `IntensiveSupportEpisode` は `src/Tsumugi.Domain/Entities/IntensiveSupportEpisode.cs` に存在
- `ClaimBatch` / `ClaimDetail` / `ClaimBatchAggregate` / `IClaimBatchRepository` / `ClaimBatchRepository` は Phase 3-1 で実装済
- `ClaimSnapshotValidationCodecV1` / `ClaimRecipientSnapshotWriter` / `ValidatedClaimSnapshotEnvelope` は Phase 3-1 で実装済。v2 で置換
- `CloseClaimUseCase` は `IClaimCalculationSnapshotReader` / `ClaimPreparationReadiness` / `IClaimBatchRepository` / `IClaimFinalizationStore` を組み合わせて確定を行う。Task 3 で `OperationLocalSnapshotReader` を注入して v2 payload に置換
- `IClaimInputRequirementProvider` は `src/Tsumugi.Application/Abstractions/IClaimInputRequirementProvider.cs`、実装は `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`（21 report フィールドの readiness ルールを追加する対象）
- App の既存 View: `OfficeView.axaml` / `CertificateView.axaml` / `DailyRecordView.axaml` / `ClaimInputView.axaml` / `ClaimPreparationView.axaml`（21 フィールドは models にはあるが UI に露出されていないものが多く、Task 4-8 で追加）
- `QuestPdfLicenseConfigurator.Initialize()` は Phase 4/S1 で完成、Noto Sans JP 埋込済
- 帳票の reference implementation は `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`。テストヘルパは `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs` の `ExtractText`（`KangxiRadicalNormalizer` 経由）
- `IFileSaveService` は Phase 2 で確立済（`PickSaveFileAsync(defaultName, extension, ct)`）

## ファイル構成マップ

### 新規作成

```
src/Tsumugi.Application/
  Claim/
    ClaimSnapshotValidationCodecV2.cs                 codec v2 本体
    ClaimFinalizationSnapshotWriter.cs                v2 finalization payload の canonical JSON writer
    ClaimFinalizationSnapshotReader.cs                v2 finalization payload の parser（DTO 復元）
    ClaimFinalizationSnapshot.cs                      parse 結果 DTO（21 フィールド全て）
    OperationLocalSnapshotReader.cs                   確定時に entities → v2 payload を集約
    IOperationLocalSnapshotReader.cs                  抽象
  Abstractions/
    IClaimReportGenerator.cs                          帳票 generator 抽象
  Dtos/Claim/Reports/
    ClaimReportOfficeDto.cs
    ClaimReportRecipientDto.cs
    ClaimReportCertificateDto.cs
    ClaimReportSpecVersionDto.cs
    DailyServiceRecordDto.cs
    IntensiveSupportEpisodeDto.cs
    ClaimInputSummaryDto.cs
    ClaimLineDto.cs                                   Basic/Addition line + enum ClaimDetailLineKind
    ServiceProvisionRecordDto.cs
    ClaimInvoiceDto.cs
    ClaimStatementDto.cs
    RecipientClaimDetailDto.cs
  UseCases/Claim/
    GenerateClaimReportsUseCase.cs                    3 帳票 UseCase

src/Tsumugi.Infrastructure.Reporting/
  ClaimReportGenerator.cs                             QuestPDF 実装

src/Tsumugi.App/
  ViewModels/Claim/
    ClaimReportSection.cs                             ClaimPreparationViewModel から切り出す帳票セクション状態
                                                       （ClaimReportKind, 選択受給者, SaveCommand 群）

tests/Tsumugi.Application.Tests/Claim/
  ClaimSnapshotValidationCodecV2Tests.cs
  OperationLocalSnapshotReaderTests.cs
  ClaimFinalizationSnapshotReaderTests.cs
tests/Tsumugi.Application.Tests/UseCases/Claim/
  GenerateClaimReportsUseCaseTests.cs
tests/Tsumugi.Infrastructure.Reporting.Tests/
  ClaimReportGeneratorServiceProvisionRecordTests.cs
  ClaimReportGeneratorClaimInvoiceTests.cs
  ClaimReportGeneratorClaimStatementTests.cs
tests/Tsumugi.App.Tests/ViewModels/Claim/
  ClaimReportSectionTests.cs

docs/
  phase3-2-acceptance.md                              Task 15 で作成
  decisions/
    0029-claim-snapshot-codec-v2.md                   Task 2 で作成
    0030-report-input-ui-responsibility.md            Task 4-8 進行中に作成
```

### 変更

```
src/Tsumugi.Application/
  Claim/
    ClaimRecipientSnapshotWriter.cs                   calculation snapshot は残す、finalization writer は削除
    ClaimSnapshotValidationCodecV1.cs                 削除（Task 2）
    ProductionClaimSnapshotValidationCodecRegistry.cs v2 のみ登録
  UseCases/Claim/
    CloseClaimUseCase.cs                              OperationLocalSnapshotReader を注入、v2 payload を書く

src/Tsumugi.Infrastructure.Csv/Mapping/
  ClaimInputRequirementProvider.cs                    21 report フィールドの readiness ルール追加

src/Tsumugi.App/
  ViewModels/
    ClaimPreparationViewModel.cs                      ClaimReportSection の露出、確定履歴からの帳票コマンド
    OfficeViewModel.cs                                4 フィールド入力欄
    CertificateViewModel.cs                           3 フィールド入力欄
    DailyRecordViewModel.cs (or DailyCellViewModel)   10 フィールド入力欄 + IntensiveSupport
    ClaimInputViewModel.cs                            3 フィールド入力欄
  Views/
    ClaimPreparationView.axaml                        「帳票出力」セクション追加
    OfficeView.axaml                                  4 フィールド入力欄
    CertificateView.axaml                             3 フィールド入力欄
    DailyRecordView.axaml                             10 + 1 フィールド入力欄
    ClaimInputView.axaml                              3 フィールド入力欄
  App.axaml.cs (or CompositionRoot)                   IClaimReportGenerator, GenerateClaimReportsUseCase 登録

docs/
  open-questions.md                                   21 report フィールド関連の open question を [x] へ
  phase3-claim-field-mapping.md                       21 report フィールドの status を「実装済」相当に更新（既存 sourceRefs は保持）
  superpowers/plans/
    2026-06-29-phase3-2-reports.md                    Status: SUPERSEDED セクション追記（削除しない、リンクは維持）
CLAUDE.md                                             「現在地」を Phase 3-2 完了に更新（Task 15）
```

---

## Task 1: 旧計画 supersede + ワークツリー確認

**Files:**
- Modify: `docs/superpowers/plans/2026-06-29-phase3-2-reports.md`（先頭に SUPERSEDED セクション追記）
- Verify: `git status --short` がクリーン

**Interfaces:**
- Consumes: なし
- Produces: 以後の全タスクの前提となるクリーンなワークツリー、旧計画の履歴保持

- [x] **Step 1: 旧 Phase 3-2 計画に SUPERSEDED 追記**

`docs/superpowers/plans/2026-06-29-phase3-2-reports.md` の 1 行目直前に以下を挿入：

```markdown
> **Status: SUPERSEDED（2026-07-20）** — 本計画は spec §8「確定時のスナップショットだけを入力」規約に反する設計だったため、`docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md` と `docs/superpowers/plans/2026-07-20-phase3-2-reports.md` に置き換え。以後編集しない。履歴として保存。

```

- [x] **Step 2: `git status --short` を実行してワークツリー確認**

Run: `git status --short`
Expected: `M docs/superpowers/plans/2026-06-29-phase3-2-reports.md` と本plan本体（未追跡）のみ

- [x] **Step 3: コミット**

```bash
git add docs/superpowers/plans/2026-06-29-phase3-2-reports.md docs/superpowers/plans/2026-07-20-phase3-2-reports.md
git commit -m "docs(phase3-2/task 1): supersede old plan and add redesign plan"
```

---

## Task 2: ClaimSnapshotValidationCodecV2

**Files:**
- Create: `src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV2.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimFinalizationSnapshotWriter.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimFinalizationSnapshotReader.cs`
- Create: `src/Tsumugi.Application/Claim/ClaimFinalizationSnapshot.cs`
- Delete: `src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs`
- Modify: `src/Tsumugi.Application/Claim/ClaimRecipientSnapshotWriter.cs`（calculation snapshot は残す、identity 定数を v2 に更新）
- Modify: `src/Tsumugi.Application/Claim/ProductionClaimSnapshotValidationCodecRegistry.cs`（v2 のみ登録）
- Create: `docs/decisions/0029-claim-snapshot-codec-v2.md`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotValidationCodecV2Tests.cs`
- Test: `tests/Tsumugi.Application.Tests/Claim/ClaimFinalizationSnapshotReaderTests.cs`

**Interfaces:**
- Consumes: 既存 `IClaimSnapshotValidationCodec`、`ValidatedClaimSnapshotEnvelope.CreateValidated`、`ClaimSnapshotValidationCodecV1` は削除
- Produces:
  - `public const string ClaimSnapshotValidationCodecV2.SchemaVersionValue = "claim-snapshot-v2";`
  - `public const string ClaimSnapshotValidationCodecV2.ValidationCodecIdValue = "claim-snapshot-codec-v2";`
  - `ClaimFinalizationSnapshotWriter.Write(ClaimFinalizationSnapshot snapshot) → byte[]`（canonical JSON）
  - `ClaimFinalizationSnapshotReader.Parse(ReadOnlySpan<byte> canonicalUtf8) → ClaimFinalizationSnapshot`
  - `sealed record ClaimFinalizationSnapshot(...)` — spec §6 の全21フィールド + 集計 + 既存フィールド。次タスク以降が消費する
  - `ClaimRecipientSnapshotWriter.WriteCalculationSnapshot` はそのまま残す（calculation snapshot 経路は codec v2 の同 SchemaVersion/CodecId で書く）

- [x] **Step 1: ADR 0029 を作成**

`docs/decisions/0029-claim-snapshot-codec-v2.md`:

```markdown
# ADR 0029 — Claim snapshot codec v2

- Status: Accepted
- Date: 2026-07-20

## 決定

`ClaimSnapshotValidationCodecV1` を削除し、`ClaimSnapshotValidationCodecV2` に破壊的置換する。schema version は `claim-snapshot-v2`、validationCodecId は `claim-snapshot-codec-v2`。

## 背景

Phase 3-1 vertical slice で導入した codec v1 は `serviceCode` + 集計値だけの軽量 payload で、3帳票が必要とする Office / Recipient / Certificate / DailyRecord / ClaimInput / IntensiveSupportEpisode / ClaimLines を保持していない。spec §8「生成時に現行 DailyRecord、Certificate、Office 等を再読込せず、確定時のスナップショットだけを入力とする」を満たすには payload 拡張が必須。

## 選択肢

1. v1 を残し、v2 追加で並走（backwards-compat）
2. v1 を破壊的に v2 置換（backwards-compat 無し）
3. v1 に新フィールドを additive に追加（schema version 据置）

## 決定

**選択肢 2**（破壊的置換）。CLAUDE.md「backwards-compat hack禁止」規約に従う。前提として production DB に確定 revision が存在しない（Phase 3-1 マージ直後、GUI 貫通も未実施）ため data migration は不要。

## 影響

- `ClaimSnapshotValidationCodecV1` は削除
- v1 payload を持つ ClaimDetail が存在する場合は codec が拒否する（parse 失敗）
- production 導入前に codec v2 で再確定する運用を前提とする
```

- [x] **Step 2: 失敗テストを書く（codec v2 identity + canonical bytes 決定論）**

`tests/Tsumugi.Application.Tests/Claim/ClaimSnapshotValidationCodecV2Tests.cs`:

```csharp
using FluentAssertions;
using Tsumugi.Application.Claim;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimSnapshotValidationCodecV2Tests
{
    [Fact]
    public void Identity_constants_are_v2()
    {
        ClaimSnapshotValidationCodecV2.SchemaVersionValue.Should().Be("claim-snapshot-v2");
        ClaimSnapshotValidationCodecV2.ValidationCodecIdValue.Should().Be("claim-snapshot-codec-v2");
    }

    [Fact]
    public void CreateEnvelope_produces_matching_sha256_for_same_canonical_bytes()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var canonical = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"finalization"}"""u8.ToArray();
        var env1 = codec.CreateEnvelope(canonical);
        var env2 = codec.CreateEnvelope(canonical);
        env1.PayloadSha256.Should().Be(env2.PayloadSha256);
        env1.GetCanonicalUtf8Bytes().Should().Equal(env2.GetCanonicalUtf8Bytes());
    }

    [Fact]
    public void CreateEnvelope_rejects_v1_schema_version()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var v1canonical = """{"schemaVersion":"claim-snapshot-v1","validationCodecId":"claim-snapshot-codec-v1","snapshotKind":"calculation"}"""u8.ToArray();
        var act = () => codec.CreateEnvelope(v1canonical);
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [x] **Step 3: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "ClaimSnapshotValidationCodecV2Tests" -v minimal`
Expected: FAIL（`ClaimSnapshotValidationCodecV2` 未定義）

- [x] **Step 4: `ClaimSnapshotValidationCodecV2` を実装**

`src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV2.cs`：既存 `ClaimSnapshotValidationCodecV1.cs` を参照し、identity 定数だけ更新した実装。**canonical JSON の parse ロジックはそのまま流用**（重複キー禁止、JSON object 必須、identity 一致検証）。v1 identity を持つ payload は `InvalidOperationException` で拒否する分岐を追加。

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

public sealed class ClaimSnapshotValidationCodecV2 : IClaimSnapshotValidationCodec
{
    public const string SchemaVersionValue = "claim-snapshot-v2";
    public const string ValidationCodecIdValue = "claim-snapshot-codec-v2";
    private static readonly object Marker = new();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
    };

    public string SchemaVersion => SchemaVersionValue;
    public string ValidationCodecId => ValidationCodecIdValue;

    public ValidatedClaimSnapshotEnvelope CreateEnvelope(ReadOnlySpan<byte> canonicalUtf8)
    {
        if (canonicalUtf8.IsEmpty)
            throw new InvalidOperationException("canonical bytes が空です。");
        // 重複キー禁止 + JSON object 検証（v1 と同じ）
        using var doc = JsonDocument.Parse(canonicalUtf8.ToArray(), new JsonDocumentOptions { AllowTrailingCommas = false });
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("canonical bytes は JSON object でなければなりません。");
        if (!doc.RootElement.TryGetProperty("schemaVersion", out var sv) || sv.GetString() != SchemaVersionValue)
            throw new InvalidOperationException($"schemaVersion は '{SchemaVersionValue}' でなければなりません。");
        if (!doc.RootElement.TryGetProperty("validationCodecId", out var cid) || cid.GetString() != ValidationCodecIdValue)
            throw new InvalidOperationException($"validationCodecId は '{ValidationCodecIdValue}' でなければなりません。");

        var sha256 = Convert.ToHexString(SHA256.HashData(canonicalUtf8)).ToLowerInvariant();
        return ValidatedClaimSnapshotEnvelope.CreateValidated(
            SchemaVersion, ValidationCodecId, sha256, canonicalUtf8, Marker);
    }
}
```

- [x] **Step 5: テスト緑確認**

Run: `dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "ClaimSnapshotValidationCodecV2Tests" -v minimal`
Expected: PASS

- [x] **Step 6: v1 削除 + registry 更新**

`src/Tsumugi.Application/Claim/ClaimSnapshotValidationCodecV1.cs` を削除。`ProductionClaimSnapshotValidationCodecRegistry.cs` を修正して v2 のみ登録：

```csharp
public sealed class ProductionClaimSnapshotValidationCodecRegistry : IClaimSnapshotValidationCodecRegistry
{
    private static readonly IClaimSnapshotValidationCodec V2 = new ClaimSnapshotValidationCodecV2();

    public IClaimSnapshotValidationCodec GetById(string validationCodecId) => validationCodecId switch
    {
        ClaimSnapshotValidationCodecV2.ValidationCodecIdValue => V2,
        _ => throw new InvalidOperationException($"未対応の codec id: {validationCodecId}"),
    };
}
```

- [x] **Step 7: `ClaimRecipientSnapshotWriter` の identity 定数を v2 参照へ差替**

`src/Tsumugi.Application/Claim/ClaimRecipientSnapshotWriter.cs` の `WriteEnvelopeIdentity` を修正：`ClaimSnapshotValidationCodecV1.*` → `ClaimSnapshotValidationCodecV2.*`。calculation snapshot の書き出しはそのまま維持（`snapshotKind = "calculation"`）。

- [x] **Step 8: 全 test suite で v1 参照が残っていないことを確認**

Run: `grep -rn 'ClaimSnapshotValidationCodecV1\|claim-snapshot-v1\|claim-snapshot-codec-v1' src tests`
Expected: 出力なし。もし v1 名を期待するテストがあれば v2 名へ書き換える。

- [x] **Step 9: ビルド + 全 test 緑確認**

Run: `dotnet build -c Release && dotnet test -v minimal`
Expected: 警告ゼロ、全テスト緑

- [x] **Step 10: 失敗テストを書く（finalization writer + parser round-trip）**

`tests/Tsumugi.Application.Tests/Claim/ClaimFinalizationSnapshotReaderTests.cs`：

```csharp
using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimFinalizationSnapshotReaderTests
{
    [Fact]
    public void Write_then_parse_roundtrips_all_21_report_fields()
    {
        var snapshot = SampleSnapshot();
        var bytes = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var parsed = ClaimFinalizationSnapshotReader.Parse(bytes);
        parsed.Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    public void Write_produces_identical_bytes_for_identical_input()
    {
        var snapshot = SampleSnapshot();
        var a = ClaimFinalizationSnapshotWriter.Write(snapshot);
        var b = ClaimFinalizationSnapshotWriter.Write(snapshot);
        a.Should().Equal(b);
    }

    [Fact]
    public void Parse_rejects_calculation_kind_payload()
    {
        var payload = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"calculation"}"""u8.ToArray();
        var act = () => ClaimFinalizationSnapshotReader.Parse(payload);
        act.Should().Throw<InvalidOperationException>().WithMessage("*finalization*");
    }

    private static ClaimFinalizationSnapshot SampleSnapshot() => new(
        RecipientId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
        ServiceMonth: new ServiceMonth(2026, 5),
        ClaimMasterVersion: "r6-2026-04",
        CsvSpecificationVersion: "r7-10",
        ReportSpecificationVersion: "r1-10",
        Office: new ClaimFinalizationOfficeSnapshot("0123456789", "テスト事業所", RegionGrade.None,
            "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎"),
        Recipient: new ClaimFinalizationRecipientSnapshot("山田太郎", "ヤマダタロウ"),
        Certificate: new ClaimFinalizationCertificateSnapshot("9876543210", "131016", null, 9300, null, null),
        ClaimInput: new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
        DailyRecords: [ new ClaimFinalizationDailyRecordSnapshot(
            new DateOnly(2026, 5, 1), Attendance.Present, true, TransportKind.None, null,
            new TimeOnly(9, 0), new TimeOnly(16, 0), null, false, null, null,
            false, false, false, true) ],
        IntensiveSupportEpisode: null,
        ClaimLines: [ new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720) ],
        BilledDays: 20, TotalUnits: 630, TotalCostYen: 7056, BenefitYen: 6351, BurdenYen: 705);
}
```

- [x] **Step 11: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "ClaimFinalizationSnapshotReaderTests" -v minimal`
Expected: FAIL

- [x] **Step 12: `ClaimFinalizationSnapshot` DTO 定義**

`src/Tsumugi.Application/Claim/ClaimFinalizationSnapshot.cs`：spec §6 の JSON schema を厳密に反映した sealed record 群。フィールド名は payload の JSON key と一致。

```csharp
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Application.Dtos.Claim.Reports; // ClaimDetailLineKind

namespace Tsumugi.Application.Claim;

public sealed record ClaimFinalizationSnapshot(
    Guid RecipientId,
    ServiceMonth ServiceMonth,
    string ClaimMasterVersion,
    string CsvSpecificationVersion,
    string ReportSpecificationVersion,
    ClaimFinalizationOfficeSnapshot Office,
    ClaimFinalizationRecipientSnapshot Recipient,
    ClaimFinalizationCertificateSnapshot Certificate,
    ClaimFinalizationClaimInputSnapshot ClaimInput,
    IReadOnlyList<ClaimFinalizationDailyRecordSnapshot> DailyRecords,
    ClaimFinalizationIntensiveSupportEpisodeSnapshot? IntensiveSupportEpisode,
    IReadOnlyList<ClaimFinalizationClaimLineSnapshot> ClaimLines,
    int BilledDays, int TotalUnits, int TotalCostYen, int BenefitYen, int BurdenYen);

public sealed record ClaimFinalizationOfficeSnapshot(
    string OfficeNumber, string OfficeName, RegionGrade RegionGrade,
    string PostalCode, string Address, string PhoneNumber, string RepresentativeTitleAndName);

public sealed record ClaimFinalizationRecipientSnapshot(string KanjiName, string KanaName);

public sealed record ClaimFinalizationCertificateSnapshot(
    string CertificateNumber, string MunicipalityNumber, string? SubsidyMunicipalityNumber,
    int MonthlyCostCap,
    string? UpperLimitManagementProviderNumber, string? UpperLimitManagementProviderName);

public sealed record ClaimFinalizationClaimInputSnapshot(
    string? UpperLimitManagementResult, int? UpperLimitManagedAmountYen,
    int? MunicipalSubsidyAmountYen,
    ServiceMonth? ExceptionalUsageStartMonth, ServiceMonth? ExceptionalUsageEndMonth,
    int? ExceptionalUsageDays, int? StandardUsageDayTotal);

public sealed record ClaimFinalizationDailyRecordSnapshot(
    DateOnly ServiceDate, Attendance Attendance, bool MealProvided, TransportKind Transport,
    string? AbsenceResponseNote,
    TimeOnly? ServiceStartTime, TimeOnly? ServiceEndTime,
    int? SpecialVisitSupportMinutes, bool OffsiteSupportApplied,
    string? MedicalCoordinationType, string? TrialUseSupportType,
    bool RegionalCollaborationApplied, bool IntensiveSupportApplied,
    bool EmergencyAdmissionApplied, bool RecipientConfirmation);

public sealed record ClaimFinalizationIntensiveSupportEpisodeSnapshot(DateOnly StartDate);

public sealed record ClaimFinalizationClaimLineSnapshot(
    ClaimDetailLineKind Kind, string ServiceCode, int Unit, int Count, int AmountYen);
```

- [x] **Step 13: Writer 実装**

`src/Tsumugi.Application/Claim/ClaimFinalizationSnapshotWriter.cs`：既存 `ClaimRecipientSnapshotWriter` の canonical JSON 書き出しパターン（`Utf8JsonWriter` + `WriteStartObject`/`WriteEndObject` + キー順序固定）を踏襲。fields は spec §6 のスキーマ順序と完全一致。`null` の optional は `WriteNull()` で明示。`ClaimDetailLineKind` は `ToString()`（PascalCase）。

- [x] **Step 14: Reader 実装**

`src/Tsumugi.Application/Claim/ClaimFinalizationSnapshotReader.cs`：`JsonDocument.Parse` → `RootElement` から順序に依らず全キーを読み取り、`ClaimFinalizationSnapshot` を返す。`snapshotKind != "finalization"` は `InvalidOperationException`。schemaVersion/validationCodecId は codec の識別子と一致することを検証。

- [x] **Step 15: テスト緑確認 + コミット**

Run: `dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj -v minimal`
Expected: 全緑

```bash
git add src/Tsumugi.Application/Claim/ tests/Tsumugi.Application.Tests/Claim/ docs/decisions/0029-claim-snapshot-codec-v2.md
git commit -m "feat(phase3-2/task 2): claim snapshot codec v2 + finalization payload writer/reader"
```

---

## Task 3: OperationLocalSnapshotReader

**Files:**
- Create: `src/Tsumugi.Application/Claim/IOperationLocalSnapshotReader.cs`
- Create: `src/Tsumugi.Application/Claim/OperationLocalSnapshotReader.cs`
- Modify: `src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs`（v2 finalization payload 書き出しへ差替）
- Test: `tests/Tsumugi.Application.Tests/Claim/OperationLocalSnapshotReaderTests.cs`

**Interfaces:**
- Consumes: `IOfficeRepository`, `IRecipientRepository`, `ICertificateRepository`, `IDailyRecordRepository`, `IIntensiveSupportEpisodeRepository`（`src/Tsumugi.Application/Abstractions/` に存在するものを利用、無ければ Task 内で最低限追加）、Phase 3-1 の `RecipientClaimResult`
- Produces:
  - `interface IOperationLocalSnapshotReader { Task<ClaimFinalizationSnapshot> ReadAsync(Guid officeId, Guid recipientId, ServiceMonth serviceMonth, RecipientClaimResult calculationResult, string masterVersion, string csvSpec, string reportSpec, CancellationToken ct); }`
  - `sealed class OperationLocalSnapshotReader` 実装

- [x] **Step 1: 抽象を作成**

`src/Tsumugi.Application/Claim/IOperationLocalSnapshotReader.cs`：

```csharp
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

public interface IOperationLocalSnapshotReader
{
    Task<ClaimFinalizationSnapshot> ReadAsync(
        Guid officeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        RecipientClaimResult calculationResult,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        CancellationToken ct);
}
```

- [x] **Step 2: 失敗テストを書く（21フィールド全てが payload に載る）**

`tests/Tsumugi.Application.Tests/Claim/OperationLocalSnapshotReaderTests.cs`：`Moq` で全 repository を stub し、21 フィールドを持つ Office/Recipient/Certificate/DailyRecord/IntensiveSupportEpisode/ClaimInput を返す。ReadAsync 結果の各フィールドを `Should().Be(...)` で assert（21件、1 field = 1 assertion）。

```csharp
using FluentAssertions;
using Moq;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

public sealed class OperationLocalSnapshotReaderTests
{
    [Fact]
    public async Task ReadAsync_captures_all_21_report_fields_from_entities()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var ym = new ServiceMonth(2026, 5);

        var office = Office.Create(officeId, "0123456789", "テスト事業所",
            ServiceCategory.TypeB, RegionGrade.None,
            createdBy: "seed", createdAt: DateTimeOffset.UnixEpoch, concurrencyToken: Guid.NewGuid(),
            postalCode: "1000001", address: "東京都千代田区千代田1-1",
            phoneNumber: "03-0000-0000", representativeTitleAndName: "代表取締役 山田太郎");
        // Certificate, DailyRecord, IntensiveSupportEpisode, ClaimInput を同様に構築
        // ...
        var officeRepo = new Mock<IOfficeRepository>();
        officeRepo.Setup(r => r.GetByIdAsync(officeId, It.IsAny<CancellationToken>())).ReturnsAsync(office);
        // recipient / certificate / dailyRecords / intensiveSupport / claimInput の mock も同様

        var sut = new OperationLocalSnapshotReader(officeRepo.Object, /* ... */);
        var snapshot = await sut.ReadAsync(officeId, recipientId, ym,
            calculationResult: /* sample RecipientClaimResult */,
            claimMasterVersion: "r6-2026-04",
            csvSpecificationVersion: "r7-10",
            reportSpecificationVersion: "r1-10",
            ct: default);

        // Office 4 fields
        snapshot.Office.PostalCode.Should().Be("1000001");
        snapshot.Office.Address.Should().Be("東京都千代田区千代田1-1");
        snapshot.Office.PhoneNumber.Should().Be("03-0000-0000");
        snapshot.Office.RepresentativeTitleAndName.Should().Be("代表取締役 山田太郎");
        // Certificate 3 fields
        snapshot.Certificate.MunicipalityNumber.Should().Be("131016");
        // ... 21 assertions total
    }

    [Fact]
    public async Task ReadAsync_throws_when_office_missing() { /* ... */ }

    [Fact]
    public async Task ReadAsync_builds_claim_lines_from_calculation_result() { /* ClaimLines[] = Basic + Addition の混在 */ }
}
```

- [x] **Step 3: テスト赤確認**

Run: `dotnet test --filter "OperationLocalSnapshotReaderTests" -v minimal`
Expected: FAIL

- [x] **Step 4: `OperationLocalSnapshotReader` を実装**

`src/Tsumugi.Application/Claim/OperationLocalSnapshotReader.cs`：constructor で必要な repository 一式を受け取り、`ReadAsync` で並列取得（`Task.WhenAll`）してから `ClaimFinalizationSnapshot` を構築。`RecipientClaimResult` の line 列を `ClaimFinalizationClaimLineSnapshot` に射影（`Basic` は基本報酬 line、`Addition` は加算 line、既存 `RecipientClaimResult.Lines` の kind field を参照）。

repository が null 返しなら `InvalidOperationException($"...id={id} が存在しない")`。

- [x] **Step 5: テスト緑確認**

Run: `dotnet test --filter "OperationLocalSnapshotReaderTests" -v minimal`
Expected: 全緑

- [x] **Step 6: `CloseClaimUseCase` を修正**

既存の `CloseClaimUseCase` は `IClaimCalculationSnapshotReader` を使って軽量 payload を書いている。これを次で置き換える：

- constructor に `IOperationLocalSnapshotReader operationSnapshotReader` を追加
- 確定 draft を構築する箇所（受給者ごとの `ClaimDetail.Create` 呼び出し前）で、`operationSnapshotReader.ReadAsync(...)` → `ClaimFinalizationSnapshotWriter.Write(snapshot)` で v2 canonical bytes を得る
- そのバイト列を `IClaimSnapshotValidationCodecRegistry.GetById(v2 codecId).CreateEnvelope(bytes)` で envelope 化
- envelope の `GetCanonicalUtf8Bytes()` を `ClaimDetail.CalculationSnapshotJson` に格納（プロパティ名は既存維持だが中身は finalization payload）
- 既存 `IClaimCalculationSnapshotReader` は Preview（`CalculateClaimUseCase`）でだけ使うため残置

- [x] **Step 7: 既存 `CloseClaimUseCase` テストを緑に戻す**

Run: `dotnet test tests/Tsumugi.Application.Tests -v minimal`
Expected: `CloseClaimUseCaseTests` の期待 payload が v1 想定なら v2 に更新。全緑にする。

- [x] **Step 8: DI 登録**

`src/Tsumugi.App/App.axaml.cs`（または `CompositionRoot.cs`）で:

```csharp
services.AddScoped<IOperationLocalSnapshotReader, OperationLocalSnapshotReader>();
```

- [x] **Step 9: `./build/ci.sh` 緑確認 + コミット**

Run: `./build/ci.sh`
Expected: `==> CI OK`

```bash
git add src/Tsumugi.Application/Claim/ src/Tsumugi.Application/UseCases/Claim/CloseClaimUseCase.cs src/Tsumugi.App/App.axaml.cs tests/Tsumugi.Application.Tests/Claim/
git commit -m "feat(phase3-2/task 3): OperationLocalSnapshotReader wires v2 finalization payload into CloseClaimUseCase"
```

---

## Task 4: DailyRecord 10フィールド UI 露出 + readiness gate

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/DailyRecordViewModel.cs`（既存名称に合わせる。無ければ `DailyCellViewModel.cs`）
- Modify: `src/Tsumugi.App/Views/DailyRecordView.axaml`（同上）
- Modify: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`（readiness ルール追加）
- Test: 既存 `tests/Tsumugi.App.Tests/ViewModels/DailyRecordViewModelTests.cs`（無ければ作成）に10フィールド分の binding テスト追加
- Test: `tests/Tsumugi.Application.Tests/Csv/Mapping/ClaimInputRequirementProviderTests.cs`（無ければ作成）に readiness negative test 追加

**Interfaces:**
- Consumes: 既存 `DailyRecord` エンティティ（10 フィールド既存）、既存 `ClaimInputRequirementProvider`、既存 `IClaimInputRequirementProvider`
- Produces: 10フィールドの入力UIと、対象月に `Attendance.Present` の日があるとき `ServiceStartTime`/`ServiceEndTime`/`RecipientConfirmation` が欠落なら readiness issue を出す `ClaimInputRequirement` 群

- [x] **Step 1: 失敗テストを書く（ViewModel binding × 10 fields）**

`tests/Tsumugi.App.Tests/ViewModels/DailyRecordViewModelTests.cs` に「10 フィールドが ObservableProperty として読み書きできる」テストを追加。既存 view model のパターンを踏襲。

- [x] **Step 2: テスト赤確認 → 実装**

`DailyRecordViewModel.cs` に `[ObservableProperty]` として10フィールド追加（`TimeOnly?`, `int?`, `bool`, `enum` に応じた型）。`DailyRecordView.axaml` に対応 UI（`TimePicker`, `NumericUpDown`, `CheckBox`, `ComboBox`）を追加。既存 grid レイアウトに沿って配置。

- [x] **Step 3: テスト緑確認 → 一度コミット**

```bash
dotnet test tests/Tsumugi.App.Tests -v minimal
git add src/Tsumugi.App/ViewModels/DailyRecordViewModel.cs src/Tsumugi.App/Views/DailyRecordView.axaml tests/Tsumugi.App.Tests/ViewModels/DailyRecordViewModelTests.cs
git commit -m "feat(phase3-2/task 4): expose 10 DailyRecord report fields in UI"
```

- [x] **Step 4: 失敗テストを書く（readiness gate × 3 fields）**

`tests/Tsumugi.Application.Tests/Csv/Mapping/ClaimInputRequirementProviderTests.cs` の新規メソッド：

```csharp
[Fact]
public void Requirements_flag_missing_service_start_time_on_present_day()
{
    var provider = new ClaimInputRequirementProvider();
    var reqs = provider.GetRequirements();
    reqs.Should().Contain(r => r.FieldIds.Contains("report:service-performance:daily:004")
                            && r.TargetPath == "DailyRecord.ServiceStartTime");
}
// 同様に daily:005 (ServiceEndTime) / daily:016 (RecipientConfirmation)
```

`ServiceStartTime`/`ServiceEndTime`/`RecipientConfirmation` の3つは「Present の日があれば必須」なので、`Condition` に既存 `all(...)` DSL を組み立てる。残り7 field（SpecialVisitSupportMinutes / OffsiteSupportApplied / MedicalCoordinationType / TrialUseSupportType / RegionalCollaborationApplied / IntensiveSupportApplied / EmergencyAdmissionApplied）は optional として `RecordPresent(DailyRecord)` のみで readiness には出さず、payload に null/false のまま載せる。

- [x] **Step 5: テスト赤確認 → `ClaimInputRequirementProvider` に readiness ルール追加**

10 フィールドすべて `ClaimInputRequirement` として登録（`Destination = DailyRecordView`）。必須3 fields は `Condition = all(recordPresent(DailyRecord), attendanceEquals(Present))`、残り7 fields は `Condition = rowPresent(DailyRecord)`。

- [x] **Step 6: テスト緑確認 + コミット**

```bash
dotnet test tests/Tsumugi.Application.Tests -v minimal
git add src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs tests/Tsumugi.Application.Tests/Csv/Mapping/ClaimInputRequirementProviderTests.cs
git commit -m "feat(phase3-2/task 4): DailyRecord 10 fields registered in requirement provider"
```

---

## Task 5: Certificate 3フィールド UI 露出 + readiness gate

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/CertificateViewModel.cs`
- Modify: `src/Tsumugi.App/Views/CertificateView.axaml`
- Modify: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`
- Test: 既存 `tests/Tsumugi.App.Tests/ViewModels/CertificateViewModelTests.cs` に3フィールド binding テスト追加
- Test: `ClaimInputRequirementProviderTests.cs` に3件の negative test 追加

**Interfaces:**
- Consumes: 既存 `Certificate` エンティティ（3フィールド既存）
- Produces: UIから `MunicipalityNumber`（必須）、`SubsidyMunicipalityNumber`（任意）、`UpperLimitManagementProviderNumber`（任意）を編集可能、readiness rules 3件

- [x] **Step 1: 失敗テストを書く（binding）**

3 フィールドの ObservableProperty binding test を追加。

- [x] **Step 2: 実装（ViewModel + XAML）**

`CertificateViewModel.cs` に 3 `[ObservableProperty]` を追加（`string?`）。`CertificateView.axaml` に `TextBox` × 3 を追加、ラベル：`市町村番号（必須）` / `自治体助成市町村番号` / `上限管理事業所番号`。

- [x] **Step 3: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 5): expose 3 Certificate report fields in UI"
```

- [x] **Step 4: readiness ルール追加**

`ClaimInputRequirementProvider` に 3 fields を追加：
- `MunicipalityNumber`（`report:benefit-claim-detail:header:001` + `provider:J111:01:004` 等 6箇所）: `Condition = always`
- `SubsidyMunicipalityNumber`（`report:benefit-claim-detail:header:003` + `provider:J121:01:007`）: `Condition = modelPresent(certificate.subsidyMunicipality)` — optional
- `UpperLimitManagementProviderNumber`（`report:benefit-claim-detail:upper-limit-management:001` + `provider:J121:01:015`）: `Condition = modelPresent(certificate.upperLimitManagementProvider)` — optional

- [x] **Step 5: negative test 追加 → テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 5): Certificate 3 fields registered in requirement provider"
```

---

## Task 6: Office 4フィールド UI 露出 + readiness gate

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/OfficeViewModel.cs`（またはリストとの分割に応じて `OfficeEditViewModel.cs`）
- Modify: `src/Tsumugi.App/Views/OfficeView.axaml`（同上）
- Modify: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`
- Test: 既存 `tests/Tsumugi.App.Tests/ViewModels/OfficeViewModelTests.cs`（無ければ作成）に4フィールド binding テスト追加
- Test: `ClaimInputRequirementProviderTests.cs` に4件の negative test 追加

**Interfaces:**
- Consumes: 既存 `Office` エンティティ（4フィールド既存、migration 不要）、既存 `OfficeDto`（4フィールド既存）
- Produces: UIから `PostalCode` / `Address` / `PhoneNumber` / `RepresentativeTitleAndName` を編集可能、readiness rules 4件（全て `always` 必須）

- [x] **Step 1: 失敗テストを書く（binding × 4 fields）**

- [x] **Step 2: 実装（ViewModel + XAML）**

4 `[ObservableProperty]` を追加。XAML に `TextBox` × 4 を追加、ラベル：`郵便番号` / `所在地` / `電話番号` / `代表者職氏名`。既存 `RegisterOfficeUseCase` / `UpdateOfficeUseCase` の signature が4フィールドを既に受け取っているか確認、無ければ optional 引数として追加。

- [x] **Step 3: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 6): expose 4 Office report fields in UI"
```

- [x] **Step 4: readiness rules 追加 + negative tests**

4 fields すべて `Condition = always`、`Destination = OfficeView`。fieldId は `report:benefit-claim-form:header:004-006, 008`。

- [x] **Step 5: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 6): Office 4 fields registered in requirement provider"
```

---

## Task 7: ClaimInput 3フィールド UI 露出 + readiness gate

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/ClaimInputViewModel.cs`
- Modify: `src/Tsumugi.App/Views/ClaimInputView.axaml`
- Modify: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`
- Test: `tests/Tsumugi.App.Tests/ViewModels/ClaimInputViewModelTests.cs`（既存無ければ作成）
- Test: `ClaimInputRequirementProviderTests.cs` に3件の conditional negative test 追加

**Interfaces:**
- Consumes: 既存 `ClaimInput` モデル（`UpperLimitManagementResult`, `UpperLimitManagedAmountYen`, `MunicipalSubsidyAmountYen` 全て Phase 3-1 Task 12 で実装済）
- Produces: UIから3フィールド編集可能、readiness rules 3件（conditional：`UpperLimitManagement*` は `Certificate.UpperLimitManagementProviderNumber != null` のとき必須、`MunicipalSubsidyAmountYen` は事業所が自治体助成対象のときのみ必須、無ければ Task 内で optional にfallback）

- [x] **Step 1: 失敗テストを書く（binding × 3 fields）**

- [x] **Step 2: 実装（ViewModel + XAML）**

`UpperLimitManagementResult` は enum（Phase 3-1 で定義済）→ ComboBox、`UpperLimitManagedAmountYen`/`MunicipalSubsidyAmountYen` は `int?` → NumericUpDown。ラベル：`上限管理結果` / `上限管理後利用者負担額` / `自治体助成分請求額`。

- [x] **Step 3: テスト緑 + コミット**

- [x] **Step 4: readiness rules 追加 + negative tests**

- `UpperLimitManagementResult` fieldId `report:benefit-claim-detail:upper-limit-management:003`: `Condition = modelPresent(certificate.upperLimitManagementProvider)`
- `UpperLimitManagedAmountYen` fieldId `report:benefit-claim-detail:upper-limit-management:004`: 同上
- `MunicipalSubsidyAmountYen` fieldId `report:benefit-claim-detail:summary:015`: `Condition = modelPresent(claimInput.municipalSubsidyAmountYen)`（現状 optional で fail-closed しない扱い。事業所プロファイル判定は Phase 3-3 の CSV スコープに委譲）

- [x] **Step 5: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 7): ClaimInput 3 fields registered in requirement provider"
```

---

## Task 8: IntensiveSupportEpisode.StartDate UI 露出 + readiness gate

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/DailyRecordViewModel.cs`（IntensiveSupport は日次と同時に入力する運用を想定）
- Modify: `src/Tsumugi.App/Views/DailyRecordView.axaml`（別セクション「集中的支援エピソード」）
- Modify: `src/Tsumugi.Infrastructure.Csv/Mapping/ClaimInputRequirementProvider.cs`
- Test: 上記 ViewModel テストに `StartDate` binding テスト追加
- Test: `ClaimInputRequirementProviderTests.cs` に1件の conditional negative test 追加

**Interfaces:**
- Consumes: 既存 `IntensiveSupportEpisode` エンティティ
- Produces: UIから `StartDate` を編集可能、readiness rule 1件（対象月に `DailyRecord.IntensiveSupportApplied=true` の日があるときのみ必須）

- [x] **Step 1: 失敗テスト（binding + readiness）**

- [x] **Step 2: 実装**

`DatePicker` を DailyRecordView の別セクションに配置。ViewModel は `[ObservableProperty] private DateOnly? _intensiveSupportEpisodeStartDate;`。

- [x] **Step 3: readiness rule 追加**

fieldId `report:service-performance:intensive-support:001`, `Condition = all(recordPresent(DailyRecord), fieldEquals(DailyRecord.IntensiveSupportApplied, true))`。

- [x] **Step 4: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 8): IntensiveSupportEpisode.StartDate exposed + requirement provider registered"
```

- [x] **Step 5: ADR 0030 を作成**

`docs/decisions/0030-report-input-ui-responsibility.md`：21 フィールドのUI責務分割（Certificate/DailyRecord/Office/ClaimPreparation/IntensiveSupportEpisode の各 View の境界）を記録。1画面1責務規約に基づく分割根拠。

```bash
git add docs/decisions/0030-report-input-ui-responsibility.md
git commit -m "docs(phase3-2/task 8): ADR 0030 report input UI responsibility split"
```

---

## Task 9: 帳票 DTO 群 + IClaimReportGenerator 抽象

**Files:**
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimDetailLineKind.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimReportOfficeDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimReportRecipientDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimReportCertificateDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimReportSpecVersionDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/DailyServiceRecordDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/IntensiveSupportEpisodeDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimInputSummaryDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimLineDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ServiceProvisionRecordDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimInvoiceDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/ClaimStatementDto.cs`
- Create: `src/Tsumugi.Application/Dtos/Claim/Reports/RecipientClaimDetailDto.cs`
- Create: `src/Tsumugi.Application/Abstractions/IClaimReportGenerator.cs`

**Interfaces:**
- Consumes: `Tsumugi.Domain.Enums`（`Attendance`, `TransportKind`, `RegionGrade`）、`ValueObjects`（`YearMonth`）
- Produces: spec §7 の全 DTO record 群 + `interface IClaimReportGenerator { byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto); byte[] GenerateClaimInvoice(ClaimInvoiceDto dto); byte[] GenerateClaimStatement(ClaimStatementDto dto); }`

- [x] **Step 1: DTO 12ファイルを spec §7 通りに作成**

spec §7.1〜§7.4 のシグネチャをそのまま反映。`ClaimDetailLineKind` は `enum { Basic, Addition }`。

- [x] **Step 2: `IClaimReportGenerator` を作成**

```csharp
using Tsumugi.Application.Dtos.Claim.Reports;

namespace Tsumugi.Application.Abstractions;

/// <summary>請求関連 PDF 帳票の生成抽象。決定論（同 payload + 同 TimeProvider → 同バイト）。</summary>
public interface IClaimReportGenerator
{
    /// <summary>サービス提供実績記録票（A4、利用者×月次）。</summary>
    byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求書（事業所×月次の集計）。</summary>
    byte[] GenerateClaimInvoice(ClaimInvoiceDto dto);

    /// <summary>介護給付費・訓練等給付費等 請求明細書（事業所×月次の受給者別明細）。</summary>
    byte[] GenerateClaimStatement(ClaimStatementDto dto);
}
```

- [x] **Step 3: ビルド緑確認 + コミット**

Run: `dotnet build -c Release`
Expected: 警告ゼロ

```bash
git add src/Tsumugi.Application/Dtos/Claim/Reports/ src/Tsumugi.Application/Abstractions/IClaimReportGenerator.cs
git commit -m "feat(phase3-2/task 9): Claim report DTOs + IClaimReportGenerator abstraction"
```

---

## Task 10: ClaimReportGenerator.GenerateServiceProvisionRecord

**Files:**
- Create: `src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`（3 メソッドのうち今回は 1 つを実装、他2つは `NotImplementedException`）
- Test: `tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimReportGeneratorServiceProvisionRecordTests.cs`

**Interfaces:**
- Consumes: `IClaimReportGenerator`, `ServiceProvisionRecordDto`, `TimeProvider`, QuestPDF, `QuestPdfLicenseConfigurator.Initialize()`
- Produces: サービス提供実績記録票 PDF byte 列。10 dailyフィールド全てを描画テキストに含める

- [x] **Step 1: 失敗テストを書く**

`ClaimReportGeneratorServiceProvisionRecordTests.cs`：

```csharp
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using System.Text;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Reporting;
using UglyToad.PdfPig;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class ClaimReportGeneratorServiceProvisionRecordTests
{
    [Fact]
    public void GenerateServiceProvisionRecord_includes_office_recipient_month_in_extracted_text()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var gen = new ClaimReportGenerator(clock);
        var dto = SampleDto();
        var bytes = gen.GenerateServiceProvisionRecord(dto);
        var text = ExtractText(bytes);
        text.Should().Contain("テスト事業所").And.Contain("山田太郎").And.Contain("2026年5月");
        text.Should().Contain("サービス提供実績記録票");
    }

    [Fact]
    public void GenerateServiceProvisionRecord_renders_all_10_daily_fields()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var dto = SampleDto();
        var text = ExtractText(new ClaimReportGenerator(clock).GenerateServiceProvisionRecord(dto));
        text.Should().Contain("09:00").And.Contain("16:00");                    // ServiceStartTime/EndTime
        text.Should().Contain("特別訪問");                                          // SpecialVisitSupportMinutes ヘッダ
        text.Should().Contain("敷地外");                                            // OffsiteSupportApplied
        text.Should().Contain("医療連携");                                          // MedicalCoordinationType
        text.Should().Contain("体験利用");                                          // TrialUseSupportType
        text.Should().Contain("地域協働");                                          // RegionalCollaborationApplied
        text.Should().Contain("集中的支援");                                        // IntensiveSupportApplied
        text.Should().Contain("緊急受入");                                          // EmergencyAdmissionApplied
        text.Should().Contain("利用者確認");                                        // RecipientConfirmation
    }

    [Fact]
    public void GenerateServiceProvisionRecord_is_deterministic_for_same_inputs_and_timeprovider()
    {
        QuestPdfLicenseConfigurator.Initialize();
        var clock1 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var clock2 = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
        var dto = SampleDto();
        var a = new ClaimReportGenerator(clock1).GenerateServiceProvisionRecord(dto);
        var b = new ClaimReportGenerator(clock2).GenerateServiceProvisionRecord(dto);
        a.Should().Equal(b);
    }

    private static ServiceProvisionRecordDto SampleDto() =>
        new(
            Office: new ClaimReportOfficeDto("0123456789", "テスト事業所", RegionGrade.None,
                "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎"),
            Recipient: new ClaimReportRecipientDto("山田太郎", "ヤマダタロウ"),
            Certificate: new ClaimReportCertificateDto("9876543210", "131016", null, 9300, null, null),
            YearMonth: new YearMonth(2026, 5),
            Days: [
                new DailyServiceRecordDto(
                    new DateOnly(2026, 5, 1), Attendance.Present, true, TransportKind.None, null,
                    new TimeOnly(9, 0), new TimeOnly(16, 0), null, false, null, null,
                    false, false, false, true)
            ],
            IntensiveSupport: null,
            SpecVersion: new ClaimReportSpecVersionDto("r6-2026-04", "r7-10", "r1-10"));

    private static string ExtractText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages()) sb.Append(page.Text);
        return KangxiRadicalNormalizer.FoldKangxiRadicals(sb.ToString());
    }
}
```

- [x] **Step 2: テスト赤確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests/Tsumugi.Infrastructure.Reporting.Tests.csproj --filter "ServiceProvisionRecord" -v minimal`
Expected: FAIL（`ClaimReportGenerator` 未定義）

- [x] **Step 3: `ClaimReportGenerator` 骨組み + `GenerateServiceProvisionRecord` 実装**

`src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`：`WageStatementPdfGenerator.cs` の流儀を踏襲。`QuestPdfLicenseConfigurator.Initialize()` を private static ctor で呼ぶ。`DefaultTextStyle(x => x.FontFamily("Noto Sans JP"))` を注入。A4、20mm margin、Header（帳票名 + 事業所 + 利用者 + 対象月）→ Content（テーブル：日付・出欠・食事・送迎・開始/終了時刻・欠席時対応・特別訪問分・敷地外・医療連携・体験利用・地域協働・集中的支援・緊急受入・利用者確認）→ Footer（`clock.GetUtcNow():yyyy/MM/dd HH:mm:ss UTC`、spec version 3 種）。

`GenerateClaimInvoice` / `GenerateClaimStatement` は Task 11/12 まで `throw new NotImplementedException("Task 11/12")`。

- [x] **Step 4: テスト緑確認 + コミット**

```bash
git add src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimReportGeneratorServiceProvisionRecordTests.cs
git commit -m "feat(phase3-2/task 10/AC3-5): ClaimReportGenerator.GenerateServiceProvisionRecord with 10 daily fields"
```

---

## Task 11: ClaimReportGenerator.GenerateClaimInvoice

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`
- Test: `tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimReportGeneratorClaimInvoiceTests.cs`

**Interfaces:**
- Consumes: `ClaimInvoiceDto`, `TimeProvider`
- Produces: 請求書 PDF byte 列。4 office header フィールド全てを含む

- [x] **Step 1: 失敗テストを書く**

CJK 抽出／決定論／4 office header フィールド（郵便番号 / 住所 / 電話 / 代表者職氏名）／合計値（`TotalCostYen`）／master 版表示 の assertions。

```csharp
[Fact]
public void GenerateClaimInvoice_shows_office_contact_totals_and_spec_versions()
{
    QuestPdfLicenseConfigurator.Initialize();
    var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
    var gen = new ClaimReportGenerator(clock);
    var dto = new ClaimInvoiceDto(
        Office: new ClaimReportOfficeDto("0123456789", "テスト事業所", RegionGrade.None,
            "1000001", "東京都千代田区千代田1-1", "03-0000-0000", "代表取締役 山田太郎"),
        YearMonth: new YearMonth(2026, 5),
        TotalUnit: 12000, TotalCostYen: 1_344_000,
        TotalBenefitYen: 1_209_600, TotalBurdenYen: 134_400,
        SpecVersion: new ClaimReportSpecVersionDto("r6-2026-04", "r7-10", "r1-10"));
    var text = ExtractText(gen.GenerateClaimInvoice(dto));
    text.Should().Contain("介護給付費・訓練等給付費等請求書");
    text.Should().Contain("テスト事業所");
    text.Should().Contain("1000001").And.Contain("東京都千代田区千代田1-1")
                 .And.Contain("03-0000-0000").And.Contain("代表取締役 山田太郎");
    text.Should().Contain("1,344,000");
    text.Should().Contain("r6-2026-04");
}

[Fact]
public void GenerateClaimInvoice_is_deterministic() { /* 同 payload + 同 TimeProvider */ }
```

- [x] **Step 2: テスト赤 → 実装 → 緑**

`GenerateClaimInvoice` を実装（Header に事業所連絡先4 fields を出力、Content に集計テーブル、Footer に spec 3 versions + timestamp）。

- [x] **Step 3: コミット**

```bash
git commit -m "feat(phase3-2/task 11/AC3-5): ClaimReportGenerator.GenerateClaimInvoice with 4 office header fields"
```

---

## Task 12: ClaimReportGenerator.GenerateClaimStatement

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/ClaimReportGenerator.cs`
- Test: `tests/Tsumugi.Infrastructure.Reporting.Tests/ClaimReportGeneratorClaimStatementTests.cs`

**Interfaces:**
- Consumes: `ClaimStatementDto`, `TimeProvider`
- Produces: 請求明細書 PDF byte 列。7 detail フィールド全て（`certificate.municipalityNumber`, `certificate.subsidyMunicipalityNumber`, `certificate.upperLimitManagementProviderNumber`, `claimInput.upperLimitManagementResult`, `claimInput.upperLimitManagedAmountYen`, `claimInput.municipalSubsidyAmountYen`, claim-lines 内訳）を含む

- [x] **Step 1: 失敗テストを書く**

```csharp
[Fact]
public void GenerateClaimStatement_lists_each_recipient_with_lines_and_all_detail_fields()
{
    QuestPdfLicenseConfigurator.Initialize();
    var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-29T00:00:00Z"));
    var dto = new ClaimStatementDto(
        Office: /* ... */,
        YearMonth: new YearMonth(2026, 5),
        Recipients: [
            new RecipientClaimDetailDto(
                Recipient: new ClaimReportRecipientDto("山田太郎", "ヤマダタロウ"),
                Certificate: new ClaimReportCertificateDto("9876543210", "131016", "999999",
                    9300, "1234567890", "管理事業所A"),
                Lines: [
                    new ClaimLineDto(ClaimDetailLineKind.Basic, "B_BASE_W1_C20_S1", 600, 20, 6720),
                    new ClaimLineDto(ClaimDetailLineKind.Addition, "MEAL_PROVISION_I", 30, 20, 336),
                ],
                SubtotalUnit: 630, SubtotalCostYen: 7056, SubtotalBenefitYen: 6351, SubtotalBurdenYen: 705,
                ClaimInput: new ClaimInputSummaryDto("Compliant", 705, 500)),
        ],
        TotalUnit: 630, TotalCostYen: 7056, TotalBenefitYen: 6351, TotalBurdenYen: 705,
        SpecVersion: /* ... */);

    var text = ExtractText(new ClaimReportGenerator(clock).GenerateClaimStatement(dto));
    text.Should().Contain("介護給付費・訓練等給付費等請求明細書");
    text.Should().Contain("山田太郎");
    text.Should().Contain("131016");                     // MunicipalityNumber
    text.Should().Contain("999999");                     // SubsidyMunicipalityNumber
    text.Should().Contain("1234567890").And.Contain("管理事業所A"); // UpperLimitManagementProvider
    text.Should().Contain("B_BASE_W1_C20_S1").And.Contain("MEAL_PROVISION_I");   // claimLines
    text.Should().Contain("6,720").And.Contain("336");   // amounts
}

[Fact]
public void GenerateClaimStatement_is_deterministic() { /* ... */ }

[Fact]
public void GenerateClaimStatement_totals_equal_sum_of_recipient_subtotals() { /* 合計値検証 */ }
```

- [x] **Step 2: 赤 → 実装 → 緑**

明細書は受給者ごとに（Header: 名前 + 受給者証番号 + 市町村番号 + 上限管理事業所） → 明細行テーブル → 小計行 → summary行（上限管理結果 + 上限管理後 + 自治体助成）を並べる。事業所合計をFooterへ。

- [x] **Step 3: コミット**

```bash
git commit -m "feat(phase3-2/task 12/AC3-5): ClaimReportGenerator.GenerateClaimStatement with 7 detail fields + claim-lines"
```

---

## Task 13: GenerateClaimReportsUseCase + DI 配線

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Claim/GenerateClaimReportsUseCase.cs`
- Modify: `src/Tsumugi.App/App.axaml.cs`（DI 登録）
- Test: `tests/Tsumugi.Application.Tests/UseCases/Claim/GenerateClaimReportsUseCaseTests.cs`

**Interfaces:**
- Consumes: `IClaimBatchRepository`, `IClaimSnapshotValidationCodecRegistry`, `IClaimReportGenerator`, `ClaimFinalizationSnapshotReader`
- Produces:
  - `Task<byte[]> GenerateServiceProvisionRecordAsync(Guid officeId, ServiceMonth ym, Guid recipientId, CancellationToken ct)`
  - `Task<byte[]> GenerateClaimInvoiceAsync(Guid officeId, ServiceMonth ym, CancellationToken ct)`
  - `Task<byte[]> GenerateClaimStatementAsync(Guid officeId, ServiceMonth ym, CancellationToken ct)`

- [x] **Step 1: 失敗テストを書く**

```csharp
public sealed class GenerateClaimReportsUseCaseTests
{
    [Fact]
    public async Task GenerateServiceProvisionRecordAsync_parses_v2_payload_and_invokes_generator()
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var ym = new ServiceMonth(2026, 5);

        var v2payload = ClaimFinalizationSnapshotWriter.Write(SampleSnapshot(recipientId, ym));
        var detail = ClaimDetail.Create(/* ... */, calculationSnapshotJson: v2payload, /* ... */);
        var header = ClaimBatch.NewRecord(/* ... officeId ... */);
        var repo = new Mock<IClaimBatchRepository>();
        repo.Setup(r => r.ListHistoryAggregatesAsync(officeId, ym, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ClaimBatchAggregate(header, new[] { detail }) });

        var registry = new ProductionClaimSnapshotValidationCodecRegistry();
        var gen = new Mock<IClaimReportGenerator>();
        gen.Setup(g => g.GenerateServiceProvisionRecord(It.IsAny<ServiceProvisionRecordDto>()))
           .Returns([1, 2, 3]);

        var uc = new GenerateClaimReportsUseCase(repo.Object, registry, gen.Object);
        var bytes = await uc.GenerateServiceProvisionRecordAsync(officeId, ym, recipientId, default);

        bytes.Should().Equal(1, 2, 3);
        gen.Verify(g => g.GenerateServiceProvisionRecord(It.Is<ServiceProvisionRecordDto>(
            dto => dto.Recipient.KanjiName == "山田太郎"
                && dto.Days.Count == 1
                && dto.Days[0].ServiceStartTime == new TimeOnly(9, 0))), Times.Once);
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_selects_latest_non_cancel_revision() { /* ... */ }

    [Fact]
    public async Task GenerateClaimStatementAsync_aggregates_all_recipients_in_latest_revision() { /* ... */ }

    [Fact]
    public async Task Throws_when_no_finalized_revision_exists() { /* InvalidOperationException */ }
}
```

- [x] **Step 2: 実装**

```csharp
public sealed class GenerateClaimReportsUseCase(
    IClaimBatchRepository claimBatchRepository,
    IClaimSnapshotValidationCodecRegistry codecRegistry,
    IClaimReportGenerator generator)
{
    public async Task<byte[]> GenerateServiceProvisionRecordAsync(
        Guid officeId, ServiceMonth serviceMonth, Guid recipientId, CancellationToken ct)
    {
        var (header, detail) = await ResolveLatestDetailAsync(officeId, serviceMonth, recipientId, ct);
        var snapshot = ClaimFinalizationSnapshotReader.Parse(detail.CalculationSnapshotJson);
        var dto = MapToServiceProvisionRecordDto(snapshot);
        return generator.GenerateServiceProvisionRecord(dto);
    }

    public async Task<byte[]> GenerateClaimInvoiceAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var (header, details) = await ResolveLatestBatchAsync(officeId, serviceMonth, ct);
        var firstSnapshot = ClaimFinalizationSnapshotReader.Parse(details[0].CalculationSnapshotJson);
        var dto = new ClaimInvoiceDto(
            Office: MapOffice(firstSnapshot),
            YearMonth: new YearMonth(serviceMonth.Year, serviceMonth.Month),
            TotalUnit: header.TotalUnits,
            TotalCostYen: header.TotalCostYen,
            TotalBenefitYen: header.TotalBenefitYen,
            TotalBurdenYen: header.TotalBurdenYen,
            SpecVersion: MapSpec(firstSnapshot));
        return generator.GenerateClaimInvoice(dto);
    }

    // GenerateClaimStatementAsync も同様
}
```

- [x] **Step 3: DI 登録**

```csharp
services.AddSingleton<IClaimReportGenerator>(sp => new ClaimReportGenerator(sp.GetRequiredService<TimeProvider>()));
services.AddScoped<GenerateClaimReportsUseCase>();
```

- [x] **Step 4: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 13/AC3-6): GenerateClaimReportsUseCase reads v2 snapshot and wires DI"
```

---

## Task 14: ClaimPreparationViewModel + View に「帳票出力」セクション

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/ClaimPreparationViewModel.cs`
- Create: `src/Tsumugi.App/ViewModels/Claim/ClaimReportSection.cs`（画面状態と帳票コマンド群）
- Modify: `src/Tsumugi.App/Views/ClaimPreparationView.axaml`
- Test: `tests/Tsumugi.App.Tests/ViewModels/Claim/ClaimReportSectionTests.cs`

**Interfaces:**
- Consumes: `GenerateClaimReportsUseCase`, `IFileSaveService`, 既存 `ClaimPreparationViewModel.History`
- Produces:
  - `ClaimReportSection` は `Kind`（`enum { ServiceProvisionRecord, Invoice, Statement }`）、`SelectedRecipient`（実績記録票のみ必要）、3 つの `SaveCommand`
  - `ClaimPreparationViewModel` に `ReportSection` プロパティ追加
  - `ClaimPreparationView.axaml` に「帳票出力」パネル追加（確定済 revision があるときのみ表示）

- [x] **Step 1: 失敗テストを書く**

`ClaimReportSectionTests.cs`：3 SaveCommand のうち、確定済 revision が無ければ全て `CanExecute=false`、あれば実績記録票は SelectedRecipient 必須で他2つは月×事業所だけで有効。`IFileSaveService` mock で `PickSaveFileAsync` を stub し、書き込みバイトが `GenerateClaimReportsUseCase` の返却と一致することを確認。ユーザーキャンセル（`PickSaveFileAsync` が null）で例外を投げないこと。

- [x] **Step 2: 実装（ViewModel + XAML）**

`ClaimReportSection.cs`：CommunityToolkit.Mvvm の `[ObservableProperty]` / `[RelayCommand]` を使う。`ClaimPreparationViewModel` は既存の `History` から確定済 revision の有無を判定して `ReportSection.HasFinalizedRevision` を通知。XAML は「帳票出力」`Border`（`IsVisible={Binding ReportSection.HasFinalizedRevision}`）内に3ボタン + 受給者選択 ComboBox。

デフォルトファイル名：
- 実績記録票: `{Recipient.KanjiName}_サービス提供実績記録票_{Year}-{Month:D2}.pdf`
- 請求書: `{Office.Name}_請求書_{Year}-{Month:D2}.pdf`
- 明細書: `{Office.Name}_請求明細書_{Year}-{Month:D2}.pdf`

- [x] **Step 3: テスト緑 + コミット**

```bash
git commit -m "feat(phase3-2/task 14/AC3-6): ClaimPreparation view reports section saves 3 PDF types"
```

- [x] **Step 4: 起動確認（自動）**

Run: `dotnet run --project src/Tsumugi.App --no-build`
Expected: `Program.cs` の Splash → MainWindow 表示 → 「請求確定」タブに「帳票出力」セクションが表示される（起動後即終了で良い、視覚確認は Task 15 で人手）

- [x] **Step 5: DI で `IFileSaveService` と `GenerateClaimReportsUseCase` の解決確認**

`services.GetRequiredService<ClaimPreparationViewModel>()` が例外を投げないことを既存の App composition テストで確認。無ければ smoke test を追加：

```csharp
[Fact]
public void ClaimPreparationViewModel_can_be_resolved_from_composition_root()
{
    using var sp = new ServiceCollection().AddAppServices(/* real config */).BuildServiceProvider();
    var vm = sp.GetRequiredService<ClaimPreparationViewModel>();
    vm.Should().NotBeNull();
    vm.ReportSection.Should().NotBeNull();
}
```

- [x] **Step 6: コミット**

```bash
git commit -m "feat(phase3-2/task 14/AC3-6): confirm ClaimPreparationViewModel composition resolves report section"
```

---

## Task 15: 受け入れ証跡 + CLAUDE.md + open-questions クローズ

**Files:**
- Create: `docs/phase3-2-acceptance.md`
- Modify: `docs/open-questions.md`（21 report フィールド関連の open question を [x] へ）
- Modify: `docs/phase3-claim-field-mapping.md`（21 report フィールドの status を「実装済」相当に、Phase 3-1 open note 部分を修正）
- Modify: `CLAUDE.md`（「現在地」を Phase 3-2 完了に更新）
- Modify: `CHANGELOG.md`（Phase 3-2 完了エントリ）

**Interfaces:**
- Consumes: 全 Task の成果、`./build/ci.sh` の結果
- Produces: spec §15 の成功基準5項目に対する判定 + 証跡

- [x] **Step 1: `./build/ci.sh` を実行して末尾出力を保存**

Run: `./build/ci.sh 2>&1 | tail -40`
Expected: `==> CI OK`

- [x] **Step 2: `docs/phase3-2-acceptance.md` を作成**

Phase 3-1 acceptance のフォーマットに倣う。項目：
- spec §15 の成功基準5項目のそれぞれについて 判定（✔/部分/✘）・証跡（テスト名 or 実行コマンドと結果）を表で記録
- ci.sh 末尾出力（テスト件数・警告ゼロ・カバレッジ）
- 21 report フィールドが PDF 抽出テキストで検出可能な証跡（Task 10/11/12 のテスト名列挙）
- codec v1 が repo に残っていないこと（`grep -rn ClaimSnapshotValidationCodecV1 src tests` = 出力なし）を実行結果で示す
- 依存方向テスト緑（`Tsumugi.Infrastructure.Reporting → Application/Domain` のみ）
- オフライン検査緑（Reporting 含む）
- Phase 3-1 と同種の残課題：GUI 手動貫通確認は本 phase でも人手実施推奨と明記

- [x] **Step 3: `docs/open-questions.md` の該当 21 report フィールド関連項目を [x] へ**

対応する open-question 行（`Phase 3-1へ送る未実装入力` セクションと関連する 21 `report:*` フィールドをrefer するもの）を `[x]` に変え、末尾に `2026-07-20 クローズ / Phase 3-2` を追記。

- [x] **Step 4: `docs/phase3-claim-field-mapping.md` の Phase 3-1 note を更新**

「Phase 3-1へ送る未実装入力 (51 mapping entries)」セクションの上部 note を次のように更新：

> **実装進捗 (2026-07-20 / Phase 3-2 完了)**: 21 `report:*` フィールドは snapshot v2 (`claim-snapshot-v2`) 経由で確定時に凍結され、3帳票の抽出テキストで検出可能。30 `provider:*` フィールドは Phase 3-3 (CSV) スコープに引き継ぐ。

- [x] **Step 5: `CLAUDE.md` の「現在地」を更新**

「ワークフロー」節の現在地を次で置き換える：

```markdown
- **現在地**: フェーズ0・1・2、Phase 3-0、Phase 3-1、Phase 3-2 は完了（Phase 3-2 は snapshot v2 + 3帳票、spec: `docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md`、計画: `docs/superpowers/plans/2026-07-20-phase3-2-reports.md`、受け入れ証跡: `docs/phase3-2-acceptance.md`）。次は Phase 3-3 CSV 生成（30 `provider:*` フィールドの入力UI補完 + CSV writer + close 統合）。GUI 手動貫通確認（プレビュー→確定→帳票保存×3）は残課題として open-questions に維持。
```

「仕様の所在」節に spec / plan / acceptance の3行を追加。

- [x] **Step 6: `CHANGELOG.md` に Phase 3-2 エントリ追記**

新規セクション：

```markdown
## Phase 3-2 完了 (2026-07-20)

- Claim snapshot codec v2 (`claim-snapshot-v2`) で office / recipient / certificate / dailyRecord[] / claimInput / intensiveSupportEpisode / claimLines[] を確定時に凍結
- 3帳票 PDF: サービス提供実績記録票 / 介護給付費等請求書 / 介護給付費等請求明細書 を `ClaimPreparation` 画面から保存可能
- 21 `report:*` フィールドを snapshot v2 で凍結、CSV仕様 21件をクローズ
- 破壊的変更: `ClaimSnapshotValidationCodecV1` 削除（Phase 3-1 マージ直後のため production 影響なし）
- ADR 0029 / 0030 追加
```

- [x] **Step 7: 最終コミット**

```bash
git add docs/phase3-2-acceptance.md docs/open-questions.md docs/phase3-claim-field-mapping.md CLAUDE.md CHANGELOG.md
git commit -m "docs(phase3-2/task 15): record phase 3-2 acceptance and update project state"
```

- [x] **Step 8: 本 plan の Task 15 チェックを入れて締める**

Task 1〜14 のチェックボックスと Task 15 Step 1〜8 のチェックボックスをすべて `[x]` にし、コミット：

```bash
git add docs/superpowers/plans/2026-07-20-phase3-2-reports.md
git commit -m "docs(phase3-2/task 15): check off plan task 15"
```

---

## Phase 3-2 全体受け入れ基準（spec §15 対応）

- [ ] **AC3-5** 3帳票（実績記録票・請求書・明細書）で CJK 抽出（Kangxi 正規化後）に主要漢字が含まれ、決定論と合計値検証が緑（Task 10/11/12）
- [ ] **AC3-6** `ClaimPreparation` 画面から3帳票を保存できる（Task 13/14）
- [ ] `docs/phase3-claim-field-mapping.md` の21 `report:*` フィールド全てが snapshot v2 に凍結され、対応する PDF 描画テキストで抽出可能（Task 3/10/11/12）
- [ ] codec v1 → v2 の破壊的置換完了、backwards-compat コード無し（Task 2）
- [ ] `./build/ci.sh` 緑、依存方向不変、オフライン検査緑（Reporting 含む）（Task 15）

## 参考

- 正本spec: `docs/superpowers/specs/2026-07-20-phase3-2-reports-redesign-design.md`
- 帳票フィールド仕様: `docs/phase3-claim-field-mapping.md`
- Phase 3-1 受け入れ証跡: `docs/phase3-1-acceptance.md`
- Phase 4/S1 フォント埋込計画: `docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md`
- Phase 2 帳票実装（reference）: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs`、`tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`
- Phase 3 実装指示書: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`
