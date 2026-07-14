from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


MODULE_PATH = Path(__file__).parents[1] / "phase3_task13_manifest_v2.py"
REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
REPOSITORY_MANIFEST_PATH = (
    REPOSITORY_ROOT
    / "docs/spec-data/phase3/claim-master-source-row-manifest.json"
)
SOURCE_CATALOG_PATH = (
    REPOSITORY_ROOT / "src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json"
)
BASELINE_IDENTITY_SHA = (
    "90fb9d309e878d22f0d4bb867c4fe36c3fab83ad45938b64da2d5b3bfd34dee7"
)
FINAL_IDENTITY_SHA = (
    "c80f4e8da0aefc9d91bd978777bdb8e59261f4982826555f8a324e2023b9bcd7"
)
PROTECTED_CODES = [
    "462841", "462842", "462843", "462844", "462845", "462846",
    "46C841", "46C842", "46C843", "46C844", "46C845", "46C846",
    "46D841", "46D842", "46D843", "46D844", "46D845", "46D846",
    "46E841", "46E844", "46F841", "46F844",
]
PROTECTED_GAP_GROUPS = [
    (
        "r6-service-codes-2-xlsx",
        "r6-b-basic",
        [f"workbook-order=38;row={row}" for row in range(907, 913)],
    ),
    (
        "r6-service-codes-2-xlsx",
        "r6-b-support-staff-shortage",
        [f"workbook-order=40;row={row}" for row in range(1807, 1819)],
    ),
    (
        "r6-service-codes-2-xlsx",
        "r6-b-service-manager-shortage",
        [f"workbook-order=41;row={row}" for row in range(607, 611)],
    ),
    (
        "r8-service-codes-2-xlsx",
        "r8-b-basic",
        [f"workbook-order=38;row={row}" for row in range(1987, 1993)],
    ),
    (
        "r8-service-codes-2-xlsx",
        "r8-b-support-staff-shortage",
        [f"workbook-order=40;row={row}" for row in range(3967, 3979)],
    ),
    (
        "r8-service-codes-2-xlsx",
        "r8-b-service-manager-shortage",
        [f"workbook-order=41;row={row}" for row in range(1327, 1331)],
    ),
]
PROTECTED_GAP_IDENTITIES = [
    (document_id, range_id, locator)
    for document_id, range_id, locators in PROTECTED_GAP_GROUPS
    for locator in locators
]
PROTECTED_EVIDENCE_IDENTITIES = [
    ("r6-fee-notice", "r6-protected-facility-b-comparison", "pdf:physical-page=137"),
    (
        "r6-fee-notice",
        "r6-protected-facility-b-local-government",
        "pdf:physical-page=138",
    ),
    (
        "current-fee-notice-html",
        "r6-protected-facility-b-current-consolidated",
        "html:lines=l000002791,l000002793",
    ),
    (
        "current-fee-notice-html",
        "r8-protected-facility-b-current-consolidated",
        "html:lines=l000002791,l000002793",
    ),
    (
        "protected-facility-administrative-expense-standard-html",
        "r6-protected-facility-administrative-expense-provenance",
        "html:lines=l000000054,l000000060-l000000062",
    ),
    (
        "protected-facility-administrative-expense-standard-html",
        "r8-protected-facility-administrative-expense-provenance",
        "html:lines=l000000054,l000000060-l000000062",
    ),
    (
        "h31-fee-notice-consolidated",
        "r6-protected-facility-b-formula-continuity",
        "pdf:physical-page=46",
    ),
    (
        "h31-fee-notice-consolidated",
        "r6-protected-facility-b-local-government-continuity",
        "pdf:physical-page=47",
    ),
]
PROTECTED_RANGE_IDS = {identity[1] for identity in PROTECTED_EVIDENCE_IDENTITIES}
if MODULE_PATH.exists():
    SPEC = importlib.util.spec_from_file_location("phase3_task13_manifest_v2", MODULE_PATH)
    assert SPEC is not None and SPEC.loader is not None
    manifest_v2 = importlib.util.module_from_spec(SPEC)
    SPEC.loader.exec_module(manifest_v2)
else:
    manifest_v2 = None


def fixture_identity(row: dict) -> tuple[str, str, str]:
    return row["sourceDocumentId"], row["rangeId"], row["sourceLocator"]


