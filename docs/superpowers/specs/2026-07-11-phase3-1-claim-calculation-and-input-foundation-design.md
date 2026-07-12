# Tsumugi Phase 3-1 設計 — 請求計算・入力基盤

> **Source**: `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`、ADR 0020〜0026、`docs/phase3-0-acceptance.md`
> **Status**: 設計合意済（2026-07-11）/ 実装中（2026-07-12、現行計画Tasks 2〜10実装済み、Phase 3-1未受け入れ）
> **Execution boundary**: 2026-06-29付けの旧Phase 3-1計画は実行対象外。実行正本は [`docs/superpowers/plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md`](../plans/2026-07-11-phase3-1-claim-calculation-and-input-foundation.md) とする。

## 1. 目的

Phase 3-0で確立した出典契約、版付きマスタ境界、CSV仕様アセンブリ、append-only `ClaimBatch` / `ClaimDetail`、fail-closedの確定ストアを利用し、Phase 3-1の次の受け入れ基準を一括で満たす。

- AC3-1: 令和6・令和8の単位数、加算・減算、地域単価、負担上限を適用年月版マスタから供給する。
- AC3-2: 平均工賃正式式、令和8区分、経過措置をマスタ駆動で解決する。
- AC3-3: 基本報酬、加算・減算、利用者負担を公式ケースと一致させる。
- AC3-4: 2026-05 / 2026-06の境界でコード変更なしに適用版を切り替える。
- AC3-8: Phase 3-0で確定した51 mapping entries / 26 implementation targetsをモデル、migration、実UIへ追加する。
- AC3-9: 請求確定を`New` / `Correction` / `Cancel`のappend-only履歴として実運用可能にする。

Phase 3-1は複数の依存スライスに分けて実装するが、最終的な受け入れは上記ACの一括判定とする。

## 2. ハード制約

- 制度値、サービスコード、割合、地域単価、区分境界をC#へ直書きしない。
- 金額は非負整数円、単位数は整数、地域単価・割合・丸め前中間値は`decimal`とし、`double` / `float`を使用しない。
- 未入力を`false`、`0`、空文字、自由記述から推測しない。
- 下層データの訂正で確定済み請求を自動更新しない。
- 入力不足、版不明、履歴不整合、未知コード、overflowでは請求結果を生成しない。
- UI、ログ、例外へ氏名、受給者証番号、snapshot JSON、保存先フルパスを出さない。
- 伝送、電子証明書、回線処理、返戻・過誤再請求の自動化を実装しない。
- Phase 3-2の帳票生成、Phase 3-3のCP932 / CRLF CSV生成を先行実装しない。

## 3. スコープ集合の区別

### 3.1 AC3-8の出力項目集合

`docs/phase3-claim-field-mapping.md`が正本であり、51 mapping entriesを次の26 implementation targetsへ集約する。この集合は帳票・CSVへ出力する不足項目であり、件数を算定入力契約の件数と混同しない。

| モデル | 対象プロパティ |
| --- | --- |
| `Certificate` | `MunicipalityNumber`、`SubsidyMunicipalityNumber`、`UpperLimitManagementProviderNumber` |
| `ClaimInput` | `UpperLimitManagementResult`、`UpperLimitManagedAmountYen`、`MunicipalSubsidyAmountYen`、`ExceptionalUsageStartMonth`、`ExceptionalUsageEndMonth`、`ExceptionalUsageDays`、`StandardUsageDayTotal` |
| `ContractedProvider` | `CertificateEntryNumber` |
| `DailyRecord` | `ServiceStartTime`、`ServiceEndTime`、`SpecialVisitSupportMinutes`、`OffsiteSupportApplied`、`MedicalCoordinationType`、`TrialUseSupportType`、`RegionalCollaborationApplied`、`IntensiveSupportApplied`、`EmergencyAdmissionApplied`、`RecipientConfirmation` |
| `IntensiveSupportEpisode` | `StartDate` |
| `Office` | `PostalCode`、`Address`、`PhoneNumber`、`RepresentativeTitleAndName` |

