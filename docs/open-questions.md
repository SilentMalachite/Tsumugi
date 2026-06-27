# Open Questions（仕様未確定・要確認）

> 推測で実装しない。確定するまで該当機能の実装は止める。

- [ ] 報酬の単位数・加算・地域区分単価の公式出典（令和6年改定）。フェーズ3着手前に必須。出典URL/版を記す。
- [ ] **事業所体制の加算フラグキー一覧**: フェーズ1では文字列キーで保持。代表例（暫定）: `mealProvision`（食事提供体制加算）、`transportSupport`（送迎加算）等。フェーズ3で報酬告示と突合して正式コード・単位数を確定する。
- [ ] 国保連 請求データCSVの公式インターフェース仕様書の版・文字コード・改行・レコード種別・項目順。フェーズ3着手前に必須。
- [ ] 工賃計算の既定（出来高/時間/固定）・締め日・端数規則の確定（事業所運用に依存）。フェーズ2着手前。
- [ ] SQLite 暗号化（SQLCipher等）の採否（ADR 0003 で暫定判断、運用要件で再確認）。
- [ ] **[Security] NuGet audit suppression GHSA-2m69-gcr7-jv3q**: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11（EF Core SQLite 経由のトランジティブ依存）に HIGH 深刻度のアドバイザリが存在し、現時点で修正バージョンが upstream から提供されていない。`Directory.Build.props` に `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-2m69-gcr7-jv3q"/>` を設定して抑制している。許容根拠: 修正版なし／完全オフラインのデスクトップアプリで SQLite がネットワーク面に露出しない／唯一の生SQL経路（`VACUUM INTO`）にシングルクォートエスケープを適用済み。解除条件: upstream がパッチ済みネイティブバンドルを公開したタイミングで抑制を除去し、バージョンを更新する。**セキュリティ再レビュー必須。**
- [x] **App 層のオフライン遵守確認（2026-06-26 クローズ）**: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs` で `System.Reflection.Metadata` の `PEReader`/`MetadataReader` を用いて `Tsumugi.App.dll` の `TypeReference`/`MemberReference` を直接走査し、`System.Net.*` 等の禁止 API への直接参照を CI で機械判定する（AC0-5）。allowlist は最小・理由付きで `AppOfflineComplianceTests.Allowlist` に明示（既定は空）。歯のある検査であることを `HttpClient` 一時混入で確認済み。実行時ネットワーク監視はスコープ外（直接参照禁止＋オフライン端末運用で十分とする）。
- [ ] **アーキテクチャ/オフラインテストは直接参照のみを検査**: `GetReferencedAssemblies()` はトランジティブ参照をたどらない。オープンな問い: 将来的に依存グラフ全体を検査できるアーキテクチャテストツール（例: NetArchTest）を採用するかどうかを検討する。

## Phase 1 → Phase 2/3 引継ぎ

- [ ] **DailyRecord 多重 New 重複の検知**: 同一 (RecipientId, ServiceDate) に複数の `RecordKind.New` が登録された場合、`DailyRecordPolicy.Effective` は `CreatedAt` 最早を選択する（決定論的）。永続化レベルでの一意制約は未実装。Phase 2 で `RecordDailyRecordUseCase` に Application 層の重複チェックを追加するか、DB に partial unique index を追加するか検討。
- [ ] **AppendOnlyGuard と EF Core bulk operations**: `AppendOnlyGuard.Inspect` は ChangeTracker 経由の `Modified`/`Deleted` のみ検出。`ExecuteUpdateAsync`/`ExecuteDeleteAsync` は ChangeTracker を経由しないため検出できない。現在の Repository 実装に bulk 呼び出しはないが、将来追加する際は別途ガードが必要。`ArchitectureTests` で append-only 型に対する bulk 呼び出しを禁止する案あり。
