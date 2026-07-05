# Open Questions（仕様未確定・要確認）

> 推測で実装しない。確定するまで該当機能の実装は止める。

- [ ] 報酬の単位数・加算・地域区分単価の公式出典（令和6年改定）。フェーズ3着手前に必須。出典URL/版を記す。
- [ ] **事業所体制の加算フラグキー一覧**: フェーズ1では文字列キーで保持。代表例（暫定）: `mealProvision`（食事提供体制加算）、`transportSupport`（送迎加算）等。フェーズ3で報酬告示と突合して正式コード・単位数を確定する。
- [ ] 国保連 請求データCSVの公式インターフェース仕様書の版・文字コード・改行・レコード種別・項目順。フェーズ3着手前に必須。
- [ ] SQLite 暗号化（SQLCipher等）の採否（ADR 0003 で暫定判断、運用要件で再確認）。
- [ ] **[Security] NuGet audit suppression GHSA-2m69-gcr7-jv3q**: `SQLitePCLRaw.lib.e_sqlite3` 2.1.11（EF Core SQLite 経由のトランジティブ依存）に HIGH 深刻度のアドバイザリが存在し、現時点で修正バージョンが upstream から提供されていない。`Directory.Build.props` に `<NuGetAuditSuppress Include="https://github.com/advisories/GHSA-2m69-gcr7-jv3q"/>` を設定して抑制している。許容根拠: 修正版なし／完全オフラインのデスクトップアプリで SQLite がネットワーク面に露出しない／唯一の生SQL経路（`VACUUM INTO`）にシングルクォートエスケープを適用済み。解除条件: upstream がパッチ済みネイティブバンドルを公開したタイミングで抑制を除去し、バージョンを更新する。**セキュリティ再レビュー必須。**
- [x] **App 層のオフライン遵守確認（2026-06-26 クローズ）**: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs` で `System.Reflection.Metadata` の `PEReader`/`MetadataReader` を用いて `Tsumugi.App.dll` の `TypeReference`/`MemberReference` を直接走査し、`System.Net.*` 等の禁止 API への直接参照を CI で機械判定する（AC0-5）。allowlist は最小・理由付きで `AppOfflineComplianceTests.Allowlist` に明示（既定は空）。歯のある検査であることを `HttpClient` 一時混入で確認済み。実行時ネットワーク監視はスコープ外（直接参照禁止＋オフライン端末運用で十分とする）。
- [ ] **アーキテクチャ/オフラインテストは直接参照のみを検査**: `GetReferencedAssemblies()` はトランジティブ参照をたどらない。オープンな問い: 将来的に依存グラフ全体を検査できるアーキテクチャテストツール（例: NetArchTest）を採用するかどうかを検討する。

## Phase 1 → Phase 2/3 引継ぎ

- [x] **DailyRecord 多重 New 重複の検知（2026-06-28 クローズ / ADR 0015）**: `DailyRecordConfiguration` に `(RecipientId, ServiceDate) WHERE Kind=1` の partial unique index を追加 (migration `20260628015004_DailyRecordDuplicateNewIndex`)。`DailyRecordDuplicateNewIndexTests` でレース条件下の二重 New 挿入が `DbUpdateException` で拒否されることを検証済。
- [ ] **AppendOnlyGuard と EF Core bulk operations**: `AppendOnlyGuard.Inspect` は ChangeTracker 経由の `Modified`/`Deleted` のみ検出。`ExecuteUpdateAsync`/`ExecuteDeleteAsync` は ChangeTracker を経由しないため検出できない。現在の Repository 実装に bulk 呼び出しはないが、将来追加する際は別途ガードが必要。`ArchitectureTests` で append-only 型に対する bulk 呼び出しを禁止する案あり。
- [ ] **報酬・CSV ハードコード機械判定 (CLAUDE.md §ハード制約 3)**: Phase 1 には報酬算定・CSV 生成のサーフェスが存在しないため、現時点で「単位数/加算/CSV フィールド literal が混入していないこと」を機械判定するテストはエントリポイントを持たない。Phase 3 で報酬テーブル・CSV 生成器を導入する際に以下を同時に追加する: (a) Domain/Application のソース文字列スキャナで `単位数` `加算` `区分単価` 等の語彙が seed JSON 以外に現れたら失敗するテスト、(b) CSV カラム名 literal が `Tsumugi.Infrastructure.Csv` 名前空間以外に現れたら失敗するテスト、(c) 整数 literal の上限ガード（例: 1000 を超える decimal/int literal を Domain 内で禁止）。本項目は Phase 3 着手前のチェックリスト。
- [ ] **Avalonia GUI 目視確認 (AC1-8 補完)**: Phase 1 では `AccessibilityDefaults` の値・適用・XAML 配線を全て CI テストで担保したが、実機起動でのフォント拡大追従、Reduce Motion の Transition 抑止、各 View のタブ順とフォーカス移動は手動 QA でしか確認できない。Phase 2 着手前に macOS/Windows 双方で 1 回ずつ目視チェックする。
- [ ] **OfficeCapability の正式コード集合**: ADR 0006 の通り Phase 1 は `mealProvision` / `transportSupport` のみの暫定キーで運用。Phase 3 で報酬告示と突合して正式コード（食事提供体制加算 I/II、送迎加算 I/II 等）を確定する。
- [x] **`UpdateOffice` / `UpdateRecipient` の actor 監査ログ（2026-06-29 クローズ / Phase 2）**: `UpdateOfficeUseCase` / `UpdateRecipientUseCase` で `IAuditTrail.RecordAsync` を呼び `actor` / `AuditAction.Update` / `TimeProvider` / 要約文字列を `AuditLog` に追記する実装に置換。空 actor は `ArgumentException` で弾く。各 UseCase テストで監査行が追記されることを検証済。
- [ ] **性別など利用者属性の拡張**: 国保連請求 CSV では性別が必須項目の可能性が高い。Phase 1 の `Recipient` は漢字氏名 / カナ氏名 / 生年月日のみ。Phase 3 着手前に CSV インターフェース仕様書で必須項目を洗い出し、enum + migration を発行する。**進捗**: 2026-06-28 に `Certificate` 側に発行時点スナップショットとして `RecipientGender` 等を追加（ADR 0010）。`Recipient` マスタへの拡張は Phase 3 で CSV 仕様確認後に実施。

## Phase 1 受給者証 様式準拠（2026-06-28 追加）

- [ ] **自治体差異**: 受給者証の様式は MHLW 告示で共通項目が定義される一方、自治体ごとに独自の補足欄（例: 通所給食提供時間帯・上限管理事業所の電話番号など）がある。Tsumugi では「主要セクションのみ準拠」とし、自治体独自項目は当面 `SupplyNotes` / `Notes` の自由記述で受ける（ADR 0010）。利用自治体ごとに具体的な追加項目が判明したら個別に enum 化を検討する。
- [ ] **食事提供体制加算 / 高額障害福祉サービス費等の単位数**: Phase 1 では「適用 yes/no」のフラグのみ保持し、金額算定（単位数・利用額）は Phase 3 報酬告示と突合してから実装する（CLAUDE.md §ハード制約 3）。
- [ ] **負担区分の月額上限金額表**: `PaymentBurdenCategory` の各区分（生活保護/低所得/一般1/一般2）の月額上限額（円）は告示で定義される。現状は `MonthlyCostCap` 列に手入力。Phase 3 で区分→上限額の対応テーブルをシード JSON 化し、`PaymentBurden` 設定時に自動入力する案を検討する。
- [ ] **計画相談支援事業者マスタ**: 現状は受給者証ごとに事業者名を自由記述。複数受給者で同じ事業者を参照することが多いため、Phase 3 で `ConsultationProvider` マスタを切り出して FK 参照に変更するか検討する。
- [ ] **ContractedProvider と Contract の整理**: Phase 1 既存の `Contract` は自社事業所の利用契約のみ表現する。新規 `ContractedProvider` は受給者証「サービス事業者記入欄」に書かれる**全契約事業所**（他事業所含む）を網羅する。重複格納を避けたい場合は、自社契約は `Contract` 側のみで管理し `ContractedProvider` には他事業所のみ書く運用も検討する。Phase 2 着手前に運用方針を確定する。

## Phase 1 障害者手帳・フェースシート（2026-06-28 追加）

- [ ] **療育手帳の等級表記**: 自治体ごとに「A1/A2/B1/B2」「1〜4度」「重度/中軽度」など表記が異なるため、`DisabilityCertificate.Grade` は文字列で受けている（ADR 0011）。利用自治体の表記体系が確定したら、自治体ごとの選択肢を `ComboBox` の候補として提供できるよう設定で外部化することを検討する。
- [ ] **精神障害者保健福祉手帳の更新通知**: 2 年ごとの更新が必要。`NextRenewalDate` を保持しているが、期限が近い手帳を一覧でアラート表示するビュー（受給者証期限アラートと同様）は未実装。Phase 2 で追加する。
- [ ] **フェースシートの自治体/事業所独自項目**: 連絡先・医療・受給状況・生活歴の主要グループは実装済みだが、事業所により「行動障害特性」「コミュニケーション手段」「移動手段」「日課」等の追加項目を求める場合がある。利用事業所の様式が判明したら個別項目として追加するか、`AssessmentSummary` の構造化を検討する。
- [ ] **フェースシート履歴の差分表示**: append-only により全バージョン保持されるが、UI からは現行版しか見えない。Phase 2 で「直前バージョンとの差分」「変更履歴一覧」を提供する案あり。
- [ ] **障害者手帳と受給者証の障害種別整合**: 受給者証側の `Disabilities`（身体/知的/精神/難病）チェックと、障害者手帳の `Type` の整合性は現状チェックしていない。例えば「身体手帳のみ所持しているのに受給者証で精神もチェック」というデータが入りうる。Phase 2 で警告（エラーではない）として通知する案を検討する。

## Phase 2 工賃計算（2026-06-28 追加）

- [x] **KouchinModule.bas v5 の実挙動突合（2026-07-05 クローズ / ADR 0012 v2）**: `.xlsm` 検査で方式 Hourly・端数 HalfUp（`ROUND(…,0)` = `BD5`）・`HourUnitMinutes = 15`（`AY9 = 1/96日`）・年度起点 4 月を確認。ADR 0012 を「確定」へ書き換え済み。実装補足として丸めスコープは per-rate 集計後 1 回（.xlsm の per-day BD5 との semantic drift はユーザ承認済み）。
- [ ] **平均工賃月額の正式定義**: 厚労省告示/通知の定義（分母＝延べ利用者 or 実利用者、基準期間、控除項目）を一次情報で確認。確定までは `AverageWageMetric` の暫定式を `[Obsolete("要・通知突合（暫定）")]` 相当のコメントで明示し、テストで形を固定（分母切替に強い構造）する。一次資料入手時にクローズ。
- [x] **特別手当の性格（2026-07-05 クローズ / ADR 0018）**: `.xlsm` の G 列（特別手当）を確認。利用者×月の任意支給であり、`WageAdjustment` append-only エンティティで受ける設計に確定。作業手当・職能手当は `WageSettings` 拡張で表現、特別手当のみ `WageAdjustment` に分離。
- [x] **QuestPDF ライセンス（2026-07-05 クローズ / Phase 4 S1 / ADR 0013 v2）**: Community 継続を確定。一次情報確認 (docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md §15) で就労B型事業所の想定規模との整合を確認。閾値超過時の Avalonia 印刷経路フォールバック計画は ADR 0013 に記載 (実装は別 ADR)。多施設運営法人の consolidated 年商が閾値近傍の場合は再判定すること。
- [ ] **工賃確定後の下層訂正方針**: 自動再計算しない（Correction で履歴に残す）方針を ADR 0012 に併記済。次月調整 or 再確定の手順は運用ガイドへ。
- [ ] **職能手当閾値の妥当性**: `SkillAllowanceTiers` の閾値（55h/70h は 2025 年時点の運用者設定値）。UI から編集可能とする形で対応済みだが、他事業所での標準的な設定値があるか確認する。
- [x] **PDF 帳票の日本語フォント埋込（2026-07-05 クローズ / Phase 4 S1）**: Noto Sans JP (SIL OFL 1.1) Regular / Bold を assets/fonts/ に追加し Reporting アセンブリの EmbeddedResource として同梱。QuestPdfLicenseConfigurator.Initialize() で FontManager.RegisterFont(Stream) 経由で登録し、WageStatementPdfGenerator の DefaultTextStyle で "Noto Sans JP" を全 Document に適用。WageStatementPdfGeneratorTests / WagePaymentListPdfGeneratorTests の CJK 3 系統 + Bold ASCII substring assertion を復活。NOTICE に OFL 帰属追記済 (ADR 0013 v2)。
- [ ] **[Phase 4 S1 final review 由来 / I-1] PDF テキスト抽出時の康熙部首 (Kangxi Radical) 誤置換**: Noto Sans JP を埋め込んだ生成 PDF において、QuestPDF/SkiaSharp が生成する `ToUnicode` CMap が「山」「田」「工」「支」「一」等、一部の漢字を康熙部首 (Kangxi Radicals, U+2F00-2FD5) の対応コードポイントとしてエンコードすることを確認した。**レンダリングされるグリフ自体は正しい**（画面表示・印刷は問題なし）が、`pdftotext` や PdfPig 等の PDF テキスト抽出ライブラリでこれらの文字を含む文字列（例: 利用者氏名「山田太郎」）を抽出すると、部首の文字（⼭⽥等）に置き換わって返る。エンドユーザーが生成済み工賃明細 PDF から利用者名をコピー＆ペーストする、または PDF ビューアの検索機能で氏名検索する場合に、当該漢字を含む氏名で不整合が生じうる。原因は QuestPDF/Skia 側のグリフ→Unicode 逆引きテーブル生成ロジックであり、Tsumugi 側のバグではない（pdftotext でも同一挙動を確認済み、PdfPig 固有ではない）。回避策未定（QuestPDF 側の Issue 報告 or 代替 PDF ライブラリへの切替が必要になる可能性）。AC4-1（日本語フォント埋込）自体は「グリフ描画」の観点で完全達成しているため本件で再オープンしない。テストでの回避策は `tests/Tsumugi.Infrastructure.Reporting.Tests/KangxiRadicalNormalizer.cs` の畳み込みテーブルを参照（テストの成否には影響しない、抽出後の後処理のみ）。関連: ADR 0013 影響節。

## Phase 2 Codex review 由来クローズ（2026-06-29 追加）

- [x] **WageFund 重複 New 検知（2026-06-29 クローズ / ADR 0017）**: `WageFundConfiguration` に `(OfficeId, MonthKey) WHERE Kind=1` の partial unique index を追加 (migration `20260628204038_WageFundDuplicateNewIndex`)。`WageFundDuplicateNewIndexTests` でレース条件下の二重 New 挿入が拒否され、`Correction` は許容されることを検証済。
- [x] **工賃計算 工賃基礎の Present 限定（2026-06-29 クローズ / AC2-5）**: `WageBasisExtractor` を recipient×date の実効 `Attendance=Present` セットでフィルタするよう修正。Absent / AbsenceSupport / DailyRecord なし / Cancellation 後の WorkRecord がいずれも除外されることを `WageBasisExtractorTests` で検証。
- [x] **按分 Σ=原資 不変条件のゼロ重み対応（2026-06-29 クローズ / ADR 0016 / AC2-4）**: `AllocationPolicy.Allocate` の `totalWeight=0 && totalYen>0` 分岐を `RemainderPolicy` 別に決定論で実装（`LargestRemainder` は throw、`ReserveToOffice` は全額留保）。
- [x] **工賃 VM の事業所選択 UI 配線（2026-06-29 クローズ）**: `WageFundSettingsViewModel` / `WageCalculationViewModel` / `WageStatementViewModel` の 3 工賃 VM に `OfficeCapabilityViewModel` パターンで `ListOfficesUseCase` 注入 + `SelectedOffice` バインド + View ComboBox を追加。実 UI から `OfficeId`（および `WageStatement` では `Office`）が設定可能に。
- [x] **PDF 保存ダイアログの UI 配線（2026-06-29 クローズ）**: `IFileSaveService` 抽象 + Avalonia `IStorageProvider` 実装 (`AvaloniaFileSaveService`) を導入し、`WageStatementViewModel` に `SaveSelectedStatementPdfCommand` / `SavePaymentListPdfCommand` を配線、View に保存ボタン 2 つを追加。AC2-7 が VM/View/Service の 3 層で疎通。
- [x] **PDF 出力の決定論化（2026-06-29 クローズ）**: `WageStatementPdfGenerator` の `DateTime.UtcNow` を `TimeProvider` 注入に置換。同一入力＋同一 TimeProvider で同一バイト列を返すことを `Statement_pdf_is_deterministic_for_same_inputs_and_same_timeprovider` で固定。
- [x] **Reporting アセンブリのオフライン直接参照スキャン対象化（2026-06-29 クローズ）**: `OfflineComplianceTests.Tsumugi_assemblies_do_not_reference_network_libraries` の `[Theory]` に `Tsumugi.Infrastructure.Reporting` を追加。
- [x] **Domain ≥95% カバレッジ到達 + CI 強制（2026-06-29 クローズ）**: `WageStatementPolicyTests` / `FaceSheetTests` / `WageStatement.NewRecord/Correction validation` / `YearMonth` boundaries 等を追加し Domain line coverage を 85.83% → **98.03%** に。`build/ci.sh` の Domain 閾値を 70 → **95** に昇格。
