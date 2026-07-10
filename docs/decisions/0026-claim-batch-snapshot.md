# ADR 0026: 請求確定スナップショットと追記履歴

**Status**: Accepted（2026-07-10）

## Context

請求確定後に`Office`、`Recipient`、受給者証、日次記録又は報酬マスタが訂正されても、確定請求は自動で変わってはならない。一方、再確定、取下げ、制度改定、アプリ更新及びsnapshot schema更新後も、確定時の入力、規則、版及び出典を検証し、既存の確定請求から帳票とCSVを再生成できる必要がある。

既存の`AppendOnlyChainPolicy`は`OriginId`が直前レコードを指すhop-by-hop連鎖である。請求は全`Correct`／`Cancel`が初代`New.Id`を直接指すroot-origin lineageとするため、このpolicyを流用できない。また`CreatedAt`はwall clockであり、SQLite上の順序、時計の単調性又は一意性を保証しない。現行`IUnitOfWork.SaveChangesAsync`だけでは履歴読込から保存までが同一transactionにならず、並行確定とcommit結果不明後の再送を安全に扱えない。

2026-06-29付けPhase 3-1〜3-3計画にある、`Cancel`が直近recordを参照する例、明細行単位の`ClaimDetail`、帳票／CSV生成時に現行`Office`や`Recipient`等を再読込する例は採用しない。現行化済みPhase 3設計、ADR 0020、0024、0025及び本ADRを優先する。

## Decision

### 1. `ClaimBatch`のlineageと永続項目

`ClaimBatch`の粒度は`(OfficeId, ServiceMonth)`とし、既存`RecordKind`の実値`New = 1`、`Correct = 2`、`Cancel = 3`を使う。

- 初回確定だけを`New`とし、1 lineageに1件だけ許可する。`New.OriginId`はnull、`Revision`は1とする。
- 再確定は`Correct`を追記する。全`Correct.OriginId`は直前版ではなく初代`New.Id`とする。
- 取下げは`Cancel`を追記する。全`Cancel.OriginId`も初代`New.Id`とする。
- root以外のID、別`OfficeId`又は別`ServiceMonth`のrootを`OriginId`に指定してはならない。
- `Correct`は差分ではなく、そのrevisionの受給者集合を含む完全なheader＋全`ClaimDetail` snapshotとする。
- `Cancel`は`ClaimDetail`を持たず、4つの合計を全て0とする。4つのsnapshot版は取り消す実効headからコピーし、操作を実行したアプリ版だけを`OperationApplicationVersion`へ記録する。

`ClaimBatch`は次を保持する。

```text
OfficeId / ServiceMonth / Revision / Kind / OriginId
ExpectedHeadBatchId / ExpectedHeadRevision
TotalUnits / TotalCostYen / TotalBenefitYen / TotalBurdenYen
ClaimMasterVersion / CsvSpecificationVersion / ReportSpecificationVersion
SnapshotApplicationVersion / OperationApplicationVersion
FinalizationOperationId / OperationPayloadSchemaVersion / OperationPayloadSha256
Entity.Id / CreatedAt / CreatedBy / ConcurrencyToken
```

`ExpectedHeadBatchId`／`ExpectedHeadRevision`は`New`ではnull、`Correct`／`Cancel`では操作開始時に呼出元が確認した実効headを保持する。`FinalizationOperationId`は確定、再確定又は取下げの1操作を識別する非空`Guid`であり、全batchで一意とする。`OperationPayloadSchemaVersion`は当該hashを再構築するschema IDを保持し、本ADRの新規writeでは`claim-finalization-operation-v1`とする。

### 2. `Revision`だけを追記順の権威にする

確定transaction内で検証済み履歴の`max(Revision) + 1`を次revisionとして採番する。revisionは1始まりの連続整数で、欠番、重複、0以下を許可しない。`New`はrevision 1に1件だけ存在し、revision 2以降は`Correct`又は末尾1件の`Cancel`だけを許可する。

専用`ClaimBatchPolicy`はheader履歴を`Revision`昇順だけで検証する。保存時と読込時のsnapshot aggregate validatorは、このpolicyに加えて対応details、JSON、版及び合計を検証する。次を検出した場合は履歴全体を不正として例外にし、実効版、帳票又はCSVを返さない。

