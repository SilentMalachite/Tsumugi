# Phase 3-1 Task 13 Claim Master Seeds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 令和6年・令和8年の就労継続支援B型制度実値を、再取得可能な一次資料、SHA-256、source-side row inventory、適用期間、独立全件照合付きで6つのclaim master seedへ投入する。

**Architecture:** 監査フェーズ（資料集合固定 → 未加工バイト取得 → SHA検証 → source-side manifest → スキーマ適合判定）を先行し、`schema-gap = 0`の場合だけseedフェーズへ進む。seedは独立マスタ → service code → 基本報酬 → 割合加減算の依存順で投入し、最終candidate commitを転記担当とは別のreviewerが公式側の全対象rowから照合する。

**Tech Stack:** .NET 10、C# 14、xUnit、FluentAssertions、System.Text.Json、JSON Schema、`curl`、`jq`、`shasum`、Poppler (`pdfinfo` / `pdftotext`)、Python 3 + openpyxl 3.1.5

---

## 実行契約

- 設計正本: `docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md`
- 上位計画: `docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md` Task 13
- 出典正本: `docs/decisions/0020-claim-master-sources-and-versioning.md`、`0022-burden-cap-master.md`、`0023-average-wage-and-r8-transition.md`、`0025-claim-rounding-rules.md`
- 各コード／テスト変更は`@superpowers:test-driven-development`でRed → Green → Refactorを守る。
- Task 9完了前とTask 10の最終証跡前に`@superpowers:verification-before-completion`を使う。
- Task 10で`@superpowers:requesting-code-review`を使い、manifest、6 seed、テスト、証跡をレビューする。
- 公式資料の原本は`/tmp/tsumugi-phase31-task13/sources/`だけに置き、gitへ追加しない。
- 公式値をC#へ直書きしない。production seedの全値をテストfixtureへ複製しない。
- SHA不一致、locator到達不能、source矛盾又は`schema-gap`が1件でもあれば、seedフェーズ（Task 4以降）を実行しない。
- `docs/open-questions.md`は一次資料から一意に解消できない事項が生じた場合だけ変更する。

## ファイル構成

### Create

- `docs/spec-data/phase3/claim-master-source-row-manifest.json` — Task 13対象document、機械集計可能な抽出範囲、公式側の採用・除外・schema-gap全row。
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs` — manifest契約、source catalog整合、seed completeness、版境界、参照整合を検証する唯一のTask 13 test class。
- `docs/phase3-1-master-transcription-review.md` — SHA、スキーマ監査、candidate commit、独立全件照合、CIの最終証跡。

### Modify

- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json` — 4つの制度上限額。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json` — 地域区分別のB型単位単価。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json` — 5 releaseのoption閉集合とR8経過措置。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json` — R6／R8 B型のservice codeとselector。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json` — payment band、人員配置、定員、service code、単位数。
- `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json` — 現行スキーマへ適合する割合加減算だけ。

### Conditional only — Task 13へ混ぜない

スキーマ適合監査が失敗した場合、次の変更はTask 12再設計の別spec／別planへ送る。Task 13実行者は変更しない。

- `src/Tsumugi.Domain/Logic/Claim/Models/ClaimCalculationMasters.cs`
- `src/Tsumugi.Infrastructure/ClaimMasters/Schema/claim-master-file.schema.json`
- `src/Tsumugi.Infrastructure/ClaimMasters/ClaimMasterFileValidator.cs`
- `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSchemaPhase31Tests.cs`

---

## 監査フェーズ

### Task 1: source-side manifestの閉じた契約と資料集合を固定する

**Files:**
- Create: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Create: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Reference: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`
- Reference: `docs/decisions/0020-claim-master-sources-and-versioning.md`

- [ ] **Step 1: manifest未作成で失敗するtestを書く**

`ClaimMasterSeedPhase31Tests.cs`を作成し、solution rootからmanifestを開くhelperと最初のtestを置く。

```csharp
using System.Text.Json;
using FluentAssertions;
using Tsumugi.Infrastructure.Tests;

namespace Tsumugi.Infrastructure.Tests.ClaimMasters;

public sealed class ClaimMasterSeedPhase31Tests
{
    private const string ManifestPath =
        "docs/spec-data/phase3/claim-master-source-row-manifest.json";

    [Fact]
    public void Source_manifest_exists_and_has_a_closed_root_contract()
    {
        using var manifest = OpenRepositoryJson(ManifestPath);
        var root = manifest.RootElement;

        root.EnumerateObject().Select(property => property.Name)
            .Should().Equal("schemaVersion", "documents", "rows");
        root.GetProperty("schemaVersion").GetString().Should().Be("1");
        root.GetProperty("documents").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("rows").ValueKind.Should().Be(JsonValueKind.Array);
    }

