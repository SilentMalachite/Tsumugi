# H-3: 工賃系 VM への事業所選択 UI 配線 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 2 の 3 つの工賃系 VM (`WageFundSettingsViewModel` / `WageCalculationViewModel` / `WageStatementViewModel`) と対応する View に「事業所選択 ComboBox」を追加し、実アプリから `OfficeId`（および `WageStatement` では `Office`）を設定できるようにする。Codex H-3 を解消する。

**Architecture:**
既存パターン `OfficeCapabilityViewModel` を踏襲する（実績あり、テストも完備）:
1. VM が `ListOfficesUseCase` を DI で受け取り、`ObservableCollection<OfficeDto> Offices` を保持。
2. `OfficeDto? SelectedOffice` プロパティと `partial void OnSelectedOfficeChanged` で `OfficeId` を自動更新（`WageStatementViewModel` では `Office` も同時更新）。
3. `Task InitializeAsync(CancellationToken)` を公開し、View の `Loaded` から呼び出す（既存 OfficeCapability と同じ）。
4. View XAML に ComboBox を追加し `Offices` をバインド、`SelectedItem` を `SelectedOffice` にバインド。OfficeCapability の DataTemplate (`<TextBlock Text="{Binding Name}"/>`) を流用。

3 VM とも独立した選択状態を持つ（タブをまたぐ共有はしない）。これは現状の OfficeCapability と一貫しており、共有 selector の導入は本タスクのスコープ外（必要なら将来 ADR で別途検討）。

**Tech Stack:** .NET 10 / C# / Avalonia 11 / CommunityToolkit.Mvvm / xUnit / FluentAssertions

## Global Constraints

- 依存方向: `App → Application → Domain`。本タスクは `App` (VM + XAML) と `App.Tests` のみ変更。Domain/Infrastructure は触らない。
- DI: 新規依存 `ListOfficesUseCase` は既に `CompositionRoot.AddTsumugiServices` で `AddScoped` 登録済み（`src/Tsumugi.App/CompositionRoot.cs:36`）。VM 側の primary constructor に追加するだけで配線可能、CompositionRoot 変更不要。
- 既存 VM のテストは「`OfficeId` を直接代入」で書かれている (`vm.OfficeId = Office`)。本変更後もこの直接代入はそのまま機能（`OfficeId` は依然 `ObservableProperty`）。既存テストの破壊なし。
- 新規テストとして「ComboBox 選択 → OfficeId 更新」「InitializeAsync で Offices ロード」を追加し、新パスを正面から検証する。
- `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`、警告ゼロ。
- 各 commit 前に `dotnet format --verify-no-changes` 緑、`dotnet build -warnaserror` 緑、`dotnet test` 全緑。
- Avalonia XAML 変更は自動テストでは検証できない（既知制約: `docs/open-questions.md` の "Avalonia GUI 目視確認 (AC1-8 補完)"）。本計画では XAML の構文整合性はビルドで保証し、見た目の妥当性は将来の手動 QA に委ねる旨を report に明記する。
- コミットメッセージは `phase2` + `H-3` を含む。1 コミット=1 VM の VM + View + Tests を atomic に。

## File Structure

タスクごとに以下のセットを変更（各タスクは独立、依存関係なし）:

| Task | VM | View | Tests |
|---|---|---|---|
| 1 | `src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs` | `src/Tsumugi.App/Views/WageFundSettingsView.axaml` | `tests/Tsumugi.App.Tests/WageFundSettingsViewModelTests.cs` |
| 2 | `src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs` | `src/Tsumugi.App/Views/WageCalculationView.axaml` | `tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs` |
| 3 | `src/Tsumugi.App/ViewModels/WageStatementViewModel.cs` | `src/Tsumugi.App/Views/WageStatementView.axaml` | `tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs` |

## 仕様根拠

