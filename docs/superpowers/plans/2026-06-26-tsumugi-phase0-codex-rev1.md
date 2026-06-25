# Tsumugi フェーズ0 修正実装プラン（Codex Major×2 対応 / AC0-5・AC0-6）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (推奨) または superpowers:executing-plans でタスク単位に実装する。各ステップは `- [ ]` チェックボックスで追跡する。

**Goal:** Codexレビュー Major×2 を解消する: (A) `Tsumugi.App` を含めたオフライン CI 判定を**機械的に**機能させ、(B) SQLite 保存先ディレクトリ／DBファイル権限を **作成時に強制**して OS 別検証する。両方ともテストファースト、構造テストが「実際に落ちる」ことまで確認する。

**Architecture:**
- (A) `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs` を新設し、**`System.Reflection.Metadata`（BCL）の `PEReader`/`MetadataReader`** で `Tsumugi.App.dll` を直接読み、**App 自身の `TypeReference`/`MemberReference` のみ**を走査する（推移閉包は見ない＝Avalonia 内部を巻き込まない）。テストプロジェクトに `Tsumugi.App` への `ProjectReference` は**作らない**（Avalonia 実体ロード回避）。App.dll は CI 上は `dotnet build` 済みなので出力パスから読む。
- (B) 保存先決定・ディレクトリ作成・接続文字列生成を `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`（`ISqliteLocation` 抽象つき）に切り出す。App は合成ルートでこのサービスを 1 回だけ構築し、`EnsureSecuredStorage()` を呼んでから `ConnectionString` を `CompositionRoot.Build` に渡す。Unix は `Directory.CreateDirectory(..., UnixFileMode)` ＋ `File.SetUnixFileMode`、Windows は `DirectorySecurity`/`FileSecurity` + `FileSystemAclExtensions` で現在ユーザーのみフルコントロール・継承無効・継承 ACE 除去（`OperatingSystem.IsWindows()` ガード ＋ `[SupportedOSPlatform("windows")]`）。

**Tech Stack:** .NET 10 / xUnit 2.9.3 / FluentAssertions 7.x / `System.Reflection.Metadata`（BCL同梱・追加パッケージ不要） / Avalonia 11.3.18（変更なし） / EF Core 10.0.9 (SQLite)。

---

## Global Constraints（全タスク共通・CLAUDE.md / 親文書 §6 から逐語）

- ターゲットフレームワーク: `net10.0`。
- **オフライン**: `Tsumugi.App` を含む**全プロダクションアセンブリ**で通信API使用を検出・禁止（各アセンブリ自身の参照のみ走査、推移閉包は対象外）。不可避な参照は理由付き allowlist に限定し**既定は空**。
- **データ保護**: SQLite 保存先の権限を作成時に強制（Unix: ディレクトリ `0700` / DBファイル `0600`、Windows: 現在ユーザーのみ DACL・継承無効）。WAL/SHM サイドカーもディレクトリ権限で保護。ログに氏名・受給者証番号・**保存先フルパス**を出さない。
- `<Nullable>enable</Nullable>` ＋ `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`。`dotnet build` 警告ゼロ。
- `dotnet format --verify-no-changes` 通過。
- 依存方向不変: `App → Application → Domain`、`Infrastructure → Application/Domain`。新サービスは **Infrastructure 内**。Domain/Application を一切汚さない。
- TDD: Red→Green→Refactor。1コミット=1論理変更。コミットメッセージはフェーズ番号と受け入れ基準IDを含む（例: `phase0: AC0-5 App offline scan with PEReader`）。
- 出力ログに保存先フルパスを混入させない。例外メッセージはファイル名 (`Path.GetFileName`) のみ。
- 文書: 設計判断は `docs/decisions/` の ADR、未確定事項は `docs/open-questions.md` に書く。**本書では実装のみを扱い、本書自体は ADR ではない**。

### このプランが満たすフェーズ0受け入れ基準（親文書 §7・既に追記済み）

- AC0-5: 品質ゲート §6 の 1〜5 が CI 判定対象であること。オフライン検査は **`Tsumugi.App` を含む全プロダクションアセンブリ**を対象とし、禁止 API 参照で**実際に赤**になる（歯のある検査）。allowlist は最小・理由付き。
- AC0-6: SQLite の保存先決定が Infrastructure のサービスへ分離され、Unix `0700`/`0600`・Windows 現在ユーザーのみ DACL を**作成時に保証**。OS 別の権限検証テストが該当 OS で緑・非該当でスキップ。ADR `0003` を権限ポリシーで更新済み。

---

## File Structure（このフェーズで新規/変更するファイルと責務）

```
src/Tsumugi.Infrastructure/
  Persistence/
    ISqliteLocation.cs          [NEW] DBパス・接続文字列・EnsureSecuredStorage を公開する抽象
    SqliteLocationService.cs    [NEW] OS分岐で 0700/0600 (Unix) または DACL (Win) を作成時強制
src/Tsumugi.App/
  App.axaml.cs                  [MOD] ApplicationData 配下を SqliteLocationService 経由で安全初期化
tests/Tsumugi.Infrastructure.Tests/
  AppOfflineComplianceTests.cs  [NEW] PEReader/MetadataReader で App.dll を走査
  SqliteLocationServiceTests.cs [NEW] OS別パーミッション検証（非該当OSはスキップ）
  AssemblyMetadataScanner.cs    [NEW] PEReader/MetadataReader ラッパ。テスト内で共有
docs/decisions/
  0003-sqlite-location-and-encryption.md  [MOD] 権限ポリシー（Unix 0700/0600 / Win DACL）と既存ゆるい権限の扱いを追記
docs/
  open-questions.md             [MOD] App 手動レビュー項目をクローズ
```