- revisionの欠番、重複、0以下、revision 1以外の`New`又はrevision 1の非`New`
- `New`なし又は複数`New`、`New.OriginId != null`
- 孤立した`Correct`／`Cancel`、空の`OriginId`、初代`New.Id`以外への参照
- `Correct`／`Cancel`の`ExpectedHeadRevision != Revision - 1`、又は`ExpectedHeadBatchId`が直前revisionを指さない
- 異なる`OfficeId`又は`ServiceMonth`の混在
- `Cancel`後のrecord又は複数`Cancel`
- 未知`RecordKind`、負の合計、`Cancel`の非0合計又はdetail保持
- `New`／`Correct`のheader合計と受給者別detail合計の不一致
- snapshot版のheader／detail不一致、重複operation ID又は不正operation hash

検証済み履歴が空なら実効版はない。最大revisionが`New`又は`Correct`ならそれを実効headとし、最大revisionが`Cancel`なら実効版なしとする。`expected head`もrevision最大のrecordで判定する。

`CreatedAt`と`Id`は監査及び重複revision等の不正データを診断する識別子に限定する。順序、実効版又は欠損revisionの復旧推測に使わず、同一revisionを`CreatedAt`／`Id`で並べて救済しない。`AppendOnlyChainPolicy`も呼ばない。

### 3. versioned operation payloadと再送同一性

`OperationPayloadSha256`は、schema ID`claim-finalization-operation-v1`でcanonical化したoperation payload全体のUTF-8 bytesに対するSHA-256（小文字64桁hex）とする。`OperationPayloadSha256`自体はhash入力へ含めない。

UTF-8、BOMなし、indent／改行なし、`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`、null／default非省略で出力する。property順と値表現を次に固定し、未知property、欠落property及び順序違いを拒否する。GUIDは小文字`D`形式、`serviceMonth`は`YYYY-MM`、`kind`は既存の整数code、版／schema IDはASCII、`createdBy`はNFC正規化済み文字列、nullと0は省略しない。`details`は小文字`D`形式`recipientId`のUTF-8 bytesをordinal比較した昇順とする。serializer／encoder設定を変更する場合は同じschema IDを再利用しない。

```json
{
  "schemaVersion": "claim-finalization-operation-v1",
  "kind": 1,
  "officeId": "00000000-0000-0000-0000-000000000000",
  "serviceMonth": "2026-06",
  "rootBatchId": null,
  "expectedHeadBatchId": null,
  "expectedHeadRevision": null,
  "createdBy": "actor",
  "operationApplicationVersion": "app-operation-version",
  "claimMasterVersion": "claim-master-r8-06",
  "csvSpecificationVersion": "csv-r7-10",
  "reportSpecificationVersion": "report-r8-06",
  "snapshotApplicationVersion": "app-snapshot-version",
  "totalUnits": 0,
  "totalCostYen": 0,
  "totalBenefitYen": 0,
  "totalBurdenYen": 0,
  "details": [
    {
      "recipientId": "00000000-0000-0000-0000-000000000000",
      "snapshotSchemaVersion": "claim-snapshot-v1",
      "claimMasterVersion": "claim-master-r8-06",
      "csvSpecificationVersion": "csv-r7-10",
      "reportSpecificationVersion": "report-r8-06",
      "snapshotApplicationVersion": "app-snapshot-version",
      "inputSnapshotEnvelope": {},
      "calculationSnapshotEnvelope": {},
      "totalUnits": 0,
      "totalCostYen": 0,
      "benefitYen": 0,
      "burdenYen": 0
    }
  ]
}
```

2つのenvelopeはescaped JSON文字列又は再serializeした近似値ではなく、DBへ保存するcanonical JSONのUTF-8 bytesをそのままoperation JSONのobject値へ埋め込む。新規batch／detail ID、`CreatedAt`及び`Revision`はtransaction取得後に採番され、再試行で変わり得るためhash入力へ含めない。`rootBatchId`は`New`ではnull、`Correct`／`Cancel`では初代`New.Id`とする。

operation ID再送時は保存済み`OperationPayloadSchemaVersion`でcanonicalizerを選び、batch＋全detailsからpayloadを再構築する。再計算hash、保存hash、要求hash、office、month、kind、root、expected head及び`CreatedBy`が全て一致すれば追記せず既存aggregateを返す。一つでも異なればoperation ID衝突又は保存済みsnapshot破損として拒否する。未知operation schemaをv1又は現在版として解釈せずフェイルクローズする。

### 4. 並行確定、precondition及びtransaction

確定操作はInfrastructureが所有する単一の明示transactionで次を実行する。

