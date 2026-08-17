# ADR 0054: 配布構成（self-contained・単一ファイル・trim 無効）

- 状態: 確定（2026-08-18）
- 関連: [ADR 0003](0003-sqlite-location-and-encryption.md)、[ADR 0052](0052-backup-operations.md)、[ADR 0013](0013-pdf-engine-questpdf.md)（self-contained サイズの引き継ぎ）
- 対応: Phase 4 S5 / AC4-9

## 結論

macOS／Windows 向けの配布は **RID 固定の Release・self-contained・単一ファイル・trim 無効** とする。出力は `artifacts/publish/<RID>/` に置き、リポジトリへ追跡しない。インストーラ、コード署名、オンライン更新、font sub-setting は本 ADR の対象外。Windows 実機 smoke は未実施であり、macOS 実施後も Windows 側の再確認が必要である。

| OS | RID | スクリプト | 出力 |
|---|---|---|---|
| macOS | `osx-arm64` | `build/publish.sh` | `artifacts/publish/osx-arm64/` |
| Windows | `win-x64` | `build/publish.ps1` | `artifacts/publish/win-x64/` |

## 背景

Phase 4 S5 の目的は、オフラインのまま事業所端末へ配布できる状態にすることである。フレームワーク依存（framework-dependent）配布は対象端末への .NET ランタイム導入を前提にし、現場セットアップの摩擦とバージョンずれのリスクが増える。一方、self-contained は成果物が大きくなるが、端末側の前提を最小化できる。

Avalonia と EF Core はリフレクション／ソース生成以外の動的解決に依存する箇所があり、IL trimming を有効にすると起動時や初回 DB 操作で欠落アセンブリ／メンバが表面化しやすい。PublishTrimmed の既定オフを明示し、退行を防ぐ必要がある。

フォント同梱によるサイズ増は S1 から許容してきた。体感遅延が出るまでは font sub-setting を採らない方針を S5 でも維持する。

## 選択肢

### A: framework-dependent 配布（不採用）

成果物は小さいが、対象端末に互換ランタイムのインストールが必要になる。オフライン配布・現場セットアップの単純さという S5 の目的に合わない。不採用。

### B: self-contained ＋ 単一ファイル ＋ trim 有効（不採用）

サイズ削減は見込めるが、Avalonia／EF Core のリフレクション利用を壊すリスクが高い。除外リストの維持コストが S5 の射程を超える。不採用。

### C: self-contained ＋ 単一ファイル ＋ trim 無効（採用）

ランタイム同梱で端末前提を最小化し、単一ファイルでコピー配布を単純にする。`PublishTrimmed=false` を明示して反射破壊を避ける。サイズ増は許容する。

### D: インストーラ／署名／オンライン更新まで本スライスで揃える（不採用）

msi／pkg／dmg、コード署名、自動更新は別スライスの運用・証明書調達を要する。S5 は単一ファイルのコピー配布までに閉じる。

## 決定

### 決定1: RID・構成・フラグをスクリプトで固定する

`build/publish.sh`（`osx-arm64`）と `build/publish.ps1`（`win-x64`）は次を固定し、引数で RID や trim 有無を変えられないようにする。

- `-c Release`
- `--self-contained true`
- `-p:PublishSingleFile=true`
- `-p:PublishTrimmed=false`

静的契約テスト（`PublishScriptContractTests`）が退行を検出する。

### 決定2: 出力先は `artifacts/publish/<RID>/`、追跡しない

発行成果物はリポジトリにコミットしない。`.gitignore` で `artifacts/publish/` を除外する（S5 完了文書タスクと同期）。配布は生成物のコピーで行う。

### 決定3: trim を有効化しない

Avalonia（XAML／テーマ／コントロールの動的解決）と EF Core（モデル・マイグレーション・プロバイダのリフレクション）を理由に、`PublishTrimmed=false` を明示する。サイズ最適化が必要になったときは別スライスで除外リスト付きの再評価を行う。

### 決定4: 対象外を明示する

次は本 ADR／S5 の対象外とする。

- インストーラ（msi／pkg／dmg 等）
- コード署名・公証・配布ストア掲載
- オンライン更新（アップデータ）
- font sub-setting（同梱フォントのサブセット化）

### 決定5: Windows 実機 smoke は未実施のまま残す

利用可能な実機は当面 macOS のみである。`docs/manual-qa.md` に Windows 行を置き、「Windows 実機未実施」として残す。macOS smoke を実施した後も、Windows 実機での再確認が AC 完全クローズの前提である。

## 影響

### 運用への帰結

- 配布手順は `docs/operations.md` に従う。macOS は `./build/publish.sh`、Windows は `.\build\publish.ps1`。
- DB・バックアップの保存先実体パスは OS／環境変数で変動するため、本 ADR にも運用ガイドにも固定フルパスを書かない（[ADR 0003](0003-sqlite-location-and-encryption.md) 参照）。
- 終了時自動バックアップ・復元の運用は [ADR 0052](0052-backup-operations.md) に従う。

### 残る限界

1. **成果物サイズが大きい。** self-contained＋単一ファイル＋フォント同梱のため、framework-dependent より重い。
2. **インストーラ／署名が無い。** OS のゲートキーパーや SmartScreen 警告は現場で手動解除が必要になりうる。
3. **Windows 実機 smoke は未実施。** 発行スクリプト契約テストは通っても、実機起動・Wizard・バックアップ・アクセシビリティの確認は後続タスクである。
4. **font sub-setting 未実施。** 体感遅延が出た時点で別スライスとする。
