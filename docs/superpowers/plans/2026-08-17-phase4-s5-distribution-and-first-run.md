# Phase 4 S5 配布・初回セットアップ・運用ガイド・手動 QA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** macOS／Windows 向け発行手段、事業所未登録時の初回セットアップウィザード、運用ガイド、macOS 実機 smoke 記録を整備し、オフライン配布を可能にする。

**Architecture:** 初回判定は Domain の副作用なし Policy に閉じ、Application の薄い `RegisterFirstRunUseCase` は既存 `RegisterOfficeUseCase` へ委譲する。App は testable な startup coordinator で起動先を決め、専用 Wizard の成功時だけ MainWindow へ置き換える。発行スクリプトは RID ごとに self-contained・単一ファイル・trim 無効を固定し、静的契約テストで退行を防ぐ。

**Tech Stack:** .NET 10 / C# 14、Avalonia 11、CommunityToolkit.Mvvm、EF Core 10 / SQLite、xUnit、FluentAssertions、Bash、PowerShell。

**Spec:** `docs/superpowers/specs/2026-08-17-phase4-s5-distribution-and-first-run-design.md`

---

## ファイル構成

| 区分 | ファイル | 責務 |
|---|---|---|
| Domain | `src/Tsumugi.Domain/Logic/FirstRunPolicy.cs` | `Office` 件数だけから初回起動を決定する純粋関数 |
| Application | `src/Tsumugi.Application/Dtos/RegisterFirstRunInput.cs` | 初回登録の入力契約 |
| Application | `src/Tsumugi.Application/UseCases/Office/RegisterFirstRunUseCase.cs` | `RegionGrade.None` を拒否して既存事業所登録へ委譲 |
| App | `src/Tsumugi.App/Startup/FirstRunStartupCoordinator.cs` | migration 後に一覧件数を問い合わせ、初回／通常の起動先を返す |
| App | `src/Tsumugi.App/Startup/FirstRunDesktopStartupOrchestrator.cs` | 判定結果に従って Main／Wizard／終了を選び、非同期起動エラーを安全に終端する |
| App | `src/Tsumugi.App/Startup/IInitialWindowHost.cs` | Avalonia Window 操作を隔離し、起動分岐を headless テスト可能にする |
| App | `src/Tsumugi.App/Startup/AvaloniaInitialWindowHost.cs` | `desktop.MainWindow` の置換順序と Wizard のイベント接続を実装する |
| App | `src/Tsumugi.App/ViewModels/FirstRunWizardViewModel.cs` | 入力・保存エラー・登録成功通知を扱う |
| App | `src/Tsumugi.App/FirstRunWizardWindow.axaml` と `.axaml.cs` | 専用初回登録画面、閉じる要求の仲介 |
| App | `src/Tsumugi.App/App.axaml.cs` | startup coordinator と Window の生成・置換・終了時バックアップを接続 |
| App | `src/Tsumugi.App/CompositionRoot.cs` | S5 の UseCase、coordinator、Wizard VM を DI 登録 |
| Build | `build/publish.sh` / `build/publish.ps1` | RID 固定の self-contained 単一ファイル発行 |
| Tests | `tests/Tsumugi.Domain.Tests/FirstRunPolicyTests.cs` | 初回判定の純粋関数テスト |
| Tests | `tests/Tsumugi.Application.Tests/RegisterFirstRunUseCaseTests.cs` | 委譲・地域区分検証・既存入力規則のテスト |
| Tests | `tests/Tsumugi.App.Tests/FirstRunStartupCoordinatorTests.cs` | 空／登録済みの起動先判定 |
| Tests | `tests/Tsumugi.App.Tests/FirstRunWizardViewModelTests.cs` | 成功・入力エラー・キャンセル通知の VM テスト |
| Tests | `tests/Tsumugi.App.Tests/CompositionRootTests.cs` | S5 の DI 解決テスト |
| Tests | `tests/Tsumugi.App.Tests/PublishScriptContractTests.cs` | 発行スクリプトの RID・publish プロパティ静的契約 |
| Docs | `docs/decisions/0054-distribution-configuration.md` | 配布構成の ADR |
| Docs | `docs/operations.md` / `docs/manual-qa.md` | 運用手順と OS ごとの実施記録 |
| Docs | `.gitignore` / `CHANGELOG.md` / roadmap | 発行物除外と S5 の部分クローズ記録 |

