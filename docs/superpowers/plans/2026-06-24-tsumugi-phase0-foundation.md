# Tsumugi フェーズ0（プロジェクト基盤）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (推奨) または superpowers:executing-plans でタスク単位に実装する。各ステップは `- [ ]` チェックボックスで追跡する。

**Goal:** .NET 10 / Avalonia / EF Core(SQLite) のオフライン専用デスクトップアプリ「Tsumugi」のソリューション基盤を、依存方向・DI合成ルート・EF Core往復・VACUUM INTOバックアップ・CI品質ゲートまで含めてTDDで構築する。

**Architecture:** クリーンアーキテクチャ4層（`Domain ← Application ← App` / `Infrastructure → Application,Domain`）。Domainは外部依存ゼロの純粋層。永続化はEF Core(SQLite)、合成ルートは`App`に集約し`Infrastructure`をテストで差し替え可能にする。依存方向はリフレクションベースのアーキテクチャテスト（外部依存なし）で機械的に固定する。

**Tech Stack:** .NET 10 (`net10.0`, LTS) / Avalonia 11.x (Fluent) / EF Core 10.x + `Microsoft.EntityFrameworkCore.Sqlite` / `CommunityToolkit.Mvvm` / `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.Hosting` / xUnit + FluentAssertions(**7.x系に固定**) / coverlet。

---

## Global Constraints（全タスク共通・指示書 §2・§6 から逐語）

- ターゲットフレームワーク: `net10.0`。Avalonia **11.x**、EF Core **10.x**。
- **完全オフライン**: 実行時にネットワークを使うコードを持たない。外部通信ライブラリを参照しない。テレメトリ・クラウド依存禁止。
- ビルド設定: `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。`dotnet build` 警告ゼロ。
- `.editorconfig` ＋ Roslynアナライザ有効。`dotnet format --verify-no-changes` 通過。
- 依存方向厳守: `App → Application → Domain`、`Infrastructure → Application/Domain`。**DomainはInfrastructure/UIを知らない**。UIから`DbContext`を直接触らない（必ずApplication層のユースケース経由）。
- 主キーは`Guid`。全永続エンティティは `作成日時(CreatedAt)` / `作成者(CreatedBy)` を持つ。更新は**楽観的同時実行**：更新トークン(Guid)列を保存時に更新し競合検知（SQLiteに`rowversion`が無いため）。
- エンティティは可能な限り C# `record` ＋ 追記型（append-only）。訂正は新レコードで履歴を残す。
- **データ保護**: ログ・例外メッセージに氏名・受給者証番号など個人情報を出さない。
- **バックアップ**: `VACUUM INTO` による単一ファイルバックアップ手段を提供する。
- クロスプラットフォーム: macOS/Windows双方でCLIビルドとViewModelテストが緑であること。
- 文書化済み判断は `docs/decisions/` にADR（1件1ファイル、結論→背景→選択肢→決定→影響）として残す。仕様が公式資料から一意に確定できない点は `docs/open-questions.md` に列挙し、推測で実装しない。
- コミットメッセージにフェーズ番号と受け入れ基準IDを記す（例: `phase0: solution skeleton`）。

### このプランが満たすフェーズ0受け入れ基準（指示書 §7）

- AC0-1: ソリューションが §4.1 の構成・依存方向で生成され、`dotnet build`/`dotnet test` が緑。
- AC0-2: DI合成ルートが`App`にあり、`Infrastructure`差し替えがテストで可能。
- AC0-3: EF Core初期マイグレーションが適用でき、一時ファイルSQLiteで往復テストが通る。
- AC0-4: `VACUUM INTO` バックアップのユースケースとテストが存在。
- AC0-5: 品質ゲート §6 の 1〜5 がCIで判定される。

---

## Prerequisites（コード着手前に1度だけ・TDD対象外）

これらは環境準備であり、現在の環境では未充足。タスク1の前に解消すること。

- [ ] **P-1: .NET 10 SDK をインストール**（現在 `dotnet` 未インストール）。
  - macOS: 公式インストーラまたは `brew install --cask dotnet-sdk`（10.0 が入ることを確認）。
  - 確認: `dotnet --version` が `10.0.x` を返す。`dotnet --list-sdks` に `10.0` が含まれる。
- [ ] **P-2: git リポジトリを初期化**（現在 not a git repository）。
  - Run: `cd /Users/hiro/Projetct/GitHub/Tsumugi && git init && git branch -m main`
  - `.gitignore` を作成（下記タスク1のステップ1で内容を投入する）。
- [ ] **P-3: Avalonia テンプレートを取得**（任意・スキャフォールド補助）。
  - Run: `dotnet new install Avalonia.Templates`
  - 確認: `dotnet new list | grep -i avalonia` に Avalonia テンプレートが並ぶ。

> 注: 本プランは exact なパッケージ版を `dotnet add package <name>`（版指定なし＝NuGet最新の互換版を解決）で導入する手順とし、patch版の逐語固定は実装者がコミット時の `*.csproj` で確定する。`net10.0` 非対応の版が解決された場合は、対応する最新版へ落とすこと（その判断はADRに残す）。

---

## File Structure（このフェーズで作成・変更するファイルと責務）

```
Tsumugi.sln
.gitignore
.editorconfig                              全プロジェクト共通のスタイル/アナライザ規則
Directory.Build.props                      net10.0 / Nullable / TreatWarningsAsErrors を全プロジェクトへ一括適用
build/ci.sh                                CI判定スクリプト(restore→format verify→build→test+coverage→offline check)
build/ci.ps1                               Windows用の同等スクリプト
docs/open-questions.md                     仕様未確定事項の起票簿
docs/decisions/0001-record-and-ef-core.md  recordと楽観ロックの整合方針(ADR)
docs/decisions/0002-fluentassertions-version.md  FluentAssertions 7.x固定の判断(ADR)
docs/decisions/0003-sqlite-location-and-encryption.md  SQLite保存先・暗号化採否(ADR)

