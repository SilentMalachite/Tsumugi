# Phase 4 S5 設計spec — 配布・初回セットアップ・運用ガイド・手動 QA

> **Source**: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.5 から派生。親文書 `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` はリポジトリに存在しない（`01`〜`06` のみ）。実質の正本は roadmap。
> **Status**: 設計合意済（2026-08-17）。
> **対応 AC**: AC4-9（self-contained 発行 smoke）／AC4-10（初回セットアップウィザード）／AC4-11（運用ガイド＋手動 QA 実施記録）。
> **想定サイズ**: 中（1〜2 PR＋macOS 実機 QA。Windows 実機は未実施として残す）。

---

## 1. 目的

**オフラインのまま事業所端末へ配布できる状態**にする。ロードマップ AC4-9〜AC4-11 に対する S5 の受け入れ定義は次のとおり（roadmap 文言との差分は §2.2・決定6）。

| AC | S5 で満たすもの | S5 で明示的に残すもの |
|---|---|---|
| AC4-9 | `publish.sh`／`publish.ps1` の RID・フラグ固定＋**macOS 実機** smoke | Windows 実機 smoke（スクリプト検証のみ） |
| AC4-10 | `Office` 0 件判定の初回ウィザード＋登録 | （なし） |
| AC4-11 | `operations.md`＋`manual-qa.md`＋**macOS 実施記録** | Windows 実施記録（未実施行） |

1. **発行**: macOS / Windows 向け self-contained 単一ファイル発行スクリプトを固定する。
2. **初回導線**: 事業所未登録の起動を専用ウィザードで受け止め、通常画面へ未設定状態を持ち込まない。
3. **運用文書**: バックアップ／復元・OS ディスク暗号化・請求 CSV の責務境界を日本語で明文化する。
4. **手動 QA**: 実施記録表を用意し、macOS smoke を S5 内で記録する。Windows 実機は未実施チェックとして残す。

---

## 2. 現状調査（2026-08-17 実施）

### 2.1 コードベースの実測

| 対象 | 実測 |
|---|---|
| `build/publish.sh` / `build/publish.ps1` | **無い**（`build/` にあるのは `ci.sh` / `ci.ps1` と Phase 3 抽出スクリプト） |
| `docs/operations.md` / `docs/manual-qa.md` | **無い** |
| 起動フロー | `App.axaml.cs` が保存先確保 → DI → migration → **常に MainWindow**。初回分岐なし |
| `RegisterOfficeUseCase` | 事業所番号・名称・サービス種別・地域区分・郵便番号・住所・電話・代表者職氏名を登録可能 |
| `Office.RepresentativeTitleAndName` | 代表者職氏名（任意文字列）。**管理者専用列は無い** |
| `ListOfficesUseCase` | 全事業所一覧。初回判定の入力源として使える |
| バックアップ／復元 | S3a 完了（終了時自動・世代・復元 UI・`SecureFileSystem`） |
| OS ディスク暗号化 | ADR 0003 で FileVault／BitLocker を運用要件として確定済み。運用ガイドへの引き渡し待ち |

### 2.2 ロードマップ §8.5 との差分（本 spec で修正する前提）

| ロードマップ記載 | 本 spec の扱い |
|---|---|
| `FirstRunPolicy` が「DB 有無→初回判定」 | **破棄**。migration 済み空 DB を見逃すため、`Office` 件数 0 で判定する（決定2） |
| 「管理者名」 | 専用列は作らず、既存 `RepresentativeTitleAndName` に保存する（決定3） |
| 両 OS smoke を S5 最終コミットで完了／AC4-11「両OS各1回」 | **意図的に縮小**。macOS は S5 内実施。Windows は表と手順まで用意し、実機は未実施チェック（決定6）。roadmap／CHANGELOG に「Windows 未実施」を明記して AC を部分クローズする |

---

## 3. 決定

### 決定1: 配布は self-contained・単一ファイル・trim 無効

RID:

| OS | RID | スクリプト |
|---|---|---|
| macOS | `osx-arm64` | `build/publish.sh` |
| Windows | `win-x64` | `build/publish.ps1` |

共通条件:

- `dotnet publish src/Tsumugi.App -c Release -r <RID> --self-contained true`
- `-p:PublishSingleFile=true`
- `-p:PublishTrimmed=false`（既定オフ。Avalonia／EF Core の反射破壊を避ける）
- 出力: `artifacts/publish/<RID>/`（`.gitignore` に追加。成果物をコミットしない）

配布構成 ADR（着手時採番・想定 **0054**）に上記を確定する。フォント同梱によるサイズ増は許容し、sub-setting は今回採らない（S1 からの引き継ぎ。体感遅延が出たら別スライスで再評価）。