## 実装上の固定事項

- 初回判定は DB ファイルの有無ではなく、migration 後の `ListOfficesUseCase` 結果件数で行う。
- `FirstRunPolicy` は `int officeCount` のみを受け、`officeCount < 1` を初回として扱う。`OfficeDto`、日時、I/O、DI を参照しない。
- Wizard では `RegionGrade.None` を登録前に拒否する。既存の `OfficeView` の振る舞いは変更しない。
- `RegisterFirstRunUseCase` はデータベースを直接触らず、既存 `RegisterOfficeUseCase` の詳細 overload へ一度だけ委譲する。
- Wizard 成功時は `desktop.MainWindow = new MainWindow(...)` を**先に**行ってから Wizard を閉じる。逆順は禁止。
- Wizard のキャンセルまたは Window close は MainWindow を作らず `desktop.Shutdown()` を呼ぶ。終了時自動バックアップの既存フックは両経路で維持する。
- `PublishTrimmed=false` を明示する。Avalonia／EF Core のリフレクション利用を理由に trim は有効化しない。
- 生成した `artifacts/publish/` 以下を追跡しない。署名、インストーラ、アップデータ、S3b は対象外。
- Windows 実機 smoke は S5 では未実施であり、完了扱いにしない。roadmap と CHANGELOG へ明記する。

---

### Task 1: 初回判定 Policy と初回登録 UseCase を TDD で追加する

**Files:**
- Create: `src/Tsumugi.Domain/Logic/FirstRunPolicy.cs`
- Create: `src/Tsumugi.Application/Dtos/RegisterFirstRunInput.cs`
- Create: `src/Tsumugi.Application/UseCases/Office/RegisterFirstRunUseCase.cs`
- Create: `tests/Tsumugi.Domain.Tests/FirstRunPolicyTests.cs`
- Create: `tests/Tsumugi.Application.Tests/RegisterFirstRunUseCaseTests.cs`

- [x] **Step 1: FirstRunPolicy の失敗テストを書く**

  `NeedsFirstRun(int officeCount)` に対して `0` と負数 → `true`、`1` と複数件 → `false` を検証する。負数は repository からは到達しないが、spec の「`officeCount < 1 → true`」契約をそのまま固定する。

  ```csharp
  FirstRunPolicy.NeedsFirstRun(0).Should().BeTrue();
  FirstRunPolicy.NeedsFirstRun(-1).Should().BeTrue();
  FirstRunPolicy.NeedsFirstRun(1).Should().BeFalse();
  ```

- [x] **Step 2: Domain テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~FirstRunPolicyTests`

  Expected: FAIL（`FirstRunPolicy` が未定義）。

- [x] **Step 3: 最小の純粋 Policy を実装する**

  `FirstRunPolicy.NeedsFirstRun(int officeCount)` を追加し、`officeCount < 1` をそのまま返す。コメントも含め、Application DTO・`DateTime`・I/O を参照しない。

- [x] **Step 4: Domain テストを通す**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~FirstRunPolicyTests`

  Expected: PASS。