src/Tsumugi.Domain/
  Tsumugi.Domain.csproj                    外部依存なし
  Entities/Entity.cs                        Guid Id + CreatedAt/CreatedBy + ConcurrencyToken を持つ基底
  Entities/Office.cs                        最小マスタ(事業所番号/名称)。フェーズ1で拡張
src/Tsumugi.Application/
  Tsumugi.Application.csproj               Domainのみ参照
  Abstractions/IOfficeRepository.cs        リポジトリ抽象
  Abstractions/IUnitOfWork.cs              保存(コミット)抽象
  Abstractions/IBackupService.cs           VACUUM INTO バックアップ抽象
  Dtos/OfficeDto.cs                        入出力DTO
  UseCases/RegisterOfficeUseCase.cs        事業所登録ユースケース(入力検証含む)
  UseCases/BackupDatabaseUseCase.cs        バックアップ実行ユースケース
src/Tsumugi.Infrastructure/
  Tsumugi.Infrastructure.csproj            Application/Domain参照
  Persistence/TsumugiDbContext.cs          DbContext(SaveChanges で監査列/トークン更新)
  Persistence/Configurations/OfficeConfiguration.cs  Officeのマッピング
  Persistence/OfficeRepository.cs          IOfficeRepository実装
  Persistence/EfUnitOfWork.cs              IUnitOfWork実装
  Persistence/SqliteBackupService.cs       IBackupService実装(VACUUM INTO)
  DependencyInjection.cs                   AddTsumugiInfrastructure(...) 拡張
  Migrations/                              EF Core 生成物
src/Tsumugi.App/
  Tsumugi.App.csproj                       Avalonia UI。合成ルート
  Program.cs                               エントリポイント
  App.axaml / App.axaml.cs                 Avaloniaアプリ + DIブートストラップ
  CompositionRoot.cs                       IServiceCollection 構成(合成ルート)
  MainWindow.axaml / MainWindow.axaml.cs   最小ウィンドウ(プレースホルダ)

tests/Tsumugi.Domain.Tests/
  Tsumugi.Domain.Tests.csproj
  ArchitectureTests.cs                     依存方向のリフレクション検証
  OfficeTests.cs                           Domain純粋ロジックの単体テスト
tests/Tsumugi.Application.Tests/
  Tsumugi.Application.Tests.csproj
  RegisterOfficeUseCaseTests.cs            モックリポジトリでユースケース検証
tests/Tsumugi.Infrastructure.Tests/
  Tsumugi.Infrastructure.Tests.csproj
  SqliteFixture.cs                         一時ファイルSQLiteの使い捨てDB土台
  OfficeRoundTripTests.cs                  マイグレーション適用→CRUD→楽観ロック競合
  BackupServiceTests.cs                    VACUUM INTO バックアップ往復
tests/Tsumugi.App.Tests/
  Tsumugi.App.Tests.csproj
  CompositionRootTests.cs                  合成ルートが全依存を解決でき、Infra差し替え可能
```

---

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

## Task 2: 依存方向アーキテクチャテスト（リフレクション・外部依存なし）

**Files:**
- Create: `tests/Tsumugi.Domain.Tests/ArchitectureTests.cs`

**Interfaces:**
- Consumes: `Tsumugi.Domain`（被検査アセンブリ）, `Tsumugi.Application` / `Tsumugi.Infrastructure` / `Tsumugi.App` のアセンブリ名（参照先名の禁止判定に使用）
- Produces: `Domain` が外側（Application/Infrastructure/App/Avalonia/EFCore）を参照しないことを保証する `[Fact]`。後続フェーズもこのテストで守られる。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenForDomain =
    {
        "Tsumugi.Application",
        "Tsumugi.Infrastructure",
        "Tsumugi.App",
        "Avalonia",
        "Microsoft.EntityFrameworkCore",
    };

    [Fact]
    public void Domain_does_not_reference_outer_layers()
    {
        // Domain 内の既知の型からアセンブリを取得（型は Task 3 で追加する Entity を使う）
        var domainAssembly = typeof(Tsumugi.Domain.Entities.Entity).Assembly;

        var referencedNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        foreach (var forbidden in ForbiddenForDomain)
        {
            referencedNames.Should().NotContain(
                name => name == forbidden || name.StartsWith(forbidden + "."),
                because: $"Domain は {forbidden} を参照してはならない（依存方向厳守）");
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認**

Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~ArchitectureTests`
Expected: **コンパイル失敗**（`Tsumugi.Domain.Entities.Entity` 未定義）。これはタスク3で解消する想定の意図的なREDであり、タスク3のステップ完了後に GREEN になる。

> 順序上、本テストはタスク3の `Entity` 追加に依存する。実装者はタスク2のテストを書いた後、タスク3を完了させてから本テストの GREEN を確認すること。コミットはタスク3末尾でまとめて行う。

---

## Task 3: Domain基盤（Entity基底 ＋ 最小Office record）

