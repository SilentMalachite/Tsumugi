# Phase 3-1 Task 7 Claim Input Persistence Design

**Status:** Approved design
**Date:** 2026-07-11
**Scope:** Task 7 only — repositories, EF Core configuration, SQLite migration, append-only guard, DI registration, and persistence tests

## 1. Goal

Persist the append-only claim input and calculation-evidence models introduced by Tasks 5 and 6 without inventing values for existing data. The persistence boundary must return raw histories, preserve the distinction between missing amounts and formally entered zero yen, reject broken lineage at the database boundary, and leave effective-version selection to Domain policies.

## 2. Non-goals

- Do not implement Task 8 write use cases or DTOs.
- Do not select effective revisions in repositories.
- Do not add a generic history repository.
- Do not add an operation-local write store or `BEGIN IMMEDIATE` transaction in Task 7.
- Do not persist `AverageWageBandOptionVersionRule`, status-to-option dictionaries, or transition resolvers. They are claim-master inputs, not selected evidence snapshots.
- Do not fix the Task 9 CSV-condition failures or the later App input-wiring failure.

## 3. Application repository contracts

Create `src/Tsumugi.Application/Abstractions/IClaimInputRepositories.cs` with six type-specific repository interfaces.

```csharp
public interface IClaimInputRepository
{
    Task AddAsync(ClaimInput input, CancellationToken ct);

    Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
        Guid officeId,
        Guid recipientId,
        ServiceMonth serviceMonth,
        CancellationToken ct);
}

public interface IIntensiveSupportEpisodeRepository
{
    Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct);

    Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
        Guid officeId,
        Guid recipientId,
        CancellationToken ct);
}

public interface IAverageWageAnnualEvidenceRepository
{
    Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct);

    Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
        Guid officeId,
        int sourceFiscalYear,
        CancellationToken ct);
}

public interface IOfficeClaimProfileRepository
{
    Task AddAsync(OfficeClaimProfile profile, CancellationToken ct);

    Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
        Guid officeId,
        CancellationToken ct);
}

public interface ICertificateClaimEvidenceRepository
{
    Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct);

    Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
        Guid certificateId,
        CancellationToken ct);
}

public interface IUpperLimitManagementStatementRepository
{
    Task AddAsync(
        UpperLimitManagementStatement statement,
        IReadOnlyCollection<UpperLimitManagementStatementLine> lines,
        CancellationToken ct);

    Task<IReadOnlyList<UpperLimitManagementStatementAggregate>>
        ListHistoryAggregatesAsync(
            Guid managingOfficeId,
            Guid recipientId,
            ServiceMonth serviceMonth,
            CancellationToken ct);
}
```

Create `UpperLimitManagementStatementAggregate.cs` separately. It holds one header and a defensive, line-number-ordered copy of all lines.

Repositories stage additions only. `IUnitOfWork.SaveChangesAsync` owns the single save boundary. All reads use `AsNoTracking`, return every candidate in stable revision order, and do not repair, omit, or select effective records.

`OfficeClaimProfile` reads all profiles for an office so period overlap and competing roots remain visible. Statement lookup intentionally omits `CertificateId`; competing certificates for the same managing office, recipient, and service month must remain visible to Application and Domain validation.

## 4. Repository implementations and DI

Create one Infrastructure implementation per repository under `src/Tsumugi.Infrastructure/Persistence/`.

- `ClaimInputRepository.cs`
- `IntensiveSupportEpisodeRepository.cs`
- `AverageWageAnnualEvidenceRepository.cs`
- `OfficeClaimProfileRepository.cs`
- `CertificateClaimEvidenceRepository.cs`
- `UpperLimitManagementStatementRepository.cs`

The statement repository stages the header and all lines into the same scoped `TsumugiDbContext`. It must not call `SaveChanges` twice. Reads load headers first, load all matching lines by header ID, order headers by revision and lines by line number, then build aggregates.

Register all six interfaces as scoped services in `src/Tsumugi.Infrastructure/DependencyInjection.cs`. Extend `tests/Tsumugi.App.Tests/CompositionRootTests.cs` to prove all six resolve through the real composition root.

## 5. Tables and DbSets

Add these DbSets and tables:

| DbSet | Table |
| --- | --- |
| `ClaimInputs` | `ClaimInputs` |
| `IntensiveSupportEpisodes` | `IntensiveSupportEpisodes` |
| `AverageWageAnnualEvidences` | `AverageWageAnnualEvidences` |
| `OfficeClaimProfiles` | `OfficeClaimProfiles` |
| `CertificateClaimEvidences` | `CertificateClaimEvidences` |
| `UpperLimitManagementStatements` | `UpperLimitManagementStatements` |
| `UpperLimitManagementStatementLines` | `UpperLimitManagementStatementLines` |

Create one `IEntityTypeConfiguration<T>` file per entity. Configuration discovery continues through `ApplyConfigurationsFromAssembly`.

## 6. Value-object storage