- Codex H-3: "工賃系タブから `OfficeId` を設定できず、実アプリで原資保存・計算・確定へ進めません。修正方針: `OfficeCapabilityViewModel` 同様に `ListOfficesUseCase` で事業所選択を持たせ、選択結果を各工賃VMの `OfficeId` / `Office` に明示的に渡す。"
- 既存パターン: `src/Tsumugi.App/ViewModels/OfficeCapabilityViewModel.cs:14-79`、`src/Tsumugi.App/Views/OfficeCapabilityView.axaml:14-26`
- 既存テストパターン: `tests/Tsumugi.App.Tests/OfficeCapabilityViewModelTests.cs:24-37` (`InitializeAsync_loads_offices_for_selection`), `:39-58` (`SaveCommand_registers_capability_for_selected_office`)

## 参考: OfficeCapabilityViewModel パターン（既存・正本）

```csharp
public sealed partial class OfficeCapabilityViewModel(
    RegisterOfficeCapabilityUseCase registerUseCase,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    // ...

    public ObservableCollection<OfficeDto> Offices { get; } = new();

    partial void OnSelectedOfficeChanged(OfficeDto? value)
        => OfficeId = value?.Id ?? Guid.Empty;

    public Task InitializeAsync(CancellationToken ct = default) => LoadOfficesAsync(ct);

    public async Task LoadOfficesAsync(CancellationToken ct = default)
    {
        var list = await listOfficesUseCase.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in list) Offices.Add(o);
    }
}
```

XAML 側（既存・正本、`OfficeCapabilityView.axaml:15-25`）:
```xml
<StackPanel Spacing="4">
    <TextBlock Text="事業所（必須）" />
    <ComboBox ItemsSource="{Binding Offices}"
              SelectedItem="{Binding SelectedOffice}"
              PlaceholderText="事業所を選択"
              HorizontalAlignment="Stretch">
        <ComboBox.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Name}" />
            </DataTemplate>
        </ComboBox.ItemTemplate>
    </ComboBox>
</StackPanel>
```

各 View に対しては「画面冒頭の見出し直後・既存の年/月コントロール行の直前」に配置する。

---

### Task 1: `WageFundSettingsViewModel` — 事業所選択配線

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs:13-65`
- Modify: `src/Tsumugi.App/Views/WageFundSettingsView.axaml`
- Modify: `tests/Tsumugi.App.Tests/WageFundSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: 既存 `SetWageFundUseCase`, `ConfigureWageSettingsUseCase`, 新規 `ListOfficesUseCase`
- Produces: 公開 API として `Offices` (ObservableCollection<OfficeDto>), `SelectedOffice` (OfficeDto?), `InitializeAsync(CancellationToken)` (Task), `LoadOfficesAsync(CancellationToken)` (Task) を追加。`OfficeId` は既存通り（`SelectedOffice` 設定で自動更新）。

- [ ] **Step 1.1: VM 修正 — primary constructor に `ListOfficesUseCase` 追加**

`src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs` の class 宣言とフィールドを以下に修正：

差し替え対象（現状）:
```csharp
public sealed partial class WageFundSettingsViewModel(
    SetWageFundUseCase setFund,
    ConfigureWageSettingsUseCase configureSettings) : ViewModelBase
{
    [ObservableProperty] private Guid _officeId;
```

新しい宣言:
```csharp
public sealed partial class WageFundSettingsViewModel(
    SetWageFundUseCase setFund,
    ConfigureWageSettingsUseCase configureSettings,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
```

クラスの末尾（`SaveSettingsAsync` メソッドの直後、クラス閉じ `}` の直前）に挿入：

```csharp

    public ObservableCollection<OfficeDto> Offices { get; } = new();

    partial void OnSelectedOfficeChanged(OfficeDto? value)
        => OfficeId = value?.Id ?? Guid.Empty;

    /// <summary>View の Loaded から呼ばれる初期化フック。事業所一覧を読み込む。</summary>
    public Task InitializeAsync(CancellationToken ct = default) => LoadOfficesAsync(ct);

    public async Task LoadOfficesAsync(CancellationToken ct = default)
    {
        var list = await listOfficesUseCase.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in list) Offices.Add(o);
    }
```

ファイル先頭の `using` ディレクティブに、不足していれば追加：
```csharp
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Office;
```

（OfficeCapabilityViewModel.cs の using セクションを参照して同等の構成にする。）

- [ ] **Step 1.2: View XAML 修正 — ComboBox を配置**

