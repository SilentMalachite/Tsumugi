# Phase 4 / S1 実装計画 — 日本語フォント埋込 + QuestPDF ライセンス確定

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Noto Sans JP フォントを Reporting アセンブリに EmbeddedResource として同梱し、QuestPDF ドキュメントに DefaultTextStyle で適用することで CJK 抽出テストを緑にする。同時に ADR 0013 を QuestPDF Community License 継続確定として書き換え、`docs/open-questions.md` の 2 項をクローズする。

**Architecture:** Reporting アセンブリのみに変更が閉じる。フォント登録は `QuestPdfLicenseConfigurator.Initialize()` に一本化し、`Settings.License = Community` + `Settings.UseEnvironmentFonts = false` + 2 フォント登録を冪等に実行する。既存 `ApplyCommunityLicense()` は削除し `Initialize()` に改名（backwards-compat hack を導入しない CLAUDE.md 規約）。`WageStatementPdfGenerator` の全 `Document.Create` に `DefaultTextStyle(x => x.FontFamily("Noto Sans JP"))` を注入して Family 継承させる。

**Tech Stack:** .NET SDK 10 (`net10.0`) / C# 14 / QuestPDF 2025.4.0 / xUnit + FluentAssertions / UglyToad.PdfPig (PDF テキスト抽出) / SIL OFL 1.1 (Noto Sans JP)

## Global Constraints

- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 維持。警告ゼロ。
- 依存方向: `App → Application → Domain`、`Infrastructure → Application/Domain`。本 S1 では Reporting アセンブリと App の起動フックのみを触る。Domain/Application は一切変更しない。
- 全プロダクションアセンブリで通信 API（`System.Net.*` 等）への直接参照を追加しない。`System.Reflection.Assembly.GetManifestResourceStream` は `System.Reflection` API で対象外。
- `.otf` バイナリは Git LFS を導入せず通常 add で追加（更新頻度が極めて低いため）。
- 1 コミット = 1 論理変更。メッセージは既存慣習に従う（`feat(phase4/s1):` / `test(phase4/s1):` / `docs(phase4/s1):` / `chore(phase4/s1):`）。末尾に `Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>`。
- TDD: Red → Green → Refactor。ただしテスト赤の状態は同一コミット内で解消し、コミット後は常に緑を保つ。
- Domain 95% カバレッジ閾値には影響なし（Reporting はカバレッジ集計対象外）。
- 全タスク完了後に `./build/ci.sh` が緑で通ること。

## Spec 参照

- 設計仕様: `docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md`
- 全体ロードマップ: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md` §2, §5, §8.1
- 親文書: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「(A) 帳票フォント・ライセンス」

## ファイル構造マップ

**新規作成**:

```
assets/fonts/
  NotoSansJP-Regular.otf                                   [~5MB binary]
  NotoSansJP-Bold.otf                                      [~5MB binary]
  NotoSansJP.LICENSE.txt                                   [OFL 1.1 全文]

tests/Tsumugi.Infrastructure.Reporting.Tests/
  QuestPdfFontRegistrationTests.cs                         [新規テスト]
```

**修正**:

```
src/Tsumugi.Infrastructure.Reporting/
  QuestPdfLicenseConfigurator.cs                           [書き換え]
  WageStatementPdfGenerator.cs                             [DefaultTextStyle 注入 x 2]
  Tsumugi.Infrastructure.Reporting.csproj                  [<EmbeddedResource> 追加]

src/Tsumugi.App/App.axaml.cs                               [呼出改名: ApplyCommunityLicense → Initialize]

tests/Tsumugi.Infrastructure.Reporting.Tests/
  WageStatementPdfGeneratorTests.cs                        [static ctor 改名 + CJK assertion 復活]
  WagePaymentListPdfGeneratorTests.cs                      [static ctor 改名 + CJK assertion 復活]

NOTICE                                                     [OFL 帰属追記]
CHANGELOG.md                                               [S1 完了エントリ + deferred 2 件除去]

docs/decisions/0013-pdf-engine-questpdf.md                 [暫定 → 確定へ書き換え]
docs/open-questions.md                                     [2 項 [x] へ]
docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md  [Task 0 完了時に §15 追加]
docs/superpowers/plans/2026-06-29-phase3-2-reports.md      [フォント前提タスク除去]
```

---

## Task 0: QuestPDF ライセンス一次情報確認 + 設計 spec への転記

**Files:**
- Modify: `docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md` (§15 追加)

**Interfaces:**
- Consumes: なし
- Produces: なし（gate タスク）

**目的:** S1 実装着手前に QuestPDF Community License の適用条件と就労B型事業所の想定規模との整合を確定する。整合しない場合は S1 保留（他系統へ切替）。

- [ ] **Step 1: 公式ライセンスページを確認**

`https://www.questpdf.com/license/` にアクセスし、以下を確認:
- Community License の適用条件（年商 USD 1M 未満などの閾値）
- 適用単位（会社年商 vs 個別事業所年商）
- 非商用/内部使用の扱い
- 帰属表示の要否

閾値超過が就労B型事業所で現実的に発生し得ない場合のみ S1 続行。近傍または超過の場合は本タスクを halt commit して他系統へ切替（この場合、以降のタスクは実行しない）。

- [ ] **Step 2: SIL OFL 1.1 全文を確認**

`https://openfontlicense.org/` から OFL 1.1 の全文と帰属要件を確認:
- 原著作権表示
- Reserved Font Name の必須記載
- ライセンス本文の同梱

- [ ] **Step 3: 設計 spec の §15 に確認ログを追加**

`docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md` の末尾に `## 15. 一次情報確認ログ` セクションを追加:

```markdown
## 15. 一次情報確認ログ

**確認日**: 2026-07-05 (JST)

### QuestPDF Community License
- URL: https://www.questpdf.com/license/
- 適用条件: [ここに実確認内容を転記 — 年商閾値、適用単位、帰属要否]
- 判断: **Community 継続確定** / 想定事業所規模との整合を確認

### SIL OFL 1.1
- URL: https://openfontlicense.org/
- 帰属要件: 原著作権表示 + Reserved Font Name + LICENSE 本文同梱の 3 点
- 判断: NOTICE と assets/fonts/NotoSansJP.LICENSE.txt で対応

### Noto Sans JP 配布
- URL: https://fonts.google.com/noto/specimen/Noto+Sans+JP
- ライセンス: SIL Open Font License 1.1
- ダウンロード: Regular + Bold の 2 ウェイト、.otf 形式
```

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md
git commit -m "$(cat <<'EOF'
docs(phase4/s1): capture primary-source check for QuestPDF and OFL