### 決定2: 初回判定は「Office が 0 件」

Domain の純粋関数:

```csharp
FirstRunPolicy.NeedsFirstRun(int officeCount) // officeCount < 1 → true
```

Application／App が `ListOfficesUseCase` の件数を渡す。**`OfficeDto` を Domain に持ち込まない**（依存方向違反になるため）。

DB ファイル有無は使わない。理由: 起動時に既に `EnsureSecuredStorage` と `Migrate` が走り、空の `tsumugi.db` が存在する。復元失敗・試験用空 DB でもウィザードを出すべきだから。

判定タイミング: migration 完了後、MainWindow 生成前。

### 決定3: 初回登録の入力範囲と管理者名の保存先

必須:

- 事業所番号
- 事業所名
- サービス種別（既定 `TypeB`）
- 地域区分（**`RegionGrade.None` はウィザードでは拒否**。既存 `OfficeView` は `None` 可だが、初回は請求に必要な級地を必ず選ばせる）

任意（初回で入力を促す）:

- 郵便番号・住所・電話番号
- **管理者（職氏名）** → 既存 `RepresentativeTitleAndName` に保存

schema／migration は追加しない。請求プロファイル・体制届は初回対象外（既存画面から後入力）。

`RegisterFirstRunUseCase` は新規永続化ロジックを持たず、既存 `RegisterOfficeUseCase` を呼ぶ薄いラッパ（または同等の Application 入口）とする。`actor` は既存画面と同様 `Environment.UserName` を ViewModel から渡す。

### 決定4: 専用 FirstRunWizardWindow（MainWindow より先）

起動フロー:

```
EnsureSecuredStorage → CompositionRoot → Migrate
  → ListOffices → FirstRunPolicy
  ├─ NeedsFirstRun = false → MainWindow
  └─ NeedsFirstRun = true  → FirstRunWizardWindow を desktop.MainWindow に設定
                               ├─ 登録成功 → 先に MainWindow を desktop.MainWindow に差し替え、
                               │              その後に Wizard を Close（順序厳守）
                               └─ キャンセル／閉じる → desktop.Shutdown()（MainWindow を開かない）
```

**Avalonia ライフサイクル**: Wizard を `MainWindow` にしたまま閉じると、既定の `ShutdownMode.OnMainWindowClose` でプロセスが終了し、置換前に落ちうる。成功時は **必ず MainWindow を先に代入してから** Wizard を閉じる。キャンセル時は明示的に `Shutdown()` する。

`ShutdownRequested`（終了時自動バックアップ）は、Wizard／Main いずれのパスでも lifetime 確定後に登録する。Wizard キャンセルで空 DB をバックアップしても害はない。

MainWindow 内切替や、空の通常画面上のモーダルは採らない。既存 MainViewModel／タブ構成を汚さない。

### 決定5: キャンセルはアプリ終了

ウィザードを途中で閉じた場合:

- MainWindow を開かない
- プロセスを終了する
- 次回起動で再度ウィザードを出す（Office がまだ 0 件のため）

未設定状態での通常運用を許容しない。

### 決定6: 手動 QA は macOS 実施・Windows 未実施チェック（AC 部分クローズ）

`docs/manual-qa.md` に両 OS のチェック表を置く。

| 項目 | S5 内 |
|---|---|
| 発行スクリプトの RID・フラグ検証 | 両 OS（スクリプト／静的テスト） |
| macOS (`osx-arm64`) 実機 smoke | **実施・記録** |
| Windows (`win-x64`) 実機 smoke | **未実施**として表に残す（後続運用タスク） |

S5 完了時の roadmap／CHANGELOG 表記例: 「AC4-9〜11: macOS まで完了。Windows 実機 smoke は未実施」。両 OS 完了を「AC クローズ」とは呼ばない。

smoke 手順（共通）:

1. 発行物をクリーン環境相当で起動
2. DB 作成と権限設定が行われる
3. 初回ウィザードが表示される
4. 事業所＋管理者（職氏名）を登録できる
5. 再起動後は通常 MainWindow（ウィザードなし）
6. 終了時に自動バックアップが走る（S3a）

### 決定7: 運用ガイドの必須節

`docs/operations.md`（新規・日本語）:

1. 初回セットアップ
2. 日常のバックアップ／復元（S3a 前提）
3. OS ディスク暗号化（FileVault／BitLocker。ADR 0003）
4. 請求 CSV の責務境界（生成まで。伝送・電子証明書は範囲外）
5. 障害時の連絡前確認（ログに氏名・証番号・フルパスを出さないことへの注意を含む）

