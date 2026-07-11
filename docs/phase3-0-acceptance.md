# Phase 3-0 受け入れ証跡

- 日付: 2026-07-11
- 対象範囲: `ec0b4b9..ed50ced`
- 証跡HEAD: `ed50ced`
- 最終レビュー対象: committed `ec0b4b9..ed50ced` + staged Task 16 docs
- 最終レビュー結果（2026-07-11）: Critical 0 / High 0 / Medium 0
- Overall: **✅ accepted**

Phase 3-0の実装、targeted test証跡、final-fix後の証跡HEAD `ed50ced`でのfresh full quality gateが揃い、committed rangeとstaged Task 16 docsに対する最終レビューでCritical / High / Mediumが0件となったため受け入れる。

## AC3-0-1〜5

| AC | state | ADR / spec | implementation | tests | representative commits | result |
| --- | --- | --- | --- | --- | --- | --- |
| AC3-0-1 出典・設計判断 | 実装済み、最終受入保留 | [ADR 0020](decisions/0020-claim-master-sources-and-versioning.md)〜[ADR 0026](decisions/0026-claim-batch-snapshot.md)、[Phase 3実装指示](../06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md) | URL・版・施行日・取得日・SHA-256・結論・影響、令和6/7/8の5版連続source chain、体制コード、負担上限、平均工賃、CSV、端数、snapshot規律 | `JsonClaimMasterProviderTests`、`CsvSpecificationCompletenessTests`、`ClaimFinalizationOperationV1Tests` | `9dcf165` `807e08b` `56d8110` `09f1f75` `9cb4e00` `95f8d4c` `82c2863` `a3ef614` `a3dfa90` `a7ee1b6` `c05aa4e` `22393b8` `3978fb9` `1f057b2` `bf6ea45` `ee70a1f` `ed50ced` | 5 releaseの連続性と成人B型の負担上限sourceを解決。source gap 0のtargeted証跡あり。fresh full gate通過、最終受入保留 |
| AC3-0-2 版付きマスタ・境界検査 | 実装済み、最終受入保留 | [ADR 0020](decisions/0020-claim-master-sources-and-versioning.md)、[ADR 0025](decisions/0025-claim-rounding-rules.md) | `ServiceMonth` / `ProcessingMonth`、`ClaimMasterCatalogPolicy`、`IClaimMasterProvider`、`JsonClaimMasterProvider`、schema・seed、`ExternalSpecificationLiteralGuard` | `ClaimMasterCatalogPolicyTests`、`JsonClaimMasterProviderTests`、`ClaimSpecificationBoundaryTests`、`CompositionRootTests` | `58125ee` `c68fe44` `41d5b79` `5dd2d8f` `2aa8a86` `a1ce396` `9cdc302` `e3fb79b` `7baabb4` `95066fd` `c7ed6ae` `44a5010` `c89e9ad` `2d48107` | 適用年月解決、重複・空白・未知source拒否、ハードコード境界のtargeted証跡あり。制度実値投入はPhase 3-1 |
| AC3-0-3 CSV仕様アセンブリ | 実装済み、最終受入保留 | [ADR 0024](decisions/0024-kokuhoren-csv-and-field-mapping.md) | `Tsumugi.Infrastructure.Csv`、embedded specification catalog / loader、solution・検査配線 | `CsvSpecificationLoaderTests`、`ArchitectureTests`、`OfflineComplianceTests`、`AppOfflineComplianceTests`、`KokuhorenTransmissionSeparationTests` | `8d193f9` `5ecc154` `f6a9420` `c2b9f9e` | Csv 52/52、Infrastructure Claim/Offline/Architecture 219/219に含めて通過 |
| AC3-0-4 append-only請求土台 | 実装済み、最終受入保留 | [ADR 0026](decisions/0026-claim-batch-snapshot.md) | `ClaimBatch` / `ClaimDetail`、`ClaimBatchPolicy`、EF configuration / migration、`AppendOnlyGuard`、raw repository、operation-local `ClaimFinalizationStore`、typed audit、unavailable codec registry | `ClaimBatchTests`、`ClaimBatchPolicyTests`、`AppendOnlyGuardPhase3Tests`、`ClaimBatchUniqueConstraintTests`、`ClaimBatchMigrationTests`、`ClaimBatchRepositoryTests`、`ClaimFinalizationStoreTests`、Application Claim tests | `35ca6fa` `dce5ce5` `23ae4a6` `875cee6` `1280f5c` `8b5c225` `47a656e` `38dd67a` `b1c7839` `419be08` `be37992` | Domain Claim 150/150、Application Claim 55/55、Infrastructure targeted suiteに含めて通過。replay時も全履歴を検証する回帰テストを追加。production codecは意図的に未登録 |
| AC3-0-5 全項目mapping | 実装済み、Phase 3-1引継ぎあり、最終受入保留 | [ADR 0024](decisions/0024-kokuhoren-csv-and-field-mapping.md)、[全項目mapping](phase3-claim-field-mapping.md) | CSV共通編・事業所編443 fieldIds、3帳票113 fieldIds、machine-readable inventory / mapping | `CsvSpecificationCompletenessTests`、`ClaimFieldMappingCompletenessTests` | `164cac2` `6ce1011` `85e1fe0` `7949635` `6e92151` | inventory CSV 443/443、帳票 113/113、mapping差分0。missingは51 fieldIds / 26 implementation targetsとしてPhase 3-1へ送る |