S1 実装着手前の一次情報確認ログを spec §15 に転記。QuestPDF Community
License の適用条件と SIL OFL 1.1 の帰属要件を確認し、Community 継続と
Noto Sans JP 採用を確定。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 1: Noto Sans JP フォント資産 + OFL LICENSE.txt の追加

**Files:**
- Create: `assets/fonts/NotoSansJP-Regular.otf`
- Create: `assets/fonts/NotoSansJP-Bold.otf`
- Create: `assets/fonts/NotoSansJP.LICENSE.txt`

**Interfaces:**
- Consumes: なし
- Produces: `assets/fonts/*.otf` を Task 2 が csproj で参照する

- [ ] **Step 1: `assets/fonts/` ディレクトリを作成**

```bash
mkdir -p assets/fonts
ls -la assets/fonts/
```

Expected: 空のディレクトリ

- [ ] **Step 2: Noto Sans JP Regular + Bold を Google Fonts からダウンロード**

`https://fonts.google.com/noto/specimen/Noto+Sans+JP` にアクセスし、Regular と Bold の `.otf` をダウンロード。ファイル名を以下に統一して配置:

- `assets/fonts/NotoSansJP-Regular.otf`
- `assets/fonts/NotoSansJP-Bold.otf`

**代替入手先**: `https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/Japanese` から `NotoSansJP-Regular.otf` / `NotoSansJP-Bold.otf` を取得。

**確認**:

```bash
ls -la assets/fonts/NotoSansJP-*.otf
file assets/fonts/NotoSansJP-Regular.otf
file assets/fonts/NotoSansJP-Bold.otf
```

Expected: 2 ファイルが `.otf` 形式（`OpenType font data` などの出力）で存在。Regular は約 5MB、Bold も約 5MB。

- [ ] **Step 3: SIL OFL 1.1 全文を LICENSE.txt に保存**

`https://openfontlicense.org/documents/OFL.txt` から全文を取得し、`assets/fonts/NotoSansJP.LICENSE.txt` に保存。

**必須構造**:
- 1 行目に `Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source Han Sans' and 'Noto Sans CJK'.` を含む
- 続いて OFL 1.1 本文（`This license is copied below, and is also available with a FAQ at: https://openfontlicense.org` 以下すべて）

**確認**:

```bash
head -3 assets/fonts/NotoSansJP.LICENSE.txt
grep -c "SIL OPEN FONT LICENSE" assets/fonts/NotoSansJP.LICENSE.txt
```

Expected: Copyright 行が先頭に、`SIL OPEN FONT LICENSE` の記載が本文中に存在（`grep -c` は 1 以上）。

- [ ] **Step 4: Commit**

```bash
git add assets/fonts/NotoSansJP-Regular.otf assets/fonts/NotoSansJP-Bold.otf assets/fonts/NotoSansJP.LICENSE.txt
git commit -m "$(cat <<'EOF'
chore(phase4/s1): add Noto Sans JP fonts (Regular + Bold, SIL OFL 1.1)

Noto Sans JP Regular / Bold と OFL 1.1 全文を assets/fonts/ に追加。Task 2
で Reporting アセンブリの EmbeddedResource として同梱し、Task 3 で
QuestPdfLicenseConfigurator.Initialize() から登録する。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

**確認**:

```bash
git log --oneline -1 --stat | head -6
```

Expected: 3 ファイル追加、`NotoSansJP-Regular.otf` と `NotoSansJP-Bold.otf` が各 ~5MB。

---

## Task 2: EmbeddedResource 配線 + リソース解決テスト

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj`
- Create: `tests/Tsumugi.Infrastructure.Reporting.Tests/QuestPdfFontRegistrationTests.cs`

**Interfaces:**
- Consumes: `assets/fonts/*.otf` と `NotoSansJP.LICENSE.txt` (Task 1)
- Produces: `typeof(QuestPdfLicenseConfigurator).Assembly.GetManifestResourceStream("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-{Regular,Bold}.otf")` が Task 3 で使える

- [ ] **Step 1: 失敗するリソース解決テストを書く**

`tests/Tsumugi.Infrastructure.Reporting.Tests/QuestPdfFontRegistrationTests.cs` を新規作成:

```csharp
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Reporting.Tests;

public sealed class QuestPdfFontRegistrationTests
{
    private static readonly Assembly ReportingAssembly = typeof(QuestPdfLicenseConfigurator).Assembly;

    [Theory]
    [InlineData("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Regular.otf")]
    [InlineData("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Bold.otf")]
    [InlineData("Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP.LICENSE.txt")]
    public void Embedded_resource_is_present(string resourceName)
    {
        using var stream = ReportingAssembly.GetManifestResourceStream(resourceName);
        stream.Should().NotBeNull(because: $"{resourceName} は Reporting アセンブリの EmbeddedResource として登録済みのはず");
        stream!.Length.Should().BeGreaterThan(0, because: "リソースは空でないこと");
    }
}
```

- [ ] **Step 2: テストが赤で失敗することを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests --filter "FullyQualifiedName~QuestPdfFontRegistrationTests" -v minimal
```

Expected: FAIL、`GetManifestResourceStream` が null を返し `stream.Should().NotBeNull` で 3 件失敗。

- [ ] **Step 3: csproj に EmbeddedResource を追加**

`src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj` を以下に書き換え:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>Tsumugi.Infrastructure.Reporting</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="QuestPDF" Version="2025.4.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Tsumugi.Application\Tsumugi.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <EmbeddedResource Include="assets\fonts\NotoSansJP-Regular.otf" />
    <EmbeddedResource Include="assets\fonts\NotoSansJP-Bold.otf" />
    <EmbeddedResource Include="assets\fonts\NotoSansJP.LICENSE.txt" />
  </ItemGroup>
</Project>
```

