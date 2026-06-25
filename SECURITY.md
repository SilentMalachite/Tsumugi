# セキュリティポリシー

Tsumugi は障害福祉サービス事業所の個人情報（氏名・受給者証番号・障害情報など）を扱うため、脆弱性の取り扱いを最優先しています。

## 報告方法

**脆弱性は GitHub の Public Issue に書かないでください。**

代わりに、GitHub Security Advisory の **「Report a vulnerability」** から非公開で報告してください:

🔒 **<https://github.com/SilentMalachite/Tsumugi/security/advisories/new>**

報告に含めてほしい情報:

1. 脆弱性の概要（1〜2 文）
2. 影響範囲（どの層・どのバージョン・どの OS）
3. 再現手順（最小再現コード・テストケース歓迎）
4. 想定される被害（情報漏えい・改ざん・サービス停止 等）
5. 報告者の連絡先（任意・修正クレジット記載のため）

回答 SLA: 受領 7 営業日以内に初動応答、30 日以内に修正方針返答を目標とします。Critical / High と判断したものは可能な限り早期にパッチをリリースします。

## サポートバージョン

| バージョン | サポート状況 |
|---|---|
| `main` ブランチ最新 | ✅ |
| タグ付きリリース最新 | ✅ |
| それ以前 | ❌（最新バージョンへのアップグレードを推奨） |

フェーズ 0 完了後・正式 v1.0 リリース前の現状は、`main` ブランチがサポート対象です。

## スコープ

### 対象内

- `Tsumugi.Domain` / `Tsumugi.Application` / `Tsumugi.Infrastructure` / `Tsumugi.App` の全ソースコード
- ビルドスクリプト（`build/ci.sh` / `build/ci.ps1`）と CI 設定（`.github/workflows/`）
- ドキュメントに記載された設計判断（ADR）の安全性
- 依存パッケージのトランジティブな脆弱性（修正版が存在するもの）

### 対象外

- 国保連電子請求受付システムの伝送・電子証明書・回線処理（**アプリの責務範囲外**）
- 報酬告示・CSV 仕様書そのものの内容
- OS 全体のディスク暗号化・LDAP/Active Directory 連携・端末管理
- 利用者の運用ミスに起因する情報漏えい（DB ファイルを USB に複製する等）

## データ保護方針

| 項目 | 方針 | 検証 |
|---|---|---|
| DB 保存先 | `Environment.SpecialFolder.ApplicationData/Tsumugi/tsumugi.db` | `SqliteLocationService` |
| Unix 権限 | ディレクトリ `0700` / DB ファイル `0600` — **作成時に強制** | `SqliteLocationServiceTests` |
| Windows 権限 | 現在ユーザーのみフルコントロール / 継承無効 / 継承 ACE 除去 | `SqliteLocationServiceTests` |
| WAL/SHM サイドカー | ディレクトリ権限（Unix `0700` / Windows DACL）で保護 | 〃 |
| 既存ゆるい権限 | 起動時に **冪等に締め直す**（広げない・狭めるのみ） | 〃 |
| ログ方針 | 氏名・受給者証番号・保存先フルパスを出力しない | コードレビュー |
| 外部通信 | `Tsumugi.App` 含む全アセンブリで通信 API 直接参照を CI 禁止 | `AppOfflineComplianceTests` / `OfflineComplianceTests` |

詳細: [docs/decisions/0003-sqlite-location-and-encryption.md](docs/decisions/0003-sqlite-location-and-encryption.md)

## 暗号化採否

SQLCipher 等の DB 暗号化はフェーズ 0 時点で **未採用**（OS ファイル権限 + OS 全体ディスク暗号化への委譲で対応）。運用端末の要件が確定次第、ADR 0003 を再評価します（[docs/open-questions.md](docs/open-questions.md) に起票済み）。

## 既知の脆弱性

- `SQLitePCLRaw.lib.e_sqlite3 2.1.11`（[GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)、HIGH） — `Microsoft.EntityFrameworkCore.Sqlite` のトランジティブ依存。upstream に修正版が無いため `Directory.Build.props` で advisory-scoped に抑制中。詳細は [docs/open-questions.md](docs/open-questions.md) を参照。
  - **許容根拠**: 修正版なし／完全オフラインのデスクトップアプリで SQLite がネットワーク面に露出しない／唯一の生 SQL 経路（`VACUUM INTO`）はシングルクォートエスケープ済み。
  - **解除条件**: upstream がパッチ済みネイティブバンドルを公開したタイミングで抑制を除去しバージョン更新。

## クレジット

責任を持って報告いただいた方は、修正後のリリースノート（[CHANGELOG.md](CHANGELOG.md)）にお名前・ハンドル名を記載します（希望者のみ）。

## 関連ドキュメント

- [docs/decisions/0003-sqlite-location-and-encryption.md](docs/decisions/0003-sqlite-location-and-encryption.md) — 保存先・権限・暗号化採否
- [docs/open-questions.md](docs/open-questions.md) — 既知の未解決事項
- [CLAUDE.md](CLAUDE.md) §ハード制約 — オフライン・データ保護要件
