# Phase 3-1 Task 7 Claim Input Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tasks 5〜6で追加した請求入力・算定根拠のappend-only履歴を、未入力と正式な0円を区別したままSQLiteへ保存し、raw履歴を返す6 repository、migration、DB制約、append-only guard、DI登録を完成する。

**Architecture:** Applicationには6個の用途別repository契約と上限額管理結果票aggregateだけを置き、Infrastructureのscoped repositoryは同一`TsumugiDbContext`へ追加をstageする。EF Core 10のcomplex typeをsplit-column mappingに使い、SQLiteのnamed CHECK・partial unique index・RESTRICT FKでlineageと入力状態を守る。実効版選択、stale-headのerror変換、operation-local transactionはTask 8以降へ残す。

**Tech Stack:** .NET 10 / C#、EF Core 10.0.9、SQLite、xUnit、FluentAssertions、`dotnet-ef`、既存`DateRangeJson`

---

## 実行前提

- 作業場所は `/Users/hiro/Projetct/GitHub/Tsumugi/.worktrees/phase3-1-claim-calculation`。
- 開始基準は `aec9ca0`（Task 7詳細設計コミット）。
- 正本は `docs/superpowers/specs/2026-07-11-phase3-1-task7-persistence-design.md`。
- 上位設計は `docs/superpowers/specs/2026-07-11-phase3-1-claim-calculation-and-input-foundation-design.md`。
- 元計画のTask 7より正本specを優先する。元計画で漏れているDomain prerequisite、aggregate、DI、CompositionRoot、`ClaimBatchMigrationTests`回帰修正もTask 7に含む。
- 実装では `@superpowers:test-driven-development` を使い、各タスクでRedを確認してから最小実装を行う。
- 完了を主張する前に `@superpowers:verification-before-completion` を使う。
- graphifyの既存graphはTask 6以降を含まないため、ファイル名・行番号・APIはlive worktreeを正本にする。

## スコープ外

- Task 8の保存use case、DTO、stale-head error code、`BEGIN IMMEDIATE`。
- repository内の実効版選択、履歴修復、複数rootの黙示的な除外。
- `AverageWageBandOptionVersionRule`、状態別辞書、transition resolverの永続化。
- Cancel statementにlineを禁止するSQLite trigger。これはDomain/Applicationの検証責務とする。
- Task 9のCSV条件判定と、その後のApp入力配線。
- `SqliteFixture.cs`、`DateRangeJson.cs`、`Phase31ClaimInputRoundTripTests.cs`の変更。

## ファイル構成

### 新規作成（19ファイル）

- `src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs` — 6 repository契約。
- `src/Tsumugi.Application/Abstractions/UpperLimitManagementStatementAggregate.cs` — headerと防御的にコピーしたline群。
- `src/Tsumugi.Infrastructure/Persistence/ClaimInputRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/IntensiveSupportEpisodeRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/AverageWageAnnualEvidenceRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/OfficeClaimProfileRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/CertificateClaimEvidenceRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/UpperLimitManagementStatementRepository.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimInputConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/IntensiveSupportEpisodeConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/AverageWageAnnualEvidenceConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/OfficeClaimProfileConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/CertificateClaimEvidenceConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/UpperLimitManagementStatementConfiguration.cs`
- `src/Tsumugi.Infrastructure/Persistence/Configurations/UpperLimitManagementStatementLineConfiguration.cs`
- `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.cs` — `dotnet ef`生成後、backfill順だけ調整。
- `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.Designer.cs` — `dotnet ef`生成物。
- `tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputMigrationTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs`

### 既存変更（9ファイル）

- `src/Tsumugi.Domain/Logic/Claim/ClaimInputPolicy.cs:39-60` — forged Cancel payloadを拒否。
- `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimInputPolicyTests.cs:26-82` — payload 7項目の取消検証。
- `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs:25-37` — 7 DbSet。
- `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs:13-32` — 新規7 entity型。
- `src/Tsumugi.Infrastructure/DependencyInjection.cs:20-46` — 6 scoped repository。
- `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs` — `dotnet ef`生成更新。
- `tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs:13,107-115` — suffix位置基準のmigration解決。
- `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs:15-90` — 7 entityのModify/Delete拒否。
- `tests/Tsumugi.App.Tests/CompositionRootTests.cs:22-53` — 6 repositoryの実解決。

## 固定するrelational contract

### Table / business key / 安定順