`src/Tsumugi.App/Views/WageFundSettingsView.axaml` の `<StackPanel Margin="8" Spacing="8">` 内、`<TextBlock Text="工賃原資と設定" ...>` の **直後**（次の `<TextBlock Text="月次原資（WageFund）"` の **直前**）に挿入：

```xml
            <StackPanel Spacing="4">
                <TextBlock Text="事業所（必須）" />
                <ComboBox ItemsSource="{Binding Offices}"
                          SelectedItem="{Binding SelectedOffice}"
                          PlaceholderText="事業所を選択"
                          HorizontalAlignment="Stretch">
                    <ComboBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Name}" />
                        </DataTemplate>
                    </ComboBox.ItemTemplate>
                </ComboBox>
            </StackPanel>
```

- [ ] **Step 1.3: テスト修正 — 既存 NewVm() を更新し、選択挙動の新規テスト 2 件追加**

`tests/Tsumugi.App.Tests/WageFundSettingsViewModelTests.cs` を読み、`NewVm()` ヘルパを確認する。現状は `new WageFundSettingsViewModel(set, configure)` のような 2 引数呼び出しになっているはず（既存ファイルの内容は事前 Read で把握済み: フィールド `_funds`, `_settings`, `_uow`, `_clock`、ヘルパ `NewVm()`）。

**修正:**
1. `NewVm()` ヘルパに `ListOfficesUseCase` を渡すよう更新。Fake のオフィスリポジトリを追加（既存テストに `InMemoryOfficeRepo` パターンがあれば再利用、なければ簡易な fake を class 内に追加）。
2. 既存テスト 5 件はそのまま PASS する想定（`OfficeId` 直接代入は壊さない）。
3. 末尾に 2 件のテストを追加:

```csharp
[Fact]
public async Task InitializeAsync_loads_offices_for_selection()
{
    var o = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
        "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
    _offices.Add(o);

    var vm = NewVm();
    await vm.InitializeAsync();

    vm.Offices.Should().ContainSingle(x => x.Id == o.Id);
}

[Fact]
public void Setting_SelectedOffice_updates_OfficeId()
{
    var vm = NewVm();
    var oid = Guid.NewGuid();
    var dto = new Tsumugi.Application.Dtos.OfficeDto(
        oid, "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB,
        Tsumugi.Domain.Enums.RegionGrade.None,
        Guid.NewGuid());

    vm.SelectedOffice = dto;

    vm.OfficeId.Should().Be(oid);

    vm.SelectedOffice = null;
    vm.OfficeId.Should().Be(Guid.Empty);
}
```

`_offices` フィールドは既存 `OfficeCapabilityViewModelTests` と同じ `InMemoryOfficeRepo` を参照する：
```csharp
private readonly InMemoryOfficeRepo _offices = new();
```

`NewVm()` の更新例：
```csharp
private WageFundSettingsViewModel NewVm() => new(
    new SetWageFundUseCase(_funds, _uow, _clock),
    new ConfigureWageSettingsUseCase(_settings, _uow, _clock),
    new ListOfficesUseCase(_offices));
```

実際のテストファイル内の既存ヘルパ呼び出しに合わせて型名・引数順を調整すること。

- [ ] **Step 1.4: ビルド・フォーマット・テスト**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~WageFundSettingsViewModelTests"
```

Expected: 全 PASS（既存 5 + 新規 2 = 7 件）。Build 0 warnings、format clean。

- [ ] **Step 1.5: コミット**

```bash
git add src/Tsumugi.App/ViewModels/WageFundSettingsViewModel.cs \
        src/Tsumugi.App/Views/WageFundSettingsView.axaml \
        tests/Tsumugi.App.Tests/WageFundSettingsViewModelTests.cs
git commit -m "feat(phase2): H-3 wire office selection into WageFundSettings VM/View"
```

---

### Task 2: `WageCalculationViewModel` — 事業所選択配線

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs:14-52`
- Modify: `src/Tsumugi.App/Views/WageCalculationView.axaml`
- Modify: `tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs`

**Interfaces:**
- Consumes: 既存 `CalculateWagesUseCase`, 新規 `ListOfficesUseCase`
- Produces: Task 1 と同形 (`Offices`, `SelectedOffice`, `InitializeAsync`, `LoadOfficesAsync`)

