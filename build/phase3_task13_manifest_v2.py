#!/usr/bin/env python3
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile


OLD_MAPPING_FIELDS = (
    "masterKind",
    "seedKey",
    "aggregationId",
    "aggregationKind",
    "aggregationReason",
)
ALLOWED_KINDS = {
    "basic-rewards",
    "additions",
    "region-unit-prices",
    "burden-caps",
    "transition-rules",
    "service-codes",
    "service-code-conditions",
}
ALLOWED_ROLES = {"primary", "component", "supporting-evidence"}
ALLOWED_SUPPORTS = {
    "service-identity",
    "selectors",
    "unit-rule-kind",
    "unit-rule-value",
    "unit-rule-target",
    "unit-rule-step",
    "unit-rule-rounding",
    "conditions",
    "effective-period",
    "master-values",
    "unit-rule-formula",
    "unit-rule-comparison",
    "unit-rule-local-government-adjustment",
    "unit-rule-runtime-input",
    "unit-rule-runtime-input-provenance",
}
REQUIRED_DECISION_FIELDS = {
    "sourceDocumentId",
    "rangeId",
    "sourceLocator",
    "disposition",
    "productionTargets",
    "exclusionReason",
}
OPTIONAL_DECISION_FIELDS = {"sourceLabel", "effectiveFrom", "effectiveTo"}
TARGET_FIELDS = {
    "masterKind",
    "seedKey",
    "mappingRole",
    "supports",
    "mappingReason",
}

BASELINE_IDENTITY_SHA = (
    "90fb9d309e878d22f0d4bb867c4fe36c3fab83ad45938b64da2d5b3bfd34dee7"
)
FINAL_IDENTITY_SHA = (
    "c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7"
)
PROTECTED_CODES = (
    "462841", "462842", "462843", "462844", "462845", "462846",
    "46C841", "46C842", "46C843", "46C844", "46C845", "46C846",
    "46D841", "46D842", "46D843", "46D844", "46D845", "46D846",
    "46E841", "46E844", "46F841", "46F844",
)
PROTECTED_GAP_GROUPS = (
    (
        "r6-service-codes-2-xlsx",
        "r6-b-basic",
        tuple(f"workbook-order=38;row={row}" for row in range(907, 913)),
    ),
    (
        "r6-service-codes-2-xlsx",
        "r6-b-support-staff-shortage",
        tuple(f"workbook-order=40;row={row}" for row in range(1807, 1819)),
    ),
    (
        "r6-service-codes-2-xlsx",
        "r6-b-service-manager-shortage",
        tuple(f"workbook-order=41;row={row}" for row in range(607, 611)),
    ),
    (
        "r8-service-codes-2-xlsx",
        "r8-b-basic",
        tuple(f"workbook-order=38;row={row}" for row in range(1987, 1993)),
    ),
    (
        "r8-service-codes-2-xlsx",
        "r8-b-support-staff-shortage",
        tuple(f"workbook-order=40;row={row}" for row in range(3967, 3979)),
    ),
    (
        "r8-service-codes-2-xlsx",
        "r8-b-service-manager-shortage",
        tuple(f"workbook-order=41;row={row}" for row in range(1327, 1331)),
    ),
)
PROTECTED_GAP_TARGETS = tuple(
    (document_id, range_id, locator, code)
    for release_groups in (PROTECTED_GAP_GROUPS[:3], PROTECTED_GAP_GROUPS[3:])
    for (document_id, range_id, locators), code_group in zip(
        release_groups,
        (PROTECTED_CODES[:6], PROTECTED_CODES[6:18], PROTECTED_CODES[18:]),
        strict=True,
    )
    for locator, code in zip(locators, code_group, strict=True)
)
EXPECTED_SOURCE_SHAS = {
    "current-fee-notice-html":
        "0b5c75203f589701e8d0d3ba7cf192f4873b7aeae023da6e137882b225286768",
    "protected-facility-administrative-expense-standard-html":
        "e6d94b5279ca33d60daa83f29e6fdb1f5c3d1ba08f076812cf2c0f64a37ba8a5",
    "h31-fee-notice-consolidated":
        "79054870b88b1ca97b3b31a811857ed8a614e59da0b6d14435df30bcb5bf4bc9",
}
HTML_LINES_LOCATOR_PATTERN = re.compile(
    r"^html:lines=l\d+(?:-l\d+)?(?:,l\d+(?:-l\d+)?)*$"
)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: dict) -> None:
    payload = json.dumps(value, ensure_ascii=False, indent=2) + "\n"
    temporary_path = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            dir=path.parent,
            prefix=f".{path.name}.",
            suffix=".tmp",
            delete=False,
        ) as stream:
            temporary_path = Path(stream.name)
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        temporary_path.replace(path)
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)