### 3.2 AC3-2 / AC3-3の算定入力契約

ADR 0022、0023、0025は、26 targetsとは別に次の構造化入力を要求する。これらは出力fieldIdの追加ではなく、平均工賃、経過措置、法31条特例、利用者負担を正しく算定するための根拠である。

- 前年度の年間工賃支払総額、年間延べ利用者数、年間開所日数、年度完全性、日別・月別への逆引き根拠。
- `AverageWageBandOption`の有効登録、`R8ReformStatus`、過去区分比較、新規指定日・支援開始日、`FiledTransition`の対象期間と根拠文書。
- 受給者証上限の入力済み状態、原本参照、確認日時、確認者、確認根拠。
- `Article31SpecialBurdenStatus`、特例額、有効期間、原本参照、確認証跡。
- 上限額管理対象の三値、正式な管理結果票header、事業所行、合計、確定状態、原本参照。

これらを26 targetsへ水増しせず、別の永続モデル・DTO・UI・readiness規則として管理する。

## 4. アーキテクチャ

Phase 3-1は次の依存順5スライスで実装する。

1. **請求準備入力**: 26 targets、算定根拠入力、migration、入力UI、補完・訂正経路、readinessを完成する。
2. **制度マスタ実値**: 現在空の5種のseedへ適用版実値とsource locatorを投入し、版解決を完成する。
3. **純粋算定**: 平均工賃、区分、基本報酬、加算・減算、地域単価、利用者負担をDomainの純粋関数として実装する。
4. **プレビュー・差分**: 一貫した入力snapshotからreadiness、算定、preview hash、確定済みとの差分を返す。
5. **validated finalization**: production codecとreaderを登録し、`CloseClaimUseCase`から既存`ClaimFinalizationStore`へ接続する。

各スライスは前スライスの契約を入力にし、前提が未完成なら後続を解禁しない。

2026-07-12時点ではTasks 2〜10の入力モデル、永続化、保存ユースケース、typed requirements、readiness gateまで実装済み。Task 11の実UIが未実装のため、請求準備入力スライス全体は未完了である。

## 5. データモデル

### 5.1 既存モデル拡張

- `Certificate`、`ContractedProvider`、`DailyRecord`、`Office`へ3.1の対象を追加する。
- 既存行を推測移行しないため、追加列はnullableで作成する。
- 時刻は`TimeOnly?`、日数・分数・金額は`int?`、明示選択が必要な真偽値は`bool?`、公式コードは`Unspecified`を持つ専用enum又は値オブジェクトとする。
- `DailyRecord`は既存の`NewRecord` / `Correction` / `Cancellation`と実効版選択を拡張する。
- `Office`と`ContractedProvider`は同一性マスタとして楽観的同時実行を維持する。
- `Certificate`はappend-onlyガード対象のため直接更新しない。`RootCertificateId`、`Revision`、`ExpectedHeadCertificateId`を追加し、選択したrootのheadを複製・補完する`CorrectCertificateUseCase`で新しいrevisionを追記する。
- lineage migrationでは既存の各Certificateを独立rootとして、`RootCertificateId = Id`、`Revision = 1`、`ExpectedHeadCertificateId = null`へ決定的に設定する。これは請求値の推測ではなく内部履歴metadataの初期化であり、この3列のうちrootとrevisionはmigration後non-nullとする。
- 新規rootも`RootCertificateId = Id`、`Revision = 1`、expected headなしとする。訂正版はroot IDを維持し、`Revision = head.Revision + 1`、`ExpectedHeadCertificateId = head.Id`とする。
- DBは`(RootCertificateId, Revision)`と非nullの`ExpectedHeadCertificateId`を一意化し、revision 1のroot自己参照・expected headなし、revision 2以降のexpected head必須をcheck constraintとPolicyで検証する。これにより同じheadからの分岐を拒否する。
- 請求用の`CertificatePolicy.EffectiveVersion`はrevisionが連続した単一chainだけを解決し、`CreatedAt`又は`Guid`の大小で候補を任意選択しない。同じサービス日に有効なroot chainが複数ある、revisionが分岐・欠落する、expected headが一致しない場合はreadinessで拒否する。
- 既存行はすべてrevision 1のrootになるため、`CorrectCertificateUseCase`の起点にできる。重複期間を持つ複数rootをmigrationで統合せず、利用者が正しいrootを補完・訂正しても請求日に複数rootが有効ならreadinessで停止する。

