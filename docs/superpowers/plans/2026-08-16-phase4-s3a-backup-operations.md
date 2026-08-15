# Phase 4 S3a 実装計画 — バックアップ運用化（終了時自動・世代管理・復元・保存先権限）

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:subagent-driven-development`（推奨）または `superpowers:executing-plans` でタスク単位に実装すること。ステップは checkbox (`- [ ]`) で進捗を管理する。**進捗の正本はこのチェックボックス。**

**Goal:** 実装済みだが UI から到達できない `IBackupService` を、終了時自動バックアップ・世代管理・復元・保存先権限強制まで備えた運用可能な機能に引き上げ、ADR 0003 の暗号化採否を確定する。

**Architecture:** 権限適用ロジックを `SecureFileSystem` へ抽出して DB とバックアップの双方に効かせる。保持規則は Application の純粋関数 `BackupGenerationPolicy` に閉じる。ファイル I/O（`VACUUM INTO`・接続プール解放・ファイル置換）は Infrastructure のサービスに閉じ、UseCase は順序と監査だけを持つ。設定は持たず、保存先と保持世代は固定する。

**Tech Stack:** .NET 10 / C# 14、EF Core 10 ＋ `Microsoft.Data.Sqlite`、Avalonia 11 ＋ CommunityToolkit.Mvvm、xUnit ＋ FluentAssertions。**新規 NuGet 依存・migration は無い**（`AuditAction` への enum 値追加は `HasConversion<int>()` の末尾追加なのでスキーマ変更を伴わない）。

## Global Constraints

- **spec正本**: `docs/superpowers/specs/2026-08-16-phase4-s3-backup-operations-design.md`。逸脱は理由付きで ADR へ記録する。
- **依存方向を破らないこと（本計画の最大の罠）**: `ISqliteLocation` は `Tsumugi.Infrastructure.Persistence` にある。**Application からこれを参照してはならない**（`ArchitectureTests` が落ちる）。Application が保存先を知る必要がある箇所では、`Tsumugi.Application.Abstractions` に**新しいインターフェースを立てて** `SqliteLocationService` に実装させる。
- **設定を作らない**（spec 決定3）。`AutoBackupOnExit` / `MaxGenerations` のような設定項目も、設定の永続化機構も作らない。保存先・保持世代は固定。
- **純粋関数は日付/乱数/I/O に依存しない**。`BackupGenerationPolicy` は基準日を引数で受け取る。
- **ログ・監査にフルパスを書かない**（CLAUDE.md ハード制約4）。固定ディレクトリのものはファイル名のみ、利用者が選んだ保存先は**ファイル名もパスも記録しない**。
- **`VACUUM INTO` の宛先ファイルは既存であってはならない**（空ファイルは可）。一次資料 `https://www.sqlite.org/lang_vacuum.html` §2.1。必ず一時名へ書いてから移動する。
- **`VACUUM` は同じ接続にオープン中のトランザクションがあると失敗する**（同上）。バックアップを保存処理の途中で呼ばない。
- **`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` / `<Nullable>enable</Nullable>`**。`dotnet build` は警告ゼロが前提。
- **bulk operations 禁止**（ADR 0050）。`ExecuteUpdate*`/`ExecuteDelete*` と、行を書き換える raw SQL を `src/` に書かない。`VACUUM INTO` は内容判定で通る。
- **クロスプラットフォーム**: 権限テストは OS 別に `[Fact]` を立て、該当 OS 以外は早期 `return`（xUnit 2.x のため `Skip.If` は使わない）。**利用可能な実機は macOS のみ**なので、Windows 固有経路は自動テストとレビューで担保する。
- ブランチは既に `feature/phase4-s3-backup-operations`（spec コミット `5bec970` 済み）。各タスクの最後に `dotnet build`（警告ゼロ）と関連テストの緑を確認してからコミットする。全タスク完了後に `./build/ci.sh` を通す。

---

## File Structure

| ファイル | 責務 | 種別 |
|---|---|---|
| `src/Tsumugi.Infrastructure/Persistence/SecureFileSystem.cs` | OS 別の最小権限をディレクトリ／ファイルへ適用する。`internal static` | 新規 |
| `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs` | 権限適用を `SecureFileSystem` へ委譲。`BackupDirectory` を持つ | 変更 |
| `src/Tsumugi.Infrastructure/Persistence/ISqliteLocation.cs` | `BackupDirectory` を追加 | 変更 |
| `src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs` | 一時名→移動、権限適用 | 変更 |
| `src/Tsumugi.Infrastructure/Persistence/SqliteRestoreService.cs` | 接続プール解放・DB 置換・WAL/SHM 削除 | 新規 |
| `src/Tsumugi.Application/Abstractions/IDatabaseFileLocation.cs` | Application 側から見た保存先（DB パス・バックアップディレクトリ） | 新規 |
| `src/Tsumugi.Application/Abstractions/IDatabaseRestoreService.cs` | 復元の I/O 抽象 | 新規 |
| `src/Tsumugi.Application/Backup/BackupGenerationPolicy.cs` | 保持規則の純粋関数 | 新規 |
| `src/Tsumugi.Application/Backup/BackupFileName.cs` | ファイル名の生成と解析（純粋関数） | 新規 |
| `src/Tsumugi.Application/UseCases/Backup/RunScheduledBackupUseCase.cs` | バックアップ→世代削除→監査 | 新規 |
| `src/Tsumugi.Application/UseCases/Backup/RestoreDatabaseUseCase.cs` | 監査→保存→退避→置換 | 新規 |
| `src/Tsumugi.Application/UseCases/Backup/ListBackupGenerationsUseCase.cs` | 世代一覧の取得 | 新規 |
| `src/Tsumugi.Domain/Enums/AuditAction.cs` | `Backup = 4` を追加 | 変更 |
| `src/Tsumugi.App/ViewModels/BackupViewModel.cs` ＋ `Views/BackupView.axaml`(.cs) | 3操作＋世代一覧 | 新規 |
| `src/Tsumugi.App/App.axaml.cs` | 終了時フック | 変更 |
| `src/Tsumugi.App/CompositionRoot.cs` | DI 配線 | 変更 |
| `src/Tsumugi.App/MainWindow.axaml` ＋ `ViewModels/MainViewModel.cs` | タブ追加 | 変更 |

---

## Task 1: 権限適用ロジックを `SecureFileSystem` へ抽出する

**Files:**
- Create: `src/Tsumugi.Infrastructure/Persistence/SecureFileSystem.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/SecureFileSystemTests.cs`（新規）

**Interfaces:**
- Produces: `internal static class SecureFileSystem` の `EnsureDirectory(string path)` / `EnsureFile(string path)` / `TryEnsureFile(string path) → bool`。Task 3・5 が使う。
- Consumes: 無し。

> **これはリファクタリングであり、振る舞いを変えない。** 既存の `tests/Tsumugi.Infrastructure.Tests/SqliteLocationServiceTests.cs` がそのまま回帰網になる。**このタスクで既存テストを1行も変えないこと。**

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.Infrastructure.Tests/SecureFileSystemTests.cs` を新規作成する。

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// ADR 0003 追補の権限ポリシーを、DB 以外のファイル（バックアップ）にも適用できる形へ抽出したもの。
/// Unix: dir 0700 / file 0600。Windows: 現在ユーザーのみフルコントロール・継承無効。
/// </summary>
public sealed class SecureFileSystemTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-securefs-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureDirectory_creates_the_directory_when_missing()
    {
        SecureFileSystem.EnsureDirectory(_root);
        Directory.Exists(_root).Should().BeTrue();
    }

    [Fact]
    public void EnsureDirectory_is_idempotent()
    {
        SecureFileSystem.EnsureDirectory(_root);
        var act = () => SecureFileSystem.EnsureDirectory(_root);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureFile_creates_the_file_when_missing()
    {
        SecureFileSystem.EnsureDirectory(_root);
        var file = Path.Combine(_root, "x.db");
        SecureFileSystem.EnsureFile(file);
        File.Exists(file).Should().BeTrue();
    }

    [Fact]
    public void Unix_directory_mode_is_0700_and_file_mode_is_0600()
    {
        // 該当 OS 以外は早期 return（xUnit 2.x のため Skip.If は使わない）
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        SecureFileSystem.EnsureDirectory(_root);
        var file = Path.Combine(_root, "x.db");
        SecureFileSystem.EnsureFile(file);

        const UnixFileMode dirMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        const UnixFileMode fileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        File.GetUnixFileMode(_root).Should().Be(dirMode);
        File.GetUnixFileMode(file).Should().Be(fileMode);
    }

    [Fact]
    public void Unix_tightens_an_existing_loose_mode()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        Directory.CreateDirectory(_root);
        File.SetUnixFileMode(_root,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        SecureFileSystem.EnsureDirectory(_root);

        File.GetUnixFileMode(_root).Should().Be(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public void TryEnsureFile_returns_false_instead_of_throwing_when_the_path_is_unusable()
    {
        // 存在しないディレクトリ配下のファイルには権限を適用できない。
        // 外部媒体（FAT32/exFAT 等）で権限適用が失敗する状況の代理。
        var unusable = Path.Combine(_root, "no-such-dir", "x.db");
        SecureFileSystem.TryEnsureFile(unusable).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet build tests/Tsumugi.Infrastructure.Tests`

Expected: **コンパイルエラー**（`SecureFileSystem` が存在しない）。これがこのタスクの RED。

- [ ] **Step 3: `SecureFileSystem` を実装する**

