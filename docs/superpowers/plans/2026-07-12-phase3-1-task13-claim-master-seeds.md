# Phase 3-1 Task 13 Claim Master Seeds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 41件の一次資料と14,709件のsource rowをschema v2 manifestへ損失なく再監査し、Audit Gate通過後だけR6／R8制度実値を6つのclaim master seedへ投入する。

**Architecture:** 作業を`Audit Gate`と`Conditional Seed Phase`へ分離する。Audit Gateは資料取得、SHA検証、manifest v1→v2機械変換、全row再監査、物理locator確認をseed変更なしで完了させる。`schema-gap = 0`になった後だけ、manifest target revisionを正本として独立masterと相互依存するservice code／componentを投入し、固定candidate commitをfresh reviewerが全件照合する。

**Tech Stack:** .NET 10、C# 14、xUnit、FluentAssertions、System.Text.Json、Python 3、openpyxl 3.x、`curl`、`jq`、`shasum`、Poppler (`pdfinfo` / `pdftotext`)

---

## 実行契約

- 設計正本: `docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md`
- schema v2正本: `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md`
- 上位計画: `docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md` Task 13
- source正本: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`、ADR 0020／0022／0023／0025
- 実装開始時のHEADを`/tmp/tsumugi-phase31-task13-base.txt`へ固定し、後続workerは毎回そこから`TASK13_IMPLEMENTATION_BASE`を復元する。
- 各test変更は`@superpowers:test-driven-development`でRed → Green → Refactorを守る。
- Audit Gate、seed candidate及び最終完了前に`@superpowers:verification-before-completion`を使う。
- 最終Taskで`@superpowers:requesting-code-review`を使う。
- 公式資料、receipt、chunk及び一時mappingは`/tmp/tsumugi-phase31-task13/`だけに置き、gitへ追加しない。
- SHA不一致、locator到達不能、source矛盾、row identity変化又は`schema-gap > 0`ではseed phaseへ進まない。
- 公式値をC#へ全量複製しない。代表境界以外の全件性はmanifest mappingと独立reviewで検証する。
- `dotnet test`は共有PDB競合を避けて逐次実行する。
- `docs/open-questions.md`は一次資料から一意に解消できない事項が生じた場合だけ変更する。
- main workspaceの`.serena/project.yml`、`graphify-out/`、既存未追跡planその他の変更をstageしない。

## ファイル構成

### Audit Gate

- Create: `build/phase3_task13_manifest_v2.py` — manifest v1→v2機械変換、200-row chunk生成、decision適用、identity検証。
- Modify: `docs/spec-data/phase3/claim-master-source-row-manifest.json` — schema v2の全source rowとproduction target mapping。
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs` — manifest v2 contract、identity、range、mapping、停止ゲート。

### Conditional Seed Phase

- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json` — 4負担区分。
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json` — B型地域区分単価。
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json` — R6／R8版境界とoption。
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json` — condition definitions、service identity、selector、unit rule、component refs。
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json` — 基本報酬component。
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json` — `UnitAdjustmentMasterRow` component。
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs` — target revision completeness、source ref双方向照合、seed境界。
- Create: `docs/phase3-1-master-transcription-review.md` — audit／seed candidate、全件照合、CI証跡。

### Read only

- `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`
- `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/JsonClaimMasterProviderTests.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimSpecificationBoundaryTests.cs`

Task 13では上記schema v2実装を変更しない。変更が必要になった時点で停止し、Task 12 follow-upを別spec／planとして提案する。

---

## Audit Gate — seed fileを変更しない

### Task 1: baselineと停止条件を固定する

**Files:**
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`
- Read: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: implementation baseとclean worktreeを固定する**

Run:

```bash
test -z "$(git status --porcelain)"
TASK13_BRANCH=$(git branch --show-current)
test -n "$TASK13_BRANCH"
test "$TASK13_BRANCH" != "main"
TASK13_IMPLEMENTATION_BASE=$(git rev-parse HEAD)
test ! -e /tmp/tsumugi-phase31-task13-base.txt
printf '%s\n' "$TASK13_IMPLEMENTATION_BASE" \
  > /tmp/tsumugi-phase31-task13-base.txt
```

Expected: 全command exit 0、branchはTask 13専用branch。base receiptは1行のcommit hashで、後続taskを別workerが実行しても同じbaseを復元できる。

- [ ] **Step 2: 必須toolを確認する**

Run:

```bash
command -v curl jq shasum pdftotext pdfinfo python3
python3 -c 'import openpyxl; print(openpyxl.__version__)'
```

Expected: 全commandが解決し、openpyxlは互換性のある3.x。欠落時はsource取得前に停止する。

- [ ] **Step 3: manifest v1 baselineを固定する**

Run:

```bash
jq '{
  schemaVersion,
  documents: (.documents | length),
  ranges: ([.documents[].extractionRanges[]] | length),
  expectedRows: ([.documents[].extractionRanges[].expectedItemCount] | add),
  rows: (.rows | length),
  seed: ([.rows[] | select(.disposition == "seed")] | length),
  excluded: ([.rows[] | select(.disposition == "excluded")] | length),
  schemaGap: ([.rows[] | select(.disposition == "schema-gap")] | length)
}' docs/spec-data/phase3/claim-master-source-row-manifest.json

jq -c '.rows[] | [.sourceDocumentId,.rangeId,.sourceLocator]' \
  docs/spec-data/phase3/claim-master-source-row-manifest.json \
  | shasum -a 256
```

Expected:

```text
schemaVersion = 1
documents = 41
ranges = 51
expectedRows = rows = 14709
seed = 15
excluded = 744
schemaGap = 13950
identity SHA-256 = 0d0e7361bf37e1f604f9dc59dcc408d2f64d513e7259596bed04499575bb3377
```

- [ ] **Step 4: 6 seedがschema v2かつ空であることを確認する**

Run:

```bash
for file in src/Tsumugi.Infrastructure/ClaimMasters/Seed/{basic-rewards,additions,region-unit-prices,burden-caps,transition-rules,service-codes}.json; do
  jq -e '.schemaVersion == "2" and (.entries | length) == 0' "$file"
done
jq -e '(.conditionDefinitions | length) == 0' \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json
```

Expected: 全command exit 0。

- [ ] **Step 5: baseline testsとfull gateを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests -v normal
./build/ci.sh
```

Expected: 現行schema v1停止snapshot testsがPASSし、`CI OK`。失敗時はTask 13変更前の問題として停止する。

---

### Task 2: 41資料を再取得してSHA receiptを固定する

**Files:**
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`
- Temporary: `/tmp/tsumugi-phase31-task13/sources/`
- Temporary: `/tmp/tsumugi-phase31-task13/acquisition.jsonl`

- [ ] **Step 1: 空の一時領域を作る**

Run:

```bash
TASK13_TMP=/tmp/tsumugi-phase31-task13
test ! -e "$TASK13_TMP"
mkdir -p "$TASK13_TMP/sources"
: > "$TASK13_TMP/acquisition.jsonl"
```

Expected: 新規の空directoryがrepository外に作成される。既存directoryがあれば上書きせず停止する。

- [ ] **Step 2: manifest documentだけの取得一覧を生成する**

Run:

