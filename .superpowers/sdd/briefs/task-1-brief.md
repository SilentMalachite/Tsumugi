## Task 1: ソリューション骨格とビルド構成

**Files:**
- Create: `Tsumugi.sln`, `.gitignore`, `.editorconfig`, `Directory.Build.props`
- Create: 各 `src/*/Tsumugi.*.csproj`（4本）, 各 `tests/*/Tsumugi.*.Tests.csproj`（4本）と最小プレースホルダ型
- Create: `src/Tsumugi.App/Program.cs`, `App.axaml(.cs)`, `MainWindow.axaml(.cs)`, `CompositionRoot.cs`（中身は最小・タスク7で実装）

**Interfaces:**
- Consumes: なし（最初のタスク）
- Produces: ソリューション構成と参照グラフ。`Tsumugi.Domain` / `Tsumugi.Application` / `Tsumugi.Infrastructure` / `Tsumugi.App` の各アセンブリ名。`Directory.Build.props` による `net10.0` / Nullable / TreatWarningsAsErrors の一括適用。

- [ ] **Step 1: `.gitignore` と `.editorconfig` と `Directory.Build.props` を作成**

`.gitignore`:
```gitignore
bin/
obj/
*.user
.vs/
.idea/
TestResults/
coverage/
*.db
*.db-shm
*.db-wal
```

`.editorconfig`（抜粋・C#規則とアナライザ昇格）:
```ini
root = true

[*.cs]
indent_style = space
indent_size = 4
charset = utf-8
end_of_line = lf
insert_final_newline = true
dotnet_diagnostic.CA1062.severity = warning
csharp_style_namespace_declarations = file_scoped:warning
dotnet_style_qualification_for_field = false:warning
dotnet_sort_system_directives_first = true

[*.{csproj,props,targets}]
indent_size = 2

[*.{json,yml,yaml}]
indent_size = 2
```

`Directory.Build.props`（リポジトリ直下＝全プロジェクトへ自動適用）:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: ソリューションとプロジェクトを生成し参照を張る**

Run:
```bash
cd /Users/hiro/Projetct/GitHub/Tsumugi
dotnet new sln -n Tsumugi
dotnet new classlib  -o src/Tsumugi.Domain         -n Tsumugi.Domain
dotnet new classlib  -o src/Tsumugi.Application    -n Tsumugi.Application
dotnet new classlib  -o src/Tsumugi.Infrastructure -n Tsumugi.Infrastructure
dotnet new avalonia.app -o src/Tsumugi.App         -n Tsumugi.App      # P-3 のテンプレート
dotnet new xunit -o tests/Tsumugi.Domain.Tests         -n Tsumugi.Domain.Tests
dotnet new xunit -o tests/Tsumugi.Application.Tests    -n Tsumugi.Application.Tests
dotnet new xunit -o tests/Tsumugi.Infrastructure.Tests -n Tsumugi.Infrastructure.Tests
dotnet new xunit -o tests/Tsumugi.App.Tests            -n Tsumugi.App.Tests
# 自動生成された Class1.cs / UnitTest1.cs は削除
find src tests -name Class1.cs -delete -o -name UnitTest1.cs -delete
# ソリューションへ追加
dotnet sln add $(find src tests -name '*.csproj')
# 参照（依存方向を厳守）
dotnet add src/Tsumugi.Application    reference src/Tsumugi.Domain
dotnet add src/Tsumugi.Infrastructure reference src/Tsumugi.Application src/Tsumugi.Domain
dotnet add src/Tsumugi.App            reference src/Tsumugi.Application src/Tsumugi.Infrastructure
dotnet add tests/Tsumugi.Domain.Tests         reference src/Tsumugi.Domain
dotnet add tests/Tsumugi.Application.Tests    reference src/Tsumugi.Application
dotnet add tests/Tsumugi.Infrastructure.Tests reference src/Tsumugi.Infrastructure
dotnet add tests/Tsumugi.App.Tests            reference src/Tsumugi.App
```

> 注: `App → Infrastructure` 参照は合成ルート専用。依存方向テスト（タスク2）では `Domain` が外側を参照しないことのみを検証し、`App` の Infrastructure 参照は許容する。

- [ ] **Step 3: 各テストプロジェクトに FluentAssertions(7.x) と coverlet を追加**

Run:
```bash
for t in Domain Application Infrastructure App; do
  dotnet add tests/Tsumugi.$t.Tests package FluentAssertions --version 7.*
  dotnet add tests/Tsumugi.$t.Tests package coverlet.collector
done
```

> FluentAssertions 8.x は商用ライセンス（Xceed）。本プロジェクトは無償の **7.x 系（Apache-2.0）に固定**する。判断はタスク9のADR `0002` に記録。

- [ ] **Step 4: ビルドとフォーマット検証**

Run: `dotnet build`
Expected: 成功・**警告ゼロ**（TreatWarningsAsErrors のため警告が出れば失敗）。

Run: `dotnet format --verify-no-changes`
Expected: 差分なしで終了（非ゼロ終了なら `dotnet format` を実行して整形しコミットに含める）。

- [ ] **Step 5: コミット**

```bash
git add -A
git commit -m "phase0: solution skeleton (AC0-1)"
```

---