`src/Tsumugi.Infrastructure/Persistence/SecureFileSystem.cs` を新規作成する。中身は現行 `SqliteLocationService` の私有メソッドを、対象パスを引数で受ける形へ移したもの。

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// ADR 0003 追補の権限ポリシーを、任意のディレクトリ／ファイルへ適用する。
/// Unix: dir 0700 / file 0600。Windows: 現在ユーザーのみフルコントロール・継承無効。
/// 既存のゆるい権限は「広げない・狭めるのみ」で冪等に締め直す。
/// </summary>
internal static class SecureFileSystem
{
    private const UnixFileMode DirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode FileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void EnsureDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (IsUnix())
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path, DirectoryMode);
            else File.SetUnixFileMode(path, DirectoryMode);
            return;
        }

        if (OperatingSystem.IsWindows()) { EnsureWindowsDirectory(path); return; }

        throw new PlatformNotSupportedException(
            "サポートされないOSで Tsumugi の保存先を初期化しようとした。");
    }

    public static void EnsureFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (IsUnix())
        {
            // File.Create はプロセス umask 経由の権限で作成し、直後に 0600 へ締める。
            // 管理コードに「モード付きアトミック作成」API は無いため、この瞬間は不可避。
            // 親ディレクトリ 0700 の保護が外部ユーザーを遮るため実害は無視できる。
            if (!File.Exists(path)) { using (File.Create(path)) { } }
            File.SetUnixFileMode(path, FileMode);
            return;
        }

        if (OperatingSystem.IsWindows()) { EnsureWindowsFile(path); return; }

        throw new PlatformNotSupportedException(
            "サポートされないOSで Tsumugi の保存先を初期化しようとした。");
    }

    /// <summary>
    /// 権限適用を試み、失敗しても例外にしない版。外部媒体（FAT32/exFAT 等、
    /// Unix パーミッションも Windows ACL も持たないファイルシステム）向け。
    /// 「安全のための操作」が安全機構のせいで失敗するのを避ける。
    /// </summary>
    public static bool TryEnsureFile(string path)
    {
        try { EnsureFile(path); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (PlatformNotSupportedException) { return false; }
    }

    private static bool IsUnix() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsDirectory(string path)
    {
        var currentUser = WindowsIdentity.GetCurrent().User!;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        if (!Directory.Exists(path)) Directory.CreateDirectory(path).SetAccessControl(security);
        else new DirectoryInfo(path).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void EnsureWindowsFile(string path)
    {
        var currentUser = WindowsIdentity.GetCurrent().User!;
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser, FileSystemRights.FullControl, AccessControlType.Allow));

        if (!File.Exists(path)) { using (File.Create(path)) { } }
        new FileInfo(path).SetAccessControl(security);
    }
}
```

- [ ] **Step 4: `SqliteLocationService` を委譲へ書き換える**

`EnsureUnix()` / `EnsureWindows()` の2メソッドと、それらが使っていた `using` を削除し、`EnsureSecuredStorage()` を次に差し替える。`_directory` フィールド・コンストラクタ・`DatabasePath` / `ConnectionString` は変更しない。

```csharp
    public void EnsureSecuredStorage()
    {
        SecureFileSystem.EnsureDirectory(_directory);
        SecureFileSystem.EnsureFile(DatabasePath);
    }
```

不要になった `using` を削除する（`System.Runtime.InteropServices` / `System.Runtime.Versioning` / `System.Security.AccessControl` / `System.Security.Principal`）。`<TreatWarningsAsErrors>` があるため未使用 using が残ると失敗する場合がある。

- [ ] **Step 5: 新旧両方のテストが通ることを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~SecureFileSystemTests|FullyQualifiedName~SqliteLocationServiceTests"`

Expected: 両クラスとも全緑。**`SqliteLocationServiceTests` を変更せずに緑になること**が、振る舞いを変えていない証拠。

- [ ] **Step 6: ビルド警告ゼロを確認してコミットする**

```bash
dotnet build
git add src/Tsumugi.Infrastructure/Persistence/SecureFileSystem.cs \
        src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs \
        tests/Tsumugi.Infrastructure.Tests/SecureFileSystemTests.cs
git commit -m "refactor(phase4-s3a): 権限適用ロジックを SecureFileSystem へ抽出する"
```

---

## Task 2: 保持規則の純粋関数

**Files:**
- Create: `src/Tsumugi.Application/Backup/BackupFileName.cs`
- Create: `src/Tsumugi.Application/Backup/BackupGenerationPolicy.cs`
- Test: `tests/Tsumugi.Application.Tests/Backup/BackupGenerationPolicyTests.cs`（新規。`Backup` ディレクトリも新規）

**Interfaces:**
- Produces:
  - `public static class BackupFileName` の
    `Create(DateTimeOffset at) → string`（`tsumugi-backup-yyyyMMdd-HHmmss.db`）、
    `CreatePreRestore(DateTimeOffset at) → string`（`pre-restore-yyyyMMdd-HHmmss.db`）、
    `TryParse(string fileName, out DateTimeOffset at) → bool`（自動バックアップ名だけを解析。`pre-restore-` は `false`）。
  - `public static class BackupGenerationPolicy` の
    `SelectForDeletion(IEnumerable<string> fileNames, DateOnly asOf) → IReadOnlyList<string>`。
- Consumes: 無し。Task 4 が両方を使う。

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.Application.Tests/Backup/BackupGenerationPolicyTests.cs` を新規作成する。

```csharp
using System;
using System.Linq;
using FluentAssertions;
using Tsumugi.Application.Backup;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