| Table | New-only business key | Repository read order |
| --- | --- | --- |
| `ClaimInputs` | `OfficeId, RecipientId, ServiceMonthKey` | `RootId, Revision` |
| `IntensiveSupportEpisodes` | `OfficeId, RecipientId` | `RootId, Revision` |
| `AverageWageAnnualEvidences` | `OfficeId, SourceFiscalYear` | `RootId, Revision` |
| `OfficeClaimProfiles` | `OfficeId, EffectiveFrom, EffectiveTo` | client側で`EffectiveFrom, EffectiveTo(nullは末尾), RootId, Revision` |
| `CertificateClaimEvidences` | `CertificateId, Validity` | client側で`Validity.Start, Validity.End(nullは末尾), RootId, Revision` |
| `UpperLimitManagementStatements` | `RecipientId, CertificateId, ManagingOfficeId, ServiceMonthKey` | `CertificateId, RootId, Revision` |
| `UpperLimitManagementStatementLines` | header従属 | `LineNumber` |

`OfficeClaimProfiles`はSQLiteのnull一意性を避けるため、closed period用 `(OfficeId, EffectiveFrom, EffectiveTo)` とopen period用 `(OfficeId, EffectiveFrom)` の2本の`Kind = 1` partial unique indexを作る。statement検索は意図的に`CertificateId`をpredicateへ含めず、競合する証の候補も返す。

### 共通lineage contract（6 header table）

各header tableに以下を同じ名前規則で置く。

- `UX_<Table>_RootId_Revision` — `(RootId, Revision)` unique。
- `UX_<Table>_ExpectedHeadId` — `ExpectedHeadId IS NOT NULL` filtered unique。
- `FK_<Table>_<Table>_RootId` — self FK、`RESTRICT`。
- `FK_<Table>_<Table>_ExpectedHeadId` — self FK、`RESTRICT`。
- `CK_<Table>_RevisionLineage` — 次の論理式。

```sql
"Revision" >= 1
AND "Kind" IN (1, 2, 3)
AND (
  ("Revision" = 1 AND "RootId" = "Id" AND "Kind" = 1 AND "ExpectedHeadId" IS NULL)
  OR
  ("Revision" >= 2 AND "RootId" <> "Id" AND "Kind" IN (2, 3) AND "ExpectedHeadId" IS NOT NULL)
)
```

業務FKもすべて`RESTRICT`とする。Office / Recipient / Certificate / Statementの削除でclaim履歴をcascade deleteしない。

### 値オブジェクト contract

- `ServiceMonth` / `ServiceMonth?`: `YYYYMM`の`INTEGER`。nullable側も明示converterを設定する。
- `DateRange` / `DateRange?`: `DateRangeJson`による決定的`TEXT`。
- `ClaimMasterVersion?`: `Value`との明示converter、nullable `TEXT`、最大64文字。
- `EnteredYen`: `{Prefix}IsEntered INTEGER NOT NULL` と `{Prefix}ValueYen INTEGER NULL`。
- `AverageWageBandOption?`: `{Prefix}Kind` と `{Prefix}OfficialOptionCode`。両方null、またはKindが1〜3かつcodeが正数。
- `VersionedAverageWageBandOption?`: `{Prefix}MasterVersion`、`{Prefix}Kind`、`{Prefix}OfficialOptionCode`。3列ともnull、またはversionが非空白・64文字以下、Kindが1〜3、codeが正数。

各`EnteredYen` pairには次のnamed CHECKを付け、未入力と0円入力を分離する。

```sql
(("<Prefix>IsEntered" = 0 AND "<Prefix>ValueYen" IS NULL)
 OR ("<Prefix>IsEntered" = 1 AND "<Prefix>ValueYen" >= 0))
```

文字列は正本spec又は既存Domain定数が長さを決めているものだけ`HasMaxLength`を付ける。Task 7で未決定の証跡ID・参照・理由に任意の最大長を発明しない。

### Cancel contract

- `ClaimInput`: 7 payload列がすべてnull。
- `IntensiveSupportEpisode`: Cancelは`StartDate IS NULL`、New/Correctはnon-null。
- `AverageWageAnnualEvidence`: Cancelは金額・人数・日数・完全性・証跡列がすべてnull。
- `OfficeClaimProfile`: Cancelはmaster、option、R8状態、日付、経過措置、証跡列がすべてnull。
- `CertificateClaimEvidence`: Cancelは2つの`EnteredYen`が`false/null`、enumは0、残りの業務値・証跡がnull。
- `UpperLimitManagementStatement`: Cancelは文字列sentinelが空文字、enumが0、確認状態が`false/null`、4つの`EnteredYen`が`false/null`。
- Cancel statementのline 0件規則はDB trigger化しない。

---

### Task 1: ClaimInput CancelのDomain不変条件を閉じる

