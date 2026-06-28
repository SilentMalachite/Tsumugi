# Tsumugi（紡ぎ）

> 就労継続支援B型事業所向け **オフライン専用** デスクトップアプリ
> 記録・工賃計算・国保連請求データ生成を一体で扱う

[![CI](https://github.com/SilentMalachite/Tsumugi/actions/workflows/ci.yml/badge.svg)](https://github.com/SilentMalachite/Tsumugi/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

---

## 概要

**Tsumugi** は、障害福祉サービス（就労継続支援B型）を提供する事業所の日常業務を支援するデスクトップアプリです。サービス提供実績の記録、利用者への工賃計算、国民健康保険団体連合会（国保連）に提出する請求データ（CSV）の生成までを、**端末完結・ネットワーク接続なし** で扱えるように設計されています。

ハンドリングする個人情報（氏名・受給者証番号・障害情報など）の保護を最優先にし、SQLite データベースのファイル権限を OS レベルで強制し、外部通信を持たない構造を CI で機械的に保証しています。

## 主な特徴

- **完全オフライン** — 外部通信 API への直接参照は CI で検出・拒否（PEReader による DLL 直接走査）。テレメトリ・クラウド依存なし。
- **クロスプラットフォーム** — macOS / Windows / Linux 上で動作。CLI ビルドと UI テストが 3 OS で緑。
- **データ保護** — DB は OS のユーザー専用領域に配置し、Unix `0700` / `0600`、Windows は現在ユーザーのみの DACL（継承無効）を **作成時に強制**。
- **追記型データモデル** — エンティティは C# `record` ＋ append-only。訂正は元データを破壊せず履歴として残す。
- **報酬告示の外部化** — 単位数・加算・地域区分単価・CSV フィールドはコードに直書きせず、シード JSON ＋ DB で `適用開始年月` 単位にバージョン管理。
- **アクセシビリティ既定** — ダークテーマ / 低アニメーション / 広い余白・行間 / フォント拡大追従 / 頻用操作のキーボード完結。
- **VACUUM INTO バックアップ** — 単一ファイルバックアップ手段を標準提供。
- **アーキテクチャ検査** — 依存方向違反を Domain / Application / Infrastructure 各層で機械的に検出。

## 責務境界（重要・誤解防止）

国保連請求は **「仕様準拠の請求データ（CSV）を生成し、公式の取込・送信システムにインポートできる状態」まで** が責務です。**伝送・電子証明書・回線処理はアプリの範囲外** とし、これによりオフライン特性を担保します。

## 動作環境

| 項目 | バージョン |
|---|---|
| .NET SDK | **10.0**（LTS） |
| OS | macOS 12+ / Windows 10 1809+ / Linux（glibc 2.31+） |
| UI | Avalonia 11.x（Fluent） |
| DB | SQLite（EF Core 10.x） |

## クイックスタート

```bash
# 1. リポジトリを取得
git clone https://github.com/SilentMalachite/Tsumugi.git
cd Tsumugi

# 2. ローカルツール（dotnet-ef 等）を復元
dotnet tool restore

# 3. 依存解決 → ビルド → テスト
dotnet restore
dotnet build
dotnet test

# 4. アプリを起動
dotnet run --project src/Tsumugi.App
```

品質ゲート一括チェック（format / build / test / coverage / オフライン / アーキテクチャ）:

```bash
./build/ci.sh        # Linux / macOS
pwsh ./build/ci.ps1  # Windows
```

## アーキテクチャ

クリーンアーキテクチャ 4 層構成。依存方向は CI のアーキテクチャテストで機械的に保証されます。

```
src/
  Tsumugi.Domain/         純粋ロジック（record / 値オブジェクト / 報酬算定）
  Tsumugi.Application/    ユースケース / リポジトリ抽象 / DTO / 入力検証
  Tsumugi.Infrastructure/ EF Core / SQLite / バックアップ / CSV 出力 / 保存先サービス
  Tsumugi.App/            Avalonia UI（MVVM）・合成ルート
```

依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。**Domain は外側を一切知らない**。

詳細: [CLAUDE.md](CLAUDE.md)、[ADR 一覧](docs/decisions/)

## ロードマップ

- [x] **フェーズ 0** — プロジェクト基盤・DI 合成ルート・EF Core 往復・VACUUM INTO バックアップ・品質ゲート・オフライン検査・SQLite 権限ポリシー（AC0-1 〜 AC0-6 完了）
- [x] **フェーズ 1** — マスタ + 記録（利用者 / 受給者証 / 契約 / 事業所 / 事業所体制 / 日次記録の CRUD、追記型・楽観的同時実行・期限アラート、AC1-1 〜 AC1-9 完了）
- [x] **フェーズ 2** — 工賃計算（作業実績・WageFund / WageSettings 期間マスタ・4 方式（Piece/Hourly/Fixed/Equal）・Σ配分=原資 不変条件・WageStatement 確定スナップショット・PDF 帳票・AuditEntry、AC2-1 〜 AC2-10 完了。AC2-8 のみ正式定義未確定で暫定式）
- [ ] **フェーズ 3** — 国保連 請求データ（CSV）生成

各フェーズの受け入れ基準は [`01_ClaudeCode_実装指示書_Tsumugi.md`](01_ClaudeCode_実装指示書_Tsumugi.md) §7 を参照。フェーズ 1 実装指示は [`04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md`](04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md)、フェーズ 2 実装指示は [`05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md`](05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md)。

## データ保護とセキュリティ

- **DB 保存先**: `Environment.GetFolderPath(SpecialFolder.ApplicationData)/Tsumugi/tsumugi.db`
  - .NET SDK 版／環境変数により macOS は `~/Library/Application Support/Tsumugi/` または `~/.config/Tsumugi/` に解決
  - Windows は `%APPDATA%\Tsumugi\`
- **権限**: Unix ディレクトリ `0700` / DB ファイル `0600`、Windows は現在ユーザーのみフルコントロール + 継承無効
- **ログ方針**: 氏名・受給者証番号・保存先フルパスをログに出さない
- **既知の脆弱性管理**: [docs/open-questions.md](docs/open-questions.md) と [docs/decisions/0003-sqlite-location-and-encryption.md](docs/decisions/0003-sqlite-location-and-encryption.md)
- **脆弱性報告**: [SECURITY.md](SECURITY.md) を参照（GitHub Security Advisory で非公開報告）

## 貢献

[CONTRIBUTING.md](CONTRIBUTING.md) を読んでから Issue / PR を立ててください。コミュニケーションは日本語ベースです。行動規範は [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) に準拠します。

## ライセンス

[Apache License 2.0](LICENSE) © 2026 SilentMalachite

サードパーティ表記は [NOTICE](NOTICE) を参照。

## 関連ドキュメント

| 文書 | 役割 |
|---|---|
| [CLAUDE.md](CLAUDE.md) | プロジェクト常設指示（依存方向・ドメイン用語・ハード制約） |
| [01_ClaudeCode_実装指示書_Tsumugi.md](01_ClaudeCode_実装指示書_Tsumugi.md) | 全体仕様・フェーズ別受け入れ基準 |
| [03_ClaudeCode_Phase0修正指示_Tsumugi.md](03_ClaudeCode_Phase0修正指示_Tsumugi.md) | フェーズ0 Codex レビュー対応指示 |
| [04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md](04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md) | フェーズ1（マスタ・記録）実装指示と受け入れ基準 |
| [05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md](05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md) | フェーズ2（工賃計算）実装指示と受け入れ基準 |
| [docs/phase2-acceptance.md](docs/phase2-acceptance.md) | フェーズ2 受け入れ基準セルフチェック |
| [docs/decisions/](docs/decisions/) | Architecture Decision Records（ADR） |
| [docs/open-questions.md](docs/open-questions.md) | 仕様未確定事項の起票簿 |
| [CHANGELOG.md](CHANGELOG.md) | リリース履歴 |
| [SECURITY.md](SECURITY.md) | セキュリティポリシーと脆弱性報告手順 |