    private static JsonDocument OpenRepositoryJson(string relativePath)
    {
        var fullPath = Path.Combine(
            TsumugiAssemblyLocator.FindSolutionRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return JsonDocument.Parse(File.ReadAllText(fullPath));
    }
}
```

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest_exists \
  -v normal
```

Expected: FAIL with `FileNotFoundException` for `claim-master-source-row-manifest.json`。

- [ ] **Step 3: Task 13のdocument ID閉集合をmanifestへ追加する**

manifestの`documents`には次だけを登録する。`authoritative`は直接転記元、`cross-check`は訂正・意味・適用条件の確認資料である。

**Authoritative**

```text
mhlw-unit-price-notice-observed-946c3d96
r6-disability-support-guide-202404
r6-capability-202404
r6-capability-202406
r6-service-codes-2-xlsx
r8-capability-202606
r8-service-codes-2-xlsx
```

**Cross-check**

```text
r6-revision-overview
r6-fee-notice
r6-calculation-note
r6-employment-guidance-r6
r6-employment-guidance
r6-employment-guidance-corr-4
r6-qa-v1
r6-qa-v2
r6-qa-v3
r6-qa-v5
r6-qa-v6
r6-qa-v7
r6-qa-v8
r6-calculation-corr-1
r6-calculation-corr-3
r6-calculation-corr-5
r6-calculation-corr-6
r6-calculation-corr-7
r6-calculation-corr-8
r6-qa-corr-1
r6-qa-corr-2
r6-qa-corr-3
r6-reward-structure
r6-service-codes-2-pdf
r8-revision-overview
r8-fee-notice
r8-calculation-note
r8-b-reward-band-guide
r8-employment-transition-guide
r8-qa-v1
r8-amendment-qa
r8-capability-correction
r8-reward-structure
r8-service-codes-2-pdf
```

各documentへ`sourceSha256`と`role`を置き、`extractionRanges`を次の閉じた形にする。最初はADRに明記済みのrangeだけを追加し、`rows`は空配列でよい。

```json
{
  "schemaVersion": "1",
  "documents": [
    {
      "documentId": "r8-service-codes-2-xlsx",
      "sourceSha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
      "role": "authoritative",
      "extractionRanges": [
        {
          "rangeId": "r8-b-basic",
          "kind": "xlsx-rows",
          "workbookOrder": 38,
          "rowFrom": 7,
          "rowTo": 1993,
          "expectedItemCount": 1987
        }
      ]
    }
  ],
  "rows": []
}
```

`xlsx-rows`は`workbookOrder / rowFrom / rowTo / expectedItemCount`、`pdf-pages`は`pageFrom / pageTo / expectedItemCount`、`html-page`は`pageNo / expectedItemCount = 1`を持つ。R6/R8 service code workbookはworkbook順38〜41を全て登録し、基本報酬範囲より後ろの行を省略しない。各sheetの最終行はTask 3で取得バイトから確定する。

- [ ] **Step 4: manifest documentがcatalogとreleaseに存在するtestを書く**

同じtest classへ、次を検査するtestを追加する。

```csharp
[Fact]
public void Source_manifest_documents_match_the_catalog_and_release_bundles()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    using var catalog = OpenRepositoryJson(
        "src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json");

    var catalogSources = catalog.RootElement.GetProperty("sources")
        .EnumerateArray()
        .ToDictionary(
            source => source.GetProperty("documentId").GetString()!,
            StringComparer.Ordinal);
    var releasedIds = catalog.RootElement.GetProperty("releases")
        .EnumerateArray()
        .SelectMany(release => release.GetProperty("sourceDocumentIds").EnumerateArray())
        .Select(item => item.GetString()!)
        .ToHashSet(StringComparer.Ordinal);

    foreach (var document in manifest.RootElement.GetProperty("documents").EnumerateArray())
    {
        var id = document.GetProperty("documentId").GetString()!;
        catalogSources.Should().ContainKey(id);
        releasedIds.Should().Contain(id);
        document.GetProperty("sourceSha256").GetString().Should()
            .Be(catalogSources[id].GetProperty("sha256").GetString());
        document.GetProperty("role").GetString().Should()
            .BeOneOf("authoritative", "cross-check");
    }
}
```

- [ ] **Step 5: manifest契約testを通す**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest \
  -v normal
```

Expected: PASS。

- [ ] **Step 6: manifest契約をcommitする**

```bash
git add docs/spec-data/phase3/claim-master-source-row-manifest.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "test(phase3-1/AC3-1): define task 13 source inventory"
```

---

### Task 2: 公式資料を再取得して未加工バイトのSHAを検証する

**Files:**
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json`
- Temporary only: `/tmp/tsumugi-phase31-task13/sources/`

- [ ] **Step 1: 取得ツールを確認する**

Run:

```bash
command -v curl jq shasum pdftotext pdfinfo
python3 -c 'import openpyxl; print(openpyxl.__version__)'
```

Expected: all commands resolve; openpyxl prints `3.1.5` or a compatible installed 3.x version。欠落があれば取得前に停止し、環境を報告する。

- [ ] **Step 2: 一時ディレクトリを作る**

Run:

```bash
TASK13_SOURCE_DIR=/tmp/tsumugi-phase31-task13/sources
TASK13_RECEIPT=/tmp/tsumugi-phase31-task13/acquisition.jsonl
test ! -e "$TASK13_SOURCE_DIR" || {
  echo "Task 13 source directory already exists: $TASK13_SOURCE_DIR" >&2
  exit 1
}
test ! -e "$TASK13_RECEIPT" || {
  echo "Task 13 receipt already exists: $TASK13_RECEIPT" >&2
  exit 1
}
mkdir -p "$TASK13_SOURCE_DIR"
```

Expected: directory exists outside the repository。

- [ ] **Step 3: manifest documentだけをcatalog URLから取得する**

Run from repository root:

```bash
TASK13_SOURCE_DIR=/tmp/tsumugi-phase31-task13/sources
TASK13_RECEIPT=/tmp/tsumugi-phase31-task13/acquisition.jsonl

while IFS=$'\t' read -r document_id url expected_sha; do
  case "${url%%\?*}" in
    *.pdf) suffix=.pdf ;;
    *.xlsx) suffix=.xlsx ;;
    *.xls) suffix=.xls ;;
    *) suffix=.html ;;
  esac

  curl --fail --location --retry 3 --retry-all-errors \
    --user-agent 'Tsumugi-ClaimMaster-Audit/1.0' \
    --output "$TASK13_SOURCE_DIR/$document_id$suffix" \
    "$url"

  actual_sha=$(shasum -a 256 "$TASK13_SOURCE_DIR/$document_id$suffix" | awk '{print $1}')
  test "$actual_sha" = "$expected_sha" || {
    echo "SHA mismatch: $document_id expected=$expected_sha actual=$actual_sha" >&2
    exit 1
  }

  bytes=$(wc -c < "$TASK13_SOURCE_DIR/$document_id$suffix" | tr -d ' ')
  retrieved_at=$(date -u '+%Y-%m-%dT%H:%M:%SZ')
  jq -cn \
    --arg documentId "$document_id" \
    --arg url "$url" \
    --arg expectedSha256 "$expected_sha" \
    --arg actualSha256 "$actual_sha" \
    --argjson bytes "$bytes" \
    --arg retrievedAt "$retrieved_at" \
    '{
      documentId: $documentId,
      url: $url,
      expectedSha256: $expectedSha256,
      actualSha256: $actualSha256,
      bytes: $bytes,
      retrievedAt: $retrievedAt,
      result: "PASS"
    }' >> "$TASK13_RECEIPT"
done < <(
  jq -r --slurpfile manifest docs/spec-data/phase3/claim-master-source-row-manifest.json '
    ($manifest[0].documents | map(.documentId)) as $ids
    | .sources[]
    | select(.documentId as $id | $ids | index($id))
    | [.documentId, .url, .sha256]
    | @tsv
  ' src/Tsumugi.Infrastructure/ClaimMasters/Seed/sources.json
)
```

Expected: exit 0; one file per manifest document; no SHA mismatch。HTTP 404、空ファイル、HTML error body又はSHA不一致は停止条件であり、catalogを更新しない。

- [ ] **Step 4: 取得率とSHA一致率を再集計する**

Run:

```bash
expected=$(jq '.documents | length' docs/spec-data/phase3/claim-master-source-row-manifest.json)
actual=$(rg --files /tmp/tsumugi-phase31-task13/sources | wc -l | tr -d ' ')
receipt_count=$(jq -s 'length' /tmp/tsumugi-phase31-task13/acquisition.jsonl)
test "$actual" = "$expected"
test "$receipt_count" = "$expected"
jq -e -s 'all(.;
  .result == "PASS"
  and .expectedSha256 == .actualSha256
  and .bytes > 0
  and (.retrievedAt | test("^[0-9]{4}-[0-9]{2}-[0-9]{2}T")))' \
  /tmp/tsumugi-phase31-task13/acquisition.jsonl
```

Expected: exit 0、`actual == expected == receipt_count`、全receiptがPASS。Task 10は取得日時を記憶から再構成せず、このJSONLを証跡正本にする。

- [ ] **Step 5: gitへ原本が混入していないことを確認する**

Run: `git status --short`

Expected: `/tmp`原本が表示されない。Task 2はrepository変更がないためcommitしない。

---

### Task 3: 公式側row inventoryを完成しスキーマ適合監査を実行する

**Files:**
- Modify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Read: `/tmp/tsumugi-phase31-task13/sources/*`
- Reference: `docs/decisions/0020-claim-master-sources-and-versioning.md`
- Reference: `docs/decisions/0022-burden-cap-master.md`
- Reference: `docs/decisions/0023-average-wage-and-r8-transition.md`
- Reference: `docs/decisions/0025-claim-rounding-rules.md`

- [ ] **Step 1: manifest range検証の失敗testを書く**

次を検査するtestを追加する。現時点では`rows`が空なのでRedになる。

```csharp
[Fact]
public void Source_manifest_ranges_are_machine_countable_and_fully_inventoried()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var root = manifest.RootElement;
    var rows = root.GetProperty("rows").EnumerateArray().ToArray();
    rows.Should().NotBeEmpty();

    var uniqueRowIds = rows.Select(row => (
        DocumentId: row.GetProperty("sourceDocumentId").GetString(),
        Locator: row.GetProperty("sourceLocator").GetString())).ToArray();
    uniqueRowIds.Should().OnlyHaveUniqueItems();

    foreach (var document in root.GetProperty("documents").EnumerateArray())
    {
        var documentId = document.GetProperty("documentId").GetString();
        foreach (var range in document.GetProperty("extractionRanges").EnumerateArray())
        {
            var rangeId = range.GetProperty("rangeId").GetString();
            var expected = range.GetProperty("expectedItemCount").GetInt32();
            rows.Count(row =>
                row.GetProperty("sourceDocumentId").GetString() == documentId
                && row.GetProperty("rangeId").GetString() == rangeId)
                .Should().Be(expected);
        }
    }
}
```

- [ ] **Step 2: range testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest_ranges \
  -v normal
```

Expected: FAIL because `rows` is empty or a range count is incomplete。

- [ ] **Step 3: XLSXの全対象sheetと実データ最終行を確認する**

Run for both service-code workbooks:

```bash
python3 - <<'PY'
from pathlib import Path
from openpyxl import load_workbook

root = Path('/tmp/tsumugi-phase31-task13/sources')
for path in sorted(root.glob('*service-codes-2-xlsx*.xlsx')):
    wb = load_workbook(path, read_only=True, data_only=True)
    print(path.name)
    for order in (38, 39, 40, 41):
        ws = wb.worksheets[order - 1]
        print(order, ws.title, ws.max_row, ws.max_column)
PY
```

Expected: workbook順38〜41のsheet名と`max_row`が表示される。`rowFrom = 7`、`rowTo = max_row`、`expectedItemCount = rowTo - rowFrom + 1`としてmanifest rangeを固定する。空飾り行もsource-side inventoryへ`excluded`で記録し、黙って省略しない。

- [ ] **Step 4: PDF／HTML locatorを到達確認する**

PDFはmanifestの構造化rangeから物理頁を読み、次を実行する。

```bash
TASK13_SOURCE_DIR=/tmp/tsumugi-phase31-task13/sources

while IFS=$'\t' read -r document_id range_id page_from page_to; do
  pdfinfo "$TASK13_SOURCE_DIR/$document_id.pdf" >/dev/null
  echo "document=$document_id range=$range_id pages=$page_from-$page_to"
  pdftotext -f "$page_from" -l "$page_to" -layout \
    "$TASK13_SOURCE_DIR/$document_id.pdf" -
done < <(
  jq -r '
    .documents[] as $document
    | $document.extractionRanges[]
    | select(.kind == "pdf-pages")
    | [$document.documentId, .rangeId, .pageFrom, .pageTo]
    | @tsv
  ' docs/spec-data/phase3/claim-master-source-row-manifest.json
)
```

HTMLは次で`pageNo=1`とB型を含む単価表の存在を確認する。

```bash
rg -n '就労継続支援B型|一単位の単価|pageNo=1' \
  /tmp/tsumugi-phase31-task13/sources/mhlw-unit-price-notice-observed-946c3d96.html
```

Expected: ADR 0020が指定するB型と単価表の本文が見つかる。出力本文、sheet名、row位置がADR locatorと一致しなければ停止する。

- [ ] **Step 5: manifest rowsをsource側から全件列挙する**

各rowへ次の閉じた形を使用する。

```json
{
  "sourceDocumentId": "r8-service-codes-2-xlsx",
  "rangeId": "r8-b-basic",
  "sourceLocator": "workbook-order=38;row=7",
  "sourceLabel": "公式行の表示名",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "disposition": "seed",
  "masterKind": "basic-rewards",
  "seedKey": "安定key",
  "aggregationId": null,
  "aggregationKind": null,
  "aggregationReason": null,
  "exclusionReason": null
}
```

`disposition`の決定規則:

- `seed`: 現行6型へ損失なく一意に写像できる。
- `excluded`: A型専用、別service、見出し、空飾り行、同一値のcross-check掲載など。具体的`exclusionReason`が必須。
- `schema-gap`: 固定単位、合成済み単位又は別の演算意味を現行6型へ偽装しなければ保存できない。

複数source rowを1 production rowへ集約する場合は、同じ`masterKind + seedKey`の全rowへ同一`aggregationId`、`aggregationKind = multi-source-one-seed`、具体的な`aggregationReason`を設定する。1対1対応では3 fieldをnullにする。

manifestは値の期待fixtureにしない。公式の値そのものはproduction seedへだけ転記し、manifestはsource位置、分類、seed key、採否理由を保持する。

- [ ] **Step 6: dispositionの閉集合と必須組合せtestを書く**

```csharp
[Fact]
public void Source_manifest_rows_have_closed_and_consistent_dispositions()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    foreach (var row in manifest.RootElement.GetProperty("rows").EnumerateArray())
    {
        var disposition = row.GetProperty("disposition").GetString();
        disposition.Should().BeOneOf("seed", "excluded", "schema-gap");

        var masterKind = row.GetProperty("masterKind");
        var seedKey = row.GetProperty("seedKey");
        var aggregationId = row.GetProperty("aggregationId");
        var aggregationKind = row.GetProperty("aggregationKind");
        var aggregationReason = row.GetProperty("aggregationReason");
        var reason = row.GetProperty("exclusionReason");
        if (disposition == "seed")
        {
            masterKind.ValueKind.Should().Be(JsonValueKind.String);
            seedKey.ValueKind.Should().Be(JsonValueKind.String);
            reason.ValueKind.Should().Be(JsonValueKind.Null);
            if (aggregationId.ValueKind == JsonValueKind.Null)
            {
                aggregationKind.ValueKind.Should().Be(JsonValueKind.Null);
                aggregationReason.ValueKind.Should().Be(JsonValueKind.Null);
            }
            else
            {
                aggregationId.GetString().Should().NotBeNullOrWhiteSpace();
                aggregationKind.GetString().Should().Be("multi-source-one-seed");
                aggregationReason.GetString().Should().NotBeNullOrWhiteSpace();
            }
        }
        else
        {
            reason.GetString().Should().NotBeNullOrWhiteSpace();
        }
    }
}
```

- [ ] **Step 7: 全rowのrange所属と物理locator到達率を検証する**

まずC# testへ次を追加し、各rowのdocument／range所属とlocator位置を閉じる。

```csharp
[Fact]
public void Source_manifest_row_locators_belong_to_the_declared_document_ranges()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var documents = manifest.RootElement.GetProperty("documents").EnumerateArray()
        .ToDictionary(
            document => document.GetProperty("documentId").GetString()!,
            StringComparer.Ordinal);

    foreach (var row in manifest.RootElement.GetProperty("rows").EnumerateArray())
    {
        var documentId = row.GetProperty("sourceDocumentId").GetString()!;
        documents.Should().ContainKey(documentId);
        var rangeId = row.GetProperty("rangeId").GetString();
        documents[documentId].GetProperty("extractionRanges").EnumerateArray()
            .Count(range => range.GetProperty("rangeId").GetString() == rangeId)
            .Should().Be(1);
        row.GetProperty("sourceLocator").GetString()
            .Should().NotBeNullOrWhiteSpace();
    }
}
```

次にSHA検証済み原本に対して物理到達を確認する。これはrepository testではなく、Task 3の必須実行ゲートである。

```bash
python3 - <<'PY'
import json
import re
import subprocess
from pathlib import Path
from openpyxl import load_workbook