| Domain type | Relational representation |
| --- | --- |
| `ServiceMonth` | required `INTEGER` in `YYYYMM` format |
| `ServiceMonth?` | nullable `INTEGER` in `YYYYMM` format |
| `DateRange` / `DateRange?` | deterministic `TEXT` through existing `DateRangeJson` |
| `ClaimMasterVersion?` | nullable `TEXT`, maximum 64 characters, explicit converter |
| `EnteredYen` | complex type split into `{Prefix}IsEntered` and `{Prefix}ValueYen` |
| `AverageWageBandOption?` | optional complex type split into Kind and OfficialOptionCode |
| `VersionedAverageWageBandOption?` | optional nested complex type split into MasterVersion, Kind, and OfficialOptionCode |

`EnteredYen` pairs require a named CHECK constraint equivalent to:

```sql
((IsEntered = 0 AND ValueYen IS NULL)
 OR (IsEntered = 1 AND ValueYen >= 0))
```

Optional option columns must be either all null or all valid. A valid option has Kind in the closed set `1, 2, 3` and a positive official option code. A versioned option additionally requires a nonblank master version no longer than 64 characters.

EF Core 10 optional complex types and struct support are the chosen mapping mechanism. JSON is retained only for `DateRange`, following the existing Certificate mapping; entered-state and option structures must remain split columns so SQLite CHECK constraints can inspect them.

## 7. Common append-only lineage constraints

Apply the following to all six history-header tables:

- unique `(RootId, Revision)` index;
- unique filtered `ExpectedHeadId` index where ExpectedHeadId is not null;
- self-referencing `RootId -> Id` FK with `RESTRICT`;
- self-referencing `ExpectedHeadId -> Id` FK with `RESTRICT`;
- named lineage CHECK constraint.

The lineage CHECK must enforce:

```sql
Revision >= 1
AND Kind IN (1, 2, 3)
AND (
  (Revision = 1 AND RootId = Id AND Kind = 1 AND ExpectedHeadId IS NULL)
  OR
  (Revision >= 2 AND RootId <> Id AND Kind IN (2, 3) AND ExpectedHeadId IS NOT NULL)
)
```

Business references use `RESTRICT` foreign keys:

- ClaimInput to Office and Recipient;
- IntensiveSupportEpisode to Office and Recipient;
- AverageWageAnnualEvidence to Office;
- OfficeClaimProfile to Office;
- CertificateClaimEvidence to Certificate;
- UpperLimitManagementStatement to Recipient, Certificate, and managing Office;
- StatementLine to Statement.

## 8. Lookup and new-root indexes

Create lookup indexes matching repository predicates, plus New-only partial unique indexes that prevent a second root for the same business key.

| Table | Lookup / New-only key |
| --- | --- |
| ClaimInputs | OfficeId, RecipientId, ServiceMonthKey |
| IntensiveSupportEpisodes | OfficeId, RecipientId |
| AverageWageAnnualEvidences | OfficeId, SourceFiscalYear |
| OfficeClaimProfiles | OfficeId, EffectiveFrom, EffectiveTo |
| CertificateClaimEvidences | CertificateId, Validity JSON |
| UpperLimitManagementStatements | RecipientId, CertificateId, ManagingOfficeId, ServiceMonthKey |

Because SQLite unique indexes treat nulls as distinct, OfficeClaimProfile uses two New-only indexes:

- closed period: `(OfficeId, EffectiveFrom, EffectiveTo)` where `Kind = 1 AND EffectiveTo IS NOT NULL`;
- open period: `(OfficeId, EffectiveFrom)` where `Kind = 1 AND EffectiveTo IS NULL`.

Statement lines require unique `(StatementId, LineNumber)` and unique `(StatementId, OfficeNumber)` indexes. Deleting a header must never cascade to its lines.

## 9. Cancellation storage

Database constraints must match Domain cancellation normalization.

- ClaimInput Cancel keeps the business key and lineage but requires all seven claim payload fields to be null.
- IntensiveSupportEpisode Cancel requires StartDate null; New and Correct require it non-null.
- AverageWageAnnualEvidence, OfficeClaimProfile, and CertificateClaimEvidence keep business keys but clear calculation values and confirmation evidence as defined by their policies.
- Certificate evidence and statement amounts preserve `EnteredYen(false, null)` for cancellation.
- UpperLimitManagementStatement Cancel keeps its business key and uses the current Domain sentinels: empty strings, enum zero, false/null confirmation state, and unentered yen pairs.
- A Cancel statement must have no lines. This cross-table rule remains in Application/Domain validation rather than a SQLite trigger.

`ClaimInputPolicy` currently constructs a cleared Cancel but does not reject a forged Cancel containing payload values. Task 7 includes a prerequisite Domain test and the minimum policy validation needed to align the Domain and database constraints.

## 10. Migration strategy

Generate one migration after all DbSets and configurations are complete:

