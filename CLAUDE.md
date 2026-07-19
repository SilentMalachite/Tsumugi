# CLAUDE.md — Tsumugi

> Claude Code が毎セッション参照するプロジェクト常設指示。**恒久ルール・コマンド・依存方向・ドメイン用語**のみを置く。
> 機能ごとの詳細仕様は別文書（下記「仕様の所在」）にあり、ここでは重複させない。指示が矛盾する場合は本書の禁止事項（§ハード制約）が最優先。

---

## プロジェクト概要

就労継続支援B型事業所向け、ポチパス相当の **オフライン専用デスクトップアプリ**。機能は **記録 ＋ 工賃 ＋ 国保連請求データ生成**。
コードネーム `Tsumugi`（紡ぎ）。

**責務境界（最重要・誤解防止）**: 国保連請求は **「仕様準拠の請求データ(CSV)を生成し、公式の取込・送信システムにインポートできる状態まで」** が責務。**伝送・電子証明書・回線処理はアプリの範囲外**。これによりオフラインを保つ。

---

## 技術スタック

- .NET SDK **10.0**（`net10.0`、LTS）／**言語は C# 14 を前提**（`<LangVersion>` を下げない）
- Avalonia **11.x**（Fluentテーマ）＋ `CommunityToolkit.Mvvm`（MVVM、ソースジェネレータ）
- EF Core **10.x** ＋ `Microsoft.EntityFrameworkCore.Sqlite`
- DI/ホスト: `Microsoft.Extensions.DependencyInjection` / `.Hosting`
- テスト: `xUnit` ＋ `FluentAssertions`
- 開発: Rider（非商用）。**CLIビルド/テストが常に通ること**。運用はWindows端末も想定 → クロスプラットフォーム必須。

> **C# / Avalonia / EF Core の具体的なコーディング規律は `docs/csharp-conventions.md` を必読。** レガシー構文・WPF/MAUI混入・EF Coreの反映タイミングなど、本プロジェクトで最も事故が起きやすい3点を具体名で固定している。

---

## ソリューション構成と依存方向（厳守）

```
src/
  Tsumugi.Domain/          record・値オブジェクト・報酬算定/工賃の純粋ロジック。外部依存なし。
  Tsumugi.Application/     ユースケース・リポジトリ抽象(interface)・DTO・入力検証。Domainのみ参照。
  Tsumugi.Infrastructure/  EF Core DbContext・SQLite・リポジトリ実装・バックアップ・CSV出力・DB保存先サービス。
  Tsumugi.App/             Avalonia UI(MVVM)。合成ルート(DI構成)はここだけ。
tests/
  Tsumugi.Domain.Tests/  Tsumugi.Application.Tests/  Tsumugi.Infrastructure.Tests/  Tsumugi.App.Tests/
```

- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。**DomainはInfrastructure/UI/EF/Avaloniaを一切知らない**。
- **UIから `DbContext` を直接触らない**。必ずApplication層のユースケース経由。
- 違反はアーキテクチャテストで落とす。

---

## コマンド

```
dotnet tool restore                         # dotnet-ef 等のローカルツール復元
dotnet build                                # 警告ゼロが前提
dotnet test                                 # 全緑が前提
dotnet test --collect:"XPlat Code Coverage" # カバレッジ（Domain ≧ 95% 目標、報酬算定は 100% 目標）
dotnet format --verify-no-changes           # 整形チェック
./build/ci.sh                               # 品質ゲート一括（push前に必ず緑）
dotnet run --project src/Tsumugi.App        # アプリ起動

# EF Core マイグレーション（startup は合成ルートのApp）
dotnet ef migrations add <Name> --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App
dotnet ef database update            --project src/Tsumugi.Infrastructure --startup-project src/Tsumugi.App

dotnet list package --vulnerable --include-transitive  # 脆弱性確認
```

---

## コーディング規約