def identity(row: dict) -> tuple[str, str, str]:
    return row["sourceDocumentId"], row["rangeId"], row["sourceLocator"]


def identity_digest(rows: list[dict]) -> str:
    payload = "".join(
        json.dumps(identity(row), ensure_ascii=False, separators=(",", ":")) + "\n"
        for row in rows
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def inventory_summary(manifest: dict, label: str) -> dict:
    if not isinstance(manifest, dict):
        raise ValueError(f"{label} manifest must be an object")
    documents = manifest.get("documents")
    rows = manifest.get("rows")
    if not isinstance(documents, list):
        raise ValueError(f"{label} documents must be a list")
    if not isinstance(rows, list):
        raise ValueError(f"{label} rows must be a list")

    range_count = 0
    expected_row_count = 0
    for document_index, document in enumerate(documents):
        if not isinstance(document, dict):
            raise ValueError(f"{label} documents[{document_index}] must be an object")
        extraction_ranges = document.get("extractionRanges")
        if not isinstance(extraction_ranges, list):
            raise ValueError(
                f"{label} documents[{document_index}].extractionRanges must be a list"
            )
        range_count += len(extraction_ranges)
        for range_index, extraction_range in enumerate(extraction_ranges):
            if not isinstance(extraction_range, dict):
                raise ValueError(
                    f"{label} documents[{document_index}].extractionRanges"
                    f"[{range_index}] must be an object"
                )
            expected_count = extraction_range.get("expectedItemCount")
            if (
                not isinstance(expected_count, int)
                or isinstance(expected_count, bool)
                or expected_count <= 0
            ):
                raise ValueError(
                    f"{label} documents[{document_index}].extractionRanges"
                    f"[{range_index}].expectedItemCount must be a positive integer"
                )
            expected_row_count += expected_count

    if expected_row_count != len(rows):
        raise ValueError(
            f"{label} expectedItemCount total mismatch: "
            f"expected={expected_row_count}, rows={len(rows)}"
        )

    return {
        "documents": len(documents),
        "ranges": range_count,
        "rows": len(rows),
        "identitySha256": identity_digest(rows),
    }


def migration_summary(source: dict, migrated: dict) -> dict:
    before = inventory_summary(source, "source")
    after = inventory_summary(migrated, "migrated")
    if before != after:
        raise ValueError("migration inventory summary mismatch")
    return {"before": before, "after": after}


def positive_int(value: str) -> int:
    try:
        parsed = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("must be a positive integer") from error
    if parsed <= 0:
        raise argparse.ArgumentTypeError("must be a positive integer")
    return parsed


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
            targets.append(
                {
                    "masterKind": row["masterKind"],
                    "seedKey": row["seedKey"],
                    "mappingRole": "primary",
                    "supports": ["effective-period", "master-values"],
                    "mappingReason": None,
                }
            )

        for field in OLD_MAPPING_FIELDS:
            row.pop(field)

        migrated_rows.append(
            {
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
        )

    return {
        "schemaVersion": "2",
        "documents": source["documents"],
        "rows": migrated_rows,
    }


def write_chunks(manifest: dict, output_dir: Path, chunk_size: int) -> None:
    if chunk_size <= 0:
        raise ValueError("chunk_size must be positive")
    if output_dir.exists() and any(output_dir.iterdir()):
        raise ValueError("chunk output directory must be empty")
    output_dir.mkdir(parents=True, exist_ok=True)

    rows = manifest["rows"]
    for offset in range(0, len(rows), chunk_size):
        path = output_dir / f"chunk-{offset // chunk_size + 1:03d}.jsonl"
        with path.open("w", encoding="utf-8") as stream:
            for row in rows[offset : offset + chunk_size]:
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
    if not isinstance(decision, dict):
        raise ValueError("decision must be an object")
    decision_fields = set(decision)
    if not REQUIRED_DECISION_FIELDS <= decision_fields or not decision_fields <= (
        REQUIRED_DECISION_FIELDS | OPTIONAL_DECISION_FIELDS
    ):
        raise ValueError("decision has unexpected fields")
    if any(
        not isinstance(decision[field], str) or not decision[field].strip()
        for field in ("sourceDocumentId", "rangeId", "sourceLocator")
    ):
        raise ValueError("decision identity fields must be nonblank strings")
    if "sourceLabel" in decision and (
        not isinstance(decision["sourceLabel"], str) or not decision["sourceLabel"].strip()
    ):
        raise ValueError("sourceLabel must be a nonblank string")
    if "effectiveFrom" in decision and (
        not isinstance(decision["effectiveFrom"], str)
        or not decision["effectiveFrom"].strip()
    ):
        raise ValueError("effectiveFrom must be a nonblank string")
    if "effectiveTo" in decision:
        effective_to = decision["effectiveTo"]
        if effective_to is not None and (
            not isinstance(effective_to, str) or not effective_to.strip()
        ):
            raise ValueError("effectiveTo must be null or a nonblank string")

    targets = decision["productionTargets"]
    disposition = decision["disposition"]
    reason = decision["exclusionReason"]
    if not isinstance(disposition, str):
        raise ValueError("disposition must be a string")
    if not isinstance(targets, list):
        raise ValueError("productionTargets must be a list")
    if disposition == "seed":
        if not targets or reason is not None:
            raise ValueError("seed decision requires targets and null reason")
    elif disposition in {"excluded", "schema-gap"}:
        if targets or not isinstance(reason, str) or not reason.strip():
            raise ValueError("non-seed decision requires no targets and a reason")
    else:
        raise ValueError(f"unknown disposition: {disposition}")

    seen_targets = set()
    for target in targets:
        if not isinstance(target, dict):
            raise ValueError("target must be an object")
        if set(target) != TARGET_FIELDS:
            raise ValueError("target has unexpected fields")
        if (
            not isinstance(target["masterKind"], str)
            or target["masterKind"] not in ALLOWED_KINDS
        ):
            raise ValueError("unknown target masterKind")
        if not isinstance(target["seedKey"], str) or not target["seedKey"].strip():
            raise ValueError("blank target seedKey")
        if (
            not isinstance(target["mappingRole"], str)
            or target["mappingRole"] not in ALLOWED_ROLES
        ):
            raise ValueError("unknown mappingRole")
        supports = target["supports"]
        if not isinstance(supports, list) or not supports or not all(
            isinstance(support, str) for support in supports
        ):
            raise ValueError("supports must be a nonempty list of strings")
        if len(supports) != len(set(supports)) or not set(supports) <= ALLOWED_SUPPORTS:
            raise ValueError("invalid supports")
        mapping_reason = target["mappingReason"]
        if mapping_reason is not None and (
            not isinstance(mapping_reason, str) or not mapping_reason.strip()
        ):
            raise ValueError("mappingReason must be null or a nonblank string")
        if target["mappingRole"] != "primary" and mapping_reason is None:
            raise ValueError("component/supporting target requires mappingReason")
        canonical_target = (
            target["masterKind"],
            target["seedKey"],
            target["mappingRole"],
            tuple(sorted(supports)),
            mapping_reason,
        )
        if canonical_target in seen_targets:
            raise ValueError("duplicate target")
        seen_targets.add(canonical_target)


def apply_decisions(manifest: dict, decision_dir: Path) -> dict:
    decisions = {}
    for path in sorted(decision_dir.glob("*.jsonl")):
        for line in path.read_text(encoding="utf-8").splitlines():
            decision = json.loads(line)
            validate_decision(decision)
            key = identity(decision)
            if key in decisions:
                raise ValueError(f"duplicate decision: {key}")
            decisions[key] = decision

    rows_by_identity = {}
    for row in manifest["rows"]:
        key = identity(row)
        if key in rows_by_identity:
            raise ValueError(f"duplicate manifest row identity: {key}")
        rows_by_identity[key] = row

    if set(decisions) != set(rows_by_identity):
        missing = len(set(rows_by_identity) - set(decisions))
        extra = len(set(decisions) - set(rows_by_identity))
        raise ValueError(f"decision coverage mismatch: missing={missing}, extra={extra}")

    updated_rows = []
    for row in manifest["rows"]:
        decision = decisions[identity(row)]
        for field in OPTIONAL_DECISION_FIELDS:
            if field in decision and decision[field] != row[field]:
                raise ValueError(f"decision context mismatch: {field} for {identity(row)}")
        updated = dict(row)
        updated["disposition"] = decision["disposition"]
        updated["productionTargets"] = decision["productionTargets"]
        updated["exclusionReason"] = decision["exclusionReason"]
        updated_rows.append(updated)
    return {**manifest, "rows": updated_rows}


def disposition_counts(rows: list[dict]) -> dict[str, int]:
    return {
        disposition: sum(row.get("disposition") == disposition for row in rows)
        for disposition in ("seed", "excluded", "schema-gap")
    }


def ensure_unique_identities(rows: list[dict], label: str) -> None:
    identities = [identity(row) for row in rows]
    if len(identities) != len(set(identities)):
        raise ValueError(f"{label} contains duplicate row identities")


def validate_source_catalog(source_catalog: dict) -> dict[str, dict]:
    if source_catalog.get("schemaVersion") != "1":
        raise ValueError("source catalog schemaVersion must be 1")
    sources = source_catalog.get("sources")
    if not isinstance(sources, list):
        raise ValueError("source catalog sources must be a list")
    sources_by_id = {}
    for source in sources:
        if not isinstance(source, dict) or not isinstance(source.get("documentId"), str):
            raise ValueError("source catalog contains an invalid source")
        document_id = source["documentId"]
        if document_id in sources_by_id:
            raise ValueError(f"source catalog contains duplicate source: {document_id}")
        sources_by_id[document_id] = source
    for document_id, expected_sha in EXPECTED_SOURCE_SHAS.items():
        source = sources_by_id.get(document_id)
        if source is None or source.get("sha256") != expected_sha:
            raise ValueError(f"source catalog SHA mismatch: {document_id}")
    return sources_by_id


def validate_protected_baseline(manifest: dict) -> None:
    if manifest.get("schemaVersion") != "2":
        raise ValueError("protected facility baseline requires schemaVersion 2")
    summary = inventory_summary(manifest, "baseline")
    expected_summary = {
        "documents": 41,
        "ranges": 53,
        "rows": 14_718,
        "identitySha256": BASELINE_IDENTITY_SHA,
    }
    if summary["identitySha256"] != BASELINE_IDENTITY_SHA:
        raise ValueError(
            f"baseline identity digest mismatch: {summary['identitySha256']}"
        )
    if summary != expected_summary:
        raise ValueError(f"baseline inventory counts mismatch: {summary}")
    counts = disposition_counts(manifest["rows"])
    if counts != {"seed": 14_137, "excluded": 537, "schema-gap": 44}:
        raise ValueError(f"baseline disposition counts mismatch: {counts}")
    ensure_unique_identities(manifest["rows"], "baseline")
    actual_gap_identities = [
        identity(row)
        for row in manifest["rows"]
        if row["disposition"] == "schema-gap"
    ]
    expected_gap_identities = [target[:3] for target in PROTECTED_GAP_TARGETS]
    if actual_gap_identities != expected_gap_identities:
        raise ValueError("baseline schema-gap identities mismatch")


def primary_target(row: dict, code: str) -> dict:
    label = row["sourceLabel"]
    supports = ["service-identity", "selectors"]
    if any(marker in label for marker in ("地公体", "未計画", "人欠", "責欠")):
        supports.append("conditions")
    if any(marker in label for marker in ("未計画", "人欠", "責欠")):
        supports.extend(("unit-rule-value", "unit-rule-target"))
    supports.append("effective-period")
    mapping_reason = None
    if row["sourceDocumentId"] == "r6-service-codes-2-xlsx":
        mapping_reason = (
            f"R6公式XLSXの{row['sourceLocator']}をschema-v2 planned revision "
            f"service-code-{code}へcontinuity mappingし、略記された告示から式定数を推定しない"
        )
    return {
        "masterKind": "service-codes",
        "seedKey": f"service-code-{code}",
        "mappingRole": "primary",
        "supports": supports,
        "mappingReason": mapping_reason,
    }


def supporting_targets(
    codes: tuple[str, ...],
    supports: tuple[str, ...],
    reason: str,
) -> list[dict]:
    return [
        {
            "masterKind": "service-codes",
            "seedKey": f"service-code-{code}",
            "mappingRole": "supporting-evidence",
            "supports": list(supports),
            "mappingReason": reason.format(code=code),
        }
        for code in codes
    ]


def find_row(rows: list[dict], row_identity: tuple[str, str, str]) -> dict:
    matches = [row for row in rows if identity(row) == row_identity]
    if len(matches) != 1:
        raise ValueError(f"expected one row for identity: {row_identity}")
    return matches[0]


def validate_html_lines_contract(manifest: dict) -> None:
    ranges_by_identity = {
        (document["documentId"], extraction_range["rangeId"]): extraction_range
        for document in manifest["documents"]
        for extraction_range in document["extractionRanges"]
    }
    for row in manifest["rows"]:
        locator = row["sourceLocator"]
        if not locator.startswith("html:lines="):
            continue
        if HTML_LINES_LOCATOR_PATTERN.fullmatch(locator) is None:
            raise ValueError(f"invalid html-lines locator: {locator}")
        extraction_range = ranges_by_identity.get(
            (row["sourceDocumentId"], row["rangeId"])
        )
        if extraction_range is None or set(extraction_range) != {
            "rangeId", "kind", "lineFrom", "lineTo", "expectedItemCount"
        }:
            raise ValueError(f"invalid html-lines range: {row['rangeId']}")
        if extraction_range["kind"] != "html-lines":
            raise ValueError(f"invalid html-lines range kind: {row['rangeId']}")
        line_from = extraction_range["lineFrom"]
        line_to = extraction_range["lineTo"]
        if (
            not isinstance(line_from, int)
            or isinstance(line_from, bool)
            or not isinstance(line_to, int)
            or isinstance(line_to, bool)
            or line_from <= 0
            or line_to < line_from
            or extraction_range["expectedItemCount"] != 1
        ):
            raise ValueError(f"invalid html-lines range bounds: {row['rangeId']}")
        for match in re.finditer(r"l(\d+)(?:-l(\d+))?", locator):
            locator_from = int(match.group(1))
            locator_to = int(match.group(2) or match.group(1))
            if not line_from <= locator_from <= locator_to <= line_to:
                raise ValueError(f"html-lines locator outside range: {locator}")


def is_ordered_subsequence(
    expected: list[tuple[str, str, str]],
    actual: list[tuple[str, str, str]],
) -> bool:
    actual_iterator = iter(actual)
    return all(any(candidate == item for candidate in actual_iterator) for item in expected)


def finalize_protected_facility(manifest: dict, source_catalog: dict) -> dict:
    validate_protected_baseline(manifest)
    sources_by_id = validate_source_catalog(source_catalog)
    finalized = copy.deepcopy(manifest)
    rows = finalized["rows"]
    baseline_identities = [identity(row) for row in rows]

    for document_id, range_id, locator, code in PROTECTED_GAP_TARGETS:
        row = find_row(rows, (document_id, range_id, locator))
        row["disposition"] = "seed"
        row["productionTargets"] = [primary_target(row, code)]
        row["exclusionReason"] = None

    supporting_row_specs = (
        (
            ("r6-calculation-note", "r6-unit-and-cost-rounding", "pdf:physical-page=8"),
            ("unit-rule-step", "unit-rule-rounding"),
            "R6算定留意事項8頁が{code}の式適用stepと端数処理を同一期間で裏付けるため",
        ),
        (
            ("r6-calculation-note", "r6-unit-and-cost-rounding", "pdf:physical-page=9"),
            ("unit-rule-step", "unit-rule-rounding"),
            "R6算定留意事項9頁の計算例が{code}の式適用stepと端数処理を同一期間で裏付けるため",
        ),
        (
            ("r8-calculation-note", "r8-unit-and-cost-rounding", "pdf:physical-page=8"),
            ("unit-rule-step", "unit-rule-rounding"),
            "R8算定留意事項8頁が{code}の式適用stepと端数処理を同一期間で裏付けるため",
        ),
        (
            ("r8-calculation-note", "r8-unit-and-cost-rounding", "pdf:physical-page=9"),
            ("unit-rule-step", "unit-rule-rounding"),
            "R8算定留意事項9頁の計算例が{code}の式適用stepと端数処理を同一期間で裏付けるため",
        ),
        (
            ("r8-fee-notice", "r8-b-emergency-rate-and-reduction-continuity", "pdf:physical-page=56"),
            (
                "unit-rule-formula",
                "unit-rule-comparison",
                "unit-rule-local-government-adjustment",
                "unit-rule-runtime-input",
            ),
            "R8改正告示56頁が{code}の基準該当B型式と比較構造の改正関係を同一期間でcross-checkするため",
        ),
    )
    for row_identity, supports, reason in supporting_row_specs:
        row = find_row(rows, row_identity)
        if row["disposition"] != "seed":
            raise ValueError(f"supporting row must already be seed: {row_identity}")
        row["productionTargets"].extend(
            supporting_targets(PROTECTED_CODES, supports, reason)
        )

    documents_by_id = {
        document["documentId"]: document for document in finalized["documents"]
    }
    r6_fee_notice = documents_by_id.get("r6-fee-notice")
    if r6_fee_notice is None:
        raise ValueError("missing r6-fee-notice document")
    r6_fee_notice["extractionRanges"].extend(
        [
            {
                "rangeId": "r6-protected-facility-b-comparison",
                "kind": "pdf-pages",
                "pageFrom": 137,
                "pageTo": 137,
                "expectedItemCount": 1,
            },
            {
                "rangeId": "r6-protected-facility-b-local-government",
                "kind": "pdf-pages",
                "pageFrom": 138,
                "pageTo": 138,
                "expectedItemCount": 1,
            },
        ]
    )
    finalized["documents"].extend(
        [
            {
                "documentId": "current-fee-notice-html",
                "sourceSha256": sources_by_id["current-fee-notice-html"]["sha256"],
                "role": "authoritative",
                "extractionRanges": [
                    {
                        "rangeId": "r6-protected-facility-b-current-consolidated",
                        "kind": "html-lines",
                        "lineFrom": 2791,
                        "lineTo": 2793,
                        "expectedItemCount": 1,
                    },
                    {
                        "rangeId": "r8-protected-facility-b-current-consolidated",
                        "kind": "html-lines",
                        "lineFrom": 2791,
                        "lineTo": 2793,
                        "expectedItemCount": 1,
                    },
                ],
            },
            {
                "documentId": "protected-facility-administrative-expense-standard-html",
                "sourceSha256": sources_by_id[
                    "protected-facility-administrative-expense-standard-html"
                ]["sha256"],
                "role": "authoritative",
                "extractionRanges": [
                    {
                        "rangeId": "r6-protected-facility-administrative-expense-provenance",
                        "kind": "html-lines",
                        "lineFrom": 54,
                        "lineTo": 62,
                        "expectedItemCount": 1,
                    },
                    {
                        "rangeId": "r8-protected-facility-administrative-expense-provenance",
                        "kind": "html-lines",
                        "lineFrom": 54,
                        "lineTo": 62,
                        "expectedItemCount": 1,
                    },
                ],
            },
            {
                "documentId": "h31-fee-notice-consolidated",
                "sourceSha256": sources_by_id["h31-fee-notice-consolidated"]["sha256"],
                "role": "cross-check",
                "extractionRanges": [
                    {
                        "rangeId": "r6-protected-facility-b-formula-continuity",
                        "kind": "pdf-pages",
                        "pageFrom": 46,
                        "pageTo": 46,
                        "expectedItemCount": 1,
                    },
                    {
                        "rangeId": "r6-protected-facility-b-local-government-continuity",
                        "kind": "pdf-pages",
                        "pageFrom": 47,
                        "pageTo": 47,
                        "expectedItemCount": 1,
                    },
                ],
            },
        ]
    )

    evidence_specs = (
        (
            "r6-fee-notice",
            "r6-protected-facility-b-comparison",
            "pdf:physical-page=137",
            "基準該当就労継続支援B型の比較構造（R6継続性）",
            "2024-04",
            "2026-05",
            ("unit-rule-comparison",),
            "R6改正告示137頁が{code}の通常B型との比較構造の継続性をcross-checkするため",
        ),
        (
            "r6-fee-notice",
            "r6-protected-facility-b-local-government",
            "pdf:physical-page=138",
            "基準該当就労継続支援B型の地方公共団体補正（R6継続性）",
            "2024-04",
            "2026-05",
            ("unit-rule-local-government-adjustment",),
            "R6改正告示138頁が{code}の地方公共団体補正の継続性をcross-checkするため",
        ),
        (
            "current-fee-notice-html",
            "r6-protected-facility-b-current-consolidated",
            "html:lines=l000002791,l000002793",
            "基準該当就労継続支援B型の現行統合式（R6期間）",
            "2024-04",
            "2026-05",
            (
                "unit-rule-formula",
                "unit-rule-comparison",
                "unit-rule-local-government-adjustment",
                "unit-rule-runtime-input",
            ),
            "現行統合告示HTMLが{code}の式・比較・最小値・地方公共団体補正・runtime入力をR6期間で直接裏付けるため",
        ),
        (
            "current-fee-notice-html",
            "r8-protected-facility-b-current-consolidated",
            "html:lines=l000002791,l000002793",
            "基準該当就労継続支援B型の現行統合式（R8期間）",
            "2026-06",
            None,
            (
                "unit-rule-formula",
                "unit-rule-comparison",
                "unit-rule-local-government-adjustment",
                "unit-rule-runtime-input",
            ),
            "現行統合告示HTMLが{code}の式・比較・最小値・地方公共団体補正・runtime入力をR8期間で直接裏付けるため",
        ),
        (
            "protected-facility-administrative-expense-standard-html",
            "r6-protected-facility-administrative-expense-provenance",
            "html:lines=l000000054,l000000060-l000000062",
            "保護施設事務費の施設別provenance（R6期間）",
            "2024-04",
            "2026-05",
            ("unit-rule-runtime-input-provenance",),
            "支弁基準HTMLが{code}の1人当たり月額・施設別・地域別・定員別・加算別・通知provenanceをR6期間で裏付けるため",
        ),
        (
            "protected-facility-administrative-expense-standard-html",
            "r8-protected-facility-administrative-expense-provenance",
            "html:lines=l000000054,l000000060-l000000062",
            "保護施設事務費の施設別provenance（R8期間）",
            "2026-06",
            None,
            ("unit-rule-runtime-input-provenance",),
            "支弁基準HTMLが{code}の1人当たり月額・施設別・地域別・定員別・加算別・通知provenanceをR8期間で裏付けるため",
        ),
        (
            "h31-fee-notice-consolidated",
            "r6-protected-facility-b-formula-continuity",
            "pdf:physical-page=46",
            "基準該当就労継続支援B型の式・比較継続性",
            "2024-04",
            "2026-05",
            ("unit-rule-formula", "unit-rule-comparison"),
            "平成31年統合版46頁が{code}の公式式と比較構造のR6継続性だけをcross-checkするため",
        ),
        (
            "h31-fee-notice-consolidated",
            "r6-protected-facility-b-local-government-continuity",
            "pdf:physical-page=47",
            "基準該当就労継続支援B型の地方公共団体補正継続性",
            "2024-04",
            "2026-05",
            ("unit-rule-local-government-adjustment",),
            "平成31年統合版47頁が{code}の地方公共団体補正のR6継続性だけをcross-checkするため",
        ),
    )
    rows.extend(
        {
            "sourceDocumentId": document_id,
            "rangeId": range_id,
            "sourceLocator": locator,
            "sourceLabel": source_label,
            "effectiveFrom": effective_from,
            "effectiveTo": effective_to,
            "disposition": "seed",
            "productionTargets": supporting_targets(PROTECTED_CODES, supports, reason),
            "exclusionReason": None,
        }
        for (
            document_id,
            range_id,
            locator,
            source_label,
            effective_from,
            effective_to,
            supports,
            reason,
        ) in evidence_specs
    )

    final_summary = inventory_summary(finalized, "final")
    expected_final_summary = {
        "documents": 44,
        "ranges": 61,
        "rows": 14_726,
        "identitySha256": FINAL_IDENTITY_SHA,
    }
    if final_summary != expected_final_summary:
        raise ValueError(f"final inventory mismatch: {final_summary}")
    final_counts = disposition_counts(rows)
    if final_counts != {"seed": 14_189, "excluded": 537, "schema-gap": 0}:
        raise ValueError(f"final disposition counts mismatch: {final_counts}")
    ensure_unique_identities(rows, "final")
    if not is_ordered_subsequence(
        baseline_identities,
        [identity(row) for row in rows],
    ):
        raise ValueError("baseline identities are not an ordered subsequence")
    for row in rows:
        validate_decision(row)
    validate_html_lines_contract(finalized)

    evidence_rows = rows[-8:]
    expected_evidence_identities = [spec[:3] for spec in evidence_specs]
    if [identity(row) for row in evidence_rows] != expected_evidence_identities:
        raise ValueError("evidence row order mismatch")
    expected_seed_keys = {f"service-code-{code}" for code in PROTECTED_CODES}
    for row in evidence_rows:
        targets = row["productionTargets"]
        if (
            len(targets) != 22
            or {target["seedKey"] for target in targets} != expected_seed_keys
            or any(target["mappingRole"] != "supporting-evidence" for target in targets)
        ):
            raise ValueError(f"evidence target mismatch: {identity(row)}")
        is_r8 = row["rangeId"].startswith("r8-")
        expected_period = ("2026-06", None) if is_r8 else ("2024-04", "2026-05")
        if (row["effectiveFrom"], row["effectiveTo"]) != expected_period:
            raise ValueError(f"evidence period mismatch: {identity(row)}")
        if row["sourceDocumentId"] == "h31-fee-notice-consolidated" and is_r8:
            raise ValueError("H31 evidence must not map to R8")

    return finalized


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
    chunk_parser.add_argument("--chunk-size", type=positive_int, default=200)

    apply_parser = subparsers.add_parser("apply")
    apply_parser.add_argument("--manifest", type=Path, required=True)
    apply_parser.add_argument("--decision-dir", type=Path, required=True)
    apply_parser.add_argument("--output", type=Path, required=True)

    finalize_parser = subparsers.add_parser("finalize-protected-facility")
    finalize_parser.add_argument("--manifest", type=Path, required=True)
    finalize_parser.add_argument("--source-catalog", type=Path, required=True)
    finalize_parser.add_argument("--output", type=Path, required=True)

    args = parser.parse_args()
    if args.command == "migrate":
        source = load_json(args.input)
        migrated = migrate(source, args.expected_digest)
        summary = migration_summary(source, migrated)
        write_json(args.output, migrated)
        print(
            json.dumps(
                summary,
                ensure_ascii=False,
                separators=(",", ":"),
            )
        )
    elif args.command == "chunk":
        write_chunks(load_json(args.manifest), args.output_dir, args.chunk_size)
    elif args.command == "apply":
        write_json(args.output, apply_decisions(load_json(args.manifest), args.decision_dir))
    elif args.command == "finalize-protected-facility":
        finalized = finalize_protected_facility(
            load_json(args.manifest),
            load_json(args.source_catalog),
        )
        write_json(args.output, finalized)


if __name__ == "__main__":
    main()