- [x] **Step 5: 初回登録 UseCase の失敗テストを書く**

  `RegisterFirstRunUseCaseTests` に既存 `RegisterOfficeUseCaseTests` と同型の fake repository / unit of work を置き、次を固定する。

  - `RegisterFirstRunInput` の郵便番号・住所・電話・管理者職氏名が、保存された `Office` の対応プロパティへ渡る。
  - `RegionGrade.None` は repository 呼出前に `ArgumentException`（「地域区分」）となる。
  - 空の事業所番号・名称、重複事業所番号、任意入力の長さ／空白は、委譲先と同じ例外規則になる。
  - `actor` と `CancellationToken` を入力として受け、保存回数が1回である。

  ```csharp
  var input = new RegisterFirstRunInput(
      "1234567890", "つむぎ作業所", ServiceCategory.TypeB, RegionGrade.Grade4)
  {
      RepresentativeTitleAndName = "管理者 山田太郎",
  };

  var result = await sut.ExecuteAsync(input, "tester", CancellationToken.None);
  result.RepresentativeTitleAndName.Should().Be("管理者 山田太郎");
  ```

- [x] **Step 6: Application テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.Application.Tests --filter FullyQualifiedName~RegisterFirstRunUseCaseTests`

  Expected: FAIL（input DTO / UseCase が未定義）。

- [x] **Step 7: input DTO と薄い委譲 UseCase を実装する**

  `RegisterFirstRunInput` は事業所番号・名称・サービス種別・地域区分と、任意の住所／連絡先／管理者職氏名を持つ immutable record とする。`RegisterFirstRunUseCase` は `RegisterOfficeUseCase` を constructor 注入し、`RegionGrade.None` のみを初回固有の validation として拒否した後、以下の既存 overload へそのまま渡す。

  ```csharp
  return await registerOfficeUseCase.ExecuteAsync(
      input.OfficeNumber, input.Name, input.ServiceCategory, input.RegionGrade,
      input.PostalCode, input.Address, input.PhoneNumber,
      input.RepresentativeTitleAndName, actor, ct);
  ```

- [x] **Step 8: Domain / Application 対象テストを通す**

  Run: `dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~FirstRunPolicyTests && dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~RegisterFirstRunUseCaseTests|FullyQualifiedName~RegisterOfficeUseCaseTests"`

  Expected: PASS。

- [x] **Step 9: コミットする**

  ```bash
  git add src/Tsumugi.Domain/Logic/FirstRunPolicy.cs src/Tsumugi.Application/Dtos/RegisterFirstRunInput.cs src/Tsumugi.Application/UseCases/Office/RegisterFirstRunUseCase.cs tests/Tsumugi.Domain.Tests/FirstRunPolicyTests.cs tests/Tsumugi.Application.Tests/RegisterFirstRunUseCaseTests.cs
  git commit -m "feat(phase4-s5): AC4-10の初回登録ユースケースを追加する"
  ```

### Task 2: 起動先判定 coordinator と Wizard ViewModel を TDD で追加する

**Files:**
- Create: `src/Tsumugi.App/Startup/FirstRunStartupDestination.cs`
- Create: `src/Tsumugi.App/Startup/FirstRunStartupCoordinator.cs`
- Create: `src/Tsumugi.App/ViewModels/FirstRunWizardViewModel.cs`
- Create: `tests/Tsumugi.App.Tests/FirstRunStartupCoordinatorTests.cs`
- Create: `tests/Tsumugi.App.Tests/FirstRunWizardViewModelTests.cs`

- [x] **Step 1: startup coordinator の失敗テストを書く**

  `ListOfficesUseCase` 用の in-memory repository を用い、`DecideAsync` が次を返すことを検証する。

  - 空一覧 → `FirstRunStartupDestination.Wizard`
  - 1件以上 → `FirstRunStartupDestination.Main`
  - repository の例外を握り潰さず呼出側へ伝播する