1. SQLiteのwrite lockを先に取得する非deferred transactionを開始する。現行providerでは`Microsoft.Data.Sqlite.SqliteConnection.BeginTransaction(deferred: false)`相当を使い、EF Core contextを同transactionへ参加させる。
2. 同じoperation IDのaggregateを読込み、あれば前節の再送同一性を検証して既存結果を返す。
3. 対象office／monthの全batch＋全detailsを読込み、`Revision`とaggregateを検証する。
4. `New`はexpected headなし／履歴なし、`Correct`／`Cancel`は要求したhead ID＋revisionが最大revision recordと一致することを確認する。
5. `Revision = max + 1`を採番し、batch、全details及びPIIを含まない`AuditEntry`を追跡して1回の`SaveChanges`で保存し、commitする。

並行する異なる操作はwrite lockで直列化され、後続操作は先行commit後のheadを検証する。`ClaimBatches(OfficeId, ServiceMonthKey) WHERE Kind = 1`、`FinalizationOperationId`及び`(OfficeId, ServiceMonthKey, Revision)`の3つのbatch unique indexを二重防御とする。

SQLite busy又はconstraint errorではtransaction全体をrollbackし、ChangeTrackerを破棄する。自動再試行は同じoperation IDと同じpayloadでtransaction全体を最初から行う。commit結果不明時も同じoperation IDで再送して既存aggregateを回収する。失敗した`New`を暗黙に`Correct`へ、失敗した`Correct`を別の`Correct`へ変換してはならない。

### 5. 受給者単位のsnapshot

`ClaimDetail`は明細行単位ではなく受給者単位とし、1つのbatch内で各`RecipientId`を最大1件とする。次を保持する。

```text
ClaimBatchId / RecipientId / SnapshotSchemaVersion
ClaimMasterVersion / CsvSpecificationVersion / ReportSpecificationVersion
SnapshotApplicationVersion
InputSnapshotJson / CalculationSnapshotJson
TotalUnits / TotalCostYen / BenefitYen / BurdenYen
Entity.Id / CreatedAt / CreatedBy / ConcurrencyToken
```

`InputSnapshotJson`は確定時に検証した事業所、受給者、受給者証、契約、日次実績、請求固有入力及び適用期間を含む。`CalculationSnapshotJson`は受給者別の算定行、集計、負担及び給付を含み、次を省略しない。

- 各算定段階の`calculationStepId`と、端数処理を行う段階だけの`roundingRuleId`
- `percentageBaseScope`、selector、適用順、丸め前後値及び公式service-code row
- `sourceDocumentId`、source SHA-256及びphysical page／row locator

4つのsnapshot版はheader、全details及びsnapshot JSON内で一致しなければならない。ADR 0020の未登録source、ADR 0024の未登録CSV仕様版、ADR 0025の未登録rule／stepへフォールバックしない。Phase 3-1では不可変の型付きrecordからだけsnapshot JSONを生成する。Domain entityは文字列、版及び非負合計を検証するが、JSONの意味は解釈しない。

### 6. 決定論的JSONとschema互換性

各snapshot JSONは次のenvelopeとする。

```json
{"schemaVersion":"claim-snapshot-v1","payloadSha256":"64 lowercase hex characters","payload":{}}
```

`payloadSha256`はcanonical payloadのUTF-8 bytesに対するSHA-256である。schema別codecはUTF-8／BOMなし／indentなし、固定property順、nullとdefaultを含む固定property集合、安定keyによる配列順、二進浮動小数を経由しないdecimal scale、InvariantCulture／UTCの年月日時表現を固定する。未知・重複property、非有限値、範囲外値、trailing token及び非canonicalな保存bytesを拒否する。

`SnapshotSchemaVersion`列、両envelopeのschema、選択codec及び`SnapshotApplicationVersion`互換表が一致しなければならない。version dispatcher／codec registryは、一度リリースしたschemaの型付きreader、canonicalizer及びvalidatorを別の意味へ変更しない。既知旧版は旧codecでhash、deserialize及び意味を検証して帳票／CSV入力へ復元する。

未知schema、既知版のhash不一致、deserialize失敗、非canonical bytes、schema／版／合計不一致又はcodec欠落はフェイルクローズする。JSON本文を修復、既定値補完、現行schemaとして再解釈又は下層データから再計算してはならない。

旧schemaを新schemaへ変換する場合は、旧codecで検証したsnapshotをversioned converterへ渡し、新しい完全snapshotを`Correct`として追記する。既存行は更新せず、schema変換のために現行`DailyRecord`、`Certificate`、`Office`等を再読込しない。旧行がappend-only historyに1件でも残る限り、その旧schemaのcodec、validator、snapshot application compatibility reader及び対応rendererの読取supportを削除できない。停止できるのは旧schemaの新規write supportだけである。