**注**: Reporting プロジェクトの `assets/fonts/` は `src/Tsumugi.Infrastructure.Reporting/assets/fonts/` **ではなく**リポジトリルートの `assets/fonts/` を参照する。以下 Step 3.5 で確認。

- [ ] **Step 3.5: リソースパスの解決を確認**

Reporting csproj の `<EmbeddedResource>` は csproj ファイルからの相対パスで解決される。csproj は `src/Tsumugi.Infrastructure.Reporting/` にあるため、`assets\fonts\...` はリポジトリ相対ではなく `src/Tsumugi.Infrastructure.Reporting/assets/fonts/` を指す。

方針: リポジトリルートの `assets/fonts/` を参照するため、csproj で `Link` を使い相対パスを解決:

```xml
<ItemGroup>
  <EmbeddedResource Include="..\..\assets\fonts\NotoSansJP-Regular.otf">
    <LogicalName>Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Regular.otf</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Include="..\..\assets\fonts\NotoSansJP-Bold.otf">
    <LogicalName>Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP-Bold.otf</LogicalName>
  </EmbeddedResource>
  <EmbeddedResource Include="..\..\assets\fonts\NotoSansJP.LICENSE.txt">
    <LogicalName>Tsumugi.Infrastructure.Reporting.assets.fonts.NotoSansJP.LICENSE.txt</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

**理由**: `<LogicalName>` を明示することで、csproj からの相対パス（`..\..\assets\...`）とアセンブリ内リソース名（`Tsumugi.Infrastructure.Reporting.assets.fonts.*`）を分離する。これにより Task 2 Step 1 のテスト定数と一致させられる。

Step 3 で書いた csproj を上記に上書き。

- [ ] **Step 4: テストが緑で通ることを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests --filter "FullyQualifiedName~QuestPdfFontRegistrationTests" -v minimal
```

Expected: PASS 3 件。3 件のリソースが取得でき、`Length > 0`。

- [ ] **Step 5: ビルド警告ゼロを確認**

```bash
dotnet build src/Tsumugi.Infrastructure.Reporting -warnaserror
```

Expected: `Build succeeded`、警告 0 件。

- [ ] **Step 6: Commit**

```bash
git add src/Tsumugi.Infrastructure.Reporting/Tsumugi.Infrastructure.Reporting.csproj tests/Tsumugi.Infrastructure.Reporting.Tests/QuestPdfFontRegistrationTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s1): wire Noto Sans JP fonts as EmbeddedResource

Reporting csproj に <EmbeddedResource> を追加し、リポジトリルートの
assets/fonts/ 配下の Noto Sans JP Regular / Bold と LICENSE.txt を
アセンブリに埋込。<LogicalName> で csproj 相対パスとリソース名を分離。

新規テスト QuestPdfFontRegistrationTests でリソース解決を検証。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: `QuestPdfLicenseConfigurator.Initialize()` へ一本化

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs` (書き換え)
- Modify: `src/Tsumugi.App/App.axaml.cs` (呼出改名)
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs` (static ctor 改名)
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs` (static ctor 改名)
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/QuestPdfFontRegistrationTests.cs` (Initialize テスト追加)

**Interfaces:**
- Consumes: Task 2 で登録した EmbeddedResource
- Produces:
  - `QuestPdfLicenseConfigurator.Initialize()`: static void, 冪等
  - `QuestPdfLicenseConfigurator.NotoSansJpFamilyName`: `internal const string`, Task 4 が使用

- [ ] **Step 1: Initialize() の失敗テストを追加**

`tests/Tsumugi.Infrastructure.Reporting.Tests/QuestPdfFontRegistrationTests.cs` に以下を追記:

```csharp
using QuestPDF.Infrastructure;
// 既存 using は保持

public sealed class QuestPdfFontRegistrationTests
{
    // 既存の Embedded_resource_is_present テストはそのまま

    [Fact]
    public void Initialize_sets_community_license()
    {
        QuestPdfLicenseConfigurator.Initialize();
        QuestPDF.Settings.License.Should().Be(LicenseType.Community);
    }

    [Fact]
    public void Initialize_disables_environment_fonts()
    {
        QuestPdfLicenseConfigurator.Initialize();
        QuestPDF.Settings.UseEnvironmentFonts.Should().BeFalse(
            because: "CI と本番で挙動一致させるため環境フォントを抑止");
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        var action = () =>
        {
            QuestPdfLicenseConfigurator.Initialize();
            QuestPdfLicenseConfigurator.Initialize();
            QuestPdfLicenseConfigurator.Initialize();
        };
        action.Should().NotThrow(because: "冪等に呼べること");
    }

