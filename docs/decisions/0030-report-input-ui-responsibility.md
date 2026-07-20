# ADR 0030 — 帳票必須21項目のUI入力責務分割

- Status: Accepted
- Date: 2026-07-20

## 決定

`report-field-mapping-r8-06.json` で `status: "missing"`（=まだ値の入力元がなく、UIから新規に入力させる必要がある）とマークされた21項目は、専用の「帳票入力」画面を新設せず、各項目の`targetModel`が既に持つ既存編集画面へ`uiSurface`の指示どおりに分配する。画面と項目数の対応は次のとおり。

| uiSurface | 画面 | 件数 | 対象targetModel |
| --- | --- | --- | --- |
| CertificateView | 証情報編集画面 | 3 | Certificate |
| OfficeView | 事業所情報編集画面 | 4 | Office |
| ClaimInputView | 請求月次入力画面 | 3 | ClaimInput |
| DailyRecordView | 日次記録編集画面 | 11 | DailyRecord（10）／IntensiveSupportEpisode（1） |

合計21件（`ClaimFieldMappingCompletenessTests.Report_inventory_and_mapping_are_complete_and_bijective`がstatus別件数`missing=21`を固定）。

## 背景

Phase 3-1（Task 9c）で、`report-field-mapping-r8-06.json`の各`status: "missing"`項目に`targetModel`/`targetProperty`/`uiSurface`が付与され、`ClaimInputRequirementProvider`が`uiSurface`文字列を`ClaimInputDestination`（Certificate/ClaimInput/ClaimPreparation/DailyRecord/Office）へ写像するようになった（`ParseDestination`）。この時点で「どの画面が入力責務を持つか」はJSON側のメタデータとして既に確定していたが、実際にUIへ入力欄を追加する作業（および、値が入力されないと`ClaimPreparationReadiness`のfail-closedゲートが正しく機能するかの検証）はPhase 3-2 Task 4〜8で1画面ずつ順に実施した。

- **Task 4（DailyRecordView, 10項目）**: `ServiceStartTime`/`ServiceEndTime`/`SpecialVisitSupportMinutes`/`MedicalCoordinationType`/`TrialUseSupportType`/`RegionalCollaborationApplied`/`EmergencyAdmissionApplied`/`IntensiveSupportApplied`/`OffsiteSupportApplied`/`RecipientConfirmation`はいずれも既存のDailyRecordエンティティのプロパティで、DailyRecordViewの日次編集フォームに入力欄自体は既に存在していた。ここでの主な修正はUI追加ではなく、`ClaimPreparationContextBuilder.rowScopes`が常に空集合だったために`rowPresent(service-performance.daily)`系条件が恒久的にNotApplicableへ縮退していたバグの修正（billedDays由来のrowScope母集団化）と、`ServiceStartTime`/`ServiceEndTime`の`requiredCondition`から自己参照`modelPresent`を除去したことである。
- **Task 5（CertificateView, 3項目）**: `MunicipalityNumber`（常時必須）/`SubsidyMunicipalityNumber`・`UpperLimitManagementProviderNumber`（自己参照条件、spec §10により任意）。後者2つの自己参照条件は意図的にfail-openのまま（値を入れれば入るほど自分自身の条件が満たされるだけの無意味な検査であり、これ自体を強めない）。ただし`UpperLimitManagementProviderNumber`が非nullのとき`ClaimInput.UpperLimitManagementResult`/`UpperLimitManagedAmountYen`を必須化するクロスフィールド規則が、field-mapping-r7-10.json側の自己参照レグとreport-field-mapping-r8-06.json側のクロスフィールドレグの`Any(...)`合流により別途実現されている。
- **Task 6（OfficeView, 4項目）**: `PostalCode`/`Address`/`PhoneNumber`/`RepresentativeTitleAndName`はいずれも常時必須（`always`）。
- **Task 7（ClaimInputView, 3項目のうち1項目）**: `MunicipalSubsidyAmountYen`は当初`uiSurface: "ClaimPreparationView"`だったが、`ClaimPreparationView`は請求プレビューの読み取り専用ステージング画面であり、入力欄を持たない設計だった（brief記載のCRITICAL cascade risk調査で発覚——`Certificate.SubsidyMunicipalityNumber`が非nullのときこのフィールドをfail-closedで必須化するクロスフィールドゲートに到達すると、値を訂正するUI入力欄がどこにも存在せず永久にNotReadyのまま抜け出せなかった）。`targetModel`が`ClaimInput`である以上、同じ`ClaimInput`の他フィールド（`UpperLimitManagementResult`等）を既に編集しているClaimInputViewへ入力欄（NumericUpDown）を追加し、`uiSurface`を`ClaimInputView`へ修正した。これによりCertificate/Office/ClaimInput/DailyRecordの4分割が「1画面1責務」に沿った形で確定した。
- **Task 8（DailyRecordView, 11項目目）**: `IntensiveSupportEpisode.StartDate`は`DailyRecord`とは別の独立entity（`OfficeId`/`RecipientId`/`RootId`/`Revision`/`Kind`/`StartDate`を持つ、利用者単位・追記型の集中的支援エピソード）だが、`uiSurface`は`DailyRecordView`を指す。spec §10の要件（「対象月に`DailyRecord.IntensiveSupportApplied=true`の日があるときのみ`StartDate`必須」）が`DailyRecord`側の値に依存するクロスフィールド規則であること、および実際の入力導線（集中的支援が適用された日を記録する操作者は、その場でエピソード開始日も確認・入力する）が同一ワークフローに属することから、DailyRecordViewの中に「重度支援対象期間」という区別された別セクション（Border区切りのカード）として既に実装済みだった（Phase 3-1時点でエンティティ・UseCase・ViewModel・AXAMLの配線は完了しており、Task 8時点で未着手だったのはUI露出ではなく readiness gate 側のみ）。1画面の中でエンティティ境界ごとにセクションを分けるこの形は、DailyRecordView自身が既に「日次編集カード」と「エピソードカード」を分離している既存パターンと一致しており、1画面1責務の単位を「画面」ではなく「画面内のセクション」で守っている。

