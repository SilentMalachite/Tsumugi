# M-2: 工賃 PDF 保存ダイアログを UI から到達可能にする 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Codex M-2 を解消する。`WageStatementViewModel.GenerateStatementPdf` / `GeneratePaymentListPdf` は実装済みだが、`RelayCommand` ではなく View にもボタンが無く UI から到達できない。`IFileSaveService` 抽象 + Avalonia `IStorageProvider` 実装を導入し、VM に RelayCommand + DataGrid 選択を追加、XAML にボタン 2 つを配線して、AC2-7 PDF 出力を真に "✅" にする。

**Architecture:**
- `Tsumugi.App.Services.IFileSaveService` 抽象を定義（VM がテスト可能）。
- `Tsumugi.App.Services.AvaloniaFileSaveService` を Avalonia `TopLevel.StorageProvider.SaveFilePickerAsync` で実装。`Application.Current?.ApplicationLifetime` 経由で MainWindow の TopLevel を取得（DI に Func<TopLevel?> を渡さなくて済む）。
- `WageStatementViewModel` に `IFileSaveService` 注入、`SelectedStatement` `ObservableProperty`、`SaveStatementPdfCommand` / `SavePaymentListPdfCommand` を追加。既存の素 PDF メソッドはコマンド本体に統合（公開 method は残す → テスト互換性維持）。
- `WageStatementView` XAML: DataGrid に `SelectedItem` バインド、ボタン 2 つを追加（選択依存・選択不要）。
- テストは `FakeFileSaveService` で「ダイアログ → 仮想パス → 書き込みバイト列受け取り」を検証。

**Tech Stack:** .NET 10 / C# / Avalonia 11 / CommunityToolkit.Mvvm / xUnit / FluentAssertions

## Global Constraints

- 依存方向: 本計画は Tsumugi.App と Tsumugi.App.Tests のみ変更。Domain / Application / Infrastructure には触らない。
- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — 0 warnings。
- 既存テスト (`WageStatementViewModelTests` 7 件) は VM 構築シグネチャ変更を伴うため `Build` ヘルパを更新するが、本質的テストロジックは破壊しない。
- 既存 `GenerateStatementPdf(Guid recipientId)` / `GeneratePaymentListPdf()` の public メソッドは温存（既存テストが直接呼ぶ）。RelayCommand はそれらをラップする形で追加。
- `CompositionRoot.AddTsumugiServices` に `services.AddSingleton<IFileSaveService, AvaloniaFileSaveService>()` 1 行追加。
- 各 commit 前に `dotnet format --verify-no-changes` 緑、`dotnet build -warnaserror` 緑、`dotnet test` 全緑。
- コミットメッセージは `phase2` + `M-2` を含む。

## File Structure

| Task | Files |
|---|---|
| 1 (Service) | `src/Tsumugi.App/Services/IFileSaveService.cs` (新規), `src/Tsumugi.App/Services/AvaloniaFileSaveService.cs` (新規), `src/Tsumugi.App/CompositionRoot.cs:24-113` (1 行追加) |
| 2 (VM + tests) | `src/Tsumugi.App/ViewModels/WageStatementViewModel.cs:17-114` (修正), `tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs` (修正・追加) |
| 3 (View + doc) | `src/Tsumugi.App/Views/WageStatementView.axaml:36-43` (DataGrid SelectedItem + 2 buttons 追加), `docs/phase2-acceptance.md:39` (deferred セクション更新) |

## 仕様根拠

- Codex M-2: "AC2-7 を ✅ にしていますが、PDF生成はライブUIから到達できません。ViewModelには通常メソッドがありますが `RelayCommand` ではなく、画面には再読込/確定ボタンしかありません。修正方針: 保存/印刷コマンドとView側の保存ダイアログ配線を入れるか、AC2-7を partial に下げる。"
- 既存 deferred 記録: `docs/phase2-acceptance.md:39` "UI WageStatementView の SaveFileDialog 配線 (ViewModel は byte[] を返すまで実装済)" → M-2 はこの deferred を解消。
- Avalonia 11 公式 IStorageProvider: `TopLevel.GetTopLevel(control).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{...})`。

---

### Task 1: `IFileSaveService` 抽象 + Avalonia 実装 + DI