横断CI配線の代表コミットは`dab2102`、停止条件cleanupは`55483bb`、replay全履歴検証修正は`be37992`、source final-fixは`1f057b2` / `bf6ea45` / `ee70a1f` / `ed50ced`である。

## Targeted tests（証跡HEAD `ed50ced`）

| command | result |
| --- | --- |
| `dotnet test tests/Tsumugi.Domain.Tests/Tsumugi.Domain.Tests.csproj --filter "FullyQualifiedName~Claim" -v normal` | 150/150 passed、failed 0、skipped 0 |
| `dotnet test tests/Tsumugi.Application.Tests/Tsumugi.Application.Tests.csproj --filter "FullyQualifiedName~Claim" -v normal` | 55/55 passed、failed 0、skipped 0 |
| `dotnet test tests/Tsumugi.Infrastructure.Csv.Tests/Tsumugi.Infrastructure.Csv.Tests.csproj -v normal` | 52/52 passed、failed 0、skipped 0 |
| `dotnet test tests/Tsumugi.Infrastructure.Tests/Tsumugi.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Claim|FullyQualifiedName~OfflineCompliance|FullyQualifiedName~Architecture" -v normal` | 219/219 passed、failed 0、skipped 0 |
| `dotnet test tests/Tsumugi.App.Tests/Tsumugi.App.Tests.csproj --filter "FullyQualifiedName~CompositionRootTests" -v normal` | 4/4 passed、failed 0、skipped 0 |

合計は **480/480 passed、failed 0、skipped 0**。初回の並列実行は共有PDB build artifactの競合が発生したため証跡から除外した。上記exact 5 commandsをfreshかつ逐次実行し、各commandがexit 0となった結果だけを権威あるtargeted evidenceとする。

## Full quality gate

2026-07-11に証跡HEAD `ed50ced`からfresh full quality gateを実行し、全コマンドがexit 0で通過した。後続の最終レビューもCritical / High / Medium 0件で通過したため、overallをacceptedとする。

| command / gate | fresh result |
| --- | --- |
| `git diff --check -- CHANGELOG.md docs/open-questions.md docs/phase3-0-acceptance.md` | exit 0 |
| `dotnet format --verify-no-changes` | exit 0、変更要求なし |
| `./build/ci.sh` | exit 0、build warnings 0 / errors 0、failed 0 / skipped 0、`CI OK` |
| Domain line coverage | **98.12%**（floor 95%） |
| Application line coverage | **85.85%**（floor 70%） |
| `dotnet list package --vulnerable --include-transitive` | exit 0。報告は既知抑制 `GHSA-2m69-gcr7-jv3q` のみ、新規未抑制脆弱性 0 |