### 5.2 `ClaimInput`

`ClaimInput`は`(OfficeId, RecipientId, ServiceMonth)`単位の月次請求固有入力とする。

- 3.1の7プロパティを保持する。
- 上限額管理結果票の識別・確定状態・原本参照と整合できる関連を持つ。
- revision、`RecordKind`、root、expected headを持つappend-only履歴とする。
- `ClaimInputPolicy`は取消後の再入力を次revisionとして許可し、`ClaimBatchPolicy`の「Cancel後は永久終了」を流用しない。
- 同じrevisionの重複、欠落、分岐、stale headを拒否する。

### 5.3 `IntensiveSupportEpisode`

`IntensiveSupportEpisode`は`OfficeId`、`RecipientId`、`StartDate`を持つ独立したappend-only入力とする。`DailyRecord.IntensiveSupportApplied`から開始日を推測しない。訂正・取消・再入力は専用Policyで履歴を検証する。

### 5.4 算定根拠モデル

責務を混ぜず、少なくとも次の構造を分離する。

| 構造 | 責務 |
| --- | --- |
| `AverageWageAnnualEvidence` | 対象年度、年間工賃、年間延べ利用者、年間開所日、完全性、日別・月別根拠 |
| `OfficeClaimProfile` | 有効な体制option、`R8ReformStatus`、指定・支援開始日、経過措置、指定権者確認証跡 |
| `CertificateClaimEvidence` | 証上限入力状態、原本参照、法31条特例の三値・金額・期間・確認証跡 |
| `UpperLimitManagementStatement` | 正式結果票header、事業所行、合計、確定状態、原本参照 |

これらは期間又は月単位のappend-onlyスナップショットとし、既存の0、null、`SupplyNotes`から正式状態を生成しない。

## 6. 請求準備readiness

`ClaimPreparationReadiness`を算定前の唯一のゲートとする。

- Applicationに`IClaimInputRequirementProvider`を置き、Infrastructure.Csv実装がfield mappingをtyped requirementへ変換する。
- ApplicationはCSV仕様の文字列DSLやInfrastructure型へ依存しない。
- `ClaimPreparationIssue`は安全な識別子、項目コード、理由コード、修正先`AppSection`と最小contextを持つ。
- 常時必須、該当時必須、明示的不適用、正式な0を区別する。
- 入力不足だけでなく、実効版の複数候補、履歴破損、マスタ版欠落、source不整合も停止理由にする。
- issueが1件でもあれば算定を呼ばない。

## 7. 一貫した入力snapshot

複数Repositoryの逐次読取りで異なる時点のデータが混在しないよう、`IClaimPreparationSnapshotReader`をApplicationに置く。

- Infrastructure実装は`IDbContextFactory<TsumugiDbContext>`からoperation-local DbContextを生成する。
- SQLite read transaction内で、対象月の全入力、全履歴、現在のClaim headを読み切る。
- transaction外でlazy load又は追加Repository queryを行わない。
- 同じ読取り操作中に適用マスタ版とsource SHAを1回だけ解決する。
- 利用者、日付、サービスコードを安定順に並べ、immutable `ClaimPreparationSnapshot`を返す。
- `CalculateClaimUseCase`と、`New` / `Correction`を行う`CloseClaimUseCase`の両方が必ずこのreaderを使用する。`CancelClaimUseCase`は下層入力snapshotを必要とせず、確定済みClaim履歴だけを読む。

snapshot取得後の訂正は、当該snapshotを過去の一貫した状態として維持し、次回queryで差分として検出する。