**Files:**
- Create: `src/Tsumugi.App/Services/IFileSaveService.cs`
- Create: `src/Tsumugi.App/Services/AvaloniaFileSaveService.cs`
- Modify: `src/Tsumugi.App/CompositionRoot.cs:24-113` (add 1 line after existing ViewModel registrations or near other singletons)

**Interfaces:**
- Consumes: `Avalonia.Controls.TopLevel`, `Avalonia.Platform.Storage.IStorageProvider`, `Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime`, `System.IO.File`
- Produces: `IFileSaveService` with 1 method `Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName, string extension, CancellationToken ct = default)` — returns `true` if user picked a file and write succeeded, `false` if user cancelled. Throws on I/O failure.

- [ ] **Step 1.1: 抽象インターフェースを作成**

`src/Tsumugi.App/Services/IFileSaveService.cs` を以下で新規作成:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace Tsumugi.App.Services;

/// <summary>ファイル保存ダイアログを介して指定バイト列をユーザ指定パスへ書き出す抽象。</summary>
/// <remarks>VM 層からテスト可能にするための薄い抽象。UI 実装は <see cref="AvaloniaFileSaveService"/>。</remarks>
public interface IFileSaveService
{
    /// <summary>
    /// 保存ダイアログを開き、ユーザが選択したパスへ <paramref name="bytes"/> を書き出す。
    /// </summary>
    /// <param name="bytes">書き出すバイト列。</param>
    /// <param name="suggestedFileName">既定ファイル名（拡張子含めても可）。</param>
    /// <param name="fileTypeName">ダイアログのファイル種別名（例: "PDF"）。</param>
    /// <param name="extension">既定拡張子（例: ".pdf"、ドット必須）。</param>
    /// <param name="ct">キャンセル要求。</param>
    /// <returns>ユーザが保存先を確定し書き込みが完了したら <c>true</c>、キャンセル時は <c>false</c>。</returns>
    Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName, string extension, CancellationToken ct = default);
}
```

- [ ] **Step 1.2: Avalonia 実装を作成**

`src/Tsumugi.App/Services/AvaloniaFileSaveService.cs` を以下で新規作成:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaApplication = Avalonia.Application;

namespace Tsumugi.App.Services;

/// <summary>Avalonia の <see cref="IStorageProvider"/> を介する <see cref="IFileSaveService"/> 実装。</summary>
public sealed class AvaloniaFileSaveService : IFileSaveService
{
    public async Task<bool> SaveAsync(
        byte[] bytes,
        string suggestedFileName,
        string fileTypeName,
        string extension,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var topLevel = ResolveTopLevel()
            ?? throw new InvalidOperationException("保存ダイアログを開く TopLevel が解決できません。");

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(fileTypeName) { Patterns = new[] { "*" + extension } },
            },
            DefaultExtension = extension.TrimStart('.'),
        });
        if (file is null) return false;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            throw new InvalidOperationException("保存先パスをローカルファイルとして解決できません。");

        await File.WriteAllBytesAsync(path, bytes, ct);
        return true;
    }

    private static TopLevel? ResolveTopLevel()
    {
        if (AvaloniaApplication.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { } mw)
        {
            return TopLevel.GetTopLevel(mw);
        }
        return null;
    }
}
```

- [ ] **Step 1.3: `CompositionRoot.AddTsumugiServices` に DI 登録を追加**

`src/Tsumugi.App/CompositionRoot.cs` の `AddTsumugiServices` メソッド内、「// Phase 2: 帳票（E2/E3）」のブロックの直後（`services.AddScoped<IWageReportGenerator, WageStatementPdfGenerator>();` の次の行）に挿入:

```csharp
        // Phase 2: PDF 保存ダイアログ抽象（M-2）
        services.AddSingleton<Tsumugi.App.Services.IFileSaveService, Tsumugi.App.Services.AvaloniaFileSaveService>();
```

- [ ] **Step 1.4: ビルド・format**

```bash
dotnet build
dotnet format --verify-no-changes
```

Expected: 0 warnings、format clean。Avalonia の `IStorageProvider`、`FilePickerSaveOptions`、`TopLevel` の参照が `Tsumugi.App.csproj` 経由で既に解決可能（Avalonia 11.x 標準）。

- [ ] **Step 1.5: ソリューション全体テスト**

```bash
dotnet test
```

Expected: 既存全 PASS（466 baseline）。`IFileSaveService` は VM が要求していないので、まだ DI 解決失敗は起こらない。