S3b（暗号化バックアップ）は運用ガイドの前提にしない。

---

## 4. アーキテクチャ

```
App.axaml.cs (起動フロー)
  → SqliteLocationService.EnsureSecuredStorage
  → CompositionRoot.Build
  → Migrate
  → ListOfficesUseCase
  → FirstRunPolicy.NeedsFirstRun
  → FirstRunWizardWindow / MainWindow

FirstRunWizardViewModel
  → RegisterFirstRunUseCase
  → RegisterOfficeUseCase
  → IOfficeRepository
```

依存方向は既存どおり `App → Application → Domain`。UI は `DbContext` を触らない。

### 4.1 Domain（新規）

| 型 | 契約 |
|---|---|
| `FirstRunPolicy.NeedsFirstRun(int officeCount)` | `officeCount < 1` なら true。日付／I/O／DTO に依存しない |

配置は `src/Tsumugi.Domain/Logic/FirstRunPolicy.cs`（roadmap の Application 配置は純粋関数の置き場として不適切なので Domain へ移す。§2.2 と同様の実測修正）。

### 4.2 Application

| 追加 | 内容 |
|---|---|
| `RegisterFirstRunUseCase` | 入力 DTO → `RegisterOfficeUseCase`。actor は既存どおり呼び出し側が渡す |
| （任意）起動用 Query | `ListOfficesUseCase` をそのまま使ってよい |

既存 `RegisterOfficeUseCase` / `UpdateOfficeUseCase` / `OfficeView` は変更しない（ウィザードは別 View）。

### 4.3 App

| 追加 | 内容 |
|---|---|
| `FirstRunWizardViewModel` / `FirstRunWizardView` / Window | 専用 Window。キーボード完結・既存 AccessibilityDefaults に従う |
| `App.axaml.cs` | 起動分岐。テスト可能な起動フローへ分離（例: 小さな起動オーケストレータ） |
| `CompositionRoot` | Wizard VM / UseCase 登録 |

### 4.4 build / docs

| 追加 | 内容 |
|---|---|
| `build/publish.sh` / `build/publish.ps1` | 決定1 |
| `.gitignore` | `artifacts/` |
| ADR 0054（想定） | 配布構成 |
| `docs/operations.md` | 決定7 |
| `docs/manual-qa.md` | 決定6 |
| CHANGELOG / roadmap | S5 進捗反映 |

---

## 5. テスト方針

| 層 | 内容 |
|---|---|
| Domain | `FirstRunPolicy`（空→true、1件以上→false） |
| Application | 初回登録が `RepresentativeTitleAndName` を保存。必須欠落と重複番号は既存規則どおり失敗 |
| App | Wizard VM の成功／入力エラー／キャンセル状態。起動オーケストレータの分岐（初回→Wizard、登録済み→Main、キャンセル→終了） |
| 発行 | スクリプトが RID・self-contained・single-file・trim 無効を渡すことを固定（シェル／PowerShell の静的アサーション、または小さなラッパのユニットテスト） |
| 実機 | macOS smoke を `manual-qa.md` に記録。Windows は未実施行を残す |

---

## 6. 文書成果物

| 文書 | 内容 |
|---|---|
| 本 spec | 設計正本 |
| 実装計画 | `docs/superpowers/plans/2026-08-17-phase4-s5-distribution-and-first-run.md` |
| ADR 0054（想定） | 配布構成（決定1） |
| `docs/operations.md` | 運用ガイド |
| `docs/manual-qa.md` | 手動 QA 表＋macOS 記録 |
| roadmap / CHANGELOG | S5 完了／Windows 未実施の明示 |

専用 acceptance doc は必須としない（CHANGELOG + ADR + manual-qa で足りる。S2/S3a/S4 と同型）。

---

## 7. スコープ外

- S3b（外部媒体向け AES-256-GCM 暗号化バックアップ）
- Windows 実機 smoke の完了（表と手順のみ）
- フォント sub-setting／バイナリサイズ最適化
- 請求プロファイル・体制届の初回入力
- インストーラ（msi／pkg／dmg）。単一ファイル配布のみ
- オンライン更新・署名付き配布ストア掲載

---

## 8. 参照

- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §8.5
- `docs/decisions/0003-sqlite-location-and-encryption.md`
- `docs/decisions/0052-backup-operations.md`
- `docs/decisions/0013-pdf-engine-questpdf.md`（self-contained サイズの S5 引き継ぎ）
- `src/Tsumugi.App/App.axaml.cs`
- `src/Tsumugi.Application/UseCases/RegisterOfficeUseCase.cs`
- `src/Tsumugi.Domain/Entities/Office.cs`