- エンティティは **`record` ＋ 追記型(append-only)**。訂正は元を壊さず訂正レコードで履歴を残す。
- 報酬算定・工賃は **副作用のない純粋関数**（日付/乱数/I/Oに依存しない）。入出力を値で閉じる。
- 主キーは `Guid`。`作成日時`/`作成者` を持つ。更新は **楽観的同時実行（更新トークン方式。SQLiteに `rowversion` なし）**。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。
- 1画面1責務。状態を画面間で暗黙共有しない。可変状態の追跡を最小化する。
- TDD: Red→Green→Refactor。1コミット=1論理変更。メッセージにフェーズ番号と受け入れ基準IDを記す。

---

## ハード制約（常に成立。CIで機械判定できるものは判定する）

1. **オフライン**: `Tsumugi.App` を含む**全プロダクションアセンブリ**で通信API使用を検出・禁止（各アセンブリ自身の参照のみ走査、推移閉包は対象外）。不可避な参照は理由付きallowlistに限定し既定は空。
2. **国保連は生成のみ**: 伝送・電子証明書処理を実装に混ぜない。混入はスコープ逸脱として扱う。
3. **報酬告示・CSV仕様の外部化**: 単位数/加算/地域区分単価/CSVフィールドを**ハードコードしない**。DBテーブル＋シードJSONで `適用開始年月` バージョン管理。出典バージョンをデータに保持。**公式資料から一意に確定できない値は推測で埋めず `docs/open-questions.md` へ**。
4. **データ保護**: SQLite保存先の権限を作成時に強制（Unix: ディレクトリ`0700`/DBファイル`0600`、Windows: 現在ユーザーのみDACL・継承無効）。WAL/SHMサイドカーもディレクトリ権限で保護。ログに氏名・受給者証番号・保存先フルパスを出さない。
5. **アクセシビリティ既定**: ダークテーマ・低アニメーション（Reduce Motion相当）・広い余白/行間・フォント拡大追従・**頻用操作はキーボードで完結**。テーマ/設定として実装しハードコードしない。
6. **クロスプラットフォーム**: macOS/Windows双方でビルドとViewModelテストが緑。パス区切り・改行・カルチャ依存（数値/日付整形）にOS差の地雷を作らない。
7. **バックアップ**: `VACUUM INTO` による単一ファイルバックアップ手段を維持。

> 設計判断（暗号化採否・PDFライブラリ・端数規則・権限ポリシー等）は `docs/decisions/` にADR（1件1ファイル、結論→背景→選択肢→決定→影響）として残す。

---

## ワークフロー

- **本書(Claude Code)が実装、Codexがレビュー**。Codex指摘は重大度付きで返り、Claude Code向け修正指示に**トリアージして取り込む**。
- 不明点・公式仕様の未確定事項は推測で実装せず `docs/open-questions.md` に起票し、設計相談へ回す。
- フェーズは 0→1→2→3 の順（基盤→記録→工賃→国保連請求）。前フェーズの受け入れ基準を満たすまで次に着手しない。
- **現在地**: フェーズ0・1・2とPhase 3-0は完了。Phase 3-1（最小垂直スライス、spec: `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md`、計画: `docs/superpowers/plans/2026-07-19-phase3-1-minimal-vertical-slice.md`）は実装完了・受け入れ証跡記録済み（`docs/phase3-1-acceptance.md`）で、最終ブランチレビュー通過後に**mainへマージ済み**（2026-07-19、fast-forward、ブランチ削除済み）。残る受け入れ項目はGUI手動での貫通確認（プレビュー→確定→取下げ）のみ。基本報酬135行(R6、R8-06継続確認済み)＋主要加算16行(ADR 0028)＋地域単価8＋負担上限4＋遷移規則3をseed済み。保護施設・基準該当B型、R8改定対象12区分の新単価、R8-06処遇改善率等は引き続き凍結・スコープ外で`docs/open-questions.md`へ起票済み。次はPhase 3-2帳票（2026-06-29付の旧Phase 3-2計画は再設計前提のため、着手時に本スライスの成果物に合わせて再計画する）。