- [ ] **Step 1.6: コミット**

```bash
git add src/Tsumugi.App/Services/IFileSaveService.cs \
        src/Tsumugi.App/Services/AvaloniaFileSaveService.cs \
        src/Tsumugi.App/CompositionRoot.cs
git commit -m "feat(phase2): M-2 add IFileSaveService abstraction and Avalonia implementation"
```

---

### Task 2: `WageStatementViewModel` に SelectedStatement + 2 RelayCommands + テスト

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/WageStatementViewModel.cs:17-114`
- Modify: `tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs`

**Interfaces:**
- Consumes: 既存 VM コンストラクタ + 新規 `IFileSaveService fileSaveService` (6 引数目)
- Produces: 
  - 新 `[ObservableProperty] WageStatementDto? _selectedStatement;`
  - 新 `[RelayCommand] async Task SaveSelectedStatementPdfAsync()` — `SelectedStatement` 必須、`GenerateStatementPdf` を呼び出して結果バイト列を `IFileSaveService.SaveAsync` に渡す。
  - 新 `[RelayCommand] async Task SavePaymentListPdfAsync()` — `Statements` から `GeneratePaymentListPdf` を呼び出して結果を保存。
- 既存 `GenerateStatementPdf(Guid recipientId)` / `GeneratePaymentListPdf()` は **そのまま温存**（既存テストとの互換性維持）。

- [ ] **Step 2.1: VM の primary constructor + observable property + 2 RelayCommands を追加**

`src/Tsumugi.App/ViewModels/WageStatementViewModel.cs` のクラス宣言を以下に修正:

差し替え対象（現状）:
```csharp
public sealed partial class WageStatementViewModel(
    CloseWagesUseCase close,
    QueryWageStatementUseCase query,
    ListRecipientsUseCase listRecipients,
    IWageReportGenerator reportGenerator,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private OfficeDto? _office;
```

新しい宣言:
```csharp
public sealed partial class WageStatementViewModel(
    CloseWagesUseCase close,
    QueryWageStatementUseCase query,
    ListRecipientsUseCase listRecipients,
    IWageReportGenerator reportGenerator,
    ListOfficesUseCase listOfficesUseCase,
    Tsumugi.App.Services.IFileSaveService fileSaveService) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private OfficeDto? _office;
    [ObservableProperty] private WageStatementDto? _selectedStatement;
```

クラスの末尾（`LoadOfficesAsync` メソッドの直後、クラス閉じ `}` の直前）に以下を挿入:

```csharp

    [RelayCommand]
    public async Task SaveSelectedStatementPdfAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        StatusMessage = null;
        if (SelectedStatement is null)
        {
            ErrorMessage = "保存する明細を選択してください。";
            return;
        }
        var bytes = GenerateStatementPdf(SelectedStatement.RecipientId);
        if (bytes is null) return;  // GenerateStatementPdf 内で ErrorMessage 設定済み

        var suggested = $"工賃明細_{Year:D4}-{Month:D2}_{SelectedStatement.RecipientId:N}.pdf";
        try
        {
            var saved = await fileSaveService.SaveAsync(bytes, suggested, "PDF", ".pdf", ct);
            StatusMessage = saved ? $"明細 PDF を保存しました（{suggested}）。" : null;
        }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
        catch (System.IO.IOException ex) { ErrorMessage = $"ファイル書き込みに失敗しました: {ex.Message}"; }
    }

    [RelayCommand]
    public async Task SavePaymentListPdfAsync(CancellationToken ct = default)
    {
        ErrorMessage = null;
        StatusMessage = null;
        if (Statements.Count == 0)
        {
            ErrorMessage = "対象月の確定明細がありません。";
            return;
        }
        var bytes = GeneratePaymentListPdf();
        if (bytes is null) return;

        var suggested = $"工賃支払一覧_{Year:D4}-{Month:D2}.pdf";
        try
        {
            var saved = await fileSaveService.SaveAsync(bytes, suggested, "PDF", ".pdf", ct);
            StatusMessage = saved ? $"支払一覧 PDF を保存しました（{suggested}）。" : null;
        }
        catch (InvalidOperationException ex) { ErrorMessage = ex.Message; }
        catch (System.IO.IOException ex) { ErrorMessage = $"ファイル書き込みに失敗しました: {ex.Message}"; }
    }