### 7. 版の分離と帳票／CSV再現

snapshotを表す4版と確定操作を表す1版を混同しない。

| version | 用途 | 選択／検証 |
| --- | --- | --- |
| `ClaimMasterVersion` | 算定master／source束 | master、rule、step及びsource検証 |
| `CsvSpecificationVersion` | CSV field／record仕様 | CSV writer registry選択 |
| `ReportSpecificationVersion` | 3帳票のfield／layout仕様 | report renderer registry選択 |
| `SnapshotApplicationVersion` | snapshotを生成したアプリと型互換性 | codec compatibility検証、必要時renderer adapter選択 |
| `OperationApplicationVersion` | 確定／再送操作を実行したアプリ | operation hash、transaction監査だけに使用 |

`OperationApplicationVersion`でsnapshot codec、CSV writer又はreport rendererを選択しない。`Cancel`ではsnapshot4版を取消対象headからコピーし、`OperationApplicationVersion`だけを実行版にする。operation payload、監査及びheader／detail整合検査には5版全てを含める。

帳票とCSVは、Phase 3-1の`IValidatedClaimSnapshotReader`が全history＋全detailsを旧版codecまで含めて検証した実効aggregateだけを読む。生成時に現行`Office`、`Recipient`、`Certificate`、`Contract`、`DailyRecord`、報酬master又は自由記述を再読込しない。帳票は`ReportSpecificationVersion`、CSVは`CsvSpecificationVersion`をregistry keyとし、`SnapshotSchemaVersion`／`SnapshotApplicationVersion`の互換性を併せて検証する。対応entryがなければ最新実装へ暗黙移行せず停止する。

CSVの`ProcessingMonth`は独立入力とし`ServiceMonth`から推測しない。同じ実効aggregate、同じ出力版、同じ明示入力及び同じ決定論的clock入力から同じbytesを生成する。末尾`Cancel`なら出力を拒否し、`Correct`後はその完全snapshotだけを使う。

### 8. EF Core／SQLite制約

現行の`Guid`主キー、`DateTimeOffset CreatedAt`、`string CreatedBy`、`Guid ConcurrencyToken`及び`ServiceMonth -> YYYYMM int`変換に合わせ、次の4 unique indexを構成する。

```csharp
builder.HasIndex(x => new { x.OfficeId, x.ServiceMonth })
    .HasFilter("\"Kind\" = 1")
    .IsUnique()
    .HasDatabaseName("UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly");

builder.HasIndex(x => x.FinalizationOperationId)
    .IsUnique()
    .HasDatabaseName("UX_ClaimBatches_FinalizationOperationId");

builder.HasIndex(x => new { x.OfficeId, x.ServiceMonth, x.Revision })
    .IsUnique()
    .HasDatabaseName("UX_ClaimBatches_OfficeId_ServiceMonthKey_Revision");

builder.HasIndex(x => new { x.ClaimBatchId, x.RecipientId })
    .IsUnique()
    .HasDatabaseName("UX_ClaimDetails_ClaimBatchId_RecipientId");
```

`ServiceMonth`列は`ServiceMonthKey`とする。`ClaimDetail.ClaimBatchId`、`ClaimBatch.OriginId`及び`ClaimBatch.ExpectedHeadBatchId`から`ClaimBatch.Id`へのFKは全て`DeleteBehavior.Restrict`とし、順に`FK_ClaimDetails_ClaimBatches_ClaimBatchId`、`FK_ClaimBatches_ClaimBatches_OriginId`、`FK_ClaimBatches_ClaimBatches_ExpectedHeadBatchId`とする。対応する非unique indexも付ける。DBで表せない連続revision、root一致、expected head一致、Cancel終端及び合計はtransaction内policyと全読込時validatorに二重化する。

5つのapplication／specification version、`SnapshotSchemaVersion`及び`OperationPayloadSchemaVersion`はrequired・最大64文字、`OperationPayloadSha256`はrequired・64文字、両JSONはrequiredのSQLite `TEXT`、`Revision`はrequired、`FinalizationOperationId`はrequired、`CreatedBy`はrequired・最大64文字とする。`ClaimBatch`と`ClaimDetail`を`AppendOnlyGuard`へ登録し、Modified／Deletedを拒否する。外部キー有効化と4 unique indexはmigration round-trip testで確認する。

### 9. 型付き請求監査と例外

