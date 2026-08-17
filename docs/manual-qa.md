# 手動 QA（Phase 4 S5）

AC4-9〜AC4-11 向けの実機／発行 smoke 記録。設計は `docs/superpowers/specs/2026-08-17-phase4-s5-distribution-and-first-run-design.md`、配布構成は [ADR 0054](decisions/0054-distribution-configuration.md) を正とする。

## 実施メタデータ

| OS | 実施日 | 実施者 | OS・ハードウェア | 発行物 SHA-256 | 結果 | 備考 |
|---|---|---|---|---|---|---|
| macOS (`osx-arm64`) | （未実施） | （未記入） | （未記入） | （未記入） | 未実施 | Task 6 で記録する |
| Windows (`win-x64`) | （未実施） | （未記入） | （未記入） | （未記入） | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |

## チェック表（macOS / `osx-arm64`）

| # | 項目 | 結果 | 備考 |
|---|---|---|---|
| 1 | 発行スクリプト成功、単一ファイル生成（`./build/publish.sh` → `artifacts/publish/osx-arm64/`） | 未実施 | |
| 2 | クリーンな ApplicationData で起動すると Wizard が出る | 未実施 | |
| 3 | `RegionGrade.None` は登録できず、必須／任意入力エラーが安全に表示される | 未実施 | |
| 4 | 妥当な事業所・管理者職氏名を登録できる | 未実施 | |
| 5 | 再起動後は Wizard なしで MainWindow | 未実施 | |
| 6 | 終了時自動バックアップが生成される（[ADR 0052](decisions/0052-backup-operations.md)） | 未実施 | |
| 7 | キーボード操作（`Ctrl+Enter`／`Escape`、フォーカス順、フォント拡大、Reduce Motion） | 未実施 | |

## チェック表（Windows / `win-x64`）

| # | 項目 | 結果 | 備考 |
|---|---|---|---|
| 1 | 発行スクリプト成功、単一ファイル生成（`.\build\publish.ps1` → `artifacts/publish/win-x64/`） | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |
| 2 | クリーンな ApplicationData で起動すると Wizard が出る | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |
| 3 | `RegionGrade.None` は登録できず、必須／任意入力エラーが安全に表示される | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |
| 4 | 妥当な事業所・管理者職氏名を登録できる | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |
| 5 | 再起動後は Wizard なしで MainWindow | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |
| 6 | 終了時自動バックアップが生成される（[ADR 0052](decisions/0052-backup-operations.md)） | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |
| 7 | キーボード操作（`Ctrl+Enter`／`Escape`、フォーカス順、フォント拡大、Reduce Motion） | 未実施 | Windows 実機未実施。発行スクリプト契約テストのみ実行済み |

## 実施上の注意

- 開発用の既存 ApplicationData を使わず、クリーンな環境で起動する。
- DB／バックアップのフルパス、氏名、受給者証番号を本表や問い合わせに書かない（[ADR 0003](decisions/0003-sqlite-location-and-encryption.md)、`docs/operations.md` §6）。
- macOS 実施後も、Windows 実機での再確認が AC 完全クローズの前提である（ADR 0054）。