**Files:**
- Create: `src/Tsumugi.Domain/Entities/Entity.cs`
- Create: `src/Tsumugi.Domain/Entities/Office.cs`
- Create: `tests/Tsumugi.Domain.Tests/OfficeTests.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `abstract record Entity` — プロパティ `Guid Id`, `DateTimeOffset CreatedAt`, `string CreatedBy`, `Guid ConcurrencyToken`（すべて `required init`）。トークンの更新は Domain ではなく `TsumugiDbContext.SaveChangesAsync`（タスク5）で行う。
  - `sealed record Office : Entity` — 追加プロパティ `string OfficeNumber`, `string Name`。ファクトリ `Office Create(Guid id, string officeNumber, string name, string createdBy, DateTimeOffset createdAt, Guid concurrencyToken)`（引数順は id が先頭）。
  - これらは Application/Infrastructure の全タスクが参照する。

- [ ] **Step 1: `Entity` 基底を実装**

```csharp
namespace Tsumugi.Domain.Entities;

/// <summary>全永続エンティティの基底。Guid主キー＋監査列＋楽観ロック用トークンを持つ。</summary>
public abstract record Entity
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }

    /// <summary>楽観的同時実行の更新トークン（SQLiteに rowversion が無いため Guid で代替）。</summary>
    public required Guid ConcurrencyToken { get; init; }
}
```

- [ ] **Step 2: `Office` record を実装**

```csharp
namespace Tsumugi.Domain.Entities;

/// <summary>事業所マスタ（フェーズ0は最小。フェーズ1でサービス種別・地域区分・体制届を拡張）。</summary>
public sealed record Office : Entity
{
    public required string OfficeNumber { get; init; }
    public required string Name { get; init; }

    public static Office Create(
        Guid id,
        string officeNumber,
        string name,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken) => new()
    {
        Id = id,
        OfficeNumber = officeNumber,
        Name = name,
        CreatedBy = createdBy,
        CreatedAt = createdAt,
        ConcurrencyToken = concurrencyToken,
    };
}
```

- [ ] **Step 3: Domain単体テストを書く**

```csharp
using System;
using FluentAssertions;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class OfficeTests
{
    [Fact]
    public void Create_sets_all_fields()
    {
        var id = Guid.NewGuid();
        var token = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.FromHours(9));

        var office = Office.Create(id, "1234567890", "つむぎ作業所", "tester", at, token);

        office.Id.Should().Be(id);
        office.OfficeNumber.Should().Be("1234567890");
        office.Name.Should().Be("つむぎ作業所");
        office.CreatedBy.Should().Be("tester");
        office.CreatedAt.Should().Be(at);
        office.ConcurrencyToken.Should().Be(token);
    }

    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var id = Guid.NewGuid();
        var token = Guid.NewGuid();
        var at = DateTimeOffset.UnixEpoch;

        var a = Office.Create(id, "1", "x", "u", at, token);
        var b = Office.Create(id, "1", "x", "u", at, token);

        a.Should().Be(b); // record の値等価性
    }
}
```

- [ ] **Step 4: テスト実行（タスク2のアーキテクチャテストも含めて GREEN 確認）**

Run: `dotnet test tests/Tsumugi.Domain.Tests`
Expected: `OfficeTests`（2件）と `ArchitectureTests`（1件）が **すべて PASS**。

- [ ] **Step 5: コミット**

```bash
git add -A
git commit -m "phase0: domain Entity/Office + architecture test (AC0-1)"
```

---

## Task 4: Application層（抽象・DTO・登録ユースケース）

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IOfficeRepository.cs`
- Create: `src/Tsumugi.Application/Abstractions/IUnitOfWork.cs`
- Create: `src/Tsumugi.Application/Dtos/OfficeDto.cs`
- Create: `src/Tsumugi.Application/UseCases/RegisterOfficeUseCase.cs`
- Create: `tests/Tsumugi.Application.Tests/RegisterOfficeUseCaseTests.cs`

**Interfaces:**
- Consumes: `Tsumugi.Domain.Entities.Office`
- Produces:
  - `interface IOfficeRepository` — `Task AddAsync(Office office, CancellationToken ct)`, `Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct)`。
  - `interface IUnitOfWork` — `Task<int> SaveChangesAsync(CancellationToken ct)`。
  - `sealed record OfficeDto(Guid Id, string OfficeNumber, string Name)`。
  - `sealed class RegisterOfficeUseCase` — コンストラクタ `(IOfficeRepository, IUnitOfWork, TimeProvider)`、メソッド `Task<OfficeDto> ExecuteAsync(string officeNumber, string name, string actor, CancellationToken ct)`。重複番号は `InvalidOperationException`、空入力は `ArgumentException`。
  - Infrastructure（タスク5）が `IOfficeRepository`/`IUnitOfWork` を実装し、App（タスク7）が `RegisterOfficeUseCase` を解決する。

- [ ] **Step 1: 抽象とDTOを実装**

`Abstractions/IOfficeRepository.cs`:
```csharp
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.Abstractions;

public interface IOfficeRepository
{
    Task AddAsync(Office office, CancellationToken ct);
    Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct);
}
```

`Abstractions/IUnitOfWork.cs`:
```csharp
namespace Tsumugi.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
```

`Dtos/OfficeDto.cs`:
```csharp
namespace Tsumugi.Application.Dtos;

public sealed record OfficeDto(Guid Id, string OfficeNumber, string Name);
```