repo = Path.cwd()
source_dir = Path('/tmp/tsumugi-phase31-task13/sources')
manifest = json.loads((repo / 'docs/spec-data/phase3/claim-master-source-row-manifest.json').read_text())

documents = {document['documentId']: document for document in manifest['documents']}
ranges = {
    (document['documentId'], item['rangeId']): item
    for document in manifest['documents']
    for item in document['extractionRanges']
}
workbooks = {}
pdf_page_counts = {}
failures = []

def source_path(document_id: str) -> Path:
    matches = sorted(source_dir.glob(document_id + '.*'))
    if len(matches) != 1:
        raise RuntimeError(f'{document_id}: expected one source file, found {len(matches)}')
    return matches[0]

for row in manifest['rows']:
    document_id = row['sourceDocumentId']
    range_id = row['rangeId']
    locator = row['sourceLocator']
    item = ranges.get((document_id, range_id))
    if item is None:
        failures.append(f'{document_id}/{range_id}: undeclared range')
        continue

    path = source_path(document_id)
    kind = item['kind']
    if kind == 'xlsx-rows':
        match = re.fullmatch(r'workbook-order=(\d+);row=(\d+)', locator)
        if not match:
            failures.append(f'{document_id}: invalid xlsx locator {locator}')
            continue
        workbook_order, row_number = map(int, match.groups())
        if workbook_order != item['workbookOrder'] or not item['rowFrom'] <= row_number <= item['rowTo']:
            failures.append(f'{document_id}: locator outside range {locator}')
            continue
        if document_id not in workbooks:
            workbooks[document_id] = load_workbook(path, read_only=True, data_only=True)
        workbook = workbooks[document_id]
        sheet = workbook.worksheets[workbook_order - 1]
        if row_number > sheet.max_row:
            failures.append(f'{document_id}: row does not exist {locator}')
    elif kind == 'pdf-pages':
        match = re.match(r'pdf:physical-page=(\d+)(?:;.*)?$', locator)
        if not match:
            failures.append(f'{document_id}: invalid pdf locator {locator}')
            continue
        page = int(match.group(1))
        if not item['pageFrom'] <= page <= item['pageTo']:
            failures.append(f'{document_id}: locator outside range {locator}')
            continue
        if document_id not in pdf_page_counts:
            info = subprocess.check_output(['pdfinfo', str(path)], text=True)
            pdf_page_counts[document_id] = int(re.search(r'^Pages:\s+(\d+)$', info, re.MULTILINE).group(1))
        if page > pdf_page_counts[document_id]:
            failures.append(f'{document_id}: page does not exist {locator}')
    elif kind == 'html-page':
        match = re.match(r'html:pageNo=(\d+)(?:;.*)?$', locator)
        if not match or int(match.group(1)) != item['pageNo'] or path.stat().st_size == 0:
            failures.append(f'{document_id}: invalid or unreachable html locator {locator}')
    else:
        failures.append(f'{document_id}: unknown range kind {kind}')