    [Fact]
    public void Family_name_constant_is_defined()
    {
        var name = typeof(QuestPdfLicenseConfigurator)
            .GetField("NotoSansJpFamilyName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null) as string;
        name.Should().NotBeNullOrWhiteSpace(because: "登録した .otf の Family 名を定数化していること");
    }
}
```

- [ ] **Step 2: テストが赤で失敗することを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests --filter "FullyQualifiedName~QuestPdfFontRegistrationTests" -v minimal
```

Expected: FAIL。既存 3 テストは緑、新規 4 テストが「`Initialize` メソッドが存在しない」「`NotoSansJpFamilyName` フィールドが存在しない」でコンパイルエラーまたは失敗。

- [ ] **Step 3: `QuestPdfLicenseConfigurator` を書き換え**

`src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs` を以下に上書き:

```csharp
using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Tsumugi.Infrastructure.Reporting;

/// <summary>
/// QuestPDF のライセンス設定と日本語フォント登録。
/// ADR 0013 により Community License を採用し、Noto Sans JP (SIL OFL 1.1) を EmbeddedResource として同梱する。
/// </summary>
public static class QuestPdfLicenseConfigurator
{
    internal const string NotoSansJpFamilyName = "Noto Sans JP";

    private static readonly object _sync = new();
    private static bool _initialized;

    /// <summary>アプリ起動時・テスト static ctor で 1 度だけ呼ぶ。ライセンス + フォント登録 + 環境フォント抑止を一気に適用する。</summary>
    public static void Initialize()
    {
        lock (_sync)
        {
            if (_initialized) return;

            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.UseEnvironmentFonts = false;
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

- [ ] **Step 4: 呼出 3 箇所を Initialize() に改名**

`src/Tsumugi.App/App.axaml.cs` の 31 行目付近:

```bash
grep -n "ApplyCommunityLicense" src/Tsumugi.App/App.axaml.cs
```

以下 sed 相当の変更（Edit ツールで実施）:

`src/Tsumugi.App/App.axaml.cs`:
- Before: `QuestPdfLicenseConfigurator.ApplyCommunityLicense();`
- After: `QuestPdfLicenseConfigurator.Initialize();`

`tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs`:
- Before: `static WageStatementPdfGeneratorTests() => QuestPdfLicenseConfigurator.ApplyCommunityLicense();`
- After: `static WageStatementPdfGeneratorTests() => QuestPdfLicenseConfigurator.Initialize();`

`tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs`:
- Before: `static WagePaymentListPdfGeneratorTests() => QuestPdfLicenseConfigurator.ApplyCommunityLicense();`
- After: `static WagePaymentListPdfGeneratorTests() => QuestPdfLicenseConfigurator.Initialize();`

**確認**:

```bash
grep -rn "ApplyCommunityLicense" src tests
```

Expected: 空（該当なし）。後方互換 API は完全に削除されている。

- [ ] **Step 5: テストが緑で通ることを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests --filter "FullyQualifiedName~QuestPdfFontRegistrationTests" -v minimal
```

Expected: PASS 7 件（既存 3 + 新規 4）。`Family_name_constant_is_defined` は `NotoSansJpFamilyName = "Noto Sans JP"` を確認。

- [ ] **Step 6: 全 Reporting テストが緑を確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests -v minimal
```

Expected: 全緑。`WageStatementPdfGeneratorTests` / `WagePaymentListPdfGeneratorTests` の既存 assertion（ASCII/数字のみ）は Initialize() 経由でも通ること。

- [ ] **Step 7: App プロジェクトのビルドを確認**

```bash
dotnet build src/Tsumugi.App -warnaserror
```

Expected: 警告ゼロ、`Build succeeded`。

- [ ] **Step 8: Commit**

```bash
git add src/Tsumugi.Infrastructure.Reporting/QuestPdfLicenseConfigurator.cs src/Tsumugi.App/App.axaml.cs tests/Tsumugi.Infrastructure.Reporting.Tests/QuestPdfFontRegistrationTests.cs tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s1): consolidate QuestPdf setup into Initialize() with font registration

QuestPdfLicenseConfigurator.ApplyCommunityLicense() を Initialize() に
一本化し、ライセンス設定 + UseEnvironmentFonts=false + Noto Sans JP
Regular/Bold の登録を lock で冪等に実行するように書き換え。

CLAUDE.md §運用メモ「backwards-compatibility hacks を導入しない」に従い、
旧 ApplyCommunityLicense() は削除。App.axaml.cs と 2 つの PDF テストの
static ctor 計 3 箇所を Initialize() 呼出へ改名。

QuestPdfFontRegistrationTests にライセンス設定・環境フォント抑止・冪等性・
Family 名定数の 4 テストを追加。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: DefaultTextStyle 注入 + CJK / Bold ASCII assertion 復活

**Files:**
- Modify: `src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs` (DefaultTextStyle 追加 x 2)
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs` (CJK / Bold ASCII assertion 追加)
- Modify: `tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs` (CJK / Bold ASCII assertion 追加)

**Interfaces:**
- Consumes: `QuestPdfLicenseConfigurator.NotoSansJpFamilyName` (Task 3)
- Produces: 全 PDF ドキュメントに DefaultTextStyle が適用され、CJK 抽出が macOS/Linux/Windows で緑

- [ ] **Step 1: WageStatementPdfGeneratorTests に CJK assertion を追加（Red）**

`tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs` の `Statement_pdf_contains_recipient_name_office_name_and_amount` テストを以下に書き換え。既存の `text.Should().Contain("12,345", ...);` と `text.Should().Contain("2026", ...);` の後、および `// ★日本語フォント埋込が未実装のため...` のコメントブロック（4 行）を削除し、代わりに CJK assertion を追加:

```csharp
        // 既存の ASCII/数字 assertion はそのまま
        text.Should().Contain("12,345", because: "金額は N0 形式で桁区切り");
        text.Should().Contain("2026", because: "対象年がヘッダに出る");

        // S1: Noto Sans JP 埋込により CJK が抽出可能
        text.Should().Contain("山田太郎", because: "利用者名 (漢字) がフォント埋込で抽出可能");
        text.Should().Contain("ヤマダタロウ", because: "カナが抽出可能");
        text.Should().Contain("テスト事業所", because: "事業所名 (ひらがな含む漢字) が抽出可能");
        text.Should().Contain("工賃明細", because: "帳票タイトル (漢字) がヘッダ Bold で抽出可能");
```

- [ ] **Step 2: WagePaymentListPdfGeneratorTests に CJK / Bold ASCII assertion を追加（Red）**

`tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs` の該当テストで既存の `// PdfPig 抽出時に NUL バイトに化ける...` コメントブロック（3 行）を削除し、既存 assertion の後に追加:

```csharp
        text.Should().Contain("12,000");
        text.Should().Contain("8,000");

        // S1: Noto Sans JP 埋込により Bold 行の ASCII と CJK が抽出可能
        text.Should().Contain("20,000", because: "合計行の Bold ASCII が抽出可能");
        text.Should().Contain("10,000", because: "平均行の Bold ASCII が抽出可能");
        text.Should().Contain("工賃支払一覧", because: "ヘッダ Bold 漢字が抽出可能");
```

**注**: 具体的な行番号は既存ファイルの状態に依存するため、Edit ツールで `text.Should().Contain("12,000");` を目印に前後を確認して挿入する。空リスト用の `text.Should().Contain("0", ...);` テストには CJK 追加不要。

