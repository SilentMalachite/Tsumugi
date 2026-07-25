# 0035 成果物（請求CSV・3帳票）は検証済み実効 revision からのみ生成する

- 状態: 採用（2026-07-25）
- 関連: ADR 0029（snapshot codec v2 / 確定操作 payload）/ ADR 0030（Phase 3-2 帳票）/ ADR 0034
- 起点: Codex レビュー HIGH「CSV・3帳票がいずれも未検証 raw aggregate から生成されている」

## 結論

`IClaimBatchRepository` は自身の XML doc のとおり **検証や実効版選択を行わない raw aggregate** を返す。
国保連へ渡す成果物はこれを直接読まず、**`VerifiedClaimBatchProvider`** が返す
**`VerifiedClaimBatch`** からのみ生成する。

## 背景

`ClaimFinalizationStore`（書込み経路）は履歴を厳格に検証していたが、その実装は Infrastructure の
private メソッドで、読出し経路からは使えなかった。結果として

- `GenerateClaimReportsUseCase`（Phase 3-2）
- `ExportClaimCsvUseCase`（Phase 3-3）

はどちらも履歴構造・envelope・確定操作 payload ハッシュ・版一致・合計整合の検証を経ずに
`ClaimFinalizationSnapshotReader.Parse` を呼んでいた。DB を直接書き換えた（あるいは破損した）
行から請求データや帳票が出てしまう。

さらに両者の「最新確定 revision」の選び方が食い違っていた。CSV は Cancel を含む最大 Revision を
head とし取消済みなら拒否する（Codex 指摘で修正済み）が、帳票は **Cancel を除外してから**最大を
採っていたため、取消済みの月の請求書が過去 revision から復活していた。Phase 3-2 spec は
「Cancel 状態や revision 不在の場合は `InvalidOperationException`（fail-closed）」と定めており、
帳票側の実装が spec に反していた。

## 決定

1. 検証実装を Application へ移設し **`ClaimHistoryVerifier`** を唯一の実装とする。
   `ClaimFinalizationStore` はこれに委譲する（write と read が別の検証を持たない）。
   文字列形式規則と codec 例外の正規化は `ClaimFinalizationGuards` に集約する。
2. **`VerifiedClaimBatch`** は private コンストラクタ＋同一 assembly の internal factory のみ。
   raw aggregate を包み直して「検証済み」に見せる迂回路を型で塞ぐ。
3. **`VerifiedClaimBatchProvider.FindEffectiveAsync`** が唯一の入口。履歴全体を検証し、
   実効 revision（Cancel も含めた最大 Revision）を返す。head が Cancel、履歴が空、detail 0 件は
   `null`（＝実効請求なし）で、consumer が fail-close する。
4. 検証項目: 履歴構造（`ClaimBatchPolicy`）／確定操作ID一意性／header と detail の版・作成者一致／
   Σdetail＝header（Cancel は detail 0 件）／snapshot envelope の codec 検証／
   確定操作 payload SHA-256 の再構築照合。

## 影響

- 帳票の挙動が変わる: 取消済みの月は請求書・明細書・実績記録票を出さない（spec §9 に整合）。
- 「header 合計 ≠ Σdetail」という状態は検証で成立しなくなるため、その差で「素通し」を確かめていた
  テストは、snapshot 内部の合計と detail 行の値の差で確かめる形へ書き換えた。
- `ClaimHistoryVerifier.ComputeOperationPayloadSha256` を公開した。ハッシュは秘密鍵を用いない
  完全性検査なので権限を与えない。テストが「整合した履歴」を組み立てるために使う。
- 歯の確認（mutation）: ①provider を Cancel 除外 head に戻す ②`requireOperationHash: false` にする
  の 2 通りで、`A_cancelled_head_has_no_effective_revision` /
  `GenerateClaimInvoiceAsync_refuses_when_the_head_revision_is_cancelled` /
  `A_history_whose_operation_payload_hash_does_not_match_fails_closed` /
  `GenerateClaimInvoiceAsync_rejects_a_history_whose_persisted_snapshot_was_tampered_with`
  が RED になることを確認した。
