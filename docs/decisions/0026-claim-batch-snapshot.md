# ADR 0026: 請求確定スナップショットと追記履歴

**Status**: Accepted（2026-07-10）

## Context

請求の確定後に`Office`、`Recipient`、受給者証、日次記録又は報酬マスタが訂正されても、提出済み又は提出予定の請求内容は自動で変わってはならない。一方、再確定、取下げ、制度改定、アプリ更新及びsnapshot schema更新後も、どの入力、規則、版及び出典で確定したかを検証し、既存の確定請求から帳票とCSVを再生成できる必要がある。

既存の`AppendOnlyChainPolicy`は`OriginId`が直前レコードを指すhop-by-hop連鎖である。請求は、全`Correct`／`Cancel`が初代`New.Id`を直接指すroot-origin lineageとするため、このpolicyを流用できない。また、現行`IUnitOfWork.SaveChangesAsync`だけでは履歴読込から保存までを同一transactionに含めないので、並行確定の契約としては不十分である。

2026-06-29付けのPhase 3-1〜3-3計画にある、`Cancel`が直近レコードを参照する例、明細行単位の`ClaimDetail`、帳票／CSV生成時に現行`Office`や`Recipient`等を再読込する例は採用しない。現行化済みPhase 3設計、ADR 0020、0024、0025及び本ADRを優先する。

## Decision

### 1. `ClaimBatch`のlineage

`ClaimBatch`の粒度は`(OfficeId, ServiceMonth)`とし、既存`RecordKind`の実値`New = 1`、`Correct = 2`、`Cancel = 3`を使う。

- 初回確定だけを`New`とし、1 lineageにちょうど1件だけ許可する。`New.OriginId`はnullとする。
- 再確定は`Correct`を追記する。全`Correct.OriginId`は直前版ではなく初代`New.Id`とする。
- 取下げは`Cancel`を追記する。全`Cancel.OriginId`も初代`New.Id`とする。
- root以外のID、別`OfficeId`又は別`ServiceMonth`のrootを`OriginId`に指定してはならない。
- `Correct`は差分ではなく、その時点の受給者集合を含む完全なheader＋全`ClaimDetail` snapshotとする。
- `Cancel`は`ClaimDetail`を持たず、`TotalUnits`、`TotalCostYen`、`TotalBenefitYen`、`TotalBurdenYen`を全て0とする。版情報は取り消した実効版からコピーし、現在の版へ置換しない。

`ClaimBatch`は少なくとも次を保持する。

```text
OfficeId / ServiceMonth / Kind / OriginId
TotalUnits / TotalCostYen / TotalBenefitYen / TotalBurdenYen
ClaimMasterVersion / CsvSpecificationVersion / ApplicationVersion
FinalizationOperationId / OperationPayloadSha256
Entity.Id / CreatedAt / CreatedBy / ConcurrencyToken
```

`FinalizationOperationId`は確定、再確定又は取下げの1操作を識別する非空`Guid`であり、全batchで一意とする。`OperationPayloadSha256`は、操作種別、`OfficeId`、`ServiceMonth`、root、`expectedEffectiveBatchId`、操作者、版、合計及び`RecipientId`順のdetail snapshot hashを含む正規化済み操作payloadのSHA-256（小文字64桁hex）とする。生成予定のbatch／detail IDと`CreatedAt`は再試行ごとに変わり得るためpayloadへ含めない。

### 2. 履歴の検証と実効版

専用の`ClaimBatchPolicy`はheader履歴を`CreatedAt`昇順、同時刻を`Id`昇順に並べてから検証する。SQLiteでは`DateTimeOffset`の順序をSQLへ委ねず、対象`OfficeId`／`ServiceMonth`で絞り込んだ後にこの順序を適用する。保存時と読込時のsnapshot aggregate validatorは、このpolicyに加えて対応details、JSON及び合計を検証する。

次のいずれかをpolicy又はsnapshot aggregate validatorが検出した場合は履歴全体を不正として例外にし、実効版、帳票又はCSVを返さない。

- 空でない履歴に`New`がない、又は`New`が複数ある
- `New`より前に並ぶ`Correct`／`Cancel`、`New.OriginId != null`
- 孤立した`Correct`／`Cancel`、空の`OriginId`、初代`New.Id`以外への参照
- 異なる`OfficeId`又は`ServiceMonth`の混在
- `Cancel`後の`Correct`、`Cancel`後の`Cancel`、複数`Cancel`
- 未知の`RecordKind`、負の合計、`Cancel`の非0合計又はdetail保持
- `New`／`Correct`のheader合計と受給者別detail合計の不一致
- 重複`FinalizationOperationId`、不正なoperation hash、同一operation IDに対する異なるpayload