```bash
dotnet tool restore
dotnet ef migrations add Phase31ClaimInputFoundation \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App
```

Do not manually rename the timestamped migration or its designer. Tests resolve the target by the `_Phase31ClaimInputFoundation` suffix.

Certificate lineage backfill must run in this order:

1. Add RootCertificateId, Revision, and ExpectedHeadCertificateId as temporary nullable columns.
2. Backfill every existing Certificate independently:

```sql
UPDATE "Certificates"
SET "RootCertificateId" = "Id",
    "Revision" = 1,
    "ExpectedHeadCertificateId" = NULL;
```

3. Make RootCertificateId and Revision non-null.
4. Add lineage CHECK and unique indexes.

Do not merge Certificates with the same recipient or overlapping validity. Each legacy row becomes its own revision-1 root.

Existing nullable claim columns remain null. The three non-null DailyRecord enums migrate to `Unspecified = 0`: MedicalCoordinationType, TrialUseSupportType, and RecipientConfirmation. ClaimInput's seven targets and IntensiveSupportEpisode.StartDate belong to new empty tables and are not inferred.

## 11. Migration regression compatibility

`ClaimBatchMigrationTests` currently assumes `_AddClaimBatchAndDetail` is the latest migration. Replace that assumption with suffix-index lookup and derive the previous migration from the target's actual position. The test remains an isolated AddClaim migration regression test after newer migrations are added.

`SqliteFixture` remains unchanged and continues to migrate an empty database to the latest schema.

## 12. Test strategy

### Domain prerequisite

Extend ClaimInputPolicy tests to reject a Cancel containing any claim payload value.

### Migration tests

Create `Phase31ClaimInputMigrationTests.cs` with independent tests for:

- previous-migration seed to Up;
- preservation of legacy Office, Certificate, ContractedProvider, and DailyRecord rows;
- two legacy Certificates becoming separate revision-1 roots;
- expected null/default behavior for existing claim columns;
- seven new tables existing and empty;
- column type, nullability, and default through `PRAGMA table_info`;
- indexes through `PRAGMA index_list` and `PRAGMA index_info`;
- foreign keys through `PRAGMA foreign_key_list`;
- named CHECK constraints through `sqlite_master.sql`;
- Down to the previous migration;
- deterministic re-Up;
- isolated raw-SQL violations for lineage, branch, FK, EnteredYen, cancellation, and statement-line constraints.

### Repository tests

Create `Persistence/ClaimInputRepositoryTests.cs` covering all six repositories:

- Add staging and one-unit-of-work save;
- raw history reads in revision order;
- no tracking after reads;
- multiple roots or competing candidates remain visible;
- round trips for nullable and non-null ServiceMonth, DateRange, ClaimMasterVersion, option snapshots, explicit zero yen, unentered yen, and cancellation rows;
- statement header and lines stage together;
- line order is stable;
- a line constraint failure rolls back the same SaveChanges transaction, leaving no header.

### Append-only and DI tests

Extend `AppendOnlyGuardPhase3Tests` for all seven new entity types. Modification and deletion must be rejected for both headers and lines.

Extend `CompositionRootTests` to resolve all six repository interfaces from a scope.

## 13. Error and concurrency boundary

Task 7 does not translate database failures into UI/Application error codes. Task 8 owns stale-head and persistence error normalization.

Database unique constraints reject duplicate roots, revisions, and branches. Repository reads, Domain validation, staging, and one `IUnitOfWork` save form the Task 8 flow. A dedicated operation-local store is deferred unless Task 8 concurrency tests prove the scoped repository/UoW boundary cannot produce deterministic stale-head behavior.

## 14. Completion gates

Required gates:

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimInputPolicyTests -v normal

dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~Phase31ClaimInputMigrationTests|FullyQualifiedName~ClaimBatchMigrationTests' \
  -v normal

dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimInputRepositoryTests|FullyQualifiedName~AppendOnlyGuardPhase3Tests' \
  -v normal

dotnet test tests/Tsumugi.App.Tests \
  --filter FullyQualifiedName~CompositionRootTests -v normal

dotnet ef migrations has-pending-model-changes \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App

dotnet build Tsumugi.sln -c Release --no-restore
dotnet format Tsumugi.sln --verify-no-changes --no-restore
git diff --check
```

Run `./build/ci.sh` and classify any remaining failures by scope. The known CSV condition failures and later App input-wiring failure are not Task 7 acceptance failures. Infrastructure failures caused by pending model changes must be eliminated by this task.

## 15. References

- `docs/superpowers/specs/2026-07-11-phase3-1-claim-calculation-and-input-foundation-design.md`
- `docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md`, Task 7
- `docs/decisions/0022-burden-cap-and-upper-limit-management.md`
- `docs/decisions/0023-average-wage-and-r8-transition.md`
- `docs/decisions/0025-claim-rounding-rules.md`
- [EF Core 10 complex types](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew#complex-types)
- [EF Core value conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)
- [EF Core SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)