- [ ] **Step 3: テストが赤で失敗することを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests --filter "FullyQualifiedName~PdfGeneratorTests" -v minimal
```

Expected: FAIL。CJK substring が現状の抽出テキストに含まれない（NUL 化） or Bold ASCII (`20,000`, `10,000`) がフォールバックフォントで NUL 化。合計 4〜7 件の assertion 失敗。

- [ ] **Step 4: `WageStatementPdfGenerator` に DefaultTextStyle を注入（Green）**

`src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs` の `GenerateStatement` メソッド内の `c.Page(p => { ... })` に DefaultTextStyle を追加:

Before (28 行目付近):
```csharp
        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.Header().Text($"{office.Name}　{statement.Year}年{statement.Month}月分 工賃明細")
                    .FontSize(14).Bold();
```

After:
```csharp
        var doc = Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.DefaultTextStyle(x => x.FontFamily(QuestPdfLicenseConfigurator.NotoSansJpFamilyName));
                p.Header().Text($"{office.Name}　{statement.Year}年{statement.Month}月分 工賃明細")
                    .FontSize(14).Bold();
```

同じく `GeneratePaymentList` メソッド内の `c.Page(p => { ... })` にも同じ 1 行を `p.Margin(...)` の直後に追加:

```csharp
                p.Size(PageSizes.A4);
                p.Margin(2, Unit.Centimetre);
                p.DefaultTextStyle(x => x.FontFamily(QuestPdfLicenseConfigurator.NotoSansJpFamilyName));
                p.Header().Text($"{office.Name}　{year}年{month}月分 工賃支払一覧")
```

- [ ] **Step 5: テストが緑で通ることを確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests --filter "FullyQualifiedName~PdfGeneratorTests" -v minimal
```

Expected: PASS。CJK 3 系統・Bold ASCII の全 assertion が緑。

**もし Family 名不一致で失敗した場合**（リスク R2）:
1. 一時的なプローブ: `dotnet test` の実行中に `FontManager.EnumerateFonts()` 相当の呼出でどの Family 名で登録されているか確認する diagnostic テストを一時的に追加
2. 実際の Family 名（例: `Noto Sans JP` / `NotoSansJP` / `Noto Sans` など）に `QuestPdfLicenseConfigurator.NotoSansJpFamilyName` を更新
3. diagnostic テストを削除
4. 再度 Step 5 を実行

- [ ] **Step 6: 全 Reporting テストが緑を確認**

```bash
dotnet test tests/Tsumugi.Infrastructure.Reporting.Tests -v minimal
```

Expected: 全緑（Font Registration 7 件 + WageStatement 系 + WagePaymentList 系）。

- [ ] **Step 7: 全体ビルドと CI ゲート**

```bash
dotnet build -warnaserror
dotnet test
```

Expected: 全プロジェクト警告ゼロ、全テスト緑。

- [ ] **Step 8: Commit**

```bash
git add src/Tsumugi.Infrastructure.Reporting/WageStatementPdfGenerator.cs tests/Tsumugi.Infrastructure.Reporting.Tests/WageStatementPdfGeneratorTests.cs tests/Tsumugi.Infrastructure.Reporting.Tests/WagePaymentListPdfGeneratorTests.cs
git commit -m "$(cat <<'EOF'
feat(phase4/s1): apply Noto Sans JP via DefaultTextStyle + restore CJK assertions

WageStatementPdfGenerator の GenerateStatement / GeneratePaymentList 双方の
Document.Create 内で DefaultTextStyle にフォントファミリを指定し、Header/
Content/Footer の全 Text() 呼出で Noto Sans JP を継承。Bold は登録済み Bold
ウェイトが自動選択される。

WageStatementPdfGeneratorTests / WagePaymentListPdfGeneratorTests で CJK 3
系統 (漢字・ひらがな・カタカナ) + Bold ASCII (20,000 / 10,000) の
substring assertion を復活。フォント埋込により macOS/Linux/Windows で
共通の抽出結果になる。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: NOTICE に OFL 帰属を追記

**Files:**
- Modify: `NOTICE`

**Interfaces:**
- Consumes: なし
- Produces: なし（ドキュメント）

- [ ] **Step 1: `NOTICE` の既存内容を確認**

```bash
cat NOTICE
```

Expected: 現行の第三者ソフトウェア一覧（.NET Runtime, Avalonia, EF Core, ...）が表示される。

- [ ] **Step 2: `NOTICE` の末尾（第三者ソフトウェア一覧の末尾）に Noto Sans JP を追記**

既存の `- Coverlet — MIT License — https://github.com/coverlet-coverage/coverlet` の次に、`ライセンス本文は各プロジェクトのリポジトリを参照してください。` の**前**に以下を挿入:

```
- Noto Sans JP — SIL Open Font License 1.1 — https://github.com/notofonts/noto-cjk
  Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source Han Sans' and 'Noto Sans CJK'.
  ライセンス本文: assets/fonts/NotoSansJP.LICENSE.txt
```

**確認**:

```bash
grep -A2 "Noto Sans JP" NOTICE
```

Expected: 追記した 3 行が表示される。

- [ ] **Step 3: Commit**

```bash
git add NOTICE
git commit -m "$(cat <<'EOF'
docs(phase4/s1): attribute Noto Sans JP in NOTICE per SIL OFL 1.1

Reporting アセンブリに同梱した Noto Sans JP について OFL 1.1 の帰属要件
(Copyright + Reserved Font Name + LICENSE 本文同梱) を NOTICE に追記。
本文は assets/fonts/NotoSansJP.LICENSE.txt に格納済。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: ADR 0013 を「暫定」→「確定」へ書き換え

**Files:**
- Modify: `docs/decisions/0013-pdf-engine-questpdf.md` (全面書き換え)

**Interfaces:**
- Consumes: Task 0 で確認した QuestPDF Community 適用条件
- Produces: なし（ドキュメント）

- [ ] **Step 1: 現行 ADR 0013 を確認**

```bash
cat docs/decisions/0013-pdf-engine-questpdf.md
```

Expected: 「暫定」ラベル付きの現行内容が表示される。

- [ ] **Step 2: ADR 0013 を書き換え**

`docs/decisions/0013-pdf-engine-questpdf.md` を以下の構造で全面書き換え:

```markdown
# ADR 0013: PDF エンジンとして QuestPDF Community License を採用（確定）

## 決定

**QuestPDF Community License** を採用し、日本語帳票フォントとして **Noto Sans JP (SIL OFL 1.1)** を Reporting アセンブリに EmbeddedResource として同梱する。閾値超過時のフォールバック（Avalonia 印刷経路）は本 ADR に計画のみ記載し、実装は必要になった時点で別 ADR で扱う。