```bash
jq -r --slurpfile manifest \
  docs/spec-data/phase3/claim-master-source-row-manifest.json '
  ($manifest[0].documents
    | map({
        key: .documentId,
        value: (
          if .extractionRanges[0].kind == "xlsx-rows" then ".xlsx"
          elif .extractionRanges[0].kind == "pdf-pages" then ".pdf"
          elif .extractionRanges[0].kind == "html-page" then ".html"
          else error("unknown extraction range kind")
          end)
      })
    | from_entries) as $suffixById
  | .sources[]
  | select($suffixById[.documentId] != null)
  | [.documentId, .url, .sha256, $suffixById[.documentId]]
  | @tsv
' src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json \
  > /tmp/tsumugi-phase31-task13/acquisition.tsv

test "$(wc -l < /tmp/tsumugi-phase31-task13/acquisition.tsv | tr -d ' ')" = 41
```

Expected: exit 0、41行。release bundle全63 sourcesを無条件取得しない。

- [ ] **Step 3: 未加工バイトを取得して即時SHA検証する**

Run:

```bash
TASK13_TMP=/tmp/tsumugi-phase31-task13
while IFS=$'\t' read -r document_id url expected_sha suffix; do
  output="$TASK13_TMP/sources/$document_id$suffix"
  curl --fail --location --retry 3 --retry-all-errors \
    --user-agent 'Tsumugi-ClaimMaster-Audit/2.0' \
    --output "$output" "$url" || exit 1

  test -s "$output" || exit 1
  actual_sha=$(shasum -a 256 "$output" | awk '{print $1}')
  test "$actual_sha" = "$expected_sha" || exit 1
  bytes=$(wc -c < "$output" | tr -d ' ')
  retrieved_at=$(date -u '+%Y-%m-%dT%H:%M:%SZ')

  jq -cn \
    --arg documentId "$document_id" \
    --arg url "$url" \
    --arg expectedSha256 "$expected_sha" \
    --arg actualSha256 "$actual_sha" \
    --argjson bytes "$bytes" \
    --arg retrievedAt "$retrieved_at" '
      {
        documentId: $documentId,
        url: $url,
        expectedSha256: $expectedSha256,
        actualSha256: $actualSha256,
        bytes: $bytes,
        retrievedAt: $retrievedAt,
        result: "PASS"
      }
    ' >> "$TASK13_TMP/acquisition.jsonl"
done < "$TASK13_TMP/acquisition.tsv"
```

Expected: 41 files、全SHA一致。404、空file、HTML error body又はSHA不一致で即時停止し、catalogを更新しない。

- [ ] **Step 4: receipt完全性とrepository非混入を確認する**

Run:

```bash
jq -e -s '
  length == 41
  and all(.[]; .result == "PASS")
  and all(.[]; .expectedSha256 == .actualSha256 and .bytes > 0)
' /tmp/tsumugi-phase31-task13/acquisition.jsonl

test "$(rg --files /tmp/tsumugi-phase31-task13/sources | wc -l | tr -d ' ')" = 41
git status --short
```

Expected: receipt 41件、files 41件、repository statusはTask 2開始前から変化しない。Task 2はcommitしない。

---

### Task 3: manifest v2 contract testsをRedにする

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`

- [ ] **Step 1: rootとrowのschema v2失敗testを書く**

`Source_manifest_exists_and_has_a_closed_root_contract`をschema v2へ変更し、row contract testを次の形へ置き換える。

```csharp
[Fact]
public void Source_manifest_exists_and_has_a_closed_v2_contract()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var root = manifest.RootElement;

    root.EnumerateObject().Select(property => property.Name)
        .Should().Equal("schemaVersion", "documents", "rows");
    root.GetProperty("schemaVersion").GetString().Should().Be("2");
    root.GetProperty("documents").GetArrayLength().Should().Be(41);
    root.GetProperty("rows").GetArrayLength().Should().Be(14_709);

    foreach (var row in root.GetProperty("rows").EnumerateArray())
    {
        row.EnumerateObject().Select(property => property.Name).Should().Equal(
            "sourceDocumentId",
            "rangeId",
            "sourceLocator",
            "sourceLabel",
            "effectiveFrom",
            "effectiveTo",
            "disposition",
            "productionTargets",
            "exclusionReason");
    }
}
```

- [ ] **Step 2: productionTargetsの閉集合testを書く**

```csharp
private static readonly string[] AllowedTargetKinds =
[
    "basic-rewards",
    "additions",
    "region-unit-prices",
    "burden-caps",
    "transition-rules",
    "service-codes",
    "service-code-conditions",
];

private static readonly string[] AllowedMappingRoles =
[
    "primary", "component", "supporting-evidence",
];

private static readonly string[] AllowedSupports =
[
    "service-identity", "selectors", "unit-rule-kind", "unit-rule-value",
    "unit-rule-target", "unit-rule-step", "unit-rule-rounding", "conditions",
    "effective-period", "master-values",
];

[Fact]
public void Source_manifest_v2_targets_have_closed_roles_and_supports()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    foreach (var row in manifest.RootElement.GetProperty("rows").EnumerateArray())
    {
        var disposition = row.GetProperty("disposition").GetString();
        var targets = row.GetProperty("productionTargets").EnumerateArray().ToArray();
        var reason = row.GetProperty("exclusionReason");

        if (disposition == "seed")
        {
            targets.Should().NotBeEmpty();
            reason.ValueKind.Should().Be(JsonValueKind.Null);
        }
        else
        {
            disposition.Should().BeOneOf("excluded", "schema-gap");
            targets.Should().BeEmpty();
            reason.GetString().Should().NotBeNullOrWhiteSpace();
        }

        foreach (var target in targets)
        {
            target.EnumerateObject().Select(property => property.Name).Should().Equal(
                "masterKind", "seedKey", "mappingRole", "supports", "mappingReason");
            target.GetProperty("masterKind").GetString().Should().BeOneOf(AllowedTargetKinds);
            target.GetProperty("seedKey").GetString().Should().NotBeNullOrWhiteSpace();
            var role = target.GetProperty("mappingRole").GetString();
            role.Should().BeOneOf(AllowedMappingRoles);
            var supports = target.GetProperty("supports").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            supports.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
            supports.Should().OnlyContain(support => AllowedSupports.Contains(support));
            if (role is "component" or "supporting-evidence")
                target.GetProperty("mappingReason").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }
}
```

- [ ] **Step 3: baseline identity／range preservation testを書く**

既存range testを維持し、次を追加する。

```csharp
[Fact]
public void Source_manifest_v2_preserves_the_v1_inventory_size()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var root = manifest.RootElement;
    root.GetProperty("documents").GetArrayLength().Should().Be(41);
    root.GetProperty("rows").GetArrayLength().Should().Be(14_709);
    var ranges = root.GetProperty("documents").EnumerateArray()
        .SelectMany(document => document.GetProperty("extractionRanges").EnumerateArray())
        .ToArray();
    ranges.Should().HaveCount(51);
    ranges.Sum(range => range.GetProperty("expectedItemCount").GetInt32())
        .Should().Be(14_709);
}
```

- [ ] **Step 4: testsがschema v1を理由にRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest_exists_and_has_a_closed_v2_contract|FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest_v2_targets|FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest_v2_preserves' \
  -v normal
```

Expected: FAIL because manifest `schemaVersion` is `1` and v1 rows do not contain`productionTargets`。

---

### Task 4: deterministic converterでmanifest v2へ機械変換する

**Files:**
- Create: `build/phase3_task13_manifest_v2.py`
- Create: `build/tests/test_phase3_task13_manifest_v2.py`
- Modify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: converter pure functionsの失敗testを書く**

標準libraryの`unittest`で`build/tests/test_phase3_task13_manifest_v2.py`を作り、最低限次を固定する。