検証済み履歴が空なら実効版はない。末尾が`New`又は`Correct`ならその末尾を実効版とし、末尾が`Cancel`なら実効版なしとする。時刻だけ又はDBの取得順だけで選択しない。`AppendOnlyChainPolicy`は呼ばない。

### 3. 並行確定、precondition及び再試行

確定操作は、Infrastructureが所有する単一の明示transactionで次を実行する。

1. SQLiteのwrite lockを先に取得する非deferred transactionを開始する。現行providerでは`Microsoft.Data.Sqlite.SqliteConnection.BeginTransaction(deferred: false)`相当を使い、EF Core contextを同transactionへ参加させる。
2. transaction内で対象履歴と、同じ`FinalizationOperationId`の既存batchを再読込する。
3. 同じoperation IDが既に存在する場合は、そのdetailsとenvelope hashも検証してoperation hashを再計算する。再計算値、要求payloadのhash、`OfficeId`、`ServiceMonth`、`Kind`、`OriginId`及び`CreatedBy`が全て一致すれば、追記せず既存結果を返す。一つでも異なればoperation ID衝突又は保存済みsnapshot破損として拒否する。
4. 新規操作なら履歴を`ClaimBatchPolicy`で検証し、コマンドのpreconditionを確認する。
5. batch、全details及びPIIを含まない`AuditEntry`を追跡し、1回の`SaveChanges`で保存してcommitする。

`New`は`expectedEffectiveBatchId = null`かつ履歴なしを必須とする。`Correct`／`Cancel`は、呼出元が最後に表示した実効batch IDを`expectedEffectiveBatchId`として必須指定し、transaction内の実効batch IDと完全一致しなければ競合として拒否する。`Correct`は実効版が`New`又は`Correct`の場合だけ、`Cancel`も実効版が存在する場合だけ許可する。

`ClaimBatches(OfficeId, ServiceMonthKey) WHERE Kind = 1`のpartial unique indexと`FinalizationOperationId`のunique indexをtransaction規律の二重防御とする。並行する異なる操作はwrite lockで直列化され、後続操作は先行commit後の履歴を検証する。したがって、`Cancel`が先に確定した後の`Correct`は拒否され、`Correct`後の明示`Cancel`は許可される。

SQLite busy又はconstraint errorではtransaction全体をrollbackし、ChangeTrackerを破棄する。自動再試行する場合は同じoperation IDと同じpayloadでtransaction全体を最初から実行する。commit結果が不明な場合も同じoperation IDで再送し、上記の同一性検査で既存結果を回収する。失敗した`New`を暗黙に`Correct`へ、失敗した`Correct`を別の`Correct`へ変換してはならない。

### 4. 受給者単位のsnapshot

`ClaimDetail`は明細行単位ではなく受給者単位とし、1つの`ClaimBatch`内で各`RecipientId`を最大1件とする。少なくとも次を保持する。

```text
ClaimBatchId / RecipientId / SnapshotSchemaVersion
InputSnapshotJson / CalculationSnapshotJson
TotalUnits / TotalCostYen / BenefitYen / BurdenYen
Entity.Id / CreatedAt / CreatedBy / ConcurrencyToken
```

`InputSnapshotJson`は確定時に検証した事業所、受給者、受給者証、契約、日次実績、請求固有入力及び適用期間を含む。`CalculationSnapshotJson`は受給者別の算定行、集計、負担及び給付を含み、次の追跡情報を省略しない。

- 使用したclaim master、CSV仕様及びapplicationのversion
- 各算定段階の`calculationStepId`と、端数処理を行う段階だけの`roundingRuleId`
- `percentageBaseScope`、selector、適用順、丸め前後値及び公式service-code row
- `sourceDocumentId`、source SHA-256及びphysical page／row locator

headerの版とdetail内の版又はsource束が矛盾する場合は確定も読込も拒否する。ADR 0020の未登録source、ADR 0024の未登録CSV仕様版、ADR 0025の未登録rule／stepへフォールバックしない。

Phase 3-1では入力と算定結果を不可変の型付きrecordとして定義し、そのrecordからだけsnapshot JSONを生成する。Domain entityはJSON文字列の空白、schema version及び合計を検証するが、JSONを解釈しない。

### 5. 決定論的JSONとschema互換性

各snapshot JSONは次のenvelopeとする。

```json
{
  "schemaVersion": "claim-snapshot-v1",
  "payloadSha256": "64 lowercase hex characters",
  "payload": {}
}
```

`payloadSha256`はcanonical payloadのUTF-8 bytesに対するSHA-256であり、JSON全体の自己参照hashではない。canonical化はschemaごとのcodecで次を固定する。