result = {
    'total': len(manifest['rows']),
    'reachable': len(manifest['rows']) - len(failures),
    'failures': failures,
}
print(json.dumps(result, ensure_ascii=False, indent=2))
if failures or result['reachable'] != result['total']:
    raise SystemExit(1)
PY
```

Expected: exit 0、`reachable == total`、`failures: []`。1件でも未所属、range外、存在しないXLSX row／PDF page又は空HTMLならTask 4へ進まない。物理的に存在する空XLSX rowは`excluded`として保持する。

- [ ] **Step 8: 監査結果を判定する**

Run:

```bash
jq '{
  total: (.rows | length),
  seed: ([.rows[] | select(.disposition == "seed")] | length),
  excluded: ([.rows[] | select(.disposition == "excluded")] | length),
  schemaGap: ([.rows[] | select(.disposition == "schema-gap")] | length)
}' docs/spec-data/phase3/claim-master-source-row-manifest.json
```

Expected for continuing: `schemaGap: 0` and `total == seed + excluded`。

**STOP BRANCH:** `schemaGap > 0`の場合は、manifest契約testとrange testを通し、manifestだけを次のmessageでcommitする。

```bash
git add docs/spec-data/phase3/claim-master-source-row-manifest.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "docs(phase3-1/AC3-1): record task 13 schema gaps"
```

その後、Task 12再設計が必要なrow、現在不足するfield、影響候補4ファイルを報告し、Task 4以降を実行しない。

- [ ] **Step 9: `schema-gap = 0`の停止ゲートtestを追加する**

continuing branchだけで追加する。

```csharp
[Fact]
public void Source_manifest_has_no_schema_gaps_before_seed_transcription()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    manifest.RootElement.GetProperty("rows").EnumerateArray()
        .Should().NotContain(row =>
            row.GetProperty("disposition").GetString() == "schema-gap");
}
```

- [ ] **Step 10: 監査testsを通してcommitする**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Source_manifest \
  -v normal
```