```

- [ ] **Step 2.2: テストファイル `Build` ヘルパを更新**

`tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs` の `Build` ヘルパ（class 内の private method、optional パラメータを既に持つ）に `IFileSaveService` 引数を追加し、デフォルトで `FakeFileSaveService` を渡すように変更する。

**まず** ファイル class 内に nested class `FakeFileSaveService` を追加:

```csharp
private sealed class FakeFileSaveService : Tsumugi.App.Services.IFileSaveService
{
    public byte[]? LastSavedBytes { get; private set; }
    public string? LastSuggestedFileName { get; private set; }
    public string? LastFileTypeName { get; private set; }
    public string? LastExtension { get; private set; }
    public bool ReturnValue { get; init; } = true;
    public Exception? ThrowOnSave { get; init; }

    public Task<bool> SaveAsync(byte[] bytes, string suggestedFileName, string fileTypeName, string extension, CancellationToken ct = default)
    {
        if (ThrowOnSave is not null) throw ThrowOnSave;
        LastSavedBytes = bytes;
        LastSuggestedFileName = suggestedFileName;
        LastFileTypeName = fileTypeName;
        LastExtension = extension;
        return Task.FromResult(ReturnValue);
    }
}
```

**次に** `Build` ヘルパに引数を追加し、コンストラクタ呼び出しを更新:

```csharp
private WageStatementViewModel Build(
    Tsumugi.Domain.Entities.Office[]? offices = null,
    /* 既存 optional パラメータ ... */,
    Tsumugi.App.Services.IFileSaveService? fileSaveService = null)
{
    // 既存セットアップ
    var officeRepo = new InMemoryOfficeRepo();
    if (offices is not null) foreach (var o in offices) officeRepo.Add(o);

    return new WageStatementViewModel(close, query, listRecipients, reportGen,
        new ListOfficesUseCase(officeRepo),
        fileSaveService ?? new FakeFileSaveService());
}
```

（実際の `Build` ヘルパの構造に合わせる。既存の optional パラメータ列に新 1 つを追加し、`new WageStatementViewModel(...)` の最終引数を追加。）

- [ ] **Step 2.3: 新規テスト 4 件追加**

ファイル末尾（クラス閉じ `}` の直前）に挿入:

```csharp
[Fact]
public async Task SaveSelectedStatementPdf_without_selection_sets_error()
{
    var fake = new FakeFileSaveService();
    var vm = Build(fileSaveService: fake);

    await vm.SaveSelectedStatementPdfCommand.ExecuteAsync(null);

    vm.ErrorMessage.Should().NotBeNullOrEmpty();
    fake.LastSavedBytes.Should().BeNull();
}

[Fact]
public async Task SaveSelectedStatementPdf_invokes_service_with_pdf_bytes_when_statement_selected()
{
    var (vm, fake, stmtId) = await BuildWithClosedStatementAsync();
    vm.SelectedStatement = vm.Statements.First();

    await vm.SaveSelectedStatementPdfCommand.ExecuteAsync(null);

    vm.ErrorMessage.Should().BeNull();
    fake.LastSavedBytes.Should().NotBeNullOrEmpty();
    fake.LastSuggestedFileName.Should().StartWith("工賃明細_");
    fake.LastFileTypeName.Should().Be("PDF");
    fake.LastExtension.Should().Be(".pdf");
    vm.StatusMessage.Should().Contain("保存しました");
}

[Fact]
public async Task SavePaymentListPdf_without_statements_sets_error()
{
    var fake = new FakeFileSaveService();
    var vm = Build(fileSaveService: fake);

    await vm.SavePaymentListPdfCommand.ExecuteAsync(null);

    vm.ErrorMessage.Should().NotBeNullOrEmpty();
    fake.LastSavedBytes.Should().BeNull();
}