- `migrate`が41 documents／14,709 rows／row identity digestを保持し、既存`seed`だけをprimary targetへ機械投影する。
- schema version、inventory count又はexpected digestが違えば`ValueError`。
- `validate_decision`がunknown kind／role／support、duplicate support、blank key、dispositionとtarget／reasonの不整合を拒否する。
- `apply_decisions`がmissing／extra／duplicate identityを拒否し、全件一致時だけrow順序とidentityを保持する。
- `write_chunks`が非空output directoryを拒否し、200-row境界を決定的なfile名へ分割する。

14,709行fixtureは小さなrow templateからtest内で生成し、repository manifestをtest fixtureとして書き換えない。

- [ ] **Step 2: converter未実装を理由にRedになることを確認する**

Run:

```bash
python3 -m unittest build/tests/test_phase3_task13_manifest_v2.py -v
```

Expected: FAIL because `build/phase3_task13_manifest_v2.py` does not exist。syntax error又はtest discovery errorではなく、対象module未実装が理由であること。

- [ ] **Step 3: converterのCLIとpure functionsを実装する**

`build/phase3_task13_manifest_v2.py`へ次の責務を実装する。

```python
#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

OLD_MAPPING_FIELDS = (
    "masterKind",
    "seedKey",
    "aggregationId",
    "aggregationKind",
    "aggregationReason",
)
ALLOWED_KINDS = {
    "basic-rewards", "additions", "region-unit-prices", "burden-caps",
    "transition-rules", "service-codes", "service-code-conditions",
}
ALLOWED_ROLES = {"primary", "component", "supporting-evidence"}
ALLOWED_SUPPORTS = {
    "service-identity", "selectors", "unit-rule-kind", "unit-rule-value",
    "unit-rule-target", "unit-rule-step", "unit-rule-rounding", "conditions",
    "effective-period", "master-values",
}


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: dict) -> None:
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def identity(row: dict) -> tuple[str, str, str]:
    return row["sourceDocumentId"], row["rangeId"], row["sourceLocator"]


def identity_digest(rows: list[dict]) -> str:
    payload = "".join(
        json.dumps(identity(row), ensure_ascii=False, separators=(",", ":")) + "\n"
        for row in rows
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def migrate(source: dict, expected_digest: str) -> dict:
    if source.get("schemaVersion") != "1":
        raise ValueError("migrate requires schemaVersion 1")
    if len(source.get("documents", [])) != 41 or len(source.get("rows", [])) != 14_709:
        raise ValueError("unexpected manifest inventory size")
    actual_digest = identity_digest(source["rows"])
    if actual_digest != expected_digest:
        raise ValueError(f"row identity digest mismatch: {actual_digest}")

    migrated_rows = []
    for source_row in source["rows"]:
        row = dict(source_row)
        disposition = row["disposition"]
        targets = []
        if disposition == "seed":
            targets.append({
                "masterKind": row["masterKind"],
                "seedKey": row["seedKey"],
                "mappingRole": "primary",
                "supports": ["effective-period", "master-values"],
                "mappingReason": None,
            })
        for field in OLD_MAPPING_FIELDS:
            row.pop(field)
        row = {
            "sourceDocumentId": row["sourceDocumentId"],
            "rangeId": row["rangeId"],
            "sourceLocator": row["sourceLocator"],
            "sourceLabel": row["sourceLabel"],
            "effectiveFrom": row["effectiveFrom"],
            "effectiveTo": row["effectiveTo"],
            "disposition": disposition,
            "productionTargets": targets,
            "exclusionReason": row["exclusionReason"],
        }
        migrated_rows.append(row)

    return {
        "schemaVersion": "2",
        "documents": source["documents"],
        "rows": migrated_rows,
    }


def write_chunks(manifest: dict, output_dir: Path, chunk_size: int) -> None:
    if output_dir.exists() and any(output_dir.iterdir()):
        raise ValueError("chunk output directory must be empty")
    output_dir.mkdir(parents=True, exist_ok=True)
    rows = manifest["rows"]
    for offset in range(0, len(rows), chunk_size):
        path = output_dir / f"chunk-{offset // chunk_size + 1:03d}.jsonl"
        with path.open("w", encoding="utf-8") as stream:
            for row in rows[offset:offset + chunk_size]:
                item = {
                    "sourceDocumentId": row["sourceDocumentId"],
                    "rangeId": row["rangeId"],
                    "sourceLocator": row["sourceLocator"],
                    "sourceLabel": row["sourceLabel"],
                    "effectiveFrom": row["effectiveFrom"],
                    "effectiveTo": row["effectiveTo"],
                    "disposition": row["disposition"],
                    "productionTargets": row["productionTargets"],
                    "exclusionReason": row["exclusionReason"],
                }
                stream.write(json.dumps(item, ensure_ascii=False) + "\n")


def validate_decision(decision: dict) -> None:
    targets = decision["productionTargets"]
    disposition = decision["disposition"]
    reason = decision["exclusionReason"]
    if disposition == "seed":
        if not targets or reason is not None:
            raise ValueError("seed decision requires targets and null reason")
    elif disposition in {"excluded", "schema-gap"}:
        if targets or not isinstance(reason, str) or not reason.strip():
            raise ValueError("non-seed decision requires no targets and a reason")
    else:
        raise ValueError(f"unknown disposition: {disposition}")

    for target in targets:
        if set(target) != {"masterKind", "seedKey", "mappingRole", "supports", "mappingReason"}:
            raise ValueError("target has unexpected fields")
        if target["masterKind"] not in ALLOWED_KINDS:
            raise ValueError("unknown target masterKind")
        if not isinstance(target["seedKey"], str) or not target["seedKey"].strip():
            raise ValueError("blank target seedKey")
        if target["mappingRole"] not in ALLOWED_ROLES:
            raise ValueError("unknown mappingRole")
        supports = target["supports"]
        if not supports or len(supports) != len(set(supports)) or not set(supports) <= ALLOWED_SUPPORTS:
            raise ValueError("invalid supports")
        if target["mappingRole"] != "primary" and not str(target["mappingReason"] or "").strip():
            raise ValueError("component/supporting target requires mappingReason")


def apply_decisions(manifest: dict, decision_dir: Path) -> dict:
    decisions = {}
    for path in sorted(decision_dir.glob("*.jsonl")):
        for line in path.read_text(encoding="utf-8").splitlines():
            decision = json.loads(line)
            key = identity(decision)
            if key in decisions:
                raise ValueError(f"duplicate decision: {key}")
            validate_decision(decision)
            decisions[key] = decision

    rows_by_identity = {identity(row): row for row in manifest["rows"]}
    if set(decisions) != set(rows_by_identity):
        missing = len(set(rows_by_identity) - set(decisions))
        extra = len(set(decisions) - set(rows_by_identity))
        raise ValueError(f"decision coverage mismatch: missing={missing}, extra={extra}")

    updated_rows = []
    for row in manifest["rows"]:
        decision = decisions[identity(row)]
        updated = dict(row)
        updated["disposition"] = decision["disposition"]
        updated["productionTargets"] = decision["productionTargets"]
        updated["exclusionReason"] = decision["exclusionReason"]
        updated_rows.append(updated)
    return {**manifest, "rows": updated_rows}


def main() -> None:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    migrate_parser = subparsers.add_parser("migrate")
    migrate_parser.add_argument("--input", type=Path, required=True)
    migrate_parser.add_argument("--output", type=Path, required=True)
    migrate_parser.add_argument("--expected-digest", required=True)

    chunk_parser = subparsers.add_parser("chunk")
    chunk_parser.add_argument("--manifest", type=Path, required=True)
    chunk_parser.add_argument("--output-dir", type=Path, required=True)
    chunk_parser.add_argument("--chunk-size", type=int, default=200)

    apply_parser = subparsers.add_parser("apply")
    apply_parser.add_argument("--manifest", type=Path, required=True)
    apply_parser.add_argument("--decision-dir", type=Path, required=True)
    apply_parser.add_argument("--output", type=Path, required=True)

    args = parser.parse_args()
    if args.command == "migrate":
        write_json(args.output, migrate(load_json(args.input), args.expected_digest))
    elif args.command == "chunk":
        write_chunks(load_json(args.manifest), args.output_dir, args.chunk_size)
    elif args.command == "apply":
        write_json(args.output, apply_decisions(load_json(args.manifest), args.decision_dir))


if __name__ == "__main__":
    main()
```