- [x] **Step 2: coordinator テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~FirstRunStartupCoordinatorTests`

  Expected: FAIL（startup 型が未定義）。

- [x] **Step 3: testable な startup coordinator を実装する**

  App 層に enum `FirstRunStartupDestination { Wizard, Main }` と `FirstRunStartupCoordinator(ListOfficesUseCase)` を追加する。`DecideAsync` は一覧の `Count` を `FirstRunPolicy.NeedsFirstRun` へ渡すだけにする。migration、Window 生成、Avalonia lifetime には依存させない。

- [x] **Step 4: Wizard ViewModel の失敗テストを書く**

  `FirstRunWizardViewModel` を `RegisterFirstRunUseCase` と test callback で組み立て、次を検証する。

  - 妥当な必須入力と `Grade1` 以上で `RegisterCommand` を実行すると `Registered` callback が1回呼ばれ、エラーが空になる。
  - `RegionGrade.None`、空の番号／名称、重複番号は `SaveErrorMessage` に表示され、callback は呼ばれない。
  - `CancelCommand` は `Cancelled` callback を1回呼び、永続化しない。
  - 任意入力の空文字は既存 `OfficeViewModel` と同じく `null` として渡す。

- [x] **Step 5: Wizard ViewModel テストがコンパイル不能で失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~FirstRunWizardViewModelTests`

  Expected: FAIL（Wizard VM が未定義）。

- [x] **Step 6: Wizard ViewModel を最小実装する**

  `OfficeViewModel` の入力型・`NullIfEmpty`・`Environment.UserName` の使い方を踏襲するが、既存 VM を流用しない。以下を置く。

  - `OfficeNumber`、`Name`、`Category = TypeB`、`Region = None`、住所／連絡先／`RepresentativeTitleAndName`
  - `SaveErrorMessage` と `IsSaving`
  - `[RelayCommand] RegisterAsync()`：`RegisterFirstRunUseCase` を呼び、成功時だけ `Registered?.Invoke()`
  - `[RelayCommand] Cancel()`：`Cancelled?.Invoke()`
  - `Action? Registered` / `Action? Cancelled`：Window／App がテスト可能に寿命イベントを受け取るための薄い通知

  `ArgumentException` と既存登録 UseCase が重複時に送出する `InvalidOperationException` を捕捉し、`SaveErrorMessage` へ安全なドメイン文言を設定する。`OperationCanceledException` は `CancellationToken` がキャンセルされた場合だけ再送出し、その他の予期しない例外は利用者向け固定文言「登録に失敗しました。入力内容を確認して再度お試しください。」に置き換える。例外の生メッセージや個人情報を表示しない。

- [x] **Step 7: App 対象テストを通す**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~FirstRunStartupCoordinatorTests|FullyQualifiedName~FirstRunWizardViewModelTests"`

  Expected: PASS。

- [x] **Step 8: コミットする**

  ```bash
  git add src/Tsumugi.App/Startup src/Tsumugi.App/ViewModels/FirstRunWizardViewModel.cs tests/Tsumugi.App.Tests/FirstRunStartupCoordinatorTests.cs tests/Tsumugi.App.Tests/FirstRunWizardViewModelTests.cs
  git commit -m "feat(phase4-s5): 初回起動判定とウィザードVMを追加する"
  ```

### Task 3: 専用 Wizard Window・DI・起動ライフサイクルを接続する

**Files:**
- Create: `src/Tsumugi.App/FirstRunWizardWindow.axaml`
- Create: `src/Tsumugi.App/FirstRunWizardWindow.axaml.cs`
- Create: `src/Tsumugi.App/Startup/IInitialWindowHost.cs`
- Create: `src/Tsumugi.App/Startup/FirstRunDesktopStartupOrchestrator.cs`
- Create: `src/Tsumugi.App/Startup/AvaloniaInitialWindowHost.cs`
- Modify: `src/Tsumugi.App/App.axaml.cs`
- Modify: `src/Tsumugi.App/CompositionRoot.cs`
- Modify: `tests/Tsumugi.App.Tests/CompositionRootTests.cs`
- Create: `tests/Tsumugi.App.Tests/FirstRunWizardWindowWiringTests.cs`

