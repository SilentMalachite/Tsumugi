# ADR 0003: SQLite 保存先とファイル権限・暗号化採否

- 結論: DBは `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` 配下の `Tsumugi/tsumugi.db` に保存する。具体的には macOS では `~/.config/Tsumugi/tsumugi.db`、Windows では `%APPDATA%\Tsumugi\tsumugi.db` となる。暗号化はフェーズ0では未採用（要再評価）。
- 背景: 個人情報を扱う（§2.2, §6-6）。完全オフライン端末運用前提。
- 選択肢: (a) 平文+OSファイル権限 / (b) SQLCipher / (c) OS全体ディスク暗号化に委譲。
- 決定: 暫定 (a)+(c)。保存先は `Environment.SpecialFolder.ApplicationData` で解決（実装済み）。SQLCipher 採否は運用端末の要件確定後に再判断（open-questions に残す）。
- 影響:
  - 保存先パス解決を合成ルートに集約済み。
  - ログに個人情報を出さない実装規約を維持。
  - macOS での `ApplicationData` が `~/Library/Application Support` ではなく `~/.config` に解決される点は macOS 慣例と異なる。将来的に macOS ネイティブパス（`~/Library/Application Support/Tsumugi`）へ移行するかどうかは open-questions として残す。
