# コントリビューションガイド

Tsumugi へのコントリビューションを歓迎します。本書は外部コントリビューターと内部開発者の双方を対象に、開発ワークフロー・コーディング規約・PR フローを説明します。

> 行動規範: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) に同意したうえで参加してください。
> セキュリティ脆弱性は GitHub Issue に書かず、[SECURITY.md](SECURITY.md) の手順で報告してください。

---

## 開発環境

| 項目 | 要件 |
|---|---|
| .NET SDK | **10.0**（LTS） |
| OS | macOS 12+ / Windows 10 1809+ / Linux（glibc 2.31+） |
| IDE | Rider / Visual Studio 2026+ / VS Code（C# Dev Kit）など任意 |

インストール後、リポジトリのルートで:

```bash
git clone https://github.com/SilentMalachite/Tsumugi.git
cd Tsumugi
dotnet tool restore
dotnet restore
dotnet build
dotnet test
```

CLI ビルド・CLI テストが緑になることを常に維持してください（IDE が緑でも CLI が赤なら CI でブロックされます）。

---

## 品質ゲート（必須）

PR を出す前に下記が全て緑であること:

```bash
./build/ci.sh        # Linux / macOS
pwsh ./build/ci.ps1  # Windows
```

これは次の 5 つのゲートを順に実行します:

1. `dotnet restore`
2. `dotnet format --verify-no-changes` — フォーマット差分ゼロ
3. `dotnet build --no-restore -c Release` — **警告ゼロ**（`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`）
4. `dotnet test -c Release` — 全テスト緑
5. カバレッジ閾値 — `Tsumugi.Domain` と `Tsumugi.Application` で行カバレッジ 70% 以上（Domain は 100% を目標）

---

## ハード制約（CI が機械判定する）

これらは PR レビューより前に CI で検出されます。回避は **しないでください**。

1. **完全オフライン** — `Tsumugi.App` を含む全プロダクションアセンブリで `System.Net.*` / `HttpClient` / `Socket` / `WebClient` 等の通信 API への **直接参照を禁止**。不可避な参照は `Allowlist` に `(完全名, 理由)` で明示する（既定空、追加は根拠必須）。
2. **依存方向** — `App → Application → Domain`、`Infrastructure → Application/Domain`。**Domain は外側を一切知らない**。違反は `tests/Tsumugi.*.Tests/ArchitectureTests.cs` が検出します。
3. **データ保護** — DB 保存先の権限を作成時に強制（Unix `0700`/`0600`、Windows 現在ユーザーのみ DACL + 継承無効）。ログに氏名・受給者証番号・保存先フルパスを出さない。
4. **報酬告示・CSV 仕様の外部化** — 単位数/加算/地域区分単価/CSV フィールドを **ハードコード禁止**。DB テーブル + シード JSON で `適用開始年月` 単位にバージョン管理。
5. **クロスプラットフォーム** — macOS / Windows / Linux すべてでビルドとテストが緑。パス区切り・改行・カルチャ依存に OS 差の地雷を作らない。
6. **アクセシビリティ既定** — ダークテーマ / 低アニメーション / 広い余白 / フォント拡大追従 / 頻用操作のキーボード完結。

詳細は [CLAUDE.md](CLAUDE.md) の §ハード制約 を参照。

---

## コーディング規約

- エンティティは **`record` + append-only**。訂正は元データを破壊せず訂正レコードで履歴を残す。
- 報酬算定・工賃計算は **副作用のない純粋関数**（日付・乱数・I/O に依存しない）。
- 主キーは `Guid`。`作成日時`/`作成者` を持つ。更新は **楽観的同時実行**（更新トークン方式。SQLite には `rowversion` がないため）。
- UI から `DbContext` を直接触らない。必ず Application 層のユースケース経由。
- 1 画面 1 責務。状態を画面間で暗黙共有しない。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。
- 文字列リテラルやテストの命名・テーブル名・列名に日本語を用いて構わない（ドメイン語の表現力を優先）。

---

## TDD ワークフロー

新機能・バグ修正はテストから始めます。

1. **Red** — 失敗するテストを書く。
2. **Green** — テストを通す最小実装。
3. **Refactor** — 動作を変えずに整理。

1 コミット = 1 論理変更。コミットメッセージにフェーズ番号と受け入れ基準 ID（あれば）を含めてください:

```
phase1: AC1-3 add 受給者証 expiry alert use case
```

---

## ブランチ運用

- 既定ブランチ: `main`
- 機能・修正ブランチ: `phase<N>-<short-name>` / `fix/<area>-<issue>` / `docs/<area>`
- フェーズ番号順（0 → 1 → 2 → 3）で進める。前フェーズの受け入れ基準を満たすまで次に着手しない。
- マージ方針: `--no-ff` でマージコミットを残し、レビューの単位を明示。

### コミットメッセージ規約

[Conventional Commits](https://www.conventionalcommits.org/) に準拠した type を付けます:

| Type | 用途 |
|---|---|
| `feat` | 新機能 |
| `fix` | バグ修正 |
| `refactor` | 振る舞いを変えない構造変更 |
| `docs` | ドキュメント変更 |
| `test` | テスト追加・修正のみ |
| `chore` | 依存・ツール・ビルド設定 |
| `perf` | 性能改善 |
| `ci` | CI 設定変更 |

例:

```
feat: add 受給者証期限アラート use case (AC1-2)
fix: correct CSV record terminator on Windows (#42)
docs: add ADR 0004 for 工賃端数規則
```

---

## レビュー体制

本リポジトリは **Claude Code が実装、Codex がレビュー** という二者レビュー体制を採用しています。Codex の指摘は重大度（Major / Important / Minor / Nit）付きで返り、Claude Code 側が修正指示にトリアージして取り込みます。

外部コントリビューターから PR を受けた場合、メンテナが上記サイクルを回したうえでマージ判断します。

### PR チェックリスト

PR テンプレート（[`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md)）に沿って下記を確認してください:

- [ ] `./build/ci.sh` が緑
- [ ] テストが追加・更新されている（TDD 順序）
- [ ] 依存方向違反なし
- [ ] オフライン制約違反なし（CI 自動検出）
- [ ] ログに個人情報や保存先フルパスを出力していない
- [ ] 報酬告示・CSV 仕様をハードコードしていない
- [ ] ADR が必要な設計判断は `docs/decisions/` に追記
- [ ] 仕様不確定事項は `docs/open-questions.md` に起票

---

## 仕様外の事項

公式仕様（報酬告示・国保連インターフェース仕様書など）から **一意に確定できない値は推測で実装しない** でください。代わりに `docs/open-questions.md` に起票し、設計相談に回します。フェーズ着手前にクローズすることが受け入れ基準です。

---

## 関連ドキュメント

- [README.md](README.md) — 概要・クイックスタート
- [CLAUDE.md](CLAUDE.md) — プロジェクト常設指示
- [01_ClaudeCode_実装指示書_Tsumugi.md](01_ClaudeCode_実装指示書_Tsumugi.md) — 全体仕様・受け入れ基準
- [docs/decisions/](docs/decisions/) — ADR
- [docs/open-questions.md](docs/open-questions.md) — 未確定事項
- [CHANGELOG.md](CHANGELOG.md) — リリース履歴

---

ご質問は GitHub Discussions または Issue でお気軽にどうぞ。
