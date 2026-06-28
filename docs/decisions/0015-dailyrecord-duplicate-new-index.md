# ADR 0015: DailyRecord 重複 New を SQLite partial unique index で防止

## 結論

- `(RecipientId, ServiceDate)` かつ `Kind = 1`（`RecordKind.New`）の組み合わせを SQLite の partial unique index（`HasFilter("\"Kind\" = 1")`）で一意化する。
- `DailyRecordPolicy.Effective` は変更しない。

## 背景

- Phase 1 → Phase 2 引継ぎの open-questions 項目。`RecordDailyRecordUseCase` のチェックはレース条件下で多重 New を許してしまう。
- 工賃計算（Phase 2）は実効レコードを合算する前提のため、データ層で一意性を担保する必要がある。
- `RecordKind.New = 1`（EF Core により `int` でストアされる）。brief では 0 と記述されていたが、実際の enum 定義に合わせて `"\"Kind\" = 1"` を採用した。

## 選択肢

1. **アプリケーション層の排他制御のみ**（現状）: レース条件を塞げない。
2. **DB ユニーク制約（全 Kind 対象）**: Correction/Cancel の同一日複数挿入が不能になる。ドメイン要件に合わない。
3. **Partial unique index（Kind=New のみ）**: New の二重挿入をアトミックに防止しつつ Correction/Cancel を許容する。

## 決定

選択肢 3 を採用。

```csharp
builder.HasIndex(r => new { r.RecipientId, r.ServiceDate })
    .HasFilter("\"Kind\" = 1")
    .IsUnique()
    .HasDatabaseName("UX_DailyRecords_RecipientId_ServiceDate_NewOnly");
```

既存の非 unique index `IX_DailyRecords_RecipientId_ServiceDate` は同一列の重複定義になるため削除した。

## 影響

- Migration 1 件追加（`20260628015004_DailyRecordDuplicateNewIndex`）。アプリ起動時の `Database.Migrate()` が自動適用。
- Correction/Cancel（`Kind != 1`）には影響しない。
- Phase 1 の既存テスト 287 件はすべて緑を維持。新規テスト 2 件追加（計 289 件）。