## 8. 純粋算定パイプライン

Domainの算定順はADR 0023 / 0025から変更しない。

```text
平均工賃正式式
  -> PaymentBand / 実在service-code解決
  -> PerServiceCodeUnit割合を都度四捨五入
  -> 整数単位 × 回数
  -> サービス種別ごとの月次単位合算
  -> MonthlyTargetUnitSum割合を四捨五入して反映
  -> 最終月次給付単位数 × decimal地域単価を円未満切捨て
  -> 総費用額の1割相当を円未満切捨て
  -> 法31条特例
  -> 証上限・制度上限
  -> 同一事業所内の公式順による利用者負担調整
  -> 正式上限額管理結果
  -> 総費用額 - 決定利用者負担額 = 給付費
```

主な純粋コンポーネントは次とする。

- `AverageWageCalculator`
- `PaymentBandResolver`
- `ServiceCodeResolver`
- `PercentageAdjustmentCalculator`
- `RoundingPolicy`
- `RegionalCostCalculator`
- `Article31SpecialBurdenPolicy`
- `BurdenCalculator`
- `ClaimCalculator`

各結果は`calculationStepId`、必要な`roundingRuleId`、master version、source document IDs、source rowを保持する。選択、整数乗算、合算、`min`、減算を丸め関数へ混ぜない。

## 9. プレビュー、hash、差分

公開Application APIは上位契約どおり`CalculateClaimUseCase`とし、`PrepareClaimUseCase`へのrename又は併存は行わない。`CalculateClaimUseCase`は次の順に処理する。

1. 一貫した`ClaimPreparationSnapshot`を取得する。
2. readinessを評価する。
3. 適用版のtyped master rowsを解決する。
4. Domainの純粋算定を実行する。
5. version付き入力snapshotと算定snapshotをcanonical化する。
6. 請求関連値だけから`PreviewHash`を生成する。

canonical hashには現在時刻、UI状態、表示順、ローカライズ済み文言を含めない。利用者、日付、サービスコード、計算stepは安定順に並べる。

`QueryClaimUseCase`は確定済みsnapshotと現在の再計算結果を、利用者、サービスコード、計算step単位で比較する。下層データを確定済みClaimBatchへ反映せず、差分だけを返す。

## 10. validated finalization

### 10.1 `New` / `Correction`

`CloseClaimUseCase`は`New` / `Correction`だけを担当し、画面計算値を入力として受け取らない。入力は`OfficeId`、`ServiceMonth`、`PreviewHash`、`RecordKind`、expected head、actor、operation IDに限定し、`RecordKind.Cancel`を拒否する。

1. `IClaimPreparationSnapshotReader`で最新の一貫したsnapshotを再取得する。
2. readinessと算定を再実行する。
3. canonical hashが画面の`PreviewHash`と一致しなければ`StalePreview`で停止する。
4. production snapshot codecで入力・算定snapshotを検証する。
5. `ValidatedClaimSnapshotEnvelope`とtyped `ClaimFinalizationDraft`を作る。
6. 既存`IClaimFinalizationStore.CommitAsync`へ渡す。

production codecは現行schemaを書込み可能とし、登録済み旧schemaを読取り可能にする。未知schema、未知codec、hash不一致、非canonical payloadを拒否する。Phase 3-1完了時に`UnavailableClaimSnapshotValidationCodecRegistry`をproduction registryへ置換する。

### 10.2 `Cancel`

`CancelClaimUseCase`を別経路として追加する。入力は`OfficeId`、`ServiceMonth`、expected head、actor、operation IDに限定し、現在のCertificate、DailyRecord、ClaimInput等のreadiness、再算定、`PreviewHash`を要求しない。

