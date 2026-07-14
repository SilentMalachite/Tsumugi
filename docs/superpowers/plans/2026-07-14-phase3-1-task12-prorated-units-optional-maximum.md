# Phase 3-1 Task 12 Optional Proration Maximum Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow `prorated-units` to preserve the official absence of a recipient maximum and correct the service code 469992 fixtures without resuming Task 13.

**Architecture:** Keep the closed `prorated-units` union and make only `maximumRecipientsPerStaff` optional. Domain uses `int?`, JSON absence maps to `null`, a present value remains positive-only, and service/addition equality continues to compare the full typed amount.

**Tech Stack:** .NET 10, C# records, `System.Text.Json`, JSON Schema 2020-12, xUnit, FluentAssertions

---

## Worktree boundary

- Execute in `/Users/hiro/Projetct/GitHub/Tsumugi/.worktrees/phase3-1-task13-schema-v2-plan`.
- Preserve the existing uncommitted Task 13 Red gate in `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`.
- Do not stage or commit that test, production seed files, `/tmp` audit outputs, or Task 13 decisions.
- Run final full CI from a temporary clean detached worktree at the follow-up commit.

### Task 1: Make the Domain maximum nullable with TDD

**Files:**

- Modify: `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs`
- Modify: `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`

- [ ] **Step 1: Write the failing Domain contract test**

Add unbounded and bounded examples:

```csharp
var unbounded = new ProratedUnitsAmount(
    500,
    "medical-coordination-v-visiting-nurse-count",
    "medical-coordination-v-supported-recipient-count",
    null);
var bounded = unbounded with { MaximumRecipientsPerStaff = 8 };

unbounded.MaximumRecipientsPerStaff.Should().BeNull();
bounded.MaximumRecipientsPerStaff.Should().Be(8);
```

- [ ] **Step 2: Run the focused test and verify Red**

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimCalculationMasterContractTests \
  -v minimal
```

Expected: compile failure because the record still requires non-nullable `int`.

- [ ] **Step 3: Implement the minimal Domain change**

```csharp
public sealed record ProratedUnitsAmount(
    int PoolUnitsPerStaff,
    string StaffCountSelector,
    string RecipientCountSelector,
    int? MaximumRecipientsPerStaff) : UnitAdjustmentAmount;
```

- [ ] **Step 4: Rerun Step 2 and verify Green**

Expected: PASS with zero failures.

- [ ] **Step 5: Commit the Domain slice**

```bash
git add \
  src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs \
  tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs
git commit -m "feat(phase3-1): allow unbounded prorated units"
```

### Task 2: Make JSON maximum optional and correct 469992 fixtures with TDD

**Files:**

- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`

- [ ] **Step 1: Write failing embedded-schema assertions**

Add to `Embedded_schema_resources_express_the_runtime_contract`:

```csharp
var proratedAmount = definitions.GetProperty("proratedUnitsAmount");
Required(proratedAmount).Should().NotContain("maximumRecipientsPerStaff");
var maximum = proratedAmount.GetProperty("properties")
    .GetProperty("maximumRecipientsPerStaff");
maximum.GetProperty("type").GetString().Should().Be("integer");
maximum.GetProperty("minimum").GetInt32().Should().Be(1);
```

- [ ] **Step 2: Write failing 469992 and optional-value tests**

Remove `maximumRecipientsPerStaff` from both the `service-prorated` and `add-prorated` JSON fixtures. Assert the loaded 469992 amount has `MaximumRecipientsPerStaff == null`.

Add a positive-path test that inserts `8` into both matching fixtures and loads successfully. Add an invalid optional-value theory for `0`, `-1`, JSON string `"8"`, and JSON `null`; each must fail on `maximumRecipientsPerStaff`.

- [ ] **Step 3: Run focused Infrastructure tests and verify Red**

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests.Embedded_schema_resources_express_the_runtime_contract' \
  -v minimal
```

Expected: FAIL because the embedded schema and runtime parser still require the property.

- [ ] **Step 4: Make the JSON Schema property optional**

Remove `maximumRecipientsPerStaff` only from `proratedUnitsAmount.required`; retain:

```json
"maximumRecipientsPerStaff": { "type": "integer", "minimum": 1 }
```

- [ ] **Step 5: Parse absence as null and presence as positive**

In `ParseProratedUnits`, detect property presence. Pass the four required names to `RequireProperties` when absent, or all five names when present. Construct the nullable field with:

```csharp
hasMaximum
    ? PositiveInt(element, "maximumRecipientsPerStaff", fileName, key)
    : null
```

Do not change selector validation, step/rounding validation, or cross-entry equality.

- [ ] **Step 6: Rerun Step 3 and verify Green**

Expected: PASS with zero failures.

- [ ] **Step 7: Commit the schema/runtime slice**

```bash
git add \
  src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json \
  src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs
git commit -m "fix(phase3-1): model optional proration maximum"
```

### Task 3: Synchronize the normative Task 12 design

**Files:**

- Modify: `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`

- [ ] **Step 1: Update every mandatory-maximum statement**

Synchronize §8.2, §15.1, §15.3, §15.4, and §18. State `maximumRecipientsPerStaff: optional positive integer`; apply the runtime limit only when present; state that 469992 omits the field and loads `null` because the official B-type read-through excludes V from the common eight-recipient rule.

- [ ] **Step 2: Verify stale requirements are gone**

```bash
rg -n \
  'maximumRecipientsPerStaff = 8|正の`poolUnitsPerStaff`と`maximumRecipientsPerStaff`|pool、staff count selector、recipient count selector及び上限|非正のpool／maximum' \
  docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md
```

Expected: no matches.

- [ ] **Step 3: Commit the normative documentation update**

```bash
git add docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md
git commit -m "docs(phase3-1): correct proration maximum contract"
```

### Task 4: Verify the complete follow-up

**Files:**

- Verify: all Task 1–3 paths
- Preserve uncommitted: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Verify unchanged from pre-follow-up HEAD: six files under `src/Tsumugi.Infrastructure/ClaimMasters/Seed/`

- [ ] **Step 1: Run focused contract suites in the existing worktree**

```bash
dotnet test tests/Tsumugi.Domain.Tests \
  --filter FullyQualifiedName~ClaimCalculationMasterContractTests \
  -v minimal
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests' \
  -v minimal
```

Expected: both PASS with zero failures.

- [ ] **Step 2: Verify formatting and patch hygiene**

```bash
dotnet format Tsumugi.sln --verify-no-changes
git diff --check
git status --short
```

Expected: checks exit 0. Status contains only the preserved Task 13 Red gate.

- [ ] **Step 3: Verify production seeds are unchanged**

```bash
git diff --exit-code <PRE_FOLLOWUP_HEAD> -- \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json
```

Expected: exit 0 with no diff.

- [ ] **Step 4: Run full CI from a clean detached worktree**

```bash
git worktree add --detach /tmp/tsumugi-task12-proration-ci HEAD
(cd /tmp/tsumugi-task12-proration-ci && ./build/ci.sh)
git worktree remove /tmp/tsumugi-task12-proration-ci
```

Expected: `CI OK`. The clean worktree excludes the preserved uncommitted Task 13 Red gate.

- [ ] **Step 5: Audit final scope**

```bash
git log --oneline <PRE_FOLLOWUP_HEAD>..HEAD
git diff --name-only <PRE_FOLLOWUP_HEAD>..HEAD
git status --short
```

Expected: committed paths are limited to the follow-up spec/plan, Domain, JSON Schema, validator, relevant contract tests, and original Task 12 design. The only uncommitted path is the preserved Task 13 Red gate.