- [ ] **Step 4: converter unit testsをGreenにして一時出力へ実行する**

Run:

```bash
python3 -m unittest build/tests/test_phase3_task13_manifest_v2.py -v

python3 build/phase3_task13_manifest_v2.py migrate \
  --input docs/spec-data/phase3/claim-master-source-row-manifest.json \
  --output /tmp/tsumugi-phase31-task13/manifest-v2-mechanical.json \
  --expected-digest 0d0e7361bf37e1f604f9dc59dcc408d2f64d513e7259596bed04499575bb3377

jq -e '.schemaVersion == "2" and (.rows | length) == 14709' \
  /tmp/tsumugi-phase31-task13/manifest-v2-mechanical.json
```

Expected: unit tests PASS、conversion exit 0。identity不一致ならoutputを採用せず停止する。

- [ ] **Step 5: mechanical outputのidentity digestを再確認する**

Run:

```bash
jq -c '.rows[] | [.sourceDocumentId,.rangeId,.sourceLocator]' \
  /tmp/tsumugi-phase31-task13/manifest-v2-mechanical.json \
  | shasum -a 256
```

Expected: `0d0e7361bf37e1f604f9dc59dcc408d2f64d513e7259596bed04499575bb3377`。

- [ ] **Step 6: manifestをmechanical outputへ置換する**

Use `apply_patch` to replace `docs/spec-data/phase3/claim-master-source-row-manifest.json` with the verified temporary output. Do not copy any acquisition files into the repository.

- [ ] **Step 7: v2 contract testsをGreenにする**

`Source_manifest_schema_audit_snapshot_stays_stopped`は次へ変更し、13,950 gapsがまだ停止状態であることを保持する。

```csharp
[Fact]
public void Source_manifest_v2_mechanical_migration_remains_stopped_for_reaudit()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var rows = manifest.RootElement.GetProperty("rows").EnumerateArray().ToArray();
    rows.Should().HaveCount(14_709);
    rows.Count(row => row.GetProperty("disposition").GetString() == "seed").Should().Be(15);
    rows.Count(row => row.GetProperty("disposition").GetString() == "excluded").Should().Be(744);
    rows.Count(row => row.GetProperty("disposition").GetString() == "schema-gap")
        .Should().Be(13_950);
}
```

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests -v normal
```

Expected: PASS。`schema-gap = 0`testはまだ追加しない。

- [ ] **Step 8: mechanical migrationをcommitする**

```bash
git add build/phase3_task13_manifest_v2.py \
  build/tests/test_phase3_task13_manifest_v2.py \
  docs/spec-data/phase3/claim-master-source-row-manifest.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "test(phase3-1/AC3-1): migrate claim source manifest to v2"