1. 確定済みClaim履歴を読み、実効headが存在して`Cancel`でないこと、expected headが一致することを確認する。
2. rootは初代`New`を指し、expected headには現在の実効headを指定する。revisionをUseCase側で採番しない。
3. claim master、CSV仕様、帳票仕様、snapshot applicationの4版を実効headからコピーする。
4. detailsなし、全合計0の`ClaimFinalizationDraft`を作る。
5. 既存`IClaimFinalizationStore.CommitAsync`へ渡す。storeがnon-deferred write transaction内で全履歴を再検証し、`max + 1`を採番する。同じoperation IDのreplayもstoreが処理する。

これにより、下層入力が不足又は破損していても、既に確定した請求を安全に取下げできる。

## 11. エラー処理

- 入力不足は例外ではなく`ClaimPreparationResult.Issues`で返す。
- 制度・履歴・算定・競合は閉じたerror codeで表す。
- 少なくとも`MasterVersionUnavailable`、`UnknownServiceCode`、`InvalidEffectiveHistory`、`CalculationOverflow`、`StalePreview`、`NavigationTargetUnavailable`を区別する。
- Appの`ClaimErrorMessageProvider`がcodeを固定の日本語へ変換し、任意の`Exception.Message`をそのまま表示しない。
- `ClaimFinalizationException`の既存sanitized error contractを維持する。
- ログにはoperation ID、error code、安全な技術メタデータだけを出す。

## 12. UIとtyped navigation

### 12.1 画面責務

- `ClaimInputView`: 事業所、対象月、利用者単位で`UpperLimitManagementResult`、`UpperLimitManagedAmountYen`、上限額管理結果票、法31条特例、証上限確認等の請求固有入力、根拠入力、revision履歴を扱う。
- `ClaimPreparationView`: 事業所・サービス月選択に加え、field mapping正本どおり`MunicipalSubsidyAmountYen`、`ExceptionalUsageStartMonth`、`ExceptionalUsageEndMonth`、`ExceptionalUsageDays`、`StandardUsageDayTotal`を入力する。readiness、不足一覧、プレビュー、確定済みとの差分、`New` / `Correction`と専用`Cancel`操作も扱う。
- `CertificateView`、`DailyRecordView`、`OfficeView`: それぞれの26 targetsと補完経路を扱う。
- Phase 3-2帳票、Phase 3-3 CSVの未実装ボタンは置かない。

### 12.2 navigation

現行固定TabControlへViewModel同士の参照を追加しない。

- Appに`AppSection`、`NavigationRequest`、`IAppNavigationService`を追加する。
- `MainViewModel.SelectedSection`を`TabControl`へ双方向バインドする。
- navigation requestは移動先と必要最小限の`RecipientId`、`ServiceDate`、`CertificateId`、`OfficeId`、`ServiceMonth`だけを持つ。
- 各ViewModelは自分向けcontextを読み込み、他ViewModelを参照しない。
- 移動失敗時もreadiness issueを消さず、typed errorを表示する。

### 12.3 操作制御

readiness未通過、プレビューなし、stale preview、expected head不一致では確定操作を無効化する。UI状態をセキュリティ境界にせず、UseCase側でも同じ検査を必須にする。更新、保存、プレビュー、確定をキーボードで完結できるようにする。

## 13. テスト戦略

### 13.1 TDDとスライス別検証

全論理変更をRed -> Green -> Refactorで実装する。

- 入力: 51 entriesと26 targetsの集合一致、条件付き必須、既存null、補完、訂正、取消、再入力、migration往復、navigation。
- マスタ: 2024-04 / 06、2025-01 / 09、2026-06境界、source SHA、空白・重複・未知・不連続、版外option。
- 算定: 公式ケース、中間値、最終値、端数位置、割合順、2026-05 / 06、0除算、上限、overflow、未知経過措置。
- snapshot: read transactionの一貫性、安定順、canonical hash、不変値・変化値の境界。
- codec / 確定: round-trip、改ざん、旧版読取り、現行版書込み、New / Correction / Cancel、replay、stale preview、expected head競合。
- App: readiness表示、修正先navigation、ボタン制御、UseCase再検証、キーボード操作。

### 13.2 二重オラクル

production seedと期待fixtureを同じ転記から作らない。