def fixture_identity_digest(rows: list[dict]) -> str:
    payload = "".join(
        json.dumps(fixture_identity(row), ensure_ascii=False, separators=(",", ":")) + "\n"
        for row in rows
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def make_v1_manifest() -> dict:
    documents = []
    range_index = 0
    for document_index in range(41):
        extraction_ranges = []
        for _ in range(2 if document_index < 10 else 1):
            extraction_ranges.append(
                {
                    "rangeId": f"range-{range_index}",
                    "expectedItemCount": 14_659 if range_index == 0 else 1,
                }
            )
            range_index += 1
        documents.append(
            {
                "documentId": f"document-{document_index:02d}",
                "extractionRanges": extraction_ranges,
            }
        )
    rows = []
    for index in range(14_709):
        disposition = "seed" if index == 0 else "excluded" if index == 1 else "schema-gap"
        rows.append(
            {
                "sourceDocumentId": f"document-{index % 41:02d}",
                "rangeId": f"range-{index % 7}",
                "sourceLocator": f"fixture:row={index + 1}",
                "sourceLabel": f"fixture label {index + 1}",
                "effectiveFrom": "2024-04",
                "effectiveTo": None,
                "disposition": disposition,
                "masterKind": "region-unit-prices" if disposition == "seed" else None,
                "seedKey": "fixture-region-grade-1" if disposition == "seed" else None,
                "aggregationId": None,
                "aggregationKind": None,
                "aggregationReason": None,
                "exclusionReason": None if disposition == "seed" else "fixture reason",
            }
        )
    return {"schemaVersion": "1", "documents": documents, "rows": rows}


def make_v2_row(index: int) -> dict:
    return {
        "sourceDocumentId": "document-00",
        "rangeId": "range-0",
        "sourceLocator": f"fixture:row={index + 1}",
        "sourceLabel": f"fixture label {index + 1}",
        "effectiveFrom": "2024-04",
        "effectiveTo": None,
        "disposition": "schema-gap",
        "productionTargets": [],
        "exclusionReason": "fixture reason",
    }


def make_target(**overrides: object) -> dict:
    target = {
        "masterKind": "service-codes",
        "seedKey": "fixture-service-code",
        "mappingRole": "primary",
        "supports": ["service-identity", "effective-period"],
        "mappingReason": None,
    }
    target.update(overrides)
    return target


def make_decision(row: dict, **overrides: object) -> dict:
    decision = {
        "sourceDocumentId": row["sourceDocumentId"],
        "rangeId": row["rangeId"],
        "sourceLocator": row["sourceLocator"],
        "sourceLabel": row["sourceLabel"],
        "effectiveFrom": row["effectiveFrom"],
        "effectiveTo": row["effectiveTo"],
        "disposition": "seed",
        "productionTargets": [make_target()],
        "exclusionReason": None,
    }
    decision.update(overrides)
    return decision


def make_plan_style_decision(row: dict, **overrides: object) -> dict:
    decision = {
        "sourceDocumentId": row["sourceDocumentId"],
        "rangeId": row["rangeId"],
        "sourceLocator": row["sourceLocator"],
        "disposition": "seed",
        "productionTargets": [make_target()],
        "exclusionReason": None,
    }
    decision.update(overrides)
    return decision


def write_decisions(path: Path, decisions: list[dict]) -> None:
    path.write_text(
        "".join(json.dumps(decision, ensure_ascii=False) + "\n" for decision in decisions),
        encoding="utf-8",
    )


def run_cli(*arguments: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(MODULE_PATH), *arguments],
        check=False,
        capture_output=True,
        text=True,
    )


def make_protected_facility_baseline() -> dict:
    manifest = json.loads(REPOSITORY_MANIFEST_PATH.read_text(encoding="utf-8"))
    baseline = copy.deepcopy(manifest)
    baseline["documents"] = baseline["documents"][:41]
    r6_fee_notice = next(
        document
        for document in baseline["documents"]
        if document["documentId"] == "r6-fee-notice"
    )
    r6_fee_notice["extractionRanges"] = [
        extraction_range
        for extraction_range in r6_fee_notice["extractionRanges"]
        if extraction_range["rangeId"] not in PROTECTED_RANGE_IDS
    ]
    baseline["rows"] = baseline["rows"][:14_718]

    rows_by_identity = {
        fixture_identity(row): row for row in baseline["rows"]
    }
    protected_seed_keys = {f"service-code-{code}" for code in PROTECTED_CODES}
    for supporting_identity in (
        ("r6-calculation-note", "r6-unit-and-cost-rounding", "pdf:physical-page=8"),
        ("r6-calculation-note", "r6-unit-and-cost-rounding", "pdf:physical-page=9"),
        ("r8-calculation-note", "r8-unit-and-cost-rounding", "pdf:physical-page=8"),
        ("r8-calculation-note", "r8-unit-and-cost-rounding", "pdf:physical-page=9"),
        (
            "r8-fee-notice",
            "r8-b-emergency-rate-and-reduction-continuity",
            "pdf:physical-page=56",
        ),
    ):
        supporting_row = rows_by_identity[supporting_identity]
        supporting_row["productionTargets"] = [
            target
            for target in supporting_row["productionTargets"]
            if target["seedKey"] not in protected_seed_keys
        ]
    for target_identity in PROTECTED_GAP_IDENTITIES:
        row = rows_by_identity[target_identity]
        row["disposition"] = "schema-gap"
        row["productionTargets"] = []
        row["exclusionReason"] = (
            "protected-facility-benchmark-minimum: schema contract pending"
        )
    return baseline