**一次情報**:
- QuestPDF Community License: https://www.questpdf.com/license/ (2026-07-05 確認)
- SIL OFL 1.1: https://openfontlicense.org/
- Noto Sans JP: https://fonts.google.com/noto/specimen/Noto+Sans+JP

## 背景

- Phase 2 で工賃明細 PDF・工賃支払一覧 PDF の 2 帳票を実装。QuestPDF を選定した経緯は 0.1 版で「暫定」として記録。
- CI 環境（Linux/Windows ランナー）で日本語フォントが解決できず CJK が NUL バイトに化ける問題が発生し、CHANGELOG「本番投入前に必須の deferred」でフォント埋込が要件化されていた。
- QuestPDF Community License は年商閾値超過時に商用ライセンスが必要。就労B型事業所の想定規模との整合を一次情報で確認する必要があった。

## 選択肢

- **A: QuestPDF Community + Noto Sans JP 埋込（採用）**: 現行の Reporting 実装を維持しつつフォント同梱でクロスプラットフォーム対応。
- **B: QuestPDF Professional 移行**: ライセンス費用が発生。閾値未満で不要。
- **C: Avalonia 印刷経路（PrintDialog + ビジュアル Print）へ差し替え**: 実装コストが大きく、既存の決定論的 PDF 出力（`TimeProvider` 注入含む）を捨てることになる。

## 判断根拠

- QuestPDF Community は Task 0 の一次情報確認で就労B型事業所の想定規模と整合（詳細は `docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md` §15 を参照）。
- Noto Sans JP は SIL OFL 1.1 で商用同梱可能、帰属表示のみで足りる。
- Regular + Bold の 2 ウェイトで帳票デザインを満たす（Medium/Light は不使用）。

## 影響

- `src/Tsumugi.Infrastructure.Reporting/assets/fonts/` に `.otf` 2 ファイル + LICENSE.txt を同梱。バイナリサイズは ~10MB 増。
- `Tsumugi.Infrastructure.Reporting.dll` のマニフェストリソースサイズが増加。self-contained 発行時のサイズは Phase 4 / S5 で再評価。
- `NOTICE` に OFL 帰属を追記。
- `QuestPdfLicenseConfigurator.Initialize()` に統合し、ライセンス + フォント登録 + 環境フォント抑止を冪等に実行。

## 閾値超過時のフォールバック計画（未実装）

事業所年商が QuestPDF Community 閾値を超過した場合、以下のいずれかを取る:

1. **Professional 移行**: ライセンス費用は事業所負担。既存実装は無変更で稼働継続。
2. **Avalonia 印刷経路への差し替え**: `IWageReportGenerator` の実装を Avalonia の `PrintDialog` + ビジュアル `Print` に差し替え。決定論的 PDF 出力は失う。参考実装として Avalonia の `Print` API と `IStorageProvider` を組合せる。

判断は運用側で実施し、必要になったタイミングで別 ADR を起こす。

## 参照

- Phase 4 / S1 設計仕様: `docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md`
- Phase 4 / S1 実装計画: `docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md`
- 親 Phase 4 指示: `07_ClaudeCode_Phase4実装指示_リリース準備_Tsumugi.md` §「(A) 帳票フォント・ライセンス」
```

**注**: 上記は雛形。Task 0 で確認した具体的な閾値（USD 1M など）は「詳細は spec §15 を参照」で spec に委ね、ADR には結論のみを書く（`docs/decisions/` は結論→背景→選択肢→決定→影響の順、CLAUDE.md 規約）。

- [ ] **Step 3: Commit**

```bash
git add docs/decisions/0013-pdf-engine-questpdf.md
git commit -m "$(cat <<'EOF'
docs(phase4/s1): finalize ADR 0013 for QuestPDF + Noto Sans JP embedded

ADR 0013 を「暫定」から「確定」へ書き換え。QuestPDF Community License
継続の判断根拠 (Task 0 一次情報確認)、Noto Sans JP (SIL OFL 1.1) 採用、
閾値超過時の Avalonia 印刷経路フォールバック計画 (実装は別 ADR) を記載。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: `docs/open-questions.md` の 2 項をクローズ

**Files:**
- Modify: `docs/open-questions.md`

**Interfaces:**
- Consumes: なし
- Produces: なし（ドキュメント）

- [ ] **Step 1: 該当項目を特定**

```bash
grep -n "QuestPDF ライセンス\|PDF 帳票の日本語フォント埋込" docs/open-questions.md
```

Expected: 2 件の該当行が表示される。

- [ ] **Step 2: 2 項目を `[ ]` → `[x]` に変更し、クローズ日と根拠を追記**

`docs/open-questions.md` で以下を変更:

**変更 1: QuestPDF ライセンス**

Before:
```
- [ ] **QuestPDF ライセンス**: Community License の収益閾値・帰属表示要件を確認し、ADR 0013 で採否を確定。社会福祉事業の収益閾値超過リスクが大きい場合は Avalonia 印刷経路（PrintDialog → 視覚 Print）にフォールバックする判断を ADR に書く。
```

After:
```
- [x] **QuestPDF ライセンス（2026-07-05 クローズ / Phase 4 S1 / ADR 0013 v2）**: Community 継続を確定。一次情報確認 (docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md §15) で就労B型事業所の想定規模との整合を確認。閾値超過時の Avalonia 印刷経路フォールバック計画は ADR 0013 に記載 (実装は別 ADR)。
```

**変更 2: PDF 帳票の日本語フォント埋込**

Before:
```
- [ ] **PDF 帳票の日本語フォント埋込**: Phase 2 / Task E2 で QuestPDF による工賃明細 PDF を実装したが、日本語埋込フォントを構成していないためシステムフォントへフォールバックする。**2026-06-29 訂正**: ...（長文）
```

