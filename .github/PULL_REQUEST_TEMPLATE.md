# Pull Request

## 概要

<!-- このPRが何を解決するかを1〜3文で。「なぜ」を重視。 -->

## 関連 Issue

<!-- 例: Closes #42 / Refs #100 -->

## 種別

<!-- 該当するものに [x] -->

- [ ] `feat` 新機能
- [ ] `fix` バグ修正
- [ ] `refactor` 振る舞いを変えない構造変更
- [ ] `docs` ドキュメントのみ
- [ ] `test` テスト追加・修正のみ
- [ ] `chore` 依存・ツール・ビルド
- [ ] `perf` 性能改善
- [ ] `ci` CI 設定

## フェーズと受け入れ基準

<!-- 該当するフェーズの AC（受け入れ基準）ID を引用。例: AC1-3 受給者証期限アラート -->

- フェーズ: <!-- 0 / 1 / 2 / 3 -->
- AC: <!-- 例: AC1-3 -->

## テスト

<!-- TDD: Red → Green → Refactor の証跡があれば添付。新規・更新したテスト名を列挙。 -->

- 追加・更新したテスト:
  -
- 実行結果（CI 以外のローカル検証があれば）:
  -

## 影響範囲

<!-- 触れたレイヤー / 機能 / 公開 API -->

- 触れた層: <!-- Domain / Application / Infrastructure / App / tests / docs / CI -->
- 公開 API への影響: <!-- なし / 破壊的変更 / 後方互換 -->

## スクリーンショット（UI 変更がある場合）

<!-- 必要に応じて添付 -->

## チェックリスト

### 品質ゲート（CI で機械判定）

- [ ] `./build/ci.sh`（または `pwsh ./build/ci.ps1`）が緑
- [ ] `dotnet format --verify-no-changes` 通過
- [ ] `dotnet build` 警告ゼロ
- [ ] `dotnet test` 全緑
- [ ] Domain / Application 行カバレッジ 70% 以上維持

### ハード制約（CLAUDE.md §ハード制約）

- [ ] **オフライン** — `System.Net.*` / `HttpClient` / `Socket` 等の通信 API 直接参照なし（allowlist 追加時は `(完全名, 理由)` で明示）
- [ ] **依存方向** — `App → Application → Domain` / `Infrastructure → Application/Domain` を遵守。Domain は外側を一切知らない
- [ ] **データ保護** — DB ファイル権限ポリシー（Unix `0700/0600` / Windows DACL）を破っていない。ログに氏名・受給者証番号・保存先フルパスを出していない
- [ ] **報酬告示・CSV 仕様の外部化** — 単位数・加算・地域区分単価・CSV フィールドをハードコードしていない（DB + シード JSON 経由）
- [ ] **アクセシビリティ** — ダークテーマ / 低アニメーション / キーボード完結を維持

### ドキュメント

- [ ] 設計判断は `docs/decisions/` に ADR を追加（必要な場合のみ）
- [ ] 仕様不確定事項は `docs/open-questions.md` に起票
- [ ] 破壊的変更・新機能は [CHANGELOG.md](../CHANGELOG.md) に反映
- [ ] 必要なら [README.md](../README.md) を更新

### コミット

- [ ] 1 コミット = 1 論理変更
- [ ] コミットメッセージにフェーズ番号と AC ID を含めている（例: `phase1: AC1-3 add ...`）
- [ ] `--no-verify` / `--no-gpg-sign` は使っていない