- [ ] **Step 2.1: VM 修正**

`src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs` の class 宣言を以下に置換:

差し替え対象（現状）:
```csharp
public sealed partial class WageCalculationViewModel(CalculateWagesUseCase calculate) : ViewModelBase
{
    [ObservableProperty] private Guid _officeId;
```

新しい宣言:
```csharp
public sealed partial class WageCalculationViewModel(
    CalculateWagesUseCase calculate,
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
```

クラス末尾（`LoadPreviewAsync` メソッドの直後）に Task 1 と同じ 4 メンバー (`Offices` プロパティ、`OnSelectedOfficeChanged`、`InitializeAsync`、`LoadOfficesAsync`) を挿入。**Task 1 のコードと一字一句同じ**（クラス名以外）。

ファイル先頭の using ディレクティブに不足分を追加（`System.Collections.ObjectModel` / `System.Threading` / `Tsumugi.Application.Dtos` / `Tsumugi.Application.UseCases.Office`）。

- [ ] **Step 2.2: View XAML 修正**

`src/Tsumugi.App/Views/WageCalculationView.axaml` の `<DockPanel>` 内、`<TextBlock DockPanel.Dock="Top" Text="工賃計算（プレビュー）" .../>` の **直後**（次の `<StackPanel DockPanel.Dock="Top" Orientation="Horizontal" .../>` の **直前**）に挿入：

```xml
        <StackPanel DockPanel.Dock="Top" Spacing="4" Margin="8,0,8,8">
            <TextBlock Text="事業所（必須）" />
            <ComboBox ItemsSource="{Binding Offices}"
                      SelectedItem="{Binding SelectedOffice}"
                      PlaceholderText="事業所を選択"
                      HorizontalAlignment="Stretch">
                <ComboBox.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </ComboBox.ItemTemplate>
            </ComboBox>
        </StackPanel>
```

`DockPanel.Dock="Top"` を明示すること（既存パターンとの整合）。Margin は周辺の `<StackPanel Margin="8,0,8,8">` と同一にする。

- [ ] **Step 2.3: テスト修正**

`tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs` の既存テスト 3 件は `vm.OfficeId` 直接代入で動作するためそのまま PASS する想定。

**追加:**
末尾に新規テスト 2 件 (`InitializeAsync_loads_offices_for_selection` / `Setting_SelectedOffice_updates_OfficeId`) を追加。Task 1.3 の test と同じ構造、ただし VM 構築コードはこのファイルのスタイル (`var calc = new CalculateWagesUseCase(...)` を構築してから `new WageCalculationViewModel(calc, new ListOfficesUseCase(offices))` で組み立てる) に合わせる。

`InitializeAsync` テストでは新規に `InMemoryOfficeRepo` を導入するか、`OfficeCapabilityViewModelTests` と同じ fake を class 内に追加してインスタンスを使い回す。同テストクラスのスタイル（既存は `params Office[] seed` を取る独自 Fake repo を class 内 nested class として複数定義）に合わせて、`FakeOfficeRepo` を class 内 nested で追加するのが整合的：

```csharp
private sealed class FakeOfficeRepo(params Office[] seed) : IOfficeRepository
{
    private readonly List<Office> _items = seed.ToList();
    public Task AddAsync(Office o, CancellationToken ct) { _items.Add(o); return Task.CompletedTask; }
    public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct)
        => Task.FromResult<Office?>(_items.FirstOrDefault(o => o.Id == id));
    public Task UpdateAsync(Office o, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Office>>(_items.ToList());
}
```

注意: `IOfficeRepository` の正確なシグネチャは `src/Tsumugi.Application/Abstractions/IOfficeRepository.cs` を参照。差異があればそれに合わせる（事前 Read 必須）。