**Files:**
- Modify: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimInputPolicyTests.cs:50-82`
- Modify: `src/Tsumugi.Domain/Logic/Claim/ClaimInputPolicy.cs:39-60`

- [ ] **Step 1: payloadを持つCancelの失敗testを書く**

`CancelPayloadHistories`を追加し、取消行へ7項目を1つずつ戻したケースを列挙する。

```csharp
public static TheoryData<string, ClaimInput> CancelPayloads()
{
    var root = New();
    var cancel = Cancel(root);
    return new()
    {
        { "result", cancel with { UpperLimitManagementResult = UpperLimitManagementResult.Result1 } },
        { "managed amount", cancel with { UpperLimitManagedAmountYen = 0 } },
        { "subsidy amount", cancel with { MunicipalSubsidyAmountYen = 0 } },
        { "start month", cancel with { ExceptionalUsageStartMonth = Month } },
        { "end month", cancel with { ExceptionalUsageEndMonth = Month } },
        { "days", cancel with { ExceptionalUsageDays = 0 } },
        { "standard days", cancel with { StandardUsageDayTotal = 0 } },
    };
}

[Theory]
[MemberData(nameof(CancelPayloads))]
public void Cancel_with_claim_payload_is_rejected(string _, ClaimInput cancellation)
{
    var root = New();
    FluentActions.Invoking(() => ClaimInputPolicy.ValidateHistory([root, cancellation]))
        .Should().Throw<InvalidOperationException>();
}
```

- [ ] **Step 2: testが意図どおりRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimInputPolicyTests -v normal
```

Expected: 新しい7ケースだけが「例外が発生しない」ためFAILする。

- [ ] **Step 3: Cancel payloadの最小validationを追加する**

`ValidateHistory`の各row検証で、enumのclosed-set確認後に次を追加する。

```csharp
if (input.Kind == RecordKind.Cancel
    && (input.UpperLimitManagementResult is not null
        || input.UpperLimitManagedAmountYen is not null
        || input.MunicipalSubsidyAmountYen is not null
        || input.ExceptionalUsageStartMonth is not null
        || input.ExceptionalUsageEndMonth is not null
        || input.ExceptionalUsageDays is not null
        || input.StandardUsageDayTotal is not null))
    throw Invalid("ClaimInputのCancelは請求入力値を持てません。");
```

- [ ] **Step 4: Domain testをGreenにする**

Run: Task 1 Step 2と同じ。

Expected: `ClaimInputPolicyTests`が全件PASS。

- [ ] **Step 5: Task 1をコミットする**

```bash
git add src/Tsumugi.Domain/Logic/Claim/ClaimInputPolicy.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimInputPolicyTests.cs
git commit -m "fix(phase3-1/AC3-8): reject claim input cancel payload"
```

---

### Task 2: Application repository契約とstatement aggregateを定義する

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs`
- Create: `src/Tsumugi.Application/Abstractions/UpperLimitManagementStatementAggregate.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs`

- [ ] **Step 1: aggregateの防御的コピーtestを先に書く**

`ClaimInputRepositoryTests.cs`はまずaggregateだけを対象にする。
同じtest classへ、required fieldsを満たす`ClaimRows.Statement()`と
`ClaimRows.Line(statementId, lineNumber)`のprivate factoryも置き、
後続repository testsで再利用する。factoryはDomain Policyの検証対象ではなく、
aggregateのコピーと順序だけを観測できる最小の有効値を返す。

```csharp
[Fact]
public void Statement_aggregate_copies_and_orders_lines()
{
    var statement = ClaimRows.Statement();
    var lines = new List<UpperLimitManagementStatementLine>
    {
        ClaimRows.Line(statement.Id, 2),
        ClaimRows.Line(statement.Id, 1),
    };

    var aggregate = new UpperLimitManagementStatementAggregate(statement, lines);
    lines.Clear();

    aggregate.Lines.Select(line => line.LineNumber).Should().Equal(1, 2);
}
```

- [ ] **Step 2: compile failureを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimInputRepositoryTests -v normal
```

Expected: `UpperLimitManagementStatementAggregate`が存在せずcompile FAIL。

- [ ] **Step 3: 6個の型別interfaceを追加する**

`IClaimInputRepositories.cs`へ詳細設計の署名をそのまま置く。

```csharp
public interface IClaimInputRepository
{
    Task AddAsync(ClaimInput input, CancellationToken ct);
    Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
        Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct);
}

public interface IIntensiveSupportEpisodeRepository
{
    Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct);
    Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
        Guid officeId, Guid recipientId, CancellationToken ct);
}

public interface IAverageWageAnnualEvidenceRepository
{
    Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct);
    Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
        Guid officeId, int sourceFiscalYear, CancellationToken ct);
}

public interface IOfficeClaimProfileRepository
{
    Task AddAsync(OfficeClaimProfile profile, CancellationToken ct);
    Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(Guid officeId, CancellationToken ct);
}

public interface ICertificateClaimEvidenceRepository
{
    Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct);
    Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
        Guid certificateId, CancellationToken ct);
}

public interface IUpperLimitManagementStatementRepository
{
    Task AddAsync(
        UpperLimitManagementStatement statement,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines,
        CancellationToken ct);

    Task<IReadOnlyList<UpperLimitManagementStatementAggregate>> ListHistoryAggregatesAsync(
        Guid managingOfficeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        CancellationToken ct);
}
```

