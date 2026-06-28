# ADR 0017: WageFund 重複 New を SQLite partial unique index で防止

## 結論

- `(OfficeId, Month)`（DB 列は `MonthKey`、`YYYYMM` 整数）かつ `Kind = 1`（`RecordKind.New`）の組み合わせを SQLite の partial unique index（`HasFilter("\"Kind\" = 1")`）で一意化する。
- `WageFundPolicy.Effective` および `SetWageFundUseCase` は変更しない（UseCase は依然 New/Correction を選択するが、最終的な一意性は DB 層で担保される）。

## 背景

- Codex Phase 2 レビュー M-3 指摘事項。`SetWageFundUseCase.ExecuteAsync` (`src/Tsumugi.Application/UseCases/Wage/SetWageFundUseCase.cs`) は「既存 effective を見つけたら Correction、無ければ New」のロジックだが、レース条件下で 2 つの並行リクエストがいずれも「既存なし」と判定し、二重 New を挿入してしまう。
- `WageFundPolicy.Effective` (`src/Tsumugi.Domain/Logic/WageFundPolicy.cs`) は `Kind == RecordKind.New` のレコードを `CreatedAt` 昇順で先頭から 1 つ origin に取る — 二重 New が物理的に格納されると、Correction 連鎖が分岐して履歴解釈が壊れる可能性がある。
- ADR 0015 が `DailyRecord` で完全に対称な問題に対して partial unique index を採用済み。本 ADR はそれを `WageFund` に適用する。
- `RecordKind.New = 1`（EF Core により `int` でストアされる）。partial filter は `"\"Kind\" = 1"` で記述する。

## 選択肢

1. **アプリケーション層の排他制御のみ**（現状）: レース条件を塞げない。
2. **DB ユニーク制約（全 Kind 対象）**: Correction/Cancel の同月複数挿入が不能になる。ドメイン要件（履歴追記）に合わない。
3. **Partial unique index（Kind=New のみ）**: New の二重挿入をアトミックに防止しつつ Correction/Cancel を許容する。

## 決定

選択肢 3 を採用。`WageFundConfiguration` で以下を宣言する。

```csharp
builder.HasIndex(r => new { r.OfficeId, r.Month })
    .HasFilter("\"Kind\" = 1")
    .IsUnique()
    .HasDatabaseName("UX_WageFunds_OfficeId_MonthKey_NewOnly");
```

`builder.HasIndex(r => r.OfficeId);` の単独 index は重複定義になるためそのままにせず、partial unique index で代替する想定であれば削除を検討する。ただし他のクエリ（事業所単独での検索）がこの単独 index を使う可能性があるため、本 ADR では `(OfficeId)` 単独 index は **温存** し、partial unique index を追加する形で並存させる（ADR 0015 では非 unique の `(RecipientId, ServiceDate)` 単独 index を削除したが、本件は `(OfficeId, Month)` 複合と `(OfficeId)` 単独で意味が異なる）。

## 影響

- Migration 1 件追加 (`{timestamp}_WageFundDuplicateNewIndex`)。アプリ起動時の `Database.Migrate()` が自動適用。
- `WageFund.Correction` / `Cancellation`（`Kind != 1`）には影響しない。
- 既存テストはすべて緑を維持。新規テスト 2 件追加（duplicate New 拒否 / Correction 許容）。
- `SetWageFundUseCase` は変更不要だが、レース条件下では `DbUpdateException` が発生する。これは想定挙動として呼び出し側（UI: `WageFundSettingsViewModel`）が `catch (DbUpdateException ex)` で再試行 or エラー表示することで対処する案がある（本 ADR スコープ外、open-questions に追記する案を report で検討）。