- [x] **Step 1: DI / Window 配線の失敗テストを書く**

  - location-aware の `CompositionRoot` から `RegisterFirstRunUseCase`、`FirstRunStartupCoordinator`、`FirstRunWizardViewModel` を解決できること。
  - `FirstRunDesktopStartupOrchestrator` を fake `IInitialWindowHost` で実行し、空一覧では `ShowWizard`、登録済みでは `ShowMain`、Wizard 成功 callback では `ShowMain`、キャンセル callback では `Shutdown` が呼ばれること。
  - coordinator の一覧取得が失敗した場合、orchestrator が例外を外へ漏らさず `Shutdown` だけを一度呼ぶこと。
  - `FirstRunWizardWindow.axaml` が `FirstRunWizardViewModel` を `x:DataType` にし、登録／キャンセル command を持つこと。
  - `.axaml.cs` が `Registered` と `Cancelled` を購読すること（XAML 文字列・コード文字列を検査する既存 `AppNavigationServiceTests` の方式でよい）。

- [x] **Step 2: テストが失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~CompositionRootTests|FullyQualifiedName~FirstRunWizardWindowWiringTests"`

  Expected: FAIL（未登録 DI または Window ファイル未作成）。

- [x] **Step 3: キーボード完結の Wizard Window を実装する**

  `FirstRunWizardWindow.axaml` は `Window` として実装し、既存 `OfficeView` と同じ DynamicResource・余白・FontSize を使う。次を含める。

  - タイトル「初回セットアップ」と、登録後も請求プロファイル／体制届の入力が必要な旨の短い説明
  - 事業所番号・事業所名・サービス種別（TypeB 固定表示可）・`None` を除く地域区分選択
  - 郵便番号・住所・電話番号・ラベル「管理者（職氏名）」の任意入力
  - エラー表示（`StringNotEmptyConverter`）と、`Ctrl+Enter` の登録、`Escape` のキャンセル KeyBinding
  - 登録／キャンセル Button。`IsSaving` 中は二重送信を防ぐ

  code-behind は ViewModel の `Registered` / `Cancelled` を Window のイベントへ中継するだけにし、UseCase や `DbContext` を参照しない。

- [x] **Step 4: CompositionRoot の DI を追加する**

  `RegisterFirstRunUseCase` と `FirstRunStartupCoordinator` は scoped、`FirstRunWizardViewModel` は transient に登録する。`FirstRunPolicy` は static 純粋関数のため DI 登録しない。`Build(string)` の既存部分検査用 overload に MainViewModel が未解決である制約を変えない。

- [x] **Step 5: testable な desktop orchestrator と App 起動を Wizard/Main 分岐へ変更する**

  `IInitialWindowHost` を App 層の小さな抽象として追加する。

  ```csharp
  public interface IInitialWindowHost
  {
      void ShowMain();
      void ShowWizard(Action registered, Action cancelled);
      void Shutdown();
  }
  ```

  `FirstRunDesktopStartupOrchestrator` は `FirstRunStartupCoordinator` と `IInitialWindowHost` を受ける。`StartAsync` は判定が Main なら `ShowMain`、Wizard なら `ShowWizard(host.ShowMain, host.Shutdown)` を呼ぶ。**すべての例外をこの Task 内で捕捉して `host.Shutdown()` を一度だけ呼び、再送出しない**。これにより App 側の fire-and-forget Task に未監視例外を残さず、失敗時も空のデスクトッププロセスを残さない。

  `AvaloniaInitialWindowHost` は `IClassicDesktopStyleApplicationLifetime` と scope の `IServiceProvider` を受け、以下を実装する。

  - `ShowMain`: MainViewModel を解決して `desktop.MainWindow` を新しい `MainWindow` に置き換える。
  - `ShowWizard`: Wizard VM / Window を作り、登録成功時は **最初に `ShowMain()`、次に Wizard `Close()`**、キャンセル／未完了 Close 時は `desktop.Shutdown()` を結ぶ。
  - Window が成功後の `Close` をキャンセル扱いにしないよう、Wizard code-behind は登録成功イベントを受けた時点で完了フラグを立てる。

  実装の順序を次で固定する。

  1. 現在どおり保存先確保・CompositionRoot・scope・migration を同期的に完了する。
  2. `desktop.ShutdownRequested += OnShutdownRequested` を、最初の Window を設定する前に一度だけ登録する。
  3. scope から coordinator と、`AvaloniaInitialWindowHost` を組み立てた orchestrator を解決／生成する。
  4. `_ = orchestrator.StartAsync()` を開始する。orchestrator 自身が例外を終端するため、App に `async void` や `.GetAwaiter().GetResult()` を追加しない。

  バックアップは `OnShutdownRequested` の既存一回限りの意味論を維持し、Wizard キャンセルでも空 DB の scheduled backup が通ってよい。

