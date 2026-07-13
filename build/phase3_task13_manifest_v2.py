#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
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


if __name__ == "__main__":
    main()