- production seedの各行はsource document ID、SHA-256、sheet / row / cell又は物理頁を保持する。
- seed全件をテストfixtureへ複製しない。
- 算定結果は公式資料の公開計算例を独立golden caseとして検証する。
- 公式計算例のないマスタ行は、source locator付き転記一覧を生成し、実装担当とは別のレビューで一次資料と全件照合する。
- マスタ件数、キー集合、期間、参照整合、訂正関係は機械検査する。
- 値だけが変わりsource SHA又はlocatorが変わらない変更を境界テストで拒否する。

### 13.3 品質ゲート

証跡コマンドは共有build artifact競合を避けて逐次実行する。

- targeted Domain / Application / Infrastructure / App tests
- migration up / down / upとrepository round-trip
- `dotnet format --verify-no-changes`
- `./build/ci.sh`
- `dotnet list package --vulnerable --include-transitive`
- Domain line coverage 95%以上
- Application line coverage 90%以上へCI下限を引き上げる
- `Logic.Claim` branch coverage 100%
- offline、dependency direction、制度値hardcode境界を意図的違反で赤化確認

## 14. 受け入れ条件

Phase 3-1は次の全項目を満たしたときだけ受け入れる。

1. 26 implementation targetsと算定根拠入力がモデル、migration、実UIで入力可能。
2. 既存データを推測移行せず、readinessから補完経路へ到達可能。
3. 令和6・令和8の制度マスタ実値が出典・版・locator付きで解決可能。
4. 2026-05 / 2026-06境界で無改修に版が切り替わる。
5. 公式計算例と全中間値・最終値が一致する。
6. 不足、未知、履歴破損、overflow、stale previewがfail closedになる。
7. previewとcloseが一貫した入力snapshotを使用する。
8. production codecがvalidated snapshotだけを書き込む。
9. `New` / `Correction` / `Cancel`、replay、expected head競合が既存finalization storeで正しく処理される。
10. すべての品質ゲートと独立転記レビューが完了する。

## 15. 主なリスクと対策

| リスク | 対策 |
| --- | --- |
| production seedとfixtureの共通誤転記 | 二重オラクル、公式golden case、独立全件照合 |
| 複数Repositoryの読取り時点混在 | operation-local read transactionを所有するsnapshot reader |
| 既存nullを正式なfalse / 0へ誤認 | nullable + entered state + 原本参照 + readiness |
| Certificate補完がappend-onlyガード違反 | 新しい版を追記し、実効版選択を明文化 |
| ClaimInput取消後に再入力不能 | ClaimBatchPolicyを流用せず、専用revision policyを使用 |
| 修正先画面への移動がViewModel間参照になる | typed navigation coordinator |
| プレビュー金額の改ざん又は古期化 | 金額をCloseへ渡さず、最新snapshot再算定とhash比較 |
| 公式正誤・差替えの未反映 | source SHA、supersedes / corrects、適用期間を新releaseとして追記 |

## 16. 参照

- `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md`
- `docs/superpowers/specs/2026-06-29-phase3-claim-data-design.md`
- `docs/phase3-0-acceptance.md`
- `docs/phase3-claim-field-mapping.md`
- `docs/decisions/0020-claim-master-sources-and-versioning.md`
- `docs/decisions/0021-office-capability-official-codes.md`
- `docs/decisions/0022-burden-cap-master.md`
- `docs/decisions/0023-average-wage-and-r8-transition.md`
- `docs/decisions/0024-kokuhoren-csv-and-field-mapping.md`
- `docs/decisions/0025-claim-rounding-rules.md`
- `docs/decisions/0026-claim-batch-snapshot.md`
- 厚生労働省「令和6年度障害福祉サービス等報酬改定について」: https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00009.html
- 厚生労働省「令和8年度障害福祉サービス等報酬改定について」: https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000202214_00013.html
- 厚生労働省「報酬算定構造・サービスコード表等」: https://www.mhlw.go.jp/stf/seisakunitsuite/bunya/0000174644_00022.html