After:
```
- [x] **PDF 帳票の日本語フォント埋込（2026-07-05 クローズ / Phase 4 S1）**: Noto Sans JP (SIL OFL 1.1) Regular / Bold を assets/fonts/ に追加し Reporting アセンブリの EmbeddedResource として同梱。QuestPdfLicenseConfigurator.Initialize() で FontManager.RegisterFontFromStream 経由で登録し、WageStatementPdfGenerator の DefaultTextStyle で "Noto Sans JP" を全 Document に適用。WageStatementPdfGeneratorTests / WagePaymentListPdfGeneratorTests の CJK 3 系統 + Bold ASCII substring assertion を復活。NOTICE に OFL 帰属追記済 (ADR 0013 v2)。
```

- [ ] **Step 3: Commit**

```bash
git add docs/open-questions.md
git commit -m "$(cat <<'EOF'
docs(phase4/s1): close QuestPDF license and PDF font open-questions

S1 の完了に伴い、docs/open-questions.md の 2 項 (QuestPDF ライセンス /
PDF 帳票の日本語フォント埋込) をクローズし、決着コミットと根拠を追記。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Phase 3-2 プランからフォント前提タスクを除去

**Files:**
- Modify: `docs/superpowers/plans/2026-06-29-phase3-2-reports.md`

**Interfaces:**
- Consumes: なし
- Produces: なし（ドキュメント）

- [ ] **Step 1: フォント前提タスクを特定**

```bash
grep -nE "フォント|Noto|NotoSans|CJK 埋込|font.*embed" docs/superpowers/plans/2026-06-29-phase3-2-reports.md
```

Expected: フォント関連の前提タスク・注記が数件表示される。

- [ ] **Step 2: 該当タスクを除去または短縮**

Phase 3-2 プランの中で「フォント埋込を先に済ませる」旨のタスクや注記を、以下の 1 行に置き換える:

```
> **前提解消**: 日本語フォント埋込は Phase 4 / S1 で解消済 (`docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md`)。本プランでは前提として扱う。
```

具体的な置換位置は現行プランの構造による。Grep で見つけた箇所ごとに Edit ツールで適用する。

- [ ] **Step 3: 変更差分を確認**

```bash
git diff docs/superpowers/plans/2026-06-29-phase3-2-reports.md
```

Expected: フォント関連タスクの削除 / 短縮のみが表示される。

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/plans/2026-06-29-phase3-2-reports.md
git commit -m "$(cat <<'EOF'
docs(phase4/s1): drop font-embed prerequisite from Phase 3-2 reports plan

Phase 4 / S1 で日本語フォント埋込を解消したため、Phase 3-2 プランの
フォント前提タスクを除去し、S1 の実装計画への参照リンクに置換。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: `CHANGELOG.md` に S1 完了エントリを追加 + deferred 2 件除去

**Files:**
- Modify: `CHANGELOG.md`

**Interfaces:**
- Consumes: なし
- Produces: なし（ドキュメント）

- [ ] **Step 1: 「本番投入前に必須の deferred」から該当 2 件を削除**

`CHANGELOG.md` の `[Unreleased]` > `### 本番投入前に必須の deferred` セクションで以下 2 行を削除:

- `- QuestPDF Community License の事業所年商閾値確認（ADR 0013 / \`docs/open-questions.md\`）。`
- `- PDF 帳票の日本語フォント埋込（Noto Sans CJK JP）。漢字抽出が CJK 互換ブロックに化けるため、運用投入前に \`assets/fonts/\` 追加 + \`Settings.UseEnvironmentFonts = false\` + \`FontManager.RegisterFontFromEmbeddedResource\` を実施。`

残るのは平均工賃月額の 1 項のみになる:
```
### 本番投入前に必須の deferred
- 平均工賃月額（AC2-8 / AC4-14）の厚労省告示/通知突合 → 正式定義確定（構造整備完了、値差替のみで完了できる状態）。
```

- [ ] **Step 2: `[Unreleased]` の直下、`[0.3.0-phase4-s0]` の直上に S1 セクションを追加**

`CHANGELOG.md` に以下を挿入（`## [0.3.0-phase4-s0] - 2026-07-05` の直前）:

```markdown
## [0.3.1-phase4-s1] - 2026-07-05

Phase 4 S1（日本語フォント埋込 + QuestPDF ライセンス確定）完了。CHANGELOG
「本番投入前に必須の deferred」から 2 件 (QuestPDF 閾値 / PDF フォント) を
クローズし、Phase 3-2 の帳票フォント前提を先行解消 (AC4-1, AC4-2 達成)。

### 追加（Added）— Phase 4 S1: Reporting アセンブリの日本語フォント同梱

#### Infrastructure.Reporting
- `assets/fonts/NotoSansJP-Regular.otf` / `NotoSansJP-Bold.otf`（SIL OFL 1.1）を EmbeddedResource として同梱
- `assets/fonts/NotoSansJP.LICENSE.txt`（OFL 1.1 全文）同梱
- `QuestPdfLicenseConfigurator.Initialize()` に一本化 — ライセンス設定 + `Settings.UseEnvironmentFonts = false` + フォント登録を lock で冪等実行
- `NotoSansJpFamilyName = "Noto Sans JP"` の internal const 化
- `WageStatementPdfGenerator` の `GenerateStatement` / `GeneratePaymentList` で `DefaultTextStyle` にフォント指定

#### Tests
- `QuestPdfFontRegistrationTests`（新規）: EmbeddedResource 解決 3 件 + Initialize 冪等・License・UseEnvironmentFonts・Family 名定数の 4 件
- `WageStatementPdfGeneratorTests` に CJK 3 系統 (漢字・ひらがな・カタカナ) + Bold 漢字 の substring assertion を復活
- `WagePaymentListPdfGeneratorTests` に Bold ASCII (20,000 / 10,000) + Bold 漢字の substring assertion を復活

#### Docs
- NOTICE に Noto Sans JP の OFL 帰属追記
- ADR 0013 を「暫定」→「確定」へ書き換え（一次情報 URL 付き、閾値超過時の Avalonia 印刷フォールバック計画を併記）
- `docs/open-questions.md` の QuestPDF ライセンス項・PDF フォント項をクローズ
- `docs/superpowers/plans/2026-06-29-phase3-2-reports.md` からフォント前提タスク除去

### 変更（Changed）
- `QuestPdfLicenseConfigurator.ApplyCommunityLicense()` を削除し `Initialize()` へ改名。既存呼出 3 箇所（`App.axaml.cs` / 2 つの PDF テスト static ctor）を改名済（CLAUDE.md §運用メモ「backwards-compatibility hacks を導入しない」規約）

---
```