- [ ] **Step 4: aggregateを最小実装する**

```csharp
public sealed record UpperLimitManagementStatementAggregate
{
    public UpperLimitManagementStatementAggregate(
        UpperLimitManagementStatement header,
        IEnumerable<UpperLimitManagementStatementLine> lines)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);
        Header = header;
        Lines = lines.OrderBy(line => line.LineNumber).ToArray();
    }

    public UpperLimitManagementStatement Header { get; }
    public IReadOnlyList<UpperLimitManagementStatementLine> Lines { get; }
}
```

- [ ] **Step 5: focused testとApplication buildを通す**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Statement_aggregate_copies_and_orders_lines -v normal
dotnet build src/Tsumugi.Application/Tsumugi.Application.csproj --no-restore
```

Expected: 両方PASS。

- [ ] **Step 6: Task 2をコミットする**

```bash
git add src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs \
  src/Tsumugi.Application/Abstractions/UpperLimitManagementStatementAggregate.cs \
  tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs
git commit -m "feat(phase3-1/AC3-8): define claim input repositories"
```

---

### Task 3: migration契約testをRedにし既存回帰testを将来対応する

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputMigrationTests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs:107-115`

- [ ] **Step 1: target migrationの解決helperを書く**

`Phase31ClaimInputMigrationTests`は`_Phase31ClaimInputFoundation` suffixで対象を探し、対象位置の1つ前をpreviousとする。timestampを固定しない。

```csharp
private const string MigrationSuffix = "_Phase31ClaimInputFoundation";

private static (string Target, string Previous) ResolveMigration(TsumugiDbContext context)
{
    var migrations = context.Database.GetMigrations().ToArray();
    var targetIndex = Array.FindIndex(migrations,
        migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
    targetIndex.Should().BeGreaterThan(0);
    return (migrations[targetIndex], migrations[targetIndex - 1]);
}
```

- [ ] **Step 2: previous→seed→Upの失敗testを書く**

file-backed SQLiteと`IMigrator`を使い、previous migrationまで進めてからraw SQLでOffice、Recipient、Certificate 2件、ContractedProvider、DailyRecordを投入する。2つのCertificateは同一Recipientかつ重複期間にし、Up後も別rootになることを固定する。

Assert:

- 既存row数、業務列、監査列、ConcurrencyTokenが保持される。
- 各Certificateは`RootCertificateId = Id / Revision = 1 / ExpectedHeadCertificateId = null`。
- 既存nullable claim列はnull。
- DailyRecordの`MedicalCoordinationType / TrialUseSupportType / RecipientConfirmation`は0。
- 新規7 tableは存在し、0件。

- [ ] **Step 3: schema introspection testを書く**

次のhelperを同test file内へ置く。

```csharp
private static Task<IReadOnlyDictionary<string, SqliteColumn>> ReadColumnsAsync(
    SqliteConnection connection, string tableName);
private static Task<IReadOnlyDictionary<string, SqliteIndex>> ReadIndexesAsync(
    SqliteConnection connection, string tableName);
private static Task<IReadOnlyList<string>> ReadIndexColumnsAsync(
    SqliteConnection connection, string indexName);
private static Task<IReadOnlyList<SqliteForeignKey>> ReadForeignKeysAsync(
    SqliteConnection connection, string tableName);
private static Task<string> ReadCreateTableSqlAsync(
    SqliteConnection connection, string tableName);
private static Task<bool> TableExistsAsync(
    SqliteConnection connection, string tableName);

private sealed record SqliteColumn(string Name, string Type, bool NotNull, string? DefaultValue);
private sealed record SqliteIndex(string Name, bool IsUnique, bool IsPartial);
private sealed record SqliteForeignKey(
    string FromColumn, string PrincipalTable, string PrincipalColumn, string OnDelete);
```

`PRAGMA table_info/index_list/index_info/foreign_key_list`と`sqlite_master.sql`で、7 tableの列型・nullability・index列順・filtered unique・RESTRICT FK・named CHECKを検査する。文字列比較だけでなく、`ServiceMonthKey`が`INTEGER`、`DateRange`が`TEXT`、`EnteredYen` pairが`INTEGER + nullable INTEGER`であることを固定する。

- [ ] **Step 4: constraint violation testを書く**

各違反を独立DB又は独立transactionでraw SQL挿入し、
`SqliteException.SqliteErrorCode == 19`を確認する。named CHECK違反だけは例外messageに
CHECK名が含まれることも確認する。SQLiteのUNIQUE違反は通常table/column、FK違反は
汎用messageしか返さないため、UNIQUE/FK名を例外messageへ要求しない。
UNIQUE/FKの正しい対象はStep 3のschema introspectionで名前・列順・参照先を固定し、
違反test側では他制約を満たすrowを用意して対象制約だけを孤立させる。

