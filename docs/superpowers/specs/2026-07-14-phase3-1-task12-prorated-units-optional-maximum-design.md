# Phase 3-1 Task 12 Follow-up: Optional Proration Maximum Design

**Status:** Spec review approved; implementation requested
**Date:** 2026-07-14
**Scope:** Task 12 schema follow-up only — `prorated-units` optional maximum and service code 469992 fixture correction
**Supersedes:** The mandatory `maximumRecipientsPerStaff = 8` contract for service code 469992 in `2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`

## 1. Problem

Task 12 currently requires every `prorated-units` amount to carry a positive `maximumRecipientsPerStaff`, and the representative fixture for service code `469992` (`就継Ｂ医療連携体制加算Ⅴ`) fixes that value to `8`.

The official R6 and R8 calculation-note read-throughs instead replace the common medical-coordination scope `I–V` with B-type `I–IV`, and replace common `IV and V` with B-type `IV`. Therefore the common eight-recipient rule does not apply to B-type adjustment V. The current schema cannot represent the official rule without inventing an upper limit.

This is a Task 12 meaning-contract defect, not a Task 13 source-inventory gap.

## 2. Goals

- Represent prorated unit rules with or without an official recipient maximum.
- Preserve positive validation when an official maximum exists.
- Remove the unsupported maximum `8` from the R6 service code 469992 fixture and its referenced adjustment fixture.
- Keep selector, pool, calculation-step, rounding, billing-unit and provenance contracts unchanged.
- Keep Task 13 manifest decisions, production seeds and audit outputs unchanged.

## 3. Non-goals

- Do not resume the Task 13 Audit Gate or conditional seed phase.
- Do not change the proration formula, selector vocabulary or runtime fact ownership.
- Do not add a sentinel value, arbitrary expression language or new amount union kind.
- Do not change source catalog entries or official-source hashes.

## 4. Options considered

### 4.1 Optional maximum field — selected

Change the Domain value to `int? MaximumRecipientsPerStaff`. In JSON, field absence means that the official rule has no recipient maximum; a present value must be a positive integer. JSON `null` remains invalid.

This keeps one closed `prorated-units` shape, preserves bounded rules, and directly represents the official absence of a limit.

### 4.2 Sentinel zero for unlimited — rejected

Treating `0` as unlimited would overload a numeric limit with a second meaning, weaken the existing positive-value invariant, and make zero indistinguishable from invalid source data.

### 4.3 Separate bounded and unbounded union kinds — rejected

Adding a second amount kind would make the distinction explicit but would duplicate the same pool, selectors, step and rounding contract. No current official row requires that wider schema surface.

## 5. Contract

The `prorated-units` JSON shape becomes:

```text
prorated-units
  poolUnitsPerStaff: positive integer
  staffCountSelector: non-blank string
  recipientCountSelector: non-blank string
  maximumRecipientsPerStaff: optional positive integer
```

Semantics:

- field absent: no official recipient maximum applies;
- field present: the value is a positive per-staff recipient maximum;
- field present as `0`, a negative value, a non-integer or `null`: invalid;
- unknown fields remain invalid.

The Domain record preserves this distinction with nullable `int?`. The static validator continues to validate the closed JSON shape, known selectors, positive pool, optional-positive maximum, calculation step and rounding. A later calculator validates positive runtime staff and recipient counts, and applies the recipient-limit comparison only when the nullable maximum has a value.

## 6. Service code 469992 fixture

Both the service code fixture and its referenced addition component omit `maximumRecipientsPerStaff`. After loading:

```text
PoolUnitsPerStaff = 500
StaffCountSelector = medical-coordination-v-visiting-nurse-count
RecipientCountSelector = medical-coordination-v-supported-recipient-count
MaximumRecipientsPerStaff = null
```

The fixture keeps the existing per-day billing unit, proration calculation step, half-up rounding and source supports. A separate positive-path test adds `maximumRecipientsPerStaff = 8` to both matching service and component fixtures to prove that bounded prorated rules remain supported.

## 7. Validation and compatibility

- JSON Schema removes `maximumRecipientsPerStaff` from the required set while retaining its integer/minimum definition.
- Runtime parsing allows the property to be absent, rejects `null`, and parses a present value through the existing positive-integer validation.
- Service/addition structural equality continues to compare the complete typed amount, including nullable maximum.
- Existing invalid-value coverage continues to reject zero when the field is present.
- This is a schema-v2 follow-up; no version bump or backward-compatibility adapter is introduced.

## 8. Files

Create:

- `docs/superpowers/specs/2026-07-14-phase3-1-task12-prorated-units-optional-maximum-design.md`
- `docs/superpowers/plans/2026-07-14-phase3-1-task12-prorated-units-optional-maximum.md`

Modify:

- `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`
- `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`
- `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`
- `tests/Tsumugi.Domain.Tests/Logic/Claim/ClaimCalculationMasterContractTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs`

## 9. Acceptance criteria

- Domain contract distinguishes a missing maximum from a positive maximum.
- A complete prorated fixture without `maximumRecipientsPerStaff` loads successfully.
- A positive optional maximum loads successfully.
- Present zero, negative, non-integer and `null` maximum values are rejected.
- Service code 469992 and its addition component load with `MaximumRecipientsPerStaff = null`.
- Existing selector, pool, step, rounding and cross-entry equality validations remain green.
- Focused Domain and Infrastructure claim-master tests pass.
- Task 13 files and six production seed files are not changed by this follow-up.
