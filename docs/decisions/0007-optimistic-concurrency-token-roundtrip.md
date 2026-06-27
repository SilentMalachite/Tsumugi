# ADR 0007: 楽観的同時実行トークンを UseCase 引数で往復させる

- 結論: 同一性マスタ（`Office` / `Recipient`）の更新は、Application 層が `expectedConcurrencyToken` を引数で受け取り、DB の現在トークンと一致しない場合に `OptimisticConcurrencyException` を投げる。DTO（`OfficeDto` / `RecipientDto`）に `ConcurrencyToken` を含めて画面が読んだ時点の値を保持し、Update 要求でそのまま戻す。
- 背景: SQLite に `rowversion` がないため、CLAUDE.md §コーディング規約は「更新トークン列方式」を要求。EF Core は `ConcurrencyToken` 属性の付与でストア側の競合検知は可能だが、それは「DB に届いた時点」での衝突を見るだけで、「画面が読んだ時点」と「Save した時点」の DB 状態の差を検知するには **画面が読んだ token を画面〜UseCase〜Repository〜DB の往復で運ぶ** 必要がある。
- 選択肢:
  - (a) **採用**: UseCase 引数に `expectedConcurrencyToken` を追加し、Application 層で読み直したエンティティのトークンと比較。不一致なら `OptimisticConcurrencyException`。DTO にトークンを含めて UI が round-trip する。
  - (b) EF Core の `IsConcurrencyToken` だけに頼り、DbContext の `SaveChanges` で `DbUpdateConcurrencyException` を待つ。
  - (c) UI レベルで「読込から Save までの間隔」をタイマーで監視して reload を促す（楽観的検知ではない）。
- 決定: (a)。理由:
  - Application 層で検知できるため、Infrastructure（EF Core）への依存を上層に漏らさない（`OptimisticConcurrencyException` は Application 層に定義）。
  - 単一スコープ DbContext（ADR 0008）で `AsNoTracking` 読込 → 編集 → Update する我々のリポジトリ実装では (b) だけでは衝突を見逃すケースがある（既存 tracked entry が前回 Save 後の最新トークンを持っていて、画面の古いトークンを再現できない）。
  - DTO にトークンを含める負担は record の追記フィールド 1 つで小さい。
- 影響:
  - `OfficeDto` / `RecipientDto` のシグネチャに `Guid ConcurrencyToken` が追加。生成箇所（`ListOffices` / `RegisterOffice` / `ListRecipients` / `RegisterRecipient`）は全て更新。
  - `OfficeViewModel.SelectedItem` 受信時、`RecipientEditViewModel.LoadForEdit` で `_editingConcurrencyToken` を保持し、Update コマンドでそのまま渡す。
  - VM 側で `OptimisticConcurrencyException` を捕捉し「他のユーザに先に更新されています」を `SaveErrorMessage` に出す。
  - 追記型エンティティ（Certificate / Contract / OfficeCapability / DailyRecord）は本 ADR の対象外。それらは Update を持たず、訂正/取消で対応する。
- 関連: Codex Round 3 review P3-B 指摘、`OptimisticConcurrencyException.cs`、`UpdateOfficeUseCase` / `UpdateRecipientUseCase`。