追加テスト本体:
```csharp
[Fact]
public async Task InitializeAsync_loads_offices_for_selection()
{
    var o = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
        "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
    var calc = new CalculateWagesUseCase(
        new FakeDailyRepo(), new FakeWorkRepo(), new FakeFundRepo(),
        new FakeSettingsRepo(), new FakeContractRepo(), new FakeRecipientRepo(),
        AllStrategies);
    var vm = new WageCalculationViewModel(calc, new ListOfficesUseCase(new FakeOfficeRepo(o)));

    await vm.InitializeAsync();

    vm.Offices.Should().ContainSingle(x => x.Id == o.Id);
}

[Fact]
public void Setting_SelectedOffice_updates_OfficeId()
{
    var calc = new CalculateWagesUseCase(
        new FakeDailyRepo(), new FakeWorkRepo(), new FakeFundRepo(),
        new FakeSettingsRepo(), new FakeContractRepo(), new FakeRecipientRepo(),
        AllStrategies);
    var vm = new WageCalculationViewModel(calc, new ListOfficesUseCase(new FakeOfficeRepo()));
    var oid = Guid.NewGuid();
    var dto = new Tsumugi.Application.Dtos.OfficeDto(
        oid, "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB,
        Tsumugi.Domain.Enums.RegionGrade.None,
        Guid.NewGuid());

    vm.SelectedOffice = dto;

    vm.OfficeId.Should().Be(oid);
    vm.SelectedOffice = null;
    vm.OfficeId.Should().Be(Guid.Empty);
}
```

既存 3 件のテスト (`LoadPreviewAsync_with_empty_office_id_sets_error`, `..._with_missing_settings_sets_error_from_usecase`, `LoadPreviewAsync_populates_lines_and_summary_on_success`) は VM 構築呼び出しを `new WageCalculationViewModel(calc, new ListOfficesUseCase(new FakeOfficeRepo()))` に更新する。

- [ ] **Step 2.4: ビルド・フォーマット・テスト**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~WageCalculationViewModelTests"
```

Expected: 既存 3 (更新) + 新規 2 = 5/5 PASS。

- [ ] **Step 2.5: コミット**

```bash
git add src/Tsumugi.App/ViewModels/WageCalculationViewModel.cs \
        src/Tsumugi.App/Views/WageCalculationView.axaml \
        tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs
git commit -m "feat(phase2): H-3 wire office selection into WageCalculation VM/View"
```

---

### Task 3: `WageStatementViewModel` — 事業所選択配線（`Office` も同時設定）

**Files:**
- Modify: `src/Tsumugi.App/ViewModels/WageStatementViewModel.cs:16-94`
- Modify: `src/Tsumugi.App/Views/WageStatementView.axaml`
- Modify: `tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs`

**Interfaces:**
- Consumes: 既存 `CloseWagesUseCase`, `QueryWageStatementUseCase`, `ListRecipientsUseCase`, `IWageReportGenerator`, 新規 `ListOfficesUseCase`
- Produces: Task 1/2 と同形だが `OnSelectedOfficeChanged` で `OfficeId` **と** 既存 `Office` (`OfficeDto?`) を両方更新（PDF 生成に必須）。

**事前メモ:**
既存 `_office` プロパティは PDF 生成 (`GenerateStatementPdf`, `GeneratePaymentListPdf`) で参照されているが、これまで外部から手動代入する API しかなかった。本タスク後は `SelectedOffice` 経由で自動同期される。

- [ ] **Step 3.1: VM 修正**

`src/Tsumugi.App/ViewModels/WageStatementViewModel.cs` の primary constructor 引数を以下に置換:

差し替え対象（現状）:
```csharp
public sealed partial class WageStatementViewModel(
    CloseWagesUseCase close,
    QueryWageStatementUseCase query,
    ListRecipientsUseCase listRecipients,
    IWageReportGenerator reportGenerator) : ViewModelBase
{
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
    ListOfficesUseCase listOfficesUseCase) : ViewModelBase
{
    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private OfficeDto? _office;
```

クラスの末尾（`GeneratePaymentListPdf` メソッドの直後、クラス閉じ `}` の直前）に挿入：

```csharp

    public ObservableCollection<OfficeDto> Offices { get; } = new();

    partial void OnSelectedOfficeChanged(OfficeDto? value)
    {
        OfficeId = value?.Id ?? Guid.Empty;
        Office = value;
    }

    public Task InitializeAsync(CancellationToken ct = default) => LoadOfficesAsync(ct);

    public async Task LoadOfficesAsync(CancellationToken ct = default)
    {
        var list = await listOfficesUseCase.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in list) Offices.Add(o);
    }