- [x] **Step 6: 配線テストとビルドを通す**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~CompositionRootTests|FullyQualifiedName~FirstRunWizardWindowWiringTests" && dotnet build`

  Expected: PASS、警告ゼロ。

- [x] **Step 7: コミットする**

  ```bash
  git add src/Tsumugi.App/App.axaml.cs src/Tsumugi.App/CompositionRoot.cs src/Tsumugi.App/FirstRunWizardWindow.axaml src/Tsumugi.App/FirstRunWizardWindow.axaml.cs src/Tsumugi.App/Startup tests/Tsumugi.App.Tests/CompositionRootTests.cs tests/Tsumugi.App.Tests/FirstRunWizardWindowWiringTests.cs
  git commit -m "feat(phase4-s5): 初回セットアップ画面を起動フローへ接続する"
  ```

### Task 4: self-contained 発行スクリプトと静的契約テストを追加する

**Files:**
- Create: `build/publish.sh`
- Create: `build/publish.ps1`
- Modify: `.gitignore`
- Create: `tests/Tsumugi.App.Tests/PublishScriptContractTests.cs`

- [x] **Step 1: 発行スクリプト契約の失敗テストを書く**

  repository root を既存 `AppNavigationServiceTests` と同じ探索方法で特定して両スクリプトを読む。各ファイルに次が含まれることを検証する。

  | スクリプト | 必須文字列 |
  |---|---|
  | `publish.sh` | `osx-arm64`、`dotnet publish src/Tsumugi.App`、`-c Release`、`--self-contained true`、`-p:PublishSingleFile=true`、`-p:PublishTrimmed=false`、`artifacts/publish/osx-arm64` |
  | `publish.ps1` | `win-x64`、同じ publish properties、`artifacts/publish/win-x64` |

  さらに `.gitignore` に `artifacts/` があることを固定する。

- [x] **Step 2: 発行契約テストが失敗することを確認する**

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~PublishScriptContractTests`

  Expected: FAIL（スクリプト未作成）。

- [x] **Step 3: Bash 発行スクリプトを実装する**

  `set -euo pipefail`、repository root への移動、出力先の作り直しを置く。引数で RID や trim 有無を変えられるようにせず、次の固定コマンドを実行する。

  ```bash
  dotnet publish src/Tsumugi.App \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o artifacts/publish/osx-arm64
  ```

  スクリプト自身に実行権限を付ける（Git index mode `100755`）。

- [x] **Step 4: PowerShell 発行スクリプトを実装する**

  `$ErrorActionPreference = "Stop"` を設定し、`$PSScriptRoot/..` を解決して `Push-Location` / `Pop-Location` を `try/finally` で対にする。成果物の削除と作成後、次と等価のコマンドを実行する。

  ```powershell
  dotnet publish src/Tsumugi.App `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -o artifacts/publish/win-x64
  ```

  Windows 専用 API、署名、インストーラ処理を追加しない。