**注**: バージョン `0.3.1-phase4-s1` は S0 (`0.3.0-phase4-s0`) の次にセマンティックに揃える。実際のバージョン方針は S0 準拠。

- [ ] **Step 3: Commit**

```bash
git add CHANGELOG.md
git commit -m "$(cat <<'EOF'
docs(phase4/s1): cut CHANGELOG entry for v0.3.1-phase4-s1

Phase 4 S1 (日本語フォント埋込 + QuestPDF ライセンス確定) の CHANGELOG
エントリを追加。「本番投入前に必須の deferred」から QuestPDF 閾値 / PDF
フォントの 2 件を除去し、残るは平均工賃月額 (AC4-14) のみとなる。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: CI ゲート最終確認

**Files:** なし（実行のみ）

**Interfaces:** なし

- [ ] **Step 1: 全ビルド + 警告ゼロ確認**

```bash
dotnet build -warnaserror
```

Expected: 全プロジェクト `Build succeeded`、警告 0 件。

- [ ] **Step 2: 全テスト緑確認**

```bash
dotnet test
```

Expected: 全テスト緑（Domain / Application / Infrastructure / Infrastructure.Reporting / App）。

- [ ] **Step 3: `dotnet format` 検証**

```bash
dotnet format --verify-no-changes
```

Expected: 変更なし（インデント・改行が既存規則に準拠）。もし差分が出た場合は `dotnet format` を実行して commit する（別コミット、`style(phase4/s1): apply dotnet format`）。

- [ ] **Step 4: `./build/ci.sh` 一括ゲート**

```bash
./build/ci.sh
```

Expected: 全項目緑（オフライン検査・DB権限・依存方向・Domain 95% カバレッジ）。

- [ ] **Step 5: 完了確認**

`docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md` §12「S1 完了の必要十分条件」の 10 項目を目視で確認:

```bash
grep -n "^[0-9]\+\." docs/superpowers/specs/2026-07-05-phase4-s1-font-embed-and-questpdf-license-design.md | grep -A0 "^[0-9]"
```

10 項目それぞれが該当 commit で満たされていることを確認。満たされていない項目があれば別 commit で補完。

- [ ] **Step 6: S1 完了ログを本プランに追記**

本ファイル `docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md` の末尾に以下を追記:

```markdown
---

## 完了ログ

**完了日**: [実行日を記入]
**最終 commit**: `[git log -1 --oneline を記入]`
**全 AC 達成**: AC4-1 / AC4-2 ✅

**CI 結果**:
- `dotnet build -warnaserror`: 緑
- `dotnet test`: 緑（全プロジェクト）
- `dotnet format --verify-no-changes`: 差分なし
- `./build/ci.sh`: 全項目緑

**Codex レビュー**: [レビュー結果 URL / 実施日 / 重大度別件数を記入]

**確認事項の残**:
- [ ] Windows 実機での CJK 抽出（Task 4 Step 5 は CI で検証済）→ S5 の手動 QA で最終確認
- [ ] self-contained 発行時のバイナリサイズ確認 → S5 の smoke で計測
```

- [ ] **Step 7: Commit**

```bash
git add docs/superpowers/plans/2026-07-05-phase4-s1-font-embed-and-questpdf-license.md
git commit -m "$(cat <<'EOF'
docs(phase4/s1): record S1 completion log and CI verification

S1 の全 10 タスク完了ログをプランに追記。CI ゲート (build / test / format /
ci.sh) 全項目緑、AC4-1 / AC4-2 達成。残タスクは S5 の手動 QA (実機 CJK
表示 / self-contained サイズ計測) に引き継ぎ。

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

---

## 完了時のスライス出口

S1 完了時点で以下の状態:
- `git log --oneline` に Task 0〜10 の 11 コミットが積まれている
- `CHANGELOG.md`「本番投入前に必須の deferred」は平均工賃月額 (AC4-14) のみが残存
- `docs/decisions/0013-pdf-engine-questpdf.md` は「確定」ラベル
- `docs/open-questions.md` の 2 項が `[x]` へ
- Codex レビュー実施 → 重大度別トリアージ → 修正 commit を積む（もし指摘があれば）

次スライスは **S2 (bulk operations 禁止スキャナ + NetArchTest 見送り ADR)**。ロードマップ §2 / §8.2 参照。

---

## 完了ログ

**完了日**: 2026-07-05
**最終 commit（Task 10 commit の直前）**: `237048a test(phase4/s1): scope offline URL allowlist per assembly for Noto Sans JP font metadata`
**全 AC 達成**: AC4-1 / AC4-2 ✅

**CI 結果**:
- `dotnet build -warnaserror`: 緑
- `dotnet test`: 緑（607/607 passing across all projects — Domain 207 / Application 124 / Infrastructure 131 / Infrastructure.Reporting 12 / App 133）
- `dotnet format --verify-no-changes`: 差分なし
- `./build/ci.sh`: 全項目緑（format / build / test+coverage / Domain 98.66% line / Application 83.34% line）

**S0 パターン比較**:
- Task 0（一次情報確認）→ 1 fix pass 後 clean（fabricated quote 修正）
- Task 3（Initialize 一本化）→ API drift 発見 → `RegisterFontFromStream` → `RegisterFont(Stream)` 置換で解消
- Task 4（DefaultTextStyle + CJK assertion）→ 2件の follow-up 発生:
  - Kangxi Radical fold ヘルパー: 2 テストで重複、DRY refactor 予定（Task 9.6, Minor）
  - Font Copyright URL による offline compliance 赤化 → Task 9.5 で per-assembly allowlist 化して解消

**確認事項の残**:
- [ ] Windows 実機での CJK 抽出（Task 4 は macOS + CI で検証済）→ S5 の手動 QA で最終確認
- [ ] self-contained 発行時のバイナリサイズ確認 → S5 の smoke で計測
- [ ] Kangxi Radical fold の DRY refactor（Task 9.6, Minor）

**Codex レビュー**: whole-branch code review は subagent-driven-development のプロセスに従い、本 Task 10 完了後の Final review で実施予定