/// <summary>
/// spec 決定4: 同日は最新1つだけ残し、直近7日分を保持する。
/// 「最新N件を残す」にすると、1日に何度も起動終了した日でN件が埋まり
/// 1週間分の履歴が1日分に潰れるため、日付単位で数える。
/// </summary>
public sealed class BackupGenerationPolicyTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 16);

    [Fact]
    public void Keeps_only_the_newest_file_of_each_day()
    {
        string[] files =
        [
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-180000.db",
            "tsumugi-backup-20260816-120000.db",
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().BeEquivalentTo(
        [
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-120000.db",
        ]);
    }

    [Fact]
    public void Keeps_seven_days_and_deletes_the_eighth()
    {
        // 2026-08-16 から遡って 8 日分。最も古い 2026-08-09 が落ちる。
        var files = Enumerable.Range(0, 8)
            .Select(i => $"tsumugi-backup-{AsOf.AddDays(-i):yyyyMMdd}-100000.db")
            .ToArray();

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().ContainSingle().Which.Should().Be("tsumugi-backup-20260809-100000.db");
    }

    [Fact]
    public void Counts_days_not_files()
    {
        // 同じ日に 10 件あっても「1 日分」として数える。7 日分は残る。
        var manySameDay = Enumerable.Range(0, 10)
            .Select(i => $"tsumugi-backup-20260816-{i:00}0000.db");
        var otherDays = Enumerable.Range(1, 6)
            .Select(i => $"tsumugi-backup-{AsOf.AddDays(-i):yyyyMMdd}-100000.db");

        var deleted = BackupGenerationPolicy.SelectForDeletion(
            manySameDay.Concat(otherDays), AsOf);

        // 削除されるのは 2026-08-16 の古い 9 件だけ。他日の 6 件は 7 日以内なので残る。
        deleted.Should().HaveCount(9);
        deleted.Should().OnlyContain(f => f.StartsWith("tsumugi-backup-20260816-", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignores_pre_restore_snapshots()
    {
        string[] files =
        [
            "pre-restore-20200101-000000.db",
            "pre-restore-20200102-000000.db",
            "tsumugi-backup-20260816-100000.db",
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().BeEmpty();
    }

    [Fact]
    public void Ignores_files_that_do_not_match_the_naming_convention()
    {
        string[] files =
        [
            "readme.txt",
            "tsumugi.db",
            "tsumugi-backup-not-a-date.db",
            "tsumugi-backup-20200101-000000.db",   // 命名規則に合致する古いもの → 削除対象
        ];

        var deleted = BackupGenerationPolicy.SelectForDeletion(files, AsOf);

        deleted.Should().ContainSingle().Which.Should().Be("tsumugi-backup-20200101-000000.db");
    }

    [Fact]
    public void Returns_empty_for_an_empty_input()
    {
        BackupGenerationPolicy.SelectForDeletion([], AsOf).Should().BeEmpty();
    }

    [Fact]
    public void Is_deterministic_and_does_not_depend_on_input_order()
    {
        string[] ascending =
        [
            "tsumugi-backup-20260810-100000.db",
            "tsumugi-backup-20260816-100000.db",
        ];
        var descending = ascending.Reverse().ToArray();

        BackupGenerationPolicy.SelectForDeletion(ascending, AsOf)
            .Should().BeEquivalentTo(BackupGenerationPolicy.SelectForDeletion(descending, AsOf));
    }

    [Fact]
    public void Create_and_TryParse_round_trip()
    {
        var at = new DateTimeOffset(2026, 8, 16, 13, 45, 7, TimeSpan.Zero);

        var name = BackupFileName.Create(at);
        name.Should().Be("tsumugi-backup-20260816-134507.db");

        BackupFileName.TryParse(name, out var parsed).Should().BeTrue();
        parsed.Should().Be(new DateTimeOffset(2026, 8, 16, 13, 45, 7, TimeSpan.Zero));
    }

    [Fact]
    public void TryParse_rejects_pre_restore_names()
    {
        var name = BackupFileName.CreatePreRestore(
            new DateTimeOffset(2026, 8, 16, 13, 45, 7, TimeSpan.Zero));
        name.Should().Be("pre-restore-20260816-134507.db");

        BackupFileName.TryParse(name, out _).Should().BeFalse();
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet build tests/Tsumugi.Application.Tests`

Expected: **コンパイルエラー**（`Tsumugi.Application.Backup` 名前空間も型も存在しない）。

- [ ] **Step 3: `BackupFileName` を実装する**

`src/Tsumugi.Application/Backup/BackupFileName.cs`:

```csharp
using System.Globalization;

namespace Tsumugi.Application.Backup;

/// <summary>
/// バックアップファイル名の生成と解析。時刻に依存しない（生成時刻は引数で受け取る）。
/// 自動バックアップ: tsumugi-backup-yyyyMMdd-HHmmss.db
/// 復元前の退避:     pre-restore-yyyyMMdd-HHmmss.db（世代管理の対象外）
/// </summary>
public static class BackupFileName
{
    public const string AutomaticPrefix = "tsumugi-backup-";
    public const string PreRestorePrefix = "pre-restore-";
    public const string Extension = ".db";

    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    // 埋め込む時刻は常に UTC（入力のオフセットに依らない）。TryParse が UTC として読み戻す契約と対になる。
    // at.ToString(...) にすると入力のオフセットローカルな数字が入り、SelectForDeletion の
    // 日付バケットが最大1日ずれて 7 日境界の判定を誤る（Task 2 レビューで実測。2026-08-16 訂正）。
    public static string Create(DateTimeOffset at) =>
        AutomaticPrefix + at.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture) + Extension;

    public static string CreatePreRestore(DateTimeOffset at) =>
        PreRestorePrefix + at.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture) + Extension;

    /// <summary>自動バックアップ名だけを解析する。pre-restore や規則外の名前は false。</summary>
    public static bool TryParse(string fileName, out DateTimeOffset at)
    {
        at = default;
        ArgumentNullException.ThrowIfNull(fileName);

        if (!fileName.StartsWith(AutomaticPrefix, StringComparison.Ordinal)) return false;
        if (!fileName.EndsWith(Extension, StringComparison.Ordinal)) return false;

        var stamp = fileName[AutomaticPrefix.Length..^Extension.Length];
        if (!DateTimeOffset.TryParseExact(
                stamp, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        at = parsed;
        return true;
    }
}
```

- [ ] **Step 4: `BackupGenerationPolicy` を実装する**

`src/Tsumugi.Application/Backup/BackupGenerationPolicy.cs`:

```csharp
namespace Tsumugi.Application.Backup;

/// <summary>
/// バックアップ世代の保持規則（spec 決定4）。純粋関数。日付は引数で受け取る。
/// 1. 命名規則に合致しないファイル・pre-restore の退避は対象外（触らない）。
/// 2. 同一日の中では最も新しい 1 つだけ残す。
/// 3. 残った日付のうち、基準日から数えて新しい 7 日分だけ残す。
/// </summary>
public static class BackupGenerationPolicy
{
    /// <summary>保持する日数。</summary>
    public const int RetainedDays = 7;

    public static IReadOnlyList<string> SelectForDeletion(
        IEnumerable<string> fileNames, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var parsed = new List<(string Name, DateTimeOffset At)>();
        foreach (var name in fileNames)
        {
            if (BackupFileName.TryParse(name, out var at)) parsed.Add((name, at));
        }

        var deletions = new List<string>();

        var byDay = parsed
            .GroupBy(x => DateOnly.FromDateTime(x.At.UtcDateTime))
            .OrderByDescending(g => g.Key)
            .ToList();

        for (var dayIndex = 0; dayIndex < byDay.Count; dayIndex++)
        {
            var group = byDay[dayIndex];
            var dayIsRetained =
                dayIndex < RetainedDays && group.Key > asOf.AddDays(-RetainedDays);

            if (!dayIsRetained)
            {
                deletions.AddRange(group.Select(x => x.Name));
                continue;
            }

            // 同日は最新 1 つだけ残す。同時刻が複数ある場合は名前順で決定論的に選ぶ。
            var survivor = group
                .OrderByDescending(x => x.At)
                .ThenByDescending(x => x.Name, StringComparer.Ordinal)
                .First();

            deletions.AddRange(group.Where(x => x.Name != survivor.Name).Select(x => x.Name));
        }

        return deletions.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
```

> `dayIsRetained` が 2 条件なのは、`asOf` より未来の日付が混ざった場合でも「新しい 7 日分」を素直に数えるため。件数（`dayIndex < RetainedDays`）と暦日の窓（`asOf.AddDays(-7)` より新しい）の両方を満たす日だけ残す。

- [ ] **Step 5: テストが通ることを確認する**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~BackupGenerationPolicyTests"`

Expected: 9件全緑。

- [ ] **Step 6: ビルド警告ゼロを確認してコミットする**

```bash
dotnet build
git add src/Tsumugi.Application/Backup/ tests/Tsumugi.Application.Tests/Backup/
git commit -m "feat(phase4-s3a): バックアップ世代の保持規則を純粋関数として追加する"
```

---

## Task 3: バックアップ保存先と `VACUUM INTO` の衝突回避

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IDatabaseFileLocation.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/ISqliteLocation.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs`
- Modify: `src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/SqliteBackupServiceTests.cs`（新規）

**Interfaces:**
- Consumes: Task 1 の `SecureFileSystem.EnsureDirectory` / `TryEnsureFile`。
- Produces:
  - `Tsumugi.Application.Abstractions.IDatabaseFileLocation` の `string DatabasePath { get; }` / `string BackupDirectory { get; }`。**Application 側の型**。Task 4・5 が使う。
  - `ISqliteLocation.BackupDirectory`（Infrastructure 側。`SqliteLocationService` が両インターフェースを実装する）。

> **依存方向の罠**: `ISqliteLocation` は Infrastructure の型なので Application から参照できない。だから同じ情報を Application 側の `IDatabaseFileLocation` で宣言し、`SqliteLocationService` に両方を実装させる。

- [ ] **Step 1: 失敗するテストを書く**

`tests/Tsumugi.Infrastructure.Tests/SqliteBackupServiceTests.cs` を新規作成する。

```csharp
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// VACUUM INTO は宛先ファイルが既存だと失敗する（https://www.sqlite.org/lang_vacuum.html §2.1）。
/// 一時名へ書いてから移動することで、同名の既存バックアップがあっても上書きできることを固定する。
/// </summary>
public sealed class SqliteBackupServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-backup-" + Guid.NewGuid().ToString("N"));

    private TsumugiDbContext NewContext()
    {
        Directory.CreateDirectory(_root);
        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_root, "tsumugi.db")}")
            .Options;
        var db = new TsumugiDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task BackupToAsync_creates_the_destination_file()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");

        await sut.BackupToAsync(destination, CancellationToken.None);

        File.Exists(destination).Should().BeTrue();
        new FileInfo(destination).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BackupToAsync_overwrites_an_existing_destination()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");
        await File.WriteAllTextAsync(destination, "既存の中身", CancellationToken.None);

        // VACUUM INTO を宛先へ直接発行していると、ここで SqliteException になる。
        await sut.BackupToAsync(destination, CancellationToken.None);

        var head = await File.ReadAllBytesAsync(destination, CancellationToken.None);
        // SQLite ファイルは "SQLite format 3\0" で始まる
        System.Text.Encoding.ASCII.GetString(head, 0, 15).Should().Be("SQLite format 3");
    }

    [Fact]
    public void EnsureSecuredStorage_creates_the_backup_directory_with_the_same_permissions()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        Directory.Exists(location.BackupDirectory).Should().BeTrue();

        // 該当 OS 以外は早期 return（xUnit 2.x のため Skip.If は使わない）
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        File.GetUnixFileMode(location.BackupDirectory).Should().Be(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task BackupToAsync_applies_the_permission_policy_to_the_written_file()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");

        await sut.BackupToAsync(destination, CancellationToken.None);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

        File.GetUnixFileMode(destination).Should().Be(
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task BackupToAsync_leaves_no_temporary_file_behind()
    {
        using var db = NewContext();
        var sut = new SqliteBackupService(db);
        var destination = Path.Combine(_root, "out.db");

        await sut.BackupToAsync(destination, CancellationToken.None);

        Directory.EnumerateFiles(_root, "*.tmp").Should().BeEmpty();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~SqliteBackupServiceTests"`

Expected: `BackupToAsync_overwrites_an_existing_destination` が **FAIL**（`SqliteException`: output file already exists）。`EnsureSecuredStorage_creates_the_backup_directory_with_the_same_permissions` と `BackupToAsync_applies_the_permission_policy_to_the_written_file` も **FAIL**（`BackupDirectory` が未実装／権限適用が未実装）。残り2件は現行実装でも PASS しうる。

- [ ] **Step 3: `IDatabaseFileLocation` を追加する**

`src/Tsumugi.Application/Abstractions/IDatabaseFileLocation.cs`:

```csharp
namespace Tsumugi.Application.Abstractions;

/// <summary>
/// Application 層から見た DB の保存先。Infrastructure の ISqliteLocation とは別に宣言する
/// （依存方向: Application は Infrastructure を知らない）。実装は SqliteLocationService。
/// </summary>
public interface IDatabaseFileLocation
{
    /// <summary>DB ファイルの絶対パス。</summary>
    string DatabasePath { get; }

    /// <summary>自動バックアップと復元前退避の保存先ディレクトリの絶対パス。</summary>
    string BackupDirectory { get; }
}
```

- [ ] **Step 4: `ISqliteLocation` と `SqliteLocationService` を広げる**

`ISqliteLocation` に次を追加する。

```csharp
    /// <summary>自動バックアップの保存先ディレクトリ。EnsureSecuredStorage で作成・権限適用される。</summary>
    string BackupDirectory { get; }
```

`SqliteLocationService` を次のように変える（`IDatabaseFileLocation` も実装する）。

```csharp
public sealed class SqliteLocationService : ISqliteLocation, IDatabaseFileLocation
{
    private readonly string _directory;

    public SqliteLocationService(string applicationDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDataRoot);
        _directory = applicationDataRoot;
        DatabasePath = Path.Combine(applicationDataRoot, "tsumugi.db");
        BackupDirectory = Path.Combine(applicationDataRoot, "backups");
        ConnectionString = $"Data Source={DatabasePath}";
    }

    public string DatabasePath { get; }
    public string BackupDirectory { get; }
    public string ConnectionString { get; }

    public void EnsureSecuredStorage()
    {
        SecureFileSystem.EnsureDirectory(_directory);
        SecureFileSystem.EnsureFile(DatabasePath);
        SecureFileSystem.EnsureDirectory(BackupDirectory);
    }
}
```

`using Tsumugi.Application.Abstractions;` を追加すること。

- [ ] **Step 5: `SqliteBackupService` を一時名→移動に変える**

```csharp
using Microsoft.EntityFrameworkCore;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

public sealed class SqliteBackupService(TsumugiDbContext db) : IBackupService
{
    public async Task BackupToAsync(string destinationPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        // VACUUM INTO は宛先が既存だと失敗する（空ファイルなら可）。
        // https://www.sqlite.org/lang_vacuum.html §2.1
        // そのため一時名へ書き、成功してから移動する。移動は同一ディレクトリ内なので原子的。
        var temporaryPath = destinationPath + ".tmp";
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);

        var escaped = temporaryPath.Replace("'", "''");
        try
        {
            // SQLite の VACUUM INTO は単一ファイルの一貫したバックアップを生成する。
            // パスはパラメータ化できないため、シングルクォートをエスケープして埋め込む。
#pragma warning disable EF1002 // VACUUM INTO はパラメータ化不可。シングルクォートをエスケープして埋め込む。
            await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}'", ct);
#pragma warning restore EF1002

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            // 失敗時に半端な一時ファイルを残さない。
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }

        // 保存先の権限を締める。外部媒体で適用できない場合は警告扱いで続行する。
        SecureFileSystem.TryEnsureFile(destinationPath);
    }
}
```

- [ ] **Step 6: テストが通ることを確認してコミットする**

Run: `dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~SqliteBackupServiceTests|FullyQualifiedName~SqliteLocationServiceTests"`

Expected: 全緑。

```bash
dotnet build
git add src/Tsumugi.Application/Abstractions/IDatabaseFileLocation.cs \
        src/Tsumugi.Infrastructure/Persistence/ISqliteLocation.cs \
        src/Tsumugi.Infrastructure/Persistence/SqliteLocationService.cs \
        src/Tsumugi.Infrastructure/Persistence/SqliteBackupService.cs \
        tests/Tsumugi.Infrastructure.Tests/SqliteBackupServiceTests.cs
git commit -m "feat(phase4-s3a): バックアップ保存先を追加し VACUUM INTO の既存ファイル衝突を回避する"
```

---

## Task 4: 終了時バックアップのユースケース

**Files:**
- Modify: `src/Tsumugi.Domain/Enums/AuditAction.cs`
- Create: `src/Tsumugi.Application/UseCases/Backup/RunScheduledBackupUseCase.cs`
- Create: `src/Tsumugi.Application/UseCases/Backup/ListBackupGenerationsUseCase.cs`
- Create: `src/Tsumugi.Application/Abstractions/IBackupDirectory.cs`
- Test: `tests/Tsumugi.Application.Tests/Backup/RunScheduledBackupUseCaseTests.cs`

**Interfaces:**
- Consumes: Task 2 の `BackupFileName.Create` / `BackupGenerationPolicy.SelectForDeletion`、Task 3 の `IDatabaseFileLocation.BackupDirectory`、既存の `IBackupService.BackupToAsync` / `IAuditTrail.RecordAsync` / `IUnitOfWork.SaveChangesAsync`。
- Produces: `RunScheduledBackupUseCase.ExecuteAsync(CancellationToken) → Task`、`ListBackupGenerationsUseCase.Execute() → IReadOnlyList<string>`、`IBackupDirectory`（ディレクトリ列挙・削除の抽象）。Task 6・7 が使う。

> ディレクトリの列挙と削除は I/O なので Application が直接 `Directory.EnumerateFiles` を呼ばない。`IBackupDirectory` を立てて Infrastructure に実装させる。これによりユースケースはファイルシステム無しでテストできる。

- [ ] **Step 1: `AuditAction` に `Backup` を足す**

`src/Tsumugi.Domain/Enums/AuditAction.cs`:

```csharp
namespace Tsumugi.Domain.Enums;

public enum AuditAction
{
    Register = 0,
    Update = 1,
    Archive = 2,
    Restore = 3,
    Backup = 4,
}
```

> `AuditEntryConfiguration` は `HasConversion<int>()` なので、**末尾への追加はスキーマ変更を伴わない**（migration 不要）。既存値の番号を変えないこと。

- [ ] **Step 2: 失敗するテストを書く**

`tests/Tsumugi.Application.Tests/Backup/RunScheduledBackupUseCaseTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

public sealed class RunScheduledBackupUseCaseTests
{
    private sealed class FakeLocation : IDatabaseFileLocation
    {
        public string DatabasePath => "/data/tsumugi.db";
        public string BackupDirectory => "/data/backups";
    }

    private sealed class FakeBackupService : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            Destinations.Add(destinationPath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBackupDirectory(params string[] existing) : IBackupDirectory
    {
        public List<string> Deleted { get; } = [];
        public IReadOnlyList<string> ListFileNames() => existing;
        public void Delete(string fileName) => Deleted.Add(fileName);
    }

    private sealed class FakeAuditTrail : IAuditTrail
    {
        public List<(AuditAction Action, string TargetType, string? Summary)> Records { get; } = [];
        public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
            DateTimeOffset occurredAt, string? summary, CancellationToken ct)
        {
            Records.Add((action, targetType, summary));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.FromResult(0); }
    }

    private static readonly DateTimeOffset Now =
        new(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);

    private static RunScheduledBackupUseCase Build(
        FakeBackupService backup, FakeBackupDirectory dir, FakeAuditTrail audit, FakeUnitOfWork uow)
        => new(new FakeLocation(), backup, dir, audit, uow,
               new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Writes_a_backup_named_from_the_current_time_into_the_backup_directory()
    {
        var backup = new FakeBackupService();
        var uc = Build(backup, new FakeBackupDirectory(), new FakeAuditTrail(), new FakeUnitOfWork());

        await uc.ExecuteAsync(CancellationToken.None);

        backup.Destinations.Should().ContainSingle()
            .Which.Should().EndWith("tsumugi-backup-20260816-173000.db");
    }

    [Fact]
    public async Task Deletes_the_generations_the_policy_selects()
    {
        var dir = new FakeBackupDirectory(
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-120000.db",
            "tsumugi-backup-20200101-000000.db",
            "pre-restore-20200101-000000.db");
        var uc = Build(new FakeBackupService(), dir, new FakeAuditTrail(), new FakeUnitOfWork());

        await uc.ExecuteAsync(CancellationToken.None);

        // 当日の古い2件 + 期限切れ1件。pre-restore は触らない。
        dir.Deleted.Should().BeEquivalentTo(
        [
            "tsumugi-backup-20260816-090000.db",
            "tsumugi-backup-20260816-120000.db",
            "tsumugi-backup-20200101-000000.db",
        ]);
    }

    [Fact]
    public async Task Records_an_audit_entry_without_a_full_path()
    {
        var audit = new FakeAuditTrail();
        var uow = new FakeUnitOfWork();
        var uc = Build(new FakeBackupService(), new FakeBackupDirectory(), audit, uow);

        await uc.ExecuteAsync(CancellationToken.None);

        var record = audit.Records.Should().ContainSingle().Subject;
        record.Action.Should().Be(AuditAction.Backup);
        record.TargetType.Should().Be("Database");
        record.Summary.Should().Contain("tsumugi-backup-20260816-173000.db");
        record.Summary.Should().NotContain("/data");   // フルパスを書かない（ハード制約4）
        uow.SaveCount.Should().Be(1);
    }
}
```

- [ ] **Step 3: テストが失敗することを確認する**

Run: `dotnet build tests/Tsumugi.Application.Tests`

Expected: **コンパイルエラー**（`IBackupDirectory` と `RunScheduledBackupUseCase` が存在しない）。

- [ ] **Step 4: `IBackupDirectory` を追加する**

`src/Tsumugi.Application/Abstractions/IBackupDirectory.cs`:

```csharp
namespace Tsumugi.Application.Abstractions;

/// <summary>
/// バックアップディレクトリの列挙と削除。Application がファイルシステムを直接触らないための抽象。
/// </summary>
public interface IBackupDirectory
{
    /// <summary>ディレクトリ直下のファイル名（パスを含まない）を返す。</summary>
    IReadOnlyList<string> ListFileNames();

    /// <summary>ディレクトリ直下の指定ファイルを削除する。存在しない場合は何もしない。</summary>
    void Delete(string fileName);
}
```

- [ ] **Step 5: ユースケース2本を実装する**

`src/Tsumugi.Application/UseCases/Backup/RunScheduledBackupUseCase.cs`:

```csharp
using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>
/// 終了時の自動バックアップ。バックアップ → 世代削除 → 監査 の順に実行する。
/// 設定は持たない（spec 決定3）。保存先も保持世代も固定。
/// </summary>
public sealed class RunScheduledBackupUseCase(
    IDatabaseFileLocation location,
    IBackupService backupService,
    IBackupDirectory backupDirectory,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string TargetType = "Database";

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var fileName = Application.Backup.BackupFileName.Create(now);
        var destination = Path.Combine(location.BackupDirectory, fileName);

        await backupService.BackupToAsync(destination, ct);

        var asOf = DateOnly.FromDateTime(now.UtcDateTime);
        var doomed = Application.Backup.BackupGenerationPolicy.SelectForDeletion(
            backupDirectory.ListFileNames(), asOf);
        foreach (var name in doomed) backupDirectory.Delete(name);

        // 監査にはファイル名だけを書く（固定ディレクトリなのでフルパスは不要。ハード制約4）。
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"自動バックアップ {fileName}（世代削除 {doomed.Count} 件）");

        await auditTrail.RecordAsync(
            actor: "system", AuditAction.Backup, TargetType, Guid.Empty,
            occurredAt: now, summary, ct);

        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

`src/Tsumugi.Application/UseCases/Backup/ListBackupGenerationsUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>復元 UI 用に、自動バックアップの世代を新しい順で返す。退避（pre-restore）も含める。</summary>
public sealed class ListBackupGenerationsUseCase(IBackupDirectory backupDirectory)
{
    public IReadOnlyList<string> Execute() =>
        backupDirectory.ListFileNames()
            .Where(n => n.EndsWith(Application.Backup.BackupFileName.Extension, StringComparison.Ordinal))
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .ToArray();
}
```

- [ ] **Step 6: テストが通ることを確認してコミットする**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~RunScheduledBackupUseCaseTests"`

Expected: 3件全緑。

```bash
dotnet build
git add src/Tsumugi.Domain/Enums/AuditAction.cs \
        src/Tsumugi.Application/Abstractions/IBackupDirectory.cs \
        src/Tsumugi.Application/UseCases/Backup/ \
        tests/Tsumugi.Application.Tests/Backup/RunScheduledBackupUseCaseTests.cs
git commit -m "feat(phase4-s3a): 終了時バックアップと世代削除のユースケースを追加する"
```

---

## Task 5: 復元

**Files:**
- Create: `src/Tsumugi.Application/Abstractions/IDatabaseRestoreService.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/SqliteRestoreService.cs`
- Create: `src/Tsumugi.Infrastructure/Persistence/BackupDirectoryService.cs`
- Create: `src/Tsumugi.Application/UseCases/Backup/RestoreDatabaseUseCase.cs`
- Test: `tests/Tsumugi.Application.Tests/Backup/RestoreDatabaseUseCaseTests.cs`
- Test: `tests/Tsumugi.Infrastructure.Tests/SqliteRestoreServiceTests.cs`

**Interfaces:**
- Consumes: Task 2 の `BackupFileName.CreatePreRestore`、Task 3 の `IDatabaseFileLocation`、Task 4 の `IBackupDirectory`、既存の `IBackupService` / `IAuditTrail` / `IUnitOfWork`。
- Produces: `IDatabaseRestoreService.RestoreFromAsync(string backupFilePath, CancellationToken) → Task`、`RestoreDatabaseUseCase.ExecuteAsync(string backupFilePath, string actor, CancellationToken) → Task`。Task 7 が使う。

> **順序が仕様である。** 監査 → 保存 → 退避 → 置換 の順に実行する。理由: 置換すると現行 DB は失われるため、監査エントリを先に書いて保存し、**その後に取る退避スナップショットへ監査エントリごと写す**。逆順だと「復元した事実」がどこにも残らない。

- [ ] **Step 1: 失敗するテストを書く（UseCase の順序）**

`tests/Tsumugi.Application.Tests/Backup/RestoreDatabaseUseCaseTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests.Backup;

public sealed class RestoreDatabaseUseCaseTests
{
    private readonly List<string> _trace = [];

    private sealed class FakeLocation : IDatabaseFileLocation
    {
        public string DatabasePath => "/data/tsumugi.db";
        public string BackupDirectory => "/data/backups";
    }

    private sealed class TracingBackupService(List<string> trace) : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            Destinations.Add(destinationPath);
            trace.Add("backup");
            return Task.CompletedTask;
        }
    }

    private sealed class TracingRestoreService(List<string> trace) : IDatabaseRestoreService
    {
        public string? Source { get; private set; }
        public Task RestoreFromAsync(string backupFilePath, CancellationToken ct)
        {
            Source = backupFilePath;
            trace.Add("restore");
            return Task.CompletedTask;
        }
    }

    private sealed class TracingAuditTrail(List<string> trace) : IAuditTrail
    {
        public List<(AuditAction Action, string? Summary)> Records { get; } = [];
        public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
            DateTimeOffset occurredAt, string? summary, CancellationToken ct)
        {
            Records.Add((action, summary));
            trace.Add("audit");
            return Task.CompletedTask;
        }
    }

    private sealed class TracingUnitOfWork(List<string> trace) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            trace.Add("save");
            return Task.FromResult(0);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Runs_audit_save_snapshot_then_replace_in_that_order()
    {
        var backup = new TracingBackupService(_trace);
        var restore = new TracingRestoreService(_trace);
        var uc = new RestoreDatabaseUseCase(
            new FakeLocation(), backup, restore,
            new TracingAuditTrail(_trace), new TracingUnitOfWork(_trace),
            new FixedTimeProvider(Now));

        await uc.ExecuteAsync("tsumugi-backup-20260810-100000.db", "tester", CancellationToken.None);

        // 監査を先に保存してから退避を取ることで、退避スナップショットに復元の記録が載る。
        _trace.Should().Equal(["audit", "save", "backup", "restore"]);

        // 引数はバックアップディレクトリ直下のファイル名。ユースケースが結合する。
        restore.Source.Should().Be(Path.Combine("/data/backups", "tsumugi-backup-20260810-100000.db"));
    }

    [Fact]
    public async Task Snapshots_the_current_database_with_the_pre_restore_prefix()
    {
        var backup = new TracingBackupService(_trace);
        var uc = new RestoreDatabaseUseCase(
            new FakeLocation(), backup, new TracingRestoreService(_trace),
            new TracingAuditTrail(_trace), new TracingUnitOfWork(_trace),
            new FixedTimeProvider(Now));

        await uc.ExecuteAsync("tsumugi-backup-20260810-100000.db", "tester", CancellationToken.None);

        backup.Destinations.Should().ContainSingle()
            .Which.Should().EndWith("pre-restore-20260816-173000.db");
    }

    [Fact]
    public async Task Records_the_restore_with_file_names_but_no_full_path()
    {
        var audit = new TracingAuditTrail(_trace);
        var uc = new RestoreDatabaseUseCase(
            new FakeLocation(), new TracingBackupService(_trace), new TracingRestoreService(_trace),
            audit, new TracingUnitOfWork(_trace), new FixedTimeProvider(Now));

        await uc.ExecuteAsync("tsumugi-backup-20260810-100000.db", "tester", CancellationToken.None);

        var record = audit.Records.Should().ContainSingle().Subject;
        record.Action.Should().Be(AuditAction.Restore);
        record.Summary.Should().Contain("tsumugi-backup-20260810-100000.db");
        record.Summary.Should().Contain("pre-restore-20260816-173000.db");
        record.Summary.Should().NotContain("/data");
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet build tests/Tsumugi.Application.Tests`

Expected: **コンパイルエラー**（`IDatabaseRestoreService` と `RestoreDatabaseUseCase` が存在しない）。

- [ ] **Step 3: 抽象とユースケースを実装する**

`src/Tsumugi.Application/Abstractions/IDatabaseRestoreService.cs`:

```csharp
namespace Tsumugi.Application.Abstractions;

/// <summary>
/// バックアップファイルで現行 DB を置き換える。接続プールの解放と WAL/SHM の後始末を含む。
/// 呼び出し後、アプリは再起動される前提（稼働中の DbContext は古い DB を指したままになる）。
/// </summary>
public interface IDatabaseRestoreService
{
    Task RestoreFromAsync(string backupFilePath, CancellationToken ct);
}
```

`src/Tsumugi.Application/UseCases/Backup/RestoreDatabaseUseCase.cs`:

```csharp
using System.Globalization;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>
/// バックアップからの復元。監査 → 保存 → 退避 → 置換 の順（spec 決定5）。
///
/// 順序が仕様である理由: 置換すると現行 DB は失われるため、監査エントリを先に保存し、
/// その後に取る退避スナップショットへ記録ごと写す。逆順だと復元の事実が残らない。
/// </summary>
public sealed class RestoreDatabaseUseCase(
    IDatabaseFileLocation location,
    IBackupService backupService,
    IDatabaseRestoreService restoreService,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string TargetType = "Database";

    /// <param name="backupFileName">
    /// バックアップディレクトリ**直下のファイル名**（パスではない）。呼び出し側に保存先を知らせないため。
    /// </param>
    public async Task ExecuteAsync(string backupFileName, string actor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (backupFileName != Path.GetFileName(backupFileName))
        {
            throw new ArgumentException(
                "バックアップディレクトリ直下のファイル名のみ指定できます。", nameof(backupFileName));
        }

        var now = clock.GetUtcNow();
        var snapshotName = Application.Backup.BackupFileName.CreatePreRestore(now);
        var sourceName = backupFileName;

        // 1. 監査（ファイル名のみ。フルパスは書かない。ハード制約4）
        var summary = string.Create(
            CultureInfo.InvariantCulture,
            $"復元 {sourceName} を適用（復元前退避 {snapshotName}）");
        await auditTrail.RecordAsync(
            actor, AuditAction.Restore, TargetType, Guid.Empty, now, summary, ct);

        // 2. 保存（ここまでが退避スナップショットに載る）
        await unitOfWork.SaveChangesAsync(ct);

        // 3. 退避（pre-restore 接頭辞は世代管理の対象外＝自動削除しない）
        await backupService.BackupToAsync(
            Path.Combine(location.BackupDirectory, snapshotName), ct);

        // 4. 置換
        await restoreService.RestoreFromAsync(
            Path.Combine(location.BackupDirectory, backupFileName), ct);
    }
}
```

- [ ] **Step 4: Infrastructure 側の I/O を実装する**

`src/Tsumugi.Infrastructure/Persistence/SqliteRestoreService.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>
/// DB ファイルの置換。接続プールを解放してから差し替え、WAL/SHM サイドカーを削除する。
/// 古い WAL が新しい DB に適用されると破損するため、サイドカーの削除は必須。
/// </summary>
public sealed class SqliteRestoreService(ISqliteLocation location) : IDatabaseRestoreService
{
    public Task RestoreFromAsync(string backupFilePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("復元元のバックアップファイルが見つかりません。");
        }

        ct.ThrowIfCancellationRequested();

        // Microsoft.Data.Sqlite は接続をプールするため、解放しないとファイルを掴んだままになる。
        SqliteConnection.ClearAllPools();

        File.Copy(backupFilePath, location.DatabasePath, overwrite: true);

        foreach (var sidecar in new[] { location.DatabasePath + "-wal", location.DatabasePath + "-shm" })
        {
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }

        SecureFileSystem.EnsureFile(location.DatabasePath);
        return Task.CompletedTask;
    }
}
```

> `FileNotFoundException` のメッセージにパスを入れないこと（ハード制約4）。

`src/Tsumugi.Infrastructure/Persistence/BackupDirectoryService.cs`:

```csharp
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>バックアップディレクトリ直下の列挙と削除。</summary>
public sealed class BackupDirectoryService(ISqliteLocation location) : IBackupDirectory
{
    public IReadOnlyList<string> ListFileNames()
    {
        if (!Directory.Exists(location.BackupDirectory)) return [];
        return Directory.EnumerateFiles(location.BackupDirectory)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToArray();
    }

    public void Delete(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        // ディレクトリ直下に限定する（パス区切りを含む入力を拒否）。
        if (fileName != Path.GetFileName(fileName))
        {
            throw new ArgumentException("バックアップディレクトリ直下のファイル名のみ指定できます。", nameof(fileName));
        }

        var path = Path.Combine(location.BackupDirectory, fileName);
        if (File.Exists(path)) File.Delete(path);
    }
}
```

- [ ] **Step 5: 復元の往復テストを書いて通す**

`tests/Tsumugi.Infrastructure.Tests/SqliteRestoreServiceTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.Infrastructure.Tests;

public sealed class SqliteRestoreServiceTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-restore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Restore_brings_back_the_content_of_the_backup()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(location.ConnectionString).Options;

        var backupPath = Path.Combine(location.BackupDirectory, "snapshot.db");

        // 1. スキーマを作り、バックアップを取る
        using (var db = new TsumugiDbContext(options))
        {
            db.Database.EnsureCreated();
            await new SqliteBackupService(db).BackupToAsync(backupPath, CancellationToken.None);
        }
        SqliteConnection.ClearAllPools();

        // 2. 現行 DB を壊す（中身を潰す）
        await File.WriteAllTextAsync(location.DatabasePath, "壊れたDB", CancellationToken.None);

        // 3. 復元する
        await new SqliteRestoreService(location).RestoreFromAsync(backupPath, CancellationToken.None);

        // 4. 復元後の DB が SQLite として開けること
        using var restored = new TsumugiDbContext(options);
        var act = () => restored.Database.CanConnect();
        act.Should().NotThrow();
        restored.Database.CanConnect().Should().BeTrue();
    }

    [Fact]
    public async Task Restore_deletes_stale_wal_and_shm_sidecars()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        var options = new DbContextOptionsBuilder<TsumugiDbContext>()
            .UseSqlite(location.ConnectionString).Options;
        var backupPath = Path.Combine(location.BackupDirectory, "snapshot.db");

        using (var db = new TsumugiDbContext(options))
        {
            db.Database.EnsureCreated();
            await new SqliteBackupService(db).BackupToAsync(backupPath, CancellationToken.None);
        }
        SqliteConnection.ClearAllPools();

        await File.WriteAllTextAsync(location.DatabasePath + "-wal", "stale", CancellationToken.None);
        await File.WriteAllTextAsync(location.DatabasePath + "-shm", "stale", CancellationToken.None);

        await new SqliteRestoreService(location).RestoreFromAsync(backupPath, CancellationToken.None);

        File.Exists(location.DatabasePath + "-wal").Should().BeFalse();
        File.Exists(location.DatabasePath + "-shm").Should().BeFalse();
    }

    [Fact]
    public async Task Restore_throws_when_the_source_is_missing_and_the_message_has_no_path()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();
        var sut = new SqliteRestoreService(location);

        var act = async () => await sut.RestoreFromAsync(
            Path.Combine(_root, "does-not-exist.db"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<FileNotFoundException>();
        ex.Which.Message.Should().NotContain(_root);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 6: テストが通ることを確認してコミットする**

Run: `dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~RestoreDatabaseUseCaseTests"` と
`dotnet test tests/Tsumugi.Infrastructure.Tests --filter "FullyQualifiedName~SqliteRestoreServiceTests"`

Expected: 両方とも全緑（Application 3件 / Infrastructure 3件）。

```bash
dotnet build
git add src/Tsumugi.Application/Abstractions/IDatabaseRestoreService.cs \
        src/Tsumugi.Application/UseCases/Backup/RestoreDatabaseUseCase.cs \
        src/Tsumugi.Infrastructure/Persistence/SqliteRestoreService.cs \
        src/Tsumugi.Infrastructure/Persistence/BackupDirectoryService.cs \
        tests/Tsumugi.Application.Tests/Backup/RestoreDatabaseUseCaseTests.cs \
        tests/Tsumugi.Infrastructure.Tests/SqliteRestoreServiceTests.cs
git commit -m "feat(phase4-s3a): 復元（退避・接続プール解放・WAL削除）を追加する"
```

---

## Task 6: 終了時フックと DI 配線

**Files:**
- Modify: `src/Tsumugi.App/App.axaml.cs`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`
- Modify: `src/Tsumugi.Infrastructure/DependencyInjection.cs`
- Test: `tests/Tsumugi.App.Tests/BackupWiringTests.cs`（新規）

**Interfaces:**
- Consumes: Task 4 の `RunScheduledBackupUseCase`、Task 5 の `SqliteRestoreService` / `BackupDirectoryService`。
- Produces: 無し（配線）。

> **`ShutdownRequested` は同期イベントである。** ここで `.Result` / `.GetAwaiter().GetResult()` を使うと UI スレッドでデッドロックしうる。**一度だけ終了をキャンセルし、非同期でバックアップしてから明示的に `Shutdown()` を呼び直す**パターンを使う。

- [ ] **Step 1: 保存先を知る合成ルートの経路を作る**

**現状（着手前に確認済みの事実）**: `ISqliteLocation` は **DI に登録されていない**。`App.axaml.cs` が `SqliteLocationService` をローカルに生成し、`CompositionRoot.Build(location.ConnectionString)` へ**接続文字列だけ**渡している。そのため `SqliteRestoreService` / `BackupDirectoryService`（どちらも `ISqliteLocation` を要求する）は現状のままでは解決できない。

既存の `Build(string connectionString)` / `AddTsumugiServices(services, connectionString)` は**シグネチャも振る舞いも変えない**（既存の App.Tests がこれを使っている）。**保存先を知る版のオーバーロードを足し**、バックアップ・復元の登録はそちらだけで行う。

`src/Tsumugi.App/CompositionRoot.cs` へ追加する。

```csharp
    /// <summary>
    /// 保存先を知る合成ルート。バックアップ・復元は DB ファイルの実体パスを要するため、
    /// 接続文字列だけの版では登録しない（接続文字列から保存先を推測しない）。
    /// </summary>
    public static IServiceProvider Build(SqliteLocationService location)
        => new ServiceCollection().AddTsumugiServices(location).BuildServiceProvider();

    public static IServiceCollection AddTsumugiServices(
        this IServiceCollection services, SqliteLocationService location)
    {
        ArgumentNullException.ThrowIfNull(location);

        services.AddTsumugiServices(location.ConnectionString);

        services.AddSingleton<ISqliteLocation>(location);
        services.AddSingleton<IDatabaseFileLocation>(location);
        services.AddScoped<IDatabaseRestoreService, SqliteRestoreService>();
        services.AddScoped<IBackupDirectory, BackupDirectoryService>();
        services.AddScoped<RunScheduledBackupUseCase>();
        services.AddScoped<RestoreDatabaseUseCase>();
        services.AddScoped<ListBackupGenerationsUseCase>();
        services.AddScoped<ExportBackupCopyUseCase>();   // Task 7 で作る
        services.AddTransient<BackupViewModel>();        // Task 7 で作る

        return services;
    }
```

> `ExportBackupCopyUseCase` と `BackupViewModel` はまだ存在しない。**この2行は Task 7 で足すこと**。Task 6 では残り7行だけを入れる。

`using Tsumugi.Infrastructure.Persistence;` と `using Tsumugi.Application.UseCases.Backup;` を追加する。

`src/Tsumugi.App/App.axaml.cs` の `_services = CompositionRoot.Build(location.ConnectionString);` を次へ変える。

```csharp
        _services = CompositionRoot.Build(location);
```

- [ ] **Step 2: 配線テストを書く**

`tests/Tsumugi.App.Tests/BackupWiringTests.cs`:

```csharp
using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class BackupWiringTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "tsumugi-wiring-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Backup_and_restore_services_resolve_from_the_location_aware_composition_root()
    {
        var location = new SqliteLocationService(_root);
        location.EnsureSecuredStorage();

        var services = CompositionRoot.Build(location);
        using var scope = services.CreateScope();

        scope.ServiceProvider.GetRequiredService<RunScheduledBackupUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<RestoreDatabaseUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ListBackupGenerationsUseCase>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IBackupDirectory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDatabaseRestoreService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IDatabaseFileLocation>()
            .BackupDirectory.Should().Be(Path.Combine(_root, "backups"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
```

- [ ] **Step 3: 終了時フックを実装する**

`src/Tsumugi.App/App.axaml.cs` の `if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)` ブロックを次に差し替える。

```csharp
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _appScope.ServiceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow(mainVm);
            desktop.ShutdownRequested += OnShutdownRequested;
        }
```

同クラスへ次を追加する。

```csharp
    private bool _shutdownBackupDone;

    /// <summary>
    /// 終了時の自動バックアップ（spec 決定2）。ShutdownRequested は同期イベントなので、
    /// 一度だけ終了をキャンセルして非同期にバックアップし、完了後に改めて終了する。
    /// ここで同期待ちすると UI スレッドでデッドロックしうる。
    /// </summary>
    private async void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_shutdownBackupDone) return;

        e.Cancel = true;
        _shutdownBackupDone = true;

        try
        {
            var useCase = _appScope!.ServiceProvider
                .GetRequiredService<RunScheduledBackupUseCase>();
            await useCase.ExecuteAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // バックアップの失敗で終了を妨げない。パスを含む情報は残さない（ハード制約4）。
        }

        _appScope?.Dispose();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
```

`using System.Threading;`、`using Tsumugi.Application.UseCases.Backup;` を追加すること。**既存の `desktop.ShutdownRequested += (_, _) => _appScope?.Dispose();` は削除する**（`OnShutdownRequested` が dispose を引き受ける）。

- [ ] **Step 4: 例外を握り潰す箇所についてのコメントを残す**

上記 `catch (Exception)` は**意図的な握り潰し**である。理由をコード内コメントに書いたうえで、`docs/superpowers/plans` ではなく **ADR（Task 8）の「影響」節にも記載する**。「終了できないアプリ」は「バックアップされないアプリ」より悪いという判断であることを明示する。

- [ ] **Step 5: テストとビルドを確認してコミットする**

Run: `dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~BackupWiringTests"`

Expected: 緑。

> **headless では終了フックそのものを検証できない。** 実際の `ShutdownRequested` 経路は手動 QA でしか確認できないため、Task 8 で「本番投入前に必須の deferred」へ項目を追加する。

```bash
dotnet build
git add src/Tsumugi.App/App.axaml.cs src/Tsumugi.App/CompositionRoot.cs \
        src/Tsumugi.Infrastructure/DependencyInjection.cs \
        tests/Tsumugi.App.Tests/BackupWiringTests.cs
git commit -m "feat(phase4-s3a): 終了時の自動バックアップを配線する"
```

---

## Task 7: バックアップ画面

**Files:**
- Create: `src/Tsumugi.Application/UseCases/Backup/ExportBackupCopyUseCase.cs`
- Create: `src/Tsumugi.App/ViewModels/BackupViewModel.cs`
- Create: `src/Tsumugi.App/Views/BackupView.axaml` ＋ `BackupView.axaml.cs`
- Modify: `src/Tsumugi.App/ViewModels/MainViewModel.cs`
- Modify: `src/Tsumugi.App/MainWindow.axaml`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`
- Test: `tests/Tsumugi.App.Tests/ViewModels/BackupViewModelTests.cs`

**Interfaces:**
- Consumes: Task 4 の `RunScheduledBackupUseCase` / `ListBackupGenerationsUseCase`、Task 5 の `RestoreDatabaseUseCase`、既存の `IFileSaveService`。
- Produces: `BackupViewModel`（`MainViewModel` が公開プロパティとして保持）。

UI の3操作（spec §4.1）:

| 操作 | 実装 |
|---|---|
| 今すぐバックアップ | `RunScheduledBackupUseCase.ExecuteAsync` をそのまま呼ぶ（終了時と同じ経路）。完了後に世代一覧を再読込 |
| 控えを保存 | `IFileSaveService.SaveAsync` で利用者が選んだ場所へ書き出す |
| 復元 | 世代一覧から選び、確認のうえ `RestoreDatabaseUseCase.ExecuteAsync`。完了後に「再起動が必要」を表示する |

- [ ] **Step 1: ViewModel のテストを書く**

`tests/Tsumugi.App.Tests/ViewModels/BackupViewModelTests.cs` を作る。ユースケースは `sealed class` なのでインターフェース経由でモックできない。**`IBackupDirectory` / `IBackupService` / `IDatabaseRestoreService` / `IAuditTrail` / `IUnitOfWork` の fake から実物のユースケースを組み立てる**（Task 4・5 のテストで書いた fake と同じ形）。

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.App.Services;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.UseCases.Backup;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests.ViewModels;

public sealed class BackupViewModelTests
{
    private sealed class FakeLocation : IDatabaseFileLocation
    {
        public string DatabasePath => Path.Combine("/data", "tsumugi.db");
        public string BackupDirectory => Path.Combine("/data", "backups");
    }

    private sealed class FakeBackupService : IBackupService
    {
        public List<string> Destinations { get; } = [];
        public byte[] Payload { get; set; } = [1, 2, 3];
        public Task BackupToAsync(string destinationPath, CancellationToken ct)
        {
            Destinations.Add(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            return File.WriteAllBytesAsync(destinationPath, Payload, ct);
        }
    }

    private sealed class FakeBackupDirectory : IBackupDirectory
    {
        public List<string> Files { get; } = [];
        public List<string> Deleted { get; } = [];
        public IReadOnlyList<string> ListFileNames() => Files.ToArray();
        public void Delete(string fileName) { Deleted.Add(fileName); Files.Remove(fileName); }
    }

    private sealed class FakeRestoreService : IDatabaseRestoreService
    {
        public string? Source { get; private set; }
        public Exception? Throws { get; set; }
        public Task RestoreFromAsync(string backupFilePath, CancellationToken ct)
        {
            if (Throws is not null) throw Throws;
            Source = backupFilePath;
            return Task.CompletedTask;
        }
    }

    private sealed class NullAuditTrail : IAuditTrail
    {
        public Task RecordAsync(string actor, AuditAction action, string targetType, Guid targetId,
            DateTimeOffset occurredAt, string? summary, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NullUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakeFileSave : IFileSaveService
    {
        public byte[]? Saved { get; private set; }
        public string? SuggestedFileName { get; private set; }
        public bool Result { get; set; } = true;
        public Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName,
            string extension, CancellationToken ct = default)
        {
            Saved = bytes;
            SuggestedFileName = suggestedFileName;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 16, 17, 30, 0, TimeSpan.Zero);

    private sealed record Harness(
        BackupViewModel ViewModel,
        FakeBackupDirectory Directory,
        FakeRestoreService Restore,
        FakeFileSave FileSave);

    private static Harness Build(string tempRoot)
    {
        var location = new TempLocation(tempRoot);
        var backup = new FakeBackupService();
        var dir = new FakeBackupDirectory();
        var restore = new FakeRestoreService();
        var fileSave = new FakeFileSave();
        var clock = new FixedTimeProvider(Now);

        var run = new RunScheduledBackupUseCase(
            location, backup, dir, new NullAuditTrail(), new NullUnitOfWork(), clock);
        var list = new ListBackupGenerationsUseCase(dir);
        var restoreUc = new RestoreDatabaseUseCase(
            location, backup, restore, new NullAuditTrail(), new NullUnitOfWork(), clock);
        var export = new ExportBackupCopyUseCase(location, backup, clock);

        return new Harness(
            new BackupViewModel(run, list, restoreUc, export, fileSave), dir, restore, fileSave);
    }

    private sealed class TempLocation(string root) : IDatabaseFileLocation
    {
        public string DatabasePath => Path.Combine(root, "tsumugi.db");
        public string BackupDirectory => Path.Combine(root, "backups");
    }

    private static string NewTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "tsumugi-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "backups"));
        return root;
    }

    [Fact]
    public async Task LoadAsync_lists_generations_newest_first()
    {
        var root = NewTempRoot();
        var h = Build(root);
        h.Directory.Files.AddRange(
        [
            "tsumugi-backup-20260810-100000.db",
            "tsumugi-backup-20260816-100000.db",
        ]);

        await h.ViewModel.LoadAsync();

        h.ViewModel.Generations.First().Should().Be("tsumugi-backup-20260816-100000.db");
    }

    [Fact]
    public async Task RestoreAsync_does_nothing_when_no_generation_is_selected()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = null;

        await h.ViewModel.RestoreAsync();

        h.Restore.Source.Should().BeNull();
        h.ViewModel.RestartRequired.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreAsync_sets_restart_required_after_a_successful_restore()
    {
        var h = Build(NewTempRoot());
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();

        h.ViewModel.RestartRequired.Should().BeTrue();
        h.ViewModel.StatusMessage.Should().Contain("再起動");
        h.ViewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task RestoreAsync_surfaces_a_failure_without_claiming_success()
    {
        var h = Build(NewTempRoot());
        h.Restore.Throws = new IOException("置換に失敗");
        h.ViewModel.SelectedGeneration = "tsumugi-backup-20260810-100000.db";

        await h.ViewModel.RestoreAsync();

        h.ViewModel.RestartRequired.Should().BeFalse();
        h.ViewModel.ErrorMessage.Should().Contain("復元に失敗");
    }

    [Fact]
    public async Task SaveCopyAsync_hands_the_backup_bytes_to_the_file_save_service()
    {
        var h = Build(NewTempRoot());

        await h.ViewModel.SaveCopyAsync();

        h.FileSave.Saved.Should().NotBeNull().And.NotBeEmpty();
        h.FileSave.SuggestedFileName.Should().Be("tsumugi-backup-20260816-173000.db");
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run: `dotnet build tests/Tsumugi.App.Tests`

Expected: コンパイルエラー（`BackupViewModel` が存在しない）。

- [ ] **Step 3a: `ExportBackupCopyUseCase` を実装する**

`src/Tsumugi.Application/UseCases/Backup/ExportBackupCopyUseCase.cs`:

```csharp
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Backup;

/// <summary>
/// 「控えを保存」用に、バックアップの中身をバイト列で返す。
///
/// 一時ファイルを**保護ディレクトリ内**に作ってから読み出し、直後に削除する。
/// システムの一時ディレクトリを使わないのは、そこが 0700 で保護されておらず、
/// 個人情報を含む DB の平文コピーを共有領域へ置くことになるため。
/// </summary>
public sealed class ExportBackupCopyUseCase(
    IDatabaseFileLocation location,
    IBackupService backupService,
    TimeProvider clock)
{
    public async Task<(string SuggestedFileName, byte[] Content)> ExecuteAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var fileName = Application.Backup.BackupFileName.Create(now);
        var staging = Path.Combine(location.BackupDirectory, fileName + ".export");

        try
        {
            await backupService.BackupToAsync(staging, ct);
            var content = await File.ReadAllBytesAsync(staging, ct);
            return (fileName, content);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }
}
```

> バイト列を丸ごとメモリに載せる。B型事業所の DB 規模では問題ないが、巨大化した場合はストリーミングへの作り替えが要る。この限界を ADR 0052 の「残る限界」に書く。
> `.export` 拡張子にすることで、`BackupGenerationPolicy` の命名規則（`.db` 終わり）に合致せず**世代管理に巻き込まれない**。

- [ ] **Step 3b: `BackupViewModel` を実装する**

`CommunityToolkit.Mvvm` のソースジェネレータを使い、既存 ViewModel と同じ形（`sealed partial class ... : ViewModelBase`、`[ObservableProperty]`、`[RelayCommand]`）で書く。

```csharp
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.App.Services;
using Tsumugi.Application.UseCases.Backup;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// バックアップと復元の画面。設定は持たない（spec 決定3）。
/// 保存先と保持世代は固定で、利用者が変更できるのは「控えの保存先」だけ。
/// </summary>
public sealed partial class BackupViewModel(
    RunScheduledBackupUseCase runBackup,
    ListBackupGenerationsUseCase listGenerations,
    RestoreDatabaseUseCase restore,
    ExportBackupCopyUseCase exportCopy,
    IFileSaveService fileSave) : ViewModelBase
{
    [ObservableProperty] private string? _selectedGeneration;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _restartRequired;

    public ObservableCollection<string> Generations { get; } = new();

    [RelayCommand]
    public Task LoadAsync()
    {
        Generations.Clear();
        foreach (var name in listGenerations.Execute()) Generations.Add(name);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task BackupNowAsync()
    {
        ErrorMessage = null;
        try
        {
            await runBackup.ExecuteAsync(CancellationToken.None);
            await LoadAsync();
            StatusMessage = "バックアップを作成しました。";
        }
        catch (Exception ex)
        {
            // 例外メッセージにパスが載らないことは各サービス側で保証している（ハード制約4）。
            ErrorMessage = "バックアップに失敗しました: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task SaveCopyAsync()
    {
        ErrorMessage = null;
        try
        {
            var (suggestedFileName, content) = await exportCopy.ExecuteAsync(CancellationToken.None);
            var saved = await fileSave.SaveAsync(
                content, suggestedFileName, "SQLite データベース", ".db", CancellationToken.None);
            StatusMessage = saved ? "控えを保存しました。" : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "控えの保存に失敗しました: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task RestoreAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(SelectedGeneration)) return;

        try
        {
            // 引数はバックアップディレクトリ直下のファイル名。VM は保存先を知らない。
            await restore.ExecuteAsync(SelectedGeneration, actor: "operator", CancellationToken.None);
            RestartRequired = true;
            StatusMessage = "復元しました。反映するにはアプリを再起動してください。";
        }
        catch (Exception ex)
        {
            ErrorMessage = "復元に失敗しました: " + ex.Message;
        }
    }
}
```

> 「控えを保存」で外部媒体へ出すファイルは**平文**である（暗号化は S3b）。この事実を ADR 0052 の「残る限界」と CHANGELOG に書く。

- [ ] **Step 4: View とタブを足す**

`src/Tsumugi.App/Views/BackupView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tsumugi.App.ViewModels"
             x:Class="Tsumugi.App.Views.BackupView"
             x:DataType="vm:BackupViewModel">
    <ScrollViewer>
        <StackPanel Margin="8" Spacing="8">
            <TextBlock Text="バックアップと復元"
                       FontSize="{DynamicResource HeadingFontSize}" />

            <TextBlock TextWrapping="Wrap"
                       Text="アプリの終了時に自動でバックアップを作成します。同じ日のものは最新の1件だけを残し、直近7日分を保持します。" />

            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Content="今すぐバックアップ(_B)" Command="{Binding BackupNowCommand}" />
                <Button Content="控えを保存(_S)" Command="{Binding SaveCopyCommand}" />
            </StackPanel>

            <TextBlock Text="世代一覧" FontWeight="Bold" />
            <ListBox ItemsSource="{Binding Generations}"
                     SelectedItem="{Binding SelectedGeneration}"
                     Height="220" />

            <Button Content="選択した世代へ復元(_R)" Command="{Binding RestoreCommand}" />

            <Border IsVisible="{Binding RestartRequired}"
                    Padding="8" CornerRadius="4"
                    Background="{DynamicResource SystemAccentColorLight2}">
                <TextBlock TextWrapping="Wrap"
                           Text="復元を反映するにはアプリを再起動してください。" />
            </Border>

            <TextBlock Text="{Binding StatusMessage}"
                       IsVisible="{Binding StatusMessage, Converter={x:Static ObjectConverters.IsNotNull}}"
                       TextWrapping="Wrap" />
            <TextBlock Text="{Binding ErrorMessage}"
                       IsVisible="{Binding ErrorMessage, Converter={x:Static ObjectConverters.IsNotNull}}"
                       Foreground="{DynamicResource SystemErrorTextColor}"
                       TextWrapping="Wrap" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

`src/Tsumugi.App/Views/BackupView.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Interactivity;
using Tsumugi.App.ViewModels;

namespace Tsumugi.App.Views;

public partial class BackupView : UserControl
{
    public BackupView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // 実画面で世代一覧を表示するため、Loaded で VM の読み込みを発火させる。
    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BackupViewModel vm) await vm.LoadAsync();
    }
}
```

> `SystemAccentColorLight2` / `SystemErrorTextColor` などの `DynamicResource` キーが現行テーマに存在するかは、**着手時に既存 View が使っているキーを確認して合わせること**（存在しないキーはランタイムで無視され、見た目が崩れる）。存在しなければ既存 View が使っている同等のキーへ置き換える。

`MainWindow.axaml` の最後の `TabItem`（請求確定(_H)）の後ろへ追加する。

```xml
        <TabItem Header="バックアップ(_I)">
            <views:BackupView DataContext="{Binding Backup}" />
        </TabItem>
```

`MainViewModel` のコンストラクタ引数に `BackupViewModel backup` を足し、`public BackupViewModel Backup { get; }` を公開する（既存の並びに合わせる）。`CompositionRoot` に `services.AddTransient<BackupViewModel>();` を足す。

- [ ] **Step 5: テストとビルドを確認してコミットする**

Run: `dotnet test tests/Tsumugi.App.Tests`

Expected: 全緑（既存の `MainViewModel` 構築テストがあれば、引数追加で落ちないことも確認する）。

```bash
dotnet build
git add src/Tsumugi.Application/UseCases/Backup/ExportBackupCopyUseCase.cs \
        src/Tsumugi.App/ViewModels/BackupViewModel.cs src/Tsumugi.App/Views/BackupView.axaml* \
        src/Tsumugi.App/ViewModels/MainViewModel.cs src/Tsumugi.App/MainWindow.axaml \
        src/Tsumugi.App/CompositionRoot.cs \
        tests/Tsumugi.App.Tests/ViewModels/BackupViewModelTests.cs
git commit -m "feat(phase4-s3a): バックアップ・復元の画面を追加する"
```

---

## Task 8: ADR・open-questions・CHANGELOG・ロードマップ

**Files:**
- Modify: `docs/decisions/0003-sqlite-location-and-encryption.md`
- Create: `docs/decisions/0052-backup-operations.md`
- Modify: `docs/open-questions.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md`

- [ ] **Step 1: ADR 0003 を改訂して暗号化を確定する**

既存の本文は残し、**追補として**「暗号化採否の確定（2026-08-16 / AC4-3）」節を足す。含める内容（spec §3 決定1 の全要素）:

- 結論: **(a) 平文＋OS ファイル権限 ＋ (c) OS ディスク暗号化への委譲**で確定。SQLCipher は採らない。
- 判断の根拠となった運用実測: **1台を複数職員で共有し OS アカウントも共通／鍵の紛失は許容できない**（2026-08-16 に利用者へ確認）。
- 構造的な理由: 共有 OS アカウント運用では DB は1つ・鍵も1つなので、**DB 暗号化は職員間の分離を提供しない**。守れるのは端末外へ出たファイルだけで、盗難は FileVault / BitLocker が担当する。
- 鍵紛失が許容できないためパスフレーズ方式は選べない（SQLCipher に復旧手段は無い）。
- 一次情報（URL を明記）: SQLCipher の設計（PBKDF2-HMAC-SHA512・既定 256,000 反復・鍵はアプリが供給）、**公式 .NET パッケージは Commercial Edition 専用で `PRAGMA cipher_license` が必要**、公開 `bundle_e_sqlcipher` はベンダー非サポートのサードパーティ製、Community Edition は BSD 相当でライセンス全文の掲示義務あり。
- **運用要件**: FileVault（macOS）／BitLocker（Windows）の有効化。S5 の運用ガイドへ引き渡す。
- **再評価トリガ3件**: 職員ごとに OS アカウントを分ける運用になったとき／端末を事業所外へ持ち出す運用が発生したとき／鍵の預け先が用意でき紛失が復旧可能になったとき。
- **NOTICE への追記は不要**（SQLCipher を採らないため掲示義務が発生しない）ことを明記する。

- [ ] **Step 2: ADR 0052 を書く**

`docs/decisions/0052-backup-operations.md`。既存 ADR（`0050-bulk-operations-forbidden.md`）の構成（結論 → 背景 → 決定 → 選択肢 → 影響 → テスト）に合わせる。含める内容:

- **結論**: 自動（終了時・固定保存先・世代管理・平文）と手動（利用者が選ぶ保存先）の2系統。設定は持たない。
- **決定1（設定を持たない）**: `AppSettings` はリポジトリに存在せず設定の永続化機構も無い。復元は DB が壊れたときの機能なので、設定を DB に置くと壊れた DB から設定を読むことになる。**ロードマップ §8.3 の前提（`AppSettings` に項目を足す）を破棄したこと**を明記する。
- **決定2（保持規則）**: 同日は最新1つ・直近7日分。「最新 N ファイル」にしない理由（1日に何度も起動終了すると1週間分が1日分に潰れる）。
- **決定3（`VACUUM INTO` の一時名→移動）**: 一次資料の引用付き（宛先が既存だと失敗する）。
- **決定4（復元の順序）**: 監査 → 保存 → 退避 → 置換。**復元の記録は退避スナップショットに残る**（現行 DB は定義上置き換わるため）ことを明記する。
- **決定5（`pre-restore-` は世代管理の対象外）**: 誤った復元からの最後の綱。自動削除しない代わりに溜まるので、削除は利用者の手に委ねる。
- **決定6（復元後は再起動）**: 稼働中の `DbContext` の下でファイルを差し替えない。
- **決定7（外部媒体では権限適用の失敗を許容）**: FAT32/exFAT を想定。「安全のための操作」が安全機構のせいで失敗するのを避ける。
- **影響 — 残る限界**:
  - 終了時フックの `catch (Exception)` は**意図的な握り潰し**である。「終了できないアプリ」は「バックアップされないアプリ」より悪いという判断。失敗は画面に出ない。
  - **`ShutdownRequested` 経路は headless テストで検証できない**。手動 QA 項目。
  - 起動中の定期バックアップは無い（終了せずに使い続けるとバックアップが増えない）。
  - **「控えを保存」で外部媒体へ出るファイルは平文である**（暗号化は S3b）。決定1 の分析における「唯一の実質的な穴」は S3a の時点では開いたままである。
  - `ExportBackupCopyUseCase` は**バックアップ全体をメモリに載せる**。B型事業所の規模では問題ないが、巨大化した場合はストリーミングへの作り替えが要る。
  - **Windows 実機での確認ができない**（利用可能な実機は macOS のみ）。DACL 適用と `ClearAllPools` 後のファイル置換は自動テストとレビューで担保する。

- [ ] **Step 3: open-questions の暗号化項をクローズする**

`docs/open-questions.md` の SQLite 暗号化採否の項目を `- [x]` にし、クローズ日（2026-08-16）と ADR 0003 追補への参照、確定内容の1行要約を追記する。**行番号ではなく本文で該当項目を特定すること。**

- [ ] **Step 4: CHANGELOG を更新する**

`## Phase 4 S3a 完了 (2026-08-16)` 節を `## [Unreleased]` の直後へ挿入する。内容:

- 暗号化採否の確定（ADR 0003 追補）と、その根拠（共有アカウント運用では DB 暗号化が職員間分離を提供しない／鍵紛失が許容できない）
- 終了時自動バックアップ・世代管理（同日最新1つ×7日）・復元・保存先権限強制（ADR 0052）
- 権限適用ロジックを `SecureFileSystem` へ抽出し、DB とバックアップの双方へ同じポリシーを適用
- `VACUUM INTO` の既存ファイル衝突を一時名→移動で回避（一次資料の制約）
- AC4-3／AC4-4 達成

「本番投入前に必須の deferred」から **「SQLite 暗号化方針の決定」と「バックアップ自動化」を削除**し、代わりに次を**追加**する:
- **FileVault（macOS）／BitLocker（Windows）の有効化が運用要件**になったこと（ADR 0003 追補）
- **終了時フックと復元の実機確認が未実施**であること（headless で検証できない。Windows 実機は無い）

- [ ] **Step 5: ロードマップを更新する**

`docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md`:

- §2 スライス表の **S3 行**を S1/S2 と同じ体裁で完了へ（ADR 欄は 0003 改・0052。**版タグは打っていないのでタグ名を書かない**）。ただし **S3b（暗号化バックアップ）が未着手**であることが分かる書き方にする。
- §5 AC マッピングの **AC4-3・AC4-4 行**を「✅ 完了」へ。
- §3 着手順の残りを S4 → S5 へ。
- §4 依存関係の「S3 → S5 運用ガイド」「S3 暗号化 ADR → S5 §5」が解消したことを反映する。

- [ ] **Step 6: 品質ゲートを通してコミットする**

Run: `./build/ci.sh`

Expected: 全緑。

```bash
git add docs/decisions/0003-sqlite-location-and-encryption.md \
        docs/decisions/0052-backup-operations.md \
        docs/open-questions.md CHANGELOG.md \
        docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md
git commit -m "docs(phase4-s3a): ADR 0003 改訂・ADR 0052・open-questions・CHANGELOGを同期する"
```

---

## 完了条件

- [ ] `SecureFileSystem` が抽出され、`SqliteLocationServiceTests` が**無変更のまま**緑
- [ ] `BackupGenerationPolicy` が同日最新1つ×7日を満たし、`pre-restore-` と規則外ファイルを触らない
- [ ] 既存ファイルがある宛先へバックアップしてもエラーにならず、一時ファイルが残らない
- [ ] `backups/` ディレクトリと各バックアップファイルに 0700 / 0600（Windows は現在ユーザーのみ DACL）が適用される
- [ ] 復元が「監査 → 保存 → 退避 → 置換」の順で走り、WAL/SHM が削除される
- [ ] 監査エントリにフルパスが含まれない
- [ ] 終了時フックが配線され、`BackupViewModel` の3操作（今すぐバックアップ／控えを保存／復元）が動く
- [ ] 「控えを保存」の一時ファイルが保護ディレクトリ内に作られ、読み出し後に削除される（システム一時ディレクトリへ平文コピーを置かない）
- [ ] ADR 0003 が暗号化採否を確定し、再評価トリガ3件を持つ
- [ ] ADR 0052 が「残る限界」（握り潰し・headless 検証不能・Windows 実機無し・起動中の定期バックアップ無し）を持つ
- [ ] `./build/ci.sh` が緑

## スコープ外

- 暗号化バックアップ（S3b。別計画）
- 設定の永続化機構
- DB 本体の暗号化
- 起動中の定期バックアップ、外部媒体への自動同期
- 運用ガイド本体（S5）