```

**重要**: `OnSelectedOfficeChanged` は `OfficeId` と `Office` の **両方** を更新する。Task 1/2 と 1 行違うので注意。

using 追加分は Task 1/2 と同様。

- [ ] **Step 3.2: View XAML 修正**

`src/Tsumugi.App/Views/WageStatementView.axaml` の `<DockPanel>` 内、`<TextBlock DockPanel.Dock="Top" Text="工賃確定" .../>` の **直後**（次の `<StackPanel DockPanel.Dock="Top" Orientation="Horizontal" .../>` の **直前**）に挿入：

```xml
        <StackPanel DockPanel.Dock="Top" Spacing="4" Margin="8,0,8,8">
            <TextBlock Text="事業所（必須）" />
            <ComboBox ItemsSource="{Binding Offices}"
                      SelectedItem="{Binding SelectedOffice}"
                      PlaceholderText="事業所を選択"
                      HorizontalAlignment="Stretch">
                <ComboBox.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding Name}" />
                    </DataTemplate>
                </ComboBox.ItemTemplate>
            </ComboBox>
        </StackPanel>
```

Task 2 と完全に同じスニペット（DataContext が異なるだけ）。

- [ ] **Step 3.3: テスト修正**

`tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs` の `Build` ヘルパ (`body_location` 46-73 行) を変更し、`new ListOfficesUseCase(...)` を追加する。

既存テスト 4 件 (`RefreshAsync_with_empty_office_id_sets_error`, `CloseAsync_persists_statements_and_refreshes_list`, `GenerateStatementPdf_returns_bytes_after_close`, `GenerateStatementPdf_without_office_sets_error`) は `vm.OfficeId` および `vm.Office` 直接代入で動作するためそのまま PASS する想定。`Build` ヘルパに `ListOfficesUseCase` を渡すパラメタを追加するだけ。

末尾に新規テスト 3 件を追加:

```csharp
[Fact]
public async Task InitializeAsync_loads_offices_for_selection()
{
    var o = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
        "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
    var vm = Build(offices: new[] { o });

    await vm.InitializeAsync();

    vm.Offices.Should().ContainSingle(x => x.Id == o.Id);
}

[Fact]
public void Setting_SelectedOffice_updates_OfficeId_and_Office()
{
    var vm = Build();
    var oid = Guid.NewGuid();
    var dto = new Tsumugi.Application.Dtos.OfficeDto(
        oid, "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB,
        Tsumugi.Domain.Enums.RegionGrade.None,
        Guid.NewGuid());

    vm.SelectedOffice = dto;

    vm.OfficeId.Should().Be(oid);
    vm.Office.Should().Be(dto);
}

[Fact]
public void Clearing_SelectedOffice_resets_OfficeId_and_Office()
{
    var vm = Build();
    var dto = new Tsumugi.Application.Dtos.OfficeDto(
        Guid.NewGuid(), "1234567890", "事業所",
        Tsumugi.Domain.Enums.ServiceCategory.TypeB,
        Tsumugi.Domain.Enums.RegionGrade.None,
        Guid.NewGuid());
    vm.SelectedOffice = dto;

    vm.SelectedOffice = null;

    vm.OfficeId.Should().Be(Guid.Empty);
    vm.Office.Should().BeNull();
}
```

`Build` ヘルパは optional `offices` パラメタを受け付けるよう変更：
```csharp
private WageStatementViewModel Build(
    Tsumugi.Domain.Entities.Office[]? offices = null,
    /* 既存パラメタ */)
{
    // ... 既存セットアップ
    var officeRepo = new FakeOfficeRepo(offices ?? Array.Empty<Tsumugi.Domain.Entities.Office>());
    return new WageStatementViewModel(close, query, listRecipients, reportGen,
        new ListOfficesUseCase(officeRepo));
}
```

`FakeOfficeRepo` は Task 2 の class 内 nested fake と同じ実装を本ファイルにも追加（`tests/Tsumugi.App.Tests/WageCalculationViewModelTests.cs` 内に追加された後にコピーして使う、または共通ヘルパに切り出すかは Task 3 の判断）。本タスクでは「同じ実装をコピー」を採用（既存テストが各ファイル内に nested fake を持つパターンと一貫）。

- [ ] **Step 3.4: ビルド・フォーマット・テスト**

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test tests/Tsumugi.App.Tests --filter "FullyQualifiedName~WageStatementViewModelTests"
```