- UTF-8、BOMなし、改行とindentなし、property順はschemaで固定する
- property名、enum値及び版付きIDは安定文字列とし、現在の表示名から生成しない
- nullとdefault値を省略せず、同じ型で常に同じproperty集合を出す
- 配列は意味上の安定keyで並べ、順序不定のdictionaryを直接serializeしない
- 整数はJSON整数、`decimal`は二進浮動小数を経由せずschema指定のscaleで正規化する
- `DateOnly`、`DateTimeOffset`、年月はschema指定のInvariantCulture／UTC表現に固定する
- 未知property、重複property、非有限値、範囲外値及びtrailing tokenを拒否する

readerはpayloadを再canonical化してhashを照合するだけでなく、envelope全体を再serializeしたUTF-8 bytesが保存文字列のUTF-8 bytesと一致することも確認する。意味が同じでも非canonicalなproperty順、数値表現又は余分な空白を保存済み正本として受け入れない。

`SnapshotSchemaVersion`列、両envelopeの`schemaVersion`及び選択したcodec versionは一致しなければならない。Applicationにはversion dispatcher／codec registryを置き、一度リリースしたschema versionの型付きreader、canonicalizer及びvalidatorを削除又は別の意味へ変更しない。既知の旧版はその版のcodecでhash検証、deserialize及び意味検証を行い、新版へin-place変換せずそのまま帳票／CSV入力へ復元できるよう維持する。

未知schema version、既知版のhash不一致、deserialize失敗、schema違反、header/detail合計不一致又はcodec欠落はフェイルクローズする。JSON本文を修復、既定値補完、現行schemaとして再解釈又は下層データから再計算してはならない。

旧schemaを新schemaへ変換する必要がある場合は、検証済み旧snapshotを旧codecで読んで明示的なversioned converterへ渡し、新しい完全snapshotを`Correct`として追記する。既存行は更新せず、schema変換のために現行`DailyRecord`、`Certificate`、`Office`等を再読込しない。業務データ訂正に基づく通常の再確定は別の明示操作であり、新しい入力を計算して完全な`Correct`を追記する。

### 6. 帳票／CSVの再現規律

帳票とCSVは、検証済み実効`ClaimBatch`及びその`ClaimDetail`の確定時JSONだけを読む。生成時に現行`Office`、`Recipient`、`Certificate`、`Contract`、`DailyRecord`、報酬master又は自由記述を再読込しない。

- 帳票はsnapshotに記録されたversionと対応codec／rendererを選ぶ。対応版がなければ最新rendererへ暗黙移行せず停止する。
- CSVはsnapshotに記録された`CsvSpecificationVersion`を使い、独立入力の`ProcessingMonth`を追加する。`ServiceMonth`から推測しない。
- 同じ実効batch、同じ出力版、同じ明示入力及び同じ決定論的clock入力から同じbytesを生成する。
- `Cancel`が末尾なら実効版がないため、帳票／CSV生成を拒否する。
- `Correct`後はその完全snapshotだけを使い、初代`New`との差分を合成しない。

### 7. EF Core／SQLite制約

現行の`Guid`主キー、`DateTimeOffset CreatedAt`、`string CreatedBy`、`Guid ConcurrencyToken`及び`ServiceMonth -> YYYYMM int`変換に合わせ、次を構成する。

```csharp
builder.HasIndex(x => new { x.OfficeId, x.ServiceMonth })
    .HasFilter("\"Kind\" = 1")
    .IsUnique()
    .HasDatabaseName("UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly");

builder.HasIndex(x => x.FinalizationOperationId)
    .IsUnique()
    .HasDatabaseName("UX_ClaimBatches_FinalizationOperationId");

builder.HasIndex(x => new { x.ClaimBatchId, x.RecipientId })
    .IsUnique()
    .HasDatabaseName("UX_ClaimDetails_ClaimBatchId_RecipientId");
```

`ServiceMonth`の列名は`ServiceMonthKey`とする。`ClaimDetail.ClaimBatchId -> ClaimBatch.Id`と`ClaimBatch.OriginId -> ClaimBatch.Id`は明示FKとし、どちらも`DeleteBehavior.Restrict`にする。`OriginId`と`ClaimBatchId`には検索indexを付ける。`OriginId`自己FKはrootの存在だけをDBで保証し、root-origin、同じoffice／month、時系列及びCancel終端はtransaction内policyと読込時validatorの両方で検証する。

FK名は`FK_ClaimDetails_ClaimBatches_ClaimBatchId`と`FK_ClaimBatches_ClaimBatches_OriginId`、補助index名は`IX_ClaimDetails_ClaimBatchId`と`IX_ClaimBatches_OriginId`に固定する。`ClaimMasterVersion`、`CsvSpecificationVersion`、`ApplicationVersion`及び`SnapshotSchemaVersion`はrequired・最大64文字、`OperationPayloadSha256`はrequired・64文字、両JSONはrequiredのSQLite `TEXT`、`CreatedBy`はrequired・最大64文字、`FinalizationOperationId`はrequiredとする。

