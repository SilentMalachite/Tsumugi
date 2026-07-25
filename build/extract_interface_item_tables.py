#!/usr/bin/env python3
"""インタフェース仕様書（PDF）の項目表を、罫線からセルを決めて機械抽出する。

目的
----
CSV 仕様 JSON（`provider-claim-r7-10.json` / `common-r7-10.json`）は人が PDF を読んで
書き起こしたもので、書き起こし誤りは「誰かがもう一度 PDF を読む」ときにしか見つからない。
実際に Phase 3-3 では単位（項目27 は 1/100 時間なのに分を出力）・日数2項目の定義混同・
地域区分コード・レコードの性質の誤りが後から発覚した。

本スクリプトは項目表を機械抽出して checked-in の抽出結果（`*-item-tables.json`）を作る。
突合は `CsvSpecificationExtractionCrossCheckTests` が行う。新しい施行分の PDF が出たら
「340 頁を読み直す」のではなく「抽出結果の差分をレビューする」運用にするための土台。

抽出方式
--------
対象の表は罫線で囲まれているため、ヒューリスティックな列推定をしない:

1. ページの描画命令から水平・垂直罫線を集める（`line` と極細 `rect` の両方）。
2. 水平罫線 → 行帯、垂直罫線 → 列帯。両者の交差でセルを決める。
3. セル内の単語（`page.get_text("words")` の矩形）を x 昇順に連結してセル文字列にする。
4. 「項番」列が整数のセルを持つ行帯を 1 項目として採る（※脚注や見出しは項番を持たない）。

項目名欄が「群ラベル｜個別名」に分かれる表（請求書・明細書の集計欄）では、群ラベルは
複数行にまたがり縦書きで書かれる。群ラベルの読み取りには文字落ちが残るため成果物には
載せず、**個別名だけ**を記録する。突合側は spec の `officialName`（群ラベル＋個別名の連結）に
個別名が含まれることを確認する。

列は表ヘッダ（項番 / 項目名 / 属性 / ﾊﾞｲﾄ数 / 説明 / 備考）の語の x 位置から決める。
ヘッダが無いページは対象表の続きではないと判断して打ち切る。

起点ページは**既存 spec の `sourcePage`** から採る（このスクリプトに制度実値を持たせない）。

使い方
------
    python3 build/extract_interface_item_tables.py \
        --pdf /path/to/001565561.pdf --document-id provider-r7-10 \
        --out src/Tsumugi.Infrastructure.Csv/Specifications/provider-r7-10-item-tables.json

PDF はリポジトリに含めない（`sources.json` の URL から取得する）。SHA-256 が
`sources.json` の登録値と一致しない PDF では fail-close する（登録済み文書に紐づかない
抽出結果を作らないため）。
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
import unicodedata
from pathlib import Path

try:
    import fitz  # PyMuPDF
except ImportError:  # pragma: no cover - 実行環境の問題であって仕様の問題ではない
    sys.exit("PyMuPDF が必要です: python3 -m pip install pymupdf")

REPO_ROOT = Path(__file__).resolve().parent.parent
SPEC_DIR = REPO_ROOT / "src" / "Tsumugi.Infrastructure.Csv" / "Specifications"
SOURCES = SPEC_DIR / "sources.json"

EXTRACTOR_VERSION = 1

# 表ヘッダの語 → 論理列名。「ﾊﾞｲﾄ」は半角カナで、次行に「数」が来る 2 行ヘッダ。
HEADER_TOKENS = {
    "項番": "position",
    "項目名": "officialName",
    "項目": "officialName",  # 共通編の表は「項目」、事業所編の表は「項目名」
    "属性": "officialAttribute",
    "ﾊﾞｲﾄ": "byteLength",
    "説明": "officialNote",
    "内容": "officialNote",  # 請求書・明細書の表は「内容」、就労継続支援の表は「説明」
    "備考": "remark",
}

# spec の記載順（レコード）ごとの対象文書。spec 側の sourceDocumentId と一致させる。
SPEC_FILES = {
    "provider-r7-10": "provider-claim-r7-10.json",
    "common-r7-10": "common-r7-10.json",
}


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def registered_sha256(document_id: str) -> tuple[str, int]:
    sources = json.loads(SOURCES.read_text(encoding="utf-8"))
    for source in sources["sources"]:
        if source["sourceDocumentId"] == document_id:
            return source["sha256"], source.get("pageCount", 0)
    raise SystemExit(f"sources.json に {document_id} が登録されていません。")


def rules_of(
    page,
) -> tuple[list[tuple[float, float, float]], list[float], list[tuple[float, float, float]]]:
    """水平罫線（y と x 範囲）・列区切りの垂直罫線 x・セル内仕切りの垂直罫線 x を返す。

    垂直罫線のうち表の上端から始まっていないものは列の区切りではなく<b>セル内部の仕切り</b>
    （項目名欄を「群ラベル｜個別名」に分ける縦線など）なので、列区切りからは外して別に返す。
    水平罫線は x 範囲も返す。群ラベルは複数行にまたがる（rowspan）ので、
    「その罫線が群ラベル列を横切っているか」で行帯を分ける必要がある。
    """
    horizontal: list[tuple[float, float, float]] = []
    vertical_spans: list[tuple[float, float]] = []
    for drawing in page.get_drawings():
        for item in drawing["items"]:
            if item[0] == "l":
                (x0, y0), (x1, y1) = item[1], item[2]
                if abs(y0 - y1) < 0.6 and abs(x1 - x0) > 20:
                    horizontal.append((y0, min(x0, x1), max(x0, x1)))
                elif abs(x0 - x1) < 0.6 and abs(y1 - y0) > 5:
                    vertical_spans.append((x0, min(y0, y1), max(y0, y1)))
            elif item[0] == "re":
                rect = item[1]
                if rect.height < 0.6 and rect.width > 20:
                    horizontal.append((rect.y0, rect.x0, rect.x1))
                elif rect.width < 0.6 and rect.height > 5:
                    vertical_spans.append((rect.x0, rect.y0, rect.y1))
    horizontal.sort()
    if not vertical_spans:
        return horizontal, [], []
    table_top = min(top for _x, top, _bottom in vertical_spans)
    vertical = merge_close([x for x, top, _bottom in vertical_spans if top - table_top <= 5])
    # セル内仕切りは頁の途中から始まる（群ラベル欄が現れる行から下だけ）ので y 範囲も保つ。
    inner = [span for span in vertical_spans if span[1] - table_top > 5]
    return horizontal, vertical, inner


# 表の外枠は二重罫線で描かれることがあるため、3pt 以内は 1 本に丸める
# （行の高さは 12pt 以上なので行帯を潰さない）。
def merge_close(values: list[float], tolerance: float = 3.0) -> list[float]:
    merged: list[float] = []
    for value in sorted(values):
        if merged and value - merged[-1] <= tolerance:
            continue
        merged.append(value)
    return merged


def header_columns(
    words, vertical: list[float], header_band: tuple[float, float]
) -> dict[str, tuple[float, float]] | None:
    """ヘッダ行のセル文字列から論理列 → x 範囲を決める。対象表でなければ None。

    ヘッダの語は縦に割れることがある（「項番」が「項」「番」の 2 語になる頁がある）ため、
    語単位ではなく<b>列ごとに結合した文字列</b>で判定する。
    """
    columns: dict[str, tuple[float, float]] = {}
    for left, right in zip(vertical, vertical[1:]):
        text = normalize(cell_text(words, (left, right), header_band))
        if not text:
            continue
        for token, name in HEADER_TOKENS.items():
            if name not in columns and MatchesHeaderToken(token, name, text):
                columns[name] = (left, right)
                break
    # 項番・項目名・説明（内容）が揃っていなければ対象表ではない。
    if not {"position", "officialName", "officialNote"} <= columns.keys():
        return None
    return columns


def has_header_token(words, vertical: list[float], band: tuple[float, float]) -> bool:
    """行帯のどこかに表ヘッダの見出し語があるか（ヘッダの開始帯かどうかの判定）。"""
    for left, right in zip(vertical, vertical[1:]):
        text = normalize(cell_text(words, (left, right), band))
        if any(token in text for token in HEADER_TOKENS):
            return True
    return False


def MatchesHeaderToken(token: str, name: str, text: str) -> bool:
    """ヘッダ列の判定。

    項番列だけは<b>完全一致</b>を要求する。表の境界をまたぐ広い band を試すと、次の表のヘッダを
    巻き込んで「１２項番」のような文字列になり、境界がヘッダだと誤検出される（1 頁に複数表がある頁で
    実際に起きた）。他の列は「属性（※Z）」のように注記が付くため含有判定のままにする。
    """
    return text == token if name == "position" else token in text


def cell_text(words, x_range: tuple[float, float], y_range: tuple[float, float]) -> str:
    left, right = x_range
    top, bottom = y_range
    inside = [
        word
        for word in words
        if top <= (word[1] + word[3]) / 2 <= bottom and left <= (word[0] + word[2]) / 2 <= right
    ]
    if not inside:
        return ""

    columns = cluster_by_x(inside)
    if len(columns) > 1 and all(len(word[4].strip()) == 1 for word in inside):
        # 縦書きセル（群ラベル欄）。右の列から順に、各列は上から下へ読む。
        # 横書きと同じ (y, x) 順で読むと列が交互に混ざる。
        columns.sort(key=lambda column: -min(word[0] for word in column))
        return "".join(
            "".join(word[4] for word in sorted(column, key=lambda word: word[1]))
            for column in columns
        )

    inside.sort(key=lambda word: ((word[1] + word[3]) / 2, word[0]))
    return "".join(word[4] for word in inside)


def cluster_by_x(words, tolerance: float = 3.0) -> list[list]:
    """語を x 位置で列にまとめる（縦書き判定と読み順のため）。"""
    clusters: list[list] = []
    for word in sorted(words, key=lambda word: word[0]):
        if clusters and word[0] - min(other[0] for other in clusters[-1]) <= tolerance:
            clusters[-1].append(word)
        else:
            clusters.append([word])
    return clusters


def caption_position(words, caption: str) -> float | None:
    """表の見出し（キャプション）の y 位置。

    見出しの<b>全文</b>を含む語だけを採る。部分一致を許すと、節見出し（例「レコードフォーマット」）に
    引っかかって別の表を選んでしまう（実際に 3 表すべてが最初の表になる誤りが出た）。
    行内で語が分割されている場合に備え、同一行の語を連結してからも判定する。
    """
    target = normalize(caption)
    for _x0, y0, _x1, _y1, text, *_ in words:
        if target in normalize(text):
            return y0

    by_line: dict[float, list[tuple[float, str]]] = {}
    for x0, y0, _x1, _y1, text, *_ in words:
        by_line.setdefault(round(y0, 1), []).append((x0, text))
    for y0, line in sorted(by_line.items()):
        joined = normalize("".join(text for _x, text in sorted(line)))
        if target in joined:
            return y0
    return None


def normalize(text: str) -> str:
    """空白だけを落とす（文字幅は公式表記のまま保つ）。

    項目名や説明文の全角・半角は公式表記そのものなので変換しない（例: 「１割相当額」は全角）。
    幅を寄せてしまうと、突合が「書き起こしの忠実さ」を検証できなくなる。
    数値として読む列（項番・桁数）だけ NFKC で正規化する。
    """
    return "".join(character for character in text if not character.isspace())


def to_int(text: str) -> int | None:
    """項番・桁数の数値化。共通編の項番は全角数字なので NFKC で寄せてから判定する。"""
    digits = unicodedata.normalize("NFKC", text)
    return int(digits) if digits.isdigit() else None


def parse_position(text: str) -> int | None:
    return to_int(normalize(text))


def parse_byte_length(text: str) -> int | None:
    return to_int(normalize(text))


def extract_page(page, page_number: int, caption: str | None = None) -> list[dict] | None:
    """1 ページから項目行を抽出する。対象表でなければ None。

    <b>1 頁に複数の表が並ぶ頁</b>（共通編のレコードフォーマットはコントロール／データ／エンドの
    3 表が同じ頁にある）では、`caption` に表の見出し（例「・コントロールレコードフォーマット」）を
    渡して対象表を選ぶ。見出しの直下にあるヘッダ行から、次のヘッダ行の手前までを 1 表として読む。
    `caption` を渡さない頁は従来どおり「最初のヘッダ行から頁末まで」を 1 表として読む。

    項目名欄が「群ラベル｜個別名」に分かれている表（請求書・明細書の集計欄など）では、
    群ラベルが複数行にまたがる。spec の `officialName` は「群ラベル＋個別名」の連結なので、
    群ラベルの行帯を別に求めてから連結する。
    """
    words = page.get_text("words")
    horizontal_rules, vertical, inner = rules_of(page)
    horizontal = merge_close([y for y, _x0, _x1 in horizontal_rules])
    if len(horizontal) < 2 or len(vertical) < 3:
        return None

    # 頁内のヘッダ行を<b>全部</b>見つける（1 頁に複数表がある頁のため）。
    # ヘッダ行は 1 行帯で収まる表（就労継続支援の項目表）と 2〜3 行帯に割れる表
    # （請求書・明細書の項目表。属性/ﾊﾞｲﾄ数の見出しが上下に分かれる）がある。
    headers: list[tuple[int, int, dict[str, tuple[float, float]]]] = []
    scan = 0
    while scan < len(horizontal) - 1:
        found = None
        # ヘッダ候補は「先頭の行帯に見出し語がある」ことを要求する。表と表の間の空き帯を
        # 起点にすると、次の表のヘッダを巻き込んで<b>ヘッダ位置が 1 帯早くずれる</b>
        # （キャプションで表を選べなくなる。1 頁 3 表の頁で実際に起きた）。
        if has_header_token(words, vertical, (horizontal[scan], horizontal[scan + 1])):
            for span in (1, 2, 3):
                if scan + span >= len(horizontal):
                    break
                candidate = header_columns(
                    words, vertical, (horizontal[scan], horizontal[scan + span]))
                if candidate is not None:
                    found = (scan, scan + span, candidate)
                    break
        if found is None:
            scan += 1
            continue
        headers.append(found)
        scan = found[1]

    if not headers:
        return None

    if caption is None:
        _start, header_end, columns = headers[0]
        row_end = len(horizontal) - 1
    else:
        caption_y = caption_position(words, caption)
        if caption_y is None:
            return None
        below = [entry for entry in headers if horizontal[entry[0]] > caption_y]
        if not below:
            return None
        start, header_end, columns = below[0]
        following = [entry[0] for entry in headers if entry[0] > start]
        row_end = following[0] if following else len(horizontal) - 1

    band_cache: dict[tuple[float, float], list[tuple[float, float]]] = {}

    items: list[dict] = []
    for top, bottom in zip(horizontal[header_end:row_end], horizontal[header_end + 1:row_end + 1]):
        if bottom - top < 4:
            continue
        band = (top, bottom)
        position = parse_position(cell_text(words, columns["position"], band))
        if position is None:
            continue
        middle = (top + bottom) / 2
        group_ranges, name_range = split_name_column(columns["officialName"], inner, middle)
        name = normalize(cell_text(words, name_range, band))
        labels = []
        for group_range in group_ranges:
            if group_range not in band_cache:
                band_cache[group_range] = band_index(horizontal_rules, group_range)
            for group_top, group_bottom in band_cache[group_range]:
                if group_top <= middle <= group_bottom:
                    labels.append(
                        normalize(cell_text(words, group_range, (group_top, group_bottom)))
                    )
                    break
        group = "".join(labels)
        items.append(
            {
                "position": position,
                # 項目単位の出典頁。generatorRule の source=doc:pNN:itemNN アンカーと突合する。
                "sourcePage": page_number,
                # 群ラベル（group）は縦書きセルの読み取りに欠けが残るため成果物へ載せない。
                # 突合は「個別名が spec の officialName に含まれるか」で行う（下記 docstring 参照）。
                "officialName": name,
                "officialAttribute": normalize(
                    cell_text(words, columns["officialAttribute"], band)
                )
                if "officialAttribute" in columns
                else "",
                "byteLength": parse_byte_length(cell_text(words, columns["byteLength"], band))
                if "byteLength" in columns
                else None,
                "officialNote": normalize(cell_text(words, columns["officialNote"], band)),
                "remark": normalize(cell_text(words, columns["remark"], band))
                if "remark" in columns
                else "",
            }
        )
    return items


def split_name_column(
    name_column: tuple[float, float],
    inner: list[tuple[float, float, float]],
    row_middle: float,
) -> tuple[list[tuple[float, float]], tuple[float, float]]:
    """項目名欄を「群ラベル列（複数段ありうる）」と「個別名列」に分ける。

    仕切り線は表の途中から始まる（群ラベルが現れる行から下だけ）ため、**行ごとに**
    その行を横切っている仕切りだけを使う。頁全体に一律で適用すると、仕切りより上の行の
    項目名が群ラベル欄の側に入って空になる。

    請求書・明細書の集計欄は群ラベルが入れ子になる（例: 「請求額集計欄合計」→「A型減免」→
    「事業者減免額」）ので、仕切りは 1 本に限らない。
    """
    left, right = name_column
    dividers = sorted(
        x
        for x, top, bottom in inner
        if left + 2 < x < right - 2 and top <= row_middle <= bottom
    )
    if not dividers:
        return [], name_column
    bounds = [left, *dividers, right]
    groups = [(bounds[index], bounds[index + 1]) for index in range(len(bounds) - 2)]
    return groups, (bounds[-2], bounds[-1])


def band_index(
    horizontal_rules: list[tuple[float, float, float]], x_range: tuple[float, float]
) -> list[tuple[float, float]]:
    """指定した x 範囲を横切る水平罫線だけで行帯を作る（rowspan セル用）。"""
    left, right = x_range
    middle = (left + right) / 2
    crossing = merge_close([y for y, x0, x1 in horizontal_rules if x0 <= middle <= x1])
    return list(zip(crossing, crossing[1:]))


def extract_record(
    document,
    start_page: int,
    expected_last_position: int,
    caption: str | None = None,
) -> tuple[list[dict], list[int]]:
    """起点ページから項番が昇順に続く限り読み進める。

    `caption` を渡すと、起点ページ内で見出しに対応する表だけを読む（1 頁複数表の頁）。
    """
    items: dict[int, dict] = {}
    pages: list[int] = []
    highest = 0
    for page_number in range(start_page, document.page_count + 1):
        page_items = extract_page(
            document[page_number - 1], page_number, caption if page_number == start_page else None)
        if page_items is None:
            if page_number == start_page:
                raise SystemExit(f"物理 {start_page} 頁に項目表が見つかりません。")
            break
        # 表が次のレコードへ移ると項番が振り直される。昇順が崩れたら打ち切る。
        if page_items and page_items[0]["position"] <= highest:
            break
        fresh = [item for item in page_items if item["position"] > highest]
        if not fresh:
            break
        for item in fresh:
            items[item["position"]] = item
        highest = fresh[-1]["position"]
        pages.append(page_number)
        if highest >= expected_last_position:
            break
    return [items[key] for key in sorted(items)], pages


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pdf", required=True, type=Path)
    parser.add_argument("--document-id", required=True, choices=sorted(SPEC_FILES))
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()

    expected_sha, _ = registered_sha256(args.document_id)
    actual_sha = sha256_of(args.pdf)
    if actual_sha != expected_sha:
        raise SystemExit(
            "PDF の SHA-256 が sources.json の登録値と一致しません。\n"
            f"  registered: {expected_sha}\n  actual:     {actual_sha}\n"
            "登録済み文書に紐づかない抽出結果は作りません。"
        )

    spec = json.loads((SPEC_DIR / SPEC_FILES[args.document_id]).read_text(encoding="utf-8"))
    document = fitz.open(args.pdf)

    records = []
    for record in spec["records"]:
        if record.get("sourceDocumentId") != args.document_id:
            continue
        last_position = record["fields"][-1]["position"]
        items, pages = extract_record(
            document, record["sourcePage"], last_position, record.get("sourceTableCaption"))
        records.append(
            {
                "recordId": record["recordId"],
                "sourcePages": pages,
                "items": items,
            }
        )
        print(
            f"{record['recordId']}: {len(items)} items from pages {pages}"
            f" (spec declares {len(record['fields'])})",
            file=sys.stderr,
        )

    payload = {
        "schemaVersion": 1,
        "extractorVersion": EXTRACTOR_VERSION,
        "sourceDocumentId": args.document_id,
        "sourceSha256": actual_sha,
        "records": records,
    }
    args.out.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"wrote {args.out}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