class ModuleAvailabilityTests(unittest.TestCase):
    def test_converter_module_is_implemented(self) -> None:
        self.assertIsNotNone(
            manifest_v2,
            f"converter module is not implemented: {MODULE_PATH}",
        )


@unittest.skipUnless(manifest_v2 is not None, "converter module is not implemented")
class ConverterBehaviorTests(unittest.TestCase):
    def test_migrate_preserves_inventory_order_identity_and_mechanical_projection(self) -> None:
        source = make_v1_manifest()
        expected_digest = fixture_identity_digest(source["rows"])

        migrated = manifest_v2.migrate(source, expected_digest)

        self.assertEqual(["schemaVersion", "documents", "rows"], list(migrated))
        self.assertEqual("2", migrated["schemaVersion"])
        self.assertEqual(source["documents"], migrated["documents"])
        self.assertEqual(41, len(migrated["documents"]))
        self.assertEqual(14_709, len(migrated["rows"]))
        self.assertEqual(
            [fixture_identity(row) for row in source["rows"]],
            [fixture_identity(row) for row in migrated["rows"]],
        )
        self.assertEqual(expected_digest, fixture_identity_digest(migrated["rows"]))

        self.assertEqual(
            [
                "sourceDocumentId",
                "rangeId",
                "sourceLocator",
                "sourceLabel",
                "effectiveFrom",
                "effectiveTo",
                "disposition",
                "productionTargets",
                "exclusionReason",
            ],
            list(migrated["rows"][0]),
        )
        self.assertEqual(
            [
                {
                    "masterKind": "region-unit-prices",
                    "seedKey": "fixture-region-grade-1",
                    "mappingRole": "primary",
                    "supports": ["effective-period", "master-values"],
                    "mappingReason": None,
                }
            ],
            migrated["rows"][0]["productionTargets"],
        )
        self.assertEqual([], migrated["rows"][1]["productionTargets"])
        self.assertEqual([], migrated["rows"][2]["productionTargets"])
        for row in migrated["rows"]:
            self.assertTrue(
                set(manifest_v2.OLD_MAPPING_FIELDS).isdisjoint(row),
                msg=f"old mapping field remained in {fixture_identity(row)}",
            )

    def test_migrate_rejects_wrong_schema_inventory_and_digest(self) -> None:
        source = make_v1_manifest()
        expected_digest = fixture_identity_digest(source["rows"])
        invalid_sources = [
            ({**source, "schemaVersion": "2"}, expected_digest),
            ({**source, "documents": source["documents"][:-1]}, expected_digest),
            ({**source, "rows": source["rows"][:-1]}, expected_digest),
            (source, "0" * 64),
        ]

        for invalid_source, digest in invalid_sources:
            with self.subTest(schema=invalid_source.get("schemaVersion"), digest=digest):
                with self.assertRaises(ValueError):
                    manifest_v2.migrate(invalid_source, digest)

    def test_validate_decision_rejects_closed_contract_violations(self) -> None:
        row = make_v2_row(0)
        valid = make_decision(row)
        invalid_decisions = [
            {**valid, "productionTargets": [make_target(masterKind="unknown")]},
            {**valid, "productionTargets": [make_target(mappingRole="unknown")]},
            {**valid, "productionTargets": [make_target(supports=["unknown"])]},
            {
                **valid,
                "productionTargets": [
                    make_target(supports=["service-identity", "service-identity"])
                ],
            },
            {**valid, "productionTargets": [make_target(seedKey="  ")]},
            {**valid, "productionTargets": []},
            {**valid, "exclusionReason": "seed must not have a reason"},
            {
                **valid,
                "disposition": "excluded",
                "exclusionReason": "excluded",
            },
            {
                **valid,
                "disposition": "excluded",
                "productionTargets": [],
                "exclusionReason": "  ",
            },
            {
                **valid,
                "disposition": "schema-gap",
                "productionTargets": [],
                "exclusionReason": None,
            },
            {**valid, "disposition": "unknown"},
            {
                **valid,
                "productionTargets": [
                    make_target(mappingRole="component", mappingReason="  ")
                ],
            },
            {
                **valid,
                "productionTargets": [
                    {**make_target(), "unexpected": "field"}
                ],
            },
        ]

        manifest_v2.validate_decision(valid)
        for invalid in invalid_decisions:
            with self.subTest(invalid=invalid):
                with self.assertRaises(ValueError):
                    manifest_v2.validate_decision(invalid)

    def test_validate_decision_rejects_invalid_shapes_types_and_duplicate_targets(self) -> None:
        row = make_v2_row(0)
        valid = make_decision(row)
        without_locator = dict(valid)
        without_locator.pop("sourceLocator")
        duplicate_target = make_target()
        invalid_decisions = [
            ([], "decision must be an object"),
            (without_locator, "decision has unexpected fields"),
            ({**valid, "unexpected": "field"}, "decision has unexpected fields"),
            ({**valid, "sourceDocumentId": "  "}, "identity fields must be nonblank strings"),
            ({**valid, "rangeId": 42}, "identity fields must be nonblank strings"),
            ({**valid, "sourceLabel": ""}, "sourceLabel must be a nonblank string"),
            ({**valid, "effectiveFrom": None}, "effectiveFrom must be a nonblank string"),
            ({**valid, "effectiveTo": 42}, "effectiveTo must be null or a nonblank string"),
            ({**valid, "productionTargets": make_target()}, "productionTargets must be a list"),
            ({**valid, "productionTargets": [42]}, "target must be an object"),
            (
                {**valid, "productionTargets": [make_target(supports="conditions")]},
                "supports must be a nonempty list of strings",
            ),
            (
                {**valid, "productionTargets": [make_target(supports=[42])]},
                "supports must be a nonempty list of strings",
            ),
            (
                {**valid, "productionTargets": [make_target(mappingReason=42)]},
                "mappingReason must be null or a nonblank string",
            ),
            (
                {**valid, "productionTargets": [make_target(mappingReason={})]},
                "mappingReason must be null or a nonblank string",
            ),
            (
                {**valid, "productionTargets": [make_target(mappingReason="  ")]},
                "mappingReason must be null or a nonblank string",
            ),
            (
                {
                    **valid,
                    "productionTargets": [duplicate_target, dict(duplicate_target)],
                },
                "duplicate target",
            ),
            (
                {
                    **valid,
                    "productionTargets": [
                        duplicate_target,
                        {
                            **duplicate_target,
                            "supports": list(reversed(duplicate_target["supports"])),
                        },
                    ],
                },
                "duplicate target",
            ),
        ]

        for invalid, message in invalid_decisions:
            with self.subTest(message=message, invalid=invalid):
                with self.assertRaisesRegex(ValueError, message):
                    manifest_v2.validate_decision(invalid)

    def test_apply_decisions_requires_exact_unique_identity_coverage(self) -> None:
        rows = [make_v2_row(index) for index in range(3)]
        manifest = {"schemaVersion": "2", "documents": [], "rows": rows}
        decisions = [make_decision(row) for row in rows]

        with tempfile.TemporaryDirectory() as temporary:
            decision_dir = Path(temporary)

            write_decisions(decision_dir / "missing.jsonl", decisions[:-1])
            with self.assertRaises(ValueError):
                manifest_v2.apply_decisions(manifest, decision_dir)

            (decision_dir / "missing.jsonl").unlink()
            extra = make_decision(make_v2_row(99))
            write_decisions(decision_dir / "extra.jsonl", decisions + [extra])
            with self.assertRaises(ValueError):
                manifest_v2.apply_decisions(manifest, decision_dir)

            (decision_dir / "extra.jsonl").unlink()
            write_decisions(decision_dir / "duplicate.jsonl", decisions + [decisions[0]])
            with self.assertRaises(ValueError):
                manifest_v2.apply_decisions(manifest, decision_dir)

            (decision_dir / "duplicate.jsonl").unlink()
            write_decisions(decision_dir / "complete.jsonl", decisions)
            duplicate_manifest = {
                **manifest,
                "rows": [rows[0], rows[0], rows[1], rows[2]],
            }
            with self.assertRaises(ValueError):
                manifest_v2.apply_decisions(duplicate_manifest, decision_dir)

    def test_apply_decisions_preserves_row_order_and_identity(self) -> None:
        rows = [make_v2_row(index) for index in range(3)]
        manifest = {"schemaVersion": "2", "documents": [], "rows": rows}
        decisions = [make_decision(row) for row in reversed(rows)]

        with tempfile.TemporaryDirectory() as temporary:
            decision_dir = Path(temporary)
            write_decisions(decision_dir / "decisions.jsonl", decisions)

            applied = manifest_v2.apply_decisions(manifest, decision_dir)

        self.assertEqual(
            [fixture_identity(row) for row in rows],
            [fixture_identity(row) for row in applied["rows"]],
        )
        self.assertTrue(all(row["disposition"] == "seed" for row in applied["rows"]))
        self.assertTrue(all(row["exclusionReason"] is None for row in applied["rows"]))

    def test_apply_decisions_accepts_exact_plan_style_decision_without_context(self) -> None:
        row = make_v2_row(0)
        manifest = {"schemaVersion": "2", "documents": [], "rows": [row]}
        decision = make_plan_style_decision(row)

        with tempfile.TemporaryDirectory() as temporary:
            decision_dir = Path(temporary)
            write_decisions(decision_dir / "decisions.jsonl", [decision])
            applied = manifest_v2.apply_decisions(manifest, decision_dir)

        self.assertEqual("seed", applied["rows"][0]["disposition"])
        self.assertEqual([make_target()], applied["rows"][0]["productionTargets"])
        self.assertIsNone(applied["rows"][0]["exclusionReason"])

    def test_apply_decisions_checks_optional_context_when_present(self) -> None:
        row = make_v2_row(0)
        manifest = {"schemaVersion": "2", "documents": [], "rows": [row]}

        with tempfile.TemporaryDirectory() as temporary:
            decision_dir = Path(temporary)
            decision_path = decision_dir / "decisions.jsonl"
            write_decisions(decision_path, [make_decision(row)])
            applied = manifest_v2.apply_decisions(manifest, decision_dir)
            self.assertEqual("seed", applied["rows"][0]["disposition"])

            mismatches = {
                "sourceLabel": "different label",
                "effectiveFrom": "2026-06",
                "effectiveTo": "2026-05",
            }
            for field, value in mismatches.items():
                with self.subTest(field=field):
                    write_decisions(decision_path, [make_decision(row, **{field: value})])
                    with self.assertRaisesRegex(ValueError, f"context mismatch: {field}"):
                        manifest_v2.apply_decisions(manifest, decision_dir)

    def test_write_chunks_rejects_nonempty_output_directory(self) -> None:
        manifest = {"schemaVersion": "2", "documents": [], "rows": [make_v2_row(0)]}
        with tempfile.TemporaryDirectory() as temporary:
            output_dir = Path(temporary)
            (output_dir / "existing.txt").write_text("occupied", encoding="utf-8")
            with self.assertRaises(ValueError):
                manifest_v2.write_chunks(manifest, output_dir, 200)

    def test_write_chunks_rejects_nonpositive_chunk_size(self) -> None:
        manifest = {"schemaVersion": "2", "documents": [], "rows": [make_v2_row(0)]}
        with tempfile.TemporaryDirectory() as temporary:
            for chunk_size in (0, -1):
                with self.subTest(chunk_size=chunk_size):
                    output_dir = Path(temporary) / f"chunks-{chunk_size}"
                    with self.assertRaisesRegex(ValueError, "chunk_size must be positive"):
                        manifest_v2.write_chunks(manifest, output_dir, chunk_size)
                    self.assertFalse(output_dir.exists())

    def test_write_chunks_splits_401_rows_deterministically(self) -> None:
        rows = [make_v2_row(index) for index in range(401)]
        manifest = {"schemaVersion": "2", "documents": [], "rows": rows}

        with tempfile.TemporaryDirectory() as first_temporary, tempfile.TemporaryDirectory() as second_temporary:
            first_output = Path(first_temporary) / "chunks"
            second_output = Path(second_temporary) / "chunks"
            manifest_v2.write_chunks(manifest, first_output, 200)
            manifest_v2.write_chunks(manifest, second_output, 200)

            first_paths = sorted(first_output.iterdir())
            second_paths = sorted(second_output.iterdir())
            self.assertEqual(
                ["chunk-001.jsonl", "chunk-002.jsonl", "chunk-003.jsonl"],
                [path.name for path in first_paths],
            )
            self.assertEqual(
                [200, 200, 1],
                [len(path.read_text(encoding="utf-8").splitlines()) for path in first_paths],
            )
            self.assertEqual(
                [path.read_bytes() for path in first_paths],
                [path.read_bytes() for path in second_paths],
            )
            chunk_rows = [
                json.loads(line)
                for path in first_paths
                for line in path.read_text(encoding="utf-8").splitlines()
            ]
            self.assertEqual(
                [fixture_identity(row) for row in rows],
                [fixture_identity(row) for row in chunk_rows],
            )

    def test_chunk_cli_rejects_nonpositive_chunk_size(self) -> None:
        manifest = {"schemaVersion": "2", "documents": [], "rows": [make_v2_row(0)]}
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            manifest_path = temporary_path / "manifest.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            for chunk_size in (0, -1):
                with self.subTest(chunk_size=chunk_size):
                    output_dir = temporary_path / f"chunks-{chunk_size}"
                    result = run_cli(
                        "chunk",
                        "--manifest",
                        str(manifest_path),
                        "--output-dir",
                        str(output_dir),
                        "--chunk-size",
                        str(chunk_size),
                    )
                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual("", result.stdout)
                    self.assertIn(
                        "argument --chunk-size: must be a positive integer",
                        result.stderr,
                    )
                    self.assertFalse(output_dir.exists())

    def test_migrate_cli_writes_output_and_prints_deterministic_summary(self) -> None:
        source = make_v1_manifest()
        expected_digest = fixture_identity_digest(source["rows"])
        expected_inventory = {
            "documents": 41,
            "ranges": 51,
            "rows": 14_709,
            "identitySha256": expected_digest,
        }
        expected_summary = {
            "before": expected_inventory,
            "after": expected_inventory,
        }
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            input_path = temporary_path / "manifest-v1.json"
            output_path = temporary_path / "manifest-v2.json"
            input_path.write_text(
                json.dumps(source, ensure_ascii=False),
                encoding="utf-8",
            )

            result = run_cli(
                "migrate",
                "--input",
                str(input_path),
                "--output",
                str(output_path),
                "--expected-digest",
                expected_digest,
            )

            self.assertEqual(0, result.returncode, msg=result.stderr)
            self.assertEqual("", result.stderr)
            self.assertEqual(
                json.dumps(expected_summary, separators=(",", ":")) + "\n",
                result.stdout,
            )
            self.assertEqual(
                manifest_v2.migrate(source, expected_digest),
                json.loads(output_path.read_text(encoding="utf-8")),
            )

            failed_output_path = temporary_path / "failed-manifest-v2.json"
            failed = run_cli(
                "migrate",
                "--input",
                str(input_path),
                "--output",
                str(failed_output_path),
                "--expected-digest",
                "0" * 64,
            )
            self.assertNotEqual(0, failed.returncode)
            self.assertEqual("", failed.stdout)
            self.assertFalse(failed_output_path.exists())

    def test_migrate_cli_rejects_malformed_inventory_without_touching_output(self) -> None:
        source = make_v1_manifest()
        expected_digest = fixture_identity_digest(source["rows"])
        malformed_cases = []

        documents_with_invalid_ranges = list(source["documents"])
        documents_with_invalid_ranges[0] = {
            **documents_with_invalid_ranges[0],
            "extractionRanges": {"not": "a list"},
        }
        malformed_cases.append(
            (
                "invalid-ranges",
                {**source, "documents": documents_with_invalid_ranges},
                "documents[0].extractionRanges must be a list",
                None,
            )
        )

        documents_with_invalid_expected_count = list(source["documents"])
        invalid_expected_count_ranges = list(
            documents_with_invalid_expected_count[0]["extractionRanges"]
        )
        invalid_expected_count_ranges[0] = {
            **invalid_expected_count_ranges[0],
            "expectedItemCount": "14709",
        }
        documents_with_invalid_expected_count[0] = {
            **documents_with_invalid_expected_count[0],
            "extractionRanges": invalid_expected_count_ranges,
        }
        malformed_cases.append(
            (
                "invalid-expected-count",
                {**source, "documents": documents_with_invalid_expected_count},
                "expectedItemCount must be a positive integer",
                "preserve-existing-output\n",
            )
        )

        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            for name, malformed, error_message, existing_output in malformed_cases:
                with self.subTest(name=name):
                    input_path = temporary_path / f"{name}.json"
                    output_path = temporary_path / f"{name}-output.json"
                    input_path.write_text(
                        json.dumps(malformed, ensure_ascii=False),
                        encoding="utf-8",
                    )
                    if existing_output is not None:
                        output_path.write_text(existing_output, encoding="utf-8")

                    result = run_cli(
                        "migrate",
                        "--input",
                        str(input_path),
                        "--output",
                        str(output_path),
                        "--expected-digest",
                        expected_digest,
                    )

                    self.assertNotEqual(0, result.returncode)
                    self.assertEqual("", result.stdout)
                    self.assertIn(error_message, result.stderr)
                    if existing_output is None:
                        self.assertFalse(output_path.exists())
                    else:
                        self.assertEqual(
                            existing_output,
                            output_path.read_text(encoding="utf-8"),
                        )
                    self.assertEqual(
                        [],
                        list(temporary_path.glob(f".{output_path.name}.*.tmp")),
                    )


