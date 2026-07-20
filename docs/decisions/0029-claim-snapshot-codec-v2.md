# ADR 0029 — Claim snapshot codec v2

- Status: Accepted
- Date: 2026-07-20

## 決定

`ClaimSnapshotValidationCodecV1` を削除し、`ClaimSnapshotValidationCodecV2` に破壊的置換する。schema version は `claim-snapshot-v2`、validationCodecId は `claim-snapshot-codec-v2`。

## 背景

Phase 3-1 vertical slice で導入した codec v1 は `serviceCode` + 集計値だけの軽量 payload で、3帳票が必要とする Office / Recipient / Certificate / DailyRecord / ClaimInput / IntensiveSupportEpisode / ClaimLines を保持していない。spec §8「生成時に現行 DailyRecord、Certificate、Office 等を再読込せず、確定時のスナップショットだけを入力とする」を満たすには payload 拡張が必須。

## 選択肢

1. v1 を残し、v2 追加で並走（backwards-compat）
2. v1 を破壊的に v2 置換（backwards-compat 無し）
3. v1 に新フィールドを additive に追加（schema version 据置）

## 決定

**選択肢 2**（破壊的置換）。CLAUDE.md「backwards-compat hack禁止」規約に従う。前提として production DB に確定 revision が存在しない（Phase 3-1 マージ直後、GUI 貫通も未実施）ため data migration は不要。

## 影響

- `ClaimSnapshotValidationCodecV1` は削除
- v1 payload を持つ ClaimDetail が存在する場合は codec が拒否する（parse 失敗）
- production 導入前に codec v2 で再確定する運用を前提とする
