# ADR 0013: PDF エンジンとして QuestPDF Community License を採用（確定）

## 決定

**QuestPDF Community License** を採用する。日本語帳票フォントとして **Noto Sans JP（SIL OFL 1.1、Regular + Bold の 2 ウェイト）** を `Tsumugi.Infrastructure.Reporting` アセンブリに `EmbeddedResource` として同梱し、実行環境のフォント有無に依存せず帳票を生成する。

ライセンス判定・フォント登録は `QuestPdfLicenseConfigurator.Initialize()` に集約し、`appsettings`/環境変数ではなく**コードでハード設定**する（オフライン保証。ライセンスサーバへの問い合わせは発生しない）。

事業所（または運営法人）の年商が Community License の閾値に近づく／超える場合のフォールバック計画は本 ADR に**計画のみ**記載し、実装は必要になった時点で別 ADR を起こす。

## 背景

- Phase 2 で工賃明細 PDF・工賃支払一覧 PDF の 2 帳票を実装し、QuestPDF を採用した経緯は本 ADR の初版（0.1 版、`4696518`）で一次情報の裏付けが未確認のまま記録していた。
- CI 環境（Linux/Windows ランナー）で日本語フォントが解決できず CJK 文字が描画されない（NUL バイト化する）問題が判明し、CHANGELOG に「本番投入前に必須」の deferred として記録されていた（フォント埋込の要件化）。
- QuestPDF Community License は年商閾値を超過すると商用ライセンスが必要になる。就労継続支援B型事業所（および運営法人）の想定規模との整合を一次情報で確認しないまま初版の判断根拠としていたため、Phase 4 / S1 で一次情報を確認し（Task 0）、本 ADR を確定させる。

## 選択肢

- **A: QuestPDF Community + Noto Sans JP 埋込（採用）**: 既存の Reporting 実装（決定論的 PDF 出力、`TimeProvider` 注入含む）を維持しつつ、フォント同梱でクロスプラットフォームの CJK 描画を保証する。
- **B: QuestPDF Professional への移行**: ライセンス費用が発生。年商閾値未満である限り不要。
- **C: Avalonia 印刷経路（`PrintDialog` + ビジュアル `Print`）へ差し替え**: 実装コストが大きく、既存の決定論的 PDF 出力を捨てることになる。閾値超過時のフォールバック候補として温存する。

## 決定根拠

### QuestPDF Community License の適用条件（一次情報で確認済み）

Task 0 にて一次情報を再確認した（詳細な確認ログ・誤記訂正の経緯は `docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md` §15 を参照。以下は結論のみ）。

- 一次情報: `https://www.questpdf.com/license/community.html`（Community edition 概要）
- 一次情報: `https://www.questpdf.com/license/guide.html`（License Selection Guide — 適用単位の実ルールはこちらに記載）
- 年商閾値: community.html に *"businesses with less than USD 1,000,000 in annual revenue"* との記載あり。
- **適用単位（consolidated basis）**: 閾値の判定単位は個別事業所ではない。guide.html に次の verbatim 記載がある。

  > "An organisation with annual gross revenue under USD 1,000,000 in its most recently completed fiscal year, measured on a consolidated basis across entities under common control."
  >
  > （出典: https://www.questpdf.com/license/guide.html）

- 上記より、判定は「共通支配下にある事業体全体を合算（consolidated）した年商」で行われる。単一の就労継続支援B型事業所単体の年商はこの閾値を大きく下回るが、**法人単位で複数施設を運営している場合は法人全体の合算年商で判定される**。

### 発動条件（再判定トリガー）— 多施設運営法人の consolidated 年商確認

**これは付随的な注記ではなく、本 ADR の判断が有効であり続けるための第一級の条件である。**

Tsumugi の実顧客には、単一の就労継続支援B型事業所ではなく、**B型＋A型＋生活介護＋グループホーム等の複数施設を運営する社会福祉法人**が多く含まれる。QuestPDF Community License の年商閾値は個別事業所単位ではなく consolidated basis（共通支配下の事業体を合算）で判定されるため、次のいずれかに該当する場合は Community License の継続適用を**再判定**しなければならない。

- 運営法人が複数の施設・事業を運営しており、法人全体の合算年商が USD 1,000,000 に接近している、または既に超えている（超えることが見込まれる）場合。
- 導入先が単一事業所から多施設運営法人に変わる、または既存の多施設運営法人の合算年商が閾値に近づいたと判明した場合。

再判定の結果 Community License の適用対象外と判明した場合は、下記フォールバック計画（Professional 移行 または Avalonia 印刷経路）のいずれかを選択し、別 ADR を起こす。単一事業所導入時点では安全マージンが大きいため、本 ADR は Community License 採用を確定するが、**法人単位の consolidated 収益確認なしに「今後も一切問題ない」と断定するものではない**。

### Noto Sans JP（SIL OFL 1.1）の選定

- 選定: **Noto Sans JP**（Regular + Bold の 2 ウェイト）。Medium/Light 等の他ウェイトは帳票デザイン上不要のため同梱しない。
- ライセンス: **SIL OFL 1.1**（`https://openfontlicense.org/`）。商用同梱可能で、エンドユーザー向けの可視的な attribution 表示は不要。原著作権表示・Reserved Font Name の扱い・ライセンス本文同梱の 3 点が必須（詳細は spec §15 参照）。
- 取得元: `notofonts/noto-cjk` の GitHub Releases、`Sans2.004` タグの `16_NotoSansJP.zip`（`https://github.com/notofonts/noto-cjk/releases/download/Sans2.004/16_NotoSansJP.zip`）。Google Fonts の既定ダウンロードパッケージは `.ttf` 形式のため採用せず、`.otf` 形式が同梱される notofonts リリースを一次情報として採用した。
- ファミリー名: 埋め込むフォントバイナリの内部メタデータ上のファミリー名は `"Noto Sans JP"` であり、実装で参照する定数 `QuestPdfLicenseConfigurator.NotoSansJpFamilyName` と一致することを確認済み（Task 1 で `fc-scan` により確認）。
- 帰属表示: `NOTICE` に SIL OFL 1.1 の帰属を追記し、ライセンス全文を `assets/fonts/NotoSansJP.LICENSE.txt` として同梱する。