```

---

### Task 5: 14,709 source rowsをv2 targetへ再監査する

**Files:**
- Modify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Read: `/tmp/tsumugi-phase31-task13/sources/*`
- Temporary: `/tmp/tsumugi-phase31-task13/chunks/`
- Temporary: `/tmp/tsumugi-phase31-task13/decisions/`

- [ ] **Step 1: no-schema-gap testを先に追加する**

```csharp
[Fact]
public void Source_manifest_v2_has_no_schema_gaps_before_seed_transcription()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    manifest.RootElement.GetProperty("rows").EnumerateArray()
        .Should().NotContain(row =>
            row.GetProperty("disposition").GetString() == "schema-gap");
}
```

- [ ] **Step 2: no-schema-gap testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Source_manifest_v2_has_no_schema_gaps_before_seed_transcription \
  -v normal
```

Expected: FAIL with 13,950 schema-gap rows。testを弱めない。

- [ ] **Step 3: 200-row audit chunksを生成する**

Run:

```bash
mkdir -p /tmp/tsumugi-phase31-task13/chunks \
  /tmp/tsumugi-phase31-task13/decisions

python3 build/phase3_task13_manifest_v2.py chunk \
  --manifest docs/spec-data/phase3/claim-master-source-row-manifest.json \
  --output-dir /tmp/tsumugi-phase31-task13/chunks \
  --chunk-size 200

test "$(rg --files /tmp/tsumugi-phase31-task13/chunks | wc -l | tr -d ' ')" = 74
test "$(jq -s 'length' /tmp/tsumugi-phase31-task13/chunks/*.jsonl)" = 14709
```

Expected: 74 chunk files、合計14,709 rows。

- [ ] **Step 4: 各chunkを一次資料から再分類する**

各chunkを`@superpowers:subagent-driven-development`の独立work unitとして扱う。auditorへ渡すものはchunk、該当source file、Task 12 design、ADRだけとする。各rowについて次のdecision JSONLを`/tmp/tsumugi-phase31-task13/decisions/`へ返す。

```json
{
  "sourceDocumentId": "r6-service-codes-2-xlsx",
  "rangeId": "r6-b-basic",
  "sourceLocator": "workbook-order=38;row=7",
  "disposition": "seed",
  "productionTargets": [
    {
      "masterKind": "service-codes",
      "seedKey": "official-stable-key",
      "mappingRole": "primary",
      "supports": [
        "service-identity",
        "unit-rule-kind",
        "unit-rule-value",
        "effective-period"
      ],
      "mappingReason": null
    }
  ],
  "exclusionReason": null
}
```

Rules:

- source rowの値、意味、期間を指定locatorから読む。
- `fixed-composite-unit`、4種のunit adjustment、`base-component-pass-through`、`factor-chain`、condition及びcomponent mappingをschema v2どおり分類する。
- 1 source rowがservice codeとcomponentを裏付ける場合は複数targetを返す。
- `component`／`supporting-evidence`は具体的`mappingReason`を必須にする。
- 名称の部分一致だけでkey、selector又はconditionを作らない。
- 不明又は損失写像は`schema-gap`として理由を残し、そのchunk以降のseed判断を保留する。

- [ ] **Step 5: decision coverageを適用する**

Run:

```bash
python3 build/phase3_task13_manifest_v2.py apply \
  --manifest docs/spec-data/phase3/claim-master-source-row-manifest.json \
  --decision-dir /tmp/tsumugi-phase31-task13/decisions \
  --output /tmp/tsumugi-phase31-task13/manifest-v2-audited.json
```

Expected: 全14,709 identitiesにdecisionがちょうど1件ある場合だけexit 0。missing／extra／duplicateで停止する。

- [ ] **Step 6: audited manifestの停止条件を集計する**

Run:

```bash
jq '{
  total: (.rows | length),
  seed: ([.rows[] | select(.disposition == "seed")] | length),
  excluded: ([.rows[] | select(.disposition == "excluded")] | length),
  schemaGap: ([.rows[] | select(.disposition == "schema-gap")] | length),
  targets: ([.rows[].productionTargets[]] | length)
}' /tmp/tsumugi-phase31-task13/manifest-v2-audited.json
```

Expected for continuing: `total = 14709`、`schemaGap = 0`、`total = seed + excluded`、`targets > 0`。

**STOP:** `schemaGap > 0`ならaudited manifestとgap集計を保持し、seed filesを変更せず、Task 12 follow-upが必要なrowと不足contractを報告する。Task 6以降へ進まない。

- [ ] **Step 7: audited manifestのphysical locatorを全件検証する**

Run:

```bash
python3 - <<'PY'
import json
import re
import subprocess
from pathlib import Path
from openpyxl import load_workbook

manifest = json.loads(Path('/tmp/tsumugi-phase31-task13/manifest-v2-audited.json').read_text())
source_dir = Path('/tmp/tsumugi-phase31-task13/sources')
ranges = {
    (document['documentId'], item['rangeId']): item
    for document in manifest['documents']
    for item in document['extractionRanges']
}
workbooks = {}
pdf_pages = {}
failures = []

def source_path(document_id):
    matches = list(source_dir.glob(document_id + '.*'))
    if len(matches) != 1:
        raise RuntimeError(f'{document_id}: expected one source file, got {len(matches)}')
    return matches[0]

for row in manifest['rows']:
    document_id = row['sourceDocumentId']
    locator = row['sourceLocator']
    item = ranges.get((document_id, row['rangeId']))
    if item is None:
        failures.append(f'{document_id}: undeclared range')
        continue
    path = source_path(document_id)
    if item['kind'] == 'xlsx-rows':
        match = re.fullmatch(r'workbook-order=(\d+);row=(\d+)', locator)
        if not match:
            failures.append(f'{document_id}: invalid xlsx locator {locator}')
            continue
        order, number = map(int, match.groups())
        if order != item['workbookOrder'] or not item['rowFrom'] <= number <= item['rowTo']:
            failures.append(f'{document_id}: locator outside range {locator}')
            continue
        if document_id not in workbooks:
            workbooks[document_id] = load_workbook(path, read_only=True, data_only=True)
        if number > workbooks[document_id].worksheets[order - 1].max_row:
            failures.append(f'{document_id}: missing row {locator}')
    elif item['kind'] == 'pdf-pages':
        match = re.match(r'pdf:physical-page=(\d+)(?:;.*)?$', locator)
        if not match:
            failures.append(f'{document_id}: invalid pdf locator {locator}')
            continue
        page = int(match.group(1))
        if document_id not in pdf_pages:
            info = subprocess.check_output(['pdfinfo', str(path)], text=True)
            pdf_pages[document_id] = int(re.search(r'^Pages:\s+(\d+)$', info, re.MULTILINE).group(1))
        if not item['pageFrom'] <= page <= item['pageTo'] or page > pdf_pages[document_id]:
            failures.append(f'{document_id}: missing page {locator}')
    elif item['kind'] == 'html-page':
        match = re.match(r'html:pageNo=(\d+)(?:;.*)?$', locator)
        if not match or int(match.group(1)) != item['pageNo'] or path.stat().st_size == 0:
            failures.append(f'{document_id}: invalid html locator {locator}')
    else:
        failures.append(f'{document_id}: unknown range kind')

print(json.dumps({
    'total': len(manifest['rows']),
    'reachable': len(manifest['rows']) - len(failures),
    'failures': failures,
}, ensure_ascii=False, indent=2))
if failures:
    raise SystemExit(1)
PY
```

Expected: `total = reachable = 14709`、`failures = []`。

- [ ] **Step 8: audited manifestをrepositoryへ反映する**

Use `apply_patch` to replace `docs/spec-data/phase3/claim-master-source-row-manifest.json` with `/tmp/tsumugi-phase31-task13/manifest-v2-audited.json` after all audit checks pass.

- [ ] **Step 9: no-gapとmanifest testsをGreenにする**

`Source_manifest_v2_mechanical_migration_remains_stopped_for_reaudit`を削除し、no-gap test、target revision 4-tuple validation及び旧field不在testを最終形にする。

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest -v normal
jq empty docs/spec-data/phase3/claim-master-source-row-manifest.json
git diff --check
```

Expected: PASS、JSON valid、diff check clean。

- [ ] **Step 10: Audit Gate candidateをcommitする**

```bash
git add docs/spec-data/phase3/claim-master-source-row-manifest.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "docs(phase3-1/AC3-1): complete claim manifest v2 audit"
AUDIT_CANDIDATE=$(git rev-parse HEAD)
printf '%s\n' "$AUDIT_CANDIDATE" \
  > /tmp/tsumugi-phase31-task13/audit-candidate.txt
```

---

### Task 6: Audit Gateをseed変更なしで固定する

**Files:**
- Verify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Verify: `build/phase3_task13_manifest_v2.py`
- Verify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Verify unchanged: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`

- [ ] **Step 1: seed filesがimplementation baseから未変更であることを確認する**

Run:

```bash
TASK13_IMPLEMENTATION_BASE=$(< /tmp/tsumugi-phase31-task13-base.txt)
git diff --exit-code "$TASK13_IMPLEMENTATION_BASE"..HEAD -- \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json
```

Expected: exit 0、差分なし。

- [ ] **Step 2: Audit Gate集計をfreshに確認する**

Run:

```bash
jq -e '
  .schemaVersion == "2"
  and (.documents | length) == 41
  and (.rows | length) == 14709
  and ([.rows[] | select(.disposition == "schema-gap")] | length) == 0
  and all(.rows[];
    if .disposition == "seed"
    then (.productionTargets | length) > 0 and .exclusionReason == null
    else (.productionTargets | length) == 0 and (.exclusionReason | length) > 0
    end)
' docs/spec-data/phase3/claim-master-source-row-manifest.json

jq -c '.rows[] | [.sourceDocumentId,.rangeId,.sourceLocator]' \
  docs/spec-data/phase3/claim-master-source-row-manifest.json \
  | shasum -a 256
```

Expected: jq exit 0、identity SHAは`0d0e7361bf37e1f604f9dc59dcc408d2f64d513e7259596bed04499575bb3377`。

- [ ] **Step 3: focused testsとfull CIを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest -v normal
./build/ci.sh
```

Expected: focused PASS、`CI OK`。

- [ ] **Step 4: Audit Gate commitとclean statusを記録する**

Run:

```bash
git rev-parse HEAD
git status --short
```

Expected: HEADは`AUDIT_CANDIDATE`、statusは空。ここまでがseed phase開始条件である。

---

## Conditional Seed Phase — Audit Gate通過後だけ実行

### Task 7: manifest target revision completenessをRedにする

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`

- [ ] **Step 1: 期間込みrevision identity helperを書く**

```csharp
private sealed record ProductionRevisionIdentity(
    string MasterKind,
    string Key,
    string EffectiveFrom,
    string? EffectiveTo);

private static ProductionRevisionIdentity TargetIdentity(JsonElement row, JsonElement target) =>
    new(
        target.GetProperty("masterKind").GetString()!,
        target.GetProperty("seedKey").GetString()!,
        row.GetProperty("effectiveFrom").GetString()!,
        row.GetProperty("effectiveTo").ValueKind == JsonValueKind.Null
            ? null
            : row.GetProperty("effectiveTo").GetString());
```

- [ ] **Step 2: manifestとproductionの双方向集合testを書く**

```csharp
[Fact]
public void Production_revision_set_exactly_matches_manifest_v2_targets()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var expected = manifest.RootElement.GetProperty("rows").EnumerateArray()
        .SelectMany(row => row.GetProperty("productionTargets").EnumerateArray()
            .Select(target => TargetIdentity(row, target)))
        .Distinct()
        .OrderBy(identity => identity.MasterKind, StringComparer.Ordinal)
        .ThenBy(identity => identity.Key, StringComparer.Ordinal)
        .ThenBy(identity => identity.EffectiveFrom, StringComparer.Ordinal)
        .ThenBy(identity => identity.EffectiveTo ?? string.Empty, StringComparer.Ordinal)
        .ToArray();

    var actual = LoadProductionRevisionIdentities()
        .OrderBy(identity => identity.MasterKind, StringComparer.Ordinal)
        .ThenBy(identity => identity.Key, StringComparer.Ordinal)
        .ThenBy(identity => identity.EffectiveFrom, StringComparer.Ordinal)
        .ThenBy(identity => identity.EffectiveTo ?? string.Empty, StringComparer.Ordinal)
        .ToArray();

    actual.Should().Equal(expected);
}
```

`LoadProductionRevisionIdentities`は6 fileの`entries`をroot `masterKind`で読み、`service-codes.json.conditionDefinitions`だけ`service-code-conditions`として追加する。各identityは`key + effectiveFrom + effectiveTo`を値変換せず使う。

- [ ] **Step 3: provenance双方向testもseed投入前に書く**

次のidentityをtest helperへ追加する。

```csharp
private sealed record ManifestSourceRefIdentity(
    ProductionRevisionIdentity Revision,
    string DocumentId,
    string Locator,
    string SupportsKey);
```

manifest側はtargetごとに親rowのdocument／locatorとordinal sort済みsupportsを組み立てる。production側はentry／conditionの全`sourceRefs`から同じidentityを組み立てる。両集合を`BeEquivalentTo`ではなくordinal sort後の`Equal`で双方向完全一致させる。

同じtest fixture内でproduction側の各source refについて次も先に固定する。

- `sha256`が`src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`の同一`documentId`と一致する。
- `evidenceRole`が`authoritative | correction | cross-check`の閉集合に属する。
- correction／cross-check refを含むrevisionは`ClaimMasterFileValidator.ValidateAll`相当の経路でcatalog `corrects` graphとsupport別正本一意性を通る。
- SHA、evidence role又はsupport正本が不正なfixture mutationでFAILすることを既存validator testsと合わせて確認する。

- [ ] **Step 4: completeness／provenance testsがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~Production_revision_set_exactly_matches_manifest_v2_targets|FullyQualifiedName~Production_source_refs_exactly_match_manifest_v2_targets' \
  -v normal
```

Expected: both FAIL because manifest targets／source rows are nonempty and all production arrays are empty。testsは未commitのままTask 8へ引き渡す。

---

### Task 8: 独立master 3種を投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: burden cap代表値testを書く**

```csharp
[Theory]
[InlineData("public-assistance", 0)]
[InlineData("low-income", 0)]
[InlineData("general-1", 9300)]
[InlineData("general-2", 37200)]
public void Burden_cap_seed_keeps_the_official_r6_values(string category, int capYen)
{
    using var seed = OpenRepositoryJson(
        "src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json");
    var row = seed.RootElement.GetProperty("entries").EnumerateArray()
        .Single(entry => entry.GetProperty("values").GetProperty("burdenCategory").GetString() == category);
    row.GetProperty("values").GetProperty("capYen").GetInt32().Should().Be(capYen);
    row.GetProperty("sourceRefs").EnumerateArray()
        .Should().Contain(source => source.GetProperty("supports").EnumerateArray()
            .Any(item => item.GetString() == "master-values"));
}
```

- [ ] **Step 2: region／transition Red testsを書く**

Test requirements:

- region target revision集合とseed集合が一致する。
- `unitPriceYen`はJSON numberを`decimal`として正確に読める。
- region keyごとにrelease期間の穴・重複がない。
- transitionはmanifestの全target revisionを持ち、R6-04、R6-06、R7-01、R7-09、R8-06境界を保持する。
- transition optionとR8 status別optionはADR 0023の閉集合と一致する。
- 全entryが`effective-period`と`master-values`をsource refsで覆う。

- [ ] **Step 3: focused testsがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~Burden_cap_seed|FullyQualifiedName~Region_unit_price_seed|FullyQualifiedName~Transition_rule_seed' \
  -v normal
```

Expected: FAIL because 3 seed arrays are empty。

- [ ] **Step 4: manifest targetと一次資料から3 seedへ転記する**

各entryはschema v2共通形を使う。

```json
{
  "key": "manifest-target-key",
  "effectiveFrom": "2024-04",
  "effectiveTo": null,
  "sourceRefs": [
    {
      "documentId": "source-document-id",
      "sha256": "catalog-sha256",
      "locator": "source-specific-locator",
      "evidenceRole": "authoritative",
      "supports": ["effective-period", "master-values"]
    }
  ],
  "values": {}
}
```

`values`は指定source locatorの実値だけで埋める。上記空objectをproductionへ残さない。

- [ ] **Step 5: focused testsをGreenにする**

Run the Step 3 command。

Expected: 3 masterのfocused tests PASS。全体completenessは他3 seedが空なのでFAILのまま。commitしない。

---

### Task 9: service code condition definitionsを投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: condition revision completenessのRed testを書く**

manifestの`masterKind = service-code-conditions` target revision集合と`conditionDefinitions`の`key + period`集合を比較し、次を検査する。

- kind、operator、value／values shapeがTask 12 closed matrix内。
- 同じkeyの期間重複なし。
- 全conditionが`conditions`と`effective-period`をsource refsで覆う。
- source refのSHAがcatalogと一致する。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Service_code_condition_seed -v normal
```

Expected: FAIL because `conditionDefinitions` is empty。

- [ ] **Step 3: conditionDefinitionsへ転記する**

```json
{
  "key": "manifest-condition-key",
  "effectiveFrom": "2024-04",
  "effectiveTo": "2026-05",
  "kind": "plan-status",
  "operator": "equals",
  "value": "not-created",
  "sourceRefs": [
    {
      "documentId": "source-document-id",
      "sha256": "catalog-sha256",
      "locator": "source-specific-locator",
      "evidenceRole": "authoritative",
      "supports": ["conditions", "effective-period"]
    }
  ]
}
```

例示tokenを別conditionへ流用せず、各manifest targetとsource rowから実値を転記する。

- [ ] **Step 4: condition testsをGreenにする**

Run the Step 2 command。

Expected: PASS。service code entriesはまだ空なので全体completenessはFAIL。commitしない。

---

### Task 10: basic reward componentsを投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: basic reward revisionとperiodのRed testを書く**

Test requirements:

- manifestの`basic-rewards` target revision集合とseed集合が一致する。
- `paymentBand / staffingKey / capacityKey / serviceCode / period`組が一意。
- `baseUnits`は正の整数。
- `serviceCode`は同じ適用期間のservice code targetへ対応する。
- `effective-period`と`master-values`をsource refsで覆う。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Basic_reward_seed -v normal
```

Expected: FAIL because `basic-rewards.json.entries` is empty。

- [ ] **Step 3: basic reward targetを転記する**

```json
{
  "key": "manifest-component-key",
  "effectiveFrom": "2024-04",
  "effectiveTo": "2026-05",
  "sourceRefs": [],
  "values": {
    "paymentBand": "source-defined-band",
    "staffingKey": "source-defined-staffing",
    "capacityKey": "source-defined-capacity",
    "serviceCode": "official-service-code",
    "baseUnits": 1
  }
}
```

`baseUnits: 1`はJSON shapeだけを示すsentinelでありproductionへ流用しない。公式表の基準単位を転記し、`FixedCompositeUnitRule.FinalUnits`と混同しない。`sourceRefs`を空のまま残さない。

- [ ] **Step 4: basic reward testsをGreenにする**

Run the Step 2 command。

Expected: PASS。service codeとのfull reference validationはTask 12まで保留。commitしない。

---

### Task 11: unit adjustment componentsを4 union対応で投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: adjustment revision completenessのRed testを書く**

manifestの`additions` target revision集合とseed集合を比較し、各amount kindを検証する。

```text
fixed-units          -> addedUnits > 0
units-per-count      -> unitsPerCount > 0 + closed countSelector
percentage-of-target -> canonical percentage + applicationKind + percentageBaseScope + targetSelector + calculationOrder
prorated-units       -> poolUnitsPerStaff + staff/recipient selectors + maximum
```

全entryで`calculationStepId`、nullable `roundingRuleId`、`billingUnit`、period、source refsを検査する。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Unit_adjustment_seed -v normal
```

Expected: FAIL because `additions.json.entries` is empty。

- [ ] **Step 3: UnitAdjustmentMasterRowを転記する**

```json
{
  "key": "manifest-adjustment-key",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [],
  "values": {
    "amount": {
      "kind": "prorated-units",
      "poolUnitsPerStaff": 1,
      "staffCountSelector": "medical-coordination-v-visiting-nurse-count",
      "recipientCountSelector": "medical-coordination-v-supported-recipient-count",
      "maximumRecipientsPerStaff": 1
    },
    "calculationStepId": "claim.step.units.service-code.prorate-by-recipient-count.v1",
    "roundingRuleId": "claim.rounding.units.half-up.v1",
    "billingUnit": "per-day"
  }
}
```

数値`1`はshape sentinelでありproductionへ流用しない。全値を指定source locatorから転記する。固定単位、人数比例又は按分をpercentageへ変換しない。`sourceRefs`を空のまま残さない。

- [ ] **Step 4: adjustment testsをGreenにする**

Run the Step 2 command。

Expected: PASS。service codeとの構造的一致はTask 12で確認する。commitしない。

---

### Task 12: service code entriesをcomponent／conditionへ接続する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: service code revision completenessのRed testを書く**

Test requirements:

- manifestの`service-codes` target revision集合とentry集合が一致する。
- `serviceCode + period`が一意でofficial labelが空でない。
- selectorsとconditionSelectorsは重複なし。
- unit ruleは`fixed-composite-unit`、`unit-addition`又は`formula`の閉集合。
- `fixed-composite-unit.finalUnits != 0`。
- unit-additionは対応`adjustmentComponentKey`とamount／step／rounding／billingUnitが一致する。
- formulaは対応`baseComponentKey`を持ち、pass-through又は連続factor chain契約を満たす。
- component refとconditionが全適用月で解決する。
- source refsがsupport必須coverageを満たす。
- 2024-04、2024-06、2025-01、2025-09、2026-05、2026-06の各境界月で、対応するservice code、component、condition及びtransition revisionがちょうど1件activeになる。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Service_code_seed -v normal
```

Expected: FAIL because `service-codes.json.entries` is empty。

- [ ] **Step 3: service code entriesを転記する**

公式請求行ごとにidentity、selector、condition、unit rule、component refs、period、source refsを転記する。

```json
{
  "key": "manifest-service-key",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceRefs": [],
  "values": {
    "serviceCode": "official-code",
    "officialLabel": "公式サービス内容略称",
    "serviceKind": "employment-continuation-support-b",
    "selectors": ["source-defined-selector"],
    "conditionSelectors": [],
    "unitRule": {
      "kind": "fixed-composite-unit",
      "finalUnits": 1,
      "billingUnit": "per-day"
    },
    "componentRefs": []
  }
}
```

`finalUnits: 1`はshape sentinelでありproductionへ流用しない。source rowが示すsigned final unitsを使う。formula又はunit-addition rowでは対応するcomponent refを必ず設定し、`sourceRefs`を空のまま残さない。

- [ ] **Step 4: service code focused testsをGreenにする**

Run the Step 2 command。

Expected: PASS。

- [ ] **Step 5: 全revision completenessをGreenにする**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Production_revision_set_exactly_matches_manifest_v2_targets \
  -v normal
```

Expected: PASS。後続版1件の欠落又は誤期間でFAILすること。

---

### Task 13: provenance双方向照合とproduction validatorを通す

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Verify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Verify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`

- [ ] **Step 1: Task 7でRed固定したprovenance双方向testを完成させる**

Task 8〜12で投入した全revision／conditionのsource refsがmanifest targetと一致するよう、転記漏れだけを修正する。test自体を弱めたり、manifest targetをproductionへ合わせて削除したりしない。

- [ ] **Step 2: bidirectional testを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~Production_source_refs_exactly_match_manifest_v2_targets \
  -v normal
```

Expected: PASS。manifestだけ又はproductionだけにあるref、期間、SHA、evidence role、support又はcorrection chain違いでFAILすること。

- [ ] **Step 3: production schemaとcross-reference testsを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests|FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' \
  -v normal
```

Expected: PASS。`ClaimMasterFileValidator`がsource correction chain、support coverage、condition、component、selector、period及びcycleを検証する。

- [ ] **Step 4: JSON完全性とdiffを検査する**

Run:

```bash
jq empty docs/spec-data/phase3/claim-master-source-row-manifest.json
for file in src/Tsumugi.Infrastructure/ClaimMasters/Seed/{basic-rewards,additions,region-unit-prices,burden-caps,transition-rules,service-codes}.json; do
  jq empty "$file"
done
git diff --check
```

Expected: all exit 0。

- [ ] **Step 5: full CIを実行する**

Run: `./build/ci.sh`

Expected: `CI OK`、build warnings/errors 0、全tests PASS、coverage、architecture、offline gate PASS。

- [ ] **Step 6: seed candidateを単一commitへ固定する**

```bash
git add docs/spec-data/phase3/claim-master-source-row-manifest.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "data(phase3-1/AC3-1): add sourced schema v2 claim masters"
SEED_CANDIDATE=$(git rev-parse HEAD)
printf '%s\n' "$SEED_CANDIDATE" \
  > /tmp/tsumugi-phase31-task13/seed-candidate.txt
git status --short
```

Expected: statusは空。review完了までmanifest又はseedを変更しない。

---

### Task 14: fresh reviewerが全source rowとseedを照合する

**Files:**
- Create: `docs/phase3-1-master-transcription-review.md`
- Review: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Review: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`
- Read: `/tmp/tsumugi-phase31-task13/sources/*`
- Read: `/tmp/tsumugi-phase31-task13/acquisition.jsonl`

- [ ] **Step 1: 転記未関与のfresh source-data reviewerを割り当てる**

候補hashをreceiptから復元し、現在のcheckoutと一致することを確認する。

```bash
AUDIT_CANDIDATE=$(< /tmp/tsumugi-phase31-task13/audit-candidate.txt)
SEED_CANDIDATE=$(< /tmp/tsumugi-phase31-task13/seed-candidate.txt)
test "$(git rev-parse HEAD)" = "$SEED_CANDIDATE"
git cat-file -e "$AUDIT_CANDIDATE^{commit}"
```

reviewerへ次だけを渡す。

```text
seed candidate: shell variable SEED_CANDIDATEの実hash
audit candidate: shell variable AUDIT_CANDIDATEの実hash
manifest: docs/spec-data/phase3/claim-master-source-row-manifest.json
seeds: src/Tsumugi.Infrastructure/ClaimMasters/Seedの6 file
source catalog: src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json
ADRs: 0020, 0022, 0023, 0025
verified sources: /tmp/tsumugi-phase31-task13/sources/
```

reviewerは次の順で全件確認する。

1. candidate commit checkout状態とpathを確認。
2. 41 documentsのSHAを原本へ再計算。
3. 51 rangesと14,709 source rowsのlocatorを確認。
4. 全rowのdisposition、production target revision、mapping role、supports、reasonを確認。
5. 全production revisionのkey、値、code、period、source refsを原本へ照合。
6. manifest target→production refとproduction ref→manifest targetの両方向を確認。
7. file別、range別、master kind別、総row数及びdiscrepancy数を返す。

- [ ] **Step 2: reviewer判定を処理する**

合格条件:

```text
SHA match rate = 100%
locator reach rate = 100%
manifest coverage rate = 100%
source row review rate = 100%
production revision review rate = 100%
unexplained exclusions = 0
schema gaps = 0
mapping/value/code/period discrepancies = 0
Status = Approved
```

Issues Foundなら該当manifest／seed／testを修正し、Task 13のfocused testsとfull CIを再実行して新`SEED_CANDIDATE`を作る。同じreviewerが全14,709 rowsを再レビューする。差分rowだけで承認しない。

- [ ] **Step 3: evidence用の実数を機械集計する**

Run:

```bash
jq -r -s '.[] | [
  .documentId, .url, .expectedSha256, .actualSha256,
  (.bytes | tostring), .retrievedAt, .result
] | @tsv' /tmp/tsumugi-phase31-task13/acquisition.jsonl

jq '{
  rows: (.rows | length),
  seed: ([.rows[] | select(.disposition == "seed")] | length),
  excluded: ([.rows[] | select(.disposition == "excluded")] | length),
  schemaGap: ([.rows[] | select(.disposition == "schema-gap")] | length),
  targetRevisions: ([.rows[] as $row | $row.productionTargets[] |
    [.masterKind, .seedKey, $row.effectiveFrom, $row.effectiveTo]] | unique | length)
}' docs/spec-data/phase3/claim-master-source-row-manifest.json

for file in src/Tsumugi.Infrastructure/ClaimMasters/Seed/{basic-rewards,additions,region-unit-prices,burden-caps,transition-rules,service-codes}.json; do
  jq -r '[input_filename, (.entries | length)] | @tsv' "$file"
done
jq '.conditionDefinitions | length' \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json
```

Expected: commands exit 0、schemaGap 0、reviewer集計と一致。

- [ ] **Step 4: review evidenceを書く**

`docs/phase3-1-master-transcription-review.md`へ実行時の値だけを記録する。

```markdown
# Phase 3-1 Claim Master Transcription Review

**Audit candidate:** 実際のAUDIT_CANDIDATE hash
**Seed candidate:** 実際のSEED_CANDIDATE hash
**Reviewer:** 実際のreview task ID又は氏名
**Reviewed at:** 実際のISO-8601 timestamp with offset
**Status:** Approved

## Source acquisition

41件のreceipt実表を記載する。

## Manifest v2 audit

documents、ranges、rows、seed、excluded、schema gap、target revisionsの実数を記載する。

## Full-row review

file別candidate revisions、reviewed revisions、source rows、discrepanciesの実数を記載する。

## Verification

focused tests、full CI、SHA、locator、manifest coverage、source row reviewの実結果を記載する。
```

説明文の「実際の...」をfileへ残さず、全項目をcommandとreviewer outputの実値へ置換する。local temporary pathは記録しない。

- [ ] **Step 5: fresh final verificationを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests|FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' \
  -v normal
./build/ci.sh
git diff --check
git status --short
```

Expected: focused PASS、`CI OK`、diff check clean。statusは未commitのreview evidenceだけ。

- [ ] **Step 6: evidence commitを作る**

```bash
git add docs/phase3-1-master-transcription-review.md
git commit -m "docs(phase3-1/AC3-1): record schema v2 source review"
```

---

### Task 15: Task 13全範囲を最終レビューして閉じる

**Files:**
- Review: Task 13で変更した全file
- Verify: `docs/phase3-1-master-transcription-review.md`

- [ ] **Step 1: Task 13 code reviewを依頼する**

```bash
TASK13_IMPLEMENTATION_BASE=$(< /tmp/tsumugi-phase31-task13-base.txt)
AUDIT_CANDIDATE=$(< /tmp/tsumugi-phase31-task13/audit-candidate.txt)
SEED_CANDIDATE=$(< /tmp/tsumugi-phase31-task13/seed-candidate.txt)
git cat-file -e "$TASK13_IMPLEMENTATION_BASE^{commit}"
test "$(git rev-parse HEAD~1)" = "$SEED_CANDIDATE"
```

最後の`test`でevidence commit直前がseed candidateであることを強制する。不一致ならreviewを開始せず、candidate receiptとcommit chainを修正する。

Use `@superpowers:requesting-code-review` with:

```text
BASE: TASK13_IMPLEMENTATION_BASEの実hash
HEAD: current HEAD
SPEC: docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md
PLAN: docs/superpowers/plans/2026-07-12-phase3-1-task13-claim-master-seeds.md
Focus: manifest v2 identity preservation, audit/seed gate separation,
       target revision completeness, provenance, effective periods,
       condition/component references, independent review evidence
```

Expected: unresolved Major／Minor 0。

- [ ] **Step 2: review修正時のcandidate整合を維持する**

manifest又はseedを変更した場合は次を全てやり直す。

1. focused tests。
2. `./build/ci.sh`。
3. 新`SEED_CANDIDATE` commit。
4. fresh reviewerによる全14,709 rows再照合。
5. evidenceのcandidate hash／実数更新。

evidence wordingだけの修正ではsource-data再レビューを要求しないが、candidate hashと実データの対応は再確認する。

- [ ] **Step 3: final gateをfreshに実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests|FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' \
  -v normal
./build/ci.sh
git diff --check
git status --short --branch
```

Expected: focused PASS、`CI OK`、diff check clean、worktree clean。

- [ ] **Step 4: 完了条件を行単位で再確認する**

```text
Audit Gateがseed変更なしで独立commitされている。
41 documents、51 ranges、14,709 row identitiesが保持されている。
SHA、locator、manifest coverage、source row reviewが100%。
schema-gap = 0。
target revision集合とproduction revision集合が双方向一致する。
manifest source rowとproduction source refが双方向一致する。
6 seedがschema v2 runtime validatorを同時に通る。
R6-04、R6-06、R7-01、R7-09、R8-06境界が一意である。
fresh reviewerがApprovedを返している。
evidenceが実candidate hashと実数を参照する。
原本、receipt、chunk、一時mappingがgitへ混入していない。
```

全項目を満たす場合だけTask 13完了を報告する。Task 14へ自動で進まない。

---

## 実行handoff

推奨は`@superpowers:subagent-driven-development`である。Audit Gateのchunk再監査は200-row単位で独立work unitにでき、seed phaseはmaster別にRed／Green確認できる。一方、manifest／seedは共有fileなので、同時編集せずtask順に統合し、最終candidateは単一workerが固定する。