[Fact]
public async Task SavePaymentListPdf_invokes_service_with_pdf_bytes()
{
    var (vm, fake, _) = await BuildWithClosedStatementAsync();

    await vm.SavePaymentListPdfCommand.ExecuteAsync(null);

    vm.ErrorMessage.Should().BeNull();
    fake.LastSavedBytes.Should().NotBeNullOrEmpty();
    fake.LastSuggestedFileName.Should().StartWith("工賃支払一覧_");
    fake.LastSuggestedFileName.Should().EndWith(".pdf");
}
```

`BuildWithClosedStatementAsync` ヘルパは既存 `CloseAsync_persists_statements_and_refreshes_list` のセットアップを再利用する形で追加:

```csharp
private async Task<(WageStatementViewModel vm, FakeFileSaveService fake, Guid stmtId)> BuildWithClosedStatementAsync()
{
    var office = Tsumugi.Domain.Entities.Office.Create(
        Guid.NewGuid(), "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
        "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
    var fake = new FakeFileSaveService();
    var vm = Build(offices: new[] { office }, fileSaveService: fake);
    await vm.InitializeAsync();
    vm.SelectedOffice = vm.Offices.First();
    vm.Year = 2026; vm.Month = 7;
    await vm.CloseCommand.ExecuteAsync(null);
    // CloseCommand が statements を populate する想定。populate されない場合は Statements が空のまま Save テストが negative パスを通る。
    return (vm, fake, vm.Statements.FirstOrDefault()?.RecipientId ?? Guid.Empty);
}
```

**注意**: `BuildWithClosedStatementAsync` は既存 `CloseAsync_persists_statements_and_refreshes_list` のロジック（fake repos が `CloseWagesUseCase` でレコード生成を行う構造）を踏襲。既存テストファイル内の `Build` ヘルパ実装と `CloseAsync` テストの仕掛けを確認して、必要なら適応する。複雑であれば、`SaveSelectedStatementPdf_invokes_service_with_pdf_bytes_when_statement_selected` テストでは VM の `Statements.Add(...)` を直接呼んで 1 件入れる簡易セットアップでも可（VM の public collection なので外部から add 可能）。

- [ ] **Step 2.4: ビルド・format・focused test**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~WageStatementViewModelTests"
```

Expected: 既存 7 + 新規 4 = 11/11 PASS（既存ヘルパ更新が正しく機能する場合）。

もし `BuildWithClosedStatementAsync` のセットアップで既存テストが影響を受けて FAIL する場合、簡易セットアップにフォールバック（VM の `Statements` に直接 `WageStatementDto` を `Add` する）して報告。

- [ ] **Step 2.5: ソリューション全体テスト**

```bash
dotnet test
```

Expected: 全 PASS（466 baseline + 4 新規 = 470）。

- [ ] **Step 2.6: コミット**

```bash
git add src/Tsumugi.App/ViewModels/WageStatementViewModel.cs \
        tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs
git commit -m "feat(phase2): M-2 wire PDF save RelayCommands into WageStatementViewModel"
```

---

### Task 3: View に DataGrid SelectedItem + ボタン 2 つ追加 + acceptance doc 更新

**Files:**
- Modify: `src/Tsumugi.App/Views/WageStatementView.axaml:22-43`
- Modify: `docs/phase2-acceptance.md:39`

**Interfaces:**
- Consumes: 既存 VM の `SelectedStatement` プロパティ、`SaveSelectedStatementPdfCommand` / `SavePaymentListPdfCommand`
- Produces: UI から PDF 保存が到達可能になる。

- [ ] **Step 3.1: XAML を修正**

`src/Tsumugi.App/Views/WageStatementView.axaml` の以下 2 箇所を修正:

(a) ボタン行（`<StackPanel DockPanel.Dock="Top" Orientation="Horizontal" ...>` 内）に保存ボタン 2 つ追加。差し替え対象:

```xml
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="8,0,8,8">
            <TextBlock Text="年" VerticalAlignment="Center" />
            <NumericUpDown Value="{Binding Year}" Minimum="1900" Maximum="9999" Width="100" FormatString="0" />
            <TextBlock Text="月" VerticalAlignment="Center" />
            <NumericUpDown Value="{Binding Month}" Minimum="1" Maximum="12" Width="80" FormatString="0" />
            <Button Content="再読込" Command="{Binding RefreshCommand}" />
            <Button Content="確定" Command="{Binding CloseCommand}" />
        </StackPanel>
```

新しい内容（保存ボタン 2 つを末尾に追加）:

```xml
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="8,0,8,8">
            <TextBlock Text="年" VerticalAlignment="Center" />
            <NumericUpDown Value="{Binding Year}" Minimum="1900" Maximum="9999" Width="100" FormatString="0" />
            <TextBlock Text="月" VerticalAlignment="Center" />
            <NumericUpDown Value="{Binding Month}" Minimum="1" Maximum="12" Width="80" FormatString="0" />
            <Button Content="再読込" Command="{Binding RefreshCommand}" />
            <Button Content="確定" Command="{Binding CloseCommand}" />
            <Button Content="選択明細を PDF 保存" Command="{Binding SaveSelectedStatementPdfCommand}" />
            <Button Content="支払一覧を PDF 保存" Command="{Binding SavePaymentListPdfCommand}" />
        </StackPanel>
```

(b) `<DataGrid>` に `SelectedItem` バインドを追加。差し替え対象:

```xml
        <DataGrid ItemsSource="{Binding Statements}" Margin="8" AutoGenerateColumns="False" IsReadOnly="True">
```

新しい内容:

```xml
        <DataGrid ItemsSource="{Binding Statements}"
                  SelectedItem="{Binding SelectedStatement, Mode=TwoWay}"
                  Margin="8" AutoGenerateColumns="False" IsReadOnly="True">
```

- [ ] **Step 3.2: `phase2-acceptance.md` の deferred 表から SaveFileDialog エントリを除去**

`docs/phase2-acceptance.md:39` の以下の行を削除（または「2026-06-29 完了」と記述して残す方針なら更新）:

差し替え対象:
```markdown
| **UI** | WageStatementView の SaveFileDialog 配線 (ViewModel は byte[] を返すまで実装済) | progress.md F6 deferred |
```

削除し、その下の「Phase 1 final review からの繰越 deferred」セクションには影響させない。

（または「✅ 2026-06-29 M-2 で完了 (commit {本タスクの SHA})」と注記する案もあるが、deferred 表は未完了項目を列挙する意図なので **削除** が正解。完了ログは ledger と commit history に残る。）

- [ ] **Step 3.3: ビルドと smoke 確認**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test
```

Expected: 全 PASS。XAML はビルド時 markup 構文チェックが入るが、見た目の妥当性は手動 QA（既存制約: open-questions Avalonia GUI 目視確認）。

- [ ] **Step 3.4: コミット**

```bash
git add src/Tsumugi.App/Views/WageStatementView.axaml \
        docs/phase2-acceptance.md
git commit -m "feat(phase2): M-2 expose PDF save commands in WageStatementView; close deferred"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- Codex M-2「保存/印刷コマンドとView側の保存ダイアログ配線を入れる」→ Task 1-3 でフル実装。
- AC2-7 が真に ✅ になる（VM/View/Service の 3 層で疎通）。
- `phase2-acceptance.md:39` deferred 解消。

**2. Placeholder scan:** TBD/TODO/「適切なエラー処理」等なし。すべて具体コード提示。

**3. Type consistency:**
- `IFileSaveService.SaveAsync(byte[], string, string, string, CancellationToken) → Task<bool>` — Task 1/2/テスト fake で完全一致。
- `WageStatementViewModel` の primary constructor — 6 引数最終形（Task 2 で 5→6）。`Build` テストヘルパも同期。
- `WageStatementDto`, `RecipientDto`, `OfficeDto` のシグネチャは触らない。
- Avalonia 11 `FilePickerSaveOptions`, `IStorageProvider.SaveFilePickerAsync`, `IStorageFile.TryGetLocalPath()` は公式 API。

**4. 影響範囲:**
- App 層のみ。Domain/Application/Infrastructure 不変。
- 既存 PDF 関連テスト（`WageStatementPdfGeneratorTests` 等）は不変。
- 既存 `WageStatementViewModelTests` は `Build` ヘルパ更新で 1 引数追加されるが、本質ロジック不変。

**5. リスク:**
- `BuildWithClosedStatementAsync` の既存テストインフラとの整合性: 既存 `CloseAsync_persists_statements_and_refreshes_list` で動くなら同形で再利用可。複雑であれば VM の `Statements.Add` 直接呼び出しの簡易セットアップにフォールバック（Step 2.3 で明記済み）。
- Avalonia の `TopLevel.GetTopLevel(MainWindow)` が `null` を返す稀有ケース（テスト実行時など）は実装で `InvalidOperationException` を投げて呼び出し側がエラー表示する設計。VM テストは fake サービスを使うのでこの経路は実行されない。
- 手動 QA: macOS/Windows での実際の SaveFilePicker 表示・書き込みは VM テスト範囲外。open-questions 既存事項として温存。