| full suite | passed / total | failed | skipped |
| --- | ---: | ---: | ---: |
| `Tsumugi.Domain.Tests` | 385 / 385 | 0 | 0 |
| `Tsumugi.Application.Tests` | 179 / 179 | 0 | 0 |
| `Tsumugi.Infrastructure.Csv.Tests` | 52 / 52 | 0 | 0 |
| `Tsumugi.Infrastructure.Reporting.Tests` | 12 / 12 | 0 | 0 |
| `Tsumugi.Infrastructure.Tests` | 337 / 337 | 0 | 0 |
| `Tsumugi.App.Tests` | 135 / 135 | 0 | 0 |
| **合計** | **1100 / 1100** | **0** | **0** |

2026-07-11の最終レビュー対象はcommitted `ec0b4b9..ed50ced` + staged Task 16 docsであり、結果はCritical 0 / High 0 / Medium 0。Overallは **✅ accepted** とする。

## 停止条件audit

- 証跡HEAD `ed50ced`でstop blockerは0件。
- 旧`Placeholder.cs` 3件は`55483bb`で削除済み。
- source catalogは63件。負担上限source chainは `r6-04`（2024-04〜05）、`r6-06`（2024-06〜12）、`r7-01`（2025-01〜08）、`r7-09`（2025-09〜2026-05）、`r8-06`（2026-06〜open）の5 releaseで連続し、source gapは0。各releaseは成人B型の現行給付sourceを正確に1件持ち、chainは単調、provenanceはmirrors / archivesを含め再現可能である。
- 未対応mappingは放置せず、[mapping正本](phase3-claim-field-mapping.md#phase-3-1へ送る未実装入力-51-mapping-entries)へ **51 missing fieldIds / 26 implementation targets** として明示した。51 fieldIdsを51実装項目とは数えない。CSV 30件＋帳票21件で、stable keyは`targetModel + targetProperty + uiSurface + migrationRequired`、全targetがmigration requiredである。Markdown / machine-readable assignment・group・countの差分は0。
- `TBD`等を拒否するテストfixtureのtoken、履歴として残る2026-06-29付旧計画、Avaloniaの`PlaceholderText`、未実装を説明する文書上の語は正当な検索hitであり、production placeholderではない。
- `UnavailableClaimSnapshotValidationCodecRegistry`は空殻ではなく、Phase 3-1でvalidated codecを登録するまで確定操作を拒否する意図的なfail-closed実装である。
- throw-onlyのCSV writer、仮コード、空SHA、未分類fieldIdはproductionに置かれていない。

## Deferred / 非実装

- Phase 3-1〜3-3、請求報酬計算、validated finalization、`IValidatedClaimSnapshotReader`、3帳票生成、CP932/CRLFの提出CSV生成、保存UIは未実装。
- Phase 3-1は51 missing fieldIdsを26 implementation targetsへ束ね、モデル・migration・実UIを追加してからvalidated readerとproduction codecを実装する。自由記述、既存別用途項目、0、空文字から推測しない。
- Phase 3-2 / 3-3は、Phase 3-1で検証された実効`ClaimBatch`だけを入力にする。
- 2026-06-29付のPhase 3旧計画は実行対象外。Phase 3-1はAC3-1 / 2 / 3 / 4 / 8 / 9を対象とする新計画を作成してから着手する。
- 伝送、電子証明書、回線処理は引き続きスコープ外。

## NuGet audit suppression

2026-07-11の証跡HEAD `ed50ced`でのfresh vulnerability auditでは、報告されたadvisoryは`GHSA-2m69-gcr7-jv3q`（`SQLitePCLRaw.lib.e_sqlite3` 2.1.11）のみであり、新規未抑制脆弱性は0件だった。既知抑制として追跡を継続する。完全オフライン、SQLiteをネットワークへ露出しないこと、唯一の動的値を埋め込む生SQL経路（`VACUUM INTO`）でエスケープ済みであることを現在の許容根拠とする。upstreamがパッチ済みネイティブバンドルを公開した時点で抑制を解除し、依存更新とセキュリティ再レビューを行う。