> **依存方向**: テストプロジェクト `Tsumugi.Infrastructure.Tests` は `Tsumugi.App.csproj` に `ProjectReference` を**追加しない**。App.dll はビルド出力パスから読み込む（`Tsumugi.sln` を上向き探索 → `src/Tsumugi.App/bin/{Configuration}/net10.0/Tsumugi.App.dll`）。

---

## Existing-loose-permission policy（事前合意：本プランで採用する方針）

既存ディレクトリ／DBファイルが「期待より緩い権限」で存在する場合、本プランでは**「締め直す」**を採用する（=`chmod`/DACL 書き換え）。理由:
- 単一ユーザーデスクトップアプリで保存先は必ずユーザー自身の `ApplicationData` 配下である。
- 起動のたびに失敗させると業務継続が阻害される。
- 締め直しは冪等で、ユーザー意図を破壊しない（広げない・狭めるのみ）。
- 一方、**ファイルの所有者が現在ユーザーでない場合は明示的に失敗**（マルチユーザー乗っ取りを疑う）。

この方針は Task 6 で ADR 0003 に転記する。本書は実装計画として方針を固定するのみ。

---

## Task 1: PEReader 走査ヘルパ + App用オフラインテスト（赤）

**Files:**
- Create: `tests/Tsumugi.Infrastructure.Tests/AssemblyMetadataScanner.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`

**Interfaces:**
- Consumes: なし
- Produces:
  - `internal static class AssemblyMetadataScanner` — `IReadOnlyList<string> ScanReferencedTypeFullNames(string dllPath)` を公開。`PEReader`/`MetadataReader` で `TypeReferences` と `MemberReferences`（Parent が TypeReference のもの）から `Namespace + "." + Name` を抽出する。
  - `public sealed class AppOfflineComplianceTests` — `App.dll` を出力パスから探し、禁止集合 ∖ allowlist が空であることを表明。

- [ ] **Step 1: `AssemblyMetadataScanner.cs` を作成（テスト用ヘルパ・本タスク内で完結）**

`tests/Tsumugi.Infrastructure.Tests/AssemblyMetadataScanner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// アセンブリ自身の TypeReference / MemberReference のみを抽出する。
/// 推移閉包は意図的にたどらない（Avalonia 内部を巻き込まないため）。
/// </summary>
internal static class AssemblyMetadataScanner
{
    /// <summary>
    /// dllPath が直接参照している型の完全名（Namespace.Name）を重複排除して返す。
    /// TypeReference と「Parent が TypeReference の MemberReference」の両方を見る。
    /// </summary>
    public static IReadOnlyList<string> ScanReferencedTypeFullNames(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException(
                $"対象アセンブリが見つからない: {Path.GetFileName(dllPath)}. " +
                "事前に `dotnet build` 済みであることを確認。",
                dllPath);
        }

        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        var results = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in md.TypeReferences)
        {
            var t = md.GetTypeReference(handle);
            var ns = md.GetString(t.Namespace);
            var name = md.GetString(t.Name);
            results.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
        }

        foreach (var handle in md.MemberReferences)
        {
            var m = md.GetMemberReference(handle);
            if (m.Parent.Kind != HandleKind.TypeReference) continue;
            var t = md.GetTypeReference((TypeReferenceHandle)m.Parent);
            var ns = md.GetString(t.Namespace);
            var name = md.GetString(t.Name);
            results.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");
        }

        var list = new List<string>(results);
        list.Sort(StringComparer.Ordinal);
        return list;
    }
}
```

- [ ] **Step 2: `AppOfflineComplianceTests.cs` を作成（最初に赤くなることを後で確認）**

`tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// Tsumugi.App.dll が外部通信 API を直接参照しないことを CI で機械判定する（AC0-5）。
/// ProjectReference は使わず、ビルド出力 DLL を PEReader で直接読む（Avalonia 実体ロード回避）。
/// </summary>
public sealed class AppOfflineComplianceTests
{
    // 禁止 namespace 集合（このいずれかが前方一致する型はアウト）
    private static readonly string[] ForbiddenNamespaces =
    {
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebSockets",
        "System.Net.Mail",
    };

    // 禁止具体型集合（namespace 単独では使われていなくても型単位で警戒）
    private static readonly string[] ForbiddenTypes =
    {
        "System.Net.WebClient",
        "System.Net.WebRequest",
        "System.Net.HttpWebRequest",
        "System.Net.HttpWebResponse",
        "System.Net.Http.HttpClient",
        "System.Net.Http.HttpRequestMessage",
        "System.Net.Http.HttpResponseMessage",
        "System.Net.Http.HttpMessageHandler",
        "System.Net.Sockets.Socket",
        "System.Net.Sockets.TcpClient",
        "System.Net.Sockets.UdpClient",
        "System.Net.Mail.SmtpClient",
    };

    // allowlist: 「(完全名) + 理由」の形で明示。**既定は空**。
    // 追加は根拠必須（PRレビューで合意し、本配列に直接書く）。
    private static readonly (string FullName, string Reason)[] Allowlist =
    {
        // 例: ("System.Net.Sockets.AddressFamily", "Avalonia 内の P/Invoke 列挙だけで通信 API 本体は使わない"),
    };

    [Fact]
    public void TsumugiApp_dll_does_not_directly_reference_network_apis()
    {
        var appDllPath = AppAssemblyLocator.LocateTsumugiAppDll();
        var referenced = AssemblyMetadataScanner.ScanReferencedTypeFullNames(appDllPath);

        var allowed = Allowlist.Select(a => a.FullName).ToHashSet(StringComparer.Ordinal);

        var hits = referenced
            .Where(name =>
                ForbiddenTypes.Contains(name, StringComparer.Ordinal)
                || ForbiddenNamespaces.Any(ns => name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal)))
            .Where(name => !allowed.Contains(name))
            .ToArray();

        hits.Should().BeEmpty(
            because: "Tsumugi.App は完全オフライン: 外部通信 API を直接参照してはならない。" +
                     "不可避な参照は AppOfflineComplianceTests.Allowlist に理由付きで明示すること。" +
                     Environment.NewLine +
                     "違反: " + string.Join(", ", hits));
    }
}

internal static class AppAssemblyLocator
{
    /// <summary>
    /// テスト bin の AppContext.BaseDirectory から Tsumugi.sln を上向き探索し、
    /// src/Tsumugi.App/bin/{Configuration}/net10.0/Tsumugi.App.dll を組み立てる。
    /// Configuration は test の bin パスから推定（Debug/Release）。
    /// </summary>
    public static string LocateTsumugiAppDll()
    {
        var baseDir = AppContext.BaseDirectory;

        // Configuration を bin パスから抽出（.../bin/Debug/net10.0/ → "Debug"）
        var configuration = ExtractConfigurationFromPath(baseDir);

        // sln ファイルを上向き探索
        var solutionRoot = FindSolutionRoot(baseDir);

        return Path.Combine(
            solutionRoot,
            "src", "Tsumugi.App", "bin", configuration, "net10.0", "Tsumugi.App.dll");
    }

    private static string ExtractConfigurationFromPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "bin", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }
        throw new InvalidOperationException(
            $"テスト実行ディレクトリから Configuration を推定できない: {path}");
    }

    private static string FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("Tsumugi.sln").Any()) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Tsumugi.sln が祖先方向に見つからない: 起点={startDir}");
    }
}
```

- [ ] **Step 3: ビルドのみ実行してコンパイルが通ることを確認**

Run: `dotnet build tests/Tsumugi.Infrastructure.Tests -c Debug`
Expected: 警告ゼロで成功。

- [ ] **Step 4: 今の App は通信 API を直接参照していないので、テストは緑のはず**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName=Tsumugi.Infrastructure.Tests.AppOfflineComplianceTests.TsumugiApp_dll_does_not_directly_reference_network_apis -c Debug`
Expected: PASS（1テスト緑）。

> もし FAIL するなら、それは既存 App コードがすでに通信 API を直接参照している証拠なので、ハードコード違反として別途扱う（このタスクは止めて報告）。

- [ ] **Step 5: 「歯のある検査」の確認（=赤を確実に出せること）**

Tsumugi.App に**一時的に**ダミー参照を入れて、テストが赤になることを目視確認する。コミットしない。

`src/Tsumugi.App/App.axaml.cs` の `OnFrameworkInitializationCompleted` の冒頭に1行追加:

```csharp
var _redCheck = new System.Net.Http.HttpClient(); // TEMP: AC0-5 歯のある確認用。直後に戻す。
```

Run: `dotnet build src/Tsumugi.App -c Debug && dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName=Tsumugi.Infrastructure.Tests.AppOfflineComplianceTests.TsumugiApp_dll_does_not_directly_reference_network_apis -c Debug`
Expected: FAIL — メッセージに `System.Net.Http.HttpClient`（あるいは `System.Net.Http.HttpClient..ctor`/`System.Net.Http` 名前空間）が違反として出る。

確認できたら、追加した1行を削除して元に戻す。

Run: `dotnet build src/Tsumugi.App -c Debug && dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName=Tsumugi.Infrastructure.Tests.AppOfflineComplianceTests.TsumugiApp_dll_does_not_directly_reference_network_apis -c Debug`
Expected: PASS。

- [ ] **Step 6: コミット**

```bash
git add tests/Tsumugi.Infrastructure.Tests/AssemblyMetadataScanner.cs \
        tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs
git commit -m "phase0: AC0-5 add App.dll offline scan via PEReader/MetadataReader"
```

---

## Task 2: `ISqliteLocation` 抽象と `SqliteLocationService`（Unix 0700/0600 から）

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/ISqliteLocation.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`
- Create: `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs`

**Interfaces:**
- Consumes: なし（このタスクから始める）
- Produces:
  - `public interface ISqliteLocation { string DatabasePath { get; } string ConnectionString { get; } void EnsureSecuredStorage(); }`
  - `public sealed class SqliteLocationService : ISqliteLocation` — `ctor(string applicationDataRoot)` を取り、`Path.Combine(applicationDataRoot, "tsumugi.db")` を `DatabasePath` とする。`ConnectionString` は `$"Data Source={DatabasePath}"`。`EnsureSecuredStorage()` で OS 分岐の権限設定（このタスクでは Unix のみ）。
  - `applicationDataRoot` は呼び出し側が「Tsumugi 専用ディレクトリ」として渡す（=末尾は `Tsumugi`）。
  - **Task 3 で `SupportsOsPlatform("windows")` の Windows 分岐を追加**、**Task 4 で既存ゆるい権限の締め直しを追加**する。