- invalid revision 1 metadata。
- duplicate `(RootId, Revision)`。
- duplicate non-null `ExpectedHeadId` branch。
- business FK違反。
- `EnteredYen(false, value)`と`EnteredYen(true, null/-1)`。
- forged Cancel payload。
- statement lineのorphan、重複line number、重複office number。

- [ ] **Step 5: Down→Upのtestを書く**

Up後にpreviousへDownし、新規7 tableとCertificate lineage列が消える一方、旧tableのseed rowが残ることを確認する。再Up後は同じCertificateが再び独立revision-1 rootになり、制約とindexが復元されることを確認する。

- [ ] **Step 6: migration未作成によるRedを確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Phase31ClaimInputMigrationTests -v normal
```

Expected: `_Phase31ClaimInputFoundation`が見つからずFAIL。SQLやfixture初期化で先に失敗する場合はtest setupを直し、migration欠落が最初の失敗になるまで進める。

- [ ] **Step 7: `ClaimBatchMigrationTests`のlatest仮定を外す**

```csharp
private static (string Target, string Previous) ResolveClaimMigration(TsumugiDbContext context)
{
    var migrations = context.Database.GetMigrations().ToArray();
    var targetIndex = Array.FindIndex(migrations,
        migration => migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
    targetIndex.Should().BeGreaterThan(0);
    return (migrations[targetIndex], migrations[targetIndex - 1]);
}
```

test名とlocal変数も`Latest`ではなく`Target`へ変更する。

- [ ] **Step 8: 既存ClaimBatch migration testだけはGreenを維持する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimBatchMigrationTests -v normal
```

Expected: PASS。Task 3はRedだけをコミットせず、Task 4のschema実装と一緒にコミットする。

---

### Task 4: 7 entityのEF configurationと単一migrationを完成する

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/ClaimInputConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/IntensiveSupportEpisodeConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/AverageWageAnnualEvidenceConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/OfficeClaimProfileConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/CertificateClaimEvidenceConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/UpperLimitManagementStatementConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/UpperLimitManagementStatementLineConfiguration.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs:25-37`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/<timestamp>_Phase31ClaimInputFoundation.Designer.cs`
- Modify: `src/Tsumugi.Infrastructure/Migrations/TsumugiDbContextModelSnapshot.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputMigrationTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs`

- [ ] **Step 1: 7 DbSetを追加する**

`TsumugiDbContext`のPhase 3 sectionへ次を追加する。

```csharp
public DbSet<ClaimInput> ClaimInputs => Set<ClaimInput>();
public DbSet<IntensiveSupportEpisode> IntensiveSupportEpisodes => Set<IntensiveSupportEpisode>();
public DbSet<AverageWageAnnualEvidence> AverageWageAnnualEvidences => Set<AverageWageAnnualEvidence>();
public DbSet<OfficeClaimProfile> OfficeClaimProfiles => Set<OfficeClaimProfile>();
public DbSet<CertificateClaimEvidence> CertificateClaimEvidences => Set<CertificateClaimEvidence>();
public DbSet<UpperLimitManagementStatement> UpperLimitManagementStatements => Set<UpperLimitManagementStatement>();
public DbSet<UpperLimitManagementStatementLine> UpperLimitManagementStatementLines => Set<UpperLimitManagementStatementLine>();
```

- [ ] **Step 2: simple history configurationsを実装する**

`ClaimInputConfiguration`と`IntensiveSupportEpisodeConfiguration`へ、共通lineage、business FK、New-only index、Cancel CHECK、ServiceMonth converterを明示する。audit fieldsは既存Phase 3 entityと同じく`CreatedBy`最大64、`CreatedAt`required、`ConcurrencyToken`を列化する。

- [ ] **Step 3: annual evidenceとoffice profile configurationsを実装する**

`AverageWageAnnualEvidenceConfiguration`へOffice FK、年度business index、取消列CHECKを置く。

`OfficeClaimProfileConfiguration`では次を分離する。

- nullable `ClaimMasterVersion` converter。
- nullable option complex type。
- nullable nested versioned option complex typeをEarlier/Laterの別prefixへ展開。
- nullable `ServiceMonth` converter。
- nullable `DateRange`を`DateRangeJson`へ変換。
- open/closed New-only index 2本。
- Cancel payload CHECK。

optional complexは`.IsRequired(false)`を明示し、all-null rowをdefault structへ読み替えない。

- [ ] **Step 4: certificate evidence configurationを実装する**

- `Validity` / `Article31EffectivePeriod`を`DateRangeJson`へ変換。
- `MonthlyCostCap`と`Article31AmountYen`をそれぞれ2列へ展開。
- Certificate FKは`RESTRICT`。
- certificate + validityのNew-only partial unique index。
- 各EnteredYen CHECK、enum closed set、Cancel payload CHECK。

- [ ] **Step 5: statement header / line configurationsを実装する**

headerはServiceMonth converter、Recipient / Certificate / ManagingOfficeの3 FK、4個の`EnteredYen` split、共通lineage、New-only business index、Cancel CHECKを持つ。

lineは次を必須にする。

```text
PK Id
FK StatementId -> UpperLimitManagementStatements.Id RESTRICT
UX (StatementId, LineNumber)
UX (StatementId, OfficeNumber)
CHECK LineNumber >= 1
EnteredYen CHECK × 3
```

- [ ] **Step 6: model buildでmapping errorを先に除く**

```bash
dotnet build src/Tsumugi.Infrastructure/Tsumugi.Infrastructure.csproj --no-restore
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Phase31ClaimInputRoundTripTests -v normal
```

Expected: compileと`EnsureCreated` model生成がPASS。migration testはまだRedのまま。

- [ ] **Step 7: migrationを一度だけ生成する**

```bash
dotnet tool restore
dotnet ef migrations add Phase31ClaimInputFoundation \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

Expected: timestamp付きmigration、designer、snapshotが生成され、既存最新`20260710154827_AddClaimBatchAndDetail`より後になる。手動renameしない。

- [ ] **Step 8: Certificate backfill順を生成migration内で補正する**

生成結果を目視し、`Up`が次の順になるよう最小調整する。

```text
1. RootCertificateId / Revision / ExpectedHeadCertificateId をnullable追加
2. UPDATE Certificates SET RootCertificateId = Id, Revision = 1,
   ExpectedHeadCertificateId = NULL
3. RootCertificateId / Revisionをnon-null化
4. CK_Certificates_RevisionLineageと2 unique indexを追加
```

既存Certificate同士を統合するSQL、claim値を旧列から推測するSQL、`Guid.Empty`をrootへ入れるdefaultを残さない。既存Office / ContractedProvider / DailyRecord / Certificateのnullable claim列はnullのまま、DailyRecordの3 enumだけdefault 0とする。

- [ ] **Step 9: migration testをGreenにする**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~Phase31ClaimInputMigrationTests|FullyQualifiedName~ClaimBatchMigrationTests' \
  -v normal
```

Expected: previous→seed→Up、constraint違反、Down→Up、既存ClaimBatch回帰がすべてPASS。

- [ ] **Step 10: modelとmigration snapshotの一致を確認する**

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

Expected: `No changes have been made to the model since the last migration.`、exit 0。

- [ ] **Step 11: Task 3〜4をコミットする**

```bash
git add src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs \
  src/Tsumugi.Infrastructure/Persistence/Configurations \
  src/Tsumugi.Infrastructure/Migrations \
  tests/Tsumugi.Infrastructure.Tests/Phase31ClaimInputMigrationTests.cs \
  tests/Tsumugi.Infrastructure.Tests/ClaimBatchMigrationTests.cs
git commit -m "feat(phase3-1/AC3-8): add claim input persistence schema"
```

---

### Task 5: 6 repositoryのraw履歴readとstage-only addを実装する

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/ClaimInputRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/IntensiveSupportEpisodeRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/AverageWageAnnualEvidenceRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/OfficeClaimProfileRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/CertificateClaimEvidenceRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/UpperLimitManagementStatementRepository.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs`

- [ ] **Step 1: repository testを全6契約へ拡張する**

1つのtest file内で次を固定する。

- `AddAsync`後、`SaveChangesAsync`前はDBに行がなくChangeTrackerはAdded。
- 実`EfUnitOfWork`の1回の`SaveChangesAsync`後に永続化され、ChangeTrackerがclearされる。
- 各readは全候補を返し、`AsNoTracking`でChangeTrackerを空に保つ。
- revision、business period、certificate/rootの安定順。
- repositoryが複数root・重複候補をrepair又は有効選択しない。
- nullable/non-null ServiceMonth、DateRange、ClaimMasterVersion、option snapshotをround-tripする。
- `EnteredYen(true, 0)`と`EnteredYen(false, null)`を区別する。
- Cancel sentinelをround-tripする。
- statementのheaderとlineを同じcontextへstageし、lineを番号順で返す。
- line constraint failure時、1回の`SaveChangesAsync` transactionがrollbackし、別contextからheaderもlineも0件になる。

- [ ] **Step 2: repository未実装によるRedを確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimInputRepositoryTests -v normal
```

Expected: 6 repository classが存在せずcompile FAIL。

- [ ] **Step 3: 単純5 repositoryを実装する**

Addはnull guard後に対応DbSetへ`AddAsync`するだけで、`SaveChangesAsync`を呼ばない。readはDBでbusiness predicateまで絞り、必要ならclient側で値オブジェクト順に並べる。

```csharp
public async Task AddAsync(ClaimInput input, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(input);
    await db.ClaimInputs.AddAsync(input, ct);
}

public async Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
    Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct) =>
    await db.ClaimInputs.AsNoTracking()
        .Where(input => input.OfficeId == officeId
            && input.RecipientId == recipientId
            && input.ServiceMonth == serviceMonth)
        .OrderBy(input => input.RootId)
        .ThenBy(input => input.Revision)
        .ToArrayAsync(ct);
```

Office profileとcertificate evidenceはJSON/nullable期間をSQL sortへ依存させず、候補を`ToArrayAsync`した後に固定順へ並べる。

- [ ] **Step 4: statement repositoryを実装する**

Addはheaderと全lineを同じcontextへstageする。lineの`StatementId`を書き換えたり、途中saveしたりしない。

Readは次の2 queryだけを使う。

1. managing office + recipient + service monthでheader候補を`AsNoTracking`取得。
2. header IDsに属する全lineを`AsNoTracking`取得。

`CertificateId, RootId, Revision`でheaderを並べ、line lookupからaggregateを組み立てる。predicateへ`CertificateId`を追加しない。

- [ ] **Step 5: repository testsをGreenにする**

Run: Task 5 Step 2と同じ。

Expected: 全repository testがPASSし、test終了時のChangeTrackerが空。

- [ ] **Step 6: Task 5をコミットする**

```bash
git add src/Tsumugi.Infrastructure/Persistence/ClaimInputRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/IntensiveSupportEpisodeRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/AverageWageAnnualEvidenceRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/OfficeClaimProfileRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/CertificateClaimEvidenceRepository.cs \
  src/Tsumugi.Infrastructure/Persistence/UpperLimitManagementStatementRepository.cs \
  tests/Tsumugi.Infrastructure.Tests/Persistence/ClaimInputRepositoryTests.cs
git commit -m "feat(phase3-1/AC3-8): add claim input repositories"
```

---

### Task 6: 7 entityをAppendOnlyGuardへ追加する

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs:15-90`
- Modify: `src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs:13-32`

- [ ] **Step 1: guard membership testを7型へ拡張する**

```csharp
[InlineData(typeof(ClaimInput))]
[InlineData(typeof(IntensiveSupportEpisode))]
[InlineData(typeof(AverageWageAnnualEvidence))]
[InlineData(typeof(OfficeClaimProfile))]
[InlineData(typeof(CertificateClaimEvidence))]
[InlineData(typeof(UpperLimitManagementStatement))]
[InlineData(typeof(UpperLimitManagementStatementLine))]
```

- [ ] **Step 2: Modify/Delete save pathの失敗testを書く**

7型を返す`MemberData` factoryを用意し、contextへattachして`EntityState.Modified`又は`Deleted`を設定する。DBへ到達する前に`SaveChangesAsync`が`AppendOnlyViolationException`を返し、`EntityName`が対象型名であることを各stateで確認する。既存ClaimBatch / ClaimDetail testは維持する。

- [ ] **Step 3: Redを確認する**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~AppendOnlyGuardPhase3Tests -v normal
```

Expected: membershipとModify/Deleteの新ケースがFAIL。

- [ ] **Step 4: `AppendOnlyTypes`へ7型を追加する**

```csharp
typeof(ClaimInput),
typeof(IntensiveSupportEpisode),
typeof(AverageWageAnnualEvidence),
typeof(OfficeClaimProfile),
typeof(CertificateClaimEvidence),
typeof(UpperLimitManagementStatement),
typeof(UpperLimitManagementStatementLine),
```

- [ ] **Step 5: guard testをGreenにする**

Run: Task 6 Step 3と同じ。

Expected: 既存と新規の全Phase 3 guard testがPASS。

- [ ] **Step 6: Task 6をコミットする**

```bash
git add src/Tsumugi.Infrastructure/Persistence/AppendOnlyGuard.cs \
  tests/Tsumugi.Infrastructure.Tests/AppendOnlyGuardPhase3Tests.cs
git commit -m "feat(phase3-1/AC3-8): guard claim input histories"
```

---

### Task 7: 6 repositoryをscoped DIへ接続する

**Files:**
- Modify: `tests/Tsumugi.App.Tests/CompositionRootTests.cs:22-53`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs:20-46`

- [ ] **Step 1: real composition rootの失敗testを書く**

`AddTsumugiServices`でproviderとscopeを作り、次の6型を`GetRequiredService`する。

```csharp
scope.ServiceProvider.GetRequiredService<IClaimInputRepository>();
scope.ServiceProvider.GetRequiredService<IIntensiveSupportEpisodeRepository>();
scope.ServiceProvider.GetRequiredService<IAverageWageAnnualEvidenceRepository>();
scope.ServiceProvider.GetRequiredService<IOfficeClaimProfileRepository>();
scope.ServiceProvider.GetRequiredService<ICertificateClaimEvidenceRepository>();
scope.ServiceProvider.GetRequiredService<IUpperLimitManagementStatementRepository>();
```

同一scopeでは同じinstance、別scopeでは別instanceになることも1契約で確認する。

- [ ] **Step 2: Redを確認する**

```bash
dotnet test tests/Tsumugi.App.Tests \
  --filter FullyQualifiedName~CompositionRootTests -v normal
```

Expected: 最初の未登録repository解決でFAIL。

- [ ] **Step 3: 6 scoped registrationを追加する**

```csharp
services.AddScoped<IClaimInputRepository, ClaimInputRepository>();
services.AddScoped<IIntensiveSupportEpisodeRepository, IntensiveSupportEpisodeRepository>();
services.AddScoped<IAverageWageAnnualEvidenceRepository, AverageWageAnnualEvidenceRepository>();
services.AddScoped<IOfficeClaimProfileRepository, OfficeClaimProfileRepository>();
services.AddScoped<ICertificateClaimEvidenceRepository, CertificateClaimEvidenceRepository>();
services.AddScoped<IUpperLimitManagementStatementRepository, UpperLimitManagementStatementRepository>();
```

- [ ] **Step 4: composition root testをGreenにする**

Run: Task 7 Step 2と同じ。

Expected: 全`CompositionRootTests`がPASS。

- [ ] **Step 5: Task 7をコミットする**

```bash
git add src/Tsumugi.Infrastructure/DependencyInjection.cs \
  tests/Tsumugi.App.Tests/CompositionRootTests.cs
git commit -m "feat(phase3-1/AC3-8): register claim input repositories"
```

---

### Task 8: Task 7全体を検証する

**Files:**
- Verify only; verification起因の修正があれば該当タスクのファイルだけを変更する。

- [ ] **Step 1: focused suitesを順に実行する**

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimInputPolicyTests -v normal

dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~Phase31ClaimInputMigrationTests|FullyQualifiedName~ClaimBatchMigrationTests' \
  -v normal

dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimInputRepositoryTests|FullyQualifiedName~AppendOnlyGuardPhase3Tests|FullyQualifiedName~Phase31ClaimInputRoundTripTests' \
  -v normal

dotnet test tests/Tsumugi.App.Tests \
  --filter FullyQualifiedName~CompositionRootTests -v normal
```

Expected: すべてPASS。

- [ ] **Step 2: migration driftがないことを再確認する**

```bash
dotnet ef migrations has-pending-model-changes \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

Expected: exit 0、pending model changesなし。

- [ ] **Step 3: project suitesを実行する**

```bash
dotnet test tests/Tsumugi.Domain.Tests -v minimal
dotnet test tests/Tsumugi.Infrastructure.Tests -v minimal
dotnet test tests/Tsumugi.App.Tests -v minimal
```

Expected: Task 7変更に起因するfailure 0。

- [ ] **Step 4: Release build・format・diffを検証する**

```bash
dotnet build Tsumugi.sln -c Release --no-restore
dotnet format Tsumugi.sln --verify-no-changes --no-restore
git diff --check
git status --short
```

Expected: build warning 0 / error 0、format差分なし、whitespace errorなし、意図したTask 7ファイル以外の変更なし。

- [ ] **Step 5: repository最終gateを実行する**

```bash
./build/ci.sh
```

Expected: Task 7のInfrastructure pending-model failureは0。既知のTask 9 CSV条件failure又は後続App入力配線failureが残る場合は、実行時の正確なtest名とログを記録し、Task 7変更との因果を確認する。既知集合以外のfailure、またはTask 7変更ファイルに由来するfailureは完了扱いにせず修正する。

- [ ] **Step 6: verificationで必要になった最小修正だけをコミットする**

修正がなければ空commitは作らない。修正があれば該当ファイルだけをstageし、原因に対応したmessageでコミット後、影響するfocused suiteからStep 5まで再実行する。

## 完了条件

- 6 repositoryがraw全履歴を安定順・`AsNoTracking`で返し、実効選択を行わない。
- `AddAsync`はstageのみで、statement header/lineも1回のUoW saveに参加する。
- 7 table、Certificate lineage backfill、既存claim列が単一migrationに入り、Up/Down/Upが決定的。
- 未入力金額と正式0円がDB round-trip後も区別される。
- DBがinvalid lineage、branch、business FK、EnteredYen、Cancel payload、statement line重複を拒否する。
- 7 entityがAppendOnlyGuard対象。
- 6 repositoryがreal composition rootからscoped解決できる。
- pending model changesがない。
- Task 7に起因するtest / build / format failureが0。
