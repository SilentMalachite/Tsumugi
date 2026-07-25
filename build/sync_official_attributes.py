#!/usr/bin/env python3
"""機械抽出した項目表の属性区分を、運用 CSV 仕様へ同期する。

なぜ必要か
----------
共通編 1.3.2(1)③ は属性区分（英数／数値／コード値／漢字）ごとに使える文字種を定める
（英数は半角英小文字不可・漢字（2バイトコード）混在不可、漢字は半角英字・数字・カナ混在不可）。
これを `CsvCellEncoder` が強制するには、属性区分が**運用 spec の項目定義**に載っていなければならない。

属性区分は ADR 0037 の機械抽出（`*-item-tables.json`）が全 443 項目について持っているので、
人が 340 頁を読み直して書き写すのではなく、このスクリプトで注入する。注入結果は
`ItemTableCrossCheckTests.Every_official_attribute_matches_the_extraction` が完全一致で固定するため、
手で書き換えても、抽出をやり直して差分が出ても、必ずテストが落ちる。

使い方
------
    python3 build/sync_official_attributes.py            # 差分を書き込む
    python3 build/sync_official_attributes.py --check    # 差分があれば非ゼロ終了（書き込まない）

新しい施行分では、先に `extract_interface_item_tables.py` で抽出をやり直し、その差分を
レビューしてから本スクリプトを流す。
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SPEC_DIR = REPO_ROOT / "src" / "Tsumugi.Infrastructure.Csv" / "Specifications"

# 運用 spec と、その属性区分の出所になる抽出結果。
PAIRS = [
    ("common-r7-10.json", "common-r7-10-item-tables.json"),
    ("provider-claim-r7-10.json", "provider-r7-10-item-tables.json"),
]

ATTRIBUTE_KEY = "officialAttribute"

# 属性区分は「項目名の次・データ型の手前」に置く（公式表の 項番／項目名／属性／ﾊﾞｲﾄ数 の並びに合わせる）。
INSERT_BEFORE = "dataType"


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def dump(path: Path, document: dict) -> None:
    path.write_text(
        json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def attributes_by_field_id(extraction: dict) -> dict[str, str]:
    result: dict[str, str] = {}
    for record in extraction["records"]:
        for item in record["items"]:
            field_id = f"{record['recordId']}:{item['position']:03d}"
            result[field_id] = item["officialAttribute"]
    return result


def with_attribute(field: dict, attribute: str) -> dict:
    """`INSERT_BEFORE` の直前に属性区分を差し込んだ新しい項目定義を返す（キー順を保つ）。"""
    rebuilt: dict[str, object] = {}
    for key, value in field.items():
        if key == ATTRIBUTE_KEY:
            continue
        if key == INSERT_BEFORE:
            rebuilt[ATTRIBUTE_KEY] = attribute
        rebuilt[key] = value
    if ATTRIBUTE_KEY not in rebuilt:
        rebuilt[ATTRIBUTE_KEY] = attribute
    return rebuilt


def sync(spec_name: str, extraction_name: str, check_only: bool) -> tuple[int, int]:
    spec_path = SPEC_DIR / spec_name
    spec = load(spec_path)
    attributes = attributes_by_field_id(load(SPEC_DIR / extraction_name))

    changed = 0
    total = 0
    for record in spec["records"]:
        fields = record["fields"]
        for index, field in enumerate(fields):
            total += 1
            field_id = field["fieldId"]
            if field_id not in attributes:
                sys.exit(f"{spec_name}: 抽出結果に {field_id} が無い（抽出をやり直す）")
            attribute = attributes[field_id]
            if field.get(ATTRIBUTE_KEY) == attribute:
                continue
            changed += 1
            fields[index] = with_attribute(field, attribute)

    if changed and not check_only:
        dump(spec_path, spec)
    return changed, total


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="書き込まずに差分の有無だけ返す")
    args = parser.parse_args()

    dirty = 0
    for spec_name, extraction_name in PAIRS:
        changed, total = sync(spec_name, extraction_name, args.check)
        dirty += changed
        state = "差分" if changed else "一致"
        print(f"{spec_name}: {state} {changed}/{total} 項目")

    if args.check and dirty:
        print("属性区分が運用 spec と抽出結果で食い違っている", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
