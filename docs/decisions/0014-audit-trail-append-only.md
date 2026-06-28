# ADR 0014: 監査ログ（AuditEntry）を append-only で導入

## 結論

- 同一性マスタ更新 UseCase（`UpdateOfficeUseCase` / `UpdateRecipientUseCase` / `ArchiveRecipientUseCase` / `RestoreRecipientUseCase`）から `IAuditTrail.RecordAsync` を呼び、`AuditEntry` を append-only で追記する。
- `AuditEntry` は `AppendOnlyGuard` 対象（Phase C1 で登録済み）。Modified / Deleted は実行時に `AppendOnlyViolationException` で落ちる。
- `IAuditTrail` は Application 層に定義（`Tsumugi.Application.Audit.IAuditTrail`）し、既定実装は Infrastructure 層の `AuditTrail` で `IAuditEntryRepository` を呼ぶ薄いラッパ。保存は呼び出し UseCase の `IUnitOfWork.SaveChangesAsync` に委ねる（同一トランザクション）。

## 背景

- Phase 1 → Phase 2 引継ぎ open-questions の項目: `UpdateRecipient`/`UpdateOffice` の `actor` は引数で受けていたが `_ = actor;` で捨てられていた。
- Phase 2 の工賃確定（CloseWages, D4）と並行して、同一性マスタ操作の事後検証を可能にする必要がある。

## 選択肢

1. EF Core 監査拡張（EFCore.Audit 系）→ 追加依存と暗黙的な保存処理が大きい。CLAUDE.md のオフライン制約・透明性原則と相性が悪く却下。
2. `ChangeTracker` フックで自動記録 → 何を残すか暗黙的になりがちで、UseCase の意図と乖離する。却下。
3. UseCase 内で明示的に `IAuditTrail.RecordAsync` を呼ぶ → **採用**。何を残すかは UseCase が制御。テストでは `RecordingAuditTrail` で配線を直接検証できる。

## 決定

- `IAuditTrail.RecordAsync(actor, action, targetType, targetId, occurredAt, summary?, ct)`。
- `AuditAction` enum（Phase B1 既存）の `Register` / `Update` / `Archive` / `Restore` を採用。
- Application 層の UseCase で `actor` の whitespace を拒否する（信頼境界）。
- 各 UseCase は Domain 操作 → repo Update → `audit.RecordAsync` → `uow.SaveChangesAsync` の順で呼ぶ。トランザクションは EF Core の `SaveChanges` が単一の。
- テストヘルパ `RecordingAuditTrail` / `NoopAuditTrail` を tests/Tsumugi.Application.Tests と tests/Tsumugi.App.Tests に置く。

## 影響

- `UpdateOfficeUseCase` / `UpdateRecipientUseCase` / `ArchiveRecipientUseCase` / `RestoreRecipientUseCase` の constructor に `TimeProvider` + `IAuditTrail` の依存追加。
- `UpdateOfficeUseCase` の `ExecuteAsync` に `actor` パラメータ追加（既存の他 UseCase は既に `actor` を受けていた）。
- DI 設定（`AddTsumugiInfrastructure`）に `services.AddScoped<IAuditTrail, AuditTrail>()` 追加。
- 既存の Application/App テストはすべて新 constructor シグネチャに更新。
- `OfficeViewModel` は `Environment.UserName` を `actor` として渡す（Phase 1 で `actor` を持っていなかったため）。Phase 3 で認証モデルが入る時に置き換える。