readiness gate側では、`report:service-performance:intensive-support:001`（帳票側）と`provider:J611:01:156`（CSV側、field-mapping-r7-10.json）が両方とも自己参照`modelPresent(IntensiveSupportEpisode.StartDate)`のまま同一文字列で一致していたため、`ClaimInputRequirementProvider.CreateRequirement`が単一の`ModelPresent`へ縮退し、Task 4/7のレビューで指摘された「最後のsilently inert候補」として恒久的にfail-openだった。Task 8で帳票側の条件を`rowPresent(service-performance.intensive-support)`（`DailyRecord.IntensiveSupportApplied=true`の実効Present日がある月にのみ`ClaimPreparationContextBuilder`がこのrowScopeを母集団化する、Task 4のdaily:004/005/016と同型のbare rowPresentパターン）へ置換した。CSV側は意図的に未変更（Phase 3-3のCSVスコープ）のまま条件文字列が分岐した結果、`Any(...)`合流により実効fail-closedになった。

## 選択肢

1. **専用「帳票入力」画面を新設**し、21項目を1画面に集約する。
2. **`uiSurface`が指す既存編集画面へ分配**する（targetModelごとに1画面、DailyRecord/IntensiveSupportEpisodeはDailyRecordView内で別セクション）。
3. **タスクごとの場当たり的な配置**（`uiSurface`メタデータを設計指針として使わず、実装時の都合で画面を選ぶ）。

## 決定

**選択肢2**。理由:

- CLAUDE.mdの「1画面1責務。状態を画面間で暗黙共有しない」規約に反する形で、Certificate・Office・ClaimInput・DailyRecord・IntensiveSupportEpisodeという5つの異なるエンティティ境界を1画面（選択肢1）へ集約すると、その画面自体が複数責務を持つことになる。
- `uiSurface`は既にJSONメタデータとして各フィールドに付与済みであり、これを設計指針として使わない（選択肢3）と、画面ごとの入力責務がドリフトし、将来同じ問題（Task 7で発覚したClaimPreparationView集約のミス配置）を繰り返すリスクが高い。
- 選択肢2は既存の1画面1責務構造をそのまま延長するだけで、新規画面を作らずに済む（低リスク・低コスト）。

## 影響

- 新規画面は作成しない。5画面（Certificate/Office/ClaimInput/DailyRecord×2セクション）で21項目全てをカバーする。
- `uiSurface`文字列と`ClaimInputRequirementProvider.ParseDestination`が返す`ClaimInputDestination`の対応は、`ClaimInputRequirementProviderTests`の各テスト（`Provider_registers_*_report_fields`系）で画面ごとに固定済み。
- `MunicipalSubsidyAmountYen`（Task 7）と`IntensiveSupportEpisode.StartDate`（Task 8）は、いずれも「クロスフィールド規則を持つが、値そのものの自己参照条件は意図的にfail-openのまま」という同型のパターンであり、fail-closedの実効性は`Any(...)`合流（クロスフィールドレグ）または`rowPresent`単独条件（行スコープレグ）のいずれかが担う。今後同種のフィールドを追加する際は、まず「本当に自己参照条件だけで十分か（spec §10が値そのものの有無以外の条件を課していないか）」を確認し、クロスフィールド/行スコープ条件が必要なら本ADRのTask 4/7/8と同じ二段構造（`ClaimPreparationContextBuilder`のrowScope/Values母集団化＋`report-field-mapping-r8-06.json`のbare条件）を踏襲する。
- 本ADRは`status: "missing"`の21項目のみを対象とする。`status: "existing"`のまま`ClaimPreparationView`をuiSurfaceに持つ項目（例: `ClaimInput.ExceptionalUsageStartMonth`等）は別contract（既存モデルの値をそのまま参照するだけで新規入力欄を要しない）であり、本ADRのスコープ外。
- CSV側（field-mapping-r7-10.json）の`provider:J611:01:156`等、Phase 3-3のCSV生成スコープに属するフィールドは本ADRの対象外（帳票UI入力責務のみを扱う）。
