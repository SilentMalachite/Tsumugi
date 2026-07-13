from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


MODULE_PATH = Path(__file__).parents[1] / "phase3_task13_manifest_v2.py"
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
    documents = [{"documentId": f"document-{index:02d}"} for index in range(41)]
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


def write_decisions(path: Path, decisions: list[dict]) -> None:
    path.write_text(
        "".join(json.dumps(decision, ensure_ascii=False) + "\n" for decision in decisions),
        encoding="utf-8",
    )


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

    def test_write_chunks_rejects_nonempty_output_directory(self) -> None:
        manifest = {"schemaVersion": "2", "documents": [], "rows": [make_v2_row(0)]}
        with tempfile.TemporaryDirectory() as temporary:
            output_dir = Path(temporary)
            (output_dir / "existing.txt").write_text("occupied", encoding="utf-8")
            with self.assertRaises(ValueError):
                manifest_v2.write_chunks(manifest, output_dir, 200)

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


if __name__ == "__main__":
    unittest.main()
