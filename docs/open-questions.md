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

- [ ] **DailyRecord 多重 New 重複の検知**: 同一 (RecipientId, ServiceDate) に複数の `RecordKind.New` が登録された場合、`DailyRecordPolicy.Effective` は `CreatedAt` 最早を選択する（決定論的）。`RecordDailyRecordUseCase` は同日 New が既存ならエラーで拒否するが、レース条件下では二重新規が物理的に格納され得る。Phase 2 で DB に partial unique index を追加するか、SqliteEventBus 経由のロックを検討。
- [ ] **AppendOnlyGuard と EF Core bulk operations**: `AppendOnlyGuard.Inspect` は ChangeTracker 経由の `Modified`/`Deleted` のみ検出。`ExecuteUpdateAsync`/`ExecuteDeleteAsync` は ChangeTracker を経由しないため検出できない。現在の Repository 実装に bulk 呼び出しはないが、将来追加する際は別途ガードが必要。`ArchitectureTests` で append-only 型に対する bulk 呼び出しを禁止する案あり。
- [ ] **報酬・CSV ハードコード機械判定 (CLAUDE.md §ハード制約 3)**: Phase 1 には報酬算定・CSV 生成のサーフェスが存在しないため、現時点で「単位数/加算/CSV フィールド literal が混入していないこと」を機械判定するテストはエントリポイントを持たない。Phase 3 で報酬テーブル・CSV 生成器を導入する際に以下を同時に追加する: (a) Domain/Application のソース文字列スキャナで `単位数` `加算` `区分単価` 等の語彙が seed JSON 以外に現れたら失敗するテスト、(b) CSV カラム名 literal が `Tsumugi.Infrastructure.Csv` 名前空間以外に現れたら失敗するテスト、(c) 整数 literal の上限ガード（例: 1000 を超える decimal/int literal を Domain 内で禁止）。本項目は Phase 3 着手前のチェックリスト。
- [ ] **Avalonia GUI 目視確認 (AC1-8 補完)**: Phase 1 では `AccessibilityDefaults` の値・適用・XAML 配線を全て CI テストで担保したが、実機起動でのフォント拡大追従、Reduce Motion の Transition 抑止、各 View のタブ順とフォーカス移動は手動 QA でしか確認できない。Phase 2 着手前に macOS/Windows 双方で 1 回ずつ目視チェックする。
- [ ] **OfficeCapability の正式コード集合**: ADR 0006 の通り Phase 1 は `mealProvision` / `transportSupport` のみの暫定キーで運用。Phase 3 で報酬告示と突合して正式コード（食事提供体制加算 I/II、送迎加算 I/II 等）を確定する。
- [ ] **`UpdateOffice` / `UpdateRecipient` の actor 監査ログ**: 引数で `actor` を受けているが Phase 1 では未使用（`_ = actor`）。Phase 2 で監査ログテーブルへ「いつ誰が何を更新したか」を追記する設計を入れる。
- [ ] **性別など利用者属性の拡張**: 国保連請求 CSV では性別が必須項目の可能性が高い。Phase 1 の `Recipient` は漢字氏名 / カナ氏名 / 生年月日のみ。Phase 3 着手前に CSV インターフェース仕様書で必須項目を洗い出し、enum + migration を発行する。**進捗**: 2026-06-28 に `Certificate` 側に発行時点スナップショットとして `RecipientGender` 等を追加（ADR 0010）。`Recipient` マスタへの拡張は Phase 3 で CSV 仕様確認後に実施。

## Phase 1 受給者証 様式準拠（2026-06-28 追加）

- [ ] **自治体差異**: 受給者証の様式は MHLW 告示で共通項目が定義される一方、自治体ごとに独自の補足欄（例: 通所給食提供時間帯・上限管理事業所の電話番号など）がある。Tsumugi では「主要セクションのみ準拠」とし、自治体独自項目は当面 `SupplyNotes` / `Notes` の自由記述で受ける（ADR 0010）。利用自治体ごとに具体的な追加項目が判明したら個別に enum 化を検討する。
- [ ] **食事提供体制加算 / 高額障害福祉サービス費等の単位数**: Phase 1 では「適用 yes/no」のフラグのみ保持し、金額算定（単位数・利用額）は Phase 3 報酬告示と突合してから実装する（CLAUDE.md §ハード制約 3）。
- [ ] **負担区分の月額上限金額表**: `PaymentBurdenCategory` の各区分（生活保護/低所得/一般1/一般2）の月額上限額（円）は告示で定義される。現状は `MonthlyCostCap` 列に手入力。Phase 3 で区分→上限額の対応テーブルをシード JSON 化し、`PaymentBurden` 設定時に自動入力する案を検討する。
- [ ] **計画相談支援事業者マスタ**: 現状は受給者証ごとに事業者名を自由記述。複数受給者で同じ事業者を参照することが多いため、Phase 3 で `ConsultationProvider` マスタを切り出して FK 参照に変更するか検討する。
- [ ] **ContractedProvider と Contract の整理**: Phase 1 既存の `Contract` は自社事業所の利用契約のみ表現する。新規 `ContractedProvider` は受給者証「サービス事業者記入欄」に書かれる**全契約事業所**（他事業所含む）を網羅する。重複格納を避けたい場合は、自社契約は `Contract` 側のみで管理し `ContractedProvider` には他事業所のみ書く運用も検討する。Phase 2 着手前に運用方針を確定する。