- [x] **Step 5: ignore と契約テストを通す**

  `.gitignore` に `artifacts/` を追加する。

  Run: `dotnet test tests/Tsumugi.App.Tests --filter FullyQualifiedName~PublishScriptContractTests && bash -n build/publish.sh`

  Expected: PASS。macOS 上で `pwsh` が利用可能なら続けて `pwsh -NoProfile -Command "Invoke-ScriptAnalyzer -Path build/publish.ps1"` を実行する。利用不可または ScriptAnalyzer 未導入なら、その事実を `manual-qa.md` の Windows 未実施欄へ記録し、静的契約テストを代替根拠とする。

- [x] **Step 6: コミットする**

  ```bash
  git add build/publish.sh build/publish.ps1 .gitignore tests/Tsumugi.App.Tests/PublishScriptContractTests.cs
  git commit -m "build(phase4-s5): self-contained配布スクリプトを追加する"
  ```

### Task 5: ADR・運用ガイド・manual QA 表を作成する

**Files:**
- Create: `docs/decisions/0054-distribution-configuration.md`
- Create: `docs/operations.md`
- Create: `docs/manual-qa.md`

- [x] **Step 1: 配布構成 ADR 0054 を作成する**

  着手時に `docs/decisions/` の最新番号を確認し、0054 が空いている場合だけこの名前を使う。既存 ADR 形式（結論 → 背景 → 選択肢 → 決定 → 影響）で以下を記録する。

  - `osx-arm64` / `win-x64`、Release、self-contained、単一ファイル、trim 無効
  - `artifacts/publish/<RID>/` を生成し追跡しない
  - trim 不採用の理由（Avalonia／EF Core のリフレクション）
  - インストーラ、署名、オンライン更新、font sub-setting は今回の対象外
  - Windows 実機 smoke は未実施で、macOS 実施済み後も再確認が必要

- [x] **Step 2: 運用ガイドを作成する**

  `docs/operations.md` は日本語で次の節を持つ。

  1. 対応 OS と発行物の作成・配布（スクリプト実行例を含む）
  2. 初回セットアップ（入力項目、事業所登録後に請求プロファイル／体制届を既存画面で設定すること）
  3. 日常バックアップ・復元（ADR 0052 の自動バックアップ、手動「控えを保存」、復元後に終了すること、外部控えの復元は固定 backups ディレクトリへ手動コピーが必要な現時点の制約）
  4. FileVault／BitLocker の有効化確認（ADR 0003。DB 本体を暗号化しない理由と再評価トリガは ADR へのリンク）
  5. 請求 CSV の責務境界（CSV の生成まで。伝送・電子証明書・回線処理は対象外）
  6. 障害時の連絡前確認（氏名、受給者証番号、DB・バックアップのフルパスを問い合わせ文やログへ含めない）

  実体パスを本文へ固定で書かない。OS／環境変数で変動するため ADR 0003 を参照する。

- [x] **Step 3: manual QA 表を作成する**

  `docs/manual-qa.md` に実施日、実施者、OS・ハードウェア、発行物の SHA-256、結果、備考を記録できる列を持たせる。macOS／Windows のそれぞれで以下をチェックする表を作る。

  1. 発行スクリプト成功、単一ファイル生成
  2. クリーンな ApplicationData で起動すると Wizard が出る
  3. `RegionGrade.None` は登録できず、必須／任意入力のエラーが安全に表示される
  4. 妥当な事業所・管理者職氏名を登録できる
  5. 再起動後は Wizard を出さず MainWindow を出す
  6. 終了時自動バックアップが生成される
  7. キーボード操作（`Ctrl+Enter`／`Escape`、フォーカス順、フォント拡大、Reduce Motion）

  初期状態では全項目を未実施にする。Windows 行には「Windows 実機未実施。発行スクリプト契約テストのみ実行済み」と明記する。

- [x] **Step 4: 文書の内部リンクを確認する**

  Run: `rg -n "ADR 0003|ADR 0052|FileVault|BitLocker|伝送|電子証明書|Windows 実機未実施|osx-arm64|win-x64" docs/decisions/0054-distribution-configuration.md docs/operations.md docs/manual-qa.md`

  Expected: 指定語と ADR 参照があり、平文 DB のフルパスを固定記載していない。