`ClaimBatch`と`ClaimDetail`を`AppendOnlyGuard`へ登録し、Modified／Deletedを`AppendOnlyViolationException`にする。`ConcurrencyToken`は追加時に生成してconcurrency tokenとして構成するが、更新を許可する根拠にはしない。外部キー制約は各SQLite connectionで有効であることをmigration／round-trip testで確認する。

### 8. 監査と機微情報

`AuditEntry`及び例外には、氏名、受給者証番号、住所、障害情報、自由記述、snapshot JSON本文、個々の入力値又は保存先を含めない。記録してよいのはbatch ID、operation ID、office ID、recipient ID、service month、kind、root ID、schema／master／CSV／application version、source ID、payload hash、件数、エラーコード及びJSON pathまでとする。JSON pathに値を埋め込まない。

監査追記は請求snapshotと同じtransactionで保存する。snapshot生成前のvalidation failureは、PIIを除いた失敗コード、version及びhashだけを別の安全な監査経路へ記録できる。hashは改ざん防止署名ではなく、決定論と破損検出の識別子として扱う。

## Alternatives

### A. 現行行を更新し、最新の下層データから出力する

過去の提出内容と根拠を再現できず、確定後不変に反するため採用しない。

### B. `OriginId`を直前recordへ向け、既存`AppendOnlyChainPolicy`を再利用する

分岐や孤立を生みやすく、初代rootをDBとpolicyで一意に検証しにくいため採用しない。

### C. 明細行ごとに`ClaimDetail`を保存する

同じ受給者の入力、算定結果及び版情報が分散し、帳票／CSVの受給者単位復元とschema移行が複雑になるため採用しない。

### D. snapshot JSONを最新版へin-place migrationする

確定時bytesとhashを失い、過去版readerの欠落を隠すため採用しない。旧codecを維持し、必要な変換は新しい`Correct`で表す。

### E. pre-checkと通常の`SaveChanges`だけで並行確定を処理する

履歴読込と保存の間に競合が入り、`Cancel`と`Correct`の順序やcommit結果不明後の再送を安全に扱えないため採用しない。

## Consequences

- Phase 3-0のDomain／EF実装は、計画済み項目に加えて`FinalizationOperationId`、`OperationPayloadSha256`及び2つ目のbatch unique indexを持つ。
- Phase 3-1は型付きsnapshot record、version別codec registry、canonical serializer、operation payload hasher及び確定用transaction抽象を実装してから`CloseClaimUseCase`を作る。
- schemaを廃止するには、そのschemaの全確定請求が不要になったという運用判断ではなく、対応readerを維持したまま明示`Correct`へ変換したことを検証する必要がある。
- JSONを二重保持するためDB容量は増えるが、帳票／CSV生成が下層テーブルと現在のmasterから独立する。
- `Cancel`後は通常出力できない。履歴参照は監査用途に限定し、誤って実効版として扱わない。

## Acceptance checks

- `New`のみ、複数`Correct`、同時刻ID tie-break、末尾`Cancel`の実効版を固定する。
- Newなし、複数New、別root、別office／month、rootより前のrecord、Cancel後Correct、複数Cancelを全てfail-closedにする。
- 同一operation ID・同一payloadの再送が同じbatchを返し、異なるpayload及び重複operation IDのDB挿入を拒否する。
- 並行Newは1件だけcommitし、並行Correct／Cancelは非deferred transaction内の`expectedEffectiveBatchId`再検証で直列化する。
- `ClaimDetails(ClaimBatchId, RecipientId)`重複、存在しないbatch／root、FK delete及び2件目NewをSQLiteで拒否する。
- `ClaimBatch`／`ClaimDetail`のModified／Deletedを`AppendOnlyGuard`で拒否する。
- Cancelがdetailなし／全合計0、Correctが差分でない完全snapshot、headerとdetail合計一致であることを検証する。
- canonical JSONのproperty／array順、decimal、年月、UTF-8 bytes及びhashをgolden testで固定する。
- 現行版と既知旧版をversion dispatcherで読め、未知版、hash不一致、deserialize失敗、未知rule／step／sourceをfail-closedにする。
- schema変換が既存行を更新せず、旧snapshotから新しい完全`Correct`を追記することを検証する。
- 帳票／CSV生成時に下層repositoryを呼ばず、同じsnapshot入力から同じbytesを返すことを検証する。
- audit／例外文字列に氏名、受給者証番号、JSON本文及び入力値が含まれないことを検証する。
