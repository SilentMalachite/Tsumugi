# Tsumugi Phase 4 / S1 設計 — 日本語フォント埋込 + QuestPDF ライセンス確定

> **Source**: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「(A) 帳票フォント・ライセンス」を接地したもの。
> **位置づけ**: Phase 4 残余 6 スライスのうち **S1**（`docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §2）。他スライスから独立、S1 単独で PR 化・レビュー可能。
> **Status**: 設計合意済（2026-07-05）。実装計画は writing-plans スキルで本書から派生させる。

---

## 1. 目的

- CHANGELOG「本番投入前に必須の deferred」の 2 件（QuestPDF Community License 閾値・日本語フォント埋込）をクローズ。
- Phase 3-2（実績記録票・請求明細書 PDF）の**フォント前提を先行解消**する。
- ADR 0013 を「暫定」から「確定」へ書き換える。

---

## 2. 責務境界

- 帳票 PDF に限定。Avalonia UI 側のフォントは対象外（既に OS フォントに追従、CJK 表示は問題なし）。
- **QuestPDF Community から Professional への切替は行わない**（S1 の判断で年商閾値未満を確定）。閾値超過時のフォールバック（Avalonia 印刷経路）は本 S1 では実装せず、ADR 0013 に「発動条件と実装計画」のみ記す。
- フォント同梱の**ライセンス表記**は `NOTICE` に追加（SIL OFL 1.1 の Reserved Font Name 記載も含む）。フォント本体は `assets/fonts/` に置き、Reporting アセンブリの EmbeddedResource として同梱。

---

## 3. 一次情報確認事項（S1 着手時に確認して本書に URL 転記）

| 項目 | 一次情報 | 確認内容 |
|---|---|---|
| QuestPDF Community 閾値 | https://www.questpdf.com/license/ （公式ライセンスページ） | 年商 USD 1M 未満で Community 使用可の要件と、超過時の Professional 移行条件 |
| SIL OFL 1.1 全文 | https://openfontlicense.org/ | OFL 帰属表記の必須要件（Copyright／Reserved Font Name／ライセンス文の同梱） |
| Noto Sans JP 配布物 | https://fonts.google.com/noto/specimen/Noto+Sans+JP | 実際のフォントファイル形式・ウェイト・サイズ・OFL 適用範囲 |

**判断分岐**: 一次情報確認で QuestPDF Community 閾値の適用単位（会社年商 vs 個別事業所年商）が就労B型事業所の想定規模と整合するかを確定する。整合しない場合はロードマップ §2 の「ブロッカー」に該当し S1 保留（他系統へ進む）。

---

## 4. スコープ

### 4.1 含む

- **フォントファイル同梱**: `assets/fonts/NotoSansJP-Regular.otf` / `NotoSansJP-Bold.otf` の 2 ウェイト（Regular + Bold）。**Noto Sans JP を採用**（§5.1 で選定理由）。
- **QuestPdfLicenseConfigurator の拡張**: `Initialize()` メソッドを新設し、ライセンス設定＋環境フォント抑止＋フォント登録を 1 度きり実行。既存 `ApplyCommunityLicense()` は削除して `Initialize()` に一本化（CLAUDE.md §運用メモ「backwards-compatibility hacks を導入しない」規約に従う、既存呼出は内部のみで公開 API がない）。
- **フォント指定の全 QuestPDF ドキュメントへの適用**: `WageStatementPdfGenerator` の全 `Document.Create` に `DefaultTextStyle` で Noto Sans JP を指定。既存のフォールバック依存を撤去。
- **CJK / Bold ASCII substring assertion 復活**: `WageStatementPdfGeneratorTests` / `WagePaymentListPdfGeneratorTests` の除去済み assertion を追加。macOS/Linux/Windows CI で緑にする。
- **NOTICE 更新**: OFL 帰属＋ Reserved Font Name＋ LICENSE.txt への相対リンク。
- **ADR 0013 の書き換え**: 一次情報 URL 付きで「Community 採用確定」へ。閾値超過時のフォールバック計画も同 ADR に記載。
- **open-questions クローズ**: 「QuestPDF ライセンス」と「PDF 帳票の日本語フォント埋込」の 2 項。
- **CHANGELOG 更新**: 「本番投入前に必須の deferred」から該当 2 件を削除。
- **Phase 3-2 プラン更新**: `docs/superpowers/plans/2026-06-29-phase3-2-reports.md` からフォント前提タスクを除去。

### 4.2 含まない

- **QuestPDF Professional への移行**（S1 は Community 継続を確定）。
- **Avalonia UI 側のフォント指定**（既に OS フォントで日本語表示可、Reduce Motion 等も既存規約でカバー）。
- **フォント sub-setting / 圧縮**（`.otf` をそのまま同梱。バイナリサイズは Regular + Bold で ~10MB。self-contained 単一ファイル発行では S5 スコープで再検討）。
- **Bold 以外のウェイト**（Medium/Light 等は帳票デザインで使用しないため非同梱）。
- **他フォント（等幅／欧文専用）**: 数字・記号は Noto Sans JP に含まれるためカバー可。

### 4.3 独立性

- S1 は Domain/Application に触れない。Infrastructure.Reporting とテスト、`NOTICE`、`assets/fonts/`、ADR/open-questions/CHANGELOG のみで完結。**S2〜S5 のいずれとも依存関係なし**。

---

## 5. 設計判断

### 5.1 フォント選定: Noto Sans JP を採用

| 選択肢 | サイズ (Regular+Bold) | 対応範囲 | 判断 |
|---|---|---|---|
| **Noto Sans JP** ✅ | 約 10 MB | 日本語（漢字・かな）+ ASCII + 数字 + 記号 | **採用**。就労B型帳票の必要範囲を過不足なくカバー。 |
| Noto Sans CJK JP (フル) | 約 20 MB | JP+CN+KR+一部 CJK 拡張 | 過剰。CJK 統合漢字の中国簡体・韓国変種はアプリで使わない。 |
| IPAex Gothic | 約 12 MB | 日本語（漢字・かな）+ 欧文 | ライセンスは IPA License（BSD 相当）で問題なし。ただし Google/Adobe の Web 表示標準になっていないため、UI 側で「他 PDF ビューアで見た時の見た目差」がユーザに違和感を生みうる。**次点**。 |

- 数字・記号のグリフ品質: Noto Sans JP のプロポーショナル/等幅がどちらも整っているため、金額の桁揃えは問題なし。
- Bold と Regular の 2 ウェイト。QuestPDF の `.Bold()` は Bold ウェイトを引くため、両ファイルの明示登録が必須。
- **フォント sub-setting は行わない**: `.otf` 全体を埋め込む。sub-setting は帳票内容の変化（新規利用者の氏名追加）で再ビルド必要になり運用性を損なう。self-contained 発行時のサイズは S5 で再評価。

### 5.2 埋込方式: EmbeddedResource → RegisterFontFromStream

- **`assets/fonts/*.otf` を `Tsumugi.Infrastructure.Reporting.csproj` の `<EmbeddedResource>` として同梱**。`.otf` は Reporting アセンブリのマニフェストリソースに入り、`Assembly.GetManifestResourceStream("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Regular.otf")` で取り出せる。
- **QuestPDF の `FontManager.RegisterFontFromStream(Stream)`** に渡す（`RegisterFontFromEmbeddedResource` は QuestPDF 側で削除／改名されている可能性があるため、ストリーム API を使う方が実装依存性が低い）。
- 登録は **プロセス全体で 1 度きり**。`Initialize()` にまとめ、起動時（`App.axaml.cs`）＋テスト static ctor で呼ぶ。
- **`Settings.UseEnvironmentFonts = false` を明示**（環境フォントに解決が漏れることを防ぎ、CI と本番で挙動一致）。

### 5.3 QuestPDF DefaultTextStyle への注入

- 全ての `Document.Create` の内側で `p.DefaultTextStyle(x => x.FontFamily(NotoSansJpFamilyName))` を呼ぶ。
- `WageStatementPdfGenerator` の既存 `.FontSize(14).Bold()` チェーンは維持。DefaultTextStyle はフォントファミリ指定のみ、サイズ・ウェイトは既存を尊重。
- **QuestPDF に登録される Family 名**は `.otf` の内部メタデータで決まる。実装時に `FontManager` で確認して Reporting アセンブリ内 `internal const string NotoSansJpFamilyName = "..."` として保持し、単一の真実源にする。

### 5.4 NOTICE の帰属記載

OFL 1.1 の帰属要件:
1. 原著作権表示
2. Reserved Font Name の記載
3. ライセンス全文の同梱

`NOTICE` に追記する内容:

```
- Noto Sans JP — SIL Open Font License 1.1 — https://github.com/notofonts/noto-cjk
  Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source Han Sans' and 'Noto Sans CJK'.
  ライセンス本文: assets/fonts/NotoSansJP.LICENSE.txt
```

`assets/fonts/NotoSansJP.LICENSE.txt` に OFL 1.1 全文を格納。

### 5.5 フォント登録の冪等性

- QuestPDF の `FontManager.RegisterFontFromStream` は同名 Family 二度目の登録で例外を投げる可能性がある（バージョンにより挙動差）。
- **冪等化**: `QuestPdfLicenseConfigurator` に private `static bool _initialized` を持ち、二度目以降は no-op にする。ロックは `lock` 文で十分（頻度が低い）。

---

## 6. アーキテクチャ変更

依存方向・レイヤ規律は既存維持（変更なし）。

```
src/
  Tsumugi.Infrastructure.Reporting/
    QuestPdfLicenseConfigurator.cs                [改修] Initialize() 一本化、フォント登録、UseEnvironmentFonts=false、冪等化
    WageStatementPdfGenerator.cs                  [改修] DefaultTextStyle でフォント指定
    Tsumugi.Infrastructure.Reporting.csproj       [改修] <EmbeddedResource> 追加
    assets/fonts/                                 [新規] 同梱リソース
      NotoSansJP-Regular.otf                      [新規]
      NotoSansJP-Bold.otf                         [新規]
      NotoSansJP.LICENSE.txt                      [新規] OFL 1.1 全文

  Tsumugi.App/App.axaml.cs                        [改修] ApplyCommunityLicense() → Initialize()

NOTICE                                            [改修] OFL 帰属追記

tests/
  Tsumugi.Infrastructure.Reporting.Tests/
    QuestPdfFontRegistrationTests.cs              [新規] 埋込リソース解決・Family 登録・冪等性
    WageStatementPdfGeneratorTests.cs             [改修] CJK / Bold ASCII substring assertion 復活
    WagePaymentListPdfGeneratorTests.cs           [改修] CJK / Bold ASCII substring assertion 復活

docs/
  decisions/0013-pdf-engine-questpdf.md           [改修] 一次情報 URL 付きで確定
  open-questions.md                               [改修] 2 項クローズ
  superpowers/plans/2026-06-29-phase3-2-reports.md [改修] フォント前提タスク除去

CHANGELOG.md                                      [改修] 本番投入前 deferred から 2 件除去 + [Unreleased] に S1 完了エントリ追加
```

---

## 7. 実装詳細

### 7.1 `QuestPdfLicenseConfigurator` 拡張

```csharp
using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Tsumugi.Infrastructure.Reporting;

public static class QuestPdfLicenseConfigurator
{
    internal const string NotoSansJpFamilyName = "Noto Sans JP"; // 実装時に FontManager で確認して確定

    private static readonly object _sync = new();
    private static bool _initialized;

    /// <summary>アプリ起動時・テスト static ctor で 1 度だけ呼ぶ。ライセンス + フォント登録 + 環境フォント抑止を一気に適用する。</summary>
    public static void Initialize()
    {
        lock (_sync)
        {
            if (_initialized) return;

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = false;   // CI と本番で挙動一致
            RegisterFontsFromEmbeddedResources();

            _initialized = true;
        }
    }

    private static void RegisterFontsFromEmbeddedResources()
    {
        var asm = typeof(QuestPdfLicenseConfigurator).Assembly;
        RegisterOne(asm, "Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Regular.otf");
        RegisterOne(asm, "Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Bold.otf");
    }

    private static void RegisterOne(Assembly asm, string resourceName)
    {
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"埋込フォントが見つかりません: {resourceName}");
        FontManager.RegisterFontFromStream(stream);
    }
}
```

- 既存呼出 3 箇所（`App.axaml.cs`、2 つのテスト static ctor）は `Initialize()` へ改名する。旧 `ApplyCommunityLicense()` は削除。
- `CLAUDE.md` §運用メモ「backwards-compatibility hacks を導入しない」に従い、後方互換 API を残さない。

### 7.2 csproj への EmbeddedResource 追加

```xml
<ItemGroup>
  <EmbeddedResource Include="assets\fonts\NotoSansJP-Regular.otf" />
  <EmbeddedResource Include="assets\fonts\NotoSansJP-Bold.otf" />
  <EmbeddedResource Include="assets\fonts\NotoSansJP.LICENSE.txt" />
</ItemGroup>
```

- `.NET SDK` は `\` と `/` を正規化するが、既存 csproj の書き方に合わせる。
- **LICENSE.txt も同梱**: 埋込リソースとして `NOTICE` から相対リンク＋ Reporting アセンブリ内に持たせ、配布物が単一 DLL でもライセンス条項が失われないようにする。

### 7.3 PDF 生成側の DefaultTextStyle 注入

`WageStatementPdfGenerator.GenerateStatement` / `GeneratePaymentList` の各 `Document.Create` 内で:

```csharp
c.Page(p =>
{
    p.Size(PageSizes.A4);
    p.Margin(2, Unit.Centimetre);
    p.DefaultTextStyle(x => x.FontFamily(QuestPdfLicenseConfigurator.NotoSansJpFamilyName));  // ← 追加
    // 以降既存のまま
});
```

- **Header の `.FontSize(14).Bold()` は Family 継承**。Bold は登録した Bold ウェイトが自動選択される。
- Footer 内 `t.Span(...)` も同じく継承。個別に FontFamily を書かない。

### 7.4 テストの CJK / Bold ASCII assertion 復活

`WageStatementPdfGeneratorTests`:

```csharp
text.Should().Contain("12,345", because: "金額は N0 形式で桁区切り");
text.Should().Contain("2026", because: "対象年がヘッダに出る");
// 新規復活（S1）
text.Should().Contain("山田太郎", because: "利用者名 (漢字) がフォント埋込で抽出可能");
text.Should().Contain("ヤマダタロウ", because: "カナが抽出可能");
text.Should().Contain("テスト事業所", because: "事業所名 (ひらがな含む漢字) が抽出可能");
text.Should().Contain("工賃明細", because: "帳票タイトル (漢字) がヘッダ Bold で抽出可能");
```

`WagePaymentListPdfGeneratorTests`:

```csharp
// 既存
text.Should().Contain("12,000");
text.Should().Contain("8,000");
// 新規復活（S1）: Bold 行の合計・平均を検証
text.Should().Contain("20,000", because: "合計行の Bold ASCII が抽出可能");
text.Should().Contain("10,000", because: "平均行の Bold ASCII が抽出可能");
text.Should().Contain("工賃支払一覧", because: "ヘッダ Bold 漢字が抽出可能");
```

「フォント埋込未実装ゆえの注記」コメントは撤去する。

---

## 8. テスト戦略

### 8.1 新規テスト: `QuestPdfFontRegistrationTests`

- **埋込リソースの解決**: `typeof(QuestPdfLicenseConfigurator).Assembly.GetManifestResourceStream(name)` が非 null であること（2 フォント + LICENSE.txt の 3 件）。
- **Initialize の冪等性**: `Initialize()` を 3 回連続呼んでも例外にならない。
- **Settings.UseEnvironmentFonts が false**: `Initialize()` 後に `QuestPDF.Settings.UseEnvironmentFonts` が false になっていること。
- **Family 定数の検証**: `NotoSansJpFamilyName` が空でないこと（実装時に `FontManager` で確認した名前と一致）。

### 8.2 改修テスト: PDF 抽出

- 既存の ASCII/数字 assertion に加え、CJK 3 系統（漢字・ひらがな・カタカナ）と Bold 数字を追加。
- **クロスプラットフォーム CI 検証**: macOS/Linux/Windows のいずれでも緑（フォント埋込により OS 依存が消える）。
- `[Fact]` のまま、テーブル駆動化は不要。

### 8.3 意図的違反（歯のある検査）

- CI ワークフローでは行わないが、**S1 実装時に手動で** `NotoSansJP-Regular.otf` を一時的に csproj から外す → テストが赤になることを確認 → 戻す。**この検証は commit しない**（S1 では ASCII/CJK assertion 自体が既に強力な赤化トリガーとなるため）。

### 8.4 CI カバレッジ / 品質ゲート

- Domain 95% 閾値には影響なし（Reporting はカバレッジ集計対象外）。
- `build/ci.sh` はそのまま緑を維持。
- `dotnet format --verify-no-changes` は既存規則で緑（新規 csproj エントリの indent を既存に揃える）。
- `OfflineComplianceTests` は影響なし（`Assembly.GetManifestResourceStream` は `System.Reflection` API で `System.Net.*` に触れない、allowlist 変更不要）。

---

## 9. ADR 影響

### ADR 0013（改修・確定化）

**変更内容**:

1. 「暫定」ラベル削除。
2. 一次情報 URL を「決定」節に転記。
3. Community 継続の判断根拠を明記:
   - QuestPDF Community 適用条件（一次情報から転記した年商閾値）
   - 対象事業所の想定規模との整合（就労B型は年商 USD 1M 未満が現実的）
4. **フォント埋込の決定を追記**:
   - フォント: Noto Sans JP（Regular + Bold）
   - 埋込方式: `EmbeddedResource` → `FontManager.RegisterFontFromStream`
   - `UseEnvironmentFonts = false` の理由
5. **閾値超過時のフォールバック計画**（実装は行わない）:
   - 発動条件: 事業所年商が Community 閾値を超過した場合
   - 対処: Reporting 層を Avalonia 印刷経路（PrintDialog → ビジュアル Print）に差し替え
   - 参考実装: Avalonia の `Print` API と `IStorageProvider` の組合せ

### ADR 追加はなし

- OFL 帰属方針は NOTICE に閉じるため ADR 化不要。
- フォント選定（Noto Sans JP）自体は「デザイン判断」だが、ADR 0013 の「フォント埋込の決定」節に統合する（新規 ADR 分割はコスト過大）。

---

## 10. リスク

| # | リスク | 影響 | 緩和策 |
|---|---|---|---|
| R1 | QuestPDF Community 閾値の一次情報確認で「事業所年商が閾値近傍」と判明 | S1 が保留に転じる（ADR 0013 の決着が Professional 移行または Avalonia 印刷へフォールバック検討へ変質） | 一次情報確認を **S1 実装着手の最初のタスク**に置く。閾値近傍が判明した時点で S1 を止め、他系統（S2 bulk 禁止）へ切替。ADR 0013 に「Professional 移行時のライセンス費用の負担者は事業所」を明記して判断を運用側に委ねる。 |
| R2 | Noto Sans JP の Family 名がテスト仮置き `"Noto Sans JP"` と実登録名で相違 | PDF 生成時にフォント解決失敗 → CI 赤 | 実装時に `FontManager.EnumerateFonts()` 相当で登録名を確認して定数化。定数を Reporting アセンブリ内 `internal const string NotoSansJpFamilyName = "..."` として保持。 |
| R3 | QuestPDF のバージョン更新で `FontManager.RegisterFontFromStream` の API シグネチャが変わる | ビルド不能 | 現行 QuestPDF 2025.4.0 で固定。API 変更のリスクは Reporting プロジェクトの `<PackageReference>` を pin することで抑える（既に固定バージョン指定）。将来のバージョン更新時に spec の §7.1 を再確認する。 |
| R4 | `.otf` バイナリを git 管理下に置くことで LFS 要否や差分肥大化 | リポジトリサイズ増（~10MB） | LFS は導入しない（.otf は同梱物として 1 回追加後は更新頻度が極めて低い）。追加コミット時に `--stat` で肥大を確認。将来フォントを差替える場合は既存 `.otf` を削除してから新規追加する（バイナリ diff が git に残らない扱い）。 |
| R5 | 埋込フォントが `System.Reflection.Assembly.GetManifestResourceStream` で解決される流れが `OfflineComplianceTests` の走査で偽陽性を出す | オフライン検査が誤検知 | `Assembly.GetManifestResourceStream` は `System.Reflection` API であり `System.Net.*` に触れない。**既存 `OfflineComplianceTests` の allowlist 変更は不要**。念のため CI で緑を確認してから commit。 |
| R6 | Bold/Regular の内部フォント名（PostScript name / Full name）が QuestPDF の期待と不一致 → Bold が拾えない | Bold 部分がフォールバック描画 → CJK NUL 化再発 | Regular と Bold の両方を明示登録（§7.1）。テスト側で Bold 数字 `"20,000"` の substring を assertion に含めることで検出可能（§7.4）。 |
| R7 | フォント同梱により `Tsumugi.Infrastructure.Reporting.dll` サイズが増加、self-contained 発行（S5）の単一ファイル起動時間が悪化 | 起動体感遅延（初回のみ） | S5 の smoke で計測。悪化が体感レベルなら sub-setting を再検討（S5 の判断）。S1 の判断はサイズ増を許容する。 |

---

## 11. 受け入れ基準（Phase 4 AC への寄与）

`07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §7 の以下を本スライスでクローズ:

- **AC4-1**（日本語フォント埋込＋CJK assertion＋NOTICE 帰属）: ✅ **完全達成**。macOS/Linux/Windows CI で漢字・かな・カタカナ・Bold ASCII の substring 検証が緑。
- **AC4-2**（QuestPDF ライセンス ADR 0013 確定）: ✅ **完全達成**（一次情報確認で Community 継続が現実的な場合）。近傍/超過の場合はリスク R1 の分岐で保留扱い。

---

## 12. S1 完了の必要十分条件

`build/ci.sh` 緑 + 以下全項目:

1. `assets/fonts/NotoSansJP-Regular.otf` / `NotoSansJP-Bold.otf` / `NotoSansJP.LICENSE.txt` の 3 ファイルが `assets/fonts/` に存在し、`Tsumugi.Infrastructure.Reporting.csproj` の `<EmbeddedResource>` に登録されている。
2. `QuestPdfLicenseConfigurator.Initialize()` が 冪等・`Settings.License = Community`・`Settings.UseEnvironmentFonts = false`・2 フォント登録を 1 度きり実行する（`QuestPdfFontRegistrationTests` 緑）。
3. `App.axaml.cs` と 2 つの PDF テスト static ctor が `Initialize()` を呼ぶ（後方互換 API `ApplyCommunityLicense` は削除、既存 3 呼出は改名済み）。
4. `WageStatementPdfGenerator` の全 `Document.Create` に `DefaultTextStyle` でフォント指定が入っている。
5. `WageStatementPdfGeneratorTests` / `WagePaymentListPdfGeneratorTests` が CJK 3 系統＋ Bold ASCII の substring assertion を含み、macOS/Linux/Windows CI で緑。
6. `NOTICE` に Noto Sans JP の OFL 帰属＋ Reserved Font Name＋ LICENSE.txt 参照が追記されている。
7. `docs/decisions/0013-pdf-engine-questpdf.md` が一次情報 URL 付きで「確定」へ書き換わっている（QuestPDF Community 適用条件・Noto Sans JP 選定・フォールバック計画）。
8. `docs/open-questions.md` から「QuestPDF Community License」項と「PDF 帳票の日本語フォント埋込」項が `[x]` へ移されている。
9. `CHANGELOG.md`「本番投入前に必須の deferred」から該当 2 件が除去され、`[Unreleased]` セクションに S1 完了エントリが追加されている。
10. `docs/superpowers/plans/2026-06-29-phase3-2-reports.md` からフォント前提タスクが除去されている（Phase 3-2 の前提解消）。

---

## 13. スコープ外（S1 では扱わない）

- Phase 4 (E) bulk operations 禁止スキャナ・NetArchTest ADR（→ S2）
- Phase 4 (B) 暗号化 ADR 0003 決着・バックアップ運用化（→ S3）
- Phase 4 (C) UI 補完 3 点・ContractedProvider ADR（→ S4）
- Phase 4 (D) 発行スクリプト・初回セットアップ・運用ガイド（→ S5）
- 平均工賃月額（AC2-8）の正式定義差替（→ 後段、一次資料入手時）
- Avalonia UI 側の日本語フォント指定（既に OS フォントで表示可）
- フォント sub-setting・単一ファイル発行時サイズ最適化（→ S5 で必要になれば再検討）

---

## 14. 参照

- `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「(A) 帳票フォント・ライセンス」
- `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §2 / §5 / §8.1
- `docs/decisions/0013-pdf-engine-questpdf.md`（本スライスで確定へ書き換え）
- `docs/open-questions.md` § Phase 2 工賃計算「QuestPDF ライセンス」「PDF 帳票の日本語フォント埋込」
- QuestPDF 公式ライセンス: https://www.questpdf.com/license/
- SIL OFL 1.1: https://openfontlicense.org/
- Noto Sans JP: https://fonts.google.com/noto/specimen/Noto+Sans+JP