- [ ] **Step 1: 抽象を作成**

`src/Tsumugi.Infrastructure/Persistence/ISqliteLocation.cs`:

```csharp
namespace Tsumugi.Infrastructure.Persistence;

public interface ISqliteLocation
{
    string DatabasePath { get; }
    string ConnectionString { get; }

    /// <summary>
    /// 保存先ディレクトリと DB ファイルを作成し、OS 別の最小権限を強制する。
    /// 冪等。複数回呼ばれても安全。失敗時は説明的な例外を投げる（パスはログに残さない）。
    /// </summary>
    void EnsureSecuredStorage();
}
```

- [ ] **Step 2: テストを作成（Unix 用 1ケースだけまず追加）**

`tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs`:

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class SqliteLocationServiceTests
{
    private static string NewTempDir() =>
        Path.Combine(Path.GetTempPath(), $"tsumugi-loc-{Guid.NewGuid():N}");

    private static bool IsUnix =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void EnsureSecuredStorage_creates_dir_0700_and_db_0600_on_unix()
    {
        if (!IsUnix) return; // 非該当OSではクリーンにスキップ

        var dir = NewTempDir();
        try
        {
            var svc = new SqliteLocationService(dir);
            svc.EnsureSecuredStorage();

            Directory.Exists(dir).Should().BeTrue();
            File.Exists(svc.DatabasePath).Should().BeTrue();

            var dirMode = File.GetUnixFileMode(dir);
            var dbMode = File.GetUnixFileMode(svc.DatabasePath);

            dirMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                "ディレクトリは所有者のみ rwx（0700）であるべき");
            dbMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                "DBファイルは所有者のみ rw（0600）であるべき");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: テストが赤になることを確認（実装がない）**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~SqliteLocationServiceTests -c Debug`
Expected: FAIL — `SqliteLocationService` が存在しないためコンパイルエラー。

- [ ] **Step 4: 実装を作成（Unix 分岐のみ）**

`src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`:

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// SQLite 保存先のディレクトリ／DBファイルを作成し、OS 別の最小権限で初期化する。
/// Unix: dir 0700 / db 0600。Windows: 現在ユーザーのみフルコントロール（Task 3 で追加）。
/// WAL/SHM サイドカーはディレクトリ権限（0700）で保護される。
/// </summary>
public sealed class SqliteLocationService : ISqliteLocation
{
    private readonly string _directory;

    public SqliteLocationService(string applicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataRoot);
        _directory = applicationDataRoot;
        DatabasePath = Path.Combine(applicationDataRoot, "tsumugi.db");
        ConnectionString = $"Data Source={DatabasePath}";
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public void EnsureSecuredStorage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            EnsureUnix();
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            EnsureWindows(); // Task 3 で実装
            return;
        }

        throw new PlatformNotSupportedException(
            "サポートされないOSで Tsumugi の保存先を初期化しようとした。");
    }

    private void EnsureUnix()
    {
        const UnixFileMode dirMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        const UnixFileMode fileMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;

        if (!Directory.Exists(_directory))
        {
            Directory.CreateDirectory(_directory, dirMode);
        }
        // 既存ゆるい権限の締め直しは Task 4 で追加

        if (!File.Exists(DatabasePath))
        {
            // 空ファイルを先に作って 0600 を強制 → SQLite は空ファイルを新規DBとして扱う
            using (File.Create(DatabasePath)) { }
            File.SetUnixFileMode(DatabasePath, fileMode);
        }
    }

    private void EnsureWindows()
    {
        // Task 3 で DACL 設定を実装
        throw new NotImplementedException(
            "SqliteLocationService の Windows 分岐は Task 3 で実装する。");
    }
}
```

- [ ] **Step 5: テスト緑を確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~SqliteLocationServiceTests -c Debug`
Expected: PASS（macOS/Linux で 1 件緑、Windows では同テストは `return` でスキップ扱いになる＝Pass）。

> 注: `return` は xUnit からは Pass として記録される。Skip 表示にしたい場合は `Skip.If(...)`（xUnit v3）等が必要だが、本プロジェクトは xUnit 2.9.3 なので素朴に `if (!IsUnix) return;` で十分。Windows 用ケースは Task 3 で `if (!OperatingSystem.IsWindows()) return;` 形で別 `[Fact]` を立てる。

- [ ] **Step 6: コミット**

```bash
git add src/Tsumugi.Infrastructure/Persistence/ISqliteLocation.cs \
        src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs \
        tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs
git commit -m "phase0: AC0-6 add SqliteLocationService with Unix 0700/0600 enforcement"
```

---

## Task 3: Windows DACL（現在ユーザーのみフルコントロール・継承無効）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs`

**Interfaces:**
- Consumes: Task 2 で定義済みの `ISqliteLocation`、`SqliteLocationService(string)`
- Produces: `EnsureSecuredStorage()` が Windows でも例外を投げず、DACL を「現在ユーザー (`WindowsIdentity.GetCurrent().User`) のみフルコントロール／継承無効／継承された ACE は除去」に設定する。

- [ ] **Step 1: Windows用テストを追加**

`tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs` のクラス内に追記:

```csharp
    [Fact]
    public void EnsureSecuredStorage_sets_current_user_only_dacl_on_windows()
    {
        if (!OperatingSystem.IsWindows()) return; // 非該当OSではスキップ

        var dir = NewTempDir();
        try
        {
            var svc = new SqliteLocationService(dir);
            svc.EnsureSecuredStorage();

            Directory.Exists(dir).Should().BeTrue();
            File.Exists(svc.DatabasePath).Should().BeTrue();

            AssertWindowsDaclIsCurrentUserOnly(dir, isDirectory: true);
            AssertWindowsDaclIsCurrentUserOnly(svc.DatabasePath, isDirectory: false);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

#pragma warning disable CA1416 // platform-guarded via OperatingSystem.IsWindows()
    private static void AssertWindowsDaclIsCurrentUserOnly(string path, bool isDirectory)
    {
        var currentSid = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        System.Security.AccessControl.AuthorizationRuleCollection rules;
        bool inheritanceProtected;

        if (isDirectory)
        {
            var sec = new DirectoryInfo(path).GetAccessControl();
            rules = sec.GetAccessRules(true, true,
                typeof(System.Security.Principal.SecurityIdentifier));
            inheritanceProtected = sec.AreAccessRulesProtected;
        }
        else
        {
            var sec = new FileInfo(path).GetAccessControl();
            rules = sec.GetAccessRules(true, true,
                typeof(System.Security.Principal.SecurityIdentifier));
            inheritanceProtected = sec.AreAccessRulesProtected;
        }

        inheritanceProtected.Should().BeTrue("継承は無効化されているべき");

        foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules)
        {
            rule.IdentityReference.Value.Should().Be(currentSid.Value,
                "DACL のすべての ACE は現在ユーザーに対するもののみであるべき");
            rule.AccessControlType.Should().Be(
                System.Security.AccessControl.AccessControlType.Allow);
            rule.FileSystemRights.Should().Be(
                System.Security.AccessControl.FileSystemRights.FullControl);
        }
    }
#pragma warning restore CA1416
```

- [ ] **Step 2: テストが赤になる（実装が `NotImplementedException`）ことを確認**

Run（Windows 環境で）: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~SqliteLocationServiceTests -c Debug`
Expected: FAIL — `SqliteLocationService.EnsureWindows` が `NotImplementedException`。

> macOS 側で書いている場合はこのステップを SKIP し、Step 3 でコードだけ仕上げてから CI / Windows ランナーで赤→緑を確認する。本プランは macOS でも Step 3 の実装まで進めてよい（Windows コードはコンパイルだけ通せばよい）。

- [ ] **Step 3: `SqliteLocationService.EnsureWindows` を実装**

`src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs` の冒頭 using 追加:

```csharp
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
```

`EnsureWindows` を以下に置き換え:

```csharp
    [SupportedOSPlatform("windows")]
    private void EnsureWindows()
    {
        var currentUser = WindowsIdentity.GetCurrent().User!;

        var dirSecurity = new DirectorySecurity();
        dirSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        dirSecurity.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        if (!Directory.Exists(_directory))
        {
            var di = Directory.CreateDirectory(_directory);
            di.SetAccessControl(dirSecurity);
        }
        else
        {
            new DirectoryInfo(_directory).SetAccessControl(dirSecurity);
        }

        var fileSecurity = new FileSecurity();
        fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        if (!File.Exists(DatabasePath))
        {
            using (File.Create(DatabasePath)) { }
        }
        new FileInfo(DatabasePath).SetAccessControl(fileSecurity);
    }
```

> `[SupportedOSPlatform("windows")]` を付けることで CA1416 アナライザの誤検知を避ける。`EnsureSecuredStorage()` 側は `OperatingSystem.IsWindows()` でガード済み。

- [ ] **Step 4: 全テスト緑を確認（macOS 環境では Windows テストは早期 `return`）**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~SqliteLocationServiceTests -c Debug`
Expected: PASS。

> Windows 環境のCIで赤→緑が確認できない場合は、本リポジトリの方針として **Windows ランナーが整うまでは「コンパイル通過＋実装存在」を以て暫定 OK** とし、Windows 実機検証は `docs/open-questions.md` に残す（既存項目に追記）。

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs \
        tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs
git commit -m "phase0: AC0-6 add Windows DACL (current-user-only, no inheritance)"
```

---

## Task 4: 既存ゆるい権限の締め直し（冪等）

**Files:**
- Modify: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`
- Modify: `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs`

**Interfaces:**
- Consumes: Task 2/3 の `EnsureSecuredStorage()`
- Produces: 既存ディレクトリ/DBファイルが緩い権限で存在しても、`EnsureSecuredStorage()` が**締め直して**期待値に揃える。所有者が現在ユーザーでない場合は明示的に失敗（Unix のみ判定可能。Windows は DACL の上書きで対応）。

- [ ] **Step 1: Unix 用「ゆるい→締め直し」テストを追加**

`tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs` のクラス内に追記:

```csharp
    [Fact]
    public void EnsureSecuredStorage_tightens_loose_permissions_on_unix()
    {
        if (!IsUnix) return;

        var dir = NewTempDir();
        try
        {
            // 事前に「ゆるい」状態を作る: 0755 のディレクトリ + 0644 のDBファイル
            Directory.CreateDirectory(dir);
            File.SetUnixFileMode(dir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

            var dbPath = Path.Combine(dir, "tsumugi.db");
            using (File.Create(dbPath)) { }
            File.SetUnixFileMode(dbPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            var svc = new SqliteLocationService(dir);
            svc.EnsureSecuredStorage();

            File.GetUnixFileMode(dir).Should().Be(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                "既存ディレクトリは 0700 に締め直されるべき");
            File.GetUnixFileMode(dbPath).Should().Be(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                "既存DBファイルは 0600 に締め直されるべき");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
```

- [ ] **Step 2: テストが赤になることを確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~SqliteLocationServiceTests -c Debug`
Expected: 新規 `EnsureSecuredStorage_tightens_loose_permissions_on_unix` が FAIL（モードが 0755/0644 のまま）。

- [ ] **Step 3: 実装を「常に締め直す」に変更**

`SqliteLocationService.EnsureUnix` を以下に置き換え:

```csharp
    private void EnsureUnix()
    {
        const UnixFileMode dirMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        const UnixFileMode fileMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;

        if (!Directory.Exists(_directory))
        {
            Directory.CreateDirectory(_directory, dirMode);
        }
        else
        {
            // 既存ディレクトリは「広い→狭める」方向にのみ更新する
            File.SetUnixFileMode(_directory, dirMode);
        }

        if (!File.Exists(DatabasePath))
        {
            using (File.Create(DatabasePath)) { }
        }
        // 既存ファイルも含めて常に 0600 に揃える（冪等）
        File.SetUnixFileMode(DatabasePath, fileMode);
    }
```

- [ ] **Step 4: 全テスト緑を確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter FullyQualifiedName~SqliteLocationServiceTests -c Debug`
Expected: 3 件 PASS（Unix 通常 / Unix ゆるい→締め直し / Windows DACL は非該当 OS では早期 return）。

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs \
        tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs
git commit -m "phase0: AC0-6 tighten loose permissions idempotently on Unix"
```

---

## Task 5: `App.axaml.cs` を `SqliteLocationService` 経由に切り替え

**Files:**
- Modify: `src/Tsumugi.App/App.axaml.cs`

**Interfaces:**
- Consumes: Task 2-4 の `SqliteLocationService(string applicationDataRoot)` ＋ `EnsureSecuredStorage()` ＋ `ConnectionString`
- Produces: App はパス解決のみ行い、ディレクトリ作成・権限設定・接続文字列組み立ては `SqliteLocationService` に委譲する。`CompositionRoot.Build(string)` の API は変更しない。

- [ ] **Step 1: App 全体を新サービス経由に書き換え**

`src/Tsumugi.App/App.axaml.cs` を以下に置き換え:

```csharp
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tsumugi.Infrastructure.Persistence;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App;

public partial class App : AvaloniaApplication
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tsumugi");

        var location = new SqliteLocationService(appDataRoot);
        location.EnsureSecuredStorage();

        _services = CompositionRoot.Build(location.ConnectionString);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
```

> `Directory.CreateDirectory` は削除（`SqliteLocationService.EnsureSecuredStorage()` の責務）。

- [ ] **Step 2: ビルド緑を確認**

Run: `dotnet build -c Debug`
Expected: 警告ゼロで成功。

- [ ] **Step 3: 全テスト緑を確認**

Run: `dotnet test -c Debug`
Expected: 既存テスト含めて全 PASS。

- [ ] **Step 4: macOS で起動して例外なく立ち上がることを目視確認**

Run: `dotnet run --project src/Tsumugi.App -c Debug`
Expected: メインウィンドウが表示される。`~/.config/Tsumugi/tsumugi.db` が `0600` で、`~/.config/Tsumugi/` が `0700` で存在する。
Cleanup: ウィンドウを閉じる。

検証:
```bash
ls -ld ~/.config/Tsumugi ~/.config/Tsumugi/tsumugi.db
# Expected: drwx------ ... ~/.config/Tsumugi
#           -rw------- ... ~/.config/Tsumugi/tsumugi.db
```

- [ ] **Step 5: コミット**

```bash
git add src/Tsumugi.App/App.axaml.cs
git commit -m "phase0: AC0-6 wire SqliteLocationService into App composition"
```

---

## Task 6: ADR 0003 を権限ポリシーで更新 / open-questions.md の App 手動レビュー項目をクローズ

**Files:**
- Modify: `docs/decisions/0003-sqlite-location-and-encryption.md`
- Modify: `docs/open-questions.md`

**Interfaces:**
- Consumes: Task 1-5 の実装事実
- Produces: なし（ドキュメントのみ）

- [ ] **Step 1: ADR 0003 に権限ポリシーを追記**

`docs/decisions/0003-sqlite-location-and-encryption.md` の末尾に以下のセクションを追加（既存内容は壊さない）:

```markdown

## 追補: 権限ポリシー（2026-06-26 / AC0-6）

- **Unix (Linux/macOS)**: ディレクトリ `0700`、DBファイル `0600`。`Directory.CreateDirectory(path, UnixFileMode)` ＋ `File.SetUnixFileMode` で**作成時に強制**。WAL/SHM サイドカーはディレクトリ権限で保護される。
- **Windows**: 現在ユーザー (`WindowsIdentity.GetCurrent().User`) のみフルコントロール。`DirectorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false)` で**継承無効・継承 ACE 除去**。同等のポリシーを DBファイルにも適用。Windows 専用 API は `[SupportedOSPlatform("windows")]` でガードする。
- **既存ゆるい権限の扱い**: 起動時に**冪等に締め直す**（広げない・狭めるのみ）。理由: 単一ユーザーデスクトップアプリで保存先はユーザー自身の `ApplicationData` 配下である／毎回失敗にすると業務継続が阻害される／締め直しはユーザー意図を破壊しない。所有者検証ポリシー（マルチユーザー乗っ取り対策）はフェーズ1以降で追加検討。
- **検証**: `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs` で OS 別に `[Fact]` を立て、該当 OS 以外は早期 `return` でスキップ（xUnit 2.x のため `Skip.If` は使わない）。
- **実装場所**: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`（Infrastructure 層）。App は合成ルートでサービスを 1 回構築し `EnsureSecuredStorage()` を呼ぶのみ。

暗号化（SQLCipher 等）採否は本追補の対象外（open-questions.md 引き続き）。
```

- [ ] **Step 2: open-questions.md の App 手動レビュー項目をクローズ**

`docs/open-questions.md` の該当行を以下に置き換え:

旧:
```
- [ ] **App 層のオフライン遵守確認**: `OfflineComplianceTests` は Domain/Application/Infrastructure のみを対象とする。`Tsumugi.App` は Avalonia 経由で `System.Net.*` を間接参照しており、アプリコードが実行時にネットワーク通信を行わないことは現時点でコードレビューのみで担保している。オープンな問い: App が実行時にネットワーク呼び出しを行わないことを機械的にアサートする方法（例: テスト時の HttpMessageHandler 差し替え、実行時ネットワーク監視）を検討する。
```

新:
```
- [x] **App 層のオフライン遵守確認（2026-06-26 クローズ）**: `tests/Tsumugi.Infrastructure.Tests/AppOfflineComplianceTests.cs` で `System.Reflection.Metadata` の `PEReader`/`MetadataReader` を用いて `Tsumugi.App.dll` の `TypeReference`/`MemberReference` を直接走査し、`System.Net.*` 等の禁止 API への直接参照を CI で機械判定する（AC0-5）。allowlist は最小・理由付きで `AppOfflineComplianceTests.Allowlist` に明示（既定は空）。歯のある検査であることを `HttpClient` 一時混入で確認済み。実行時ネットワーク監視はスコープ外（直接参照禁止＋オフライン端末運用で十分とする）。
```

- [ ] **Step 3: ドキュメント整合性を `dotnet format` で確認（コードに変更はないが念のため）**

Run: `dotnet format --verify-no-changes`
Expected: 通過。

- [ ] **Step 4: コミット**

```bash
git add docs/decisions/0003-sqlite-location-and-encryption.md \
        docs/open-questions.md
git commit -m "phase0: AC0-5/AC0-6 docs - ADR 0003 permissions + close App offline open-question"
```

---

## Task 7: CI 緑確認（フェーズ0 受け入れ最終ゲート）

**Files:**
- Modify: なし（検証のみ）

**Interfaces:**
- Consumes: Task 1-6 のすべての成果物
- Produces: なし

- [ ] **Step 1: 完全な CI スクリプトを Release 構成で走らせる**

Run: `./build/ci.sh`
Expected: 末尾に `==> CI OK` が出る。すべてのゲート（format/build/test/coverage）が緑。

- [ ] **Step 2: オフライン検査が App を含めて走ったか手動で確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests -c Release --logger "console;verbosity=detailed" --filter "FullyQualifiedName~Offline"`
Expected: テスト一覧に
- `Tsumugi.Infrastructure.Tests.OfflineComplianceTests.Tsumugi_assemblies_do_not_reference_network_libraries(assemblyName: "Tsumugi.Domain")`
- 同 (`Tsumugi.Application`)
- 同 (`Tsumugi.Infrastructure`)
- `Tsumugi.Infrastructure.Tests.AppOfflineComplianceTests.TsumugiApp_dll_does_not_directly_reference_network_apis`

の **4 件が PASS** として出る。最後の 1 件が App 対象であることが目視できる。

- [ ] **Step 3: OS別パーミッションテストが正しく振り分けられたか確認**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests -c Release --filter "FullyQualifiedName~SqliteLocationServiceTests"`
Expected:
- macOS/Linux 環境: `..._on_unix` 系 2 件 PASS、`..._on_windows` 1 件 PASS（早期 return）。
- Windows 環境: `..._on_windows` 1 件 PASS、`..._on_unix` 系 2 件 PASS（早期 return）。

- [ ] **Step 4: 受け入れ基準セルフチェック（修正指示書 §受け入れ基準）**

以下の各項目について、対応する成果物を本プランから 1 行で引用してチェックを埋める:

```
- [x] Tsumugi.App がオフライン検査対象に含まれ、禁止API参照で実際に赤になる（歯のある検査）
      → Task 1 Step 5 で HttpClient 一時混入により赤を再現済み。
- [x] allowlist が明示・最小・理由付き（既定は空）
      → AppOfflineComplianceTests.Allowlist は空配列、追加は (FullName, Reason) 必須。
- [x] docs/open-questions.md の App 手動レビュー項目がクローズ
      → Task 6 Step 2 でクローズコメント付き。
- [x] 保存先決定が Infrastructure のサービスへ分離され、App は合成ルートで利用するのみ
      → SqliteLocationService、App.axaml.cs は new + EnsureSecuredStorage + ConnectionString のみ。
- [x] Unix 0700/0600・Windows ユーザーのみ の DACL を作成時に保証。既存ゆるい権限の扱いを実装
      → Task 2/3/4。既存ゆるい権限は冪等に締め直し（ADR 0003 追補参照）。
- [x] OS別権限検証テストが該当OSで緑・非該当でスキップ
      → SqliteLocationServiceTests の 3 [Fact]、`if (!IsUnix/IsWindows) return;` でスキップ。
- [x] 0003-sqlite-location-and-encryption.md を権限ポリシーで更新
      → Task 6 Step 1。
- [x] AC0-5 / AC0-6 適合
      → 親文書 §7 の AC0-5/AC0-6 のチェックボックスを [x] に更新するのが本タスクの最終アクション。
```

- [ ] **Step 5: 親文書 §7 のチェックボックスを更新**

`01_ClaudeCode_実装指示書_Tsumugi.md` の以下 2 行を `- [ ]` → `- [x]` に変更:

旧:
```
- [ ] (AC0-5) 品質ゲート §6 の 1〜5 がCIで判定される。...
- [ ] (AC0-6) SQLiteの保存先決定がInfrastructureのサービスへ分離され、...
```

新:
```
- [x] (AC0-5) 品質ゲート §6 の 1〜5 がCIで判定される。...
- [x] (AC0-6) SQLiteの保存先決定がInfrastructureのサービスへ分離され、...
```

(本文の説明部分は変更しない)

- [ ] **Step 6: 最終コミット**

```bash
git add 01_ClaudeCode_実装指示書_Tsumugi.md
git commit -m "phase0: mark AC0-5 and AC0-6 satisfied"
```

- [ ] **Step 7: フェーズ0 完了の宣言**

ユーザに以下を伝える:
```
フェーズ0 修正実装完了。AC0-5 / AC0-6 を満たし、Codex レビューの Major × 2 を解消。
- (A) AppOfflineComplianceTests.cs で App.dll のオフライン参照を機械判定（PEReader 走査・歯のある検査確認済み）
- (B) SqliteLocationService で Unix 0700/0600 / Windows DACL を作成時強制、既存ゆるい権限は冪等に締め直し
再レビュー依頼 → フェーズ1 着手の合図待ち。
```

---

## Self-Review（プラン作成者によるチェック）

**1. 仕様カバレッジ:**
- 修正指示書「結論」(A)(B) → Task 1 / Task 2-5 で実装。
- 「(A) 走査方式: PEReader/MetadataReader、自身の TypeRef/MemberRef のみ」→ Task 1 Step 1 `AssemblyMetadataScanner` に明記。
- 「(A) allowlist 既定空・理由付き」→ Task 1 Step 2 `Allowlist` 配列定義。
- 「(B) Infrastructure に分離」→ Task 2 で `SqliteLocationService` を Infrastructure に新設。
- 「(B) Unix 0700/0600」→ Task 2/4。
- 「(B) Windows DACL 現在ユーザーのみ・継承無効・継承 ACE 除去」→ Task 3。
- 「(B) `[SupportedOSPlatform("windows")]`」→ Task 3 Step 3。
- 「(B) 既存ゆるい権限の扱いを ADR に記す」→ Task 4 + Task 6 Step 1。
- 「(B) ADR 0003 を更新」→ Task 6 Step 1。
- 「open-questions の App 手動レビュー項目クローズ」→ Task 6 Step 2。
- 「TDD 歯のある赤の確認」→ Task 1 Step 5。
- 「OS別ケース、非該当 OS では clean skip」→ Task 2/3/4 各テストの `if (!IsUnix/IsWindows) return;`。
- 「親文書 §7 AC0-5/AC0-6 適合」→ Task 7 Step 5。

**2. プレースホルダ走査:** "TBD", "TODO", "implement later" など含まず。コード断片はすべて完全形。

**3. 型整合:**
- `ISqliteLocation` のメンバ (`DatabasePath`/`ConnectionString`/`EnsureSecuredStorage`) は Task 2 で定義し Task 5 で `SqliteLocationService` インスタンスから読み出している。一致。
- `SqliteLocationService(string applicationDataRoot)` のシグネチャは Task 2/5 で同一。
- `AssemblyMetadataScanner.ScanReferencedTypeFullNames(string)` の戻り値 `IReadOnlyList<string>` を `AppOfflineComplianceTests` で `Where`/`ToHashSet`/`ToArray` してアサート。一致。
- `AppAssemblyLocator.LocateTsumugiAppDll()` の戻り値 `string` を `File.OpenRead` に渡せる。一致。

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-06-26-tsumugi-phase0-codex-rev1.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — タスクごとに新鮮なサブエージェントを起動し、各タスク完了時にレビューチェックポイント。`superpowers:subagent-driven-development` を使用。

**2. Inline Execution** — 本セッション内で `superpowers:executing-plans` を使ってバッチ実行（チェックポイントで停止）。

**どちらで進めますか？**
