"""`extract_record` の打ち切り規則の回帰テスト（stdlib unittest のみ）。

新しい施行分で末尾に項目が追加されたとき、旧 spec の末尾で打ち切ると追加分を一度も
見ないまま「旧 spec と同じ集合」を出力し、突合テストも旧同士の比較で通ってしまう。
その静かな見落としを防ぐ規則をここで固定する。
"""

from __future__ import annotations

import importlib.util
import io
import sys
import unittest
from contextlib import redirect_stderr
from pathlib import Path


MODULE_PATH = Path(__file__).parents[1] / "extract_interface_item_tables.py"


def load_module():
    spec = importlib.util.spec_from_file_location("extract_interface_item_tables", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(module)
    return module


class FakeDocument:
    """`document[index]` が頁番号を返すだけのスタブ（頁の中身は extract_page 差し替えで与える）。"""

    def __init__(self, page_count: int) -> None:
        self.page_count = page_count

    def __getitem__(self, index: int) -> int:
        return index + 1


class ExtractRecordTests(unittest.TestCase):
    def setUp(self) -> None:
        self.module = load_module()

    def stub_pages(self, pages: dict[int, list[int] | None]) -> None:
        def fake_extract_page(page, page_number, caption=None):
            positions = pages.get(page_number)
            if positions is None:
                return None
            return [{"position": position, "officialName": f"項目{position}"}
                    for position in positions]

        self.module.extract_page = fake_extract_page

    def test_reads_items_appended_beyond_the_previous_last_position(self) -> None:
        # 旧 spec は 3 項目まで。新しい PDF では 4〜5 が次頁へ送られている。
        self.stub_pages({1: [1, 2, 3], 2: [4, 5], 3: None})
        stderr = io.StringIO()

        with redirect_stderr(stderr):
            items, pages = self.module.extract_record(FakeDocument(3), 1, 3)

        self.assertEqual([item["position"] for item in items], [1, 2, 3, 4, 5])
        self.assertEqual(pages, [1, 2])
        self.assertIn("新設項目の可能性", stderr.getvalue())

    def test_stops_when_the_next_page_restarts_the_numbering(self) -> None:
        # 次のレコードは項番 1 から振り直される。別の表なので取り込まない。
        self.stub_pages({1: [1, 2, 3], 2: [1, 2]})

        items, pages = self.module.extract_record(FakeDocument(2), 1, 3)

        self.assertEqual([item["position"] for item in items], [1, 2, 3])
        self.assertEqual(pages, [1])

    def test_stops_when_the_next_page_is_not_a_strict_continuation(self) -> None:
        # 続き頁は「前頁の末尾 + 1」から始まる。飛んでいるなら別の表。
        self.stub_pages({1: [1, 2, 3], 2: [7, 8]})

        items, pages = self.module.extract_record(FakeDocument(2), 1, 3)

        self.assertEqual([item["position"] for item in items], [1, 2, 3])
        self.assertEqual(pages, [1])

    def test_fails_closed_when_the_extraction_falls_short_of_the_spec(self) -> None:
        # 罫線検出が崩れて途中までしか読めていない状態を、成果物にせず落とす。
        self.stub_pages({1: [1, 2], 2: None})

        with self.assertRaises(SystemExit) as raised:
            self.module.extract_record(FakeDocument(2), 1, 33)

        self.assertIn("項番 2 までしか読めていません", str(raised.exception))

    def test_reports_a_missing_table_on_the_start_page(self) -> None:
        self.stub_pages({1: None})

        with self.assertRaises(SystemExit) as raised:
            self.module.extract_record(FakeDocument(1), 1, 3)

        self.assertIn("項目表が見つかりません", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