Expected: 既存 4 + 新規 3 = 7/7 PASS。

- [ ] **Step 3.5: ソリューション全体テスト**

3 タスクが完了した時点で全 VM が変更済み。回帰がないことをソリューション全体で確認。

```bash
dotnet test
```

Expected: 既存テスト数 + 7 件新規 (Task 1:2, Task 2:2, Task 3:3) で全 PASS。

- [ ] **Step 3.6: コミット**

```bash
git add src/Tsumugi.App/ViewModels/WageStatementViewModel.cs \
        src/Tsumugi.App/Views/WageStatementView.axaml \
        tests/Tsumugi.App.Tests/WageStatementViewModelTests.cs
git commit -m "feat(phase2): H-3 wire office selection into WageStatement VM/View"
```

---

## Self-Review チェック結果

**1. Spec coverage:**
- Codex H-3「工賃系タブから `OfficeId` を設定できず…修正方針: `OfficeCapabilityViewModel` 同様に `ListOfficesUseCase` で事業所選択を持たせ、選択結果を各工賃VMの `OfficeId` / `Office` に明示的に渡す」→ Task 1-3 で 3 つすべての工賃 VM に同じパターンを配線。`WageStatement` では `Office` も同時設定。
- 既存テスト方式 (`vm.OfficeId = ...` 直接代入) は壊さない設計のため、Phase 2 の既存テストの破壊なし。
- AC2-6 (PDF 生成)・AC2-5 (計算プレビュー) のいずれも実 UI 経由で `OfficeId` を入れられるようになる。

**2. Placeholder scan:** TBD/TODO/「適切なエラー処理」等なし。各テスト・XAML スニペットは具体的に提示。

**3. Type consistency:**
- `ListOfficesUseCase.ExecuteAsync(CancellationToken)` → `Task<IReadOnlyList<OfficeDto>>` — 既存（`src/Tsumugi.Application/UseCases/Office/ListOfficesUseCase.cs:7-12`）と整合。
- `OfficeDto(Guid Id, string OfficeNumber, string Name, ServiceCategory ServiceCategory, RegionGrade RegionGrade, Guid ConcurrencyToken)` — Task 1/2/3 の DTO 生成と整合（`src/Tsumugi.Application/Dtos/OfficeDto.cs:8-13`）。
- `Office.Create(Guid id, string officeNumber, string name, ServiceCategory cat, RegionGrade grade, string actor, DateTimeOffset createdAt, Guid token)` — 既存 `OfficeCapabilityViewModelTests:25-27` の呼び出しと同形。
- 全 3 VM の primary constructor が `partial` を維持 (`sealed partial class`) — CommunityToolkit.Mvvm の `[ObservableProperty]` ソースジェネレータが既存通り動作する。
- `OnSelectedOfficeChanged(OfficeDto? value)` partial method — Task 3 のみ `Office` も更新する点が Task 1/2 と異なる。明示済み。

**4. 影響範囲:**
- Production code 3 VM + 3 XAML、Tests 3 ファイル。Domain/Infrastructure 不変。
- DI 変更不要（`ListOfficesUseCase` は既登録）。
- Avalonia XAML 構文整合はビルドで検証されるが、見た目（レイアウト・操作性）は手動 QA。これは既存制約 (`docs/open-questions.md` Avalonia GUI 目視確認) と同じ。

**5. リスク:**
- 共有事業所選択（タブ間で SelectedOffice を共有）は本タスクでは導入しない。各タブで個別選択は OfficeCapability と一貫だが、ユーザ視点では再選択の手間が出る。これは UX 改善余地として `docs/open-questions.md` に追加する案を Task 3 完了後の report で提案できる（必須ではない）。