### 実装方式

- フォントファイル（`NotoSansJP-Regular.otf` / `NotoSansJP-Bold.otf` / `NotoSansJP.LICENSE.txt`）はリポジトリ直下 `assets/fonts/` に配置し、`Tsumugi.Infrastructure.Reporting.csproj` から `EmbeddedResource`（`LogicalName` を `Tsumugi.Infrastructure.Reporting.assets.fonts.*` に固定）としてアセンブリに埋め込む。
- 登録 API は **`QuestPDF.Drawing.FontManager.RegisterFont(Stream)`** を使う。設計時点で想定していた `RegisterFontFromStream` という API 名は QuestPDF 2025.4.0 には存在しない（Task 3 で発見した API ドリフト）。`RegisterFont(Stream)` は `.otf` 内部メタデータから Family/Weight/Style を自動検出するため、明示的なファミリー名指定は不要。
- `QuestPDF.Settings.UseEnvironmentFonts = false` を設定する。CI・実行環境ごとにインストール済みフォントの有無・バージョンが異なると描画結果が環境依存になり、決定論的 PDF 出力という設計方針（`TimeProvider` 注入と同じ思想）に反するため、環境フォントへのフォールバックを明示的に禁止し、埋込フォントのみを使用する。
- 上記 3 点（ライセンス種別設定・環境フォント抑止・フォント登録）は `QuestPdfLicenseConfigurator.Initialize()` に集約し、二重初期化を避けるため `lock` と `_initialized` フラグで冪等に実行する。

## 影響

- `assets/fonts/` に `.otf` 2 ファイル（Regular/Bold）+ `NotoSansJP.LICENSE.txt` を追加。`Tsumugi.Infrastructure.Reporting.dll` のマニフェストリソースサイズが増加する。self-contained 発行時の総バイナリサイズへの影響は Phase 4 / S5 で再評価する。
- `NOTICE` に OFL 1.1 の帰属表示（Noto Sans JP、取得元 URL、Copyright 表示）を追記済み。
- `QuestPdfLicenseConfigurator.Initialize()` にライセンス設定・フォント登録・環境フォント抑止を統合。呼び出し側（App の合成ルート・テストの static ctor）は 1 箇所から呼ぶだけでよい。
- 運用上、法人の事業拡大（多施設化・M&A 等）で consolidated 年商が閾値に接近した場合は、上記「発動条件」に従い再判定を行う運用ルールが発生する（自動検知の仕組みは持たない。運用側の年次確認に依存）。
- **既知の品質上の限界（follow-up）**: 「山」「田」「工」「支」「一」等、一部の漢字は QuestPDF/SkiaSharp が生成する `ToUnicode` CMap で康熙部首 (Kangxi Radicals, U+2F00-2FD5) のコードポイントとして書き出される。レンダリングされるグリフは正しいが、PDF テキスト抽出（`pdftotext`・PdfPig 等）ではこれらの部首文字が返り、氏名コピー＆ペーストや PDF 内検索で不整合が生じうる。QuestPDF/Noto Sans JP 側の生成物起因（Tsumugi 実装のバグではない）。本 ADR の採用判断（Community License + Noto Sans JP 埋込）を覆すものではないため未対応のまま追跡する。詳細は `docs/open-questions.md` § Phase 2 工賃計算「PDF テキスト抽出時の康熙部首誤置換」を参照。

## 閾値超過時のフォールバック計画（未実装・計画のみ）

上記「発動条件」に該当し、Community License の継続適用が困難と判明した場合、以下のいずれかを選択する。

1. **Professional 移行**: ライセンス費用は事業所（または運営法人）負担。既存実装は無変更で稼働継続できる。
2. **Avalonia 印刷経路への差し替え**: `IWageReportGenerator` の実装を Avalonia の `PrintDialog` + ビジュアル `Print` に差し替える。決定論的 PDF 出力（バイト単位の再現性）は失われる。参考実装として Avalonia の `Print` API と `IStorageProvider` を組み合わせる方式を想定する。

どちらを選ぶかは運用側で判断し、必要になったタイミングで本 ADR とは別の ADR を起こす。本 ADR ではこれ以上の実装詳細を確定しない。

## 参照

- Phase 4 / S1 設計仕様（一次情報確認ログの詳細は §15）: `docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md`
- Phase 4 / S1 実装計画: `docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md`
- 親 Phase 4 指示: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「(A) 帳票フォント・ライセンス」
- `docs/open-questions.md` § Phase 2 工賃計算「QuestPDF ライセンス」「PDF 帳票の日本語フォント埋込」（本 ADR により解消）
- QuestPDF Community License: https://www.questpdf.com/license/community.html
- QuestPDF License Selection Guide（適用単位ルールの一次情報）: https://www.questpdf.com/license/guide.html
- SIL OFL 1.1: https://openfontlicense.org/
- Noto Sans JP 配布元（notofonts/noto-cjk リリース）: https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004