---

## 仕様の所在（詳細はここを読む。本書には複製しない）

- `01_ClaudeCode_実装指示書_Tsumugi.md` — 全体仕様・フェーズ別受け入れ基準（正本）
- `03_ClaudeCode_Phase0修正指示_Tsumugi.md` — フェーズ0の修正（オフライン検査のApp対応・DB権限）
- `04_ClaudeCode_Phase1実装指示_マスタと記録_Tsumugi.md` — フェーズ1（マスタ・記録）の実装指示と受け入れ基準
- `05_ClaudeCode_Phase2実装指示_工賃計算_Tsumugi.md` — フェーズ2（工賃計算）の実装指示と受け入れ基準
- `docs/phase2-acceptance.md` — フェーズ2 受け入れ基準セルフチェック
- `06_ClaudeCode_Phase3実装指示_国保連請求_Tsumugi.md` — フェーズ3（国保連請求）の実装指示と受け入れ基準
- `docs/phase3-0-acceptance.md` — Phase 3-0受け入れ証跡と後続進捗
- `docs/phase3-1-acceptance.md` — Phase 3-1（最小垂直スライス）受け入れ証跡
- `docs/superpowers/specs/2026-07-19-phase3-1-minimal-vertical-slice-redesign-design.md` — Phase 3-1再設計spec（正本）
- `docs/superpowers/plans/2026-07-19-phase3-1-minimal-vertical-slice.md` — Phase 3-1実装計画（進捗はチェックボックスが正）
- `docs/superpowers/specs/2026-07-13-phase3-1-task12-claim-master-schema-v2-design.md` — claim-master schema v2のclosed contract設計
- `docs/superpowers/specs/2026-07-14-phase3-1-task13-protected-facility-b-formula-and-source-inventory-design.md` — 基準該当B型の公式計算契約・runtime入力要件・source inventory設計
- `docs/decisions/` — ADR / `docs/open-questions.md` — 未確定事項

> Codex レビューのフォーマット（重大度・観点カテゴリ・該当行・推奨アクション・根拠）は本書のワークフロー節に従う。

---

## ドメイン用語（最小）

- **就労継続支援B型**: 障害福祉サービスの一種。本アプリの第一対象。
- **利用者 / 受給者証**: サービス利用者と、その受給者証（有効期間・支給量を持つ。期限アラート対象）。
- **実績記録票**: 月次のサービス提供実績記録（送迎・欠席時対応等を含む）。
- **工賃**: 利用者に支払う作業対価（職員給与ではない）。Σ配分＝原資 を不変条件とする。
- **国保連**: 国民健康保険団体連合会。請求先。
- **報酬告示 / 単位数 / 加算 / 地域区分単価**: 報酬算定の根拠。**報酬改定は3年ごと**（令和6年=現行、令和9年=次期）。`適用開始年月`で差し替える。
- **取込・送信システム / 電子請求受付システム**: 公式の伝送系。**アプリの範囲外**。

---

## Claude Code 運用メモ

- 本リポジトリは**日本語の識別子・コメント・ドメイン語が多い**。CJK＋コードのトークン化が不安定なモデル版では出力が崩れやすいため、安定する版を選ぶこと。
- サブエージェントは軽量モデル指定で十分なことが多い（例: `CLAUDE_CODE_SUBAGENT_MODEL=claude-sonnet-4-6`）。
- 大きな変更は小さなコミットに割る。構造テスト（依存方向・オフライン）は**意図的な違反を入れると赤になる**ことを確認し、歯のある状態を保つ。
- C# は Elixir と比べ、学習データの世代ズレ・Avalonia/EF Core の知識の薄さ・規律を強制しない言語特性のため、**古い/別フレームワークの書き方が紛れやすい**。詰まったら原因を `docs/csharp-conventions.md` の 1／2／3 のどれに該当するか切り分けてから直す。