@unittest.skipUnless(manifest_v2 is not None, "converter module is not implemented")
class ProtectedFacilityFinalizerTests(unittest.TestCase):
    def test_finalizer_constants_close_the_exact_support_and_identity_contracts(self) -> None:
        self.assertEqual(BASELINE_IDENTITY_SHA, manifest_v2.BASELINE_IDENTITY_SHA)
        self.assertEqual(FINAL_IDENTITY_SHA, manifest_v2.FINAL_IDENTITY_SHA)
        self.assertEqual(
            list(zip(PROTECTED_GAP_IDENTITIES, PROTECTED_CODES * 2)),
            [
                ((document_id, range_id, locator), code)
                for document_id, range_id, locator, code
                in manifest_v2.PROTECTED_GAP_TARGETS
            ],
        )
        self.assertEqual(
            {
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
            },
            manifest_v2.ALLOWED_SUPPORTS,
        )

    def test_finalize_rejects_wrong_digest_counts_and_gap_identities(self) -> None:
        baseline = make_protected_facility_baseline()
        catalog = json.loads(SOURCE_CATALOG_PATH.read_text(encoding="utf-8"))

        wrong_digest = copy.deepcopy(baseline)
        wrong_digest["rows"][0], wrong_digest["rows"][1] = (
            wrong_digest["rows"][1],
            wrong_digest["rows"][0],
        )
        with self.assertRaisesRegex(ValueError, "baseline identity digest mismatch"):
            manifest_v2.finalize_protected_facility(wrong_digest, catalog)

        wrong_counts = copy.deepcopy(baseline)
        gap_row = next(
            row
            for row in wrong_counts["rows"]
            if fixture_identity(row) == PROTECTED_GAP_IDENTITIES[0]
        )
        gap_row["disposition"] = "excluded"
        with self.assertRaisesRegex(ValueError, "baseline disposition counts mismatch"):
            manifest_v2.finalize_protected_facility(wrong_counts, catalog)

        wrong_gaps = copy.deepcopy(baseline)
        rows_by_identity = {
            fixture_identity(row): row for row in wrong_gaps["rows"]
        }
        removed_gap = rows_by_identity[PROTECTED_GAP_IDENTITIES[0]]
        removed_gap["disposition"] = "seed"
        removed_gap["productionTargets"] = [make_target(seedKey="service-code-462841")]
        removed_gap["exclusionReason"] = None
        replacement_gap = next(
            row
            for row in wrong_gaps["rows"]
            if fixture_identity(row) not in PROTECTED_GAP_IDENTITIES
            and row["disposition"] == "seed"
        )
        replacement_gap["disposition"] = "schema-gap"
        replacement_gap["productionTargets"] = []
        replacement_gap["exclusionReason"] = "replacement gap"
        with self.assertRaisesRegex(ValueError, "baseline schema-gap identities mismatch"):
            manifest_v2.finalize_protected_facility(wrong_gaps, catalog)

    def test_finalize_preserves_baseline_as_ordered_subsequence_and_closes_inventory(self) -> None:
        baseline = make_protected_facility_baseline()
        catalog = json.loads(SOURCE_CATALOG_PATH.read_text(encoding="utf-8"))

        first = manifest_v2.finalize_protected_facility(baseline, catalog)
        second = manifest_v2.finalize_protected_facility(
            copy.deepcopy(baseline),
            copy.deepcopy(catalog),
        )

        self.assertEqual(first, second)
        self.assertEqual(
            [fixture_identity(row) for row in baseline["rows"]],
            [fixture_identity(row) for row in first["rows"][:14_718]],
        )
        self.assertEqual(
            {
                "documents": 44,
                "ranges": 61,
                "rows": 14_726,
                "identitySha256": FINAL_IDENTITY_SHA,
            },
            manifest_v2.inventory_summary(first, "final"),
        )
        disposition_counts = {
            disposition: sum(
                row["disposition"] == disposition for row in first["rows"]
            )
            for disposition in ("seed", "excluded", "schema-gap")
        }
        self.assertEqual(
            {"seed": 14_189, "excluded": 537, "schema-gap": 0},
            disposition_counts,
        )

        rows_by_identity = {
            fixture_identity(row): row for row in first["rows"]
        }
        for identity_value in PROTECTED_GAP_IDENTITIES:
            row = rows_by_identity[identity_value]
            self.assertEqual("seed", row["disposition"])
            primary_targets = [
                target
                for target in row["productionTargets"]
                if target["mappingRole"] == "primary"
            ]
            self.assertEqual(1, len(primary_targets))
            if identity_value[0].startswith("r6-"):
                self.assertTrue(primary_targets[0]["mappingReason"].strip())

    def test_evidence_rows_map_only_to_the_22_same_period_planned_targets(self) -> None:
        final = manifest_v2.finalize_protected_facility(
            make_protected_facility_baseline(),
            json.loads(SOURCE_CATALOG_PATH.read_text(encoding="utf-8")),
        )
        evidence_rows = final["rows"][-8:]

        self.assertEqual(
            PROTECTED_EVIDENCE_IDENTITIES,
            [fixture_identity(row) for row in evidence_rows],
        )
        expected_seed_keys = {f"service-code-{code}" for code in PROTECTED_CODES}
        for row in evidence_rows:
            self.assertEqual("seed", row["disposition"])
            self.assertEqual(22, len(row["productionTargets"]))
            self.assertEqual(
                expected_seed_keys,
                {target["seedKey"] for target in row["productionTargets"]},
            )
            self.assertTrue(
                all(
                    target["mappingRole"] == "supporting-evidence"
                    and target["mappingReason"].strip()
                    for target in row["productionTargets"]
                )
            )
            if row["rangeId"].startswith("r6-"):
                self.assertEqual(("2024-04", "2026-05"), (
                    row["effectiveFrom"], row["effectiveTo"]
                ))
            else:
                self.assertEqual(("2026-06", None), (
                    row["effectiveFrom"], row["effectiveTo"]
                ))
        self.assertTrue(
            all(
                row["effectiveTo"] == "2026-05"
                for row in evidence_rows
                if row["sourceDocumentId"] == "h31-fee-notice-consolidated"
            )
        )

    def test_finalize_cli_is_deterministic_and_atomic_on_failure(self) -> None:
        baseline = make_protected_facility_baseline()
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            manifest_path = temporary_path / "baseline.json"
            manifest_path.write_text(
                json.dumps(baseline, ensure_ascii=False),
                encoding="utf-8",
            )
            first_output = temporary_path / "first.json"
            second_output = temporary_path / "second.json"
            arguments = (
                "finalize-protected-facility",
                "--manifest",
                str(manifest_path),
                "--source-catalog",
                str(SOURCE_CATALOG_PATH),
            )

            first = run_cli(*arguments, "--output", str(first_output))
            second = run_cli(*arguments, "--output", str(second_output))

            self.assertEqual(0, first.returncode, msg=first.stderr)
            self.assertEqual(0, second.returncode, msg=second.stderr)
            self.assertEqual(first_output.read_bytes(), second_output.read_bytes())

            invalid = copy.deepcopy(baseline)
            invalid["rows"][0], invalid["rows"][1] = (
                invalid["rows"][1],
                invalid["rows"][0],
            )
            invalid_path = temporary_path / "invalid.json"
            invalid_path.write_text(
                json.dumps(invalid, ensure_ascii=False),
                encoding="utf-8",
            )
            preserved_output = temporary_path / "preserved.json"
            preserved_output.write_text("preserve-existing-output\n", encoding="utf-8")

            failed = run_cli(
                "finalize-protected-facility",
                "--manifest",
                str(invalid_path),
                "--source-catalog",
                str(SOURCE_CATALOG_PATH),
                "--output",
                str(preserved_output),
            )

            self.assertNotEqual(0, failed.returncode)
            self.assertEqual("", failed.stdout)
            self.assertEqual(
                "preserve-existing-output\n",
                preserved_output.read_text(encoding="utf-8"),
            )
            self.assertEqual(
                [],
                list(temporary_path.glob(f".{preserved_output.name}.*.tmp")),
            )


if __name__ == "__main__":
    unittest.main()