- [ ] **Step 2: 失敗するユースケーステストを書く**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterOfficeUseCaseTests
{
    private sealed class FakeOfficeRepository : IOfficeRepository
    {
        public Office? Added { get; private set; }
        public Office? Existing { get; init; }
        public Task AddAsync(Office office, CancellationToken ct) { Added = office; return Task.CompletedTask; }
        public Task<Office?> FindByNumberAsync(string n, CancellationToken ct) =>
            Task.FromResult(Existing?.OfficeNumber == n ? Existing : null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCalls++; return Task.FromResult(1); }
    }

    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Execute_persists_new_office_and_saves()
    {
        var repo = new FakeOfficeRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterOfficeUseCase(repo, uow, Clock);

        var dto = await sut.ExecuteAsync("1234567890", "つむぎ作業所", "tester", CancellationToken.None);

        dto.OfficeNumber.Should().Be("1234567890");
        repo.Added.Should().NotBeNull();
        repo.Added!.CreatedBy.Should().Be("tester");
        repo.Added.ConcurrencyToken.Should().NotBe(Guid.Empty);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Execute_rejects_duplicate_office_number()
    {
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "既存", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = new RegisterOfficeUseCase(repo, new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync("1234567890", "別名", "tester", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("123", "")]
    public async Task Execute_rejects_blank_input(string number, string name)
    {
        var sut = new RegisterOfficeUseCase(new FakeOfficeRepository(), new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync(number, name, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認**

Run: `dotnet test tests/Tsumugi.Application.Tests`
Expected: **コンパイル失敗**（`RegisterOfficeUseCase` 未定義）。

- [ ] **Step 4: ユースケースを実装**

`UseCases/RegisterOfficeUseCase.cs`:
```csharp
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.UseCases;

public sealed class RegisterOfficeUseCase(
    IOfficeRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<OfficeDto> ExecuteAsync(
        string officeNumber, string name, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(officeNumber))
            throw new ArgumentException("事業所番号は必須です。", nameof(officeNumber));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));

        if (await repository.FindByNumberAsync(officeNumber, ct) is not null)
            throw new InvalidOperationException("同一の事業所番号が既に登録されています。");

        var office = Office.Create(
            id: Guid.NewGuid(),
            officeNumber: officeNumber,
            name: name,
            createdBy: actor,
            createdAt: timeProvider.GetUtcNow(),
            concurrencyToken: Guid.NewGuid());

        await repository.AddAsync(office, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new OfficeDto(office.Id, office.OfficeNumber, office.Name);
    }
}
```

- [ ] **Step 5: テスト実行で GREEN 確認 ＆ コミット**

Run: `dotnet test tests/Tsumugi.Application.Tests`
Expected: 4件すべて PASS。

```bash
git add -A
git commit -m "phase0: application abstractions + RegisterOfficeUseCase (AC0-2)"
```

---

## Task 5: Infrastructure（DbContext・リポジトリ・初期マイグレーション・往復/楽観ロック）

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/TsumugiDbContext.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/Configurations/OfficeConfiguration.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/OfficeRepository.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/EfUnitOfWork.cs`
- Create: `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- Create: `src/Tsumugi.Infrastructure/Migrations/`（`dotnet ef migrations add` 生成物）
- Create: `tests/Tsumugi.Infrastructure.Tests/SqliteFixture.cs`, `OfficeRoundTripTests.cs`

**Interfaces:**
- Consumes: `IOfficeRepository`, `IUnitOfWork`, `Office`, `Entity`
- Produces:
  - `sealed class TsumugiDbContext(DbContextOptions<TsumugiDbContext>) : DbContext` — `DbSet<Office> Offices`、`SaveChangesAsync` で `ConcurrencyToken` を新Guidへ更新。`Office.ConcurrencyToken` を `IsConcurrencyToken()` でマッピング。
  - `static class DependencyInjection` — `IServiceCollection AddTsumugiInfrastructure(this IServiceCollection, string connectionString)`：`TsumugiDbContext`/`IOfficeRepository`/`IUnitOfWork`/`IBackupService` を登録。
  - App（タスク7）がこの拡張で合成する。バックアップ実装はタスク6で追加。

- [ ] **Step 1: EF Core パッケージとツールを追加**

Run:
```bash
dotnet add src/Tsumugi.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Tsumugi.Infrastructure package Microsoft.EntityFrameworkCore.Relational
dotnet add src/Tsumugi.Infrastructure package Microsoft.Extensions.DependencyInjection.Abstractions
dotnet new tool-manifest 2>/dev/null; dotnet tool install dotnet-ef
```

- [ ] **Step 2: DbContext と Office マッピングを実装**

`Persistence/TsumugiDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class TsumugiDbContext(DbContextOptions<TsumugiDbContext> options) : DbContext(options)
{
    public DbSet<Office> Offices => Set<Office>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(TsumugiDbContext).Assembly);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 楽観ロック: 追跡中の変更エンティティのトークンを保存時に更新する。
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Property(nameof(Entity.ConcurrencyToken)).CurrentValue = Guid.NewGuid();
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
```

`Persistence/Configurations/OfficeConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence.Configurations;

public sealed class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.ToTable("Offices");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OfficeNumber).IsRequired().HasMaxLength(32);
        builder.HasIndex(o => o.OfficeNumber).IsUnique();
        builder.Property(o => o.Name).IsRequired().HasMaxLength(128);
        builder.Property(o => o.CreatedBy).IsRequired().HasMaxLength(64);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.ConcurrencyToken).IsConcurrencyToken();
    }
}
```

- [ ] **Step 3: リポジトリ・UnitOfWork・DI拡張を実装**

`Persistence/OfficeRepository.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class OfficeRepository(TsumugiDbContext db) : IOfficeRepository
{
    public async Task AddAsync(Office office, CancellationToken ct) => await db.Offices.AddAsync(office, ct);

    public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
        db.Offices.AsNoTracking().FirstOrDefaultAsync(o => o.OfficeNumber == officeNumber, ct);
}
```

`Persistence/EfUnitOfWork.cs`:
```csharp
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class EfUnitOfWork(TsumugiDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

`DependencyInjection.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.Abstractions;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTsumugiInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TsumugiDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBackupService, SqliteBackupService>(); // 実装はタスク6
        return services;
    }
}
```

> タスク6完了まで `SqliteBackupService` / `IBackupService` は未定義。本ステップでは DI 登録行をコメントアウトしておき、タスク6ステップ末で有効化してもよい（ビルドを緑に保つため）。

- [ ] **Step 4: 初期マイグレーションを生成**

Run:
```bash
dotnet ef migrations add InitialCreate \
  --project src/Tsumugi.Infrastructure \
  --startup-project src/Tsumugi.App \
  --output-dir Migrations
```
Expected: `src/Tsumugi.Infrastructure/Migrations/*_InitialCreate.cs` が生成され、`Offices` テーブル定義を含む。

> `--startup-project` に `App` を使うため、タスク7で `App` に設計時 `IDesignTimeDbContextFactory<TsumugiDbContext>` か `AddTsumugiInfrastructure` 呼び出しが必要。先に最小の設計時ファクトリを `src/Tsumugi.Infrastructure/Persistence/DesignTimeDbContextFactory.cs` に置いて自己完結させる:
> ```csharp
> using Microsoft.EntityFrameworkCore;
> using Microsoft.EntityFrameworkCore.Design;
> namespace Tsumugi.Infrastructure.Persistence;
> public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TsumugiDbContext>
> {
>     public TsumugiDbContext CreateDbContext(string[] args)
>     {
>         var options = new DbContextOptionsBuilder<TsumugiDbContext>()
>             .UseSqlite("Data Source=design.db").Options;
>         return new TsumugiDbContext(options);
>     }
> }
> ```
> これにより `--startup-project src/Tsumugi.Infrastructure` でも生成可能。`App` を起動プロジェクトにしない場合はコマンドの `--startup-project` を `src/Tsumugi.Infrastructure` に変える。

- [ ] **Step 5: 一時ファイルSQLiteの fixture を書く**

`SqliteFixture.cs`:
```csharp
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>テストごとに使い捨ての一時ファイルSQLiteを用意し、マイグレーションを適用する。</summary>
public sealed class SqliteFixture : IDisposable
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"tsumugi-test-{Guid.NewGuid():N}.db");

    public TsumugiDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options;
        var ctx = new TsumugiDbContext(options);
        ctx.Database.Migrate(); // 空DB→現行スキーマまで適用
        return ctx;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { DbPath, DbPath + "-shm", DbPath + "-wal" })
            if (File.Exists(f)) File.Delete(f);
    }
}
```

- [ ] **Step 6: 往復＋楽観ロック競合テストを書く**

`OfficeRoundTripTests.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OfficeRoundTripTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public OfficeRoundTripTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_apply_then_insert_then_read_round_trips()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Offices.Add(Office.Create(id, "1234567890", "つむぎ", "tester",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        await using (var ctx = _fixture.NewContext())
        {
            var loaded = await ctx.Offices.SingleAsync(o => o.Id == id);
            loaded.OfficeNumber.Should().Be("1234567890");
            loaded.ConcurrencyToken.Should().NotBe(Guid.Empty);
        }
    }

    [Fact]
    public async Task Concurrent_update_is_detected_by_token()
    {
        var id = Guid.NewGuid();
        await using (var seed = _fixture.NewContext())
        {
            seed.Offices.Add(Office.Create(id, "9000000000", "種", "u",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        // 2つのコンテキストで同じ行を読み、片方を先に更新する。
        await using var ctxA = _fixture.NewContext();
        await using var ctxB = _fixture.NewContext();
        var a = await ctxA.Offices.SingleAsync(o => o.Id == id);
        var b = await ctxB.Offices.SingleAsync(o => o.Id == id);

        ctxA.Entry(a).Property(x => x.Name).CurrentValue = "A更新";
        await ctxA.SaveChangesAsync(CancellationToken.None); // トークンが変わる

        ctxB.Entry(b).Property(x => x.Name).CurrentValue = "B更新";
        var act = () => ctxB.SaveChangesAsync(CancellationToken.None);
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
```

- [ ] **Step 7: テスト実行で GREEN 確認 ＆ コミット**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~OfficeRoundTripTests`
Expected: 2件 PASS（AC0-3 充足）。

```bash
git add -A
git commit -m "phase0: EF Core DbContext + migration + round-trip/optimistic-lock tests (AC0-3)"
```

---

## Task 6: VACUUM INTO バックアップ（ユースケース＋実装＋往復テスト）

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IBackupService.cs`
- Create: `src/Tsumugi.Application/UseCases/BackupDatabaseUseCase.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/BackupServiceTests.cs`

**Interfaces:**
- Consumes: `TsumugiDbContext`（接続取得）, `SqliteFixture`
- Produces:
  - `interface IBackupService` — `Task BackupToAsync(string destinationPath, CancellationToken ct)`。
  - `sealed class BackupDatabaseUseCase(IBackupService)` — `Task ExecuteAsync(string destinationPath, CancellationToken ct)`（空パス拒否）。
  - `sealed class SqliteBackupService(TsumugiDbContext) : IBackupService` — `VACUUM INTO` を実行。
  - タスク5 の DI 登録 (`AddScoped<IBackupService, SqliteBackupService>`) をここで有効化。

- [ ] **Step 1: 抽象とユースケースを実装**

`Abstractions/IBackupService.cs`:
```csharp
namespace Tsumugi.Application.Abstractions;

public interface IBackupService
{
    Task BackupToAsync(string destinationPath, CancellationToken ct);
}
```

`UseCases/BackupDatabaseUseCase.cs`:
```csharp
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases;

public sealed class BackupDatabaseUseCase(IBackupService backupService)
{
    public Task ExecuteAsync(string destinationPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("バックアップ先パスは必須です。", nameof(destinationPath));
        return backupService.BackupToAsync(destinationPath, ct);
    }
}
```

- [ ] **Step 2: 失敗するバックアップ往復テストを書く**

`BackupServiceTests.cs`:
```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Domain.Entities;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class BackupServiceTests : IClassFixture<SqliteFixture>
{
    private readonly SqliteFixture _fixture;
    public BackupServiceTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Backup_creates_single_file_that_reopens_with_data()
    {
        var id = Guid.NewGuid();
        await using (var ctx = _fixture.NewContext())
        {
            ctx.Offices.Add(Office.Create(id, "1111111111", "元DB", "u",
                DateTimeOffset.UnixEpoch, Guid.NewGuid()));
            await ctx.SaveChangesAsync(CancellationToken.None);

            var backupPath = Path.Combine(Path.GetTempPath(), $"tsumugi-bak-{Guid.NewGuid():N}.db");
            var sut = new SqliteBackupService(ctx);
            await sut.BackupToAsync(backupPath, CancellationToken.None);

            File.Exists(backupPath).Should().BeTrue();

            // バックアップを別コンテキストで開いてデータを確認
            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite($"Data Source={backupPath}").Options;
            await using var restored = new TsumugiDbContext(options);
            (await restored.Offices.SingleAsync(o => o.Id == id)).Name.Should().Be("元DB");

            File.Delete(backupPath);
        }
    }
}
```

- [ ] **Step 3: テストを実行して失敗を確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~BackupServiceTests`
Expected: **コンパイル失敗**（`SqliteBackupService` 未定義）。

- [ ] **Step 4: `SqliteBackupService` を実装し DI を有効化**

`Persistence/SqliteBackupService.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class SqliteBackupService(TsumugiDbContext db) : IBackupService
{
    public async Task BackupToAsync(string destinationPath, CancellationToken ct)
    {
        // SQLite の VACUUM INTO は単一ファイルの一貫したバックアップを生成する。
        // パスはパラメータ化できないため、シングルクォートをエスケープして埋め込む。
        var escaped = destinationPath.Replace("'", "''");
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);
    }
}
```

`DependencyInjection.cs` の `AddScoped<IBackupService, SqliteBackupService>();` をコメントアウトしていた場合は有効化する。

- [ ] **Step 5: テスト実行で GREEN 確認 ＆ コミット**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests`
Expected: 全件 PASS（AC0-4 充足）。

```bash
git add -A
git commit -m "phase0: VACUUM INTO backup use case + round-trip test (AC0-4)"
```

---

## Task 7: App 合成ルート（DI構成＋解決スモークテスト）

**Files:**
- Create: `src/Tsumugi.App/CompositionRoot.cs`
- Modify: `src/Tsumugi.App/App.axaml.cs`（DIブートストラップ）, `src/Tsumugi.App/Program.cs`
- Create: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`

**Interfaces:**
- Consumes: `AddTsumugiInfrastructure`, `RegisterOfficeUseCase`, `BackupDatabaseUseCase`
- Produces:
  - `static class CompositionRoot` — `IServiceProvider Build(string connectionString)`：Infrastructure を登録し、`RegisterOfficeUseCase`/`BackupDatabaseUseCase`/`TimeProvider` を登録する。
  - App はこの合成ルートのみで依存を束ねる（UIから `DbContext` を直接触らない）。

- [ ] **Step 1: App に必要なパッケージを追加**

Run:
```bash
dotnet add src/Tsumugi.App package CommunityToolkit.Mvvm
dotnet add src/Tsumugi.App package Microsoft.Extensions.Hosting
dotnet add src/Tsumugi.App package Microsoft.Extensions.DependencyInjection
```

- [ ] **Step 2: 合成ルートを実装**

`CompositionRoot.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.UseCases;
using Tsumugi.Infrastructure;

namespace Tsumugi.App;

/// <summary>アプリ全体のDI構成を一点に集約する合成ルート。テストからも同じ構成を再現できる。</summary>
public static class CompositionRoot
{
    public static IServiceProvider Build(string connectionString)
        => new ServiceCollection().AddTsumugiServices(connectionString).BuildServiceProvider();

    public static IServiceCollection AddTsumugiServices(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddTsumugiInfrastructure(connectionString);
        services.AddScoped<RegisterOfficeUseCase>();
        services.AddScoped<BackupDatabaseUseCase>();
        return services;
    }
}
```

- [ ] **Step 3: 失敗する合成ルート解決テストを書く**

`CompositionRootTests.cs`:
```csharp
using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CompositionRootTests
{
    [Fact]
    public void Build_resolves_use_cases_from_root()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tsumugi-ci-{Guid.NewGuid():N}.db");
        using var provider = (ServiceProvider)CompositionRoot.Build($"Data Source={dbPath}");
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<RegisterOfficeUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<BackupDatabaseUseCase>().Should().NotBeNull();
    }

    [Fact]
    public void Infrastructure_is_swappable_via_service_collection()
    {
        // App は IOfficeRepository を抽象で消費する。テストで差し替え可能であることを示す。
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");
        var fake = new FakeRepo();
        services.AddScoped<IOfficeRepository>(_ => fake);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IOfficeRepository>().Should().BeSameAs(fake);
    }

    private sealed class FakeRepo : IOfficeRepository
    {
        public System.Threading.Tasks.Task AddAsync(Tsumugi.Domain.Entities.Office o, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<Tsumugi.Domain.Entities.Office?> FindByNumberAsync(string n, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<Tsumugi.Domain.Entities.Office?>(null);
    }
}
```

- [ ] **Step 4: テストを実行して失敗を確認 → 実装 → GREEN**

Run: `dotnet test tests/Tsumugi.App.Tests`
Expected: 最初は `CompositionRoot` 未定義でコンパイル失敗 → ステップ2の実装後に 2件 PASS（AC0-2 充足）。

> 補足: `App.axaml.cs` では `OnFrameworkInitializationCompleted` 内で `CompositionRoot.Build(...)` を呼び、SQLite保存先を OS 別のアプリデータフォルダ（`Environment.SpecialFolder.ApplicationData` 配下）に解決する。保存先・権限・暗号化採否は ADR `0003`（タスク9）に記録。UIから `DbContext` を直接触らないこと。

- [ ] **Step 5: ビルド・フォーマット・コミット**

Run: `dotnet build && dotnet format --verify-no-changes`
Expected: 警告ゼロ・差分なし。

```bash
git add -A
git commit -m "phase0: App composition root + resolution/swap tests (AC0-2)"
```

---

## Task 8: CI スクリプトと品質ゲート（§6 の 1〜5 を機械判定）

**Files:**
- Create: `build/ci.sh`, `build/ci.ps1`
- Create: `tests/Tsumugi.Infrastructure.Tests/OfflineComplianceTests.cs`（外部通信ライブラリ非参照の機械判定）

**Interfaces:**
- Consumes: 全プロジェクト
- Produces: `build/ci.sh`（CI/ローカル両用）。品質ゲート §6 の項目1（警告ゼロビルド）・2（format検証）・3（test+coverage）・4（依存方向＝タスク2のアーキテクチャテスト）・5（オフライン）を1コマンドで判定。

- [ ] **Step 1: オフライン遵守テストを書く（§6-5 の機械判定）**

`OfflineComplianceTests.cs`:
```csharp
using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class OfflineComplianceTests
{
    // オフライン厳守: 全 Tsumugi アセンブリが既知の外部通信ライブラリを参照しないこと。
    private static readonly string[] ForbiddenNetworkAssemblies =
    {
        "System.Net.Http",
        "System.Net.Sockets",
        "Microsoft.AspNetCore",
        "Grpc",
    };

    [Theory]
    [InlineData("Tsumugi.Domain")]
    [InlineData("Tsumugi.Application")]
    [InlineData("Tsumugi.Infrastructure")]
    public void Tsumugi_assemblies_do_not_reference_network_libraries(string assemblyName)
    {
        var asm = Assembly.Load(assemblyName);
        var referenced = asm.GetReferencedAssemblies().Select(a => a.Name ?? "").ToArray();

        foreach (var forbidden in ForbiddenNetworkAssemblies)
        {
            referenced.Should().NotContain(
                n => n == forbidden || n.StartsWith(forbidden + "."),
                because: $"{assemblyName} は外部通信ライブラリ {forbidden} を参照してはならない（オフライン厳守）");
        }
    }
}
```

> 注: `App` は Avalonia 経由で `System.Net.*` を間接参照しうるため、本テストは Domain/Application/Infrastructure に限定する。App のオフライン性はコードレビュー（HttpClient等の不使用）で担保し、open-questions に「Appの間接ネットワーク参照の扱い」を起票する。

- [ ] **Step 2: CI スクリプトを作成**

`build/ci.sh`:
```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> restore"
dotnet restore
echo "==> format verify (gate #2)"
dotnet format --verify-no-changes
echo "==> build warnings-as-errors (gate #1)"
dotnet build --no-restore -c Release
echo "==> test + coverage (gate #3, arch=gate#4, offline=gate#5)"
dotnet test --no-build -c Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
echo "==> CI OK"
```

`build/ci.ps1`（Windows）:
```powershell
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --collect:"XPlat Code Coverage" --results-directory ./TestResults
Write-Host "CI OK"
```

- [ ] **Step 3: 実行権限付与とローカル実行**

Run:
```bash
chmod +x build/ci.sh
./build/ci.sh
```
Expected: 全ステップ成功し `==> CI OK` を出力（gate 1〜5 が緑＝AC0-5 充足）。

- [ ] **Step 4: コミット**

```bash
git add -A
git commit -m "phase0: CI script + offline compliance test (AC0-5)"
```

---

## Task 9: ADR と open-questions（仕様判断の記録）

**Files:**
- Create: `docs/open-questions.md`
- Create: `docs/decisions/0001-record-and-ef-core.md`
- Create: `docs/decisions/0002-fluentassertions-version.md`
- Create: `docs/decisions/0003-sqlite-location-and-encryption.md`

**Interfaces:**
- Consumes: なし
- Produces: フェーズ0の設計判断の正本。Codexレビュー（`02`）がこれを突合する。

- [ ] **Step 1: `docs/open-questions.md` を作成**

```markdown
# Open Questions（仕様未確定・要確認）

> 推測で実装しない。確定するまで該当機能の実装は止める。

- [ ] 報酬の単位数・加算・地域区分単価の公式出典（令和6年改定）。フェーズ3着手前に必須。出典URL/版を記す。
- [ ] 国保連 請求データCSVの公式インターフェース仕様書の版・文字コード・改行・レコード種別・項目順。フェーズ3着手前に必須。
- [ ] 工賃計算の既定（出来高/時間/固定）・締め日・端数規則の確定（事業所運用に依存）。フェーズ2着手前。
- [ ] SQLite 暗号化（SQLCipher等）の採否（ADR 0003 で暫定判断、運用要件で再確認）。
- [ ] `App` の間接ネットワーク参照（Avalonia経由 System.Net.*）の扱い。実行時に通信が発生しないことの確認方法。
```

- [ ] **Step 2: ADR 3本を作成**

`docs/decisions/0001-record-and-ef-core.md`:
```markdown
# ADR 0001: record + 追記型と EF Core 楽観ロックの整合

- 結論: エンティティは `record`（init専用プロパティ）で表現し、楽観ロックは Guid 更新トークンを `SaveChanges` 時に更新する方式とする。
- 背景: 認知特性に基づき可変状態を最小化したい（指示書 §2.4）。一方 SQLite に rowversion が無い（§4.3）。
- 選択肢: (a) record + トークン方式 / (b) 可変クラス + rowversion 風列 / (c) イベントソーシング。
- 決定: (a)。マスタ更新はトークンで競合検知、記録系は追記型（訂正は新レコード）。
- 影響: EF Core は init プロパティを更新できるよう shadow/backing を要構成。マスタとレコードでミュータビリティ方針を分ける。
```

`docs/decisions/0002-fluentassertions-version.md`:
```markdown
# ADR 0002: FluentAssertions を 7.x 系に固定

- 結論: FluentAssertions は無償の 7.x 系（Apache-2.0）に固定し、8.x（商用ライセンス）へは上げない。
- 背景: 8.x 以降は商用ライセンスが必要。オフライン・コスト最小の方針に反する。
- 選択肢: (a) FA 7.x 固定 / (b) FA 8.x 商用購入 / (c) Shouldly 等へ移行。
- 決定: (a)。将来制約が出れば (c) を再検討。
- 影響: `*.csproj` で `--version 7.*` を固定。CIで版逸脱を監視。
```

`docs/decisions/0003-sqlite-location-and-encryption.md`:
```markdown
# ADR 0003: SQLite 保存先とファイル権限・暗号化採否

- 結論: DBは OS 別アプリデータ領域（Windows: %APPDATA%、macOS: ~/Library/Application Support）配下の Tsumugi フォルダに保存。暗号化はフェーズ0では未採用（要再評価）。
- 背景: 個人情報を扱う（§2.2, §6-6）。完全オフライン端末運用前提。
- 選択肢: (a) 平文+OSファイル権限 / (b) SQLCipher / (c) OS全体ディスク暗号化に委譲。
- 決定: 暫定 (a)+(c)。SQLCipher 採否は運用端末の要件確定後に再判断（open-questions に残す）。
- 影響: 保存先パス解決を合成ルートに集約。ログに個人情報を出さない実装規約を維持。
```

- [ ] **Step 3: コミット**

```bash
git add -A
git commit -m "phase0: ADRs + open-questions (docs)"
```

---

## フェーズ0 完了判定（このプランの Definition of Done）

- [ ] `./build/ci.sh` が緑（gate 1〜5 = §6 機械判定項目すべて）。
- [ ] AC0-1〜AC0-5 のテスト/成果物がそろっている（各タスクのコミットで対応）。
- [ ] `docs/decisions/` に ADR 3本、`docs/open-questions.md` が存在。
- [ ] macOS でグリーンを確認後、可能なら Windows でも `build/ci.ps1` を実行し緑を確認（クロスプラットフォーム §6-10）。
- [ ] Codexレビュー指示書（`02`）§4 のフェーズ0関連チェック（依存方向・EF Core・データ保護・オフライン）に回し、Blocker ゼロを確認。

---

## 後続フェーズの着手条件と骨子（詳細プランは各着手時に作成）

> 各フェーズの bite-sized プランは、前フェーズ完了で確定するスケルトン・ADR・マスタ構造に依存するため、着手時に別ファイル（`docs/superpowers/plans/`）として作成する。ここでは着手ゲートと骨子のみ示す。

### フェーズ1: マスタ＋記録（着手条件: フェーズ0 DoD 充足）
- Office を本仕様へ拡張（サービス種別・地域区分・体制届フラグ群）。Recipient/Certificate/Contract/ServiceProvisionRecord/Attendance を追加。
- 追記型＋楽観ロック、受給者証期限アラート（テーブル駆動テスト）、訂正レコード（元データ非破壊）。
- 既定ダークテーマ・低アニメ・キーボード完結のアクセシビリティ（§4.6）を ViewModel/テーマで実装。
- 受け入れ: 指示書 §7 フェーズ1 の4項目。

### フェーズ2: 工賃計算（着手条件: フェーズ1 充足 ＋ 工賃の締め/端数/基準の確定）
- 月次集計の純粋関数（会計年度4月開始）、設定化された締め・端数・基準。PDF/印刷（Avalonia印刷経路優先、必要時 QuestPDF=ADR）。
- 受け入れ: 指示書 §7 フェーズ2 の2項目。

### フェーズ3: 国保連請求データ生成（着手条件: フェーズ2 充足 ＋ 報酬告示・CSV仕様の公式出典確定）
- 報酬算定エンジン（純粋関数・外部マスタ＋適用開始年月でバージョン管理・分岐網羅100%目標）。
- 実績記録票A4印刷・請求書/明細書・CSV出力（バイト単位スナップショット・出典版記録）。
- マスタ差し替えで無改修切替をテスト実証。
- 受け入れ: 指示書 §7 フェーズ3 の5項目。