- [x] **Step 5: コミットする**

  ```bash
  git add docs/decisions/0054-distribution-configuration.md docs/operations.md docs/manual-qa.md
  git commit -m "docs(phase4-s5): 配布構成と運用手順を記録する"
  ```

### Task 6: macOS 発行・smoke を実施し、完了文書と品質ゲートを同期する

**Files:**
- Modify: `docs/manual-qa.md`
- Modify: `docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/superpowers/plans/2026-08-17-phase4-s5-distribution-and-first-run.md`（完了 checkbox のみ）

- [x] **Step 1: 対象テストと全体品質ゲートを通す**

  Run:

  ```bash
  dotnet test tests/Tsumugi.Domain.Tests --filter FullyQualifiedName~FirstRunPolicyTests
  dotnet test tests/Tsumugi.Application.Tests --filter "FullyQualifiedName~RegisterFirstRunUseCaseTests|FullyQualifiedName~RegisterOfficeUseCaseTests"
  dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~FirstRun|FullyQualifiedName~PublishScriptContractTests|FullyQualifiedName~CompositionRootTests"
  ./build/ci.sh
  ```

  Expected: すべて PASS、警告ゼロ、format／architecture／offline guard を含めて PASS。

- [x] **Step 2: macOS 用発行物を作る**

  Run: `./build/publish.sh`

  Expected: `artifacts/publish/osx-arm64/` に self-contained 単一ファイルが生成される。`file` と `shasum -a 256` の出力を manual QA の macOS 行へ記録する。成果物そのものはコミットしない。

- [x] **Step 3: macOS smoke を実施して記録する**

  開発用の既存 ApplicationData を使わない。隔離した起動環境（少なくとも一時の `HOME` / `XDG_CONFIG_HOME`、または専用 OS ユーザー）で発行物を起動し、Task 5 の macOS 7項目をすべて実施する。

  - Wizard のキャンセルで MainWindow が開かず終了することも確認する。
  - 登録成功後に、Wizard を閉じても MainWindow が残ることを確認する。
  - 再起動・終了時バックアップまで確認してから、実施日時・環境・結果・発行物 SHA-256 を `docs/manual-qa.md` へ書く。

  実施不能な項目があれば PASS を装わず、未実施または失敗として根拠とともに残し、S5 を完了扱いにしない。

- [x] **Step 4: roadmap と CHANGELOG を部分クローズ表記へ更新する**

  roadmap の S5 と AC4-9〜AC4-11 を次の意味で更新する。

  - macOS 発行・初回 Wizard・運用ガイド・macOS QA は完了
  - **Windows 実機 smoke は未実施**（スクリプトと静的契約テストのみ）
  - したがって「AC4-9〜11を完全達成」とは書かず、「macOS 完了、Windows 実機確認待ち」と明記

  CHANGELOG に S5 節を追加し、発行設定、初回 Wizard、operations/manual QA、Windows 未実施を要約する。

- [x] **Step 5: 最終差分を確認する**

  Run: `git status --short && git diff --check && rg -n "AC4-9|AC4-10|AC4-11|Windows.*未実施|osx-arm64|win-x64" CHANGELOG.md docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md docs/manual-qa.md`

  Expected: 意図した S5 の変更だけで空白エラーなし。Windows smoke が完了扱いになっていない。

- [x] **Step 6: 最終コミットする**

  ```bash
  git add docs/manual-qa.md docs/superpowers/specs/2026-07-05-phase4-remaining-roadmap.md CHANGELOG.md docs/superpowers/plans/2026-08-17-phase4-s5-distribution-and-first-run.md
  git commit -m "docs(phase4-s5): AC4-9〜AC4-11のmacOS検証を記録する"
  ```