請求経路は一般`IAuditTrail`を直接呼ばず、`IClaimAuditTrail.RecordAsync(string actor, ClaimAuditPayload payload, CancellationToken)`だけを使う。`actor`は一般`IAuditTrail`のactor引数へだけ渡し、summaryへ含めない。adapterのsafe formatterだけが一般`IAuditTrail.RecordAsync`の`summary`を生成でき、呼出元から自由文字列summaryを受け取らない。payloadはbatch／operation／office／recipient ID、service month、kind、root、revision、expected head、5版、snapshot／operation schema、source ID、hash、件数及び閉じたevent／error codeだけを持つ。

請求例外は閉じた`ClaimErrorCode`と、値を保持できない`ClaimJsonPath`型を使う。pathはschema property tokenと配列indexだけから構成し、入力値、自由記述又は表示文言を埋め込めない。氏名、受給者証番号、住所、障害情報、snapshot JSON本文、個々の入力値、金額又は保存先を監査と例外へ含めない。

監査追記はsnapshotと同じtransactionで保存する。validation failureもtyped adapterを通し、error code、値なしpath、version及びhashだけを記録する。hashは署名ではなく決定論と破損検出の識別子として扱う。

## Alternatives

### A. `CreatedAt`＋`Id`で追記順を決める

時計は単調でも一意でもなく、同時刻のGUID順は業務上の追記順ではないため採用しない。transaction内採番の`Revision`だけを使う。

### B. 現行行を更新し、最新の下層データから出力する

過去の提出内容と根拠を再現できず、確定後不変に反するため採用しない。

### C. `OriginId`を直前recordへ向け、既存`AppendOnlyChainPolicy`を再利用する

初代rootを一意に検証しにくいため採用しない。直前headは別のtyped preconditionとして保存する。

### D. snapshot JSONを最新版へin-place migrationする

確定時bytesとhashを失い、過去historyを検証できなくなるため採用しない。旧read supportを保持し、変換結果は`Correct`で追記する。

### E. pre-checkと通常の`SaveChanges`だけで並行確定を処理する

履歴読込と保存の間に競合が入り、revision、Cancel終端及びcommit結果不明後の再送を安全に扱えないため採用しない。

## Consequences

- Phase 3-0のDomain／EF／Repositoryは`Revision`、expected head、5版、operation schema／ID／hash、3つのbatch unique index及びdetail unique indexを実装する。
- Phase 3-1は型付きsnapshot record、全旧版を保持するcodec／validator registry及びvalidated aggregate serviceを追加してから`CloseClaimUseCase`を公開する。
- append-only旧行が残る限り旧schemaのreader／validator／rendererを削除できない。明示`Correct`後も履歴検証に必要であり、新規writeだけを停止できる。
- JSONと版を重複保持するためDB容量は増えるが、帳票／CSV生成が下層テーブルと現在のmasterから独立する。
- `Cancel`後は通常出力できない。履歴参照は監査用途に限定する。

## Acceptance checks

- revision 1のNew、連続Correct、末尾Cancelを固定し、欠番、重複、0以下、複数New、別root、expected head不一致、Cancel後recordをfail-closedにする。
- CreatedAt逆転又は同値でもRevision順だけを使い、同一RevisionをCreatedAt／Idで救済しない。
- `claim-finalization-operation-v1`の全property、順序、値表現、detail順及びcanonical envelope bytesをgolden testで固定する。
- 同一operation ID・同一payloadの再送は同じaggregateを返し、異なるpayload又は保存aggregateからの再構築hash不一致を拒否する。
- 並行確定は非deferred transaction内でexpected head再検証とRevision採番を行い、同一revisionをDBでも拒否する。
- New partial、operation ID、office／month／revision、batch／recipient detailの4 unique indexと3つのRestrict FKをSQLiteで検証する。
- Cancelはdetailなし／全合計0／snapshot4版コピー、Correctは完全snapshot、header／detailの4版と合計が一致する。
- 現行版と既知旧版をregistryで読め、未知版、hash不一致、deserialize失敗、未知rule／step／sourceをfail-closedにする。
- 旧schemaをCorrectへ変換した後も旧historyのcodec／validator／renderer read testを維持する。
- raw Repositoryだけでは実効版を返さず、Phase 3-1のvalidated aggregate serviceだけが全batch＋details検証後に返す。
- 帳票／CSV生成時に下層repositoryを呼ばず、同じsnapshot入力から同じbytesを返す。
- 請求監査がtyped adapterだけを通り、自由summary、PII、JSON本文、入力値及び金額を出力できないことを検証する。