Expected: PASS。

```bash
git add docs/spec-data/phase3/claim-master-source-row-manifest.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "docs(phase3-1/AC3-1): inventory task 13 source rows"
```

---

## Seedフェーズ — Task 3が`schema-gap = 0`で完了した場合だけ実行

### Task 4: manifest駆動のseed completeness testをRedにする

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Read: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Read: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`

- [ ] **Step 1: manifest採用keyとproduction seedを比較するtestを書く**

```csharp
[Fact]
public void Production_seed_keys_exactly_match_manifest_seed_rows()
{
    using var manifest = OpenRepositoryJson(ManifestPath);
    var expectedByKind = manifest.RootElement.GetProperty("rows").EnumerateArray()
        .Where(row => row.GetProperty("disposition").GetString() == "seed")
        .GroupBy(row => row.GetProperty("masterKind").GetString()!, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => group.Select(row => row.GetProperty("seedKey").GetString()!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);

    foreach (var kind in new[]
             {
                 "basic-rewards", "additions", "region-unit-prices",
                 "burden-caps", "transition-rules", "service-codes",
             })
    {
        using var seed = OpenRepositoryJson(
            $"src/Tsumugi.Infrastructure/ClaimMasters/Seed/{kind}.json");
        var actual = seed.RootElement.GetProperty("entries").EnumerateArray()
            .Select(row => row.GetProperty("key").GetString()!)
            .Order(StringComparer.Ordinal).ToArray();
        actual.Should().Equal(expectedByKind[kind]);
    }
}
```

同じ`masterKind + seedKey`が1 source rowだけのgroupは、3つのaggregation fieldがすべてnullであることをassertする。複数source rowに現れるgroupは、全rowが同一の非空`aggregationId`、`aggregationKind = multi-source-one-seed`、非空`aggregationReason`を持つことを別testで検証する。重複source rowを重複production entryとして要求しない。

- [ ] **Step 2: completeness testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Production_seed_keys \
  -v normal
```

Expected: FAIL because all six production `entries` arrays are empty。

- [ ] **Step 3: Red testを未commitのままseed実装へ引き渡す**

Run: `git diff --check`

Expected: diff check PASS。全6 seedが揃うまでこのtestは意図的にRedなのでcommitしない。Task 5〜8のtestとseedも同じworking treeで積み上げ、Task 9で全体Greenを確認して単一candidate data commitにする。

---

### Task 5: 独立マスタ3種を投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json`
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: 4 burden categoryと版境界のtestを書く**

manifestの`masterKind = burden-caps` key集合に加え、ADR 0022で独立に固定済みの次を代表値testへ置く。

```csharp
[Theory]
[InlineData("welfare", 0)]
[InlineData("low-income", 0)]
[InlineData("general-1", 9300)]
[InlineData("general-2", 37200)]
public void Burden_cap_seed_keeps_the_adr_0022_values(string key, int capYen)
{
    using var seed = OpenRepositoryJson(
        "src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json");
    var row = seed.RootElement.GetProperty("entries").EnumerateArray()
        .Single(item => item.GetProperty("key").GetString() == key);
    row.GetProperty("values").GetProperty("capYen").GetInt32().Should().Be(capYen);
    row.GetProperty("sourceDocumentId").GetString()
        .Should().Be("r6-disability-support-guide-202404");
    row.GetProperty("sourceLocator").GetString()
        .Should().Be("pdf:physical-page=9");
}
```

- [ ] **Step 2: region／transitionのRed testsを書く**

- regionはmanifestのkey集合、`decimal`文字列、B型service kind、期間の穴・重複なしを検査する。
- transitionは5 master versionが1件ずつ存在し、R6 option 1〜10、R8 option 1〜22、`filed-transition`、`production-activity-support`、R8 status別option集合がADR 0023の閉集合と一致することを検査する。
- testへ全地域単価表又は全option rowの値を二重転記せず、manifest key集合と代表境界だけを使う。

- [ ] **Step 3: focused testsがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests.Burden|FullyQualifiedName~ClaimMasterSeedPhase31Tests.Region|FullyQualifiedName~ClaimMasterSeedPhase31Tests.Transition' \
  -v normal
```

Expected: FAIL because the three seed files are empty。

- [ ] **Step 4: 3 seedへsource rowどおりに転記する**

各entryはこの共通形にする。

```json
{
  "key": "stable-key",
  "effectiveFrom": "2024-04",
  "effectiveTo": null,
  "sourceDocumentId": "document-id",
  "sourceSha256": "64-lowercase-hex",
  "sourceLocator": "pdf:physical-page=9",
  "values": {}
}
```

- burdenはADR 0022の4 rowだけを入れ、`Unspecified`を作らない。
- regionは`mhlw-unit-price-notice-observed-946c3d96`の表記を十進文字列で保存し、`double`を経由しない。
- transitionはR6-04、R6-06、R7-01、R7-09、R8-06を別rowにし、後版を過去月へ遡及しない。

- [ ] **Step 5: 3 seedのtestsを通す**

Run the Step 3 command。

Expected: PASS for Burden / Region / Transition tests。全6 seed completeness testはまだservice code等が空なのでFAILのままでよい。変更はcommitせずTask 6へ引き渡す。

---

### Task 6: service codeとselectorを投入する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: service code completenessのRed testを書く**

manifestの`masterKind = service-codes`全rowについて、`seedKey`とproduction key、source document ID、source locator、effective periodを比較する。さらに次を検査する。

- `serviceCode`が空でない。
- `serviceKind`がB型の安定keyである。
- `selectors`が非空・重複なし。
- 同じservice codeの有効期間が重ならない。
- R6とR8のworkbook-order 38〜41で`disposition = seed`になった全行が存在する。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Service_code \
  -v normal
```

Expected: FAIL because `service-codes.json` is empty。

- [ ] **Step 3: service-codes.jsonへ転記する**

```json
{
  "key": "manifest-seed-key",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceDocumentId": "r8-service-codes-2-xlsx",
  "sourceSha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
  "sourceLocator": "workbook-order=38;row=7",
  "values": {
    "serviceCode": "official-code",
    "serviceKind": "employment-continuation-support-b",
    "selectors": ["source-defined-selector"]
  }
}
```

selectorは公式rowとADR 0025の対象集合から決める。名称の部分一致又は実装者都合の広いselectorを作らない。

- [ ] **Step 4: service code testを通す**

Run the Step 2 command。

Expected: PASS。

- [ ] **Step 5: service code変更をTask 7へ引き渡す**

Run: `git diff --check`

Expected: PASS。全体completenessがまだRedなのでcommitしない。

---

### Task 7: 基本報酬をservice codeへ接続する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: basic reward completenessと参照のRed testを書く**

次を検査する。

- manifestの`masterKind = basic-rewards` key集合と完全一致。
- R6 option 1〜10、R8 option 1〜22をPaymentBandへ解決するために必要な基本報酬rowが揃う。
- `paymentBand / staffingKey / capacityKey / serviceCode`の同一期間組が一意。
- `units`が非負整数。
- 各`serviceCode`を同じeffective period全体で覆う`service-codes.json` rowがちょうど1件ある。
- ADR 0023のR6 workbook順38 row 7〜912、R8 row 7〜1993のうちmanifestで基本報酬に分類したrowだけが入る。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Basic_reward \
  -v normal
```

Expected: FAIL because `basic-rewards.json` is empty。

- [ ] **Step 3: basic-rewards.jsonへ転記する**

```json
{
  "key": "manifest-seed-key",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceDocumentId": "r8-service-codes-2-xlsx",
  "sourceSha256": "307b631ed91a07d4fc9a77b090030b2819731aa018a0374544c1984bf2935049",
  "sourceLocator": "workbook-order=38;row=7",
  "values": {
    "paymentBand": "manifest-classification",
    "staffingKey": "manifest-classification",
    "capacityKey": "manifest-classification",
    "serviceCode": "official-code",
    "units": 0
  }
}
```

上例の`0`はJSON shapeだけを示す例示sentinelであり、productionへそのまま使用しない。`units`を含む全値はsource locatorの実値だけを転記する。分類不能ならTask 3のmanifestを`schema-gap`へ戻し、seedを作らない。

- [ ] **Step 4: basic rewardとreference testsを通す**

Run the Step 2 command and:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSchemaPhase31Tests \
  -v normal
```

Expected: PASS。

- [ ] **Step 5: 基本報酬変更をTask 8へ引き渡す**

Run: `git diff --check`

Expected: PASS。`additions.json`が空なのでcommitしない。

---

### Task 8: 割合加減算をselectorへ接続する

**Files:**
- Modify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json`
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`

- [ ] **Step 1: percentage adjustment completenessのRed testを書く**

manifestの`masterKind = additions`全rowについて、次を検査する。

- `percentage`はcanonical decimal string。
- `percentageBaseScope`は`per-service-code-unit`又は`monthly-target-unit-sum`。
- `percentageApplicationKind`は`replace`、`add`又は`subtract`。
- `targetSelector`が同じ期間を覆うservice code rowのselectorへ1件以上一致。
- 同一selector・同一期間の`calculationOrder`が1から連続し、重複・循環がない。
- `roundingRuleId = claim.rounding.units.half-up.v1`。
- scopeに対応する`calculationStepId`がADR 0025と一致。

- [ ] **Step 2: testがRedになることを確認する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter FullyQualifiedName~ClaimMasterSeedPhase31Tests.Percentage_adjustment \
  -v normal
```

Expected: FAIL because `additions.json` is empty。

- [ ] **Step 3: additions.jsonへ転記する**

```json
{
  "key": "manifest-seed-key",
  "effectiveFrom": "2026-06",
  "effectiveTo": null,
  "sourceDocumentId": "source-id",
  "sourceSha256": "64-lowercase-hex",
  "sourceLocator": "source-specific-locator",
  "values": {
    "percentage": "0.15",
    "percentageBaseScope": "monthly-target-unit-sum",
    "percentageApplicationKind": "add",
    "targetSelector": "source-defined-selector",
    "calculationOrder": 1,
    "roundingRuleId": "claim.rounding.units.half-up.v1",
    "calculationStepId": "claim.step.units.monthly-target.percentage.v1"
  }
}
```

例示値`0.15`をproductionへ流用せず、各値を公式source rowから転記する。固定単位又は合成済み単位をpercentageへ変換しない。そのようなrowを発見した場合はTask 3へ戻し、`schema-gap`としてTask 13を停止する。

- [ ] **Step 4: additionsと全reference validationを通す**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests.Percentage_adjustment|FullyQualifiedName~ClaimMasterSchemaPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests' \
  -v normal
```

Expected: PASS。

- [ ] **Step 5: 全seed変更をTask 9へ引き渡す**

Run: `git diff --check`

Expected: PASS。Task 9の全体testとCIがGreenになるまでcommitしない。

---

### Task 9: 6 seedの全体整合とcandidate commitを固定する

**Files:**
- Modify: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Verify: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Verify: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`

- [ ] **Step 1: production embedded bundleのRed integration testを書く**

```csharp
[Theory]
[InlineData(2024, 4, "claim-master-r6-04")]
[InlineData(2024, 6, "claim-master-r6-06")]
[InlineData(2025, 1, "claim-master-r7-01")]
[InlineData(2025, 9, "claim-master-r7-09")]
[InlineData(2026, 5, "claim-master-r7-09")]
[InlineData(2026, 6, "claim-master-r8-06")]
public void Embedded_production_bundle_loads_and_resolves_boundaries(
    int year,
    int month,
    string version)
{
    var provider = JsonClaimMasterProvider.LoadEmbedded();

    provider.ResolveVersion(new ServiceMonth(year, month)).Version.Value
        .Should().Be(version);
}
```

追加で次を検査する。

- manifestのdistinct `masterKind + seedKey`数が6 seed entries総数と一致。
- manifest rangeの`expectedItemCount`合計がmanifest rows総数と一致。
- `excluded`全rowに理由があり、対応seed keyがない。
- stable key orderingをordinalで固定できる。

- [ ] **Step 2: integration testを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' \
  -v normal
```

Expected: PASS。Redになった場合は、期待値を弱めず該当seed又はmanifest分類を修正する。

- [ ] **Step 3: repository差分のJSON完全性を確認する**

Run:

```bash
jq empty docs/spec-data/phase3/claim-master-source-row-manifest.json
for file in src/Tsumugi.Infrastructure/ClaimMasters/Seed/{basic-rewards,additions,region-unit-prices,burden-caps,transition-rules,service-codes}.json; do
  jq empty "$file"
done
git diff --check
```

Expected: all commands exit 0。

- [ ] **Step 4: full CIを実行する**

Run: `./build/ci.sh`

Expected: `CI OK`、build warnings/errors 0、全tests PASS、Domain／Application coverage gate、architecture、offline gate PASS。

- [ ] **Step 5: 全データとintegration testをcandidate commitにする**

```bash
git add docs/spec-data/phase3/claim-master-source-row-manifest.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json \
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json \
  tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs
git commit -m "data(phase3-1/AC3-1): add sourced R6 and R8 claim masters"
```

- [ ] **Step 6: candidate commitを固定する**

Run:

```bash
git status --short
git rev-parse HEAD
```

Expected: clean status。表示されたHEADを`CANDIDATE_COMMIT`として保存し、Task 10のreviewerへ渡す。candidate以後、review完了までmanifest又はseedを変更しない。

---

### Task 10: 独立全件照合・証跡・最終レビューを完了する

**Files:**
- Create: `docs/phase3-1-master-transcription-review.md`
- Review: `docs/spec-data/phase3/claim-master-source-row-manifest.json`
- Review: `src/Tsumugi.Infrastructure/ClaimMasters/Seed/*.json`
- Review: `tests/Tsumugi.Infrastructure.Tests/ClaimMasters/ClaimMasterSeedPhase31Tests.cs`
- Read: `/tmp/tsumugi-phase31-task13/acquisition.jsonl`

- [ ] **Step 1: fresh source-data reviewerを割り当てる**

転記担当ではない別担当者又はfresh subagentへ、次だけを渡す。

```text
candidate commit: <CANDIDATE_COMMIT>
manifest: docs/spec-data/phase3/claim-master-source-row-manifest.json
seeds:
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/basic-rewards.json
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/additions.json
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/region-unit-prices.json
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/burden-caps.json
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/transition-rules.json
  src/Tsumugi.Infrastructure/ClaimMasters/Seed/service-codes.json
ADRs: 0020, 0022, 0023, 0025
verified source directory: /tmp/tsumugi-phase31-task13/sources/
```

reviewerは次の順で全件確認する。

1. candidate commitをcheckoutした状態とpathを確認。
2. manifest `documents`の全SHAを原本へ再計算。
3. `extractionRanges`が各公式資料のB型対象範囲を覆うことを確認。
4. range別公式row数とmanifest row数を照合。
5. 各`excluded`／`schema-gap`理由を確認。
6. 各`seed` rowのkey、値、code、period、source ID、SHA、locatorを原本とproduction seedへ照合。
7. file別row count、総row count、照合数、discrepancy数を返す。

- [ ] **Step 2: reviewer判定を処理する**

合格条件:

```text
SHA match rate: 100%
locator reach rate: 100%
manifest coverage rate: 100%
source row review rate: 100%
unexplained exclusions: 0
schema gaps: 0
value/code/period discrepancies: 0
Status: Approved
```

Issues Foundなら、該当seed又はmanifestを修正してfocused testsと`./build/ci.sh`を再実行し、新candidate commitを作る。同じreviewerへ全row再レビューを依頼し、差分rowだけで合格にしない。

- [ ] **Step 3: transcription review証跡を書く**

取得表は記憶又はファイルmtimeから再構成せず、Task 2のreceiptから生成する。

```bash
jq -r -s '.[] | [
  .documentId,
  .url,
  .expectedSha256,
  .actualSha256,
  (.bytes | tostring),
  .retrievedAt,
  .result
] | @tsv' /tmp/tsumugi-phase31-task13/acquisition.jsonl
```

Expected: manifest document件数と同じ行数で、全行のexpected SHAとactual SHAが一致し、resultがPASS。

```markdown
# Phase 3-1 Claim Master Transcription Review

**Candidate commit:** `<hash>`
**Reviewer:** `<task-id-or-name>`
**Reviewed at:** `<ISO-8601 with offset>`
**Status:** Approved

## Source acquisition

| Document ID | URL | Expected SHA-256 | Actual SHA-256 | Bytes | Retrieved at | Result |
| --- | --- | --- | --- | ---: | --- | --- |

## Schema-fit audit

| Classification | Rows |
| --- | ---: |
| Seed | `<n>` |
| Excluded with reason | `<n>` |
| Schema gap | `0` |

## Full-row review

| Seed file | Candidate rows | Reviewed rows | Discrepancies |
| --- | ---: | ---: | ---: |

## Verification

- Focused tests: PASS
- `./build/ci.sh`: CI OK
- Manifest coverage: 100%
- SHA match: 100%
- Locator reach: 100%
```

テンプレート記号を残さず、reviewerの実数と実行時刻を記入する。ローカル一時pathは記録しない。

- [ ] **Step 4: fresh final verificationを実行する**

Run:

```bash
dotnet test tests/Tsumugi.Infrastructure.Tests \
  --filter 'FullyQualifiedName~ClaimMasterSeedPhase31Tests|FullyQualifiedName~JsonClaimMasterProviderTests|FullyQualifiedName~ClaimSpecificationBoundaryTests' \
  -v normal
./build/ci.sh
git diff --check
git status --short
```

Expected: focused tests PASS、`CI OK`、diff check clean。`git status`には未commitのreview docだけが表示される。

- [ ] **Step 5: evidence commitを作る**

```bash
git add docs/phase3-1-master-transcription-review.md
git commit -m "docs(phase3-1/AC3-1): record claim master source review"
```

- [ ] **Step 6: Task 13全範囲のcode reviewを依頼する**

Use `@superpowers:requesting-code-review` with:

```text
BASE: Task 13開始前commit
HEAD: current HEAD
SPEC: docs/superpowers/specs/2026-07-12-phase3-1-task13-claim-master-seed-design.md
PLAN: docs/superpowers/plans/2026-07-12-phase3-1-task13-claim-master-seeds.md
Focus: source completeness, schema-fit gate, provenance, effective periods,
       service-code/selector references, independent review evidence
```

Expected: no unresolved Major／Minor findings。指摘修正後はfocused tests、full CI、独立全件照合の対象commit整合を再確認する。

---

## Task 13完了条件

- [ ] manifest document取得率100%。
- [ ] SHA一致率100%。
- [ ] locator到達率100%。
- [ ] source-side range網羅率100%。
- [ ] `schema-gap = 0`。
- [ ] manifest `seed` key集合と6 production seed key集合が完全一致。
- [ ] R6-04、R6-06、R7-01、R7-09、R8-06の境界が一意。
- [ ] basic reward → service code、adjustment → selector参照が全件有効。
- [ ] independent reviewerが全source rowをApproved。
- [ ] focused tests PASS、`./build/ci.sh`が`CI OK`。
- [ ] 原本がgitへ混入していない。
- [ ] `docs/phase3-1-master-transcription-review.md`が実際のcandidate commitを参照する。
